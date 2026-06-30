using System;
using ReactionTactics.Grid;

namespace ReactionTactics.Pathfinding
{
    /// <summary>
    /// Captures the cheapest known AP cost for reaching a cell during movement-range
    /// searches, plus an optional predecessor for later path reconstruction.
    /// </summary>
    public readonly struct ReachableCell : IEquatable<ReachableCell>
    {
        public ReachableCell(GridPosition position, int totalApCost, GridPosition? previousPosition = null)
        {
            if (totalApCost < 0)
            {
                throw new ArgumentOutOfRangeException(
                    nameof(totalApCost),
                    totalApCost,
                    "Reachable cell AP cost cannot be negative.");
            }

            Position = position;
            TotalApCost = totalApCost;
            PreviousPosition = previousPosition;
        }

        public GridPosition Position { get; }

        /// <summary>
        /// Cheapest known cumulative AP cost to reach this cell from the search origin.
        /// </summary>
        public int TotalApCost { get; }

        /// <summary>
        /// Optional predecessor used by path reconstruction. The start cell has no predecessor.
        /// </summary>
        public GridPosition? PreviousPosition { get; }

        public bool HasPreviousPosition
        {
            get { return PreviousPosition.HasValue; }
        }

        public bool Equals(ReachableCell other)
        {
            return Position == other.Position
                && TotalApCost == other.TotalApCost
                && Nullable.Equals(PreviousPosition, other.PreviousPosition);
        }

        public override bool Equals(object obj)
        {
            return obj is ReachableCell other && Equals(other);
        }

        public override int GetHashCode()
        {
            unchecked
            {
                var hash = 17;
                hash = (hash * 31) + Position.GetHashCode();
                hash = (hash * 31) + TotalApCost;
                hash = (hash * 31) + (PreviousPosition.HasValue ? PreviousPosition.Value.GetHashCode() : 0);
                return hash;
            }
        }

        public override string ToString()
        {
            return PreviousPosition.HasValue
                ? $"{Position} totalAP={TotalApCost} previous={PreviousPosition.Value}"
                : $"{Position} totalAP={TotalApCost}";
        }

        public static bool operator ==(ReachableCell left, ReachableCell right)
        {
            return left.Equals(right);
        }

        public static bool operator !=(ReachableCell left, ReachableCell right)
        {
            return !left.Equals(right);
        }
    }
}
