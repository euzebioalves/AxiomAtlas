#!/usr/bin/env bash
set -Eeuo pipefail
SCRIPT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
# shellcheck disable=SC1091
source "$SCRIPT_DIR/lib.sh"

version="${1:-}"
[[ "$version" =~ ^[0-9]+\.[0-9]+\.[0-9]+$ ]] || fail "Uso: $0 <versão-sem-v>. Exemplo: $0 1.0.17"
require_production_environment
require_command docker
require_command flock
require_command curl
mkdir -p "$STATE_DIR" "$BACKUP_DIR"

exec 9>"$STATE_DIR/deploy.lock"
flock -n 9 || fail "Já existe uma implantação ou rollback em andamento."
available_kb="$(df --output=avail -k "$ROOT_DIR" | tail -1 | tr -d ' ')"
(( available_kb >= 5242880 )) || fail "Espaço livre insuficiente: são necessários pelo menos 5 GB."

current="$(cat "$STATE_DIR/current-version" 2>/dev/null || true)"
"$SCRIPT_DIR/backup-postgres.sh"
"$SCRIPT_DIR/backup-state.sh"

APP_VERSION="$version" compose pull api web migrate caddy
for image in "ghcr.io/euzebioalves/axiomatlas-api:$version" "ghcr.io/euzebioalves/axiomatlas-web:$version" "ghcr.io/euzebioalves/axiomatlas-migrator:$version"; do
  docker image inspect "$image" >/dev/null
done

APP_VERSION="$version" compose run --rm migrate
APP_VERSION="$version" compose up -d --remove-orphans postgres api web caddy
"$SCRIPT_DIR/healthcheck.sh"

[[ -z "$current" ]] || printf '%s\n' "$current" > "$STATE_DIR/previous-version"
printf '%s\n' "$version" > "$STATE_DIR/current-version"
date -u +%Y-%m-%dT%H:%M:%SZ > "$STATE_DIR/last-successful-deploy"
update_env_version "$version"
printf 'Implantação %s concluída com sucesso.\n' "$version"
