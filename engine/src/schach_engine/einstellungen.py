"""Einstellungen aus der Umgebung (SCHACH-SPEC 5.2, engine/API.md „Umgebung“)."""

import os
from dataclasses import dataclass, field
from pathlib import Path

ALLE_ROLLEN = frozenset({"maia", "stockfish", "fairy"})


def _ganzzahl(name: str, vorgabe: int, minimum: int = 1) -> int:
    roh = os.environ.get(name, "").strip()
    if not roh:
        return vorgabe
    wert = int(roh)
    if wert < minimum:
        raise ValueError(f"{name} must be >= {minimum}")
    return wert


def _rollen() -> frozenset[str]:
    roh = os.environ.get("ENGINE_ROLES")
    if roh is None:
        return ALLE_ROLLEN
    return frozenset(r.strip() for r in roh.split(",") if r.strip())


@dataclass(frozen=True)
class Einstellungen:
    maia_torch_threads: int = 1
    maia_queue_max: int = 32
    maia_frist_s: float = 2.0  # API.md: Frist 2 s, danach 503 queue_full
    sf_pool_size: int = 2
    sf_max_movetime_ms: int = 800
    sf_wartegrenze_s: float = 3.0  # API.md: Wartegrenze 3 s, danach 503 pool_busy
    fairy_pool_size: int = 1
    fairy_movetime_ms: int = 100
    rollen: frozenset[str] = field(default=ALLE_ROLLEN)
    fake_model: bool = False
    models_dir: Path = Path("/models")
    stockfish_pfad: str = "/usr/local/bin/stockfish"
    fairy_pfad: str = "/usr/local/bin/fairy-stockfish"
    start_pause_s: float = 5.0  # erste Pause vor einem neuen Startversuch, verdoppelt bis 60 s

    @classmethod
    def aus_env(cls) -> "Einstellungen":
        return cls(
            maia_torch_threads=_ganzzahl("MAIA_TORCH_THREADS", 1),
            maia_queue_max=_ganzzahl("MAIA_QUEUE_MAX", 32),
            sf_pool_size=_ganzzahl("SF_POOL_SIZE", 2),
            sf_max_movetime_ms=_ganzzahl("SF_MAX_MOVETIME_MS", 800),
            fairy_pool_size=_ganzzahl("FAIRY_POOL_SIZE", 1),
            rollen=_rollen(),
            fake_model=os.environ.get("SCHACH_ENGINE_FAKE_MODEL", "").strip() == "1",
            models_dir=Path(os.environ.get("ENGINE_MODELS_DIR", "/models")),
            stockfish_pfad=os.environ.get("STOCKFISH_PATH", "/usr/local/bin/stockfish"),
            fairy_pfad=os.environ.get("FAIRY_STOCKFISH_PATH", "/usr/local/bin/fairy-stockfish"),
        )
