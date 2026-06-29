using System;
using NUnit.Framework;
using ReactionTactics.Grid;

namespace ReactionTactics.Tests.EditMode
{
    public sealed class CardinalDirectionTests
    {
        [Test]
        public void ToOffsetReturnsExpectedHorizontalGridStepForEveryDirection()
        {
            Assert.That(CardinalDirection.North.ToOffset(), Is.EqualTo(new GridPosition(0, 0, 1)));
            Assert.That(CardinalDirection.East.ToOffset(), Is.EqualTo(new GridPosition(1, 0, 0)));
            Assert.That(CardinalDirection.South.ToOffset(), Is.EqualTo(new GridPosition(0, 0, -1)));
            Assert.That(CardinalDirection.West.ToOffset(), Is.EqualTo(new GridPosition(-1, 0, 0)));
        }

        [Test]
        public void LeftAndRightHelpersRotateClockwiseAndCounterClockwise()
        {
            Assert.That(CardinalDirection.North.Left(), Is.EqualTo(CardinalDirection.West));
            Assert.That(CardinalDirection.North.Right(), Is.EqualTo(CardinalDirection.East));

            Assert.That(CardinalDirection.East.Left(), Is.EqualTo(CardinalDirection.North));
            Assert.That(CardinalDirection.East.Right(), Is.EqualTo(CardinalDirection.South));

            Assert.That(CardinalDirection.South.Left(), Is.EqualTo(CardinalDirection.East));
            Assert.That(CardinalDirection.South.Right(), Is.EqualTo(CardinalDirection.West));

            Assert.That(CardinalDirection.West.Left(), Is.EqualTo(CardinalDirection.South));
            Assert.That(CardinalDirection.West.Right(), Is.EqualTo(CardinalDirection.North));
        }

        [Test]
        public void PerpendicularHelpersReturnLeftAndRightDirectionsAndOffsets()
        {
            var northPerpendiculars = CardinalDirection.North.Perpendiculars();
            Assert.That(northPerpendiculars.Left, Is.EqualTo(CardinalDirection.West));
            Assert.That(northPerpendiculars.Right, Is.EqualTo(CardinalDirection.East));
            Assert.That(CardinalDirection.North.LeftOffset(), Is.EqualTo(GridPosition.West));
            Assert.That(CardinalDirection.North.RightOffset(), Is.EqualTo(GridPosition.East));

            var eastPerpendiculars = CardinalDirection.East.Perpendiculars();
            Assert.That(eastPerpendiculars.Left, Is.EqualTo(CardinalDirection.North));
            Assert.That(eastPerpendiculars.Right, Is.EqualTo(CardinalDirection.South));
            Assert.That(CardinalDirection.East.LeftOffset(), Is.EqualTo(GridPosition.North));
            Assert.That(CardinalDirection.East.RightOffset(), Is.EqualTo(GridPosition.South));
        }

        [Test]
        public void OppositeReturnsReverseDirection()
        {
            Assert.That(CardinalDirection.North.Opposite(), Is.EqualTo(CardinalDirection.South));
            Assert.That(CardinalDirection.East.Opposite(), Is.EqualTo(CardinalDirection.West));
            Assert.That(CardinalDirection.South.Opposite(), Is.EqualTo(CardinalDirection.North));
            Assert.That(CardinalDirection.West.Opposite(), Is.EqualTo(CardinalDirection.East));
        }

        [Test]
        public void FromToChoosesDominantAxisDirection()
        {
            var origin = new GridPosition(5, 9, 5);

            Assert.That(CardinalDirectionMath.FromTo(origin, new GridPosition(6, -3, 9)), Is.EqualTo(CardinalDirection.North));
            Assert.That(CardinalDirectionMath.FromTo(origin, new GridPosition(9, -3, 6)), Is.EqualTo(CardinalDirection.East));
            Assert.That(CardinalDirectionMath.FromTo(origin, new GridPosition(4, -3, 1)), Is.EqualTo(CardinalDirection.South));
            Assert.That(CardinalDirectionMath.FromTo(origin, new GridPosition(1, -3, 4)), Is.EqualTo(CardinalDirection.West));
        }

        [Test]
        public void FromToResolvesDiagonalTiesTowardNorthSouthAxis()
        {
            var origin = GridPosition.Zero;

            Assert.That(CardinalDirectionMath.FromTo(origin, new GridPosition(3, 0, 3)), Is.EqualTo(CardinalDirection.North));
            Assert.That(CardinalDirectionMath.FromTo(origin, new GridPosition(-3, 0, 3)), Is.EqualTo(CardinalDirection.North));
            Assert.That(CardinalDirectionMath.FromTo(origin, new GridPosition(3, 0, -3)), Is.EqualTo(CardinalDirection.South));
            Assert.That(CardinalDirectionMath.FromTo(origin, new GridPosition(-3, 0, -3)), Is.EqualTo(CardinalDirection.South));
        }

        [Test]
        public void FromDeltaRejectsZeroHorizontalDelta()
        {
            Assert.Throws<ArgumentException>(() => CardinalDirectionMath.FromDelta(0, 0));
        }
    }
}
