using System;
using System.Collections.Generic;
using NUnit.Framework;
using ReactionTactics.Actions;
using ReactionTactics.Grid;
using ReactionTactics.Units;
using UnityEngine;

namespace ReactionTactics.Tests.EditMode.Actions
{
    public sealed class ActionIntentTests
    {
        [Test]
        public void IntentCapturesDeclarationDataAndDerivedAbilityFlags()
        {
            var actorObject = new GameObject("Intent Actor");
            var targetObject = new GameObject("Intent Target");
            var actorStats = CreateStats("Actor");
            var targetStats = CreateStats("Target");
            var ability = CreateAbility(
                abilityKey: "melee_slash",
                displayName: "Melee Slash",
                timing: AbilityTiming.Telegraphed,
                shape: AbilityShape.Melee,
                triggersReactions: true);

            try
            {
                var actor = actorObject.AddComponent<TacticalUnit>();
                var targetUnit = targetObject.AddComponent<TacticalUnit>();
                var actorPosition = new GridPosition(1, 0, 2);
                var targetPosition = new GridPosition(2, 0, 2);
                actor.Initialize(new UnitId(11), TeamId.Player, actorStats, actorPosition);
                targetUnit.Initialize(new UnitId(22), TeamId.Enemy, targetStats, targetPosition);

                var target = ActionTarget.ForUnit(targetUnit);
                var affectedCells = new[] { targetPosition };

                var intent = new ActionIntent(
                    actor,
                    ability,
                    actorPosition,
                    target,
                    affectedCells,
                    declarationRound: 3,
                    declarationSequence: 7);
                actor.SetGridPosition(new GridPosition(5, 0, 5));
                targetUnit.SetGridPosition(new GridPosition(6, 0, 5));

                Assert.That(intent.Actor, Is.SameAs(actor));
                Assert.That(intent.Ability, Is.SameAs(ability));
                Assert.That(intent.OriginPosition, Is.EqualTo(actorPosition));
                Assert.That(intent.ActorPositionAtDeclaration, Is.EqualTo(actorPosition));
                Assert.That(intent.Target.TargetUnit, Is.SameAs(targetUnit));
                Assert.That(intent.Target.TargetCell, Is.EqualTo(targetPosition));
                Assert.That(intent.DeclaredTargetUnit, Is.SameAs(targetUnit));
                Assert.That(intent.HasDeclaredTargetUnit, Is.True);
                Assert.That(intent.DeclaredAffectedCells, Is.EqualTo(affectedCells));
                Assert.That(intent.DeclarationRound, Is.EqualTo(3));
                Assert.That(intent.DeclarationSequence, Is.EqualTo(7));
                Assert.That(intent.TriggersReactionWindow, Is.True);
                Assert.That(intent.IsTelegraphed, Is.True);
                Assert.That(intent.Actor.CurrentGridPosition, Is.Not.EqualTo(intent.ActorPositionAtDeclaration));
                Assert.That(intent.DeclaredTargetUnit.CurrentGridPosition, Is.Not.EqualTo(intent.Target.TargetCell));
            }
            finally
            {
                Destroy(ability);
                Destroy(targetStats);
                Destroy(actorStats);
                UnityEngine.Object.DestroyImmediate(targetObject);
                UnityEngine.Object.DestroyImmediate(actorObject);
            }
        }

        [Test]
        public void DeclaredAffectedCellsAreCopiedAtConstruction()
        {
            var actorObject = new GameObject("Intent Copy Actor");
            var actorStats = CreateStats("Copy Actor");
            var ability = CreateAbility(
                abilityKey: "fireball",
                displayName: "Fireball",
                timing: AbilityTiming.Telegraphed,
                shape: AbilityShape.Radius,
                range: 5,
                radius: 1,
                triggersReactions: true);

            try
            {
                var actor = actorObject.AddComponent<TacticalUnit>();
                actor.Initialize(new UnitId(33), TeamId.Player, actorStats, GridPosition.Zero);
                var cells = new List<GridPosition>
                {
                    new GridPosition(1, 0, 1),
                    new GridPosition(1, 0, 2)
                };

                var intent = new ActionIntent(
                    actor,
                    ability,
                    actor.CurrentGridPosition,
                    ActionTarget.ForCell(new GridPosition(1, 0, 1)),
                    cells,
                    declarationRound: 1,
                    declarationSequence: 2);
                cells[0] = new GridPosition(9, 0, 9);
                cells.Add(new GridPosition(10, 0, 10));

                Assert.That(intent.DeclaredAffectedCells, Has.Count.EqualTo(2));
                Assert.That(intent.DeclaredAffectedCells[0], Is.EqualTo(new GridPosition(1, 0, 1)));
                Assert.That(intent.DeclaredAffectedCells[1], Is.EqualTo(new GridPosition(1, 0, 2)));
            }
            finally
            {
                Destroy(ability);
                Destroy(actorStats);
                UnityEngine.Object.DestroyImmediate(actorObject);
            }
        }

        [Test]
        public void ImmediateNonReactiveAbilityDerivesFalseFlags()
        {
            var actorObject = new GameObject("Immediate Intent Actor");
            var actorStats = CreateStats("Immediate Actor");
            var ability = CreateAbility(
                abilityKey: "brace",
                displayName: "Brace",
                usage: AbilityUsage.Reaction,
                timing: AbilityTiming.Immediate,
                shape: AbilityShape.Self,
                triggersReactions: false);

            try
            {
                var actor = actorObject.AddComponent<TacticalUnit>();
                actor.Initialize(new UnitId(44), TeamId.Player, actorStats, GridPosition.Zero);

                var intent = new ActionIntent(
                    actor,
                    ability,
                    actor.CurrentGridPosition,
                    ActionTarget.None,
                    declaredAffectedCells: null,
                    declarationRound: 1,
                    declarationSequence: 0);

                Assert.That(intent.DeclaredAffectedCells, Is.Empty);
                Assert.That(intent.HasDeclaredTargetUnit, Is.False);
                Assert.That(intent.TriggersReactionWindow, Is.False);
                Assert.That(intent.IsTelegraphed, Is.False);
            }
            finally
            {
                Destroy(ability);
                Destroy(actorStats);
                UnityEngine.Object.DestroyImmediate(actorObject);
            }
        }

        [Test]
        public void ConstructorRejectsMissingRequiredDataAndInvalidDeclarationNumbers()
        {
            var actorObject = new GameObject("Invalid Intent Actor");
            var actorStats = CreateStats("Invalid Actor");
            var ability = CreateAbility("move", "Move", AbilityUsage.Both, AbilityTiming.Immediate, AbilityShape.Self, false);

            try
            {
                var actor = actorObject.AddComponent<TacticalUnit>();
                actor.Initialize(new UnitId(55), TeamId.Player, actorStats, GridPosition.Zero);

                Assert.Throws<ArgumentNullException>(() => new ActionIntent(null, ability, GridPosition.Zero, ActionTarget.None, null, 1, 1));
                Assert.Throws<ArgumentNullException>(() => new ActionIntent(actor, null, GridPosition.Zero, ActionTarget.None, null, 1, 1));
                Assert.Throws<ArgumentOutOfRangeException>(() => new ActionIntent(actor, ability, GridPosition.Zero, ActionTarget.None, null, -1, 1));
                Assert.Throws<ArgumentOutOfRangeException>(() => new ActionIntent(actor, ability, GridPosition.Zero, ActionTarget.None, null, 1, -1));
            }
            finally
            {
                Destroy(ability);
                Destroy(actorStats);
                UnityEngine.Object.DestroyImmediate(actorObject);
            }
        }

        private static UnitStatsDefinition CreateStats(string displayName)
        {
            var stats = ScriptableObject.CreateInstance<UnitStatsDefinition>();
            stats.Configure(
                displayName: displayName,
                maxHP: 10,
                maxAP: 6,
                movementAnimationSpeed: 4f,
                meleeRange: 1,
                teamColorHint: Color.white);
            return stats;
        }

        private static AbilityDefinition CreateAbility(
            string abilityKey,
            string displayName,
            AbilityUsage usage = AbilityUsage.Action,
            AbilityTiming timing = AbilityTiming.Telegraphed,
            AbilityShape shape = AbilityShape.Melee,
            bool triggersReactions = true,
            int range = 0,
            int radius = 0,
            int damage = 4)
        {
            var ability = ScriptableObject.CreateInstance<AbilityDefinition>();
            ability.Configure(
                abilityKey,
                displayName,
                apCost: 3,
                usage,
                timing,
                shape,
                range,
                radius,
                damage,
                triggersReactions);
            return ability;
        }

        private static void Destroy(ScriptableObject asset)
        {
            if (asset != null)
            {
                UnityEngine.Object.DestroyImmediate(asset);
            }
        }
    }
}
