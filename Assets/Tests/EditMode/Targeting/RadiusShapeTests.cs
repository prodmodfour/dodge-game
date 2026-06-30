using System;
using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using ReactionTactics.Grid;
using ReactionTactics.Targeting;

public sealed class RadiusShapeTests
{
    [Test]
    public void GetRadiusCellsReturnsHorizontalManhattanDiamondAtActualTerrainHeights()
    {
        var map = CreateSteppedMap(width: 5, depth: 5);
        var center = new GridPosition(2, 99, 2);

        var cells = AreaShapeService.GetRadiusCells(center, radius: 1, map);

        Assert.That(cells, Is.EqualTo(new[]
        {
            new GridPosition(2, 4, 2),
            new GridPosition(2, 3, 1),
            new GridPosition(1, 3, 2),
            new GridPosition(3, 5, 2),
            new GridPosition(2, 5, 3),
        }));
        Assert.That(cells.Contains(new GridPosition(1, 2, 1)), Is.False, "Diagonal cells are outside radius 1 when using Manhattan distance.");
    }

    [Test]
    public void GetRadiusCellsIncludesOnlyCellsWithinHorizontalManhattanRadius()
    {
        var map = CreateSteppedMap(width: 5, depth: 5);
        var center = new GridPosition(2, 0, 2);

        var cells = AreaShapeService.GetRadiusCells(center, radius: 2, map);
        var cellSet = new HashSet<GridPosition>(cells);

        Assert.That(cells.Count, Is.EqualTo(13));
        Assert.That(cellSet.Contains(new GridPosition(0, 2, 2)), Is.True, "Cells two horizontal steps away are included.");
        Assert.That(cellSet.Contains(new GridPosition(1, 2, 1)), Is.True, "Diagonal cells whose |dx| + |dz| is within radius are included.");
        Assert.That(cellSet.Contains(new GridPosition(0, 0, 0)), Is.False, "Cells whose |dx| + |dz| exceeds radius are excluded.");
        Assert.That(cells.Select(position => center.HorizontalDistanceTo(position)), Is.Ordered);
    }

    [Test]
    public void GetRadiusCellsClipsToMapAndDoesNotInventOutOfBoundsCells()
    {
        var map = CreateSteppedMap(width: 3, depth: 3);
        var center = new GridPosition(0, 0, 0);

        var cells = AreaShapeService.GetRadiusCells(center, radius: 2, map);

        Assert.That(cells, Is.EqualTo(new[]
        {
            new GridPosition(0, 0, 0),
            new GridPosition(1, 1, 0),
            new GridPosition(0, 1, 1),
            new GridPosition(2, 2, 0),
            new GridPosition(1, 2, 1),
            new GridPosition(0, 2, 2),
        }));
        Assert.That(cells.All(position => position.X >= 0 && position.Z >= 0), Is.True);
    }

    [Test]
    public void GetRadiusCellsIncludesBlockedAndUnwalkableCellsForInitialPrototypeShape()
    {
        var cells = new[]
        {
            new GridCell(new GridPosition(1, 0, 1)),
            new GridCell(new GridPosition(1, 2, 0), walkable: false, blocksMovement: true, blocksLineOfSight: true),
            new GridCell(new GridPosition(0, 3, 1), walkable: false, blocksMovement: true, blocksLineOfSight: false),
            new GridCell(new GridPosition(2, 4, 1), walkable: true, blocksMovement: false, blocksLineOfSight: true),
            new GridCell(new GridPosition(1, 5, 2)),
        };
        var map = new GridMap(cells);

        var affected = AreaShapeService.GetRadiusCells(new GridPosition(1, 0, 1), radius: 1, map);

        Assert.That(affected, Is.EqualTo(new[]
        {
            new GridPosition(1, 0, 1),
            new GridPosition(1, 2, 0),
            new GridPosition(0, 3, 1),
            new GridPosition(2, 4, 1),
            new GridPosition(1, 5, 2),
        }));
    }

    [Test]
    public void GetRadiusCellsWithZeroRadiusReturnsCenterCellAtActualHeightWhenPresent()
    {
        var map = CreateSteppedMap(width: 3, depth: 3);

        var cells = AreaShapeService.GetRadiusCells(new GridPosition(1, -4, 1), radius: 0, map);

        Assert.That(cells, Is.EqualTo(new[] { new GridPosition(1, 2, 1) }));
    }

    [Test]
    public void GetRadiusCellsRejectsInvalidArguments()
    {
        var map = CreateSteppedMap(width: 1, depth: 1);

        Assert.Throws<ArgumentNullException>(() => AreaShapeService.GetRadiusCells(GridPosition.Zero, radius: 1, map: null));
        Assert.Throws<ArgumentOutOfRangeException>(() => AreaShapeService.GetRadiusCells(GridPosition.Zero, radius: -1, map));
    }

    private static GridMap CreateSteppedMap(int width, int depth)
    {
        var cells = new List<GridCell>();
        for (var z = 0; z < depth; z += 1)
        {
            for (var x = 0; x < width; x += 1)
            {
                cells.Add(new GridCell(new GridPosition(x, x + z, z)));
            }
        }

        return new GridMap(cells);
    }
}
