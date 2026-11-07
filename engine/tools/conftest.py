import os
import socket
import threading
import time

import pytest

# Die Werkzeug-Tests laufen gegen den Engine-Dienst mit Fixture-Kernen (SCHACH-SPEC 15.2).
os.environ.setdefault("SCHACH_ENGINE_FAKE_MODEL", "1")


@pytest.fixture(scope="session")
def engine_url():
    """Echter HTTP-Dienst (uvicorn im Thread) mit Fake-Modell, wie ihn die Werkzeuge ansprechen."""
    import uvicorn

    from schach_engine.app import create_app

    with socket.socket() as sock:
        sock.bind(("127.0.0.1", 0))
        port = sock.getsockname()[1]
    server = uvicorn.Server(uvicorn.Config(create_app(), host="127.0.0.1", port=port, log_level="warning"))
    thread = threading.Thread(target=server.run, daemon=True)
    thread.start()
    url = f"http://127.0.0.1:{port}"

    import urllib.request

    ende = time.monotonic() + 10
    while time.monotonic() < ende:
        try:
            with urllib.request.urlopen(url + "/healthz", timeout=1) as antwort:
                if antwort.status == 200:
                    break
        except OSError:
            pass
        time.sleep(0.05)
    else:
        raise RuntimeError("engine did not become healthy")
    yield url
    server.should_exit = True
    thread.join(timeout=5)
