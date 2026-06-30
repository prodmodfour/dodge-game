using ReactionTactics.Units;

namespace ReactionTactics.Grid
{
    /// <summary>
    /// Read-only query interface for unit occupancy on the tactical grid.
    /// Terrain remains owned by <see cref="IGridMap"/>; this interface answers which
    /// cells are currently occupied by units so pathfinding and targeting can stay
    /// decoupled from concrete unit registries.
    /// </summary>
    public interface IGridOccupancy
    {
        /// <summary>
        /// Returns true when a living or otherwise movement-blocking unit occupies the position.
        /// </summary>
        bool IsOccupied(GridPosition position);

        /// <summary>
        /// Attempts to retrieve the stable unit identifier occupying the position.
        /// </summary>
        bool TryGetOccupant(GridPosition position, out UnitId occupantId);

        /// <summary>
        /// Returns true when normal movement is allowed to enter the position according to occupancy alone.
        /// Terrain walkability is still validated separately by grid and movement services.
        /// </summary>
        bool CanEnter(GridPosition position);
    }
}
