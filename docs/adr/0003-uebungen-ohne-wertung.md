# 0003: Übungen ohne Wertung, nur im Browser

Status: angenommen (Betreiber, 2026-11-13). Bezug: SCHACH-SPEC 1, 10.16.

## Kontext

- SCHACH-SPEC 1 schloss einen Lektionen-Kurs (Zugregeln, Mattbilder, Rätsel) aus. Lernen lief nur über Partien und Lernhilfen.
- Der Betreiber wünscht nach dem MVP eine Liste geführter Übungen zu typischen Anfänger-Situationen (en passant, Matt erzwingen, Eröffnungen). Ein Gegner erklärt, es gibt keine Uhr und keine Wertung.
- `Contracts/*` und `Abstractions/*` sind eingefroren (ADR 0001). Eine Übung als Server-Partie bräuchte einen eigenen Vertrags-PR, würde mit der Regel „höchstens eine offene Partie“ kollidieren und Engine- und Mistral-Kosten auslösen.

## Entscheidung

- Übungen sind im Umfang. Ein Kurs mit Reihenfolge, Fortschritt oder Wertung bleibt außerhalb.
- Eine Übung ist keine Partie. Sie läuft ganz im Browser: fester Katalog in `src/Schach.Web/src/uebungen/katalog.ts`, Regeln per chess.js, Züge des Gegners vorgegeben.
- Einziger Serveraufruf ist `GET /gegner` für den zufälligen Gegner. Seine Erklärungen sind feste Texte, kein LLM. Je Persönlichkeit gibt es eine eigene Fassung (`uebungen/stimmen.ts`).
- Gast und Spieler sehen Übungen gleich. Nichts wird gespeichert.

## Folgen

- Kein Vertrags-PR, keine Migration, keine Kosten je Übung.
- Neue Übungen sind reine Frontend-PRs. Ein Test spielt jede Übung mit chess.js durch, damit falsche Züge im Katalog auffallen.
- Die Erklärungen klingen nach der Persönlichkeit des Gegners, ohne LLM und ohne neuen Endpunkt: feste Fassungen je Persönlichkeit in `uebungen/stimmen.ts` (Betreiber, 2026-11-13). Verworfen: Umschreiben per Mistral über die Spruch-Pipeline, das einen Vertrags-PR (ADR 0001), eine Budget-Grenze und Wartezeit gebraucht hätte.
- Preis: rund 50 Texte je Persönlichkeit, also etwa 300 Texte mehr. Jede neue Übung braucht sechs Fassungen; der Test bricht sonst. Die beiden Gegner einer Persönlichkeit (Finn und Maren, Oscar und Konrad) sprechen in Übungen gleich.
- Freies Weiterspielen aus einer Übungsstellung (z. B. Dame gegen König mit wechselnden Antworten) geht so nicht. Dafür bräuchte es die Engine.
