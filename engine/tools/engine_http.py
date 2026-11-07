"""Schlanker HTTP-Client fuer den Engine-Dienst (engine/API.md), nur Standardbibliothek.

`messung.py` laeuft damit in einem nackten `python:3.12-slim` (plus `pip install chess`).
"""

import json
import time
import urllib.error
import urllib.request
from dataclasses import dataclass
from typing import Any, Protocol

GRUNDSTELLUNG = "rnbqkbnr/pppppppp/8/8/8/8/PPPPPPPP/RNBQKBNR w KQkq - 0 1"


@dataclass(frozen=True)
class Antwort:
    status: int  # 0 = keine HTTP-Antwort (Timeout, Verbindung)
    daten: dict[str, Any] | None
    dauer_s: float

    @property
    def ok(self) -> bool:
        return self.status == 200

    @property
    def fehlercode(self) -> str | None:
        if self.ok:
            return None
        if self.daten and isinstance(self.daten.get("error"), str):
            return self.daten["error"]
        return "transport" if self.status == 0 else f"http_{self.status}"


class Transport(Protocol):
    def post(self, pfad: str, nutzlast: dict[str, Any]) -> Antwort: ...
    def get(self, pfad: str) -> Antwort: ...


class HttpTransport:
    def __init__(self, basis_url: str, timeout_s: float = 5.0) -> None:
        self._basis = basis_url.rstrip("/")
        self._timeout_s = timeout_s

    def post(self, pfad: str, nutzlast: dict[str, Any]) -> Antwort:
        rumpf = json.dumps(nutzlast).encode("utf-8")
        anfrage = urllib.request.Request(
            self._basis + pfad, data=rumpf, method="POST", headers={"Content-Type": "application/json"}
        )
        return self._senden(anfrage)

    def get(self, pfad: str) -> Antwort:
        return self._senden(urllib.request.Request(self._basis + pfad, method="GET"))

    def _senden(self, anfrage: urllib.request.Request) -> Antwort:
        start = time.perf_counter()
        try:
            with urllib.request.urlopen(anfrage, timeout=self._timeout_s) as antwort:
                daten = json.loads(antwort.read() or b"null")
                return Antwort(antwort.status, daten, time.perf_counter() - start)
        except urllib.error.HTTPError as fehler:
            try:
                daten = json.loads(fehler.read() or b"null")
            except ValueError:
                daten = None
            return Antwort(fehler.code, daten if isinstance(daten, dict) else None, time.perf_counter() - start)
        except (OSError, ValueError):
            return Antwort(0, None, time.perf_counter() - start)


def move_anfrage(
    moves: list[str], self_elo: int, oppo_elo: int, engine: str = "maia", fairy_skill: int | None = None, fen: str = GRUNDSTELLUNG
) -> dict[str, Any]:
    nutzlast: dict[str, Any] = {"fen": fen, "moves": moves, "self_elo": self_elo, "oppo_elo": oppo_elo, "engine": engine}
    if fairy_skill is not None:
        nutzlast["fairy_skill"] = fairy_skill
    return nutzlast


def analyse_anfrage(moves: list[str], movetime_ms: int, multipv: int, fen: str = GRUNDSTELLUNG) -> dict[str, Any]:
    return {"fen": fen, "moves": moves, "movetime_ms": movetime_ms, "multipv": multipv}
