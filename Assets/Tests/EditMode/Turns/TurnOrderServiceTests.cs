using System;
using System.Linq;
using NUnit.Framework;
using ReactionTactics.Grid;
using ReactionTactics.Turns;
using ReactionTactics.Units;
using UnityEngine;

public sealed class TurnOrderServiceTests
{
    [Test]
    public void BuildTurnOrderSortsLivingUnitsByTeamThenUnitId()
    {
        var fixture = new UnitFixture();

        try
        {
            var enemyEarlyId = fixture.CreateUnit("Enemy Early", new UnitId(1), TeamId.Enemy, 0);
            var playerLaterId = fixture.CreateUnit("Player Later", new UnitId(5), TeamId.Player, 1);
            var deadPlayer = fixture.CreateUnit("Dead Player", new UnitId(2), TeamId.Player, 2);
            var enemyLaterId = fixture.CreateUnit("Enemy Later", new UnitId(4), TeamId.Enemy, 3);
            var playerEarlyId = fixture.CreateUnit("Player Early", new UnitId(3), TeamId.Player, 4);
            Assert.That(deadPlayer.ApplyDamage(deadPlayer.MaxHP, DamageSource.Environmental("Turn order test")).IsSuccess, Is.True);

            var order = TurnOrderService.BuildTurnOrder(new[]
            {
                enemyEarlyId,
                playerLaterId,
                deadPlayer,
                enemyLaterId,
                playerEarlyId,
            });

            Assert.That(order.ToArray(), Is.EqualTo(new[]
            {
                playerEarlyId,
                playerLaterId,
                enemyEarlyId,
                enemyLaterId,
            }));
        }
        finally
        {
            fixture.Dispose();
        }
    }

    [Test]
    public void SetUnitsSelectsFirstCurrentUnitAndExposesNextUnit()
    {
        var fixture = new UnitFixture();

        try
        {
            var enemy = fixture.CreateUnit("Enemy", new UnitId(1), TeamId.Enemy, 0);
            var firstPlayer = fixture.CreateUnit("First Player", new UnitId(2), TeamId.Player, 1);
            var secondPlayer = fixture.CreateUnit("Second Player", new UnitId(3), TeamId.Player, 2);
            var service = new TurnOrderService();

            service.SetUnits(new[] { enemy, secondPlayer, firstPlayer });

            Assert.That(service.TurnOrder.ToArray(), Is.EqualTo(new[] { firstPlayer, secondPlayer, enemy }));
            Assert.That(service.CurrentActiveUnit, Is.SameAs(firstPlayer));
            Assert.That(service.NextActiveUnit, Is.SameAs(secondPlayer));
            Assert.That(service.HasCurrentActiveUnit, Is.True);
        }
        finally
        {
            fixture.Dispose();
        }
    }

    [Test]
    public void AdvanceSkipsUnitsThatDieAfterOrderIsBuiltAndStopsAtEnd()
    {
        var fixture = new UnitFixture();

        try
        {
            var firstPlayer = fixture.CreateUnit("First Player", new UnitId(1), TeamId.Player, 0);
            var secondPlayer = fixture.CreateUnit("Second Player", new UnitId(2), TeamId.Player, 1);
            var enemy = fixture.CreateUnit("Enemy", new UnitId(3), TeamId.Enemy, 2);
            var service = new TurnOrderService();
            service.SetUnits(new[] { firstPlayer, secondPlayer, enemy });
            Assert.That(service.CurrentActiveUnit, Is.SameAs(firstPlayer));

            Assert.That(secondPlayer.ApplyDamage(secondPlayer.MaxHP, DamageSource.Environmental("Turn order test")).IsSuccess, Is.True);

            Assert.That(service.TryAdvanceToNext(), Is.True);
            Assert.That(service.CurrentActiveUnit, Is.SameAs(enemy));
            Assert.That(service.NextActiveUnit, Is.Null);
            Assert.That(service.TurnOrder.ToArray(), Is.EqualTo(new[] { firstPlayer, enemy }));

            Assert.That(service.TryAdvanceToNext(), Is.False);
            Assert.That(service.CurrentActiveUnit, Is.SameAs(enemy));
        }
        finally
        {
            fixture.Dispose();
        }
    }

    [Test]
    public void NextUnitCanWrapWhenRequestedForPreview()
    {
        var fixture = new UnitFixture();

        try
        {
            var firstPlayer = fixture.CreateUnit("First Player", new UnitId(1), TeamId.Player, 0);
            var enemy = fixture.CreateUnit("Enemy", new UnitId(2), TeamId.Enemy, 1);
            var service = new TurnOrderService();
            service.SetUnits(new[] { enemy, firstPlayer });
            Assert.That(service.TrySetCurrentActiveUnit(enemy), Is.True);

            Assert.That(service.TryGetNextActiveUnit(out var nonWrappingNext), Is.False);
            Assert.That(nonWrappingNext, Is.Null);
            Assert.That(service.TryGetNextActiveUnit(wrapAtEnd: true, out var wrappingNext), Is.True);
            Assert.That(wrappingNext, Is.SameAs(firstPlayer));
        }
        finally
        {
            fixture.Dispose();
        }
    }

    [Test]
    public void RejectsNullUnitCollectionsAndInvalidCurrentUnits()
    {
        var fixture = new UnitFixture();

        try
        {
            var service = new TurnOrderService();
            var livingUnit = fixture.CreateUnit("Living", new UnitId(1), TeamId.Player, 0);
            var deadUnit = fixture.CreateUnit("Dead", new UnitId(2), TeamId.Player, 1);
            Assert.That(deadUnit.ApplyDamage(deadUnit.MaxHP, DamageSource.Environmental("Turn order test")).IsSuccess, Is.True);
            service.SetUnits(new[] { livingUnit });

            Assert.Throws<ArgumentNullException>(() => TurnOrderService.BuildTurnOrder(null));
            Assert.Throws<ArgumentNullException>(() => service.SetUnits(null));
            Assert.That(service.TrySetCurrentActiveUnit(null), Is.False);
            Assert.That(service.TrySetCurrentActiveUnit(deadUnit), Is.False);
        }
        finally
        {
            fixture.Dispose();
        }
    }

    private sealed class UnitFixture : IDisposable
    {
        private readonly System.Collections.Generic.List<GameObject> unitObjects =
            new System.Collections.Generic.List<GameObject>();
        private readonly UnitStatsDefinition stats;

        public UnitFixture()
        {
            stats = ScriptableObject.CreateInstance<UnitStatsDefinition>();
            stats.Configure(
                displayName: "Turn Order Test Unit",
                maxHP: 10,
                maxAP: 6,
                movementAnimationSpeed: 4f,
                meleeRange: 1,
                teamColorHint: Color.white);
        }

        public TacticalUnit CreateUnit(string name, UnitId unitId, TeamId team, int x)
        {
            var gameObject = new GameObject(name);
            unitObjects.Add(gameObject);
            var unit = gameObject.AddComponent<TacticalUnit>();
            unit.Initialize(unitId, team, stats, new GridPosition(x, 0, 0));
            return unit;
        }

        public void Dispose()
        {
            for (var i = unitObjects.Count - 1; i >= 0; i--)
            {
                UnityEngine.Object.DestroyImmediate(unitObjects[i]);
            }

            UnityEngine.Object.DestroyImmediate(stats);
        }
    }
}
