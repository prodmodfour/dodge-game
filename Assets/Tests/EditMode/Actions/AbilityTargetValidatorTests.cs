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
    public sealed class AbilityTargetValidatorTests
    {
        [Test]
        public void ActiveMeleeAgainstAdjacentHostileSucceedsWithoutSpendingAP()
        {
            var battle = new TestBattle();
            var ability = CreateAbility("melee_slash", "Melee Slash", shape: AbilityShape.Melee, apCost: 3);

            try
            {
                var state = new CombatState(1, CombatPhase.ActiveTurn, battle.Actor);
                var context = new AbilityTargetValidationContext(
                    battle.Actor,
                    ability,
                    ActionTarget.ForUnit(battle.Enemy),
                    AbilityUsage.Action,
                    state,
                    battle.Map);

                var result = AbilityTargetValidator.Instance.ValidateTarget(context);

                Assert.That(result.IsSuccess, Is.True, result.ErrorMessage);
                Assert.That(battle.Actor.CurrentAP, Is.EqualTo(battle.Actor.MaxAP));
            }
            finally
            {
                Destroy(ability);
                battle.Dispose();
            }
        }

        [Test]
        public void InsufficientAPFailsWithoutSpendingAP()
        {
            var battle = new TestBattle();
            var ability = CreateAbility("melee_slash", "Melee Slash", shape: AbilityShape.Melee, apCost: 3);

            try
            {
                battle.Actor.SetAPForTest(2);
                var state = new CombatState(1, CombatPhase.ActiveTurn, battle.Actor);
                var context = new AbilityTargetValidationContext(
                    battle.Actor,
                    ability,
                    ActionTarget.ForUnit(battle.Enemy),
                    AbilityUsage.Action,
                    state,
                    battle.Map);

                var result = AbilityTargetValidator.Instance.ValidateTarget(context);

                Assert.That(result.IsFailure, Is.True);
                Assert.That(result.ErrorMessage, Does.Contain("needs 3 AP"));
                Assert.That(result.ErrorMessage, Does.Contain("only has 2 AP"));
                Assert.That(battle.Actor.CurrentAP, Is.EqualTo(2));
            }
            finally
            {
                Destroy(ability);
                battle.Dispose();
            }
        }

        [Test]
        public void ActiveActionRequiresActiveUnitPhaseAndActor()
        {
            var battle = new TestBattle();
            var ability = CreateAbility("melee_slash", "Melee Slash", shape: AbilityShape.Melee);

            try
            {
                var reactionPhaseState = new CombatState(1, CombatPhase.ReactionWindow, battle.Enemy, battle.Actor);
                var wrongActorState = new CombatState(1, CombatPhase.ActiveTurn, battle.Enemy);

                var wrongPhaseResult = AbilityTargetValidator.Instance.ValidateTarget(new AbilityTargetValidationContext(
                    battle.Actor,
                    ability,
                    ActionTarget.ForUnit(battle.Enemy),
                    AbilityUsage.Action,
                    reactionPhaseState,
                    battle.Map));
                var wrongActorResult = AbilityTargetValidator.Instance.ValidateTarget(new AbilityTargetValidationContext(
                    battle.Actor,
                    ability,
                    ActionTarget.ForUnit(battle.Enemy),
                    AbilityUsage.Action,
                    wrongActorState,
                    battle.Map));

                Assert.That(wrongPhaseResult.IsFailure, Is.True);
                Assert.That(wrongPhaseResult.ErrorMessage, Does.Contain("active action"));
                Assert.That(wrongPhaseResult.ErrorMessage, Does.Contain(nameof(CombatPhase.ReactionWindow)));
                Assert.That(wrongActorResult.IsFailure, Is.True);
                Assert.That(wrongActorResult.ErrorMessage, Does.Contain("active unit"));
            }
            finally
            {
                Destroy(ability);
                battle.Dispose();
            }
        }

        [Test]
        public void MeleeRequiresHostileTargetByDefault()
        {
            var battle = new TestBattle();
            var ability = CreateAbility("melee_slash", "Melee Slash", shape: AbilityShape.Melee);

            try
            {
                var state = new CombatState(1, CombatPhase.ActiveTurn, battle.Actor);
                var context = new AbilityTargetValidationContext(
                    battle.Actor,
                    ability,
                    ActionTarget.ForUnit(battle.Ally),
                    AbilityUsage.Action,
                    state,
                    battle.Map);

                var result = AbilityTargetValidator.Instance.ValidateTarget(context);

                Assert.That(result.IsFailure, Is.True);
                Assert.That(result.ErrorMessage, Does.Contain("hostile target"));
                Assert.That(result.ErrorMessage, Does.Contain("friendly"));
            }
            finally
            {
                Destroy(ability);
                battle.Dispose();
            }
        }

        [Test]
        public void SingleTargetRangeUsesHorizontalDistance()
        {
            var battle = new TestBattle(enemyPosition: new GridPosition(3, 2, 0));
            var ability = CreateAbility(
                "bolt",
                "Bolt",
                shape: AbilityShape.SingleTarget,
                range: 2,
                damage: 2);

            try
            {
                var state = new CombatState(1, CombatPhase.ActiveTurn, battle.Actor);
                var context = new AbilityTargetValidationContext(
                    battle.Actor,
                    ability,
                    ActionTarget.ForUnit(battle.Enemy),
                    AbilityUsage.Action,
                    state,
                    battle.Map);

                var result = AbilityTargetValidator.Instance.ValidateTarget(context);

                Assert.That(result.IsFailure, Is.True);
                Assert.That(result.ErrorMessage, Does.Contain("beyond range 2"));
                Assert.That(result.ErrorMessage, Does.Contain("3 cells away"));
            }
            finally
            {
                Destroy(ability);
                battle.Dispose();
            }
        }

        [Test]
        public void ReactionAbilityRequiresCurrentReactingUnit()
        {
            var battle = new TestBattle();
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
                var validReactionState = new CombatState(
                    1,
                    CombatPhase.ReactionWindow,
                    activeUnit: battle.Enemy,
                    reactingUnit: battle.Actor);
                var wrongReactorState = new CombatState(
                    1,
                    CombatPhase.ReactionWindow,
                    activeUnit: battle.Enemy,
                    reactingUnit: battle.Ally);

                var validResult = AbilityTargetValidator.Instance.ValidateTarget(new AbilityTargetValidationContext(
                    battle.Actor,
                    ability,
                    ActionTarget.None,
                    AbilityUsage.Reaction,
                    validReactionState,
                    battle.Map));
                var wrongReactorResult = AbilityTargetValidator.Instance.ValidateTarget(new AbilityTargetValidationContext(
                    battle.Actor,
                    ability,
                    ActionTarget.None,
                    AbilityUsage.Reaction,
                    wrongReactorState,
                    battle.Map));

                Assert.That(validResult.IsSuccess, Is.True, validResult.ErrorMessage);
                Assert.That(wrongReactorResult.IsFailure, Is.True);
                Assert.That(wrongReactorResult.ErrorMessage, Does.Contain("reacting unit"));
            }
            finally
            {
                Destroy(ability);
                battle.Dispose();
            }
        }

        [Test]
        public void CellTargetRequiresMapCellAndCanRequireWalkability()
        {
            var blockedCell = new GridPosition(2, 0, 0);
            var battle = new TestBattle(map: CreateMap(new GridCell(
                blockedCell,
                walkable: false,
                blocksMovement: true,
                blocksLineOfSight: false,
                movementCost: 1)));
            var ability = CreateAbility(
                "fireball",
                "Fireball",
                shape: AbilityShape.Radius,
                range: 4,
                radius: 1);

            try
            {
                var state = new CombatState(1, CombatPhase.ActiveTurn, battle.Actor);
                var defaultContext = new AbilityTargetValidationContext(
                    battle.Actor,
                    ability,
                    ActionTarget.ForCell(blockedCell),
                    AbilityUsage.Action,
                    state,
                    battle.Map);
                var walkableContext = new AbilityTargetValidationContext(
                    battle.Actor,
                    ability,
                    ActionTarget.ForCell(blockedCell),
                    AbilityUsage.Action,
                    state,
                    battle.Map,
                    requireWalkableTargetCell: true);
                var missingCellContext = new AbilityTargetValidationContext(
                    battle.Actor,
                    ability,
                    ActionTarget.ForCell(new GridPosition(9, 0, 9)),
                    AbilityUsage.Action,
                    state,
                    battle.Map);

                var defaultResult = AbilityTargetValidator.Instance.ValidateTarget(defaultContext);
                var walkableResult = AbilityTargetValidator.Instance.ValidateTarget(walkableContext);
                var missingCellResult = AbilityTargetValidator.Instance.ValidateTarget(missingCellContext);

                Assert.That(defaultResult.IsSuccess, Is.True, defaultResult.ErrorMessage);
                Assert.That(walkableResult.IsFailure, Is.True);
                Assert.That(walkableResult.ErrorMessage, Does.Contain("not walkable"));
                Assert.That(missingCellResult.IsFailure, Is.True);
                Assert.That(missingCellResult.ErrorMessage, Does.Contain("not on the grid map"));
            }
            finally
            {
                Destroy(ability);
                battle.Dispose();
            }
        }

        [Test]
        public void ExplicitFriendlyRelationshipCanValidateSupportTargets()
        {
            var battle = new TestBattle();
            var ability = CreateAbility(
                "heal",
                "Heal",
                shape: AbilityShape.SingleTarget,
                range: 3,
                damage: 0,
                triggersReactions: false);

            try
            {
                var state = new CombatState(1, CombatPhase.ActiveTurn, battle.Actor);
                var friendlyContext = new AbilityTargetValidationContext(
                    battle.Actor,
                    ability,
                    ActionTarget.ForUnit(battle.Ally),
                    AbilityUsage.Action,
                    state,
                    battle.Map,
                    AbilityTargetRelationship.Friendly);
                var hostileContext = new AbilityTargetValidationContext(
                    battle.Actor,
                    ability,
                    ActionTarget.ForUnit(battle.Enemy),
                    AbilityUsage.Action,
                    state,
                    battle.Map,
                    AbilityTargetRelationship.Friendly);

                var friendlyResult = AbilityTargetValidator.Instance.ValidateTarget(friendlyContext);
                var hostileResult = AbilityTargetValidator.Instance.ValidateTarget(hostileContext);

                Assert.That(friendlyResult.IsSuccess, Is.True, friendlyResult.ErrorMessage);
                Assert.That(hostileResult.IsFailure, Is.True);
                Assert.That(hostileResult.ErrorMessage, Does.Contain("friendly target"));
            }
            finally
            {
                Destroy(ability);
                battle.Dispose();
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

        private static GridMap CreateMap(params GridCell[] overrides)
        {
            var cells = new Dictionary<GridPosition, GridCell>();
            for (var x = 0; x <= 5; x += 1)
            {
                for (var z = 0; z <= 2; z += 1)
                {
                    var position = new GridPosition(x, 0, z);
                    cells[position] = new GridCell(position);
                }
            }

            var elevatedEnemyCell = new GridPosition(3, 2, 0);
            cells[elevatedEnemyCell] = new GridCell(elevatedEnemyCell);

            if (overrides != null)
            {
                foreach (var cell in overrides)
                {
                    cells[cell.Position] = cell;
                }
            }

            return new GridMap(cells.Values);
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
                GridPosition? actorPosition = null,
                GridPosition? enemyPosition = null,
                GridPosition? allyPosition = null,
                GridMap map = null)
            {
                ActorPosition = actorPosition ?? new GridPosition(0, 0, 0);
                EnemyPosition = enemyPosition ?? new GridPosition(1, 0, 0);
                AllyPosition = allyPosition ?? new GridPosition(0, 0, 1);
                Map = map ?? CreateMap();

                actorStats = CreateStats("Actor");
                enemyStats = CreateStats("Enemy");
                allyStats = CreateStats("Ally");

                actorObject = new GameObject("Actor");
                enemyObject = new GameObject("Enemy");
                allyObject = new GameObject("Ally");

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
