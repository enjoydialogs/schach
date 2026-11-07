"""Modelltest mit dem echten Maia3-5M (SCHACH-SPEC 15.2).

Nur mit `SCHACH_ENGINE_MODEL_TESTS=1`: braucht das Extra `model` (PyTorch CPU) und laedt beim ersten
Lauf `maia3-5m.pt` (ca. 21 MB) von Hugging Face nach `SCHACH_ENGINE_MODEL_CACHE` oder in den
pytest-Cache. Ohne die Variable wird der Test uebersprungen.
"""

import os
from pathlib import Path

import chess
import pytest

from schach_engine.maia import EchteMaia

pytestmark = pytest.mark.model


@pytest.fixture(scope="module")
def maia(tmp_path_factory):
    cache = os.environ.get("SCHACH_ENGINE_MODEL_CACHE")
    kern = EchteMaia(Path(cache) if cache else tmp_path_factory.mktemp("models"), torch_threads=1)
    kern.starten()
    return kern


def nach(*zuege):
    board = chess.Board()
    for uci in zuege:
        board.push_uci(uci)
    return board


def test_policy_ueber_alle_antworten_auf_e4_ist_menschlich(maia):
    ergebnis = maia.bewerte(nach("e2e4"), self_elo=1500, oppo_elo=1500)

    zuege = [e["uci"] for e in ergebnis.policy]
    ps = [e["p"] for e in ergebnis.policy]
    assert sorted(zuege) == sorted(m.uci() for m in nach("e2e4").legal_moves)
    assert sum(ps) == pytest.approx(1.0, abs=1e-5)
    assert ps == sorted(ps, reverse=True)
    assert {"e7e5", "c7c5"} & set(zuege[:3])
    assert sum(ergebnis.wdl.values()) == pytest.approx(1.0, abs=1e-5)


def test_policy_fuer_schwarz_und_umwandlung_ist_gespiegelt_richtig(maia):
    # Schwarz am Zug wandelt auf a1 um; die Dame ist der haeufigste menschliche Zug.
    board = chess.Board("8/8/8/8/8/8/p7/4K2k b - - 0 1")

    ergebnis = maia.bewerte(board, self_elo=1500, oppo_elo=1500)

    assert ergebnis.policy[0]["uci"] == "a2a1q"


def test_schaefermatt_ist_der_wahrscheinlichste_zug_und_wdl_sieht_den_gewinn(maia):
    board = nach("e2e4", "e7e5", "f1c4", "b8c6", "d1h5", "g8f6")

    ergebnis = maia.bewerte(board, self_elo=1800, oppo_elo=1800)

    assert ergebnis.policy[0]["uci"] == "h5f7"
    assert ergebnis.wdl["w"] > 0.8


def test_narrenmatt_steht_fuer_schwarz_weit_vorn(maia):
    ergebnis = maia.bewerte(nach("f2f3", "e7e5", "g2g4"), self_elo=2200, oppo_elo=2200)

    assert "d8h4" in [e["uci"] for e in ergebnis.policy[:2]]
