using System;

namespace ReactionTactics.Units
{
    /// <summary>
    /// Reports the deterministic HP damage that was applied after one-shot defense
    /// state was considered. It intentionally contains no random hit, miss, dodge,
    /// accuracy, or roll data.
    /// </summary>
    public readonly struct DamageApplication : IEquatable<DamageApplication>
    {
        private DamageApplication(
            int originalAmount,
            int finalAmount,
            bool wasBraced,
            int preventedAmount,
            DamageSource source)
        {
            if (originalAmount < 0)
            {
                throw new ArgumentOutOfRangeException(nameof(originalAmount), originalAmount, "Original damage cannot be negative.");
            }

            if (finalAmount < 0)
            {
                throw new ArgumentOutOfRangeException(nameof(finalAmount), finalAmount, "Final damage cannot be negative.");
            }

            if (preventedAmount < 0)
            {
                throw new ArgumentOutOfRangeException(nameof(preventedAmount), preventedAmount, "Prevented damage cannot be negative.");
            }

            if (finalAmount > originalAmount)
            {
                throw new ArgumentException("Final damage cannot exceed original damage.", nameof(finalAmount));
            }

            OriginalAmount = originalAmount;
            FinalAmount = finalAmount;
            WasBraced = wasBraced;
            PreventedAmount = preventedAmount;
            Source = source;
        }

        public int OriginalAmount { get; }

        public int FinalAmount { get; }

        public bool WasBraced { get; }

        public int PreventedAmount { get; }

        public DamageSource Source { get; }

        public static DamageApplication Unbraced(int amount, DamageSource source)
        {
            return new DamageApplication(amount, amount, false, 0, source);
        }

        public static DamageApplication Braced(
            int originalAmount,
            int finalAmount,
            int preventedAmount,
            DamageSource source)
        {
            return new DamageApplication(originalAmount, finalAmount, true, preventedAmount, source);
        }

        public bool Equals(DamageApplication other)
        {
            return OriginalAmount == other.OriginalAmount
                && FinalAmount == other.FinalAmount
                && WasBraced == other.WasBraced
                && PreventedAmount == other.PreventedAmount
                && Source == other.Source;
        }

        public override bool Equals(object obj)
        {
            return obj is DamageApplication other && Equals(other);
        }

        public override int GetHashCode()
        {
            unchecked
            {
                var hashCode = OriginalAmount;
                hashCode = (hashCode * 397) ^ FinalAmount;
                hashCode = (hashCode * 397) ^ WasBraced.GetHashCode();
                hashCode = (hashCode * 397) ^ PreventedAmount;
                hashCode = (hashCode * 397) ^ Source.GetHashCode();
                return hashCode;
            }
        }

        public override string ToString()
        {
            return WasBraced
                ? $"{FinalAmount}/{OriginalAmount} damage after brace prevented {PreventedAmount} ({Source})"
                : $"{FinalAmount} damage ({Source})";
        }

        public static bool operator ==(DamageApplication left, DamageApplication right)
        {
            return left.Equals(right);
        }

        public static bool operator !=(DamageApplication left, DamageApplication right)
        {
            return !left.Equals(right);
        }
    }
}
