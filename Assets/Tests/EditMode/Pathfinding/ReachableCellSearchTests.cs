using System;
using System.Collections.Generic;
using NUnit.Framework;
using ReactionTactics.Grid;
using ReactionTactics.Pathfinding;

public sealed class ReachableCellSearchTests
{
    [Test]
    public void SearchIncludesStartCellWithZeroCostWhenBudgetIsZero()
    {
        var map = CreateFlatMap(width: 2, depth: 2);
        var search = new ReachableCellSearch();
        var start = new GridPosition(0, 0, 0);

        var reachable = search.FindReachableCells(map, start, apBudget: 0);

        Assert.That(reachable.Keys, Is.EquivalentTo(new[] { start }));
        Assert.That(reachable[start], Is.EqualTo(new ReachableCell(start, totalApCost: 0)));
    }

    [Test]
    public void SearchRespectsMovementCostsAndApBudget()
    {
        var start = new GridPosition(0, 0, 0);
        var north = new GridPosition(0, 0, 1);
        var east = new GridPosition(1, 0, 0);
        var northEast = new GridPosition(1, 0, 1);
        var map = new GridMap(
            new GridCell(start),
            new GridCell(north),
            new GridCell(east, movementCost: 3),
            new GridCell(northEast));
        var search = new ReachableCellSearch();

        var reachable = search.FindReachableCells(map, start, apBudget: 2);

        Assert.That(reachable.Keys, Is.EquivalentTo(new[] { start, north, northEast }));
        Assert.That(reachable[north].TotalApCost, Is.EqualTo(1));
        Assert.That(reachable[north].PreviousPosition, Is.EqualTo(start));
        Assert.That(reachable[northEast].TotalApCost, Is.EqualTo(2));
        Assert.That(reachable[northEast].PreviousPosition, Is.EqualTo(north));
        Assert.That(reachable.ContainsKey(east), Is.False, "The rough east cell costs more AP than the budget allows.");
    }

    [Test]
    public void SearchRespectsHeightLimitsFromNeighborService()
    {
        var start = new GridPosition(0, 0, 0);
        var gentleStep = new GridPosition(0, 1, 1);
        var steepStep = new GridPosition(1, 2, 0);
        var map = new GridMap(
            new GridCell(start),
            new GridCell(gentleStep),
            new GridCell(steepStep));
        var neighborService = new GridNeighborService(maxClimb: 1, maxDrop: 1);
        var costService = new MovementCostService(neighborService);
        var search = new ReachableCellSearch(neighborService, costService);

        var reachable = search.FindReachableCells(
            map,
            start,
            apBudget: 3,
            neighborService,
            costService,
            canEnterPosition: null);

        Assert.That(reachable.Keys, Is.EquivalentTo(new[] { start, gentleStep }));
        Assert.That(reachable.ContainsKey(steepStep), Is.False, "A climb above the configured limit should not be reachable.");
    }

    [Test]
    public void SearchUsesOccupancyPredicateToRejectDestinations()
    {
        var start = new GridPosition(0, 0, 0);
        var occupied = new GridPosition(1, 0, 0);
        var beyondOccupied = new GridPosition(2, 0, 0);
        var north = new GridPosition(0, 0, 1);
        var map = new GridMap(
            new GridCell(start),
            new GridCell(occupied),
            new GridCell(beyondOccupied),
            new GridCell(north));
        var search = new ReachableCellSearch();

        var reachable = search.FindReachableCells(
            map,
            start,
            apBudget: 3,
            new GridNeighborService(),
            new MovementCostService(),
            position => position != occupied);

        Assert.That(reachable.Keys, Is.EquivalentTo(new[] { start, north }));
        Assert.That(reachable.ContainsKey(occupied), Is.False);
        Assert.That(reachable.ContainsKey(beyondOccupied), Is.False, "Cells behind an occupied chokepoint should not be reachable through it.");
    }

    [Test]
    public void SearchAllowsStartCellEvenWhenOccupancyPredicateRejectsIt()
    {
        var start = new GridPosition(0, 0, 0);
        var east = new GridPosition(1, 0, 0);
        var map = new GridMap(new GridCell(start), new GridCell(east));
        var search = new ReachableCellSearch();

        var reachable = search.FindReachableCells(
            map,
            start,
            apBudget: 1,
            new GridNeighborService(),
            new MovementCostService(),
            position => position != start);

        Assert.That(reachable.Keys, Is.EquivalentTo(new[] { start, east }));
    }

    [Test]
    public void SearchRejectsInvalidInputs()
    {
        var map = CreateFlatMap(width: 1, depth: 1);
        var search = new ReachableCellSearch();

        Assert.Throws<ArgumentNullException>(() => search.FindReachableCells(null, GridPosition.Zero, 1));
        Assert.Throws<ArgumentOutOfRangeException>(() => search.FindReachableCells(map, GridPosition.Zero, -1));
        Assert.Throws<ArgumentException>(() => search.FindReachableCells(map, GridPosition.East, 1));
    }

    private static GridMap CreateFlatMap(int width, int depth)
    {
        var cells = new List<GridCell>();
        for (var x = 0; x < width; x++)
        {
            for (var z = 0; z < depth; z++)
            {
                cells.Add(new GridCell(new GridPosition(x, 0, z)));
            }
        }

        return new GridMap(cells);
    }
}
