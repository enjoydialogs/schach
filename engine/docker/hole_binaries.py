"""Laedt Stockfish und Fairy-Stockfish fuer das Image, gepinnt auf Version und sha256 (SCHACH-SPEC 5.2).

Aufruf im Dockerfile: `python hole_binaries.py --arch "$SF_ARCH" --ziel /opt/engines --doc /opt/doc`.

Mit `--doc` legt es die GPL-3.0-Weitergabepapiere (GPL-3.0 Abschnitt 6) ab: `COPYING` (der Lizenztext
`Copying.txt` aus dem Stockfish-Archiv) und `NOTICES.md` (Versionen, Quellen, sha256). Die Platzhalter
`@COMMIT_SHA@` und `@TORCH_VERSION@` ersetzt erst die Laufzeit-Stufe, damit diese Stufe im Cache bleibt.

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
    tag: str = ""
    lizenz_mitglied: str | None = None  # Lizenztext im .tar.gz, landet als COPYING im Doku-Verzeichnis


STOCKFISH = Quelle(
    name="stockfish",
    url="https://github.com/official-stockfish/Stockfish/releases/download/sf_19/stockfish-linux-x86-64-universal.tar.gz",
    sha256="9defc0d4e55d49c65a6d042f3e571a39fcea499ade6dbe741b53b8c65e03611f",
    archiv_mitglied="stockfish-linux-x86-64-universal",
    tag="sf_19",
    lizenz_mitglied="Copying.txt",
)

_FAIRY_BMI2 = Quelle(
    name="fairy-stockfish",
    url="https://github.com/fairy-stockfish/Fairy-Stockfish/releases/download/fairy_sf_14/fairy-stockfish_x86-64-bmi2",
    sha256="9c8ff22d7474100cf067a8f2a83b278619369997aa9d0e69284c465f160e3419",
    tag="fairy_sf_14",
)

# Stand von CSSLab/maia3, aus dem src/schach_engine/maia3/ abgeleitet ist (engine/README.md).
MAIA3_COMMIT = "1e13597c42d4858b7cfd7cfdae01e297263364b2"
ENGINE_QUELLE = "https://github.com/enjoydialogs/schach/tree/@COMMIT_SHA@/engine"

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


def _einmal(tar: tarfile.TarFile, dateiname: str) -> tarfile.TarInfo:
    treffer = [m for m in tar.getmembers() if m.isfile() and Path(m.name).name == dateiname]
    if len(treffer) != 1:
        namen = ", ".join(m.name for m in tar.getmembers())
        raise ValueError(f"{dateiname} not found exactly once in archive: {namen}")
    return treffer[0]


def _kopiere(tar: tarfile.TarFile, mitglied: tarfile.TarInfo, ziel: Path) -> None:
    quelle_datei = tar.extractfile(mitglied)
    assert quelle_datei is not None
    with quelle_datei, ziel.open("wb") as aus:
        shutil.copyfileobj(quelle_datei, aus, _BLOCK)


def hole(quelle: Quelle, ziel_dir: Path, doc_dir: Path | None = None) -> Path:
    """Laedt `quelle`, prueft die sha256 und legt das Binary ausfuehrbar unter `ziel_dir/name` ab.

    Mit `doc_dir` landet ein `lizenz_mitglied` des Archivs dort als `COPYING`.
    """
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
                _kopiere(tar, _einmal(tar, quelle.archiv_mitglied), ziel)
                if doc_dir is not None and quelle.lizenz_mitglied is not None:
                    doc_dir.mkdir(parents=True, exist_ok=True)
                    _kopiere(tar, _einmal(tar, quelle.lizenz_mitglied), doc_dir / "COPYING")
    os.chmod(ziel, os.stat(ziel).st_mode | stat.S_IXUSR | stat.S_IXGRP | stat.S_IXOTH)
    return ziel


def hinweise(sf_arch: str) -> str:
    """Text von NOTICES.md: was das Image weitergibt und wo die Quellen liegen (GPL-3.0 Abschnitt 6)."""
    zeilen = [
        "# schach-engine: Hinweise zur Weitergabe",
        "",
        "Dieses Image enthaelt Objektcode von Stockfish und Fairy-Stockfish (GPL-3.0, Lizenztext in",
        "`COPYING` neben dieser Datei). Der Engine-Dienst selbst steht unter AGPL-3.0-or-later (`/app/LICENSE`).",
        "",
        "## Engines (GPL-3.0)",
        "",
    ]
    for q in quellen(sf_arch):
        repo = q.url.split("/releases/")[0]
        zeilen += [
            f"- {q.name}, Tag `{q.tag}`",
            f"  - Binary: {q.url}",
            f"  - sha256: `{q.sha256}`",
            f"  - Quellcode (Corresponding Source): {repo}/tree/{q.tag}",
        ]
    zeilen += [
        "",
        "## Weitere Komponenten",
        "",
        f"- Maia-3: geaenderter Code aus https://github.com/CSSLab/maia3, Commit `{MAIA3_COMMIT}` (AGPL-3.0).",
        "  Die Gewichte sind nicht im Image.",
        "- PyTorch @TORCH_VERSION@ (CPU), BSD-3-Clause, https://github.com/pytorch/pytorch",
        "- python-chess, GPL-3.0-or-later, https://github.com/niklasf/python-chess",
        "",
        "## Quellcode des Engine-Dienstes",
        "",
        f"- {ENGINE_QUELLE}",
        "- Commit `@COMMIT_SHA@` (auch im Image-Label `org.opencontainers.image.revision`).",
        "",
    ]
    return "\n".join(zeilen)


def main(argv: list[str] | None = None) -> int:
    parser = argparse.ArgumentParser(description=__doc__.splitlines()[0])
    parser.add_argument("--arch", required=True, help="SF_ARCH, z. B. x86-64-avx2")
    parser.add_argument("--ziel", required=True, type=Path)
    parser.add_argument("--doc", type=Path, help="Ziel fuer COPYING und NOTICES.md")
    args = parser.parse_args(argv)
    for quelle in quellen(args.arch):
        print(f"{quelle.name}: {quelle.url}", flush=True)
        hole(quelle, args.ziel, args.doc)
    if args.doc is not None:
        if not (args.doc / "COPYING").is_file():
            raise ValueError("no licence text found for COPYING")
        (args.doc / "NOTICES.md").write_text(hinweise(args.arch), encoding="utf-8")
    return 0


if __name__ == "__main__":
    sys.exit(main())
