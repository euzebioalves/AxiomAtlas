#!/usr/bin/env bash
set -Eeuo pipefail

ROOT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")/.." && pwd)"
release_version=""
revision=""
output_dir=""

usage() {
  printf 'Uso: %s --version X.Y.Z --revision <git-sha> --output <diretório>\n' "$0" >&2
  exit 1
}

while [[ $# -gt 0 ]]; do
  case "$1" in
    --version) release_version="${2:-}"; shift 2 ;;
    --revision) revision="${2:-}"; shift 2 ;;
    --output) output_dir="${2:-}"; shift 2 ;;
    *) usage ;;
  esac
done

[[ "$release_version" =~ ^[0-9]+\.[0-9]+\.[0-9]+(-[0-9A-Za-z.-]+)?$ ]] || usage
[[ "$revision" =~ ^[0-9a-f]{7,64}$ ]] || usage
[[ -n "$output_dir" ]] || usage

output_dir="$(mkdir -p "$output_dir" && cd "$output_dir" && pwd)"
artifacts_dir="$output_dir/artifacts"
release_dir="$output_dir/release"
assembly_version="${release_version%%-*}"
mkdir -p "$artifacts_dir" "$release_dir"

properties=(
  "/p:Version=${release_version}"
  "/p:VersionPrefix=${release_version}"
  "/p:AssemblyVersion=${assembly_version}"
  "/p:FileVersion=${assembly_version}"
  "/p:InformationalVersion=${release_version}+${revision}"
  "/p:SourceRevisionId=${revision}"
  "/p:Revision=${revision}"
  "/p:IncludeSourceRevisionInInformationalVersion=false"
  "/p:ContinuousIntegrationBuild=true"
)

publish_project() {
  local project="$1"
  local name="$2"
  local target="$artifacts_dir/$name"
  dotnet publish "$ROOT_DIR/$project" --configuration Release --no-restore --output "$target" "${properties[@]}"
  (cd "$target" && zip -qr "$release_dir/AxiomAtlas-${name^}-${release_version}.zip" .)
}

publish_project 'Axiom.Atlas.API/Axiom.Atlas.API.csproj' 'api'
publish_project 'Axiom.Atlas.Web/Axiom.Atlas.Web.csproj' 'web'
publish_project 'Axiom.Atlas.Migrator/Axiom.Atlas.Migrator.csproj' 'migrator'
tar --exclude='.env' --exclude='*.local' -C "$ROOT_DIR" -czf \
  "$release_dir/AxiomAtlas-Deployment-${release_version}.tar.gz" deploy docs
(cd "$release_dir" && sha256sum \
  "AxiomAtlas-API-${release_version}.zip" \
  "AxiomAtlas-Web-${release_version}.zip" \
  "AxiomAtlas-Migrator-${release_version}.zip" \
  "AxiomAtlas-Deployment-${release_version}.tar.gz" \
  > "AxiomAtlas-${release_version}-SHA256SUMS.txt")
printf '%s\n' "$release_dir"
