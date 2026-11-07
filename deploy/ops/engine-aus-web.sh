#!/usr/bin/env bash
# Prueft aus dem web-Container heraus, ob der Engine-Dienst gesund ist (SCHACH-SPEC 15.3, Phase 0).
# Beweist Netz `internal`, Dienstnamen `engine` und den Engine-Healthcheck in einem Schritt.
#
#   deploy/ops/engine-aus-web.sh <web-container>
#
# Auf der Box: Containername mit `docker ps --format '{{.Names}}' | grep web` bestimmen.
# Das aspnet-Image hat weder curl noch wget, darum bash + /dev/tcp.
set -euo pipefail

container=${1:?Aufruf: engine-aus-web.sh <web-container>}

# shellcheck disable=SC2016 # Der Befehl laeuft im Container, nicht hier.
antwort=$(docker exec "$container" bash -c '
  exec 3<>/dev/tcp/engine/8081 &&
  printf "GET /healthz HTTP/1.1\r\nHost: engine\r\nConnection: close\r\n\r\n" >&3 &&
  cat <&3' | tr -d '\r')

status=$(printf '%s\n' "$antwort" | head -n 1)
echo "engine/healthz aus $container: $status"
printf '%s\n' "$antwort" | tail -n 1
case "$status" in
  *" 200 "*) exit 0 ;;
  *) echo "Engine aus dem web-Container nicht gesund erreichbar." >&2; exit 1 ;;
esac
