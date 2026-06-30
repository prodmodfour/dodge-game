# Reaction Tactics Prototype

Reaction Tactics Prototype is a Unity 3D fantasy tactics prototype focused on deterministic action/reaction timing.

Target pillars:

* discrete `x, y, z` grid over visible stepped 3D terrain
* shared AP per unit, refreshed each round
* actions only on a unit's own turn
* reactions only during another unit's action window
* every action opens a Reaction Turn for every other living unit, ordered by distance from the Action Character
* Option A melee timing: melee attacks are declared, reactions happen, then the hit resolves only if the target remains in melee range
* no dodge chance and no accuracy rolls; avoidance is physical movement on the grid
* ranged cones and AoEs resolve against units still inside their shapes after reactions
* small 2v2 or 3v3 scenario playable in-editor and buildable as a Linux standalone

This repository is configured for an autonomous, ticket-driven build workflow based on [`prodmodfour/autonomous-build-template`](https://github.com/prodmodfour/autonomous-build-template).

## Autobuild files

* `AGENTS.md` — general rules for autonomous coding agents
* `PROJECT_BRIEF.md` — Reaction Tactics project brief and constraints
* `BUILD_TICKETS.md` — build-loop-compatible ticket queue generated from `tickets.md`
* `scripts/build-loop.sh` — autonomous build loop entrypoint
* `scripts/run-agent.sh` — Pi-compatible agent wrapper using `pi-dan-rinse`
* `scripts/quality-gate.sh` — shell/security checks plus optional Unity validation
* `Justfile` — convenience command for running the full autonomous queue

## Important

The autobuild system has been set up, but it has **not** been run.

Before running autonomous cycles, make sure the working tree is clean and review the first ticket in `BUILD_TICKETS.md`.

## Running later

Run one local cycle without pushing:

```bash
scripts/build-loop.sh --max-cycles 1 --no-push
```

Run on a dedicated branch:

```bash
scripts/build-loop.sh --create-branch feature/autonomous-build --max-cycles 1 --no-push
```

Run multiple cycles when ready:

```bash
scripts/build-loop.sh --branch feature/autonomous-build --max-cycles 20
```

Run the configured 180-cycle queue with Just:

```bash
just run
```

By default, successful cycles push commits. Use `--no-push` to keep commits local.

If an agent run fails after making file changes or commits, the loop checkpoints those failed-run changes, pushes the current branch unless `--no-push` is set, and retries the same cycle.

## Prototype in-game help

Press `H` in play mode to show or hide the prototype rules overlay. It summarizes active-turn actions, off-turn reactions, reaction ordering, movement-based avoidance, and the no-dodge-chance combat rule for new playtesters.

## Unity workflow

Autonomous Unity work should follow the project brief, `CLAUDE.md`, and the Unity skill workflow:

1. inspect project/editor state with `unity-ctrl --project "$PWD" status` before changing Unity project files
2. make the smallest safe edit for the selected ticket only
3. refresh, compile, test, or reserialize as appropriate for the changed file type
4. query Unity to verify the change
5. save project/scene state when scene or asset wiring changes
6. run `scripts/quality-gate.sh`
7. update only the selected ticket status
8. commit exactly one completed ticket

Scenario-driven scene bootstrapping can be rebuilt with one command: `unity-ctrl --project "$PWD" rt_setup_prototype_scene`. The setup creates or updates the default map, units, abilities, scenario asset, grid/input/UI wiring, and scenario loader. It leaves `Units/Scenario Units` empty in edit mode; `ScenarioLoader` spawns `DefaultSkirmish.asset` once when play mode starts.

Structural scene, prefab, and asset wiring should use `unity-ctrl exec` or project-specific `[UnityCliTool]` commands. Raw Unity YAML edits are for simple serialized value changes only and must be followed by `unity-ctrl --project "$PWD" reserialize <path>`.

Do not commit generated Unity folders such as `Library/`, `Temp/`, `Obj/`, `Logs/`, `UserSettings/`, or build output. Do not delete `.meta` files.
