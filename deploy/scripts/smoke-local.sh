#!/usr/bin/env bash
set -Eeuo pipefail

domain="${AXIOM_DOMAIN:?AXIOM_DOMAIN deve estar configurado}"
port="${AXIOM_LOCAL_PORT:-8088}"
base_url="http://${domain}:${port}"
curl --resolve "${domain}:${port}:127.0.0.1" --fail --silent --show-error "$base_url/health/live" >/dev/null
curl --resolve "${domain}:${port}:127.0.0.1" --fail --silent --show-error "$base_url/health/ready" >/dev/null
status="$(curl --resolve "${domain}:${port}:127.0.0.1" --silent --output /dev/null --write-out '%{http_code}' "${base_url}/register")"
[[ "$status" == "404" ]] || { echo "O endpoint /register não retornou 404." >&2; exit 1; }
printf 'Smoke tests concluídos.\n'
