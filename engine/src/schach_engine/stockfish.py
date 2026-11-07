"""Stockfish und Fairy-Stockfish ueber python-chess `chess.engine` (SCHACH-SPEC 5.2).

`UciPool` haelt bis zu `groesse` UCI-Prozesse. Wer keinen freien Prozess bekommt, wartet hoechstens
`wartegrenze_s` (3 s) und bekommt dann 503 `pool_busy`. Ein abgestuerzter Prozess wird verworfen und
beim naechsten Zugriff neu gestartet.
"""

import logging
import queue
import threading
from collections.abc import Callable, Iterator, Sequence
from contextlib import contextmanager
from typing import Any

import chess
import chess.engine

from schach_engine.fehler import EngineFehler

log = logging.getLogger("schach_engine")

# Fehler, nach denen ein UCI-Prozess nicht mehr vertrauenswuerdig ist.
_PROZESS_FEHLER = (chess.engine.EngineError, chess.engine.EngineTerminatedError, TimeoutError, OSError)


def uci_starter(befehl: str | Sequence[str], optionen: dict[str, Any]) -> Callable[[], chess.engine.SimpleEngine]:
    """Fabrik fuer einen UCI-Prozess. Setzt nur Optionen, die die Engine meldet."""

    def starten() -> chess.engine.SimpleEngine:
        engine = chess.engine.SimpleEngine.popen_uci(list(befehl) if not isinstance(befehl, str) else befehl)
        try:
            engine.configure({name: wert for name, wert in optionen.items() if name in engine.options})
            engine.ping()  # `isready`
        except BaseException:
            engine.close()
            raise
        return engine

    return starten


class UciPool:
    def __init__(
        self,
        name: str,
        starter: Callable[[], chess.engine.SimpleEngine],
        groesse: int,
        wartegrenze_s: float,
    ) -> None:
        self._name = name
        self._starter = starter
        self._groesse = max(1, groesse)
        self._wartegrenze_s = wartegrenze_s
        self._frei: queue.LifoQueue[chess.engine.SimpleEngine] = queue.LifoQueue()
        self._lock = threading.Lock()
        self._gestartet = 0

    @property
    def gestartet(self) -> int:
        return self._gestartet

    def starten(self) -> None:
        """Startet alle Prozesse sofort und wartet auf `readyok` (fuer `/healthz`)."""
        while self._reserviere_platz():
            self._frei.put(self._neuer_prozess())

    @contextmanager
    def engine(self) -> Iterator[chess.engine.SimpleEngine]:
        if self._frei.empty() and self._reserviere_platz():
            engine = self._neuer_prozess()
        else:
            try:
                engine = self._frei.get(timeout=self._wartegrenze_s)
            except queue.Empty:
                raise EngineFehler(
                    503, "pool_busy", f"{self._name} pool busy ({self._groesse}) after {self._wartegrenze_s:g} s"
                ) from None
        try:
            yield engine
        except _PROZESS_FEHLER as fehler:
            self._verwerfen(engine)
            log.warning("%s process failed and was discarded: %s", self._name, type(fehler).__name__)
            raise EngineFehler(503, "pool_busy", f"{self._name} process failed, restarting") from None
        except BaseException:
            self._frei.put(engine)
            raise
        else:
            self._frei.put(engine)

    def schliessen(self) -> None:
        while True:
            try:
                engine = self._frei.get_nowait()
            except queue.Empty:
                return
            try:
                engine.quit()
            except _PROZESS_FEHLER:
                engine.close()

    def _reserviere_platz(self) -> bool:
        with self._lock:
            if self._gestartet >= self._groesse:
                return False
            self._gestartet += 1
            return True

    def _neuer_prozess(self) -> chess.engine.SimpleEngine:
        try:
            return self._starter()
        except BaseException:
            with self._lock:
                self._gestartet -= 1
            raise

    def _verwerfen(self, engine: chess.engine.SimpleEngine) -> None:
        try:
            engine.close()
        finally:
            with self._lock:
                self._gestartet -= 1


def _wdl(wdl: chess.engine.Wdl) -> dict[str, float]:
    gesamt = wdl.total() or 1
    return {"w": wdl.wins / gesamt, "d": wdl.draws / gesamt, "l": wdl.losses / gesamt}


class StockfishAnalyse:
    """`/analyse`: Stockfish in voller Staerke, `movetime_ms` gekappt auf `SF_MAX_MOVETIME_MS`."""

    def __init__(self, pool: UciPool, max_movetime_ms: int) -> None:
        self._pool = pool
        self._max_movetime_ms = max_movetime_ms

    def starten(self) -> None:
        self._pool.starten()

    def schliessen(self) -> None:
        self._pool.schliessen()

    def analyse(self, board: chess.Board, movetime_ms: int, multipv: int) -> dict[str, object]:
        limit = chess.engine.Limit(time=min(movetime_ms, self._max_movetime_ms) / 1000)
        with self._pool.engine() as engine:
            infos = engine.analyse(board, limit, multipv=multipv)
        zeilen = []
        for info in infos:
            if not info.get("pv"):
                continue
            wertung = info["score"].relative
            mate = wertung.mate()
            wdl = info["wdl"].relative if "wdl" in info else wertung.wdl(model="sf", ply=board.ply())
            zeilen.append(
                {
                    "uci": info["pv"][0].uci(),
                    "cp": None if mate is not None else wertung.score(),
                    "mate": mate,
                    "wdl": _wdl(wdl),
                }
            )
        if not zeilen:
            raise EngineFehler(503, "pool_busy", "stockfish returned no line")
        return {"bestmove": zeilen[0]["uci"], "lines": zeilen}


class FairyZug:
    """`/move` mit `engine: "fairy"`: Fairy-Stockfish mit `UCI_LimitStrength` und `UCI_Elo`."""

    def __init__(self, pool: UciPool, movetime_ms: int) -> None:
        self._pool = pool
        self._movetime_ms = movetime_ms

    def starten(self) -> None:
        """Fairy-Stockfish startet lazy (SCHACH-SPEC 5.2) und zaehlt nicht fuer `/healthz`."""

    def schliessen(self) -> None:
        self._pool.schliessen()

    def bestmove(self, board: chess.Board, skill: int) -> str:
        with self._pool.engine() as engine:
            option = engine.options.get("UCI_Elo")
            elo = skill if option is None else max(option.min or skill, min(option.max or skill, skill))
            engine.configure({"UCI_LimitStrength": True, "UCI_Elo": elo})
            ergebnis = engine.play(board, chess.engine.Limit(time=self._movetime_ms / 1000))
        if ergebnis.move is None:
            raise EngineFehler(503, "pool_busy", "fairy-stockfish returned no move")
        return ergebnis.move.uci()
