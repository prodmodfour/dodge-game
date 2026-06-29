#!/usr/bin/env bash
set -euo pipefail

SCRIPT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
REPO_ROOT="$(cd "$SCRIPT_DIR/.." && pwd)"
# shellcheck source=scripts/lib/pretty-print.sh
source "$SCRIPT_DIR/lib/pretty-print.sh"

fail() {
  pp_error "$*"
  exit 1
}

tmp_dir="$(mktemp -d)"
cleanup() {
  rm -rf "$tmp_dir"
}
trap cleanup EXIT

work_dir="$tmp_dir/work"
state_dir="$tmp_dir/state"

pp_step "Regression: build-loop state survives .agent cleanup"

git init -q "$work_dir"

(
  cd "$work_dir"

  git config user.name "Build Loop Test"
  git config user.email "build-loop-test@example.invalid"

  mkdir -p scripts/lib
  cp "$REPO_ROOT/scripts/build-loop.sh" scripts/build-loop.sh
  cp "$REPO_ROOT/scripts/lib/pretty-print.sh" scripts/lib/pretty-print.sh
  cp "$REPO_ROOT/scripts/lib/git-branch.sh" scripts/lib/git-branch.sh
  cp "$REPO_ROOT/scripts/lib/pull-request.sh" scripts/lib/pull-request.sh

  cat > scripts/quality-gate.sh <<'QUALITY_GATE'
#!/usr/bin/env bash
set -euo pipefail
exit 0
QUALITY_GATE

  cat > scripts/run-agent.sh <<'RUN_AGENT'
#!/usr/bin/env bash
set -euo pipefail

: "${AUTONOMOUS_BUILD_LOOP_STATE_DIR:?AUTONOMOUS_BUILD_LOOP_STATE_DIR must be set by the test}"

commit_count="$(git rev-list --count HEAD)"
echo "Stub agent cycle from commit count ${commit_count}."
printf '\nStub cycle %s\n' "$commit_count" >> WORK_LOG.md
git add WORK_LOG.md
git commit -q -m "test: stub cycle ${commit_count}"

# Simulate repo guardrails removing private/runtime state after an agent cycle.
rm -rf .agent

# Simulate the active log directory disappearing between cycles. The build loop
# must recreate it before the next tee invocation.
if [[ "$commit_count" == "1" ]]; then
  rm -rf "$AUTONOMOUS_BUILD_LOOP_STATE_DIR/logs"
fi
RUN_AGENT

  chmod +x scripts/build-loop.sh scripts/quality-gate.sh scripts/run-agent.sh

  cat > AGENTS.md <<'AGENTS'
# AGENTS.md

Test fixture.
AGENTS

  cat > PROJECT_BRIEF.md <<'PROJECT_BRIEF'
# PROJECT_BRIEF.md

TEMPLATE_CUSTOMISED: true
PROJECT_BRIEF

  cat > BUILD_TICKETS.md <<'BUILD_TICKETS'
# BUILD_TICKETS.md

AUTOMATION_STATUS: NOT_DONE

## 000 — Test ticket

Status: TODO
BUILD_TICKETS

  git add .
  git commit -q -m "test: initial fixture"

  AUTONOMOUS_BUILD_LOOP_STATE_DIR="$state_dir" \
    bash scripts/build-loop.sh --max-cycles 2 --no-push

  if [[ -e .agent/logs/build-loop || -e .agent/build-loop.lock ]]; then
    fail "build-loop wrote active log/lock state inside .agent"
  fi
)

if [[ -d "$state_dir/lock" ]]; then
  fail "build-loop lock directory was not cleaned up: $state_dir/lock"
fi

if [[ ! -d "$state_dir/logs" ]]; then
  fail "build-loop did not recreate the external log directory"
fi

log_count="$(find "$state_dir/logs" -type f -name 'cycle-*.log' | wc -l | tr -d ' ')"
if [[ "$log_count" -lt 1 ]]; then
  fail "expected at least one external build-loop log in $state_dir/logs"
fi

pp_success "Build-loop state regression passed."
