using System;
using System.Collections.Generic;
using ReactionTactics.Grid;

namespace ReactionTactics.Targeting
{
    /// <summary>
    /// Simple deterministic grid line-of-sight queries for ranged prototype targeting.
    /// The service projects sight across horizontal X/Z coordinates, resolves each
    /// sample to the map's actual terrain height, and treats intermediate
    /// blocksLineOfSight cells or missing cells as blockers.
    /// </summary>
    public static class LineOfSightService
    {
        /// <summary>
        /// Returns true when the simple grid line between <paramref name="origin"/>
        /// and <paramref name="target"/> is not blocked by any intermediate sight blocker.
        /// Origin and target cells do not block their own line of sight.
        /// </summary>
        public static bool HasLineOfSight(GridPosition origin, GridPosition target, IGridMap map)
        {
            return !TryFindBlockingCell(origin, target, map, out _);
        }

        /// <summary>
        /// Returns the terrain cells crossed by the simple projected line from origin
        /// to target, including both endpoints. Returned positions use each map cell's
        /// actual Y height when that horizontal coordinate exists; missing samples keep
        /// their projected coordinate so callers can diagnose off-map gaps.
        /// </summary>
        public static IReadOnlyList<GridPosition> GetLineCells(
            GridPosition origin,
            GridPosition target,
            IGridMap map)
        {
            if (map == null)
            {
                throw new ArgumentNullException(nameof(map));
            }

            var samples = GetProjectedHorizontalSamples(origin, target);
            var cells = new GridPosition[samples.Count];
            for (var i = 0; i < samples.Count; i += 1)
            {
                cells[i] = TryFindCellAtHorizontal(map, samples[i].X, samples[i].Z, out var cell)
                    ? cell.Position
                    : samples[i];
            }

            return cells;
        }

        /// <summary>
        /// Finds the first cell that blocks the simple projected line. Missing cells
        /// block sight so lines cannot see through gaps outside the authored map.
        /// Sight-blocking origin and target cells are ignored as blockers for their
        /// own line; only missing endpoints fail the query.
        /// </summary>
        public static bool TryFindBlockingCell(
            GridPosition origin,
            GridPosition target,
            IGridMap map,
            out GridPosition blockingPosition)
        {
            if (map == null)
            {
                throw new ArgumentNullException(nameof(map));
            }

            var samples = GetProjectedHorizontalSamples(origin, target);
            for (var i = 0; i < samples.Count; i += 1)
            {
                var sample = samples[i];
                if (!TryFindCellAtHorizontal(map, sample.X, sample.Z, out var cell))
                {
                    blockingPosition = sample;
                    return true;
                }

                var isEndpoint = i == 0 || i == samples.Count - 1;
                if (!isEndpoint && cell.BlocksLineOfSight)
                {
                    blockingPosition = cell.Position;
                    return true;
                }
            }

            blockingPosition = GridPosition.Zero;
            return false;
        }

        private static IReadOnlyList<GridPosition> GetProjectedHorizontalSamples(
            GridPosition origin,
            GridPosition target)
        {
            var deltaX = target.X - origin.X;
            var deltaZ = target.Z - origin.Z;
            var steps = Math.Max(Math.Abs(deltaX), Math.Abs(deltaZ));

            if (steps == 0)
            {
                return new[] { origin };
            }

            var samples = new List<GridPosition>(steps + 1);
            for (var step = 0; step <= steps; step += 1)
            {
                var x = origin.X + DivideRounded(deltaX * step, steps);
                var z = origin.Z + DivideRounded(deltaZ * step, steps);
                samples.Add(new GridPosition(x, 0, z));
            }

            return samples;
        }

        private static int DivideRounded(int numerator, int denominator)
        {
            if (denominator <= 0)
            {
                throw new ArgumentOutOfRangeException(nameof(denominator), denominator, "Denominator must be positive.");
            }

            if (numerator >= 0)
            {
                return (numerator + (denominator / 2)) / denominator;
            }

            return -((-numerator + (denominator / 2)) / denominator);
        }

        private static bool TryFindCellAtHorizontal(
            IGridMap map,
            int x,
            int z,
            out GridCell cell)
        {
            var found = false;
            cell = default;

            foreach (var candidate in map.AllCells)
            {
                if (candidate.Position.X != x || candidate.Position.Z != z)
                {
                    continue;
                }

                if (!found || candidate.Position.Y < cell.Position.Y)
                {
                    cell = candidate;
                    found = true;
                }
            }

            return found;
        }
    }
}
