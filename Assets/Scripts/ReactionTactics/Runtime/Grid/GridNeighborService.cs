using System;
using System.Collections.Generic;

namespace ReactionTactics.Grid
{
    /// <summary>
    /// Finds legal 4-way movement neighbors on stepped grid terrain.
    /// </summary>
    public sealed class GridNeighborService
    {
        public const int DefaultMaxClimb = 1;
        public const int DefaultMaxDrop = 1;

        private static readonly GridPosition[] FourWayOffsets =
        {
            GridPosition.North,
            GridPosition.East,
            GridPosition.South,
            GridPosition.West,
        };

        public GridNeighborService()
            : this(DefaultMaxClimb, DefaultMaxDrop)
        {
        }

        public GridNeighborService(int maxClimb, int maxDrop)
        {
            if (maxClimb < 0)
            {
                throw new ArgumentOutOfRangeException(
                    nameof(maxClimb),
                    maxClimb,
                    "Maximum climb cannot be negative.");
            }

            if (maxDrop < 0)
            {
                throw new ArgumentOutOfRangeException(
                    nameof(maxDrop),
                    maxDrop,
                    "Maximum drop cannot be negative.");
            }

            MaxClimb = maxClimb;
            MaxDrop = maxDrop;
        }

        public int MaxClimb { get; }

        public int MaxDrop { get; }

        /// <summary>
        /// Returns valid neighboring grid positions in deterministic cardinal order: north, east, south, west.
        /// Neighbor heights come from the destination terrain cells, not from the origin height.
        /// </summary>
        public IReadOnlyList<GridPosition> GetNeighbors(IGridMap map, GridPosition origin)
        {
            var neighborCells = GetNeighborCells(map, origin);
            var positions = new List<GridPosition>(neighborCells.Count);

            foreach (var neighborCell in neighborCells)
            {
                positions.Add(neighborCell.Position);
            }

            return positions.AsReadOnly();
        }

        /// <summary>
        /// Returns valid neighboring cells in deterministic cardinal order: north, east, south, west.
        /// </summary>
        public IReadOnlyList<GridCell> GetNeighborCells(IGridMap map, GridPosition origin)
        {
            if (map == null)
            {
                throw new ArgumentNullException(nameof(map));
            }

            var neighbors = new List<GridCell>(FourWayOffsets.Length);
            if (!map.TryGetCell(origin, out var originCell) || !CanEnterCell(originCell))
            {
                return neighbors.AsReadOnly();
            }

            foreach (var offset in FourWayOffsets)
            {
                var candidateX = origin.X + offset.X;
                var candidateZ = origin.Z + offset.Z;
                AddValidNeighborsAtHorizontal(map, originCell, candidateX, candidateZ, neighbors);
            }

            return neighbors.AsReadOnly();
        }

        /// <summary>
        /// Returns true when the destination is a legal 4-way movement neighbor from the origin.
        /// </summary>
        public bool IsValidNeighbor(IGridMap map, GridPosition origin, GridPosition destination)
        {
            if (map == null)
            {
                throw new ArgumentNullException(nameof(map));
            }

            if (!map.TryGetCell(origin, out var originCell) || !CanEnterCell(originCell))
            {
                return false;
            }

            if (!map.TryGetCell(destination, out var destinationCell) || !CanEnterCell(destinationCell))
            {
                return false;
            }

            return GridPosition.HorizontalDistance(origin, destination) == 1
                && IsHeightTransitionAllowed(originCell.Position, destinationCell.Position);
        }

        private void AddValidNeighborsAtHorizontal(
            IGridMap map,
            GridCell originCell,
            int candidateX,
            int candidateZ,
            ICollection<GridCell> neighbors)
        {
            if (!IsWithinHorizontalBounds(map, candidateX, candidateZ))
            {
                return;
            }

            foreach (var cell in map.AllCells)
            {
                if (cell.Position.X != candidateX || cell.Position.Z != candidateZ)
                {
                    continue;
                }

                if (CanEnterCell(cell) && IsHeightTransitionAllowed(originCell.Position, cell.Position))
                {
                    neighbors.Add(cell);
                }
            }
        }

        private bool IsHeightTransitionAllowed(GridPosition from, GridPosition to)
        {
            var heightDelta = to.Y - from.Y;
            return heightDelta <= MaxClimb && -heightDelta <= MaxDrop;
        }

        private static bool CanEnterCell(GridCell cell)
        {
            return cell.Walkable && !cell.BlocksMovement;
        }

        private static bool IsWithinHorizontalBounds(IGridMap map, int x, int z)
        {
            return x >= map.MinBounds.X
                && x <= map.MaxBounds.X
                && z >= map.MinBounds.Z
                && z <= map.MaxBounds.Z;
        }
    }
}
