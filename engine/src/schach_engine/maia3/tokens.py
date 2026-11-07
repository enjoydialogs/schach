# Eingabe und Zugvokabular von Maia-3, uebernommen aus CSSLab/maia3, Dateien maia3/dataset.py und
# maia3/utils.py, Commit 1e13597c42d4858b7cfd7cfdae01e297263364b2, Lizenz AGPL-3.0
# (https://github.com/CSSLab/maia3).
#
# Geaendert fuer schach-engine (2026-11-07):
# - Nur die Teile fuer die Inferenz: `tokenize_board`, `get_legal_moves_mask`,
#   `get_historical_tokens`, `get_all_possible_moves`, `mirror_move`.
# - `get_historical_tokens` ohne Zeitangaben (Maia3-5M: `include_time_info=False`), die Spalte fuer
#   `clk_ponder` bleibt mit 0 belegt wie im Original beim UCI-Betrieb.
# - `positionen` (neu) baut die Zughistorie aus `board.move_stack` wie `--use-uci-history`.

from collections.abc import Sequence

import chess
import torch

PIECE_MAP = {
    chess.PAWN: 1,
    chess.KNIGHT: 2,
    chess.BISHOP: 3,
    chess.ROOK: 4,
    chess.QUEEN: 5,
    chess.KING: 6,
}


def get_all_possible_moves() -> list[str]:
    all_moves = []
    for rank in range(8):
        for file in range(8):
            square = chess.square(file, rank)
            for target_rank in range(8):
                for target_file in range(8):
                    target_square = chess.square(target_file, target_rank)
                    all_moves.append(chess.square_name(square) + chess.square_name(target_square))

    # Umwandlungen immer von Reihe 7 nach 8: fuer Schwarz am Zug wird das Brett gespiegelt.
    for file_from in "abcdefgh":
        for file_to in "abcdefgh":
            for piece in ["q", "r", "b", "n"]:
                all_moves.append(f"{file_from}7{file_to}8{piece}")

    return all_moves


def mirror_square(square: str) -> str:
    return square[0] + str(9 - int(square[1]))


def mirror_move(move_uci: str) -> str:
    return mirror_square(move_uci[:2]) + mirror_square(move_uci[2:4]) + move_uci[4:]


def tokenize_board(board: chess.Board) -> torch.Tensor:
    tokens = torch.zeros((64, 12), dtype=torch.float)

    if board.turn == chess.BLACK:
        board = board.mirror()

    for square in chess.SQUARES:
        piece = board.piece_at(square)
        if piece:
            mapped = PIECE_MAP[piece.piece_type]
            token = mapped + (6 if piece.color == chess.BLACK else 0)
            tokens[square][token - 1] = 1

    return tokens


def get_legal_moves_mask(board: chess.Board, all_moves_dict: dict[str, int]) -> torch.Tensor:
    legal_moves_mask = torch.zeros((len(all_moves_dict),), dtype=torch.bool)
    for legal_move in board.legal_moves:
        move_uci = legal_move.uci() if board.turn == chess.WHITE else mirror_move(legal_move.uci())
        if move_uci in all_moves_dict:
            legal_moves_mask[all_moves_dict[move_uci]] = 1
    return legal_moves_mask


def get_historical_tokens(board_history: Sequence[torch.Tensor], history: int) -> torch.Tensor:
    """(64, 12 * history + 1): bei kurzer Historie mit der fruehesten Stellung aufgefuellt."""
    historical_tokens = torch.cat(list(board_history), dim=1)
    if len(board_history) < history:
        pad = board_history[0].repeat(1, history - len(board_history))
        historical_tokens = torch.cat([pad, historical_tokens], dim=1)
    return torch.cat([historical_tokens, torch.zeros((64, 1))], dim=1)


def positionen(board: chess.Board, history: int) -> list[torch.Tensor]:
    """Die letzten `history` Stellungen bis einschliesslich `board`, aelteste zuerst."""
    replay = board.root()
    erste = len(board.move_stack) + 1 - history  # Index der aeltesten benoetigten Stellung
    stellungen = [tokenize_board(replay)] if erste <= 0 else []
    for index, move in enumerate(board.move_stack, start=1):
        replay.push(move)
        if index >= erste:
            stellungen.append(tokenize_board(replay))
    return stellungen
