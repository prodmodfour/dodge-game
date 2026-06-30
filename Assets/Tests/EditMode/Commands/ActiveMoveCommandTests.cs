using System;
using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using ReactionTactics.Commands;
using ReactionTactics.Grid;
using ReactionTactics.Input;
using ReactionTactics.Turns;
using ReactionTactics.Units;
using UnityEditor;
using UnityEngine;

public sealed class ActiveMoveCommandTests
{
    [Test]
    public void ActiveMoveSpendsApUpdatesOccupancyAndKeepsActiveTurn()
    {
        using (var fixture = new Fixture())
        {
            var activeUnit = fixture.CreateUnit("Active Move Knight", new UnitId(1), TeamId.Player, new GridPosition(0, 0, 0));
            fixture.CreateUnit("Active Move Enemy", new UnitId(2), TeamId.Enemy, new GridPosition(3, 0, 0));
            Assert.That(fixture.Manager.StartCombat().IsSuccess, Is.True);

            var apEvents = new List<ActionPointsChangedEvent>();
            fixture.EventBus.ActionPointsChanged += eventData =>
            {
                if (ReferenceEquals(eventData.Unit, activeUnit))
                {
                    apEvents.Add(eventData);
                }
            };

            var start = activeUnit.CurrentGridPosition;
            var destination = new GridPosition(1, 0, 0);
            var apBeforeMove = activeUnit.CurrentAP;
            var commandResult = ActiveMoveCommand.TryCreate(
                activeUnit,
                destination,
                fixture.GridManager.CurrentMap,
                fixture.Registry,
                fixture.Manager.CurrentState);

            Assert.That(commandResult.IsSuccess, Is.True, commandResult.ErrorMessage);
            Assert.That(commandResult.Value.MoveCommand.SourcePhase, Is.EqualTo(MovementMode.Active));
            Assert.That(commandResult.Value.Cost, Is.EqualTo(1));
            Assert.That(commandResult.Value.Path.Positions.ToArray(), Is.EqualTo(new[] { start, destination }));

            var moveResult = fixture.Manager.MoveActiveUnit(destination);

            Assert.That(moveResult.IsSuccess, Is.True, moveResult.ErrorMessage);
            Assert.That(activeUnit.CurrentGridPosition, Is.EqualTo(destination));
            Assert.That(activeUnit.CurrentAP, Is.EqualTo(apBeforeMove - 1));
            Assert.That(apEvents.Count, Is.EqualTo(1));
            Assert.That(apEvents[0].PreviousAP, Is.EqualTo(apBeforeMove));
            Assert.That(apEvents[0].CurrentAP, Is.EqualTo(apBeforeMove - 1));
            Assert.That(fixture.Registry.IsOccupied(start), Is.False);
            Assert.That(fixture.Registry.IsOccupied(destination), Is.True);
            Assert.That(fixture.Registry.TryGetOccupant(destination, out var occupantId), Is.True);
            Assert.That(occupantId, Is.EqualTo(activeUnit.UnitId));
            Assert.That(fixture.Manager.CurrentReactionWindow, Is.Null);
            Assert.That(fixture.Manager.CurrentState.CurrentRound, Is.EqualTo(1));
            Assert.That(fixture.Manager.CurrentState.Phase, Is.EqualTo(CombatPhase.ActiveTurn));
            Assert.That(fixture.Manager.CurrentState.ActiveUnit, Is.SameAs(activeUnit));
            Assert.That(fixture.Manager.CurrentState.ReactingUnit, Is.Null);
            Assert.That(fixture.Manager.CurrentState.PendingActionIntent, Is.Null);
            Assert.That(fixture.Manager.ValidateUnitCanTakeAction(activeUnit).IsSuccess, Is.True);
        }
    }

    [Test]
    public void ActiveMoveRejectsNonActiveUnitsOccupiedDestinationsAndNonActiveTurnPhases()
    {
        using (var fixture = new Fixture())
        {
            var activeUnit = fixture.CreateUnit("Active Move Current", new UnitId(1), TeamId.Player, new GridPosition(0, 0, 0));
            var nonActiveUnit = fixture.CreateUnit("Active Move Waiting", new UnitId(2), TeamId.Player, new GridPosition(1, 0, 0));
            fixture.CreateUnit("Active Move Far Enemy", new UnitId(3), TeamId.Enemy, new GridPosition(3, 0, 0));
            Assert.That(fixture.Manager.StartCombat().IsSuccess, Is.True);

            var activeStart = activeUnit.CurrentGridPosition;
            var activeAp = activeUnit.CurrentAP;
            var nonActiveStart = nonActiveUnit.CurrentGridPosition;

            var wrongUnitResult = fixture.Manager.MoveActiveUnit(nonActiveUnit, new GridPosition(2, 0, 0));
            Assert.That(wrongUnitResult.IsFailure, Is.True);
            Assert.That(wrongUnitResult.ErrorMessage, Does.Contain("active unit"));

            var occupiedResult = fixture.Manager.MoveActiveUnit(activeUnit, nonActiveUnit.CurrentGridPosition);
            Assert.That(occupiedResult.IsFailure, Is.True);
            Assert.That(occupiedResult.ErrorMessage, Does.Contain("not reachable"));

            fixture.Manager.CurrentState.SetPhase(CombatPhase.ActionTargeting);
            var wrongPhaseResult = fixture.Manager.MoveActiveUnit(activeUnit, new GridPosition(2, 0, 0));
            Assert.That(wrongPhaseResult.IsFailure, Is.True);
            Assert.That(wrongPhaseResult.ErrorMessage, Does.Contain(nameof(CombatPhase.ActiveTurn)));

            Assert.That(activeUnit.CurrentGridPosition, Is.EqualTo(activeStart));
            Assert.That(nonActiveUnit.CurrentGridPosition, Is.EqualTo(nonActiveStart));
            Assert.That(activeUnit.CurrentAP, Is.EqualTo(activeAp));
            Assert.That(fixture.Registry.IsOccupied(activeStart), Is.True);
            Assert.That(fixture.Registry.IsOccupied(nonActiveStart), Is.True);
            Assert.That(fixture.Manager.CurrentReactionWindow, Is.Null);
            Assert.That(fixture.Manager.CurrentState.Phase, Is.EqualTo(CombatPhase.ActionTargeting));
            Assert.That(fixture.Manager.CurrentState.ActiveUnit, Is.SameAs(activeUnit));
        }
    }

    [Test]
    public void FailedActiveMoveDoesNotSpendApMoveUnitOrLeaveActiveTurn()
    {
        using (var fixture = new Fixture())
        {
            var activeUnit = fixture.CreateUnit("Active Move Low AP", new UnitId(1), TeamId.Player, new GridPosition(0, 0, 0));
            Assert.That(fixture.Manager.StartCombat().IsSuccess, Is.True);
            activeUnit.SetAPForTest(0);
            var start = activeUnit.CurrentGridPosition;

            var moveResult = fixture.Manager.MoveActiveUnit(new GridPosition(1, 0, 0));

            Assert.That(moveResult.IsFailure, Is.True);
            Assert.That(moveResult.ErrorMessage, Does.Contain("not reachable"));
            Assert.That(activeUnit.CurrentAP, Is.EqualTo(0));
            Assert.That(activeUnit.CurrentGridPosition, Is.EqualTo(start));
            Assert.That(fixture.Registry.IsOccupied(start), Is.True);
            Assert.That(fixture.Manager.CurrentReactionWindow, Is.Null);
            Assert.That(fixture.Manager.CurrentState.Phase, Is.EqualTo(CombatPhase.ActiveTurn));
            Assert.That(fixture.Manager.CurrentState.ActiveUnit, Is.SameAs(activeUnit));
            Assert.That(fixture.Manager.CurrentState.ReactingUnit, Is.Null);
            Assert.That(fixture.Manager.CurrentState.PendingActionIntent, Is.Null);
        }
    }

    private sealed class Fixture : IDisposable
    {
        private readonly List<GameObject> rootObjects = new List<GameObject>();
        private readonly UnitStatsDefinition stats;
        private readonly GridMapDefinition mapDefinition;

        public Fixture()
        {
            stats = ScriptableObject.CreateInstance<UnitStatsDefinition>();
            stats.Configure(
                displayName: "Active Move Test Unit",
                maxHP: 10,
                maxAP: 6,
                movementAnimationSpeed: 4f,
                meleeRange: 1,
                teamColorHint: Color.white);

            mapDefinition = ScriptableObject.CreateInstance<GridMapDefinition>();
            mapDefinition.Configure(
                width: 4,
                depth: 1,
                defaultHeightY: 0,
                overrides: Array.Empty<GridMapDefinition.CellOverride>());

            var registryObject = CreateRoot("Active Move Test Registry");
            var gridObject = CreateRoot("Active Move Test Grid");
            var routerObject = CreateRoot("Active Move Test Router");
            var managerObject = CreateRoot("Active Move Test Manager");

            gridObject.SetActive(false);
            Registry = registryObject.AddComponent<UnitRegistry>();
            GridManager = gridObject.AddComponent<GridManager>();
            AssignMapDefinition(GridManager, mapDefinition);
            Assert.That(GridManager.RebuildMap(), Is.True);

            InputRouter = routerObject.AddComponent<PlayerCommandRouter>();
            EventBus = managerObject.AddComponent<CombatEventBus>();
            Manager = managerObject.AddComponent<CombatManager>();
            Manager.Configure(Registry, GridManager, InputRouter, EventBus);
            Manager.StartCombatOnStart = false;
            Manager.LogCombatStart = false;
            Manager.LogActionFlow = false;
        }

        public UnitRegistry Registry { get; }

        public GridManager GridManager { get; }

        public PlayerCommandRouter InputRouter { get; }

        public CombatEventBus EventBus { get; }

        public CombatManager Manager { get; }

        public TacticalUnit CreateUnit(string name, UnitId unitId, TeamId team, GridPosition position)
        {
            var gameObject = new GameObject(name);
            gameObject.transform.SetParent(Registry.transform, worldPositionStays: false);
            var unit = gameObject.AddComponent<TacticalUnit>();
            unit.Initialize(unitId, team, stats, position);
            return unit;
        }

        public void Dispose()
        {
            for (var i = rootObjects.Count - 1; i >= 0; i -= 1)
            {
                UnityEngine.Object.DestroyImmediate(rootObjects[i]);
            }

            UnityEngine.Object.DestroyImmediate(mapDefinition);
            UnityEngine.Object.DestroyImmediate(stats);
        }

        private GameObject CreateRoot(string name)
        {
            var gameObject = new GameObject(name);
            rootObjects.Add(gameObject);
            return gameObject;
        }

        private static void AssignMapDefinition(GridManager manager, GridMapDefinition definition)
        {
            var serializedObject = new SerializedObject(manager);
            serializedObject.FindProperty("mapDefinition").objectReferenceValue = definition;
            serializedObject.ApplyModifiedPropertiesWithoutUndo();
        }
    }
}
