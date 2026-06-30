using ReactionTactics.Actions;
using ReactionTactics.Core;
using ReactionTactics.Turns;
using ReactionTactics.Units;

namespace ReactionTactics.Reactions
{
    /// <summary>
    /// Explicit zero-cost command for a unit choosing to do nothing during its
    /// current reaction turn. The command is valid only for the current reactor
    /// in the open reaction window and never spends AP.
    /// </summary>
    public readonly struct PassReactionCommand
    {
        public const int ApCost = 0;

        private PassReactionCommand(TacticalUnit reactor, ActionIntent sourceIntent)
        {
            Reactor = reactor;
            SourceIntent = sourceIntent;
        }

        public TacticalUnit Reactor { get; }

        public ActionIntent SourceIntent { get; }

        public int Cost
        {
            get { return ApCost; }
        }

        public static TacticalResult<PassReactionCommand> TryCreate(
            TacticalUnit reactor,
            ActionIntent sourceIntent,
            CombatState combatState,
            ReactionWindow reactionWindow)
        {
            if (reactor == null)
            {
                return TacticalResult<PassReactionCommand>.Failure(
                    "Cannot pass reaction because no reacting unit was provided.");
            }

            if (sourceIntent == null)
            {
                return TacticalResult<PassReactionCommand>.Failure(
                    $"{DescribeUnit(reactor)} cannot pass reaction because the source action intent is missing.");
            }

            if (combatState == null)
            {
                return TacticalResult<PassReactionCommand>.Failure(
                    $"{DescribeUnit(reactor)} cannot pass reaction because combat state is missing.");
            }

            if (reactionWindow == null)
            {
                return TacticalResult<PassReactionCommand>.Failure(
                    $"{DescribeUnit(reactor)} cannot pass reaction because no reaction window is open.");
            }

            if (combatState.Phase != CombatPhase.ReactionWindow)
            {
                return TacticalResult<PassReactionCommand>.Failure(
                    $"{DescribeUnit(reactor)} cannot pass reaction while combat phase is {combatState.Phase}. "
                    + $"Pass reactions are only legal during {CombatPhase.ReactionWindow}.");
            }

            if (!reactionWindow.IsOpen || !reactionWindow.IsReactorTurnActive)
            {
                return TacticalResult<PassReactionCommand>.Failure(
                    $"{DescribeUnit(reactor)} cannot pass reaction because no reactor turn is currently active.");
            }

            if (!ReferenceEquals(reactionWindow.SourceIntent, sourceIntent))
            {
                return TacticalResult<PassReactionCommand>.Failure(
                    $"{DescribeUnit(reactor)} cannot pass reaction for a different source action intent.");
            }

            if (!ReferenceEquals(combatState.PendingActionIntent, sourceIntent))
            {
                return TacticalResult<PassReactionCommand>.Failure(
                    $"{DescribeUnit(reactor)} cannot pass reaction because the pending action intent does not match the reaction window.");
            }

            if (!ReferenceEquals(combatState.ReactingUnit, reactor)
                || !ReferenceEquals(reactionWindow.CurrentReactor, reactor))
            {
                return TacticalResult<PassReactionCommand>.Failure(
                    $"{DescribeUnit(reactor)} cannot pass reaction because the current reactor is {DescribeUnit(reactionWindow.CurrentReactor)}.");
            }

            if (!reactor.IsAlive)
            {
                return TacticalResult<PassReactionCommand>.Failure(
                    $"{DescribeUnit(reactor)} cannot pass reaction because it is defeated.");
            }

            return TacticalResult<PassReactionCommand>.Success(new PassReactionCommand(reactor, sourceIntent));
        }

        public override string ToString()
        {
            return $"Pass reaction {DescribeUnit(Reactor)} for '{SourceIntent.Ability.DisplayName}' at {ApCost} AP";
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
