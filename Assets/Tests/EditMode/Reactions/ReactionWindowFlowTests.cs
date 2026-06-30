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

public sealed class ReactionWindowFlowTests
{
    [Test]
    public void TriggeringActionAutoPassesOrderedReactorsBeforeResolvingOriginalAction()
    {
        using (var fixture = new Fixture())
        {
            var actor = fixture.CreateUnit("Flow Actor", new UnitId(1), TeamId.Player, new GridPosition(0, 0, 0));
            var allyReactor = fixture.CreateUnit("Flow Ally Reactor", new UnitId(2), TeamId.Player, new GridPosition(0, 0, 2));
            var farEnemyReactor = fixture.CreateUnit("Flow Far Enemy Reactor", new UnitId(3), TeamId.Enemy, new GridPosition(3, 0, 0));
            var targetReactor = fixture.CreateUnit("Flow Target Reactor", new UnitId(4), TeamId.Enemy, new GridPosition(1, 0, 0));
            fixture.AssignLoadout(actor, fixture.MeleeSlash);

            Assert.That(fixture.Manager.StartCombat().IsSuccess, Is.True);

            var eventOrder = new List<string>();
            var reactionUnits = new List<TacticalUnit>();
            var reactionPhases = new List<CombatPhase>();
            var reactingUnitsDuringEvents = new List<TacticalUnit>();
            var pendingIntentsDuringReactions = new List<object>();
            ActionIntent declaredIntent = null;
            ActionIntent resolvedIntent = null;
            CombatPhase phaseDuringDeclaration = CombatPhase.NotStarted;
            CombatPhase phaseDuringResolution = CombatPhase.NotStarted;
            ReactionWindow windowDuringResolution = null;
            TacticalUnit[] skippedReactorsDuringResolution = null;

            fixture.EventBus.ActionDeclared += eventData =>
            {
                eventOrder.Add("declared");
                declaredIntent = eventData.ActionIntent as ActionIntent;
                phaseDuringDeclaration = fixture.Manager.CurrentState.Phase;
                Assert.That(fixture.Manager.CurrentReactionWindow, Is.Not.Null);
                Assert.That(fixture.Manager.CurrentReactionWindow.IsOpen, Is.True);
            };
            fixture.EventBus.ReactionTurnStarted += eventData =>
            {
                eventOrder.Add("reaction");
                reactionUnits.Add(eventData.Reactor);
                reactionPhases.Add(fixture.Manager.CurrentState.Phase);
                reactingUnitsDuringEvents.Add(fixture.Manager.CurrentState.ReactingUnit);
                pendingIntentsDuringReactions.Add(fixture.Manager.CurrentState.PendingActionIntent);
                Assert.That(fixture.Manager.CurrentReactionWindow.CurrentReactor, Is.SameAs(eventData.Reactor));
            };
            fixture.EventBus.ActionResolved += eventData =>
            {
                eventOrder.Add("resolved");
                resolvedIntent = eventData.ActionIntent as ActionIntent;
                phaseDuringResolution = fixture.Manager.CurrentState.Phase;
                windowDuringResolution = fixture.Manager.CurrentReactionWindow;
                skippedReactorsDuringResolution = ToArray(windowDuringResolution.SkippedReactors);
            };

            Assert.That(fixture.InputRouter.SelectUnit(actor).IsSuccess, Is.True);
            Assert.That(fixture.InputRouter.SelectMeleeAttack().IsSuccess, Is.True);
            Assert.That(fixture.InputRouter.ConfirmTargetUnit(targetReactor).IsSuccess, Is.True);

            Assert.That(eventOrder, Is.EqualTo(new[] { "declared", "reaction", "reaction", "reaction", "resolved" }));
            Assert.That(declaredIntent, Is.Not.Null);
            Assert.That(resolvedIntent, Is.SameAs(declaredIntent));
            Assert.That(phaseDuringDeclaration, Is.EqualTo(CombatPhase.ReactionWindow));
            Assert.That(phaseDuringResolution, Is.EqualTo(CombatPhase.ResolvingAction));
            Assert.That(reactionUnits.ToArray(), Is.EqualTo(new[] { targetReactor, allyReactor, farEnemyReactor }));
            Assert.That(reactionPhases.ToArray(), Is.EqualTo(new[]
            {
                CombatPhase.ReactionWindow,
                CombatPhase.ReactionWindow,
                CombatPhase.ReactionWindow
            }));
            Assert.That(reactingUnitsDuringEvents.ToArray(), Is.EqualTo(reactionUnits.ToArray()));
            Assert.That(pendingIntentsDuringReactions.ToArray(), Is.EqualTo(new object[]
            {
                declaredIntent,
                declaredIntent,
                declaredIntent
            }));

            Assert.That(windowDuringResolution, Is.Not.Null);
            Assert.That(windowDuringResolution.IsClosed, Is.True);
            Assert.That(skippedReactorsDuringResolution, Is.EqualTo(new[] { targetReactor, allyReactor, farEnemyReactor }));
            Assert.That(fixture.Manager.CurrentReactionWindow, Is.Null);
            Assert.That(fixture.Manager.CurrentState.Phase, Is.EqualTo(CombatPhase.ActiveTurn));
            Assert.That(fixture.Manager.CurrentState.ActiveUnit, Is.SameAs(actor));
            Assert.That(fixture.Manager.CurrentState.ReactingUnit, Is.Null);
            Assert.That(fixture.Manager.CurrentState.PendingActionIntent, Is.Null);
            Assert.That(targetReactor.CurrentHP, Is.EqualTo(targetReactor.MaxHP - fixture.MeleeSlash.Damage));
        }
    }

    private static TacticalUnit[] ToArray(IReadOnlyList<TacticalUnit> units)
    {
        var result = new TacticalUnit[units.Count];
        for (var i = 0; i < units.Count; i += 1)
        {
            result[i] = units[i];
        }

        return result;
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
                displayName: "Reaction Flow Test Unit",
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
                description: "Reaction-window flow test ability.");

            mapDefinition = ScriptableObject.CreateInstance<GridMapDefinition>();
            mapDefinition.Configure(
                width: 4,
                depth: 3,
                defaultHeightY: 0,
                overrides: Array.Empty<GridMapDefinition.CellOverride>());

            var registryObject = CreateRoot("Reaction Flow Test Registry");
            var gridObject = CreateRoot("Reaction Flow Test Grid");
            var selectionObject = CreateRoot("Reaction Flow Test Selection");
            var routerObject = CreateRoot("Reaction Flow Test Router");
            var managerObject = CreateRoot("Reaction Flow Test Manager");

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
