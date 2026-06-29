using System;

namespace ReactionTactics.Grid
{
    /// <summary>
    /// Convenience queries shared by pathfinding, targeting, and scene presentation code.
    /// </summary>
    public static class GridMapExtensions
    {
        /// <summary>
        /// Returns true when the position has a cell that can be entered by normal movement.
        /// Missing cells are not walkable.
        /// </summary>
        public static bool IsWalkable(this IGridMap map, GridPosition position)
        {
            if (map == null)
            {
                throw new ArgumentNullException(nameof(map));
            }

            return map.TryGetCell(position, out var cell)
                && cell.Walkable
                && !cell.BlocksMovement;
        }

        /// <summary>
        /// Returns true when movement should treat the position as blocked.
        /// Missing cells, unwalkable cells, and explicit movement blockers all block movement.
        /// </summary>
        public static bool BlocksMovement(this IGridMap map, GridPosition position)
        {
            if (map == null)
            {
                throw new ArgumentNullException(nameof(map));
            }

            if (!map.TryGetCell(position, out var cell))
            {
                return true;
            }

            return !cell.Walkable || cell.BlocksMovement;
        }

        /// <summary>
        /// Returns true when line of sight should treat the position as blocked.
        /// Missing cells are treated as blocked so off-map queries are never considered clear terrain.
        /// </summary>
        public static bool BlocksLineOfSight(this IGridMap map, GridPosition position)
        {
            if (map == null)
            {
                throw new ArgumentNullException(nameof(map));
            }

            if (!map.TryGetCell(position, out var cell))
            {
                return true;
            }

            return cell.BlocksLineOfSight;
        }
    }
}
