"""Minimale UCI-Engine fuer Tests: spricht das Protokoll wie Stockfish, rechnet aber nicht.

`go` meldet je MultiPV-Zeile einen erlaubten Zug, Matt in eins zuerst (`score mate 1`, WDL
1000 0 0), sonst `score cp 10` mit WDL 300 400 300. Dazu `info string`-Zeilen mit dem empfangenen
`go`-Befehl und dem gesetzten `UCI_Elo`, damit Tests pruefen koennen, was der Adapter schickt.
`go movetime 666` beendet den Prozess, um einen Absturz nachzustellen.
"""

import sys

import chess


def antworte(zeile: str) -> None:
    sys.stdout.write(zeile + "\n")
    sys.stdout.flush()


def setzt_matt(board: chess.Board, zug: chess.Move) -> bool:
    board.push(zug)
    try:
        return board.is_checkmate()
    finally:
        board.pop()


def main() -> None:
    board = chess.Board()
    optionen = {"MultiPV": "1", "UCI_Elo": "1350", "UCI_LimitStrength": "false"}
    for roh in sys.stdin:
        teile = roh.split()
        if not teile:
            continue
        befehl = teile[0]
        if befehl == "uci":
            antworte("id name FakeUci")
            antworte("option name Threads type spin default 1 min 1 max 512")
            antworte("option name Hash type spin default 16 min 1 max 33554432")
            antworte("option name MultiPV type spin default 1 min 1 max 500")
            antworte("option name UCI_ShowWDL type check default false")
            antworte("option name UCI_LimitStrength type check default false")
            antworte("option name UCI_Elo type spin default 1350 min 500 max 2850")
            antworte("uciok")
        elif befehl == "isready":
            antworte("readyok")
        elif befehl == "setoption":
            text = roh.strip()
            name, _, wert = text.split(" name ", 1)[1].partition(" value ")
            optionen[name.strip()] = wert.strip()
        elif befehl == "position":
            if teile[1] == "startpos":
                board = chess.Board()
                rest = teile[2:]
            else:
                board = chess.Board(" ".join(teile[2:8]))
                rest = teile[8:]
            if rest and rest[0] == "moves":
                for uci in rest[1:]:
                    board.push_uci(uci)
        elif befehl == "go":
            if "666" in teile:
                sys.exit(3)
            antworte("info string go " + " ".join(teile[1:]))
            antworte(f"info string elo {optionen['UCI_Elo']} limit {optionen['UCI_LimitStrength']}")
            zuege = sorted(board.legal_moves, key=lambda z: (not setzt_matt(board, z), z.uci()))
            for rang, zug in enumerate(zuege[: int(optionen["MultiPV"])], start=1):
                if setzt_matt(board, zug):
                    bewertung = "score mate 1 wdl 1000 0 0"
                else:
                    bewertung = "score cp 10 wdl 300 400 300"
                antworte(f"info depth 5 seldepth 5 multipv {rang} {bewertung} nodes 100 pv {zug.uci()}")
            antworte(f"bestmove {zuege[0].uci()}")
        elif befehl == "quit":
            return


if __name__ == "__main__":
    main()
