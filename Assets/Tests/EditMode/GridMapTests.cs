using System;
using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using ReactionTactics.Grid;

namespace ReactionTactics.Tests.EditMode
{
    public sealed class GridMapTests
    {
        [Test]
        public void ConstructorStoresCellsInLookupAndOriginalOrder()
        {
            var first = new GridCell(new GridPosition(2, 1, 3), movementCost: 2);
            var second = new GridCell(new GridPosition(-1, 0, 5), walkable: false, blocksMovement: true, blocksLineOfSight: false, movementCost: 1);
            var third = new GridCell(new GridPosition(4, 3, -2), walkable: true, blocksMovement: false, blocksLineOfSight: true, movementCost: 4);

            var map = new GridMap(first, second, third);

            Assert.That(map.AllCells, Has.Count.EqualTo(3));
            Assert.That(map.AllCells.ToArray(), Is.EqualTo(new[] { first, second, third }));
            Assert.That(map.Contains(first.Position), Is.True);
            Assert.That(map.Contains(second.Position), Is.True);
            Assert.That(map.Contains(third.Position), Is.True);
            Assert.That(map.TryGetCell(second.Position, out var found), Is.True);
            Assert.That(found, Is.EqualTo(second));
            Assert.That(map.GetCell(third.Position), Is.EqualTo(third));
        }

        [Test]
        public void ConstructorDiscoversInclusiveBoundsAcrossAllCells()
        {
            var map = new GridMap(
                new GridCell(new GridPosition(2, 5, 1)),
                new GridCell(new GridPosition(-4, 3, 7)),
                new GridCell(new GridPosition(6, -2, -3)));

            Assert.That(map.MinBounds, Is.EqualTo(new GridPosition(-4, -2, -3)));
            Assert.That(map.MaxBounds, Is.EqualTo(new GridPosition(6, 5, 7)));
        }

        [Test]
        public void ConstructorRejectsDuplicateCellPositions()
        {
            var duplicatePosition = new GridPosition(1, 0, 1);
            var duplicate = new GridCell(duplicatePosition, walkable: false, blocksMovement: true, blocksLineOfSight: true, movementCost: 3);

            var exception = Assert.Throws<ArgumentException>(() => new GridMap(
                new GridCell(duplicatePosition),
                duplicate));

            Assert.That(exception.Message, Does.Contain(duplicatePosition.ToString()));
        }

        [Test]
        public void ConstructorRejectsNullCellCollection()
        {
            Assert.Throws<ArgumentNullException>(() => new GridMap((IEnumerable<GridCell>)null));
        }

        [Test]
        public void EmptyMapHasNoCellsAndZeroBounds()
        {
            var map = new GridMap(Array.Empty<GridCell>());

            Assert.That(map.AllCells, Is.Empty);
            Assert.That(map.MinBounds, Is.EqualTo(GridPosition.Zero));
            Assert.That(map.MaxBounds, Is.EqualTo(GridPosition.Zero));
            Assert.That(map.Contains(GridPosition.Zero), Is.False);
            Assert.That(map.TryGetCell(GridPosition.Zero, out _), Is.False);
        }

        [Test]
        public void MissingCellLookupThrowsHelpfulException()
        {
            var map = new GridMap(new GridCell(GridPosition.Zero));
            var missing = new GridPosition(9, 0, -2);

            var exception = Assert.Throws<KeyNotFoundException>(() => map.GetCell(missing));

            Assert.That(exception.Message, Does.Contain(missing.ToString()));
        }

        [Test]
        public void ImplementsGridMapConvenienceQueries()
        {
            var walkable = new GridPosition(0, 0, 0);
            var movementBlocked = new GridPosition(1, 0, 0);
            var sightBlocked = new GridPosition(2, 0, 0);
            IGridMap map = new GridMap(
                new GridCell(walkable),
                new GridCell(movementBlocked, walkable: true, blocksMovement: true, blocksLineOfSight: false, movementCost: 1),
                new GridCell(sightBlocked, walkable: true, blocksMovement: false, blocksLineOfSight: true, movementCost: 1));

            Assert.That(map.IsWalkable(walkable), Is.True);
            Assert.That(map.IsWalkable(movementBlocked), Is.False);
            Assert.That(map.BlocksMovement(movementBlocked), Is.True);
            Assert.That(map.BlocksLineOfSight(sightBlocked), Is.True);
        }
    }
}
