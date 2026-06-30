using System;
using System.Collections.Generic;
using System.Text.RegularExpressions;
using NUnit.Framework;
using ReactionTactics.Actions;
using ReactionTactics.Grid;
using ReactionTactics.Turns;
using ReactionTactics.Units;
using UnityEngine;
using UnityEngine.TestTools;

public sealed class AoeResolutionTests
{
    [Test]
    public void RadiusResolutionDamagesEveryLivingUnitInsideDeclaredArea()
    {
        using (var battle = new TestBattle())
        {
            var ability = CreateFireball(damage: 4);

            try
            {
                var intent = CreateRadiusIntent(battle, ability);
                var hpEvents = 0;
                var resolvedEvents = 0;
                battle.EventBus.HitPointsChanged += _ => hpEvents += 1;
                battle.EventBus.ActionResolved += _ => resolvedEvents += 1;

                var resolver = CreateResolver(battle, logResolutions: false);
                var result = resolver.Resolve(intent);

                Assert.That(result.IsSuccess, Is.True, result.ErrorMessage);
                Assert.That(battle.Enemy.CurrentHP, Is.EqualTo(6));
                Assert.That(battle.Ally.CurrentHP, Is.EqualTo(6));
                Assert.That(battle.Actor.CurrentHP, Is.EqualTo(battle.Actor.MaxHP));
                Assert.That(battle.FarEnemy.CurrentHP, Is.EqualTo(battle.FarEnemy.MaxHP));
                Assert.That(hpEvents, Is.EqualTo(2));
                Assert.That(resolvedEvents, Is.EqualTo(1));
            }
            finally
            {
                Destroy(ability);
            }
        }
    }

    [Test]
    public void RadiusResolutionUsesFinalPositionsSoMovedOutUnitsAvoidDamage()
    {
        using (var battle = new TestBattle())
        {
            var ability = CreateFireball(damage: 4);

            try
            {
                var intent = CreateRadiusIntent(battle, ability);
                battle.Enemy.SetGridPosition(new GridPosition(4, 0, 0));
                var hpEvents = 0;
                battle.EventBus.HitPointsChanged += _ => hpEvents += 1;

                var resolver = CreateResolver(battle, logResolutions: false);
                var result = resolver.Resolve(intent);

                Assert.That(result.IsSuccess, Is.True, result.ErrorMessage);
                Assert.That(battle.Enemy.CurrentHP, Is.EqualTo(battle.Enemy.MaxHP));
                Assert.That(battle.Ally.CurrentHP, Is.EqualTo(6));
                Assert.That(hpEvents, Is.EqualTo(1));
            }
            finally
            {
                Destroy(ability);
            }
        }
    }

    [Test]
    public void RadiusResolutionUsesFinalPositionsSoMovedInUnitsAreHit()
    {
        using (var battle = new TestBattle())
        {
            var ability = CreateFireball(damage: 4);

            try
            {
                var intent = CreateRadiusIntent(battle, ability);
                battle.FarEnemy.SetGridPosition(new GridPosition(3, 0, 1));
                var hpEvents = 0;
                battle.EventBus.HitPointsChanged += _ => hpEvents += 1;

                var resolver = CreateResolver(battle, logResolutions: false);
                var result = resolver.Resolve(intent);

                Assert.That(result.IsSuccess, Is.True, result.ErrorMessage);
                Assert.That(battle.Enemy.CurrentHP, Is.EqualTo(6));
                Assert.That(battle.Ally.CurrentHP, Is.EqualTo(6));
                Assert.That(battle.FarEnemy.CurrentHP, Is.EqualTo(6));
                Assert.That(hpEvents, Is.EqualTo(3));
            }
            finally
            {
                Destroy(ability);
            }
        }
    }

    [Test]
    public void RadiusResolutionIgnoresUnitsAlreadyDefeatedAtResolution()
    {
        using (var battle = new TestBattle())
        {
            var ability = CreateFireball(damage: 4);

            try
            {
                var intent = CreateRadiusIntent(battle, ability);
                Assert.That(battle.Enemy.ApplyDamage(99, DamageSource.Environmental("test setup defeat")).IsSuccess, Is.True);
                var hpEvents = 0;
                battle.EventBus.HitPointsChanged += _ => hpEvents += 1;

                var resolver = CreateResolver(battle, logResolutions: false);
                var result = resolver.Resolve(intent);

                Assert.That(result.IsSuccess, Is.True, result.ErrorMessage);
                Assert.That(battle.Enemy.CurrentHP, Is.EqualTo(0));
                Assert.That(battle.Enemy.IsDead, Is.True);
                Assert.That(battle.Ally.CurrentHP, Is.EqualTo(6));
                Assert.That(hpEvents, Is.EqualTo(1));
            }
            finally
            {
                Destroy(ability);
            }
        }
    }

    [Test]
    public void RadiusResolutionRejectsIntentWithoutDeclaredAffectedCells()
    {
        using (var battle = new TestBattle())
        {
            var ability = CreateFireball(damage: 4);

            try
            {
                var intent = new ActionIntent(
                    battle.Actor,
                    ability,
                    battle.Actor.CurrentGridPosition,
                    ActionTarget.ForCell(TestBattle.TargetCell),
                    Array.Empty<GridPosition>(),
                    declarationRound: 1,
                    declarationSequence: 0);
                var resolvedEvents = 0;
                battle.EventBus.ActionResolved += _ => resolvedEvents += 1;

                var resolver = CreateResolver(battle, logResolutions: false);
                var result = resolver.Resolve(intent);

                Assert.That(result.IsFailure, Is.True);
                Assert.That(result.ErrorMessage, Does.Contain("no declared affected cells"));
                Assert.That(battle.Enemy.CurrentHP, Is.EqualTo(battle.Enemy.MaxHP));
                Assert.That(battle.Ally.CurrentHP, Is.EqualTo(battle.Ally.MaxHP));
                Assert.That(resolvedEvents, Is.EqualTo(0));
            }
            finally
            {
                Destroy(ability);
            }
        }
    }

    [Test]
    public void RadiusResolutionWritesAffectedAndAvoidedUnitsToCombatLog()
    {
        using (var battle = new TestBattle())
        {
            var ability = CreateFireball(damage: 4);

            try
            {
                var intent = CreateRadiusIntent(battle, ability);
                battle.Enemy.SetGridPosition(new GridPosition(4, 0, 0));
                LogAssert.Expect(
                    LogType.Log,
                    new Regex(@"\[Combat Log\].*resolved AoE 'Fireball'.*affected: .*Ally.*; avoided: .*Enemy.*Far Enemy.*no dodge or accuracy roll"));

                var resolver = CreateResolver(battle, logResolutions: true);
                var result = resolver.Resolve(intent);

                Assert.That(result.IsSuccess, Is.True, result.ErrorMessage);
            }
            finally
            {
                Destroy(ability);
            }
        }
    }

    private static ActionResolver CreateResolver(TestBattle battle, bool logResolutions)
    {
        return new ActionResolver(
            battle.EventBus,
            battle.BusObject,
            logResolutions,
            () => battle.AllUnits);
    }

    private static ActionIntent CreateRadiusIntent(TestBattle battle, AbilityDefinition ability)
    {
        return new ActionIntent(
            battle.Actor,
            ability,
            battle.Actor.CurrentGridPosition,
            ActionTarget.ForCell(TestBattle.TargetCell),
            TestBattle.RadiusOneCells,
            declarationRound: 1,
            declarationSequence: 0);
    }

    private static AbilityDefinition CreateFireball(int damage)
    {
        var ability = ScriptableObject.CreateInstance<AbilityDefinition>();
        ability.Configure(
            "fireball",
            "Fireball",
            apCost: 5,
            usage: AbilityUsage.Action,
            timing: AbilityTiming.Telegraphed,
            shape: AbilityShape.Radius,
            range: 5,
            radius: 1,
            damage: damage,
            triggersReactions: true,
            description: "AoE resolution test ability.");
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
        public static readonly GridPosition TargetCell = new GridPosition(2, 0, 1);
        public static readonly GridPosition[] RadiusOneCells =
        {
            TargetCell,
            new GridPosition(2, 0, 0),
            new GridPosition(1, 0, 1),
            new GridPosition(3, 0, 1),
            new GridPosition(2, 0, 2),
        };

        private readonly GameObject actorObject;
        private readonly GameObject enemyObject;
        private readonly GameObject allyObject;
        private readonly GameObject farEnemyObject;
        private readonly UnitStatsDefinition actorStats;
        private readonly UnitStatsDefinition enemyStats;
        private readonly UnitStatsDefinition allyStats;
        private readonly UnitStatsDefinition farEnemyStats;
        private readonly TacticalUnit[] allUnits;

        public TestBattle()
        {
            BusObject = new GameObject("AoE Resolution Event Bus");
            EventBus = BusObject.AddComponent<CombatEventBus>();

            actorStats = CreateStats("Actor");
            enemyStats = CreateStats("Enemy");
            allyStats = CreateStats("Ally");
            farEnemyStats = CreateStats("Far Enemy");

            actorObject = new GameObject("AoE Resolution Actor");
            enemyObject = new GameObject("AoE Resolution Enemy");
            allyObject = new GameObject("AoE Resolution Ally");
            farEnemyObject = new GameObject("AoE Resolution Far Enemy");

            Actor = actorObject.AddComponent<TacticalUnit>();
            Enemy = enemyObject.AddComponent<TacticalUnit>();
            Ally = allyObject.AddComponent<TacticalUnit>();
            FarEnemy = farEnemyObject.AddComponent<TacticalUnit>();

            Actor.Initialize(new UnitId(1), TeamId.Player, actorStats, GridPosition.Zero);
            Enemy.Initialize(new UnitId(2), TeamId.Enemy, enemyStats, TargetCell);
            Ally.Initialize(new UnitId(3), TeamId.Player, allyStats, new GridPosition(1, 0, 1));
            FarEnemy.Initialize(new UnitId(4), TeamId.Enemy, farEnemyStats, new GridPosition(5, 0, 1));

            allUnits = new[] { Actor, Enemy, Ally, FarEnemy };
        }

        public GameObject BusObject { get; }

        public CombatEventBus EventBus { get; }

        public TacticalUnit Actor { get; }

        public TacticalUnit Enemy { get; }

        public TacticalUnit Ally { get; }

        public TacticalUnit FarEnemy { get; }

        public IReadOnlyList<TacticalUnit> AllUnits
        {
            get { return allUnits; }
        }

        public void Dispose()
        {
            Destroy(farEnemyObject);
            Destroy(allyObject);
            Destroy(enemyObject);
            Destroy(actorObject);
            Destroy(BusObject);
            Destroy(farEnemyStats);
            Destroy(allyStats);
            Destroy(enemyStats);
            Destroy(actorStats);
        }
    }
}
