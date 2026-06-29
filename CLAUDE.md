# Unity Project — AI Agent Instructions

These notes supplement `AGENTS.md`, `PROJECT_BRIEF.md`, and `BUILD_TICKETS.md`. Read all three before selecting or implementing an autonomous ticket.

## Project Structure

- `Assets/` — all Unity assets, including scripts, scenes, prefabs, materials, tests, and ScriptableObjects.
- `Assets/Scripts/ReactionTactics/Runtime/` — runtime C# under `ReactionTactics.*` namespaces.
- `Assets/Scripts/ReactionTactics/Editor/` — editor-only scripts, build methods, and `[UnityCliTool]` commands.
- `Assets/Tests/EditMode/` and `Assets/Tests/PlayMode/` — Unity Test Framework coverage.
- `ProjectSettings/` — version-controlled Unity project settings.
- `Packages/` — package manifest and lock file.

## Key Settings

- **Serialization**: Force Text for readable `.unity`, `.prefab`, `.asset`, and `.mat` YAML.
- **Meta Files**: Visible Meta Files; never delete `.meta` files.
- **Editor control**: use the `unity-ctrl --project "$PWD" ...` Connector workflow from the project root.

## Ticket Workflow

1. Select the lowest-numbered `TODO` ticket in `BUILD_TICKETS.md`.
2. Implement only that ticket and keep the change commit-sized.
3. Run the ticket-specific validation from `BUILD_TICKETS.md` when applicable.
4. Run `scripts/quality-gate.sh` before marking the ticket done.
5. Change only the selected ticket status to `DONE`.
6. Commit with the ticket's conventional commit message and leave the working tree clean.

Docs-only tickets do not require Unity validation beyond the repository quality gate unless their ticket explicitly says otherwise.

## Global Unity Validation Loop

Before changing Unity project files, inspect editor state:

```bash
unity-ctrl --project "$PWD" status
```

For C# changes:

```bash
unity-ctrl --project "$PWD" editor refresh --compile
unity-ctrl --project "$PWD" console --type error --stacktrace full --lines 50
```

For scene, prefab, material, or ScriptableObject asset changes:

```bash
unity-ctrl --project "$PWD" reserialize <changed-unity-asset-paths>
unity-ctrl --project "$PWD" editor refresh
```

For tests:

```bash
unity-ctrl --project "$PWD" test --filter <relevant-test-filter>
```

For scene mutation, save through Unity after the change:

```bash
unity-ctrl --project "$PWD" menu "File/Save"
unity-ctrl --project "$PWD" menu "File/Save Project"
```

## Safe Scene and Asset Editing

- Prefer `unity-ctrl exec` or project-specific `[UnityCliTool]` commands for structural scene, prefab, and asset creation or wiring.
- Raw YAML edits are allowed only for simple serialized value changes that are easy to verify.
- After every raw YAML edit to `.unity`, `.prefab`, `.asset`, or `.mat`, run `unity-ctrl --project "$PWD" reserialize <path>` before committing.
- Do not invent Unity YAML `fileID`s, hand-wire complex object references, or bypass Unity's serializer for structural changes.
- Do not edit or commit generated/private folders such as `Library/`, `Temp/`, `Obj/`, `Build/`, `Logs/`, or `UserSettings/`.

## Prototype Combat Pillars

Keep implementation aligned with these deterministic rules:

- Units move on a discrete `x, y, z` grid over visible stepped 3D terrain.
- Each unit has a shared AP pool refreshed at the start of each round.
- Actions are usable only on the acting unit's own turn.
- Reactions are usable only during another unit's action window.
- Every action that triggers reactions opens a Reaction Turn for every other living unit, ordered by distance from the Action Character.
- Option A melee timing: declare melee in range, run reactions, then hit only if the target is still in melee range at resolution.
- Ranged cones and AoEs resolve against units still inside the declared shape after reactions.
- Avoidance is physical movement on the grid; do not add dodge chance, accuracy rolls, or hidden probabilistic hit logic.

## C# Conventions

- Use `[SerializeField]` for inspector-exposed private fields.
- Use namespaces matching the feature layout, such as `ReactionTactics.Grid`, `ReactionTactics.Units`, and `ReactionTactics.Reactions`.
- Keep deterministic rules in testable runtime classes and scene wiring in thin MonoBehaviours or editor tools.
- Prefer one MonoBehaviour per file, with the filename matching the class name.
