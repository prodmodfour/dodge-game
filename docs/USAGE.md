# Autonomous Build Usage

This repository has been set up with the autonomous build loop from `prodmodfour/autonomous-build-template`.

The setup has **not** been run. Start it only when you are ready for the agent to make ticket-sized commits.

## Files used by the loop

The build loop reads these files every cycle:

* `AGENTS.md`
* `PROJECT_BRIEF.md`
* `BUILD_TICKETS.md`

The implementation agent is launched through:

```bash
scripts/run-agent.sh "$PROMPT"
```

The default wrapper uses `pi-dan-rinse`:

```bash
pi-dan-rinse --no-session -p @AGENTS.md @PROJECT_BRIEF.md @BUILD_TICKETS.md "$PROMPT"
```

## Before running

1. Review `BUILD_TICKETS.md`.
2. Ensure `PROJECT_BRIEF.md` has `TEMPLATE_CUSTOMISED: true`.
3. Commit or discard setup changes so the working tree is clean.
4. Confirm the current branch is not behind its upstream.
5. Decide whether successful cycles should push automatically.

Check status:

```bash
git status --short --branch
```

## Run one cycle without pushing

```bash
scripts/build-loop.sh --max-cycles 1 --no-push
```

## Run on a dedicated branch

Create a branch and run one cycle:

```bash
scripts/build-loop.sh --create-branch feature/autonomous-build --max-cycles 1 --no-push
```

Continue later on the same branch:

```bash
scripts/build-loop.sh --branch feature/autonomous-build --max-cycles 5 --no-push
```

## Run with default pushing

By default, every successful cycle is pushed after the ticket commit:

```bash
scripts/build-loop.sh --branch feature/autonomous-build --max-cycles 20
```

Run the configured 180-cycle queue with Just:

```bash
just run
```

## Failure checkpoints

If an agent run fails after creating commits or leaving uncommitted changes, the loop now preserves that failed-run state before retrying:

1. restore `BUILD_TICKETS.md` to its pre-run state so the same ticket stays `TODO`
2. run lightweight secret/generated-file guardrails
3. commit uncommitted changes as `chore: checkpoint failed autonomous cycle`
4. push the current branch unless `--no-push` is set
5. retry the same build cycle

If the failure produced no commits or file changes, there is nothing to checkpoint and the loop simply retries.

## PR/MR automation

After authenticating `gh` or `glab`, create or merge a PR/MR each cycle:

```bash
scripts/build-loop.sh --branch feature/autonomous-build --pr-each-cycle --pr-base main --max-cycles 20
```

```bash
scripts/build-loop.sh --branch feature/autonomous-build --merge-pr-each-cycle --pr-base main --max-cycles 20
```

## Quality gate

Each agent cycle is instructed to run:

```bash
bash scripts/quality-gate.sh
```

Unity validation is controlled with `UNITY_QUALITY_MODE`:

* `auto` (default): run Unity checks when available; skip unavailable editor tooling with warnings
* `strict`: fail if Unity tooling/status is unavailable
* `skip`: skip Unity checks, useful for generic CI runners

Example:

```bash
UNITY_QUALITY_MODE=auto bash scripts/quality-gate.sh
```

Run build-loop regression tests only when intentionally validating the copied template scripts:

```bash
RUN_BUILD_LOOP_TESTS=1 bash scripts/quality-gate.sh
```

## State and logs

Build-loop logs and lock files are stored outside the repository by default:

```text
${XDG_STATE_HOME:-$HOME/.local/state}/autonomous-build-template/build-loop/<repo-key>/
```

Override per run if needed:

```bash
AUTONOMOUS_BUILD_LOOP_STATE_DIR=/path/to/state scripts/build-loop.sh --max-cycles 1 --no-push
```
