using System;
using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using ReactionTactics.Grid;
using ReactionTactics.Input;
using ReactionTactics.Turns;
using ReactionTactics.Units;
using UnityEngine;

public sealed class Turn
{
    [Test]
    public void CombatStartSelectsFirstActiveUnitFromDeterministicTurnOrder()
    {
        using (var fixture = new Fixture())
        {
            var enemyWithLowestId = fixture.CreateUnit("Enemy With Lowest Id", new UnitId(1), TeamId.Enemy, 2);
            var laterPlayer = fixture.CreateUnit("Later Player", new UnitId(5), TeamId.Player, 1);
            var firstPlayer = fixture.CreateUnit("First Player", new UnitId(2), TeamId.Player, 0);

            var result = fixture.Manager.StartCombat();

            Assert.That(result.IsSuccess, Is.True, result.ErrorMessage);
            Assert.That(fixture.Manager.CurrentState.CurrentRound, Is.EqualTo(1));
            Assert.That(fixture.Manager.CurrentState.Phase, Is.EqualTo(CombatPhase.ActiveTurn));
            Assert.That(fixture.Manager.CurrentState.ActiveUnit, Is.SameAs(firstPlayer));
            Assert.That(fixture.Manager.TurnOrder.TurnOrder.ToArray(), Is.EqualTo(new[]
            {
                firstPlayer,
                laterPlayer,
                enemyWithLowestId,
            }));
        }
    }

    [Test]
    public void EndActiveTurnSkipsDeadUnitsInCurrentRound()
    {
        using (var fixture = new Fixture())
        {
            var firstPlayer = fixture.CreateUnit("First Player", new UnitId(1), TeamId.Player, 0);
            var deadBeforeTurn = fixture.CreateUnit("Dead Before Turn", new UnitId(2), TeamId.Player, 1);
            var enemy = fixture.CreateUnit("Enemy", new UnitId(3), TeamId.Enemy, 2);
            Assert.That(fixture.Manager.StartCombat().IsSuccess, Is.True);
            Assert.That(deadBeforeTurn.ApplyDamage(deadBeforeTurn.MaxHP, DamageSource.Environmental("turn system test")).IsSuccess, Is.True);

            var result = fixture.Manager.EndActiveTurn();

            Assert.That(result.IsSuccess, Is.True, result.ErrorMessage);
            Assert.That(fixture.Manager.CurrentState.CurrentRound, Is.EqualTo(1));
            Assert.That(fixture.Manager.CurrentState.Phase, Is.EqualTo(CombatPhase.ActiveTurn));
            Assert.That(fixture.Manager.CurrentState.ActiveUnit, Is.SameAs(enemy));
            Assert.That(fixture.Manager.TurnOrder.TurnOrder.ToArray(), Is.EqualTo(new[]
            {
                firstPlayer,
                enemy,
            }));
        }
    }

    [Test]
    public void ActionPointsRefreshOnlyWhenNextRoundStarts()
    {
        using (var fixture = new Fixture())
        {
            var player = fixture.CreateUnit("Player", new UnitId(1), TeamId.Player, 0);
            var enemy = fixture.CreateUnit("Enemy", new UnitId(2), TeamId.Enemy, 1);
            Assert.That(fixture.Manager.StartCombat().IsSuccess, Is.True);
            player.SetAPForTest(0);
            enemy.SetAPForTest(1);

            Assert.That(fixture.Manager.EndActiveTurn().IsSuccess, Is.True);

            Assert.That(fixture.Manager.CurrentState.CurrentRound, Is.EqualTo(1));
            Assert.That(fixture.Manager.CurrentState.ActiveUnit, Is.SameAs(enemy));
            Assert.That(player.CurrentAP, Is.EqualTo(0), "AP should not refresh between active turns in the same round.");
            Assert.That(enemy.CurrentAP, Is.EqualTo(1), "AP should not refresh between active turns in the same round.");

            var roundEvents = new List<RoundStartedEvent>();
            var apEvents = new List<ActionPointsChangedEvent>();
            fixture.EventBus.RoundStarted += roundEvents.Add;
            fixture.EventBus.ActionPointsChanged += apEvents.Add;

            var result = fixture.Manager.EndActiveTurn();

            Assert.That(result.IsSuccess, Is.True, result.ErrorMessage);
            Assert.That(fixture.Manager.CurrentState.CurrentRound, Is.EqualTo(2));
            Assert.That(fixture.Manager.CurrentState.Phase, Is.EqualTo(CombatPhase.ActiveTurn));
            Assert.That(fixture.Manager.CurrentState.ActiveUnit, Is.SameAs(player));
            Assert.That(player.CurrentAP, Is.EqualTo(player.MaxAP));
            Assert.That(enemy.CurrentAP, Is.EqualTo(enemy.MaxAP));
            Assert.That(roundEvents.Select(e => e.RoundNumber).ToArray(), Is.EqualTo(new[] { 2 }));
            Assert.That(apEvents.Select(e => e.Unit).ToArray(), Is.EqualTo(new[] { player, enemy }));
        }
    }

    [Test]
    public void NonActiveUnitsCannotTakeActiveTurnActions()
    {
        using (var fixture = new Fixture())
        {
            var activeUnit = fixture.CreateUnit("Active Player", new UnitId(1), TeamId.Player, 0);
            var nonActiveUnit = fixture.CreateUnit("Waiting Player", new UnitId(2), TeamId.Player, 1);
            Assert.That(fixture.Manager.StartCombat().IsSuccess, Is.True);
            Assert.That(fixture.Manager.CurrentState.ActiveUnit, Is.SameAs(activeUnit));

            var validation = fixture.Manager.ValidateUnitCanTakeAction(nonActiveUnit);

            Assert.That(validation.IsFailure, Is.True);
            Assert.That(validation.ErrorMessage, Does.Contain("active unit"));
            Assert.That(fixture.Manager.CanUnitTakeAction(nonActiveUnit), Is.False);

            Assert.That(fixture.InputRouter.SelectUnit(nonActiveUnit).IsSuccess, Is.True);
            Assert.That(fixture.InputRouter.SelectMove().IsSuccess, Is.True);

            Assert.That(fixture.Selection.SelectedUnit, Is.SameAs(nonActiveUnit));
            Assert.That(fixture.Selection.SelectedActionMode, Is.EqualTo(SelectionActionMode.None));
            Assert.That(fixture.Manager.CurrentState.ActiveUnit, Is.SameAs(activeUnit));
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
                displayName: "Turn System Test Unit",
                maxHP: 10,
                maxAP: 6,
                movementAnimationSpeed: 4f,
                meleeRange: 1,
                teamColorHint: Color.white);

            var registryObject = CreateRoot("Turn System Test Registry");
            var gridObject = CreateRoot("Turn System Test Grid");
            var selectionObject = CreateRoot("Turn System Test Selection");
            var routerObject = CreateRoot("Turn System Test Router");
            var managerObject = CreateRoot("Turn System Test Manager");

            Registry = registryObject.AddComponent<UnitRegistry>();
            GridManager = gridObject.AddComponent<GridManager>();
            Selection = selectionObject.AddComponent<SelectionController>();
            InputRouter = routerObject.AddComponent<PlayerCommandRouter>();
            InputRouter.SelectionController = Selection;
            EventBus = managerObject.AddComponent<CombatEventBus>();
            Manager = managerObject.AddComponent<CombatManager>();
            Manager.Configure(Registry, GridManager, InputRouter, EventBus);
            Manager.StartCombatOnStart = false;
            Manager.LogCombatStart = false;
        }

        public UnitRegistry Registry { get; }

        public GridManager GridManager { get; }

        public SelectionController Selection { get; }

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
