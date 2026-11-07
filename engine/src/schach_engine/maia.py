"""Maia-Kerne: echtes Maia3-5M (PyTorch) oder die Fixture fuer Tests (`SCHACH_ENGINE_FAKE_MODEL=1`)."""

import logging
from dataclasses import dataclass
from pathlib import Path
from typing import Protocol

import chess

from schach_engine import fixture
from schach_engine.modell_download import ModellQuelle, lade_modell

log = logging.getLogger("schach_engine")


@dataclass(frozen=True)
class MaiaErgebnis:
    """`policy` absteigend nach `p` ueber alle erlaubten Zuege, `wdl` aus Sicht der Seite am Zug."""

    policy: list[dict[str, object]]
    wdl: dict[str, float]


class MaiaKern(Protocol):
    def starten(self) -> None:
        """Blockiert, bis das Modell geladen ist."""

    def schliessen(self) -> None: ...

    def bewerte(self, board: chess.Board, self_elo: int, oppo_elo: int) -> MaiaErgebnis: ...


def sortierte_policy(paare: list[tuple[str, float]]) -> list[dict[str, object]]:
    """Normalisiert auf Summe 1 und sortiert absteigend nach `p`, bei Gleichstand nach UCI."""
    summe = sum(p for _, p in paare) or 1.0
    return [{"uci": uci, "p": p / summe} for uci, p in sorted(paare, key=lambda e: (-e[1], e[0]))]


class FakeMaia:
    """Fixture-Policy ohne Modell (SCHACH-SPEC 15.2)."""

    def starten(self) -> None:
        pass

    def schliessen(self) -> None:
        pass

    def bewerte(self, board: chess.Board, self_elo: int, oppo_elo: int) -> MaiaErgebnis:
        return MaiaErgebnis(policy=fixture.policy(board), wdl=dict(fixture.FIXTURE_WDL))


class EchteMaia:
    """Maia3-5M auf der CPU. Laedt die Gewichte beim Start ins Volume und rechnet ohne Suche."""

    def __init__(self, models_dir: Path, torch_threads: int, quelle: ModellQuelle | None = None) -> None:
        self._models_dir = models_dir
        self._torch_threads = torch_threads
        self._quelle = quelle or ModellQuelle.maia3_5m()
        self._netz = None

    def starten(self) -> None:
        pfad = lade_modell(self._quelle, self._models_dir)
        import torch  # erst hier: Tests und Fake-Betrieb brauchen kein PyTorch

        from schach_engine.maia3.netz import Maia3Netz

        torch.set_num_threads(self._torch_threads)
        try:
            torch.set_num_interop_threads(1)
        except RuntimeError:
            pass  # schon gesetzt (zweiter Startversuch)
        self._netz = Maia3Netz(pfad)
        log.info("maia3-5m loaded (torch %s, %d thread(s))", torch.__version__, self._torch_threads)

    def schliessen(self) -> None:
        self._netz = None

    def bewerte(self, board: chess.Board, self_elo: int, oppo_elo: int) -> MaiaErgebnis:
        if self._netz is None:
            raise RuntimeError("maia model not loaded")
        paare, (w, d, l) = self._netz.bewerte(board, self_elo, oppo_elo)
        summe = w + d + l
        return MaiaErgebnis(policy=sortierte_policy(paare), wdl={"w": w / summe, "d": d / summe, "l": l / summe})
