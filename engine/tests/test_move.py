import pytest

GRUNDSTELLUNG = "rnbqkbnr/pppppppp/8/8/8/8/PPPPPPPP/RNBQKBNR w KQkq - 0 1"


def maia_anfrage(**felder):
    anfrage = {
        "fen": GRUNDSTELLUNG,
        "moves": ["e2e4"],
        "self_elo": 1000,
        "oppo_elo": 800,
        "engine": "maia",
    }
    anfrage.update(felder)
    return anfrage


def test_maia_liefert_policy_ueber_alle_20_antworten_auf_e4(client):
    antwort = client.post("/move", json=maia_anfrage())

    assert antwort.status_code == 200
    koerper = antwort.json()
    assert koerper["engine"] == "maia"
    assert koerper["bestmove"] is None
    zuege = [e["uci"] for e in koerper["policy"]]
    assert len(zuege) == 20
    assert {"e7e5", "c7c5", "g8f6", "b8c6", "a7a6"} <= set(zuege)
    assert "e2e4" not in zuege


def test_maia_policy_ist_absteigend_sortiert_und_summiert_zu_eins(client):
    policy = client.post("/move", json=maia_anfrage()).json()["policy"]

    ps = [e["p"] for e in policy]
    assert ps == sorted(ps, reverse=True)
    assert sum(ps) == pytest.approx(1.0)
    assert all(p > 0 for p in ps)


def test_maia_wdl_ist_eine_verteilung(client):
    wdl = client.post("/move", json=maia_anfrage()).json()["wdl"]

    assert set(wdl) == {"w", "d", "l"}
    assert wdl["w"] + wdl["d"] + wdl["l"] == pytest.approx(1.0)


def test_maia_rechnet_nach_der_ganzen_zugfolge(client):
    # Nach 1. f3 e5 2. g4 hat Schwarz genau 30 Zuege.
    policy = client.post("/move", json=maia_anfrage(moves=["f2f3", "e7e5", "g2g4"])).json()["policy"]

    assert len(policy) == 30
    assert "d8h4" in [e["uci"] for e in policy]


def test_maia_schreibt_umwandlungen_klein(client):
    fen = "8/P7/8/8/8/8/8/k6K w - - 0 1"
    policy = client.post("/move", json=maia_anfrage(fen=fen, moves=[])).json()["policy"]

    assert {"a7a8q", "a7a8r", "a7a8b", "a7a8n"} <= {e["uci"] for e in policy}


def test_fairy_liefert_nur_bestmove(client):
    antwort = client.post("/move", json=maia_anfrage(engine="fairy", fairy_skill=500))

    assert antwort.status_code == 200
    koerper = antwort.json()
    assert koerper["engine"] == "fairy"
    assert koerper["policy"] is None
    assert koerper["wdl"] is None
    assert isinstance(koerper["bestmove"], str)


def test_fairy_bestmove_ist_ein_erlaubter_zug(client):
    # Weisser Koenig auf a1 hat genau drei Zuege.
    fen = "7k/8/8/8/8/8/8/K7 w - - 0 1"
    koerper = client.post("/move", json=maia_anfrage(fen=fen, moves=[], engine="fairy", fairy_skill=800)).json()

    assert koerper["bestmove"] in {"a1a2", "a1b1", "a1b2"}
