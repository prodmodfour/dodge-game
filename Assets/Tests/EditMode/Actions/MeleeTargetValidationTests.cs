using System;
using System.Collections.Generic;
using NUnit.Framework;
using ReactionTactics.Actions;
using ReactionTactics.Grid;
using ReactionTactics.Turns;
using ReactionTactics.Units;
using UnityEngine;

public sealed class MeleeTargetValidationTests
{
    [Test]
    public void AdjacentHostileTargetIsLegalAndDoesNotSpendAP()
    {
        using (var battle = new TestBattle())
        {
            var ability = CreateMeleeAbility();

            try
            {
                var result = AbilityTargetValidator.Instance.ValidateTarget(CreateContext(
                    battle,
                    ability,
                    ActionTarget.ForUnit(battle.Enemy)));

                Assert.That(result.IsSuccess, Is.True, result.ErrorMessage);
                Assert.That(battle.Actor.CurrentAP, Is.EqualTo(battle.Actor.MaxAP));
            }
            finally
            {
                Destroy(ability);
            }
        }
    }

    [Test]
    public void MeleeRequiresTargetUnit()
    {
        using (var battle = new TestBattle())
        {
            var ability = CreateMeleeAbility();

            try
            {
                var result = AbilityTargetValidator.Instance.ValidateTarget(CreateContext(
                    battle,
                    ability,
                    ActionTarget.ForCell(battle.EnemyPosition)));

                Assert.That(result.IsFailure, Is.True);
                Assert.That(result.ErrorMessage, Does.Contain("target unit"));
                Assert.That(battle.Actor.CurrentAP, Is.EqualTo(battle.Actor.MaxAP));
            }
            finally
            {
                Destroy(ability);
            }
        }
    }

    [Test]
    public void MeleeRequiresHostileTargetByDefault()
    {
        using (var battle = new TestBattle())
        {
            var ability = CreateMeleeAbility();

            try
            {
                var result = AbilityTargetValidator.Instance.ValidateTarget(CreateContext(
                    battle,
                    ability,
                    ActionTarget.ForUnit(battle.Ally)));

                Assert.That(result.IsFailure, Is.True);
                Assert.That(result.ErrorMessage, Does.Contain("hostile target"));
                Assert.That(result.ErrorMessage, Does.Contain("friendly"));
                Assert.That(battle.Actor.CurrentAP, Is.EqualTo(battle.Actor.MaxAP));
            }
            finally
            {
                Destroy(ability);
            }
        }
    }

    [Test]
    public void DefaultMeleeRangeRejectsTwoStepTargetAtDeclaration()
    {
        using (var battle = new TestBattle(enemyPosition: new GridPosition(2, 0, 0)))
        {
            var ability = CreateMeleeAbility();

            try
            {
                var result = AbilityTargetValidator.Instance.ValidateTarget(CreateContext(
                    battle,
                    ability,
                    ActionTarget.ForUnit(battle.Enemy)));

                Assert.That(result.IsFailure, Is.True);
                Assert.That(result.ErrorMessage, Does.Contain("melee range 1"));
                Assert.That(result.ErrorMessage, Does.Contain("2 cells away"));
                Assert.That(battle.Actor.CurrentAP, Is.EqualTo(battle.Actor.MaxAP));
            }
            finally
            {
                Destroy(ability);
            }
        }
    }

    [Test]
    public void ActorStatsMeleeRangeAllowsReachTargetAtDeclaration()
    {
        using (var battle = new TestBattle(actorMeleeRange: 2, enemyPosition: new GridPosition(2, 0, 0)))
        {
            var ability = CreateMeleeAbility(range: 0);

            try
            {
                var result = AbilityTargetValidator.Instance.ValidateTarget(CreateContext(
                    battle,
                    ability,
                    ActionTarget.ForUnit(battle.Enemy)));

                Assert.That(result.IsSuccess, Is.True, result.ErrorMessage);
                Assert.That(battle.Actor.CurrentAP, Is.EqualTo(battle.Actor.MaxAP));
            }
            finally
            {
                Destroy(ability);
            }
        }
    }

    [Test]
    public void MeleeRejectsHostileInSameCellBecauseItRequiresAdjacency()
    {
        using (var battle = new TestBattle(enemyPosition: GridPosition.Zero))
        {
            var ability = CreateMeleeAbility();

            try
            {
                var result = AbilityTargetValidator.Instance.ValidateTarget(CreateContext(
                    battle,
                    ability,
                    ActionTarget.ForUnit(battle.Enemy)));

                Assert.That(result.IsFailure, Is.True);
                Assert.That(result.ErrorMessage, Does.Contain("adjacent hostile target"));
                Assert.That(result.ErrorMessage, Does.Contain("same cell"));
                Assert.That(battle.Actor.CurrentAP, Is.EqualTo(battle.Actor.MaxAP));
            }
            finally
            {
                Destroy(ability);
            }
        }
    }

    [Test]
    public void ActionDeclarationRejectsOutOfRangeMeleeWithoutSpendingAP()
    {
        using (var battle = new TestBattle(enemyPosition: new GridPosition(2, 0, 0)))
        {
            var ability = CreateMeleeAbility();

            try
            {
                var service = new ActionDeclarationService();
                var result = service.DeclareAction(
                    battle.Actor,
                    ability,
                    ActionTarget.ForUnit(battle.Enemy),
                    new CombatState(1, CombatPhase.ActiveTurn, battle.Actor),
                    battle.Map);

                Assert.That(result.IsFailure, Is.True);
                Assert.That(result.ErrorMessage, Does.Contain("melee range 1"));
                Assert.That(battle.Actor.CurrentAP, Is.EqualTo(battle.Actor.MaxAP));
                Assert.That(service.NextDeclarationSequence, Is.EqualTo(0));
            }
            finally
            {
                Destroy(ability);
            }
        }
    }

    private static AbilityTargetValidationContext CreateContext(
        TestBattle battle,
        AbilityDefinition ability,
        ActionTarget target)
    {
        return new AbilityTargetValidationContext(
            battle.Actor,
            ability,
            target,
            AbilityUsage.Action,
            new CombatState(1, CombatPhase.ActiveTurn, battle.Actor),
            battle.Map);
    }

    private static AbilityDefinition CreateMeleeAbility(int range = 0)
    {
        var ability = ScriptableObject.CreateInstance<AbilityDefinition>();
        ability.Configure(
            "melee_slash",
            "Melee Slash",
            apCost: 3,
            usage: AbilityUsage.Action,
            timing: AbilityTiming.Telegraphed,
            shape: AbilityShape.Melee,
            range: range,
            radius: 0,
            damage: 4,
            triggersReactions: true);
        return ability;
    }

    private static UnitStatsDefinition CreateStats(string displayName, int meleeRange)
    {
        var stats = ScriptableObject.CreateInstance<UnitStatsDefinition>();
        stats.Configure(
            displayName,
            maxHP: 10,
            maxAP: 6,
            movementAnimationSpeed: 4f,
            meleeRange: meleeRange,
            teamColorHint: Color.white);
        return stats;
    }

    private static GridMap CreateMap()
    {
        var cells = new List<GridCell>();
        for (var x = 0; x <= 4; x += 1)
        {
            for (var z = 0; z <= 2; z += 1)
            {
                cells.Add(new GridCell(new GridPosition(x, 0, z)));
            }
        }

        return new GridMap(cells);
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
        private readonly GameObject allyObject;
        private readonly UnitStatsDefinition actorStats;
        private readonly UnitStatsDefinition enemyStats;
        private readonly UnitStatsDefinition allyStats;

        public TestBattle(
            int actorMeleeRange = UnitStatsDefinition.MinimumMeleeRange,
            GridPosition? enemyPosition = null)
        {
            ActorPosition = GridPosition.Zero;
            EnemyPosition = enemyPosition ?? GridPosition.East;
            AllyPosition = GridPosition.North;
            Map = CreateMap();

            actorStats = CreateStats("Actor", actorMeleeRange);
            enemyStats = CreateStats("Enemy", UnitStatsDefinition.MinimumMeleeRange);
            allyStats = CreateStats("Ally", UnitStatsDefinition.MinimumMeleeRange);

            actorObject = new GameObject("Melee Validation Actor");
            enemyObject = new GameObject("Melee Validation Enemy");
            allyObject = new GameObject("Melee Validation Ally");

            Actor = actorObject.AddComponent<TacticalUnit>();
            Enemy = enemyObject.AddComponent<TacticalUnit>();
            Ally = allyObject.AddComponent<TacticalUnit>();

            Actor.Initialize(new UnitId(1), TeamId.Player, actorStats, ActorPosition);
            Enemy.Initialize(new UnitId(2), TeamId.Enemy, enemyStats, EnemyPosition);
            Ally.Initialize(new UnitId(3), TeamId.Player, allyStats, AllyPosition);
        }

        public TacticalUnit Actor { get; }

        public TacticalUnit Enemy { get; }

        public TacticalUnit Ally { get; }

        public GridPosition ActorPosition { get; }

        public GridPosition EnemyPosition { get; }

        public GridPosition AllyPosition { get; }

        public GridMap Map { get; }

        public void Dispose()
        {
            Destroy(allyObject);
            Destroy(enemyObject);
            Destroy(actorObject);
            Destroy(allyStats);
            Destroy(enemyStats);
            Destroy(actorStats);
        }
    }
}
