# 0002: GHCR-Pakete `schach-web` und `schach-engine` sind öffentlich

Status: angenommen (Betreiber, 2026-11). Bezug: #24.

## Kontext

- `release.yml` pusht bei jedem Push auf `main` die Images `schach-web` und `schach-engine` nach GHCR (`ghcr.io/enjoydialogs`). Neue Pakete einer Organisation sind dort privat.
- Dokploy zieht die Images auf dem VPS. Bei privaten Paketen braucht es dafür einen Registry-Zugang, in der Praxis ein Token, das auch andere Projekte der Organisation nutzen. Läuft es ab oder wird es widerrufen, scheitert jeder Deploy.
- Private Pakete zählen gegen das Paket-Kontingent der Organisation, öffentliche nicht.
- Das Repo ist ohnehin öffentlich (MIT, `engine/` AGPL-3.0-or-later). Die Images enthalten nichts, was nicht im Repo steht oder upstream frei erhältlich ist. Die Maia-3-Gewichte liegen nie im Image (SCHACH-SPEC 13.6).
- Das Engine-Image enthält Objektcode von Stockfish 19 und Fairy-Stockfish 14 (GPL-3.0). Ein öffentliches Image gibt diesen Objektcode an jeden weiter, der es zieht. Das löst die Pflichten aus GPL-3.0 § 6 aus.

## Entscheidung

- Beide Pakete werden öffentlich. „Inherit access from source repository“ bleibt an, Schreibrechte hat weiter nur der Workflow.
- Dokploy zieht ohne Registry-Zugang.
- Das Engine-Image trägt die GPL-Papiere selbst: `/usr/share/doc/schach-engine/COPYING` (GPL-3.0) und `/usr/share/doc/schach-engine/NOTICES.md` (Tags, URLs, sha256 der Binaries, Maia-3-Commit, PyTorch-Version, Quell-URL und Commit dieses Dienstes). Der Build bricht ab, wenn eine Datei fehlt.
- Die Image-Labels nennen Lizenz (`org.opencontainers.image.licenses`), Beschreibung und Dokumentation. Für `schach-engine`: `AGPL-3.0-or-later AND GPL-3.0-or-later`, für `schach-web`: `MIT`.
- Corresponding Source der Binaries sind die Upstream-Tags `sf_19` und `fairy_sf_14` (GPL-3.0 § 6(d): Quelle an anderer Stelle, mit klarem Hinweis im Image).

## Folgen

- Die Umstellung ist praktisch nicht umkehrbar: Ein öffentliches Paket lässt sich bei GitHub nicht wieder privat stellen, und jedes gezogene Image bleibt bei Dritten.
- GPL-3.0 § 6(d): Die Quellen müssen erreichbar bleiben, solange ein Image mit diesen Binaries angeboten wird. Fallen die Upstream-Tags weg, legt der Betreiber eine Kopie der Quellen bereit oder löscht die betroffenen Image-Tags. Bei einem Versionswechsel der Binaries (`engine/docker/hole_binaries.py`) wandern Tag, URL und sha256 automatisch in `NOTICES.md`.
- Deploys hängen nicht mehr am geteilten GHCR-Token. Ein Token weniger, das ablaufen oder lecken kann.
- Kein Verbrauch des Paket-Kontingents der Organisation.
- Jeder kann die Images ziehen. Geheimnisse gehören deshalb nie ins Image (galt schon vorher: Werte kommen nur aus dem Dokploy-Environment).
