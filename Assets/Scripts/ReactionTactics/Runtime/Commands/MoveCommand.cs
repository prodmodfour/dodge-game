using System;
using ReactionTactics.Core;
using ReactionTactics.Grid;
using ReactionTactics.Pathfinding;
using ReactionTactics.Units;

namespace ReactionTactics.Commands
{
    /// <summary>
    /// Immutable command model for moving one unit along a precomputed grid path.
    /// The same model is used for active-turn movement and reaction movement.
    /// </summary>
    public sealed class MoveCommand
    {
        public MoveCommand(UnitId unit, GridPath path, MovementMode sourcePhase)
            : this(
                unit,
                GetPathDestinationOrDefault(path),
                path,
                path == null ? 0 : path.TotalApCost,
                sourcePhase)
        {
        }

        public MoveCommand(
            UnitId unit,
            GridPosition destination,
            GridPath path,
            int apCost,
            MovementMode sourcePhase)
        {
            var validation = Validate(unit, destination, path, apCost, sourcePhase);
            if (validation.IsFailure)
            {
                throw new ArgumentException(validation.ErrorMessage);
            }

            Unit = unit;
            Destination = destination;
            Path = path;
            ApCost = apCost;
            SourcePhase = sourcePhase;
        }

        public UnitId Unit { get; }

        public GridPosition Destination { get; }

        public GridPath Path { get; }

        public int ApCost { get; }

        public MovementMode SourcePhase { get; }

        public static MovementValidationResult TryCreate(
            UnitId unit,
            GridPath path,
            MovementMode sourcePhase)
        {
            return TryCreate(
                unit,
                GetPathDestinationOrDefault(path),
                path,
                path == null ? 0 : path.TotalApCost,
                sourcePhase);
        }

        public static MovementValidationResult TryCreate(
            UnitId unit,
            GridPosition destination,
            GridPath path,
            int apCost,
            MovementMode sourcePhase)
        {
            var validation = Validate(unit, destination, path, apCost, sourcePhase);
            if (validation.IsFailure)
            {
                return MovementValidationResult.Invalid(validation.ErrorMessage);
            }

            return MovementValidationResult.Valid(new MoveCommand(unit, destination, path, apCost, sourcePhase));
        }

        public override string ToString()
        {
            return $"{SourcePhase} move {Unit} to {Destination} for {ApCost} AP";
        }

        private static TacticalResult Validate(
            UnitId unit,
            GridPosition destination,
            GridPath path,
            int apCost,
            MovementMode sourcePhase)
        {
            if (!unit.IsAssigned)
            {
                return TacticalResult.Failure("Movement requires an assigned unit.");
            }

            if (path == null)
            {
                return TacticalResult.Failure("Movement requires a path.");
            }

            if (path.IsInvalid)
            {
                return TacticalResult.Failure($"Movement path is invalid: {path.FailureReason}");
            }

            if (apCost < 0)
            {
                return TacticalResult.Failure("Movement AP cost cannot be negative.");
            }

            if (!Enum.IsDefined(typeof(MovementMode), sourcePhase))
            {
                return TacticalResult.Failure("Movement source phase is unknown.");
            }

            if (path.Destination != destination)
            {
                return TacticalResult.Failure(
                    $"Movement destination {destination} must match path destination {path.Destination}.");
            }

            if (apCost != path.TotalApCost)
            {
                return TacticalResult.Failure(
                    $"Movement AP cost {apCost} must match path total AP cost {path.TotalApCost}.");
            }

            return TacticalResult.Success();
        }

        private static GridPosition GetPathDestinationOrDefault(GridPath path)
        {
            return path != null && path.IsValid ? path.Destination : GridPosition.Zero;
        }
    }
}
