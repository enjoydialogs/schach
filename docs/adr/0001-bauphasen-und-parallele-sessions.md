# 0001: Bauphasen und parallele Bau-Sessions

Status: angenommen (Betreiber, 2026-11-07). Nachträglich aufgeschrieben in Phase 4 (#41). Bezug: #19, SCHACH-SPEC 15.

## Kontext

- Schach wird fast vollständig von Agenten-Sessions gebaut. Eine Session hat ein begrenztes Kontextfenster und sieht nur ihren eigenen Branch.
- Der Betreiber ist eine Person. Er prüft und merged jeden PR selbst und bedient den VPS, Dokploy, Mistral, Lettermint und das Pixel 7.
- Ohne Regeln kollidieren parallele Sessions: zwei Sessions ändern dieselbe Datei, schreiben konkurrierende Migrationen oder ändern einen Vertrag, auf den eine andere Session baut.
- Kronrat hat mit `docs/agents/build-protocol.md` gezeigt, dass ein festes Bau-Protokoll mit eingefrorenem Gerüst und Append-Registern parallele Sessions trägt.
- Einige Fragen lassen sich erst nach einem Deploy beantworten (VPS-Messung Teil 2, Eichung, Spruch-Probe, Beta). Sie dürfen den Bau nicht blockieren.

## Entscheidung

- **Fünf Phasen (0 bis 4), vier Deploys.** Phase 0 friert das Gerüst ein und stellt ein laufendes Skelett auf die Domain (Deploy 1). Phase 2 endet mit Deploy 2 (Rechtstexte live), Phase 3 mit Deploy 3 (Beta), Phase 4 mit Deploy 4 (MVP).
- **Ein Bau-Issue = eine Session = ein Branch = ein PR**, Start von `main`. Ist ein Issue zu groß: Issue kommentieren und stoppen, nicht still kürzen.
- **Höchstens vier Sessions parallel**: ein Trunk-Slot für den Partie-Kern, drei Seiten-Slots. Ein offener PR je Slot.
- **Eingefrorene Dateien** ab Phase 0 (Lösung, zentrale Paketversionen, `Abstractions/*`, `Contracts/*`, `engine/API.md`, Dockerfiles, Compose, Workflows, `ci/*`). Änderung nur per eigenem kleinem Gerüst-PR.
- **Append-Register** für Dateien, die jede Session braucht (`Program.cs` mit einem DI-Block je Session, `router.tsx`, `i18n/de.ts`, `api/types.ts`, `THIRD-PARTY-NOTICES.md`, FEN-Suite, `ControllerAuthorizationTests`). Nur anhängen, nie umsortieren.
- **Migrationen**: nur 0b schreibt `0001_Schema`. Jede weitere Änderung ist eine eigene Vorwärts-Migration auf aktuellem `main`.
- **`main` per Merge aufnehmen**, nie Rebase oder Force-Push. Merge-fähig nur bei `behind_by = 0` und grünem CI.
- **Messungen nach Deploy 2** laufen neben Phase 3, mit Platzhaltern bis zum Ergebnis (`Engine:Eichung` = Identität).
- **Abnahme schriftlich**: Status nur im phasenschließenden Docs-PR, MVP-Freigabe in `docs/abnahme/mvp.md` mit allen elf Punkten aus SCHACH-SPEC 15.4.

## Folgen

- Sessions blockieren sich selten. Konflikte entstehen fast nur in den Append-Registern und lösen sich beim Merge durch Anhängen.
- Verträge (Zustand-DTO, Fehler-DTO, `engine/API.md`) ändern sich nicht nebenbei. Eine Vertragsänderung kostet einen eigenen PR und ist damit sichtbar.
- Der Betreiber ist der Engpass: jeder PR wartet auf seinen Merge, jede Messung und jeder Deploy auf seine Zeit. Das ist gewollt, weil er die Verantwortung für das öffentliche Repo und den VPS trägt.
- Phase 4 hängt an Dingen außerhalb des Repos (Beta-Tester, Eichlauf auf dem Entwicklungsrechner, Deploy). Sie lässt sich darum nur zum Teil von einer Session erledigen; der Rest steht als offener Punkt in `docs/abnahme/mvp.md`.
- Append-Register wachsen ungeordnet, weil niemand umsortieren darf.
