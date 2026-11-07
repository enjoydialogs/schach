# Brett- und Regelbibliotheken für Touch und Server

Recherche zu [Issue #4](https://github.com/enjoydialogs/schach/issues/4), Stand 2026-11-07.
Begriffe nach `GLOSSARY.md` (Partie, Spieler, Gegner, Lernhilfe, Nachspielen, Zugfolge).

Herkunft der Aussagen: **Extern** = Primärquelle (Repo, npm, NuGet, Lizenzdatei), verlinkt.
**Intern** = aus diesem oder einem eigenen Repo. **Abgeleitet** = aus den Quellen gefolgert, nicht direkt belegt.

## Kurzantwort

1. **Die Repo-Lizenz entscheidet die Brett-UI.** `enjoydialogs/schach` hat noch keine Lizenz (Intern, GitHub-API: `license` leer).
   - Wenn das Frontend unter GPL-3.0 stehen darf: **chessground** (lichess) ist die stärkste Touch-Lösung.
   - Wenn die Lizenz frei bleiben soll (z. B. MIT): **react-chessboard** (MIT, passt zu React wie in Kronrat) oder **cm-chessboard** (MIT, ohne Framework).
2. **Regeln:** Der Server ist die Autorität. In .NET ist **Gera.Chess** (MIT) der einzige gepflegte, brauchbare Kandidat. Im Frontend **chess.js** (BSD-2-Clause) für Anzeige, PGN und Nachspielen. Alternative für identische Regeln: chess.js im .NET-Server über **Jint** ausführen (Abgeleitet, nicht getestet).
3. **Figuren:** **Cburnett-Satz** von Wikimedia Commons. Alle 12 Dateien sind auch unter **BSD-3-Clause** wählbar, also ohne Share-Alike. Achtung: Der Standardsatz von cm-chessboard (`staunty`) ist **CC BY-NC-SA 4.0** (nicht kommerziell) und muss ersetzt werden.
4. **Offen für die Spec:** Remis-Regeln. chess.js meldet dreifache Wiederholung und 50 Züge über `isDraw()`/`isGameOver()`; nach FIDE sind das nur Antragsremis, automatisch erst fünffache Wiederholung und 75 Züge. Keine der beiden Bibliotheken erkennt fünffach/75 von sich aus, und die Wiederholungs-Erkennung von Gera.Chess ist lückenhaft. Der Server muss Wiederholung und Remis-Regeln selbst festlegen.

## 1. Brett-UI im Browser

### Vergleich

| Kriterium | chessground | cm-chessboard | react-chessboard |
|---|---|---|---|
| Paket, Version | `@lichess-org/chessground` 10.5.0 (2026-10-20) | `cm-chessboard` 8.15.4 (2026-10-19) | `react-chessboard` 5.12.1 (2026-08-16) |
| Lizenz | GPL-3.0-or-later | MIT | MIT |
| Größe (min+gzip) | 12,0 KB, keine Abhängigkeiten | 8,5 KB, keine Abhängigkeiten | 21,6 KB plus React 19 (Peer), Abhängigkeit `@dnd-kit` |
| Tippen-Tippen | eingebaut ("Move pieces by click") | eingebaut (Zustände `pieceClickedThreshold`, `secondClickThreshold`) | selbst bauen über `onSquareClick` (Beispiel "Click to move" in der Doku) |
| Ziehen | eingebaut, Mindestdistanz `draggable.distance`, Geisterfigur | eingebaut, `DRAG_THRESHOLD = 4` px | eingebaut über dnd-kit, `dragActivationDistance` (Standard 1 px) |
| Touch-Feinheiten | `touchstart/move/end`, `blockTouchScroll`, `touchIgnoreRadius` | Maus- und Touch-Listener (keine Pointer Events) | dnd-kit-Sensoren, "Mobile support" laut README |
| Pfeile, Markierungen | SVG-Pfeile und Kreise, per Code über `drawable.autoShapes`, Ziel-Felder über `movable.showDests`, letzter Zug, Schach | Extensions `Markers` und `Arrows` (nicht im Kern, eigenes CSS) | Prop `arrows`, `arrowOptions`, `squareStyles` je Feld |
| Schachlogik | keine (nimmt erlaubte Ziele als `movable.dests`) | keine (Validator-Callback) | keine |
| Pflege | lichess-org, 1394 Sterne, letzter Push 2026-10-20 | Einzelentwickler (shaack), 309 Sterne, Push 2026-10-19 | Clariity, 550 Sterne, Push 2026-08-16 |
| Einsatz | lichess.org Web und Mobil | chessmail.de | k. A. |

Quellen (Extern):
- chessground: [README](https://github.com/lichess-org/chessground/blob/master/README.md) (Features, GPL-Hinweis), [config.ts](https://github.com/lichess-org/chessground/blob/master/src/config.ts) (`blockTouchScroll`, `touchIgnoreRadius`, `draggable.distance`, `drawable.autoShapes`), [npm](https://www.npmjs.com/package/@lichess-org/chessground).
- cm-chessboard: [README](https://github.com/shaack/cm-chessboard/blob/master/README.md), [VisualMoveInput.js](https://github.com/shaack/cm-chessboard/blob/master/src/view/VisualMoveInput.js), [LICENSE](https://github.com/shaack/cm-chessboard/blob/master/LICENSE), [npm](https://www.npmjs.com/package/cm-chessboard).
- react-chessboard: [README](https://github.com/Clariity/react-chessboard/blob/main/README.md), [Options-API](https://github.com/Clariity/react-chessboard/blob/main/docs/D_OptionsApi.mdx), [Basis-Beispiele](https://github.com/Clariity/react-chessboard/blob/main/docs/B_BasicExamples.mdx), [npm](https://www.npmjs.com/package/react-chessboard).
- Größen: bundlephobia-API (`/api/size?package=...`) für die genannten Versionen. Sterne und Push-Daten: GitHub-API, abgerufen 2026-11-07.

### Bewertung für das Pixel 7

- **chessground** ist für Handy-Touch am weitesten ausgereift (Abgeleitet: lichess nutzt es in Web und Mobil; die Touch-Optionen zielen genau auf Fehlgriffe und Scrollen). Erlaubte Züge, Pfeile und Kreise für Lernhilfen sind per Konfiguration da.
  - **Lizenzfolge:** Das README sagt wörtlich, dass ein Werk mit chessground nur unter GPL verbreitet werden darf und der Quellcode an die Nutzer der Website herausgegeben werden muss. Ein öffentliches Repo erfüllt die Quellcode-Pflicht, aber das Frontend (mindestens) muss dann GPL-3.0 sein. Das entscheidet der Besitzer.
  - React-Wrapper existieren nur von Dritten (im README gelistet); ein eigener dünner Wrapper ist üblich (Abgeleitet).
- **react-chessboard** passt zum Stack: Kronrat-Frontend nutzt React 19 und Vite (Intern, `src/Kronrat.Web/package.json`), react-chessboard verlangt React `^19.0.0`. Tippen-Tippen muss die App selbst bauen; Pfeile sind standardmäßig per Rechtsklick zeichenbar, für Lernhilfen reicht die Prop `arrows` (Zeichnen per Hand kann mit `allowDrawingArrows: false` aus).
- **cm-chessboard** ist am kleinsten und hat Tippen-Tippen und Ziehen eingebaut, ist aber kein React-Baustein und hängt an einem Einzelentwickler. Standard-Figurensatz ersetzen (siehe Abschnitt 3).
- **Nicht weiter betrachtet:** `chessboardjs` (oakmac), da jQuery-basiert und seit Jahren ohne Release (Abgeleitet, nicht im Detail geprüft).

**Vor der Entscheidung prüfen:** ein kurzer Prototyp auf dem Pixel 7 mit Tippen-Tippen, Ziehen und Scrollen der Seite neben dem Brett. Die Tabelle belegt Funktionen, nicht das Gefühl am Gerät.

## 2. Regeln: Frontend (TypeScript) und Server (.NET)

### TypeScript

| Kandidat | Lizenz | Stand | Bewertung |
|---|---|---|---|
| **chess.js** 1.4.0 | BSD-2-Clause | npm 2025-06-14, Repo-Push 2026-08-11, 4449 Sterne | De-facto-Standard. Zuggenerierung, FEN, PGN, Matt, Patt, Remis, `attackers()` und `isAttacked()` (nützlich für "ungedeckte Figur"). 12,0 KB gzip. |
| chessops 0.15.1 | GPL-3.0-or-later | 2026-07-12 | Von lichess, sehr korrekt, aber GPL wie chessground. Nur sinnvoll, wenn ohnehin GPL. |

Quellen (Extern): [chess.js Repo](https://github.com/jhlywa/chess.js), [chess.js Doku](https://jhlywa.github.io/chess.js/), [chess.ts Quelltext](https://github.com/jhlywa/chess.js/blob/master/src/chess.ts), [chessops](https://github.com/niklasf/chessops).

**Wichtig für die Spec (Extern, Quelltext `src/chess.ts`):**
```ts
isDraw(): boolean {
  return (
    this.isDrawByFiftyMoves() ||
    this.isStalemate() ||
    this.isInsufficientMaterial() ||
    this.isThreefoldRepetition()
  )
}
isGameOver(): boolean { return this.isCheckmate() || this.isDraw() }
```
chess.js **meldet** also dreifache Wiederholung und 50-Züge-Regel als Remis. chess.js ist ohne Oberfläche: `move()` verweigert danach keine Züge (Extern, `move()` in `src/chess.ts` prüft `isGameOver()` nicht), die App entscheidet, welche Prädikate die Partie beenden. Fünffache Wiederholung und 75 Züge erkennt chess.js nicht; das ließe sich aus `history()` und dem Halbzugzähler im FEN berechnen (Abgeleitet). Die gerenderte Doku beschreibt `isDraw()` enger als der Quelltext; maßgeblich ist der Quelltext.

Nach den [FIDE-Regeln (gültig ab 2023-01-01)](https://handbook.fide.com/chapter/E012023) sind dreifache Wiederholung (Art. 9.2) und 50 Züge (Art. 9.3) Antragsremis; automatisch remis sind fünffache Wiederholung und 75 Züge (Art. 9.6).

### .NET

| Kandidat (NuGet) | Lizenz | Stand | Bewertung |
|---|---|---|---|
| **Gera.Chess** 1.2.0 | MIT | NuGet 2025-02-10, net8.0; Repo-Push 2026-09-15 (Version 1.3.0 im Repo, nicht auf NuGet) | Zuggenerierung und -prüfung, SAN, FEN, PGN, Matt, Patt, optionale Endspielregeln `AutoEndgameRules` (InsufficientMaterial, FiftyMoveRule, Repetition), Zurücknehmen. Unit-Tests vorhanden, aber **keine Perft-Tests** gefunden. **Wiederholung lückenhaft** (siehe unten). |
| ChessDotNet 1.0.0 | MIT | README: "I don't intend to continue maintaining this project" | Nicht gepflegt, Wiederholung lange nur "geplant". Ausscheiden. |
| pax.chess 0.7.0 | AGPL-3.0-or-later | 2026-06-08, net10.0 | AGPL zwingt das ganze Backend unter AGPL. Nur wenn das gewollt ist. |
| Rudzoft.ChessLib 0.0.3 | MIT | NuGet 2022-10-15 (Repo aktiv 2026-09) | Engine-orientiert (Bitboards, Perft), aber NuGet-Paket uralt; PGN und Remis-Regeln nicht im README. Eher für eigene Engine. |
| ChessDotNetCore 1.6.0 | **keine Lizenz** | 2024-05-16 | Ohne Lizenz nicht nutzbar. |

Quellen (Extern): [Gera.Chess Repo](https://github.com/Geras1mleo/Chess), [Gera.Chess LICENSE.md](https://github.com/Geras1mleo/Chess/blob/master/LICENSE.md), [NuGet Gera.Chess](https://www.nuget.org/packages/Gera.Chess/), [Chess.NET README](https://github.com/thomas-daniels/Chess.NET), [pax.chess](https://github.com/ipax77/pax.chess), [Rudzoft.ChessLib](https://github.com/rudzen/ChessLib), [ChessDotNetCore](https://github.com/lrepaty/ChessDotNetCore). NuGet-Daten über die NuGet-Registrierungs-API.

**Gera.Chess-Endspielregeln im Quelltext (Extern):**
- [`FiftyMoveRule.cs`](https://github.com/Geras1mleo/Chess/blob/master/ChessLibrary/ChessBoard/EndGameTypes/FiftyMoveRule.cs): `MAX_HALF_MOVE_CLOCK = 100`, also 50 Züge. Keine 75-Züge-Regel.
- [`RepetitionRule.cs`](https://github.com/Geras1mleo/Chess/blob/master/ChessLibrary/ChessBoard/EndGameTypes/RepetitionRule.cs): vergleicht nur die Stellungen vor 0, 4 und 8 Halbzügen und nur, wenn die letzten 8 Halbzüge umkehrbar waren. Das erkennt Hin-und-her-Wiederholung, aber keine allgemeine dreifache Wiederholung mit dazwischenliegenden anderen Zügen (Abgeleitet aus dem Code). Keine fünffache Wiederholung. Offenes Issue [#31 "Repetition rule optimisation"](https://github.com/Geras1mleo/Chess/issues/31).
- Folge: Wiederholung im Server selbst zählen, z. B. über die ersten vier FEN-Felder je Stellung (Abgeleitet). `AutoEndgameRules` dann ohne `Repetition` setzen.

### Gleiche Regeln auf beiden Seiten?

Zwei verschiedene Bibliotheken können in Randfällen abweichen (Rochade-Rechte aus FEN, En passant, Remis-Erkennung). Optionen:

- **A. Gera.Chess im Server, chess.js im Browser.** Server entscheidet; der Browser zeigt nur an. Abweichungen fängt eine **gemeinsame Testsuite** ab: dieselben FEN-Stellungen mit erwarteter Zugliste (und Perft-Zahlen) laufen in beiden Testprojekten (Abgeleitet).
- **B. Server liefert erlaubte Züge mit.** Zu jeder Stellung schickt der Server die Liste erlaubter Ziel-Felder; chessground (`movable.dests`) und die anderen Bretter nehmen genau so etwas entgegen. Dann gibt es im Browser keine zweite Regelquelle für die Partie; chess.js bleibt nur für PGN und Nachspielen offline (Abgeleitet). Kostet eine Antwort-Runde, aber jeder Zug geht ohnehin an den Server.
- **C. chess.js auch im Server, über Jint.** Jint ist ein JavaScript-Interpreter für .NET, BSD-2-Clause, aktiv gepflegt (NuGet 4.17.0, Push 2026-11-07). Dann sind die Regeln auf beiden Seiten identisch. **Abgeleitet, nicht getestet:** ob das ESM-Bundle von chess.js in Jint ohne Anpassung läuft, und ob die Geschwindigkeit für Uhr und Engine-Schleife reicht.

Hinweis: Die Engine auf dem VPS (UCI) prüft keine Züge; sie braucht trotzdem eine Regelbibliothek davor (Abgeleitet).

Empfehlung: **A plus B** (Gera.Chess autoritativ, Wiederholung im Server selbst gezählt, erlaubte Züge vom Server, chess.js für PGN und Nachspielen, gemeinsame Test-Stellungen). C als Fallback, falls Gera.Chess in den Tests Fehler zeigt.

## 3. Figurensatz (SVG, schlicht)

| Satz | Lizenz | Stil | Quelle |
|---|---|---|---|
| **Cburnett** (Wikimedia "Standard") | wählbar: GFDL 1.2+, CC BY-SA 3.0, **BSD-3-Clause**, GPL-2.0+ | klassisch, schwarz/weiß, klare Linien | Commons-Seiten aller 12 Dateien `Chess_{k,q,r,b,n,p}{l,d}t45.svg`, Wikitext `{{self|GFDL|migration=relicense|BSD|GPL}}` |
| mono (lichess) | GPL-2.0+ | einfarbig, sehr schlicht | lichess `COPYING.md` |
| rhosgfx (lichess) | CC0 1.0 | k. A. | lichess `COPYING.md` |
| chessnut (lichess) | Apache-2.0 | flach | lichess `COPYING.md` |
| kiwen-suwi, Firi (lichess) | CC BY 4.0 | k. A. | lichess `COPYING.md` |
| shapes (lichess) | CC BY-SA 4.0 | geometrisch | lichess `COPYING.md` |
| staunty (Standard in cm-chessboard) | **CC BY-NC-SA 4.0** | | Kopf von `cm-chessboard/assets/pieces/staunty.svg` |

Quellen (Extern): [Commons Chess_klt45.svg](https://commons.wikimedia.org/wiki/File:Chess_klt45.svg) (stellvertretend; die übrigen 11 Seiten tragen denselben Lizenzbaustein), [lichess COPYING.md](https://github.com/lichess-org/lila/blob/master/COPYING.md), [cm-chessboard staunty.svg](https://github.com/shaack/cm-chessboard/blob/master/assets/pieces/staunty.svg), [cm-chessboard standard.svg](https://github.com/shaack/cm-chessboard/blob/master/assets/pieces/standard.svg).

Hinweise:
- **Cburnett unter BSD-3-Clause** ist die einfachste Wahl: Hinweis auf Urheber und Lizenztext behalten, kein Share-Alike, verträglich mit MIT und GPL. react-chessboard bringt Cburnett schon mit (`src/pieces.tsx`, dort als CC BY-SA 3.0 vermerkt), chessground ebenso (`assets/chessground.cburnett.css`). lichess führt Cburnett als GPLv2+; das ist eine der vier Optionen, kein Widerspruch.
- Die **BSD-Option gilt für Cburnetts Originaldateien auf Commons.** Die gebündelten Kopien in react-chessboard und cm-chessboard sind bearbeitet und tragen eigene Hinweise (CC BY-SA 3.0). Für BSD die SVGs direkt von Commons nehmen; sonst dem Hinweis der jeweiligen Kopie folgen.
- cm-chessboard nennt im Kopf von `standard.svg` zusätzlich den Autor **Rfc1394** und die Bearbeitung durch shaack. Bei Nutzung dieser Datei alle nennen.
- Die lichess-Sätze außer Cburnett sind **nur nach lichess-Angabe** erfasst, Upstream-Lizenzen nicht geprüft. Vor Nutzung die Original-Repos prüfen.
- **NC-Sätze meiden** (staunty, california, cardinal usw.): "NonCommercial" ist rechtlich unscharf und blockiert spätere Werbung oder Bezahlfunktionen.
- Für "wenige Farben": Cburnett lässt sich per CSS/SVG-`fill` umfärben (Abgeleitet). Geänderte Dateien bei CC BY-SA als geändert kennzeichnen; unter BSD reicht der Hinweis.

## 4. Lizenz-Verträglichkeit mit einem öffentlichen Repo

- **Ohne Lizenzdatei** ist ein öffentliches Repo "alle Rechte vorbehalten" (Abgeleitet aus Urheberrecht). Das verträgt sich **nur mit den freizügigen Kandidaten** (MIT, BSD, Apache, CC0), deren Hinweise mitgeliefert werden. **chessground oder chessops gehen erst, wenn das Frontend unter GPL-3.0 steht**; ein Hinweis-File allein reicht dafür nicht. Eine Lizenzwahl gehört in die Spec.
- **MIT, BSD-2/3, Apache-2.0, CC0:** mit jeder Repo-Lizenz verträglich. Pflicht: Copyright- und Lizenzhinweis mitliefern (z. B. `THIRD-PARTY-NOTICES.md` und im Impressum/Über-Bereich der App).
- **GPL-3.0 (chessground, chessops):** Das Frontend, das sie bündelt, muss unter GPL-3.0 verbreitet werden; Quellcode ist öffentlich, das erfüllt die Herausgabepflicht. Backend über API getrennt ist davon nicht berührt (Abgeleitet, übliche Auslegung; keine Rechtsberatung).
- **AGPL-3.0 (pax.chess):** würde das Backend erfassen, weil es übers Netz genutzt wird. Meiden, wenn das Backend nicht AGPL werden soll.
- **CC BY-SA 3.0/4.0 (Figuren):** Namensnennung und Weitergabe geänderter Figuren unter gleicher Lizenz; betrifft nur die Grafiken, nicht den Code (Abgeleitet). Bei Cburnett durch BSD-Wahl vermeidbar.
- **CC BY-NC-SA:** nicht verwenden.
- **Keine Lizenz (ChessDotNetCore):** nicht verwenden.

## Offen für die Spec

1. **Repo-Lizenz:** MIT (Bibliotheken: react-chessboard oder cm-chessboard) oder GPL-3.0 (chessground möglich).
2. **Brett:** nach Lizenz und kurzem Test auf dem Pixel 7 festlegen.
3. **Remis-Regeln:** automatisch bei dreifacher Wiederholung und 50 Zügen (einfach für Anfänger) oder FIDE-nah (Antrag, automatisch erst bei fünffacher Wiederholung und 75 Zügen). Keine der beiden Bibliotheken liefert fünffach/75 fertig; Gera.Chess erkennt Wiederholung nur teilweise. Der Server legt das Ergebnis fest; bei Option B betrifft die Frontend-Logik nur die Anzeige.
4. **Regel-Architektur:** Option A plus B wie oben, mit gemeinsamer Testsuite aus FEN-Stellungen.
5. **Gera.Chess-Version:** NuGet 1.2.0 gegen Repo 1.3.x klären; falls nötig aus dem Repo bauen oder auf Release warten.
