using System;
using System.Collections.Generic;
using NUnit.Framework;
using ReactionTactics.Actions;
using ReactionTactics.Grid;
using ReactionTactics.Input;
using ReactionTactics.Turns;
using ReactionTactics.Units;
using UnityEditor;
using UnityEngine;

namespace ReactionTactics.Tests.EditMode.Turns
{
    public sealed class CombatManagerActionFlowTests
    {
        [Test]
        public void RoutedAttackConfirmationDeclaresResolvesSpendsApAndReturnsToActiveTurn()
        {
            using (var fixture = new Fixture())
            {
                var actor = fixture.CreateUnit("Action Actor", new UnitId(1), TeamId.Player, new GridPosition(0, 0, 0));
                var enemy = fixture.CreateUnit("Action Enemy", new UnitId(2), TeamId.Enemy, new GridPosition(1, 0, 0));
                fixture.AssignLoadout(actor, fixture.MeleeSlash);
                Assert.That(fixture.Manager.StartCombat().IsSuccess, Is.True);

                var actionPointEvents = new List<ActionPointsChangedEvent>();
                var declaredEvents = new List<ActionDeclaredEvent>();
                var resolvedEvents = new List<ActionResolvedEvent>();
                CombatPhase phaseDuringDeclaration = CombatPhase.NotStarted;
                object pendingDuringDeclaration = null;
                CombatPhase phaseDuringResolution = CombatPhase.NotStarted;
                object pendingDuringResolution = null;

                fixture.EventBus.ActionPointsChanged += actionPointEvents.Add;
                fixture.EventBus.ActionDeclared += eventData =>
                {
                    declaredEvents.Add(eventData);
                    phaseDuringDeclaration = fixture.Manager.CurrentState.Phase;
                    pendingDuringDeclaration = fixture.Manager.CurrentState.PendingActionIntent;
                };
                fixture.EventBus.ActionResolved += eventData =>
                {
                    resolvedEvents.Add(eventData);
                    phaseDuringResolution = fixture.Manager.CurrentState.Phase;
                    pendingDuringResolution = fixture.Manager.CurrentState.PendingActionIntent;
                };

                Assert.That(fixture.InputRouter.SelectUnit(actor).IsSuccess, Is.True);
                Assert.That(fixture.InputRouter.SelectMeleeAttack().IsSuccess, Is.True);
                Assert.That(fixture.InputRouter.ConfirmTargetUnit(enemy).IsSuccess, Is.True);

                Assert.That(actor.CurrentAP, Is.EqualTo(actor.MaxAP - fixture.MeleeSlash.APCost));
                Assert.That(fixture.Manager.CurrentState.CurrentRound, Is.EqualTo(1));
                Assert.That(fixture.Manager.CurrentState.Phase, Is.EqualTo(CombatPhase.ActiveTurn));
                Assert.That(fixture.Manager.CurrentState.ActiveUnit, Is.SameAs(actor));
                Assert.That(fixture.Manager.CurrentState.ReactingUnit, Is.Null);
                Assert.That(fixture.Manager.CurrentState.PendingActionIntent, Is.Null);
                Assert.That(fixture.Selection.SelectedUnit, Is.SameAs(actor));
                Assert.That(fixture.Selection.SelectedActionMode, Is.EqualTo(SelectionActionMode.None));
                Assert.That(fixture.Selection.SelectedTarget.HasTarget, Is.False);

                Assert.That(actionPointEvents.Count, Is.EqualTo(1));
                Assert.That(actionPointEvents[0].Unit, Is.SameAs(actor));
                Assert.That(actionPointEvents[0].PreviousAP, Is.EqualTo(actor.MaxAP));
                Assert.That(actionPointEvents[0].CurrentAP, Is.EqualTo(actor.MaxAP - fixture.MeleeSlash.APCost));

                Assert.That(declaredEvents.Count, Is.EqualTo(1));
                Assert.That(resolvedEvents.Count, Is.EqualTo(1));
                var intent = declaredEvents[0].ActionIntent as ActionIntent;
                Assert.That(intent, Is.Not.Null);
                Assert.That(resolvedEvents[0].ActionIntent, Is.SameAs(intent));
                Assert.That(intent.Actor, Is.SameAs(actor));
                Assert.That(intent.Ability, Is.SameAs(fixture.MeleeSlash));
                Assert.That(intent.DeclaredTargetUnit, Is.SameAs(enemy));
                Assert.That(intent.DeclaredAffectedCells, Is.EqualTo(new[] { enemy.CurrentGridPosition }));
                Assert.That(intent.DeclarationRound, Is.EqualTo(1));
                Assert.That(intent.DeclarationSequence, Is.EqualTo(0));

                Assert.That(phaseDuringDeclaration, Is.EqualTo(CombatPhase.ResolvingAction));
                Assert.That(pendingDuringDeclaration, Is.SameAs(intent));
                Assert.That(phaseDuringResolution, Is.EqualTo(CombatPhase.ResolvingAction));
                Assert.That(pendingDuringResolution, Is.SameAs(intent));
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
                    displayName: "Action Flow Test Unit",
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
                    description: "Combat manager action-flow test ability.");

                mapDefinition = ScriptableObject.CreateInstance<GridMapDefinition>();
                mapDefinition.Configure(
                    width: 3,
                    depth: 2,
                    defaultHeightY: 0,
                    overrides: Array.Empty<GridMapDefinition.CellOverride>());

                var registryObject = CreateRoot("Action Flow Test Registry");
                var gridObject = CreateRoot("Action Flow Test Grid");
                var selectionObject = CreateRoot("Action Flow Test Selection");
                var routerObject = CreateRoot("Action Flow Test Router");
                var managerObject = CreateRoot("Action Flow Test Manager");

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
}
