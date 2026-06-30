using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using ReactionTactics.Grid;
using ReactionTactics.Pathfinding;
using ReactionTactics.Units;

public sealed class OccupancyAwarePathfindingTests
{
    [Test]
    public void ReachableSearchBlocksFriendlyAndEnemyOccupantsByDefault()
    {
        var start = new GridPosition(0, 0, 0);
        var friendlyOccupied = new GridPosition(1, 0, 0);
        var behindFriendly = new GridPosition(2, 0, 0);
        var enemyOccupied = new GridPosition(0, 0, 1);
        var behindEnemy = new GridPosition(0, 0, 2);
        var map = new GridMap(
            new GridCell(start),
            new GridCell(friendlyOccupied),
            new GridCell(behindFriendly),
            new GridCell(enemyOccupied),
            new GridCell(behindEnemy));
        var occupancy = new TeamGridOccupancy(
            (friendlyOccupied, UnitId.FromInt(2), TeamId.Player),
            (enemyOccupied, UnitId.FromInt(3), TeamId.Enemy));
        var search = new ReachableCellSearch();

        var reachable = search.FindReachableCells(map, start, apBudget: 3, occupancy);
        var pathThroughFriendly = search.TryFindPath(map, start, behindFriendly, apBudget: 3, occupancy);
        var pathThroughEnemy = search.TryFindPath(map, start, behindEnemy, apBudget: 3, occupancy);

        Assert.That(occupancy.GetTeam(friendlyOccupied), Is.EqualTo(TeamId.Player));
        Assert.That(occupancy.GetTeam(enemyOccupied), Is.EqualTo(TeamId.Enemy));
        Assert.That(reachable.Keys, Is.EquivalentTo(new[] { start }));
        Assert.That(reachable.ContainsKey(friendlyOccupied), Is.False, "Friendly units should block movement by default.");
        Assert.That(reachable.ContainsKey(enemyOccupied), Is.False, "Enemy units should block movement by default.");
        Assert.That(reachable.ContainsKey(behindFriendly), Is.False, "Movement should not pass through a friendly occupant.");
        Assert.That(reachable.ContainsKey(behindEnemy), Is.False, "Movement should not pass through an enemy occupant.");
        Assert.That(pathThroughFriendly.IsInvalid, Is.True);
        Assert.That(pathThroughEnemy.IsInvalid, Is.True);
    }

    [Test]
    public void IgnoreUnitsOptionAllowsMovingThroughAndEndingOnOccupiedCells()
    {
        var start = new GridPosition(0, 0, 0);
        var occupiedMiddle = new GridPosition(1, 0, 0);
        var destination = new GridPosition(2, 0, 0);
        var map = new GridMap(
            new GridCell(start),
            new GridCell(occupiedMiddle),
            new GridCell(destination));
        var occupancy = new TeamGridOccupancy((occupiedMiddle, UnitId.FromInt(4), TeamId.Enemy));
        var search = new ReachableCellSearch();

        var defaultReachable = search.FindReachableCells(map, start, apBudget: 2, occupancy);
        var ignoreUnitsReachable = search.FindReachableCells(map, start, apBudget: 2, occupancy, ignoreUnits: true);
        var ignoreUnitsPath = search.TryFindPath(map, start, destination, apBudget: 2, occupancy, ignoreUnits: true);

        Assert.That(defaultReachable.Keys, Is.EquivalentTo(new[] { start }));
        Assert.That(ignoreUnitsReachable.Keys, Is.EquivalentTo(new[] { start, occupiedMiddle, destination }));
        Assert.That(ignoreUnitsPath.IsValid, Is.True, ignoreUnitsPath.FailureReason);
        Assert.That(ignoreUnitsPath.Positions.ToArray(), Is.EqualTo(new[] { start, occupiedMiddle, destination }));
        Assert.That(ignoreUnitsPath.TotalApCost, Is.EqualTo(2));
    }

    [Test]
    public void IgnoreUnitsOptionAlsoAllowsOccupiedDestination()
    {
        var start = new GridPosition(0, 0, 0);
        var occupiedDestination = new GridPosition(1, 0, 0);
        var map = new GridMap(new GridCell(start), new GridCell(occupiedDestination));
        var occupancy = new TeamGridOccupancy((occupiedDestination, UnitId.FromInt(5), TeamId.Player));
        var search = new ReachableCellSearch();

        var defaultPath = search.TryFindPath(map, start, occupiedDestination, apBudget: 1, occupancy);
        var ignoreUnitsPath = search.TryFindPath(map, start, occupiedDestination, apBudget: 1, occupancy, ignoreUnits: true);

        Assert.That(defaultPath.IsInvalid, Is.True);
        Assert.That(ignoreUnitsPath.IsValid, Is.True, ignoreUnitsPath.FailureReason);
        Assert.That(ignoreUnitsPath.Destination, Is.EqualTo(occupiedDestination));
    }

    private sealed class TeamGridOccupancy : IGridOccupancy
    {
        private readonly Dictionary<GridPosition, Occupant> occupantsByPosition;

        public TeamGridOccupancy(params (GridPosition Position, UnitId UnitId, TeamId Team)[] occupants)
        {
            occupantsByPosition = new Dictionary<GridPosition, Occupant>();
            foreach (var occupant in occupants)
            {
                occupantsByPosition.Add(occupant.Position, new Occupant(occupant.UnitId, occupant.Team));
            }
        }

        public bool IsOccupied(GridPosition position)
        {
            return occupantsByPosition.ContainsKey(position);
        }

        public bool TryGetOccupant(GridPosition position, out UnitId occupantId)
        {
            if (occupantsByPosition.TryGetValue(position, out var occupant))
            {
                occupantId = occupant.UnitId;
                return true;
            }

            occupantId = UnitId.None;
            return false;
        }

        public bool CanEnter(GridPosition position)
        {
            return !IsOccupied(position);
        }

        public TeamId GetTeam(GridPosition position)
        {
            return occupantsByPosition[position].Team;
        }

        private readonly struct Occupant
        {
            public Occupant(UnitId unitId, TeamId team)
            {
                UnitId = unitId;
                Team = team;
            }

            public UnitId UnitId { get; }

            public TeamId Team { get; }
        }
    }
}
