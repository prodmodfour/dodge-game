using System;

namespace ReactionTactics.Grid
{
    /// <summary>
    /// Horizontal cardinal directions used for grid movement and targeting shapes.
    /// </summary>
    public enum CardinalDirection
    {
        North,
        East,
        South,
        West
    }

    public static class CardinalDirectionExtensions
    {
        /// <summary>
        /// Converts a direction to its one-cell horizontal grid offset.
        /// </summary>
        public static GridPosition ToOffset(this CardinalDirection direction)
        {
            switch (direction)
            {
                case CardinalDirection.North:
                    return GridPosition.North;
                case CardinalDirection.East:
                    return GridPosition.East;
                case CardinalDirection.South:
                    return GridPosition.South;
                case CardinalDirection.West:
                    return GridPosition.West;
                default:
                    throw new ArgumentOutOfRangeException(nameof(direction), direction, "Unknown cardinal direction.");
            }
        }

        public static CardinalDirection Left(this CardinalDirection direction)
        {
            switch (direction)
            {
                case CardinalDirection.North:
                    return CardinalDirection.West;
                case CardinalDirection.East:
                    return CardinalDirection.North;
                case CardinalDirection.South:
                    return CardinalDirection.East;
                case CardinalDirection.West:
                    return CardinalDirection.South;
                default:
                    throw new ArgumentOutOfRangeException(nameof(direction), direction, "Unknown cardinal direction.");
            }
        }

        public static CardinalDirection Right(this CardinalDirection direction)
        {
            switch (direction)
            {
                case CardinalDirection.North:
                    return CardinalDirection.East;
                case CardinalDirection.East:
                    return CardinalDirection.South;
                case CardinalDirection.South:
                    return CardinalDirection.West;
                case CardinalDirection.West:
                    return CardinalDirection.North;
                default:
                    throw new ArgumentOutOfRangeException(nameof(direction), direction, "Unknown cardinal direction.");
            }
        }

        public static CardinalDirection Opposite(this CardinalDirection direction)
        {
            return direction.Left().Left();
        }

        /// <summary>
        /// Returns the directions perpendicular to this direction in left/right order.
        /// </summary>
        public static (CardinalDirection Left, CardinalDirection Right) Perpendiculars(this CardinalDirection direction)
        {
            return (direction.Left(), direction.Right());
        }

        public static GridPosition LeftOffset(this CardinalDirection direction)
        {
            return direction.Left().ToOffset();
        }

        public static GridPosition RightOffset(this CardinalDirection direction)
        {
            return direction.Right().ToOffset();
        }
    }

    public static class CardinalDirectionMath
    {
        /// <summary>
        /// Chooses the dominant horizontal cardinal direction from origin to target.
        /// Diagonal ties are resolved deterministically toward the north/south axis.
        /// </summary>
        public static CardinalDirection FromTo(GridPosition origin, GridPosition target)
        {
            return FromDelta(target.X - origin.X, target.Z - origin.Z);
        }

        /// <summary>
        /// Chooses the dominant horizontal cardinal direction for an x/z delta.
        /// Diagonal ties are resolved deterministically toward the north/south axis.
        /// </summary>
        public static CardinalDirection FromDelta(int deltaX, int deltaZ)
        {
            if (deltaX == 0 && deltaZ == 0)
            {
                throw new ArgumentException("Cannot choose a cardinal direction from a zero horizontal delta.");
            }

            if (Math.Abs(deltaZ) >= Math.Abs(deltaX))
            {
                return deltaZ >= 0 ? CardinalDirection.North : CardinalDirection.South;
            }

            return deltaX >= 0 ? CardinalDirection.East : CardinalDirection.West;
        }
    }
}
