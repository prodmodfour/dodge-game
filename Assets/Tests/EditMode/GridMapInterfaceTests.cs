using System;
using System.Collections.Generic;
using NUnit.Framework;
using ReactionTactics.Grid;

namespace ReactionTactics.Tests.EditMode
{
    public sealed class GridMapInterfaceTests
    {
        [Test]
        public void ConvenienceQueriesTreatClearCellsAsWalkableAndUnblocked()
        {
            var position = new GridPosition(1, 0, 1);
            IGridMap map = new TestGridMap(new GridCell(position));

            Assert.That(map.Contains(position), Is.True);
            Assert.That(map.GetCell(position).Position, Is.EqualTo(position));
            Assert.That(map.AllCells, Has.Count.EqualTo(1));
            Assert.That(map.MinBounds, Is.EqualTo(position));
            Assert.That(map.MaxBounds, Is.EqualTo(position));
            Assert.That(map.IsWalkable(position), Is.True);
            Assert.That(map.BlocksMovement(position), Is.False);
            Assert.That(map.BlocksLineOfSight(position), Is.False);
        }

        [Test]
        public void ConvenienceQueriesRejectMissingCellsForMovementAndSight()
        {
            IGridMap map = new TestGridMap(new GridCell(GridPosition.Zero));
            var missing = new GridPosition(9, 0, 9);

            Assert.That(map.Contains(missing), Is.False);
            Assert.That(map.TryGetCell(missing, out _), Is.False);
            Assert.That(map.IsWalkable(missing), Is.False);
            Assert.That(map.BlocksMovement(missing), Is.True);
            Assert.That(map.BlocksLineOfSight(missing), Is.True);
        }

        [Test]
        public void MovementBlockingDistinguishesUnwalkableAndLineOfSightBlockers()
        {
            var unwalkable = new GridPosition(0, 0, 0);
            var movementBlocked = new GridPosition(1, 0, 0);
            var sightBlocked = new GridPosition(2, 0, 0);
            IGridMap map = new TestGridMap(
                new GridCell(unwalkable, walkable: false, blocksMovement: false, blocksLineOfSight: false, movementCost: 1),
                new GridCell(movementBlocked, walkable: true, blocksMovement: true, blocksLineOfSight: false, movementCost: 1),
                new GridCell(sightBlocked, walkable: true, blocksMovement: false, blocksLineOfSight: true, movementCost: 1));

            Assert.That(map.IsWalkable(unwalkable), Is.False);
            Assert.That(map.BlocksMovement(unwalkable), Is.True);
            Assert.That(map.BlocksLineOfSight(unwalkable), Is.False);

            Assert.That(map.IsWalkable(movementBlocked), Is.False);
            Assert.That(map.BlocksMovement(movementBlocked), Is.True);
            Assert.That(map.BlocksLineOfSight(movementBlocked), Is.False);

            Assert.That(map.IsWalkable(sightBlocked), Is.True);
            Assert.That(map.BlocksMovement(sightBlocked), Is.False);
            Assert.That(map.BlocksLineOfSight(sightBlocked), Is.True);
        }

        [Test]
        public void ConvenienceQueriesRejectNullMaps()
        {
            IGridMap map = null;

            Assert.Throws<ArgumentNullException>(() => map.IsWalkable(GridPosition.Zero));
            Assert.Throws<ArgumentNullException>(() => map.BlocksMovement(GridPosition.Zero));
            Assert.Throws<ArgumentNullException>(() => map.BlocksLineOfSight(GridPosition.Zero));
        }

        private sealed class TestGridMap : IGridMap
        {
            private readonly Dictionary<GridPosition, GridCell> cellsByPosition;
            private readonly List<GridCell> allCells;

            public TestGridMap(params GridCell[] cells)
            {
                cellsByPosition = new Dictionary<GridPosition, GridCell>();
                allCells = new List<GridCell>(cells.Length);

                if (cells.Length == 0)
                {
                    MinBounds = GridPosition.Zero;
                    MaxBounds = GridPosition.Zero;
                    return;
                }

                var minX = int.MaxValue;
                var minY = int.MaxValue;
                var minZ = int.MaxValue;
                var maxX = int.MinValue;
                var maxY = int.MinValue;
                var maxZ = int.MinValue;

                foreach (var cell in cells)
                {
                    cellsByPosition.Add(cell.Position, cell);
                    allCells.Add(cell);

                    minX = Math.Min(minX, cell.Position.X);
                    minY = Math.Min(minY, cell.Position.Y);
                    minZ = Math.Min(minZ, cell.Position.Z);
                    maxX = Math.Max(maxX, cell.Position.X);
                    maxY = Math.Max(maxY, cell.Position.Y);
                    maxZ = Math.Max(maxZ, cell.Position.Z);
                }

                MinBounds = new GridPosition(minX, minY, minZ);
                MaxBounds = new GridPosition(maxX, maxY, maxZ);
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
                return cellsByPosition[position];
            }
        }
    }
}
