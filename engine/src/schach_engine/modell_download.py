"""Download der Maia-3-Gewichte beim ersten Start (SCHACH-SPEC 5.2, 13.6).

Die Gewichte liegen nie im Repo und nie im Image. Beim Start laedt der Dienst `maia3-5m.pt` von
Hugging Face in `ENGINE_MODELS_DIR` (Volume `engine-models`) und prueft die sha256. Revision und
Pruefsumme sind gepinnt; ein neues Modell ist eine bewusste Codeaenderung.
"""

import hashlib
import logging
import os
import shutil
import tempfile
import urllib.request
from dataclasses import dataclass
from pathlib import Path

log = logging.getLogger("schach_engine")

# Hugging Face UofTCSSLab/Maia3-5M, Stand 2026-05-23 (lastModified), Datei 20.968.049 Byte.
MAIA3_5M_REPO = "UofTCSSLab/Maia3-5M"
MAIA3_5M_REVISION = "b6559de2398d7140b985f28fd2c19fb5e47ddabe"
MAIA3_5M_DATEI = "maia3-5m.pt"
MAIA3_5M_SHA256 = "ba14208b2992d85502f5fb501934abf6aaaeb355e9f3fdf90e326911f562524f"

_BLOCK = 1 << 20


@dataclass(frozen=True)
class ModellQuelle:
    url: str
    dateiname: str
    sha256: str

    @classmethod
    def maia3_5m(cls) -> "ModellQuelle":
        return cls(
            url=f"https://huggingface.co/{MAIA3_5M_REPO}/resolve/{MAIA3_5M_REVISION}/{MAIA3_5M_DATEI}",
            dateiname=MAIA3_5M_DATEI,
            sha256=MAIA3_5M_SHA256,
        )


def _sha256(pfad: Path) -> str:
    summe = hashlib.sha256()
    with pfad.open("rb") as datei:
        while block := datei.read(_BLOCK):
            summe.update(block)
    return summe.hexdigest()


def lade_modell(quelle: ModellQuelle, ziel_dir: Path, timeout_s: float = 60.0) -> Path:
    """Liefert den Pfad der geprueften Gewichte; laedt sie, falls sie fehlen oder nicht passen."""
    ziel_dir.mkdir(parents=True, exist_ok=True)
    ziel = ziel_dir / quelle.dateiname
    if ziel.is_file() and _sha256(ziel) == quelle.sha256:
        return ziel

    log.info("downloading %s", quelle.dateiname)
    fd, tmp_name = tempfile.mkstemp(prefix=".download-", dir=ziel_dir)
    tmp = Path(tmp_name)
    try:
        with os.fdopen(fd, "wb") as aus, urllib.request.urlopen(quelle.url, timeout=timeout_s) as antwort:  # noqa: S310
            shutil.copyfileobj(antwort, aus, _BLOCK)
        ist = _sha256(tmp)
        if ist != quelle.sha256:
            raise ValueError(f"sha256 mismatch for {quelle.dateiname}: expected {quelle.sha256}, got {ist}")
        os.replace(tmp, ziel)  # atomar: ein abgebrochener Download hinterlaesst keine halbe Datei
    finally:
        tmp.unlink(missing_ok=True)
    log.info("model %s ready", quelle.dateiname)
    return ziel
