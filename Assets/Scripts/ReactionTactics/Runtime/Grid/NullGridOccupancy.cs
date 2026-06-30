using ReactionTactics.Units;

namespace ReactionTactics.Grid
{
    /// <summary>
    /// Occupancy implementation for maps with no units. Useful for tests and systems that
    /// want terrain-only movement queries before unit registries exist.
    /// </summary>
    public sealed class NullGridOccupancy : IGridOccupancy
    {
        public static readonly NullGridOccupancy Instance = new NullGridOccupancy();

        private NullGridOccupancy()
        {
        }

        public bool IsOccupied(GridPosition position)
        {
            return false;
        }

        public bool TryGetOccupant(GridPosition position, out UnitId occupantId)
        {
            occupantId = UnitId.None;
            return false;
        }

        public bool CanEnter(GridPosition position)
        {
            return true;
        }
    }
}
