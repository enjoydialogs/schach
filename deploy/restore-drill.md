# Backup und Restore-Probe

SCHACH-SPEC 14.1: Dokploy sichert Postgres nachts um 03:40 nach Hetzner Object Storage, Präfix `schach-prod/`, höchstens 30 Tage. `.github/workflows/backup-check.yml` prüft jeden Morgen den neuesten Dump. Vor Go-live (Deploy 3) einmal die Restore-Probe unten durchführen und das Ergebnis im Ticket festhalten.

Bucket-Name, Endpunkt, Schlüssel und Heartbeat-URLs stehen nie im Repo. Sie liegen im Dokploy-Panel, in den GitHub-Secrets und -Variablen und beim Betreiber.

## Was wo liegt

| Teil | Was es tut |
|---|---|
| Dokploy, Dienst `postgres`, Reiter „Backups“ | `pg_dump` um 03:40, Upload in den Bucket unter `schach-prod/`, 30 Tage |
| [`ops/backup-check.sh`](ops/backup-check.sh) | Listet je Präfix den Bucket, nimmt das **neueste Objekt** und schlägt fehl, wenn es älter als 26 h oder kleiner als die Mindestgröße ist. Liest das Artefakt, nie den Dokploy-Status: Dokploy meldete in GladiusEtPilum „success“ für 20-Byte-Leerdumps. |
| [`ops/backup-check.test.sh`](ops/backup-check.test.sh) | Fixture-Fälle: frisch, 30 h alt, 20 Byte, leeres Präfix, neuester statt größter, fremde Zeitzone, kein JSON. Läuft bei jedem PR, der die Prüfung ändert. |
| [`../.github/workflows/backup-check.yml`](../.github/workflows/backup-check.yml) | Täglich 05:30 UTC auf `ubuntu-latest`, außerhalb des VPS. Warnt nur, bis der Schlüssel existiert. |

**Die Meldung kommt vom Heartbeat-Monitor, nicht von GitHub.** Nur ein bestandener Lauf pingt. Fällt die Prüfung durch, läuft sie nicht oder stellt GitHub den Zeitplan ein (60 Tage ohne Aktivität im Repo), bleibt der Ping aus und der Monitor mailt.

## 1 · Backup in Dokploy einrichten

1. Hetzner Console → Object Storage: Bucket anlegen oder den vorhandenen gemeinsamen Bucket nehmen. Einen S3-Schlüssel `dokploy-write` für Dokploy erzeugen (schreiben und löschen, damit die Aufbewahrung greift).
2. Dokploy → Settings → S3 Destinations: Ziel mit Endpunkt, Region, Bucket und diesem Schlüssel anlegen, „Test“ drücken.
3. Dokploy → Projekt Schach → Compose-Dienst → Backups → „Create Backup“:
   - Dienst `postgres`, Datenbank `schach`, Benutzer `schach`.
   - Ziel: die S3 Destination aus Schritt 2, Präfix `schach-prod/`.
   - Zeitplan `40 3 * * *`.
   - Aufbewahrung: 30 Backups (eins je Nacht = 30 Tage).
4. Zeitzone der Box prüfen, denn Dokploys Cron läuft in Box-Zeit: `timedatectl | grep "Time zone"`. Bei UTC liegt der Lauf um 03:40 UTC (04:40/05:40 Berlin). Beides passt zur Prüfung um 05:30 UTC.
5. Einmal „Run now“ drücken. Danach im Bucket nachsehen (Schritt 2 unten), welcher Pfad wirklich entstanden ist. Dokploy hängt Unterordner an; der Prüfung das längste gemeinsame Präfix geben, das nur Schach-Dumps enthält.

## 2 · Schlüssel nur zum Auflisten

1. Mit dem Besitzer-Schlüssel in einer Shell den Bucket listen:

   ```bash
   export AWS_ACCESS_KEY_ID='<besitzer>' AWS_SECRET_ACCESS_KEY='<geheim>' AWS_DEFAULT_REGION=<region>
   EP=<endpunkt>
   BUCKET=<bucket>
   aws s3 ls "s3://$BUCKET/schach-prod/" --recursive --endpoint-url "$EP" | tail -n 5
   ```

   Größe des neuesten Dumps notieren. Eine leere Datenbank mit Schema ist klein (einige KB). Ein Wert von 20 Byte ist ein Leerdump.
2. Hetzner Console → S3 credentials → Schlüssel `schach-backup-check` erzeugen.
3. Bucket-Policy erweitern, so dass `schach-backup-check` nur `s3:ListBucket` darf. **Erst die vorhandene Policy lesen**, denn `put-bucket-policy` ersetzt das ganze Dokument. Vor dem Anwenden das neue Dokument ausgeben, prüfen, dass der neue Schlüssel genau einmal und der Besitzer-Schlüssel gar nicht als Principal vorkommt (GladiusEtPilum `deploy/backup-monitor.md`, Schritt 2, beschreibt die Falle):

   ```bash
   aws s3api get-bucket-policy --bucket "$BUCKET" --endpoint-url "$EP" --query Policy --output text | jq . > policy-vorher.json
   cp policy-vorher.json policy-nachher.json   # Statement fuer schach-backup-check ergaenzen
   diff policy-vorher.json policy-nachher.json
   aws s3api put-bucket-policy --bucket "$BUCKET" --endpoint-url "$EP" --policy file://policy-nachher.json
   ```

4. In einer zweiten Shell nur mit dem neuen Schlüssel prüfen: `aws s3api list-objects-v2 … --prefix schach-prod/` liefert eine Liste; `aws s3 cp s3://$BUCKET/<key> -` scheitert mit `AccessDenied`.

## 3 · Heartbeat-Monitor

Bei einem Heartbeat-Dienst (z. B. Healthchecks.io) einen Check `schach-backup` anlegen: Periode 1 Tag, Gnadenfrist 2 Stunden, Mail an den Betreiber. Ping-URL und, falls angeboten, die `/fail`-URL notieren.

## 4 · GitHub einrichten

Repository → Settings → Secrets and variables → Actions:

| Art | Name | Wert |
|---|---|---|
| Secret | `BACKUP_CHECK_ACCESS_KEY_ID` | Schlüssel aus Schritt 2 |
| Secret | `BACKUP_CHECK_SECRET_ACCESS_KEY` | dazu |
| Secret | `BACKUP_HEARTBEAT_URL` | Ping-URL |
| Secret | `BACKUP_HEARTBEAT_FAIL_URL` | optional, `/fail`-URL |
| Variable | `BACKUP_ENDPOINT` | S3-Endpunkt |
| Variable | `BACKUP_BUCKET` | Bucket |
| Variable | `BACKUP_PREFIXES` | Präfix aus Schritt 1.5 |
| Variable | `BACKUP_REGION` | optional, Standard `fsn1` |
| Variable | `BACKUP_MIN_BYTES` | optional, Standard 8192; auf etwa die Hälfte der echten Dump-Größe setzen |

Dann Actions → `backup-check` → „Run workflow“. Erwartet: `OK schach-prod/… size=… age_h=…` und `alle Praefixe bestanden`; der Monitor zeigt den Ping.

## 5 · Restore-Probe (einmal vor Go-live, danach nach jeder Schemaänderung größerer Art)

Alles auf dem VPS. Die Probe berührt die Live-Datenbank nur lesend. Sie stellt den **Dump aus dem Bucket** wieder her, nicht einen frischen, denn genau dieser Weg muss im Ernstfall tragen.

```bash
docker compose ls                                   # Projektname des Schach-Dienstes
APP=<projektname>
DIR=/root/schach-restore && mkdir -p "$DIR" && cd "$DIR"
df -h / | tail -1                                   # ein paar GB frei reichen

# Neuesten Dump holen (Besitzer- oder Lese-Schluessel; der List-Schluessel darf das nicht).
export AWS_ACCESS_KEY_ID='<…>' AWS_SECRET_ACCESS_KEY='<…>' AWS_DEFAULT_REGION=<region>
EP=<endpunkt>; BUCKET=<bucket>; PREFIX=<praefix aus schritt 1.5>
KEY=$(aws s3api list-objects-v2 --bucket "$BUCKET" --prefix "$PREFIX" --endpoint-url "$EP" \
        --query 'sort_by(Contents,&LastModified)[-1].Key' --output text)
echo "KEY=$KEY"
aws s3 cp "s3://$BUCKET/$KEY" "$DIR/dump" --endpoint-url "$EP"
ls -l "$DIR/dump"                                   # Groesse notieren

# Dokploy schreibt `.sql.gz`, darin aber pg_dump im CUSTOM-Format (GladiusEtPilum, 2026-09-08).
# Ohne Pipe entpacken, damit ein kaputtes Archiv hier scheitert und nicht still leer einspielt.
gunzip -t "$DIR/dump" && gunzip -c "$DIR/dump" > "$DIR/dump.raw" || cp "$DIR/dump" "$DIR/dump.raw"
head -c5 "$DIR/dump.raw"; echo                     # PGDMP = custom format, sonst Klartext-SQL
```

Wiederherstellen in einen **isolierten** Container: eigener Name, kein Netz (`--network none`), kein Port, gleiche Postgres-Hauptversion wie live (`postgres:17`):

```bash
date -u +"restore start %FT%TZ" | tee "$DIR/protokoll.txt"
docker run -d --name schach-restore --network none --cpus 1 --memory 1g \
  -e POSTGRES_USER=schach -e POSTGRES_DB=schach -e POSTGRES_PASSWORD="$(openssl rand -hex 16)" postgres:17
until docker exec schach-restore pg_isready -U schach -d schach; do sleep 2; done
docker cp "$DIR/dump.raw" schach-restore:/tmp/dump

if [ "$(head -c5 "$DIR/dump.raw")" = "PGDMP" ]; then
  docker exec schach-restore pg_restore -U schach -d schach --no-owner --no-privileges --exit-on-error /tmp/dump
else
  docker exec schach-restore psql -v ON_ERROR_STOP=1 -q -U schach -d schach -f /tmp/dump
fi
echo "restore exit $?" | tee -a "$DIR/protokoll.txt"   # 0
date -u +"restore ende %FT%TZ" | tee -a "$DIR/protokoll.txt"
```

Vergleichen mit live:

```bash
# Nur lesend: letzte Migration und exakte Zeilenzahl je Tabelle (die Datenbank ist klein).
Q="SELECT max(\"MigrationId\") FROM \"__EFMigrationsHistory\";
   SELECT format('SELECT %L, count(*) FROM %I.%I;', tablename, schemaname, tablename)
     FROM pg_tables WHERE schemaname = 'public' ORDER BY tablename \\gexec"
echo "$Q" | docker exec -i "$APP-postgres-1" psql -U schach -d schach -At > "$DIR/live.txt"
echo "$Q" | docker exec -i schach-restore   psql -U schach -d schach -At > "$DIR/restore.txt"
diff "$DIR/live.txt" "$DIR/restore.txt" && echo "gleich"
```

Erwartet: gleiche letzte Migration. Zeilenzahlen dürfen um das abweichen, was seit 03:40 live dazukam (neue Partien, Züge); keine Tabelle darf im Restore leer sein, die live gefüllt ist. `DataProtectionKeys` muss Zeilen haben, sonst sind nach einem Restore alle Sitzungen ungültig.

Aufräumen:

```bash
docker rm -f schach-restore
rm -rf "$DIR"
```

## 6 · Protokoll

Im Ticket festhalten: Datum, Key des Dumps, Größe, Restore-Dauer (Start bis Ende), Exit-Code, Ergebnis des Vergleichs, Abweichungen. Erst dann gilt die Restore-Probe als durchgeführt.

## Ernstfall

Wie Schritt 5, aber in die Live-Datenbank: `web` in Dokploy stoppen, Volume `pgdata` sichern (`docker run --rm -v <projekt>_pgdata:/d -v /root:/b alpine tar czf /b/pgdata-vorher.tgz -C /d .`), Datenbank `schach` neu anlegen (über die Datenbank `postgres`: `psql -U schach -d postgres -c 'DROP DATABASE schach' -c 'CREATE DATABASE schach'`), Dump einspielen, `web` starten, Rauchtest (`deploy/ops/rauchtest.sh`). Alles seit dem letzten Dump ist verloren.
