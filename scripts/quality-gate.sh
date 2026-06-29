#!/usr/bin/env bash
set -euo pipefail

SCRIPT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
# shellcheck source=scripts/lib/pretty-print.sh
source "$SCRIPT_DIR/lib/pretty-print.sh"

warn() {
  pp_warn "$*"
}

have() {
  command -v "$1" >/dev/null 2>&1
}

run_cmd() {
  pp_cmd "$*"
  "$@"
}

pp_banner "Quality gate"

pp_section "Shell syntax checks"
while IFS= read -r -d '' script; do
  pp_step "bash -n $script"
  bash -n "$script"
done < <(find scripts -type f -name '*.sh' -print0 | sort -z)
pp_success "Shell syntax checks passed."

if [[ "${RUN_BUILD_LOOP_TESTS:-0}" == "1" ]]; then
  mapfile -d '' script_regression_tests < <(
    find scripts -maxdepth 1 -type f -name 'test-build-loop-*.sh' -print0 | sort -z
  )

  if (( ${#script_regression_tests[@]} > 0 )); then
    pp_section "Build-loop regression tests"
    for test_script in "${script_regression_tests[@]}"; do
      run_cmd bash "$test_script"
    done
  fi
else
  pp_info "Skipping build-loop regression tests. Set RUN_BUILD_LOOP_TESTS=1 to enable them."
fi

if [[ -f scripts/check-no-secrets.sh ]]; then
  pp_section "Secret guardrail"
  run_cmd bash scripts/check-no-secrets.sh
fi

if [[ -f scripts/check-no-generated-private-files.sh ]]; then
  pp_section "Generated/private-file guardrail"
  run_cmd bash scripts/check-no-generated-private-files.sh
fi

if [[ -f Makefile ]] && grep -Eq '^[[:space:]]*quality:' Makefile; then
  pp_section "Make quality"
  run_cmd make quality
fi

if [[ -f package.json ]]; then
  pp_section "Node project"

  if have npm; then
    if [[ -f package-lock.json ]]; then
      run_cmd npm ci
    else
      run_cmd npm install
    fi

    run_cmd npm run lint --if-present
    run_cmd npm run typecheck --if-present
    run_cmd npm test --if-present
    run_cmd npm run build --if-present
  else
    warn "npm not installed; skipping Node checks"
  fi
fi

if [[ -f pyproject.toml ]]; then
  pp_section "Python project"

  if have uv; then
    if [[ -f uv.lock ]]; then
      run_cmd uv sync --locked --all-groups
    else
      run_cmd uv sync --all-groups
    fi

    if grep -Eq 'ruff' pyproject.toml; then
      run_cmd uv run ruff check .
      run_cmd uv run ruff format --check .
    else
      pp_info "ruff not configured; skipping ruff checks."
    fi

    if grep -Eq 'mypy' pyproject.toml; then
      run_cmd uv run mypy .
    else
      pp_info "mypy not configured; skipping type checks."
    fi

    if [[ -d tests ]] && grep -Eq 'pytest' pyproject.toml; then
      run_cmd uv run pytest
    else
      pp_info "pytest tests not detected; skipping pytest."
    fi
  elif have python; then
    warn "uv not installed; running minimal Python syntax checks only"
    run_cmd python -m compileall -q .
  else
    warn "Python tooling not installed; skipping Python checks"
  fi
fi

UNITY_QUALITY_MODE="${UNITY_QUALITY_MODE:-auto}"
case "$UNITY_QUALITY_MODE" in
  auto|strict|skip) ;;
  *)
    pp_error "UNITY_QUALITY_MODE must be auto, strict, or skip."
    exit 2
    ;;
esac

PROJECT_DIR="${PROJECT:-$PWD}"
if [[ "$UNITY_QUALITY_MODE" == "skip" ]]; then
  pp_info "Skipping Unity validation because UNITY_QUALITY_MODE=skip."
elif [[ -d "$PROJECT_DIR/Assets" || -f "$PROJECT_DIR/Packages/manifest.json" || -d "$PROJECT_DIR/ProjectSettings" ]]; then
  pp_section "Unity project validation"

  if ! have unity-ctrl; then
    if [[ "$UNITY_QUALITY_MODE" == "strict" ]]; then
      pp_error "unity-ctrl not found."
      exit 127
    fi
    warn "unity-ctrl not found; skipping Unity validation in auto mode."
  else
    status_ok=0
    pp_cmd "unity-ctrl --project $PROJECT_DIR status"
    if unity-ctrl --project "$PROJECT_DIR" status; then
      status_ok=1
    elif [[ "$UNITY_QUALITY_MODE" == "strict" ]]; then
      pp_error "unity-ctrl status failed."
      exit 1
    else
      warn "unity-ctrl status failed; continuing because UNITY_QUALITY_MODE=$UNITY_QUALITY_MODE."
    fi

    if [[ -f "$PROJECT_DIR/Packages/manifest.json" ]] && grep -q 'unity-cli-connector' "$PROJECT_DIR/Packages/manifest.json"; then
      if (( status_ok == 1 )); then
        run_cmd unity-ctrl --project "$PROJECT_DIR" editor refresh --compile
        run_cmd unity-ctrl --project "$PROJECT_DIR" console --type error --stacktrace full --lines 80
      else
        warn "Skipping Unity compile because editor status is not available."
      fi
    else
      pp_info "Unity Connector is not present yet; skipping compile validation until the connector ticket is complete."
    fi
  fi
else
  pp_info "No Unity project folders detected yet; skipping Unity validation."
fi

pp_section "Summary"
pp_success "Quality gate passed."
