using System;
using System.Collections.Generic;
using NUnit.Framework;
using ReactionTactics.AI;
using ReactionTactics.Actions;
using ReactionTactics.Grid;
using ReactionTactics.Input;
using ReactionTactics.Pathfinding;
using ReactionTactics.Turns;
using ReactionTactics.Units;
using UnityEditor;
using UnityEngine;

public sealed class AiMovementTests
    {
        [Test]
        public void ChooseActiveMoveDestinationMinimizesDistanceWithinReactionReserveBudget()
        {
            using (var fixture = new Fixture(width: 6, depth: 1))
            {
                var enemy = fixture.CreateUnit("AI Movement Enemy", new UnitId(2), TeamId.Enemy, new GridPosition(0, 0, 0));
                var player = fixture.CreateUnit("AI Movement Player", new UnitId(1), TeamId.Player, new GridPosition(5, 0, 0));
                enemy.SetAPForTest(6);
                fixture.Controller.ReactionApReserve = 2;

                var destinationResult = fixture.Controller.TryChooseActiveMoveDestination(
                    enemy,
                    player,
                    fixture.GridManager.CurrentMap,
                    fixture.Registry);

                Assert.That(destinationResult.IsSuccess, Is.True, destinationResult.ErrorMessage);
                Assert.That(destinationResult.Value, Is.EqualTo(new GridPosition(4, 0, 0)));

                var path = new ReachableCellSearch().TryFindPath(
                    fixture.GridManager.CurrentMap,
                    enemy.CurrentGridPosition,
                    destinationResult.Value,
                    enemy.CurrentAP,
                    fixture.Registry);

                Assert.That(path.IsValid, Is.True, path.FailureReason);
                Assert.That(path.TotalApCost, Is.EqualTo(4));
                Assert.That(enemy.CurrentAP - path.TotalApCost, Is.GreaterThanOrEqualTo(fixture.Controller.ReactionApReserve));
            }
        }

        [Test]
        public void EnemyActiveTurnMovesTowardNearestTargetThenEndsTurn()
        {
            using (var fixture = new Fixture(width: 6, depth: 1))
            {
                var player = fixture.CreateUnit("AI Movement Player", new UnitId(1), TeamId.Player, new GridPosition(5, 0, 0));
                var enemy = fixture.CreateUnit("AI Movement Enemy", new UnitId(2), TeamId.Enemy, new GridPosition(0, 0, 0));
                var enemyStart = enemy.CurrentGridPosition;

                Assert.That(fixture.Manager.StartCombat().IsSuccess, Is.True);
                Assert.That(fixture.Manager.CurrentState.ActiveUnit, Is.SameAs(player));

                var result = fixture.Manager.EndActiveTurn();

                Assert.That(result.IsSuccess, Is.True, result.ErrorMessage);
                Assert.That(enemy.CurrentGridPosition, Is.EqualTo(new GridPosition(4, 0, 0)));
                Assert.That(fixture.Registry.IsOccupied(enemyStart), Is.False);
                Assert.That(fixture.Registry.TryGetOccupant(enemy.CurrentGridPosition, out var occupantId), Is.True);
                Assert.That(occupantId, Is.EqualTo(enemy.UnitId));
                Assert.That(fixture.Manager.CurrentReactionWindow, Is.Null);
                Assert.That(fixture.Manager.CurrentState.Phase, Is.EqualTo(CombatPhase.ActiveTurn));
                Assert.That(fixture.Manager.CurrentState.ActiveUnit, Is.SameAs(player));
                Assert.That(fixture.Manager.CurrentState.CurrentRound, Is.EqualTo(2));
            }
        }

        [Test]
        public void EnemyActiveTurnDoesNotMoveWhenValidAttackIsAvailable()
        {
            using (var fixture = new Fixture(width: 6, depth: 1))
            {
                var player = fixture.CreateUnit("AI Movement Cone Target", new UnitId(1), TeamId.Player, new GridPosition(3, 0, 0));
                var enemy = fixture.CreateUnit("AI Movement Cone Archer", new UnitId(2), TeamId.Enemy, new GridPosition(0, 0, 0));
                fixture.AssignLoadout(enemy, fixture.ConeShot);
                var enemyStart = enemy.CurrentGridPosition;

                Assert.That(fixture.Manager.StartCombat().IsSuccess, Is.True);
                Assert.That(fixture.Manager.EndActiveTurn().IsSuccess, Is.True);

                Assert.That(enemy.CurrentGridPosition, Is.EqualTo(enemyStart));
                Assert.That(fixture.Registry.TryGetOccupant(enemyStart, out var occupantId), Is.True);
                Assert.That(occupantId, Is.EqualTo(enemy.UnitId));
                Assert.That(player.CurrentGridPosition, Is.EqualTo(new GridPosition(3, 0, 0)));
            }
        }

        private sealed class Fixture : IDisposable
        {
            private readonly List<GameObject> rootObjects = new List<GameObject>();
            private readonly UnitStatsDefinition stats;
            private readonly GridMapDefinition mapDefinition;

            public Fixture(int width, int depth)
            {
                stats = ScriptableObject.CreateInstance<UnitStatsDefinition>();
                stats.Configure(
                    displayName: "AI Movement Test Unit",
                    maxHP: 10,
                    maxAP: 6,
                    movementAnimationSpeed: 4f,
                    meleeRange: 1,
                    teamColorHint: Color.white);

                ConeShot = ScriptableObject.CreateInstance<AbilityDefinition>();
                ConeShot.Configure(
                    abilityKey: "cone_shot",
                    displayName: "Cone Shot",
                    apCost: 4,
                    usage: AbilityUsage.Action,
                    timing: AbilityTiming.Telegraphed,
                    shape: AbilityShape.Cone,
                    range: 4,
                    radius: 0,
                    damage: 3,
                    triggersReactions: true,
                    description: "AI movement valid-attack test ability.");

                mapDefinition = ScriptableObject.CreateInstance<GridMapDefinition>();
                mapDefinition.Configure(
                    width: width,
                    depth: depth,
                    defaultHeightY: 0,
                    overrides: Array.Empty<GridMapDefinition.CellOverride>());

                var registryObject = CreateRoot("AI Movement Test Registry");
                var gridObject = CreateRoot("AI Movement Test Grid");
                var selectionObject = CreateRoot("AI Movement Test Selection");
                var routerObject = CreateRoot("AI Movement Test Router");
                var managerObject = CreateRoot("AI Movement Test Manager");

                gridObject.SetActive(false);
                Registry = registryObject.AddComponent<UnitRegistry>();
                GridManager = gridObject.AddComponent<GridManager>();
                AssignMapDefinition(GridManager, mapDefinition);
                Assert.That(GridManager.RebuildMap(), Is.True);

                Selection = selectionObject.AddComponent<SelectionController>();
                Router = routerObject.AddComponent<PlayerCommandRouter>();
                Router.SelectionController = Selection;
                EventBus = managerObject.AddComponent<CombatEventBus>();
                Controller = managerObject.AddComponent<AiController>();
                Controller.LogDecisions = false;
                Manager = managerObject.AddComponent<CombatManager>();
                Manager.Configure(Registry, GridManager, Router, EventBus, Controller);
                Manager.StartCombatOnStart = false;
                Manager.LogCombatStart = false;
                Manager.LogActionFlow = false;
            }

            public UnitRegistry Registry { get; }

            public GridManager GridManager { get; }

            public SelectionController Selection { get; }

            public PlayerCommandRouter Router { get; }

            public CombatEventBus EventBus { get; }

            public AiController Controller { get; }

            public CombatManager Manager { get; }

            public AbilityDefinition ConeShot { get; }

            public TacticalUnit CreateUnit(string name, UnitId unitId, TeamId team, GridPosition position)
            {
                var gameObject = new GameObject(name);
                gameObject.transform.SetParent(Registry.transform, worldPositionStays: false);
                var unit = gameObject.AddComponent<TacticalUnit>();
                unit.Initialize(unitId, team, stats, position);
                Assert.That(Registry.Register(unit).IsSuccess, Is.True);
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
                UnityEngine.Object.DestroyImmediate(ConeShot);
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
