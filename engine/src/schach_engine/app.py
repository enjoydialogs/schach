"""HTTP-Schicht des Engine-Dienstes nach engine/API.md (eingefroren ab Phase 0).

Validierung, Fehlercodes und Rollen stehen hier; gerechnet wird in den Kernen (`kerne.py`):
Maia-3 hinter einer Inferenz-Warteschlange, Stockfish und Fairy-Stockfish in UCI-Pools. Mit
`SCHACH_ENGINE_FAKE_MODEL=1` antworten alle Kerne aus der Fixture (pytest, SCHACH-SPEC 15.2).
"""

import logging
import threading
from collections.abc import AsyncIterator
from contextlib import asynccontextmanager
from typing import Literal

import chess
from fastapi import FastAPI, Request
from fastapi.exceptions import RequestValidationError
from fastapi.responses import JSONResponse
from pydantic import BaseModel, Field

from schach_engine.einstellungen import Einstellungen
from schach_engine.fehler import EngineFehler
from schach_engine.kerne import Kerne
from schach_engine.warteschlange import InferenzWarteschlange

log = logging.getLogger("schach_engine")

# Rollen, auf die /healthz wartet. Fairy-Stockfish startet lazy und zaehlt nicht (API.md).
_HEALTH_ROLLEN = ("stockfish", "maia")
_MAX_START_PAUSE_S = 60.0

__all__ = ["EngineFehler", "create_app", "stellung"]


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


def _logging_einrichten() -> None:
    if not logging.getLogger().handlers:
        logging.basicConfig(level=logging.INFO, format="%(asctime)s %(levelname)s %(name)s %(message)s")


def create_app(einstellungen: Einstellungen | None = None, kerne: Kerne | None = None) -> FastAPI:
    e = einstellungen or Einstellungen.aus_env()
    k = kerne or Kerne.aus_einstellungen(e)
    aktive_rollen = e.rollen
    bereit = {rolle: False for rolle in _HEALTH_ROLLEN if rolle in aktive_rollen}
    zu_starten = {"stockfish": k.analyse, "maia": k.maia}
    warteschlange = InferenzWarteschlange(max_laenge=e.maia_queue_max, frist_s=e.maia_frist_s)
    stopp = threading.Event()

    def starte_kerne() -> None:
        """Startet Stockfish und Maia nacheinander; wiederholt Fehlschlaege mit wachsender Pause."""
        for rolle in bereit:
            pause = e.start_pause_s
            while not stopp.is_set():
                try:
                    zu_starten[rolle].starten()
                except Exception as fehler:  # noqa: BLE001 - Start wird wiederholt, /healthz bleibt 503
                    log.error("starting %s failed (%s: %s), retrying in %g s", rolle, type(fehler).__name__, fehler, pause)
                    stopp.wait(pause)
                    pause = min(pause * 2, _MAX_START_PAUSE_S)
                else:
                    bereit[rolle] = True
                    log.info("%s ready", rolle)
                    break

    @asynccontextmanager
    async def lifespan(app: FastAPI) -> AsyncIterator[None]:
        _logging_einrichten()
        warteschlange.starten()
        starter = threading.Thread(target=starte_kerne, name="engine-start", daemon=True)
        if bereit:
            starter.start()
        try:
            yield
        finally:
            stopp.set()
            if starter.is_alive():
                starter.join(timeout=1)
            warteschlange.stoppen()
            for kern in (k.maia, k.analyse, k.fairy):
                try:
                    kern.schliessen()
                except Exception as fehler:  # noqa: BLE001 - Herunterfahren nicht abbrechen
                    log.warning("closing %s failed: %s", type(kern).__name__, type(fehler).__name__)

    app = FastAPI(title="schach-engine", lifespan=lifespan, docs_url=None, redoc_url=None, openapi_url=None)

    def pruefe_rolle(rolle: str) -> None:
        if rolle not in aktive_rollen:
            raise EngineFehler(503, "role_disabled", f"role {rolle} not in ENGINE_ROLES")

    def pruefe_bereit(rolle: str) -> None:
        if not bereit.get(rolle, False):
            raise EngineFehler(503, "starting", f"{rolle} is starting")

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
        if not all(bereit.values()):
            return JSONResponse(status_code=503, content={"status": "starting"})
        return JSONResponse(status_code=200, content={"status": "ok"})

    @app.post("/move")
    def move(anfrage: MoveAnfrage) -> dict[str, object]:
        if anfrage.engine == "fairy" and anfrage.fairy_skill is None:
            raise EngineFehler(400, "invalid_request", "invalid fields: fairy_skill")
        pruefe_rolle(anfrage.engine)
        board = stellung(anfrage.fen, anfrage.moves)
        if anfrage.engine == "fairy":
            assert anfrage.fairy_skill is not None
            return {"engine": "fairy", "policy": None, "wdl": None, "bestmove": k.fairy.bestmove(board, anfrage.fairy_skill)}
        pruefe_bereit("maia")
        ergebnis = warteschlange.ausfuehren(lambda: k.maia.bewerte(board, anfrage.self_elo, anfrage.oppo_elo))
        return {"engine": "maia", "policy": ergebnis.policy, "wdl": ergebnis.wdl, "bestmove": None}

    @app.post("/analyse")
    def analyse(anfrage: AnalyseAnfrage) -> dict[str, object]:
        # movetime_ms ueber SF_MAX_MOVETIME_MS kappt der Kern, statt abzulehnen (API.md).
        pruefe_rolle("stockfish")
        board = stellung(anfrage.fen, anfrage.moves)
        pruefe_bereit("stockfish")
        return k.analyse.analyse(board, anfrage.movetime_ms, anfrage.multipv)

    return app


app = create_app()
