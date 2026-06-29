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
