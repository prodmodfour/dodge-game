# BUILD_TICKETS.md — Reaction Tactics Prototype

This backlog is written for a coding agent using the `pi-unity-skill` workflow. Each ticket is intended to be a single commit-sized piece of work. Work through the tickets in order unless a ticket is explicitly marked as parallel-safe.

AUTOMATION_STATUS: NOT_DONE

Ticket statuses:

* TODO — not done
* DONE — done

The build loop must select the lowest-numbered TODO ticket.

---

## Prototype target

Build a playable Unity 3D fantasy tactics prototype with:

- A discrete `x, y, z` grid over visible 3D stepped terrain.
- Shared AP per unit, refreshed at the start of each round.
- Actions usable only on a unit's own turn.
- Reactions usable only during another unit's action window.
- Every action opens a Reaction Turn for every other living unit, ordered by distance from the Action Character.
- Option A melee timing: melee attacks are declared, reactions happen, then the attack hits only if the target is still in melee range at resolution.
- No dodge chance and no accuracy rolls. Avoidance is physical movement on the grid.
- Ranged cones hit units still inside the cone after reactions.
- AoEs hit units still inside the area after reactions.
- A small 2v2 or 3v3 scenario that can be played in-editor and built as a Linux standalone.

## Global agent rules for every ticket

Before changing Unity project files:

```bash
cd <project-root>
unity-ctrl --project "$PWD" status
```

For C# changes:

```bash
unity-ctrl --project "$PWD" editor refresh --compile
unity-ctrl --project "$PWD" console --type error --stacktrace full --lines 50
```

For scene, prefab, material, or asset YAML edits:

```bash
unity-ctrl --project "$PWD" reserialize <changed-unity-asset-paths>
unity-ctrl --project "$PWD" editor refresh
```

Prefer `unity-ctrl exec` or project-specific `[UnityCliTool]` commands for structural scene/prefab edits. Do not invent Unity YAML `fileID`s. Do not edit `Library/`, `Temp/`, `Obj/`, generated build output, or delete `.meta` files.

For tests:

```bash
unity-ctrl --project "$PWD" test --filter <relevant-test-filter>
```

For scene mutation:

```bash
unity-ctrl --project "$PWD" menu "File/Save"
unity-ctrl --project "$PWD" menu "File/Save Project"
```

Each commit should leave the project compiling with no console errors unless the ticket explicitly states it is a docs-only commit.

---

# Milestone 00 — Project and agent setup

## 001 — Initialize or verify the Unity project

Status: DONE

**Commit:** `chore: initialize reaction tactics unity project`

**Goal:** Create a clean Unity project or verify the existing project is ready for terminal-driven development.

**Scope:** Project root, `Packages/`, `ProjectSettings/`, `.gitignore`.

**Tasks:**

- If no project exists, create `ReactionTacticsPrototype` with `unity-init-project`.
- Add the Unity Connector package using the skill's setup script if missing.
- Run the skill preflight script.
- Confirm Force Text serialization and Visible Meta Files are enabled.
- Confirm Unity opens and `unity-ctrl status` reports ready.

**Acceptance:**

- `unity-ctrl --project "$PWD" status` returns a ready Unity instance.
- Project can refresh and compile without errors.
- No generated folders are committed.

**Validation:**

```bash
bash "$SKILL_DIR/scripts/preflight.sh" "$PWD"
unity-ctrl --project "$PWD" editor refresh --compile
unity-ctrl --project "$PWD" console --type error --stacktrace full --lines 50
```

## 002 — Add project-level coding-agent instructions

Status: DONE

**Commit:** `docs: add coding agent unity workflow notes`

**Goal:** Give the coding agent durable instructions inside the repository.

**Scope:** `CLAUDE.md` or `AGENTS.md`, plus `README.md` if present.

**Tasks:**

- Document the global validation loop.
- State that structural scene changes should use `unity-ctrl exec` or custom CLI tools.
- State that raw YAML edits are allowed only for simple serialized values and must be followed by reserialize.
- State the prototype combat pillars: AP, actions, reactions, reaction order, no dodge chance, Option A melee.

**Acceptance:**

- Agent instructions are visible at repository root.
- Instructions are specific enough for future tickets to be executed safely.

**Validation:** Docs-only; no Unity validation required.

## 003 — Create the prototype folder layout

Status: DONE

**Commit:** `chore: add prototype folder structure`

**Goal:** Establish a predictable file layout.

**Scope:** `Assets/` folders only.

**Tasks:**

- Create these folders:
  - `Assets/Scenes`
  - `Assets/Scripts/ReactionTactics/Runtime`
  - `Assets/Scripts/ReactionTactics/Editor`
  - `Assets/Tests/EditMode`
  - `Assets/Tests/PlayMode`
  - `Assets/Prefabs`
  - `Assets/Materials`
  - `Assets/ScriptableObjects`
  - `Assets/Resources`
  - `Assets/Art/Prototype`
- Add `.keep` files only where Unity does not create `.meta` files automatically.

**Acceptance:**

- Folders exist in Unity project.
- No runtime behavior is changed.

**Validation:**

```bash
unity-ctrl --project "$PWD" editor refresh
unity-ctrl --project "$PWD" console --type error --stacktrace full --lines 50
```

## 004 — Add runtime, editor, and test assembly definitions

Status: DONE

**Commit:** `chore: add reaction tactics assembly definitions`

**Goal:** Create fast, isolated compilation boundaries.

**Scope:** `.asmdef` files.

**Tasks:**

- Add `ReactionTactics.Runtime.asmdef` under runtime scripts.
- Add `ReactionTactics.Editor.asmdef` under editor scripts, referencing runtime and UnityEditor assemblies.
- Add `ReactionTactics.Tests.EditMode.asmdef`, referencing runtime and NUnit.
- Add `ReactionTactics.Tests.PlayMode.asmdef`, referencing runtime and NUnit.

**Acceptance:**

- Assemblies compile.
- EditMode and PlayMode test assemblies are discoverable.

**Validation:**

```bash
unity-ctrl --project "$PWD" editor refresh --compile
unity-ctrl --project "$PWD" test --filter ReactionTactics
unity-ctrl --project "$PWD" console --type error --stacktrace full --lines 50
```

## 005 — Create and register the main prototype scene

Status: DONE

**Commit:** `scene: add main prototype scene`

**Goal:** Create `Assets/Scenes/MainPrototype.unity` and make it the primary scene.

**Scope:** Scene asset and build settings.

**Tasks:**

- Create a new empty scene named `MainPrototype`.
- Add a camera, directional light, and root GameObjects: `Systems`, `Grid`, `Units`, `UI`, `Scenario`.
- Add the scene to build settings.
- Save scene and project.

**Acceptance:**

- Scene opens in Unity.
- Scene is the only enabled scene in build settings.
- Scene hierarchy contains the expected root objects.

**Validation:**

```bash
unity-ctrl --project "$PWD" exec "return UnityEditor.SceneManagement.EditorSceneManager.GetActiveScene().path;"
unity-ctrl --project "$PWD" menu "File/Save"
unity-ctrl --project "$PWD" menu "File/Save Project"
```

## 006 — Add a minimal custom hierarchy CLI tool

Status: DONE

**Commit:** `tooling: add hierarchy dump cli tool`

**Goal:** Let the coding agent inspect scenes as structured data.

**Scope:** `Assets/Scripts/ReactionTactics/Editor/HierarchyDumpTool.cs`.

**Tasks:**

- Create a static `[UnityCliTool]` named `rt_hierarchy`.
- Return root objects, child depths, active flags, positions, and component names.
- Include optional parameter `includeInactive`.
- Keep output compact and JSON-like.

**Acceptance:**

- `unity-ctrl rt_hierarchy` lists the main scene hierarchy.
- The tool works after script compilation.

**Validation:**

```bash
unity-ctrl --project "$PWD" editor refresh --compile
unity-ctrl --project "$PWD" list
unity-ctrl --project "$PWD" rt_hierarchy
unity-ctrl --project "$PWD" console --type error --stacktrace full --lines 50
```

## 007 — Add a minimal asset finder CLI tool

Status: DONE

**Commit:** `tooling: add asset finder cli tool`

**Goal:** Give the agent a fast way to locate Unity assets.

**Scope:** `Assets/Scripts/ReactionTactics/Editor/AssetFinderTool.cs`.

**Tasks:**

- Create `[UnityCliTool]` named `rt_find_assets`.
- Accept `filter` and optional `limit` parameters.
- Return asset paths found by `AssetDatabase.FindAssets`.

**Acceptance:**

- The tool can list scenes, scripts, materials, prefabs, and ScriptableObjects.

**Validation:**

```bash
unity-ctrl --project "$PWD" editor refresh --compile
unity-ctrl --project "$PWD" rt_find_assets --params '{"filter":"t:Scene"}'
unity-ctrl --project "$PWD" console --type error --stacktrace full --lines 50
```

## 008 — Add a prototype design README

Status: DONE

**Commit:** `docs: document prototype combat design`

**Goal:** Record the intended prototype rules for future implementation tickets.

**Scope:** `docs/prototype-design.md`.

**Tasks:**

- Document AP refresh at round start.
- Document actions vs reactions.
- Document reaction order by distance from the acting unit.
- Document Option A melee timing.
- Document ranged cone and AoE avoidance by movement.
- Document that reactions do not open full nested reaction windows unless a future ability explicitly says so.

**Acceptance:**

- Design doc matches the requested mechanics.

**Validation:** Docs-only.

## 009 — Add a Linux build script shell

Status: DONE

**Commit:** `build: add prototype build script shell`

**Goal:** Prepare a reproducible build entry point before gameplay is complete.

**Scope:** `Assets/Scripts/ReactionTactics/Editor/BuildPrototype.cs`.

**Tasks:**

- Add static editor build method `ReactionTactics.Editor.BuildPrototype.PerformBuild`.
- Build `Assets/Scenes/MainPrototype.unity` to `Build/ReactionTacticsPrototype`.
- Log build summary and result.

**Acceptance:**

- Build method compiles.
- Running the build may produce a simple empty-player build.

**Validation:**

```bash
unity-ctrl --project "$PWD" editor refresh --compile
unity-batch "$PWD" -executeMethod ReactionTactics.Editor.BuildPrototype.PerformBuild -quit
```

## 010 — Add the first empty EditMode smoke test

Status: DONE

**Commit:** `test: add initial editmode smoke test`

**Goal:** Ensure test infrastructure is wired correctly.

**Scope:** `Assets/Tests/EditMode/ReactionTacticsSmokeTests.cs`.

**Tasks:**

- Add an NUnit test that asserts the runtime assembly loads.
- Use namespace `ReactionTactics.Tests.EditMode`.

**Acceptance:**

- Test appears and passes in Unity Test Framework.

**Validation:**

```bash
unity-ctrl --project "$PWD" editor refresh --compile
unity-ctrl --project "$PWD" test --filter ReactionTacticsSmokeTests
```

---

# Milestone 01 — Core domain model

## 011 — Implement `GridPosition`

Status: DONE

**Commit:** `feat: add grid position value type`

**Goal:** Add the canonical `x, y, z` coordinate type.

**Scope:** Runtime core model.

**Tasks:**

- Create immutable `GridPosition` struct with `int X`, `int Y`, `int Z`.
- Add equality, `GetHashCode`, `ToString`, `+`, `-`, and common static positions.
- Add constructors and helpers for horizontal-only positions.

**Acceptance:**

- `GridPosition` can be used as a dictionary key.
- String output is useful for logs, e.g. `(3,1,5)`.

**Validation:**

```bash
unity-ctrl --project "$PWD" editor refresh --compile
unity-ctrl --project "$PWD" console --type error --stacktrace full --lines 50
```

## 012 — Add grid distance helpers

Status: DONE

**Commit:** `feat: add grid distance helpers`

**Goal:** Provide deterministic distance calculations used by reaction ordering and ability range.

**Scope:** `GridPosition` or `GridMath`.

**Tasks:**

- Add Manhattan distance: `abs(dx) + abs(dy) + abs(dz)`.
- Add horizontal distance: `abs(dx) + abs(dz)`.
- Add tactical distance with configurable vertical weight.
- Add adjacency helper for 4-way horizontal adjacency with optional height difference.

**Acceptance:**

- Reaction order can use a single canonical distance helper.
- Tests cover distance symmetry and vertical weighting.

**Validation:**

```bash
unity-ctrl --project "$PWD" editor refresh --compile
unity-ctrl --project "$PWD" test --filter GridPositionDistanceTests
```

## 013 — Add `CardinalDirection` and direction math

Status: DONE

**Commit:** `feat: add cardinal grid direction math`

**Goal:** Support cone targeting and movement directions.

**Scope:** Runtime grid math.

**Tasks:**

- Add enum `CardinalDirection { North, East, South, West }`.
- Add methods to convert direction to `GridPosition` offset.
- Add method to choose the dominant cardinal direction from origin to target.
- Add left/right/perpendicular helpers for cone width calculations.

**Acceptance:**

- Direction selection is deterministic when target is diagonal.
- Tests cover all directions.

**Validation:**

```bash
unity-ctrl --project "$PWD" editor refresh --compile
unity-ctrl --project "$PWD" test --filter CardinalDirectionTests
```

## 014 — Add teams and unit identity model

Status: DONE

**Commit:** `feat: add team and unit identity types`

**Goal:** Represent player/enemy sides and stable unit IDs.

**Scope:** Runtime combat model.

**Tasks:**

- Add `TeamId` enum or struct with Player and Enemy.
- Add `UnitId` value type or stable integer ID generator.
- Add helper for `IsHostileTo` and `IsFriendlyTo`.

**Acceptance:**

- Units can be sorted, logged, and compared by stable identity.

**Validation:**

```bash
unity-ctrl --project "$PWD" editor refresh --compile
unity-ctrl --project "$PWD" console --type error --stacktrace full --lines 50
```

## 015 — Add lightweight result/error types

Status: DONE

**Commit:** `feat: add tactical result type`

**Goal:** Avoid throwing exceptions for expected invalid commands.

**Scope:** Runtime core utilities.

**Tasks:**

- Add `TacticalResult` with success/failure and error message.
- Add generic `TacticalResult<T>` if useful.
- Use clear messages suitable for UI and logs.

**Acceptance:**

- Future command validation can return structured failures.

**Validation:**

```bash
unity-ctrl --project "$PWD" editor refresh --compile
unity-ctrl --project "$PWD" console --type error --stacktrace full --lines 50
```

## 016 — Add grid cell data model

Status: DONE

**Commit:** `feat: add grid cell data model`

**Goal:** Represent terrain cells independent of visual tiles.

**Scope:** Runtime grid model.

**Tasks:**

- Add `GridCell` class or struct with position, walkable, blocksMovement, blocksLineOfSight, movementCost, displayHeight.
- Keep occupant reference out of serialized terrain data initially; occupancy belongs to runtime state.
- Add methods for clone/copy or immutable construction.

**Acceptance:**

- Grid cells can represent height, blockers, and cost.

**Validation:**

```bash
unity-ctrl --project "$PWD" editor refresh --compile
unity-ctrl --project "$PWD" console --type error --stacktrace full --lines 50
```

## 017 — Add grid map interface

Status: DONE

**Commit:** `feat: add grid map query interface`

**Goal:** Decouple systems from a concrete map implementation.

**Scope:** Runtime grid interfaces.

**Tasks:**

- Add `IGridMap` with `Contains`, `TryGetCell`, `GetCell`, `AllCells`, and bounds properties.
- Add convenience helpers for walkability and blockers.

**Acceptance:**

- Pathfinding and targeting can depend on `IGridMap`.

**Validation:**

```bash
unity-ctrl --project "$PWD" editor refresh --compile
unity-ctrl --project "$PWD" console --type error --stacktrace full --lines 50
```

## 018 — Add in-memory grid map implementation

Status: DONE

**Commit:** `feat: add in memory grid map`

**Goal:** Provide a pure C# grid map implementation testable without scenes.

**Scope:** Runtime grid implementation.

**Tasks:**

- Add `GridMap` backed by dictionary keyed by `GridPosition`.
- Add constructor from cells.
- Validate no duplicate cells.
- Add bounds discovery from cells.

**Acceptance:**

- `GridMap` works in EditMode tests without Unity scene objects.

**Validation:**

```bash
unity-ctrl --project "$PWD" editor refresh --compile
unity-ctrl --project "$PWD" test --filter GridMapTests
```

## 019 — Add grid/world conversion metrics

Status: DONE

**Commit:** `feat: add grid world metrics`

**Goal:** Centralize conversion between grid cells and Unity world positions.

**Scope:** Runtime grid presentation utilities.

**Tasks:**

- Add `GridMetrics` with `cellSize`, `heightStep`, and `origin`.
- Add `GridToWorldCenter(GridPosition)`.
- Add `WorldToApproxGrid(Vector3)` for picking support.

**Acceptance:**

- Units and tiles can share consistent world placement.

**Validation:**

```bash
unity-ctrl --project "$PWD" editor refresh --compile
unity-ctrl --project "$PWD" test --filter GridMetricsTests
```

## 020 — Add core model tests

Status: DONE

**Commit:** `test: cover core grid and identity models`

**Goal:** Lock down foundational types before gameplay depends on them.

**Scope:** EditMode tests.

**Tasks:**

- Test `GridPosition` equality and hash behavior.
- Test distance helpers.
- Test direction helpers.
- Test `GridMap` duplicate detection and lookup.
- Test `GridMetrics` conversions at default values.

**Acceptance:**

- Core model tests pass consistently.

**Validation:**

```bash
unity-ctrl --project "$PWD" test --filter ReactionTactics.Tests.EditMode.Core
```

---

# Milestone 02 — 3D grid terrain

## 021 — Add serialized prototype map definition

Status: DONE

**Commit:** `feat: add prototype map definition asset type`

**Goal:** Let designers define a small grid map as data.

**Scope:** Runtime ScriptableObject.

**Tasks:**

- Add `GridMapDefinition : ScriptableObject`.
- Store width, depth, default height, and per-cell overrides.
- Cell overrides must include `x`, `z`, `heightY`, `walkable`, `blocksLineOfSight`, `movementCost`.
- Add method to build an in-memory `GridMap`.

**Acceptance:**

- A map can be created as a Unity asset.
- In-memory map generation is deterministic.

**Validation:**

```bash
unity-ctrl --project "$PWD" editor refresh --compile
unity-ctrl --project "$PWD" console --type error --stacktrace full --lines 50
```

## 022 — Add editor menu to create the default prototype map

Status: DONE

**Commit:** `tooling: add default map asset creator`

**Goal:** Make the base terrain reproducible.

**Scope:** Editor script.

**Tasks:**

- Add a menu item or CLI tool `rt_create_default_map`.
- Create `Assets/ScriptableObjects/DefaultPrototypeMap.asset`.
- Define an 8x8 map with varied heights, a few blockers, and no impossible starts.

**Acceptance:**

- Running the tool creates or updates the map asset.
- Asset builds into a valid `GridMap`.

**Validation:**

```bash
unity-ctrl --project "$PWD" editor refresh --compile
unity-ctrl --project "$PWD" rt_create_default_map
unity-ctrl --project "$PWD" rt_find_assets --params '{"filter":"DefaultPrototypeMap t:GridMapDefinition"}'
```

## 023 — Add `GridManager` scene component

Status: DONE

**Commit:** `feat: add grid manager scene component`

**Goal:** Provide the scene-level owner for the active grid map.

**Scope:** Runtime MonoBehaviour.

**Tasks:**

- Add `GridManager : MonoBehaviour` with serialized `GridMapDefinition` and `GridMetrics`.
- Build the in-memory grid on `Awake`.
- Expose read-only `CurrentMap`.
- Guard against missing map definition with clear errors.

**Acceptance:**

- `GridManager` can be placed in scene and queried at runtime.

**Validation:**

```bash
unity-ctrl --project "$PWD" editor refresh --compile
unity-ctrl --project "$PWD" console --type error --stacktrace full --lines 50
```

## 024 — Add primitive tile view component

Status: DONE

**Commit:** `feat: add grid tile view component`

**Goal:** Represent visible cells with simple 3D tiles.

**Scope:** Runtime presentation.

**Tasks:**

- Add `GridTileView : MonoBehaviour`.
- Store `GridPosition`, base material reference, and highlight state.
- Support setting height scale from cell data.
- Include a collider for mouse picking.

**Acceptance:**

- Tile views can be created from primitives and know their grid position.

**Validation:**

```bash
unity-ctrl --project "$PWD" editor refresh --compile
unity-ctrl --project "$PWD" console --type error --stacktrace full --lines 50
```

## 025 — Add terrain visualizer

Status: DONE

**Commit:** `feat: generate visible grid terrain from map`

**Goal:** Spawn the stepped 3D terrain at scene start.

**Scope:** Runtime presentation.

**Tasks:**

- Add `GridTerrainView : MonoBehaviour`.
- Given a `GridManager`, instantiate tile primitives for each grid cell.
- Set each tile's world position and vertical scale based on `GridMetrics` and cell height.
- Parent all tiles under the `Grid` root.

**Acceptance:**

- Entering play mode shows an 8x8 stepped terrain grid.
- Blocked/unwalkable cells are visually distinguishable.

**Validation:**

```bash
unity-ctrl --project "$PWD" editor refresh --compile
unity-ctrl --project "$PWD" editor play --wait
unity-ctrl --project "$PWD" screenshot --view game --output_path /tmp/reaction-tactics-grid.png
unity-ctrl --project "$PWD" editor stop
```

## 026 — Add prototype terrain materials

Status: DONE

**Commit:** `art: add prototype grid materials`

**Goal:** Create simple materials for walkable, blocked, highlighted, danger, and safe cells.

**Scope:** `Assets/Materials` and terrain view references.

**Tasks:**

- Create materials using built-in shaders.
- Add serialized material slots to `GridTerrainView` or a `GridHighlightPalette` asset.
- Do not depend on final art.

**Acceptance:**

- Walkable and blocked cells are easy to distinguish in Game view.

**Validation:**

```bash
unity-ctrl --project "$PWD" reserialize Assets/Materials/*.mat
unity-ctrl --project "$PWD" editor refresh
unity-ctrl --project "$PWD" console --type error --stacktrace full --lines 50
```

## 027 — Add terrain CLI scene setup tool

Status: DONE

**Commit:** `tooling: add grid terrain scene setup tool`

**Goal:** Make grid scene setup reproducible from the terminal.

**Scope:** Editor CLI tool.

**Tasks:**

- Add `[UnityCliTool]` named `rt_setup_grid_scene`.
- Ensure the scene has `GridManager` and `GridTerrainView` on the correct root objects.
- Assign `DefaultPrototypeMap.asset` and material references.
- Save the scene.

**Acceptance:**

- Running the tool on `MainPrototype` creates a visible grid setup.

**Validation:**

```bash
unity-ctrl --project "$PWD" editor refresh --compile
unity-ctrl --project "$PWD" rt_setup_grid_scene
unity-ctrl --project "$PWD" rt_hierarchy
unity-ctrl --project "$PWD" menu "File/Save"
```

## 028 — Add grid neighbor service

Status: DONE

**Commit:** `feat: add grid neighbor service`

**Goal:** Provide valid movement neighbors over 3D terrain.

**Scope:** Runtime grid logic.

**Tasks:**

- Add `GridNeighborService` with 4-way neighbors.
- Use cell heights to produce neighbor positions with correct `Y`.
- Respect map bounds and walkability.
- Add configurable max climb and max drop values.

**Acceptance:**

- Neighbor lookup ignores blocked cells and impossible height transitions.

**Validation:**

```bash
unity-ctrl --project "$PWD" editor refresh --compile
unity-ctrl --project "$PWD" test --filter GridNeighborServiceTests
```

## 029 — Add movement cost service

Status: DONE

**Commit:** `feat: add movement cost service`

**Goal:** Determine AP cost for moving from one cell to a neighbor.

**Scope:** Runtime movement logic.

**Tasks:**

- Add `MovementCostService`.
- Base cost should be destination cell movement cost.
- Add optional uphill surcharge for prototype tuning.
- Return invalid result for non-neighbor moves.

**Acceptance:**

- Movement cost is deterministic and testable.

**Validation:**

```bash
unity-ctrl --project "$PWD" editor refresh --compile
unity-ctrl --project "$PWD" test --filter MovementCostServiceTests
```

## 030 — Add grid blocker and line-of-sight flags to visuals

Status: DONE

**Commit:** `feat: visualize terrain blocker flags`

**Goal:** Show blockers that affect movement and sight.

**Scope:** Map definition, tile visuals.

**Tasks:**

- Add visual state for `blocksLineOfSight`.
- Make line-of-sight blockers distinct from merely unwalkable cells.
- Update default map to include two or three blocker cells.

**Acceptance:**

- The map visibly communicates blocked and sight-blocking cells.

**Validation:**

```bash
unity-ctrl --project "$PWD" rt_create_default_map
unity-ctrl --project "$PWD" rt_setup_grid_scene
unity-ctrl --project "$PWD" editor play --wait
unity-ctrl --project "$PWD" screenshot --view game --output_path /tmp/reaction-tactics-blockers.png
unity-ctrl --project "$PWD" editor stop
```

## 031 — Add grid terrain tests

Status: DONE

**Commit:** `test: cover prototype grid terrain generation`

**Goal:** Prove the terrain data layer behaves before movement depends on it.

**Scope:** EditMode tests.

**Tasks:**

- Test default map dimensions.
- Test blocker and walkability flags.
- Test cell heights produce expected `GridPosition.Y` values.
- Test neighbor service honors height limits.
- Test movement costs on flat and uphill cells.

**Acceptance:**

- Tests pass without loading a scene.

**Validation:**

```bash
unity-ctrl --project "$PWD" test --filter GridTerrain
```

---

# Milestone 03 — Pathfinding and movement ranges

## 032 — Add pathfinding node and path result types

Status: DONE

**Commit:** `feat: add pathfinding result types`

**Goal:** Define stable data structures for paths and reachable cells.

**Scope:** Runtime pathfinding.

**Tasks:**

- Add `PathStep`, `GridPath`, and `ReachableCell` types.
- Include total AP cost and ordered positions.
- Include failure reason for invalid paths.

**Acceptance:**

- Movement systems can represent successful and failed paths.

**Validation:**

```bash
unity-ctrl --project "$PWD" editor refresh --compile
unity-ctrl --project "$PWD" console --type error --stacktrace full --lines 50
```

## 033 — Implement reachable-cell search

Status: DONE

**Commit:** `feat: calculate reachable cells by ap budget`

**Goal:** Find all cells a unit can reach with a given AP budget.

**Scope:** Runtime pathfinding.

**Tasks:**

- Implement Dijkstra or BFS with costs.
- Input: start cell, AP budget, map, neighbor service, movement cost service, occupancy query.
- Output: dictionary of reachable positions and costs.
- Include start cell with cost 0.

**Acceptance:**

- Movement preview can query all legal destinations.
- Search respects terrain cost and height limits.

**Validation:**

```bash
unity-ctrl --project "$PWD" editor refresh --compile
unity-ctrl --project "$PWD" test --filter ReachableCellSearchTests
```

## 034 — Implement shortest path reconstruction

Status: DONE

**Commit:** `feat: reconstruct shortest grid paths`

**Goal:** Return the path to a selected reachable cell.

**Scope:** Runtime pathfinding.

**Tasks:**

- Store parent pointers from reachable-cell search.
- Add `TryFindPath(start, destination, budget, ...)`.
- Return ordered positions from start to destination.
- Include total AP cost.

**Acceptance:**

- A unit can animate through the chosen path in order.

**Validation:**

```bash
unity-ctrl --project "$PWD" editor refresh --compile
unity-ctrl --project "$PWD" test --filter GridPathfindingTests
```

## 035 — Add occupancy query interface

Status: DONE

**Commit:** `feat: add grid occupancy query interface`

**Goal:** Allow pathfinding and targeting to know which cells contain units.

**Scope:** Runtime grid/combat interfaces.

**Tasks:**

- Add `IGridOccupancy` with `IsOccupied`, `TryGetOccupant`, and `CanEnter`.
- Add a null occupancy implementation for tests.
- Ensure pathfinding takes an occupancy dependency.

**Acceptance:**

- Tests can run with or without units.
- Occupied cells can block movement.

**Validation:**

```bash
unity-ctrl --project "$PWD" editor refresh --compile
unity-ctrl --project "$PWD" test --filter GridOccupancyTests
```

## 036 — Make pathfinding occupancy-aware

Status: DONE

**Commit:** `feat: block movement through occupied cells`

**Goal:** Prevent units from walking through or ending on occupied cells.

**Scope:** Runtime pathfinding.

**Tasks:**

- Update reachable search to skip occupied cells except the starting cell.
- Add an option for future abilities to ignore units, default false.
- Add tests for friendly and enemy occupants blocking paths.

**Acceptance:**

- Occupants affect movement ranges correctly.

**Validation:**

```bash
unity-ctrl --project "$PWD" editor refresh --compile
unity-ctrl --project "$PWD" test --filter OccupancyAwarePathfindingTests
```

## 037 — Add movement command model

Status: DONE

**Commit:** `feat: add movement command model`

**Goal:** Represent active and reaction movement consistently.

**Scope:** Runtime commands.

**Tasks:**

- Add `MoveCommand` with unit, destination, path, AP cost, and source phase.
- Add validation result type for movement.
- Include `MovementMode.Active` and `MovementMode.Reaction`.

**Acceptance:**

- Both action movement and reaction movement can use the same command path.

**Validation:**

```bash
unity-ctrl --project "$PWD" editor refresh --compile
unity-ctrl --project "$PWD" console --type error --stacktrace full --lines 50
```

## 038 — Add movement range preview data

Status: DONE

**Commit:** `feat: add movement preview model`

**Goal:** Separate reachable-cell calculation from visual highlighting.

**Scope:** Runtime/UI bridge.

**Tasks:**

- Add `MovementPreview` containing reachable cells, AP costs, and selected path.
- Add method to recompute selected path when hovering a destination.

**Acceptance:**

- UI can render movement ranges without duplicating pathfinding logic.

**Validation:**

```bash
unity-ctrl --project "$PWD" editor refresh --compile
unity-ctrl --project "$PWD" console --type error --stacktrace full --lines 50
```

## 039 — Add movement path animation component

Status: DONE

**Commit:** `feat: animate units along grid paths`

**Goal:** Move unit GameObjects through world-space positions.

**Scope:** Runtime presentation.

**Tasks:**

- Add `GridPathMover : MonoBehaviour`.
- Animate from cell center to cell center at configurable speed.
- Snap final position exactly to destination.
- Expose coroutine or async completion callback.

**Acceptance:**

- A unit can visually move along a path without changing game logic mid-step.

**Validation:**

```bash
unity-ctrl --project "$PWD" editor refresh --compile
unity-ctrl --project "$PWD" console --type error --stacktrace full --lines 50
```

## 040 — Add movement pathfinding tests

Status: DONE

**Commit:** `test: cover movement ranges and paths`

**Goal:** Lock down movement behavior before integrating units.

**Scope:** EditMode tests.

**Tasks:**

- Test reachable cells with AP 0, 1, and 3.
- Test blocked cells and occupied cells.
- Test height transitions.
- Test shortest path total cost.

**Acceptance:**

- Pathfinding tests pass and are deterministic.

**Validation:**

```bash
unity-ctrl --project "$PWD" test --filter Pathfinding
```

---

# Milestone 04 — Units, AP, HP, and spawning

## 041 — Add unit stats ScriptableObject

Status: DONE

**Commit:** `feat: add unit stats definition asset`

**Goal:** Define prototype unit archetypes as data.

**Scope:** Runtime ScriptableObject.

**Tasks:**

- Add `UnitStatsDefinition : ScriptableObject`.
- Fields: display name, max HP, max AP, movement speed/animation, melee range, team color/material hint.
- Keep stats minimal for prototype.

**Acceptance:**

- Designers can create unit archetype assets.

**Validation:**

```bash
unity-ctrl --project "$PWD" editor refresh --compile
unity-ctrl --project "$PWD" console --type error --stacktrace full --lines 50
```

## 042 — Add unit runtime component

Status: DONE

**Commit:** `feat: add tactical unit component`

**Goal:** Represent a combatant in the scene.

**Scope:** Runtime MonoBehaviour.

**Tasks:**

- Add `TacticalUnit : MonoBehaviour`.
- Store unit ID, team, stats reference, current grid position, current HP, current AP, alive/dead state.
- Provide methods to initialize from stats and position.
- Keep logic methods small; do not implement turn flow yet.

**Acceptance:**

- A unit can be initialized in play mode and queried by other systems.

**Validation:**

```bash
unity-ctrl --project "$PWD" editor refresh --compile
unity-ctrl --project "$PWD" console --type error --stacktrace full --lines 50
```

## 043 — Add AP wallet behavior

Status: DONE

**Commit:** `feat: add unit action point wallet`

**Goal:** Centralize AP spend and refresh rules.

**Scope:** Runtime unit/combat model.

**Tasks:**

- Add AP methods: `CanSpendAP`, `SpendAP`, `RefreshAP`, `SetAPForTest`.
- Prevent AP from going negative.
- Log or return failure when insufficient AP.

**Acceptance:**

- Active actions and reactions can share the same AP pool.

**Validation:**

```bash
unity-ctrl --project "$PWD" editor refresh --compile
unity-ctrl --project "$PWD" test --filter ActionPointTests
```

## 044 — Add health and damage behavior

Status: DONE

**Commit:** `feat: add unit health and damage behavior`

**Goal:** Support deterministic damage and death.

**Scope:** Runtime unit/combat model.

**Tasks:**

- Add `ApplyDamage(int amount, DamageSource source)`.
- Clamp HP at 0.
- Set alive/dead state when HP reaches 0.
- Add event or callback for death.

**Acceptance:**

- Damage does not rely on hit chance or dodge chance.
- Dead units can be filtered out by turn/reaction systems later.

**Validation:**

```bash
unity-ctrl --project "$PWD" editor refresh --compile
unity-ctrl --project "$PWD" test --filter HealthDamageTests
```

## 045 — Add unit registry

Status: DONE

**Commit:** `feat: add tactical unit registry`

**Goal:** Provide a single source of truth for living units and occupancy.

**Scope:** Runtime combat systems.

**Tasks:**

- Add `UnitRegistry : MonoBehaviour` implementing `IGridOccupancy`.
- Register/unregister units.
- Query units by ID, team, cell, alive status.
- Occupancy should update when units move.

**Acceptance:**

- Unit registry can answer occupancy queries for pathfinding.

**Validation:**

```bash
unity-ctrl --project "$PWD" editor refresh --compile
unity-ctrl --project "$PWD" test --filter UnitRegistryTests
```

## 046 — Add prototype unit prefab

Status: DONE

**Commit:** `prefab: add prototype tactical unit prefab`

**Goal:** Create a reusable primitive-based unit prefab.

**Scope:** `Assets/Prefabs/PrototypeUnit.prefab`, materials.

**Tasks:**

- Create capsule or cylinder-based unit prefab.
- Add `TacticalUnit` and `GridPathMover`.
- Add simple child marker for team/selection highlight.
- Ensure collider supports clicking.

**Acceptance:**

- Prefab can be instantiated by editor tool or runtime spawner.

**Validation:**

```bash
unity-ctrl --project "$PWD" reserialize Assets/Prefabs/PrototypeUnit.prefab
unity-ctrl --project "$PWD" editor refresh
unity-ctrl --project "$PWD" console --type error --stacktrace full --lines 50
```

## 047 — Add unit spawner service

Status: DONE

**Commit:** `feat: add tactical unit spawner`

**Goal:** Spawn units at valid grid cells and snap them to world positions.

**Scope:** Runtime and editor setup support.

**Tasks:**

- Add `UnitSpawner : MonoBehaviour`.
- Use prefab, stats definition, team, and grid position inputs.
- Register spawned units with `UnitRegistry`.
- Reject blocked or occupied spawn cells.

**Acceptance:**

- Units appear on correct tile heights.
- Registry occupancy is correct immediately after spawn.

**Validation:**

```bash
unity-ctrl --project "$PWD" editor refresh --compile
unity-ctrl --project "$PWD" console --type error --stacktrace full --lines 50
```

## 048 — Add editor CLI tool to create unit stat assets

Status: DONE

**Commit:** `tooling: add default unit stat asset creator`

**Goal:** Create standard prototype units reproducibly.

**Scope:** Editor CLI tool and ScriptableObject assets.

**Tasks:**

- Add `[UnityCliTool]` named `rt_create_default_units`.
- Create stat assets for Knight, Rogue, Archer, Mage, Goblin, Shaman.
- Suggested defaults:
  - Knight: high HP, 6 AP.
  - Rogue: medium HP, 7 AP.
  - Archer: medium HP, 6 AP.
  - Mage: low HP, 6 AP.
  - Goblin: low HP, 6 AP.
  - Shaman: low HP, 6 AP.

**Acceptance:**

- Assets exist under `Assets/ScriptableObjects/Units`.
- Assets can be assigned to unit spawner.

**Validation:**

```bash
unity-ctrl --project "$PWD" editor refresh --compile
unity-ctrl --project "$PWD" rt_create_default_units
unity-ctrl --project "$PWD" rt_find_assets --params '{"filter":"t:UnitStatsDefinition"}'
```

## 049 — Add unit scene setup CLI tool

Status: DONE

**Commit:** `tooling: add prototype unit scene setup tool`

**Goal:** Reproducibly add unit registry, spawner, and initial units to the main scene.

**Scope:** Editor CLI tool.

**Tasks:**

- Add `[UnityCliTool]` named `rt_setup_units_scene`.
- Ensure `UnitRegistry` and `UnitSpawner` exist under `Systems` or `Units`.
- Instantiate two player units and two enemy units at valid cells.
- Use default stat assets.
- Save scene.

**Acceptance:**

- Running the tool produces a 2v2 scene with correct grid positions and occupancy.

**Validation:**

```bash
unity-ctrl --project "$PWD" rt_create_default_units
unity-ctrl --project "$PWD" rt_setup_units_scene
unity-ctrl --project "$PWD" rt_hierarchy
unity-ctrl --project "$PWD" menu "File/Save"
```

## 050 — Add unit model tests

Status: DONE

**Commit:** `test: cover unit ap hp and registry behavior`

**Goal:** Confirm unit fundamentals before turn flow.

**Scope:** EditMode tests.

**Tasks:**

- Test AP spend/refund/refresh behavior.
- Test damage and death.
- Test registry occupancy before and after moving a unit's grid position.
- Test spawn validation rejects occupied cells if spawner logic is testable.

**Acceptance:**

- Unit tests pass consistently.

**Validation:**

```bash
unity-ctrl --project "$PWD" test --filter Unit
```

---

# Milestone 05 — Camera, picking, and command input

## 051 — Add tactical camera rig

Status: DONE

**Commit:** `feat: add tactical camera rig`

**Goal:** Provide a usable prototype camera over the 3D board.

**Scope:** Runtime presentation and scene.

**Tasks:**

- Add `TacticalCameraController`.
- Support pan with keyboard, rotate with Q/E, zoom with scroll wheel.
- Clamp zoom and pitch to sane prototype values.
- Position camera to view the whole default map.

**Acceptance:**

- Player can inspect the whole board in play mode.

**Validation:**

```bash
unity-ctrl --project "$PWD" editor refresh --compile
unity-ctrl --project "$PWD" editor play --wait
unity-ctrl --project "$PWD" screenshot --view game --output_path /tmp/reaction-tactics-camera.png
unity-ctrl --project "$PWD" editor stop
```

## 052 — Add grid mouse picking service

Status: DONE

**Commit:** `feat: add grid mouse picking service`

**Goal:** Convert mouse clicks into grid cells.

**Scope:** Runtime input.

**Tasks:**

- Add `GridPicker : MonoBehaviour`.
- Raycast from active camera to `GridTileView` colliders.
- Expose current hover cell and clicked cell events.
- Return no result when pointer is over UI or empty space.

**Acceptance:**

- Clicking a tile logs or emits the correct `GridPosition`.

**Validation:**

```bash
unity-ctrl --project "$PWD" editor refresh --compile
unity-ctrl --project "$PWD" console --clear
unity-ctrl --project "$PWD" editor play --wait
unity-ctrl --project "$PWD" console --type error,warning,log --lines 50
unity-ctrl --project "$PWD" editor stop
```

## 053 — Add unit mouse picking

Status: DONE

**Commit:** `feat: add tactical unit picking`

**Goal:** Let the player select units directly.

**Scope:** Runtime input.

**Tasks:**

- Extend picking to detect `TacticalUnit` colliders.
- Emit unit hover and unit clicked events.
- Prioritize unit clicks over tile clicks when both are hit.

**Acceptance:**

- Clicking a unit identifies that unit and team.

**Validation:**

```bash
unity-ctrl --project "$PWD" editor refresh --compile
unity-ctrl --project "$PWD" console --type error --stacktrace full --lines 50
```

## 054 — Add selection state model

Status: DONE

**Commit:** `feat: add tactical selection state`

**Goal:** Store which unit, action, and cell the player is interacting with.

**Scope:** Runtime input/UI state.

**Tasks:**

- Add `SelectionState` or `SelectionController`.
- Track selected unit, hovered cell, selected action mode, selected target.
- Clear selection safely when unit dies or phase changes.

**Acceptance:**

- Other systems can query current selection without coupling to mouse input.

**Validation:**

```bash
unity-ctrl --project "$PWD" editor refresh --compile
unity-ctrl --project "$PWD" console --type error --stacktrace full --lines 50
```

## 055 — Add command input router

Status: DONE

**Commit:** `feat: add player command input router`

**Goal:** Turn UI and mouse input into high-level commands.

**Scope:** Runtime input.

**Tasks:**

- Add `PlayerCommandRouter`.
- Handle select unit, select move, select attack, confirm target, cancel, and end turn requests.
- Do not implement full command execution yet; route to events or stubs.

**Acceptance:**

- Input handling is centralized and ready for combat integration.

**Validation:**

```bash
unity-ctrl --project "$PWD" editor refresh --compile
unity-ctrl --project "$PWD" console --type error --stacktrace full --lines 50
```

## 056 — Add keyboard shortcuts

Status: DONE

**Commit:** `feat: add prototype keyboard shortcuts`

**Goal:** Speed up testing and playing.

**Scope:** Runtime input.

**Tasks:**

- Add shortcuts:
  - `M` for Move.
  - `1` for melee.
  - `2` for cone attack.
  - `3` for AoE.
  - `B` for Brace during reaction.
  - `Space` for Pass/End Turn depending on phase.
  - `Escape` for Cancel.
- Keep shortcuts documented in UI later.

**Acceptance:**

- Shortcuts call the same router methods as buttons.

**Validation:**

```bash
unity-ctrl --project "$PWD" editor refresh --compile
unity-ctrl --project "$PWD" console --type error --stacktrace full --lines 50
```

## 057 — Add hover debug overlay

Status: DONE

**Commit:** `feat: show hover grid debug info`

**Goal:** Help validate grid picking, heights, and occupancy.

**Scope:** Prototype UI/debug.

**Tasks:**

- Add a lightweight `OnGUI` debug panel or text UI.
- Show hovered cell, occupant name, walkable flag, height, and movement cost.
- Hide or compact when no cell is hovered.

**Acceptance:**

- Hovering terrain gives enough data to debug movement and targeting.

**Validation:**

```bash
unity-ctrl --project "$PWD" editor refresh --compile
unity-ctrl --project "$PWD" editor play --wait
unity-ctrl --project "$PWD" screenshot --view game --output_path /tmp/reaction-tactics-hover.png
unity-ctrl --project "$PWD" editor stop
```

## 058 — Add input scene setup tool

Status: DONE

**Commit:** `tooling: add input scene setup tool`

**Goal:** Attach camera, picker, selection, and input components consistently.

**Scope:** Editor CLI tool.

**Tasks:**

- Add `[UnityCliTool]` named `rt_setup_input_scene`.
- Ensure camera controller, grid picker, selection controller, and command router exist.
- Wire obvious serialized references.
- Save the scene.

**Acceptance:**

- Running setup after grid/unit setup creates a clickable prototype scene.

**Validation:**

```bash
unity-ctrl --project "$PWD" rt_setup_grid_scene
unity-ctrl --project "$PWD" rt_setup_units_scene
unity-ctrl --project "$PWD" rt_setup_input_scene
unity-ctrl --project "$PWD" rt_hierarchy
unity-ctrl --project "$PWD" menu "File/Save"
```

---

# Milestone 06 — Turn system and AP lifecycle

## 059 — Add combat phase enum and state object

Status: DONE

**Commit:** `feat: add combat phase state model`

**Goal:** Define the legal phases of the combat loop.

**Scope:** Runtime combat state.

**Tasks:**

- Add `CombatPhase` enum: `NotStarted`, `RoundStart`, `ActiveTurn`, `ActionTargeting`, `ReactionWindow`, `ResolvingAction`, `RoundEnd`, `CombatOver`.
- Add `CombatState` with current round, phase, active unit, reacting unit, and pending action intent.

**Acceptance:**

- Systems can make legal/illegal decisions based on explicit phase.

**Validation:**

```bash
unity-ctrl --project "$PWD" editor refresh --compile
unity-ctrl --project "$PWD" console --type error --stacktrace full --lines 50
```

## 060 — Add combat event bus

Status: DONE

**Commit:** `feat: add combat event bus`

**Goal:** Decouple UI, log, and combat systems.

**Scope:** Runtime events.

**Tasks:**

- Add events for round started, active unit changed, AP changed, HP changed, action declared, reaction turn started, action resolved, unit died, combat ended.
- Keep event payloads minimal and typed.
- Avoid global static state unless carefully scoped to the scene.

**Acceptance:**

- UI and logs can subscribe without combat manager hard dependencies.

**Validation:**

```bash
unity-ctrl --project "$PWD" editor refresh --compile
unity-ctrl --project "$PWD" console --type error --stacktrace full --lines 50
```

## 061 — Add initiative and active-turn order

Status: DONE

**Commit:** `feat: add active unit turn order`

**Goal:** Create deterministic active turns for the prototype.

**Scope:** Runtime turn manager.

**Tasks:**

- Add `TurnOrderService`.
- Sort living units by team then spawn/order index for prototype clarity.
- Provide current active unit and next active unit.
- Skip dead units.

**Acceptance:**

- The player can predict whose turn is next.

**Validation:**

```bash
unity-ctrl --project "$PWD" editor refresh --compile
unity-ctrl --project "$PWD" test --filter TurnOrderServiceTests
```

## 062 — Add round start AP refresh

Status: DONE

**Commit:** `feat: refresh action points at round start`

**Goal:** Implement the shared AP economy lifecycle.

**Scope:** Runtime turn manager/unit AP.

**Tasks:**

- At start of each round, refresh every living unit to max AP.
- Dead units should not matter.
- Emit AP changed events.
- Increment round counter.

**Acceptance:**

- Units regain AP once per round, not per individual turn.

**Validation:**

```bash
unity-ctrl --project "$PWD" editor refresh --compile
unity-ctrl --project "$PWD" test --filter RoundApRefreshTests
```

## 063 — Add combat manager shell

Status: DONE

**Commit:** `feat: add combat manager shell`

**Goal:** Own the high-level combat loop.

**Scope:** Runtime MonoBehaviour.

**Tasks:**

- Add `CombatManager : MonoBehaviour`.
- Reference `UnitRegistry`, `GridManager`, and input router.
- Initialize combat on play mode start.
- Enter round 1 and active turn for first unit.
- Expose read-only current combat state.

**Acceptance:**

- Play mode logs round start and active unit.
- No actions are implemented yet.

**Validation:**

```bash
unity-ctrl --project "$PWD" editor refresh --compile
unity-ctrl --project "$PWD" editor play --wait
unity-ctrl --project "$PWD" console --type error,warning,log --lines 50
unity-ctrl --project "$PWD" editor stop
```

## 064 — Add end turn behavior

Status: DONE

**Commit:** `feat: allow active units to end turn`

**Goal:** Let combat advance through units and rounds.

**Scope:** Runtime turn manager/input integration.

**Tasks:**

- Implement `EndActiveTurn` command.
- Advance to next living unit.
- When all living units have acted, enter next round and refresh AP.
- Wire Space key or debug button to end turn.

**Acceptance:**

- Player can cycle through active turns and rounds in play mode.

**Validation:**

```bash
unity-ctrl --project "$PWD" editor refresh --compile
unity-ctrl --project "$PWD" test --filter EndTurnTests
unity-ctrl --project "$PWD" console --type error --stacktrace full --lines 50
```

## 065 — Enforce actions only on active unit's turn

Status: DONE

**Commit:** `feat: restrict actions to active unit turn`

**Goal:** Prevent illegal active actions.

**Scope:** Combat command validation.

**Tasks:**

- Add validation method `CanUnitTakeAction(unit)`.
- Return false unless phase is active/action-targeting and unit is active unit.
- Add clear error messages for non-active units.

**Acceptance:**

- Clicking a non-active unit does not allow actions.
- Tests cover legal and illegal action users.

**Validation:**

```bash
unity-ctrl --project "$PWD" editor refresh --compile
unity-ctrl --project "$PWD" test --filter ActiveActionLegalityTests
```

## 066 — Add active unit visual highlight

Status: DONE

**Commit:** `feat: highlight active tactical unit`

**Goal:** Make turn ownership visible.

**Scope:** Runtime presentation.

**Tasks:**

- Add `UnitHighlightView` or extend unit view.
- Highlight active unit using simple ring/marker/material change.
- Clear highlight when active unit changes or dies.

**Acceptance:**

- Active unit is obvious in Game view.

**Validation:**

```bash
unity-ctrl --project "$PWD" editor refresh --compile
unity-ctrl --project "$PWD" editor play --wait
unity-ctrl --project "$PWD" screenshot --view game --output_path /tmp/reaction-tactics-active-unit.png
unity-ctrl --project "$PWD" editor stop
```

## 067 — Add turn system tests

Status: DONE

**Commit:** `test: cover active turns and round lifecycle`

**Goal:** Lock down basic turn rules.

**Scope:** EditMode tests.

**Tasks:**

- Test first active unit after combat starts.
- Test dead units are skipped.
- Test AP refresh happens at round start.
- Test non-active units cannot take actions.

**Acceptance:**

- Turn system tests pass.

**Validation:**

```bash
unity-ctrl --project "$PWD" test --filter Turn
```

---

# Milestone 07 — Ability and action intent system

## 068 — Add ability timing and shape enums

Status: DONE

**Commit:** `feat: add ability timing and shape enums`

**Goal:** Establish the vocabulary for actions and reactions.

**Scope:** Runtime ability model.

**Tasks:**

- Add `AbilityTiming { Immediate, Telegraphed }`.
- Add `AbilityShape { Self, SingleTarget, Melee, Cone, Radius }`.
- Add `AbilityUsage { Action, Reaction, Both }` or equivalent flags.
- Add `ActionResolutionMode` if needed.

**Acceptance:**

- Ability code can distinguish telegraphed attacks from future immediate skills.

**Validation:**

```bash
unity-ctrl --project "$PWD" editor refresh --compile
unity-ctrl --project "$PWD" console --type error --stacktrace full --lines 50
```

## 069 — Add ability definition ScriptableObject

Status: DONE

**Commit:** `feat: add ability definition asset type`

**Goal:** Store prototype ability data in assets.

**Scope:** Runtime ScriptableObject.

**Tasks:**

- Add `AbilityDefinition : ScriptableObject`.
- Fields: display name, AP cost, usage flags, timing, shape, range, radius, damage, triggersReactions, description.
- Include stable ability key/string ID.
- Add validation helpers for missing/invalid data.

**Acceptance:**

- Ability assets can represent move, melee, cone, AoE, brace, and pass.

**Validation:**

```bash
unity-ctrl --project "$PWD" editor refresh --compile
unity-ctrl --project "$PWD" console --type error --stacktrace full --lines 50
```

## 070 — Add action target model

Status: DONE

**Commit:** `feat: add action target model`

**Goal:** Represent targets consistently across melee, cone, and AoE.

**Scope:** Runtime ability/combat model.

**Tasks:**

- Add `ActionTarget` containing optional target unit, target cell, and direction.
- Add helpers for target-cell-only and unit-targeted actions.
- Validate that an action has the needed target data for its shape.

**Acceptance:**

- Ability declarations do not depend on UI selection objects directly.

**Validation:**

```bash
unity-ctrl --project "$PWD" editor refresh --compile
unity-ctrl --project "$PWD" test --filter ActionTargetTests
```

## 071 — Add action intent model

Status: DONE

**Commit:** `feat: add action intent model`

**Goal:** Represent a declared action before it resolves.

**Scope:** Runtime combat model.

**Tasks:**

- Add `ActionIntent` with actor, ability, origin position, target, declared affected cells, declared target unit, and declaration round/sequence number.
- Include `TriggersReactionWindow` derived from ability.
- Include `IsTelegraphed` derived from timing.
- Record actor position at declaration for reaction ordering and preview.

**Acceptance:**

- The combat manager can create an intent, run reactions, then resolve it later.

**Validation:**

```bash
unity-ctrl --project "$PWD" editor refresh --compile
unity-ctrl --project "$PWD" test --filter ActionIntentTests
```

## 072 — Add ability target validator interface

Status: DONE

**Commit:** `feat: add ability target validation interface`

**Goal:** Keep target legality separate from UI.

**Scope:** Runtime ability validation.

**Tasks:**

- Add `IAbilityTargetValidator` or service class.
- Validate AP, usage phase, range, target existence, target hostility/friendliness, and walkability where relevant.
- Return `TacticalResult` with clear failure messages.

**Acceptance:**

- Future UI can display why an action cannot be used.

**Validation:**

```bash
unity-ctrl --project "$PWD" editor refresh --compile
unity-ctrl --project "$PWD" console --type error --stacktrace full --lines 50
```

## 073 — Add action declaration service

Status: DONE

**Commit:** `feat: declare actions as intents`

**Goal:** Spend AP and create `ActionIntent`s for legal actions.

**Scope:** Runtime combat/ability service.

**Tasks:**

- Add `ActionDeclarationService`.
- Validate actor can use the ability as an action.
- Spend AP at declaration time.
- Create and return an `ActionIntent`.
- Do not resolve damage yet.

**Acceptance:**

- Declaring a legal action creates an intent and reduces AP.
- Illegal actions do not spend AP.

**Validation:**

```bash
unity-ctrl --project "$PWD" editor refresh --compile
unity-ctrl --project "$PWD" test --filter ActionDeclarationTests
```

## 074 — Add ability catalog component

Status: DONE

**Commit:** `feat: add ability catalog for prototype units`

**Goal:** Give units access to available action and reaction abilities.

**Scope:** Runtime ability model.

**Tasks:**

- Add `UnitAbilityLoadout` component or data field.
- Reference a list of `AbilityDefinition` assets.
- Provide methods `GetActionAbilities` and `GetReactionAbilities`.
- For prototype, allow manual assignment or setup tool assignment.

**Acceptance:**

- Each unit can advertise available actions/reactions to UI.

**Validation:**

```bash
unity-ctrl --project "$PWD" editor refresh --compile
unity-ctrl --project "$PWD" console --type error --stacktrace full --lines 50
```

## 075 — Add editor CLI tool to create default ability assets

Status: DONE

**Commit:** `tooling: add default ability asset creator`

**Goal:** Reproducibly create prototype abilities.

**Scope:** Editor CLI tool and ScriptableObject assets.

**Tasks:**

- Add `[UnityCliTool]` named `rt_create_default_abilities`.
- Create assets:
  - `Move` — action/both support handled by movement command, cost per tile.
  - `Melee Slash` — action, telegraphed, melee, cost 3, damage 4, triggers reactions.
  - `Cone Shot` — action, telegraphed, cone, range 4, cost 4, damage 3, triggers reactions.
  - `Fireball` — action, telegraphed, radius, range 5, radius 1 or 2, cost 5, damage 4, triggers reactions.
  - `Brace` — reaction, immediate, self, cost 2, reduces next incoming damage.
  - `Pass Reaction` — reaction, self, cost 0.
- Keep asset creation idempotent.

**Acceptance:**

- Ability assets exist under `Assets/ScriptableObjects/Abilities`.
- Re-running the tool updates rather than duplicates assets.

**Validation:**

```bash
unity-ctrl --project "$PWD" editor refresh --compile
unity-ctrl --project "$PWD" rt_create_default_abilities
unity-ctrl --project "$PWD" rt_find_assets --params '{"filter":"t:AbilityDefinition"}'
```

## 076 — Assign default abilities to units

Status: DONE

**Commit:** `tooling: assign prototype ability loadouts`

**Goal:** Give each default unit a sensible prototype loadout.

**Scope:** Editor setup tool and unit prefabs/scene instances.

**Tasks:**

- Update unit setup to assign ability loadouts.
- Suggested loadouts:
  - Knight: Move, Melee Slash, Brace, Pass Reaction.
  - Rogue: Move, Melee Slash, Brace, Pass Reaction.
  - Archer: Move, Cone Shot, Melee Slash, Brace, Pass Reaction.
  - Mage/Shaman: Move, Fireball, Melee Slash, Brace, Pass Reaction.
- Ensure enemies also have loadouts for AI.

**Acceptance:**

- In play mode, selected unit can list its abilities.

**Validation:**

```bash
unity-ctrl --project "$PWD" rt_create_default_abilities
unity-ctrl --project "$PWD" rt_setup_units_scene
unity-ctrl --project "$PWD" editor refresh --compile
unity-ctrl --project "$PWD" console --type error --stacktrace full --lines 50
```

## 077 — Add intent preview model

Status: DONE

**Commit:** `feat: add action intent preview data`

**Goal:** Provide UI with affected cells and threatened units before resolution.

**Scope:** Runtime ability/UI bridge.

**Tasks:**

- Add `ActionIntentPreview` with affected cells, threatened units, safe cells optional, invalid reason optional.
- Add `IntentPreviewService` that produces preview from actor, ability, and target.
- For now, support empty affected cells for unimplemented shapes.

**Acceptance:**

- UI can request preview without declaring/spending AP.

**Validation:**

```bash
unity-ctrl --project "$PWD" editor refresh --compile
unity-ctrl --project "$PWD" test --filter IntentPreviewTests
```

## 078 — Add action resolver shell

Status: DONE

**Commit:** `feat: add action resolver shell`

**Goal:** Create the system that will apply effects after reactions.

**Scope:** Runtime combat resolution.

**Tasks:**

- Add `ActionResolver` with `Resolve(ActionIntent intent)`.
- For now, log action name and actor.
- Include extensibility points for shape-specific resolution.
- Emit action resolved event.

**Acceptance:**

- Declared actions can pass through a resolver without damage yet.

**Validation:**

```bash
unity-ctrl --project "$PWD" editor refresh --compile
unity-ctrl --project "$PWD" console --type error --stacktrace full --lines 50
```

## 079 — Wire action declaration into combat manager

Status: DONE

**Commit:** `feat: wire declared actions into combat loop`

**Goal:** Let the active unit declare an action from input and have it resolve through the shell resolver.

**Scope:** Combat manager/input integration.

**Tasks:**

- Route selected action + target to `ActionDeclarationService`.
- Store pending intent in combat state.
- Enter `ResolvingAction` and call resolver.
- Return to active turn after resolution.
- Reactions are still not implemented in this ticket.

**Acceptance:**

- Using a prototype action logs declaration and resolution.
- AP is spent.
- Active unit keeps turn after action if it still has AP.

**Validation:**

```bash
unity-ctrl --project "$PWD" editor refresh --compile
unity-ctrl --project "$PWD" editor play --wait
unity-ctrl --project "$PWD" console --type error,warning,log --lines 80
unity-ctrl --project "$PWD" editor stop
```

## 080 — Add ability/action system tests

Status: DONE

**Commit:** `test: cover ability declarations and intent model`

**Goal:** Lock down declaration before real attack shapes.

**Scope:** EditMode tests.

**Tasks:**

- Test legal action spends AP and returns intent.
- Test insufficient AP does not spend AP.
- Test reactions cannot be declared as active actions.
- Test non-active units cannot declare active actions.
- Test preview does not spend AP.

**Acceptance:**

- Ability declaration tests pass.

**Validation:**

```bash
unity-ctrl --project "$PWD" test --filter AbilityDeclaration
```

---

# Milestone 08 — Attack shapes and deterministic resolution

## 081 — Implement melee targeting validation

Status: DONE

**Commit:** `feat: validate melee attack targets`

**Goal:** Make melee attacks target adjacent hostile units.

**Scope:** Ability validation.

**Tasks:**

- For `AbilityShape.Melee`, require hostile target unit.
- Validate target is in melee range at declaration.
- Use actor stats melee range, default 1 grid step.
- Do not roll hit chance.

**Acceptance:**

- Melee can only be declared against a hostile unit in range.

**Validation:**

```bash
unity-ctrl --project "$PWD" editor refresh --compile
unity-ctrl --project "$PWD" test --filter MeleeTargetValidationTests
```

## 082 — Implement Option A melee resolution

Status: DONE

**Commit:** `feat: resolve melee after reactions by final range`

**Goal:** Implement the requested melee timing.

**Scope:** Action resolver.

**Tasks:**

- At declaration, melee requires target in range.
- At resolution, re-check whether target is alive and still in melee range.
- If target is still in range, apply damage automatically.
- If target moved out of range, log a positional miss/avoid, not a dodge.
- No random accuracy or dodge code.

**Acceptance:**

- Melee always hits when target remains in range at resolution.
- Melee does not hit if target physically moved out of range before resolution.

**Validation:**

```bash
unity-ctrl --project "$PWD" editor refresh --compile
unity-ctrl --project "$PWD" test --filter OptionAMeleeResolutionTests
```

## 083 — Add melee attack presentation placeholder

Status: DONE

**Commit:** `feat: add melee attack presentation placeholder`

**Goal:** Make melee resolution visible enough for prototype testing.

**Scope:** Runtime presentation/logs.

**Tasks:**

- Add a simple facing turn toward target.
- Add a short lunge or weaponless animation placeholder if easy.
- Log hit or avoided outcome to combat log.
- Do not block on final animation quality.

**Acceptance:**

- Player can tell when melee attack resolved and whether it hit.

**Validation:**

```bash
unity-ctrl --project "$PWD" editor refresh --compile
unity-ctrl --project "$PWD" editor play --wait
unity-ctrl --project "$PWD" console --type error,warning,log --lines 80
unity-ctrl --project "$PWD" editor stop
```

## 084 — Implement radius AoE shape service

Status: DONE

**Commit:** `feat: calculate radius aoe affected cells`

**Goal:** Support Fireball-style area attacks.

**Scope:** Targeting shapes.

**Tasks:**

- Add `AreaShapeService.GetRadiusCells(center, radius, map)`.
- Use horizontal Manhattan radius for prototype clarity.
- Include cells at their actual terrain height.
- Ignore blocked cells for initial version unless out of map.

**Acceptance:**

- Radius shape returns deterministic cells on stepped terrain.

**Validation:**

```bash
unity-ctrl --project "$PWD" editor refresh --compile
unity-ctrl --project "$PWD" test --filter RadiusShapeTests
```

## 085 — Implement AoE targeting validation

Status: DONE

**Commit:** `feat: validate aoe ability targets`

**Goal:** Allow Fireball to target cells within range.

**Scope:** Ability validation.

**Tasks:**

- For radius abilities, require target cell.
- Validate target cell exists and is within ability range from actor.
- Allow targeting empty cells.
- Produce affected cell preview.

**Acceptance:**

- Fireball can be declared at valid cells and rejected out of range.

**Validation:**

```bash
unity-ctrl --project "$PWD" editor refresh --compile
unity-ctrl --project "$PWD" test --filter AoeTargetValidationTests
```

## 086 — Implement AoE resolution after reactions

Status: DONE

**Commit:** `feat: resolve aoe attacks against final positions`

**Goal:** Apply AoE damage only to units still inside the AoE after reactions.

**Scope:** Action resolver.

**Tasks:**

- Resolve radius ability using declared affected cells.
- At resolution, check every living unit's current grid position.
- Apply damage to units inside affected cells.
- Units that moved out take no damage.
- Log affected and avoided units.

**Acceptance:**

- Movement out of the AoE is the dodge.
- No hit chance is used.

**Validation:**

```bash
unity-ctrl --project "$PWD" editor refresh --compile
unity-ctrl --project "$PWD" test --filter AoeResolutionTests
```

## 087 — Implement cardinal cone shape service

Status: DONE

**Commit:** `feat: calculate cardinal cone affected cells`

**Goal:** Support ranged cone attacks that can be avoided by stepping out of shape.

**Scope:** Targeting shapes.

**Tasks:**

- Add `AreaShapeService.GetConeCells(origin, direction, range, map)`.
- Use cardinal direction from origin to target.
- Make cone widen by distance, e.g. width 0 at distance 1, width 1 at distance 2-3, width 2 at distance 4+.
- Include cells at actual terrain heights.
- Exclude actor's own cell.

**Acceptance:**

- Cone shape is deterministic and readable on an 8x8 map.

**Validation:**

```bash
unity-ctrl --project "$PWD" editor refresh --compile
unity-ctrl --project "$PWD" test --filter ConeShapeTests
```

## 088 — Implement cone targeting validation

Status: DONE

**Commit:** `feat: validate cone attack targets`

**Goal:** Let Cone Shot choose a direction and range.

**Scope:** Ability validation/preview.

**Tasks:**

- For cone abilities, require target cell.
- Determine cardinal direction from actor origin to target cell.
- Validate target cell is not actor cell and is within range.
- Produce affected cells for preview.

**Acceptance:**

- Cone Shot can be aimed by clicking a cell in the desired direction.

**Validation:**

```bash
unity-ctrl --project "$PWD" editor refresh --compile
unity-ctrl --project "$PWD" test --filter ConeTargetValidationTests
```

## 089 — Implement cone resolution after reactions

Status: DONE

**Commit:** `feat: resolve cone attacks against final positions`

**Goal:** Apply cone damage only to units still inside the declared cone after reactions.

**Scope:** Action resolver.

**Tasks:**

- Resolve cone ability using declared affected cells.
- At resolution, check every living hostile unit's current position.
- Decide whether friendly fire is enabled for prototype; document and implement consistently.
- Log hit and avoided units.

**Acceptance:**

- Units that move out of the cone before resolution avoid damage.
- No dodge/accuracy rolls exist.

**Validation:**

```bash
unity-ctrl --project "$PWD" editor refresh --compile
unity-ctrl --project "$PWD" test --filter ConeResolutionTests
```

## 090 — Add simple line-of-sight service

Status: DONE

**Commit:** `feat: add simple grid line of sight service`

**Goal:** Support ranged readability without overbuilding.

**Scope:** Runtime targeting.

**Tasks:**

- Add `LineOfSightService` using a simple grid line between origin and target.
- Respect `blocksLineOfSight` cells.
- For prototype, use line of sight for target validation and optionally for cone preview.
- Keep vertical rules simple and documented.

**Acceptance:**

- Blockers can prevent ranged targeting through them.

**Validation:**

```bash
unity-ctrl --project "$PWD" editor refresh --compile
unity-ctrl --project "$PWD" test --filter LineOfSightTests
```

## 091 — Integrate line of sight into ranged validation

Status: DONE

**Commit:** `feat: require line of sight for ranged declarations`

**Goal:** Make terrain matter for ranged actions.

**Scope:** Ability validation.

**Tasks:**

- Require LoS from actor to target cell for cone and AoE declarations, unless ability has `ignoresLineOfSight` future flag.
- Display validation failure when blocked.
- Do not retroactively change declared AoE after reactions.

**Acceptance:**

- Ranged attacks cannot be declared through sight blockers.

**Validation:**

```bash
unity-ctrl --project "$PWD" editor refresh --compile
unity-ctrl --project "$PWD" test --filter RangedLineOfSightValidationTests
```

## 092 — Add deterministic damage event data

Status: DONE

**Commit:** `feat: add deterministic damage event data`

**Goal:** Make damage resolution visible and testable.

**Scope:** Runtime combat events.

**Tasks:**

- Add `DamageEvent` with source intent, attacker, target, amount, wasBraced, finalAmount.
- Emit event when damage is applied.
- Include no fields for hit chance, accuracy roll, or dodge roll.

**Acceptance:**

- Combat log can explain exactly why damage happened.

**Validation:**

```bash
unity-ctrl --project "$PWD" editor refresh --compile
unity-ctrl --project "$PWD" test --filter DamageEventTests
```

## 093 — Add attack shape integration tests

Status: DONE

**Commit:** `test: cover deterministic attack shape resolution`

**Goal:** Confirm all prototype attacks resolve based on final positions.

**Scope:** EditMode tests.

**Tasks:**

- Test melee target moved out before resolution avoids hit.
- Test melee target still adjacent is hit.
- Test cone target moved out avoids hit.
- Test AoE target moved out avoids hit.
- Test no resolver path uses random hit chance.

**Acceptance:**

- Shape resolution tests pass and document the core design identity.

**Validation:**

```bash
unity-ctrl --project "$PWD" test --filter DeterministicAttackResolution
```

---

# Milestone 09 — Reaction windows

## 094 — Add reaction window model

Status: DONE

**Commit:** `feat: add reaction window model`

**Goal:** Represent the off-turn response phase triggered by an action.

**Scope:** Runtime reactions.

**Tasks:**

- Add `ReactionWindow` with source `ActionIntent`, ordered reactors, current index, completed reactors, and skipped reactors.
- Store action actor position at declaration for ordering.
- Include phase transitions: opened, reactor started, reactor completed, closed.

**Acceptance:**

- A pending action can own an explicit reaction window before resolution.

**Validation:**

```bash
unity-ctrl --project "$PWD" editor refresh --compile
unity-ctrl --project "$PWD" console --type error --stacktrace full --lines 50
```

## 095 — Implement reaction distance ordering

Status: DONE

**Commit:** `feat: order reaction turns by distance from actor`

**Goal:** Apply the core reaction-order rule.

**Scope:** Runtime reactions.

**Tasks:**

- Add `ReactionOrderService`.
- Input: action actor, all units, source intent.
- Exclude actor and dead units.
- Sort by tactical distance from action actor's declaration position.
- Add deterministic tie-breakers by unit ID or turn order.

**Acceptance:**

- Every other living character is considered in sorted order.

**Validation:**

```bash
unity-ctrl --project "$PWD" editor refresh --compile
unity-ctrl --project "$PWD" test --filter ReactionOrderServiceTests
```

## 096 — Add reaction eligibility validation

Status: DONE

**Commit:** `feat: validate reaction eligibility`

**Goal:** Determine whether a unit may take a reaction turn.

**Scope:** Runtime reactions.

**Tasks:**

- Add `CanUnitReact(unit, intent)`.
- Require not actor, alive, not currently active action unit, phase reaction window, and AP available or zero-cost pass.
- Add hook for status effects to disable reactions later.
- Return clear reasons for auto-pass.

**Acceptance:**

- Invalid reactors are skipped without breaking the reaction window.

**Validation:**

```bash
unity-ctrl --project "$PWD" editor refresh --compile
unity-ctrl --project "$PWD" test --filter ReactionEligibilityTests
```

## 097 — Wire reaction window before action resolution

Status: DONE

**Commit:** `feat: open reaction window before resolving actions`

**Goal:** Change action flow to declaration → reactions → resolution.

**Scope:** Combat manager.

**Tasks:**

- After a declared action that triggers reactions, create a `ReactionWindow`.
- Enter `CombatPhase.ReactionWindow`.
- Iterate reactors in order.
- For now, auto-pass each reactor.
- Resolve original action only after window closes.

**Acceptance:**

- Action resolution is delayed until all reactors have had a reaction turn.
- Console/log shows reaction order.

**Validation:**

```bash
unity-ctrl --project "$PWD" editor refresh --compile
unity-ctrl --project "$PWD" test --filter ReactionWindowFlowTests
unity-ctrl --project "$PWD" console --type error --stacktrace full --lines 50
```

## 098 — Add pass reaction command

Status: DONE

**Commit:** `feat: add pass reaction command`

**Goal:** Let a reactor explicitly do nothing.

**Scope:** Runtime reactions/input.

**Tasks:**

- Add `PassReactionCommand` or equivalent.
- Make it legal only during the unit's reaction turn.
- Cost 0 AP.
- Mark current reactor complete and advance window.

**Acceptance:**

- Player can pass reaction with button or Space.

**Validation:**

```bash
unity-ctrl --project "$PWD" editor refresh --compile
unity-ctrl --project "$PWD" test --filter PassReactionTests
```

## 099 — Add reaction movement command

Status: DONE

**Commit:** `feat: allow movement as a reaction`

**Goal:** Implement the main off-turn avoidance mechanic.

**Scope:** Runtime movement/reactions.

**Tasks:**

- Add `ReactionMoveCommand` using existing pathfinding.
- Legal only for current reacting unit during reaction window.
- Spend AP per movement path cost.
- Update registry occupancy and unit grid position.
- Finish reactor turn after movement for prototype simplicity.

**Acceptance:**

- A reacting unit can move to a reachable cell and spend AP.

**Validation:**

```bash
unity-ctrl --project "$PWD" editor refresh --compile
unity-ctrl --project "$PWD" test --filter ReactionMoveCommandTests
```

## 100 — Add active movement command

Status: DONE

**Commit:** `feat: allow active movement on unit turn`

**Goal:** Let active units reposition on their own turns using the same movement system.

**Scope:** Runtime movement/turn manager.

**Tasks:**

- Add `ActiveMoveCommand`.
- Legal only for active unit during active turn.
- Spend AP per path cost.
- Do not open a reaction window for simple movement in this prototype unless explicitly enabled later.
- Return to active turn after movement.

**Acceptance:**

- Active unit can move and continue acting if it has AP.

**Validation:**

```bash
unity-ctrl --project "$PWD" editor refresh --compile
unity-ctrl --project "$PWD" test --filter ActiveMoveCommandTests
```

## 101 — Add safe cell analyzer for pending intent

Status: DONE

**Commit:** `feat: classify safe reaction movement cells`

**Goal:** Show which reachable cells avoid the pending action.

**Scope:** Runtime reactions/preview.

**Tasks:**

- Add `ReactionSafetyAnalyzer`.
- Given reactor, pending intent, and reachable cells, classify each destination as safe or threatened.
- For melee target, safe means outside actor melee range at resolution.
- For cone and AoE, safe means destination not inside affected cells.
- Include reason strings for UI tooltips.

**Acceptance:**

- Reaction UI can show movement as avoidance, not random dodge.

**Validation:**

```bash
unity-ctrl --project "$PWD" editor refresh --compile
unity-ctrl --project "$PWD" test --filter ReactionSafetyAnalyzerTests
```

## 102 — Add brace reaction state

Status: DONE

**Commit:** `feat: add brace reaction damage reduction`

**Goal:** Give units a useful reaction when they cannot escape.

**Scope:** Runtime reactions/damage resolution.

**Tasks:**

- Add `BracedUntilNextHit` or `DefenseState` on units.
- Brace costs 2 AP by default.
- Brace is legal only during the unit's reaction turn.
- Reduce next incoming damage by a fixed amount or percentage for prototype.
- Clear brace after damage or after pending action resolves.

**Acceptance:**

- Bracing reduces deterministic damage and appears in combat log.

**Validation:**

```bash
unity-ctrl --project "$PWD" editor refresh --compile
unity-ctrl --project "$PWD" test --filter BraceReactionTests
```

## 103 — Integrate brace into resolver

Status: DONE

**Commit:** `feat: apply brace during action resolution`

**Goal:** Ensure braced units take reduced damage from the pending action.

**Scope:** Action resolver/damage.

**Tasks:**

- Before applying damage, check target defense state.
- Apply reduction and emit `DamageEvent` with braced fields.
- Clear brace after use.
- Ensure units that avoid the shape do not consume brace unless desired; document chosen rule.

**Acceptance:**

- Brace has observable effect in melee, cone, and AoE damage.

**Validation:**

```bash
unity-ctrl --project "$PWD" editor refresh --compile
unity-ctrl --project "$PWD" test --filter BraceResolutionTests
```

## 104 — Prevent nested full reaction windows

Status: DONE

**Commit:** `feat: prevent nested reaction windows`

**Goal:** Keep prototype pacing manageable and avoid reaction recursion.

**Scope:** Combat manager/reaction rules.

**Tasks:**

- Add rule: actions trigger reactions; reactions do not trigger full reaction windows.
- Ensure reaction movement does not open another reaction window.
- Add explicit guard if a reaction command tries to declare an action that triggers reactions.
- Leave extension point for future special reactions.

**Acceptance:**

- No reaction command can recursively start a full reaction window.

**Validation:**

```bash
unity-ctrl --project "$PWD" editor refresh --compile
unity-ctrl --project "$PWD" test --filter NoNestedReactionWindowTests
```

## 105 — Add reaction UI state integration

Status: DONE

**Commit:** `feat: expose current reactor to input and ui`

**Goal:** Allow the player to control the current reacting unit.

**Scope:** Combat manager/input/UI bridge.

**Tasks:**

- Expose current reacting unit in `CombatState`.
- Highlight the current reactor separately from active actor.
- Input router should send reaction commands only for current reactor.
- Disallow selecting another unit to react out of order.

**Acceptance:**

- During reaction window, the player clearly controls the correct unit.

**Validation:**

```bash
unity-ctrl --project "$PWD" editor refresh --compile
unity-ctrl --project "$PWD" console --type error --stacktrace full --lines 50
```

## 106 — Auto-pass reactors with no meaningful reaction

Status: DONE

**Commit:** `feat: auto pass reactors without useful reactions`

**Goal:** Improve pacing when units cannot act.

**Scope:** Reaction manager.

**Tasks:**

- Auto-pass dead, incapacitated, or AP-empty units.
- Auto-pass AI/player reactors if there are no legal reaction commands other than pass.
- Log auto-pass reason in debug mode.

**Acceptance:**

- Reaction windows do not stall on units that cannot do anything.

**Validation:**

```bash
unity-ctrl --project "$PWD" editor refresh --compile
unity-ctrl --project "$PWD" test --filter ReactionAutoPassTests
```

## 107 — Add reaction system tests

Status: DONE

**Commit:** `test: cover reaction windows and movement avoidance`

**Goal:** Verify the core unique mechanic.

**Scope:** EditMode tests.

**Tasks:**

- Test every other living unit receives a reaction turn.
- Test order by distance from action actor.
- Test pass reaction advances to next reactor.
- Test reaction move spends AP and updates occupancy.
- Test reaction movement can avoid melee, cone, and AoE.
- Test original action resolves only after window closes.

**Acceptance:**

- Reaction tests document the game loop.

**Validation:**

```bash
unity-ctrl --project "$PWD" test --filter Reaction
```

---

# Milestone 10 — Player UI and tactical feedback

## 108 — Add combat HUD shell

Status: DONE

**Commit:** `feat: add prototype combat hud shell`

**Goal:** Show essential combat state in play mode.

**Scope:** Runtime UI.

**Tasks:**

- Add lightweight `CombatHud` using UGUI or `OnGUI` for speed.
- Show current round, phase, active unit, current reactor, and pending action.
- Keep UI implementation simple and disposable.

**Acceptance:**

- Player can understand current phase without console logs.

**Validation:**

```bash
unity-ctrl --project "$PWD" editor refresh --compile
unity-ctrl --project "$PWD" editor play --wait
unity-ctrl --project "$PWD" screenshot --view game --output_path /tmp/reaction-tactics-hud.png
unity-ctrl --project "$PWD" editor stop
```

## 109 — Add unit HP/AP nameplates

Status: DONE

**Commit:** `feat: show unit hp and ap plates`

**Goal:** Make resource state visible above units or in HUD.

**Scope:** Runtime UI/presentation.

**Tasks:**

- Add `UnitStatusView` for HP/AP/name/team.
- Update when AP or HP events fire.
- Hide or mark dead units clearly.
- Keep labels readable from tactical camera.

**Acceptance:**

- Player can see which units have AP saved for reactions.

**Validation:**

```bash
unity-ctrl --project "$PWD" editor refresh --compile
unity-ctrl --project "$PWD" editor play --wait
unity-ctrl --project "$PWD" screenshot --view game --output_path /tmp/reaction-tactics-unit-status.png
unity-ctrl --project "$PWD" editor stop
```

## 110 — Add active action menu

Status: DONE

**Commit:** `feat: add active action menu`

**Goal:** Let players choose active-turn actions without relying on hotkeys.

**Scope:** Runtime UI/input.

**Tasks:**

- List selected active unit's action abilities.
- Include Move and End Turn controls.
- Disable buttons when AP or phase makes an action illegal.
- Show AP costs.

**Acceptance:**

- Player can move, melee, cone, AoE, and end turn through UI.

**Validation:**

```bash
unity-ctrl --project "$PWD" editor refresh --compile
unity-ctrl --project "$PWD" editor play --wait
unity-ctrl --project "$PWD" screenshot --view game --output_path /tmp/reaction-tactics-action-menu.png
unity-ctrl --project "$PWD" editor stop
```

## 111 — Add reaction menu

Status: DONE

**Commit:** `feat: add reaction menu`

**Goal:** Let players choose reactions during reaction turns.

**Scope:** Runtime UI/input.

**Tasks:**

- During `ReactionWindow`, show current reacting unit's legal reaction options.
- Include Reaction Move, Brace, and Pass.
- Disable options when AP is insufficient.
- Display pending action name and actor.

**Acceptance:**

- Player can complete each reaction turn from UI.

**Validation:**

```bash
unity-ctrl --project "$PWD" editor refresh --compile
unity-ctrl --project "$PWD" editor play --wait
unity-ctrl --project "$PWD" screenshot --view game --output_path /tmp/reaction-tactics-reaction-menu.png
unity-ctrl --project "$PWD" editor stop
```

## 112 — Add tile highlight manager

Status: DONE

**Commit:** `feat: add grid tile highlight manager`

**Goal:** Centralize visual overlays for move ranges, danger areas, and safe cells.

**Scope:** Runtime presentation.

**Tasks:**

- Add `GridHighlightManager`.
- Support clear all, highlight cells by category, and set hover path.
- Categories: movement range, selected path, action danger, reaction safe, reaction threatened, target cell.
- Use material swaps or overlay primitives.

**Acceptance:**

- Multiple systems can highlight tiles without fighting each other.

**Validation:**

```bash
unity-ctrl --project "$PWD" editor refresh --compile
unity-ctrl --project "$PWD" console --type error --stacktrace full --lines 50
```

## 113 — Show active movement range

Status: DONE

**Commit:** `feat: show active movement range`

**Goal:** Make active movement understandable.

**Scope:** UI/presentation/input.

**Tasks:**

- When active unit selects Move, show reachable cells from current AP.
- On hover, show selected path and AP cost.
- Clicking reachable destination executes active move.
- Clicking unreachable destination shows reason or does nothing clearly.

**Acceptance:**

- Player can use movement without console logs.

**Validation:**

```bash
unity-ctrl --project "$PWD" editor refresh --compile
unity-ctrl --project "$PWD" editor play --wait
unity-ctrl --project "$PWD" screenshot --view game --output_path /tmp/reaction-tactics-active-move-range.png
unity-ctrl --project "$PWD" editor stop
```

## 114 — Show action danger previews

Status: DONE

**Commit:** `feat: show danger cells for selected actions`

**Goal:** Preview melee, cone, and AoE before declaration.

**Scope:** UI/presentation/input.

**Tasks:**

- When selected action is melee, highlight target enemy if valid.
- When selected action is cone, highlight cone cells from hovered target cell.
- When selected action is AoE, highlight radius cells from hovered target cell.
- Mark currently threatened units.

**Acceptance:**

- Player knows what cells the action will threaten before confirming.

**Validation:**

```bash
unity-ctrl --project "$PWD" editor refresh --compile
unity-ctrl --project "$PWD" editor play --wait
unity-ctrl --project "$PWD" screenshot --view game --output_path /tmp/reaction-tactics-danger-preview.png
unity-ctrl --project "$PWD" editor stop
```

## 115 — Show reaction movement safety

Status: DONE

**Commit:** `feat: show safe and threatened reaction destinations`

**Goal:** Communicate movement-as-dodge clearly.

**Scope:** UI/presentation/reactions.

**Tasks:**

- During reaction move selection, compute reachable cells with current AP.
- Classify reachable cells with `ReactionSafetyAnalyzer`.
- Highlight safe and still-threatened destinations differently.
- Show why a cell is safe or unsafe in hover tooltip/debug panel.

**Acceptance:**

- Player can see how to physically avoid melee, cones, and AoEs.

**Validation:**

```bash
unity-ctrl --project "$PWD" editor refresh --compile
unity-ctrl --project "$PWD" editor play --wait
unity-ctrl --project "$PWD" screenshot --view game --output_path /tmp/reaction-tactics-reaction-safety.png
unity-ctrl --project "$PWD" editor stop
```

## 116 — Add target confirmation and cancel flow

Status: DONE

**Commit:** `feat: add targeting confirm and cancel flow`

**Goal:** Prevent accidental declarations.

**Scope:** UI/input.

**Tasks:**

- Clicking a valid target enters a confirmation state or declares immediately if simple; choose and document one behavior.
- `Escape` cancels selected action/reaction mode.
- UI shows current selected action and AP cost.
- Clear highlights when canceled or resolved.

**Acceptance:**

- Player can recover from wrong targeting choices.

**Validation:**

```bash
unity-ctrl --project "$PWD" editor refresh --compile
unity-ctrl --project "$PWD" console --type error --stacktrace full --lines 50
```

## 117 — Add combat log UI

Status: DONE

**Commit:** `feat: add combat log ui`

**Goal:** Explain deterministic outcomes to the player.

**Scope:** Runtime UI/events.

**Tasks:**

- Add a scrollable or fixed-size combat log panel.
- Log action declaration, reaction order, reaction choices, hits, avoided attacks, brace reductions, deaths, and round starts.
- Use plain language: "Rogue avoided Melee Slash by moving out of range".

**Acceptance:**

- Every hit or avoid can be explained from log text.

**Validation:**

```bash
unity-ctrl --project "$PWD" editor refresh --compile
unity-ctrl --project "$PWD" editor play --wait
unity-ctrl --project "$PWD" screenshot --view game --output_path /tmp/reaction-tactics-combat-log.png
unity-ctrl --project "$PWD" editor stop
```

## 118 — Add win/loss state UI

Status: DONE

**Commit:** `feat: show combat win and loss states`

**Goal:** Give the prototype a clear endpoint.

**Scope:** Runtime combat/UI.

**Tasks:**

- Detect when one team has no living units.
- Enter `CombatOver` phase.
- Disable further actions/reactions.
- Show Victory or Defeat panel.
- Add Restart Scene button or hotkey if easy.

**Acceptance:**

- Combat ends cleanly when one side is defeated.

**Validation:**

```bash
unity-ctrl --project "$PWD" editor refresh --compile
unity-ctrl --project "$PWD" test --filter CombatEndTests
```

## 119 — Add rules tooltip/help overlay

Status: DONE

**Commit:** `docs: add in game prototype rules overlay`

**Goal:** Explain the unusual reaction system inside the prototype.

**Scope:** UI/docs.

**Tasks:**

- Add help overlay toggled by `H`.
- Include concise rules:
  - Actions only on your turn.
  - Reactions only off-turn.
  - Every action triggers reactions by distance.
  - Melee hits if target remains in range at resolution.
  - Move out of cones/AoEs to avoid them.
  - No dodge chance.

**Acceptance:**

- A new tester can understand the main mechanics from inside the build.

**Validation:**

```bash
unity-ctrl --project "$PWD" editor refresh --compile
unity-ctrl --project "$PWD" editor play --wait
unity-ctrl --project "$PWD" screenshot --view game --output_path /tmp/reaction-tactics-help.png
unity-ctrl --project "$PWD" editor stop
```

---

# Milestone 11 — AI for solo playtesting

## 120 — Add AI controller shell

Status: DONE

**Commit:** `feat: add enemy ai controller shell`

**Goal:** Let enemy units act without player input.

**Scope:** Runtime AI.

**Tasks:**

- Add `AiController : MonoBehaviour`.
- Combat manager delegates enemy active turns and enemy reactions to AI.
- AI methods can initially pass.
- Keep AI deterministic for tests.

**Acceptance:**

- Enemy turns do not require player input.

**Validation:**

```bash
unity-ctrl --project "$PWD" editor refresh --compile
unity-ctrl --project "$PWD" console --type error --stacktrace full --lines 50
```

## 121 — Implement AI target selection

Status: DONE

**Commit:** `feat: select nearest hostile target for ai`

**Goal:** Give AI a deterministic target.

**Scope:** Runtime AI.

**Tasks:**

- Select nearest living hostile unit by tactical distance.
- Tie-break by lowest HP, then unit ID.
- Expose method for tests.

**Acceptance:**

- AI consistently chooses a sensible target.

**Validation:**

```bash
unity-ctrl --project "$PWD" editor refresh --compile
unity-ctrl --project "$PWD" test --filter AiTargetSelectionTests
```

## 122 — Implement AI active movement toward target

Status: DONE

**Commit:** `feat: move ai units toward nearest target`

**Goal:** Prevent enemy turns from doing nothing when out of range.

**Scope:** Runtime AI/movement.

**Tasks:**

- If no valid attack is available, find reachable cell that minimizes distance to target.
- Spend a conservative AP amount, leaving at least 1 or 2 AP for reactions if possible.
- Move using active movement command.

**Acceptance:**

- Enemy units advance toward players.

**Validation:**

```bash
unity-ctrl --project "$PWD" editor refresh --compile
unity-ctrl --project "$PWD" test --filter AiMovementTests
```

## 123 — Implement AI melee action

Status: DONE

**Commit:** `feat: let ai use melee attacks`

**Goal:** Allow adjacent enemies to attack.

**Scope:** Runtime AI/actions.

**Tasks:**

- If a hostile target is in melee range and AI has AP, declare Melee Slash.
- Use the same action declaration flow as player actions.
- Let reactions occur normally.

**Acceptance:**

- Enemy melee attacks trigger player reaction windows.

**Validation:**

```bash
unity-ctrl --project "$PWD" editor refresh --compile
unity-ctrl --project "$PWD" test --filter AiMeleeActionTests
```

## 124 — Implement AI cone action

Status: DONE

**Commit:** `feat: let ai use cone attacks when valuable`

**Goal:** Make ranged enemy behavior exercise cone reactions.

**Scope:** Runtime AI/actions.

**Tasks:**

- Evaluate available cardinal cone directions.
- Prefer cone if it threatens at least one hostile unit and does not hit more friendlies than hostiles, based on chosen friendly-fire rule.
- Declare Cone Shot through normal action flow.

**Acceptance:**

- Enemy cone attacks create reaction opportunities for the player.

**Validation:**

```bash
unity-ctrl --project "$PWD" editor refresh --compile
unity-ctrl --project "$PWD" test --filter AiConeActionTests
```

## 125 — Implement AI AoE action

Status: DONE

**Commit:** `feat: let ai use aoe attacks when valuable`

**Goal:** Exercise AoE reaction movement in solo play.

**Scope:** Runtime AI/actions.

**Tasks:**

- Evaluate target cells near hostile units within range and line of sight.
- Prefer AoE if it threatens at least one hostile and has acceptable friendly-fire score.
- Declare Fireball through normal action flow.

**Acceptance:**

- Enemy AoEs force player reaction movement or brace choices.

**Validation:**

```bash
unity-ctrl --project "$PWD" editor refresh --compile
unity-ctrl --project "$PWD" test --filter AiAoeActionTests
```

## 126 — Implement AI reaction movement

Status: DONE

**Commit:** `feat: let ai move out of danger as reaction`

**Goal:** Make the reaction system symmetrical.

**Scope:** Runtime AI/reactions.

**Tasks:**

- During enemy reaction turn, compute safe reachable cells.
- If currently threatened by pending action, move to the lowest-cost safe cell that improves position.
- If not threatened, pass or conserve AP.

**Acceptance:**

- Enemies can avoid player cone/AoE/melee by moving.

**Validation:**

```bash
unity-ctrl --project "$PWD" editor refresh --compile
unity-ctrl --project "$PWD" test --filter AiReactionMoveTests
```

## 127 — Implement AI brace reaction

Status: DONE

**Commit:** `feat: let ai brace when it cannot escape`

**Goal:** Give AI a fallback defensive reaction.

**Scope:** Runtime AI/reactions.

**Tasks:**

- If threatened and no safe reachable cell exists, use Brace if AP allows.
- Otherwise pass.
- Log AI reaction decision reason.

**Acceptance:**

- Enemy behavior demonstrates both movement avoidance and brace.

**Validation:**

```bash
unity-ctrl --project "$PWD" editor refresh --compile
unity-ctrl --project "$PWD" test --filter AiBraceReactionTests
```

## 128 — Add AI pacing delay

Status: DONE

**Commit:** `feat: add small ai action pacing delay`

**Goal:** Make enemy behavior readable in play mode.

**Scope:** Runtime AI/presentation.

**Tasks:**

- Add configurable delay before AI confirms action/reaction.
- Keep delay short and skippable in tests.
- Do not block editor test execution.

**Acceptance:**

- AI actions are observable without feeling stuck.

**Validation:**

```bash
unity-ctrl --project "$PWD" editor refresh --compile
unity-ctrl --project "$PWD" console --type error --stacktrace full --lines 50
```

## 129 — Add AI integration tests

Status: DONE

**Commit:** `test: cover ai active and reaction decisions`

**Goal:** Ensure AI does not break the combat loop.

**Scope:** EditMode tests.

**Tasks:**

- Test AI picks nearest target.
- Test AI moves toward target when no attack is available.
- Test AI declares attacks through normal intent flow.
- Test AI reaction movement avoids pending action when possible.
- Test AI brace fallback.

**Acceptance:**

- AI tests pass deterministically.

**Validation:**

```bash
unity-ctrl --project "$PWD" test --filter Ai
```

---

# Milestone 12 — Scenario content and scene bootstrapping

## 130 — Add scenario definition ScriptableObject

Status: DONE

**Commit:** `feat: add scenario definition asset type`

**Goal:** Store map, units, and starting positions as data.

**Scope:** Runtime ScriptableObject.

**Tasks:**

- Add `ScenarioDefinition : ScriptableObject`.
- Fields: map definition, list of unit entries, team, stats, starting cell, optional ability loadout.
- Add validation for duplicate spawn cells and invalid map cells.

**Acceptance:**

- A complete battle setup can be described without manually editing the scene.

**Validation:**

```bash
unity-ctrl --project "$PWD" editor refresh --compile
unity-ctrl --project "$PWD" test --filter ScenarioDefinitionTests
```

## 131 — Add default scenario asset creator

Status: DONE

**Commit:** `tooling: add default scenario asset creator`

**Goal:** Generate the main prototype encounter as data.

**Scope:** Editor CLI tool and scenario asset.

**Tasks:**

- Add `[UnityCliTool]` named `rt_create_default_scenario`.
- Create `Assets/ScriptableObjects/Scenarios/DefaultSkirmish.asset`.
- Use an 8x8 map and 2v2 or 3v3 unit setup.
- Include at least one melee unit and one ranged/AoE unit per side if using 3v3.

**Acceptance:**

- Scenario asset validates successfully.

**Validation:**

```bash
unity-ctrl --project "$PWD" rt_create_default_map
unity-ctrl --project "$PWD" rt_create_default_units
unity-ctrl --project "$PWD" rt_create_default_abilities
unity-ctrl --project "$PWD" rt_create_default_scenario
unity-ctrl --project "$PWD" rt_find_assets --params '{"filter":"t:ScenarioDefinition"}'
```

## 132 — Add scenario loader component

Status: TODO

**Commit:** `feat: add scenario loader component`

**Goal:** Spawn the battle from scenario data at runtime.

**Scope:** Runtime scene/system.

**Tasks:**

- Add `ScenarioLoader : MonoBehaviour`.
- Reference `ScenarioDefinition`, `GridManager`, `UnitSpawner`, and `CombatManager`.
- On play, load map reference and spawn units before combat starts.
- Ensure idempotence or clear existing spawned units before loading.

**Acceptance:**

- Main scene can be reset by reloading scenario data.

**Validation:**

```bash
unity-ctrl --project "$PWD" editor refresh --compile
unity-ctrl --project "$PWD" console --type error --stacktrace full --lines 50
```

## 133 — Replace manual unit setup with scenario setup

Status: TODO

**Commit:** `refactor: bootstrap units from scenario definition`

**Goal:** Make `MainPrototype` reproduce from data rather than manual instances.

**Scope:** Editor tools and scene.

**Tasks:**

- Update `rt_setup_units_scene` or create `rt_setup_scenario_scene`.
- Ensure scene has `ScenarioLoader` assigned to `DefaultSkirmish.asset`.
- Remove or disable duplicate manually placed units.
- Save scene.

**Acceptance:**

- Entering play mode spawns units from scenario data exactly once.

**Validation:**

```bash
unity-ctrl --project "$PWD" rt_create_default_scenario
unity-ctrl --project "$PWD" rt_setup_scenario_scene
unity-ctrl --project "$PWD" editor play --wait
unity-ctrl --project "$PWD" console --type error,warning,log --lines 100
unity-ctrl --project "$PWD" editor stop
```

## 134 — Add all-in-one prototype scene setup tool

Status: TODO

**Commit:** `tooling: add all in one prototype setup tool`

**Goal:** Let the coding agent rebuild the prototype scene from scratch.

**Scope:** Editor CLI tool.

**Tasks:**

- Add `[UnityCliTool]` named `rt_setup_prototype_scene`.
- Run or call logic for default map, units, abilities, scenario, grid scene, input scene, UI, and scenario scene setup.
- Save scene and project.
- Return a concise JSON summary of created/updated assets and scene objects.

**Acceptance:**

- A clean scene can be turned into the playable prototype setup with one command.

**Validation:**

```bash
unity-ctrl --project "$PWD" editor refresh --compile
unity-ctrl --project "$PWD" rt_setup_prototype_scene
unity-ctrl --project "$PWD" rt_hierarchy
unity-ctrl --project "$PWD" menu "File/Save"
unity-ctrl --project "$PWD" menu "File/Save Project"
```

## 135 — Tune default AP, HP, and damage values

Status: TODO

**Commit:** `balance: tune prototype ap hp and damage values`

**Goal:** Make the demo last long enough to show reactions.

**Scope:** ScriptableObject assets and docs.

**Tasks:**

- Tune max AP so units can act and still save AP for reactions.
- Tune damage so most units survive at least two hits unless focused.
- Ensure Reaction Move cost creates real choices.
- Update design doc with current values.

**Acceptance:**

- A typical unit can attack and still sometimes react, but cannot do everything.
- Fights are not decided by one unavoidable attack.

**Validation:**

```bash
unity-ctrl --project "$PWD" reserialize Assets/ScriptableObjects/**/*.asset
unity-ctrl --project "$PWD" editor refresh
unity-ctrl --project "$PWD" console --type error --stacktrace full --lines 50
```

## 136 — Add prototype visuals polish pass

Status: TODO

**Commit:** `art: polish prototype scene readability`

**Goal:** Improve readability without final art.

**Scope:** Scene, materials, prefabs.

**Tasks:**

- Use distinct materials for teams.
- Add simple directional light and ambient settings.
- Add tile outlines or spacing if grid readability is poor.
- Add unit selection/active/reactor markers.
- Ensure camera starts at a good angle.

**Acceptance:**

- A screenshot clearly shows terrain, teams, active unit, and UI.

**Validation:**

```bash
unity-ctrl --project "$PWD" rt_setup_prototype_scene
unity-ctrl --project "$PWD" editor play --wait
unity-ctrl --project "$PWD" screenshot --view game --output_path /tmp/reaction-tactics-readable-prototype.png
unity-ctrl --project "$PWD" editor stop
```

## 137 — Add scene content tests or validation CLI tool

Status: TODO

**Commit:** `tooling: validate prototype scene content`

**Goal:** Catch missing references and invalid scene setup quickly.

**Scope:** Editor CLI tool or EditMode tests.

**Tasks:**

- Add `[UnityCliTool]` named `rt_validate_prototype_scene`.
- Check required systems exist.
- Check map, scenario, unit prefab, materials, and abilities are assigned.
- Check scenario spawn cells are valid and unique.
- Return success/failure with readable details.

**Acceptance:**

- The validation tool fails loudly if the scene cannot run.

**Validation:**

```bash
unity-ctrl --project "$PWD" editor refresh --compile
unity-ctrl --project "$PWD" rt_validate_prototype_scene
```

---

# Milestone 13 — End-to-end gameplay integration

## 138 — Wire active movement to UI and command router

Status: TODO

**Commit:** `feat: integrate active movement gameplay flow`

**Goal:** Let the player move active units through the complete UI/input path.

**Scope:** Combat manager, input, UI, movement.

**Tasks:**

- Selecting Move shows range.
- Clicking reachable cell executes active movement.
- AP updates and unit nameplate updates.
- Movement highlights clear after completion.
- Active unit can act again if AP remains.

**Acceptance:**

- Player can complete a full active movement from UI in play mode.

**Validation:**

```bash
unity-ctrl --project "$PWD" editor refresh --compile
unity-ctrl --project "$PWD" test --filter ActiveMovementIntegrationTests
unity-ctrl --project "$PWD" console --type error --stacktrace full --lines 50
```

## 139 — Wire melee action to UI and command router

Status: TODO

**Commit:** `feat: integrate melee action gameplay flow`

**Goal:** Let active units use Option A melee through the complete flow.

**Scope:** Combat manager, input, UI, resolver, reactions.

**Tasks:**

- Selecting Melee Slash allows valid hostile target selection.
- Declaration opens reaction window.
- Target can reaction-move away if it has AP.
- Melee resolves after all reactions.
- Log hit or avoided-by-movement result.

**Acceptance:**

- Melee demonstrates the requested Option A timing in play mode.

**Validation:**

```bash
unity-ctrl --project "$PWD" editor refresh --compile
unity-ctrl --project "$PWD" test --filter MeleeGameplayFlowTests
unity-ctrl --project "$PWD" console --type error --stacktrace full --lines 50
```

## 140 — Wire cone action to UI and command router

Status: TODO

**Commit:** `feat: integrate cone attack gameplay flow`

**Goal:** Let active units use cone attacks through the complete flow.

**Scope:** Combat manager, input, UI, resolver, reactions.

**Tasks:**

- Selecting Cone Shot previews affected cells by hover direction.
- Confirming declaration opens reaction window.
- Threatened units can move out if they have AP.
- Cone resolves against final positions.
- Log hit and avoided units.

**Acceptance:**

- Player can avoid cone damage by moving out of the cone during reaction.

**Validation:**

```bash
unity-ctrl --project "$PWD" editor refresh --compile
unity-ctrl --project "$PWD" test --filter ConeGameplayFlowTests
unity-ctrl --project "$PWD" console --type error --stacktrace full --lines 50
```

## 141 — Wire AoE action to UI and command router

Status: TODO

**Commit:** `feat: integrate aoe attack gameplay flow`

**Goal:** Let active units use Fireball through the complete flow.

**Scope:** Combat manager, input, UI, resolver, reactions.

**Tasks:**

- Selecting Fireball previews radius at hovered target cell.
- Confirming declaration opens reaction window.
- Threatened units can exit the AoE if they have AP.
- AoE resolves against final positions.
- Log hit and avoided units.

**Acceptance:**

- Player can avoid AoE damage by leaving the area during reaction.

**Validation:**

```bash
unity-ctrl --project "$PWD" editor refresh --compile
unity-ctrl --project "$PWD" test --filter AoeGameplayFlowTests
unity-ctrl --project "$PWD" console --type error --stacktrace full --lines 50
```

## 142 — Wire brace reaction to UI and command router

Status: TODO

**Commit:** `feat: integrate brace reaction gameplay flow`

**Goal:** Let players reduce incoming damage when they cannot escape.

**Scope:** Reaction UI, command router, resolver.

**Tasks:**

- During reaction turn, Brace button spends AP and sets braced state.
- Braced unit cannot also move during the same reaction turn in prototype.
- Pending action resolves and applies reduced damage if unit is hit.
- Log reduction amount.

**Acceptance:**

- Brace is a meaningful alternative to reaction movement.

**Validation:**

```bash
unity-ctrl --project "$PWD" editor refresh --compile
unity-ctrl --project "$PWD" test --filter BraceGameplayFlowTests
unity-ctrl --project "$PWD" console --type error --stacktrace full --lines 50
```

## 143 — Complete player-vs-AI turn handoff

Status: TODO

**Commit:** `feat: complete player and ai turn handoff`

**Goal:** Allow a solo player to fight enemy AI continuously.

**Scope:** Combat manager, AI, UI.

**Tasks:**

- Player controls player-team active turns and player-team reactions.
- AI controls enemy-team active turns and enemy-team reactions.
- Enemy actions still open player reaction windows.
- Player actions still allow enemy AI reactions.
- End turn and combat over states behave correctly.

**Acceptance:**

- A complete skirmish can play from round 1 to victory/defeat without manual console commands.

**Validation:**

```bash
unity-ctrl --project "$PWD" editor refresh --compile
unity-ctrl --project "$PWD" test --filter PlayerAiHandoffTests
unity-ctrl --project "$PWD" console --type error --stacktrace full --lines 50
```

## 144 — Add PlayMode tactical smoke test

Status: TODO

**Commit:** `test: add playmode tactical smoke test`

**Goal:** Verify the main scene starts and core systems exist in play mode.

**Scope:** PlayMode tests.

**Tasks:**

- Load `MainPrototype`.
- Assert grid manager, combat manager, unit registry, scenario loader, UI, and camera exist.
- Assert at least two teams and multiple living units exist after scenario load.
- Advance a few frames without errors.

**Acceptance:**

- PlayMode smoke test passes.

**Validation:**

```bash
unity-ctrl --project "$PWD" test --mode PlayMode --filter TacticalSmokePlayModeTests
```

## 145 — Add deterministic combat simulation test

Status: TODO

**Commit:** `test: add deterministic combat simulation test`

**Goal:** Exercise the unique action/reaction mechanics without UI.

**Scope:** EditMode or PlayMode tests.

**Tasks:**

- Build a small test map and units in memory.
- Declare melee, reaction move target out, assert no damage.
- Declare cone, reaction move target out, assert no damage.
- Declare AoE, reaction move target out, assert no damage.
- Declare melee with target staying in range, assert damage.

**Acceptance:**

- The prototype's central mechanic is protected by automated tests.

**Validation:**

```bash
unity-ctrl --project "$PWD" test --filter DeterministicCombatSimulationTests
```

## 146 — Add full-scene validation CLI smoke tool

Status: TODO

**Commit:** `tooling: add full scene tactical smoke cli tool`

**Goal:** Give the coding agent a fast non-interactive sanity check.

**Scope:** Editor CLI tool.

**Tasks:**

- Add `[UnityCliTool]` named `rt_smoke_tactical_scene`.
- Validate scene content.
- Enter play mode if supported by tool pattern or run editor-side checks only if safer.
- Report grid cell count, living units, abilities, current phase, and testable references.
- Return failure if any required reference is missing.

**Acceptance:**

- Agent can run one command and know whether the prototype scene is structurally healthy.

**Validation:**

```bash
unity-ctrl --project "$PWD" editor refresh --compile
unity-ctrl --project "$PWD" rt_smoke_tactical_scene
unity-ctrl --project "$PWD" console --type error --stacktrace full --lines 50
```

## 147 — Run full EditMode suite and fix failures

Status: TODO

**Commit:** `test: fix editmode suite before prototype build`

**Goal:** Stabilize all non-playmode logic.

**Scope:** Tests and small fixes only.

**Tasks:**

- Run all EditMode tests.
- Fix failures without expanding feature scope.
- Ensure tests are not flaky.
- Commit only fixes directly related to failing tests.

**Acceptance:**

- All EditMode tests pass.

**Validation:**

```bash
unity-ctrl --project "$PWD" test
unity-ctrl --project "$PWD" console --type error --stacktrace full --lines 50
```

## 148 — Run full PlayMode suite and fix failures

Status: TODO

**Commit:** `test: fix playmode suite before prototype build`

**Goal:** Stabilize scene/runtime behavior.

**Scope:** PlayMode tests and small fixes only.

**Tasks:**

- Run all PlayMode tests.
- Fix scene setup, async timing, or missing references.
- Keep tests deterministic.

**Acceptance:**

- All PlayMode tests pass.

**Validation:**

```bash
unity-ctrl --project "$PWD" test --mode PlayMode
unity-ctrl --project "$PWD" console --type error --stacktrace full --lines 50
```

## 149 — Capture prototype screenshots for review

Status: TODO

**Commit:** `docs: add prototype review screenshots`

**Goal:** Create visual proof of the current prototype state.

**Scope:** `docs/screenshots` or similar.

**Tasks:**

- Capture Game view at start of combat.
- Capture active movement range.
- Capture an enemy action with reaction safety cells.
- Capture combat log after a movement avoid.
- Add a short markdown note explaining each image.

**Acceptance:**

- Reviewers can inspect prototype readability without opening Unity.

**Validation:**

```bash
unity-ctrl --project "$PWD" editor play --wait
unity-ctrl --project "$PWD" screenshot --view game --output_path docs/screenshots/start.png
unity-ctrl --project "$PWD" editor stop
```

## 150 — Profile the prototype scene briefly

Status: TODO

**Commit:** `perf: add initial prototype performance notes`

**Goal:** Catch obvious performance issues before build.

**Scope:** Docs and small fixes only.

**Tasks:**

- Run scene in play mode.
- Capture profiler hierarchy over 30 frames.
- Note top costs in `docs/performance-notes.md`.
- Fix only obvious accidental issues, such as per-frame allocations from debug UI if severe.

**Acceptance:**

- Prototype does not have obvious runaway per-frame work.

**Validation:**

```bash
unity-ctrl --project "$PWD" editor play --wait
unity-ctrl --project "$PWD" profiler hierarchy --frames 30 --sort self --max 20
unity-ctrl --project "$PWD" editor stop
```

## 151 — Build Linux prototype player

Status: TODO

**Commit:** `build: produce linux prototype build`

**Goal:** Verify the prototype can build outside the editor.

**Scope:** Build script and minor build fixes.

**Tasks:**

- Run the build script.
- Fix build-only compile errors.
- Confirm output exists under `Build/ReactionTacticsPrototype` or documented path.
- Do not commit large build artifacts unless explicitly desired.

**Acceptance:**

- Build completes successfully.

**Validation:**

```bash
unity-batch "$PWD" -executeMethod ReactionTactics.Editor.BuildPrototype.PerformBuild -quit
```

## 152 — Add prototype playtest checklist

Status: TODO

**Commit:** `docs: add prototype playtest checklist`

**Goal:** Define manual verification for the prototype's game feel.

**Scope:** `docs/playtest-checklist.md`.

**Tasks:**

- Checklist includes:
  - Can move active unit.
  - Can save AP for reactions.
  - Melee target can move out of range and avoid hit.
  - Melee target staying in range is hit.
  - Cone target can move out of cone.
  - AoE target can exit area.
  - Brace reduces damage.
  - Reaction order follows distance from actor.
  - No outcome uses dodge chance.
  - Combat can end in victory/defeat.
- Add space for observed bugs and tuning notes.

**Acceptance:**

- A tester can validate the prototype without reading code.

**Validation:** Docs-only.

## 153 — Add known limitations document

Status: TODO

**Commit:** `docs: document prototype limitations`

**Goal:** Make current non-goals explicit.

**Scope:** `docs/known-limitations.md`.

**Tasks:**

- Document rough edges such as primitive art, simple AI, simple cone math, simple line of sight, limited classes, and placeholder UI.
- Document intended future expansions: opportunity attacks, interrupts, counterspells, push/pull, cover, procedural maps, better animations.
- Do not apologize for prototype scope; state it clearly.

**Acceptance:**

- Future work is separated from prototype completion.

**Validation:** Docs-only.

## 154 — Final prototype cleanup pass

Status: TODO

**Commit:** `chore: clean up prototype before handoff`

**Goal:** Remove temporary debug mess while keeping useful tools.

**Scope:** Small cleanup across project.

**Tasks:**

- Remove dead scripts and unused assets.
- Keep useful CLI tools.
- Ensure namespaces are consistent.
- Ensure serialized fields have clear names and tooltips where helpful.
- Confirm no `Debug.Log` spam outside combat log/debug mode.

**Acceptance:**

- Project is organized and ready for continued development.

**Validation:**

```bash
unity-ctrl --project "$PWD" editor refresh --compile
unity-ctrl --project "$PWD" test
unity-ctrl --project "$PWD" test --mode PlayMode
unity-ctrl --project "$PWD" console --type error --stacktrace full --lines 50
```

## 155 — Tag prototype-complete checklist commit

Status: TODO

**Commit:** `docs: mark reaction tactics prototype complete`

**Goal:** Record exactly what is complete at the prototype milestone.

**Scope:** Docs only unless tiny fixes are found.

**Tasks:**

- Add `docs/prototype-complete.md`.
- Summarize implemented features.
- Link playtest checklist, known limitations, screenshots, and build instructions.
- Include exact Unity version and validation commands used.

**Acceptance:**

- A future developer can understand the prototype state and continue from it.

**Validation:** Docs-only, plus optional final full validation:

```bash
unity-ctrl --project "$PWD" editor refresh --compile
unity-ctrl --project "$PWD" test
unity-ctrl --project "$PWD" test --mode PlayMode
unity-batch "$PWD" -executeMethod ReactionTactics.Editor.BuildPrototype.PerformBuild -quit
```

---

# Prototype completion definition

The prototype is complete when all of the following are true:

- The project compiles cleanly with no console errors.
- EditMode tests pass.
- PlayMode tests pass.
- `MainPrototype` loads into a visible 3D stepped grid.
- Units have HP and AP visible.
- AP refreshes at the start of each round.
- Active units can move and take actions only on their own turns.
- Off-turn units can react only during reaction windows.
- Every declared action that triggers reactions gives every other living unit a reaction turn in distance order.
- Reaction movement spends AP and changes final positions before the original action resolves.
- Melee uses Option A timing: declared in range, then hits only if the target remains in range at resolution.
- Cone attacks hit only units still inside the cone at resolution.
- AoEs hit only units still inside the area at resolution.
- Brace reduces incoming damage when escape is impossible or undesirable.
- No dodge chance, accuracy roll, or random hit roll affects whether attacks connect.
- Enemy AI can take basic active turns and reactions.
- Combat can end with victory or defeat.
- A Linux standalone build can be produced.

