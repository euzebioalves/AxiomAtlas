#!/usr/bin/env bash
set -Eeuo pipefail
SCRIPT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
# shellcheck disable=SC1091
source "$SCRIPT_DIR/lib.sh"

[[ "${1:-}" == "--confirm" && -n "${2:-}" ]] || fail "AÇÃO DESTRUTIVA. Uso: $0 --confirm <backup.dump.age>"
backup="$2"
require_production_environment
require_file "$backup"
require_file "${backup}.sha256"
[[ -n "${BACKUP_AGE_IDENTITY_FILE:-}" ]] || fail "BACKUP_AGE_IDENTITY_FILE é obrigatório para restaurar."
"$SCRIPT_DIR/verify-backup.sh" "$backup"
temporary="$(mktemp "$BACKUP_DIR/.restore.XXXXXX.dump")"
trap 'rm -f "$temporary"' EXIT
age -d -i "$BACKUP_AGE_IDENTITY_FILE" -o "$temporary" "$backup"
compose exec -T postgres pg_restore --list < "$temporary" >/dev/null
"$SCRIPT_DIR/backup-postgres.sh"
compose stop web api
compose exec -T postgres pg_restore --clean --if-exists --no-owner --no-acl -U "$POSTGRES_USER" -d "$POSTGRES_DB" < "$temporary"
compose up -d api web caddy
"$SCRIPT_DIR/healthcheck.sh"
printf 'Banco restaurado e validado. O backup de origem foi preservado.\n'
