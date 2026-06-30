using System;
using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using ReactionTactics.Grid;
using ReactionTactics.Input;
using ReactionTactics.Turns;
using ReactionTactics.Units;
using UnityEngine;

namespace ReactionTactics.Tests.EditMode.Turns
{
    public sealed class CombatManagerTests
    {
        [Test]
        public void StartCombatRegistersChildUnitsStartsRoundAndSelectsFirstActiveUnit()
        {
            using (var fixture = new Fixture())
            {
                var enemy = fixture.CreateUnit("Enemy Early Id", new UnitId(1), TeamId.Enemy, 2);
                var firstPlayer = fixture.CreateUnit("Player Early Id", new UnitId(2), TeamId.Player, 0);
                var secondPlayer = fixture.CreateUnit("Player Later Id", new UnitId(5), TeamId.Player, 1);
                enemy.SetAPForTest(2);
                firstPlayer.SetAPForTest(1);
                secondPlayer.SetAPForTest(3);

                var roundEvents = new List<RoundStartedEvent>();
                var activeEvents = new List<ActiveUnitChangedEvent>();
                var apEvents = new List<ActionPointsChangedEvent>();
                fixture.EventBus.RoundStarted += roundEvents.Add;
                fixture.EventBus.ActiveUnitChanged += activeEvents.Add;
                fixture.EventBus.ActionPointsChanged += apEvents.Add;

                var result = fixture.Manager.StartCombat();

                Assert.That(result.IsSuccess, Is.True, result.ErrorMessage);
                Assert.That(fixture.Registry.RegisteredCount, Is.EqualTo(3));
                Assert.That(fixture.Manager.CurrentState.CurrentRound, Is.EqualTo(1));
                Assert.That(fixture.Manager.CurrentState.Phase, Is.EqualTo(CombatPhase.ActiveTurn));
                Assert.That(fixture.Manager.CurrentState.ActiveUnit, Is.SameAs(firstPlayer));
                Assert.That(fixture.Manager.CurrentState.ReactingUnit, Is.Null);
                Assert.That(fixture.Manager.CurrentState.PendingActionIntent, Is.Null);
                Assert.That(fixture.Manager.TurnOrder.CurrentActiveUnit, Is.SameAs(firstPlayer));

                Assert.That(enemy.CurrentAP, Is.EqualTo(enemy.MaxAP));
                Assert.That(firstPlayer.CurrentAP, Is.EqualTo(firstPlayer.MaxAP));
                Assert.That(secondPlayer.CurrentAP, Is.EqualTo(secondPlayer.MaxAP));

                Assert.That(roundEvents.Select(e => e.RoundNumber).ToArray(), Is.EqualTo(new[] { 1 }));
                Assert.That(activeEvents.Count, Is.EqualTo(1));
                Assert.That(activeEvents[0].PreviousUnit, Is.Null);
                Assert.That(activeEvents[0].ActiveUnit, Is.SameAs(firstPlayer));
                Assert.That(apEvents.Select(e => e.Unit).ToArray(), Is.EqualTo(new[] { firstPlayer, secondPlayer, enemy }));
            }
        }

        [Test]
        public void StartCombatFailsClearlyWhenNoLivingUnitsAreAvailable()
        {
            using (var fixture = new Fixture())
            {
                var result = fixture.Manager.StartCombat();

                Assert.That(result.IsFailure, Is.True);
                Assert.That(result.ErrorMessage, Does.Contain("unit registry is empty"));
                Assert.That(fixture.Manager.CurrentState.CurrentRound, Is.EqualTo(0));
                Assert.That(fixture.Manager.CurrentState.Phase, Is.EqualTo(CombatPhase.NotStarted));
                Assert.That(fixture.Manager.CurrentState.ActiveUnit, Is.Null);
            }
        }

        [Test]
        public void StartCombatRejectsRepeatedStartWithoutAdvancingRoundAgain()
        {
            using (var fixture = new Fixture())
            {
                fixture.CreateUnit("Solo Player", new UnitId(1), TeamId.Player, 0);

                var firstStart = fixture.Manager.StartCombat();
                var secondStart = fixture.Manager.StartCombat();

                Assert.That(firstStart.IsSuccess, Is.True, firstStart.ErrorMessage);
                Assert.That(secondStart.IsFailure, Is.True);
                Assert.That(secondStart.ErrorMessage, Does.Contain(nameof(CombatPhase.ActiveTurn)));
                Assert.That(fixture.Manager.CurrentState.CurrentRound, Is.EqualTo(1));
                Assert.That(fixture.Manager.CurrentState.Phase, Is.EqualTo(CombatPhase.ActiveTurn));
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
                    displayName: "Combat Manager Test Unit",
                    maxHP: 10,
                    maxAP: 6,
                    movementAnimationSpeed: 4f,
                    meleeRange: 1,
                    teamColorHint: Color.white);

                var registryObject = CreateRoot("Combat Manager Test Registry");
                var gridObject = CreateRoot("Combat Manager Test Grid");
                var routerObject = CreateRoot("Combat Manager Test Router");
                var managerObject = CreateRoot("Combat Manager Test Manager");

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
}
