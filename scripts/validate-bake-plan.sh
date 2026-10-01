#!/usr/bin/env bash
set -Eeuo pipefail

ROOT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")/.." && pwd)"
release_version="${1:-}"
revision="${2:-}"
[[ "$release_version" =~ ^[0-9]+\.[0-9]+\.[0-9]+(-[0-9A-Za-z.-]+)?$ ]] || exit 1
[[ "$revision" =~ ^[0-9a-f]{7,64}$ ]] || exit 1

plan="$(mktemp)"
trap 'rm -f "$plan"' EXIT
VERSION="$release_version" REVISION="$revision" \
  docker buildx bake --file "$ROOT_DIR/docker-bake.hcl" --print api web migrator > "$plan"
if grep -Eq '(^|[^[:alnum:]_-])latest([^[:alnum:]_-]|$)' "$plan"; then
  printf 'O plano Docker Bake não pode publicar a tag latest.\n' >&2
  exit 1
fi
for component in api web migrator; do
  grep -Fq "ghcr.io/euzebioalves/axiomatlas-${component}:${release_version}" "$plan"
  grep -Fq "ghcr.io/euzebioalves/axiomatlas-${component}:sha-${revision}" "$plan"
done
