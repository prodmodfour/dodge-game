using System;
using System.Linq;
using NUnit.Framework;
using ReactionTactics.Actions;
using ReactionTactics.Grid;
using ReactionTactics.Turns;
using ReactionTactics.Units;
using UnityEngine;

public sealed class DamageEventTests
{
    [Test]
    public void DamageEventStoresDeterministicDamageContext()
    {
        using (var battle = new TestBattle())
        {
            var ability = CreateMeleeAbility(damage: 4);

            try
            {
                var intent = CreateMeleeIntent(battle, ability);

                var eventData = new DamageEvent(
                    intent,
                    battle.Actor,
                    battle.Enemy,
                    amount: 4,
                    wasBraced: true,
                    finalAmount: 2);

                Assert.That(eventData.SourceIntent, Is.SameAs(intent));
                Assert.That(eventData.Attacker, Is.SameAs(battle.Actor));
                Assert.That(eventData.Target, Is.SameAs(battle.Enemy));
                Assert.That(eventData.Amount, Is.EqualTo(4));
                Assert.That(eventData.WasBraced, Is.True);
                Assert.That(eventData.FinalAmount, Is.EqualTo(2));
            }
            finally
            {
                Destroy(ability);
            }
        }
    }

    [Test]
    public void CombatEventBusPublishesDamageAppliedPayload()
    {
        using (var battle = new TestBattle())
        {
            var ability = CreateMeleeAbility(damage: 4);

            try
            {
                var intent = CreateMeleeIntent(battle, ability);
                var eventRaised = false;
                var observedEvent = default(DamageEvent);

                battle.EventBus.DamageApplied += eventData =>
                {
                    eventRaised = true;
                    observedEvent = eventData;
                };

                battle.EventBus.PublishDamageApplied(
                    intent,
                    battle.Actor,
                    battle.Enemy,
                    amount: 4,
                    wasBraced: false,
                    finalAmount: 4);

                Assert.That(eventRaised, Is.True);
                Assert.That(observedEvent.SourceIntent, Is.SameAs(intent));
                Assert.That(observedEvent.Attacker, Is.SameAs(battle.Actor));
                Assert.That(observedEvent.Target, Is.SameAs(battle.Enemy));
                Assert.That(observedEvent.Amount, Is.EqualTo(4));
                Assert.That(observedEvent.WasBraced, Is.False);
                Assert.That(observedEvent.FinalAmount, Is.EqualTo(4));
            }
            finally
            {
                Destroy(ability);
            }
        }
    }

    [Test]
    public void ActionResolverPublishesDamageEventWhenMeleeAppliesDamage()
    {
        using (var battle = new TestBattle())
        {
            var ability = CreateMeleeAbility(damage: 4);

            try
            {
                var intent = CreateMeleeIntent(battle, ability);
                var damageEvents = 0;
                var observedEvent = default(DamageEvent);
                battle.EventBus.DamageApplied += eventData =>
                {
                    damageEvents += 1;
                    observedEvent = eventData;
                };

                var resolver = new ActionResolver(battle.EventBus, battle.BusObject, logResolutions: false);
                var result = resolver.Resolve(intent);

                Assert.That(result.IsSuccess, Is.True, result.ErrorMessage);
                Assert.That(damageEvents, Is.EqualTo(1));
                Assert.That(observedEvent.SourceIntent, Is.SameAs(intent));
                Assert.That(observedEvent.Attacker, Is.SameAs(battle.Actor));
                Assert.That(observedEvent.Target, Is.SameAs(battle.Enemy));
                Assert.That(observedEvent.Amount, Is.EqualTo(4));
                Assert.That(observedEvent.WasBraced, Is.False);
                Assert.That(observedEvent.FinalAmount, Is.EqualTo(4));
                Assert.That(battle.Enemy.CurrentHP, Is.EqualTo(6));
            }
            finally
            {
                Destroy(ability);
            }
        }
    }

    [Test]
    public void ActionResolverDoesNotPublishDamageEventWhenMeleeIsAvoidedByMovement()
    {
        using (var battle = new TestBattle())
        {
            var ability = CreateMeleeAbility(damage: 4);

            try
            {
                var intent = CreateMeleeIntent(battle, ability);
                battle.Enemy.SetGridPosition(new GridPosition(3, 0, 0));
                var damageEvents = 0;
                battle.EventBus.DamageApplied += _ => damageEvents += 1;

                var resolver = new ActionResolver(battle.EventBus, battle.BusObject, logResolutions: false);
                var result = resolver.Resolve(intent);

                Assert.That(result.IsSuccess, Is.True, result.ErrorMessage);
                Assert.That(damageEvents, Is.EqualTo(0));
                Assert.That(battle.Enemy.CurrentHP, Is.EqualTo(battle.Enemy.MaxHP));
            }
            finally
            {
                Destroy(ability);
            }
        }
    }

    [Test]
    public void DamageEventPayloadContainsNoRandomHitOrDodgeFields()
    {
        var memberNames = typeof(DamageEvent)
            .GetProperties()
            .Select(property => property.Name)
            .Concat(typeof(DamageEvent).GetFields().Select(field => field.Name));

        foreach (var memberName in memberNames)
        {
            Assert.That(memberName, Does.Not.Contain("HitChance"));
            Assert.That(memberName, Does.Not.Contain("Accuracy"));
            Assert.That(memberName, Does.Not.Contain("Dodge"));
            Assert.That(memberName, Does.Not.Contain("Roll"));
        }
    }

    private static ActionIntent CreateMeleeIntent(TestBattle battle, AbilityDefinition ability)
    {
        return new ActionIntent(
            battle.Actor,
            ability,
            battle.Actor.CurrentGridPosition,
            ActionTarget.ForUnit(battle.Enemy),
            new[] { battle.Enemy.CurrentGridPosition },
            declarationRound: 1,
            declarationSequence: 0);
    }

    private static AbilityDefinition CreateMeleeAbility(int damage)
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
            damage: damage,
            triggersReactions: true,
            description: "Damage event test ability.");
        return ability;
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

    private static void Destroy(UnityEngine.Object asset)
    {
        if (asset != null)
        {
            UnityEngine.Object.DestroyImmediate(asset);
        }
    }

    private sealed class TestBattle : IDisposable
    {
        private readonly GameObject actorObject;
        private readonly GameObject enemyObject;
        private readonly UnitStatsDefinition actorStats;
        private readonly UnitStatsDefinition enemyStats;

        public TestBattle()
        {
            BusObject = new GameObject("Damage Event Bus");
            EventBus = BusObject.AddComponent<CombatEventBus>();
            actorObject = new GameObject("Damage Event Actor");
            enemyObject = new GameObject("Damage Event Enemy");
            actorStats = CreateStats("Actor");
            enemyStats = CreateStats("Enemy");

            Actor = actorObject.AddComponent<TacticalUnit>();
            Enemy = enemyObject.AddComponent<TacticalUnit>();
            Actor.Initialize(new UnitId(1), TeamId.Player, actorStats, GridPosition.Zero);
            Enemy.Initialize(new UnitId(2), TeamId.Enemy, enemyStats, GridPosition.East);
        }

        public GameObject BusObject { get; }

        public CombatEventBus EventBus { get; }

        public TacticalUnit Actor { get; }

        public TacticalUnit Enemy { get; }

        public void Dispose()
        {
            Destroy(enemyObject);
            Destroy(actorObject);
            Destroy(BusObject);
            Destroy(enemyStats);
            Destroy(actorStats);
        }
    }
}
