# Reaction Tactics prototype complete

Date: 2026-06-30  
Unity version: `6000.4.1f1 (8535861f39e1)`  
Primary scene: `Assets/Scenes/MainPrototype.unity`  
Default scenario: `Assets/ScriptableObjects/Scenarios/DefaultSkirmish.asset`  
Linux build output: `Build/ReactionTacticsPrototype`

This document records the prototype-complete handoff state for the Reaction Tactics milestone. The project is a playable Unity 3D tactics prototype focused on deterministic action/reaction timing rather than dodge chance or accuracy rolls.

## Implemented feature summary

- A Unity project with checked-in `Assets/`, `Packages/`, and `ProjectSettings/` is available for terminal-driven development.
- `MainPrototype` loads a visible stepped 3D grid from data-driven map/scenario assets.
- Units have deterministic identity, team, HP, AP, grid position, occupancy, movement, and death behavior.
- AP is shared between active-turn actions and off-turn reactions, then refreshed at the start of each round.
- Active turns enforce that only the current active unit can move or declare active actions.
- Every declared attack action that triggers reactions opens a reaction window for every other living unit, ordered by tactical distance from the acting unit's declaration position.
- Reactions are limited to reaction movement, Brace, and Pass Reaction; reactions do not open nested full reaction windows.
- Option A melee timing is implemented: melee must be declared in range, reactions happen, then the hit lands only if the target remains in melee range at resolution.
- Cone Shot and Fireball resolve against units' final positions after reactions; moving out of the cone or AoE is the avoidance mechanic.
- Brace spends AP and reduces the next incoming deterministic damage when the unit is still hit.
- Enemy AI can take deterministic active turns and reactions so a solo player can complete a skirmish.
- HUD, action/reaction menus, HP/AP nameplates, tile highlights, combat log, victory/defeat UI, and an `H` help overlay explain the prototype loop in play mode.
- Editor CLI tools can rebuild, inspect, smoke-test, and build the prototype scene from the terminal.
- EditMode, PlayMode, deterministic combat simulation, scene validation, and Linux build tickets have been completed in earlier milestone commits.

## Key handoff references

- [Prototype combat design](prototype-design.md) — implemented rules, AP values, timing, targeting, line-of-sight, and AI behavior.
- [Playtest checklist](playtest-checklist.md) — manual verification steps for movement, reactions, melee timing, cones, AoEs, Brace, determinism, AI, and combat end states.
- [Known limitations](known-limitations.md) — intentional prototype boundaries and future expansion areas.
- [Prototype review screenshots](screenshots/README.md) — visual review images for the current prototype state.
- [Performance notes](performance-notes.md) — initial editor Play Mode profiling observations.
- [Autonomous build usage](USAGE.md) and [repository README](../README.md) — build-loop and Unity workflow entry points.

## Build instructions

From the repository root, ensure the Unity editor/CLI environment is available, then run:

```bash
unity-ctrl --project "$PWD" status
unity-ctrl --project "$PWD" rt_setup_prototype_scene
unity-ctrl --project "$PWD" menu "File/Save"
unity-ctrl --project "$PWD" menu "File/Save Project"
unity-batch "$PWD" -executeMethod ReactionTactics.Editor.BuildPrototype.PerformBuild -quit
```

The build script targets `StandaloneLinux64` and writes the player under `Build/ReactionTacticsPrototype`. Build artifacts are generated output and should not be committed.

## Validation commands used for handoff

The completion handoff records this validation command set:

```bash
unity-ctrl --project "$PWD" status
bash scripts/quality-gate.sh
unity-ctrl --project "$PWD" editor refresh --compile
unity-ctrl --project "$PWD" test
unity-batch "$PWD" -runTests -testPlatform PlayMode -testResults /tmp/reaction-tactics-playmode-results.xml
unity-batch "$PWD" -executeMethod ReactionTactics.Editor.BuildPrototype.PerformBuild -quit
```

The quality gate performs shell syntax checks, secret/generated-file guardrails, and Unity compile/console validation when `unity-ctrl` is available. The final handoff validation passed with the EditMode suite, PlayMode smoke test, and Linux build command above; rerun this set before release-style handoff or larger follow-up work.

## Continuing from this milestone

Future work should be added as new tickets. Preserve the prototype identity unless a new design decision explicitly changes it: tactical avoidance comes from visible grid movement and explicit defensive reactions, not random dodge, accuracy, or hidden evasion rolls.
