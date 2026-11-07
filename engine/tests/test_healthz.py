def test_healthz_meldet_ok_sobald_das_fake_modell_geladen_ist(client):
    antwort = client.get("/healthz")

    assert antwort.status_code == 200
    assert antwort.json() == {"status": "ok"}
