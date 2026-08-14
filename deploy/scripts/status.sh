#!/usr/bin/env bash
set -Eeuo pipefail
SCRIPT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
# shellcheck disable=SC1091
source "$SCRIPT_DIR/lib.sh"

require_production_environment
printf 'Versão atual: %s\n' "$(cat "$STATE_DIR/current-version" 2>/dev/null || printf 'não registrada')"
printf 'Versão anterior: %s\n' "$(cat "$STATE_DIR/previous-version" 2>/dev/null || printf 'não registrada')"
printf 'Último deploy: %s\n\n' "$(cat "$STATE_DIR/last-successful-deploy" 2>/dev/null || printf 'não registrado')"
compose ps
printf '\nDisco:\n'; df -h "$ROOT_DIR"
printf '\nMemória e swap:\n'; free -h
printf '\nÚltimo backup local:\n'; find "$BACKUP_DIR" -type f -name '*.age' -printf '%TY-%Tm-%Td %TT %p\n' 2>/dev/null | sort | tail -2 || true
printf '\nTimer de backup:\n'; systemctl status axiom-atlas-backup.timer --no-pager 2>/dev/null || true
printf '\nLogs recentes:\n'; compose logs --tail=30 web api 2>/dev/null || true
