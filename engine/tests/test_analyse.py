ITALIENISCH = "r1bqkbnr/pppp1ppp/2n5/4p3/4P3/5N2/PPPP1PPP/RNBQKB1R w KQkq - 2 3"
NACH_F3_E5_G4 = "rnbqkbnr/pppp1ppp/8/4p3/6P1/5P2/PPPPP2P/RNBQKBNR b KQkq g3 0 2"


def analyse_anfrage(**felder):
    anfrage = {"fen": ITALIENISCH, "moves": [], "movetime_ms": 800, "multipv": 2}
    anfrage.update(felder)
    return anfrage


def test_analyse_liefert_so_viele_zeilen_wie_multipv(client):
    antwort = client.post("/analyse", json=analyse_anfrage(multipv=3))

    assert antwort.status_code == 200
    koerper = antwort.json()
    assert len(koerper["lines"]) == 3
    assert koerper["bestmove"] == koerper["lines"][0]["uci"]
    for zeile in koerper["lines"]:
        assert set(zeile) == {"uci", "cp", "mate", "wdl"}
        assert set(zeile["wdl"]) == {"w", "d", "l"}


def test_analyse_findet_matt_in_eins_mit_cp_null(client):
    koerper = client.post("/analyse", json=analyse_anfrage(fen=NACH_F3_E5_G4, multipv=1)).json()

    assert koerper["bestmove"] == "d8h4"
    assert koerper["lines"] == [
        {"uci": "d8h4", "cp": None, "mate": 1, "wdl": {"w": 1.0, "d": 0.0, "l": 0.0}}
    ]


def test_analyse_kappt_movetime_statt_abzulehnen(client):
    antwort = client.post("/analyse", json=analyse_anfrage(movetime_ms=5000))

    assert antwort.status_code == 200
