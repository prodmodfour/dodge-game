using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using ReactionTactics.Units;

namespace ReactionTactics.Turns
{
    /// <summary>
    /// Maintains deterministic active-turn order for the prototype combat loop.
    /// Living units act by team first, then by their stable UnitId, which currently
    /// represents spawn/order index from the unit spawner.
    /// </summary>
    public sealed class TurnOrderService
    {
        private readonly List<TacticalUnit> turnOrder = new List<TacticalUnit>();
        private readonly ReadOnlyCollection<TacticalUnit> readOnlyTurnOrder;
        private int currentIndex = -1;

        public TurnOrderService()
        {
            readOnlyTurnOrder = turnOrder.AsReadOnly();
        }

        /// <summary>
        /// Current living turn order sorted by team and stable spawn/order identity.
        /// Dead units are pruned when this list is queried.
        /// </summary>
        public IReadOnlyList<TacticalUnit> TurnOrder
        {
            get
            {
                PruneDeadUnits();
                return readOnlyTurnOrder;
            }
        }

        public TacticalUnit CurrentActiveUnit
        {
            get
            {
                return TryGetCurrentActiveUnit(out var unit) ? unit : null;
            }
        }

        public TacticalUnit NextActiveUnit
        {
            get
            {
                return TryGetNextActiveUnit(out var unit) ? unit : null;
            }
        }

        public bool HasCurrentActiveUnit
        {
            get { return CurrentActiveUnit != null; }
        }

        /// <summary>
        /// Rebuilds the ordered living-unit list and selects the first unit as current.
        /// </summary>
        public void SetUnits(IEnumerable<TacticalUnit> units)
        {
            var sortedUnits = BuildTurnOrder(units);
            turnOrder.Clear();
            turnOrder.AddRange(sortedUnits);
            currentIndex = turnOrder.Count > 0 ? 0 : -1;
        }

        /// <summary>
        /// Returns a deterministic, living-only active-turn order without mutating this service.
        /// </summary>
        public static IReadOnlyList<TacticalUnit> BuildTurnOrder(IEnumerable<TacticalUnit> units)
        {
            if (units == null)
            {
                throw new ArgumentNullException(nameof(units));
            }

            var sortedUnits = new List<TacticalUnit>();
            foreach (var unit in units)
            {
                if (IsLivingUnit(unit))
                {
                    sortedUnits.Add(unit);
                }
            }

            sortedUnits.Sort(CompareUnitsForTurnOrder);
            return sortedUnits.AsReadOnly();
        }

        public bool TryGetCurrentActiveUnit(out TacticalUnit unit)
        {
            PruneDeadUnits();

            if (currentIndex >= 0 && currentIndex < turnOrder.Count)
            {
                unit = turnOrder[currentIndex];
                return true;
            }

            unit = null;
            return false;
        }

        public bool TryGetNextActiveUnit(out TacticalUnit unit)
        {
            return TryGetNextActiveUnit(wrapAtEnd: false, out unit);
        }

        public bool TryGetNextActiveUnit(bool wrapAtEnd, out TacticalUnit unit)
        {
            PruneDeadUnits();

            if (turnOrder.Count == 0)
            {
                unit = null;
                return false;
            }

            var nextIndex = currentIndex >= 0 ? currentIndex + 1 : 0;
            if (nextIndex < turnOrder.Count)
            {
                unit = turnOrder[nextIndex];
                return true;
            }

            if (wrapAtEnd)
            {
                unit = turnOrder[0];
                return true;
            }

            unit = null;
            return false;
        }

        /// <summary>
        /// Advances to the next living unit in the current order. Returns false at the end
        /// of the order so later round-management code can decide when to start a new round.
        /// </summary>
        public bool TryAdvanceToNext()
        {
            return TryAdvanceToNext(wrapAtEnd: false);
        }

        public bool TryAdvanceToNext(bool wrapAtEnd)
        {
            PruneDeadUnits();

            if (turnOrder.Count == 0)
            {
                currentIndex = -1;
                return false;
            }

            var nextIndex = currentIndex >= 0 ? currentIndex + 1 : 0;
            if (nextIndex < turnOrder.Count)
            {
                currentIndex = nextIndex;
                return true;
            }

            if (wrapAtEnd)
            {
                currentIndex = 0;
                return true;
            }

            return false;
        }

        public bool TrySetCurrentActiveUnit(TacticalUnit unit)
        {
            PruneDeadUnits();

            if (!IsLivingUnit(unit))
            {
                return false;
            }

            var index = turnOrder.IndexOf(unit);
            if (index < 0)
            {
                return false;
            }

            currentIndex = index;
            return true;
        }

        public static int CompareUnitsForTurnOrder(TacticalUnit left, TacticalUnit right)
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

            var teamComparison = ((int)left.Team).CompareTo((int)right.Team);
            if (teamComparison != 0)
            {
                return teamComparison;
            }

            var idComparison = left.UnitId.CompareTo(right.UnitId);
            if (idComparison != 0)
            {
                return idComparison;
            }

            var nameComparison = string.Compare(left.name, right.name, StringComparison.Ordinal);
            if (nameComparison != 0)
            {
                return nameComparison;
            }

            return left.GetInstanceID().CompareTo(right.GetInstanceID());
        }

        private void PruneDeadUnits()
        {
            var previousCurrent = currentIndex >= 0 && currentIndex < turnOrder.Count
                ? turnOrder[currentIndex]
                : null;

            for (var i = turnOrder.Count - 1; i >= 0; i--)
            {
                if (!IsLivingUnit(turnOrder[i]))
                {
                    turnOrder.RemoveAt(i);
                }
            }

            if (turnOrder.Count == 0)
            {
                currentIndex = -1;
                return;
            }

            if (IsLivingUnit(previousCurrent))
            {
                currentIndex = turnOrder.IndexOf(previousCurrent);
                return;
            }

            if (currentIndex >= turnOrder.Count)
            {
                currentIndex = -1;
            }
        }

        private static bool IsLivingUnit(TacticalUnit unit)
        {
            return unit != null && unit.IsAlive;
        }
    }
}
