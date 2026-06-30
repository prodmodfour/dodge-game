using System;
using ReactionTactics.Core;
using ReactionTactics.Grid;
using UnityEngine;

namespace ReactionTactics.Units
{
    /// <summary>
    /// Scene service that creates tactical units from the prototype unit prefab,
    /// initializes their runtime state, snaps them to terrain cell centers, and
    /// registers them for immediate occupancy queries.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class UnitSpawner : MonoBehaviour
    {
        private const int DefaultFirstUnitId = 1;

        [SerializeField]
        [Tooltip("Prefab containing a TacticalUnit component to instantiate for spawned combatants.")]
        private TacticalUnit unitPrefab;

        [SerializeField]
        [Tooltip("Scene registry that owns unit lookup and grid occupancy after spawn.")]
        private UnitRegistry unitRegistry;

        [SerializeField]
        [Tooltip("Active grid manager used to validate terrain cells and convert grid positions to world positions.")]
        private GridManager gridManager;

        [SerializeField]
        [Tooltip("Optional parent for spawned unit instances. Defaults to this spawner's transform.")]
        private Transform spawnParent;

        [SerializeField]
        [Min(DefaultFirstUnitId)]
        [Tooltip("First runtime UnitId assigned by this spawner after scene startup or sequence reset.")]
        private int firstUnitId = DefaultFirstUnitId;

        private UnitIdGenerator unitIdGenerator;

        public TacticalUnit UnitPrefab
        {
            get { return unitPrefab; }
        }

        public UnitRegistry Registry
        {
            get { return unitRegistry; }
        }

        public GridManager GridManager
        {
            get { return gridManager; }
        }

        public Transform SpawnParent
        {
            get { return spawnParent; }
        }

        public int FirstUnitId
        {
            get { return Mathf.Max(DefaultFirstUnitId, firstUnitId); }
        }

        /// <summary>
        /// Assigns scene dependencies in one step for editor setup tools and tests.
        /// </summary>
        public void Configure(
            TacticalUnit unitPrefab,
            UnitRegistry unitRegistry,
            GridManager gridManager,
            Transform spawnParent = null)
        {
            this.unitPrefab = unitPrefab;
            this.unitRegistry = unitRegistry;
            this.gridManager = gridManager;
            this.spawnParent = spawnParent;
            ResetUnitIdSequence(FirstUnitId);
        }

        /// <summary>
        /// Resets the deterministic runtime ID sequence used for subsequent spawns.
        /// </summary>
        public void ResetUnitIdSequence(int nextUnitId = DefaultFirstUnitId)
        {
            if (nextUnitId < DefaultFirstUnitId)
            {
                throw new ArgumentOutOfRangeException(
                    nameof(nextUnitId),
                    nextUnitId,
                    "The next spawned unit ID must be a positive integer.");
            }

            firstUnitId = nextUnitId;
            unitIdGenerator = new UnitIdGenerator(nextUnitId);
        }

        /// <summary>
        /// Validates that a unit can be spawned at the requested terrain cell.
        /// </summary>
        public TacticalResult<GridCell> ValidateSpawnCell(GridPosition position)
        {
            var dependencyResult = TryGetMapAndRegistry(out var map, out var registry);
            if (dependencyResult.IsFailure)
            {
                return TacticalResult<GridCell>.Failure(dependencyResult.ErrorMessage);
            }

            if (!TryResolveSpawnCell(map, position, out var cell, out var resolutionFailure))
            {
                return TacticalResult<GridCell>.Failure(resolutionFailure);
            }

            if (!cell.Walkable || cell.BlocksMovement)
            {
                return TacticalResult<GridCell>.Failure($"Spawn cell {cell.Position} is blocked or unwalkable.");
            }

            if (!registry.CanEnter(cell.Position))
            {
                return TacticalResult<GridCell>.Failure($"Spawn cell {cell.Position} is already occupied.");
            }

            return TacticalResult<GridCell>.Success(cell);
        }

        /// <summary>
        /// Spawns a unit using the spawner's configured prefab and scene dependencies.
        /// </summary>
        public TacticalResult<TacticalUnit> Spawn(
            UnitStatsDefinition statsDefinition,
            TeamId team,
            GridPosition position)
        {
            return Spawn(unitPrefab, statsDefinition, team, position);
        }

        /// <summary>
        /// Spawns a unit from a specific prefab, initializes HP/AP/team/position, snaps the
        /// instance to the grid cell center, and registers it for occupancy immediately.
        /// Expected invalid spawn requests return failures and do not create units.
        /// </summary>
        public TacticalResult<TacticalUnit> Spawn(
            TacticalUnit prefab,
            UnitStatsDefinition statsDefinition,
            TeamId team,
            GridPosition position)
        {
            var inputResult = ValidateSpawnInputs(prefab, statsDefinition, team);
            if (inputResult.IsFailure)
            {
                return TacticalResult<TacticalUnit>.Failure(inputResult.ErrorMessage);
            }

            var cellResult = ValidateSpawnCell(position);
            if (cellResult.IsFailure)
            {
                return TacticalResult<TacticalUnit>.Failure(cellResult.ErrorMessage);
            }

            var gridMetrics = gridManager.Metrics;
            var unitId = GetUnitIdGenerator().Next();
            var parent = spawnParent != null ? spawnParent : transform;
            var unit = Instantiate(prefab, parent);
            unit.name = BuildUnitName(statsDefinition, team, unitId);

            try
            {
                unit.Initialize(unitId, team, statsDefinition, cellResult.Value.Position);
                ApplyUnitVisuals(unit);
                SnapUnitToCell(unit, cellResult.Value.Position, gridMetrics, statsDefinition);

                var registerResult = unitRegistry.Register(unit);
                if (registerResult.IsFailure)
                {
                    DestroySpawnedUnit(unit);
                    return TacticalResult<TacticalUnit>.Failure(registerResult.ErrorMessage);
                }

                return TacticalResult<TacticalUnit>.Success(unit);
            }
            catch (Exception exception)
            {
                DestroySpawnedUnit(unit);
                return TacticalResult<TacticalUnit>.Failure($"Failed to spawn unit at {position}: {exception.Message}");
            }
        }

        private void Awake()
        {
            EnsureUnitIdGenerator();
        }

        private void Reset()
        {
            firstUnitId = DefaultFirstUnitId;
            spawnParent = transform;
        }

        private void OnValidate()
        {
            if (firstUnitId < DefaultFirstUnitId)
            {
                firstUnitId = DefaultFirstUnitId;
            }
        }

        private TacticalResult ValidateSpawnInputs(TacticalUnit prefab, UnitStatsDefinition statsDefinition, TeamId team)
        {
            if (prefab == null)
            {
                return TacticalResult.Failure("Unit spawner has no unit prefab assigned.");
            }

            if (statsDefinition == null)
            {
                return TacticalResult.Failure("Cannot spawn a unit without a stats definition.");
            }

            if (!Enum.IsDefined(typeof(TeamId), team))
            {
                return TacticalResult.Failure($"Cannot spawn a unit for unknown team '{team}'.");
            }

            return TacticalResult.Success();
        }

        private TacticalResult TryGetMapAndRegistry(out IGridMap map, out UnitRegistry registry)
        {
            map = null;
            registry = null;

            if (gridManager == null)
            {
                return TacticalResult.Failure("Unit spawner has no grid manager assigned.");
            }

            if (!gridManager.HasCurrentMap && !gridManager.RebuildMap())
            {
                return TacticalResult.Failure("Unit spawner cannot spawn because the grid manager has no current map.");
            }

            map = gridManager.CurrentMap;
            if (map == null)
            {
                return TacticalResult.Failure("Unit spawner cannot spawn because the grid manager has no current map.");
            }

            if (unitRegistry == null)
            {
                return TacticalResult.Failure("Unit spawner has no unit registry assigned.");
            }

            registry = unitRegistry;
            return TacticalResult.Success();
        }

        private UnitIdGenerator GetUnitIdGenerator()
        {
            EnsureUnitIdGenerator();
            return unitIdGenerator;
        }

        private static bool TryResolveSpawnCell(
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
                    failureReason =
                        $"Spawn coordinate ({requestedPosition.X},{requestedPosition.Z}) matches multiple height cells and is ambiguous.";
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
            failureReason = $"No spawn cell exists at {requestedPosition}.";
            return false;
        }

        private void EnsureUnitIdGenerator()
        {
            if (unitIdGenerator == null)
            {
                unitIdGenerator = new UnitIdGenerator(FirstUnitId);
            }
        }

        private static void ApplyUnitVisuals(TacticalUnit unit)
        {
            var teamVisualView = unit != null ? unit.GetComponent<UnitTeamVisualView>() : null;
            if (teamVisualView != null)
            {
                teamVisualView.ApplyTeamVisuals();
            }
        }

        private static void SnapUnitToCell(
            TacticalUnit unit,
            GridPosition position,
            GridMetrics metrics,
            UnitStatsDefinition statsDefinition)
        {
            var mover = unit.GetComponent<GridPathMover>();
            if (mover != null)
            {
                mover.Metrics = metrics;
                mover.MovementSpeed = statsDefinition.MovementAnimationSpeed;
                mover.SnapTo(position, metrics);
                return;
            }

            unit.transform.position = metrics.GridToWorldCenter(position);
        }

        private static string BuildUnitName(UnitStatsDefinition statsDefinition, TeamId team, UnitId unitId)
        {
            return $"{statsDefinition.DisplayName} {team} {unitId}";
        }

        private static void DestroySpawnedUnit(TacticalUnit unit)
        {
            if (unit == null)
            {
                return;
            }

            if (Application.isPlaying)
            {
                Destroy(unit.gameObject);
            }
            else
            {
                DestroyImmediate(unit.gameObject);
            }
        }
    }
}
