using System;

namespace ReactionTactics.Units
{
    /// <summary>
    /// Describes the deterministic source of HP damage for logs, events, and tests.
    /// It intentionally contains no hit chance, dodge chance, or random-roll data.
    /// </summary>
    public readonly struct DamageSource : IEquatable<DamageSource>
    {
        private const string DefaultDescription = "Unspecified damage";

        private readonly string description;

        public DamageSource(string description)
            : this(UnitId.None, description)
        {
        }

        public DamageSource(UnitId sourceUnitId, string description)
        {
            SourceUnitId = sourceUnitId;
            this.description = NormalizeDescription(description);
        }

        public UnitId SourceUnitId { get; }

        public bool HasSourceUnit
        {
            get { return SourceUnitId.IsAssigned; }
        }

        public string Description
        {
            get { return string.IsNullOrWhiteSpace(description) ? DefaultDescription : description; }
        }

        public static DamageSource Unspecified
        {
            get { return new DamageSource(UnitId.None, DefaultDescription); }
        }

        public static DamageSource FromUnit(UnitId sourceUnitId, string description)
        {
            if (!sourceUnitId.IsAssigned)
            {
                throw new ArgumentException("Damage from a unit requires an assigned source unit ID.", nameof(sourceUnitId));
            }

            return new DamageSource(sourceUnitId, description);
        }

        public static DamageSource Environmental(string description)
        {
            return new DamageSource(UnitId.None, description);
        }

        public bool Equals(DamageSource other)
        {
            return SourceUnitId == other.SourceUnitId
                && string.Equals(Description, other.Description, StringComparison.Ordinal);
        }

        public override bool Equals(object obj)
        {
            return obj is DamageSource other && Equals(other);
        }

        public override int GetHashCode()
        {
            unchecked
            {
                return (SourceUnitId.GetHashCode() * 397) ^ StringComparer.Ordinal.GetHashCode(Description);
            }
        }

        public override string ToString()
        {
            return HasSourceUnit ? $"{Description} from {SourceUnitId}" : Description;
        }

        public static bool operator ==(DamageSource left, DamageSource right)
        {
            return left.Equals(right);
        }

        public static bool operator !=(DamageSource left, DamageSource right)
        {
            return !left.Equals(right);
        }

        private static string NormalizeDescription(string value)
        {
            return string.IsNullOrWhiteSpace(value) ? DefaultDescription : value.Trim();
        }
    }
}
