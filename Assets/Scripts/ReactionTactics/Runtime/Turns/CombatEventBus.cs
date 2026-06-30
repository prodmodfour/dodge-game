using System;
using ReactionTactics.Actions;
using ReactionTactics.Units;
using UnityEngine;

namespace ReactionTactics.Turns
{
    /// <summary>
    /// Scene-scoped event hub for combat systems, UI, and logs.
    /// It intentionally uses instance events instead of global static state so each scene
    /// can own one bus and tests can create isolated buses.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class CombatEventBus : MonoBehaviour
    {
        public event Action<RoundStartedEvent> RoundStarted;

        public event Action<ActiveUnitChangedEvent> ActiveUnitChanged;

        public event Action<ActionPointsChangedEvent> ActionPointsChanged;

        public event Action<HitPointsChangedEvent> HitPointsChanged;

        public event Action<DamageEvent> DamageApplied;

        public event Action<ActionDeclaredEvent> ActionDeclared;

        public event Action<ReactionTurnStartedEvent> ReactionTurnStarted;

        public event Action<ActionResolvedEvent> ActionResolved;

        public event Action<UnitDiedEvent> UnitDied;

        public event Action<CombatEndedEvent> CombatEnded;

        public event Action<CombatLogEntry> CombatLogMessageAdded;

        private int combatLogSequence;

        public void PublishRoundStarted(int roundNumber)
        {
            var eventData = new RoundStartedEvent(roundNumber);
            RoundStarted?.Invoke(eventData);
        }

        public void PublishActiveUnitChanged(TacticalUnit previousUnit, TacticalUnit activeUnit)
        {
            var eventData = new ActiveUnitChangedEvent(previousUnit, activeUnit);
            ActiveUnitChanged?.Invoke(eventData);
        }

        public void PublishActionPointsChanged(TacticalUnit unit, int previousAP, int currentAP)
        {
            var eventData = new ActionPointsChangedEvent(unit, previousAP, currentAP);
            ActionPointsChanged?.Invoke(eventData);
        }

        public void PublishHitPointsChanged(TacticalUnit unit, int previousHP, int currentHP, DamageSource source)
        {
            var eventData = new HitPointsChangedEvent(unit, previousHP, currentHP, source);
            HitPointsChanged?.Invoke(eventData);
        }

        public void PublishDamageApplied(
            ActionIntent sourceIntent,
            TacticalUnit attacker,
            TacticalUnit target,
            int amount,
            bool wasBraced,
            int finalAmount)
        {
            var eventData = new DamageEvent(sourceIntent, attacker, target, amount, wasBraced, finalAmount);
            DamageApplied?.Invoke(eventData);
        }

        public void PublishActionDeclared(TacticalUnit actor, object actionIntent)
        {
            var eventData = new ActionDeclaredEvent(actor, actionIntent);
            ActionDeclared?.Invoke(eventData);
        }

        public void PublishReactionTurnStarted(TacticalUnit reactor, object sourceActionIntent)
        {
            var eventData = new ReactionTurnStartedEvent(reactor, sourceActionIntent);
            ReactionTurnStarted?.Invoke(eventData);
        }

        public void PublishActionResolved(TacticalUnit actor, object actionIntent)
        {
            var eventData = new ActionResolvedEvent(actor, actionIntent);
            ActionResolved?.Invoke(eventData);
        }

        public void PublishUnitDied(TacticalUnit unit, DamageSource source)
        {
            var eventData = new UnitDiedEvent(unit, source);
            UnitDied?.Invoke(eventData);
        }

        public void PublishCombatEnded(TeamId winningTeam)
        {
            var eventData = CombatEndedEvent.WithWinner(winningTeam);
            CombatEnded?.Invoke(eventData);
        }

        public void PublishCombatEndedWithoutWinner()
        {
            var eventData = CombatEndedEvent.WithoutWinner();
            CombatEnded?.Invoke(eventData);
        }

        public void PublishCombatLog(string message)
        {
            var entry = new CombatLogEntry(combatLogSequence + 1, message, Time.time);
            combatLogSequence = entry.SequenceNumber;
            CombatLogMessageAdded?.Invoke(entry);
        }
    }
}
