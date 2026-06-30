using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using ReactionTactics.Grid;
using ReactionTactics.Pathfinding;
using ReactionTactics.Units;

public sealed class GridOccupancyTests
{
    [Test]
    public void NullGridOccupancyReportsNoOccupantsAndAllowsEntry()
    {
        var position = new GridPosition(2, 0, 3);
        var occupancy = NullGridOccupancy.Instance;

        Assert.That(occupancy.IsOccupied(position), Is.False);
        Assert.That(occupancy.TryGetOccupant(position, out var occupantId), Is.False);
        Assert.That(occupantId, Is.EqualTo(UnitId.None));
        Assert.That(occupancy.CanEnter(position), Is.True);
    }

    [Test]
    public void PathfindingWorksWithNullGridOccupancy()
    {
        var start = new GridPosition(0, 0, 0);
        var middle = new GridPosition(1, 0, 0);
        var destination = new GridPosition(2, 0, 0);
        var map = new GridMap(new GridCell(start), new GridCell(middle), new GridCell(destination));
        var search = new ReachableCellSearch();

        var path = search.TryFindPath(map, start, destination, 2, NullGridOccupancy.Instance);

        Assert.That(path.IsValid, Is.True, path.FailureReason);
        Assert.That(path.Positions.ToArray(), Is.EqualTo(new[] { start, middle, destination }));
        Assert.That(path.TotalApCost, Is.EqualTo(2));
    }

    [Test]
    public void OccupancyReportsUnitIdentifiersForOccupiedCells()
    {
        var occupied = new GridPosition(1, 0, 0);
        var occupant = UnitId.FromInt(7);
        var occupancy = new TestGridOccupancy((occupied, occupant));

        Assert.That(occupancy.IsOccupied(occupied), Is.True);
        Assert.That(occupancy.TryGetOccupant(occupied, out var occupantId), Is.True);
        Assert.That(occupantId, Is.EqualTo(occupant));
        Assert.That(occupancy.CanEnter(occupied), Is.False);

        var empty = GridPosition.North;
        Assert.That(occupancy.IsOccupied(empty), Is.False);
        Assert.That(occupancy.TryGetOccupant(empty, out var emptyOccupant), Is.False);
        Assert.That(emptyOccupant, Is.EqualTo(UnitId.None));
        Assert.That(occupancy.CanEnter(empty), Is.True);
    }

    [Test]
    public void ReachableSearchUsesOccupancyToRejectOccupiedCells()
    {
        var start = new GridPosition(0, 0, 0);
        var occupiedChokepoint = new GridPosition(1, 0, 0);
        var beyondOccupied = new GridPosition(2, 0, 0);
        var map = new GridMap(
            new GridCell(start),
            new GridCell(occupiedChokepoint),
            new GridCell(beyondOccupied));
        var occupancy = new TestGridOccupancy((occupiedChokepoint, UnitId.FromInt(3)));
        var search = new ReachableCellSearch();

        var reachable = search.FindReachableCells(map, start, 3, occupancy);

        Assert.That(reachable.Keys, Is.EquivalentTo(new[] { start }));
        Assert.That(reachable.ContainsKey(occupiedChokepoint), Is.False);
        Assert.That(reachable.ContainsKey(beyondOccupied), Is.False);
    }

    [Test]
    public void ReachableSearchAllowsStartCellWhenOccupiedByMovingUnit()
    {
        var start = new GridPosition(0, 0, 0);
        var destination = new GridPosition(1, 0, 0);
        var map = new GridMap(new GridCell(start), new GridCell(destination));
        var occupancy = new TestGridOccupancy((start, UnitId.FromInt(1)));
        var search = new ReachableCellSearch();

        var reachable = search.FindReachableCells(map, start, 1, occupancy);

        Assert.That(reachable.Keys, Is.EquivalentTo(new[] { start, destination }));
        Assert.That(reachable[start].TotalApCost, Is.Zero);
        Assert.That(reachable[destination].TotalApCost, Is.EqualTo(1));
    }

    [Test]
    public void TryFindPathUsesOccupancyToRejectOccupiedDestinations()
    {
        var start = new GridPosition(0, 0, 0);
        var destination = new GridPosition(1, 0, 0);
        var map = new GridMap(new GridCell(start), new GridCell(destination));
        var occupancy = new TestGridOccupancy((destination, UnitId.FromInt(2)));
        var search = new ReachableCellSearch();

        var path = search.TryFindPath(map, start, destination, 1, occupancy);

        Assert.That(path.IsInvalid, Is.True);
        Assert.That(path.FailureReason, Does.Contain($"Destination {destination} is not reachable"));
    }

    private sealed class TestGridOccupancy : IGridOccupancy
    {
        private readonly Dictionary<GridPosition, UnitId> occupantsByPosition;

        public TestGridOccupancy(params (GridPosition Position, UnitId OccupantId)[] occupants)
        {
            occupantsByPosition = new Dictionary<GridPosition, UnitId>();
            foreach (var occupant in occupants)
            {
                occupantsByPosition.Add(occupant.Position, occupant.OccupantId);
            }
        }

        public bool IsOccupied(GridPosition position)
        {
            return occupantsByPosition.ContainsKey(position);
        }

        public bool TryGetOccupant(GridPosition position, out UnitId occupantId)
        {
            if (occupantsByPosition.TryGetValue(position, out occupantId))
            {
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
