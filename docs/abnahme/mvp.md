# MVP-Freigabe

Beleg für SCHACH-SPEC 15.4. Alle elf Punkte sind Pflicht. Deploy 4 = MVP erst, wenn jede Zeile „belegt“ ist.

Stand: 2026-11-07, Phase 4 Teil 1 (#41). Phase 3 ist noch nicht abgeschlossen (#38, #39 offen), Deploy 3 (Beta) hat noch nicht stattgefunden. Viele Punkte sind darum noch offen.

## Übersicht

| Nr. | Punkt | Status | Wer |
|---|---|---|---|
| 1 | `main` grün, deploytes Image = Kandidat | offen | Betreiber |
| 2 | Alle Teststufen grün, inkl. Playwright lokal | teilweise | Session + Betreiber |
| 3 | Rauchtest grün | offen (für den Kandidaten) | Betreiber |
| 4 | Pixel-7-Abnahme als installierte PWA | offen | Betreiber |
| 5 | VPS-Messung, Limits, `SF_ARCH` | belegt (Vorgaben bestätigt), Erststart offen | Betreiber (Erststart) |
| 6 | Eichung | offen | Betreiber (Entwicklungsrechner) |
| 7 | Spruch-Probe, Ersatzquote < 20 % in der Beta | offen | Betreiber |
| 8 | Backup, Restore-Probe, Budgets, Warnmail | offen | Betreiber (#39) |
| 9 | Rechtstexte live, Konto löschen, Export, null Fremd-Hosts | teilweise | Betreiber |
| 10 | Lizenzen, Notices, Quellcode-Link, gitleaks, keine Servernamen | teilweise | Betreiber (Renovate) |
| 11 | Beta: ≥ 3 Tester, ≥ 20 Partien, kein blockierender Fehler | offen | Betreiber |

## Belege je Punkt

### 1. `main` grün, deploytes Image ist der Kandidat

- Stand 2026-11-07: `release.yml` auf `main` grün bis `24688e2`. Letzter Prod-Deploy (`workflow_dispatch`) auf `4b8f65f`, also nicht der aktuelle Stand.
- Offen: Kandidat festlegen, nach Abschluss von #38/#39 und diesem Ticket per `workflow_dispatch` deployen, SHA hier eintragen.

### 2. Alle Teststufen grün

- CI (`ci.yml`): .NET Unit, Api.Tests, Integration, Vitest, pytest, FEN-Suite. Lauf zum PR dieses Tickets eintragen.
- E2E (`e2e.yml`, `workflow_dispatch`): grün auf `24688e2` (2026-11-07).
- Offen: Playwright lokal gegen den Kandidaten (SCHACH-SPEC 15.2), Ergebnis hier eintragen.

### 3. Rauchtest

- Läuft in `release.yml` nach jedem Deploy. Letzter grüner Lauf mit Deploy: `4b8f65f`.
- Offen: Rauchtest des Kandidaten.

### 4. Pixel-7-Abnahme als installierte PWA

- Offen. Checkliste: SCHACH-SPEC 15.3 Phase 3 „Fertig, wenn“ und 10.15.
- Vorab geprüft (Session, 2026-11-07, lokaler Produktionsbau mit `vite preview`, Chromium): Manifest mit Name, `display: standalone`, `start_url: /`, Icons 192, 512, 512 maskable; Service Worker aktiv. Die Installierbarkeit ist damit erfüllt; die Prüfung auf dem Gerät bleibt Pflicht.

### 5. VPS-Messung

- Belegt in `docs/engine-messung.md` („Ergebnis VPS-Messung Teil 2“, 2026-11-07): Gegnerzug p95 67,7 ms (≤ 1 000), Tipp p95 987,7 ms (≤ 1 500), RSS 469 MiB von 2 GiB.
- Gesetzt heißt hier: die Vorgaben wurden durch die Messung bestätigt, kein Wert wurde geändert.
- Limits aus SCHACH-SPEC 11.2 sowie `MAIA_QUEUE_MAX` und `SF_POOL_SIZE` aus 5.2 bleiben auf den Vorgaben. Begründung: 0 % 503 auf `/move` und `/analyse` bei 10 parallelen Partien, Gegnerzug p95 bei 7 % des Grenzwerts. Nach der Beta mit deren Befunden erneut prüfen (Phase 4: „Limits nachziehen“).
- `SF_ARCH` bleibt `x86-64-avx2`, wie in `docs/engine-messung.md` festgehalten. Das Produktions-Image baut `release.yml` aus `engine/Dockerfile` ohne Build-Arg, also mit dieser Vorgabe; `docker-compose.dokploy.yml` zieht nur das fertige Image. Stockfish 19 ist ein Universal-Binary und nutzt AVX-512 zur Laufzeit selbst; `SF_ARCH` wählt nur das Fairy-Stockfish-Build, das Ausweg-Engine ist. Ein Wechsel auf `x86-64-avx512` beträfe also nur Fairy-Stockfish; ob er sich lohnt, ist nicht gemessen.
- Offen (SCHACH-SPEC 16.1, gehört zu Messung Teil 2): Erststart mit leerem Volume `engine-models` inklusive Hugging-Face-Download, mit `messung.py --erststart`.

### 6. Eichung

- Offen. `docs/engine-messung.md` „Ergebnis Eichung“: noch nicht geeicht. `Engine:Eichung` bleibt Identität (SCHACH-SPEC 5.4).
- Betreiber: `eichung.py staerke` und `eichung.py stil` auf dem Entwicklungsrechner laufen lassen (Befehle in `docs/engine-messung.md`), Tabellen dort füllen, Block `Engine:Eichung` in die Konfiguration übernehmen.
- Bedingung: alle acht Elo-Stufen ±100 (oder 600/800 auf Fairy-Stockfish), Stil-A/B 45 bis 55 %, Sichtbarkeit erfüllt.

### 7. Spruch-Probe

- Offen. Ablauf SCHACH-SPEC 6.8. Ersatzquote erst in der Beta messbar (Meter `schach.llm.aufrufe{quelle,grund}`).

### 8. Backup und Budgets

- Offen, hängt an #39: Backup an mindestens 3 Nächten (`backup-check.yml`), Restore-Probe nach `deploy/restore-drill.md`, Mistral-Limit 10 €, `Mail:MaxJeTag` zum Lettermint-Tarif, Budget-Warnmail einmal empfangen.

### 9. Rechtstexte, Konto löschen, Export, Fremd-Hosts

- Rechtstexte gebaut (#35 geschlossen). E2E-Strecken „Konto löschen“ und „Rechtstexte plus null Fremd-Hosts plus CSP“ grün auf `24688e2`.
- Offen: Prüfung gegen den Kandidaten in Produktion (Rechtstexte erreichbar, Konto löschen hinterlässt 0 Zeilen, Export lädt).

### 10. Lizenzen, Notices, Quellcode-Link, gitleaks, Servernamen

- `THIRD-PARTY-NOTICES.md`: Abschnitt „Ergänzung Phase 4“ (#41) ergänzt alles, was ausgeliefert wird. Abgleich gegen `dotnet publish`, `npm ls --omit=dev` und die Python-Abhängigkeiten des Engine-Images.
- Quellcode-Link mit Commit-SHA im Footer (`src/Schach.Web/src/components/quellcode.ts`).
- gitleaks läuft in `ci.yml`.
- Renovate: `renovate.json` ist konfiguriert, aber die Renovate-App ist für das Repo nicht installiert (kein Dependency-Dashboard-Issue, kein Onboarding-PR, Stand 2026-11-07). **Betreiber:** GitHub-App Renovate für `enjoydialogs/schach` freischalten und prüfen, dass das Dashboard-Issue erscheint.
- Servernamen: `git grep` auf IPv4-Adressen und Hostnamen am 2026-11-07 (Stand dieses PRs). Treffer nur `schach.enjoy-dialogs.de` (öffentliche Domain, Spec 13.5) und `203.0.113.7` (Dokumentations-Adresse TEST-NET-3 in `SchachMeterTests`). Vor Deploy 4 auf dem Kandidaten wiederholen.

### 11. Beta

- Offen. Deploy 3 hat noch nicht stattgefunden.

## Härtung aus Phase 4 Teil 1 (#41)

### Kontraste

- `src/Schach.Web/src/components/palette.test.ts` prüft je Palette (Lila, Pink, Grün, Blau) 20 Text-Hintergrund-Paare auf ≥ 4,5:1. Neu: Akzent auf Hover-Tint, Gefahr auf Grund und Glas, Weiß auf Gefahr, Warntext, gesperrte Knöpfe, Weiß auf dunklem Glas.
- Befund: Lila-Akzent auf dem Hover-Tint der Outline- und Textknöpfe hatte 4,05:1. Akzent von `#1e7a62` auf `#1c715b` abgedunkelt: jetzt 4,53:1 (Grund 5,32:1, Weiß darauf 5,89:1). SCHACH-SPEC 10.3 ist nachgezogen. `--avatar-vereinsspieler` bleibt bewusst `#1e7a62` (Spec 4.4, Weiß darauf 5,23:1).

### Zielgrößen

- `src/Schach.Web/src/styles/zielgroessen.test.ts` prüft die Höhe von neun Tippziel-Klassen auf ≥ 48 px.
- Befund: `.segment` (Auswahl in „Neue Partie“) war 44 px hoch, jetzt 48 px. Schalter und Farbwahl hatten schon eine Trefferfläche von 48 px über die ganze Zeile.
- Breite: `.knopf` hat `min-width: 48px`, `.symbolknopf` ist 48 × 48, `.zugchip` hat `min-width: 48px`. Segmente, Zeilen, Leistenknöpfe, Schalter und die Farbwahl (Raster mit vier Spalten) teilen sich die volle Breite und sind damit breiter als 48 px. Der Test prüft nur die Höhe.

### Lighthouse

Lighthouse 13.6.0, mobil, lokaler Produktionsbau (`vite preview`, ohne API), 2026-11-07.

| Seite | Performance | Barrierefreiheit | Best Practices |
|---|---|---|---|
| `/` | 96 | 100 | 96 |
| `/neu` | 96 | 100 | 96 |
| `/impressum` | 100 | 100 | 96 |

- Best Practices: die Abzüge kommen von 502 auf `/api/v1/*`, weil lokal keine API lief. Gefunden und behoben: `/favicon.ico` gab 404, weil `index.html` kein Icon verlinkte (Test `src/pwa/indexHtml.test.ts`).
- Lighthouse hat seit Version 12 keine PWA-Kategorie mehr. Die Installierbarkeit ist unter Punkt 4 belegt.
- Offen: Lighthouse gegen die Domain mit dem Kandidaten wiederholen.
