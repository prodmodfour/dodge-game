using System.Collections.Generic;

namespace ReactionTactics.Grid
{
    /// <summary>
    /// Read-only query interface for tactical terrain maps.
    /// Gameplay systems should depend on this abstraction instead of a concrete map implementation.
    /// </summary>
    public interface IGridMap
    {
        /// <summary>
        /// Inclusive minimum occupied grid coordinate across all cells in the map.
        /// </summary>
        GridPosition MinBounds { get; }

        /// <summary>
        /// Inclusive maximum occupied grid coordinate across all cells in the map.
        /// </summary>
        GridPosition MaxBounds { get; }

        /// <summary>
        /// All terrain cells in deterministic map storage order.
        /// </summary>
        IReadOnlyCollection<GridCell> AllCells { get; }

        /// <summary>
        /// Returns true when a terrain cell exists at the exact 3D grid position.
        /// </summary>
        bool Contains(GridPosition position);

        /// <summary>
        /// Attempts to retrieve a terrain cell at the exact 3D grid position.
        /// </summary>
        bool TryGetCell(GridPosition position, out GridCell cell);

        /// <summary>
        /// Retrieves a terrain cell at the exact 3D grid position, or throws when the cell is missing.
        /// </summary>
        GridCell GetCell(GridPosition position);
    }
}
