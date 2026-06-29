# Customising This Template

## Files you usually edit first

```text
PROJECT_BRIEF.md
BUILD_TICKETS.md
README.md
```

## Files you usually keep

```text
AGENTS.md
scripts/build-loop.sh
scripts/create-remote-repo.sh
scripts/run-agent.sh
scripts/quality-gate.sh
scripts/lib/pretty-print.sh
scripts/lib/git-branch.sh
scripts/lib/pull-request.sh
```

## Common customisations

### Python backend

Update `PROJECT_BRIEF.md` with:

* Python version
* package manager
* framework
* database
* testing stack
* architecture boundaries

Add tickets for:

* package skeleton
* settings/logging
* database/migrations
* routes/services/repositories
* tests
* Docker
* CI
* docs

### Frontend app

Update `PROJECT_BRIEF.md` with:

* framework
* package manager
* testing strategy
* build command
* UI boundaries

Add tickets for:

* app skeleton
* layout/routes
* state management
* components
* tests
* CI
* README/screenshots

### Infrastructure repo

Update `PROJECT_BRIEF.md` with:

* cloud provider
* IaC tool
* environments
* validation-only CI
* no mutation rules

Add tickets for:

* module structure
* environments
* validation scripts
* CI
* architecture docs
* deployment docs
* rollback docs
* cost notes
* security notes

## Changing the agent

Edit:

```text
scripts/run-agent.sh
```

Do not hard-code model or thinking flags into `scripts/build-loop.sh`.
