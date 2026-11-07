import hashlib

import pytest

from schach_engine.modell_download import ModellQuelle, lade_modell

INHALT = b"maia3 gewichte (fake)"


def quelle(tmp_path, inhalt=INHALT, sha256=None):
    datei = tmp_path / "quelle" / "maia3-5m.pt"
    datei.parent.mkdir()
    datei.write_bytes(inhalt)
    return ModellQuelle(
        url=datei.as_uri(),
        dateiname="maia3-5m.pt",
        sha256=sha256 or hashlib.sha256(inhalt).hexdigest(),
    )


def test_laedt_das_modell_ins_volume(tmp_path):
    ziel = tmp_path / "models"

    pfad = lade_modell(quelle(tmp_path), ziel)

    assert pfad == ziel / "maia3-5m.pt"
    assert pfad.read_bytes() == INHALT


def test_vorhandenes_modell_mit_passender_pruefsumme_wird_nicht_neu_geladen(tmp_path):
    ziel = tmp_path / "models"
    q = quelle(tmp_path)
    lade_modell(q, ziel)
    (tmp_path / "quelle" / "maia3-5m.pt").unlink()  # ein zweiter Download wuerde scheitern

    assert lade_modell(q, ziel).read_bytes() == INHALT


def test_falsche_pruefsumme_bricht_ab_und_hinterlaesst_nichts(tmp_path):
    ziel = tmp_path / "models"

    with pytest.raises(ValueError, match="sha256"):
        lade_modell(quelle(tmp_path, sha256="0" * 64), ziel)

    assert list(ziel.iterdir()) == []


def test_beschaedigte_datei_im_volume_wird_ersetzt(tmp_path):
    ziel = tmp_path / "models"
    ziel.mkdir()
    (ziel / "maia3-5m.pt").write_bytes(b"halb geladen")

    assert lade_modell(quelle(tmp_path), ziel).read_bytes() == INHALT


def test_standardquelle_ist_maia3_5m_mit_gepinnter_revision():
    q = ModellQuelle.maia3_5m()

    assert q.dateiname == "maia3-5m.pt"
    assert q.url.startswith("https://huggingface.co/UofTCSSLab/Maia3-5M/resolve/")
    assert "/resolve/main/" not in q.url
    assert len(q.sha256) == 64
