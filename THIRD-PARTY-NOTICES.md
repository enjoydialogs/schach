# Drittanbieter-Hinweise

Schach steht unter der MIT-Lizenz (`LICENSE`). Der Engine-Dienst in `engine/` steht unter AGPL-3.0-or-later (`engine/LICENSE`).

Diese Datei ist ein Append-Register (SCHACH-SPEC 15.1 Regel 5): Einträge nur anhängen. Die Session, die eine Komponente einbaut, ergänzt Version, Quelle und den Lizenztext oder einen Verweis darauf. Vollständig wird die Liste in Phase 4.

## Server (.NET)

| Komponente | Lizenz | Quelle | Verwendet in |
|---|---|---|---|
| Gera.Chess | MIT | https://github.com/Geras1mleo/Chess | `Schach.Domain` (Legalität, Matt, Patt) |

## Browser

| Komponente | Lizenz | Quelle | Verwendet in |
|---|---|---|---|
| chess.js | BSD-2-Clause | https://github.com/jhlywa/chess.js | PGN, Nachspielen, Warnung (ab S3a) |
| react-chessboard | MIT | https://github.com/Clariity/react-chessboard | Komponente `Brett` (ab S3a) |
| Cburnett-Figuren | BSD-3-Clause | Wikimedia Commons | Figuren-SVGs (ab S3a) |
| Inter | SIL Open Font License 1.1 | https://github.com/rsms/inter | Schrift, selbst gehostet |

## Engine-Dienst (`engine/`, AGPL-3.0-or-later)

| Komponente | Lizenz | Quelle | Verwendet in |
|---|---|---|---|
| Maia-3 (Code) | AGPL-3.0 | https://github.com/CSSLab/maia3 | Kandidatenzüge (ab S4a) |
| Maia-3 (Gewichte) | angefragt bei CSSLab (Issue #8) | Hugging Face | nur serverseitig, nie im Repo oder Image |
| Stockfish | GPL-3.0 | https://github.com/official-stockfish/Stockfish | Tipp und Remis-Bewertung (ab S4a) |
| Fairy-Stockfish | GPL-3.0 | https://github.com/fairy-stockfish/Fairy-Stockfish | Ausweg für Anfänger und bei Maia-Ausfall (ab S4a) |
| python-chess | GPL-3.0-or-later | https://github.com/niklasf/python-chess | Stellung, Legalität und UCI im Engine-Dienst (ab 0c) |
| FastAPI | MIT | https://github.com/fastapi/fastapi | HTTP-Schicht des Engine-Dienstes (ab 0c) |
| Uvicorn | BSD-3-Clause | https://github.com/encode/uvicorn | ASGI-Server des Engine-Dienstes (ab 0c) |
| PyTorch 2.15.0 (CPU) | BSD-3-Clause | https://github.com/pytorch/pytorch | Maia-3-Inferenz im Engine-Image (ab S4a) |
| Stockfish 19 (Binary) | GPL-3.0 | https://github.com/official-stockfish/Stockfish, Tag `sf_19` | Release-Binary im Engine-Image, sha256 gepinnt (ab S4a) |
| Fairy-Stockfish 14 (Binary) | GPL-3.0 | https://github.com/fairy-stockfish/Fairy-Stockfish, Tag `fairy_sf_14` | Release-Binary im Engine-Image, sha256 gepinnt (ab S4a) |

## Browser, Ergänzung S3a (Brett-Prototyp)

| Komponente | Version | Lizenz | Quelle | Verwendet in |
|---|---|---|---|---|
| react-chessboard | 5.12.1 | MIT | https://github.com/Clariity/react-chessboard | Komponente `Brett`; der mitgelieferte Figurensatz wird nicht verwendet |
| @dnd-kit/core, @dnd-kit/modifiers | 6.3.1, 9.0.0 (über react-chessboard) | MIT | https://github.com/clauderic/dnd-kit | Ziehen auf dem Brett |
| chess.js | 1.4.0 | BSD-2-Clause | https://github.com/jhlywa/chess.js | Warnung, Schach-Markierung, optimistische Anzeige |
| lucide-react | 1.70.0 | ISC | https://github.com/lucide-icons/lucide | Icons der Knopfleiste |
| Mock Service Worker (msw) | 3.0.4 | MIT | https://github.com/mswjs/msw | nur Entwicklung und Tests (Fake-Server), nicht im Produktionsbau |

### Cburnett-Figuren (BSD-3-Clause)

Die 12 Figuren in `src/Schach.Web/src/brett/figuren.tsx` stammen von Wikimedia Commons, Urheber Colin M.L. Burnett (User:Cburnett): `Chess_klt45.svg`, `Chess_kdt45.svg`, `Chess_qlt45.svg`, `Chess_qdt45.svg`, `Chess_rlt45.svg`, `Chess_rdt45.svg`, `Chess_blt45.svg`, `Chess_bdt45.svg`, `Chess_nlt45.svg`, `Chess_ndt45.svg`, `Chess_plt45.svg`, `Chess_pdt45.svg` (https://commons.wikimedia.org/wiki/Category:SVG_chess_pieces), abgerufen am 2026-11-07. Die Dateien stehen dort unter einer Mehrfachlizenz (GFDL, BSD, GPL); Schach verwendet sie unter der BSD-Option (Commons-Vorlage `BSD`, 3-Clause). Die Pfade sind unverändert, nur nach JSX übertragen.

```
Copyright (c) 2006, Colin M.L. Burnett
All rights reserved.

Redistribution and use in source and binary forms, with or without
modification, are permitted provided that the following conditions are met:

1. Redistributions of source code must retain the above copyright notice, this
   list of conditions and the following disclaimer.
2. Redistributions in binary form must reproduce the above copyright notice,
   this list of conditions and the following disclaimer in the documentation
   and/or other materials provided with the distribution.
3. Neither the name of the copyright holder nor the names of its contributors
   may be used to endorse or promote products derived from this software
   without specific prior written permission.

THIS SOFTWARE IS PROVIDED BY THE COPYRIGHT HOLDERS AND CONTRIBUTORS "AS IS" AND
ANY EXPRESS OR IMPLIED WARRANTIES, INCLUDING, BUT NOT LIMITED TO, THE IMPLIED
WARRANTIES OF MERCHANTABILITY AND FITNESS FOR A PARTICULAR PURPOSE ARE
DISCLAIMED. IN NO EVENT SHALL THE COPYRIGHT HOLDER OR CONTRIBUTORS BE LIABLE
FOR ANY DIRECT, INDIRECT, INCIDENTAL, SPECIAL, EXEMPLARY, OR CONSEQUENTIAL
DAMAGES (INCLUDING, BUT NOT LIMITED TO, PROCUREMENT OF SUBSTITUTE GOODS OR
SERVICES; LOSS OF USE, DATA, OR PROFITS; OR BUSINESS INTERRUPTION) HOWEVER
CAUSED AND ON ANY THEORY OF LIABILITY, WHETHER IN CONTRACT, STRICT LIABILITY,
OR TORT (INCLUDING NEGLIGENCE OR OTHERWISE) ARISING IN ANY WAY OUT OF THE USE
OF THIS SOFTWARE, EVEN IF ADVISED OF THE POSSIBILITY OF SUCH DAMAGE.
```

## Engine-Dienst, Ergänzung Weitergabe (ADR 0002)

| Komponente | Lizenz | Quelle | Verwendet in |
|---|---|---|---|
| Maia-3 (Code), Commit `1e13597c42d4858b7cfd7cfdae01e297263364b2` | AGPL-3.0 | https://github.com/CSSLab/maia3/tree/1e13597c42d4858b7cfd7cfdae01e297263364b2 | geänderter Auszug in `engine/src/schach_engine/maia3/` (ab S4a) |

Weitergabe im öffentlichen Engine-Image (ADR 0002): Corresponding Source der Binaries sind die Upstream-Tags https://github.com/official-stockfish/Stockfish/tree/sf_19 und https://github.com/fairy-stockfish/Fairy-Stockfish/tree/fairy_sf_14. GPL-Text, sha256 und Quellen liegen im Image unter `/usr/share/doc/schach-engine/` (`COPYING`, `NOTICES.md`).

## Browser, Ergänzung S3d (PWA)

| Komponente | Version | Lizenz | Quelle | Verwendet in |
|---|---|---|---|---|
| Workbox (workbox-window, workbox-precaching, workbox-routing, workbox-core) | 7.4.1 | MIT | https://github.com/GoogleChrome/workbox | Registrierung und Service Worker (`sw.js`, `workbox-*.js`) |
| vite-plugin-pwa | 2.0.0 | MIT | https://github.com/vite-pwa/vite-plugin-pwa | nur Bau: erzeugt `sw.js` und `manifest.webmanifest` |

Die App-Icons (`public/icons/`) zeigen die Silhouette des Cburnett-Springers (BSD-3-Clause, Hinweis oben), erzeugt mit `src/Schach.Web/scripts/icons.mjs`.

## Ergänzung Phase 4 (Vollständigkeit, #41)

Abgleich am 2026-11-07 gegen das, was tatsächlich ausgeliefert wird. Lizenzen aus den Paket-Metadaten (`package.json`, `.nuspec`, Python-Wheel-Metadaten), nicht aus dem Gedächtnis. Reine Entwicklungs- und Testwerkzeuge (Vitest, Testing Library, oxlint, TypeScript, Vite, xunit, Testcontainers, pytest, httpx) werden nicht ausgeliefert und stehen nicht hier.

### Server (.NET), ausgeliefert in `schach-web`

Grundlage: `dotnet publish src/Schach.Api -c Release` (wie im `Dockerfile`). `Microsoft.EntityFrameworkCore.Design` ist `PrivateAssets=all` und landet nicht im Image.

| Komponente | Version | Lizenz | Quelle |
|---|---|---|---|
| Ardalis.SmartEnum (über Gera.Chess) | 2.0.1 | MIT (Projekt-Repo; die `.nuspec` nennt keine Lizenz) | https://github.com/ardalis/SmartEnum |
| Microsoft.EntityFrameworkCore, .Abstractions, .Relational | 10.0.14 | MIT | https://github.com/dotnet/efcore |
| Microsoft.AspNetCore.DataProtection.EntityFrameworkCore | 10.0.14 | MIT | https://github.com/dotnet/aspnetcore |
| Microsoft.Extensions.Diagnostics.HealthChecks.EntityFrameworkCore | 10.0.14 | MIT | https://github.com/dotnet/aspnetcore |
| Npgsql, Npgsql.EntityFrameworkCore.PostgreSQL, Npgsql.OpenTelemetry | 10.0.3 | PostgreSQL License | https://github.com/npgsql/npgsql, https://github.com/npgsql/efcore.pg |
| OpenTelemetry, .Api, .Api.ProviderBuilderExtensions, .Extensions.Hosting, .Exporter.OpenTelemetryProtocol | 1.20.1 | Apache-2.0 | https://github.com/open-telemetry/opentelemetry-dotnet |
| OpenTelemetry.Instrumentation.AspNetCore, .Http | 1.20.2 | Apache-2.0 | https://github.com/open-telemetry/opentelemetry-dotnet-contrib |
| .NET 10 Runtime und ASP.NET Core (Basis-Image `mcr.microsoft.com/dotnet/aspnet`) | 10.0 | MIT | https://github.com/dotnet/runtime, https://github.com/dotnet/aspnetcore |

### Browser, ausgeliefert im Vite-Bau

Grundlage: `npm ls --omit=dev --all` in `src/Schach.Web`. Komponenten, die oben schon stehen (chess.js, react-chessboard, @dnd-kit, lucide-react, Workbox), sind hier nicht wiederholt.

| Komponente | Version | Lizenz | Quelle |
|---|---|---|---|
| react, react-dom, scheduler | 19.3.1, 19.3.1, 0.28.0 | MIT | https://github.com/facebook/react |
| react-router | 7.18.5 | MIT | https://github.com/remix-run/react-router |
| cookie, set-cookie-parser (über react-router) | 1.1.1, 2.7.3 | MIT | https://github.com/jshttp/cookie, https://github.com/nfriedly/set-cookie-parser |
| @dnd-kit/accessibility, @dnd-kit/utilities (über react-chessboard) | 3.1.1, 3.2.2 | MIT | https://github.com/clauderic/dnd-kit |
| tslib (über @dnd-kit) | 2.9.1 | 0BSD | https://github.com/microsoft/tslib |
| @fontsource/inter (Schrift Inter, selbst gehostet) | 5.3.0 | OFL-1.1 | https://github.com/fontsource/font-files |

### Engine-Dienst, ausgeliefert in `schach-engine`

Grundlage: Abhängigkeiten von `pip install ".[model]"` wie im `engine/Dockerfile`, aufgelöst am 2026-11-07. Die Versionen sind nicht gepinnt; ein neuer Bau kann neuere Patch-Stände ziehen.

| Komponente | Version | Lizenz | Quelle |
|---|---|---|---|
| Starlette (über FastAPI) | 1.10.0 | BSD-3-Clause | https://github.com/encode/starlette |
| pydantic, pydantic-core | 2.14.3, 2.50.4 | MIT | https://github.com/pydantic/pydantic |
| anyio | 4.16.0 | MIT | https://github.com/agronholm/anyio |
| idna | 3.20 | BSD-3-Clause | https://github.com/kjd/idna |
| h11, click (über Uvicorn) | 0.16.0, 8.5.1 | MIT, BSD-3-Clause | https://github.com/python-hyper/h11, https://github.com/pallets/click |
| annotated-types, annotated-doc, typing-inspection | 0.8.0, 0.0.5, 0.4.5 | MIT | https://github.com/annotated-types/annotated-types, https://github.com/fastapi/annotated-doc, https://github.com/pydantic/typing-inspection |
| typing-extensions | 4.16.0 | PSF-2.0 | https://github.com/python/typing_extensions |
| opentelemetry-api (über FastAPI) | 1.45.2 | Apache-2.0 | https://github.com/open-telemetry/opentelemetry-python |
| filelock, setuptools (über PyTorch) | 4.2.4, 78.1.0 | MIT | https://github.com/tox-dev/filelock, https://github.com/pypa/setuptools |
| sympy, mpmath (über PyTorch) | 1.14.0, 1.3.0 | BSD-3-Clause | https://github.com/sympy/sympy, https://github.com/mpmath/mpmath |
| networkx, fsspec (über PyTorch) | 3.7, 2026.9.0 | BSD-3-Clause | https://github.com/networkx/networkx, https://github.com/fsspec/filesystem_spec |
| Jinja2, MarkupSafe (über PyTorch) | 3.1.6, 3.0.3 | BSD-3-Clause | https://github.com/pallets/jinja, https://github.com/pallets/markupsafe |
| Python 3.12 (Basis-Image `python:3.12-slim-bookworm`) | 3.12 | PSF-2.0 | https://www.python.org |

### Betrieb

| Komponente | Version | Lizenz | Quelle |
|---|---|---|---|
| PostgreSQL (Image `postgres:17`) | 17 | PostgreSQL License | https://www.postgresql.org |
