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

        /// <summary>
        /// Returns all cells inside a cardinal cone starting one cell in front of
        /// <paramref name="origin"/>. Callers aiming at a target cell should derive
        /// <paramref name="direction"/> with <see cref="CardinalDirectionMath.FromTo"/>.
        /// Width is measured in cells to either side of the center line: 0 at distance 1,
        /// 1 at distances 2-3, and 2 at distance 4 and beyond.
        /// Returned positions use each cell's actual terrain height and never include
        /// the origin cell.
        /// </summary>
        public static IReadOnlyList<GridPosition> GetConeCells(
            GridPosition origin,
            CardinalDirection direction,
            int range,
            IGridMap map)
        {
            if (map == null)
            {
                throw new ArgumentNullException(nameof(map));
            }

            if (!Enum.IsDefined(typeof(CardinalDirection), direction))
            {
                throw new ArgumentOutOfRangeException(nameof(direction), direction, "Unknown cardinal direction.");
            }

            if (range < 0)
            {
                throw new ArgumentOutOfRangeException(
                    nameof(range),
                    range,
                    "Cone range cannot be negative.");
            }

            return map.AllCells
                .Select(cell => cell.Position)
                .Where(position => IsInsideCone(origin, direction, range, position))
                .OrderBy(position => GetForwardDistance(origin, direction, position))
                .ThenBy(position => GetSignedLateralOffset(origin, direction, position))
                .ThenBy(position => position.Y)
                .ToArray();
        }

        private static bool IsInsideCone(
            GridPosition origin,
            CardinalDirection direction,
            int range,
            GridPosition position)
        {
            var forwardDistance = GetForwardDistance(origin, direction, position);
            if (forwardDistance <= 0 || forwardDistance > range)
            {
                return false;
            }

            var lateralOffset = GetSignedLateralOffset(origin, direction, position);
            return Math.Abs(lateralOffset) <= GetConeHalfWidth(forwardDistance);
        }

        private static int GetConeHalfWidth(int forwardDistance)
        {
            if (forwardDistance <= 1)
            {
                return 0;
            }

            if (forwardDistance <= 3)
            {
                return 1;
            }

            return 2;
        }

        private static int GetForwardDistance(
            GridPosition origin,
            CardinalDirection direction,
            GridPosition position)
        {
            switch (direction)
            {
                case CardinalDirection.North:
                    return position.Z - origin.Z;
                case CardinalDirection.East:
                    return position.X - origin.X;
                case CardinalDirection.South:
                    return origin.Z - position.Z;
                case CardinalDirection.West:
                    return origin.X - position.X;
                default:
                    throw new ArgumentOutOfRangeException(nameof(direction), direction, "Unknown cardinal direction.");
            }
        }

        private static int GetSignedLateralOffset(
            GridPosition origin,
            CardinalDirection direction,
            GridPosition position)
        {
            switch (direction)
            {
                case CardinalDirection.North:
                    return position.X - origin.X;
                case CardinalDirection.East:
                    return origin.Z - position.Z;
                case CardinalDirection.South:
                    return origin.X - position.X;
                case CardinalDirection.West:
                    return position.Z - origin.Z;
                default:
                    throw new ArgumentOutOfRangeException(nameof(direction), direction, "Unknown cardinal direction.");
            }
        }
    }
}
