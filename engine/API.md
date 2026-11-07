# Engine-Dienst: HTTP-Vertrag

Eingefroren ab Phase 0 (SCHACH-SPEC 15.1 Regel 4). Änderungen nur per eigenem kleinen Gerüst-PR, der diese Datei, `src/Schach.Application/Abstractions/EngineContracts.cs` und den Engine-Stub gemeinsam ändert.

Grundlage: SCHACH-SPEC Abschnitt 5.2. Der Dienst steht unter AGPL-3.0-or-later (`engine/LICENSE`) und spricht nur HTTP.

## Allgemein

- JSON in UTF-8, Feldnamen in `snake_case`.
- Züge immer in UCI, Umwandlung mit Kleinbuchstaben (`e7e8q`).
- `fen` ist die Ausgangsstellung, `moves` alle Halbzüge seit dieser Stellung. Gerechnet wird für die Stellung **nach** allen `moves`. Der Server schickt in der Regel die Grundstellung plus die ganze Zugfolge, damit Maia die Zughistorie sieht und Wiederholungen erkennbar bleiben.
- `wdl` ist immer ein Objekt `{w, d, l}` mit Wahrscheinlichkeiten in [0, 1], Summe 1, **aus Sicht der Seite am Zug** in der bewerteten Stellung. Bei `/move` ist das der Gegner, bei `/analyse` der Spieler.
- Kein Endpunkt hat Seiteneffekte. Wiederholte Anfragen sind sicher.

## `POST /move`

Kandidatenzüge für die Seite am Zug. `engine` wählt Maia-3 oder Fairy-Stockfish.

### Anfrage

| Feld | Typ | Pflicht | Bedeutung |
|---|---|---|---|
| `fen` | string | ja | Ausgangsstellung |
| `moves` | string[] | ja | Halbzüge seit `fen` in UCI, darf leer sein |
| `self_elo` | int | ja | geeichte Elo der Gegner-Spielstärke (Maia `SelfElo`) |
| `oppo_elo` | int | ja | Elo des Spielers, Gast 800 |
| `engine` | `"maia"` \| `"fairy"` | ja | gewünschte Engine |
| `fairy_skill` | int | nur bei `fairy` | `UCI_Elo` für Fairy-Stockfish, ab 500 |

```json
{
  "fen": "rnbqkbnr/pppppppp/8/8/8/8/PPPPPPPP/RNBQKBNR w KQkq - 0 1",
  "moves": ["e2e4"],
  "self_elo": 1000,
  "oppo_elo": 800,
  "engine": "maia"
}
```

### Antwort 200 (Maia)

`policy` enthält **alle** erlaubten Züge, absteigend nach `p` sortiert. Die Summe der `p` ist 1. Maias eigener `bestmove` wird nicht mitgeschickt (SCHACH-SPEC 5.3 Schritt 1), das Feld ist `null`. Das Beispiel ist gekürzt; echte Antworten listen alle 20 Züge.

```json
{
  "engine": "maia",
  "policy": [
    { "uci": "e7e5", "p": 0.412 },
    { "uci": "c7c5", "p": 0.188 },
    { "uci": "e7e6", "p": 0.097 },
    { "uci": "a7a6", "p": 0.003 }
  ],
  "wdl": { "w": 0.41, "d": 0.18, "l": 0.41 },
  "bestmove": null
}
```

### Antwort 200 (Fairy-Stockfish)

```json
{
  "engine": "fairy",
  "policy": null,
  "wdl": null,
  "bestmove": "e7e5"
}
```

## `POST /analyse`

Stockfish 19 in voller Stärke für Tipp und Remis-Bewertung. Nie derselbe Prozess wie der Gegner.

### Anfrage

| Feld | Typ | Pflicht | Bedeutung |
|---|---|---|---|
| `fen` | string | ja | Ausgangsstellung |
| `moves` | string[] | ja | Halbzüge seit `fen` |
| `movetime_ms` | int | ja | Rechenzeit, höchstens `SF_MAX_MOVETIME_MS` (800); größere Werte kappt der Dienst |
| `multipv` | int | ja | 1 bis 3 |

```json
{
  "fen": "r1bqkbnr/pppp1ppp/2n5/4p3/4P3/5N2/PPPP1PPP/RNBQKB1R w KQkq - 2 3",
  "moves": [],
  "movetime_ms": 800,
  "multipv": 2
}
```

### Antwort 200

`lines` ist nach Rang sortiert, `lines[0].uci` = `bestmove`. `cp` in Centipawns aus Sicht der Seite am Zug. Bei erkanntem Matt ist `cp` `null` und `mate` die Zahl der Züge (positiv: Seite am Zug setzt matt; negativ: wird matt gesetzt). Ohne Matt fehlt `mate` oder ist `null`.

```json
{
  "bestmove": "f1b5",
  "lines": [
    { "uci": "f1b5", "cp": 38, "mate": null, "wdl": { "w": 0.21, "d": 0.74, "l": 0.05 } },
    { "uci": "d2d4", "cp": 31, "mate": null, "wdl": { "w": 0.18, "d": 0.76, "l": 0.06 } }
  ]
}
```

Mattbeispiel:

```json
{
  "bestmove": "d8h4",
  "lines": [
    { "uci": "d8h4", "cp": null, "mate": 1, "wdl": { "w": 1.0, "d": 0.0, "l": 0.0 } }
  ]
}
```

## `GET /healthz`

200 erst nach dem Laden des Maia-Modells und Stockfish `isready`, vorher 503. Fairy-Stockfish startet lazy und zählt nicht.

```json
{ "status": "ok" }
```

```json
{ "status": "starting" }
```

## Fehler

Alle Fehler tragen `{ "error": "<code>", "detail": "<Text für Logs>" }`. `detail` enthält nie Spielerdaten.

| Status | `error` | Wann | Reaktion des .NET-Clients |
|---|---|---|---|
| 400 | `invalid_fen` | `fen` nicht lesbar | Programmfehler, nicht wiederholen |
| 400 | `illegal_move` | ein Zug in `moves` ist illegal | Programmfehler, nicht wiederholen |
| 400 | `invalid_request` | Feld fehlt oder außerhalb des Bereichs | Programmfehler, nicht wiederholen |
| 409 | `no_legal_moves` | Stellung ist matt oder patt | Programmfehler (Server prüft vorher) |
| 503 | `queue_full` | Maia-Warteschlange voll (`MAIA_QUEUE_MAX`) oder Frist 2 s überschritten | `/move`: „denkt nach“, erneut versuchen |
| 503 | `pool_busy` | Stockfish-Pool belegt, Wartegrenze 3 s | Tipp: „gerade nicht verfügbar“; Remis: Materialbilanz |
| 503 | `role_disabled` | Rolle nicht in `ENGINE_ROLES` | wie `queue_full` bzw. `pool_busy` |
| 503 | `starting` | Modell lädt noch | wie `queue_full` |

```json
{ "error": "queue_full", "detail": "maia queue at capacity (32)" }
```

## Umgebung

Siehe SCHACH-SPEC 5.2: `MAIA_TORCH_THREADS`, `MAIA_QUEUE_MAX`, `SF_POOL_SIZE`, `SF_MAX_MOVETIME_MS`, `FAIRY_POOL_SIZE`, `ENGINE_ROLES`, `SCHACH_ENGINE_FAKE_MODEL` (pytest ohne echtes Modell).
