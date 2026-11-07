#!/usr/bin/env bash
# Rauchtest nach jedem Deploy (SCHACH-SPEC 14.2, 15.2). Laeuft in release.yml gegen die Domain und
# in der PR-CI gegen `docker compose --profile full` auf localhost.
#
#   deploy/ops/rauchtest.sh https://schach.enjoy-dialogs.de
#
# Spaetere Sessions haengen ihre Pruefung unten an (Rechtstexte S3c, /sitzung S2a, Gast-Partie ab
# Deploy 2), damit release.yml unveraendert bleibt.
#
# Umgebung: RAUCHTEST_VERSUCHE (Standard 60) x 5 s Warten auf /healthz.
set -euo pipefail

basis=${1:?Aufruf: rauchtest.sh <basis-url>}
basis=${basis%/}
versuche=${RAUCHTEST_VERSUCHE:-60}
fail=0

# ---------------------------------------------------------------- 1. /healthz
# Durch Traefik beweist das DNS, Zertifikat und Router. Ein paar 502/503 waehrend der start_period
# von `web` sind normal.
code=
for versuch in $(seq 1 "$versuche"); do
  code=$(curl -sS -o /dev/null -w '%{http_code}' --max-time 10 "$basis/healthz" || true)
  if [ "$code" = "200" ]; then
    echo "/healthz 200 nach $versuch Versuch(en)"
    break
  fi
  echo "Versuch $versuch: /healthz lieferte ${code:-keine Antwort}"
  sleep 5
done
[ "$code" = "200" ] || { echo "$basis/healthz wurde nicht gesund." >&2; exit 1; }

# ---------------------------------------------------------------- 2. /readyz
# Fragt die Datenbank. Program.cs migriert vor app.Run(), also beweist die 200 auch die Migration.
code=$(curl -sS -o /dev/null -w '%{http_code}' --max-time 10 "$basis/readyz" || true)
echo "/readyz -> $code"
[ "$code" = "200" ] || { echo "  Datenbank nicht erreichbar." >&2; fail=1; }

# ---------------------------------------------------------------- 3. Asset und Header
# `/` mit 200 beweist nichts: ein Skript muss als Skript kommen (Kronrat, Erst-Deploy 2026-08-10).
asset=$(curl -fsS --max-time 10 "$basis/" | grep -o '/assets/index-[^"]*\.js' | head -n 1 || true)
if [ -z "$asset" ]; then
  echo "Kein Skript-Verweis in der Startseite gefunden." >&2
  exit 1
fi

header() { curl -fsSI --max-time 10 "$basis$1" | tr -d '\r' | awk -F': ' -v h="$2" 'tolower($1)==h{print $2}'; }
expect() { # Pfad, Header, Muster, Bedeutung bei Abweichung
  local wert
  wert=$(header "$1" "$2" || true)
  echo "$1 -> $2: ${wert:-(keiner)}"
  # shellcheck disable=SC2254 # $3 ist bewusst ein Muster.
  case "$wert" in
    $3) ;;
    *) echo "  $4" >&2; fail=1 ;;
  esac
}

expect "$asset" content-type '*javascript*' 'Die Instanz liefert index.html fuer Assets aus.'
expect "$asset" cache-control '*immutable*' 'Das gehashte Buendel muesste ohne Nachfrage aus dem Cache kommen.'
expect / content-type '*text/html*' 'Die Startseite ist kein HTML.'
expect / cache-control '*no-cache*' 'Ohne no-cache haelt ein Browser die index.html eines alten Release.'
expect / content-security-policy "*default-src 'self'*" 'CSP fehlt (SecurityHeaders.cs).'
expect / x-content-type-options 'nosniff' 'X-Content-Type-Options fehlt.'
expect / referrer-policy 'no-referrer' 'Referrer-Policy fehlt.'

# Eine fehlende API-Route ist ein 404, kein 200 mit HTML (SpaHosting).
code=$(curl -sS -o /dev/null -w '%{http_code}' --max-time 10 "$basis/api/v1/rauchtest-gibt-es-nicht" || true)
echo "/api/v1/<fehlt> -> $code"
[ "$code" = "404" ] || { echo "  Die Rueckfallroute beantwortet /api/." >&2; fail=1; }

# ---------------------------------------------------------------- Spaetere Pruefungen (anhaengen)

exit "$fail"
