using System;

namespace ReactionTactics.Units
{
    /// <summary>
    /// Stable integer identity for a tactical unit.
    /// </summary>
    public readonly struct UnitId : IEquatable<UnitId>, IComparable<UnitId>, IComparable
    {
        public static readonly UnitId None = new UnitId(0, skipValidation: true);

        public UnitId(int value)
        {
            if (value <= 0)
            {
                throw new ArgumentOutOfRangeException(
                    nameof(value),
                    value,
                    "Assigned unit identifiers must be positive integers.");
            }

            Value = value;
        }

        private UnitId(int value, bool skipValidation)
        {
            Value = value;
        }

        public int Value { get; }

        public bool IsAssigned
        {
            get { return Value > 0; }
        }

        public static UnitId FromInt(int value)
        {
            return new UnitId(value);
        }

        public int CompareTo(UnitId other)
        {
            return Value.CompareTo(other.Value);
        }

        public int CompareTo(object obj)
        {
            if (obj == null)
            {
                return 1;
            }

            if (obj is UnitId other)
            {
                return CompareTo(other);
            }

            throw new ArgumentException("Object must be a UnitId.", nameof(obj));
        }

        public bool Equals(UnitId other)
        {
            return Value == other.Value;
        }

        public override bool Equals(object obj)
        {
            return obj is UnitId other && Equals(other);
        }

        public override int GetHashCode()
        {
            return Value;
        }

        public override string ToString()
        {
            return IsAssigned ? $"Unit#{Value}" : "Unit#None";
        }

        public static bool operator ==(UnitId left, UnitId right)
        {
            return left.Equals(right);
        }

        public static bool operator !=(UnitId left, UnitId right)
        {
            return !left.Equals(right);
        }

        public static bool operator <(UnitId left, UnitId right)
        {
            return left.CompareTo(right) < 0;
        }

        public static bool operator >(UnitId left, UnitId right)
        {
            return left.CompareTo(right) > 0;
        }

        public static bool operator <=(UnitId left, UnitId right)
        {
            return left.CompareTo(right) <= 0;
        }

        public static bool operator >=(UnitId left, UnitId right)
        {
            return left.CompareTo(right) >= 0;
        }
    }
}
