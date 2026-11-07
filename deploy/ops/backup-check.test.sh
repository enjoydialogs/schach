#!/usr/bin/env bash
# Fixture-Tests fuer die Entscheidung von backup-check.sh (SCHACH-SPEC 14.1). Laeuft in
# .github/workflows/backup-check.yml bei jedem PR, der deploy/ops/backup-check* aendert, und lokal:
#   bash deploy/ops/backup-check.test.sh
# Braucht bash und jq. Kein Bucket, kein Schluessel: jeder Fall fuettert ein ListObjectsV2-Dokument
# und prueft das Urteilswort.
set -euo pipefail
# shellcheck source=deploy/ops/backup-check.sh
source "$(dirname "$0")/backup-check.sh"

NOW=1793336400   # 2026-10-30T05:00:00Z
fails=0

expect() {
  local want=$1 name=$2 listing=$3 got
  got=$(evaluate schach-prod/ "$NOW" 26 8192 <<<"$listing")
  if [ "${got%% *}" = "$want" ]; then
    echo "ok   $name: $got"
  else
    echo "FAIL $name: erwartet $want, bekommen: $got"
    fails=$((fails + 1))
  fi
}

expect OK    "frischer Dump realer Groesse" \
  '{"Contents":[{"Key":"schach-prod/2026-10-30T02:40:05.sql.gz","Size":4194304,"LastModified":"2026-10-30T02:40:12+00:00"}]}'
expect STALE "neuester Dump ist 30 Stunden alt" \
  '{"Contents":[{"Key":"schach-prod/2026-10-28T23:00:05.sql.gz","Size":4194304,"LastModified":"2026-10-28T23:00:00+00:00"}]}'
expect SMALL "der 20-Byte-Leerdump, den Dokploy Erfolg nannte" \
  '{"Contents":[{"Key":"schach-prod/2026-10-30T02:40:05.sql.gz","Size":20,"LastModified":"2026-10-30T02:40:12+00:00"}]}'
expect EMPTY "nichts unter dem Praefix" \
  '{"RequestCharged":null,"Prefix":"schach-prod/"}'
expect EMPTY "die CLI gab gar nichts aus" ''
expect SMALL "der NEUESTE Dump entscheidet, nicht der groesste" \
  '{"Contents":[{"Key":"a","Size":4194304,"LastModified":"2026-10-29T02:40:12+00:00"},{"Key":"b","Size":20,"LastModified":"2026-10-30T02:40:12+00:00"}]}'
expect OK    "ein alter Leerdump verdeckt keinen guten neuen" \
  '{"Contents":[{"Key":"a","Size":20,"LastModified":"2026-10-29T02:40:12+00:00"},{"Key":"b","Size":4194304,"LastModified":"2026-10-30T02:40:12.000Z"}]}'
expect ERROR "Zeitstempel in anderer Zone wird abgelehnt, nicht falsch gelesen" \
  '{"Contents":[{"Key":"a","Size":4194304,"LastModified":"2026-10-30T02:40:12+02:00"}]}'
expect ERROR "kein JSON" 'Access Denied'

[ "$fails" = 0 ] || { echo "$fails Fall/Faelle fehlgeschlagen" >&2; exit 1; }
echo "alle Faelle bestanden"
