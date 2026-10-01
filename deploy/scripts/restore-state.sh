#!/usr/bin/env bash
set -Eeuo pipefail
SCRIPT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
# shellcheck disable=SC1091
source "$SCRIPT_DIR/lib.sh"

[[ "${1:-}" == "--confirm" && -n "${2:-}" ]] || fail "AÇÃO DESTRUTIVA. Uso: $0 --confirm <backup-state.tar.gz.age>"
backup="$2"
require_production_environment
require_file "$backup"
require_file "${backup}.sha256"
[[ -n "${BACKUP_AGE_IDENTITY_FILE:-}" ]] || fail "BACKUP_AGE_IDENTITY_FILE é obrigatório para restaurar."
"$SCRIPT_DIR/verify-backup.sh" "$backup"
work="$(mktemp -d "$BACKUP_DIR/.restore-state.XXXXXX")"
trap 'rm -rf "$work"' EXIT
age -d -i "$BACKUP_AGE_IDENTITY_FILE" -o "$work/state.tar.gz" "$backup"
mkdir -p "$work/extracted"
tar -xzf "$work/state.tar.gz" -C "$work/extracted"
"$SCRIPT_DIR/backup-state.sh"
compose stop web api
rm -rf "$STATE_DIR"
mkdir -p "$STATE_DIR"
cp -a "$work/extracted/state/." "$STATE_DIR/"
docker run --rm -v axiom-atlas_api_dataprotection:/target -v "$work/extracted/volumes/api:/source:ro" alpine:3.21 sh -c 'rm -rf /target/* && cp -a /source/. /target/'
docker run --rm -v axiom-atlas_web_dataprotection:/target -v "$work/extracted/volumes/web:/source:ro" alpine:3.21 sh -c 'rm -rf /target/* && cp -a /source/. /target/'
compose up -d api web caddy
"$SCRIPT_DIR/healthcheck.sh"
printf 'Estado crítico restaurado e validado.\n'
