using System;
using NUnit.Framework;
using ReactionTactics.Grid;

namespace ReactionTactics.Tests.EditMode
{
    public sealed class GridCellTests
    {
        [Test]
        public void ConstructorStoresTerrainFlagsHeightAndCost()
        {
            var position = new GridPosition(2, 3, 4);
            var cell = new GridCell(
                position,
                walkable: false,
                blocksMovement: true,
                blocksLineOfSight: true,
                movementCost: 5,
                displayHeight: 3.5f);

            Assert.That(cell.Position, Is.EqualTo(position));
            Assert.That(cell.Walkable, Is.False);
            Assert.That(cell.BlocksMovement, Is.True);
            Assert.That(cell.BlocksLineOfSight, Is.True);
            Assert.That(cell.MovementCost, Is.EqualTo(5));
            Assert.That(cell.DisplayHeight, Is.EqualTo(3.5f));
        }

        [Test]
        public void DefaultConstructorCreatesWalkableCellAtPositionHeight()
        {
            var position = new GridPosition(1, 2, 3);
            var cell = new GridCell(position);

            Assert.That(cell.Position, Is.EqualTo(position));
            Assert.That(cell.Walkable, Is.True);
            Assert.That(cell.BlocksMovement, Is.False);
            Assert.That(cell.BlocksLineOfSight, Is.False);
            Assert.That(cell.MovementCost, Is.EqualTo(GridCell.DefaultMovementCost));
            Assert.That(cell.DisplayHeight, Is.EqualTo(2f));
        }

        [Test]
        public void CopyWithCreatesModifiedCellWithoutChangingOriginal()
        {
            var original = new GridCell(
                new GridPosition(0, 1, 0),
                walkable: true,
                blocksMovement: false,
                blocksLineOfSight: false,
                movementCost: 2,
                displayHeight: 1.25f);

            var modified = original.CopyWith(
                walkable: false,
                blocksMovement: true,
                blocksLineOfSight: true,
                movementCost: 4,
                displayHeight: 2.5f);

            Assert.That(original.Walkable, Is.True);
            Assert.That(original.BlocksMovement, Is.False);
            Assert.That(original.BlocksLineOfSight, Is.False);
            Assert.That(original.MovementCost, Is.EqualTo(2));
            Assert.That(original.DisplayHeight, Is.EqualTo(1.25f));

            Assert.That(modified.Position, Is.EqualTo(original.Position));
            Assert.That(modified.Walkable, Is.False);
            Assert.That(modified.BlocksMovement, Is.True);
            Assert.That(modified.BlocksLineOfSight, Is.True);
            Assert.That(modified.MovementCost, Is.EqualTo(4));
            Assert.That(modified.DisplayHeight, Is.EqualTo(2.5f));
        }

        [Test]
        public void WithHelpersReturnIndependentCopies()
        {
            var original = new GridCell(new GridPosition(1, 0, 1));

            Assert.That(original.WithPosition(new GridPosition(2, 1, 2)).Position, Is.EqualTo(new GridPosition(2, 1, 2)));
            Assert.That(original.WithWalkable(false).Walkable, Is.False);
            Assert.That(original.WithBlocksMovement(true).BlocksMovement, Is.True);
            Assert.That(original.WithBlocksLineOfSight(true).BlocksLineOfSight, Is.True);
            Assert.That(original.WithMovementCost(3).MovementCost, Is.EqualTo(3));
            Assert.That(original.WithDisplayHeight(4f).DisplayHeight, Is.EqualTo(4f));

            Assert.That(original.Position, Is.EqualTo(new GridPosition(1, 0, 1)));
            Assert.That(original.Walkable, Is.True);
            Assert.That(original.BlocksMovement, Is.False);
            Assert.That(original.BlocksLineOfSight, Is.False);
            Assert.That(original.MovementCost, Is.EqualTo(GridCell.DefaultMovementCost));
            Assert.That(original.DisplayHeight, Is.EqualTo(0f));
        }

        [Test]
        public void CloneReturnsEquivalentCell()
        {
            var cell = new GridCell(
                new GridPosition(4, 2, 6),
                walkable: false,
                blocksMovement: true,
                blocksLineOfSight: false,
                movementCost: 6,
                displayHeight: 2f);

            Assert.That(cell.Clone(), Is.EqualTo(cell));
        }

        [Test]
        public void ConstructorRejectsInvalidMovementCost()
        {
            Assert.Throws<ArgumentOutOfRangeException>(() => new GridCell(GridPosition.Zero, movementCost: 0));
        }

        [Test]
        public void ConstructorRejectsNonFiniteDisplayHeight()
        {
            Assert.Throws<ArgumentOutOfRangeException>(() => new GridCell(
                GridPosition.Zero,
                walkable: true,
                blocksMovement: false,
                blocksLineOfSight: false,
                movementCost: 1,
                displayHeight: float.NaN));
        }
    }
}
