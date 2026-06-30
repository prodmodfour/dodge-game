using System;
using ReactionTactics.Grid;

namespace ReactionTactics.Pathfinding
{
    /// <summary>
    /// One ordered step in a grid path. The first step represents the starting cell
    /// and should have zero step and total AP cost.
    /// </summary>
    public readonly struct PathStep : IEquatable<PathStep>
    {
        public PathStep(GridPosition position, int stepApCost, int totalApCost)
        {
            if (stepApCost < 0)
            {
                throw new ArgumentOutOfRangeException(
                    nameof(stepApCost),
                    stepApCost,
                    "Step AP cost cannot be negative.");
            }

            if (totalApCost < 0)
            {
                throw new ArgumentOutOfRangeException(
                    nameof(totalApCost),
                    totalApCost,
                    "Total AP cost cannot be negative.");
            }

            if (stepApCost > totalApCost)
            {
                throw new ArgumentException(
                    "Step AP cost cannot exceed the cumulative total AP cost for the step.",
                    nameof(stepApCost));
            }

            Position = position;
            StepApCost = stepApCost;
            TotalApCost = totalApCost;
        }

        public GridPosition Position { get; }

        /// <summary>
        /// AP spent to enter this position from the previous path step. The starting
        /// step uses zero.
        /// </summary>
        public int StepApCost { get; }

        /// <summary>
        /// Cumulative AP spent from the start of the path through this step.
        /// </summary>
        public int TotalApCost { get; }

        public bool Equals(PathStep other)
        {
            return Position == other.Position
                && StepApCost == other.StepApCost
                && TotalApCost == other.TotalApCost;
        }

        public override bool Equals(object obj)
        {
            return obj is PathStep other && Equals(other);
        }

        public override int GetHashCode()
        {
            unchecked
            {
                var hash = 17;
                hash = (hash * 31) + Position.GetHashCode();
                hash = (hash * 31) + StepApCost;
                hash = (hash * 31) + TotalApCost;
                return hash;
            }
        }

        public override string ToString()
        {
            return $"{Position} stepAP={StepApCost} totalAP={TotalApCost}";
        }

        public static bool operator ==(PathStep left, PathStep right)
        {
            return left.Equals(right);
        }

        public static bool operator !=(PathStep left, PathStep right)
        {
            return !left.Equals(right);
        }
    }
}
