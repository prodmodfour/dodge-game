#!/usr/bin/env bash
set -euo pipefail

SCRIPT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
# shellcheck source=scripts/lib/pretty-print.sh
source "$SCRIPT_DIR/lib/pretty-print.sh"

pp_step "Checking for generated or private files that should not be committed."

fail=0

while IFS= read -r file; do
  [[ -f "$file" ]] || continue

  case "$file" in
    [Ll]ibrary/*|[Tt]emp/*|[Oo]bj/*|[Bb]uild/*|[Bb]uilds/*|[Ll]ogs/*|[Uu]ser[Ss]ettings/*|[Mm]emoryCaptures/*|[Rr]ecordings/*|ExportedObj/*)
      pp_error "Do not commit Unity generated/runtime output folders."
      pp_kv "File" "$file" >&2
      fail=1
      ;;
    *.csproj|*.sln|*.suo|*.user|*.userprefs|*.pidb|*.booproj|*.opendb|*.VC.db)
      pp_error "Do not commit Unity generated IDE/project files."
      pp_kv "File" "$file" >&2
      fail=1
      ;;
    *.tfstate|*.tfstate.*|*.tfplan)
      pp_error "Do not commit Terraform state or plan files."
      pp_kv "File" "$file" >&2
      fail=1
      ;;
    *.tfvars)
      case "$file" in
        *.tfvars.example)
          ;;
        *)
          pp_error "Do not commit real Terraform variable files."
          pp_kv "File" "$file" >&2
          fail=1
          ;;
      esac
      ;;
    *.env|*.env.*)
      case "$file" in
        .env.example|*.env.example)
          ;;
        *)
          pp_error "Do not commit real environment files."
          pp_kv "File" "$file" >&2
          fail=1
          ;;
      esac
      ;;
    *.pem|*.key|*.p12|*.pfx|id_rsa|*/id_rsa|id_ed25519|*/id_ed25519)
      pp_error "Do not commit private key/certificate material."
      pp_kv "File" "$file" >&2
      fail=1
      ;;
  esac
done < <(git ls-files --cached --others --exclude-standard)

if (( fail != 0 )); then
  pp_error "Generated/private file check failed."
  exit 1
fi

pp_success "No generated/private files found."
