"""HTTP-Schicht des Engine-Dienstes nach engine/API.md (eingefroren ab Phase 0).

Phase 0c: Antworten aus `fixture`. S4a tauscht die Rechenkerne gegen Maia-3, Stockfish und
Fairy-Stockfish; Validierung, Fehlercodes und Rollen bleiben hier.
"""

import os
from collections.abc import AsyncIterator
from contextlib import asynccontextmanager
from typing import Literal

import chess
from fastapi import FastAPI, Request
from fastapi.exceptions import RequestValidationError
from fastapi.responses import JSONResponse
from pydantic import BaseModel, Field

from schach_engine import fixture

ALLE_ROLLEN = "maia,stockfish,fairy"


class EngineFehler(Exception):
    """Wird zu `{error, detail}` mit Status nach engine/API.md. `detail` nie mit Spielerdaten."""

    def __init__(self, status: int, code: str, detail: str) -> None:
        super().__init__(detail)
        self.status = status
        self.code = code
        self.detail = detail


class MoveAnfrage(BaseModel):
    fen: str
    moves: list[str]
    self_elo: int
    oppo_elo: int
    engine: Literal["maia", "fairy"]
    fairy_skill: int | None = Field(default=None, ge=500)


class AnalyseAnfrage(BaseModel):
    fen: str
    moves: list[str]
    movetime_ms: int = Field(ge=1)
    multipv: int = Field(ge=1, le=3)


def stellung(fen: str, moves: list[str]) -> chess.Board:
    """Stellung nach allen `moves`; wirft `EngineFehler` fuer FEN, Zug oder Endstellung."""
    try:
        board = chess.Board(fen)
    except ValueError:
        raise EngineFehler(400, "invalid_fen", "fen not readable") from None
    if not board.is_valid():
        raise EngineFehler(400, "invalid_fen", "fen describes no legal position")
    for halbzug, uci in enumerate(moves, start=1):
        try:
            zug = chess.Move.from_uci(uci)
        except ValueError:
            raise EngineFehler(400, "illegal_move", f"move {halbzug} is not uci") from None
        if zug not in board.legal_moves:
            raise EngineFehler(400, "illegal_move", f"move {halbzug} is illegal")
        board.push(zug)
    if not any(board.legal_moves):
        raise EngineFehler(409, "no_legal_moves", "position is checkmate or stalemate")
    return board


def rollen() -> set[str]:
    return {r.strip() for r in os.environ.get("ENGINE_ROLES", ALLE_ROLLEN).split(",") if r.strip()}


def create_app() -> FastAPI:
    aktive_rollen = rollen()

    @asynccontextmanager
    async def lifespan(app: FastAPI) -> AsyncIterator[None]:
        # /healthz meldet 503 "starting", bis das Modell geladen und Stockfish `isready` ist.
        # Die Fixture laedt nichts; S4a laedt hier Maia-3 und startet den Stockfish-Pool.
        app.state.bereit = True
        yield

    app = FastAPI(title="schach-engine", lifespan=lifespan, docs_url=None, redoc_url=None, openapi_url=None)
    app.state.bereit = False

    def pruefe_rolle(rolle: str) -> None:
        if rolle not in aktive_rollen:
            raise EngineFehler(503, "role_disabled", f"role {rolle} not in ENGINE_ROLES")

    @app.exception_handler(EngineFehler)
    async def engine_fehler(_: Request, fehler: EngineFehler) -> JSONResponse:
        return JSONResponse(status_code=fehler.status, content={"error": fehler.code, "detail": fehler.detail})

    @app.exception_handler(RequestValidationError)
    async def ungueltig(_: Request, fehler: RequestValidationError) -> JSONResponse:
        # Nur Feldpfade, nie Eingabewerte im detail.
        felder = sorted({".".join(str(t) for t in e["loc"][1:]) for e in fehler.errors()})
        return JSONResponse(
            status_code=400,
            content={"error": "invalid_request", "detail": "invalid fields: " + ", ".join(felder)},
        )

    @app.get("/healthz")
    def healthz() -> JSONResponse:
        if not app.state.bereit:
            return JSONResponse(status_code=503, content={"status": "starting"})
        return JSONResponse(status_code=200, content={"status": "ok"})

    @app.post("/move")
    def move(anfrage: MoveAnfrage) -> dict[str, object]:
        if anfrage.engine == "fairy" and anfrage.fairy_skill is None:
            raise EngineFehler(400, "invalid_request", "invalid fields: fairy_skill")
        pruefe_rolle(anfrage.engine)
        board = stellung(anfrage.fen, anfrage.moves)
        if anfrage.engine == "fairy":
            return {"engine": "fairy", "policy": None, "wdl": None, "bestmove": fixture.bestmove(board)}
        return {"engine": "maia", "policy": fixture.policy(board), "wdl": fixture.FIXTURE_WDL, "bestmove": None}

    @app.post("/analyse")
    def analyse(anfrage: AnalyseAnfrage) -> dict[str, object]:
        # movetime_ms ueber SF_MAX_MOVETIME_MS wird gekappt, nicht abgelehnt (API.md). Die
        # Fixture rechnet nicht; S4a reicht min(movetime_ms, Deckel) an Stockfish weiter.
        pruefe_rolle("stockfish")
        board = stellung(anfrage.fen, anfrage.moves)
        return fixture.analyse(board, anfrage.multipv)

    return app


app = create_app()
