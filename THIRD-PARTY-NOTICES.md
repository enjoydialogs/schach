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
| Maia-3 (Code), Commit `1e13597c42d4858b7cfd7cfdae01e297263364b2` | AGPL-3.0 | https://github.com/CSSLab/maia3/tree/1e13597c42d4858b7cfd7cfdae01e297263364b2 | geänderter Auszug in `engine/src/schach_engine/maia3/` (ab S4a) |

Weitergabe im öffentlichen Engine-Image (ADR 0002): Corresponding Source der Binaries sind die Upstream-Tags https://github.com/official-stockfish/Stockfish/tree/sf_19 und https://github.com/fairy-stockfish/Fairy-Stockfish/tree/fairy_sf_14. GPL-Text, sha256 und Quellen liegen im Image unter `/usr/share/doc/schach-engine/` (`COPYING`, `NOTICES.md`).
