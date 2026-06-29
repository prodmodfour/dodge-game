# Security Policy

## Supported versions

This repository is a template. Security fixes are made against the latest `main` branch.

Projects created from this template should define their own security policy, supported versions, and disclosure process after they are customised.

## Reporting a vulnerability

Please do not disclose suspected vulnerabilities publicly until they have been reviewed.

Preferred reporting path:

1. Use GitHub's private vulnerability reporting or Security Advisory flow for this repository, if available.
2. If private reporting is not available, open a GitHub issue with a minimal description and ask for a private maintainer contact. Do not include exploit details, secrets, credentials, or private data in a public issue.

When reporting, include:

* the affected file, script, or workflow
* the impact you believe the issue may have
* safe reproduction steps, if possible
* any suggested mitigation

## Scope

Security-relevant areas include:

* build-loop safety checks
* secret and generated-file guardrails
* repository automation
* documentation that could encourage unsafe use
* examples that might expose credentials or private data

Out of scope:

* vulnerabilities introduced only after a downstream project heavily customises the template
* issues that require committing real secrets or private infrastructure details to reproduce
* social-engineering reports without a concrete template issue

## Maintainer expectations

Maintainers will review reports on a best-effort basis, avoid public disclosure before a fix or mitigation is available, and credit reporters when appropriate.
