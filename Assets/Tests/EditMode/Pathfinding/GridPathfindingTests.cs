using System.Linq;
using NUnit.Framework;
using ReactionTactics.Grid;
using ReactionTactics.Pathfinding;

public sealed class GridPathfindingTests
{
    [Test]
    public void TryFindPathReturnsOrderedShortestPathWithApCosts()
    {
        var start = new GridPosition(0, 0, 0);
        var north = new GridPosition(0, 0, 1);
        var roughEast = new GridPosition(1, 0, 0);
        var destination = new GridPosition(1, 0, 1);
        var map = new GridMap(
            new GridCell(start),
            new GridCell(north),
            new GridCell(roughEast, movementCost: 3),
            new GridCell(destination));
        var search = new ReachableCellSearch();

        var path = search.TryFindPath(map, start, destination, apBudget: 3);

        Assert.That(path.IsValid, Is.True, path.FailureReason);
        Assert.That(path.Positions.ToArray(), Is.EqualTo(new[] { start, north, destination }));
        Assert.That(path.TotalApCost, Is.EqualTo(2));
        Assert.That(path.Steps.ToArray(), Is.EqualTo(new[]
        {
            new PathStep(start, stepApCost: 0, totalApCost: 0),
            new PathStep(north, stepApCost: 1, totalApCost: 1),
            new PathStep(destination, stepApCost: 1, totalApCost: 2),
        }));
    }

    [Test]
    public void TryFindPathReturnsStartOnlyPathWhenDestinationIsStart()
    {
        var start = new GridPosition(0, 0, 0);
        var map = new GridMap(new GridCell(start));
        var search = new ReachableCellSearch();

        var path = search.TryFindPath(map, start, start, apBudget: 0);

        Assert.That(path.IsValid, Is.True, path.FailureReason);
        Assert.That(path.Positions.ToArray(), Is.EqualTo(new[] { start }));
        Assert.That(path.TotalApCost, Is.Zero);
        Assert.That(path.Steps.ToArray(), Is.EqualTo(new[]
        {
            new PathStep(start, stepApCost: 0, totalApCost: 0),
        }));
    }

    [Test]
    public void TryFindPathFailsWhenDestinationExceedsBudget()
    {
        var start = new GridPosition(0, 0, 0);
        var middle = new GridPosition(1, 0, 0);
        var destination = new GridPosition(2, 0, 0);
        var map = new GridMap(new GridCell(start), new GridCell(middle), new GridCell(destination));
        var search = new ReachableCellSearch();

        var path = search.TryFindPath(map, start, destination, apBudget: 1);

        Assert.That(path.IsInvalid, Is.True);
        Assert.That(path.FailureReason, Does.Contain($"Destination {destination} is not reachable"));
        Assert.That(path.Positions, Is.Empty);
    }

    [Test]
    public void TryFindPathUsesCanEnterPredicateWhenReconstructingReachablePath()
    {
        var start = new GridPosition(0, 0, 0);
        var blockedChokepoint = new GridPosition(1, 0, 0);
        var destination = new GridPosition(2, 0, 0);
        var map = new GridMap(
            new GridCell(start),
            new GridCell(blockedChokepoint),
            new GridCell(destination));
        var search = new ReachableCellSearch();

        var path = search.TryFindPath(
            map,
            start,
            destination,
            3,
            new GridNeighborService(),
            new MovementCostService(),
            position => position != blockedChokepoint);

        Assert.That(path.IsInvalid, Is.True);
        Assert.That(path.FailureReason, Does.Contain($"Destination {destination} is not reachable"));
    }
}
