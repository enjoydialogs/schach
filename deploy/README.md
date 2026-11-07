# Deploy über Dokploy

Betrieb von `schach.enjoy-dialogs.de` (SCHACH-SPEC 13.5, 14). Panel-Adresse, Subnetze, IPs und Geheimnisse stehen nie im Repo, nur im Dokploy-Panel und im GitHub-Environment `production`.

## Was wo liegt

| Teil | Ort |
|---|---|
| Compose | `docker-compose.dokploy.yml` (postgres, engine, web) |
| Schlüssel des Environment-Tabs | [`.env.dokploy.example`](.env.dokploy.example), nur Namen |
| Images | GHCR `schach-web` und `schach-engine`, Tags `<sha>`, `main`, `prod` |
| Auslieferung | `.github/workflows/release.yml` per `workflow_dispatch` |
| Rauchtest | [`ops/rauchtest.sh`](ops/rauchtest.sh) `<basis-url>` |
| Engine aus web prüfen | [`ops/engine-aus-web.sh`](ops/engine-aus-web.sh) `<web-container>` |
| Backup-Prüfung | [`ops/backup-check.sh`](ops/backup-check.sh), täglich über `.github/workflows/backup-check.yml` |
| Backup einrichten, Restore-Probe | [`restore-drill.md`](restore-drill.md) (SCHACH-SPEC 14.1) |

## Monitoring (SCHACH-SPEC 14.2)

- Meter `Schach`: alle Instrumente in `SchachMeter` (`src/Schach.Infrastructure/Telemetry`). Export nur mit `OTEL_EXPORTER_OTLP_ENDPOINT`.
- Warn-Logzeilen `BudgetWarn`, `BudgetErschoepft`, `EngineUeberlast`, `MailKontingent`: je Art einmal je Fenster (Budget je Kalendermonat, sonst `Betrieb__WarnFensterMinuten`, Standard 60), dazu eine Mail an `OPERATOR_EMAIL` (Engine und Mail höchstens eine je Tag). Ohne Mail-Dienst (vor S2a) oder ohne Adresse nur das Log.
- Tageszeile: `Tageszeile <datum>: …` jeden Morgen um 06:00 Berliner Zeit, mit den Zählern seit der letzten Zeile. Nur Log, keine Mail.
- Suchen: `docker logs <projekt>-web-1 2>&1 | grep -E "BudgetWarn|BudgetErschoepft|EngineUeberlast|MailKontingent|Tageszeile"`.

## Ablauf eines Deploys

1. `main` baut bei jedem Push beide Images und pusht `<sha>` und `main`.
2. Actions → `release` → „Run workflow“. Ohne `image_tag` wird der aktuelle Stand gebaut, mit `image_tag` ein vorhandener SHA ausgeliefert (Rollback).
3. Der Lauf zeigt `prod` auf den SHA, bittet Dokploy um den Deploy, wartet und fährt den Rauchtest gegen die Domain.

## Erst-Einrichtung (Deploy 1)

Einmalig, vom Betreiber:

1. DNS: `schach.enjoy-dialogs.de` auf den VPS zeigen lassen.
2. GHCR: die Pakete `schach-web` und `schach-engine` nach dem ersten main-Lauf auf öffentlich stellen, oder Dokploy Registry-Zugang geben.
3. Dokploy: neuen Compose-Dienst aus diesem Repo anlegen, Compose-Pfad `docker-compose.dokploy.yml`.
4. Environment-Tab: Schlüssel aus `.env.dokploy.example` setzen. Pflicht: `REGISTRY`, `IMAGE_TAG=prod`, `POSTGRES_PASSWORD`, `TRAEFIK_NETWORK`. Das Subnetz auf der Box lesen:
   `docker network inspect dokploy-network --format '{{(index .IPAM.Config 0).Subnet}}'`
5. Domains-Tab: `schach.enjoy-dialogs.de` am Dienst `web`, Container-Port `8080`, HTTPS mit Let's Encrypt.
6. GitHub → Settings → Environments → `production`: Variablen `DOKPLOY_URL`, `DOKPLOY_COMPOSE_ID` (letzter Abschnitt der Dienst-URL im Panel), Secret `DOKPLOY_API_KEY` (Member mit Zugriff nur auf dieses Projekt).
7. `release` per `workflow_dispatch` starten. Der Rauchtest muss grün enden.
8. Auf der Box prüfen, dass `web` die Engine erreicht (aus dem Checkout, den Dokploy angelegt hat):
   `bash deploy/ops/engine-aus-web.sh "$(docker ps --format '{{.Names}}' | grep -- '-web-1$' | grep schach)"`
   Erwartet: `HTTP/1.1 200 OK` und `{"status":"ok"}`.

## Postgres-Passwort ändern

`POSTGRES_PASSWORD` wirkt nur beim ersten Anlegen des Volumes `pgdata`. Zum Ändern zuerst in der Datenbank umbiegen, dann den Wert im Panel tauschen und neu deployen:

```bash
docker exec <projekt>-postgres-1 psql -U schach -d schach -c "ALTER USER schach PASSWORD '<neu>'"
```

## Rollback

`release` per `workflow_dispatch` mit `image_tag` = letzter guter SHA. Achtung: `web` migriert beim Start. Eine schon angewendete Vorwärts-Migration bleibt in der Datenbank.
