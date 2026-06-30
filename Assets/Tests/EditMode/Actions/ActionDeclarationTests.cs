using System;
using System.Collections.Generic;
using NUnit.Framework;
using ReactionTactics.Actions;
using ReactionTactics.Grid;
using ReactionTactics.Turns;
using ReactionTactics.Units;
using UnityEngine;

namespace ReactionTactics.Tests.EditMode.Actions
{
    public sealed class ActionDeclarationTests
    {
        [Test]
        public void LegalActionSpendsAPAndReturnsIntent()
        {
            using (var battle = new TestBattle())
            {
                var ability = CreateAbility("melee_slash", "Melee Slash", apCost: 3);

                try
                {
                    var state = new CombatState(2, CombatPhase.ActiveTurn, battle.Actor);
                    var service = new ActionDeclarationService();
                    var target = ActionTarget.ForUnit(battle.Enemy);
                    var affectedCells = new[] { battle.Enemy.CurrentGridPosition };

                    var result = service.DeclareAction(
                        battle.Actor,
                        ability,
                        target,
                        state,
                        battle.Map,
                        affectedCells);

                    Assert.That(result.IsSuccess, Is.True, result.ErrorMessage);
                    Assert.That(battle.Actor.CurrentAP, Is.EqualTo(battle.Actor.MaxAP - ability.APCost));

                    var intent = result.Value;
                    Assert.That(intent.Actor, Is.SameAs(battle.Actor));
                    Assert.That(intent.Ability, Is.SameAs(ability));
                    Assert.That(intent.Target.TargetUnit, Is.SameAs(battle.Enemy));
                    Assert.That(intent.OriginPosition, Is.EqualTo(battle.ActorPosition));
                    Assert.That(intent.ActorPositionAtDeclaration, Is.EqualTo(battle.ActorPosition));
                    Assert.That(intent.DeclaredTargetUnit, Is.SameAs(battle.Enemy));
                    Assert.That(intent.DeclaredAffectedCells, Is.EqualTo(affectedCells));
                    Assert.That(intent.DeclarationRound, Is.EqualTo(2));
                    Assert.That(intent.DeclarationSequence, Is.EqualTo(0));
                    Assert.That(service.NextDeclarationSequence, Is.EqualTo(1));
                }
                finally
                {
                    Destroy(ability);
                }
            }
        }

        [Test]
        public void IllegalActionDoesNotSpendAPOrCreateIntent()
        {
            using (var battle = new TestBattle())
            {
                var ability = CreateAbility("melee_slash", "Melee Slash", apCost: 3);

                try
                {
                    battle.Actor.SetAPForTest(2);
                    var state = new CombatState(1, CombatPhase.ActiveTurn, battle.Actor);
                    var service = new ActionDeclarationService();

                    var result = service.DeclareAction(
                        battle.Actor,
                        ability,
                        ActionTarget.ForUnit(battle.Enemy),
                        state,
                        battle.Map);

                    Assert.That(result.IsFailure, Is.True);
                    Assert.That(result.ErrorMessage, Does.Contain("needs 3 AP"));
                    Assert.That(battle.Actor.CurrentAP, Is.EqualTo(2));
                    Assert.That(service.NextDeclarationSequence, Is.EqualTo(0));
                }
                finally
                {
                    Destroy(ability);
                }
            }
        }

        [Test]
        public void ReactionOnlyAbilityCannotBeDeclaredAsActiveAction()
        {
            using (var battle = new TestBattle())
            {
                var ability = CreateAbility(
                    "brace",
                    "Brace",
                    usage: AbilityUsage.Reaction,
                    timing: AbilityTiming.Immediate,
                    shape: AbilityShape.Self,
                    apCost: 2,
                    damage: 0,
                    triggersReactions: false);

                try
                {
                    var state = new CombatState(1, CombatPhase.ActiveTurn, battle.Actor);
                    var service = new ActionDeclarationService();

                    var result = service.DeclareAction(
                        battle.Actor,
                        ability,
                        ActionTarget.None,
                        state,
                        battle.Map);

                    Assert.That(result.IsFailure, Is.True);
                    Assert.That(result.ErrorMessage, Does.Contain("cannot be used as"));
                    Assert.That(battle.Actor.CurrentAP, Is.EqualTo(battle.Actor.MaxAP));
                    Assert.That(service.NextDeclarationSequence, Is.EqualTo(0));
                }
                finally
                {
                    Destroy(ability);
                }
            }
        }

        [Test]
        public void NonActiveUnitCannotDeclareActionAndDoesNotSpendAP()
        {
            using (var battle = new TestBattle())
            {
                var ability = CreateAbility("melee_slash", "Melee Slash", apCost: 3);

                try
                {
                    var state = new CombatState(1, CombatPhase.ActiveTurn, battle.Ally);
                    var service = new ActionDeclarationService();

                    var result = service.DeclareAction(
                        battle.Actor,
                        ability,
                        ActionTarget.ForUnit(battle.Enemy),
                        state,
                        battle.Map);

                    Assert.That(result.IsFailure, Is.True);
                    Assert.That(result.ErrorMessage, Does.Contain("active unit"));
                    Assert.That(battle.Actor.CurrentAP, Is.EqualTo(battle.Actor.MaxAP));
                    Assert.That(service.NextDeclarationSequence, Is.EqualTo(0));
                }
                finally
                {
                    Destroy(ability);
                }
            }
        }

        [Test]
        public void SuccessfulDeclarationsUseDeterministicSequences()
        {
            using (var battle = new TestBattle())
            {
                var ability = CreateAbility("quick_slash", "Quick Slash", apCost: 1);

                try
                {
                    var state = new CombatState(3, CombatPhase.ActiveTurn, battle.Actor);
                    var service = new ActionDeclarationService();

                    var first = service.DeclareAction(
                        battle.Actor,
                        ability,
                        ActionTarget.ForUnit(battle.Enemy),
                        state,
                        battle.Map);
                    var second = service.DeclareAction(
                        battle.Actor,
                        ability,
                        ActionTarget.ForUnit(battle.Enemy),
                        state,
                        battle.Map);

                    Assert.That(first.IsSuccess, Is.True, first.ErrorMessage);
                    Assert.That(second.IsSuccess, Is.True, second.ErrorMessage);
                    Assert.That(first.Value.DeclarationSequence, Is.EqualTo(0));
                    Assert.That(second.Value.DeclarationSequence, Is.EqualTo(1));
                    Assert.That(service.NextDeclarationSequence, Is.EqualTo(2));
                    Assert.That(battle.Actor.CurrentAP, Is.EqualTo(battle.Actor.MaxAP - 2));
                }
                finally
                {
                    Destroy(ability);
                }
            }
        }

        private static AbilityDefinition CreateAbility(
            string abilityKey,
            string displayName,
            AbilityUsage usage = AbilityUsage.Action,
            AbilityTiming timing = AbilityTiming.Telegraphed,
            AbilityShape shape = AbilityShape.Melee,
            int apCost = 3,
            int range = 0,
            int radius = 0,
            int damage = 4,
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
            for (var x = 0; x <= 3; x += 1)
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

                actorObject = new GameObject("Action Declaration Actor");
                enemyObject = new GameObject("Action Declaration Enemy");
                allyObject = new GameObject("Action Declaration Ally");

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
