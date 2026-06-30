using System;
using System.Collections.Generic;
using System.Linq;
using ReactionTactics.Grid;

namespace ReactionTactics.Targeting
{
    /// <summary>
    /// Deterministic area shape queries used by telegraphed tactical abilities.
    /// The initial radius implementation is terrain-aware but intentionally ignores
    /// movement and line-of-sight blockers; target validation decides whether the
    /// center can be selected, and resolution checks final unit positions later.
    /// </summary>
    public static class AreaShapeService
    {
        /// <summary>
        /// Returns all cells in <paramref name="map"/> whose horizontal x/z Manhattan
        /// distance from <paramref name="center"/> is within <paramref name="radius"/>.
        /// Returned positions use each cell's actual terrain height.
        /// </summary>
        public static IReadOnlyList<GridPosition> GetRadiusCells(
            GridPosition center,
            int radius,
            IGridMap map)
        {
            if (map == null)
            {
                throw new ArgumentNullException(nameof(map));
            }

            if (radius < 0)
            {
                throw new ArgumentOutOfRangeException(
                    nameof(radius),
                    radius,
                    "Radius cannot be negative.");
            }

            return map.AllCells
                .Select(cell => cell.Position)
                .Where(position => center.HorizontalDistanceTo(position) <= radius)
                .OrderBy(position => center.HorizontalDistanceTo(position))
                .ThenBy(position => position.Z)
                .ThenBy(position => position.X)
                .ThenBy(position => position.Y)
                .ToArray();
        }
    }
}
