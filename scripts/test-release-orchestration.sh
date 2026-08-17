#!/usr/bin/env bash
set -Eeuo pipefail

SCRIPT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
ORCHESTRATOR="$SCRIPT_DIR/orchestrate-release.sh"
work="$(mktemp -d)"
trap 'rm -rf "$work"' EXIT

fake_bin="$work/bin"
mkdir -p "$fake_bin"

cat > "$fake_bin/git" <<'EOF'
#!/usr/bin/env bash
set -Eeuo pipefail
[[ "$1" == rev-list ]] || { printf 'git simulado recebeu comando inesperado: %s\n' "$*" >&2; exit 1; }
printf '%s\n' "${FAKE_TAG_REVISION:?}"
EOF

cat > "$fake_bin/gh" <<'EOF'
#!/usr/bin/env bash
set -Eeuo pipefail

state="${FAKE_STATE_DIR:?}"
scenario="${FAKE_SCENARIO:?}"
version="${FAKE_VERSION:?}"
mkdir -p "$state"

view_count() {
  local count=0
  [[ -f "$state/views" ]] && count="$(<"$state/views")"
  count=$((count + 1))
  printf '%s\n' "$count" > "$state/views"
  printf '%s' "$count"
}

complete_release() {
  cat <<JSON
{"isDraft":false,"assets":[
  {"name":"AxiomAtlas-API-${version}.zip","size":1},
  {"name":"AxiomAtlas-Web-${version}.zip","size":1},
  {"name":"AxiomAtlas-Migrator-${version}.zip","size":1},
  {"name":"AxiomAtlas-Deployment-${version}.tar.gz","size":1},
  {"name":"AxiomAtlas-${version}-SHA256SUMS.txt","size":1}
]}
JSON
}

if [[ "$1 $2" == 'release view' ]]; then
  count="$(view_count)"
  case "$scenario" in
    missing)
      (( count == 1 )) && exit 1
      printf '%s\n' '{"isDraft":true,"assets":[]}'
      ;;
    draft)
      printf '%s\n' '{"isDraft":true,"assets":[]}'
      ;;
    complete)
      complete_release
      ;;
    published-incomplete)
      printf '%s\n' '{"isDraft":false,"assets":[]}'
      ;;
    temporary-read-failure)
      (( count < 3 )) && exit 1
      printf '%s\n' '{"isDraft":true,"assets":[]}'
      ;;
    *)
      printf 'Cenário simulado inválido: %s\n' "$scenario" >&2
      exit 1
      ;;
  esac
elif [[ "$1 $2" == 'release create' ]]; then
  printf '%s\n' "$*" >> "$state/creates"
else
  printf 'gh simulado recebeu comando inesperado: %s\n' "$*" >&2
  exit 1
fi
EOF

chmod +x "$fake_bin/git" "$fake_bin/gh"

run_case() {
  local scenario="$1"
  local expected_mode="$2"
  local case_dir="$work/$scenario"
  mkdir -p "$case_dir"
  PATH="$fake_bin:$PATH" \
    FAKE_STATE_DIR="$case_dir" \
    FAKE_SCENARIO="$scenario" \
    FAKE_VERSION='1.2.3' \
    FAKE_TAG_REVISION='expected-sha' \
    RELEASE_VIEW_RETRY_DELAY_SECONDS=0 \
    "$ORCHESTRATOR" \
      --tag v1.2.3 \
      --version 1.2.3 \
      --revision expected-sha \
      --repository owner/repository \
      --output "$case_dir/output"
  grep -Fx "mode=$expected_mode" "$case_dir/output" >/dev/null
  printf '%s\n' "$case_dir"
}

missing_dir="$(run_case missing draft)"
test -s "$missing_dir/creates"

draft_dir="$(run_case draft draft)"
test ! -e "$draft_dir/creates"

complete_dir="$(run_case complete complete)"
test ! -e "$complete_dir/creates"

temporary_dir="$(run_case temporary-read-failure draft)"
test -s "$temporary_dir/creates"
test "$(<"$temporary_dir/views")" -eq 3

incomplete_dir="$work/published-incomplete"
mkdir -p "$incomplete_dir"
if PATH="$fake_bin:$PATH" FAKE_STATE_DIR="$incomplete_dir" FAKE_SCENARIO='published-incomplete' FAKE_VERSION='1.2.3' FAKE_TAG_REVISION='expected-sha' "$ORCHESTRATOR" --tag v1.2.3 --version 1.2.3 --revision expected-sha --repository owner/repository --output "$incomplete_dir/output" >"$incomplete_dir/stdout" 2>"$incomplete_dir/stderr"; then
  printf 'A release publicada incompleta deveria falhar.\n' >&2
  exit 1
fi
grep -F 'não contém todos os assets esperados' "$incomplete_dir/stderr" >/dev/null

conflict_dir="$work/tag-conflict"
mkdir -p "$conflict_dir"
if PATH="$fake_bin:$PATH" FAKE_STATE_DIR="$conflict_dir" FAKE_SCENARIO='draft' FAKE_VERSION='1.2.3' FAKE_TAG_REVISION='other-sha' "$ORCHESTRATOR" --tag v1.2.3 --version 1.2.3 --revision expected-sha --repository owner/repository --output "$conflict_dir/output" >"$conflict_dir/stdout" 2>"$conflict_dir/stderr"; then
  printf 'O conflito de tag deveria falhar.\n' >&2
  exit 1
fi
grep -F 'não para expected-sha' "$conflict_dir/stderr" >/dev/null

printf 'Os testes de orquestração de release passaram.\n'
