#!/usr/bin/env bash
# Backup-Pruefung (SCHACH-SPEC 14.1, Muster aus GladiusEtPilum). Ablauf und Einrichtung:
# deploy/restore-drill.md. Laeuft taeglich ueber .github/workflows/backup-check.yml.
#
# Liest das ARTEFAKT, nie den Status in Dokploy: fuer jedes Praefix listet es den Bucket, nimmt das
# neueste Objekt und schlaegt fehl, wenn es aelter als BACKUP_MAX_AGE_HOURS oder kleiner als
# BACKUP_MIN_BYTES ist. Dokploy meldete in GladiusEtPilum "success" fuer 20-Byte-Leerdumps
# (2026-09-08); der Status eines Jobs ist kein Beweis, Alter und Groesse des Objekts schon.
#
# Nur wenn jedes Praefix besteht, pingt es BACKUP_HEARTBEAT_URL. Der externe Heartbeat-Monitor
# meldet sich, wenn dieser taegliche Ping ausbleibt; so faellt auch eine Pruefung auf, die nie laeuft.
# BACKUP_HEARTBEAT_FAIL_URL wird, falls gesetzt, bei einem Fehler sofort gepingt.
#
# Der Schluessel darf den Bucket nur LISTEN: ListObjectsV2 liefert Key, Groesse und Zeitpunkt, mehr
# liest die Pruefung nicht.
#
# Umgebung (AWS_ACCESS_KEY_ID / AWS_SECRET_ACCESS_KEY / AWS_DEFAULT_REGION wie fuer jede aws-CLI):
#   BACKUP_ENDPOINT            S3-Endpunkt von Hetzner Object Storage
#   BACKUP_BUCKET              Bucket-Name
#   BACKUP_PREFIXES            Praefixe, durch Leerzeichen getrennt; fuer Schach `schach-prod/` bzw.
#                              der laengere Pfad, unter den Dokploy wirklich schreibt
#   BACKUP_MAX_AGE_HOURS       Standard 26 (ein Dump am Tag plus zwei Stunden Luft)
#   BACKUP_MIN_BYTES           Standard 8192. Eine leere Schach-Datenbank mit Schema ist klein;
#                              anheben, sobald die echten Dump-Groessen bekannt sind.
#   BACKUP_HEARTBEAT_URL       optional; nur gepingt, wenn alle Praefixe bestehen
#   BACKUP_HEARTBEAT_FAIL_URL  optional; gepingt, wenn ein Praefix scheitert
#
# Wer die Datei einliest (source), bekommt nur `evaluate`; backup-check.test.sh macht das.
set -euo pipefail

# Eine Urteilszeile je Praefix aus einem ListObjectsV2-JSON auf stdin:
#   OK|STALE|SMALL|EMPTY|ERROR <praefix> ...
# Argumente: Praefix, jetzt (Epoch-Sekunden), Hoechstalter in Stunden, Mindestgroesse in Bytes.
evaluate() {
  local prefix=$1 now=$2 max_age_h=$3 min_bytes=$4 listing out
  listing=$(cat)
  [ -n "$listing" ] || listing='{}'
  if out=$(jq -r --arg p "$prefix" --argjson now "$now" --argjson maxh "$max_age_h" --argjson min "$min_bytes" '
    # S3-Zeitstempel sind UTC. Die aws-CLI schreibt +00:00, die rohe API Z, mit oder ohne
    # Millisekunden. Alles andere wird abgelehnt statt falsch gelesen.
    def epoch:
      if test("^[0-9]{4}-[0-9]{2}-[0-9]{2}T[0-9]{2}:[0-9]{2}:[0-9]{2}(\\.[0-9]+)?(Z|\\+00:00)$")
      then .[0:19] + "Z" | fromdateiso8601
      else error("unerwartetes LastModified-Format: \(.)") end;
    (.Contents // []) as $objects
    | if ($objects | length) == 0 then
        "EMPTY \($p) keine Objekte unter diesem Praefix"
      else
        ($objects | map(. + {t: (.LastModified | epoch)}) | max_by(.t)) as $o
        | (($now - $o.t) / 3600 * 10 | floor / 10) as $age
        | (if $age > $maxh then "STALE"
           elif $o.Size < $min then "SMALL"
           else "OK" end)
          + " \($p) key=\($o.Key) size=\($o.Size) age_h=\($age)"
      end
  ' <<<"$listing" 2>&1); then
    echo "$out"
  else
    echo "ERROR $prefix Liste nicht lesbar: ${out//$'\n'/ }"
  fi
}

ping() {
  [ -n "${1:-}" ] || return 0
  curl -fsS -m 10 --retry 3 -o /dev/null "$1" || echo "Warnung: Heartbeat-Ping fehlgeschlagen" >&2
}

main() {
  : "${BACKUP_ENDPOINT:?BACKUP_ENDPOINT setzen}" "${BACKUP_BUCKET:?BACKUP_BUCKET setzen}" "${BACKUP_PREFIXES:?BACKUP_PREFIXES setzen}"
  local max_age_h=${BACKUP_MAX_AGE_HOURS:-26} min_bytes=${BACKUP_MIN_BYTES:-8192}
  local now failed=0 prefix listing verdict
  now=$(date -u +%s)
  echo "max_age_h=$max_age_h min_bytes=$min_bytes"

  for prefix in $BACKUP_PREFIXES; do
    if listing=$(aws s3api list-objects-v2 --bucket "$BACKUP_BUCKET" --prefix "$prefix" \
                   --endpoint-url "$BACKUP_ENDPOINT" --output json); then
      verdict=$(evaluate "$prefix" "$now" "$max_age_h" "$min_bytes" <<<"$listing")
    else
      verdict="ERROR $prefix Auflisten fehlgeschlagen (Schluessel, Policy oder Endpunkt)"
    fi
    echo "$verdict"
    case $verdict in OK\ *) ;; *) failed=1 ;; esac
  done

  if [ "$failed" = 0 ]; then
    ping "${BACKUP_HEARTBEAT_URL:-}"
    echo "alle Praefixe bestanden"
  else
    ping "${BACKUP_HEARTBEAT_FAIL_URL:-}"
    echo "Backup-Pruefung FEHLGESCHLAGEN" >&2
    exit 1
  fi
}

if [ "${BASH_SOURCE[0]}" = "$0" ]; then
  main "$@"
fi
