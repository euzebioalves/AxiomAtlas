#!/usr/bin/env bash
set -Eeuo pipefail

release_version="${1:-}"
release_dir="${2:-}"
[[ "$release_version" =~ ^[0-9]+\.[0-9]+\.[0-9]+(-[0-9A-Za-z.-]+)?$ && -d "$release_dir" ]] || {
  printf 'Uso: %s <X.Y.Z> <diretório-da-release>\n' "$0" >&2
  exit 1
}

assets=(
  "AxiomAtlas-API-${release_version}.zip"
  "AxiomAtlas-Web-${release_version}.zip"
  "AxiomAtlas-Migrator-${release_version}.zip"
  "AxiomAtlas-Deployment-${release_version}.tar.gz"
  "AxiomAtlas-${release_version}-SHA256SUMS.txt"
)
for asset in "${assets[@]}"; do
  [[ -s "$release_dir/$asset" ]] || {
    printf 'Asset ausente ou vazio: %s\n' "$asset" >&2
    exit 1
  }
done
(cd "$release_dir" && sha256sum --check "AxiomAtlas-${release_version}-SHA256SUMS.txt")
for component in API Web Migrator; do
  unzip -tq "$release_dir/AxiomAtlas-${component}-${release_version}.zip" >/dev/null
done
tar -tzf "$release_dir/AxiomAtlas-Deployment-${release_version}.tar.gz" >/dev/null
