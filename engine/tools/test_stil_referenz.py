"""Paritaet der Python-Referenz mit Schach.Domain (Werte aus dotnet, testdaten/stil_dotnet.json)."""

import json
from pathlib import Path

import chess
import pytest
import stil_referenz as s

GOLD = json.loads((Path(__file__).parent / "testdaten" / "stil_dotnet.json").read_text(encoding="utf-8"))

POLICY = [("e2e4", 0.40), ("d2d4", 0.30), ("g1f3", 0.12), ("c2c4", 0.09), ("b1c3", 0.05), ("a2a3", 0.04)]
_M = {
    "e2e4": s.Merkmal.KOENIGSZONE,
    "g1f3": s.Merkmal.ENTWICKLUNG,
    "b1c3": s.Merkmal.ENTWICKLUNG | s.Merkmal.SCHACH,
    "c2c4": s.Merkmal.SCHLAGZUG | s.Merkmal.SCHACH | s.Merkmal.KOENIGSZONE,
}


def merkmale_fest(uci: str) -> s.Merkmal:
    return _M.get(uci, s.Merkmal.KEINE)


@pytest.mark.parametrize("fall", GOLD["zahl"], ids=lambda f: f"{f['seed']}-{f['halbzug']}")
def test_partiezufall_wie_dotnet(fall):
    assert s.partiezufall(fall["seed"], fall["halbzug"]) == fall["wert"]


def test_brettmerkmale_wie_dotnet():
    abweichungen = []
    for fall in GOLD["merkmale"]:
        board = chess.Board(fall["fen"])
        bits = int(s.brettmerkmale(board, chess.Move.from_uci(fall["uci"])))
        if bits != fall["bits"]:
            abweichungen.append((fall["fen"], fall["uci"], bits, fall["bits"]))
    assert abweichungen == []
    assert len(GOLD["merkmale"]) > 200


@pytest.mark.parametrize("fall", GOLD["waehle"], ids=lambda f: f"tau{f['tau']}-h{f['halbzug']}")
def test_waehle_wie_dotnet(fall):
    tau = fall["tau"]
    aggressiv = s.waehle(
        POLICY, s.Stilparameter(s.PERSOENLICHKEITEN["aggressiv"], tau), merkmale_fest, s.Remislage(), 2026, fall["halbzug"]
    )
    assert (aggressiv.uci, aggressiv.maia_rang, aggressiv.w) == (fall["aggressiv"], fall["aggressiv_rang"], fall["aggressiv_w"])

    vorn = s.Remislage(3, lambda u: u in ("d2d4", "e2e4"))
    vorsichtig = s.waehle(POLICY, s.Stilparameter(s.PERSOENLICHKEITEN["vorsichtig"], tau), merkmale_fest, vorn, 77, fall["halbzug"])
    assert vorsichtig.uci == fall["vorsichtig_remis"]

    hinten = s.Remislage(-2, lambda u: u == "g1f3")
    neutral = s.waehle(POLICY, s.Stilparameter(s.NEUTRAL, tau), merkmale_fest, hinten, 5, fall["halbzug"])
    assert neutral.uci == fall["neutral_hinten"]


def test_band_hoechstens_acht_und_r():
    policy = [(f"a{i}", 1.0 / (i + 1)) for i in range(20)]
    band = s.kandidatenband(policy, s.Stilparameter(s.NEUTRAL))
    assert band == [f"a{i}" for i in range(4)]  # p >= 0,25 * p_max
    breit = s.kandidatenband(policy, s.Stilparameter(s.NEUTRAL, band_faktor=0.01))
    assert len(breit) == 8


def test_gewicht_begrenzt():
    alles = s.Merkmal.SCHLAGZUG | s.Merkmal.SCHACH | s.Merkmal.KOENIGSZONE | s.Merkmal.RISKANT
    assert s.gewicht(alles, s.PERSOENLICHKEITEN["aggressiv"]) == 2.0
    assert s.gewicht(s.Merkmal.RISKANT | s.Merkmal.KOENIGSZONE, s.PERSOENLICHKEITEN["vorsichtig"]) == 0.5
    assert s.gewicht(alles, s.PERSOENLICHKEITEN["aggressiv"], maximum=1.5) == 1.5


def test_materialbilanz_aus_sicht_seite_am_zug():
    assert s.materialbilanz(chess.Board("4k3/8/8/8/8/8/8/R3K3 b - - 0 1")) == -5
    assert s.materialbilanz(chess.Board()) == 0


def test_waehle_auf_brett_ist_deterministisch_und_legal():
    board = chess.Board()
    policy = [(m.uci(), 1.0 / 20) for m in board.legal_moves]
    a, _ = s.waehle_auf_brett(board, policy, s.Stilparameter(s.PERSOENLICHKEITEN["aggressiv"]), 9, 0)
    b, _ = s.waehle_auf_brett(board, policy, s.Stilparameter(s.PERSOENLICHKEITEN["aggressiv"]), 9, 0)
    assert a == b
    assert chess.Move.from_uci(a.uci) in board.legal_moves
