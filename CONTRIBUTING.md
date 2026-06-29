# Contributing

Thank you for considering a contribution to Autonomous Build Template.

This repository is a project-agnostic template for autonomous, ticket-driven builds. Contributions should keep the template reusable across stacks and avoid assumptions about any one language, framework, cloud provider, or agent vendor.

## Ways to contribute

* Improve documentation, examples, or setup guidance.
* Tighten safety checks and validation scripts.
* Improve the build-loop workflow without reducing guardrails.
* Fix bugs in shell scripts or template instructions.
* Suggest small, generally useful quality-of-life improvements.

## Before you start

For larger changes, open an issue first so maintainers can confirm the scope fits the template.

Keep contributions focused. Avoid bundling unrelated documentation, script, and workflow changes into one pull request unless they are part of the same fix.

## Local validation

Run the quality gate before opening a pull request:

```bash
scripts/quality-gate.sh
```

The gate performs shell syntax checks and repository safety scans. If you add stack-specific files, make sure the generic gate still behaves safely for users who create new projects from this template.

## Template guidelines

When changing the template:

* Keep files project-agnostic.
* Do not add real secrets, credentials, private URLs, internal hostnames, or private data.
* Do not add destructive automation.
* Preserve dirty-tree, upstream, and completion-status safety checks.
* Document new behaviour in `README.md` or `docs/` when appropriate.
* Prefer clear limitations over claims that the template is production-ready for every use case.

## Pull request checklist

Before submitting, confirm that:

* [ ] `scripts/quality-gate.sh` passes.
* [ ] Documentation is updated for user-facing changes.
* [ ] No generated, private, or machine-specific files are included.
* [ ] The change remains useful for multiple project types.
* [ ] Commits use a clear, conventional style such as `docs:`, `fix:`, `chore:`, or `ci:`.

## Reporting problems

For bugs or confusing documentation, open a GitHub issue with:

* what you expected to happen
* what happened instead
* relevant command output
* the operating system and shell, if script behaviour is involved
