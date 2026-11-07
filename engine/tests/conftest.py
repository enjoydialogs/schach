import os

import pytest
from fastapi.testclient import TestClient

# SCHACH-SPEC 15.2: pytest laeuft ohne echtes Modell.
os.environ.setdefault("SCHACH_ENGINE_FAKE_MODEL", "1")

from schach_engine.app import create_app  # noqa: E402


@pytest.fixture
def client():
    with TestClient(create_app()) as c:
        yield c
