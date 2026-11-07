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
- Warn-Logzeilen `BudgetWarn`, `BudgetErschoepft`, `EngineUeberlast`, `MailKontingent`, `SprachausgabeBudgetWarn`, `SprachausgabeBudgetErschoepft`, `StimmeUnbekannt`: je Art einmal je Fenster (Budget und Sprachausgabe je Kalendermonat, sonst `Betrieb__WarnFensterMinuten`, Standard 60), dazu eine Mail an `OPERATOR_EMAIL` (Engine und Mail höchstens eine je Tag). Ohne Mail-Dienst (vor S2a) oder ohne Adresse nur das Log.
- Tageszeile: `Tageszeile <datum>: …` jeden Morgen um 06:00 Berliner Zeit, mit den Zählern seit der letzten Zeile. Nur Log, keine Mail.
- Suchen: `docker logs <projekt>-web-1 2>&1 | grep -E "BudgetWarn|BudgetErschoepft|EngineUeberlast|MailKontingent|SprachausgabeBudget|StimmeUnbekannt|Tageszeile"`.

## Ablauf eines Deploys

1. `main` baut bei jedem Push beide Images und pusht `<sha>` und `main`.
2. Actions → `release` → „Run workflow“. Ohne `image_tag` wird der aktuelle Stand gebaut, mit `image_tag` ein vorhandener SHA ausgeliefert (Rollback).
3. Der Lauf zeigt `prod` auf den SHA, bittet Dokploy um den Deploy, wartet und fährt den Rauchtest gegen die Domain.

## Erst-Einrichtung (Deploy 1)

Einmalig, vom Betreiber:

1. DNS: `schach.enjoy-dialogs.de` auf den VPS zeigen lassen.
2. GHCR: die Pakete `schach-web` und `schach-engine` sind öffentlich ([ADR 0002](../docs/adr/0002-ghcr-pakete-oeffentlich.md)); Dokploy braucht keinen Registry-Zugang. Grund: so hängen Deploys nicht an einem geteilten GHCR-Token, und der Quellcode ist ohnehin öffentlich. Umstellen nach dem ersten main-Lauf: GitHub → Organisation `enjoydialogs` → Packages → Paket → Package settings → Change visibility → Public; „Inherit access from source repository“ bleibt an.
3. Dokploy: neuen Compose-Dienst aus diesem Repo anlegen, Compose-Pfad `docker-compose.dokploy.yml`.
4. Environment-Tab: Schlüssel aus `.env.dokploy.example` setzen. Pflicht: `REGISTRY`, `IMAGE_TAG=prod`, `POSTGRES_PASSWORD`, `TRAEFIK_NETWORK`. Das Subnetz auf der Box lesen:
   `docker network inspect dokploy-network --format '{{(index .IPAM.Config 0).Subnet}}'`
   Optional, leer gilt die Vorgabe aus dem Code: `MAIL_MAX_JE_TAG` (`Mail:MaxJeTag`) an den gebuchten Lettermint-Tarif anpassen (SCHACH-SPEC 16, Punkt 4). `LIMITS_*` (`Limits:*` aus SCHACH-SPEC 11.2) nach der VPS-Messung Teil 2 setzen (Punkt 1).
5. Domains-Tab: `schach.enjoy-dialogs.de` am Dienst `web`, Container-Port `8080`, HTTPS mit Let's Encrypt.
6. GitHub → Settings → Environments → `production`: Variablen `DOKPLOY_URL`, `DOKPLOY_COMPOSE_ID` (letzter Abschnitt der Dienst-URL im Panel), Secret `DOKPLOY_API_KEY` (Member mit Zugriff nur auf dieses Projekt).
7. `release` per `workflow_dispatch` starten. Der Rauchtest muss grün enden.
8. Auf der Box prüfen, dass `web` die Engine erreicht (aus dem Checkout, den Dokploy angelegt hat):
   `bash deploy/ops/engine-aus-web.sh "$(docker ps --format '{{.Names}}' | grep -- '-web-1$' | grep schach)"`
   Erwartet: `HTTP/1.1 200 OK` und `{"status":"ok"}`.

## Sprachausgabe (SCHACH-SPEC 6.9)

Die Gegner lesen ihre Sprüche über Mistral Voxtral TTS vor. Ohne `MISTRAL_API_KEY` oder ohne Voice-Id bleibt der Gegner stumm; die Partie läuft unverändert weiter.

1. **Voice-Ids setzen:** im Environment-Tab `SPRACHAUSGABE_STIMME_<GEGNER>` je Gegner (`FINN`, `ILSE`, `KONRAD`, `MAREN`, `NELE`, `OSCAR`, `SEMENTA`, `THEO`). Fertigstimmen (z. B. `voice_481`) oder die Id einer geklonten Stimme aus dem Workspace „schach“. Nie Ids oder Aufnahmen ins Repo. Optional je Gegner `SPRACHAUSGABE_AUSDRUCK_<GEGNER>` (Expressiveness, 1.0 bis 5.0, Punkt als Dezimaltrenner).
2. **Budget:** `SPRACHAUSGABE_BUDGET_CENT` (Vorgabe `500`, `0` schaltet ab). Etwa 1.400 Zeichen je Partie, also rund 2 Cent; 5 € reichen für etwa 250 Partien im Monat. Der Preis steht als `Sprachausgabe__CentJeMillionZeichen` (Vorgabe `1300`) im Code; bei neuer Preisliste anpassen.
3. **Mistral-Limit:** das harte Workspace-Limit (heute 10 €) auf mindestens Chat-Budget plus Sprachausgabe-Budget anheben, sonst antwortet Mistral mit 429 und beide Wege fallen auf Ersatz bzw. Stille.
4. **Geklonte Stimmen jährlich erneuern:** Mistral löscht gespeicherte Stimmen nach `retention_notice` (höchstens 365 Tage). Danach kommt 404, im Log steht `StimmeUnbekannt` und eine Mail geht an `OPERATOR_EMAIL` (einmal je Monat). Vor Ablauf die Stimme mit `retention_notice=365` erneuern oder neu klonen und die neue Id im Environment-Tab eintragen. Der Ton-Cache hängt an der Voice-Id; eine neue Id erzeugt die Sprüche neu.
5. **Einwilligung:** für jede geklonte Stimme liegt die schriftliche Einwilligung der sprechenden Person vor (außerhalb des Repos). Widerruf heißt: Stimme bei Mistral löschen und Id leeren.

## Engine-Modell (Maia-3)

Der Dienst `engine` lädt beim ersten Start die Maia-3-Gewichte (ca. 21 MB) von `huggingface.co` in das Volume `engine-models` (`/models`). Bis das Modell geprüft geladen ist, meldet `/healthz` 503 und der Dienst versucht es mit wachsender Pause erneut. Die Compose-Datei sorgt bereits für beides, was er dafür braucht: `engine` hängt zusätzlich an `dokploy-network` (`internal` hat keine Route nach draußen), und das Image legt `/models` für UID 10001 an, sodass ein neues Volume diese Rechte übernimmt.

Pflichten des Betreibers:

1. Ausgehendes HTTPS (443) von der Box zu `huggingface.co` erlauben (Firewall des VPS bzw. Anbieters).
2. Nur falls `engine-models` schon existiert und nicht UID 10001 gehört (von Hand oder von einem anderen Image angelegt; Log: `Permission denied` unter `/models`), einmalig umbiegen:
   `docker run --rm -v <projekt>_engine-models:/models alpine chown -R 10001 /models`

## Postgres-Passwort ändern

`POSTGRES_PASSWORD` wirkt nur beim ersten Anlegen des Volumes `pgdata`. Zum Ändern zuerst in der Datenbank umbiegen, dann den Wert im Panel tauschen und neu deployen:

```bash
docker exec <projekt>-postgres-1 psql -U schach -d schach -c "ALTER USER schach PASSWORD '<neu>'"
```

## Rollback

`release` per `workflow_dispatch` mit `image_tag` = letzter guter SHA. Achtung: `web` migriert beim Start. Eine schon angewendete Vorwärts-Migration bleibt in der Datenbank.
