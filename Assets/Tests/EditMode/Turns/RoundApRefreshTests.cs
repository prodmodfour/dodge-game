using System;
using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using ReactionTactics.Grid;
using ReactionTactics.Turns;
using ReactionTactics.Units;
using UnityEngine;

public sealed class RoundApRefreshTests
{
    [Test]
    public void StartNextRoundIncrementsRoundRefreshesLivingUnitsAndPublishesEvents()
    {
        var fixture = new UnitFixture();
        var busObject = new GameObject("Round AP Refresh Event Bus");

        try
        {
            var player = fixture.CreateUnit("Player", new UnitId(2), TeamId.Player, 0);
            var enemy = fixture.CreateUnit("Enemy", new UnitId(1), TeamId.Enemy, 1);
            var deadUnit = fixture.CreateUnit("Dead Unit", new UnitId(3), TeamId.Player, 2);
            player.SetAPForTest(1);
            enemy.SetAPForTest(enemy.MaxAP);
            deadUnit.SetAPForTest(2);
            Assert.That(deadUnit.ApplyDamage(deadUnit.MaxHP, DamageSource.Environmental("round refresh test")).IsSuccess, Is.True);

            var bus = busObject.AddComponent<CombatEventBus>();
            var roundEvents = new List<RoundStartedEvent>();
            var apEvents = new List<ActionPointsChangedEvent>();
            bus.RoundStarted += roundEvents.Add;
            bus.ActionPointsChanged += apEvents.Add;
            var state = new CombatState();
            var service = new RoundLifecycleService();

            var startedRound = service.StartNextRound(
                state,
                new TacticalUnit[] { null, enemy, deadUnit, player },
                bus);

            Assert.That(startedRound, Is.EqualTo(1));
            Assert.That(state.CurrentRound, Is.EqualTo(1));
            Assert.That(state.Phase, Is.EqualTo(CombatPhase.RoundStart));
            Assert.That(player.CurrentAP, Is.EqualTo(player.MaxAP));
            Assert.That(enemy.CurrentAP, Is.EqualTo(enemy.MaxAP));
            Assert.That(deadUnit.CurrentAP, Is.EqualTo(2), "Dead units are ignored by round AP refresh.");

            Assert.That(roundEvents.Select(e => e.RoundNumber).ToArray(), Is.EqualTo(new[] { 1 }));
            Assert.That(apEvents.Select(e => e.Unit).ToArray(), Is.EqualTo(new[] { player, enemy }));
            Assert.That(apEvents.Select(e => e.PreviousAP).ToArray(), Is.EqualTo(new[] { 1, enemy.MaxAP }));
            Assert.That(apEvents.Select(e => e.CurrentAP).ToArray(), Is.EqualTo(new[] { player.MaxAP, enemy.MaxAP }));
            Assert.That(apEvents.Select(e => e.Delta).ToArray(), Is.EqualTo(new[] { player.MaxAP - 1, 0 }));
        }
        finally
        {
            UnityEngine.Object.DestroyImmediate(busObject);
            fixture.Dispose();
        }
    }

    [Test]
    public void StartNextRoundRefreshesAgainOnlyWhenAnotherRoundStarts()
    {
        var fixture = new UnitFixture();

        try
        {
            var unit = fixture.CreateUnit("Repeat Refresh Unit", new UnitId(1), TeamId.Player, 0);
            var state = new CombatState(1, CombatPhase.ActiveTurn, unit);
            var service = new RoundLifecycleService();
            Assert.That(unit.SpendAP(4).IsSuccess, Is.True);
            Assert.That(unit.CurrentAP, Is.EqualTo(unit.MaxAP - 4));

            service.StartNextRound(state, new[] { unit });

            Assert.That(state.CurrentRound, Is.EqualTo(2));
            Assert.That(unit.CurrentAP, Is.EqualTo(unit.MaxAP));

            Assert.That(unit.SpendAP(3).IsSuccess, Is.True);
            Assert.That(unit.CurrentAP, Is.EqualTo(unit.MaxAP - 3));
            state.SetPhase(CombatPhase.ActiveTurn);

            service.StartNextRound(state, new[] { unit });

            Assert.That(state.CurrentRound, Is.EqualTo(3));
            Assert.That(state.Phase, Is.EqualTo(CombatPhase.RoundStart));
            Assert.That(unit.CurrentAP, Is.EqualTo(unit.MaxAP));
        }
        finally
        {
            fixture.Dispose();
        }
    }

    [Test]
    public void StartNextRoundRejectsMissingStateOrUnitCollection()
    {
        var service = new RoundLifecycleService();
        var state = new CombatState();

        Assert.Throws<ArgumentNullException>(() => service.StartNextRound(null, Array.Empty<TacticalUnit>()));
        Assert.Throws<ArgumentNullException>(() => service.StartNextRound(state, null));
    }

    private sealed class UnitFixture : IDisposable
    {
        private readonly List<GameObject> unitObjects = new List<GameObject>();
        private readonly UnitStatsDefinition stats;

        public UnitFixture()
        {
            stats = ScriptableObject.CreateInstance<UnitStatsDefinition>();
            stats.Configure(
                displayName: "Round AP Test Unit",
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
