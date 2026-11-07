import json
import sys

import chess
import messung as m
import pytest
from engine_http import Antwort


class FakeTransport:
    """Antwortet vertragstreu mit festen Dauern; jede `fehler_alle`-te /move-Anfrage gibt 503."""

    def __init__(self, move_s=0.2, analyse_s=0.9, fehler_alle=0, fehlercode="queue_full"):
        self.move_s = move_s
        self.analyse_s = analyse_s
        self.fehler_alle = fehler_alle
        self.fehlercode = fehlercode
        self.anfragen: list[tuple[str, dict]] = []
        self.health = []

    def post(self, pfad, nutzlast):
        self.anfragen.append((pfad, nutzlast))
        board = chess.Board(nutzlast["fen"])
        for uci in nutzlast["moves"]:
            board.push_uci(uci)
        zuege = sorted(z.uci() for z in board.legal_moves)
        if pfad == "/move":
            n = sum(1 for p, _ in self.anfragen if p == "/move")
            if self.fehler_alle and n % self.fehler_alle == 0:
                return Antwort(503, {"error": self.fehlercode, "detail": "x"}, 0.01)
            policy = [{"uci": u, "p": 1 / len(zuege)} for u in zuege]
            return Antwort(200, {"engine": "maia", "policy": policy, "wdl": {"w": 0.4, "d": 0.2, "l": 0.4}, "bestmove": None}, self.move_s)
        line = {"uci": zuege[0], "cp": 0, "mate": None, "wdl": {"w": 0.2, "d": 0.6, "l": 0.2}}
        return Antwort(200, {"bestmove": zuege[0], "lines": [line]}, self.analyse_s)

    def get(self, pfad):
        status = self.health.pop(0) if self.health else 200
        return Antwort(status, {"status": "ok" if status == 200 else "starting"}, 0.001)


def konfig(**aenderungen):
    basis = {"partien": 4, "gegnerzuege": 6, "dauer_s": None, "denkzeit_s": (0.0, 0.0), "tipp_rate": 0.0, "seed": 1}
    basis.update(aenderungen)
    return m.Konfig(**basis)


def test_perzentil_nearest_rank():
    werte = [float(i) for i in range(1, 101)]
    assert m.perzentil(werte, 50) == 50.0
    assert m.perzentil(werte, 95) == 95.0
    assert m.perzentil([3.0], 95) == 3.0
    assert m.perzentil([], 95) is None


def test_misst_gegnerzug_je_partie_und_kriterium():
    t = FakeTransport(move_s=0.2)
    bericht = m.messe(t, konfig(), schlafen=lambda _s: None)
    phase = bericht["phasen"][0]
    gz = phase["endpunkte"]["move_maia"]
    assert gz["n"] == 4 * 6
    assert gz["ok"] == 24
    assert gz["p50_ms"] == pytest.approx(200)
    assert gz["p95_ms"] == pytest.approx(200)
    assert phase["endpunkte"]["analyse_tipp"]["n"] == 0
    assert phase["kriterium5"]["gegnerzug_p95_ok"] is True
    assert phase["kriterium5"]["tipp_p95_ok"] is None  # keine Tipps gemessen
    # Gegnerzuege nutzen die Leiter-Elo; oppo_elo ist der Gast-Wert 800.
    self_elos = {n["self_elo"] for p, n in t.anfragen if p == "/move"}
    assert self_elos <= {g.elo for g in m.GEGNER}
    assert {n["oppo_elo"] for p, n in t.anfragen if p == "/move"} == {800}


def test_tipp_rate_eins_fragt_bei_jedem_spielerzug():
    t = FakeTransport(analyse_s=1.6)
    bericht = m.messe(t, konfig(tipp_rate=1.0, tipp_movetime_ms=800), schlafen=lambda _s: None)
    tipp = bericht["phasen"][0]["endpunkte"]["analyse_tipp"]
    assert tipp["n"] > 0
    analysen = [n for p, n in t.anfragen if p == "/analyse"]
    assert all(a["movetime_ms"] == 800 and a["multipv"] == 1 for a in analysen)
    assert bericht["phasen"][0]["kriterium5"]["tipp_p95_ok"] is False  # 1,6 s > 1,5 s


def test_503_wird_gezaehlt_und_wiederholt():
    t = FakeTransport(move_s=0.3, fehler_alle=4)
    pausen = []
    bericht = m.messe(t, konfig(partien=1, gegnerzuege=6, retry_pause_s=0.5), schlafen=pausen.append)
    gz = bericht["phasen"][0]["endpunkte"]["move_maia"]
    assert gz["fehler"] == {"queue_full": gz["n"] - gz["ok"]}
    assert gz["ok"] == 6  # jeder Gegnerzug kam nach Wiederholung durch
    assert gz["quote_503"] == pytest.approx((gz["n"] - 6) / gz["n"])
    assert 0.5 in pausen
    # Gegnerzug gesamt = Versuche plus Pause, darum > Einzelanfrage.
    gesamt = bericht["phasen"][0]["gegnerzug_gesamt"]
    assert gesamt["max_ms"] == pytest.approx(10 + 500 + 300)


def test_phasen_maia_und_gemischt():
    t = FakeTransport()
    bericht = m.messe(t, konfig(tipp_rate=0.5, phasen=("maia", "gemischt")), schlafen=lambda _s: None)
    namen = [p["name"] for p in bericht["phasen"]]
    assert namen == ["maia", "gemischt"]
    assert bericht["phasen"][0]["endpunkte"]["analyse_tipp"]["n"] == 0
    assert bericht["phasen"][1]["endpunkte"]["analyse_tipp"]["n"] > 0


def test_rss_aus_befehl():
    befehl = [sys.executable, "-c", "print('512.5MiB / 2GiB')"]
    probe = m.RssProbe(befehl)
    assert probe.lies() == pytest.approx(512.5)
    assert m.parse_mib("1.2GiB / 2GiB") == pytest.approx(1228.8)
    assert m.parse_mib("734003200") == pytest.approx(700.0)  # Bytes, z. B. cgroup memory.current
    assert m.parse_mib("300MB / 2GB") == pytest.approx(300e6 / 2**20)
    assert m.parse_mib("kaputt") is None


def test_erststart_wartet_bis_healthz_200():
    t = FakeTransport()
    t.health = [503, 503, 503, 200]
    uhr = iter([0.0, 1.0, 2.0, 3.0, 4.5, 5.0])
    erg = m.erststart(t, intervall_s=1.0, timeout_s=60, schlafen=lambda _s: None, uhr=lambda: next(uhr))
    assert erg == {"gesund": True, "sekunden": 4.5, "versuche": 4}


def test_markdown_und_json(tmp_path):
    bericht = m.messe(FakeTransport(), konfig(tipp_rate=0.5), schlafen=lambda _s: None)
    md = m.als_markdown(bericht)
    assert "| move_maia |" in md and "p95" in md and "Kriterium 5" in md
    json.dumps(bericht)  # serialisierbar


def test_gegen_den_echten_fake_dienst(engine_url):
    bericht = m.messe(m.HttpTransport(engine_url), konfig(partien=3, gegnerzuege=4, tipp_rate=0.5))
    phase = bericht["phasen"][0]
    assert phase["endpunkte"]["move_maia"]["ok"] == 12
    assert phase["endpunkte"]["move_maia"]["fehler"] == {}
    assert phase["endpunkte"]["analyse_tipp"]["ok"] == phase["endpunkte"]["analyse_tipp"]["n"]


def test_cli_schreibt_dateien(engine_url, tmp_path):
    ausgabe = tmp_path / "m.json"
    md = tmp_path / "m.md"
    code = m.main(["--url", engine_url, "--partien", "2", "--gegnerzuege", "3", "--denkzeit", "0", "0", "--json", str(ausgabe), "--markdown", str(md)])
    assert code == 0
    assert json.loads(ausgabe.read_text(encoding="utf-8"))["phasen"][0]["endpunkte"]["move_maia"]["ok"] == 6
    assert "move_maia" in md.read_text(encoding="utf-8")
