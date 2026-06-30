using System;
using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using ReactionTactics.Actions;
using ReactionTactics.Grid;
using ReactionTactics.Turns;
using ReactionTactics.UI;
using ReactionTactics.Units;
using UnityEngine;

namespace ReactionTactics.Tests.EditMode.UI
{
    public sealed class IntentPreviewTests
    {
        [Test]
        public void MeleePreviewThreatensTargetAndDoesNotSpendAP()
        {
            using (var battle = new TestBattle())
            {
                var ability = CreateAbility("melee_slash", "Melee Slash", AbilityShape.Melee, apCost: 3);

                try
                {
                    var startingAP = battle.Actor.CurrentAP;
                    var service = new IntentPreviewService();
                    var state = new CombatState(1, CombatPhase.ActiveTurn, battle.Actor);

                    var preview = service.CreatePreview(
                        battle.Actor,
                        ability,
                        ActionTarget.ForUnit(battle.Enemy),
                        state,
                        battle.Map,
                        new[] { battle.Actor, battle.Enemy, battle.Ally });

                    Assert.That(preview.IsValid, Is.True, preview.InvalidReason);
                    Assert.That(preview.Actor, Is.SameAs(battle.Actor));
                    Assert.That(preview.Ability, Is.SameAs(ability));
                    Assert.That(preview.Target.TargetUnit, Is.SameAs(battle.Enemy));
                    Assert.That(preview.AffectedCells, Is.EqualTo(new[] { battle.EnemyPosition }));
                    Assert.That(preview.ThreatenedUnits, Is.EqualTo(new[] { battle.Enemy }));
                    Assert.That(preview.SafeCells, Is.Empty);
                    Assert.That(battle.Actor.CurrentAP, Is.EqualTo(startingAP));
                }
                finally
                {
                    Destroy(ability);
                }
            }
        }

        [Test]
        public void InvalidPreviewKeepsReasonAndDoesNotSpendAP()
        {
            using (var battle = new TestBattle())
            {
                var ability = CreateAbility("heavy_slash", "Heavy Slash", AbilityShape.Melee, apCost: 3);

                try
                {
                    battle.Actor.SetAPForTest(2);
                    var service = new IntentPreviewService();
                    var state = new CombatState(1, CombatPhase.ActiveTurn, battle.Actor);

                    var preview = service.CreatePreview(
                        battle.Actor,
                        ability,
                        ActionTarget.ForUnit(battle.Enemy),
                        state,
                        battle.Map);

                    Assert.That(preview.IsInvalid, Is.True);
                    Assert.That(preview.InvalidReason, Does.Contain("needs 3 AP"));
                    Assert.That(preview.AffectedCells, Is.Empty);
                    Assert.That(preview.ThreatenedUnits, Is.Empty);
                    Assert.That(preview.SafeCells, Is.Empty);
                    Assert.That(battle.Actor.CurrentAP, Is.EqualTo(2));
                }
                finally
                {
                    Destroy(ability);
                }
            }
        }

        [Test]
        public void RadiusPreviewReturnsAffectedCellsWithoutSpendingAP()
        {
            using (var battle = new TestBattle())
            {
                var ability = CreateAbility(
                    "fireball",
                    "Fireball",
                    AbilityShape.Radius,
                    apCost: 5,
                    range: 5,
                    radius: 1,
                    damage: 4);

                try
                {
                    var targetCell = new GridPosition(2, 0, 1);
                    var startingAP = battle.Actor.CurrentAP;
                    var service = new IntentPreviewService();
                    var state = new CombatState(1, CombatPhase.ActiveTurn, battle.Actor);

                    var preview = service.CreatePreview(
                        battle.Actor,
                        ability,
                        ActionTarget.ForCell(targetCell),
                        state,
                        battle.Map,
                        new[] { battle.Actor, battle.Enemy, battle.Ally });

                    Assert.That(preview.IsValid, Is.True, preview.InvalidReason);
                    Assert.That(preview.Target.TargetCell, Is.EqualTo(targetCell));
                    Assert.That(preview.AffectedCells, Is.EqualTo(new[]
                    {
                        targetCell,
                        new GridPosition(2, 0, 0),
                        new GridPosition(1, 0, 1),
                        new GridPosition(3, 0, 1),
                        new GridPosition(2, 0, 2),
                    }));
                    Assert.That(preview.ThreatenedUnits, Is.Empty);
                    Assert.That(battle.Actor.CurrentAP, Is.EqualTo(startingAP));
                }
                finally
                {
                    Destroy(ability);
                }
            }
        }

        [Test]
        public void PreviewCopiesCollectionsAndPreservesOptionalSafeCells()
        {
            using (var battle = new TestBattle())
            {
                var ability = CreateAbility("melee_slash", "Melee Slash", AbilityShape.Melee, apCost: 3);

                try
                {
                    var affectedCells = new List<GridPosition>
                    {
                        battle.EnemyPosition,
                        battle.EnemyPosition,
                        battle.AllyPosition
                    };
                    var safeCells = new List<GridPosition>
                    {
                        battle.ActorPosition,
                        battle.ActorPosition
                    };
                    var threatenedUnits = new List<TacticalUnit>
                    {
                        battle.Enemy,
                        battle.Enemy,
                        null
                    };

                    var preview = ActionIntentPreview.Valid(
                        battle.Actor,
                        ability,
                        ActionTarget.ForUnit(battle.Enemy),
                        affectedCells,
                        threatenedUnits,
                        safeCells);
                    affectedCells[0] = new GridPosition(9, 0, 9);
                    affectedCells.Add(new GridPosition(10, 0, 10));
                    threatenedUnits.Add(battle.Ally);
                    safeCells.Add(new GridPosition(11, 0, 11));

                    Assert.That(preview.IsValid, Is.True);
                    Assert.That(preview.AffectedCells.ToArray(), Is.EqualTo(new[] { battle.EnemyPosition, battle.AllyPosition }));
                    Assert.That(preview.ThreatenedUnits.ToArray(), Is.EqualTo(new[] { battle.Enemy }));
                    Assert.That(preview.SafeCells.ToArray(), Is.EqualTo(new[] { battle.ActorPosition }));
                    Assert.That(preview.HasAffectedCells, Is.True);
                    Assert.That(preview.HasThreatenedUnits, Is.True);
                    Assert.That(preview.HasSafeCells, Is.True);
                }
                finally
                {
                    Destroy(ability);
                }
            }
        }

        [Test]
        public void MissingPreviewContextReturnsInvalidPreview()
        {
            var service = new IntentPreviewService();

            var preview = service.CreatePreview(context: null);

            Assert.That(preview.IsInvalid, Is.True);
            Assert.That(preview.InvalidReason, Does.Contain("validation context is missing"));
            Assert.That(preview.Actor, Is.Null);
            Assert.That(preview.Ability, Is.Null);
        }

        private static AbilityDefinition CreateAbility(
            string abilityKey,
            string displayName,
            AbilityShape shape,
            int apCost,
            int range = 0,
            int radius = 0,
            int damage = 4,
            AbilityUsage usage = AbilityUsage.Action,
            AbilityTiming timing = AbilityTiming.Telegraphed,
            bool triggersReactions = true)
        {
            var ability = ScriptableObject.CreateInstance<AbilityDefinition>();
            ability.Configure(
                abilityKey,
                displayName,
                apCost,
                usage,
                timing,
                shape,
                range,
                radius,
                damage,
                triggersReactions);
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

            public TestBattle()
            {
                ActorPosition = new GridPosition(0, 0, 0);
                EnemyPosition = new GridPosition(1, 0, 0);
                AllyPosition = new GridPosition(0, 0, 1);
                Map = CreateMap();

                actorStats = CreateStats("Actor");
                enemyStats = CreateStats("Enemy");
                allyStats = CreateStats("Ally");

                actorObject = new GameObject("Intent Preview Actor");
                enemyObject = new GameObject("Intent Preview Enemy");
                allyObject = new GameObject("Intent Preview Ally");

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
}
