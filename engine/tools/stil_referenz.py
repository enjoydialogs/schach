"""REFERENZ, nur fuer die Eichung: Python-Nachbau des Stil-Algorithmus (SCHACH-SPEC 5.3).

Massgeblich im Spiel ist der .NET-Code in `src/Schach.Domain` (`Stil/Stilgewichtung.cs`,
`Stil/Zugmerkmale.cs`, `Partiezufall.cs`, Gewichte in `GegnerKatalog.cs`). Dieses Modul bildet ihn
Zeile fuer Zeile nach, damit `eichung.py` Partien ohne den .NET-Server spielen kann. Die Paritaet
pruefen `test_stil_referenz.py` gegen Werte, die mit dotnet aus `Schach.Domain` erzeugt wurden
(`testdaten/stil_dotnet.json`). Aendert sich der .NET-Code, muss diese Datei mitziehen.
"""

from collections.abc import Callable, Sequence
from dataclasses import dataclass
from enum import IntFlag

import chess

_MASKE64 = (1 << 64) - 1
ZWECK_ZUGWAHL = 1  # Zufallszweck.Zugwahl
UNGEWOEHNLICH_ANTEIL = 0.5
KLAR_VORN_MATERIAL = 2


class Merkmal(IntFlag):
    """Bitwerte wie `Schach.Domain.Stil.Zugmerkmal`."""

    KEINE = 0
    SCHLAGZUG = 1 << 0
    SCHACH = 1 << 1
    KOENIGSZONE = 1 << 2
    RISKANT = 1 << 3
    TAUSCH = 1 << 4
    RUECKZUG = 1 << 5
    ENTWICKLUNG = 1 << 6
    UNGEWOEHNLICH = 1 << 7


MERKMAL_NAMEN = {
    Merkmal.SCHLAGZUG: "schlagzug",
    Merkmal.SCHACH: "schach",
    Merkmal.KOENIGSZONE: "koenigszone",
    Merkmal.RISKANT: "riskant",
    Merkmal.TAUSCH: "tausch",
    Merkmal.RUECKZUG: "rueckzug",
    Merkmal.ENTWICKLUNG: "entwicklung",
    Merkmal.UNGEWOEHNLICH: "ungewoehnlich",
}


@dataclass(frozen=True)
class Gewichte:
    schlagzug: float = 1.0
    schach: float = 1.0
    koenigszone: float = 1.0
    riskant: float = 1.0
    tausch: float = 1.0
    rueckzug: float = 1.0
    entwicklung: float = 1.0
    ungewoehnlich: float = 1.0

    def fuer(self, merkmal: Merkmal) -> float:
        return getattr(self, MERKMAL_NAMEN[merkmal])


NEUTRAL = Gewichte()

# Startgewichte (SCHACH-SPEC 4.2, `Stilgewichte.Fuer`). Verspielt: Ungewoehnlich 1,5 erst nach
# bestandener Pruefung 5.4 b; die Eichung prueft genau das, darum steht hier der Zielwert.
PERSOENLICHKEITEN: dict[str, Gewichte] = {
    "aggressiv": Gewichte(1.4, 1.5, 1.5, 1.3, 0.7, 0.6, 1.0, 1.0),
    "vorsichtig": Gewichte(1.0, 0.9, 0.7, 0.5, 1.0, 1.2, 1.2, 0.7),
    "lehrerhaft": Gewichte(1.0, 1.0, 1.0, 0.8, 1.0, 1.0, 1.5, 0.5),
    "verspielt": Gewichte(1.1, 1.2, 1.0, 1.4, 0.8, 1.0, 0.9, 1.5),
    "eitel": Gewichte(1.1, 1.2, 1.2, 1.1, 0.5, 1.0, 1.0, 1.2),
    "gemuetlich": Gewichte(0.9, 0.9, 0.8, 0.6, 1.5, 1.0, 1.0, 0.8),
}


@dataclass(frozen=True)
class GegnerEintrag:
    key: str
    elo: int
    persoenlichkeit: str


# Aufstellung (SCHACH-SPEC 4.3, `GegnerKatalog`).
GEGNER: tuple[GegnerEintrag, ...] = (
    GegnerEintrag("ilse", 600, "gemuetlich"),
    GegnerEintrag("finn", 800, "aggressiv"),
    GegnerEintrag("oscar", 1000, "lehrerhaft"),
    GegnerEintrag("nele", 1200, "verspielt"),
    GegnerEintrag("theo", 1400, "vorsichtig"),
    GegnerEintrag("sementa", 1600, "eitel"),
    GegnerEintrag("konrad", 1900, "lehrerhaft"),
    GegnerEintrag("maren", 2200, "aggressiv"),
)


def gegner(key: str) -> GegnerEintrag:
    for g in GEGNER:
        if g.key == key:
            return g
    raise KeyError(key)


@dataclass(frozen=True)
class Stilparameter:
    gewichte: Gewichte
    temperatur: float = 1.0
    band_faktor: float = 0.25
    band_max: int = 8
    gewicht_min: float = 0.5
    gewicht_max: float = 2.0


@dataclass(frozen=True)
class Stilwahl:
    uci: str
    maia_rang: int
    p_anteil: float
    w: float


# --- Partiezufall (SplitMix64) -------------------------------------------------------------


def _mix(z: int) -> int:
    z = (z + 0x9E3779B97F4A7C15) & _MASKE64
    z = ((z ^ (z >> 30)) * 0xBF58476D1CE4E5B9) & _MASKE64
    z = ((z ^ (z >> 27)) * 0x94D049BB133111EB) & _MASKE64
    return z ^ (z >> 31)


def partiezufall(seed: int, halbzug: int, zweck: int = ZWECK_ZUGWAHL) -> float:
    """Wie `Partiezufall.Zahl`: gleichverteilt in [0, 1) aus (Seed, Halbzug, Zweck)."""
    x = _mix(seed & _MASKE64)
    x = _mix(x ^ (halbzug & 0xFFFFFFFF))
    x = _mix(x ^ ((zweck & 0xFFFFFFFF) << 32))
    return (x >> 11) * (1.0 / (1 << 53))


# --- Zugmerkmale ---------------------------------------------------------------------------

_WERT = {chess.PAWN: 1, chess.KNIGHT: 3, chess.BISHOP: 3, chess.ROOK: 5, chess.QUEEN: 9, chess.KING: 100}
_STARTFELDER = {
    chess.WHITE: {chess.B1, chess.C1, chess.F1, chess.G1},
    chess.BLACK: {chess.B8, chess.C8, chess.F8, chess.G8},
}


def brettmerkmale(board: chess.Board, zug: chess.Move) -> Merkmal:
    """Wie `Zugmerkmale.Bestimme` (ohne `Ungewoehnlich`, das kommt aus der Policy)."""
    if zug not in board.legal_moves:
        raise ValueError(f"Zug {zug.uci()} ist in der Stellung nicht erlaubt.")
    weiss = board.turn
    von_figur = board.piece_type_at(zug.from_square)
    schlag = board.is_capture(zug)
    rochade = board.is_castling(zug)
    if schlag:
        geschlagen = chess.PAWN if board.is_en_passant(zug) else board.piece_type_at(zug.to_square)
    else:
        geschlagen = None

    nachher = board.copy(stack=False)
    nachher.push(zug)
    figur = nachher.piece_type_at(zug.to_square)
    assert figur is not None and von_figur is not None
    gegner_koenig = nachher.king(not weiss)
    assert gegner_koenig is not None

    angreifer = [nachher.piece_type_at(s) for s in nachher.attackers(not weiss, zug.to_square)]
    verteidiger = nachher.attackers(weiss, zug.to_square)
    riskant = figur != chess.KING and bool(angreifer) and (
        not verteidiger or any(_WERT[a] < _WERT[figur] for a in angreifer if a is not None)
    )
    tausch = schlag and geschlagen is not None and _WERT[geschlagen] == _WERT[von_figur] and bool(angreifer)
    vorwaerts = 1 if weiss else -1
    reihe_diff = chess.square_rank(zug.to_square) - chess.square_rank(zug.from_square)
    rueckzug = not schlag and figur != chess.PAWN and von_figur != chess.PAWN and reihe_diff * vorwaerts < 0
    entwicklung = rochade or (von_figur in (chess.KNIGHT, chess.BISHOP) and zug.from_square in _STARTFELDER[weiss])

    m = Merkmal.KEINE
    if schlag:
        m |= Merkmal.SCHLAGZUG
    if nachher.is_check():
        m |= Merkmal.SCHACH
    if chess.square_distance(zug.to_square, gegner_koenig) <= 2:
        m |= Merkmal.KOENIGSZONE
    if riskant:
        m |= Merkmal.RISKANT
    if tausch:
        m |= Merkmal.TAUSCH
    if rueckzug:
        m |= Merkmal.RUECKZUG
    if entwicklung:
        m |= Merkmal.ENTWICKLUNG
    return m


def materialbilanz(board: chess.Board) -> int:
    """Wie `Materialbilanz.FuerSeiteAmZug`: eigenes minus gegnerisches Material (D 9, T 5, L 3, S 3, B 1)."""
    werte = {chess.PAWN: 1, chess.KNIGHT: 3, chess.BISHOP: 3, chess.ROOK: 5, chess.QUEEN: 9}
    bilanz = 0
    for typ, wert in werte.items():
        bilanz += wert * (len(board.pieces(typ, board.turn)) - len(board.pieces(typ, not board.turn)))
    return bilanz


def loest_sofort_remis_aus(board: chess.Board, zug: chess.Move) -> bool:
    """Patt, kein Mattmaterial, dreifache Wiederholung oder 50 Zuege direkt nach dem Zug (SCHACH-SPEC 3.4)."""
    board.push(zug)
    try:
        return (
            board.is_stalemate()
            or board.is_insufficient_material()
            or board.can_claim_threefold_repetition()
            or board.can_claim_fifty_moves()
        )
    finally:
        board.pop()


# --- Stilgewichtung ------------------------------------------------------------------------

Policy = Sequence[tuple[str, float]]


def _sortiere(policy: Policy) -> list[tuple[str, float]]:
    if not policy:
        raise ValueError("Die Policy enthaelt keinen Zug.")
    # p absteigend, dann UCI ordinal (StringComparer.Ordinal).
    return sorted(policy, key=lambda z: (-z[1], z[0]))


def kandidatenband(policy: Policy, parameter: Stilparameter) -> list[str]:
    sortiert = _sortiere(policy)
    p_max = sortiert[0][1]
    return [uci for uci, p in sortiert if p >= parameter.band_faktor * p_max][: parameter.band_max]


def merkmale(policy: Policy, uci: str, brett: Callable[[str], Merkmal]) -> Merkmal:
    p_max = max(p for _, p in policy)
    p = next(p for u, p in policy if u == uci)
    m = brett(uci) & ~Merkmal.UNGEWOEHNLICH
    return m | Merkmal.UNGEWOEHNLICH if p < UNGEWOEHNLICH_ANTEIL * p_max else m


def gewicht(m: Merkmal, gewichte: Gewichte, minimum: float = 0.5, maximum: float = 2.0) -> float:
    w = 1.0
    for merkmal in MERKMAL_NAMEN:
        if m & merkmal:
            w *= gewichte.fuer(merkmal)
    return min(max(w, minimum), maximum)


@dataclass(frozen=True)
class Remislage:
    materialbilanz: int = 0
    loest_sofort_remis_aus: Callable[[str], bool] = lambda _uci: False


def _remisfilter(sortiert: list[tuple[str, float]], band: set[str], remis: Remislage) -> list[tuple[str, float]]:
    if remis.materialbilanz >= KLAR_VORN_MATERIAL:
        band_ohne_remis = any(u in band and not remis.loest_sofort_remis_aus(u) for u, _ in sortiert)
        if not band_ohne_remis:
            return [(u, p) for u, p in sortiert if u in band or not remis.loest_sofort_remis_aus(u)]
        return [(u, p) for u, p in sortiert if not remis.loest_sofort_remis_aus(u)]
    if remis.materialbilanz <= -KLAR_VORN_MATERIAL:
        remis_im_band = [(u, p) for u, p in sortiert if u in band and remis.loest_sofort_remis_aus(u)]
        if remis_im_band:
            return remis_im_band
    return sortiert


def _ziehe(gewichte: list[float], zufall: float) -> int:
    summe = sum(gewichte)
    if summe <= 0:
        return 0
    ziel = zufall * summe
    kumuliert = 0.0
    for i, g in enumerate(gewichte):
        kumuliert += g
        if ziel < kumuliert:
            return i
    return max(i for i, g in enumerate(gewichte) if g > 0)


def waehle(
    policy: Policy,
    parameter: Stilparameter,
    brett: Callable[[str], Merkmal],
    remis: Remislage,
    seed: int,
    halbzug: int,
) -> Stilwahl:
    """Wie `Stilgewichtung.Waehle` (SCHACH-SPEC 5.3 Schritte 2 bis 7)."""
    if parameter.temperatur <= 0:
        raise ValueError("Die Temperatur muss positiv sein.")
    sortiert = _sortiere(policy)
    p_max = sortiert[0][1]
    band = set(kandidatenband(sortiert, parameter))
    pool = _remisfilter(sortiert, band, remis)

    gewichte: list[float] = []
    ws: list[float] = []
    for uci, p in pool:
        w = (
            gewicht(merkmale(sortiert, uci, brett), parameter.gewichte, parameter.gewicht_min, parameter.gewicht_max)
            if uci in band
            else 1.0
        )
        ws.append(w)
        q = p * w
        gewichte.append(0.0 if q <= 0 else (q / p_max) ** (1.0 / parameter.temperatur))

    i = _ziehe(gewichte, partiezufall(seed, halbzug, ZWECK_ZUGWAHL))
    uci, p = pool[i]
    rang = next(r for r, (u, _) in enumerate(sortiert, start=1) if u == uci)
    return Stilwahl(uci, rang, p / p_max, ws[i])


def waehle_auf_brett(
    board: chess.Board, policy: Policy, parameter: Stilparameter, seed: int, halbzug: int
) -> tuple[Stilwahl, Merkmal]:
    """Bequemer Einstieg fuer die Eichung: Merkmale und Remislage aus dem Brett, wie der Server."""
    zuege = {m.uci(): m for m in board.legal_moves}
    cache: dict[str, Merkmal] = {}

    def brett(uci: str) -> Merkmal:
        if uci not in cache:
            cache[uci] = brettmerkmale(board, zuege[uci])
        return cache[uci]

    bilanz = materialbilanz(board)
    remis = Remislage(
        bilanz,
        (lambda uci: loest_sofort_remis_aus(board, zuege[uci])) if abs(bilanz) >= KLAR_VORN_MATERIAL else (lambda _u: False),
    )
    wahl = waehle(policy, parameter, brett, remis, seed, halbzug)
    return wahl, merkmale(policy, wahl.uci, brett)
