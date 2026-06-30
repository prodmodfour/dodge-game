using System;
using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using ReactionTactics.Grid;
using ReactionTactics.Input;
using ReactionTactics.Turns;
using ReactionTactics.Units;
using UnityEngine;

public sealed class EndTurnTests
{
    [Test]
    public void EndActiveTurnAdvancesToNextLivingUnitWithoutRefreshingActionPoints()
    {
        using (var fixture = new Fixture())
        {
            var firstPlayer = fixture.CreateUnit("First Player", new UnitId(1), TeamId.Player, 0);
            var secondPlayer = fixture.CreateUnit("Second Player", new UnitId(2), TeamId.Player, 1);
            var enemy = fixture.CreateUnit("Enemy", new UnitId(3), TeamId.Enemy, 2);
            Assert.That(fixture.Manager.StartCombat().IsSuccess, Is.True);
            firstPlayer.SetAPForTest(1);
            secondPlayer.SetAPForTest(2);
            enemy.SetAPForTest(3);
            var activeEvents = new List<ActiveUnitChangedEvent>();
            fixture.EventBus.ActiveUnitChanged += activeEvents.Add;

            var result = fixture.Manager.EndActiveTurn();

            Assert.That(result.IsSuccess, Is.True, result.ErrorMessage);
            Assert.That(fixture.Manager.CurrentState.CurrentRound, Is.EqualTo(1));
            Assert.That(fixture.Manager.CurrentState.Phase, Is.EqualTo(CombatPhase.ActiveTurn));
            Assert.That(fixture.Manager.CurrentState.ActiveUnit, Is.SameAs(secondPlayer));
            Assert.That(fixture.Manager.CurrentState.ReactingUnit, Is.Null);
            Assert.That(fixture.Manager.CurrentState.PendingActionIntent, Is.Null);
            Assert.That(fixture.Manager.TurnOrder.CurrentActiveUnit, Is.SameAs(secondPlayer));
            Assert.That(firstPlayer.CurrentAP, Is.EqualTo(1));
            Assert.That(secondPlayer.CurrentAP, Is.EqualTo(2));
            Assert.That(enemy.CurrentAP, Is.EqualTo(3));
            Assert.That(activeEvents.Count, Is.EqualTo(1));
            Assert.That(activeEvents[0].PreviousUnit, Is.SameAs(firstPlayer));
            Assert.That(activeEvents[0].ActiveUnit, Is.SameAs(secondPlayer));
        }
    }

    [Test]
    public void EndActiveTurnSkipsDeadUnitsWhenAdvancing()
    {
        using (var fixture = new Fixture())
        {
            var firstPlayer = fixture.CreateUnit("First Player", new UnitId(1), TeamId.Player, 0);
            var deadBeforeTurn = fixture.CreateUnit("Dead Before Turn", new UnitId(2), TeamId.Player, 1);
            var enemy = fixture.CreateUnit("Enemy", new UnitId(3), TeamId.Enemy, 2);
            Assert.That(fixture.Manager.StartCombat().IsSuccess, Is.True);
            Assert.That(deadBeforeTurn.ApplyDamage(deadBeforeTurn.MaxHP, DamageSource.Environmental("end turn test")).IsSuccess, Is.True);

            var result = fixture.Manager.EndActiveTurn();

            Assert.That(result.IsSuccess, Is.True, result.ErrorMessage);
            Assert.That(fixture.Manager.CurrentState.CurrentRound, Is.EqualTo(1));
            Assert.That(fixture.Manager.CurrentState.ActiveUnit, Is.SameAs(enemy));
            Assert.That(fixture.Manager.TurnOrder.TurnOrder.ToArray(), Is.EqualTo(new[] { firstPlayer, enemy }));
        }
    }

    [Test]
    public void EndActiveTurnStartsNextRoundAndRefreshesApAfterLastLivingUnitActs()
    {
        using (var fixture = new Fixture())
        {
            var player = fixture.CreateUnit("Player", new UnitId(1), TeamId.Player, 0);
            var enemy = fixture.CreateUnit("Enemy", new UnitId(2), TeamId.Enemy, 1);
            Assert.That(fixture.Manager.StartCombat().IsSuccess, Is.True);
            Assert.That(fixture.Manager.EndActiveTurn().IsSuccess, Is.True);
            Assert.That(fixture.Manager.CurrentState.ActiveUnit, Is.SameAs(enemy));
            player.SetAPForTest(0);
            enemy.SetAPForTest(1);
            var roundEvents = new List<RoundStartedEvent>();
            var activeEvents = new List<ActiveUnitChangedEvent>();
            var apEvents = new List<ActionPointsChangedEvent>();
            fixture.EventBus.RoundStarted += roundEvents.Add;
            fixture.EventBus.ActiveUnitChanged += activeEvents.Add;
            fixture.EventBus.ActionPointsChanged += apEvents.Add;

            var result = fixture.Manager.EndActiveTurn();

            Assert.That(result.IsSuccess, Is.True, result.ErrorMessage);
            Assert.That(fixture.Manager.CurrentState.CurrentRound, Is.EqualTo(2));
            Assert.That(fixture.Manager.CurrentState.Phase, Is.EqualTo(CombatPhase.ActiveTurn));
            Assert.That(fixture.Manager.CurrentState.ActiveUnit, Is.SameAs(player));
            Assert.That(fixture.Manager.TurnOrder.CurrentActiveUnit, Is.SameAs(player));
            Assert.That(player.CurrentAP, Is.EqualTo(player.MaxAP));
            Assert.That(enemy.CurrentAP, Is.EqualTo(enemy.MaxAP));
            Assert.That(roundEvents.Select(e => e.RoundNumber).ToArray(), Is.EqualTo(new[] { 2 }));
            Assert.That(apEvents.Select(e => e.Unit).ToArray(), Is.EqualTo(new[] { player, enemy }));
            Assert.That(apEvents.Select(e => e.PreviousAP).ToArray(), Is.EqualTo(new[] { 0, 1 }));
            Assert.That(apEvents.Select(e => e.CurrentAP).ToArray(), Is.EqualTo(new[] { player.MaxAP, enemy.MaxAP }));
            Assert.That(activeEvents.Count, Is.EqualTo(1));
            Assert.That(activeEvents[0].PreviousUnit, Is.SameAs(enemy));
            Assert.That(activeEvents[0].ActiveUnit, Is.SameAs(player));
        }
    }

    [Test]
    public void RoutedEndTurnCommandAdvancesCombatManagerThroughInputRouter()
    {
        using (var fixture = new Fixture())
        {
            var firstPlayer = fixture.CreateUnit("First Player", new UnitId(1), TeamId.Player, 0);
            var secondPlayer = fixture.CreateUnit("Second Player", new UnitId(2), TeamId.Player, 1);
            Assert.That(fixture.Manager.StartCombat().IsSuccess, Is.True);
            Assert.That(fixture.Manager.CurrentState.ActiveUnit, Is.SameAs(firstPlayer));

            var result = fixture.InputRouter.RequestEndTurn();

            Assert.That(result.IsSuccess, Is.True, result.ErrorMessage);
            Assert.That(fixture.Manager.CurrentState.ActiveUnit, Is.SameAs(secondPlayer));
        }
    }

    [Test]
    public void EndActiveTurnFailsClearlyBeforeCombatStarts()
    {
        using (var fixture = new Fixture())
        {
            var result = fixture.Manager.EndActiveTurn();

            Assert.That(result.IsFailure, Is.True);
            Assert.That(result.ErrorMessage, Does.Contain(nameof(CombatPhase.NotStarted)));
            Assert.That(fixture.Manager.CurrentState.CurrentRound, Is.EqualTo(0));
            Assert.That(fixture.Manager.CurrentState.Phase, Is.EqualTo(CombatPhase.NotStarted));
            Assert.That(fixture.Manager.CurrentState.ActiveUnit, Is.Null);
        }
    }

    private sealed class Fixture : IDisposable
    {
        private readonly List<GameObject> rootObjects = new List<GameObject>();
        private readonly UnitStatsDefinition stats;

        public Fixture()
        {
            stats = ScriptableObject.CreateInstance<UnitStatsDefinition>();
            stats.Configure(
                displayName: "End Turn Test Unit",
                maxHP: 10,
                maxAP: 6,
                movementAnimationSpeed: 4f,
                meleeRange: 1,
                teamColorHint: Color.white);

            var registryObject = CreateRoot("End Turn Test Registry");
            var gridObject = CreateRoot("End Turn Test Grid");
            var routerObject = CreateRoot("End Turn Test Router");
            var managerObject = CreateRoot("End Turn Test Manager");

            Registry = registryObject.AddComponent<UnitRegistry>();
            GridManager = gridObject.AddComponent<GridManager>();
            InputRouter = routerObject.AddComponent<PlayerCommandRouter>();
            EventBus = managerObject.AddComponent<CombatEventBus>();
            Manager = managerObject.AddComponent<CombatManager>();
            Manager.Configure(Registry, GridManager, InputRouter, EventBus);
            Manager.StartCombatOnStart = false;
            Manager.LogCombatStart = false;
        }

        public UnitRegistry Registry { get; }

        public GridManager GridManager { get; }

        public PlayerCommandRouter InputRouter { get; }

        public CombatEventBus EventBus { get; }

        public CombatManager Manager { get; }

        public TacticalUnit CreateUnit(string name, UnitId unitId, TeamId team, int x)
        {
            var gameObject = new GameObject(name);
            gameObject.transform.SetParent(Registry.transform, worldPositionStays: false);
            var unit = gameObject.AddComponent<TacticalUnit>();
            unit.Initialize(unitId, team, stats, new GridPosition(x, 0, 0));
            return unit;
        }

        public void Dispose()
        {
            for (var i = rootObjects.Count - 1; i >= 0; i--)
            {
                UnityEngine.Object.DestroyImmediate(rootObjects[i]);
            }

            UnityEngine.Object.DestroyImmediate(stats);
        }

        private GameObject CreateRoot(string name)
        {
            var gameObject = new GameObject(name);
            rootObjects.Add(gameObject);
            return gameObject;
        }
    }
}
