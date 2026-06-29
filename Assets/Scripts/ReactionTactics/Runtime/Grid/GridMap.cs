using System;
using System.Collections.Generic;

namespace ReactionTactics.Grid
{
    /// <summary>
    /// Pure C# in-memory implementation of <see cref="IGridMap"/> backed by grid positions.
    /// </summary>
    public sealed class GridMap : IGridMap
    {
        private readonly Dictionary<GridPosition, GridCell> cellsByPosition;
        private readonly IReadOnlyList<GridCell> allCells;

        public GridMap(params GridCell[] cells)
            : this((IEnumerable<GridCell>)cells)
        {
        }

        public GridMap(IEnumerable<GridCell> cells)
        {
            if (cells == null)
            {
                throw new ArgumentNullException(nameof(cells));
            }

            cellsByPosition = new Dictionary<GridPosition, GridCell>();
            var orderedCells = new List<GridCell>();
            var hasCells = false;
            var minX = int.MaxValue;
            var minY = int.MaxValue;
            var minZ = int.MaxValue;
            var maxX = int.MinValue;
            var maxY = int.MinValue;
            var maxZ = int.MinValue;

            foreach (var cell in cells)
            {
                if (cellsByPosition.ContainsKey(cell.Position))
                {
                    throw new ArgumentException(
                        $"Grid map cannot contain duplicate cells at position {cell.Position}.",
                        nameof(cells));
                }

                cellsByPosition.Add(cell.Position, cell);
                orderedCells.Add(cell);

                hasCells = true;
                minX = Math.Min(minX, cell.Position.X);
                minY = Math.Min(minY, cell.Position.Y);
                minZ = Math.Min(minZ, cell.Position.Z);
                maxX = Math.Max(maxX, cell.Position.X);
                maxY = Math.Max(maxY, cell.Position.Y);
                maxZ = Math.Max(maxZ, cell.Position.Z);
            }

            allCells = orderedCells.AsReadOnly();
            MinBounds = hasCells ? new GridPosition(minX, minY, minZ) : GridPosition.Zero;
            MaxBounds = hasCells ? new GridPosition(maxX, maxY, maxZ) : GridPosition.Zero;
        }

        public GridPosition MinBounds { get; }

        public GridPosition MaxBounds { get; }

        public IReadOnlyCollection<GridCell> AllCells
        {
            get { return allCells; }
        }

        public bool Contains(GridPosition position)
        {
            return cellsByPosition.ContainsKey(position);
        }

        public bool TryGetCell(GridPosition position, out GridCell cell)
        {
            return cellsByPosition.TryGetValue(position, out cell);
        }

        public GridCell GetCell(GridPosition position)
        {
            if (cellsByPosition.TryGetValue(position, out var cell))
            {
                return cell;
            }

            throw new KeyNotFoundException($"No grid cell exists at position {position}.");
        }
    }
}
