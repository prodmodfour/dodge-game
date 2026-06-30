using System;
using System.Collections.Generic;
using NUnit.Framework;
using ReactionTactics.Actions;
using ReactionTactics.Grid;
using ReactionTactics.Turns;
using ReactionTactics.UI;
using ReactionTactics.Units;
using UnityEngine;

public sealed class RangedLineOfSightValidationTests
{
    private static readonly GridPosition ActorPosition = new GridPosition(0, 0, 0);
    private static readonly GridPosition TargetCell = new GridPosition(3, 0, 0);
    private static readonly GridPosition SightBlocker = new GridPosition(1, 0, 0);

    [Test]
    public void ConeDeclarationFailsWhenSightBlockerIsBetweenActorAndTargetCell()
    {
        using (var battle = new TestBattle(includeSightBlocker: true))
        {
            var ability = CreateConeShot();

            try
            {
                var service = new ActionDeclarationService();
                var state = new CombatState(1, CombatPhase.ActiveTurn, battle.Actor);

                var result = service.DeclareAction(
                    battle.Actor,
                    ability,
                    ActionTarget.ForCell(TargetCell),
                    state,
                    battle.Map);

                Assert.That(result.IsFailure, Is.True);
                Assert.That(result.ErrorMessage, Does.Contain("requires line of sight"));
                Assert.That(result.ErrorMessage, Does.Contain(SightBlocker.ToString()));
                Assert.That(battle.Actor.CurrentAP, Is.EqualTo(battle.Actor.MaxAP));
            }
            finally
            {
                Destroy(ability);
            }
        }
    }

    [Test]
    public void RadiusDeclarationFailsWhenSightBlockerIsBetweenActorAndTargetCell()
    {
        using (var battle = new TestBattle(includeSightBlocker: true))
        {
            var ability = CreateFireball();

            try
            {
                var service = new ActionDeclarationService();
                var state = new CombatState(1, CombatPhase.ActiveTurn, battle.Actor);

                var result = service.DeclareAction(
                    battle.Actor,
                    ability,
                    ActionTarget.ForCell(TargetCell),
                    state,
                    battle.Map);

                Assert.That(result.IsFailure, Is.True);
                Assert.That(result.ErrorMessage, Does.Contain("requires line of sight"));
                Assert.That(result.ErrorMessage, Does.Contain(SightBlocker.ToString()));
                Assert.That(battle.Actor.CurrentAP, Is.EqualTo(battle.Actor.MaxAP));
            }
            finally
            {
                Destroy(ability);
            }
        }
    }

    [Test]
    public void RangedPreviewReportsBlockedLineOfSightReasonWithoutSpendingAP()
    {
        using (var battle = new TestBattle(includeSightBlocker: true))
        {
            var ability = CreateFireball();

            try
            {
                var service = new IntentPreviewService();
                var state = new CombatState(1, CombatPhase.ActiveTurn, battle.Actor);
                var startingAP = battle.Actor.CurrentAP;

                var preview = service.CreatePreview(
                    battle.Actor,
                    ability,
                    ActionTarget.ForCell(TargetCell),
                    state,
                    battle.Map);

                Assert.That(preview.IsValid, Is.False);
                Assert.That(preview.InvalidReason, Does.Contain("requires line of sight"));
                Assert.That(preview.InvalidReason, Does.Contain(SightBlocker.ToString()));
                Assert.That(battle.Actor.CurrentAP, Is.EqualTo(startingAP));
            }
            finally
            {
                Destroy(ability);
            }
        }
    }

    [Test]
    public void ClearLineOfSightAllowsRangedDeclarations()
    {
        using (var battle = new TestBattle(includeSightBlocker: false))
        {
            var ability = CreateFireball();

            try
            {
                var service = new ActionDeclarationService();
                var state = new CombatState(1, CombatPhase.ActiveTurn, battle.Actor);

                var result = service.DeclareAction(
                    battle.Actor,
                    ability,
                    ActionTarget.ForCell(TargetCell),
                    state,
                    battle.Map);

                Assert.That(result.IsSuccess, Is.True, result.ErrorMessage);
                Assert.That(battle.Actor.CurrentAP, Is.EqualTo(battle.Actor.MaxAP - ability.APCost));
            }
            finally
            {
                Destroy(ability);
            }
        }
    }

    [Test]
    public void AbilityCanOptOutOfLineOfSightValidation()
    {
        using (var battle = new TestBattle(includeSightBlocker: true))
        {
            var ability = CreateFireball(ignoresLineOfSight: true);

            try
            {
                var service = new ActionDeclarationService();
                var state = new CombatState(1, CombatPhase.ActiveTurn, battle.Actor);

                var result = service.DeclareAction(
                    battle.Actor,
                    ability,
                    ActionTarget.ForCell(TargetCell),
                    state,
                    battle.Map);

                Assert.That(result.IsSuccess, Is.True, result.ErrorMessage);
                Assert.That(ability.IgnoresLineOfSight, Is.True);
                Assert.That(battle.Actor.CurrentAP, Is.EqualTo(battle.Actor.MaxAP - ability.APCost));
            }
            finally
            {
                Destroy(ability);
            }
        }
    }

    private static AbilityDefinition CreateConeShot(bool ignoresLineOfSight = false)
    {
        return CreateRangedAbility(
            "cone_shot",
            "Cone Shot",
            AbilityShape.Cone,
            apCost: 4,
            radius: 0,
            ignoresLineOfSight: ignoresLineOfSight);
    }

    private static AbilityDefinition CreateFireball(bool ignoresLineOfSight = false)
    {
        return CreateRangedAbility(
            "fireball",
            "Fireball",
            AbilityShape.Radius,
            apCost: 5,
            radius: 1,
            ignoresLineOfSight: ignoresLineOfSight);
    }

    private static AbilityDefinition CreateRangedAbility(
        string abilityKey,
        string displayName,
        AbilityShape shape,
        int apCost,
        int radius,
        bool ignoresLineOfSight)
    {
        var ability = ScriptableObject.CreateInstance<AbilityDefinition>();
        ability.Configure(
            abilityKey,
            displayName,
            apCost,
            AbilityUsage.Action,
            AbilityTiming.Telegraphed,
            shape,
            range: 4,
            radius: radius,
            damage: 3,
            triggersReactions: true,
            description: "Ranged line-of-sight validation test ability.",
            ignoresLineOfSight: ignoresLineOfSight);
        return ability;
    }

    private static UnitStatsDefinition CreateStats()
    {
        var stats = ScriptableObject.CreateInstance<UnitStatsDefinition>();
        stats.Configure(
            "Ranged LoS Actor",
            maxHP: 10,
            maxAP: 6,
            movementAnimationSpeed: 4f,
            meleeRange: 1,
            teamColorHint: Color.white);
        return stats;
    }

    private static GridMap CreateMap(bool includeSightBlocker)
    {
        var cells = new List<GridCell>();
        for (var x = 0; x <= 4; x += 1)
        {
            var position = new GridPosition(x, 0, 0);
            cells.Add(new GridCell(position, blocksLineOfSight: includeSightBlocker && position == SightBlocker));
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
        private readonly UnitStatsDefinition actorStats;

        public TestBattle(bool includeSightBlocker)
        {
            Map = CreateMap(includeSightBlocker);
            actorStats = CreateStats();
            actorObject = new GameObject("Ranged LoS Actor");
            Actor = actorObject.AddComponent<TacticalUnit>();
            Actor.Initialize(new UnitId(1), TeamId.Player, actorStats, ActorPosition);
        }

        public TacticalUnit Actor { get; }

        public GridMap Map { get; }

        public void Dispose()
        {
            Destroy(actorObject);
            Destroy(actorStats);
        }
    }
}
