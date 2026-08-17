#!/usr/bin/env bash
set -Eeuo pipefail
SCRIPT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
# shellcheck disable=SC1091
source "$SCRIPT_DIR/lib.sh"

[[ $# -eq 1 ]] || fail "Uso: $0 <arquivo-backup.age>"
backup="$1"
require_file "$backup"
require_file "${backup}.sha256"
require_command sha256sum
sha256sum --check "${backup}.sha256"

if [[ -n "${BACKUP_AGE_IDENTITY_FILE:-}" ]]; then
  require_file "$BACKUP_AGE_IDENTITY_FILE"
  temporary="$(mktemp)"
  trap 'rm -f "$temporary"' EXIT
  age -d -i "$BACKUP_AGE_IDENTITY_FILE" -o "$temporary" "$backup"
  if [[ "$backup" == *.dump.age ]]; then
    require_command docker
    docker run --rm --network none -i postgres:17.5-bookworm pg_restore --list < "$temporary" >/dev/null
  else
    tar -tzf "$temporary" >/dev/null
  fi
fi
printf 'Checksum do backup validado.\n'
