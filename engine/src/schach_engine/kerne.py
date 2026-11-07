"""Die drei Rechenkerne des Dienstes: Maia (`/move` maia), Stockfish (`/analyse`), Fairy (`/move` fairy)."""

from dataclasses import dataclass
from typing import Protocol

import chess

from schach_engine import fixture
from schach_engine.einstellungen import Einstellungen
from schach_engine.maia import EchteMaia, FakeMaia, MaiaKern


class AnalyseKern(Protocol):
    def starten(self) -> None: ...
    def schliessen(self) -> None: ...
    def analyse(self, board: chess.Board, movetime_ms: int, multipv: int) -> dict[str, object]: ...


class FairyKern(Protocol):
    def starten(self) -> None: ...
    def schliessen(self) -> None: ...
    def bestmove(self, board: chess.Board, skill: int) -> str: ...


class FixtureAnalyse:
    def starten(self) -> None:
        pass

    def schliessen(self) -> None:
        pass

    def analyse(self, board: chess.Board, movetime_ms: int, multipv: int) -> dict[str, object]:
        return fixture.analyse(board, multipv)


class FixtureFairy:
    def starten(self) -> None:
        pass

    def schliessen(self) -> None:
        pass

    def bestmove(self, board: chess.Board, skill: int) -> str:
        return fixture.bestmove(board)


@dataclass
class Kerne:
    maia: MaiaKern
    analyse: AnalyseKern
    fairy: FairyKern

    @classmethod
    def fake(cls) -> "Kerne":
        """Fixture-Antworten ohne Modell und ohne Binaries (`SCHACH_ENGINE_FAKE_MODEL=1`)."""
        return cls(maia=FakeMaia(), analyse=FixtureAnalyse(), fairy=FixtureFairy())

    @classmethod
    def echt(cls, e: Einstellungen) -> "Kerne":
        from schach_engine.stockfish import FairyZug, StockfishAnalyse, UciPool, uci_starter

        stockfish = UciPool(
            "stockfish",
            uci_starter(e.stockfish_pfad, {"Threads": 1, "Hash": 32, "UCI_ShowWDL": True}),
            groesse=e.sf_pool_size,
            wartegrenze_s=e.sf_wartegrenze_s,
        )
        fairy = UciPool(
            "fairy-stockfish",
            uci_starter(e.fairy_pfad, {"Threads": 1, "Hash": 16}),
            groesse=e.fairy_pool_size,
            wartegrenze_s=e.sf_wartegrenze_s,
        )
        return cls(
            maia=EchteMaia(e.models_dir, e.maia_torch_threads),
            analyse=StockfishAnalyse(stockfish, e.sf_max_movetime_ms),
            fairy=FairyZug(fairy, e.fairy_movetime_ms),
        )

    @classmethod
    def aus_einstellungen(cls, e: Einstellungen) -> "Kerne":
        return cls.fake() if e.fake_model else cls.echt(e)
