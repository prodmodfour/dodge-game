using System;
using System.Collections.Generic;
using NUnit.Framework;
using ReactionTactics.Grid;
using ReactionTactics.Targeting;

public sealed class LineOfSightTests
{
    [Test]
    public void GetLineCellsProjectsSimpleGridLineAtActualTerrainHeights()
    {
        var map = CreateSteppedMap(width: 5, depth: 3);
        var origin = new GridPosition(0, 99, 0);
        var target = new GridPosition(4, -20, 2);

        var line = LineOfSightService.GetLineCells(origin, target, map);

        Assert.That(line, Is.EqualTo(new[]
        {
            new GridPosition(0, 0, 0),
            new GridPosition(1, 2, 1),
            new GridPosition(2, 3, 1),
            new GridPosition(3, 5, 2),
            new GridPosition(4, 6, 2),
        }));
    }

    [Test]
    public void HasLineOfSightReturnsFalseWhenIntermediateCellBlocksSight()
    {
        var blocker = new GridPosition(2, 5, 0);
        var map = new GridMap(
            new GridCell(new GridPosition(0, 0, 0)),
            new GridCell(new GridPosition(1, 0, 0)),
            new GridCell(blocker, blocksLineOfSight: true),
            new GridCell(new GridPosition(3, 0, 0)),
            new GridCell(new GridPosition(4, 0, 0)));

        var hasLineOfSight = LineOfSightService.HasLineOfSight(
            new GridPosition(0, 0, 0),
            new GridPosition(4, 0, 0),
            map);
        var foundBlocker = LineOfSightService.TryFindBlockingCell(
            new GridPosition(0, 0, 0),
            new GridPosition(4, 0, 0),
            map,
            out var blockingPosition);

        Assert.That(hasLineOfSight, Is.False);
        Assert.That(foundBlocker, Is.True);
        Assert.That(blockingPosition, Is.EqualTo(blocker));
    }

    [Test]
    public void HasLineOfSightIgnoresSightBlockingOriginAndTargetCells()
    {
        var map = new GridMap(
            new GridCell(new GridPosition(0, 3, 0), blocksLineOfSight: true),
            new GridCell(new GridPosition(1, 0, 0)),
            new GridCell(new GridPosition(2, 4, 0), blocksLineOfSight: true));

        var hasLineOfSight = LineOfSightService.HasLineOfSight(
            new GridPosition(0, 0, 0),
            new GridPosition(2, 0, 0),
            map);

        Assert.That(hasLineOfSight, Is.True, "Endpoint blockers do not block their own target line; only intermediate blockers do.");
    }

    [Test]
    public void MissingCellOnProjectedLineBlocksSight()
    {
        var map = new GridMap(
            new GridCell(new GridPosition(0, 0, 0)),
            new GridCell(new GridPosition(2, 0, 0)));

        var hasLineOfSight = LineOfSightService.HasLineOfSight(
            new GridPosition(0, 0, 0),
            new GridPosition(2, 0, 0),
            map);
        var foundBlocker = LineOfSightService.TryFindBlockingCell(
            new GridPosition(0, 0, 0),
            new GridPosition(2, 0, 0),
            map,
            out var blockingPosition);

        Assert.That(hasLineOfSight, Is.False);
        Assert.That(foundBlocker, Is.True);
        Assert.That(blockingPosition, Is.EqualTo(new GridPosition(1, 0, 0)));
    }

    [Test]
    public void HasLineOfSightUsesHorizontalProjectionRegardlessOfRequestedHeights()
    {
        var blocker = new GridPosition(1, 10, 0);
        var map = new GridMap(
            new GridCell(new GridPosition(0, 0, 0)),
            new GridCell(blocker, blocksLineOfSight: true),
            new GridCell(new GridPosition(2, -3, 0)));

        var foundBlocker = LineOfSightService.TryFindBlockingCell(
            new GridPosition(0, 50, 0),
            new GridPosition(2, -50, 0),
            map,
            out var blockingPosition);

        Assert.That(foundBlocker, Is.True);
        Assert.That(blockingPosition, Is.EqualTo(blocker));
    }

    [Test]
    public void LineOfSightRejectsInvalidArguments()
    {
        var map = CreateSteppedMap(width: 1, depth: 1);

        Assert.Throws<ArgumentNullException>(() => LineOfSightService.GetLineCells(GridPosition.Zero, GridPosition.Zero, null));
        Assert.Throws<ArgumentNullException>(() => LineOfSightService.HasLineOfSight(GridPosition.Zero, GridPosition.Zero, null));
        Assert.Throws<ArgumentNullException>(() => LineOfSightService.TryFindBlockingCell(GridPosition.Zero, GridPosition.Zero, null, out _));
        Assert.That(LineOfSightService.HasLineOfSight(GridPosition.Zero, GridPosition.Zero, map), Is.True);
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
