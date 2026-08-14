#!/usr/bin/env bash
set -Eeuo pipefail
SCRIPT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
# shellcheck disable=SC1091
source "$SCRIPT_DIR/lib.sh"

require_production_environment
require_command age
require_command sha256sum
require_command pg_restore
if [[ "${BACKUP_LOCAL_TEST:-false}" != "true" ]]; then require_command rclone; fi
mkdir -p "$BACKUP_DIR/postgres"
chmod 700 "$BACKUP_DIR"

timestamp="$(date -u +%Y%m%dT%H%M%SZ)"
temporary="$(mktemp "$BACKUP_DIR/postgres/.${timestamp}.XXXXXX.dump")"
plain="$BACKUP_DIR/postgres/axiom-atlas-postgres-${timestamp}.dump"
encrypted="${plain}.age"
cleanup() { rm -f "$temporary" "$plain"; }
trap cleanup EXIT

compose exec -T postgres pg_dump --format=custom --no-owner --no-acl -U "$POSTGRES_USER" "$POSTGRES_DB" > "$temporary"
pg_restore --list "$temporary" >/dev/null
mv "$temporary" "$plain"
sha256sum "$plain" > "${plain}.sha256"
age -r "$BACKUP_AGE_RECIPIENT" -o "$encrypted" "$plain"
sha256sum "$encrypted" > "${encrypted}.sha256"

if [[ "${BACKUP_LOCAL_TEST:-false}" != "true" ]]; then
  rclone copy "$encrypted" "${BACKUP_REMOTE}/postgres" --immutable
  rclone copy "${encrypted}.sha256" "${BACKUP_REMOTE}/postgres" --immutable
fi

printf 'Backup PostgreSQL validado e criptografado: %s\n' "$(basename "$encrypted")"
