#!/usr/bin/env bash
set -Eeuo pipefail

fail() { printf 'ERROR: %s\n' "$*" >&2; exit 1; }
json_is_draft() {
  node -e '
    const release = JSON.parse(require("fs").readFileSync(0, "utf8"));
    if (typeof release.isDraft !== "boolean") process.exit(2);
    console.log(release.isDraft);
  '
}

tag=""
version=""
revision=""
repository=""
output=""

while [[ $# -gt 0 ]]; do
  case "$1" in
    --tag) tag="${2:-}"; shift 2 ;;
    --version) version="${2:-}"; shift 2 ;;
    --revision) revision="${2:-}"; shift 2 ;;
    --repository) repository="${2:-}"; shift 2 ;;
    --output) output="${2:-}"; shift 2 ;;
    *) fail "Argumento inválido: $1" ;;
  esac
done

[[ "$tag" =~ ^v[0-9]+\.[0-9]+\.[0-9]+$ ]] || fail 'A tag deve usar o formato vMAJOR.MINOR.PATCH.'
[[ "$version" == "${tag#v}" ]] || fail 'A versão deve corresponder à tag.'
[[ -n "$revision" ]] || fail 'A revisão esperada é obrigatória.'
[[ -n "$repository" ]] || fail 'O repositório é obrigatório.'
[[ -n "$output" ]] || fail 'O arquivo de saída do GitHub Actions é obrigatório.'

tag_revision="$(git rev-list -n 1 "$tag" 2>/dev/null || true)"
[[ "$tag_revision" == "$revision" ]] || fail "A tag $tag aponta para ${tag_revision:-nenhum commit}, não para $revision."

release_view() {
  gh release view "$tag" --repo "$repository" --json isDraft,assets 2>/dev/null
}

release_has_expected_assets() {
  local release_json="$1"
  # shellcheck disable=SC2016 # Template literals devem permanecer literais para o processo Node.js.
  node -e '
    const version = process.argv[1];
    const release = JSON.parse(require("fs").readFileSync(0, "utf8"));
    const expected = [
      `AxiomAtlas-API-${version}.zip`,
      `AxiomAtlas-Web-${version}.zip`,
      `AxiomAtlas-Migrator-${version}.zip`,
      `AxiomAtlas-Deployment-${version}.tar.gz`,
      `AxiomAtlas-${version}-SHA256SUMS.txt`
    ];
    const assets = Array.isArray(release.assets) ? release.assets : [];
    process.exit(expected.every(name => assets.some(asset => asset.name === name && Number(asset.size) > 0)) ? 0 : 1);
  ' "$version" <<< "$release_json"
}

write_mode() {
  printf 'mode=%s\n' "$1" >> "$output"
}

if release_json="$(release_view)"; then
  is_draft="$(json_is_draft <<< "$release_json")"
  case "$is_draft" in
    true)
      write_mode draft
      exit 0
      ;;
    false)
      if release_has_expected_assets "$release_json"; then
        write_mode complete
        exit 0
      fi
      fail "A release publicada $tag não contém todos os assets esperados. Ela não será alterada automaticamente."
      ;;
    *)
      fail "A consulta da release $tag retornou isDraft inválido: $is_draft."
      ;;
  esac
fi

gh release create "$tag" \
  --repo "$repository" \
  --target "$revision" \
  --draft \
  --title "Axiom Atlas $version" \
  --notes "Release gerada a partir do commit $revision." >/dev/null

attempts="${RELEASE_VIEW_ATTEMPTS:-10}"
delay="${RELEASE_VIEW_RETRY_DELAY_SECONDS:-2}"
[[ "$attempts" =~ ^[1-9][0-9]*$ ]] || fail 'RELEASE_VIEW_ATTEMPTS deve ser um inteiro positivo.'
[[ "$delay" =~ ^[0-9]+$ ]] || fail 'RELEASE_VIEW_RETRY_DELAY_SECONDS deve ser um inteiro não negativo.'

for ((attempt = 1; attempt <= attempts; attempt++)); do
  if release_json="$(release_view)"; then
    is_draft="$(json_is_draft <<< "$release_json")"
    [[ "$is_draft" == true ]] || fail "A release recém-criada $tag não está em rascunho."
    write_mode draft
    exit 0
  fi
  if (( attempt < attempts )); then
    sleep "$delay"
  fi
done

fail "A release em rascunho $tag não ficou disponível após $attempts tentativa(s)."
