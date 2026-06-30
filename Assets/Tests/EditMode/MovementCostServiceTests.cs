using System;
using NUnit.Framework;
using ReactionTactics.Grid;

namespace ReactionTactics.Tests.EditMode
{
    public sealed class MovementCostServiceTests
    {
        [Test]
        public void CalculateCostUsesDestinationCellMovementCost()
        {
            var origin = GridPosition.Zero;
            var destination = GridPosition.East;
            var map = new GridMap(
                new GridCell(origin, movementCost: 5),
                new GridCell(destination, movementCost: 3));
            var service = new MovementCostService();

            var result = service.CalculateCost(map, origin, destination);

            Assert.That(result.IsSuccess, Is.True);
            Assert.That(result.Value, Is.EqualTo(3));
        }

        [Test]
        public void CalculateCostAddsConfiguredUphillSurchargePerHeightClimbed()
        {
            var origin = new GridPosition(0, 1, 0);
            var destination = new GridPosition(1, 3, 0);
            var map = new GridMap(
                new GridCell(origin, movementCost: 1),
                new GridCell(destination, movementCost: 2));
            var service = new MovementCostService(
                new GridNeighborService(maxClimb: 3, maxDrop: 1),
                uphillSurchargePerHeight: 2);

            var result = service.CalculateCost(map, origin, destination);

            Assert.That(result.IsSuccess, Is.True);
            Assert.That(result.Value, Is.EqualTo(6));
        }

        [Test]
        public void CalculateCostDoesNotAddUphillSurchargeWhenMovingFlatOrDownhill()
        {
            var origin = new GridPosition(0, 2, 0);
            var flatDestination = new GridPosition(1, 2, 0);
            var downhillDestination = new GridPosition(0, 0, 1);
            var map = new GridMap(
                new GridCell(origin),
                new GridCell(flatDestination, movementCost: 4),
                new GridCell(downhillDestination, movementCost: 5));
            var service = new MovementCostService(
                new GridNeighborService(maxClimb: 1, maxDrop: 3),
                uphillSurchargePerHeight: 3);

            var flatResult = service.CalculateCost(map, origin, flatDestination);
            var downhillResult = service.CalculateCost(map, origin, downhillDestination);

            Assert.That(flatResult.IsSuccess, Is.True);
            Assert.That(flatResult.Value, Is.EqualTo(4));
            Assert.That(downhillResult.IsSuccess, Is.True);
            Assert.That(downhillResult.Value, Is.EqualTo(5));
        }

        [Test]
        public void CalculateCostReturnsFailureForNonNeighborMoves()
        {
            var origin = GridPosition.Zero;
            var diagonal = GridPosition.North + GridPosition.East;
            var map = new GridMap(
                new GridCell(origin),
                new GridCell(diagonal));
            var service = new MovementCostService();

            var result = service.CalculateCost(map, origin, diagonal);

            Assert.That(result.IsFailure, Is.True);
            Assert.That(result.ErrorMessage, Does.Contain("not a valid movement neighbor"));
        }

        [Test]
        public void CalculateCostReturnsFailureForBlockedOrMissingCells()
        {
            var origin = GridPosition.Zero;
            var blockedDestination = GridPosition.East;
            var map = new GridMap(
                new GridCell(origin),
                new GridCell(blockedDestination, walkable: true, blocksMovement: true, blocksLineOfSight: false, movementCost: 1));
            var service = new MovementCostService();

            var blockedResult = service.CalculateCost(map, origin, blockedDestination);
            var missingResult = service.CalculateCost(map, origin, GridPosition.West);

            Assert.That(blockedResult.IsFailure, Is.True);
            Assert.That(blockedResult.ErrorMessage, Does.Contain("not a valid movement neighbor"));
            Assert.That(missingResult.IsFailure, Is.True);
            Assert.That(missingResult.ErrorMessage, Does.Contain("does not exist"));
        }

        [Test]
        public void ConstructorRejectsInvalidConfiguration()
        {
            Assert.Throws<ArgumentOutOfRangeException>(() => new MovementCostService(uphillSurchargePerHeight: -1));
            Assert.Throws<ArgumentNullException>(() => new MovementCostService(null));
        }

        [Test]
        public void CalculateCostRejectsNullMap()
        {
            var service = new MovementCostService();

            Assert.Throws<ArgumentNullException>(() => service.CalculateCost(null, GridPosition.Zero, GridPosition.East));
        }
    }
}
