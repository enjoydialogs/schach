import os
import time

import pytest
from fastapi.testclient import TestClient

# SCHACH-SPEC 15.2: pytest laeuft ohne echtes Modell.
os.environ.setdefault("SCHACH_ENGINE_FAKE_MODEL", "1")

from schach_engine.app import create_app  # noqa: E402


def pytest_collection_modifyitems(config, items):
    # Modelltests laden Maia3-5M von Hugging Face und brauchen PyTorch; nur auf Wunsch.
    if os.environ.get("SCHACH_ENGINE_MODEL_TESTS") == "1":
        return
    ueberspringen = pytest.mark.skip(reason="SCHACH_ENGINE_MODEL_TESTS=1 not set")
    for item in items:
        if "model" in item.keywords:
            item.add_marker(ueberspringen)


@pytest.fixture
def client():
    with TestClient(create_app()) as c:
        # Die Kerne starten im Hintergrund; auch das Fake-Modell braucht einen Augenblick.
        ende = time.monotonic() + 5
        while c.get("/healthz").status_code != 200 and time.monotonic() < ende:
            time.sleep(0.01)
        yield c
