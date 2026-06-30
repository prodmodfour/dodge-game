using System;
using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using ReactionTactics.Actions;
using ReactionTactics.Commands;
using ReactionTactics.Grid;
using ReactionTactics.Input;
using ReactionTactics.Reactions;
using ReactionTactics.Turns;
using ReactionTactics.Units;
using UnityEditor;
using UnityEngine;

public sealed class ReactionMoveCommandTests
{
    [Test]
    public void ReactionMoveSpendsApUpdatesOccupancyCompletesReactorAndCanAvoidMelee()
    {
        using (var fixture = new Fixture())
        {
            var actor = fixture.CreateUnit("Reaction Move Actor", new UnitId(1), TeamId.Player, new GridPosition(0, 0, 0));
            var targetReactor = fixture.CreateUnit("Reaction Move Target", new UnitId(2), TeamId.Enemy, new GridPosition(1, 0, 0));
            fixture.AssignLoadout(actor, fixture.MeleeSlash);

            Assert.That(fixture.Manager.StartCombat().IsSuccess, Is.True);

            ActionIntent declaredIntent = null;
            ReactionWindow windowDuringResolution = null;
            var targetApEvents = new List<ActionPointsChangedEvent>();
            fixture.EventBus.ActionDeclared += eventData => declaredIntent = eventData.ActionIntent as ActionIntent;
            fixture.EventBus.ActionResolved += _ => windowDuringResolution = fixture.Manager.CurrentReactionWindow;
            fixture.EventBus.ActionPointsChanged += eventData =>
            {
                if (ReferenceEquals(eventData.Unit, targetReactor))
                {
                    targetApEvents.Add(eventData);
                }
            };

            Assert.That(fixture.InputRouter.SelectUnit(actor).IsSuccess, Is.True);
            Assert.That(fixture.InputRouter.SelectMeleeAttack().IsSuccess, Is.True);
            Assert.That(fixture.InputRouter.ConfirmTargetUnit(targetReactor).IsSuccess, Is.True);

            Assert.That(declaredIntent, Is.Not.Null);
            Assert.That(fixture.Manager.CurrentState.Phase, Is.EqualTo(CombatPhase.ReactionWindow));
            Assert.That(fixture.Manager.CurrentState.ReactingUnit, Is.SameAs(targetReactor));
            Assert.That(fixture.Manager.CurrentReactionWindow.CurrentReactor, Is.SameAs(targetReactor));

            var start = targetReactor.CurrentGridPosition;
            var destination = new GridPosition(2, 0, 0);
            var apBeforeMove = targetReactor.CurrentAP;
            var commandResult = ReactionMoveCommand.TryCreate(
                targetReactor,
                destination,
                fixture.GridManager.CurrentMap,
                fixture.Registry,
                fixture.Manager.CurrentState,
                fixture.Manager.CurrentReactionWindow);

            Assert.That(commandResult.IsSuccess, Is.True, commandResult.ErrorMessage);
            Assert.That(commandResult.Value.MoveCommand.SourcePhase, Is.EqualTo(MovementMode.Reaction));
            Assert.That(commandResult.Value.Cost, Is.EqualTo(1));
            Assert.That(commandResult.Value.Path.Positions.ToArray(), Is.EqualTo(new[] { start, destination }));

            var moveResult = fixture.Manager.MoveCurrentReaction(destination);

            Assert.That(moveResult.IsSuccess, Is.True, moveResult.ErrorMessage);
            Assert.That(targetReactor.CurrentGridPosition, Is.EqualTo(destination));
            Assert.That(targetReactor.CurrentAP, Is.EqualTo(apBeforeMove - 1));
            Assert.That(targetApEvents.Count, Is.EqualTo(1));
            Assert.That(targetApEvents[0].PreviousAP, Is.EqualTo(apBeforeMove));
            Assert.That(targetApEvents[0].CurrentAP, Is.EqualTo(apBeforeMove - 1));
            Assert.That(fixture.Registry.IsOccupied(start), Is.False);
            Assert.That(fixture.Registry.IsOccupied(destination), Is.True);
            Assert.That(fixture.Registry.TryGetOccupant(destination, out var occupantId), Is.True);
            Assert.That(occupantId, Is.EqualTo(targetReactor.UnitId));
            Assert.That(targetReactor.CurrentHP, Is.EqualTo(targetReactor.MaxHP));

            Assert.That(windowDuringResolution, Is.Not.Null);
            Assert.That(windowDuringResolution.IsClosed, Is.True);
            Assert.That(windowDuringResolution.CompletedReactors, Is.EqualTo(new[] { targetReactor }));
            Assert.That(fixture.Manager.CurrentReactionWindow, Is.Null);
            Assert.That(fixture.Manager.CurrentState.Phase, Is.EqualTo(CombatPhase.ActiveTurn));
            Assert.That(fixture.Manager.CurrentState.ActiveUnit, Is.SameAs(actor));
            Assert.That(fixture.Manager.CurrentState.ReactingUnit, Is.Null);
            Assert.That(fixture.Manager.CurrentState.PendingActionIntent, Is.Null);
        }
    }

    [Test]
    public void ReactionMoveIsRejectedOutsideCurrentReactorTurnOrIntoOccupiedCell()
    {
        using (var fixture = new Fixture())
        {
            var actor = fixture.CreateUnit("Reaction Move Reject Actor", new UnitId(1), TeamId.Player, new GridPosition(0, 0, 0));
            var targetReactor = fixture.CreateUnit("Reaction Move Reject Target", new UnitId(2), TeamId.Enemy, new GridPosition(1, 0, 0));
            var otherReactor = fixture.CreateUnit("Reaction Move Reject Other", new UnitId(3), TeamId.Enemy, new GridPosition(2, 0, 0));
            fixture.AssignLoadout(actor, fixture.MeleeSlash);

            Assert.That(fixture.Manager.StartCombat().IsSuccess, Is.True);

            var outsideResult = fixture.Manager.MoveCurrentReaction(targetReactor, otherReactor.CurrentGridPosition);
            Assert.That(outsideResult.IsFailure, Is.True);
            Assert.That(outsideResult.ErrorMessage, Does.Contain("no reaction window"));

            Assert.That(fixture.InputRouter.SelectUnit(actor).IsSuccess, Is.True);
            Assert.That(fixture.InputRouter.SelectMeleeAttack().IsSuccess, Is.True);
            Assert.That(fixture.InputRouter.ConfirmTargetUnit(targetReactor).IsSuccess, Is.True);

            var targetStart = targetReactor.CurrentGridPosition;
            var targetAp = targetReactor.CurrentAP;
            var wrongUnitResult = fixture.Manager.MoveCurrentReaction(otherReactor, new GridPosition(3, 0, 0));
            Assert.That(wrongUnitResult.IsFailure, Is.True);
            Assert.That(wrongUnitResult.ErrorMessage, Does.Contain("current reactor"));

            var occupiedResult = fixture.Manager.MoveCurrentReaction(targetReactor, otherReactor.CurrentGridPosition);
            Assert.That(occupiedResult.IsFailure, Is.True);
            Assert.That(occupiedResult.ErrorMessage, Does.Contain("not reachable"));

            Assert.That(targetReactor.CurrentGridPosition, Is.EqualTo(targetStart));
            Assert.That(targetReactor.CurrentAP, Is.EqualTo(targetAp));
            Assert.That(fixture.Manager.CurrentState.Phase, Is.EqualTo(CombatPhase.ReactionWindow));
            Assert.That(fixture.Manager.CurrentState.ReactingUnit, Is.SameAs(targetReactor));
            Assert.That(fixture.Manager.CurrentReactionWindow.CompletedReactors, Is.Empty);
            Assert.That(fixture.Registry.IsOccupied(targetStart), Is.True);
            Assert.That(fixture.Registry.IsOccupied(otherReactor.CurrentGridPosition), Is.True);
        }
    }

    [Test]
    public void FailedReactionMoveDoesNotSpendApMoveUnitOrCompleteReactor()
    {
        using (var fixture = new Fixture())
        {
            var actor = fixture.CreateUnit("Reaction Move AP Actor", new UnitId(1), TeamId.Player, new GridPosition(0, 0, 0));
            var targetReactor = fixture.CreateUnit("Reaction Move AP Target", new UnitId(2), TeamId.Enemy, new GridPosition(1, 0, 0));
            fixture.AssignLoadout(actor, fixture.MeleeSlash);

            Assert.That(fixture.Manager.StartCombat().IsSuccess, Is.True);
            Assert.That(fixture.InputRouter.SelectUnit(actor).IsSuccess, Is.True);
            Assert.That(fixture.InputRouter.SelectMeleeAttack().IsSuccess, Is.True);
            Assert.That(fixture.InputRouter.ConfirmTargetUnit(targetReactor).IsSuccess, Is.True);

            var start = targetReactor.CurrentGridPosition;
            targetReactor.SetAPForTest(0);

            var moveResult = fixture.Manager.MoveCurrentReaction(new GridPosition(2, 0, 0));

            Assert.That(moveResult.IsFailure, Is.True);
            Assert.That(moveResult.ErrorMessage, Does.Contain("not reachable"));
            Assert.That(targetReactor.CurrentAP, Is.EqualTo(0));
            Assert.That(targetReactor.CurrentGridPosition, Is.EqualTo(start));
            Assert.That(targetReactor.CurrentHP, Is.EqualTo(targetReactor.MaxHP));
            Assert.That(fixture.Manager.CurrentState.Phase, Is.EqualTo(CombatPhase.ReactionWindow));
            Assert.That(fixture.Manager.CurrentState.ReactingUnit, Is.SameAs(targetReactor));
            Assert.That(fixture.Manager.CurrentReactionWindow.CompletedReactors, Is.Empty);
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
                displayName: "Reaction Move Test Unit",
                maxHP: 10,
                maxAP: 6,
                movementAnimationSpeed: 4f,
                meleeRange: 1,
                teamColorHint: Color.white);

            MeleeSlash = ScriptableObject.CreateInstance<AbilityDefinition>();
            MeleeSlash.Configure(
                abilityKey: "melee_slash",
                displayName: "Melee Slash",
                apCost: 3,
                usage: AbilityUsage.Action,
                timing: AbilityTiming.Telegraphed,
                shape: AbilityShape.Melee,
                range: 0,
                radius: 0,
                damage: 4,
                triggersReactions: true,
                description: "Reaction movement test ability.");

            mapDefinition = ScriptableObject.CreateInstance<GridMapDefinition>();
            mapDefinition.Configure(
                width: 4,
                depth: 1,
                defaultHeightY: 0,
                overrides: Array.Empty<GridMapDefinition.CellOverride>());

            var registryObject = CreateRoot("Reaction Move Test Registry");
            var gridObject = CreateRoot("Reaction Move Test Grid");
            var selectionObject = CreateRoot("Reaction Move Test Selection");
            var routerObject = CreateRoot("Reaction Move Test Router");
            var managerObject = CreateRoot("Reaction Move Test Manager");

            gridObject.SetActive(false);
            Registry = registryObject.AddComponent<UnitRegistry>();
            GridManager = gridObject.AddComponent<GridManager>();
            AssignMapDefinition(GridManager, mapDefinition);
            Assert.That(GridManager.RebuildMap(), Is.True);

            Selection = selectionObject.AddComponent<SelectionController>();
            InputRouter = routerObject.AddComponent<PlayerCommandRouter>();
            InputRouter.SelectionController = Selection;
            EventBus = managerObject.AddComponent<CombatEventBus>();
            Manager = managerObject.AddComponent<CombatManager>();
            Manager.Configure(Registry, GridManager, InputRouter, EventBus);
            Manager.StartCombatOnStart = false;
            Manager.LogCombatStart = false;
            Manager.LogActionFlow = false;
        }

        public UnitRegistry Registry { get; }

        public GridManager GridManager { get; }

        public SelectionController Selection { get; }

        public PlayerCommandRouter InputRouter { get; }

        public CombatEventBus EventBus { get; }

        public CombatManager Manager { get; }

        public AbilityDefinition MeleeSlash { get; }

        public TacticalUnit CreateUnit(string name, UnitId unitId, TeamId team, GridPosition position)
        {
            var gameObject = new GameObject(name);
            gameObject.transform.SetParent(Registry.transform, worldPositionStays: false);
            var unit = gameObject.AddComponent<TacticalUnit>();
            unit.Initialize(unitId, team, stats, position);
            return unit;
        }

        public void AssignLoadout(TacticalUnit unit, params AbilityDefinition[] abilities)
        {
            var loadout = unit.gameObject.AddComponent<UnitAbilityLoadout>();
            loadout.SetAbilities(abilities);
        }

        public void Dispose()
        {
            for (var i = rootObjects.Count - 1; i >= 0; i -= 1)
            {
                UnityEngine.Object.DestroyImmediate(rootObjects[i]);
            }

            UnityEngine.Object.DestroyImmediate(mapDefinition);
            UnityEngine.Object.DestroyImmediate(MeleeSlash);
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
