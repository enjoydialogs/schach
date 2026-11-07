# FEN-Suite

Gemeinsame Testfälle für Gera.Chess (Server), chess.js (Browser) und python-chess (Engine), SCHACH-SPEC 15.2. Append-Register: Fälle nur anhängen, `id` nie ändern oder wiederverwenden. Phase 0a legt Format und 11 Fälle an, S1a bringt die Suite auf mindestens 40.

## Format

Eine Datei je Kategorie. Jede Datei ist ein Objekt:

```json
{ "kategorie": "<name>", "faelle": [ { "id": "...", "beschreibung": "...", "fen": "...", ... } ] }
```

Gemeinsame Felder jedes Falls:

| Feld | Pflicht | Bedeutung |
|---|---|---|
| `id` | ja | eindeutig über alle Dateien, `kategorie-kurzname` |
| `beschreibung` | ja | ein Satz, Deutsch |
| `fen` | ja | vollständige FEN mit 6 Feldern |
| `zugfolge` | nein | UCI-Halbzüge, die vor der Prüfung auf `fen` gespielt werden |

Kategoriefelder:

| Datei | `kategorie` | Felder |
|---|---|---|
| `perft.json` | `perft` | `perft`: Objekt Tiefe → Knotenzahl, Tiefe 1 bis höchstens 3 |
| `zuege.json` | `zuege` | `anzahl` (optional, Zahl erlaubter Züge), `enthaelt` und `enthaeltNicht` (UCI-Listen, optional) |
| `remis.json` | `remis` | `erwartet`: `Patt`, `Material`, `Wiederholung`, `FuenfzigZuege` oder `null` (kein Remis) nach der Prüfreihenfolge in SCHACH-SPEC 3.4 |
| `mattmaterial.json` | `mattmaterial` | `weissHatMattmaterial`, `schwarzHatMattmaterial` (bool, Regel 3.4.6) |
| `ungedeckt.json` | `ungedeckt` | `seite` (`Weiss` \| `Schwarz`), `ungedeckt`: Felder angegriffener, ungedeckter eigener S/L/T/D (SCHACH-SPEC 7.2), sortiert |
