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

public sealed class AoeTargetValidationTests
{
    [Test]
    public void RadiusAbilityRequiresTargetCell()
    {
        using (var battle = new TestBattle())
        {
            var ability = CreateFireball();

            try
            {
                var result = AbilityTargetValidator.Instance.ValidateTarget(CreateContext(
                    battle,
                    ability,
                    ActionTarget.None));

                Assert.That(result.IsFailure, Is.True);
                Assert.That(result.ErrorMessage, Does.Contain("Radius abilities require a target cell"));
                Assert.That(battle.Actor.CurrentAP, Is.EqualTo(battle.Actor.MaxAP));
            }
            finally
            {
                Destroy(ability);
            }
        }
    }

    [Test]
    public void EmptyCellWithinRangeIsValidAndDoesNotSpendAP()
    {
        using (var battle = new TestBattle())
        {
            var ability = CreateFireball();

            try
            {
                var emptyTargetCell = new GridPosition(2, 0, 1);

                var result = AbilityTargetValidator.Instance.ValidateTarget(CreateContext(
                    battle,
                    ability,
                    ActionTarget.ForCell(emptyTargetCell)));

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
    public void RadiusTargetCellMustExistOnMap()
    {
        using (var battle = new TestBattle())
        {
            var ability = CreateFireball();

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
    public void RadiusTargetCellMustBeWithinAbilityRange()
    {
        using (var battle = new TestBattle())
        {
            var ability = CreateFireball(range: 4);

            try
            {
                var outOfRangeCell = new GridPosition(5, 0, 0);

                var result = AbilityTargetValidator.Instance.ValidateTarget(CreateContext(
                    battle,
                    ability,
                    ActionTarget.ForCell(outOfRangeCell)));

                Assert.That(result.IsFailure, Is.True);
                Assert.That(result.ErrorMessage, Does.Contain("beyond range 4"));
                Assert.That(result.ErrorMessage, Does.Contain("5 cells away"));
                Assert.That(battle.Actor.CurrentAP, Is.EqualTo(battle.Actor.MaxAP));
            }
            finally
            {
                Destroy(ability);
            }
        }
    }

    [Test]
    public void RadiusPreviewProducesAffectedCellsAndThreatenedUnitsWithoutSpendingAP()
    {
        using (var battle = new TestBattle())
        {
            var ability = CreateFireball();

            try
            {
                var targetCell = new GridPosition(2, 0, 1);
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
                Assert.That(preview.AffectedCells, Is.EqualTo(ExpectedRadiusOneCells(targetCell)));
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
    public void RadiusActionDeclarationAcceptsValidTargetAndPreservesAffectedCells()
    {
        using (var battle = new TestBattle())
        {
            var ability = CreateFireball();

            try
            {
                var targetCell = new GridPosition(2, 0, 1);
                var affectedCells = AreaShapeService.GetRadiusCells(targetCell, ability.Radius, battle.Map);
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
                Assert.That(result.Value.DeclaredTargetUnit, Is.Null);
                Assert.That(result.Value.DeclaredAffectedCells, Is.EqualTo(ExpectedRadiusOneCells(targetCell)));
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

    private static AbilityDefinition CreateFireball(int range = 4, int radius = 1)
    {
        var ability = ScriptableObject.CreateInstance<AbilityDefinition>();
        ability.Configure(
            "fireball",
            "Fireball",
            apCost: 5,
            usage: AbilityUsage.Action,
            timing: AbilityTiming.Telegraphed,
            shape: AbilityShape.Radius,
            range: range,
            radius: radius,
            damage: 4,
            triggersReactions: true,
            description: "AoE target validation test ability.");
        return ability;
    }

    private static GridPosition[] ExpectedRadiusOneCells(GridPosition targetCell)
    {
        return new[]
        {
            targetCell,
            new GridPosition(targetCell.X, targetCell.Y, targetCell.Z - 1),
            new GridPosition(targetCell.X - 1, targetCell.Y, targetCell.Z),
            new GridPosition(targetCell.X + 1, targetCell.Y, targetCell.Z),
            new GridPosition(targetCell.X, targetCell.Y, targetCell.Z + 1),
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
            for (var z = 0; z <= 3; z += 1)
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
            ActorPosition = new GridPosition(0, 0, 0);
            EnemyPosition = new GridPosition(2, 0, 0);
            AllyPosition = new GridPosition(2, 0, 2);
            FarEnemyPosition = new GridPosition(5, 0, 3);
            Map = CreateMap();

            actorStats = CreateStats("Actor");
            enemyStats = CreateStats("Enemy");
            allyStats = CreateStats("Ally");
            farEnemyStats = CreateStats("Far Enemy");

            actorObject = new GameObject("AoE Validation Actor");
            enemyObject = new GameObject("AoE Validation Enemy");
            allyObject = new GameObject("AoE Validation Ally");
            farEnemyObject = new GameObject("AoE Validation Far Enemy");

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
