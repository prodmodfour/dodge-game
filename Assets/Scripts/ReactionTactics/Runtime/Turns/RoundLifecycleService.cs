using System;
using System.Collections.Generic;
using ReactionTactics.Units;

namespace ReactionTactics.Turns
{
    /// <summary>
    /// Owns deterministic round-start lifecycle rules that do not require a scene
    /// combat manager: advancing the round counter, entering the round-start phase,
    /// and refreshing the shared AP wallet for every living unit exactly once per
    /// round-start call.
    /// </summary>
    public sealed class RoundLifecycleService
    {
        /// <summary>
        /// Starts the next combat round, refreshes every living unit to MaxAP, and
        /// publishes round/AP events through the optional scene-scoped event bus.
        /// Dead, destroyed, and null units are ignored.
        /// </summary>
        public int StartNextRound(
            CombatState state,
            IEnumerable<TacticalUnit> units,
            CombatEventBus eventBus = null)
        {
            if (state == null)
            {
                throw new ArgumentNullException(nameof(state));
            }

            if (units == null)
            {
                throw new ArgumentNullException(nameof(units));
            }

            var nextRound = state.CurrentRound + 1;
            state.SetCurrentRound(nextRound);
            state.SetPhase(CombatPhase.RoundStart);

            eventBus?.PublishRoundStarted(nextRound);
            eventBus?.PublishCombatLog($"Round {nextRound} started. Living units refreshed their shared AP for the round.");

            foreach (var unit in TurnOrderService.BuildTurnOrder(units))
            {
                var previousAP = unit.CurrentAP;
                unit.RefreshAP();
                eventBus?.PublishActionPointsChanged(unit, previousAP, unit.CurrentAP);
            }

            return nextRound;
        }
    }
}
