using System;
using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using ReactionTactics.Grid;
using ReactionTactics.Targeting;

public sealed class ConeShapeTests
{
    [Test]
    public void GetConeCellsReturnsCardinalConeAtActualTerrainHeights()
    {
        var map = CreateSteppedMap(width: 8, depth: 8);
        var origin = new GridPosition(3, 99, 1);

        var cells = AreaShapeService.GetConeCells(origin, CardinalDirection.North, range: 4, map);

        Assert.That(cells, Is.EqualTo(new[]
        {
            new GridPosition(3, 5, 2),
            new GridPosition(2, 5, 3),
            new GridPosition(3, 6, 3),
            new GridPosition(4, 7, 3),
            new GridPosition(2, 6, 4),
            new GridPosition(3, 7, 4),
            new GridPosition(4, 8, 4),
            new GridPosition(1, 6, 5),
            new GridPosition(2, 7, 5),
            new GridPosition(3, 8, 5),
            new GridPosition(4, 9, 5),
            new GridPosition(5, 10, 5),
        }));
        Assert.That(cells.Contains(new GridPosition(3, 4, 1)), Is.False, "The actor's own cell is never part of the cone.");
        Assert.That(cells.Contains(new GridPosition(1, 5, 3)), Is.False, "Distance 2 only reaches one lateral cell on each side.");
        Assert.That(cells.Contains(new GridPosition(0, 5, 5)), Is.False, "Distance 4 only reaches two lateral cells on each side.");
    }

    [Test]
    public void GetConeCellsProjectsEveryCardinalDirectionFromOrigin()
    {
        var map = CreateSteppedMap(width: 7, depth: 7);
        var origin = new GridPosition(3, -50, 3);

        var north = new HashSet<GridPosition>(AreaShapeService.GetConeCells(origin, CardinalDirection.North, range: 2, map));
        var east = new HashSet<GridPosition>(AreaShapeService.GetConeCells(origin, CardinalDirection.East, range: 2, map));
        var south = new HashSet<GridPosition>(AreaShapeService.GetConeCells(origin, CardinalDirection.South, range: 2, map));
        var west = new HashSet<GridPosition>(AreaShapeService.GetConeCells(origin, CardinalDirection.West, range: 2, map));

        Assert.That(north, Is.EquivalentTo(new[]
        {
            new GridPosition(3, 7, 4),
            new GridPosition(2, 7, 5),
            new GridPosition(3, 8, 5),
            new GridPosition(4, 9, 5),
        }));
        Assert.That(east, Is.EquivalentTo(new[]
        {
            new GridPosition(4, 7, 3),
            new GridPosition(5, 9, 4),
            new GridPosition(5, 8, 3),
            new GridPosition(5, 7, 2),
        }));
        Assert.That(south, Is.EquivalentTo(new[]
        {
            new GridPosition(3, 5, 2),
            new GridPosition(4, 5, 1),
            new GridPosition(3, 4, 1),
            new GridPosition(2, 3, 1),
        }));
        Assert.That(west, Is.EquivalentTo(new[]
        {
            new GridPosition(2, 5, 3),
            new GridPosition(1, 5, 4),
            new GridPosition(1, 4, 3),
            new GridPosition(1, 3, 2),
        }));
    }

    [Test]
    public void GetConeCellsClipsToMapAndDoesNotInventOutOfBoundsCells()
    {
        var map = CreateSteppedMap(width: 3, depth: 3);
        var origin = new GridPosition(0, 0, 0);

        var cells = AreaShapeService.GetConeCells(origin, CardinalDirection.North, range: 4, map);

        Assert.That(cells, Is.EqualTo(new[]
        {
            new GridPosition(0, 1, 1),
            new GridPosition(0, 2, 2),
            new GridPosition(1, 3, 2),
        }));
        Assert.That(cells.All(position => position.X >= 0 && position.Z >= 0), Is.True);
        Assert.That(cells.Contains(origin), Is.False);
    }

    [Test]
    public void GetConeCellsIncludesBlockedAndUnwalkableCellsForInitialPrototypeShape()
    {
        var cells = new[]
        {
            new GridCell(new GridPosition(1, 0, 0)),
            new GridCell(new GridPosition(1, 5, 1), walkable: false, blocksMovement: true, blocksLineOfSight: true),
            new GridCell(new GridPosition(0, 6, 2), walkable: false, blocksMovement: true, blocksLineOfSight: false),
            new GridCell(new GridPosition(1, 7, 2), walkable: true, blocksMovement: false, blocksLineOfSight: true),
            new GridCell(new GridPosition(2, 8, 2)),
        };
        var map = new GridMap(cells);

        var affected = AreaShapeService.GetConeCells(new GridPosition(1, 0, 0), CardinalDirection.North, range: 2, map);

        Assert.That(affected, Is.EqualTo(new[]
        {
            new GridPosition(1, 5, 1),
            new GridPosition(0, 6, 2),
            new GridPosition(1, 7, 2),
            new GridPosition(2, 8, 2),
        }));
    }

    [Test]
    public void GetConeCellsWithZeroRangeReturnsNoCells()
    {
        var map = CreateSteppedMap(width: 3, depth: 3);

        var cells = AreaShapeService.GetConeCells(new GridPosition(1, 0, 1), CardinalDirection.North, range: 0, map);

        Assert.That(cells, Is.Empty);
    }

    [Test]
    public void GetConeCellsRejectsInvalidArguments()
    {
        var map = CreateSteppedMap(width: 1, depth: 1);

        Assert.Throws<ArgumentNullException>(() => AreaShapeService.GetConeCells(GridPosition.Zero, CardinalDirection.North, range: 1, map: null));
        Assert.Throws<ArgumentOutOfRangeException>(() => AreaShapeService.GetConeCells(GridPosition.Zero, CardinalDirection.North, range: -1, map));
        Assert.Throws<ArgumentOutOfRangeException>(() => AreaShapeService.GetConeCells(GridPosition.Zero, (CardinalDirection)999, range: 1, map));
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
