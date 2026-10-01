#!/usr/bin/env bash
set -Eeuo pipefail
SCRIPT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
# shellcheck disable=SC1091
source "$SCRIPT_DIR/lib.sh"

require_production_environment
for endpoint in /health/live /health/ready; do
  curl --fail --silent --show-error --max-time 15 "https://${AXIOM_DOMAIN}${endpoint}" >/dev/null
done
printf 'Health checks públicos concluídos com sucesso.\n'
