using System;

namespace ReactionTactics.Units
{
    /// <summary>
    /// One-shot defensive state for deterministic reactions. Brace reduces the
    /// next positive incoming damage by a fixed amount, then clears; it never
    /// represents dodge chance, accuracy, or any random avoidance roll.
    /// </summary>
    public readonly struct DefenseState : IEquatable<DefenseState>
    {
        private DefenseState(bool bracedUntilNextHit, int damageReduction)
        {
            IsBracedUntilNextHit = bracedUntilNextHit;
            DamageReduction = bracedUntilNextHit ? Math.Max(0, damageReduction) : 0;
        }

        public bool IsBracedUntilNextHit { get; }

        public int DamageReduction { get; }

        public static DefenseState None
        {
            get { return new DefenseState(false, 0); }
        }

        public static DefenseState Braced(int damageReduction)
        {
            if (damageReduction <= 0)
            {
                throw new ArgumentOutOfRangeException(
                    nameof(damageReduction),
                    damageReduction,
                    "Brace damage reduction must be greater than zero.");
            }

            return new DefenseState(true, damageReduction);
        }

        public int CalculateFinalDamage(int incomingDamage)
        {
            if (incomingDamage < 0)
            {
                throw new ArgumentOutOfRangeException(
                    nameof(incomingDamage),
                    incomingDamage,
                    "Incoming damage cannot be negative.");
            }

            if (!IsBracedUntilNextHit || incomingDamage == 0)
            {
                return incomingDamage;
            }

            return Math.Max(0, incomingDamage - DamageReduction);
        }

        public bool Equals(DefenseState other)
        {
            return IsBracedUntilNextHit == other.IsBracedUntilNextHit
                && DamageReduction == other.DamageReduction;
        }

        public override bool Equals(object obj)
        {
            return obj is DefenseState other && Equals(other);
        }

        public override int GetHashCode()
        {
            unchecked
            {
                return (IsBracedUntilNextHit.GetHashCode() * 397) ^ DamageReduction;
            }
        }

        public override string ToString()
        {
            return IsBracedUntilNextHit ? $"Braced until next hit (-{DamageReduction})" : "No defense state";
        }

        public static bool operator ==(DefenseState left, DefenseState right)
        {
            return left.Equals(right);
        }

        public static bool operator !=(DefenseState left, DefenseState right)
        {
            return !left.Equals(right);
        }
    }
}
