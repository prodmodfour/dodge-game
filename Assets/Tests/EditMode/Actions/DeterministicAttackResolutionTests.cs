using System;
using System.Collections.Generic;
using System.IO;
using System.Text.RegularExpressions;
using NUnit.Framework;
using ReactionTactics.Actions;
using ReactionTactics.Grid;
using ReactionTactics.Turns;
using ReactionTactics.Units;
using UnityEngine;

public sealed class DeterministicAttackResolution
{
    private static readonly Regex[] ForbiddenRandomApiPatterns =
    {
        new Regex(@"\bUnityEngine\s*\.\s*Random\b"),
        new Regex(@"\bUnity\s*\.\s*Mathematics\s*\.\s*Random\b"),
        new Regex(@"\bRandom\s*\.\s*(Range|value|InitState|insideUnitCircle|insideUnitSphere|onUnitSphere|rotation|rotationUniform)\b"),
        new Regex(@"\bSystem\s*\.\s*Random\s*\.\s*Shared\b"),
        new Regex(@"\bnew\s+(?:System\s*\.\s*)?Random\s*\("),
        new Regex(@"\b(hitChance|accuracyRoll|dodgeRoll)\b"),
    };

    [Test]
    public void DeterministicAttackResolution_MeleeTargetMovedOutBeforeResolutionAvoidsHit()
    {
        using (var battle = new DeterministicBattle())
        {
            var actor = battle.CreateUnit("Melee Actor", TeamId.Player, GridPosition.Zero);
            var target = battle.CreateUnit("Melee Target", TeamId.Enemy, GridPosition.East);
            var ability = CreateMeleeSlash(damage: 4);

            try
            {
                var intent = CreateMeleeIntent(actor, target, ability);
                target.SetGridPosition(new GridPosition(2, 0, 0));

                var damageEvents = CaptureDamageEvents(battle.EventBus);
                var result = battle.CreateResolver(logResolutions: false).Resolve(intent);

                Assert.That(result.IsSuccess, Is.True, result.ErrorMessage);
                Assert.That(target.CurrentHP, Is.EqualTo(target.MaxHP));
                Assert.That(damageEvents, Is.Empty);
            }
            finally
            {
                Destroy(ability);
            }
        }
    }

    [Test]
    public void DeterministicAttackResolution_MeleeTargetStillAdjacentIsHit()
    {
        using (var battle = new DeterministicBattle())
        {
            var actor = battle.CreateUnit("Melee Actor", TeamId.Player, GridPosition.Zero);
            var target = battle.CreateUnit("Melee Target", TeamId.Enemy, GridPosition.East);
            var ability = CreateMeleeSlash(damage: 4);

            try
            {
                var intent = CreateMeleeIntent(actor, target, ability);

                var damageEvents = CaptureDamageEvents(battle.EventBus);
                var result = battle.CreateResolver(logResolutions: false).Resolve(intent);

                Assert.That(result.IsSuccess, Is.True, result.ErrorMessage);
                Assert.That(target.CurrentHP, Is.EqualTo(6));
                Assert.That(damageEvents, Has.Count.EqualTo(1));
                Assert.That(damageEvents[0].SourceIntent, Is.SameAs(intent));
                Assert.That(damageEvents[0].Attacker, Is.SameAs(actor));
                Assert.That(damageEvents[0].Target, Is.SameAs(target));
                Assert.That(damageEvents[0].Amount, Is.EqualTo(4));
                Assert.That(damageEvents[0].FinalAmount, Is.EqualTo(4));
                Assert.That(damageEvents[0].WasBraced, Is.False);
            }
            finally
            {
                Destroy(ability);
            }
        }
    }

    [Test]
    public void DeterministicAttackResolution_ConeTargetMovedOutBeforeResolutionAvoidsHit()
    {
        using (var battle = new DeterministicBattle())
        {
            var actor = battle.CreateUnit("Cone Actor", TeamId.Player, GridPosition.Zero);
            var target = battle.CreateUnit("Cone Target", TeamId.Enemy, new GridPosition(0, 0, 2));
            var ability = CreateConeShot(damage: 3);

            try
            {
                var intent = CreateConeIntent(actor, ability);
                target.SetGridPosition(new GridPosition(3, 0, 0));

                var damageEvents = CaptureDamageEvents(battle.EventBus);
                var result = battle.CreateResolver(logResolutions: false).Resolve(intent);

                Assert.That(result.IsSuccess, Is.True, result.ErrorMessage);
                Assert.That(target.CurrentHP, Is.EqualTo(target.MaxHP));
                Assert.That(damageEvents, Is.Empty);
            }
            finally
            {
                Destroy(ability);
            }
        }
    }

    [Test]
    public void DeterministicAttackResolution_AoeTargetMovedOutBeforeResolutionAvoidsHit()
    {
        using (var battle = new DeterministicBattle())
        {
            var actor = battle.CreateUnit("AoE Actor", TeamId.Player, GridPosition.Zero);
            var target = battle.CreateUnit("AoE Target", TeamId.Enemy, RadiusTargetCell);
            var ability = CreateFireball(damage: 4);

            try
            {
                var intent = CreateRadiusIntent(actor, ability);
                target.SetGridPosition(new GridPosition(4, 0, 0));

                var damageEvents = CaptureDamageEvents(battle.EventBus);
                var result = battle.CreateResolver(logResolutions: false).Resolve(intent);

                Assert.That(result.IsSuccess, Is.True, result.ErrorMessage);
                Assert.That(target.CurrentHP, Is.EqualTo(target.MaxHP));
                Assert.That(damageEvents, Is.Empty);
            }
            finally
            {
                Destroy(ability);
            }
        }
    }

    [Test]
    public void DeterministicAttackResolution_ResolverPathDoesNotUseRandomHitChanceApis()
    {
        var resolverPath = Path.Combine(Application.dataPath, "Scripts/ReactionTactics/Runtime/Actions/ActionResolver.cs");
        var unitPath = Path.Combine(Application.dataPath, "Scripts/ReactionTactics/Runtime/Units/TacticalUnit.cs");
        var sourcePaths = new[] { resolverPath, unitPath };

        for (var pathIndex = 0; pathIndex < sourcePaths.Length; pathIndex += 1)
        {
            var sourcePath = sourcePaths[pathIndex];
            Assert.That(File.Exists(sourcePath), Is.True, $"Expected source file to exist: {sourcePath}");
            var source = File.ReadAllText(sourcePath);

            for (var patternIndex = 0; patternIndex < ForbiddenRandomApiPatterns.Length; patternIndex += 1)
            {
                var pattern = ForbiddenRandomApiPatterns[patternIndex];
                Assert.That(
                    source,
                    Does.Not.Match(pattern.ToString()),
                    $"Deterministic action resolution must not use random hit, accuracy, or dodge APIs in {sourcePath}.");
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
        return CreateAbility(
            "melee_slash",
            "Melee Slash",
            AbilityShape.Melee,
            apCost: 3,
            range: 0,
            radius: 0,
            damage: damage,
            description: "Deterministic melee integration test ability.");
    }

    private static AbilityDefinition CreateConeShot(int damage)
    {
        return CreateAbility(
            "cone_shot",
            "Cone Shot",
            AbilityShape.Cone,
            apCost: 4,
            range: 4,
            radius: 0,
            damage: damage,
            description: "Deterministic cone integration test ability.");
    }

    private static AbilityDefinition CreateFireball(int damage)
    {
        return CreateAbility(
            "fireball",
            "Fireball",
            AbilityShape.Radius,
            apCost: 5,
            range: 5,
            radius: 1,
            damage: damage,
            description: "Deterministic AoE integration test ability.");
    }

    private static AbilityDefinition CreateAbility(
        string abilityKey,
        string displayName,
        AbilityShape shape,
        int apCost,
        int range,
        int radius,
        int damage,
        string description)
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
            description: description);
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

    private sealed class DeterministicBattle : IDisposable
    {
        private readonly List<GameObject> unitObjects = new List<GameObject>();
        private readonly List<UnitStatsDefinition> unitStats = new List<UnitStatsDefinition>();
        private readonly List<TacticalUnit> units = new List<TacticalUnit>();

        public DeterministicBattle()
        {
            BusObject = new GameObject("Deterministic Attack Resolution Event Bus");
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
