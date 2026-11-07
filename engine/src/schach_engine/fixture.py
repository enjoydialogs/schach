"""Fixture-Engine (SCHACH-SPEC 15.2): antwortet vertragstreu, rechnet aber nicht.

Die Zuege sind echt (python-chess), die Wahrscheinlichkeiten und Bewertungen fest. Seit S4a nur
noch mit `SCHACH_ENGINE_FAKE_MODEL=1` (pytest, CI-Stufe engine); sonst rechnen Maia-3, Stockfish
und Fairy-Stockfish (`kerne.py`).
"""

import chess

# Feste WDL aus engine/API.md (Beispiel /move), Summe 1.
FIXTURE_WDL = {"w": 0.41, "d": 0.18, "l": 0.41}


def policy(board: chess.Board) -> list[dict[str, float | str]]:
    """Alle erlaubten Zuege, Gewicht 1/(Rang+1) in UCI-Reihenfolge, normalisiert, absteigend."""
    zuege = sorted(m.uci() for m in board.legal_moves)
    gewichte = [1.0 / (rang + 1) for rang in range(len(zuege))]
    summe = sum(gewichte)
    return [{"uci": uci, "p": g / summe} for uci, g in zip(zuege, gewichte, strict=True)]


def bestmove(board: chess.Board) -> str:
    """Der Zug mit dem hoechsten Fixture-Gewicht."""
    return str(policy(board)[0]["uci"])


def analyse(board: chess.Board, multipv: int) -> dict[str, object]:
    """Bis zu `multipv` Zeilen. Matt in eins steht vorn, sonst feste Bewertung 0."""

    def setzt_matt(uci: str) -> bool:
        board.push(chess.Move.from_uci(uci))
        try:
            return board.is_checkmate()
        finally:
            board.pop()

    zuege = [str(e["uci"]) for e in policy(board)]
    zuege.sort(key=lambda uci: not setzt_matt(uci))  # stabil: Matt zuerst, sonst Policy-Rang
    zeilen = [
        {"uci": uci, "cp": None, "mate": 1, "wdl": {"w": 1.0, "d": 0.0, "l": 0.0}}
        if setzt_matt(uci)
        else {"uci": uci, "cp": 0, "mate": None, "wdl": {"w": 0.2, "d": 0.6, "l": 0.2}}
        for uci in zuege[:multipv]
    ]
    return {"bestmove": zeilen[0]["uci"], "lines": zeilen}
