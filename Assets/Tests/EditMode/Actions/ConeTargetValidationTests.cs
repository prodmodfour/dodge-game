using System;
using System.Collections.Generic;
using NUnit.Framework;
using ReactionTactics.Actions;
using ReactionTactics.Grid;
using ReactionTactics.Targeting;
using ReactionTactics.Turns;
using ReactionTactics.UI;
using ReactionTactics.Units;
using UnityEngine;

public sealed class ConeTargetValidationTests
{
    [Test]
    public void ConeAbilityRequiresTargetCell()
    {
        using (var battle = new TestBattle())
        {
            var ability = CreateConeShot();

            try
            {
                var result = AbilityTargetValidator.Instance.ValidateTarget(CreateContext(
                    battle,
                    ability,
                    ActionTarget.None));

                Assert.That(result.IsFailure, Is.True);
                Assert.That(result.ErrorMessage, Does.Contain("Cone abilities require a target cell"));
                Assert.That(battle.Actor.CurrentAP, Is.EqualTo(battle.Actor.MaxAP));
            }
            finally
            {
                Destroy(ability);
            }
        }
    }

    [Test]
    public void CellOnlyConeTargetWithinRangeIsValidAndDoesNotSpendAP()
    {
        using (var battle = new TestBattle())
        {
            var ability = CreateConeShot(range: 4);

            try
            {
                var targetCell = new GridPosition(2, 0, 5);
                var target = ActionTarget.ForCell(targetCell);

                var result = AbilityTargetValidator.Instance.ValidateTarget(CreateContext(
                    battle,
                    ability,
                    target));

                Assert.That(result.IsSuccess, Is.True, result.ErrorMessage);
                Assert.That(target.HasDirection, Is.False, "Cone direction is derived from actor origin and target cell during validation/preview.");
                Assert.That(battle.Actor.CurrentAP, Is.EqualTo(battle.Actor.MaxAP));
            }
            finally
            {
                Destroy(ability);
            }
        }
    }

    [Test]
    public void ConeTargetCellMustExistOnMap()
    {
        using (var battle = new TestBattle())
        {
            var ability = CreateConeShot();

            try
            {
                var missingCell = new GridPosition(9, 0, 9);

                var result = AbilityTargetValidator.Instance.ValidateTarget(CreateContext(
                    battle,
                    ability,
                    ActionTarget.ForCell(missingCell)));

                Assert.That(result.IsFailure, Is.True);
                Assert.That(result.ErrorMessage, Does.Contain("not on the grid map"));
                Assert.That(battle.Actor.CurrentAP, Is.EqualTo(battle.Actor.MaxAP));
            }
            finally
            {
                Destroy(ability);
            }
        }
    }

    [Test]
    public void ConeTargetCellMustNotBeActorCell()
    {
        using (var battle = new TestBattle())
        {
            var ability = CreateConeShot();

            try
            {
                var result = AbilityTargetValidator.Instance.ValidateTarget(CreateContext(
                    battle,
                    ability,
                    ActionTarget.ForCell(battle.ActorPosition)));

                Assert.That(result.IsFailure, Is.True);
                Assert.That(result.ErrorMessage, Does.Contain("requires a target cell away"));
                Assert.That(battle.Actor.CurrentAP, Is.EqualTo(battle.Actor.MaxAP));
            }
            finally
            {
                Destroy(ability);
            }
        }
    }

    [Test]
    public void ConeTargetCellMustBeWithinAbilityRange()
    {
        using (var battle = new TestBattle())
        {
            var ability = CreateConeShot(range: 3);

            try
            {
                var outOfRangeCell = new GridPosition(2, 0, 5);

                var result = AbilityTargetValidator.Instance.ValidateTarget(CreateContext(
                    battle,
                    ability,
                    ActionTarget.ForCell(outOfRangeCell)));

                Assert.That(result.IsFailure, Is.True);
                Assert.That(result.ErrorMessage, Does.Contain("beyond range 3"));
                Assert.That(result.ErrorMessage, Does.Contain("4 cells away"));
                Assert.That(battle.Actor.CurrentAP, Is.EqualTo(battle.Actor.MaxAP));
            }
            finally
            {
                Destroy(ability);
            }
        }
    }

    [Test]
    public void ExplicitConeDirectionMustMatchDirectionFromOriginToTargetCell()
    {
        using (var battle = new TestBattle())
        {
            var ability = CreateConeShot();

            try
            {
                var target = ActionTarget.ForCellAndDirection(
                    new GridPosition(2, 0, 5),
                    CardinalDirection.East);

                var result = AbilityTargetValidator.Instance.ValidateTarget(CreateContext(
                    battle,
                    ability,
                    target));

                Assert.That(result.IsFailure, Is.True);
                Assert.That(result.ErrorMessage, Does.Contain("resolves to North"));
                Assert.That(result.ErrorMessage, Does.Contain("not East"));
                Assert.That(battle.Actor.CurrentAP, Is.EqualTo(battle.Actor.MaxAP));
            }
            finally
            {
                Destroy(ability);
            }
        }
    }

    [Test]
    public void ConePreviewDeterminesDirectionAndProducesAffectedCellsWithoutSpendingAP()
    {
        using (var battle = new TestBattle())
        {
            var ability = CreateConeShot(range: 4);

            try
            {
                var targetCell = new GridPosition(2, 0, 5);
                var service = new IntentPreviewService();
                var state = new CombatState(1, CombatPhase.ActiveTurn, battle.Actor);
                var startingAP = battle.Actor.CurrentAP;

                var preview = service.CreatePreview(
                    battle.Actor,
                    ability,
                    ActionTarget.ForCell(targetCell),
                    state,
                    battle.Map,
                    new[] { battle.Actor, battle.Enemy, battle.Ally, battle.FarEnemy });

                Assert.That(preview.IsValid, Is.True, preview.InvalidReason);
                Assert.That(preview.Target.TargetCell, Is.EqualTo(targetCell));
                Assert.That(preview.AffectedCells, Is.EqualTo(ExpectedNorthConeRangeFour()));
                Assert.That(preview.ThreatenedUnits, Is.EqualTo(new[] { battle.Enemy, battle.Ally }));
                Assert.That(battle.Actor.CurrentAP, Is.EqualTo(startingAP));
            }
            finally
            {
                Destroy(ability);
            }
        }
    }

    [Test]
    public void ConeActionDeclarationAcceptsValidCellTargetAndPreservesAffectedCells()
    {
        using (var battle = new TestBattle())
        {
            var ability = CreateConeShot(range: 4);

            try
            {
                var targetCell = new GridPosition(2, 0, 5);
                var affectedCells = AreaShapeService.GetConeCells(
                    battle.ActorPosition,
                    CardinalDirection.North,
                    ability.Range,
                    battle.Map);
                var state = new CombatState(2, CombatPhase.ActiveTurn, battle.Actor);
                var service = new ActionDeclarationService();

                var result = service.DeclareAction(
                    battle.Actor,
                    ability,
                    ActionTarget.ForCell(targetCell),
                    state,
                    battle.Map,
                    affectedCells);

                Assert.That(result.IsSuccess, Is.True, result.ErrorMessage);
                Assert.That(battle.Actor.CurrentAP, Is.EqualTo(battle.Actor.MaxAP - ability.APCost));
                Assert.That(result.Value.Target.TargetCell, Is.EqualTo(targetCell));
                Assert.That(result.Value.Target.HasDirection, Is.False);
                Assert.That(result.Value.DeclaredTargetUnit, Is.Null);
                Assert.That(result.Value.DeclaredAffectedCells, Is.EqualTo(ExpectedNorthConeRangeFour()));
                Assert.That(result.Value.DeclarationRound, Is.EqualTo(2));
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

    private static AbilityDefinition CreateConeShot(int range = 4)
    {
        var ability = ScriptableObject.CreateInstance<AbilityDefinition>();
        ability.Configure(
            "cone_shot",
            "Cone Shot",
            apCost: 4,
            usage: AbilityUsage.Action,
            timing: AbilityTiming.Telegraphed,
            shape: AbilityShape.Cone,
            range: range,
            radius: 0,
            damage: 3,
            triggersReactions: true,
            description: "Cone target validation test ability.");
        return ability;
    }

    private static GridPosition[] ExpectedNorthConeRangeFour()
    {
        return new[]
        {
            new GridPosition(2, 0, 2),
            new GridPosition(1, 0, 3),
            new GridPosition(2, 0, 3),
            new GridPosition(3, 0, 3),
            new GridPosition(1, 0, 4),
            new GridPosition(2, 0, 4),
            new GridPosition(3, 0, 4),
            new GridPosition(0, 0, 5),
            new GridPosition(1, 0, 5),
            new GridPosition(2, 0, 5),
            new GridPosition(3, 0, 5),
            new GridPosition(4, 0, 5),
        };
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

    private static GridMap CreateMap()
    {
        var cells = new List<GridCell>();
        for (var x = 0; x <= 5; x += 1)
        {
            for (var z = 0; z <= 5; z += 1)
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
        private readonly GameObject farEnemyObject;
        private readonly UnitStatsDefinition actorStats;
        private readonly UnitStatsDefinition enemyStats;
        private readonly UnitStatsDefinition allyStats;
        private readonly UnitStatsDefinition farEnemyStats;

        public TestBattle()
        {
            ActorPosition = new GridPosition(2, 0, 1);
            EnemyPosition = new GridPosition(2, 0, 2);
            AllyPosition = new GridPosition(1, 0, 3);
            FarEnemyPosition = new GridPosition(5, 0, 5);
            Map = CreateMap();

            actorStats = CreateStats("Actor");
            enemyStats = CreateStats("Enemy");
            allyStats = CreateStats("Ally");
            farEnemyStats = CreateStats("Far Enemy");

            actorObject = new GameObject("Cone Validation Actor");
            enemyObject = new GameObject("Cone Validation Enemy");
            allyObject = new GameObject("Cone Validation Ally");
            farEnemyObject = new GameObject("Cone Validation Far Enemy");

            Actor = actorObject.AddComponent<TacticalUnit>();
            Enemy = enemyObject.AddComponent<TacticalUnit>();
            Ally = allyObject.AddComponent<TacticalUnit>();
            FarEnemy = farEnemyObject.AddComponent<TacticalUnit>();

            Actor.Initialize(new UnitId(1), TeamId.Player, actorStats, ActorPosition);
            Enemy.Initialize(new UnitId(2), TeamId.Enemy, enemyStats, EnemyPosition);
            Ally.Initialize(new UnitId(3), TeamId.Player, allyStats, AllyPosition);
            FarEnemy.Initialize(new UnitId(4), TeamId.Enemy, farEnemyStats, FarEnemyPosition);
        }

        public TacticalUnit Actor { get; }

        public TacticalUnit Enemy { get; }

        public TacticalUnit Ally { get; }

        public TacticalUnit FarEnemy { get; }

        public GridPosition ActorPosition { get; }

        public GridPosition EnemyPosition { get; }

        public GridPosition AllyPosition { get; }

        public GridPosition FarEnemyPosition { get; }

        public GridMap Map { get; }

        public void Dispose()
        {
            Destroy(farEnemyObject);
            Destroy(allyObject);
            Destroy(enemyObject);
            Destroy(actorObject);
            Destroy(farEnemyStats);
            Destroy(allyStats);
            Destroy(enemyStats);
            Destroy(actorStats);
        }
    }
}
