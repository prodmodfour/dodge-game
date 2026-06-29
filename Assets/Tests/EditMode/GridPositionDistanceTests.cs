using System;
using NUnit.Framework;
using ReactionTactics.Grid;

namespace ReactionTactics.Tests.EditMode
{
    public sealed class GridPositionDistanceTests
    {
        [Test]
        public void ManhattanDistanceIsSymmetricAndIncludesVerticalDelta()
        {
            var first = new GridPosition(1, 2, 3);
            var second = new GridPosition(-2, 5, 8);

            Assert.That(first.ManhattanDistanceTo(second), Is.EqualTo(11));
            Assert.That(second.ManhattanDistanceTo(first), Is.EqualTo(11));
            Assert.That(GridPosition.ManhattanDistance(first, second), Is.EqualTo(11));
        }

        [Test]
        public void HorizontalDistanceIsSymmetricAndIgnoresHeight()
        {
            var low = new GridPosition(4, -3, 10);
            var high = new GridPosition(1, 12, 5);

            Assert.That(low.HorizontalDistanceTo(high), Is.EqualTo(8));
            Assert.That(high.HorizontalDistanceTo(low), Is.EqualTo(8));
            Assert.That(GridPosition.HorizontalDistance(low, high), Is.EqualTo(8));
        }

        [Test]
        public void TacticalDistanceAppliesConfiguredVerticalWeight()
        {
            var origin = GridPosition.Zero;
            var target = new GridPosition(2, 3, -1);

            Assert.That(origin.TacticalDistanceTo(target), Is.EqualTo(6));
            Assert.That(origin.TacticalDistanceTo(target, verticalWeight: 0), Is.EqualTo(3));
            Assert.That(origin.TacticalDistanceTo(target, verticalWeight: 2), Is.EqualTo(9));
            Assert.That(target.TacticalDistanceTo(origin, verticalWeight: 2), Is.EqualTo(9));
        }

        [Test]
        public void TacticalDistanceRejectsNegativeVerticalWeight()
        {
            var origin = GridPosition.Zero;
            var target = GridPosition.Up;

            Assert.Throws<ArgumentOutOfRangeException>(() => origin.TacticalDistanceTo(target, verticalWeight: -1));
        }

        [Test]
        public void FourWayAdjacencyRequiresExactlyOneHorizontalCardinalStep()
        {
            var center = new GridPosition(5, 1, 5);

            Assert.That(center.IsFourWayAdjacentTo(center + GridPosition.North), Is.True);
            Assert.That(center.IsFourWayAdjacentTo(center + GridPosition.East), Is.True);
            Assert.That(center.IsFourWayAdjacentTo(center), Is.False);
            Assert.That(center.IsFourWayAdjacentTo(center + GridPosition.Up), Is.False);
            Assert.That(center.IsFourWayAdjacentTo(center + GridPosition.North + GridPosition.East), Is.False);
        }

        [Test]
        public void FourWayAdjacencyCanLimitHeightDifference()
        {
            var low = new GridPosition(0, 2, 0);
            var highNeighbor = new GridPosition(1, 4, 0);

            Assert.That(low.IsFourWayAdjacentTo(highNeighbor), Is.True);
            Assert.That(low.IsFourWayAdjacentTo(highNeighbor, maxHeightDifference: 2), Is.True);
            Assert.That(low.IsFourWayAdjacentTo(highNeighbor, maxHeightDifference: 1), Is.False);
            Assert.That(GridPosition.AreFourWayAdjacent(highNeighbor, low, maxHeightDifference: 2), Is.True);
        }
    }
}
