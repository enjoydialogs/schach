"""Eichung (SCHACH-SPEC 5.4): Staerke je Gegner, Stil-Unabhaengigkeit und Sichtbarkeit.

Alle Partien laufen ueber den Engine-Dienst (engine/API.md): Maia ueber `POST /move` (Policy) plus
Zugwahl im Werkzeug, der Anker ueber `POST /move` mit `engine: "fairy"` und `fairy_skill`
(Fairy-Stockfish mit `UCI_LimitStrength`, im Dienst fest 100 ms je Zug) oder
optional direkt per UCI (`--anker uci`, z. B. Stockfish mit `UCI_LimitStrength`, Grenze nach
`nodes` oder `movetime`).

Die Zugwahl der Maia-Seite ist ein Hook (`Waehler`). Standard ist die REFERENZ in
`stil_referenz.py`, ein Nachbau von `Schach.Domain.Stil.Stilgewichtung` nur fuer die Eichung.

- `staerke`: (a) je Gegner SelfElo-Offsets und Temperaturen durchprobieren, bis die Elo-Schaetzung
  gegen die Anker die Gegner-Elo auf +-100 trifft. Ausgabe inkl. Block `Engine:Eichung`.
- `stil`: (b) gestylt gegen neutral, 400 Partien je Gegner, bestanden bei 45 bis 55 %, und (c)
  Sichtbarkeit der Merkmale (Aggressiv >= +25 % Schlag- plus Schachzuege, Vorsichtig >= 25 %
  weniger riskante Zuege).
- `auswerten`: Zusammenfassung aus einer vorhandenen Spieldatei.

Jede fertige Partie wird als Zeile in eine JSONL-Datei geschrieben; ein erneuter Aufruf mit
derselben Datei spielt nur fehlende Partien (fortsetzbar). Anleitung: docs/engine-messung.md.
"""

import argparse
import importlib
import json
import math
import sys
import threading
import time
import zlib
from collections.abc import Callable, Iterable, Sequence
from concurrent.futures import ThreadPoolExecutor
from dataclasses import asdict, dataclass, field, replace
from pathlib import Path
from typing import Any, Protocol

import chess
import stil_referenz as s
from engine_http import GRUNDSTELLUNG, HttpTransport, Transport, move_anfrage

BAND_ELO = 100  # 5.4 a: +-100
STIL_GRENZEN = (0.45, 0.55)  # 5.4 b
SICHTBARKEIT_MIN = 0.25  # 5.4 c
FAIRY_MIN_ELO = 500

# Kurze Eroeffnungen, je Paar mit getauschten Farben gespielt (weniger Varianz als immer 1. e4).
EROEFFNUNGEN: tuple[tuple[str, ...], ...] = (
    (),
    ("e2e4", "e7e5"),
    ("e2e4", "c7c5"),
    ("d2d4", "d7d5"),
    ("d2d4", "g8f6"),
    ("c2c4", "e7e5"),
    ("e2e4", "e7e6"),
    ("e2e4", "c7c6"),
    ("g1f3", "d7d5"),
    ("e2e4", "e7e5", "g1f3", "b8c6"),
)

# Waehler: (Brett, Policy [(uci, p)], Seed, Halbzug) -> (uci, Merkmale des gewaehlten Zugs)
Waehler = Callable[[chess.Board, list[tuple[str, float]], int, int], tuple[str, s.Merkmal]]


def referenz_waehler(parameter: s.Stilparameter) -> Waehler:
    """REFERENZ-Zugwahl nach SCHACH-SPEC 5.3 (Nachbau des .NET-Codes, nur fuer die Eichung)."""

    def waehle(board: chess.Board, policy: list[tuple[str, float]], seed: int, halbzug: int) -> tuple[str, s.Merkmal]:
        wahl, merkmale = s.waehle_auf_brett(board, policy, parameter, seed, halbzug)
        return wahl.uci, merkmale

    return waehle


def lade_waehler(pfad: str) -> Callable[[s.Stilparameter], Waehler]:
    """`modul:fabrik` laden; die Fabrik bekommt die Stilparameter und liefert einen Waehler."""
    modul, _, name = pfad.partition(":")
    return getattr(importlib.import_module(modul), name)


# --- Spieler -------------------------------------------------------------------------------


class EngineAusfall(RuntimeError):
    pass


class Spieler(Protocol):
    def zug(self, board: chess.Board, moves: list[str], seed: int) -> tuple[str, s.Merkmal | None]: ...


def _post_mit_wiederholung(transport: Transport, nutzlast: dict[str, Any], versuche: int = 6) -> dict[str, Any]:
    pause = 0.5
    for _ in range(versuche):
        antwort = transport.post("/move", nutzlast)
        if antwort.ok and antwort.daten:
            return antwort.daten
        if antwort.status not in (0, 503):
            raise EngineAusfall(f"/move {antwort.status} {antwort.fehlercode}")
        time.sleep(pause)
        pause = min(pause * 2, 8.0)
    raise EngineAusfall("/move failed repeatedly")


@dataclass
class MaiaSpieler:
    transport: Transport
    self_elo: int
    oppo_elo: int
    waehler: Waehler

    def zug(self, board: chess.Board, moves: list[str], seed: int) -> tuple[str, s.Merkmal | None]:
        daten = _post_mit_wiederholung(self.transport, move_anfrage(moves, self.self_elo, self.oppo_elo))
        policy = [(str(z["uci"]), float(z["p"])) for z in daten["policy"]]
        return self.waehler(board, policy, seed, len(moves))


@dataclass
class FairyHttpSpieler:
    transport: Transport
    skill: int

    def zug(self, board: chess.Board, moves: list[str], seed: int) -> tuple[str, s.Merkmal | None]:
        daten = _post_mit_wiederholung(self.transport, move_anfrage(moves, self.skill, self.skill, "fairy", self.skill))
        return str(daten["bestmove"]), None


class UciSpieler:
    """Anker direkt per UCI (z. B. Stockfish mit `UCI_LimitStrength`, `UCI_Elo` ab 1320)."""

    def __init__(self, pfad: str | Sequence[str], elo: int, nodes: int | None = None, movetime_ms: int | None = None) -> None:
        import chess.engine

        self._engine = chess.engine.SimpleEngine.popen_uci(pfad if isinstance(pfad, str) else list(pfad))
        optionen: dict[str, Any] = {"UCI_LimitStrength": True, "UCI_Elo": elo, "Threads": 1, "Hash": 16}
        self._engine.configure({k: v for k, v in optionen.items() if k in self._engine.options})
        self._limit = chess.engine.Limit(nodes=nodes, time=None if movetime_ms is None else movetime_ms / 1000)
        if nodes is None and movetime_ms is None:
            self._limit = chess.engine.Limit(time=0.1)
        self._lock = threading.Lock()

    def zug(self, board: chess.Board, moves: list[str], seed: int) -> tuple[str, s.Merkmal | None]:
        with self._lock:
            ergebnis = self._engine.play(board, self._limit)
        if ergebnis.move is None:
            raise EngineAusfall("uci engine returned no move")
        return ergebnis.move.uci(), None

    def schliessen(self) -> None:
        self._engine.quit()


# --- Partie --------------------------------------------------------------------------------

_MERKMALE = ("schlagzug", "schach", "koenigszone", "riskant", "tausch", "rueckzug", "entwicklung", "ungewoehnlich")


@dataclass
class Partie:
    ergebnis: str
    grund: str
    halbzuege: int
    zuege_weiss: int = 0
    zuege_schwarz: int = 0
    merkmale_weiss: dict[str, int] = field(default_factory=lambda: dict.fromkeys(_MERKMALE, 0))
    merkmale_schwarz: dict[str, int] = field(default_factory=lambda: dict.fromkeys(_MERKMALE, 0))
    moves: list[str] = field(default_factory=list)


def _zaehle(ziel: dict[str, int], m: s.Merkmal) -> None:
    for merkmal, name in s.MERKMAL_NAMEN.items():
        if m & merkmal:
            ziel[name] += 1


def spiele_partie(
    weiss: Spieler, schwarz: Spieler, seed: int, max_halbzuege: int = 300, eroeffnung: Sequence[str] = ()
) -> Partie:
    """Spielt eine Partie. Ende nach Regeln (Remis mit `claim_draw`) oder Remis am Zuglimit."""
    board = chess.Board(GRUNDSTELLUNG)
    moves: list[str] = []
    for uci in eroeffnung:
        board.push_uci(uci)
        moves.append(uci)
    p = Partie("*", "", 0)
    while True:
        outcome = board.outcome(claim_draw=True)
        if outcome is not None:
            p.ergebnis = outcome.result()
            p.grund = outcome.termination.name.lower()
            break
        if len(moves) >= max_halbzuege:
            p.ergebnis, p.grund = "1/2-1/2", "zuglimit"
            break
        am_zug = weiss if board.turn == chess.WHITE else schwarz
        uci, merkmale = am_zug.zug(board, moves, seed)
        zug = chess.Move.from_uci(uci)
        if zug not in board.legal_moves:
            raise EngineAusfall(f"illegal move {uci} from engine")
        if merkmale is None:
            merkmale = s.brettmerkmale(board, zug)
        if board.turn == chess.WHITE:
            p.zuege_weiss += 1
            _zaehle(p.merkmale_weiss, merkmale)
        else:
            p.zuege_schwarz += 1
            _zaehle(p.merkmale_schwarz, merkmale)
        board.push(zug)
        moves.append(uci)
    p.halbzuege = len(moves)
    p.moves = moves
    return p


def punkte_weiss(ergebnis: str) -> float:
    return {"1-0": 1.0, "0-1": 0.0, "1/2-1/2": 0.5}[ergebnis]


# --- Elo -----------------------------------------------------------------------------------


@dataclass(frozen=True)
class Schaetzung:
    elo: float
    unten: float
    oben: float
    n: int


def _elo_diff(score: float) -> float:
    return -400.0 * math.log10(1.0 / score - 1.0)


def elo_schaetzung(siege: int, remis: int, niederlagen: int, gegner_elo: float, z: float = 1.96) -> Schaetzung:
    """Elo aus dem Punkteanteil gegen einen Anker; Intervall aus der trinomialen Varianz."""
    n = siege + remis + niederlagen
    if n == 0:
        raise ValueError("keine Partien")
    score = (siege + 0.5 * remis) / n
    var = (siege * (1 - score) ** 2 + remis * (0.5 - score) ** 2 + niederlagen * score**2) / n
    se = math.sqrt(var / n)
    grenze = 0.5 / n  # 100 % oder 0 % auf einen halben Punkt ziehen, damit die Zahl endlich bleibt

    def elo(sc: float) -> float:
        return gegner_elo + _elo_diff(min(max(sc, grenze), 1 - grenze))

    return Schaetzung(elo(score), elo(score - z * se), elo(score + z * se), n)


def elo_gepoolt(partien: Iterable[tuple[float, float]], z: float = 1.96) -> Schaetzung:
    """Maximum-Likelihood-Elo ueber Partien (Anker-Elo, Punkte 0/0,5/1) gegen mehrere Anker."""
    liste = list(partien)
    if not liste:
        raise ValueError("keine Partien")
    n = len(liste)
    gesamt = sum(p for _, p in liste)
    # Alle gewonnen/verloren: einen halben Punkt verschieben, damit es eine Loesung gibt.
    korrektur = 0.0
    if gesamt >= n:
        korrektur = -0.5
    elif gesamt <= 0:
        korrektur = 0.5

    def erwartet(r: float, a: float) -> float:
        return 1.0 / (1.0 + 10 ** ((a - r) / 400.0))

    def f(r: float) -> float:
        return gesamt + korrektur - sum(erwartet(r, a) for a, _ in liste)

    lo, hi = -2000.0, 6000.0
    for _ in range(200):
        mitte = (lo + hi) / 2
        if f(mitte) > 0:
            lo = mitte
        else:
            hi = mitte
    r = (lo + hi) / 2
    info = sum((math.log(10) / 400) ** 2 * erwartet(r, a) * (1 - erwartet(r, a)) for a, _ in liste)
    se = 1 / math.sqrt(info) if info > 0 else float("inf")
    return Schaetzung(r, r - z * se, r + z * se, n)


# --- Suche ---------------------------------------------------------------------------------


@dataclass(frozen=True)
class Treffer:
    self_elo: int
    temperatur: float
    schaetzung: Schaetzung
    im_band: bool


def suche(
    ziel_elo: int,
    messen: Callable[[int, float], Schaetzung],
    offsets: Sequence[int],
    temperaturen: Sequence[float],
    band: int = BAND_ELO,
    protokoll: list[dict[str, Any]] | None = None,
) -> Treffer:
    """Offsets und Temperaturen in Reihenfolge; erster Versuch im Band gewinnt, sonst der naechste."""
    bester: Treffer | None = None
    for temperatur in temperaturen:
        for offset in offsets:
            self_elo = ziel_elo + offset
            sch = messen(self_elo, temperatur)
            t = Treffer(self_elo, temperatur, sch, abs(sch.elo - ziel_elo) <= band)
            if protokoll is not None:
                protokoll.append({"self_elo": self_elo, "temperatur": temperatur, **asdict(sch), "im_band": t.im_band})
            if t.im_band:
                return t
            if bester is None or abs(sch.elo - ziel_elo) < abs(bester.schaetzung.elo - ziel_elo):
                bester = t
    assert bester is not None, "keine Offsets oder Temperaturen"
    return bester


def engine_eichung(ergebnisse: dict[str, Treffer]) -> dict[str, Any]:
    """Block `Engine:Eichung` je Gegner-Key, Form wie `Schach.Domain.Eichung(SelfElo, Temperatur, Engine)`.

    Verfehlt ein Anfaenger (Ilse bis Nele) das Band, spielt die ganze Spielstaerke Anfaenger mit
    Fairy-Stockfish, `SelfElo` = Gegner-Elo als `fairy_skill` (5.1, 5.4 a). Alle anderen Gegner
    ausserhalb des Bandes bleiben bei der Identitaet und stehen in `nicht_im_band`.
    `tau_abweichung` nennt Spielstaerken, deren Gegner verschiedene Temperaturen bekamen (5.3
    Schritt 2: tau nur je Spielstaerke); dann von Hand eine gemeinsame waehlen und nachmessen.
    """
    block: dict[str, dict[str, Any]] = {}
    nicht: list[str] = []
    anfaenger = s.SPIELSTAERKEN["anfaenger"]
    anfaenger_fairy = any(k in anfaenger and not t.im_band for k, t in ergebnisse.items())
    for key, t in ergebnisse.items():
        elo = s.gegner(key).elo
        if not t.im_band:
            nicht.append(key)
        if key in anfaenger and anfaenger_fairy:
            block[key] = {"SelfElo": max(elo, FAIRY_MIN_ELO), "Temperatur": 1.0, "Engine": "Fairy"}
        elif t.im_band:
            block[key] = {"SelfElo": t.self_elo, "Temperatur": t.temperatur, "Engine": "Maia"}
        else:
            block[key] = {"SelfElo": elo, "Temperatur": 1.0, "Engine": "Maia"}
    abweichung = [
        stufe
        for stufe, keys in s.SPIELSTAERKEN.items()
        if len({block[k]["Temperatur"] for k in keys if k in block and block[k]["Engine"] == "Maia"}) > 1
    ]
    return {"Engine": {"Eichung": block}, "nicht_im_band": nicht, "tau_abweichung": abweichung}


def pruefe_uci_anker(pfad: str | Sequence[str], anker_elos: Iterable[int]) -> None:
    """Bricht vor dem Spielen ab, wenn ein Anker ausserhalb von `UCI_Elo` liegt (nicht kappen:
    sonst stuende eine falsche Anker-Elo in den Ergebnissen)."""
    import chess.engine

    engine = chess.engine.SimpleEngine.popen_uci(pfad if isinstance(pfad, str) else list(pfad))
    try:
        option = engine.options.get("UCI_Elo")
        if option is None or "UCI_LimitStrength" not in engine.options:
            raise ValueError("UCI anchor has no UCI_Elo / UCI_LimitStrength")
        ausserhalb = [a for a in anker_elos if a < (option.min or a) or a > (option.max or a)]
        if ausserhalb:
            raise ValueError(f"UCI_Elo range {option.min}..{option.max} excludes anchors {ausserhalb}")
    finally:
        engine.quit()


# --- Sichtbarkeit --------------------------------------------------------------------------


def sichtbarkeit_pruefen(persoenlichkeit: str, gestylt: dict[str, int], neutral: dict[str, int]) -> dict[str, Any]:
    """5.4 c: Aggressiv >= +25 % (Schlag + Schach) je Zug, Vorsichtig <= -25 % riskante Zuege je Zug."""

    def rate(z: dict[str, int], *namen: str) -> float:
        return sum(z[n] for n in namen) / z["zuege"] if z["zuege"] else 0.0

    if persoenlichkeit == "aggressiv":
        g, n = rate(gestylt, "schlagzug", "schach"), rate(neutral, "schlagzug", "schach")
        wert = g / n - 1 if n else math.inf
        return {"art": "aggressiv", "wert": wert, "bestanden": wert >= SICHTBARKEIT_MIN}
    if persoenlichkeit == "vorsichtig":
        g, n = rate(gestylt, "riskant"), rate(neutral, "riskant")
        wert = g / n - 1 if n else 0.0
        return {"art": "vorsichtig", "wert": wert, "bestanden": wert <= -SICHTBARKEIT_MIN}
    return {"art": persoenlichkeit, "wert": None, "bestanden": None}


# --- Lauf (fortsetzbar) --------------------------------------------------------------------


def _seed(spiel_id: str) -> int:
    return zlib.crc32(spiel_id.encode("utf-8"))


class Lauf:
    def __init__(
        self,
        transport: Transport,
        spiele: Path,
        max_halbzuege: int = 300,
        parallel: int = 4,
        eroeffnungen: Sequence[Sequence[str]] = EROEFFNUNGEN,
        waehler_fabrik: Callable[[s.Stilparameter], Waehler] = referenz_waehler,
        anker_fabrik: Callable[[int], Spieler] | None = None,
        protokoll: Callable[[str], None] = lambda _t: None,
    ) -> None:
        self.transport = transport
        self.spiele = spiele
        self.max_halbzuege = max_halbzuege
        self.parallel = max(1, parallel)
        self.eroeffnungen = list(eroeffnungen) or [()]
        self.waehler_fabrik = waehler_fabrik
        self.anker_fabrik = anker_fabrik or (lambda elo: FairyHttpSpieler(self.transport, elo))
        self.protokoll = protokoll
        self._lock = threading.Lock()
        self._fertig: dict[str, dict[str, Any]] = {}
        if spiele.exists():
            text = spiele.read_text(encoding="utf-8")
            if text and not text.endswith("\n"):
                with spiele.open("a", encoding="utf-8", newline="\n") as f:
                    f.write("\n")  # angefangene Zeile abschliessen, damit die naechste sauber beginnt
            for zeile in text.splitlines():
                if not zeile.strip():
                    continue
                try:
                    d = json.loads(zeile)
                except ValueError:
                    continue  # beim Abbruch halb geschriebene Zeile; die Partie wird neu gespielt
                self._fertig[d["id"]] = d

    def _speichern(self, zeile: dict[str, Any]) -> None:
        with self._lock:
            self._fertig[zeile["id"]] = zeile
            self.spiele.parent.mkdir(parents=True, exist_ok=True)
            with self.spiele.open("a", encoding="utf-8", newline="\n") as f:
                f.write(json.dumps(zeile, ensure_ascii=False) + "\n")

    def _ausfuehren(self, auftraege: list[tuple[str, Callable[[], dict[str, Any]]]]) -> list[dict[str, Any]]:
        offen = [(i, a) for i, a in auftraege if i not in self._fertig]
        if offen:
            self.protokoll(f"{len(offen)} games to play ({len(auftraege) - len(offen)} already done)")

        def eins(auftrag: tuple[str, Callable[[], dict[str, Any]]]) -> None:
            spiel_id, fn = auftrag
            zeile = fn()
            zeile["id"] = spiel_id
            self._speichern(zeile)

        with ThreadPoolExecutor(max_workers=self.parallel) as pool:
            list(pool.map(eins, offen))
        return [self._fertig[i] for i, _ in auftraege]

    def _maia(self, parameter: s.Stilparameter, self_elo: int, oppo_elo: int) -> MaiaSpieler:
        return MaiaSpieler(self.transport, self_elo, oppo_elo, self.waehler_fabrik(parameter))

    # (a) Staerke
    def staerke_partien(
        self, key: str, self_elo: int, temperatur: float, anker_elos: Sequence[int], partien_je_anker: int
    ) -> list[dict[str, Any]]:
        parameter = s.Stilparameter(s.NEUTRAL, temperatur)
        auftraege = []
        for anker in anker_elos:
            for i in range(partien_je_anker):
                spiel_id = f"staerke|{key}|{self_elo}|{temperatur:g}|{anker}|{i}"
                auftraege.append((spiel_id, self._staerke_auftrag(spiel_id, key, parameter, self_elo, anker, i)))
        return self._ausfuehren(auftraege)

    def _staerke_auftrag(self, spiel_id, key, parameter, self_elo, anker, i):
        def fn() -> dict[str, Any]:
            maia = self._maia(parameter, self_elo, anker)  # oppo_elo = Anker-Elo
            gegenueber = self.anker_fabrik(anker)
            maia_weiss = i % 2 == 0
            eroeffnung = self.eroeffnungen[(i // 2) % len(self.eroeffnungen)]
            weiss, schwarz = (maia, gegenueber) if maia_weiss else (gegenueber, maia)
            try:
                p = spiele_partie(weiss, schwarz, _seed(spiel_id), self.max_halbzuege, eroeffnung)
            finally:
                schliessen = getattr(gegenueber, "schliessen", None)
                if schliessen is not None:
                    schliessen()
            pw = punkte_weiss(p.ergebnis)
            return {
                "art": "staerke", "gegner": key, "self_elo": self_elo, "temperatur": parameter.temperatur,
                "anker_elo": anker, "index": i, "maia_weiss": maia_weiss, "ergebnis": p.ergebnis, "grund": p.grund,
                "halbzuege": p.halbzuege, "punkte_maia": pw if maia_weiss else 1 - pw,
            }  # fmt: skip

        return fn

    def staerke_messen(
        self, key: str, self_elo: int, temperatur: float, anker_elos: Sequence[int], partien_je_anker: int
    ) -> Schaetzung:
        zeilen = self.staerke_partien(key, self_elo, temperatur, anker_elos, partien_je_anker)
        return elo_gepoolt((z["anker_elo"], z["punkte_maia"]) for z in zeilen)

    # (b) und (c) Stil
    def stil_ab(
        self,
        key: str,
        self_elo: int,
        temperatur: float,
        partien: int = 400,
        gewicht_max: float = 2.0,
        band_faktor: float = 0.25,
    ) -> dict[str, Any]:
        g = s.gegner(key)
        gestylt = s.Stilparameter(s.PERSOENLICHKEITEN[g.persoenlichkeit], temperatur, band_faktor, gewicht_max=gewicht_max)
        neutral = replace(gestylt, gewichte=s.NEUTRAL)
        auftraege = []
        for i in range(partien):
            spiel_id = f"stil|{key}|{self_elo}|{temperatur:g}|{gewicht_max:g}|{band_faktor:g}|{i}"
            auftraege.append((spiel_id, self._stil_auftrag(spiel_id, key, gestylt, neutral, self_elo, i)))
        zeilen = self._ausfuehren(auftraege)
        return stil_bericht(key, zeilen)

    def _stil_auftrag(self, spiel_id, key, gestylt, neutral, self_elo, i):
        def fn() -> dict[str, Any]:
            a = self._maia(gestylt, self_elo, self_elo)
            b = self._maia(neutral, self_elo, self_elo)
            gestylt_weiss = i % 2 == 0
            eroeffnung = self.eroeffnungen[(i // 2) % len(self.eroeffnungen)]
            weiss, schwarz = (a, b) if gestylt_weiss else (b, a)
            p = spiele_partie(weiss, schwarz, _seed(spiel_id), self.max_halbzuege, eroeffnung)
            pw = punkte_weiss(p.ergebnis)
            mg, mn = (p.merkmale_weiss, p.merkmale_schwarz) if gestylt_weiss else (p.merkmale_schwarz, p.merkmale_weiss)
            zg, zn = (p.zuege_weiss, p.zuege_schwarz) if gestylt_weiss else (p.zuege_schwarz, p.zuege_weiss)
            return {
                "art": "stil", "gegner": key, "self_elo": self_elo, "temperatur": gestylt.temperatur,
                "gewicht_max": gestylt.gewicht_max, "band_faktor": gestylt.band_faktor, "index": i,
                "gestylt_weiss": gestylt_weiss, "ergebnis": p.ergebnis, "grund": p.grund, "halbzuege": p.halbzuege,
                "punkte_gestylt": pw if gestylt_weiss else 1 - pw,
                "gestylt": {"zuege": zg, **mg}, "neutral": {"zuege": zn, **mn},
            }  # fmt: skip

        return fn


def stil_bericht(key: str, zeilen: list[dict[str, Any]]) -> dict[str, Any]:
    n = len(zeilen)
    punkte = sum(z["punkte_gestylt"] for z in zeilen) / n if n else 0.0
    var = sum((z["punkte_gestylt"] - punkte) ** 2 for z in zeilen) / n if n else 0.0
    se = math.sqrt(var / n) if n else 0.0
    summe_g: dict[str, int] = dict.fromkeys(("zuege", *_MERKMALE), 0)
    summe_n: dict[str, int] = dict.fromkeys(("zuege", *_MERKMALE), 0)
    for z in zeilen:
        for k_, v in z["gestylt"].items():
            summe_g[k_] += v
        for k_, v in z["neutral"].items():
            summe_n[k_] += v
    persoenlichkeit = s.gegner(key).persoenlichkeit
    return {
        "gegner": key,
        "persoenlichkeit": persoenlichkeit,
        "partien": n,
        "punkte_gestylt": punkte,
        "intervall_95": [punkte - 1.96 * se, punkte + 1.96 * se],
        "bestanden": STIL_GRENZEN[0] <= punkte <= STIL_GRENZEN[1],
        "sichtbarkeit": {
            "gestylt": summe_g,
            "neutral": summe_n,
            "raten": {
                m: {
                    "gestylt": summe_g[m] / summe_g["zuege"] if summe_g["zuege"] else 0.0,
                    "neutral": summe_n[m] / summe_n["zuege"] if summe_n["zuege"] else 0.0,
                }
                for m in _MERKMALE
            },
            "pruefung": sichtbarkeit_pruefen(persoenlichkeit, summe_g, summe_n),
        },
    }


def auswerten(zeilen: Iterable[dict[str, Any]]) -> dict[str, Any]:
    """Zusammenfassung aus einer Spieldatei: Staerke je (Gegner, SelfElo, Temperatur), Stil je Gegner."""
    staerke: dict[str, list[dict[str, Any]]] = {}
    stil: dict[str, list[dict[str, Any]]] = {}
    for z in zeilen:
        if z["art"] == "staerke":
            staerke.setdefault(f"{z['gegner']}|{z['self_elo']}|{z['temperatur']:g}", []).append(z)
        else:
            stil.setdefault(f"{z['gegner']}|{z['self_elo']}|{z['temperatur']:g}|{z['gewicht_max']:g}|{z['band_faktor']:g}", []).append(z)
    aus: dict[str, Any] = {"staerke": {}, "stil": {}}
    for schluessel, liste in sorted(staerke.items()):
        sch = elo_gepoolt((z["anker_elo"], z["punkte_maia"]) for z in liste)
        key = liste[0]["gegner"]
        aus["staerke"][schluessel] = {**asdict(sch), "ziel": s.gegner(key).elo, "im_band": abs(sch.elo - s.gegner(key).elo) <= BAND_ELO}
    for schluessel, liste in sorted(stil.items()):
        aus["stil"][schluessel] = stil_bericht(liste[0]["gegner"], liste)
    return aus


# --- CLI -----------------------------------------------------------------------------------


def _anker_elos(ziel: int, text: str) -> list[int]:
    if text == "auto":
        return sorted({max(FAIRY_MIN_ELO, ziel - 200), max(FAIRY_MIN_ELO, ziel), ziel + 200})
    return [int(x) for x in text.split(",") if x.strip()]


def _gegner_liste(text: str) -> list[str]:
    if text == "alle":
        return [g.key for g in s.GEGNER]
    keys = [k.strip() for k in text.split(",") if k.strip()]
    for k in keys:
        s.gegner(k)
    return keys


def main(argv: Sequence[str] | None = None) -> int:
    a = argparse.ArgumentParser(description="Eichung der Gegner (SCHACH-SPEC 5.4, docs/engine-messung.md)")
    sub = a.add_subparsers(dest="befehl", required=True)

    def gemeinsam(p: argparse.ArgumentParser) -> None:
        p.add_argument("--url", required=True, help="Engine-Dienst, z. B. http://127.0.0.1:8081")
        p.add_argument("--gegner", default="alle", help="Komma-Liste der Gegner-Keys oder 'alle'")
        p.add_argument("--spiele", type=Path, required=True, help="JSONL-Datei, eine Zeile je Partie (fortsetzbar)")
        p.add_argument("--ausgabe", type=Path, default=None, help="Ergebnis als JSON")
        p.add_argument("--max-halbzuege", type=int, default=300, help="danach Remis (zuglimit)")
        p.add_argument("--parallel", type=int, default=4)
        p.add_argument("--ohne-eroeffnungen", action="store_true", help="immer aus der Grundstellung")
        p.add_argument("--waehler", default=None, help="eigener Zugwahl-Hook 'modul:fabrik' statt der Referenz")
        p.add_argument("--timeout", type=float, default=10.0)

    st = sub.add_parser("staerke", help="(a) Staerke je Gegner auf +-100 eichen")
    gemeinsam(st)
    st.add_argument("--partien", type=int, default=100, help="Partien je Anker und Versuch")
    st.add_argument("--anker-elos", default="auto", help="'auto' (Ziel-200, Ziel, Ziel+200) oder Komma-Liste")
    st.add_argument("--offsets", default="0,-100,100,-200,200,-300,300,-400,400", help="SelfElo-Offsets in Reihenfolge")
    st.add_argument("--temperaturen", default="1.0", help="Komma-Liste, aeussere Schleife")
    st.add_argument("--anker", choices=("fairy", "uci"), default="fairy")
    st.add_argument("--anker-pfad", default=None, help="UCI-Binary fuer --anker uci")
    st.add_argument("--anker-nodes", type=int, default=None)
    st.add_argument("--anker-movetime", type=int, default=None, help="ms je Zug fuer --anker uci")

    sl = sub.add_parser("stil", help="(b) Stil gegen neutral und (c) Sichtbarkeit")
    gemeinsam(sl)
    sl.add_argument("--partien", type=int, default=400)
    sl.add_argument("--eichung", type=Path, default=None, help="Ergebnis von 'staerke' (SelfElo, Temperatur je Gegner)")
    sl.add_argument("--gewicht-max", type=float, default=2.0, help="5.4 b Ausweg: 1.5")
    sl.add_argument("--band-faktor", type=float, default=0.25, help="5.4 b Ausweg: 0.5")

    au = sub.add_parser("auswerten", help="Zusammenfassung aus einer Spieldatei")
    au.add_argument("--spiele", type=Path, required=True)
    au.add_argument("--ausgabe", type=Path, default=None)

    args = a.parse_args(argv)

    def schreiben(daten: dict[str, Any]) -> None:
        text = json.dumps(daten, indent=2, ensure_ascii=False)
        print(text)
        if args.ausgabe:
            args.ausgabe.write_text(text + "\n", encoding="utf-8")

    if args.befehl == "auswerten":
        zeilen = [json.loads(z) for z in args.spiele.read_text(encoding="utf-8").splitlines() if z.strip()]
        schreiben(auswerten(zeilen))
        return 0

    transport = HttpTransport(args.url, timeout_s=args.timeout)
    fabrik = lade_waehler(args.waehler) if args.waehler else referenz_waehler
    anker_fabrik = None
    if getattr(args, "anker", "fairy") == "uci":
        if not args.anker_pfad:
            a.error("--anker uci needs --anker-pfad")

        def anker_fabrik(elo: int) -> Spieler:
            return UciSpieler(args.anker_pfad, elo, args.anker_nodes, args.anker_movetime)

    lauf = Lauf(
        transport,
        args.spiele,
        max_halbzuege=args.max_halbzuege,
        parallel=args.parallel,
        eroeffnungen=[()] if args.ohne_eroeffnungen else EROEFFNUNGEN,
        waehler_fabrik=fabrik,
        anker_fabrik=anker_fabrik,
        protokoll=lambda t: print(t, file=sys.stderr),
    )
    if args.befehl == "staerke":
        offsets = [int(x) for x in args.offsets.split(",") if x.strip()]
        temperaturen = [float(x) for x in args.temperaturen.split(",") if x.strip()]
        stufen: dict[str, Any] = {}
        treffer: dict[str, Treffer] = {}
        gegner_keys = _gegner_liste(args.gegner)
        if args.anker == "uci":
            alle_anker = sorted({x for k in gegner_keys for x in _anker_elos(s.gegner(k).elo, args.anker_elos)})
            try:
                pruefe_uci_anker(args.anker_pfad, alle_anker)
            except ValueError as fehler:
                a.error(f"{fehler}; choose --anker-elos inside the range or use --anker fairy")
        for key in gegner_keys:
            ziel = s.gegner(key).elo
            anker = _anker_elos(ziel, args.anker_elos)
            versuche: list[dict[str, Any]] = []

            def messen(self_elo: int, temperatur: float, key=key, anker=anker) -> Schaetzung:
                return lauf.staerke_messen(key, self_elo, temperatur, anker, args.partien)

            t = suche(ziel, messen, offsets, temperaturen, protokoll=versuche)
            treffer[key] = t
            stufen[key] = {"ziel": ziel, "anker_elos": anker, "versuche": versuche, "gewaehlt": {**asdict(t)}}
        schreiben({**engine_eichung(treffer), "stufen": stufen})
        return 0

    eichung_datei = json.loads(args.eichung.read_text(encoding="utf-8")) if args.eichung else {}
    block = eichung_datei.get("Engine", {}).get("Eichung", {})
    berichte = {}
    for key in _gegner_liste(args.gegner):
        e_ = block.get(key, {"SelfElo": s.gegner(key).elo, "Temperatur": 1.0, "Engine": "Maia"})
        if e_.get("Engine", "Maia") != "Maia":
            print(f"{key}: engine {e_['Engine']} has no style, skipped", file=sys.stderr)
            berichte[key] = {"uebersprungen": f"Engine {e_['Engine']} hat keinen Stil"}
            continue
        berichte[key] = lauf.stil_ab(key, int(e_["SelfElo"]), float(e_["Temperatur"]), args.partien, args.gewicht_max, args.band_faktor)
    schreiben({"stil": berichte})
    return 0


if __name__ == "__main__":
    sys.exit(main())
