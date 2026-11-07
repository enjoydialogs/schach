"""Laedt Stockfish und Fairy-Stockfish fuer das Image, gepinnt auf Version und sha256 (SCHACH-SPEC 5.2).

Aufruf im Dockerfile: `python hole_binaries.py --arch "$SF_ARCH" --ziel /opt/engines`.

- Stockfish 19 erscheint nur noch als Universal-Binary: es erkennt die CPU zur Laufzeit und nutzt
  dann AVX2, AVX-512 usw. Ein Binary fuer alle `SF_ARCH`.
- Fairy-Stockfish 14 (letztes Release) gibt es je CPU-Stufe. `SF_ARCH` waehlt die Stufe; AVX2 und
  AVX-512 haben beide BMI2, also das bmi2-Build.

Neue Version: URL und sha256 hier aendern (sha256 aus der GitHub-Release-Seite bzw. per sha256sum).
Lizenz der Binaries: GPL-3.0, Quellen unter den Tags `sf_19` und `fairy_sf_14` (engine/README.md).
"""

import argparse
import hashlib
import os
import shutil
import stat
import sys
import tarfile
import tempfile
import urllib.request
from dataclasses import dataclass
from pathlib import Path

_BLOCK = 1 << 20


@dataclass(frozen=True)
class Quelle:
    name: str
    url: str
    sha256: str
    archiv_mitglied: str | None = None  # Dateiname des Binarys im .tar.gz, sonst ist die URL das Binary


STOCKFISH = Quelle(
    name="stockfish",
    url="https://github.com/official-stockfish/Stockfish/releases/download/sf_19/stockfish-linux-x86-64-universal.tar.gz",
    sha256="9defc0d4e55d49c65a6d042f3e571a39fcea499ade6dbe741b53b8c65e03611f",
    archiv_mitglied="stockfish-linux-x86-64-universal",
)

_FAIRY_BMI2 = Quelle(
    name="fairy-stockfish",
    url="https://github.com/fairy-stockfish/Fairy-Stockfish/releases/download/fairy_sf_14/fairy-stockfish_x86-64-bmi2",
    sha256="9c8ff22d7474100cf067a8f2a83b278619369997aa9d0e69284c465f160e3419",
)

FAIRY_JE_ARCH = {
    "x86-64-avx2": _FAIRY_BMI2,
    "x86-64-avx512": _FAIRY_BMI2,
}


def quellen(sf_arch: str) -> list[Quelle]:
    if sf_arch not in FAIRY_JE_ARCH:
        raise ValueError(f"SF_ARCH {sf_arch!r} not supported, use one of {', '.join(FAIRY_JE_ARCH)}")
    return [STOCKFISH, FAIRY_JE_ARCH[sf_arch]]


def _sha256(pfad: Path) -> str:
    summe = hashlib.sha256()
    with pfad.open("rb") as datei:
        while block := datei.read(_BLOCK):
            summe.update(block)
    return summe.hexdigest()


def hole(quelle: Quelle, ziel_dir: Path) -> Path:
    """Laedt `quelle`, prueft die sha256 und legt das Binary ausfuehrbar unter `ziel_dir/name` ab."""
    ziel_dir.mkdir(parents=True, exist_ok=True)
    ziel = ziel_dir / quelle.name
    with tempfile.TemporaryDirectory() as tmp:
        download = Path(tmp) / "download"
        with urllib.request.urlopen(quelle.url, timeout=120) as antwort, download.open("wb") as aus:  # noqa: S310
            shutil.copyfileobj(antwort, aus, _BLOCK)
        ist = _sha256(download)
        if ist != quelle.sha256:
            raise ValueError(f"sha256 mismatch for {quelle.url}: expected {quelle.sha256}, got {ist}")
        if quelle.archiv_mitglied is None:
            shutil.copyfile(download, ziel)
        else:
            with tarfile.open(download, mode="r:gz") as tar:
                treffer = [m for m in tar.getmembers() if m.isfile() and Path(m.name).name == quelle.archiv_mitglied]
                if len(treffer) != 1:
                    namen = ", ".join(m.name for m in tar.getmembers())
                    raise ValueError(f"{quelle.archiv_mitglied} not found exactly once in archive: {namen}")
                quelle_datei = tar.extractfile(treffer[0])
                assert quelle_datei is not None
                with quelle_datei, ziel.open("wb") as aus:
                    shutil.copyfileobj(quelle_datei, aus, _BLOCK)
    os.chmod(ziel, os.stat(ziel).st_mode | stat.S_IXUSR | stat.S_IXGRP | stat.S_IXOTH)
    return ziel


def main(argv: list[str] | None = None) -> int:
    parser = argparse.ArgumentParser(description=__doc__.splitlines()[0])
    parser.add_argument("--arch", required=True, help="SF_ARCH, z. B. x86-64-avx2")
    parser.add_argument("--ziel", required=True, type=Path)
    args = parser.parse_args(argv)
    for quelle in quellen(args.arch):
        print(f"{quelle.name}: {quelle.url}", flush=True)
        hole(quelle, args.ziel)
    return 0


if __name__ == "__main__":
    sys.exit(main())
