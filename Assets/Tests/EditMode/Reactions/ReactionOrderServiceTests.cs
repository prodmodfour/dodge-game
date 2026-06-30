using System;
using System.Linq;
using NUnit.Framework;
using ReactionTactics.Actions;
using ReactionTactics.Grid;
using ReactionTactics.Reactions;
using ReactionTactics.Units;
using UnityEngine;

public sealed class ReactionOrderServiceTests
{
    [Test]
    public void BuildReactionOrderIncludesEveryOtherLivingUnitByDeclarationDistance()
    {
        using (var fixture = new ReactionOrderFixture())
        {
            var actor = fixture.CreateUnit("Actor", new UnitId(1), TeamId.Player, new GridPosition(2, 0, 2));
            var closeEnemy = fixture.CreateUnit("Close Enemy", new UnitId(4), TeamId.Enemy, new GridPosition(3, 0, 2));
            var verticalAlly = fixture.CreateUnit("Vertical Ally", new UnitId(2), TeamId.Player, new GridPosition(2, 2, 2));
            var farEnemy = fixture.CreateUnit("Far Enemy", new UnitId(3), TeamId.Enemy, new GridPosition(6, 0, 2));
            var deadUnit = fixture.CreateUnit("Dead Unit", new UnitId(5), TeamId.Enemy, new GridPosition(2, 0, 3));
            Assert.That(deadUnit.ApplyDamage(deadUnit.MaxHP, DamageSource.Environmental("Reaction order test")).IsSuccess, Is.True);

            var intent = fixture.CreateIntent(actor);
            actor.SetGridPosition(new GridPosition(10, 0, 10));
            var service = new ReactionOrderService();

            var order = service.BuildReactionOrder(
                actor,
                new[] { farEnemy, null, actor, deadUnit, verticalAlly, closeEnemy },
                intent);

            Assert.That(order.ToArray(), Is.EqualTo(new[] { closeEnemy, verticalAlly, farEnemy }));
            Assert.That(service.GetReactionDistance(closeEnemy, intent), Is.EqualTo(1));
            Assert.That(service.GetReactionDistance(verticalAlly, intent), Is.EqualTo(2));
            Assert.That(service.GetReactionDistance(farEnemy, intent), Is.EqualTo(4));
        }
    }

    [Test]
    public void BuildReactionOrderUsesUnitIdTieBreakerForEqualDistances()
    {
        using (var fixture = new ReactionOrderFixture())
        {
            var actor = fixture.CreateUnit("Actor", new UnitId(10), TeamId.Player, GridPosition.Zero);
            var highId = fixture.CreateUnit("High Id", new UnitId(9), TeamId.Enemy, new GridPosition(2, 0, 0));
            var lowId = fixture.CreateUnit("Low Id", new UnitId(2), TeamId.Enemy, new GridPosition(0, 0, 2));
            var middleId = fixture.CreateUnit("Middle Id", new UnitId(5), TeamId.Player, new GridPosition(-2, 0, 0));
            var intent = fixture.CreateIntent(actor);

            var order = new ReactionOrderService().BuildReactionOrder(
                actor,
                new[] { highId, actor, lowId, middleId },
                intent);

            Assert.That(order.ToArray(), Is.EqualTo(new[] { lowId, middleId, highId }));
        }
    }

    [Test]
    public void BuildReactionOrderUsesConfiguredTacticalVerticalWeight()
    {
        using (var fixture = new ReactionOrderFixture())
        {
            var actor = fixture.CreateUnit("Actor", new UnitId(1), TeamId.Player, GridPosition.Zero);
            var verticalUnit = fixture.CreateUnit("Vertical Unit", new UnitId(2), TeamId.Enemy, new GridPosition(0, 1, 1));
            var flatUnit = fixture.CreateUnit("Flat Unit", new UnitId(3), TeamId.Enemy, new GridPosition(2, 0, 0));
            var intent = fixture.CreateIntent(actor);

            var zeroWeightOrder = new ReactionOrderService(verticalWeight: 0)
                .BuildReactionOrder(actor, new[] { flatUnit, verticalUnit }, intent);
            var heavyVerticalOrder = new ReactionOrderService(verticalWeight: 2)
                .BuildReactionOrder(actor, new[] { flatUnit, verticalUnit }, intent);

            Assert.That(zeroWeightOrder.ToArray(), Is.EqualTo(new[] { verticalUnit, flatUnit }));
            Assert.That(heavyVerticalOrder.ToArray(), Is.EqualTo(new[] { flatUnit, verticalUnit }));
        }
    }

    [Test]
    public void BuildReactionOrderRejectsInvalidInputsAndMismatchedActor()
    {
        using (var fixture = new ReactionOrderFixture())
        {
            var actor = fixture.CreateUnit("Actor", new UnitId(1), TeamId.Player, GridPosition.Zero);
            var otherActor = fixture.CreateUnit("Other Actor", new UnitId(2), TeamId.Player, new GridPosition(1, 0, 0));
            var intent = fixture.CreateIntent(actor);
            var service = new ReactionOrderService();

            Assert.Throws<ArgumentOutOfRangeException>(() => new ReactionOrderService(verticalWeight: -1));
            Assert.Throws<ArgumentNullException>(() => service.BuildReactionOrder(null, Array.Empty<TacticalUnit>(), intent));
            Assert.Throws<ArgumentNullException>(() => service.BuildReactionOrder(actor, null, intent));
            Assert.Throws<ArgumentNullException>(() => service.BuildReactionOrder(actor, Array.Empty<TacticalUnit>(), null));
            Assert.Throws<ArgumentNullException>(() => service.BuildReactionOrder(null, Array.Empty<TacticalUnit>()));
            Assert.Throws<ArgumentNullException>(() => service.GetReactionDistance(null, intent));
            Assert.Throws<ArgumentNullException>(() => service.GetReactionDistance(actor, null));
            Assert.Throws<ArgumentException>(() => service.BuildReactionOrder(otherActor, Array.Empty<TacticalUnit>(), intent));
        }
    }

    private sealed class ReactionOrderFixture : IDisposable
    {
        private readonly System.Collections.Generic.List<GameObject> unitObjects =
            new System.Collections.Generic.List<GameObject>();
        private readonly UnitStatsDefinition stats;
        private readonly AbilityDefinition ability;

        public ReactionOrderFixture()
        {
            stats = ScriptableObject.CreateInstance<UnitStatsDefinition>();
            stats.Configure(
                displayName: "Reaction Order Test Unit",
                maxHP: 10,
                maxAP: 6,
                movementAnimationSpeed: 4f,
                meleeRange: 1,
                teamColorHint: Color.white);

            ability = ScriptableObject.CreateInstance<AbilityDefinition>();
            ability.Configure(
                "melee_slash",
                "Melee Slash",
                apCost: 3,
                usage: AbilityUsage.Action,
                timing: AbilityTiming.Telegraphed,
                shape: AbilityShape.Melee,
                range: 1,
                radius: 0,
                damage: 4,
                triggersReactions: true);
        }

        public TacticalUnit CreateUnit(string name, UnitId unitId, TeamId team, GridPosition position)
        {
            var gameObject = new GameObject(name);
            unitObjects.Add(gameObject);
            var unit = gameObject.AddComponent<TacticalUnit>();
            unit.Initialize(unitId, team, stats, position);
            return unit;
        }

        public ActionIntent CreateIntent(TacticalUnit actor)
        {
            return new ActionIntent(
                actor,
                ability,
                actor.CurrentGridPosition,
                ActionTarget.None,
                declaredAffectedCells: null,
                declarationRound: 1,
                declarationSequence: 0);
        }

        public void Dispose()
        {
            for (var i = unitObjects.Count - 1; i >= 0; i -= 1)
            {
                UnityEngine.Object.DestroyImmediate(unitObjects[i]);
            }

            UnityEngine.Object.DestroyImmediate(ability);
            UnityEngine.Object.DestroyImmediate(stats);
        }
    }
}
