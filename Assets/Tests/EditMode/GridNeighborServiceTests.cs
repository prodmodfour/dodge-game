using System;
using System.Linq;
using NUnit.Framework;
using ReactionTactics.Grid;

namespace ReactionTactics.Tests.EditMode
{
    public sealed class GridNeighborServiceTests
    {
        [Test]
        public void GetNeighborsReturnsFourWayCellsWithDestinationHeights()
        {
            var origin = new GridPosition(1, 1, 1);
            var map = new GridMap(
                new GridCell(origin),
                new GridCell(new GridPosition(1, 2, 2)),
                new GridCell(new GridPosition(2, 1, 1)),
                new GridCell(new GridPosition(1, 0, 0)),
                new GridCell(new GridPosition(0, 1, 1)),
                new GridCell(new GridPosition(2, 5, 2)));
            var service = new GridNeighborService(maxClimb: 1, maxDrop: 1);

            var neighbors = service.GetNeighbors(map, origin).ToArray();

            Assert.That(neighbors, Is.EqualTo(new[]
            {
                new GridPosition(1, 2, 2),
                new GridPosition(2, 1, 1),
                new GridPosition(1, 0, 0),
                new GridPosition(0, 1, 1),
            }));
        }

        [Test]
        public void GetNeighborCellsSkipsBlockedUnwalkableAndMissingCells()
        {
            var origin = GridPosition.Zero;
            var walkableWest = new GridPosition(-1, 0, 0);
            var map = new GridMap(
                new GridCell(origin),
                new GridCell(new GridPosition(1, 0, 0), walkable: true, blocksMovement: true, blocksLineOfSight: false, movementCost: 1),
                new GridCell(new GridPosition(0, 0, -1), walkable: false, blocksMovement: true, blocksLineOfSight: false, movementCost: 1),
                new GridCell(walkableWest));
            var service = new GridNeighborService(maxClimb: 1, maxDrop: 1);

            var neighbors = service.GetNeighborCells(map, origin).Select(cell => cell.Position).ToArray();

            Assert.That(neighbors, Is.EqualTo(new[] { walkableWest }));
        }

        [Test]
        public void GetNeighborsHonorsSeparateClimbAndDropLimits()
        {
            var origin = new GridPosition(0, 2, 0);
            var tooHigh = new GridPosition(0, 4, 1);
            var climbable = new GridPosition(1, 3, 0);
            var droppable = new GridPosition(0, 0, -1);
            var tooLow = new GridPosition(-1, -1, 0);
            var map = new GridMap(
                new GridCell(origin),
                new GridCell(tooHigh),
                new GridCell(climbable),
                new GridCell(droppable),
                new GridCell(tooLow));
            var service = new GridNeighborService(maxClimb: 1, maxDrop: 2);

            var neighbors = service.GetNeighbors(map, origin).ToArray();

            Assert.That(neighbors, Is.EqualTo(new[]
            {
                climbable,
                droppable,
            }));
            Assert.That(service.IsValidNeighbor(map, origin, tooHigh), Is.False);
            Assert.That(service.IsValidNeighbor(map, origin, tooLow), Is.False);
        }

        [Test]
        public void IsValidNeighborRejectsNonCardinalMissingOrBlockedDestinations()
        {
            var origin = GridPosition.Zero;
            var cardinal = GridPosition.East;
            var diagonal = GridPosition.North + GridPosition.East;
            var blocked = GridPosition.West;
            var map = new GridMap(
                new GridCell(origin),
                new GridCell(cardinal),
                new GridCell(diagonal),
                new GridCell(blocked, walkable: true, blocksMovement: true, blocksLineOfSight: false, movementCost: 1));
            var service = new GridNeighborService();

            Assert.That(service.IsValidNeighbor(map, origin, cardinal), Is.True);
            Assert.That(service.IsValidNeighbor(map, origin, diagonal), Is.False);
            Assert.That(service.IsValidNeighbor(map, origin, blocked), Is.False);
            Assert.That(service.IsValidNeighbor(map, origin, GridPosition.South), Is.False);
        }

        [Test]
        public void MissingOrBlockedOriginHasNoNeighbors()
        {
            var blockedOrigin = GridPosition.Zero;
            var map = new GridMap(
                new GridCell(blockedOrigin, walkable: false, blocksMovement: true, blocksLineOfSight: false, movementCost: 1),
                new GridCell(GridPosition.East));
            var service = new GridNeighborService();

            Assert.That(service.GetNeighbors(map, blockedOrigin), Is.Empty);
            Assert.That(service.GetNeighbors(map, new GridPosition(5, 0, 5)), Is.Empty);
        }

        [Test]
        public void ConstructorRejectsNegativeHeightLimits()
        {
            Assert.Throws<ArgumentOutOfRangeException>(() => new GridNeighborService(maxClimb: -1, maxDrop: 0));
            Assert.Throws<ArgumentOutOfRangeException>(() => new GridNeighborService(maxClimb: 0, maxDrop: -1));
        }

        [Test]
        public void MethodsRejectNullMap()
        {
            var service = new GridNeighborService();

            Assert.Throws<ArgumentNullException>(() => service.GetNeighbors(null, GridPosition.Zero));
            Assert.Throws<ArgumentNullException>(() => service.GetNeighborCells(null, GridPosition.Zero));
            Assert.Throws<ArgumentNullException>(() => service.IsValidNeighbor(null, GridPosition.Zero, GridPosition.East));
        }
    }
}
