#!/usr/bin/env bash
set -uo pipefail

ROOT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")/.." && pwd)"
LOG_DIR="${AIRSENSE_LOG_DIR:-/tmp/airsense-checks}"
TAIL_LINES="${AIRSENSE_TAIL_LINES:-80}"
mkdir -p "$LOG_DIR"

usage() {
  cat <<'EOF'
Usage: scripts/check-quiet.sh [all|api|ai|web|web-build|web-test|helm|diff ...]

Runs common AirSense validation commands with compact output.
Full logs are written to AIRSENSE_LOG_DIR, default: /tmp/airsense-checks.
EOF
}

safe_name() {
  printf '%s' "$1" | tr ' /:' '___'
}

run_step() {
  local label="$1"
  shift
  local log="$LOG_DIR/$(safe_name "$label").log"

  printf '[run]  %s\n' "$label"
  if "$@" >"$log" 2>&1; then
    printf '[ok]   %s (%s)\n' "$label" "$log"
    return 0
  fi

  local status=$?
  printf '[fail] %s (%s)\n' "$label" "$log" >&2
  tail -n "$TAIL_LINES" "$log" >&2 || true
  return "$status"
}

api_tests() {
  if command -v dotnet >/dev/null 2>&1; then
    run_step "api tests" dotnet test "$ROOT_DIR/api/airsense.sln" --nologo --verbosity minimal
    return
  fi

  if command -v docker >/dev/null 2>&1; then
    run_step "api tests" docker run --rm \
      -v "$ROOT_DIR:/src" \
      -w /src \
      mcr.microsoft.com/dotnet/sdk:8.0 \
      dotnet test api/airsense.sln --nologo --verbosity minimal
    return
  fi

  printf '[skip] api tests: dotnet/docker not available\n' >&2
}

ai_tests() {
  if command -v python3 >/dev/null 2>&1 && python3 -c 'import pytest, fastapi, numpy, joblib' >/dev/null 2>&1; then
    run_step "ai prediction tests" bash -lc "cd '$ROOT_DIR/services/ai-prediction-service' && python3 -m pytest -q"
    return
  fi

  if command -v docker >/dev/null 2>&1; then
    run_step "ai prediction tests" docker run --rm \
      -v "$ROOT_DIR:/src" \
      -w /src/services/ai-prediction-service \
      python:3.12-slim \
      sh -c "pip install -q -r requirements-dev.txt && python -m pytest -q"
    return
  fi

  printf '[skip] ai prediction tests: python dependencies/docker not available\n' >&2
}

web_tests() {
  run_step "web tests" npm --silent --prefix "$ROOT_DIR/web" test
}

web_build() {
  run_step "web build" npm --silent --prefix "$ROOT_DIR/web" run build
}

helm_lint() {
  run_step "helm lint" helm lint "$ROOT_DIR/charts/airsense"
}

diff_check() {
  run_step "git diff check" git -C "$ROOT_DIR" diff --check
}

run_target() {
  case "$1" in
    api) api_tests ;;
    ai) ai_tests ;;
    web) web_tests; web_build ;;
    web-test) web_tests ;;
    web-build) web_build ;;
    helm) helm_lint ;;
    diff) diff_check ;;
    all) api_tests; ai_tests; web_tests; web_build; helm_lint; diff_check ;;
    -h|--help) usage ;;
    *)
      printf 'Unknown target: %s\n' "$1" >&2
      usage >&2
      return 2
      ;;
  esac
}

if [[ "$#" -eq 0 ]]; then
  set -- all
fi

for target in "$@"; do
  run_target "$target" || exit "$?"
done
