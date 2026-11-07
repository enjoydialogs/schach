import pytest
from fastapi.testclient import TestClient

from schach_engine.app import create_app

GRUNDSTELLUNG = "rnbqkbnr/pppppppp/8/8/8/8/PPPPPPPP/RNBQKBNR w KQkq - 0 1"
# Schwarz ist nach 1. f3 e5 2. g4 Dh4 matt.
NARRENMATT_ZUEGE = ["f2f3", "e7e5", "g2g4", "d8h4"]
PATT = "7k/5Q2/6K1/8/8/8/8/8 b - - 0 1"

MOVE = {"fen": GRUNDSTELLUNG, "moves": [], "self_elo": 1000, "oppo_elo": 800, "engine": "maia"}
ANALYSE = {"fen": GRUNDSTELLUNG, "moves": [], "movetime_ms": 800, "multipv": 1}


def fehler(antwort, status, code):
    assert antwort.status_code == status
    koerper = antwort.json()
    assert set(koerper) == {"error", "detail"}
    assert koerper["error"] == code
    assert isinstance(koerper["detail"], str)


@pytest.mark.parametrize("pfad, anfrage", [("/move", MOVE), ("/analyse", ANALYSE)])
def test_unlesbare_fen_ist_invalid_fen(client, pfad, anfrage):
    fehler(client.post(pfad, json={**anfrage, "fen": "kein fen"}), 400, "invalid_fen")


@pytest.mark.parametrize("pfad, anfrage", [("/move", MOVE), ("/analyse", ANALYSE)])
def test_illegaler_zug_ist_illegal_move(client, pfad, anfrage):
    fehler(client.post(pfad, json={**anfrage, "moves": ["e2e5"]}), 400, "illegal_move")


@pytest.mark.parametrize("pfad, anfrage", [("/move", MOVE), ("/analyse", ANALYSE)])
def test_kein_uci_ist_illegal_move(client, pfad, anfrage):
    fehler(client.post(pfad, json={**anfrage, "moves": ["Nf3"]}), 400, "illegal_move")


@pytest.mark.parametrize("pfad, anfrage", [("/move", MOVE), ("/analyse", ANALYSE)])
def test_fehlendes_feld_ist_invalid_request(client, pfad, anfrage):
    ohne_moves = {k: v for k, v in anfrage.items() if k != "moves"}
    fehler(client.post(pfad, json=ohne_moves), 400, "invalid_request")


@pytest.mark.parametrize("multipv", [0, 4])
def test_multipv_ausserhalb_1_bis_3_ist_invalid_request(client, multipv):
    fehler(client.post("/analyse", json={**ANALYSE, "multipv": multipv}), 400, "invalid_request")


def test_unbekannte_engine_ist_invalid_request(client):
    fehler(client.post("/move", json={**MOVE, "engine": "leela"}), 400, "invalid_request")


@pytest.mark.parametrize("skill", [None, 499])
def test_fairy_braucht_skill_ab_500(client, skill):
    anfrage = {**MOVE, "engine": "fairy"}
    if skill is not None:
        anfrage["fairy_skill"] = skill
    fehler(client.post("/move", json=anfrage), 400, "invalid_request")


@pytest.mark.parametrize("pfad, anfrage", [("/move", MOVE), ("/analyse", ANALYSE)])
def test_matt_ist_no_legal_moves(client, pfad, anfrage):
    fehler(client.post(pfad, json={**anfrage, "moves": NARRENMATT_ZUEGE}), 409, "no_legal_moves")


def test_patt_ist_no_legal_moves(client):
    fehler(client.post("/move", json={**MOVE, "fen": PATT}), 409, "no_legal_moves")


@pytest.mark.parametrize(
    "rollen, pfad, anfrage",
    [
        ("stockfish,fairy", "/move", MOVE),
        ("maia,stockfish", "/move", {**MOVE, "engine": "fairy", "fairy_skill": 500}),
        ("maia,fairy", "/analyse", ANALYSE),
    ],
)
def test_abgeschaltete_rolle_ist_role_disabled(monkeypatch, rollen, pfad, anfrage):
    monkeypatch.setenv("ENGINE_ROLES", rollen)
    with TestClient(create_app()) as client:
        fehler(client.post(pfad, json=anfrage), 503, "role_disabled")


def test_healthz_bleibt_bei_abgeschalteter_rolle_gesund(monkeypatch):
    monkeypatch.setenv("ENGINE_ROLES", "fairy")
    with TestClient(create_app()) as client:
        assert client.get("/healthz").status_code == 200
