using System;
using System.Collections.Generic;
using NUnit.Framework;
using ReactionTactics.AI;
using ReactionTactics.Actions;
using ReactionTactics.Grid;
using ReactionTactics.Input;
using ReactionTactics.Turns;
using ReactionTactics.Units;
using UnityEditor;
using UnityEngine;

namespace ReactionTactics.Tests.EditMode.AI
{
    public sealed class AiControllerTests
    {
        [Test]
        public void EnemyActiveTurnDelegatesToAiPassAndReturnsToPlayerWithoutInput()
        {
            using (var fixture = new Fixture(includeAi: true, includeMap: false))
            {
                var player = fixture.CreateUnit("AI Shell Player", new UnitId(1), TeamId.Player, new GridPosition(0, 0, 0));
                var enemy = fixture.CreateUnit("AI Shell Enemy", new UnitId(2), TeamId.Enemy, new GridPosition(1, 0, 0));

                Assert.That(fixture.Manager.StartCombat().IsSuccess, Is.True);
                Assert.That(fixture.Manager.CurrentState.ActiveUnit, Is.SameAs(player));
                Assert.That(fixture.Manager.CurrentState.CurrentRound, Is.EqualTo(1));

                var result = fixture.Manager.EndActiveTurn();

                Assert.That(result.IsSuccess, Is.True, result.ErrorMessage);
                Assert.That(enemy.CurrentAP, Is.EqualTo(enemy.MaxAP));
                Assert.That(fixture.Manager.CurrentState.Phase, Is.EqualTo(CombatPhase.ActiveTurn));
                Assert.That(fixture.Manager.CurrentState.ActiveUnit, Is.SameAs(player));
                Assert.That(fixture.Manager.CurrentState.CurrentRound, Is.EqualTo(2));
            }
        }

        [Test]
        public void EnemyReactionDelegatesToAiMoveAndResolvesPendingActionWithoutInput()
        {
            using (var fixture = new Fixture(includeAi: true, includeMap: true))
            {
                var actor = fixture.CreateUnit("AI Shell Actor", new UnitId(1), TeamId.Player, new GridPosition(0, 0, 0));
                var enemyReactor = fixture.CreateUnit("AI Shell Reactor", new UnitId(2), TeamId.Enemy, new GridPosition(1, 0, 0));
                fixture.AssignLoadout(actor, fixture.MeleeSlash);

                Assert.That(fixture.Manager.StartCombat().IsSuccess, Is.True);

                var start = enemyReactor.CurrentGridPosition;
                var apBeforeReaction = enemyReactor.CurrentAP;
                Assert.That(fixture.InputRouter.SelectUnit(actor).IsSuccess, Is.True);
                Assert.That(fixture.InputRouter.SelectMeleeAttack().IsSuccess, Is.True);
                var confirmResult = fixture.InputRouter.ConfirmTargetUnit(enemyReactor);

                Assert.That(confirmResult.IsSuccess, Is.True, confirmResult.ErrorMessage);
                Assert.That(enemyReactor.CurrentGridPosition, Is.EqualTo(new GridPosition(2, 0, 0)));
                Assert.That(enemyReactor.CurrentAP, Is.EqualTo(apBeforeReaction - 1));
                Assert.That(fixture.Registry.IsOccupied(start), Is.False);
                Assert.That(fixture.Registry.IsOccupied(enemyReactor.CurrentGridPosition), Is.True);
                Assert.That(fixture.Manager.CurrentReactionWindow, Is.Null);
                Assert.That(fixture.Manager.CurrentState.Phase, Is.EqualTo(CombatPhase.ActiveTurn));
                Assert.That(fixture.Manager.CurrentState.ActiveUnit, Is.SameAs(actor));
                Assert.That(fixture.Manager.CurrentState.ReactingUnit, Is.Null);
                Assert.That(fixture.Manager.CurrentState.PendingActionIntent, Is.Null);
                Assert.That(enemyReactor.CurrentHP, Is.EqualTo(enemyReactor.MaxHP));
            }
        }

        private sealed class Fixture : IDisposable
        {
            private readonly List<GameObject> rootObjects = new List<GameObject>();
            private readonly UnitStatsDefinition stats;
            private readonly GridMapDefinition mapDefinition;

            public Fixture(bool includeAi, bool includeMap)
            {
                stats = ScriptableObject.CreateInstance<UnitStatsDefinition>();
                stats.Configure(
                    displayName: "AI Shell Test Unit",
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
                    description: "AI shell reaction test ability.");

                var registryObject = CreateRoot("AI Shell Test Registry");
                var gridObject = CreateRoot("AI Shell Test Grid");
                var selectionObject = CreateRoot("AI Shell Test Selection");
                var routerObject = CreateRoot("AI Shell Test Router");
                var managerObject = CreateRoot("AI Shell Test Manager");

                gridObject.SetActive(false);
                Registry = registryObject.AddComponent<UnitRegistry>();
                GridManager = gridObject.AddComponent<GridManager>();
                if (includeMap)
                {
                    mapDefinition = ScriptableObject.CreateInstance<GridMapDefinition>();
                    mapDefinition.Configure(
                        width: 4,
                        depth: 1,
                        defaultHeightY: 0,
                        overrides: Array.Empty<GridMapDefinition.CellOverride>());
                    AssignMapDefinition(GridManager, mapDefinition);
                    Assert.That(GridManager.RebuildMap(), Is.True);
                }

                Selection = selectionObject.AddComponent<SelectionController>();
                InputRouter = routerObject.AddComponent<PlayerCommandRouter>();
                InputRouter.SelectionController = Selection;
                EventBus = managerObject.AddComponent<CombatEventBus>();
                AiController = includeAi ? managerObject.AddComponent<AiController>() : null;
                if (AiController != null)
                {
                    AiController.LogDecisions = false;
                }

                Manager = managerObject.AddComponent<CombatManager>();
                Manager.Configure(Registry, GridManager, InputRouter, EventBus, AiController);
                Manager.StartCombatOnStart = false;
                Manager.LogCombatStart = false;
                Manager.LogActionFlow = false;
            }

            public UnitRegistry Registry { get; }

            public GridManager GridManager { get; }

            public SelectionController Selection { get; }

            public PlayerCommandRouter InputRouter { get; }

            public CombatEventBus EventBus { get; }

            public AiController AiController { get; }

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

                if (mapDefinition != null)
                {
                    UnityEngine.Object.DestroyImmediate(mapDefinition);
                }

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
