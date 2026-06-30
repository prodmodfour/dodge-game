using System;
using NUnit.Framework;
using ReactionTactics.Commands;
using ReactionTactics.Grid;
using ReactionTactics.Pathfinding;
using ReactionTactics.Units;

namespace ReactionTactics.Tests.EditMode.Commands
{
    public sealed class MoveCommandTests
    {
        [Test]
        public void MoveCommandStoresUnitDestinationPathCostAndSourcePhase()
        {
            var unit = new UnitId(7);
            var destination = GridPosition.East;
            var path = CreatePath(destination, totalApCost: 2);

            var command = new MoveCommand(unit, destination, path, apCost: 2, MovementMode.Active);

            Assert.That(command.Unit, Is.EqualTo(unit));
            Assert.That(command.Destination, Is.EqualTo(destination));
            Assert.That(command.Path, Is.SameAs(path));
            Assert.That(command.ApCost, Is.EqualTo(2));
            Assert.That(command.SourcePhase, Is.EqualTo(MovementMode.Active));
            Assert.That(command.ToString(), Is.EqualTo("Active move Unit#7 to (1,0,0) for 2 AP"));
        }

        [Test]
        public void MoveCommandConvenienceConstructorUsesPathDestinationAndCost()
        {
            var path = CreatePath(GridPosition.North, totalApCost: 1);

            var command = new MoveCommand(new UnitId(3), path, MovementMode.Reaction);

            Assert.That(command.Destination, Is.EqualTo(GridPosition.North));
            Assert.That(command.ApCost, Is.EqualTo(path.TotalApCost));
            Assert.That(command.SourcePhase, Is.EqualTo(MovementMode.Reaction));
        }

        [Test]
        public void MoveCommandRejectsInvalidInputs()
        {
            var unit = new UnitId(1);
            var path = CreatePath(GridPosition.East, totalApCost: 1);

            Assert.Throws<ArgumentException>(() => new MoveCommand(UnitId.None, path, MovementMode.Active));
            Assert.Throws<ArgumentException>(() => new MoveCommand(unit, null, MovementMode.Active));
            Assert.Throws<ArgumentException>(() => new MoveCommand(unit, GridPath.Failure("blocked"), MovementMode.Active));
            Assert.Throws<ArgumentException>(() => new MoveCommand(unit, GridPosition.North, path, 1, MovementMode.Active));
            Assert.Throws<ArgumentException>(() => new MoveCommand(unit, GridPosition.East, path, -1, MovementMode.Active));
            Assert.Throws<ArgumentException>(() => new MoveCommand(unit, GridPosition.East, path, 2, MovementMode.Active));
            Assert.Throws<ArgumentException>(() => new MoveCommand(unit, GridPosition.East, path, 1, (MovementMode)99));
        }

        [Test]
        public void TryCreateReturnsValidMovementResultWithoutThrowing()
        {
            var unit = new UnitId(2);
            var path = CreatePath(GridPosition.East, totalApCost: 1);

            var result = MoveCommand.TryCreate(unit, path, MovementMode.Reaction);

            Assert.That(result.IsValid, Is.True);
            Assert.That(result.IsInvalid, Is.False);
            Assert.That(result.ErrorMessage, Is.Empty);
            Assert.That(result.Command.Unit, Is.EqualTo(unit));
            Assert.That(result.Command.Path, Is.SameAs(path));
            Assert.That(result.WithoutCommand().IsSuccess, Is.True);
            Assert.That(result.ToCommandResult().Value, Is.SameAs(result.Command));
            Assert.That(result.ToString(), Is.EqualTo("Valid movement: Reaction move Unit#2 to (1,0,0) for 1 AP"));
        }

        [Test]
        public void TryCreateReturnsInvalidMovementResultWithReason()
        {
            var path = CreatePath(GridPosition.East, totalApCost: 1);

            var result = MoveCommand.TryCreate(UnitId.None, path, MovementMode.Active);

            Assert.That(result.IsValid, Is.False);
            Assert.That(result.IsInvalid, Is.True);
            Assert.That(result.ErrorMessage, Is.EqualTo("Movement requires an assigned unit."));
            Assert.Throws<InvalidOperationException>(() => _ = result.Command);
            Assert.That(result.WithoutCommand().IsFailure, Is.True);
            Assert.That(result.ToCommandResult().IsFailure, Is.True);
            Assert.That(result.ToString(), Is.EqualTo("Invalid movement: Movement requires an assigned unit."));
        }

        [Test]
        public void MovementValidationResultRejectsNullValidCommandAndNormalizesFailures()
        {
            Assert.Throws<ArgumentNullException>(() => MovementValidationResult.Valid(null));

            var result = MovementValidationResult.Invalid("  ");

            Assert.That(result.IsInvalid, Is.True);
            Assert.That(result.ErrorMessage, Is.EqualTo("Command failed."));
        }

        private static GridPath CreatePath(GridPosition destination, int totalApCost)
        {
            return GridPath.Success(new[]
            {
                new PathStep(GridPosition.Zero, stepApCost: 0, totalApCost: 0),
                new PathStep(destination, stepApCost: totalApCost, totalApCost: totalApCost),
            });
        }
    }
}
