using System;
using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using ReactionTactics.Grid;
using ReactionTactics.Pathfinding;
using ReactionTactics.UI;

namespace ReactionTactics.Tests.EditMode.UI
{
    public sealed class MovementPreviewTests
    {
        [Test]
        public void CreateStoresReachableCellsAndApCosts()
        {
            var start = new GridPosition(0, 0, 0);
            var north = new GridPosition(0, 0, 1);
            var east = new GridPosition(1, 0, 0);
            var destination = new GridPosition(1, 0, 1);
            var map = new GridMap(
                new GridCell(start),
                new GridCell(north),
                new GridCell(east),
                new GridCell(destination));

            var preview = MovementPreview.Create(map, start, apBudget: 2);

            Assert.That(preview.StartPosition, Is.EqualTo(start));
            Assert.That(preview.ApBudget, Is.EqualTo(2));
            Assert.That(preview.ReachableCells.Keys, Is.EquivalentTo(new[] { start, north, east, destination }));
            Assert.That(preview.ApCosts[start], Is.Zero);
            Assert.That(preview.ApCosts[north], Is.EqualTo(1));
            Assert.That(preview.ApCosts[east], Is.EqualTo(1));
            Assert.That(preview.ApCosts[destination], Is.EqualTo(2));
            Assert.That(preview.HasHoveredDestination, Is.False);
            Assert.That(preview.HasSelectedPath, Is.False);
            Assert.That(preview.SelectedPath.IsInvalid, Is.True);
        }

        [Test]
        public void RecomputeSelectedPathBuildsPathFromReachableParents()
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
            var preview = MovementPreview.Create(map, start, apBudget: 3);

            var hoveredPreview = preview.RecomputeSelectedPath(destination);

            Assert.That(preview.HasSelectedPath, Is.False, "MovementPreview should be immutable when hover changes.");
            Assert.That(hoveredPreview.HasHoveredDestination, Is.True);
            Assert.That(hoveredPreview.HoveredDestination, Is.EqualTo(destination));
            Assert.That(hoveredPreview.HasSelectedPath, Is.True, hoveredPreview.SelectedPath.FailureReason);
            Assert.That(hoveredPreview.SelectedPath.Positions.ToArray(), Is.EqualTo(new[] { start, north, destination }));
            Assert.That(hoveredPreview.SelectedPath.TotalApCost, Is.EqualTo(2));
        }

        [Test]
        public void RecomputeSelectedPathReportsUnreachableHoveredDestination()
        {
            var start = new GridPosition(0, 0, 0);
            var middle = new GridPosition(1, 0, 0);
            var destination = new GridPosition(2, 0, 0);
            var map = new GridMap(
                new GridCell(start),
                new GridCell(middle),
                new GridCell(destination));
            var preview = MovementPreview.Create(map, start, apBudget: 1);

            var hoveredPreview = preview.RecomputeSelectedPath(destination);

            Assert.That(hoveredPreview.HasHoveredDestination, Is.True);
            Assert.That(hoveredPreview.HoveredDestination, Is.EqualTo(destination));
            Assert.That(hoveredPreview.HasSelectedPath, Is.False);
            Assert.That(hoveredPreview.SelectedPath.IsInvalid, Is.True);
            Assert.That(hoveredPreview.SelectedPath.FailureReason, Does.Contain($"Destination {destination} is not reachable"));
            Assert.That(hoveredPreview.TryGetApCost(destination, out _), Is.False);
        }

        [Test]
        public void ClearSelectedPathRemovesHoverAndSelectedPath()
        {
            var start = new GridPosition(0, 0, 0);
            var destination = new GridPosition(1, 0, 0);
            var map = new GridMap(new GridCell(start), new GridCell(destination));
            var preview = MovementPreview.Create(map, start, apBudget: 1)
                .RecomputeSelectedPath(destination);

            var cleared = preview.ClearSelectedPath();

            Assert.That(preview.HasSelectedPath, Is.True);
            Assert.That(cleared.HasHoveredDestination, Is.False);
            Assert.That(cleared.HasSelectedPath, Is.False);
            Assert.That(cleared.SelectedPath.FailureReason, Is.EqualTo("No hovered destination selected."));
            Assert.That(cleared.IsReachable(destination), Is.True);
            Assert.That(cleared.TryGetApCost(destination, out var apCost), Is.True);
            Assert.That(apCost, Is.EqualTo(1));
        }

        [Test]
        public void ConstructorRejectsInvalidReachableCellData()
        {
            var start = new GridPosition(0, 0, 0);
            var east = new GridPosition(1, 0, 0);

            Assert.Throws<ArgumentNullException>(() => new MovementPreview(start, 1, null));
            Assert.Throws<ArgumentOutOfRangeException>(() => new MovementPreview(
                start,
                -1,
                new Dictionary<GridPosition, ReachableCell>
                {
                    { start, new ReachableCell(start, totalApCost: 0) },
                }));
            Assert.Throws<ArgumentException>(() => new MovementPreview(
                start,
                1,
                new Dictionary<GridPosition, ReachableCell>()));
            Assert.Throws<ArgumentException>(() => new MovementPreview(
                start,
                1,
                new Dictionary<GridPosition, ReachableCell>
                {
                    { start, new ReachableCell(start, totalApCost: 1) },
                }));
            Assert.Throws<ArgumentException>(() => new MovementPreview(
                start,
                1,
                new Dictionary<GridPosition, ReachableCell>
                {
                    { start, new ReachableCell(start, totalApCost: 0) },
                    { east, new ReachableCell(GridPosition.North, totalApCost: 1, previousPosition: start) },
                }));
            Assert.Throws<ArgumentException>(() => new MovementPreview(
                start,
                0,
                new Dictionary<GridPosition, ReachableCell>
                {
                    { start, new ReachableCell(start, totalApCost: 0) },
                    { east, new ReachableCell(east, totalApCost: 1, previousPosition: start) },
                }));
        }
    }
}
