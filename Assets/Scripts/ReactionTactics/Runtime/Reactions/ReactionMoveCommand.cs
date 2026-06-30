using System;
using ReactionTactics.Actions;
using ReactionTactics.Commands;
using ReactionTactics.Core;
using ReactionTactics.Grid;
using ReactionTactics.Pathfinding;
using ReactionTactics.Turns;
using ReactionTactics.Units;

namespace ReactionTactics.Reactions
{
    /// <summary>
    /// Off-turn movement command for the current reactor in an open reaction window.
    /// It builds a regular <see cref="MoveCommand" /> from existing pathfinding,
    /// spends AP from the shared wallet when executed, and updates the reactor's
    /// grid position so the pending action resolves against final positions.
    /// </summary>
    public readonly struct ReactionMoveCommand
    {
        private ReactionMoveCommand(TacticalUnit reactor, ActionIntent sourceIntent, MoveCommand moveCommand)
        {
            Reactor = reactor;
            SourceIntent = sourceIntent;
            MoveCommand = moveCommand;
        }

        public TacticalUnit Reactor { get; }

        public ActionIntent SourceIntent { get; }

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

        public static TacticalResult<ReactionMoveCommand> TryCreate(
            TacticalUnit reactor,
            GridPosition destination,
            IGridMap map,
            IGridOccupancy occupancy,
            CombatState combatState,
            ReactionWindow reactionWindow)
        {
            var timingResult = ValidateReactionTurn(reactor, combatState, reactionWindow, out var sourceIntent);
            if (timingResult.IsFailure)
            {
                return TacticalResult<ReactionMoveCommand>.Failure(timingResult.ErrorMessage);
            }

            if (map == null)
            {
                return TacticalResult<ReactionMoveCommand>.Failure(
                    $"{DescribeUnit(reactor)} cannot reaction move because no grid map is available.");
            }

            var pathResult = BuildReactionPath(reactor, destination, map, occupancy);
            if (pathResult.IsFailure)
            {
                return TacticalResult<ReactionMoveCommand>.Failure(pathResult.ErrorMessage);
            }

            var moveResult = MoveCommand.TryCreate(reactor.UnitId, pathResult.Value, MovementMode.Reaction);
            if (moveResult.IsInvalid)
            {
                return TacticalResult<ReactionMoveCommand>.Failure(moveResult.ErrorMessage);
            }

            return TacticalResult<ReactionMoveCommand>.Success(
                new ReactionMoveCommand(reactor, sourceIntent, moveResult.Command));
        }

        /// <summary>
        /// Applies the movement side effects: spend AP and set the unit's authoritative
        /// grid position. The combat manager remains responsible for completing and
        /// advancing the reaction window after execution succeeds.
        /// </summary>
        public TacticalResult Execute()
        {
            if (Reactor == null || MoveCommand == null)
            {
                return TacticalResult.Failure("Cannot execute reaction movement because the command was not created successfully.");
            }

            if (!Reactor.IsAlive)
            {
                return TacticalResult.Failure($"{DescribeUnit(Reactor)} cannot reaction move because it is defeated.");
            }

            if (Reactor.CurrentGridPosition != Path.Start)
            {
                return TacticalResult.Failure(
                    $"{DescribeUnit(Reactor)} cannot execute reaction move because it is at {Reactor.CurrentGridPosition}, but the path starts at {Path.Start}.");
            }

            var spendResult = Reactor.SpendAP(Cost);
            if (spendResult.IsFailure)
            {
                return spendResult;
            }

            Reactor.SetGridPosition(Destination);
            return TacticalResult.Success();
        }

        public override string ToString()
        {
            if (MoveCommand == null || SourceIntent == null)
            {
                return "Uninitialized reaction move command";
            }

            return $"Reaction move {DescribeUnit(Reactor)} to {Destination} for {Cost} AP responding to '{SourceIntent.Ability.DisplayName}'";
        }

        private static TacticalResult ValidateReactionTurn(
            TacticalUnit reactor,
            CombatState combatState,
            ReactionWindow reactionWindow,
            out ActionIntent sourceIntent)
        {
            sourceIntent = null;

            if (reactor == null)
            {
                return TacticalResult.Failure("Cannot reaction move because no reacting unit was provided.");
            }

            if (combatState == null)
            {
                return TacticalResult.Failure(
                    $"{DescribeUnit(reactor)} cannot reaction move because combat state is missing.");
            }

            if (reactionWindow == null)
            {
                return TacticalResult.Failure(
                    $"{DescribeUnit(reactor)} cannot reaction move because no reaction window is open.");
            }

            sourceIntent = reactionWindow.SourceIntent;
            if (sourceIntent == null)
            {
                return TacticalResult.Failure(
                    $"{DescribeUnit(reactor)} cannot reaction move because the source action intent is missing.");
            }

            if (combatState.Phase != CombatPhase.ReactionWindow)
            {
                return TacticalResult.Failure(
                    $"{DescribeUnit(reactor)} cannot reaction move while combat phase is {combatState.Phase}. "
                    + $"Reaction movement is only legal during {CombatPhase.ReactionWindow}.");
            }

            if (!reactionWindow.IsOpen || !reactionWindow.IsReactorTurnActive)
            {
                return TacticalResult.Failure(
                    $"{DescribeUnit(reactor)} cannot reaction move because no reactor turn is currently active.");
            }

            if (!ReferenceEquals(combatState.PendingActionIntent, sourceIntent))
            {
                return TacticalResult.Failure(
                    $"{DescribeUnit(reactor)} cannot reaction move because the pending action intent does not match the reaction window.");
            }

            if (!ReferenceEquals(combatState.ReactingUnit, reactor)
                || !ReferenceEquals(reactionWindow.CurrentReactor, reactor))
            {
                return TacticalResult.Failure(
                    $"{DescribeUnit(reactor)} cannot reaction move because the current reactor is {DescribeUnit(reactionWindow.CurrentReactor)}.");
            }

            if (!reactor.IsAlive)
            {
                return TacticalResult.Failure(
                    $"{DescribeUnit(reactor)} cannot reaction move because it is defeated.");
            }

            if (!reactor.UnitId.IsAssigned)
            {
                return TacticalResult.Failure(
                    $"{DescribeUnit(reactor)} cannot reaction move because it has no assigned unit ID.");
            }

            return TacticalResult.Success();
        }

        private static TacticalResult<GridPath> BuildReactionPath(
            TacticalUnit reactor,
            GridPosition destination,
            IGridMap map,
            IGridOccupancy occupancy)
        {
            try
            {
                var search = new ReachableCellSearch();
                var path = search.TryFindPath(
                    map,
                    reactor.CurrentGridPosition,
                    destination,
                    reactor.CurrentAP,
                    occupancy);

                if (path.IsInvalid)
                {
                    return TacticalResult<GridPath>.Failure(
                        $"{DescribeUnit(reactor)} cannot reaction move to {destination}: {path.FailureReason}");
                }

                return TacticalResult<GridPath>.Success(path);
            }
            catch (ArgumentException exception)
            {
                return TacticalResult<GridPath>.Failure(
                    $"{DescribeUnit(reactor)} cannot reaction move to {destination}: {exception.Message}");
            }
            catch (InvalidOperationException exception)
            {
                return TacticalResult<GridPath>.Failure(
                    $"{DescribeUnit(reactor)} cannot reaction move to {destination}: {exception.Message}");
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
