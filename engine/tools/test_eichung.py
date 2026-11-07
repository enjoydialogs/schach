import json
import math
import sys
from pathlib import Path

import chess
import chess.engine
import eichung as e
import pytest
import stil_referenz as s
from engine_http import HttpTransport

# --- Elo-Mathematik ----------------------------------------------------------------------


def test_elo_aus_punkten_symmetrisch():
    gleich = e.elo_schaetzung(siege=10, remis=0, niederlagen=10, gegner_elo=1000)
    assert gleich.elo == pytest.approx(1000)
    assert gleich.unten < 1000 < gleich.oben
    # 75 % -> +190,8 Elo
    vorn = e.elo_schaetzung(siege=15, remis=0, niederlagen=5, gegner_elo=1000)
    assert vorn.elo == pytest.approx(1000 + 400 * math.log10(3), abs=0.1)


def test_elo_remis_zaehlen_halb_und_engeres_intervall():
    nur_remis = e.elo_schaetzung(siege=0, remis=20, niederlagen=0, gegner_elo=800)
    gemischt = e.elo_schaetzung(siege=10, remis=0, niederlagen=10, gegner_elo=800)
    assert nur_remis.elo == pytest.approx(800)
    assert (nur_remis.oben - nur_remis.unten) < (gemischt.oben - gemischt.unten)


def test_elo_alles_gewonnen_bleibt_endlich():
    alles = e.elo_schaetzung(siege=20, remis=0, niederlagen=0, gegner_elo=1000)
    assert math.isfinite(alles.elo) and alles.elo > 1400


def test_elo_gepoolt_ueber_mehrere_anker():
    # 50 % gegen 900 und 50 % gegen 1100 -> 1000
    partien = [(900, 1.0), (900, 0.0)] * 10 + [(1100, 1.0), (1100, 0.0)] * 10
    g = e.elo_gepoolt(partien)
    assert g.elo == pytest.approx(1000, abs=1)
    assert g.unten < 1000 < g.oben


# --- Suche ---------------------------------------------------------------------------------


def test_suche_nimmt_den_naechsten_offset_im_band():
    # Maia spielt 250 Elo zu stark: Offset -200 trifft, -300 auch, -200 ist naeher an der Identitaet.
    gemessen = []

    def messen(self_elo, temperatur):
        gemessen.append((self_elo, temperatur))
        return e.Schaetzung(elo=self_elo + 250, unten=self_elo + 200, oben=self_elo + 300, n=100)

    treffer = e.suche(ziel_elo=1200, messen=messen, offsets=[0, -100, 100, -200, 200, -300], temperaturen=[1.0])
    assert treffer.self_elo == 1000 and treffer.temperatur == 1.0 and treffer.im_band
    assert gemessen == [(1200, 1.0), (1100, 1.0), (1300, 1.0), (1000, 1.0)]


def test_suche_ohne_treffer_meldet_besten_versuch():
    def messen(self_elo, temperatur):
        return e.Schaetzung(elo=self_elo + 500, unten=0, oben=0, n=10)

    treffer = e.suche(ziel_elo=600, messen=messen, offsets=[0, -100], temperaturen=[1.0, 0.8])
    assert not treffer.im_band
    assert treffer.self_elo == 500


def test_eichung_konfig_mit_fairy_ausweg_fuer_anfaenger():
    ergebnisse = {
        "ilse": e.Treffer(self_elo=600, temperatur=1.0, schaetzung=e.Schaetzung(1100, 1000, 1200, 50), im_band=False),
        "oscar": e.Treffer(self_elo=900, temperatur=0.9, schaetzung=e.Schaetzung(1010, 950, 1070, 50), im_band=True),
        "konrad": e.Treffer(self_elo=1900, temperatur=1.0, schaetzung=e.Schaetzung(1500, 1400, 1600, 50), im_band=False),
    }
    konfig = e.engine_eichung(ergebnisse)
    block = konfig["Engine"]["Eichung"]
    assert block["oscar"] == {"SelfElo": 900, "Temperatur": 0.9, "Engine": "Maia"}
    assert block["ilse"] == {"SelfElo": 600, "Temperatur": 1.0, "Engine": "Fairy"}  # 5.4 a: Ausweg
    assert block["konrad"] == {"SelfElo": 1900, "Temperatur": 1.0, "Engine": "Maia"}  # nicht im Band, Identitaet
    assert set(konfig["nicht_im_band"]) == {"ilse", "konrad"}


# --- Partien gegen den Fake-Dienst ----------------------------------------------------------


def test_partie_endet_regulaer_oder_am_zuglimit(engine_url):
    t = HttpTransport(engine_url)
    maia = e.MaiaSpieler(t, self_elo=1000, oppo_elo=1000, waehler=e.referenz_waehler(s.Stilparameter(s.NEUTRAL)))
    fairy = e.FairyHttpSpieler(t, skill=1000)
    p = e.spiele_partie(maia, fairy, seed=3, max_halbzuege=40)
    assert p.ergebnis in ("1-0", "0-1", "1/2-1/2")
    assert p.halbzuege <= 40
    if p.halbzuege == 40:
        assert p.grund == "zuglimit"
    assert p.zuege_weiss > 0 and p.merkmale_weiss["schlagzug"] >= 0


def test_waehler_hook_wird_benutzt(engine_url):
    aufrufe = []

    def waehler(board, policy, seed, halbzug):
        aufrufe.append(halbzug)
        uci = min(u for u, _ in policy)
        return uci, s.Merkmal.KEINE

    t = HttpTransport(engine_url)
    maia = e.MaiaSpieler(t, 1000, 1000, waehler)
    p = e.spiele_partie(maia, e.FairyHttpSpieler(t, 1000), seed=1, max_halbzuege=10)
    assert aufrufe == [0, 2, 4, 6, 8]
    assert p.halbzuege == 10


def test_waehler_aus_modulpfad():
    fabrik = e.lade_waehler("stil_referenz_waehler_test:fabrik")
    w = fabrik(s.Stilparameter(s.NEUTRAL))
    assert w(chess.Board(), [("e2e4", 1.0)], 0, 0) == ("e2e4", s.Merkmal.KEINE)


def test_staerke_ist_fortsetzbar(engine_url, tmp_path):
    spiele = tmp_path / "spiele.jsonl"
    lauf = e.Lauf(HttpTransport(engine_url), spiele, max_halbzuege=20, parallel=2)
    erste = lauf.staerke_messen("oscar", self_elo=1000, temperatur=1.0, anker_elos=[900, 1100], partien_je_anker=2)
    assert erste.n == 4
    zeilen = spiele.read_text(encoding="utf-8").splitlines()
    assert len(zeilen) == 4
    zweite = lauf.staerke_messen("oscar", self_elo=1000, temperatur=1.0, anker_elos=[900, 1100], partien_je_anker=3)
    assert zweite.n == 6
    assert len(spiele.read_text(encoding="utf-8").splitlines()) == 6  # nur die 2 neuen gespielt
    ids = [json.loads(z)["id"] for z in spiele.read_text(encoding="utf-8").splitlines()]
    assert len(set(ids)) == 6
    farben = [json.loads(z)["maia_weiss"] for z in spiele.read_text(encoding="utf-8").splitlines()]
    assert True in farben and False in farben


def test_stil_ab_und_sichtbarkeit(engine_url, tmp_path):
    lauf = e.Lauf(HttpTransport(engine_url), tmp_path / "stil.jsonl", max_halbzuege=30, parallel=2)
    bericht = lauf.stil_ab("finn", self_elo=800, temperatur=1.0, partien=4)
    assert bericht["partien"] == 4
    assert 0.0 <= bericht["punkte_gestylt"] <= 1.0
    assert bericht["bestanden"] == (0.45 <= bericht["punkte_gestylt"] <= 0.55)
    sicht = bericht["sichtbarkeit"]
    assert set(sicht["gestylt"]) >= {"schlagzug", "schach", "riskant"}
    assert sicht["pruefung"]["art"] == "aggressiv"


def test_sichtbarkeit_regeln():
    neutral = {"zuege": 100, "schlagzug": 20, "schach": 10, "riskant": 20}
    stark = {"zuege": 100, "schlagzug": 27, "schach": 11, "riskant": 20}
    a = e.sichtbarkeit_pruefen("aggressiv", stark, neutral)
    assert a["wert"] == pytest.approx(38 / 30 - 1) and a["bestanden"]
    v = e.sichtbarkeit_pruefen("vorsichtig", {"zuege": 100, "schlagzug": 0, "schach": 0, "riskant": 16}, neutral)
    assert v["wert"] == pytest.approx(-0.2) and not v["bestanden"]
    assert e.sichtbarkeit_pruefen("eitel", stark, neutral)["bestanden"] is None


def test_cli_staerke_schreibt_ergebnis(engine_url, tmp_path):
    aus = tmp_path / "e.json"
    code = e.main([
        "staerke", "--url", engine_url, "--gegner", "oscar", "--partien", "2", "--offsets", "0",
        "--max-halbzuege", "16", "--spiele", str(tmp_path / "s.jsonl"), "--ausgabe", str(aus),
    ])
    assert code == 0
    daten = json.loads(aus.read_text(encoding="utf-8"))
    assert "oscar" in daten["Engine"]["Eichung"]
    assert daten["stufen"]["oscar"]["versuche"][0]["self_elo"] == 1000


def test_uci_anker_setzt_limit_strength_und_elo(engine_url, tmp_path):
    fake = [sys.executable, str(Path(__file__).parents[1] / "tests" / "fake_uci.py")]
    lauf = e.Lauf(
        HttpTransport(engine_url), tmp_path / "u.jsonl", max_halbzuege=12, parallel=1,
        anker_fabrik=lambda elo: e.UciSpieler(fake, elo, nodes=1000),
    )  # fmt: skip
    sch = lauf.staerke_messen("theo", self_elo=1400, temperatur=1.0, anker_elos=[1500], partien_je_anker=2)
    assert sch.n == 2
    sp = e.UciSpieler(fake, 1500, nodes=1000)
    try:
        with sp._engine.analysis(chess.Board(), sp._limit, info=chess.engine.INFO_ALL) as analyse:
            texte = [i["string"] for i in analyse if "string" in i]
        assert "go nodes 1000" in texte
        assert "elo 1500 limit true" in texte
    finally:
        sp.schliessen()
