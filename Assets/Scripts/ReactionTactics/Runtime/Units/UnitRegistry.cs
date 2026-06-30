using System;
using System.Collections.Generic;
using ReactionTactics.Core;
using ReactionTactics.Grid;
using UnityEngine;

namespace ReactionTactics.Units
{
    /// <summary>
    /// Scene-level registry for tactical units. It is the runtime source of truth for
    /// unit lookup and grid occupancy while keeping terrain ownership in grid systems.
    /// Occupancy is read directly from each registered unit's current grid position, so
    /// movement commands update occupancy by moving the unit rather than maintaining a
    /// separate cell cache.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class UnitRegistry : MonoBehaviour, IGridOccupancy
    {
        private readonly Dictionary<UnitId, TacticalUnit> unitsById = new Dictionary<UnitId, TacticalUnit>();

        public int RegisteredCount
        {
            get
            {
                PruneDestroyedUnits();
                return unitsById.Count;
            }
        }

        public int LivingCount
        {
            get { return CountUnits(unit => unit.IsAlive); }
        }

        public int DeadCount
        {
            get { return CountUnits(unit => unit.IsDead); }
        }

        /// <summary>
        /// Registers an initialized tactical unit by stable unit ID.
        /// Living units may not be registered into a cell already occupied by another
        /// living registered unit.
        /// </summary>
        public TacticalResult Register(TacticalUnit unit)
        {
            PruneDestroyedUnits();

            if (unit == null)
            {
                return TacticalResult.Failure("Cannot register a null tactical unit.");
            }

            if (!unit.IsInitialized)
            {
                return TacticalResult.Failure($"Cannot register uninitialized tactical unit '{unit.name}'.");
            }

            if (unitsById.TryGetValue(unit.UnitId, out var existingUnit))
            {
                var existingName = existingUnit != null ? existingUnit.DisplayName : unit.UnitId.ToString();
                return TacticalResult.Failure($"Unit ID {unit.UnitId} is already registered to {existingName}.");
            }

            if (unit.IsAlive && TryGetUnitAt(unit.CurrentGridPosition, out var occupant))
            {
                return TacticalResult.Failure(
                    $"Cell {unit.CurrentGridPosition} is already occupied by {occupant.DisplayName} {occupant.UnitId}.");
            }

            unitsById.Add(unit.UnitId, unit);
            return TacticalResult.Success();
        }

        public bool Unregister(TacticalUnit unit)
        {
            PruneDestroyedUnits();

            if (unit == null)
            {
                return false;
            }

            if (unit.UnitId.IsAssigned
                && unitsById.TryGetValue(unit.UnitId, out var registeredUnit)
                && ReferenceEquals(registeredUnit, unit))
            {
                unitsById.Remove(unit.UnitId);
                return true;
            }

            var unitIdToRemove = UnitId.None;
            foreach (var pair in unitsById)
            {
                if (ReferenceEquals(pair.Value, unit))
                {
                    unitIdToRemove = pair.Key;
                    break;
                }
            }

            return unitIdToRemove.IsAssigned && unitsById.Remove(unitIdToRemove);
        }

        public bool Unregister(UnitId unitId)
        {
            PruneDestroyedUnits();

            return unitId.IsAssigned && unitsById.Remove(unitId);
        }

        public void Clear()
        {
            unitsById.Clear();
        }

        public bool TryGetUnit(UnitId unitId, out TacticalUnit unit)
        {
            PruneDestroyedUnits();

            if (!unitId.IsAssigned || !unitsById.TryGetValue(unitId, out unit) || unit == null)
            {
                unit = null;
                return false;
            }

            return true;
        }

        public bool TryGetLivingUnit(UnitId unitId, out TacticalUnit unit)
        {
            if (TryGetUnit(unitId, out unit) && unit.IsAlive)
            {
                return true;
            }

            unit = null;
            return false;
        }

        public IReadOnlyList<TacticalUnit> GetRegisteredUnits()
        {
            return GetUnitsWhere(unit => true);
        }

        public IReadOnlyList<TacticalUnit> GetLivingUnits()
        {
            return GetUnitsWhere(unit => unit.IsAlive);
        }

        public IReadOnlyList<TacticalUnit> GetDeadUnits()
        {
            return GetUnitsWhere(unit => unit.IsDead);
        }

        public IReadOnlyList<TacticalUnit> GetUnitsByAliveStatus(bool isAlive)
        {
            return GetUnitsWhere(unit => unit.IsAlive == isAlive);
        }

        public IReadOnlyList<TacticalUnit> GetUnitsByTeam(TeamId team, bool livingOnly = true)
        {
            ValidateTeam(team);
            return GetUnitsWhere(unit => unit.Team == team && (!livingOnly || unit.IsAlive));
        }

        public IReadOnlyList<TacticalUnit> GetUnitsAt(GridPosition position, bool livingOnly = true)
        {
            return GetUnitsWhere(unit => unit.CurrentGridPosition == position && (!livingOnly || unit.IsAlive));
        }

        public bool TryGetUnitAt(GridPosition position, out TacticalUnit unit, bool livingOnly = true)
        {
            return TryGetFirstUnitWhere(
                candidate => candidate.CurrentGridPosition == position && (!livingOnly || candidate.IsAlive),
                out unit);
        }

        public bool IsOccupied(GridPosition position)
        {
            return TryGetUnitAt(position, out _);
        }

        public bool TryGetOccupant(GridPosition position, out UnitId occupantId)
        {
            if (TryGetUnitAt(position, out var unit))
            {
                occupantId = unit.UnitId;
                return true;
            }

            occupantId = UnitId.None;
            return false;
        }

        public bool CanEnter(GridPosition position)
        {
            return !IsOccupied(position);
        }

        private int CountUnits(Predicate<TacticalUnit> predicate)
        {
            PruneDestroyedUnits();

            var count = 0;
            foreach (var unit in unitsById.Values)
            {
                if (unit != null && predicate(unit))
                {
                    count += 1;
                }
            }

            return count;
        }

        private IReadOnlyList<TacticalUnit> GetUnitsWhere(Predicate<TacticalUnit> predicate)
        {
            PruneDestroyedUnits();

            var units = new List<TacticalUnit>();
            foreach (var unit in unitsById.Values)
            {
                if (unit != null && predicate(unit))
                {
                    units.Add(unit);
                }
            }

            units.Sort(CompareUnitsById);
            return units;
        }

        private bool TryGetFirstUnitWhere(Predicate<TacticalUnit> predicate, out TacticalUnit unit)
        {
            PruneDestroyedUnits();

            unit = null;
            foreach (var candidate in unitsById.Values)
            {
                if (candidate == null || !predicate(candidate))
                {
                    continue;
                }

                if (unit == null || candidate.UnitId < unit.UnitId)
                {
                    unit = candidate;
                }
            }

            return unit != null;
        }

        private void PruneDestroyedUnits()
        {
            List<UnitId> destroyedIds = null;
            foreach (var pair in unitsById)
            {
                if (pair.Value == null)
                {
                    if (destroyedIds == null)
                    {
                        destroyedIds = new List<UnitId>();
                    }

                    destroyedIds.Add(pair.Key);
                }
            }

            if (destroyedIds == null)
            {
                return;
            }

            foreach (var unitId in destroyedIds)
            {
                unitsById.Remove(unitId);
            }
        }

        private void OnDestroy()
        {
            Clear();
        }

        private static int CompareUnitsById(TacticalUnit left, TacticalUnit right)
        {
            if (ReferenceEquals(left, right))
            {
                return 0;
            }

            if (left == null)
            {
                return 1;
            }

            if (right == null)
            {
                return -1;
            }

            return left.UnitId.CompareTo(right.UnitId);
        }

        private static void ValidateTeam(TeamId team)
        {
            if (!Enum.IsDefined(typeof(TeamId), team))
            {
                throw new ArgumentOutOfRangeException(nameof(team), team, "Unknown tactical unit team.");
            }
        }
    }
}
