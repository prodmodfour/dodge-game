using System;

namespace ReactionTactics.Units
{
    /// <summary>
    /// Allocates deterministic, monotonically increasing unit IDs for one combat setup.
    /// </summary>
    public sealed class UnitIdGenerator
    {
        private int nextValue;

        public UnitIdGenerator(int firstValue = 1)
        {
            if (firstValue <= 0)
            {
                throw new ArgumentOutOfRangeException(
                    nameof(firstValue),
                    firstValue,
                    "The first generated unit identifier must be positive.");
            }

            nextValue = firstValue;
        }

        public UnitId Next()
        {
            if (nextValue == int.MaxValue)
            {
                throw new InvalidOperationException("Unit identifier space has been exhausted.");
            }

            var id = new UnitId(nextValue);
            nextValue += 1;
            return id;
        }
    }
}
