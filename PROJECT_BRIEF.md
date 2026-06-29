# PROJECT_BRIEF.md

TEMPLATE_CUSTOMISED: true

## Project name

Reaction Tactics Prototype

## Project type

Unity 3D fantasy tactics prototype.

## Project goal

Build a playable Unity prototype focused on tactical action/reaction timing: units move on a discrete 3D grid, spend AP on their own turns, and respond during other units' action windows with deterministic movement and reactions rather than dodge chance or accuracy rolls.

## Audience

* maintainers and autonomous coding agents working in this repository
* playtesters evaluating the tactics prototype loop
* reviewers assessing project structure, validation discipline, and deterministic combat behavior

## Success criteria

The project is successful when:

* a Unity project exists with `Assets/`, `Packages/`, and `ProjectSettings/` checked in appropriately
* a small 2v2 or 3v3 scenario is playable in-editor
* the prototype demonstrates shared AP, own-turn actions, other-unit reactions, reaction ordering by distance, Option A melee timing, ranged cone resolution, and AoE resolution
* Unity compile, relevant tests, scene validation, and Linux build validation pass for their respective tickets
* generated/private Unity folders and secrets are not committed

## Non-goals

The agent must not spend time on:

* multiplayer, networking, accounts, matchmaking, or live-service systems
* large campaign content, procedural maps, final art, or production audio
* random dodge chance, accuracy rolls, or hidden probabilistic avoidance
* broad RPG progression beyond what is needed for the prototype
* destructive automation or machine-specific configuration

## Technology preferences

Preferred stack:

* engine: Unity via Unity Hub
* language: C#
* runtime namespace: `ReactionTactics`
* editor control: `unity-ctrl` Connector workflow
* agent tooling: Pi-compatible command through `scripts/run-agent.sh`, using `pi-dan-rinse`
* validation: per-ticket Unity validation commands plus `scripts/quality-gate.sh`
* CI: shell/security checks by default; Unity CI can be added later when a runner is available

Hard constraints:

* use the Unity skill workflow for project/scene/prefab/material changes
* never edit `Library/`, `Temp/`, `Obj/`, generated build output, or generated solution files
* never delete `.meta` files
* prefer `unity-ctrl exec` or project-specific `[UnityCliTool]` commands for structural scene/prefab edits
* do not invent Unity YAML `fileID`s
* after serialized Unity asset edits, reserialize the changed asset before committing
* one ticket equals one commit

Flexible choices:

* placeholder visuals may be primitives and simple materials
* systems may start as narrow vertical slices before expanding in later tickets
* local helper scripts may wrap Unity validation commands when they do not hide failures

## Architecture expectations

Use the repository layout described in `BUILD_TICKETS.md`, centered on:

```text
Assets/
  Scenes/
  Scripts/ReactionTactics/Runtime/
  Scripts/ReactionTactics/Editor/
  Tests/EditMode/
  Tests/PlayMode/
  Prefabs/
  Materials/
  ScriptableObjects/
  Resources/
  Art/Prototype/
Packages/
ProjectSettings/
```

Runtime code should use namespace prefixes under `ReactionTactics` by feature area, for example:

```text
ReactionTactics.Core
ReactionTactics.Grid
ReactionTactics.Units
ReactionTactics.Turns
ReactionTactics.Actions
ReactionTactics.Reactions
ReactionTactics.UI
```

Keep deterministic rules in testable runtime classes. Keep Unity scene/prefab wiring thin and inspectable through editor tools.

## Quality expectations

Expected quality gates:

* run the validation commands listed on each ticket in `BUILD_TICKETS.md`
* run `scripts/quality-gate.sh` before marking a ticket done
* for C# changes: compile through Unity and inspect console errors
* for scene/prefab/material/asset changes: reserialize and refresh through Unity
* for tests: run the requested EditMode or PlayMode tests
* for build tickets: validate the Linux build command and ignored output path
* maintain a clean git working tree at the end of every autonomous cycle

## Documentation expectations

Required docs over the project lifetime:

* `README.md` with project purpose and autobuild usage
* project-level coding-agent instructions when the ticket backlog reaches that work
* prototype design README, known limitations, playtest checklist, and validation docs as tickets require

## Safety and security constraints

Do not include:

* real secrets, credentials, tokens, private keys, or private data
* internal hostnames, private URLs, employer/client data, or proprietary assets
* generated Unity folders or build artifacts
* destructive scripts or unsafe arbitrary command execution
* copyrighted/trademarked art, names, mechanics, UI, or copied content

## Agent behaviour notes

Project-specific instructions for the agent:

* read `AGENTS.md`, this brief, and `BUILD_TICKETS.md` before each autonomous ticket
* select the lowest-numbered `TODO` ticket from `BUILD_TICKETS.md`
* follow the Unity skill loop: inspect state, make the smallest safe edit, refresh/compile/reserialize, query Unity to verify, then save
* if Unity is compiling or reloading, wait and retry instead of editing blindly
* keep every ticket independently reviewable and commit-sized
* keep `BUILD_TICKETS.md` limited to ticket descriptions plus status only; do not add cycle notes, validation summaries, or blocker commentary there
