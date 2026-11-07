# Laden und Abfragen von Maia3-5M, abgeleitet aus CSSLab/maia3, Dateien maia3/uci.py
# (`load_model`, `Maia3UCIEngine.score_moves`, `wdl_from_value_logits`) und maia3/model_registry.py
# (Preset `maia3-5m`), Commit 1e13597c42d4858b7cfd7cfdae01e297263364b2, Lizenz AGPL-3.0
# (https://github.com/CSSLab/maia3).
#
# Geaendert fuer schach-engine (2026-11-07):
# - Kein UCI und kein Ziehen: `bewerte` liefert die Policy ueber alle erlaubten Zuege und die WDL
#   der Stellung aus Sicht der Seite am Zug, beides aus einem Vorwaertslauf.
# - Checkpoint immer mit `weights_only=True` und `strict=True` geladen.
# - Zughistorie immer aus der Zugfolge (wie `--use-uci-history`).

from dataclasses import dataclass
from pathlib import Path

import chess
import torch

from schach_engine.maia3.models import MAIA3Model
from schach_engine.maia3.tokens import (
    get_all_possible_moves,
    get_historical_tokens,
    get_legal_moves_mask,
    mirror_move,
    positionen,
)


@dataclass(frozen=True)
class Maia3Config:
    """Preset `maia3-5m` aus maia3/model_registry.py."""

    history: int = 8
    use_padding: bool = True
    include_time_info: bool = False
    dim_emb: int = 128
    num_blocks: int = 8
    mlp_ratio: float = 2.0
    dropout: float = 0.0
    use_gab: bool = True
    use_relative_bias: bool = False
    use_absolute_pe: bool = False
    use_rms_norm: bool = True
    omit_qkv_biases: bool = True
    activation: str = "gelu"
    dim_vit: int = 256
    head_hid_dim: int = 256
    num_heads: int = 8
    gab_gen_size: int = 64
    gab_per_square_dim: int = 0
    gab_intermediate_dim: int = 64


class Maia3Netz:
    def __init__(self, checkpoint: Path, cfg: Maia3Config | None = None) -> None:
        self.cfg = cfg or Maia3Config()
        self.model = MAIA3Model(self.cfg)
        ckpt = torch.load(checkpoint, map_location="cpu", weights_only=True)
        state_dict = ckpt["model_state_dict"] if isinstance(ckpt, dict) and "model_state_dict" in ckpt else ckpt
        # Aeltere Checkpoints nennen GAB noch "smolgen".
        self.model.load_state_dict({k.replace("smolgen", "gab"): v for k, v in state_dict.items()}, strict=True)
        self.model.eval()
        self.alle_zuege = get_all_possible_moves()
        self.zug_index = {uci: i for i, uci in enumerate(self.alle_zuege)}

    @torch.inference_mode()
    def bewerte(
        self, board: chess.Board, self_elo: int, oppo_elo: int
    ) -> tuple[list[tuple[str, float]], tuple[float, float, float]]:
        """Policy `[(uci, p)]` ueber alle erlaubten Zuege (Summe 1) und `(w, d, l)` fuer die Seite am Zug."""
        tokens = get_historical_tokens(positionen(board, self.cfg.history), self.cfg.history).unsqueeze(0)
        self_elos = torch.tensor([self_elo], dtype=torch.long)
        oppo_elos = torch.tensor([oppo_elo], dtype=torch.long)
        logits_move, logits_value, _ = self.model(tokens, self_elos, oppo_elos)

        maske = get_legal_moves_mask(board, self.zug_index)
        logits = logits_move[0].float().masked_fill(~maske, float("-inf"))
        p = torch.softmax(logits, dim=-1)

        policy = []
        for zug in board.legal_moves:
            uci = zug.uci()
            schluessel = uci if board.turn == chess.WHITE else mirror_move(uci)
            policy.append((uci, float(p[self.zug_index[schluessel]])))

        # Wertkopf: [Verlust, Remis, Gewinn] fuer die Seite am Zug.
        verlust, remis, gewinn = torch.softmax(logits_value[0].float(), dim=-1).tolist()
        return policy, (gewinn, remis, verlust)
