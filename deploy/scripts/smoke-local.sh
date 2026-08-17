#!/usr/bin/env bash
set -Eeuo pipefail

domain="${AXIOM_DOMAIN:?AXIOM_DOMAIN deve estar configurado}"
port="${AXIOM_LOCAL_PORT:-8088}"
base_url="http://${domain}:${port}"
response_file="$(mktemp)"
trap 'rm -f "$response_file"' EXIT

check_endpoint() {
  local endpoint="$1"
  local expected_status="$2"
  local actual_status

  if ! actual_status="$(curl --resolve "${domain}:${port}:127.0.0.1" --silent --show-error \
    --output "$response_file" --write-out '%{http_code}' "${base_url}${endpoint}")"; then
    echo "Falha de conexão ao endpoint ${endpoint}." >&2
    exit 1
  fi

  if [[ "$actual_status" != "$expected_status" ]]; then
    echo "${endpoint}: esperado HTTP ${expected_status}, recebido HTTP ${actual_status}." >&2
    cat "$response_file" >&2
    exit 1
  fi

  if [[ "$expected_status" == "200" && ! -s "$response_file" ]]; then
    echo "${endpoint}: recebeu HTTP 200, mas a resposta está vazia." >&2
    exit 1
  fi

  printf '%s: HTTP %s\n' "$endpoint" "$actual_status"
}

check_endpoint /health/live 200
check_endpoint /health/ready 200
check_endpoint /health/pdf 404
check_endpoint /register 404
