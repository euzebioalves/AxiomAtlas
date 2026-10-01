#!/usr/bin/env bash
set -Eeuo pipefail

ROOT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")/../.." && pwd)"
DEPLOY_DIR="$ROOT_DIR/deploy"
ENV_FILE="$ROOT_DIR/.env"
# shellcheck disable=SC2034 # Referenciado pelos scripts operacionais que importam esta biblioteca.
STATE_DIR="$ROOT_DIR/state"
# shellcheck disable=SC2034 # Referenciado pelos scripts operacionais que importam esta biblioteca.
BACKUP_DIR="$ROOT_DIR/backups"
COMPOSE_FILE="$DEPLOY_DIR/compose.prod.yml"

fail() { printf 'ERROR: %s\n' "$*" >&2; exit 1; }
require_command() { command -v "$1" >/dev/null 2>&1 || fail "Comando obrigatório ausente: $1"; }
require_file() { [[ -f "$1" ]] || fail "Arquivo obrigatório ausente: $1"; }

load_environment() {
  require_file "$ENV_FILE"
  set -a
  # shellcheck disable=SC1090
  source "$ENV_FILE"
  set +a
}

compose() {
  local compose_files=(--env-file "$ENV_FILE" -f "$COMPOSE_FILE")
  if [[ -n "${COMPOSE_ADDITIONAL_FILE:-}" ]]; then
    require_file "$COMPOSE_ADDITIONAL_FILE"
    compose_files+=(-f "$COMPOSE_ADDITIONAL_FILE")
  fi
  docker compose "${compose_files[@]}" "$@"
}

require_production_environment() {
  load_environment
  for value in APP_VERSION AXIOM_DOMAIN POSTGRES_DB POSTGRES_USER POSTGRES_PASSWORD ConnectionStrings__DefaultConnection JwtSettings__SecretKey; do
    [[ -n "${!value:-}" ]] || fail "Variável obrigatória ausente: $value"
    [[ "${!value}" != *CHANGE_ME* ]] || fail "A variável $value ainda usa um placeholder."
  done
}

update_env_version() {
  local version="$1"
  local temporary
  temporary="$(mktemp "${ENV_FILE}.XXXXXX")"
  trap 'rm -f "$temporary"' RETURN
  awk -v value="$version" 'BEGIN { found=0 } /^APP_VERSION=/ { print "APP_VERSION=" value; found=1; next } { print } END { if (!found) print "APP_VERSION=" value }' "$ENV_FILE" > "$temporary"
  chmod 600 "$temporary"
  mv "$temporary" "$ENV_FILE"
  trap - RETURN
}
