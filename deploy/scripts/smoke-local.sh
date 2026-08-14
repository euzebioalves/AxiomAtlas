#!/usr/bin/env bash
set -Eeuo pipefail

base_url="${1:-http://127.0.0.1:8088}"
curl --fail --silent --show-error "$base_url/health/live" >/dev/null
curl --fail --silent --show-error "$base_url/health/ready" >/dev/null
status="$(curl --silent --output /dev/null --write-out '%{http_code}' "${base_url}/register")"
[[ "$status" == "404" ]] || { echo "O endpoint /register não retornou 404." >&2; exit 1; }
printf 'Smoke tests concluídos.\n'
