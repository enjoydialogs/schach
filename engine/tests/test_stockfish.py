"""Stockfish-Pool und Adapter gegen eine Fake-UCI-Engine (tests/fake_uci.py), ohne echte Binaries."""

import sys
import threading
from contextlib import contextmanager
import time
from pathlib import Path

import chess
import chess.engine
import pytest

from schach_engine.fehler import EngineFehler
from schach_engine.stockfish import FairyZug, StockfishAnalyse, UciPool, uci_starter

FAKE_UCI = [sys.executable, str(Path(__file__).with_name("fake_uci.py"))]
ITALIENISCH = "r1bqkbnr/pppp1ppp/2n5/4p3/4P3/5N2/PPPP1PPP/RNBQKB1R w KQkq - 2 3"
NACH_F3_E5_G4 = "rnbqkbnr/pppp1ppp/8/4p3/6P1/5P2/PPPPP2P/RNBQKBNR b KQkq g3 0 2"


@pytest.fixture
def pool():
    erzeugt = []

    def bauen(groesse=1, wartegrenze_s=3.0, optionen=None):
        p = UciPool("fake", uci_starter(FAKE_UCI, optionen or {}), groesse=groesse, wartegrenze_s=wartegrenze_s)
        erzeugt.append(p)
        return p

    yield bauen
    for p in erzeugt:
        p.schliessen()


def test_starten_startet_alle_engines_sofort(pool):
    p = pool(groesse=2)

    p.starten()

    assert p.gestartet == 2


def test_ohne_starten_startet_der_pool_erst_beim_ersten_zugriff(pool):
    p = pool(groesse=2)
    assert p.gestartet == 0

    with p.engine():
        pass

    assert p.gestartet == 1


def test_uci_starter_setzt_nur_optionen_die_die_engine_kennt(pool):
    p = pool(optionen={"Threads": 1, "Hash": 32, "UCI_ShowWDL": True, "GibtEsNicht": 7})

    with p.engine() as engine:
        assert engine.protocol.config["Hash"] == 32
        assert engine.protocol.config["UCI_ShowWDL"] is True
        assert "GibtEsNicht" not in engine.protocol.config


def test_belegter_pool_ist_pool_busy_nach_der_wartegrenze(pool):
    p = pool(groesse=1, wartegrenze_s=0.1)
    belegt = threading.Event()
    frei = threading.Event()

    def halten():
        with p.engine():
            belegt.set()
            frei.wait(5)

    halter = threading.Thread(target=halten)
    halter.start()
    belegt.wait(5)
    beginn = time.monotonic()
    with pytest.raises(EngineFehler) as fehler:
        with p.engine():
            pass
    gewartet = time.monotonic() - beginn
    frei.set()
    halter.join()

    assert (fehler.value.status, fehler.value.code) == (503, "pool_busy")
    assert gewartet >= 0.1


def test_abgestuerzte_engine_wird_ersetzt(pool):
    p = pool(groesse=1)
    analyse = StockfishAnalyse(p, max_movetime_ms=800)
    board = chess.Board(ITALIENISCH)

    with pytest.raises(EngineFehler) as fehler:
        analyse.analyse(board, movetime_ms=666, multipv=1)

    assert (fehler.value.status, fehler.value.code) == (503, "pool_busy")
    assert p.gestartet == 0
    assert analyse.analyse(board, movetime_ms=10, multipv=1)["bestmove"]


def test_analyse_liefert_zeilen_nach_multipv(pool):
    ergebnis = StockfishAnalyse(pool(), max_movetime_ms=800).analyse(chess.Board(ITALIENISCH), 10, 3)

    assert len(ergebnis["lines"]) == 3
    assert ergebnis["bestmove"] == ergebnis["lines"][0]["uci"]
    assert ergebnis["lines"][0] == {"uci": ergebnis["bestmove"], "cp": 10, "mate": None,
                                    "wdl": {"w": 0.3, "d": 0.4, "l": 0.3}}


def test_analyse_meldet_matt_mit_cp_null(pool):
    ergebnis = StockfishAnalyse(pool(), max_movetime_ms=800).analyse(chess.Board(NACH_F3_E5_G4), 10, 1)

    assert ergebnis == {
        "bestmove": "d8h4",
        "lines": [{"uci": "d8h4", "cp": None, "mate": 1, "wdl": {"w": 1.0, "d": 0.0, "l": 0.0}}],
    }


class SpionPool:
    """Pool mit einer Spion-Engine, die das uebergebene Limit festhaelt."""

    def __init__(self):
        self.limits = []

    @contextmanager
    def engine(self):
        yield self

    def analyse(self, board, limit, multipv):
        self.limits.append(limit)
        zug = next(iter(board.legal_moves))
        wertung = chess.engine.PovScore(chess.engine.Cp(0), board.turn)
        return [{"pv": [zug], "score": wertung}]


@pytest.mark.parametrize("angefragt, erwartet", [(5000, 0.05), (20, 0.02)])
def test_analyse_kappt_movetime_auf_den_deckel(angefragt, erwartet):
    spion = SpionPool()

    StockfishAnalyse(spion, max_movetime_ms=50).analyse(chess.Board(ITALIENISCH), angefragt, 1)

    assert spion.limits == [chess.engine.Limit(time=erwartet)]


def test_analyse_ohne_wdl_der_engine_schaetzt_wdl_aus_cp():
    zeile = StockfishAnalyse(SpionPool(), max_movetime_ms=50).analyse(chess.Board(ITALIENISCH), 10, 1)["lines"][0]

    assert zeile["cp"] == 0
    assert sum(zeile["wdl"].values()) == pytest.approx(1.0)
    assert zeile["wdl"]["w"] == pytest.approx(zeile["wdl"]["l"])


def test_analyse_schickt_die_zugfolge_mit(pool):
    p = pool()
    board = chess.Board()
    for uci in ["f2f3", "e7e5", "g2g4"]:
        board.push_uci(uci)

    ergebnis = StockfishAnalyse(p, max_movetime_ms=800).analyse(board, 10, 1)

    assert ergebnis["bestmove"] == "d8h4"


def test_fairy_setzt_elo_und_liefert_einen_erlaubten_zug(pool):
    board = chess.Board("7k/8/8/8/8/8/8/K7 w - - 0 1")

    p = pool()
    fairy = FairyZug(p, movetime_ms=10)

    assert fairy.bestmove(board, skill=800) in {"a1a2", "a1b1", "a1b2"}
    with p.engine() as engine:
        assert engine.protocol.config["UCI_LimitStrength"] is True
        assert engine.protocol.config["UCI_Elo"] == 800


def test_fairy_kappt_elo_auf_den_bereich_der_engine(pool):
    p = pool()

    FairyZug(p, movetime_ms=10).bestmove(chess.Board(), skill=4000)

    with p.engine() as engine:
        assert engine.protocol.config["UCI_Elo"] == 2850
