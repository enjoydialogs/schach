# 0003: Übungen ohne Wertung, nur im Browser

Status: angenommen (Betreiber, 2026-11-13). Bezug: SCHACH-SPEC 1, 10.16.

## Kontext

- SCHACH-SPEC 1 schloss einen Lektionen-Kurs (Zugregeln, Mattbilder, Rätsel) aus. Lernen lief nur über Partien und Lernhilfen.
- Der Betreiber wünscht nach dem MVP eine Liste geführter Übungen zu typischen Anfänger-Situationen (en passant, Matt erzwingen, Eröffnungen). Ein Gegner erklärt, es gibt keine Uhr und keine Wertung.
- `Contracts/*` und `Abstractions/*` sind eingefroren (ADR 0001). Eine Übung als Server-Partie bräuchte einen eigenen Vertrags-PR, würde mit der Regel „höchstens eine offene Partie“ kollidieren und Engine- und Mistral-Kosten auslösen.

## Entscheidung

- Übungen sind im Umfang. Ein Kurs mit Reihenfolge, Fortschritt oder Wertung bleibt außerhalb.
- Eine Übung ist keine Partie. Sie läuft ganz im Browser: fester Katalog in `src/Schach.Web/src/uebungen/katalog.ts`, Regeln per chess.js, Züge des Gegners vorgegeben.
- Einziger Serveraufruf ist `GET /gegner` für den zufälligen Gegner. Seine Erklärungen sind feste Texte, kein LLM.
- Gast und Spieler sehen Übungen gleich. Nichts wird gespeichert.

## Folgen

- Kein Vertrags-PR, keine Migration, keine Kosten je Übung.
- Neue Übungen sind reine Frontend-PRs. Ein Test spielt jede Übung mit chess.js durch, damit falsche Züge im Katalog auffallen.
- Die Erklärungen klingen bei jedem Gegner gleich; nur Name und Gesicht wechseln. Persönlichkeitstreue Texte bräuchten das LLM und sind ein eigenes Ticket.
- Freies Weiterspielen aus einer Übungsstellung (z. B. Dame gegen König mit wechselnden Antworten) geht so nicht. Dafür bräuchte es die Engine.
