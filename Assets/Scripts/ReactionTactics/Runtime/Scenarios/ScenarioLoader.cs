using System.Collections.Generic;
using ReactionTactics.Actions;
using ReactionTactics.Core;
using ReactionTactics.Grid;
using ReactionTactics.Turns;
using ReactionTactics.Units;
using UnityEngine;

namespace ReactionTactics.Scenarios
{
    /// <summary>
    /// Runtime scene bootstrapper that turns a scenario definition into the active
    /// map and spawned unit roster before combat starts.
    /// </summary>
    [DefaultExecutionOrder(-500)]
    [DisallowMultipleComponent]
    public sealed class ScenarioLoader : MonoBehaviour
    {
        [SerializeField]
        [Tooltip("Scenario asset containing the map, unit entries, start cells, and optional ability loadouts.")]
        private ScenarioDefinition scenarioDefinition;

        [SerializeField]
        [Tooltip("Grid manager that owns the active runtime map.")]
        private GridManager gridManager;

        [SerializeField]
        [Tooltip("Spawner used to instantiate scenario units.")]
        private UnitSpawner unitSpawner;

        [SerializeField]
        [Tooltip("Combat manager reset before loading so spawned units are ready before StartCombat runs.")]
        private CombatManager combatManager;

        [SerializeField]
        [Tooltip("Load the assigned scenario during Awake in play mode, before CombatManager.Start can start combat.")]
        private bool loadOnAwake = true;

        [SerializeField]
        [Tooltip("Clear previously spawned scenario units and registry state before loading. This makes repeated loads idempotent.")]
        private bool clearExistingUnitsBeforeLoad = true;

        private readonly List<TacticalUnit> spawnedUnits = new List<TacticalUnit>();

        public ScenarioDefinition ScenarioDefinition
        {
            get { return scenarioDefinition; }
        }

        public GridManager GridManager
        {
            get { return gridManager; }
        }

        public UnitSpawner UnitSpawner
        {
            get { return unitSpawner; }
        }

        public CombatManager CombatManager
        {
            get { return combatManager; }
        }

        public bool LoadOnAwake
        {
            get { return loadOnAwake; }
            set { loadOnAwake = value; }
        }

        public IReadOnlyList<TacticalUnit> SpawnedUnits
        {
            get { return spawnedUnits; }
        }

        public int SpawnedUnitCount
        {
            get
            {
                PruneDestroyedSpawnedUnits();
                return spawnedUnits.Count;
            }
        }

        /// <summary>
        /// Assigns loader dependencies in one step for editor setup tools and tests.
        /// </summary>
        public void Configure(
            ScenarioDefinition scenarioDefinition,
            GridManager gridManager,
            UnitSpawner unitSpawner,
            CombatManager combatManager = null)
        {
            this.scenarioDefinition = scenarioDefinition;
            this.gridManager = gridManager;
            this.unitSpawner = unitSpawner;
            this.combatManager = combatManager;
        }

        /// <summary>
        /// Loads the configured scenario into the scene. The operation is safe to call
        /// repeatedly: previous loader-spawned units are removed, the registry is
        /// cleared, unit IDs restart from the spawner's first ID, and combat state is
        /// returned to NotStarted so CombatManager can start from the fresh roster.
        /// </summary>
        public TacticalResult LoadScenario()
        {
            ResolveSceneReferences();

            var dependencyResult = ValidateDependencies();
            if (dependencyResult.IsFailure)
            {
                return dependencyResult;
            }

            var scenarioValidation = scenarioDefinition.ValidateScenario();
            if (scenarioValidation.IsFailure)
            {
                return TacticalResult.Failure($"Cannot load scenario '{scenarioDefinition.name}': {scenarioValidation.ErrorMessage}");
            }

            combatManager?.ResetForScenarioLoad();

            if (!gridManager.SetMapDefinition(scenarioDefinition.MapDefinition, rebuild: true))
            {
                return TacticalResult.Failure($"Cannot load scenario '{scenarioDefinition.name}' because the grid map could not be built.");
            }

            if (clearExistingUnitsBeforeLoad)
            {
                ClearSpawnedUnits();
            }

            unitSpawner.ResetUnitIdSequence(unitSpawner.FirstUnitId);

            foreach (var entry in scenarioDefinition.UnitEntries)
            {
                var spawnResult = unitSpawner.Spawn(entry.Stats, entry.Team, entry.StartingCell);
                if (spawnResult.IsFailure)
                {
                    ClearSpawnedUnits();
                    return TacticalResult.Failure(
                        $"Failed to spawn scenario unit '{DescribeStats(entry.Stats)}' at {entry.StartingCell}: {spawnResult.ErrorMessage}");
                }

                var unit = spawnResult.Value;
                ApplyAbilityLoadoutOverride(unit, entry);
                spawnedUnits.Add(unit);
            }

            return TacticalResult.Success();
        }

        /// <summary>
        /// Removes units spawned by this loader and clears the configured registry.
        /// </summary>
        public void ClearSpawnedUnits()
        {
            PruneDestroyedSpawnedUnits();

            for (var i = spawnedUnits.Count - 1; i >= 0; i--)
            {
                DestroySpawnedUnit(spawnedUnits[i]);
            }

            spawnedUnits.Clear();
            ClearTacticalUnitChildren(GetSpawnParent());

            var registry = unitSpawner != null ? unitSpawner.Registry : null;
            if (registry != null)
            {
                registry.Clear();
            }
        }

        private void Awake()
        {
            if (!Application.isPlaying || !loadOnAwake)
            {
                return;
            }

            var result = LoadScenario();
            if (result.IsFailure)
            {
                Debug.LogError($"{nameof(ScenarioLoader)} failed to load scenario: {result.ErrorMessage}", this);
            }
        }

        private void Reset()
        {
            loadOnAwake = true;
            clearExistingUnitsBeforeLoad = true;
            ResolveSceneReferences();
        }

        private TacticalResult ValidateDependencies()
        {
            if (scenarioDefinition == null)
            {
                return TacticalResult.Failure($"{nameof(ScenarioLoader)} requires a {nameof(ScenarioDefinition)} reference.");
            }

            if (gridManager == null)
            {
                return TacticalResult.Failure($"{nameof(ScenarioLoader)} requires a {nameof(GridManager)} reference.");
            }

            if (unitSpawner == null)
            {
                return TacticalResult.Failure($"{nameof(ScenarioLoader)} requires a {nameof(UnitSpawner)} reference.");
            }

            if (unitSpawner.UnitPrefab == null)
            {
                return TacticalResult.Failure($"{nameof(ScenarioLoader)} cannot load because {nameof(UnitSpawner)} has no unit prefab assigned.");
            }

            if (unitSpawner.Registry == null)
            {
                return TacticalResult.Failure($"{nameof(ScenarioLoader)} cannot load because {nameof(UnitSpawner)} has no {nameof(UnitRegistry)} assigned.");
            }

            if (unitSpawner.GridManager == null)
            {
                unitSpawner.Configure(unitSpawner.UnitPrefab, unitSpawner.Registry, gridManager, unitSpawner.SpawnParent);
            }
            else if (!ReferenceEquals(unitSpawner.GridManager, gridManager))
            {
                return TacticalResult.Failure(
                    $"{nameof(ScenarioLoader)} grid manager does not match the grid manager assigned to {nameof(UnitSpawner)}.");
            }

            return TacticalResult.Success();
        }

        private void ResolveSceneReferences()
        {
            if (gridManager == null)
            {
                gridManager = FindAnyObjectByType<GridManager>();
            }

            if (unitSpawner == null)
            {
                unitSpawner = FindAnyObjectByType<UnitSpawner>();
            }

            if (combatManager == null)
            {
                combatManager = FindAnyObjectByType<CombatManager>();
            }
        }

        private Transform GetSpawnParent()
        {
            if (unitSpawner == null)
            {
                return null;
            }

            return unitSpawner.SpawnParent != null ? unitSpawner.SpawnParent : unitSpawner.transform;
        }

        private void ClearTacticalUnitChildren(Transform parent)
        {
            if (parent == null)
            {
                return;
            }

            for (var childIndex = parent.childCount - 1; childIndex >= 0; childIndex--)
            {
                var child = parent.GetChild(childIndex);
                if (child == null || child.GetComponent<TacticalUnit>() == null)
                {
                    continue;
                }

                DestroySpawnedObject(child.gameObject);
            }
        }

        private void PruneDestroyedSpawnedUnits()
        {
            for (var i = spawnedUnits.Count - 1; i >= 0; i--)
            {
                if (spawnedUnits[i] == null)
                {
                    spawnedUnits.RemoveAt(i);
                }
            }
        }

        private static void ApplyAbilityLoadoutOverride(TacticalUnit unit, ScenarioDefinition.UnitEntry entry)
        {
            if (unit == null || entry == null || !entry.HasAbilityLoadoutOverride)
            {
                return;
            }

            var loadout = unit.GetComponent<UnitAbilityLoadout>();
            if (loadout == null)
            {
                loadout = unit.gameObject.AddComponent<UnitAbilityLoadout>();
            }

            loadout.SetAbilities(entry.AbilityLoadout);
        }

        private static string DescribeStats(UnitStatsDefinition stats)
        {
            return stats != null ? stats.DisplayName : "missing stats";
        }

        private static void DestroySpawnedUnit(TacticalUnit unit)
        {
            if (unit == null)
            {
                return;
            }

            DestroySpawnedObject(unit.gameObject);
        }

        private static void DestroySpawnedObject(Object target)
        {
            if (target == null)
            {
                return;
            }

            if (Application.isPlaying)
            {
                Destroy(target);
            }
            else
            {
                DestroyImmediate(target);
            }
        }
    }
}
