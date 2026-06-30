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

public sealed class NoNestedReactionWindowTests
{
        [Test]
        public void ReactionAbilityThatTriggersReactionsIsRejectedByTargetValidation()
        {
            using (var fixture = new Fixture())
            {
                var actionActor = fixture.CreateUnit("Nested Source Actor", new UnitId(1), TeamId.Player, new GridPosition(0, 0, 0));
                var reactor = fixture.CreateUnit("Nested Reactor", new UnitId(2), TeamId.Enemy, new GridPosition(1, 0, 0));
                var sourceIntent = new ActionIntent(
                    actionActor,
                    fixture.MeleeSlash,
                    actionActor.CurrentGridPosition,
                    ActionTarget.ForUnit(reactor),
                    new[] { reactor.CurrentGridPosition },
                    declarationRound: 1,
                    declarationSequence: 0);
                var combatState = new CombatState(
                    currentRound: 1,
                    phase: CombatPhase.ReactionWindow,
                    activeUnit: actionActor,
                    reactingUnit: reactor,
                    pendingActionIntent: sourceIntent);
                var nestedReaction = CreateAbility(
                    "counter_slash",
                    "Counter Slash",
                    AbilityUsage.Reaction,
                    AbilityTiming.Telegraphed,
                    AbilityShape.Melee,
                    apCost: 2,
                    range: 0,
                    radius: 0,
                    damage: 3,
                    triggersReactions: true);

                try
                {
                    var apBeforeValidation = reactor.CurrentAP;
                    var context = new AbilityTargetValidationContext(
                        reactor,
                        nestedReaction,
                        ActionTarget.ForUnit(actionActor),
                        AbilityUsage.Reaction,
                        combatState,
                        fixture.Map);

                    var result = AbilityTargetValidator.Instance.ValidateTarget(context);

                    Assert.That(result.IsFailure, Is.True);
                    Assert.That(result.ErrorMessage, Does.Contain("reactions do not trigger full reaction windows"));
                    Assert.That(result.ErrorMessage, Does.Contain("explicit nested-window rule"));
                    Assert.That(reactor.CurrentAP, Is.EqualTo(apBeforeValidation));
                }
                finally
                {
                    Destroy(nestedReaction);
                }
            }
        }

        [Test]
        public void CombatManagerRejectsNestedWindowWhileReactionWindowIsOpen()
        {
            using (var fixture = new Fixture())
            {
                var actionActor = fixture.CreateUnit("Nested Guard Actor", new UnitId(1), TeamId.Player, new GridPosition(0, 0, 0));
                var reactor = fixture.CreateUnit("Nested Guard Reactor", new UnitId(2), TeamId.Enemy, new GridPosition(1, 0, 0));
                fixture.AssignLoadout(actionActor, fixture.MeleeSlash);
                Assert.That(fixture.Manager.StartCombat().IsSuccess, Is.True);

                ActionIntent sourceIntent = null;
                fixture.EventBus.ActionDeclared += eventData => sourceIntent = eventData.ActionIntent as ActionIntent;

                Assert.That(fixture.InputRouter.SelectUnit(actionActor).IsSuccess, Is.True);
                Assert.That(fixture.InputRouter.SelectMeleeAttack().IsSuccess, Is.True);
                Assert.That(fixture.InputRouter.ConfirmTargetUnit(reactor).IsSuccess, Is.True);
                Assert.That(sourceIntent, Is.Not.Null);
                Assert.That(fixture.Manager.CurrentState.Phase, Is.EqualTo(CombatPhase.ReactionWindow));
                Assert.That(fixture.Manager.CurrentReactionWindow.SourceIntent, Is.SameAs(sourceIntent));

                var nestedReaction = CreateAbility(
                    "nested_counter",
                    "Nested Counter",
                    AbilityUsage.Reaction,
                    AbilityTiming.Telegraphed,
                    AbilityShape.Melee,
                    apCost: 1,
                    range: 0,
                    radius: 0,
                    damage: 2,
                    triggersReactions: true);

                try
                {
                    var nestedIntent = new ActionIntent(
                        reactor,
                        nestedReaction,
                        reactor.CurrentGridPosition,
                        ActionTarget.ForUnit(actionActor),
                        new[] { actionActor.CurrentGridPosition },
                        fixture.Manager.CurrentState.CurrentRound,
                        declarationSequence: 99);

                    var guardResult = fixture.Manager.ValidateCanOpenReactionWindow(nestedIntent);

                    Assert.That(guardResult.IsFailure, Is.True);
                    Assert.That(guardResult.ErrorMessage, Does.Contain("nested reaction window"));
                    Assert.That(guardResult.ErrorMessage, Does.Contain("Reactions do not trigger full reaction windows"));
                    Assert.That(fixture.Manager.CurrentReactionWindow.SourceIntent, Is.SameAs(sourceIntent));
                    Assert.That(fixture.Manager.CurrentState.ReactingUnit, Is.SameAs(reactor));
                }
                finally
                {
                    Destroy(nestedReaction);
                }

                Assert.That(fixture.Manager.PassCurrentReaction(reactor).IsSuccess, Is.True);
                Assert.That(fixture.Manager.CurrentReactionWindow, Is.Null);
                Assert.That(fixture.Manager.CurrentState.Phase, Is.EqualTo(CombatPhase.ActiveTurn));
            }
        }

        [Test]
        public void ReactionMovementDoesNotOpenNestedWindowBeforeOriginalActionResolves()
        {
            using (var fixture = new Fixture())
            {
                var actionActor = fixture.CreateUnit("Reaction Move Actor", new UnitId(1), TeamId.Player, new GridPosition(0, 0, 0));
                var reactor = fixture.CreateUnit("Reaction Move Reactor", new UnitId(2), TeamId.Enemy, new GridPosition(1, 0, 0));
                fixture.AssignLoadout(actionActor, fixture.MeleeSlash);
                Assert.That(fixture.Manager.StartCombat().IsSuccess, Is.True);

                var actionDeclarations = 0;
                var reactionTurns = 0;
                var resolvedActions = 0;
                fixture.EventBus.ActionDeclared += _ => actionDeclarations += 1;
                fixture.EventBus.ReactionTurnStarted += _ => reactionTurns += 1;
                fixture.EventBus.ActionResolved += _ => resolvedActions += 1;

                Assert.That(fixture.InputRouter.SelectUnit(actionActor).IsSuccess, Is.True);
                Assert.That(fixture.InputRouter.SelectMeleeAttack().IsSuccess, Is.True);
                Assert.That(fixture.InputRouter.ConfirmTargetUnit(reactor).IsSuccess, Is.True);

                Assert.That(actionDeclarations, Is.EqualTo(1));
                Assert.That(reactionTurns, Is.EqualTo(1));
                Assert.That(resolvedActions, Is.EqualTo(0));
                Assert.That(fixture.Manager.CurrentState.ReactingUnit, Is.SameAs(reactor));

                var moveResult = fixture.Manager.MoveCurrentReaction(reactor, new GridPosition(2, 0, 0));

                Assert.That(moveResult.IsSuccess, Is.True, moveResult.ErrorMessage);
                Assert.That(actionDeclarations, Is.EqualTo(1));
                Assert.That(reactionTurns, Is.EqualTo(1));
                Assert.That(resolvedActions, Is.EqualTo(1));
                Assert.That(fixture.Manager.CurrentReactionWindow, Is.Null);
                Assert.That(fixture.Manager.CurrentState.Phase, Is.EqualTo(CombatPhase.ActiveTurn));
                Assert.That(reactor.CurrentGridPosition, Is.EqualTo(new GridPosition(2, 0, 0)));
                Assert.That(reactor.CurrentHP, Is.EqualTo(reactor.MaxHP));
            }
        }

        private static AbilityDefinition CreateAbility(
            string abilityKey,
            string displayName,
            AbilityUsage usage,
            AbilityTiming timing,
            AbilityShape shape,
            int apCost,
            int range,
            int radius,
            int damage,
            bool triggersReactions)
        {
            var ability = ScriptableObject.CreateInstance<AbilityDefinition>();
            ability.Configure(
                abilityKey,
                displayName,
                apCost,
                usage,
                timing,
                shape,
                range,
                radius,
                damage,
                triggersReactions);
            return ability;
        }

        private static void Destroy(UnityEngine.Object asset)
        {
            if (asset != null)
            {
                UnityEngine.Object.DestroyImmediate(asset);
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
                    displayName: "No Nested Window Test Unit",
                    maxHP: 10,
                    maxAP: 6,
                    movementAnimationSpeed: 4f,
                    meleeRange: 1,
                    teamColorHint: Color.white);

                MeleeSlash = CreateAbility(
                    "melee_slash",
                    "Melee Slash",
                    AbilityUsage.Action,
                    AbilityTiming.Telegraphed,
                    AbilityShape.Melee,
                    apCost: 3,
                    range: 0,
                    radius: 0,
                    damage: 4,
                    triggersReactions: true);

                mapDefinition = ScriptableObject.CreateInstance<GridMapDefinition>();
                mapDefinition.Configure(
                    width: 4,
                    depth: 1,
                    defaultHeightY: 0,
                    overrides: Array.Empty<GridMapDefinition.CellOverride>());

                var registryObject = CreateRoot("No Nested Window Test Registry");
                var gridObject = CreateRoot("No Nested Window Test Grid");
                var selectionObject = CreateRoot("No Nested Window Test Selection");
                var routerObject = CreateRoot("No Nested Window Test Router");
                var managerObject = CreateRoot("No Nested Window Test Manager");

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

            public IGridMap Map
            {
                get { return GridManager.CurrentMap; }
            }

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

                Destroy(mapDefinition);
                Destroy(MeleeSlash);
                Destroy(stats);
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
