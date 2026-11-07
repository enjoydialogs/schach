"""Download der gepinnten Engine-Binaries fuer das Image (engine/docker/hole_binaries.py)."""

import hashlib
import importlib.util
import io
import stat
import sys
import tarfile
from pathlib import Path

import pytest

_PFAD = Path(__file__).resolve().parents[1] / "docker" / "hole_binaries.py"
_spec = importlib.util.spec_from_file_location("hole_binaries", _PFAD)
hole_binaries = importlib.util.module_from_spec(_spec)
sys.modules["hole_binaries"] = hole_binaries
_spec.loader.exec_module(hole_binaries)


def sha(daten):
    return hashlib.sha256(daten).hexdigest()


def tar_gz(mitglieder):
    puffer = io.BytesIO()
    with tarfile.open(fileobj=puffer, mode="w:gz") as tar:
        for name, daten in mitglieder.items():
            info = tarfile.TarInfo(name)
            info.size = len(daten)
            info.mode = 0o755
            tar.addfile(info, io.BytesIO(daten))
    return puffer.getvalue()


@pytest.mark.parametrize("arch", ["x86-64-avx2", "x86-64-avx512"])
def test_jede_unterstuetzte_arch_liefert_stockfish_und_fairy(arch):
    namen = [q.name for q in hole_binaries.quellen(arch)]

    assert namen == ["stockfish", "fairy-stockfish"]


def test_alle_quellen_sind_mit_version_und_sha256_gepinnt():
    for arch in hole_binaries.FAIRY_JE_ARCH:
        for q in hole_binaries.quellen(arch):
            assert q.url.startswith("https://github.com/")
            assert "/latest/" not in q.url
            assert len(q.sha256) == 64
            int(q.sha256, 16)


def test_stockfish_ist_version_19():
    assert "/download/sf_19/" in hole_binaries.STOCKFISH.url


def test_unbekannte_arch_bricht_ab():
    with pytest.raises(ValueError, match="SF_ARCH"):
        hole_binaries.quellen("armv7")


def test_einzelne_datei_wird_geprueft_und_ausfuehrbar_abgelegt(tmp_path):
    daten = b"\x7fELF fairy"
    (tmp_path / "fairy").write_bytes(daten)
    q = hole_binaries.Quelle("fairy-stockfish", (tmp_path / "fairy").as_uri(), sha(daten))

    pfad = hole_binaries.hole(q, tmp_path / "bin")

    assert pfad == tmp_path / "bin" / "fairy-stockfish"
    assert pfad.read_bytes() == daten
    if sys.platform != "win32":
        assert pfad.stat().st_mode & stat.S_IXUSR


def test_binary_wird_aus_dem_archiv_geholt(tmp_path):
    archiv = tar_gz({"stockfish/README.md": b"doc", "stockfish/stockfish-linux-x86-64-universal": b"\x7fELF sf"})
    (tmp_path / "sf.tar.gz").write_bytes(archiv)
    q = hole_binaries.Quelle("stockfish", (tmp_path / "sf.tar.gz").as_uri(), sha(archiv),
                             archiv_mitglied="stockfish-linux-x86-64-universal")

    assert hole_binaries.hole(q, tmp_path / "bin").read_bytes() == b"\x7fELF sf"


def test_falsche_pruefsumme_bricht_ab(tmp_path):
    (tmp_path / "fairy").write_bytes(b"manipuliert")
    q = hole_binaries.Quelle("fairy-stockfish", (tmp_path / "fairy").as_uri(), "0" * 64)

    with pytest.raises(ValueError, match="sha256"):
        hole_binaries.hole(q, tmp_path / "bin")

    assert not (tmp_path / "bin" / "fairy-stockfish").exists()


def test_fehlendes_archivmitglied_bricht_ab(tmp_path):
    archiv = tar_gz({"stockfish/anders": b"x"})
    (tmp_path / "sf.tar.gz").write_bytes(archiv)
    q = hole_binaries.Quelle("stockfish", (tmp_path / "sf.tar.gz").as_uri(), sha(archiv),
                             archiv_mitglied="stockfish-linux-x86-64-universal")

    with pytest.raises(ValueError, match="stockfish/anders"):
        hole_binaries.hole(q, tmp_path / "bin")
