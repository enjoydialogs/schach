# ci/

Ein Skript für lokal und Cloud, nach dem Kronrat-Harness. Eingefroren ab Phase 0 (SCHACH-SPEC 15.1 Regel 4).

- Lokal: `pwsh ci/local-ci.ps1` (nur geänderte Stufen gegen `origin/main`), `-All` für alle, `-Only web,build` für eine Auswahl.
- Cloud: `.github/workflows/ci.yml` auf `pull_request`, samstags 09:17 UTC und per `workflow_dispatch`, nur `ubuntu-latest`. Job `local-ci` ruft `ci/local-ci.ps1 -All`. Job `images` baut beide Images, startet `docker compose --profile full` und fährt `deploy/ops/rauchtest.sh` sowie `deploy/ops/engine-aus-web.sh` (nur in der Cloud, nie lokal).
- Logs je Lauf unter `.local-ci/logs/<run>/<stage>.log` (gitignored).

Nicht übernommen aus Kronrat: der pre-push-Hook, `bootstrap.ps1` und `gh signoff`. Das Gate ist hier die PR-CI.

## Stufen

| Stufe | scheitert bei | läuft bei Änderung an |
|---|---|---|
| `secrets` | gitleaks findet etwas außerhalb `.gitleaks.toml`; in CI auch, wenn gitleaks fehlt | immer |
| `scripts` | shellcheck-Fehler, CRLF in `*.sh` | `**/*.sh` |
| `format` | `dotnet format --verify-no-changes` | `**/*.cs`, Build-Dateien |
| `build` | `dotnet build -c Release` (Warnungen sind Fehler) | `**/*.cs`, `*.csproj`, Build-Dateien |
| `test` | `dotnet test --solution Schach.slnx --no-build` (MTP) | `**/*.cs`, `tests/**` |
| `vulns` | `dotnet list package --vulnerable` ab High | `Directory.Packages.props`, `*.csproj` |
| `web` | `npm ci`, `npm run lint`, `npm test`, `npm run build` in `src/Schach.Web` | `src/Schach.Web/**` |
| `engine` | pytest mit `SCHACH_ENGINE_FAKE_MODEL=1` in `engine/.venv` (Python ab 3.12) | `engine/**`, `tests/fen-suite/**` |

## Container-Tests

Tests mit Testcontainers tragen `[ContainerFact]` und `[Trait("Category", "Container")]`. Sie laufen nur mit `SCHACH_CONTAINER_TESTS=1`. Die Cloud-CI setzt die Variable. Lokal auf den Windows-Rechnern bleibt sie aus; die Tests melden sich dann als übersprungen und Docker wird nicht angefasst.

Lokal ohne Container, ganz ausgeblendet: `dotnet test --solution Schach.slnx --filter-not-trait "Category=Container"`.

## Werkzeuge

.NET SDK aus `global.json`, Node 22 (`.nvmrc`), PowerShell 7.4, gitleaks, shellcheck (optional lokal). Docker nur für Container-Tests.
