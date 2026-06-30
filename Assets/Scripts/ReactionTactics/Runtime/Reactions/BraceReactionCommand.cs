using ReactionTactics.Actions;
using ReactionTactics.Core;
using ReactionTactics.Turns;
using ReactionTactics.Units;

namespace ReactionTactics.Reactions
{
    /// <summary>
    /// Defensive off-turn command for the current reactor in an open reaction window.
    /// Brace spends AP, prepares a one-shot fixed damage reduction, completes the
    /// reactor's turn, and does not introduce any random dodge or accuracy logic.
    /// </summary>
    public readonly struct BraceReactionCommand
    {
        public const int DefaultApCost = 2;
        public const int DefaultDamageReduction = 2;

        private BraceReactionCommand(
            TacticalUnit reactor,
            ActionIntent sourceIntent,
            int cost,
            int damageReduction)
        {
            Reactor = reactor;
            SourceIntent = sourceIntent;
            Cost = cost;
            DamageReduction = damageReduction;
        }

        public TacticalUnit Reactor { get; }

        public ActionIntent SourceIntent { get; }

        public int Cost { get; }

        public int DamageReduction { get; }

        public static TacticalResult<BraceReactionCommand> TryCreate(
            TacticalUnit reactor,
            ActionIntent sourceIntent,
            CombatState combatState,
            ReactionWindow reactionWindow)
        {
            return TryCreate(
                reactor,
                sourceIntent,
                combatState,
                reactionWindow,
                DefaultApCost,
                DefaultDamageReduction);
        }

        public static TacticalResult<BraceReactionCommand> TryCreate(
            TacticalUnit reactor,
            ActionIntent sourceIntent,
            CombatState combatState,
            ReactionWindow reactionWindow,
            int cost,
            int damageReduction)
        {
            if (cost < 0)
            {
                return TacticalResult<BraceReactionCommand>.Failure("Brace AP cost cannot be negative.");
            }

            if (damageReduction <= 0)
            {
                return TacticalResult<BraceReactionCommand>.Failure("Brace damage reduction must be greater than zero.");
            }

            var timingResult = ValidateReactionTurn(reactor, sourceIntent, combatState, reactionWindow);
            if (timingResult.IsFailure)
            {
                return TacticalResult<BraceReactionCommand>.Failure(timingResult.ErrorMessage);
            }

            if (reactor.BracedUntilNextHit)
            {
                return TacticalResult<BraceReactionCommand>.Failure(
                    $"{DescribeUnit(reactor)} cannot brace because it is already braced until the next hit.");
            }

            if (!reactor.CanSpendAP(cost))
            {
                return TacticalResult<BraceReactionCommand>.Failure(
                    $"{DescribeUnit(reactor)} needs {cost} AP to brace but only has {reactor.CurrentAP} AP.");
            }

            return TacticalResult<BraceReactionCommand>.Success(
                new BraceReactionCommand(reactor, sourceIntent, cost, damageReduction));
        }

        /// <summary>
        /// Spends AP and sets the reactor's one-shot defense state. The combat manager
        /// remains responsible for completing and advancing the reaction window.
        /// </summary>
        public TacticalResult Execute()
        {
            if (Reactor == null || SourceIntent == null)
            {
                return TacticalResult.Failure("Cannot execute brace because the command was not created successfully.");
            }

            if (!Reactor.IsAlive)
            {
                return TacticalResult.Failure($"{DescribeUnit(Reactor)} cannot brace because it is defeated.");
            }

            if (Reactor.BracedUntilNextHit)
            {
                return TacticalResult.Failure($"{DescribeUnit(Reactor)} cannot brace because it is already braced until the next hit.");
            }

            if (!Reactor.CanSpendAP(Cost))
            {
                return TacticalResult.Failure(
                    $"{DescribeUnit(Reactor)} needs {Cost} AP to brace but only has {Reactor.CurrentAP} AP.");
            }

            var braceResult = Reactor.BraceUntilNextHit(DamageReduction);
            if (braceResult.IsFailure)
            {
                return braceResult;
            }

            var spendResult = Reactor.SpendAP(Cost);
            if (spendResult.IsFailure)
            {
                Reactor.ClearBrace();
                return spendResult;
            }

            return TacticalResult.Success();
        }

        public override string ToString()
        {
            if (Reactor == null || SourceIntent == null)
            {
                return "Uninitialized brace reaction command";
            }

            return $"Brace reaction {DescribeUnit(Reactor)} for {Cost} AP; next incoming damage reduced by {DamageReduction} while responding to '{SourceIntent.Ability.DisplayName}'";
        }

        private static TacticalResult ValidateReactionTurn(
            TacticalUnit reactor,
            ActionIntent sourceIntent,
            CombatState combatState,
            ReactionWindow reactionWindow)
        {
            if (reactor == null)
            {
                return TacticalResult.Failure("Cannot brace because no reacting unit was provided.");
            }

            if (sourceIntent == null)
            {
                return TacticalResult.Failure(
                    $"{DescribeUnit(reactor)} cannot brace because the source action intent is missing.");
            }

            if (combatState == null)
            {
                return TacticalResult.Failure(
                    $"{DescribeUnit(reactor)} cannot brace because combat state is missing.");
            }

            if (reactionWindow == null)
            {
                return TacticalResult.Failure(
                    $"{DescribeUnit(reactor)} cannot brace because no reaction window is open.");
            }

            if (combatState.Phase != CombatPhase.ReactionWindow)
            {
                return TacticalResult.Failure(
                    $"{DescribeUnit(reactor)} cannot brace while combat phase is {combatState.Phase}. "
                    + $"Brace is only legal during {CombatPhase.ReactionWindow}.");
            }

            if (!reactionWindow.IsOpen || !reactionWindow.IsReactorTurnActive)
            {
                return TacticalResult.Failure(
                    $"{DescribeUnit(reactor)} cannot brace because no reactor turn is currently active.");
            }

            if (!ReferenceEquals(reactionWindow.SourceIntent, sourceIntent))
            {
                return TacticalResult.Failure(
                    $"{DescribeUnit(reactor)} cannot brace for a different source action intent.");
            }

            if (!ReferenceEquals(combatState.PendingActionIntent, sourceIntent))
            {
                return TacticalResult.Failure(
                    $"{DescribeUnit(reactor)} cannot brace because the pending action intent does not match the reaction window.");
            }

            if (!ReferenceEquals(combatState.ReactingUnit, reactor)
                || !ReferenceEquals(reactionWindow.CurrentReactor, reactor))
            {
                return TacticalResult.Failure(
                    $"{DescribeUnit(reactor)} cannot brace because the current reactor is {DescribeUnit(reactionWindow.CurrentReactor)}.");
            }

            if (!reactor.IsAlive)
            {
                return TacticalResult.Failure(
                    $"{DescribeUnit(reactor)} cannot brace because it is defeated.");
            }

            return TacticalResult.Success();
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
