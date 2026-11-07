# Engine-Recherche: schwach bis meisterlich, menschlich, mit Persönlichkeit

Ticket: [#2 Welche Engine spielt schwach bis meisterlich und mit Stil?](https://github.com/enjoydialogs/schach/issues/2)
Map: [#1](https://github.com/enjoydialogs/schach/issues/1)
Stand: 2026-11-07. Begriffe nach `GLOSSARY.md` (Gegner, Spielstärke, Persönlichkeit, Partie, Lernhilfe). „Engine" meint hier nur die technische Komponente, nie den Gegner.

Kennzeichnung:

- **Verifiziert**: steht so in der verlinkten Primärquelle (Quellcode, offizielle Doku, Paper, Release-Notes).
- **Abgeleitet**: eigene Schlussfolgerung aus den Quellen, nicht direkt belegt.

## Kurzantwort

1. **Stockfish allein reicht nicht bis 600 hinunter.** `UCI_Elo` hat ein Minimum von 1320, und diese Zahl ist CCRL-Blitz-Engine-Elo, keine menschliche Elo. **Verifiziert.**
2. **Empfehlung (Abgeleitet):** Zwei Engines mit getrennten Aufgaben.
   - **Maia-3** (CSSLab, 2026) erzeugt die Züge aller Gegner. Es sagt menschliche Züge abhängig von einer Ziel-Elo voraus und ist auf Lichess-Partien von 600 bis 2600 trainiert. Damit deckt es die Spanne „Anfänger ca. 600" bis „Meister ca. 2200" als einziges geprüftes Modell ab.
   - **Stockfish 19** in voller Stärke liefert den Tipp auf Wunsch und die Stellungsbewertung für die Remis-Annahme.
   - **Fallback:** Fairy-Stockfish (`UCI_Elo` ab 500, `Skill Level` ab -20). Lichess nutzt es für seine schwächste Computerstufe. Es spielt aber nicht menschlich, sondern streut zufällige Fehler.
3. **Persönlichkeit:** Keine der geprüften Engines hat eine Option für Spielstil. Der Stil muss aus einer eigenen Zugauswahl im Backend kommen: Kandidatenzüge mit Wahrscheinlichkeiten holen, nach Merkmalen (Schlagzug, Schach, Angriff auf die Königszone, Risiko) gewichten, dann ziehen. **Abgeleitet, Entwurf.**
4. **Größte Unsicherheiten:**
   - Ob Maia-3 bei Elo 600 wirklich wie ein 600er spielt, ist **nicht belegt**. Maia-1 spielt nachweislich deutlich stärker als seine Ziel-Elo.
   - Die **Lizenz der Maia-3-Gewichte** ist nirgends angegeben.
   - CPU, RAM und Antwortzeit auf `srv1952305` sind **nicht gemessen**.

## 1. Stockfish: Mindest-Spielstärke

### Fakten

- `UCI_Elo`: `type spin default 1320 min 1320 max 3190`. `UCI_LimitStrength` muss aktiv sein; `UCI_Elo` hat dann Vorrang vor `Skill Level`. **Verifiziert**: [Stockfish-Wiki, UCI-Protokoll](https://github.com/official-stockfish/Stockfish/wiki/UCI-Protocol-and-Stockfish-Commands).
- Im Quellcode: `LowestElo = 1320`, `HighestElo = 3190`. Die Elo wird über ein Polynom 3. Grades in einen internen Skill-Level 0 bis 19 umgerechnet. **Verifiziert**: [`src/search.h`](https://github.com/official-stockfish/Stockfish/blob/master/src/search.h).
- Die Skala ist **CCRL-Blitz-Elo**, geeicht gegen die Engine Stash: „Skill 0 .. 19 now covers CCRL Blitz Elo from 1320 to 3190, approximately", Genauigkeit laut Commit etwa ±100 Elo. **Verifiziert**: [Commit a08b8d4](https://github.com/official-stockfish/Stockfish/commit/a08b8d4e9711c2) (Januar 2023).
- `Skill Level`: 0 bis 20, Standard 20. **Verifiziert**: [`src/engine.cpp`](https://github.com/official-stockfish/Stockfish/blob/master/src/engine.cpp).
- So entsteht der schwache Zug: Stockfish rechnet mindestens 4 Kandidaten (MultiPV), addiert einen zufälligen Bonus auf schlechtere Züge (`weakness = 120 - 2 * level`) und wählt bei Suchtiefe `1 + level`. **Verifiziert**: [Stockfish-FAQ](https://github.com/official-stockfish/Stockfish/wiki/Stockfish-FAQ#how-do-skill-level-and-uci_elo-work), [`src/search.cpp`](https://github.com/official-stockfish/Stockfish/blob/master/src/search.cpp) (`Skill::pick_best`).

### Bewertung

- Engine-Elo und menschliche Elo sind verschiedene Skalen. Ein Wert von 1320 CCRL lässt sich nicht verlässlich auf Lichess- oder FIDE-Elo umrechnen. **Abgeleitet.**
- Für „Meister ca. 2200" reicht Stockfish problemlos. Für 600 bis etwa 1200 fehlt eine kalibrierte Einstellung. **Abgeleitet.**
- Der Fehlermechanismus ist Zufall über eine flache Suche. Er erzeugt keine typisch menschlichen Fehler (z. B. eine hängende Figur übersehen, aber eine Mattdrohung sehen). **Abgeleitet.**

## 2. Alternativen für schwaches, menschliches Spiel

| Kandidat | Spanne | Menschlich? | Lizenz | Laufzeit |
|---|---|---|---|---|
| Stockfish 19 `UCI_Elo` | 1320 bis 3190 CCRL | nein, Zufallsfehler | GPL-3.0 | C++-Binary |
| Fairy-Stockfish `UCI_Elo` | 500 bis 2850 CCRL | nein, Zufallsfehler | GPL-3.0 | C++-Binary |
| Maia-1 (lc0-Netze) | 1100 bis 1900 Lichess | ja | GPL-3.0 | lc0 |
| Maia-2 | „across Elo levels" | ja | Code MIT, Gewichte ungeprüft | Python/PyTorch |
| **Maia-3** | **600 bis 2600 Lichess** | **ja** | Code AGPL-3.0, Gewichte unklar | Python/PyTorch |
| Eigene MultiPV-Fehlerrate auf Stockfish | frei wählbar | nur so gut wie die Fehlerregel | GPL-3.0 | C++ + Backend-Logik |

### Fairy-Stockfish

- `Skill Level` -20 bis 20, `UCI_Elo` 500 bis 2850 (Standard 1350). **Verifiziert**: [`src/ucioption.cpp`](https://github.com/fairy-stockfish/Fairy-Stockfish/blob/master/src/ucioption.cpp).
- Kalibrierung gegen CCRL-Elo (Anker goldfish 1.13 = 2000), Bedenkzeit 60+0.6. Unterhalb von etwa 1347 wird die Kurve über eine Formel fortgesetzt, nicht gemessen. **Verifiziert** (Kommentar und Formel): [`src/search.cpp`](https://github.com/fairy-stockfish/Fairy-Stockfish/blob/master/src/search.cpp). Dass der Bereich unter 1347 extrapoliert ist: **Abgeleitet** aus der Formel.
- Lichess spielt „gegen den Computer" mit Fairy-Stockfish: Zugaufträge (keine Analyse) laufen mit `EngineFlavor::MultiVariant`, das ist Fairy-Stockfish. Stufe 1 nutzt `Skill Level -9`, Tiefe 5, 50 ms. Stufe 8 nutzt `Skill Level 20`, Tiefe 22, 1000 ms. **Verifiziert**: fishnet [`src/api.rs`](https://github.com/lichess-org/fishnet/blob/main/src/api.rs), [`src/queue.rs`](https://github.com/lichess-org/fishnet/blob/main/src/queue.rs), [`src/assets.rs`](https://github.com/lichess-org/fishnet/blob/main/src/assets.rs).
- Folgerung: Es gibt einen produktiven Präzedenzfall für Gegner unterhalb der Stockfish-Grenze. Menschlich wirkt das Spiel aber nicht. **Abgeleitet.**

### Maia-1 und Maia-2

- Maia-1: 9 Modelle für 1100 bis 1900 in 100er-Schritten, betrieben in lc0 mit `go nodes 1`. Laut README sind die Modelle „stronger than the rating they are trained on". **Verifiziert**: [CSSLab/maia-chess](https://github.com/CSSLab/maia-chess).
- Belegt durch die öffentlichen Lichess-Wertungen der Maia-1-Konten (abgerufen 2026-11-07 über `https://lichess.org/api/user/<name>`). Die Profiltexte nennen das Modell, z. B. maia1: „trained by learning from over 10 million Lichess games between 1100s". **Verifiziert.**

  | Konto | Ziel | Blitz | Rapid | Classical |
  |---|---|---|---|---|
  | maia1 | 1100 | 1395 | 1548 | 1666 |
  | maia5 | 1500 | 1454 | 1649 | 1676 |
  | maia9 | 1900 | 1627 | 1791 | 1895 |

  Folgerung: Die schwachen Stufen spielen deutlich stärker als ihr Ziel, und die Spanne ist gestaucht (1100 bis 1900 Ziel ergibt etwa 1400 bis 1630 Blitz). Für eine 600er-Stufe ist das ein Warnsignal. **Abgeleitet.**
- Maia-2: ein Modell für alle Spielstärken, Eingabe `active_elo` und `opponent_elo`, läuft auf CPU, Code unter MIT-Lizenz. Das README empfiehlt für neue Projekte Maia-3. Nicht geprüft: die Lizenz der Maia-2-Gewichte und ob die Trainingsdaten bis 600 hinunterreichen. **Verifiziert**: [CSSLab/maia2](https://github.com/CSSLab/maia2).

### Maia-3 (Empfehlung für die Zugwahl)

- Paper: „Chessformer: A Unified Architecture for Chess Modeling", ICLR 2026, arXiv 2605.19091 (eingereicht 18. Mai 2026). 57,1 % Treffer beim Vorhersagen menschlicher Züge. **Verifiziert**: [arXiv](https://arxiv.org/abs/2605.19091).
- Trainingsdaten: „20 bins uniformly spanning 600 to 2600 Elo with 100-point intervals", plus je ein Bin unter 600 und über 2600. Die Elo wird als gelernte Einbettung zwischen 0 und 5000 interpoliert. **Verifiziert**: [Paper, HTML-Fassung](https://arxiv.org/html/2605.19091).
- Die Maia-Website nennt dieselbe Spanne: jede Lichess-Wertung „from 600 to 2600". **Verifiziert**: [maiachess.com](https://www.maiachess.com).
- Das Paper prüft **nicht**, ob das Modell in echten Partien auf der eingestellten Elo spielt. Es misst nur die Zug-Trefferquote je Elo-Bin. **Verifiziert** (Fehlen der Messung im Paper).
- Modelle: 5M (empfohlen für CPU), 23M, 79M Parameter. Das 5M-Checkpoint ist 20.968.049 Byte (ca. 21 MB) groß. **Verifiziert**: [CSSLab/maia3 README](https://github.com/CSSLab/maia3), [Hugging Face Maia3-5M](https://huggingface.co/UofTCSSLab/Maia3-5M).
- Betrieb als UCI-Engine: `maia3-uci --model maia3-5m --device cpu --no-use-amp`, Optionen `Elo`, `SelfElo`, `OppoElo` (je 0 bis 5000), `Temperature` (0 = immer der wahrscheinlichste Zug), `TopP`, `MultiPV`. Mit `--use-uci-history` wird die Zugfolge als Verlauf genutzt. **Verifiziert**: README und [`maia3/uci.py`](https://github.com/CSSLab/maia3/blob/main/maia3/uci.py).
- `go` macht genau einen Vorwärtsdurchlauf des Netzes und ignoriert Zeitvorgaben. Die MultiPV-Zeilen enthalten Rang und WDL, aber nicht die Zugwahrscheinlichkeit. **Verifiziert**: `maia3/uci.py` (`cmd_go`, `score_moves`).
- Abhängigkeiten: `torch`, `numpy`, `python-chess`, `huggingface-hub`. **Verifiziert**: [`requirements.txt`](https://github.com/CSSLab/maia3/blob/main/requirements.txt).

Bewertung (**Abgeleitet**):

- Maia-3 ist das einzige geprüfte Modell, dessen Trainingsdaten bei 600 beginnen und menschliche Fehler lernen. Das passt am besten zu „menschlich, Anfänger bis Meister".
- Wegen des Maia-1-Befunds muss die Spielstärke empirisch nachgeeicht werden: z. B. mit `Temperature` > 0 (mehr Streuung), einer niedrigeren eingestellten Elo als die Ziel-Elo des Gegners oder Testpartien gegen Fairy-Stockfish-Stufen.
- Die Lichess-Skala liegt nach verbreiteter Erfahrung über FIDE. Die Map nennt keine Skala; für die festen Elo der Gegner reicht eine interne Skala. Diese Zuordnung ist **nicht belegt**.

## 3. Persönlichkeit und Zugwahl

### Was die Engines anbieten

- Stockfish 19 hat keine Stil- oder Contempt-Option. Die vollständige Optionsliste in `engine.cpp` enthält nur Threads, Hash, MultiPV, Skill Level, UCI_LimitStrength, UCI_Elo, UCI_ShowWDL, Move Overhead, Ponder, Syzygy-, NUMA- und Netz-Optionen. **Verifiziert**: [`src/engine.cpp`](https://github.com/official-stockfish/Stockfish/blob/master/src/engine.cpp).
- lc0 hat seit v0.30 „WDL contempt": die Gewinn/Remis/Verlust-Schätzung wird an die Elo-Differenz angepasst; höhere Werte können zu aggressivem Spiel bis zum Überziehen führen. Laut Blog wirkt das nicht auf die Policy und braucht eine echte Suche von mindestens etwa 10.000 Knoten. **Verifiziert**: [lc0-Blog zu v0.30](https://lczero.org/blog/2023/07/the-lc0-v0.30.0-wdl-rescale/contempt-implementation).
- Folgerung: Für Maia-3 (ein Vorwärtsdurchlauf, keine Suche) ist lc0-Contempt kein Stilhebel. Ein lc0 mit Suche wäre wieder zu stark und nicht menschlich. **Abgeleitet.**
- Maia-3 hat `Temperature` und `TopP`. Das steuert Streuung bzw. Risiko, nicht Richtung (z. B. Angriff). **Verifiziert** (Optionen), Deutung: **Abgeleitet.**

### Vorschlag: Stil im Backend (Entwurf, Abgeleitet)

1. Maia-3 liefert für die Stellung die Wahrscheinlichkeiten der wahrscheinlichsten menschlichen Züge (z. B. Top 5 bis 10) bei der Elo des Gegners.
2. Das Backend berechnet je Zug einfache Merkmale: Schlagzug, Schach, Zug in oder auf die Königszone des Spielers, Bauernsturm, Figurenopfer, Tausch (eher defensiv), Rückzug.
3. Die Persönlichkeit ist ein Gewichtsvektor, z. B. aggressiv: Schlagzug ×1,5, Schach ×1,5, Königszone ×1,3, Tausch ×0,8.
4. Neue Wahrscheinlichkeit = alte × Gewicht, normieren, dann ziehen (mit der Temperatur des Gegners).
5. Optional für starke Gegner: Kandidaten, die laut Stockfish mehr als X Bauerneinheiten verlieren, abwerten, damit „aggressiv + Meister" nicht wie ein Anfänger opfert.

Hürde: Die UCI-Ausgabe von Maia-3 enthält keine Wahrscheinlichkeiten (nur die Rangfolge). Für Schritt 1 braucht es die Python-API direkt oder einen kleinen Fork von `uci.py`, z. B. eine zusätzliche `info string`-Zeile mit den Wahrscheinlichkeiten. **Verifiziert** (Fehlen in der Ausgabe), Lösung: **Abgeleitet.**

Damit ist der Stil in der Zugwahl spürbar, bleibt aber innerhalb dessen, was Menschen der Spielstärke plausibel ziehen würden. Persönlichkeit und Spielstärke bleiben unabhängig, wie in der Map verlangt. **Abgeleitet.**

## 4. CPU, RAM und Antwortzeit

Nichts davon wurde auf `srv1952305` gemessen. Die Hardware des VPS (Kerne, RAM) ist in keiner Quelle dieses Repos dokumentiert.

Bekannte Größen:

- Stockfish 19 (Release-Tag `sf_19`) bettet ein NNUE-Netz von 98.511.183 Byte (ca. 99 MB) ein (`nn-1a298aa575a0.nnue`); der aktuelle Entwicklungsstand (`master`) nutzt bereits ein größeres Netz mit ca. 136 MB (`nn-d1ee674a62ca.nnue`). Stockfish 19 hat das zweite, kleine Netz abgeschafft. **Verifiziert**: [`src/evaluate.h` in sf_19](https://github.com/official-stockfish/Stockfish/blob/sf_19/src/evaluate.h), [official-stockfish/networks](https://github.com/official-stockfish/networks), [Release-Notes Stockfish 19](https://github.com/official-stockfish/Stockfish/releases/tag/sf_19) (5. September 2026).
- Mehrere Stockfish-Prozesse mit demselben Netz teilen sich dessen Speicher über Shared Memory (Linux, ab Stockfish 19 überarbeitet). Der Commit nennt Lichess-artige parallele Partien ausdrücklich als Nutzen. **Verifiziert**: [`src/shm.h`-Historie](https://github.com/official-stockfish/Stockfish/commits/master/src/shm.h), Release-Notes Stockfish 19.
- Standard je Stockfish-Prozess: `Threads 1`, `Hash 16` MB. **Verifiziert**: `src/engine.cpp`.
- Maia-3-5M: ca. 21 MB Gewichte plus PyTorch-Laufzeit. Ein Vorwärtsdurchlauf je Zug, keine Suche. **Verifiziert** (Größe, ein Durchlauf). Latenz auf CPU: im Paper nicht angegeben. **Verifiziert** (Fehlen).

Schätzung (**Abgeleitet**, vor der Spec messen):

- Ein Maia-3-Prozess kann alle 10 Gegner bedienen: vor jedem `go` werden `position`, `setoption name SelfElo` (Elo des Gegners) und `setoption name OppoElo` (Elo des Spielers, Start 800) gesetzt. Rechenzeit vermutlich im Bereich von Millisekunden bis wenigen zehn Millisekunden je Zug beim 5M-Modell. RAM vermutlich unter 1 GB, dominiert von PyTorch.
- Ein voll starker Stockfish für Tipp und Remis-Bewertung mit `go movetime 500` bis `1000` und 1 Thread reicht für Lernhilfen. Bei mehreren gleichzeitigen Anfragen ein kleiner Pool von 1 bis 2 Prozessen.
- Weil Maia fast sofort antwortet, muss das Backend eine künstliche Denkzeit einbauen. Sie läuft laut Map auf der Uhr des Gegners und muss bei „90 Sekunden pro Zug" und 5+3 deutlich unter dem Zeitbudget liegen.

Folgeschritt: Messspike auf `srv1952305`: 10 parallele Partien, Maia-3-5M und 23M, Stockfish-Pool; RAM, CPU-Last und Zeit je Zug notieren.

## 5. Einbindung in .NET

(**Abgeleitet**, außer wo markiert)

- Beide Engines sprechen UCI über stdin/stdout. **Verifiziert** (Stockfish-Wiki, Maia-3-README).
- Das .NET-Backend startet sie als Kindprozesse (`System.Diagnostics.Process`) oder spricht sie in einem eigenen Container an. Für Maia-3 empfiehlt sich ein eigener Container (Python + CPU-PyTorch), weil das .NET-Image sonst eine Python-Laufzeit mitschleppt.
- Variante „eigener Container": ein kleiner HTTP-Dienst im Maia-Container (Python), der für FEN + Zugfolge + Elo + Persönlichkeit den Zug zurückgibt. Das löst zugleich die Hürde mit den fehlenden Wahrscheinlichkeiten aus Abschnitt 3.
- Stockfish kann als Binary im .NET-Image liegen (Kindprozess) oder als eigener Container. Bei Dokploy passt beides in den Compose-Stack.
- Die Engine kennt keine Uhr. Uhr und Denkzeit führt der Server, wie in der Map festgelegt.

## 6. Lizenzen (Abgeleitet, keine Rechtsberatung)

- Stockfish und Fairy-Stockfish: GPL-3.0. **Verifiziert**: Header in `engine.cpp`, [GitHub-Lizenzangabe Fairy-Stockfish](https://github.com/fairy-stockfish/Fairy-Stockfish).
  - Als separater UCI-Prozess wird nichts gelinkt; der .NET-Code muss dadurch nicht GPL werden.
  - Die Pflicht, Quellcode anzubieten, entsteht bei Weitergabe des Binarys an Dritte. Dokploy baut und betreibt das Image auf dem eigenen VPS; das ist vermutlich keine Weitergabe. Ein öffentlich veröffentlichtes Image mit Binary wäre eine.
- Maia-3-Code: AGPL-3.0. **Verifiziert**: [GitHub-Lizenzangabe](https://github.com/CSSLab/maia3).
  - Ein Python-Wrapper oder Fork, der `maia3` importiert oder verändert und über das Netz genutzt wird, muss selbst unter AGPL-3.0 stehen. Öffentlich sichtbar reicht nicht; der Code braucht diese Lizenz.
  - Das Repo `enjoydialogs/schach` hat derzeit keine LICENSE-Datei. Praktisch: den Maia-Dienst in ein eigenes Verzeichnis oder einen eigenen Container mit eigener AGPL-LICENSE legen und die Lizenz des übrigen Repos getrennt festlegen.
- **Maia-3-Gewichte: keine Lizenz angegeben.** Weder im Hugging-Face-Modell (kein License-Tag) noch im README; die Modellkarte verweist nur auf das Repo. **Verifiziert** (Fehlen).
  - Offene Frage, die den Produktivbetrieb blockiert, nicht dieses Ticket: bei CSSLab nachfragen oder ein Issue im Repo stellen.
  - Maia-2 ist nur bedingt eine Ausweichlösung: Code MIT, aber Gewichte-Lizenz und unteres Ende der Spanne ungeprüft.

## 7. Tipp auf Wunsch und Remis-Bewertung

- Dafür einen eigenen, **voll starken** Stockfish nutzen, nie den Prozess des Gegners. **Abgeleitet.**
- Tipp: `go movetime N`, `bestmove` zeigen. Optional mit `MultiPV` mehrere gute Züge. **Verifiziert** (Optionen), Einsatz: **Abgeleitet.**
- Remis-Annahme („nimmt an, wenn er nicht klar vorn steht"): `UCI_ShowWDL` liefert eine Gewinn/Remis/Verlust-Schätzung. Sie ist geeicht auf Stockfish-Selbstpartien bei 60+0.6, also auf Engine-Spiel, nicht auf Menschen. **Verifiziert**: [Stockfish-Wiki, `UCI_ShowWDL`](https://github.com/official-stockfish/Stockfish/wiki/UCI-Protocol-and-Stockfish-Commands).
  - Einfacher und für Anfänger fairer ist eine Schwelle in Centipawn (z. B. Remis annehmen, wenn die Bewertung aus Sicht des Gegners unter +1,5 liegt). **Abgeleitet.**
- Maia-3 liefert auch WDL, aber laut README „human-game outcome predictions", „not Stockfish-style search evaluations". Brauchbar als Ergänzung (wie wahrscheinlich gewinnt ein Mensch dieser Elo), nicht als objektive Bewertung. **Verifiziert**: [Maia-3-README](https://github.com/CSSLab/maia3).
- Dieselbe Stockfish-Instanz kann später die Engine-Analyse beim Nachspielen liefern (Map: „Not yet specified"). **Abgeleitet.**

## Offene Punkte für die Spec

1. Messspike auf `srv1952305` (Abschnitt 4).
2. Eichung: Spielt Maia-3 bei eingestellter Elo 600 wirklich wie ein Anfänger? Testpartien gegen Fairy-Stockfish-Stufen oder Lichess-Daten, dann Elo-Offset und `Temperature` je Spielstärke festlegen.
3. Lizenz der Maia-3-Gewichte bei CSSLab klären; Maia-2 als Ausweg vorher prüfen (Gewichte-Lizenz, Spanne).
4. Lizenz des Repos festlegen und den Maia-Dienst als AGPL-Teil abgrenzen (Abschnitt 6).
5. Merkmale und Gewichte der Persönlichkeiten festlegen (Abschnitt 3), Prototyp mit einer aggressiven und einer defensiven Persönlichkeit.
6. Skala der Gegner-Elo festlegen (intern, Lichess-nah oder FIDE-nah).
