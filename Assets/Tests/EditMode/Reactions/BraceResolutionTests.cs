using System;
using System.Collections.Generic;
using NUnit.Framework;
using ReactionTactics.Actions;
using ReactionTactics.Grid;
using ReactionTactics.Reactions;
using ReactionTactics.Turns;
using ReactionTactics.Units;
using UnityEngine;

public sealed class BraceResolutionTests
{
    [Test]
    public void MeleeResolutionAppliesBraceReductionAndPublishesBracedDamageEvent()
    {
        using (var battle = new ResolverBattle())
        {
            var actor = battle.CreateUnit("Brace Melee Actor", TeamId.Player, GridPosition.Zero);
            var target = battle.CreateUnit("Brace Melee Target", TeamId.Enemy, GridPosition.East);
            var ability = CreateMeleeSlash(damage: 4);

            try
            {
                var intent = CreateMeleeIntent(actor, target, ability);
                Assert.That(target.BraceUntilNextHit(BraceReactionCommand.DefaultDamageReduction).IsSuccess, Is.True);
                var damageEvents = CaptureDamageEvents(battle.EventBus);

                var result = battle.CreateResolver(logResolutions: false).Resolve(intent);

                Assert.That(result.IsSuccess, Is.True, result.ErrorMessage);
                Assert.That(target.CurrentHP, Is.EqualTo(target.MaxHP - 2));
                Assert.That(target.BracedUntilNextHit, Is.False);
                Assert.That(damageEvents, Has.Count.EqualTo(1));
                Assert.That(damageEvents[0].SourceIntent, Is.SameAs(intent));
                Assert.That(damageEvents[0].Target, Is.SameAs(target));
                Assert.That(damageEvents[0].Amount, Is.EqualTo(4));
                Assert.That(damageEvents[0].WasBraced, Is.True);
                Assert.That(damageEvents[0].FinalAmount, Is.EqualTo(2));
            }
            finally
            {
                Destroy(ability);
            }
        }
    }

    [Test]
    public void ConeResolutionAppliesBraceReductionToBracedHostileInDeclaredCone()
    {
        using (var battle = new ResolverBattle())
        {
            var actor = battle.CreateUnit("Brace Cone Actor", TeamId.Player, GridPosition.Zero);
            var target = battle.CreateUnit("Brace Cone Target", TeamId.Enemy, new GridPosition(0, 0, 2));
            var ability = CreateConeShot(damage: 3);

            try
            {
                var intent = CreateConeIntent(actor, ability);
                Assert.That(target.BraceUntilNextHit(BraceReactionCommand.DefaultDamageReduction).IsSuccess, Is.True);
                var damageEvents = CaptureDamageEvents(battle.EventBus);

                var result = battle.CreateResolver(logResolutions: false).Resolve(intent);

                Assert.That(result.IsSuccess, Is.True, result.ErrorMessage);
                Assert.That(target.CurrentHP, Is.EqualTo(target.MaxHP - 1));
                Assert.That(target.BracedUntilNextHit, Is.False);
                Assert.That(damageEvents, Has.Count.EqualTo(1));
                Assert.That(damageEvents[0].SourceIntent, Is.SameAs(intent));
                Assert.That(damageEvents[0].Target, Is.SameAs(target));
                Assert.That(damageEvents[0].Amount, Is.EqualTo(3));
                Assert.That(damageEvents[0].WasBraced, Is.True);
                Assert.That(damageEvents[0].FinalAmount, Is.EqualTo(1));
            }
            finally
            {
                Destroy(ability);
            }
        }
    }

    [Test]
    public void AoeResolutionAppliesBraceReductionToBracedUnitInDeclaredArea()
    {
        using (var battle = new ResolverBattle())
        {
            var actor = battle.CreateUnit("Brace AoE Actor", TeamId.Player, GridPosition.Zero);
            var target = battle.CreateUnit("Brace AoE Target", TeamId.Enemy, RadiusTargetCell);
            var ability = CreateFireball(damage: 4);

            try
            {
                var intent = CreateRadiusIntent(actor, ability);
                Assert.That(target.BraceUntilNextHit(BraceReactionCommand.DefaultDamageReduction).IsSuccess, Is.True);
                var damageEvents = CaptureDamageEvents(battle.EventBus);

                var result = battle.CreateResolver(logResolutions: false).Resolve(intent);

                Assert.That(result.IsSuccess, Is.True, result.ErrorMessage);
                Assert.That(target.CurrentHP, Is.EqualTo(target.MaxHP - 2));
                Assert.That(target.BracedUntilNextHit, Is.False);
                Assert.That(damageEvents, Has.Count.EqualTo(1));
                Assert.That(damageEvents[0].SourceIntent, Is.SameAs(intent));
                Assert.That(damageEvents[0].Target, Is.SameAs(target));
                Assert.That(damageEvents[0].Amount, Is.EqualTo(4));
                Assert.That(damageEvents[0].WasBraced, Is.True);
                Assert.That(damageEvents[0].FinalAmount, Is.EqualTo(2));
            }
            finally
            {
                Destroy(ability);
            }
        }
    }

    [Test]
    public void AvoidingPendingShapeDoesNotConsumeBraceDuringResolution()
    {
        AssertAvoidedMeleeDoesNotConsumeBrace();
        AssertAvoidedConeDoesNotConsumeBrace();
        AssertAvoidedAoeDoesNotConsumeBrace();
    }

    private static void AssertAvoidedMeleeDoesNotConsumeBrace()
    {
        using (var battle = new ResolverBattle())
        {
            var actor = battle.CreateUnit("Avoided Brace Melee Actor", TeamId.Player, GridPosition.Zero);
            var target = battle.CreateUnit("Avoided Brace Melee Target", TeamId.Enemy, GridPosition.East);
            var ability = CreateMeleeSlash(damage: 4);

            try
            {
                var intent = CreateMeleeIntent(actor, target, ability);
                Assert.That(target.BraceUntilNextHit(BraceReactionCommand.DefaultDamageReduction).IsSuccess, Is.True);
                target.SetGridPosition(new GridPosition(3, 0, 0));
                var damageEvents = CaptureDamageEvents(battle.EventBus);

                var result = battle.CreateResolver(logResolutions: false).Resolve(intent);

                Assert.That(result.IsSuccess, Is.True, result.ErrorMessage);
                Assert.That(target.CurrentHP, Is.EqualTo(target.MaxHP));
                Assert.That(target.BracedUntilNextHit, Is.True);
                Assert.That(damageEvents, Is.Empty);
            }
            finally
            {
                Destroy(ability);
            }
        }
    }

    private static void AssertAvoidedConeDoesNotConsumeBrace()
    {
        using (var battle = new ResolverBattle())
        {
            var actor = battle.CreateUnit("Avoided Brace Cone Actor", TeamId.Player, GridPosition.Zero);
            var target = battle.CreateUnit("Avoided Brace Cone Target", TeamId.Enemy, new GridPosition(0, 0, 2));
            var ability = CreateConeShot(damage: 3);

            try
            {
                var intent = CreateConeIntent(actor, ability);
                Assert.That(target.BraceUntilNextHit(BraceReactionCommand.DefaultDamageReduction).IsSuccess, Is.True);
                target.SetGridPosition(new GridPosition(3, 0, 0));
                var damageEvents = CaptureDamageEvents(battle.EventBus);

                var result = battle.CreateResolver(logResolutions: false).Resolve(intent);

                Assert.That(result.IsSuccess, Is.True, result.ErrorMessage);
                Assert.That(target.CurrentHP, Is.EqualTo(target.MaxHP));
                Assert.That(target.BracedUntilNextHit, Is.True);
                Assert.That(damageEvents, Is.Empty);
            }
            finally
            {
                Destroy(ability);
            }
        }
    }

    private static void AssertAvoidedAoeDoesNotConsumeBrace()
    {
        using (var battle = new ResolverBattle())
        {
            var actor = battle.CreateUnit("Avoided Brace AoE Actor", TeamId.Player, GridPosition.Zero);
            var target = battle.CreateUnit("Avoided Brace AoE Target", TeamId.Enemy, RadiusTargetCell);
            var ability = CreateFireball(damage: 4);

            try
            {
                var intent = CreateRadiusIntent(actor, ability);
                Assert.That(target.BraceUntilNextHit(BraceReactionCommand.DefaultDamageReduction).IsSuccess, Is.True);
                target.SetGridPosition(new GridPosition(5, 0, 0));
                var damageEvents = CaptureDamageEvents(battle.EventBus);

                var result = battle.CreateResolver(logResolutions: false).Resolve(intent);

                Assert.That(result.IsSuccess, Is.True, result.ErrorMessage);
                Assert.That(target.CurrentHP, Is.EqualTo(target.MaxHP));
                Assert.That(target.BracedUntilNextHit, Is.True);
                Assert.That(damageEvents, Is.Empty);
            }
            finally
            {
                Destroy(ability);
            }
        }
    }

    private static readonly GridPosition ConeTargetCell = new GridPosition(0, 0, 4);

    private static readonly GridPosition[] DeclaredConeCells =
    {
        new GridPosition(0, 0, 1),
        new GridPosition(-1, 0, 2),
        new GridPosition(0, 0, 2),
        new GridPosition(1, 0, 2),
        new GridPosition(0, 0, 3),
    };

    private static readonly GridPosition RadiusTargetCell = new GridPosition(2, 0, 1);

    private static readonly GridPosition[] RadiusOneCells =
    {
        RadiusTargetCell,
        new GridPosition(2, 0, 0),
        new GridPosition(1, 0, 1),
        new GridPosition(3, 0, 1),
        new GridPosition(2, 0, 2),
    };

    private static ActionIntent CreateMeleeIntent(TacticalUnit actor, TacticalUnit target, AbilityDefinition ability)
    {
        return new ActionIntent(
            actor,
            ability,
            actor.CurrentGridPosition,
            ActionTarget.ForUnit(target),
            new[] { target.CurrentGridPosition },
            declarationRound: 1,
            declarationSequence: 0);
    }

    private static ActionIntent CreateConeIntent(TacticalUnit actor, AbilityDefinition ability)
    {
        return new ActionIntent(
            actor,
            ability,
            actor.CurrentGridPosition,
            ActionTarget.ForCellAndDirection(ConeTargetCell, CardinalDirection.North),
            DeclaredConeCells,
            declarationRound: 1,
            declarationSequence: 0);
    }

    private static ActionIntent CreateRadiusIntent(TacticalUnit actor, AbilityDefinition ability)
    {
        return new ActionIntent(
            actor,
            ability,
            actor.CurrentGridPosition,
            ActionTarget.ForCell(RadiusTargetCell),
            RadiusOneCells,
            declarationRound: 1,
            declarationSequence: 0);
    }

    private static AbilityDefinition CreateMeleeSlash(int damage)
    {
        return CreateAbility("melee_slash", "Melee Slash", AbilityShape.Melee, apCost: 3, range: 0, radius: 0, damage: damage);
    }

    private static AbilityDefinition CreateConeShot(int damage)
    {
        return CreateAbility("cone_shot", "Cone Shot", AbilityShape.Cone, apCost: 4, range: 4, radius: 0, damage: damage);
    }

    private static AbilityDefinition CreateFireball(int damage)
    {
        return CreateAbility("fireball", "Fireball", AbilityShape.Radius, apCost: 5, range: 5, radius: 1, damage: damage);
    }

    private static AbilityDefinition CreateAbility(
        string abilityKey,
        string displayName,
        AbilityShape shape,
        int apCost,
        int range,
        int radius,
        int damage)
    {
        var ability = ScriptableObject.CreateInstance<AbilityDefinition>();
        ability.Configure(
            abilityKey,
            displayName,
            apCost,
            AbilityUsage.Action,
            AbilityTiming.Telegraphed,
            shape,
            range,
            radius,
            damage,
            triggersReactions: true,
            description: $"Brace resolution test ability: {displayName}.");
        return ability;
    }

    private static UnitStatsDefinition CreateStats(string displayName)
    {
        var stats = ScriptableObject.CreateInstance<UnitStatsDefinition>();
        stats.Configure(
            displayName,
            maxHP: 10,
            maxAP: 6,
            movementAnimationSpeed: 4f,
            meleeRange: UnitStatsDefinition.MinimumMeleeRange,
            teamColorHint: Color.white);
        return stats;
    }

    private static List<DamageEvent> CaptureDamageEvents(CombatEventBus eventBus)
    {
        var events = new List<DamageEvent>();
        eventBus.DamageApplied += eventData => events.Add(eventData);
        return events;
    }

    private static void Destroy(UnityEngine.Object asset)
    {
        if (asset != null)
        {
            UnityEngine.Object.DestroyImmediate(asset);
        }
    }

    private sealed class ResolverBattle : IDisposable
    {
        private readonly List<GameObject> unitObjects = new List<GameObject>();
        private readonly List<UnitStatsDefinition> unitStats = new List<UnitStatsDefinition>();
        private readonly List<TacticalUnit> units = new List<TacticalUnit>();

        public ResolverBattle()
        {
            BusObject = new GameObject("Brace Resolution Event Bus");
            EventBus = BusObject.AddComponent<CombatEventBus>();
        }

        public GameObject BusObject { get; }

        public CombatEventBus EventBus { get; }

        public TacticalUnit CreateUnit(string displayName, TeamId team, GridPosition position)
        {
            var stats = CreateStats(displayName);
            var unitObject = new GameObject(displayName);
            var unit = unitObject.AddComponent<TacticalUnit>();
            unit.Initialize(new UnitId(units.Count + 1), team, stats, position);

            unitStats.Add(stats);
            unitObjects.Add(unitObject);
            units.Add(unit);
            return unit;
        }

        public ActionResolver CreateResolver(bool logResolutions)
        {
            return new ActionResolver(EventBus, BusObject, logResolutions, () => units);
        }

        public void Dispose()
        {
            for (var i = unitObjects.Count - 1; i >= 0; i -= 1)
            {
                Destroy(unitObjects[i]);
            }

            Destroy(BusObject);

            for (var i = unitStats.Count - 1; i >= 0; i -= 1)
            {
                Destroy(unitStats[i]);
            }
        }
    }
}
