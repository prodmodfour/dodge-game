using System;
using ReactionTactics.Core;
using ReactionTactics.Grid;
using ReactionTactics.Pathfinding;
using ReactionTactics.Turns;
using ReactionTactics.Units;

namespace ReactionTactics.Commands
{
    /// <summary>
    /// Own-turn movement command for the current active unit. It builds the shared
    /// <see cref="MoveCommand" /> model from pathfinding, spends AP from the same
    /// wallet used by actions and reactions, and updates the unit's authoritative
    /// grid position without opening a reaction window.
    /// </summary>
    public readonly struct ActiveMoveCommand
    {
        private ActiveMoveCommand(TacticalUnit unit, MoveCommand moveCommand)
        {
            Unit = unit;
            MoveCommand = moveCommand;
        }

        public TacticalUnit Unit { get; }

        public MoveCommand MoveCommand { get; }

        public GridPath Path
        {
            get { return MoveCommand.Path; }
        }

        public GridPosition Destination
        {
            get { return MoveCommand.Destination; }
        }

        public int Cost
        {
            get { return MoveCommand.ApCost; }
        }

        public static TacticalResult<ActiveMoveCommand> TryCreate(
            TacticalUnit unit,
            GridPosition destination,
            IGridMap map,
            IGridOccupancy occupancy,
            CombatState combatState)
        {
            var timingResult = ValidateActiveTurn(unit, combatState);
            if (timingResult.IsFailure)
            {
                return TacticalResult<ActiveMoveCommand>.Failure(timingResult.ErrorMessage);
            }

            if (map == null)
            {
                return TacticalResult<ActiveMoveCommand>.Failure(
                    $"{DescribeUnit(unit)} cannot active move because no grid map is available.");
            }

            var pathResult = BuildActivePath(unit, destination, map, occupancy);
            if (pathResult.IsFailure)
            {
                return TacticalResult<ActiveMoveCommand>.Failure(pathResult.ErrorMessage);
            }

            var moveResult = MoveCommand.TryCreate(unit.UnitId, pathResult.Value, MovementMode.Active);
            if (moveResult.IsInvalid)
            {
                return TacticalResult<ActiveMoveCommand>.Failure(moveResult.ErrorMessage);
            }

            return TacticalResult<ActiveMoveCommand>.Success(new ActiveMoveCommand(unit, moveResult.Command));
        }

        /// <summary>
        /// Applies active movement side effects: spend AP and move the active unit to
        /// the path destination. Combat phase ownership remains with the combat manager.
        /// </summary>
        public TacticalResult Execute()
        {
            if (Unit == null || MoveCommand == null)
            {
                return TacticalResult.Failure("Cannot execute active movement because the command was not created successfully.");
            }

            if (!Unit.IsAlive)
            {
                return TacticalResult.Failure($"{DescribeUnit(Unit)} cannot active move because it is defeated.");
            }

            if (Unit.CurrentGridPosition != Path.Start)
            {
                return TacticalResult.Failure(
                    $"{DescribeUnit(Unit)} cannot execute active move because it is at {Unit.CurrentGridPosition}, but the path starts at {Path.Start}.");
            }

            var spendResult = Unit.SpendAP(Cost);
            if (spendResult.IsFailure)
            {
                return spendResult;
            }

            Unit.SetGridPosition(Destination);
            return TacticalResult.Success();
        }

        public override string ToString()
        {
            return MoveCommand == null
                ? "Uninitialized active move command"
                : $"Active move {DescribeUnit(Unit)} to {Destination} for {Cost} AP";
        }

        private static TacticalResult ValidateActiveTurn(TacticalUnit unit, CombatState combatState)
        {
            if (unit == null)
            {
                return TacticalResult.Failure("Cannot active move because no active unit was provided.");
            }

            if (combatState == null)
            {
                return TacticalResult.Failure(
                    $"{DescribeUnit(unit)} cannot active move because combat state is missing.");
            }

            if (combatState.Phase != CombatPhase.ActiveTurn)
            {
                return TacticalResult.Failure(
                    $"{DescribeUnit(unit)} cannot active move while combat phase is {combatState.Phase}. "
                    + $"Active movement is only legal during {CombatPhase.ActiveTurn}.");
            }

            if (combatState.ActiveUnit == null)
            {
                return TacticalResult.Failure("Cannot active move because no active unit is selected.");
            }

            if (!ReferenceEquals(combatState.ActiveUnit, unit))
            {
                return TacticalResult.Failure(
                    $"{DescribeUnit(unit)} cannot active move because the active unit is {DescribeUnit(combatState.ActiveUnit)}.");
            }

            if (!unit.IsAlive)
            {
                return TacticalResult.Failure(
                    $"{DescribeUnit(unit)} cannot active move because it is defeated.");
            }

            if (!unit.UnitId.IsAssigned)
            {
                return TacticalResult.Failure(
                    $"{DescribeUnit(unit)} cannot active move because it has no assigned unit ID.");
            }

            return TacticalResult.Success();
        }

        private static TacticalResult<GridPath> BuildActivePath(
            TacticalUnit unit,
            GridPosition destination,
            IGridMap map,
            IGridOccupancy occupancy)
        {
            try
            {
                var search = new ReachableCellSearch();
                var path = search.TryFindPath(
                    map,
                    unit.CurrentGridPosition,
                    destination,
                    unit.CurrentAP,
                    occupancy);

                if (path.IsInvalid)
                {
                    return TacticalResult<GridPath>.Failure(
                        $"{DescribeUnit(unit)} cannot active move to {destination}: {path.FailureReason}");
                }

                return TacticalResult<GridPath>.Success(path);
            }
            catch (ArgumentException exception)
            {
                return TacticalResult<GridPath>.Failure(
                    $"{DescribeUnit(unit)} cannot active move to {destination}: {exception.Message}");
            }
            catch (InvalidOperationException exception)
            {
                return TacticalResult<GridPath>.Failure(
                    $"{DescribeUnit(unit)} cannot active move to {destination}: {exception.Message}");
            }
        }

        private static string DescribeUnit(TacticalUnit unit)
        {
            if (unit == null)
            {
                return "no unit";
            }

            return unit.IsInitialized ? $"{unit.DisplayName} {unit.UnitId}" : unit.DisplayName;
        }
    }
}
