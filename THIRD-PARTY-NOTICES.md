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

## Browser, Ergänzung S3d (PWA)

| Komponente | Version | Lizenz | Quelle | Verwendet in |
|---|---|---|---|---|
| Workbox (workbox-window, workbox-precaching, workbox-routing, workbox-core) | 7.4.1 | MIT | https://github.com/GoogleChrome/workbox | Registrierung und Service Worker (`sw.js`, `workbox-*.js`) |
| vite-plugin-pwa | 2.0.0 | MIT | https://github.com/vite-pwa/vite-plugin-pwa | nur Bau: erzeugt `sw.js` und `manifest.webmanifest` |

Die App-Icons (`public/icons/`) zeigen die Silhouette des Cburnett-Springers (BSD-3-Clause, Hinweis oben), erzeugt mit `src/Schach.Web/scripts/icons.mjs`.
