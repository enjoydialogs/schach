"""Start, /healthz und Fristen mit steuerbaren Kernen (ohne Modell, ohne Stockfish)."""

import threading
import time

import pytest
from fastapi.testclient import TestClient

from schach_engine import fixture
from schach_engine.app import create_app
from schach_engine.einstellungen import Einstellungen
from schach_engine.kerne import Kerne
from schach_engine.maia import FakeMaia, MaiaErgebnis

GRUNDSTELLUNG = "rnbqkbnr/pppppppp/8/8/8/8/PPPPPPPP/RNBQKBNR w KQkq - 0 1"
MOVE = {"fen": GRUNDSTELLUNG, "moves": ["e2e4"], "self_elo": 1000, "oppo_elo": 800, "engine": "maia"}
ANALYSE = {"fen": GRUNDSTELLUNG, "moves": [], "movetime_ms": 800, "multipv": 1}


class GesperrterStart:
    """Kern, dessen `starten` wartet, bis der Test ihn freigibt; scheitert optional zuerst."""

    def __init__(self, fehlversuche=0):
        self.frei = threading.Event()
        self.versuche = 0
        self.fehlversuche = fehlversuche
        self.geschlossen = False

    def starten(self):
        self.versuche += 1
        if self.versuche <= self.fehlversuche:
            raise ConnectionError("huggingface unreachable")
        assert self.frei.wait(5)

    def schliessen(self):
        self.geschlossen = True


class LangsameMaia(FakeMaia):
    def __init__(self, dauer_s):
        self.dauer_s = dauer_s

    def bewerte(self, board, self_elo, oppo_elo):
        time.sleep(self.dauer_s)
        return super().bewerte(board, self_elo, oppo_elo)


class SpionMaia(FakeMaia):
    def __init__(self):
        self.aufrufe = []

    def bewerte(self, board, self_elo, oppo_elo):
        self.aufrufe.append((board.fen(), len(board.move_stack), self_elo, oppo_elo))
        return MaiaErgebnis(
            policy=[{"uci": m.uci(), "p": 1 / board.legal_moves.count()} for m in board.legal_moves],
            wdl={"w": 0.5, "d": 0.3, "l": 0.2},
        )


class FixtureAnalyse:
    def __init__(self, start=None):
        self.start = start

    def starten(self):
        if self.start:
            self.start.starten()

    def schliessen(self):
        pass

    def analyse(self, board, movetime_ms, multipv):
        return fixture.analyse(board, multipv)


class Maia(FakeMaia):
    def __init__(self, start=None, inner=None):
        self.start = start
        self.inner = inner or FakeMaia()

    def starten(self):
        if self.start:
            self.start.starten()

    def bewerte(self, board, self_elo, oppo_elo):
        return self.inner.bewerte(board, self_elo, oppo_elo)


def einstellungen(**felder):
    return Einstellungen(**{"fake_model": True, "start_pause_s": 0.01, **felder})


def kerne(maia=None, analyse=None):
    k = Kerne.fake()
    return Kerne(maia=maia or k.maia, analyse=analyse or k.analyse, fairy=k.fairy)


def warte_bis(bedingung, timeout_s=5.0):
    ende = time.monotonic() + timeout_s
    while time.monotonic() < ende:
        if bedingung():
            return True
        time.sleep(0.01)
    return False


def test_healthz_ist_starting_solange_das_modell_laedt():
    start = GesperrterStart()
    with TestClient(create_app(einstellungen(), kerne(maia=Maia(start)))) as client:
        antwort = client.get("/healthz")
        assert (antwort.status_code, antwort.json()) == (503, {"status": "starting"})

        start.frei.set()
        assert warte_bis(lambda: client.get("/healthz").status_code == 200)


def test_healthz_wartet_auch_auf_stockfish():
    start = GesperrterStart()
    with TestClient(create_app(einstellungen(), kerne(analyse=FixtureAnalyse(start)))) as client:
        time.sleep(0.05)
        assert client.get("/healthz").status_code == 503

        start.frei.set()
        assert warte_bis(lambda: client.get("/healthz").status_code == 200)


def test_healthz_wartet_nicht_auf_abgeschaltete_rollen():
    start = GesperrterStart()
    app = create_app(einstellungen(rollen=frozenset({"stockfish", "fairy"})), kerne(maia=Maia(start)))
    with TestClient(app) as client:
        assert warte_bis(lambda: client.get("/healthz").status_code == 200)
        assert start.versuche == 0


def test_move_waehrend_des_ladens_ist_starting():
    start = GesperrterStart()
    with TestClient(create_app(einstellungen(), kerne(maia=Maia(start)))) as client:
        antwort = client.post("/move", json=MOVE)
        start.frei.set()

    assert antwort.status_code == 503
    assert antwort.json()["error"] == "starting"


def test_analyse_waehrend_stockfish_startet_ist_starting():
    start = GesperrterStart()
    with TestClient(create_app(einstellungen(), kerne(analyse=FixtureAnalyse(start)))) as client:
        antwort = client.post("/analyse", json=ANALYSE)
        start.frei.set()

    assert antwort.status_code == 503
    assert antwort.json()["error"] == "starting"


def test_gescheiterter_start_wird_wiederholt():
    start = GesperrterStart(fehlversuche=2)
    start.frei.set()
    with TestClient(create_app(einstellungen(), kerne(maia=Maia(start)))) as client:
        assert warte_bis(lambda: client.get("/healthz").status_code == 200)

    assert start.versuche == 3


def test_ueberschrittene_frist_ist_queue_full():
    app = create_app(einstellungen(maia_frist_s=0.05), kerne(maia=Maia(inner=LangsameMaia(0.3))))
    with TestClient(app) as client:
        assert warte_bis(lambda: client.get("/healthz").status_code == 200)
        antwort = client.post("/move", json=MOVE)

    assert antwort.status_code == 503
    assert antwort.json()["error"] == "queue_full"


def test_maia_bekommt_zugfolge_und_beide_elo():
    spion = SpionMaia()
    with TestClient(create_app(einstellungen(), kerne(maia=Maia(inner=spion)))) as client:
        assert warte_bis(lambda: client.get("/healthz").status_code == 200)
        koerper = client.post("/move", json={**MOVE, "moves": ["e2e4", "e7e5"], "self_elo": 1234}).json()

    assert spion.aufrufe == [(spion.aufrufe[0][0], 2, 1234, 800)]
    assert koerper["wdl"] == {"w": 0.5, "d": 0.3, "l": 0.2}
    assert sum(e["p"] for e in koerper["policy"]) == pytest.approx(1.0)


def test_einstellungen_aus_env(monkeypatch):
    for name, wert in {
        "MAIA_TORCH_THREADS": "2",
        "MAIA_QUEUE_MAX": "8",
        "SF_POOL_SIZE": "3",
        "SF_MAX_MOVETIME_MS": "500",
        "FAIRY_POOL_SIZE": "2",
        "ENGINE_ROLES": " maia , fairy ,",
        "SCHACH_ENGINE_FAKE_MODEL": "0",
        "ENGINE_MODELS_DIR": "/daten/modelle",
    }.items():
        monkeypatch.setenv(name, wert)

    e = Einstellungen.aus_env()

    assert (e.maia_torch_threads, e.maia_queue_max, e.sf_pool_size) == (2, 8, 3)
    assert (e.sf_max_movetime_ms, e.fairy_pool_size) == (500, 2)
    assert e.rollen == frozenset({"maia", "fairy"})
    assert e.fake_model is False
    assert str(e.models_dir).replace("\\", "/") == "/daten/modelle"


def test_einstellungen_vorgaben_nach_spec(monkeypatch):
    for name in ["MAIA_TORCH_THREADS", "MAIA_QUEUE_MAX", "SF_POOL_SIZE", "SF_MAX_MOVETIME_MS",
                 "FAIRY_POOL_SIZE", "ENGINE_ROLES"]:
        monkeypatch.delenv(name, raising=False)

    e = Einstellungen.aus_env()

    assert (e.maia_torch_threads, e.maia_queue_max, e.maia_frist_s) == (1, 32, 2.0)
    assert (e.sf_pool_size, e.sf_max_movetime_ms, e.sf_wartegrenze_s, e.fairy_pool_size) == (2, 800, 3.0, 1)
    assert e.rollen == frozenset({"maia", "stockfish", "fairy"})


def test_kerne_werden_beim_beenden_geschlossen():
    analyse = GesperrterStart()
    analyse.frei.set()
    analyse.analyse = None
    with TestClient(create_app(einstellungen(), kerne(analyse=analyse))):
        pass

    assert analyse.geschlossen
