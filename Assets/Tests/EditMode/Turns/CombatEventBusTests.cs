using System;
using NUnit.Framework;
using ReactionTactics.Turns;
using ReactionTactics.Units;
using UnityEngine;

namespace ReactionTactics.Tests.EditMode.Turns
{
    public sealed class CombatEventBusTests
    {
        [Test]
        public void RoundAndActiveUnitEventsPublishTypedPayloads()
        {
            var busObject = new GameObject("Combat Event Bus Test");
            var previousObject = new GameObject("Previous Active Unit");
            var activeObject = new GameObject("Current Active Unit");

            try
            {
                var bus = busObject.AddComponent<CombatEventBus>();
                var previousUnit = previousObject.AddComponent<TacticalUnit>();
                var activeUnit = activeObject.AddComponent<TacticalUnit>();
                var roundEventRaised = false;
                var activeEventRaised = false;
                var observedRound = default(RoundStartedEvent);
                var observedActiveChange = default(ActiveUnitChangedEvent);

                bus.RoundStarted += eventData =>
                {
                    roundEventRaised = true;
                    observedRound = eventData;
                };
                bus.ActiveUnitChanged += eventData =>
                {
                    activeEventRaised = true;
                    observedActiveChange = eventData;
                };

                bus.PublishRoundStarted(1);
                bus.PublishActiveUnitChanged(previousUnit, activeUnit);

                Assert.That(roundEventRaised, Is.True);
                Assert.That(observedRound.RoundNumber, Is.EqualTo(1));
                Assert.That(activeEventRaised, Is.True);
                Assert.That(observedActiveChange.PreviousUnit, Is.SameAs(previousUnit));
                Assert.That(observedActiveChange.ActiveUnit, Is.SameAs(activeUnit));
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(activeObject);
                UnityEngine.Object.DestroyImmediate(previousObject);
                UnityEngine.Object.DestroyImmediate(busObject);
            }
        }

        [Test]
        public void ResourceAndDeathEventsPublishMinimalUnitPayloads()
        {
            var busObject = new GameObject("Combat Resource Event Bus Test");
            var unitObject = new GameObject("Resource Event Unit");

            try
            {
                var bus = busObject.AddComponent<CombatEventBus>();
                var unit = unitObject.AddComponent<TacticalUnit>();
                var damageSource = DamageSource.Environmental("event test damage");
                var observedAP = default(ActionPointsChangedEvent);
                var observedHP = default(HitPointsChangedEvent);
                var observedDeath = default(UnitDiedEvent);
                var apEventRaised = false;
                var hpEventRaised = false;
                var deathEventRaised = false;

                bus.ActionPointsChanged += eventData =>
                {
                    apEventRaised = true;
                    observedAP = eventData;
                };
                bus.HitPointsChanged += eventData =>
                {
                    hpEventRaised = true;
                    observedHP = eventData;
                };
                bus.UnitDied += eventData =>
                {
                    deathEventRaised = true;
                    observedDeath = eventData;
                };

                bus.PublishActionPointsChanged(unit, 6, 2);
                bus.PublishHitPointsChanged(unit, 10, 4, damageSource);
                bus.PublishUnitDied(unit, damageSource);

                Assert.That(apEventRaised, Is.True);
                Assert.That(observedAP.Unit, Is.SameAs(unit));
                Assert.That(observedAP.PreviousAP, Is.EqualTo(6));
                Assert.That(observedAP.CurrentAP, Is.EqualTo(2));
                Assert.That(observedAP.Delta, Is.EqualTo(-4));
                Assert.That(hpEventRaised, Is.True);
                Assert.That(observedHP.Unit, Is.SameAs(unit));
                Assert.That(observedHP.PreviousHP, Is.EqualTo(10));
                Assert.That(observedHP.CurrentHP, Is.EqualTo(4));
                Assert.That(observedHP.Delta, Is.EqualTo(-6));
                Assert.That(observedHP.Source, Is.EqualTo(damageSource));
                Assert.That(deathEventRaised, Is.True);
                Assert.That(observedDeath.Unit, Is.SameAs(unit));
                Assert.That(observedDeath.Source, Is.EqualTo(damageSource));
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(unitObject);
                UnityEngine.Object.DestroyImmediate(busObject);
            }
        }

        [Test]
        public void ActionReactionResolutionAndCombatEndEventsAreSceneScoped()
        {
            var firstBusObject = new GameObject("First Combat Event Bus Test");
            var secondBusObject = new GameObject("Second Combat Event Bus Test");
            var actorObject = new GameObject("Action Event Actor");
            var reactorObject = new GameObject("Reaction Event Reactor");

            try
            {
                var firstBus = firstBusObject.AddComponent<CombatEventBus>();
                var secondBus = secondBusObject.AddComponent<CombatEventBus>();
                var actor = actorObject.AddComponent<TacticalUnit>();
                var reactor = reactorObject.AddComponent<TacticalUnit>();
                var actionIntent = new object();
                var firstBusDeclaredEvents = 0;
                var declaredEventRaised = false;
                var reactionEventRaised = false;
                var resolvedEventRaised = false;
                var endedEventRaised = false;
                var observedDeclaration = default(ActionDeclaredEvent);
                var observedReaction = default(ReactionTurnStartedEvent);
                var observedResolution = default(ActionResolvedEvent);
                var observedEnd = default(CombatEndedEvent);

                firstBus.ActionDeclared += _ => firstBusDeclaredEvents += 1;

                secondBus.PublishActionDeclared(actor, actionIntent);

                secondBus.ActionDeclared += eventData =>
                {
                    declaredEventRaised = true;
                    observedDeclaration = eventData;
                };
                secondBus.ReactionTurnStarted += eventData =>
                {
                    reactionEventRaised = true;
                    observedReaction = eventData;
                };
                secondBus.ActionResolved += eventData =>
                {
                    resolvedEventRaised = true;
                    observedResolution = eventData;
                };
                secondBus.CombatEnded += eventData =>
                {
                    endedEventRaised = true;
                    observedEnd = eventData;
                };

                secondBus.PublishActionDeclared(actor, actionIntent);
                secondBus.PublishReactionTurnStarted(reactor, actionIntent);
                secondBus.PublishActionResolved(actor, actionIntent);
                secondBus.PublishCombatEnded(TeamId.Player);

                Assert.That(firstBusDeclaredEvents, Is.EqualTo(0));
                Assert.That(declaredEventRaised, Is.True);
                Assert.That(observedDeclaration.Actor, Is.SameAs(actor));
                Assert.That(observedDeclaration.ActionIntent, Is.SameAs(actionIntent));
                Assert.That(reactionEventRaised, Is.True);
                Assert.That(observedReaction.Reactor, Is.SameAs(reactor));
                Assert.That(observedReaction.SourceActionIntent, Is.SameAs(actionIntent));
                Assert.That(resolvedEventRaised, Is.True);
                Assert.That(observedResolution.Actor, Is.SameAs(actor));
                Assert.That(observedResolution.ActionIntent, Is.SameAs(actionIntent));
                Assert.That(endedEventRaised, Is.True);
                Assert.That(observedEnd.HasWinningTeam, Is.True);
                Assert.That(observedEnd.WinningTeam, Is.EqualTo(TeamId.Player));
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(reactorObject);
                UnityEngine.Object.DestroyImmediate(actorObject);
                UnityEngine.Object.DestroyImmediate(secondBusObject);
                UnityEngine.Object.DestroyImmediate(firstBusObject);
            }
        }

        [Test]
        public void PublishMethodsRejectInvalidPayloads()
        {
            var busObject = new GameObject("Combat Event Validation Bus Test");
            var unitObject = new GameObject("Combat Event Validation Unit");

            try
            {
                var bus = busObject.AddComponent<CombatEventBus>();
                var unit = unitObject.AddComponent<TacticalUnit>();

                Assert.Throws<ArgumentOutOfRangeException>(() => bus.PublishRoundStarted(0));
                Assert.Throws<ArgumentException>(() => bus.PublishActiveUnitChanged(null, null));
                Assert.Throws<ArgumentNullException>(() => bus.PublishActionPointsChanged(null, 1, 0));
                Assert.Throws<ArgumentOutOfRangeException>(() => bus.PublishHitPointsChanged(unit, -1, 0, DamageSource.Unspecified));
                Assert.Throws<ArgumentNullException>(() => bus.PublishActionDeclared(unit, null));
                Assert.Throws<ArgumentNullException>(() => bus.PublishReactionTurnStarted(unit, null));
                Assert.Throws<ArgumentNullException>(() => bus.PublishActionResolved(unit, null));
                Assert.Throws<ArgumentNullException>(() => bus.PublishUnitDied(null, DamageSource.Unspecified));
                Assert.Throws<ArgumentOutOfRangeException>(() => bus.PublishCombatEnded((TeamId)99));
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(unitObject);
                UnityEngine.Object.DestroyImmediate(busObject);
            }
        }
    }
}
