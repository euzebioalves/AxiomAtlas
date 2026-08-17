#!/usr/bin/env bash
set -Eeuo pipefail
SCRIPT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
# shellcheck disable=SC1091
source "$SCRIPT_DIR/lib.sh"

require_production_environment
require_command age
require_command docker
if [[ "${BACKUP_LOCAL_TEST:-false}" != "true" ]]; then require_command rclone; fi
mkdir -p "$BACKUP_DIR/state" "$STATE_DIR"

timestamp="$(date -u +%Y%m%dT%H%M%SZ)"
work="$(mktemp -d "$BACKUP_DIR/state/.${timestamp}.XXXXXX")"
archive="$BACKUP_DIR/state/axiom-atlas-state-${timestamp}.tar.gz"
encrypted="${archive}.age"
trap 'rm -rf "$work" "$archive"' EXIT

mkdir -p "$work/state" "$work/volumes/api" "$work/volumes/web"
cp "$ENV_FILE" "$work/.env"
cp "$COMPOSE_FILE" "$work/compose.prod.yml"
cp "$DEPLOY_DIR/Caddyfile" "$work/Caddyfile"
cp -a "$STATE_DIR/." "$work/state/" 2>/dev/null || true
docker run --rm \
  -v "$work:/host:ro" \
  -v axiom-atlas_api_dataprotection:/api:ro \
  -v axiom-atlas_web_dataprotection:/web:ro \
  alpine:3.21 sh -c '
    set -eu
    backup="$(mktemp -d)"
    cleanup() { rm -rf "$backup"; }
    trap cleanup EXIT
    cp -a /host/. "$backup/"
    cp -a /api/. "$backup/volumes/api/"
    cp -a /web/. "$backup/volumes/web/"
    tar -C "$backup" -czf - .
  ' > "$archive"
sha256sum "$archive" > "${archive}.sha256"
age -r "$BACKUP_AGE_RECIPIENT" -o "$encrypted" "$archive"
sha256sum "$encrypted" > "${encrypted}.sha256"

if [[ "${BACKUP_LOCAL_TEST:-false}" != "true" ]]; then
  rclone copy "$encrypted" "${BACKUP_REMOTE}/state" --immutable
  rclone copy "${encrypted}.sha256" "${BACKUP_REMOTE}/state" --immutable
fi
printf 'Backup do estado crítico criptografado: %s\n' "$(basename "$encrypted")"
