using System;
using System.Collections.Generic;
using Newtonsoft.Json.Linq;
using ReactionTactics.Actions;
using ReactionTactics.Grid;
using ReactionTactics.Units;
using UnityCliConnector;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace ReactionTactics.Editor
{
    [UnityCliTool(Name = "rt_setup_units_scene", Description = "Configure the main prototype scene with a unit registry, unit spawner, and default 2v2 units.", Group = "reaction-tactics")]
    public static class UnitSceneSetupTool
    {
        private const string DefaultScenePath = "Assets/Scenes/MainPrototype.unity";
        private const string SystemsRootName = "Systems";
        private const string UnitsRootName = "Units";
        private const string InitialUnitsRootName = "Initial Units";
        private const string PrototypeUnitPrefabPath = "Assets/Prefabs/PrototypeUnit.prefab";
        private const string UnitAssetFolderPath = "Assets/ScriptableObjects/Units";
        private const string AbilityAssetFolderPath = "Assets/ScriptableObjects/Abilities";
        private const int FirstUnitId = 1;

        private static readonly UnitSpawnSpec[] DefaultUnitSpawns =
        {
            new UnitSpawnSpec("Knight", TeamId.Player, new GridPosition(0, 0, 0)),
            new UnitSpawnSpec("Rogue", TeamId.Player, new GridPosition(1, 0, 0)),
            new UnitSpawnSpec("Goblin", TeamId.Enemy, new GridPosition(7, 0, 7)),
            new UnitSpawnSpec("Shaman", TeamId.Enemy, new GridPosition(6, 0, 7))
        };

        private static readonly AbilityAssetSpec[] RequiredAbilityAssets =
        {
            new AbilityAssetSpec("move", "Move"),
            new AbilityAssetSpec("melee_slash", "MeleeSlash"),
            new AbilityAssetSpec("cone_shot", "ConeShot"),
            new AbilityAssetSpec("fireball", "Fireball"),
            new AbilityAssetSpec("brace", "Brace"),
            new AbilityAssetSpec("pass_reaction", "PassReaction")
        };

        private static readonly UnitAbilityLoadoutSpec[] DefaultAbilityLoadouts =
        {
            new UnitAbilityLoadoutSpec("Knight", "move", "melee_slash", "brace", "pass_reaction"),
            new UnitAbilityLoadoutSpec("Rogue", "move", "melee_slash", "brace", "pass_reaction"),
            new UnitAbilityLoadoutSpec("Archer", "move", "cone_shot", "melee_slash", "brace", "pass_reaction"),
            new UnitAbilityLoadoutSpec("Mage", "move", "fireball", "melee_slash", "brace", "pass_reaction"),
            new UnitAbilityLoadoutSpec("Goblin", "move", "melee_slash", "brace", "pass_reaction"),
            new UnitAbilityLoadoutSpec("Shaman", "move", "fireball", "melee_slash", "brace", "pass_reaction")
        };

        public sealed class Parameters
        {
            [ToolParameter("Scene asset path to configure.", DefaultValue = DefaultScenePath)]
            public string ScenePath { get; set; }
        }

        public static object HandleCommand(JObject parameters)
        {
            try
            {
                var toolParams = new ToolParams(parameters ?? new JObject());
                var scenePath = toolParams.Get("scenePath", toolParams.Get("scene_path", DefaultScenePath)).Trim();
                if (string.IsNullOrWhiteSpace(scenePath))
                {
                    return new ErrorResponse("Parameter 'scenePath' cannot be empty.");
                }

                var scene = OpenOrUseScene(scenePath);
                var unitPrefab = LoadRequiredUnitPrefab(PrototypeUnitPrefabPath);
                var statsByName = LoadRequiredUnitStats(DefaultUnitSpawns);
                var abilitiesByKey = LoadRequiredAbilities(RequiredAbilityAssets);
                var loadoutsByStatsName = BuildRequiredLoadouts(DefaultAbilityLoadouts, abilitiesByKey);
                if (!TryValidateSpawnLoadouts(DefaultUnitSpawns, loadoutsByStatsName, out var loadoutFailureReason))
                {
                    return new ErrorResponse(loadoutFailureReason);
                }

                var systemsRootCreated = false;
                var unitsRootCreated = false;
                var registryCreated = false;
                var spawnerCreated = false;
                var initialUnitsRootCreated = false;

                var systemsRoot = EnsureRootObject(scene, SystemsRootName, ref systemsRootCreated);
                var unitsRoot = EnsureRootObject(scene, UnitsRootName, ref unitsRootCreated);
                var gridManager = FindGridManager(scene, systemsRoot);
                if (gridManager == null)
                {
                    return new ErrorResponse($"No {nameof(GridManager)} was found in scene '{scene.path}'. Run rt_setup_grid_scene before rt_setup_units_scene.");
                }

                if (!gridManager.RebuildMap())
                {
                    return new ErrorResponse($"{nameof(GridManager)} on '{GetScenePath(gridManager.gameObject)}' could not build a current map. Run rt_setup_grid_scene and check the Unity console.");
                }

                var validationResult = ValidateSpawnCells(gridManager.CurrentMap, DefaultUnitSpawns);
                if (!validationResult.IsValid)
                {
                    return new ErrorResponse(validationResult.FailureReason);
                }

                var registry = EnsureRootComponent<UnitRegistry>(unitsRoot, systemsRoot, ref registryCreated);
                var spawner = EnsureRootComponent<UnitSpawner>(unitsRoot, systemsRoot, ref spawnerCreated);
                var initialUnitsRoot = EnsureChild(unitsRoot.transform, InitialUnitsRootName, ref initialUnitsRootCreated);

                ConfigureSpawner(spawner, unitPrefab, registry, gridManager, initialUnitsRoot);
                registry.Clear();
                ClearChildren(initialUnitsRoot);
                spawner.ResetUnitIdSequence(FirstUnitId);

                var spawnedUnits = new List<object>();
                foreach (var spec in DefaultUnitSpawns)
                {
                    var spawnResult = spawner.Spawn(statsByName[spec.StatsAssetName], spec.Team, spec.Position);
                    if (spawnResult.IsFailure)
                    {
                        registry.Clear();
                        ClearChildren(initialUnitsRoot);
                        return new ErrorResponse($"Failed to spawn {spec.StatsAssetName} at {spec.Position}: {spawnResult.ErrorMessage}");
                    }

                    var unit = spawnResult.Value;
                    var loadout = AssignAbilityLoadout(unit, spec, loadoutsByStatsName);
                    EditorUtility.SetDirty(loadout);
                    EditorUtility.SetDirty(unit);
                    EditorUtility.SetDirty(unit.gameObject);
                    spawnedUnits.Add(ToUnitSummary(unit));
                }

                var playerCount = registry.GetUnitsByTeam(TeamId.Player).Count;
                var enemyCount = registry.GetUnitsByTeam(TeamId.Enemy).Count;
                if (playerCount != 2 || enemyCount != 2)
                {
                    return new ErrorResponse($"Expected a 2v2 scene after setup but registry contains {playerCount} player units and {enemyCount} enemy units.");
                }

                EditorUtility.SetDirty(registry);
                EditorUtility.SetDirty(spawner);
                EditorSceneManager.MarkSceneDirty(scene);
                if (!EditorSceneManager.SaveScene(scene))
                {
                    return new ErrorResponse($"Failed to save scene '{scene.path}'.");
                }

                AssetDatabase.SaveAssets();

                return new SuccessResponse("Configured prototype 2v2 unit scene.", new
                {
                    scene = scene.path,
                    systemsRoot = GetScenePath(systemsRoot),
                    systemsRootCreated,
                    unitsRoot = GetScenePath(unitsRoot),
                    unitsRootCreated,
                    unitRegistry = GetScenePath(registry.gameObject),
                    unitRegistryCreated = registryCreated,
                    unitSpawner = GetScenePath(spawner.gameObject),
                    unitSpawnerCreated = spawnerCreated,
                    initialUnitsRoot = GetScenePath(initialUnitsRoot.gameObject),
                    initialUnitsRootCreated,
                    unitPrefab = PrototypeUnitPrefabPath,
                    abilityAssetCount = abilitiesByKey.Count,
                    defaultLoadoutCount = loadoutsByStatsName.Count,
                    playerCount,
                    enemyCount,
                    registeredCount = registry.RegisteredCount,
                    units = spawnedUnits
                });
            }
            catch (Exception exception)
            {
                return new ErrorResponse($"Failed to configure prototype unit scene: {exception.Message}");
            }
        }

        private static Scene OpenOrUseScene(string scenePath)
        {
            if (AssetDatabase.LoadAssetAtPath<SceneAsset>(scenePath) == null)
            {
                throw new InvalidOperationException($"Scene asset '{scenePath}' was not found.");
            }

            var activeScene = EditorSceneManager.GetActiveScene();
            if (activeScene.IsValid() && string.Equals(activeScene.path, scenePath, StringComparison.Ordinal))
            {
                return activeScene;
            }

            if (activeScene.IsValid() && activeScene.isDirty)
            {
                throw new InvalidOperationException(
                    $"Active scene '{activeScene.path}' has unsaved changes. Save it before opening '{scenePath}'.");
            }

            return EditorSceneManager.OpenScene(scenePath, OpenSceneMode.Single);
        }

        private static TacticalUnit LoadRequiredUnitPrefab(string path)
        {
            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(path);
            if (prefab == null)
            {
                throw new InvalidOperationException($"Required prototype unit prefab was not found at '{path}'.");
            }

            var tacticalUnit = prefab.GetComponent<TacticalUnit>();
            if (tacticalUnit == null)
            {
                throw new InvalidOperationException($"Prototype unit prefab '{path}' must contain a {nameof(TacticalUnit)} component.");
            }

            if (prefab.GetComponent<GridPathMover>() == null)
            {
                throw new InvalidOperationException($"Prototype unit prefab '{path}' must contain a {nameof(GridPathMover)} component.");
            }

            return tacticalUnit;
        }

        private static Dictionary<string, UnitStatsDefinition> LoadRequiredUnitStats(IEnumerable<UnitSpawnSpec> specs)
        {
            var statsByName = new Dictionary<string, UnitStatsDefinition>(StringComparer.Ordinal);
            foreach (var spec in specs)
            {
                if (statsByName.ContainsKey(spec.StatsAssetName))
                {
                    continue;
                }

                var path = spec.StatsAssetPath;
                var stats = AssetDatabase.LoadAssetAtPath<UnitStatsDefinition>(path);
                if (stats == null)
                {
                    throw new InvalidOperationException($"Required default unit stats asset was not found at '{path}'. Run rt_create_default_units first.");
                }

                statsByName.Add(spec.StatsAssetName, stats);
            }

            return statsByName;
        }

        private static Dictionary<string, AbilityDefinition> LoadRequiredAbilities(IEnumerable<AbilityAssetSpec> specs)
        {
            var abilitiesByKey = new Dictionary<string, AbilityDefinition>(StringComparer.Ordinal);
            foreach (var spec in specs)
            {
                var ability = AssetDatabase.LoadAssetAtPath<AbilityDefinition>(spec.AssetPath);
                if (ability == null)
                {
                    throw new InvalidOperationException($"Required default ability asset was not found at '{spec.AssetPath}'. Run rt_create_default_abilities first.");
                }

                var validationResult = ability.ValidateDefinition();
                if (validationResult.IsFailure)
                {
                    throw new InvalidOperationException($"Default ability asset '{spec.AssetPath}' is invalid: {validationResult.ErrorMessage}");
                }

                if (!string.Equals(ability.AbilityKey, spec.AbilityKey, StringComparison.Ordinal))
                {
                    throw new InvalidOperationException($"Default ability asset '{spec.AssetPath}' has key '{ability.AbilityKey}' but expected '{spec.AbilityKey}'. Run rt_create_default_abilities first.");
                }

                if (abilitiesByKey.ContainsKey(ability.AbilityKey))
                {
                    throw new InvalidOperationException($"Default ability key '{ability.AbilityKey}' is loaded more than once.");
                }

                abilitiesByKey.Add(ability.AbilityKey, ability);
            }

            return abilitiesByKey;
        }

        private static Dictionary<string, IReadOnlyList<AbilityDefinition>> BuildRequiredLoadouts(
            IEnumerable<UnitAbilityLoadoutSpec> loadoutSpecs,
            IReadOnlyDictionary<string, AbilityDefinition> abilitiesByKey)
        {
            var loadoutsByStatsName = new Dictionary<string, IReadOnlyList<AbilityDefinition>>(StringComparer.Ordinal);
            foreach (var loadoutSpec in loadoutSpecs)
            {
                if (loadoutsByStatsName.ContainsKey(loadoutSpec.StatsAssetName))
                {
                    throw new InvalidOperationException($"Default loadout for '{loadoutSpec.StatsAssetName}' is defined more than once.");
                }

                var abilities = new List<AbilityDefinition>();
                foreach (var abilityKey in loadoutSpec.AbilityKeys)
                {
                    if (!abilitiesByKey.TryGetValue(abilityKey, out var ability))
                    {
                        throw new InvalidOperationException($"Default loadout for '{loadoutSpec.StatsAssetName}' requires unknown ability key '{abilityKey}'.");
                    }

                    abilities.Add(ability);
                }

                loadoutsByStatsName.Add(loadoutSpec.StatsAssetName, abilities);
            }

            return loadoutsByStatsName;
        }

        private static bool TryValidateSpawnLoadouts(
            IEnumerable<UnitSpawnSpec> unitSpawns,
            IReadOnlyDictionary<string, IReadOnlyList<AbilityDefinition>> loadoutsByStatsName,
            out string failureReason)
        {
            foreach (var spec in unitSpawns)
            {
                if (!loadoutsByStatsName.TryGetValue(spec.StatsAssetName, out var abilities) || abilities.Count == 0)
                {
                    failureReason = $"No default ability loadout is defined for spawned unit '{spec.StatsAssetName}'.";
                    return false;
                }
            }

            failureReason = string.Empty;
            return true;
        }

        private static GameObject EnsureRootObject(Scene scene, string rootName, ref bool created)
        {
            foreach (var root in scene.GetRootGameObjects())
            {
                if (string.Equals(root.name, rootName, StringComparison.Ordinal))
                {
                    created = false;
                    return root;
                }
            }

            var gameObject = new GameObject(rootName);
            SceneManager.MoveGameObjectToScene(gameObject, scene);
            created = true;
            return gameObject;
        }

        private static T EnsureRootComponent<T>(GameObject preferredRoot, GameObject fallbackRoot, ref bool created)
            where T : Component
        {
            var component = preferredRoot.GetComponent<T>();
            if (component != null)
            {
                created = false;
                return component;
            }

            component = fallbackRoot.GetComponent<T>();
            if (component != null)
            {
                created = false;
                return component;
            }

            created = true;
            return preferredRoot.AddComponent<T>();
        }

        private static Transform EnsureChild(Transform parent, string childName, ref bool created)
        {
            var existing = parent.Find(childName);
            if (existing != null)
            {
                created = false;
                existing.localPosition = Vector3.zero;
                existing.localRotation = Quaternion.identity;
                existing.localScale = Vector3.one;
                return existing;
            }

            var gameObject = new GameObject(childName);
            gameObject.transform.SetParent(parent, false);
            created = true;
            return gameObject.transform;
        }

        private static GridManager FindGridManager(Scene scene, GameObject systemsRoot)
        {
            var gridManager = systemsRoot.GetComponent<GridManager>();
            if (gridManager != null)
            {
                return gridManager;
            }

            foreach (var root in scene.GetRootGameObjects())
            {
                gridManager = root.GetComponentInChildren<GridManager>(includeInactive: true);
                if (gridManager != null)
                {
                    return gridManager;
                }
            }

            return null;
        }

        private static void ConfigureSpawner(
            UnitSpawner spawner,
            TacticalUnit unitPrefab,
            UnitRegistry registry,
            GridManager gridManager,
            Transform spawnParent)
        {
            spawner.Configure(unitPrefab, registry, gridManager, spawnParent);
            EditorUtility.SetDirty(spawner);
        }

        private static UnitAbilityLoadout AssignAbilityLoadout(
            TacticalUnit unit,
            UnitSpawnSpec spec,
            IReadOnlyDictionary<string, IReadOnlyList<AbilityDefinition>> loadoutsByStatsName)
        {
            if (!loadoutsByStatsName.TryGetValue(spec.StatsAssetName, out var abilities))
            {
                throw new InvalidOperationException($"No default ability loadout is defined for spawned unit '{spec.StatsAssetName}'.");
            }

            var loadout = unit.GetComponent<UnitAbilityLoadout>();
            if (loadout == null)
            {
                loadout = unit.gameObject.AddComponent<UnitAbilityLoadout>();
            }

            loadout.SetAbilities(abilities);
            return loadout;
        }

        private static void ClearChildren(Transform parent)
        {
            for (var index = parent.childCount - 1; index >= 0; index--)
            {
                UnityEngine.Object.DestroyImmediate(parent.GetChild(index).gameObject);
            }
        }

        private static SpawnValidationResult ValidateSpawnCells(IGridMap map, IEnumerable<UnitSpawnSpec> specs)
        {
            if (map == null)
            {
                return SpawnValidationResult.Failure("Cannot validate unit spawns because the grid manager has no current map.");
            }

            var occupiedCells = new HashSet<GridPosition>();
            foreach (var spec in specs)
            {
                if (!TryResolveWalkableCell(map, spec.Position, out var cell, out var failureReason))
                {
                    return SpawnValidationResult.Failure(failureReason);
                }

                if (!occupiedCells.Add(cell.Position))
                {
                    return SpawnValidationResult.Failure($"Default unit spawn cell {cell.Position} is used by more than one unit.");
                }
            }

            return SpawnValidationResult.Success();
        }

        private static bool TryResolveWalkableCell(
            IGridMap map,
            GridPosition requestedPosition,
            out GridCell cell,
            out string failureReason)
        {
            if (!TryResolveCell(map, requestedPosition, out cell, out failureReason))
            {
                return false;
            }

            if (!cell.Walkable || cell.BlocksMovement)
            {
                failureReason = $"Default unit spawn cell {cell.Position} is blocked or unwalkable.";
                return false;
            }

            failureReason = string.Empty;
            return true;
        }

        private static bool TryResolveCell(
            IGridMap map,
            GridPosition requestedPosition,
            out GridCell cell,
            out string failureReason)
        {
            if (map.TryGetCell(requestedPosition, out cell))
            {
                failureReason = string.Empty;
                return true;
            }

            var foundHorizontalMatch = false;
            var horizontalMatch = default(GridCell);
            foreach (var candidate in map.AllCells)
            {
                if (candidate.Position.X != requestedPosition.X || candidate.Position.Z != requestedPosition.Z)
                {
                    continue;
                }

                if (foundHorizontalMatch)
                {
                    cell = default(GridCell);
                    failureReason = $"Default unit spawn coordinate ({requestedPosition.X},{requestedPosition.Z}) matches multiple height cells and is ambiguous.";
                    return false;
                }

                foundHorizontalMatch = true;
                horizontalMatch = candidate;
            }

            if (foundHorizontalMatch)
            {
                cell = horizontalMatch;
                failureReason = string.Empty;
                return true;
            }

            cell = default(GridCell);
            failureReason = $"No grid cell exists for default unit spawn {requestedPosition}.";
            return false;
        }

        private static object ToUnitSummary(TacticalUnit unit)
        {
            var loadout = unit.GetComponent<UnitAbilityLoadout>();
            IReadOnlyList<AbilityDefinition> assignedAbilities = loadout != null
                ? loadout.GetAssignedAbilities()
                : new List<AbilityDefinition>();

            return new
            {
                name = unit.name,
                path = GetScenePath(unit.gameObject),
                unitId = unit.UnitId.Value,
                displayName = unit.DisplayName,
                team = unit.Team.ToString(),
                gridPosition = ToPositionSummary(unit.CurrentGridPosition),
                worldPosition = ToPositionSummary(unit.transform.position),
                hp = unit.CurrentHP,
                ap = unit.CurrentAP,
                abilityCount = assignedAbilities.Count,
                actionAbilityCount = loadout != null ? loadout.GetActionAbilities().Count : 0,
                reactionAbilityCount = loadout != null ? loadout.GetReactionAbilities().Count : 0,
                abilities = ToAbilitySummaries(assignedAbilities)
            };
        }

        private static List<object> ToAbilitySummaries(IReadOnlyList<AbilityDefinition> abilities)
        {
            var summaries = new List<object>();
            if (abilities == null)
            {
                return summaries;
            }

            for (var i = 0; i < abilities.Count; i++)
            {
                var ability = abilities[i];
                if (ability == null)
                {
                    continue;
                }

                summaries.Add(new
                {
                    key = ability.AbilityKey,
                    name = ability.DisplayName,
                    usage = ability.Usage.ToString()
                });
            }

            return summaries;
        }

        private static object ToPositionSummary(GridPosition position)
        {
            return new
            {
                x = position.X,
                y = position.Y,
                z = position.Z
            };
        }

        private static object ToPositionSummary(Vector3 position)
        {
            return new
            {
                x = Math.Round(position.x, 3),
                y = Math.Round(position.y, 3),
                z = Math.Round(position.z, 3)
            };
        }

        private static string GetScenePath(GameObject gameObject)
        {
            var path = gameObject.name;
            var current = gameObject.transform.parent;
            while (current != null)
            {
                path = current.name + "/" + path;
                current = current.parent;
            }

            return path;
        }

        private readonly struct SpawnValidationResult
        {
            private SpawnValidationResult(bool isValid, string failureReason)
            {
                IsValid = isValid;
                FailureReason = failureReason;
            }

            public bool IsValid { get; }

            public string FailureReason { get; }

            public static SpawnValidationResult Success()
            {
                return new SpawnValidationResult(true, string.Empty);
            }

            public static SpawnValidationResult Failure(string failureReason)
            {
                return new SpawnValidationResult(false, failureReason);
            }
        }

        private readonly struct UnitSpawnSpec
        {
            public UnitSpawnSpec(string statsAssetName, TeamId team, GridPosition position)
            {
                StatsAssetName = statsAssetName;
                Team = team;
                Position = position;
            }

            public string StatsAssetName { get; }

            public TeamId Team { get; }

            public GridPosition Position { get; }

            public string StatsAssetPath
            {
                get { return UnitAssetFolderPath + "/" + StatsAssetName + ".asset"; }
            }
        }

        private readonly struct AbilityAssetSpec
        {
            public AbilityAssetSpec(string abilityKey, string assetName)
            {
                AbilityKey = abilityKey;
                AssetName = assetName;
            }

            public string AbilityKey { get; }

            public string AssetName { get; }

            public string AssetPath
            {
                get { return AbilityAssetFolderPath + "/" + AssetName + ".asset"; }
            }
        }

        private sealed class UnitAbilityLoadoutSpec
        {
            public UnitAbilityLoadoutSpec(string statsAssetName, params string[] abilityKeys)
            {
                StatsAssetName = statsAssetName;
                AbilityKeys = abilityKeys ?? Array.Empty<string>();
            }

            public string StatsAssetName { get; }

            public IReadOnlyList<string> AbilityKeys { get; }
        }
    }
}
