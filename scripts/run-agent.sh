#!/usr/bin/env bash
set -euo pipefail

SCRIPT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
# shellcheck source=scripts/lib/pretty-print.sh
source "$SCRIPT_DIR/lib/pretty-print.sh"

if [[ $# -ne 1 ]]; then
  pp_error "Usage: scripts/run-agent.sh '<prompt>'"
  exit 2
fi

PROMPT="$1"

if ! command -v pi-dan-rinse >/dev/null 2>&1; then
  pp_error "Required command not found: pi-dan-rinse"
  pp_hint "Edit scripts/run-agent.sh if this project should use a different agent command."
  exit 127
fi

# Intentionally no model or thinking-level flags.
# This relies on the local pi-dan-rinse configuration.

pp_step "Launching Pi agent via pi-dan-rinse."
pp_cmd "pi-dan-rinse --no-session -p @AGENTS.md @PROJECT_BRIEF.md @BUILD_TICKETS.md '<prompt>'"

pi-dan-rinse --no-session -p @AGENTS.md @PROJECT_BRIEF.md @BUILD_TICKETS.md "$PROMPT"
