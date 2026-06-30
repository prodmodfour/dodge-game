using System;
using System.Collections.Generic;
using NUnit.Framework;
using ReactionTactics.Grid;
using ReactionTactics.Input;
using ReactionTactics.Turns;
using ReactionTactics.Units;
using UnityEngine;

public sealed class ActiveActionLegalityTests
{
    [Test]
    public void ActiveUnitCanTakeActionsDuringActiveAndTargetingPhases()
    {
        using (var fixture = new Fixture())
        {
            var activeUnit = fixture.CreateUnit("Active Knight", new UnitId(1), TeamId.Player, 0);
            fixture.CreateUnit("Waiting Rogue", new UnitId(2), TeamId.Player, 1);
            Assert.That(fixture.Manager.StartCombat().IsSuccess, Is.True);

            var activeTurnResult = fixture.Manager.ValidateUnitCanTakeAction(activeUnit);
            Assert.That(activeTurnResult.IsSuccess, Is.True, activeTurnResult.ErrorMessage);
            Assert.That(fixture.Manager.CanUnitTakeAction(activeUnit), Is.True);

            fixture.Manager.CurrentState.SetPhase(CombatPhase.ActionTargeting);
            var targetingResult = fixture.Manager.ValidateUnitCanTakeAction(activeUnit);
            Assert.That(targetingResult.IsSuccess, Is.True, targetingResult.ErrorMessage);
            Assert.That(fixture.Manager.CanUnitTakeAction(activeUnit), Is.True);
        }
    }

    [Test]
    public void NonActiveUnitCannotTakeActiveActionsAndReceivesClearError()
    {
        using (var fixture = new Fixture())
        {
            var activeUnit = fixture.CreateUnit("Active Knight", new UnitId(1), TeamId.Player, 0);
            var nonActiveUnit = fixture.CreateUnit("Waiting Rogue", new UnitId(2), TeamId.Player, 1);
            Assert.That(fixture.Manager.StartCombat().IsSuccess, Is.True);

            var result = fixture.Manager.ValidateUnitCanTakeAction(nonActiveUnit);

            Assert.That(fixture.Manager.CurrentState.ActiveUnit, Is.SameAs(activeUnit));
            Assert.That(fixture.Manager.CanUnitTakeAction(nonActiveUnit), Is.False);
            Assert.That(result.IsFailure, Is.True);
            Assert.That(result.ErrorMessage, Does.Contain("active unit"));
            Assert.That(result.ErrorMessage, Does.Contain(nonActiveUnit.DisplayName));
            Assert.That(result.ErrorMessage, Does.Contain(activeUnit.DisplayName));
        }
    }

    [Test]
    public void ActiveUnitCannotTakeActiveActionsOutsideActiveTurnPhases()
    {
        using (var fixture = new Fixture())
        {
            var activeUnit = fixture.CreateUnit("Active Knight", new UnitId(1), TeamId.Player, 0);
            fixture.CreateUnit("Waiting Rogue", new UnitId(2), TeamId.Player, 1);
            Assert.That(fixture.Manager.StartCombat().IsSuccess, Is.True);
            fixture.Manager.CurrentState.SetPhase(CombatPhase.ReactionWindow);

            var result = fixture.Manager.ValidateUnitCanTakeAction(activeUnit);

            Assert.That(fixture.Manager.CanUnitTakeAction(activeUnit), Is.False);
            Assert.That(result.IsFailure, Is.True);
            Assert.That(result.ErrorMessage, Does.Contain(nameof(CombatPhase.ReactionWindow)));
            Assert.That(result.ErrorMessage, Does.Contain(nameof(CombatPhase.ActiveTurn)));
            Assert.That(result.ErrorMessage, Does.Contain(nameof(CombatPhase.ActionTargeting)));
        }
    }

    [Test]
    public void RoutedActionSelectionForNonActiveUnitIsCleared()
    {
        using (var fixture = new Fixture())
        {
            fixture.CreateUnit("Active Knight", new UnitId(1), TeamId.Player, 0);
            var nonActiveUnit = fixture.CreateUnit("Waiting Rogue", new UnitId(2), TeamId.Player, 1);
            Assert.That(fixture.Manager.StartCombat().IsSuccess, Is.True);

            Assert.That(fixture.InputRouter.SelectUnit(nonActiveUnit).IsSuccess, Is.True);
            Assert.That(fixture.Selection.SelectedUnit, Is.SameAs(nonActiveUnit));

            var result = fixture.InputRouter.SelectMeleeAttack();

            Assert.That(result.IsSuccess, Is.True, result.ErrorMessage);
            Assert.That(fixture.Selection.SelectedUnit, Is.SameAs(nonActiveUnit));
            Assert.That(fixture.Selection.SelectedActionMode, Is.EqualTo(SelectionActionMode.None));
            Assert.That(fixture.Manager.CanUnitTakeAction(nonActiveUnit), Is.False);
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
                displayName: "Active Action Test Unit",
                maxHP: 10,
                maxAP: 6,
                movementAnimationSpeed: 4f,
                meleeRange: 1,
                teamColorHint: Color.white);

            var registryObject = CreateRoot("Active Action Test Registry");
            var gridObject = CreateRoot("Active Action Test Grid");
            var selectionObject = CreateRoot("Active Action Test Selection");
            var routerObject = CreateRoot("Active Action Test Router");
            var managerObject = CreateRoot("Active Action Test Manager");

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
