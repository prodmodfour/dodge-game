using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using ReactionTactics.Grid;
using ReactionTactics.Pathfinding;
using ReactionTactics.Units;

public sealed class Pathfinding
{
    [Test]
    public void ReachableCellsRespectApBudgetsZeroOneAndThree()
    {
        var start = new GridPosition(0, 0, 0);
        var east = new GridPosition(1, 0, 0);
        var farEast = new GridPosition(2, 0, 0);
        var north = new GridPosition(0, 0, 1);
        var roughNorthEast = new GridPosition(1, 0, 1);
        var farNorthEast = new GridPosition(2, 0, 1);
        var map = new GridMap(
            new GridCell(start),
            new GridCell(east),
            new GridCell(farEast),
            new GridCell(north),
            new GridCell(roughNorthEast, movementCost: 2),
            new GridCell(farNorthEast));
        var search = new ReachableCellSearch();

        var apZero = search.FindReachableCells(map, start, apBudget: 0);
        var apOne = search.FindReachableCells(map, start, apBudget: 1);
        var apThree = search.FindReachableCells(map, start, apBudget: 3);

        Assert.That(apZero.Keys, Is.EquivalentTo(new[] { start }));
        Assert.That(apOne.Keys, Is.EquivalentTo(new[] { start, east, north }));
        Assert.That(apThree.Keys, Is.EquivalentTo(new[]
        {
            start,
            east,
            farEast,
            north,
            roughNorthEast,
            farNorthEast,
        }));
        Assert.That(apThree[start].TotalApCost, Is.Zero);
        Assert.That(apThree[east].TotalApCost, Is.EqualTo(1));
        Assert.That(apThree[north].TotalApCost, Is.EqualTo(1));
        Assert.That(apThree[farEast].TotalApCost, Is.EqualTo(2));
        Assert.That(apThree[roughNorthEast].TotalApCost, Is.EqualTo(3));
        Assert.That(apThree[farNorthEast].TotalApCost, Is.EqualTo(3));
    }

    [Test]
    public void BlockedCellsAndOccupiedCellsStopMovementRangesAndPaths()
    {
        var start = new GridPosition(0, 0, 0);
        var blockedEast = new GridPosition(1, 0, 0);
        var behindBlocked = new GridPosition(2, 0, 0);
        var north = new GridPosition(0, 0, 1);
        var occupiedBridge = new GridPosition(1, 0, 1);
        var behindOccupied = new GridPosition(2, 0, 1);
        var map = new GridMap(
            new GridCell(start),
            new GridCell(blockedEast, walkable: true, blocksMovement: true, blocksLineOfSight: false, movementCost: 1),
            new GridCell(behindBlocked),
            new GridCell(north),
            new GridCell(occupiedBridge),
            new GridCell(behindOccupied));
        var occupancy = new OccupiedPositions(occupiedBridge);
        var search = new ReachableCellSearch();

        var reachable = search.FindReachableCells(map, start, apBudget: 4, occupancy);
        var pathPastBlockedCell = search.TryFindPath(map, start, behindBlocked, apBudget: 4, occupancy);
        var pathPastOccupiedCell = search.TryFindPath(map, start, behindOccupied, apBudget: 4, occupancy);

        Assert.That(reachable.Keys, Is.EquivalentTo(new[] { start, north }));
        Assert.That(reachable.ContainsKey(blockedEast), Is.False, "Blocked terrain should never be a reachable destination.");
        Assert.That(reachable.ContainsKey(occupiedBridge), Is.False, "Occupied cells should be skipped by normal movement.");
        Assert.That(reachable.ContainsKey(behindBlocked), Is.False, "Movement should not pass through blocked terrain.");
        Assert.That(reachable.ContainsKey(behindOccupied), Is.False, "Movement should not pass through an occupied chokepoint.");
        Assert.That(pathPastBlockedCell.IsInvalid, Is.True);
        Assert.That(pathPastBlockedCell.FailureReason, Does.Contain($"Destination {behindBlocked} is not reachable"));
        Assert.That(pathPastOccupiedCell.IsInvalid, Is.True);
        Assert.That(pathPastOccupiedCell.FailureReason, Does.Contain($"Destination {behindOccupied} is not reachable"));
    }

    [Test]
    public void HeightTransitionsRespectConfiguredClimbAndDropLimits()
    {
        var start = new GridPosition(0, 1, 0);
        var climbableNorth = new GridPosition(0, 2, 1);
        var tooHighEast = new GridPosition(1, 3, 0);
        var droppableSouth = new GridPosition(0, 0, -1);
        var tooLowWest = new GridPosition(-1, -1, 0);
        var map = new GridMap(
            new GridCell(start),
            new GridCell(climbableNorth),
            new GridCell(tooHighEast),
            new GridCell(droppableSouth),
            new GridCell(tooLowWest));
        var neighborService = new GridNeighborService(maxClimb: 1, maxDrop: 1);
        var costService = new MovementCostService(neighborService);
        var search = new ReachableCellSearch(neighborService, costService);

        var reachable = search.FindReachableCells(map, start, apBudget: 3);

        Assert.That(reachable.Keys, Is.EquivalentTo(new[] { start, climbableNorth, droppableSouth }));
        Assert.That(reachable.ContainsKey(tooHighEast), Is.False, "A two-cell climb exceeds the configured climb limit.");
        Assert.That(reachable.ContainsKey(tooLowWest), Is.False, "A two-cell drop exceeds the configured drop limit.");
    }

    [Test]
    public void ShortestPathUsesLowestTotalApCostInsteadOfFewestSteps()
    {
        var start = new GridPosition(0, 0, 0);
        var roughEast = new GridPosition(1, 0, 0);
        var destination = new GridPosition(2, 0, 0);
        var north = new GridPosition(0, 0, 1);
        var northEast = new GridPosition(1, 0, 1);
        var northFarEast = new GridPosition(2, 0, 1);
        var map = new GridMap(
            new GridCell(start),
            new GridCell(roughEast, movementCost: 5),
            new GridCell(destination),
            new GridCell(north),
            new GridCell(northEast),
            new GridCell(northFarEast));
        var search = new ReachableCellSearch();

        var path = search.TryFindPath(map, start, destination, apBudget: 6);

        Assert.That(path.IsValid, Is.True, path.FailureReason);
        Assert.That(path.Positions.ToArray(), Is.EqualTo(new[]
        {
            start,
            north,
            northEast,
            northFarEast,
            destination,
        }));
        Assert.That(path.TotalApCost, Is.EqualTo(4));
        Assert.That(path.Steps.ToArray(), Is.EqualTo(new[]
        {
            new PathStep(start, stepApCost: 0, totalApCost: 0),
            new PathStep(north, stepApCost: 1, totalApCost: 1),
            new PathStep(northEast, stepApCost: 1, totalApCost: 2),
            new PathStep(northFarEast, stepApCost: 1, totalApCost: 3),
            new PathStep(destination, stepApCost: 1, totalApCost: 4),
        }));
    }

    private sealed class OccupiedPositions : IGridOccupancy
    {
        private readonly HashSet<GridPosition> occupiedPositions;

        public OccupiedPositions(params GridPosition[] positions)
        {
            occupiedPositions = new HashSet<GridPosition>(positions);
        }

        public bool IsOccupied(GridPosition position)
        {
            return occupiedPositions.Contains(position);
        }

        public bool TryGetOccupant(GridPosition position, out UnitId occupantId)
        {
            if (IsOccupied(position))
            {
                occupantId = UnitId.FromInt(1);
                return true;
            }

            occupantId = UnitId.None;
            return false;
        }

        public bool CanEnter(GridPosition position)
        {
            return !IsOccupied(position);
        }
    }
}
