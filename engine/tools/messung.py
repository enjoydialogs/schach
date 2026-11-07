"""VPS-Messung Teil 2 (SCHACH-SPEC 15.4 Kriterium 5, 16 Punkt 1): Last wie im echten Spiel.

N parallele simulierte Partien gegen einen laufenden Engine-Dienst: der Gegner zieht ueber
`POST /move` (Maia, Leiter-Elo, Gast-Elo 800), der Spieler "denkt" und fragt mit `--tipp-rate`
einen Tipp ueber `POST /analyse` (800 ms, MultiPV 1). 503 auf `/move` wird wie im .NET-Client
wiederholt. Ausgabe: p50/p95 je Endpunkt, 503- und Fehlerquoten, Gegnerzug gesamt (inkl.
Wiederholungen), optional RSS ueber einen Befehl, optional Erststart (`/healthz` bis 200).

Laeuft mit Standardbibliothek plus `chess` (python-chess), z. B. in `python:3.12-slim`.
Anleitung: docs/engine-messung.md.
"""

import argparse
import json
import random
import shlex
import subprocess
import sys
import threading
import time
from collections import Counter
from collections.abc import Callable, Sequence
from dataclasses import asdict, dataclass, field
from pathlib import Path
from typing import Any

import chess
from engine_http import (
    GRUNDSTELLUNG,
    Antwort,
    HttpTransport,
    Transport,
    analyse_anfrage,
    move_anfrage,
)
from stil_referenz import GEGNER

GAST_ELO = 800
GRENZE_GEGNERZUG_MS = 1000  # Kriterium 5: p95 Gegnerzug (Engine-Anteil) <= 1 s
GRENZE_TIPP_MS = 1500  # Kriterium 5: Tipp p95 <= 1,5 s


@dataclass
class Konfig:
    partien: int = 10
    dauer_s: float | None = 120.0  # Ende nach dieser Zeit je Phase; None = nur `gegnerzuege`
    gegnerzuege: int | None = None  # Gegnerzuege je simulierter Partie-Schiene; None = unbegrenzt
    denkzeit_s: tuple[float, float] = (1.0, 3.0)  # Pause vor jedem Spielerzug
    tipp_rate: float = 0.15  # Anteil der Spielerzuege mit Tipp
    tipp_movetime_ms: int = 800  # Tipp:MovetimeMs
    tipp_multipv: int = 1
    max_halbzuege: int = 120  # danach neue Partie
    retry_pause_s: float = 0.5
    max_versuche: int = 5
    seed: int = 1
    phasen: tuple[str, ...] = ("gemischt",)  # "maia" = ohne Tipps, "gemischt" = mit `tipp_rate`


def perzentil(werte: Sequence[float], p: float) -> float | None:
    """Nearest-Rank-Perzentil; None ohne Werte."""
    if not werte:
        return None
    sortiert = sorted(werte)
    rang = max(1, -(-len(sortiert) * p // 100))  # ceil
    return sortiert[int(rang) - 1]


@dataclass
class _Messreihe:
    dauern_ms: list[float] = field(default_factory=list)  # nur erfolgreiche Anfragen
    n: int = 0
    fehler: Counter = field(default_factory=Counter)
    status_503: int = 0

    def zaehle(self, antwort: Antwort) -> None:
        self.n += 1
        if antwort.ok:
            self.dauern_ms.append(antwort.dauer_s * 1000)
        else:
            self.fehler[antwort.fehlercode] += 1
            if antwort.status == 503:
                self.status_503 += 1

    def bericht(self) -> dict[str, Any]:
        return {
            "n": self.n,
            "ok": len(self.dauern_ms),
            "p50_ms": _runde(perzentil(self.dauern_ms, 50)),
            "p95_ms": _runde(perzentil(self.dauern_ms, 95)),
            "max_ms": _runde(max(self.dauern_ms) if self.dauern_ms else None),
            "fehler": dict(self.fehler),
            "quote_503": self.status_503 / self.n if self.n else 0.0,
            "quote_fehler": (self.n - len(self.dauern_ms)) / self.n if self.n else 0.0,
        }


def _runde(wert: float | None) -> float | None:
    return None if wert is None else round(wert, 1)


class _Sammler:
    def __init__(self) -> None:
        self._lock = threading.Lock()
        self.move = _Messreihe()
        self.tipp = _Messreihe()
        self.gegnerzug_ms: list[float] = []
        self.gegnerzug_ausgefallen = 0

    def anfrage(self, reihe: _Messreihe, antwort: Antwort) -> None:
        with self._lock:
            reihe.zaehle(antwort)

    def gegnerzug(self, ms: float | None) -> None:
        with self._lock:
            if ms is None:
                self.gegnerzug_ausgefallen += 1
            else:
                self.gegnerzug_ms.append(ms)


def _gegnerzug(transport: Transport, k: Konfig, moves: list[str], gegner_elo: int, sammler: _Sammler, schlafen) -> dict | None:
    """Ein Gegnerzug mit Wiederholung bei 503; liefert die Antwort oder None."""
    gesamt_ms = 0.0
    for versuch in range(k.max_versuche):
        antwort = transport.post("/move", move_anfrage(moves, gegner_elo, GAST_ELO))
        sammler.anfrage(sammler.move, antwort)
        gesamt_ms += antwort.dauer_s * 1000
        if antwort.ok and antwort.daten:
            sammler.gegnerzug(gesamt_ms)
            return antwort.daten
        if antwort.status != 503 or versuch == k.max_versuche - 1:
            break
        schlafen(k.retry_pause_s)
        gesamt_ms += k.retry_pause_s * 1000
    sammler.gegnerzug(None)
    return None


def _schiene(transport: Transport, k: Konfig, nummer: int, tipp_rate: float, ende: float | None, sammler: _Sammler, schlafen) -> None:
    """Eine simulierte Partie-Schiene: spielt Partien nacheinander bis zum Ende der Phase."""
    rng = random.Random(k.seed * 1000 + nummer)
    gegner = GEGNER[nummer % len(GEGNER)]
    gezogen = 0

    def weiter() -> bool:
        if k.gegnerzuege is not None and gezogen >= k.gegnerzuege:
            return False
        return ende is None or time.monotonic() < ende

    while weiter():
        board = chess.Board(GRUNDSTELLUNG)
        moves: list[str] = []
        spieler_weiss = rng.random() < 0.5
        while weiter() and not board.is_game_over(claim_draw=True) and len(moves) < k.max_halbzuege:
            if board.turn == spieler_weiss:
                schlafen(rng.uniform(*k.denkzeit_s))
                zug = None
                if tipp_rate > 0 and rng.random() < tipp_rate:
                    antwort = transport.post("/analyse", analyse_anfrage(moves, k.tipp_movetime_ms, k.tipp_multipv))
                    sammler.anfrage(sammler.tipp, antwort)
                    if antwort.ok and antwort.daten and rng.random() < 0.5:
                        zug = chess.Move.from_uci(antwort.daten["bestmove"])
                if zug is None:
                    zug = rng.choice(sorted(board.legal_moves, key=lambda z: z.uci()))
            else:
                daten = _gegnerzug(transport, k, moves, gegner.elo, sammler, schlafen)
                gezogen += 1
                if daten is None:
                    break
                policy = daten["policy"]
                uci = rng.choices([e["uci"] for e in policy], weights=[e["p"] for e in policy])[0]
                zug = chess.Move.from_uci(uci)
            board.push(zug)
            moves.append(zug.uci())


def parse_mib(text: str) -> float | None:
    """Erste Mengenangabe in MiB: `512MiB / 2GiB` (docker stats), `1.2GB`, oder nackte Bytes."""
    einheiten = {
        "b": 1, "kib": 2**10, "mib": 2**20, "gib": 2**30,
        "kb": 1e3, "mb": 1e6, "gb": 1e9,
    }  # fmt: skip
    teile = text.strip().split()
    if not teile:
        return None
    roh = teile[0]
    zahl = roh.rstrip("abcdefghijklmnopqrstuvwxyzABCDEFGHIJKLMNOPQRSTUVWXYZ")
    einheit = roh[len(zahl) :].lower() or "b"
    try:
        return float(zahl) * einheiten[einheit] / 2**20
    except (ValueError, KeyError):
        return None


class RssProbe:
    """Fuehrt einen Befehl aus, der den Speicher des Engine-Containers ausgibt (siehe Doku)."""

    def __init__(self, befehl: str | Sequence[str]) -> None:
        self._befehl = befehl

    def lies(self) -> float | None:
        try:
            ergebnis = subprocess.run(
                self._befehl, shell=isinstance(self._befehl, str), capture_output=True, text=True, timeout=10, check=False
            )
        except (OSError, subprocess.TimeoutExpired):
            return None
        return parse_mib(ergebnis.stdout) if ergebnis.returncode == 0 else None


def _rss_sammeln(probe: RssProbe, intervall_s: float, stopp: threading.Event, proben: list[float]) -> None:
    while not stopp.is_set():
        wert = probe.lies()
        if wert is not None:
            proben.append(wert)
        stopp.wait(intervall_s)


def _phase(transport: Transport, k: Konfig, name: str, probe: RssProbe | None, rss_intervall_s: float, schlafen) -> dict[str, Any]:
    tipp_rate = 0.0 if name == "maia" else k.tipp_rate
    sammler = _Sammler()
    ende = None if k.dauer_s is None else time.monotonic() + k.dauer_s
    rss: list[float] = []
    stopp = threading.Event()
    rss_thread = None
    if probe is not None:
        rss_thread = threading.Thread(target=_rss_sammeln, args=(probe, rss_intervall_s, stopp, rss), daemon=True)
        rss_thread.start()
    start = time.monotonic()
    schienen = [
        threading.Thread(target=_schiene, args=(transport, k, i, tipp_rate, ende, sammler, schlafen), daemon=True)
        for i in range(k.partien)
    ]
    for t in schienen:
        t.start()
    for t in schienen:
        t.join()
    stopp.set()
    if rss_thread is not None:
        rss_thread.join(timeout=15)

    move = sammler.move.bericht()
    tipp = sammler.tipp.bericht()
    gz_p95 = perzentil(sammler.gegnerzug_ms, 95)
    return {
        "name": name,
        "tipp_rate": tipp_rate,
        "sekunden": round(time.monotonic() - start, 1),
        "endpunkte": {"move_maia": move, "analyse_tipp": tipp},
        "gegnerzug_gesamt": {
            "n": len(sammler.gegnerzug_ms),
            "ausgefallen": sammler.gegnerzug_ausgefallen,
            "p50_ms": _runde(perzentil(sammler.gegnerzug_ms, 50)),
            "p95_ms": _runde(gz_p95),
            "max_ms": _runde(max(sammler.gegnerzug_ms) if sammler.gegnerzug_ms else None),
        },
        "rss_mib": {
            "proben": len(rss),
            "max": _runde(max(rss) if rss else None),
            "letzter": _runde(rss[-1] if rss else None),
        },
        "kriterium5": {
            "gegnerzug_p95_ok": None if gz_p95 is None else gz_p95 <= GRENZE_GEGNERZUG_MS,
            "tipp_p95_ok": None if tipp["p95_ms"] is None else tipp["p95_ms"] <= GRENZE_TIPP_MS,
        },
    }


def messe(
    transport: Transport,
    k: Konfig,
    rss: RssProbe | None = None,
    rss_intervall_s: float = 2.0,
    schlafen: Callable[[float], None] = time.sleep,
) -> dict[str, Any]:
    konfig = asdict(k)
    return {
        "konfig": konfig,
        "phasen": [_phase(transport, k, name, rss, rss_intervall_s, schlafen) for name in k.phasen],
    }


def erststart(
    transport: Transport,
    intervall_s: float = 2.0,
    timeout_s: float = 900.0,
    schlafen: Callable[[float], None] = time.sleep,
    uhr: Callable[[], float] = time.monotonic,
) -> dict[str, Any]:
    """Zeit bis `/healthz` 200 meldet. Vor dem Containerstart aufrufen (Volume `engine-models` leer)."""
    start = uhr()
    versuche = 0
    while True:
        versuche += 1
        antwort = transport.get("/healthz")
        jetzt = uhr()
        if antwort.ok:
            return {"gesund": True, "sekunden": round(jetzt - start, 1), "versuche": versuche}
        if jetzt - start >= timeout_s:
            return {"gesund": False, "sekunden": round(jetzt - start, 1), "versuche": versuche}
        schlafen(intervall_s)


def _zelle(wert: Any) -> str:
    if wert is None:
        return "–"
    if isinstance(wert, bool):
        return "ja" if wert else "nein"
    if isinstance(wert, float):
        return f"{wert:.3f}" if wert < 1 else f"{wert:.1f}"
    return str(wert)


def als_markdown(bericht: dict[str, Any]) -> str:
    k = bericht["konfig"]
    zeilen = [
        (f"Partien parallel: {k['partien']}, Dauer je Phase: {_zelle(k['dauer_s'])} s, "
        f"Denkzeit {k['denkzeit_s'][0]}–{k['denkzeit_s'][1]} s, Tipp-Rate {k['tipp_rate']}, "
        f"Tipp {k['tipp_movetime_ms']} ms / MultiPV {k['tipp_multipv']}"),
        "",
    ]
    if "erststart" in bericht:
        e = bericht["erststart"]
        zeilen += [f"Erststart bis `/healthz` 200: {_zelle(e['sekunden'])} s (gesund: {_zelle(e['gesund'])})", ""]
    for phase in bericht["phasen"]:
        zeilen += [
            f"### Phase `{phase['name']}` (Tipp-Rate {phase['tipp_rate']}, {phase['sekunden']} s)",
            "",
            "| Endpunkt | n | ok | p50 ms | p95 ms | max ms | 503-Quote | Fehler |",
            "|---|---|---|---|---|---|---|---|",
        ]
        for name, e in phase["endpunkte"].items():
            fehler = ", ".join(f"{c} {n}" for c, n in sorted(e["fehler"].items())) or "–"
            zeilen.append(
                f"| {name} | {e['n']} | {e['ok']} | {_zelle(e['p50_ms'])} | {_zelle(e['p95_ms'])} | "
                f"{_zelle(e['max_ms'])} | {e['quote_503']:.3f} | {fehler} |"
            )
        g = phase["gegnerzug_gesamt"]
        zeilen.append(
            f"| Gegnerzug gesamt (inkl. Wiederholung) | {g['n'] + g['ausgefallen']} | {g['n']} | {_zelle(g['p50_ms'])} | "
            f"{_zelle(g['p95_ms'])} | {_zelle(g['max_ms'])} | – | ausgefallen {g['ausgefallen']} |"
        )
        r = phase["rss_mib"]
        k5 = phase["kriterium5"]
        zeilen += [
            "",
            f"RSS: max {_zelle(r['max'])} MiB, zuletzt {_zelle(r['letzter'])} MiB ({r['proben']} Proben)",
            "",
            (f"Kriterium 5: Gegnerzug p95 ≤ {GRENZE_GEGNERZUG_MS} ms: {_zelle(k5['gegnerzug_p95_ok'])}; "
            f"Tipp p95 ≤ {GRENZE_TIPP_MS} ms: {_zelle(k5['tipp_p95_ok'])}"),
            "",
        ]
    return "\n".join(zeilen)


def main(argv: Sequence[str] | None = None) -> int:
    a = argparse.ArgumentParser(description="Lastmessung gegen den Engine-Dienst (docs/engine-messung.md)")
    a.add_argument("--url", required=True, help="Basis-URL des Engine-Dienstes, z. B. http://engine:8081")
    a.add_argument("--partien", type=int, default=10, help="parallele Partien (Kriterium 5: 10)")
    a.add_argument("--dauer", type=float, default=120.0, help="Sekunden je Phase")
    a.add_argument("--gegnerzuege", type=int, default=None, help="statt Dauer: Gegnerzuege je Partie-Schiene")
    a.add_argument("--denkzeit", type=float, nargs=2, default=(1.0, 3.0), metavar=("MIN", "MAX"))
    a.add_argument("--tipp-rate", type=float, default=0.15)
    a.add_argument("--tipp-movetime", type=int, default=800)
    a.add_argument("--phasen", default="gemischt", help="Komma-Liste aus maia,gemischt")
    a.add_argument("--seed", type=int, default=1)
    a.add_argument("--timeout", type=float, default=5.0, help="HTTP-Timeout (wie der .NET-Client: 5 s)")
    a.add_argument("--rss-befehl", default=None, help="Befehl, der den Speicher ausgibt (z. B. docker stats ...)")
    a.add_argument("--rss-intervall", type=float, default=2.0)
    a.add_argument("--erststart", action="store_true", help="zuerst warten, bis /healthz 200 meldet, und die Zeit messen")
    a.add_argument("--erststart-timeout", type=float, default=900.0)
    a.add_argument("--json", type=Path, default=None)
    a.add_argument("--markdown", type=Path, default=None)
    a.add_argument("--streng", action="store_true", help="Exit 1, wenn Kriterium 5 nicht erfuellt ist")
    args = a.parse_args(argv)

    transport = HttpTransport(args.url, timeout_s=args.timeout)
    bericht: dict[str, Any] = {}
    if args.erststart:
        bericht["erststart"] = erststart(transport, timeout_s=args.erststart_timeout)
        if not bericht["erststart"]["gesund"]:
            print("engine not healthy within timeout", file=sys.stderr)
    k = Konfig(
        partien=args.partien,
        dauer_s=None if args.gegnerzuege is not None else args.dauer,
        gegnerzuege=args.gegnerzuege,
        denkzeit_s=(args.denkzeit[0], args.denkzeit[1]),
        tipp_rate=args.tipp_rate,
        tipp_movetime_ms=args.tipp_movetime,
        seed=args.seed,
        phasen=tuple(p.strip() for p in args.phasen.split(",") if p.strip()),
    )
    probe = None
    if args.rss_befehl:
        probe = RssProbe(shlex.split(args.rss_befehl) if sys.platform != "win32" else args.rss_befehl)
    bericht.update(messe(transport, k, probe, args.rss_intervall))

    md = als_markdown(bericht)
    print(md)
    if args.json:
        args.json.write_text(json.dumps(bericht, indent=2, ensure_ascii=False), encoding="utf-8")
    if args.markdown:
        args.markdown.write_text(md, encoding="utf-8")
    if args.streng:
        alle = [v for p in bericht["phasen"] for v in p["kriterium5"].values() if v is not None]
        return 0 if alle and all(alle) else 1
    return 0


if __name__ == "__main__":
    sys.exit(main())
