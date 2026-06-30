using System.Linq;
using NUnit.Framework;
using ReactionTactics.Grid;
using UnityEditor;

public sealed class GridTerrain
{
    private const string DefaultMapPath = "Assets/ScriptableObjects/DefaultPrototypeMap.asset";

    [Test]
    public void DefaultPrototypeMapBuildsExpectedDimensionsWithoutScene()
    {
        var definition = LoadDefaultMapDefinition();
        var map = definition.BuildGridMap();

        Assert.That(definition.Width, Is.EqualTo(8));
        Assert.That(definition.Depth, Is.EqualTo(8));
        Assert.That(definition.DefaultHeightY, Is.EqualTo(0));
        Assert.That(map.AllCells, Has.Count.EqualTo(64));
        Assert.That(map.MinBounds, Is.EqualTo(new GridPosition(0, 0, 0)));
        Assert.That(map.MaxBounds, Is.EqualTo(new GridPosition(7, 2, 7)));
        Assert.That(
            map.AllCells.Select(cell => cell.Position.ToHorizontal()).Distinct().Count(),
            Is.EqualTo(64),
            "The default terrain should produce one cell for every x/z coordinate.");
    }

    [Test]
    public void DefaultPrototypeMapPreservesBlockerAndWalkabilityFlags()
    {
        var map = LoadDefaultMapDefinition().BuildGridMap();

        AssertMovementAndSightBlocker(map, new GridPosition(3, 2, 3));
        AssertMovementAndSightBlocker(map, new GridPosition(4, 2, 3));
        AssertMovementAndSightBlocker(map, new GridPosition(4, 2, 4));

        var movementOnlyBlocker = GetCellAtHorizontal(map, x: 2, z: 5);
        Assert.That(movementOnlyBlocker.Position, Is.EqualTo(new GridPosition(2, 1, 5)));
        Assert.That(movementOnlyBlocker.Walkable, Is.False);
        Assert.That(movementOnlyBlocker.BlocksMovement, Is.True);
        Assert.That(movementOnlyBlocker.BlocksLineOfSight, Is.False);

        var playerStart = GetCellAtHorizontal(map, x: 0, z: 0);
        var enemyStart = GetCellAtHorizontal(map, x: 7, z: 7);
        Assert.That(playerStart.Walkable, Is.True);
        Assert.That(playerStart.BlocksMovement, Is.False);
        Assert.That(enemyStart.Walkable, Is.True);
        Assert.That(enemyStart.BlocksMovement, Is.False);

        Assert.That(map.AllCells.Count(cell => !cell.Walkable || cell.BlocksMovement), Is.EqualTo(4));
        Assert.That(map.AllCells.Count(cell => cell.BlocksLineOfSight), Is.EqualTo(3));
    }

    [Test]
    public void DefaultPrototypeMapCellHeightsBecomeGridPositionYValues()
    {
        var map = LoadDefaultMapDefinition().BuildGridMap();

        Assert.That(GetCellAtHorizontal(map, x: 0, z: 0).Position, Is.EqualTo(new GridPosition(0, 0, 0)));
        Assert.That(GetCellAtHorizontal(map, x: 6, z: 0).Position, Is.EqualTo(new GridPosition(6, 1, 0)));
        Assert.That(GetCellAtHorizontal(map, x: 5, z: 2).Position, Is.EqualTo(new GridPosition(5, 2, 2)));
        Assert.That(GetCellAtHorizontal(map, x: 3, z: 4).Position, Is.EqualTo(new GridPosition(3, 2, 4)));
        Assert.That(map.Contains(new GridPosition(5, 0, 2)), Is.False, "Raised cells should only exist at their configured terrain height.");
    }

    [Test]
    public void GridTerrainNeighborServiceHonorsHeightLimitsOnDefaultMap()
    {
        var map = LoadDefaultMapDefinition().BuildGridMap();
        var climbOrigin = new GridPosition(4, 1, 2);
        var climbAllowed = new GridNeighborService(maxClimb: 1, maxDrop: 1);
        var climbBlocked = new GridNeighborService(maxClimb: 0, maxDrop: 1);

        Assert.That(climbAllowed.GetNeighbors(map, climbOrigin), Is.EqualTo(new[]
        {
            new GridPosition(5, 2, 2),
            new GridPosition(4, 1, 1),
            new GridPosition(3, 1, 2),
        }));
        Assert.That(climbBlocked.GetNeighbors(map, climbOrigin), Is.EqualTo(new[]
        {
            new GridPosition(4, 1, 1),
            new GridPosition(3, 1, 2),
        }));

        var dropOrigin = new GridPosition(5, 2, 2);
        var dropAllowed = new GridNeighborService(maxClimb: 1, maxDrop: 1);
        var dropBlocked = new GridNeighborService(maxClimb: 1, maxDrop: 0);

        Assert.That(dropAllowed.GetNeighbors(map, dropOrigin), Is.EqualTo(new[]
        {
            new GridPosition(5, 2, 3),
            new GridPosition(6, 1, 2),
            new GridPosition(5, 1, 1),
            new GridPosition(4, 1, 2),
        }));
        Assert.That(dropBlocked.GetNeighbors(map, dropOrigin), Is.EqualTo(new[]
        {
            new GridPosition(5, 2, 3),
        }));
    }

    [Test]
    public void GridTerrainMovementCostsCoverFlatRoughAndUphillCells()
    {
        var map = LoadDefaultMapDefinition().BuildGridMap();
        var service = new MovementCostService(
            new GridNeighborService(maxClimb: 1, maxDrop: 1),
            uphillSurchargePerHeight: 2);

        var flatCost = service.CalculateCost(
            map,
            new GridPosition(0, 0, 0),
            new GridPosition(1, 0, 0));
        var roughFlatCost = service.CalculateCost(
            map,
            new GridPosition(3, 1, 2),
            new GridPosition(2, 1, 2));
        var uphillCost = service.CalculateCost(
            map,
            new GridPosition(4, 1, 2),
            new GridPosition(5, 2, 2));

        Assert.That(flatCost.IsSuccess, Is.True);
        Assert.That(flatCost.Value, Is.EqualTo(1));
        Assert.That(roughFlatCost.IsSuccess, Is.True);
        Assert.That(roughFlatCost.Value, Is.EqualTo(2), "Flat movement should still use the rough destination cell's movement cost.");
        Assert.That(uphillCost.IsSuccess, Is.True);
        Assert.That(uphillCost.Value, Is.EqualTo(3), "Uphill movement should include destination cost plus configured climb surcharge.");
    }

    private static GridMapDefinition LoadDefaultMapDefinition()
    {
        var definition = AssetDatabase.LoadAssetAtPath<GridMapDefinition>(DefaultMapPath);
        Assert.That(definition, Is.Not.Null, $"Expected default prototype map asset at '{DefaultMapPath}'.");
        return definition;
    }

    private static GridCell GetCellAtHorizontal(GridMap map, int x, int z)
    {
        return map.AllCells.Single(cell => cell.Position.X == x && cell.Position.Z == z);
    }

    private static void AssertMovementAndSightBlocker(GridMap map, GridPosition position)
    {
        var cell = map.GetCell(position);
        Assert.That(cell.Walkable, Is.False, $"Expected {position} to be non-walkable.");
        Assert.That(cell.BlocksMovement, Is.True, $"Expected {position} to block movement.");
        Assert.That(cell.BlocksLineOfSight, Is.True, $"Expected {position} to block line of sight.");
    }
}
