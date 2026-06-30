using NUnit.Framework;
using ReactionTactics.Actions;
using ReactionTactics.Grid;
using ReactionTactics.Turns;
using ReactionTactics.Units;
using UnityEngine;

namespace ReactionTactics.Tests.EditMode.Actions
{
    public sealed class ActionResolverTests
    {
        [Test]
        public void ResolvePublishesActionResolvedEventWithoutApplyingDamage()
        {
            var busObject = new GameObject("Action Resolver Event Bus");
            var actorObject = new GameObject("Action Resolver Actor");
            var targetObject = new GameObject("Action Resolver Target");
            var actorStats = CreateStats("Actor");
            var targetStats = CreateStats("Target");
            var ability = CreateAbility();

            try
            {
                var bus = busObject.AddComponent<CombatEventBus>();
                var actor = actorObject.AddComponent<TacticalUnit>();
                var target = targetObject.AddComponent<TacticalUnit>();
                actor.Initialize(new UnitId(101), TeamId.Player, actorStats, GridPosition.Zero);
                target.Initialize(new UnitId(202), TeamId.Enemy, targetStats, new GridPosition(1, 0, 0));

                var intent = new ActionIntent(
                    actor,
                    ability,
                    actor.CurrentGridPosition,
                    ActionTarget.ForUnit(target),
                    new[] { target.CurrentGridPosition },
                    declarationRound: 1,
                    declarationSequence: 0);

                var resolvedEventRaised = false;
                var observedEvent = default(ActionResolvedEvent);
                bus.ActionResolved += eventData =>
                {
                    resolvedEventRaised = true;
                    observedEvent = eventData;
                };

                var resolver = new ActionResolver(bus, logContext: busObject, logResolutions: false);
                var result = resolver.Resolve(intent);

                Assert.That(result.IsSuccess, Is.True, result.ErrorMessage);
                Assert.That(resolvedEventRaised, Is.True);
                Assert.That(observedEvent.Actor, Is.SameAs(actor));
                Assert.That(observedEvent.ActionIntent, Is.SameAs(intent));
                Assert.That(target.CurrentHP, Is.EqualTo(target.MaxHP));
                Assert.That(target.IsAlive, Is.True);
            }
            finally
            {
                Destroy(ability);
                Destroy(targetStats);
                Destroy(actorStats);
                Object.DestroyImmediate(targetObject);
                Object.DestroyImmediate(actorObject);
                Object.DestroyImmediate(busObject);
            }
        }

        [Test]
        public void ResolveRejectsMissingIntent()
        {
            var resolver = new ActionResolver(logResolutions: false);

            var result = resolver.Resolve(null);

            Assert.That(result.IsFailure, Is.True);
            Assert.That(result.ErrorMessage, Does.Contain("no action intent"));
        }

        private static UnitStatsDefinition CreateStats(string displayName)
        {
            var stats = ScriptableObject.CreateInstance<UnitStatsDefinition>();
            stats.Configure(
                displayName: displayName,
                maxHP: 10,
                maxAP: 6,
                movementAnimationSpeed: 4f,
                meleeRange: 1,
                teamColorHint: Color.white);
            return stats;
        }

        private static AbilityDefinition CreateAbility()
        {
            var ability = ScriptableObject.CreateInstance<AbilityDefinition>();
            ability.Configure(
                abilityKey: "melee_slash",
                displayName: "Melee Slash",
                apCost: 3,
                usage: AbilityUsage.Action,
                timing: AbilityTiming.Telegraphed,
                shape: AbilityShape.Melee,
                range: 0,
                radius: 0,
                damage: 4,
                triggersReactions: true,
                description: "Shell resolver test ability.");
            return ability;
        }

        private static void Destroy(ScriptableObject asset)
        {
            if (asset != null)
            {
                Object.DestroyImmediate(asset);
            }
        }
    }
}
