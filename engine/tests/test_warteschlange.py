import threading
import time

import pytest

from schach_engine.fehler import EngineFehler
from schach_engine.warteschlange import InferenzWarteschlange


@pytest.fixture
def warteschlange():
    erzeugt = []

    def bauen(max_laenge=4, frist_s=2.0):
        w = InferenzWarteschlange(max_laenge=max_laenge, frist_s=frist_s)
        w.starten()
        erzeugt.append(w)
        return w

    yield bauen
    for w in erzeugt:
        w.stoppen()


def test_liefert_das_ergebnis_der_aufgabe(warteschlange):
    assert warteschlange().ausfuehren(lambda: 42) == 42


def test_reicht_fehler_der_aufgabe_durch(warteschlange):
    def kaputt():
        raise ValueError("kaputt")

    with pytest.raises(ValueError):
        warteschlange().ausfuehren(kaputt)


def test_alle_aufgaben_laufen_auf_einem_einzigen_thread(warteschlange):
    w = warteschlange(max_laenge=16)
    threads = set()
    ergebnisse = []

    def anfrage():
        ergebnisse.append(w.ausfuehren(lambda: threads.add(threading.get_ident()) or 1))

    anfragen = [threading.Thread(target=anfrage) for _ in range(8)]
    for t in anfragen:
        t.start()
    for t in anfragen:
        t.join()

    assert ergebnisse == [1] * 8
    assert len(threads) == 1
    assert threading.get_ident() not in threads


def test_volle_warteschlange_ist_queue_full(warteschlange):
    w = warteschlange(max_laenge=1, frist_s=5.0)
    frei = threading.Event()
    laeuft = threading.Event()

    def blockiert():
        laeuft.set()
        frei.wait(5)
        return "fertig"

    erste = threading.Thread(target=lambda: w.ausfuehren(blockiert))
    erste.start()
    laeuft.wait(5)
    zweite = threading.Thread(target=lambda: w.ausfuehren(lambda: "wartet"))
    zweite.start()
    time.sleep(0.05)  # die zweite Aufgabe belegt den einzigen Warteplatz

    with pytest.raises(EngineFehler) as fehler:
        w.ausfuehren(lambda: "zu viel")

    frei.set()
    erste.join()
    zweite.join()
    assert (fehler.value.status, fehler.value.code) == (503, "queue_full")
    assert fehler.value.detail == "maia queue at capacity (1)"


def test_ueberschrittene_frist_ist_queue_full(warteschlange):
    w = warteschlange(frist_s=0.1)
    frei = threading.Event()

    with pytest.raises(EngineFehler) as fehler:
        w.ausfuehren(lambda: frei.wait(5))

    frei.set()
    assert (fehler.value.status, fehler.value.code) == (503, "queue_full")
    assert "deadline" in fehler.value.detail


def test_abgelaufene_aufgaben_werden_nicht_mehr_gerechnet(warteschlange):
    w = warteschlange(frist_s=0.1)
    frei = threading.Event()
    gerechnet = []

    blocker = threading.Thread(target=lambda: _ignoriere(lambda: w.ausfuehren(lambda: frei.wait(5))))
    blocker.start()
    time.sleep(0.02)
    with pytest.raises(EngineFehler):
        w.ausfuehren(lambda: gerechnet.append("spaet"))

    frei.set()
    blocker.join()
    assert w.ausfuehren(lambda: "danach") == "danach"
    assert gerechnet == []


def _ignoriere(aufruf):
    try:
        aufruf()
    except EngineFehler:
        pass
