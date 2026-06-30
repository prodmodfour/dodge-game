using System;
using System.Collections.Generic;
using NUnit.Framework;
using ReactionTactics.Actions;
using ReactionTactics.Grid;
using ReactionTactics.Input;
using ReactionTactics.Reactions;
using ReactionTactics.Turns;
using ReactionTactics.Units;
using UnityEditor;
using UnityEngine;

public sealed class PassReactionTests
{
    [Test]
    public void ExplicitPassCompletesCurrentReactorAdvancesWindowAndCostsNoAp()
    {
        using (var fixture = new Fixture())
        {
            var actor = fixture.CreateUnit("Pass Actor", new UnitId(1), TeamId.Player, new GridPosition(0, 0, 0));
            var targetReactor = fixture.CreateUnit("Pass Target Reactor", new UnitId(2), TeamId.Enemy, new GridPosition(1, 0, 0));
            var farReactor = fixture.CreateUnit("Pass Far Reactor", new UnitId(3), TeamId.Enemy, new GridPosition(2, 0, 0));
            fixture.AssignLoadout(actor, fixture.MeleeSlash);

            Assert.That(fixture.Manager.StartCombat().IsSuccess, Is.True);

            var eventOrder = new List<string>();
            var reactionUnits = new List<TacticalUnit>();
            ActionIntent declaredIntent = null;
            ReactionWindow windowDuringResolution = null;
            fixture.EventBus.ActionDeclared += eventData =>
            {
                eventOrder.Add("declared");
                declaredIntent = eventData.ActionIntent as ActionIntent;
            };
            fixture.EventBus.ReactionTurnStarted += eventData =>
            {
                eventOrder.Add("reaction");
                reactionUnits.Add(eventData.Reactor);
            };
            fixture.EventBus.ActionResolved += _ =>
            {
                eventOrder.Add("resolved");
                windowDuringResolution = fixture.Manager.CurrentReactionWindow;
            };

            Assert.That(fixture.InputRouter.SelectUnit(actor).IsSuccess, Is.True);
            Assert.That(fixture.InputRouter.SelectMeleeAttack().IsSuccess, Is.True);
            Assert.That(fixture.InputRouter.ConfirmTargetUnit(targetReactor).IsSuccess, Is.True);

            Assert.That(declaredIntent, Is.Not.Null);
            Assert.That(fixture.Manager.CurrentState.Phase, Is.EqualTo(CombatPhase.ReactionWindow));
            Assert.That(fixture.Manager.CurrentState.ReactingUnit, Is.SameAs(targetReactor));
            Assert.That(fixture.Manager.CurrentState.PendingActionIntent, Is.SameAs(declaredIntent));
            Assert.That(fixture.Manager.CurrentReactionWindow.CurrentReactor, Is.SameAs(targetReactor));
            Assert.That(targetReactor.CurrentHP, Is.EqualTo(targetReactor.MaxHP));

            var commandResult = PassReactionCommand.TryCreate(
                targetReactor,
                declaredIntent,
                fixture.Manager.CurrentState,
                fixture.Manager.CurrentReactionWindow);
            Assert.That(commandResult.IsSuccess, Is.True, commandResult.ErrorMessage);
            Assert.That(commandResult.Value.Cost, Is.EqualTo(0));

            var targetApBeforePass = targetReactor.CurrentAP;
            Assert.That(fixture.InputRouter.RequestPassOrEndTurn().IsSuccess, Is.True);

            Assert.That(targetReactor.CurrentAP, Is.EqualTo(targetApBeforePass));
            Assert.That(fixture.Manager.CurrentState.Phase, Is.EqualTo(CombatPhase.ReactionWindow));
            Assert.That(fixture.Manager.CurrentState.ReactingUnit, Is.SameAs(farReactor));
            Assert.That(fixture.Manager.CurrentReactionWindow.CompletedReactors, Is.EqualTo(new[] { targetReactor }));
            Assert.That(fixture.Manager.CurrentReactionWindow.SkippedReactors, Is.Empty);
            Assert.That(targetReactor.CurrentHP, Is.EqualTo(targetReactor.MaxHP));

            Assert.That(fixture.InputRouter.RequestPassReaction().IsSuccess, Is.True);

            Assert.That(eventOrder, Is.EqualTo(new[]
            {
                "declared",
                "reaction",
                "reaction",
                "resolved"
            }));
            Assert.That(reactionUnits, Is.EqualTo(new[] { targetReactor, farReactor }));
            Assert.That(windowDuringResolution, Is.Not.Null);
            Assert.That(windowDuringResolution.IsClosed, Is.True);
            Assert.That(windowDuringResolution.CompletedReactors, Is.EqualTo(new[] { targetReactor, farReactor }));
            Assert.That(windowDuringResolution.SkippedReactors, Is.Empty);
            Assert.That(fixture.Manager.CurrentReactionWindow, Is.Null);
            Assert.That(fixture.Manager.CurrentState.Phase, Is.EqualTo(CombatPhase.ActiveTurn));
            Assert.That(fixture.Manager.CurrentState.ActiveUnit, Is.SameAs(actor));
            Assert.That(fixture.Manager.CurrentState.ReactingUnit, Is.Null);
            Assert.That(fixture.Manager.CurrentState.PendingActionIntent, Is.Null);
            Assert.That(targetReactor.CurrentHP, Is.EqualTo(targetReactor.MaxHP - fixture.MeleeSlash.Damage));
        }
    }

    [Test]
    public void PassReactionIsRejectedOutsideCurrentReactorTurnOrForWrongUnit()
    {
        using (var fixture = new Fixture())
        {
            var actor = fixture.CreateUnit("Reject Actor", new UnitId(1), TeamId.Player, new GridPosition(0, 0, 0));
            var currentReactor = fixture.CreateUnit("Reject Current Reactor", new UnitId(2), TeamId.Enemy, new GridPosition(1, 0, 0));
            var wrongReactor = fixture.CreateUnit("Reject Wrong Reactor", new UnitId(3), TeamId.Enemy, new GridPosition(2, 0, 0));
            fixture.AssignLoadout(actor, fixture.MeleeSlash);

            Assert.That(fixture.Manager.StartCombat().IsSuccess, Is.True);

            var outsideResult = fixture.Manager.PassCurrentReaction(actor);
            Assert.That(outsideResult.IsFailure, Is.True);

            Assert.That(fixture.InputRouter.SelectUnit(actor).IsSuccess, Is.True);
            Assert.That(fixture.InputRouter.SelectMeleeAttack().IsSuccess, Is.True);
            Assert.That(fixture.InputRouter.ConfirmTargetUnit(currentReactor).IsSuccess, Is.True);

            var wrongUnitResult = fixture.Manager.PassCurrentReaction(wrongReactor);
            Assert.That(wrongUnitResult.IsFailure, Is.True);
            Assert.That(wrongUnitResult.ErrorMessage, Does.Contain("current reactor"));
            Assert.That(fixture.Manager.CurrentState.Phase, Is.EqualTo(CombatPhase.ReactionWindow));
            Assert.That(fixture.Manager.CurrentState.ReactingUnit, Is.SameAs(currentReactor));
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
                displayName: "Pass Reaction Test Unit",
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
                description: "Pass reaction test ability.");

            mapDefinition = ScriptableObject.CreateInstance<GridMapDefinition>();
            mapDefinition.Configure(
                width: 4,
                depth: 2,
                defaultHeightY: 0,
                overrides: Array.Empty<GridMapDefinition.CellOverride>());

            var registryObject = CreateRoot("Pass Reaction Test Registry");
            var gridObject = CreateRoot("Pass Reaction Test Grid");
            var selectionObject = CreateRoot("Pass Reaction Test Selection");
            var routerObject = CreateRoot("Pass Reaction Test Router");
            var managerObject = CreateRoot("Pass Reaction Test Manager");

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
