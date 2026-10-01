#!/usr/bin/env bash
set -Eeuo pipefail
SCRIPT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
# shellcheck disable=SC1091
source "$SCRIPT_DIR/lib.sh"

requested="${1:-}"
require_production_environment
mkdir -p "$STATE_DIR"
version="${requested:-$(cat "$STATE_DIR/previous-version" 2>/dev/null || true)}"
[[ "$version" =~ ^[0-9]+\.[0-9]+\.[0-9]+$ ]] || fail "Informe uma versão válida ou mantenha previous-version no estado."
exec 9>"$STATE_DIR/deploy.lock"
flock -n 9 || fail "Já existe uma implantação ou rollback em andamento."

printf 'ATENÇÃO: rollback troca somente as imagens da aplicação. Migrations não são revertidas automaticamente.\n'
"$SCRIPT_DIR/backup-postgres.sh"
"$SCRIPT_DIR/backup-state.sh"
APP_VERSION="$version" compose pull api web caddy
APP_VERSION="$version" compose up -d --no-deps api web caddy
"$SCRIPT_DIR/healthcheck.sh"
current="$(cat "$STATE_DIR/current-version" 2>/dev/null || true)"
[[ -z "$current" ]] || printf '%s\n' "$current" > "$STATE_DIR/previous-version"
printf '%s\n' "$version" > "$STATE_DIR/current-version"
date -u +%Y-%m-%dT%H:%M:%SZ > "$STATE_DIR/last-successful-deploy"
update_env_version "$version"
printf 'Rollback da aplicação para %s concluído.\n' "$version"
