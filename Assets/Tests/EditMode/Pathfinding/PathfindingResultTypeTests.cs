using System;
using System.Linq;
using NUnit.Framework;
using ReactionTactics.Grid;
using ReactionTactics.Pathfinding;

namespace ReactionTactics.Tests.EditMode.Pathfinding
{
    public sealed class PathfindingResultTypeTests
    {
        [Test]
        public void PathStepStoresPositionAndApCosts()
        {
            var position = new GridPosition(2, 1, 3);

            var step = new PathStep(position, stepApCost: 2, totalApCost: 5);

            Assert.That(step.Position, Is.EqualTo(position));
            Assert.That(step.StepApCost, Is.EqualTo(2));
            Assert.That(step.TotalApCost, Is.EqualTo(5));
            Assert.That(step.ToString(), Is.EqualTo("(2,1,3) stepAP=2 totalAP=5"));
            Assert.That(step, Is.EqualTo(new PathStep(position, 2, 5)));
        }

        [Test]
        public void PathStepRejectsInvalidCosts()
        {
            Assert.Throws<ArgumentOutOfRangeException>(() => new PathStep(GridPosition.Zero, -1, 0));
            Assert.Throws<ArgumentOutOfRangeException>(() => new PathStep(GridPosition.Zero, 0, -1));
            Assert.Throws<ArgumentException>(() => new PathStep(GridPosition.Zero, 2, 1));
        }

        [Test]
        public void SuccessfulGridPathExposesOrderedPositionsAndTotalApCost()
        {
            var start = GridPosition.Zero;
            var middle = GridPosition.East;
            var destination = GridPosition.East + GridPosition.North;
            var steps = new[]
            {
                new PathStep(start, stepApCost: 0, totalApCost: 0),
                new PathStep(middle, stepApCost: 1, totalApCost: 1),
                new PathStep(destination, stepApCost: 2, totalApCost: 3),
            };

            var path = GridPath.Success(steps);

            Assert.That(path.IsValid, Is.True);
            Assert.That(path.IsInvalid, Is.False);
            Assert.That(path.TotalApCost, Is.EqualTo(3));
            Assert.That(path.FailureReason, Is.Empty);
            Assert.That(path.Start, Is.EqualTo(start));
            Assert.That(path.Destination, Is.EqualTo(destination));
            Assert.That(path.Steps.ToArray(), Is.EqualTo(steps));
            Assert.That(path.Positions.ToArray(), Is.EqualTo(new[] { start, middle, destination }));
            Assert.That(path.ToString(), Is.EqualTo("Path totalAP=3: (0,0,0) -> (1,0,0) -> (1,0,1)"));
        }

        [Test]
        public void FailedGridPathCarriesFailureReasonWithoutPositions()
        {
            var path = GridPath.Failure("  Destination is blocked.  ");

            Assert.That(path.IsValid, Is.False);
            Assert.That(path.IsInvalid, Is.True);
            Assert.That(path.TotalApCost, Is.Zero);
            Assert.That(path.FailureReason, Is.EqualTo("Destination is blocked."));
            Assert.That(path.Steps, Is.Empty);
            Assert.That(path.Positions, Is.Empty);
            Assert.Throws<InvalidOperationException>(() => _ = path.Start);
            Assert.Throws<InvalidOperationException>(() => _ = path.Destination);
        }

        [Test]
        public void SuccessfulGridPathRejectsAmbiguousStepSequences()
        {
            Assert.Throws<ArgumentException>(() => GridPath.Success(Array.Empty<PathStep>()));
            Assert.Throws<ArgumentException>(() => GridPath.Success(new[]
            {
                new PathStep(GridPosition.Zero, stepApCost: 1, totalApCost: 1),
            }));
            Assert.Throws<ArgumentException>(() => GridPath.Success(new[]
            {
                new PathStep(GridPosition.Zero, stepApCost: 0, totalApCost: 0),
                new PathStep(GridPosition.East, stepApCost: 1, totalApCost: 3),
            }));
        }

        [Test]
        public void ReachableCellStoresCostAndOptionalPreviousPosition()
        {
            var previous = GridPosition.Zero;
            var position = GridPosition.East;

            var startCell = new ReachableCell(previous, totalApCost: 0);
            var reachableCell = new ReachableCell(position, totalApCost: 2, previousPosition: previous);

            Assert.That(startCell.Position, Is.EqualTo(previous));
            Assert.That(startCell.TotalApCost, Is.Zero);
            Assert.That(startCell.HasPreviousPosition, Is.False);
            Assert.That(reachableCell.Position, Is.EqualTo(position));
            Assert.That(reachableCell.TotalApCost, Is.EqualTo(2));
            Assert.That(reachableCell.HasPreviousPosition, Is.True);
            Assert.That(reachableCell.PreviousPosition.Value, Is.EqualTo(previous));
            Assert.That(reachableCell, Is.EqualTo(new ReachableCell(position, 2, previous)));
            Assert.That(reachableCell.ToString(), Is.EqualTo("(1,0,0) totalAP=2 previous=(0,0,0)"));
        }

        [Test]
        public void ReachableCellRejectsNegativeCost()
        {
            Assert.Throws<ArgumentOutOfRangeException>(() => new ReachableCell(GridPosition.Zero, -1));
        }
    }
}
