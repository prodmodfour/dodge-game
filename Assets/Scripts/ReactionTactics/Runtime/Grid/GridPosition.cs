using System;

namespace ReactionTactics.Grid
{
    /// <summary>
    /// Immutable integer coordinate for the prototype's discrete 3D tactical grid.
    /// </summary>
    public readonly struct GridPosition : IEquatable<GridPosition>
    {
        public static readonly GridPosition Zero = new GridPosition(0, 0, 0);
        public static readonly GridPosition Origin = Zero;
        public static readonly GridPosition Up = new GridPosition(0, 1, 0);
        public static readonly GridPosition Down = new GridPosition(0, -1, 0);
        public static readonly GridPosition North = new GridPosition(0, 0, 1);
        public static readonly GridPosition South = new GridPosition(0, 0, -1);
        public static readonly GridPosition East = new GridPosition(1, 0, 0);
        public static readonly GridPosition West = new GridPosition(-1, 0, 0);

        public GridPosition(int x, int y, int z)
        {
            X = x;
            Y = y;
            Z = z;
        }

        /// <summary>
        /// Creates a horizontal grid position at ground height using x/z coordinates.
        /// </summary>
        public GridPosition(int x, int z)
            : this(x, 0, z)
        {
        }

        public int X { get; }

        public int Y { get; }

        public int Z { get; }

        public static GridPosition FromHorizontal(int x, int z)
        {
            return new GridPosition(x, 0, z);
        }

        public GridPosition WithX(int x)
        {
            return new GridPosition(x, Y, Z);
        }

        public GridPosition WithY(int y)
        {
            return new GridPosition(X, y, Z);
        }

        public GridPosition WithZ(int z)
        {
            return new GridPosition(X, Y, z);
        }

        public GridPosition ToHorizontal()
        {
            return new GridPosition(X, 0, Z);
        }

        /// <summary>
        /// Returns the full 3D Manhattan distance to another grid position.
        /// </summary>
        public int ManhattanDistanceTo(GridPosition other)
        {
            return ManhattanDistance(this, other);
        }

        /// <summary>
        /// Returns the horizontal x/z Manhattan distance to another grid position, ignoring height.
        /// </summary>
        public int HorizontalDistanceTo(GridPosition other)
        {
            return HorizontalDistance(this, other);
        }

        /// <summary>
        /// Returns horizontal distance plus vertical delta multiplied by a configurable weight.
        /// </summary>
        public int TacticalDistanceTo(GridPosition other, int verticalWeight = 1)
        {
            return TacticalDistance(this, other, verticalWeight);
        }

        /// <summary>
        /// Returns true when the other position is one horizontal cardinal step away.
        /// Optionally rejects adjacent cells whose height delta exceeds maxHeightDifference.
        /// </summary>
        public bool IsFourWayAdjacentTo(GridPosition other, int? maxHeightDifference = null)
        {
            return AreFourWayAdjacent(this, other, maxHeightDifference);
        }

        public static int ManhattanDistance(GridPosition from, GridPosition to)
        {
            var delta = from - to;
            return Math.Abs(delta.X) + Math.Abs(delta.Y) + Math.Abs(delta.Z);
        }

        public static int HorizontalDistance(GridPosition from, GridPosition to)
        {
            return Math.Abs(from.X - to.X) + Math.Abs(from.Z - to.Z);
        }

        public static int TacticalDistance(GridPosition from, GridPosition to, int verticalWeight = 1)
        {
            if (verticalWeight < 0)
            {
                throw new ArgumentOutOfRangeException(
                    nameof(verticalWeight),
                    verticalWeight,
                    "Vertical distance weight cannot be negative.");
            }

            var horizontalDistance = HorizontalDistance(from, to);
            var verticalDistance = Math.Abs(from.Y - to.Y);
            return horizontalDistance + (verticalDistance * verticalWeight);
        }

        public static bool AreFourWayAdjacent(GridPosition first, GridPosition second, int? maxHeightDifference = null)
        {
            if (maxHeightDifference.HasValue && maxHeightDifference.Value < 0)
            {
                throw new ArgumentOutOfRangeException(
                    nameof(maxHeightDifference),
                    maxHeightDifference.Value,
                    "Maximum height difference cannot be negative.");
            }

            if (HorizontalDistance(first, second) != 1)
            {
                return false;
            }

            var heightDifference = Math.Abs(first.Y - second.Y);
            return !maxHeightDifference.HasValue || heightDifference <= maxHeightDifference.Value;
        }

        public bool Equals(GridPosition other)
        {
            return X == other.X && Y == other.Y && Z == other.Z;
        }

        public override bool Equals(object obj)
        {
            return obj is GridPosition other && Equals(other);
        }

        public override int GetHashCode()
        {
            unchecked
            {
                var hash = 17;
                hash = (hash * 31) + X;
                hash = (hash * 31) + Y;
                hash = (hash * 31) + Z;
                return hash;
            }
        }

        public override string ToString()
        {
            return $"({X},{Y},{Z})";
        }

        public static GridPosition operator +(GridPosition left, GridPosition right)
        {
            return new GridPosition(left.X + right.X, left.Y + right.Y, left.Z + right.Z);
        }

        public static GridPosition operator -(GridPosition left, GridPosition right)
        {
            return new GridPosition(left.X - right.X, left.Y - right.Y, left.Z - right.Z);
        }

        public static bool operator ==(GridPosition left, GridPosition right)
        {
            return left.Equals(right);
        }

        public static bool operator !=(GridPosition left, GridPosition right)
        {
            return !left.Equals(right);
        }
    }
}
