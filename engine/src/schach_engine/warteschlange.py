"""Eine Inferenz-Warteschlange mit genau einem Worker-Thread (SCHACH-SPEC 5.2).

Maia rechnet in einem Prozess auf `MAIA_TORCH_THREADS` Kernen. Mehr gleichzeitige Vorwaertslaeufe
bringen auf der CPU nichts, darum laufen alle Anfragen nacheinander auf einem Thread. Warten
hoechstens `MAIA_QUEUE_MAX` Aufgaben, wird die naechste sofort mit 503 `queue_full` abgelehnt.
Jede Aufgabe hat eine Frist (2 s ab Einreihen); danach antwortet der Aufrufer 503 `queue_full`,
und der Worker rechnet die abgelaufene Aufgabe nicht mehr.
"""

import queue
import threading
import time
from collections.abc import Callable
from concurrent.futures import Future
from concurrent.futures import TimeoutError as FutureTimeout
from typing import TypeVar

from schach_engine.fehler import EngineFehler

T = TypeVar("T")

_ENDE = object()


class InferenzWarteschlange:
    def __init__(self, max_laenge: int, frist_s: float, name: str = "maia-inferenz") -> None:
        self._max_laenge = max_laenge
        self._frist_s = frist_s
        self._schlange: queue.Queue[object] = queue.Queue(maxsize=max_laenge)
        self._stopp = threading.Event()
        self._thread = threading.Thread(target=self._lauf, name=name, daemon=True)

    def starten(self) -> None:
        self._thread.start()

    def stoppen(self) -> None:
        self._stopp.set()
        try:
            self._schlange.put_nowait(_ENDE)
        except queue.Full:
            pass  # der Worker sieht `_stopp` nach der laufenden Aufgabe
        self._thread.join(timeout=5)

    def ausfuehren(self, aufgabe: Callable[[], T]) -> T:
        """Rechnet `aufgabe` auf dem Worker-Thread und wartet hoechstens bis zur Frist."""
        frist = time.monotonic() + self._frist_s
        zukunft: Future[T] = Future()
        try:
            self._schlange.put_nowait((zukunft, aufgabe, frist))
        except queue.Full:
            raise EngineFehler(503, "queue_full", f"maia queue at capacity ({self._max_laenge})") from None
        try:
            return zukunft.result(timeout=max(0.0, frist - time.monotonic()))
        except FutureTimeout:
            zukunft.cancel()
            raise EngineFehler(503, "queue_full", f"maia deadline {self._frist_s:g} s exceeded") from None

    def _lauf(self) -> None:
        while not self._stopp.is_set():
            eintrag = self._schlange.get()
            if eintrag is _ENDE:
                return
            zukunft, aufgabe, frist = eintrag  # type: ignore[misc]
            if time.monotonic() >= frist:
                zukunft.cancel()
            if not zukunft.set_running_or_notify_cancel():
                continue
            try:
                zukunft.set_result(aufgabe())
            except BaseException as fehler:  # noqa: BLE001 - geht an den wartenden Aufrufer
                zukunft.set_exception(fehler)
