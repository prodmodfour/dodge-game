using System;
using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using ReactionTactics.Actions;
using ReactionTactics.Grid;
using ReactionTactics.Input;
using ReactionTactics.Turns;
using ReactionTactics.Units;
using UnityEditor;
using UnityEngine;

public sealed class CombatEndTests
{
        [Test]
        public void DefeatingLastEnemyEntersCombatOverAndPublishesVictory()
        {
            using (var fixture = new Fixture())
            {
                var player = fixture.CreateUnit("Player", new UnitId(1), TeamId.Player, new GridPosition(0, 0, 0));
                var enemy = fixture.CreateUnit("Enemy", new UnitId(2), TeamId.Enemy, new GridPosition(1, 0, 0));
                var endedEvents = new List<CombatEndedEvent>();
                var logMessages = new List<string>();
                fixture.EventBus.CombatEnded += endedEvents.Add;
                fixture.EventBus.CombatLogMessageAdded += entry => logMessages.Add(entry.Message);

                Assert.That(fixture.Manager.StartCombat().IsSuccess, Is.True);

                var damageResult = enemy.ApplyDamage(enemy.MaxHP, DamageSource.Environmental("combat end test"));

                Assert.That(damageResult.IsSuccess, Is.True, damageResult.ErrorMessage);
                Assert.That(fixture.Manager.CurrentState.Phase, Is.EqualTo(CombatPhase.CombatOver));
                Assert.That(fixture.Manager.CurrentState.ActiveUnit, Is.Null);
                Assert.That(fixture.Manager.CurrentState.CurrentReactor, Is.Null);
                Assert.That(fixture.Manager.CurrentReactionWindow, Is.Null);
                Assert.That(fixture.Manager.HasCombatEndOutcome, Is.True);
                Assert.That(fixture.Manager.HasWinningTeam, Is.True);
                Assert.That(fixture.Manager.WinningTeam, Is.EqualTo(TeamId.Player));
                Assert.That(endedEvents.Count, Is.EqualTo(1));
                Assert.That(endedEvents[0].HasWinningTeam, Is.True);
                Assert.That(endedEvents[0].WinningTeam, Is.EqualTo(TeamId.Player));
                Assert.That(logMessages.Any(message => message.Contains("Player team won combat")), Is.True);
                Assert.That(fixture.Manager.ValidateUnitCanTakeAction(player).IsFailure, Is.True);
            }
        }

        [Test]
        public void DefeatingLastPlayerReportsEnemyWinnerAndDisablesTurnCommands()
        {
            using (var fixture = new Fixture())
            {
                var player = fixture.CreateUnit("Player", new UnitId(1), TeamId.Player, new GridPosition(0, 0, 0));
                var enemy = fixture.CreateUnit("Enemy", new UnitId(2), TeamId.Enemy, new GridPosition(1, 0, 0));
                var endedEvents = new List<CombatEndedEvent>();
                fixture.EventBus.CombatEnded += endedEvents.Add;

                Assert.That(fixture.Manager.StartCombat().IsSuccess, Is.True);
                Assert.That(player.ApplyDamage(player.MaxHP, DamageSource.Environmental("combat end test")).IsSuccess, Is.True);

                Assert.That(fixture.Manager.CurrentState.Phase, Is.EqualTo(CombatPhase.CombatOver));
                Assert.That(fixture.Manager.HasWinningTeam, Is.True);
                Assert.That(fixture.Manager.WinningTeam, Is.EqualTo(TeamId.Enemy));
                Assert.That(endedEvents.Count, Is.EqualTo(1));
                Assert.That(endedEvents[0].WinningTeam, Is.EqualTo(TeamId.Enemy));

                var endTurnResult = fixture.Manager.EndActiveTurn();
                var enemyActionResult = fixture.Manager.ValidateUnitCanTakeAction(enemy);

                Assert.That(endTurnResult.IsFailure, Is.True);
                Assert.That(endTurnResult.ErrorMessage, Does.Contain("combat is already over"));
                Assert.That(enemyActionResult.IsFailure, Is.True);
                Assert.That(enemyActionResult.ErrorMessage, Does.Contain(nameof(CombatPhase.CombatOver)));
            }
        }

        [Test]
        public void ActionResolutionThatDefeatsFinalTeamDoesNotReturnToActiveTurn()
        {
            using (var fixture = new Fixture())
            {
                var actor = fixture.CreateUnit("Knight", new UnitId(1), TeamId.Player, new GridPosition(0, 0, 0));
                var enemy = fixture.CreateUnit("Goblin", new UnitId(2), TeamId.Enemy, new GridPosition(1, 0, 0));
                fixture.AssignLoadout(actor, fixture.LethalMeleeSlash);
                var endedEvents = new List<CombatEndedEvent>();
                fixture.EventBus.CombatEnded += endedEvents.Add;

                Assert.That(fixture.Manager.StartCombat().IsSuccess, Is.True);
                Assert.That(fixture.InputRouter.SelectUnit(actor).IsSuccess, Is.True);
                Assert.That(fixture.InputRouter.SelectMeleeAttack().IsSuccess, Is.True);
                Assert.That(fixture.InputRouter.ConfirmTargetUnit(enemy).IsSuccess, Is.True);
                Assert.That(fixture.Manager.CurrentState.Phase, Is.EqualTo(CombatPhase.ReactionWindow));

                Assert.That(fixture.InputRouter.RequestPassOrEndTurn().IsSuccess, Is.True);

                Assert.That(enemy.IsDead, Is.True);
                Assert.That(fixture.Manager.CurrentState.Phase, Is.EqualTo(CombatPhase.CombatOver));
                Assert.That(fixture.Manager.CurrentState.ActiveUnit, Is.Null);
                Assert.That(fixture.Manager.CurrentState.PendingActionIntent, Is.Null);
                Assert.That(fixture.Manager.HasWinningTeam, Is.True);
                Assert.That(fixture.Manager.WinningTeam, Is.EqualTo(TeamId.Player));
                Assert.That(endedEvents.Select(e => e.WinningTeam).ToArray(), Is.EqualTo(new[] { TeamId.Player }));
            }
        }

        [Test]
        public void StartingCombatWithOnlyOneTeamDoesNotEndBeforeAnOpponentExists()
        {
            using (var fixture = new Fixture())
            {
                var soloPlayer = fixture.CreateUnit("Solo Player", new UnitId(1), TeamId.Player, new GridPosition(0, 0, 0));
                var endedEvents = new List<CombatEndedEvent>();
                fixture.EventBus.CombatEnded += endedEvents.Add;

                var result = fixture.Manager.StartCombat();

                Assert.That(result.IsSuccess, Is.True, result.ErrorMessage);
                Assert.That(fixture.Manager.CurrentState.Phase, Is.EqualTo(CombatPhase.ActiveTurn));
                Assert.That(fixture.Manager.CurrentState.ActiveUnit, Is.SameAs(soloPlayer));
                Assert.That(fixture.Manager.HasCombatEndOutcome, Is.False);
                Assert.That(endedEvents.Count, Is.EqualTo(0));
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
                    displayName: "Combat End Test Unit",
                    maxHP: 6,
                    maxAP: 6,
                    movementAnimationSpeed: 4f,
                    meleeRange: 1,
                    teamColorHint: Color.white);

                LethalMeleeSlash = ScriptableObject.CreateInstance<AbilityDefinition>();
                LethalMeleeSlash.Configure(
                    abilityKey: "lethal_melee_slash",
                    displayName: "Lethal Melee Slash",
                    apCost: 3,
                    usage: AbilityUsage.Action,
                    timing: AbilityTiming.Telegraphed,
                    shape: AbilityShape.Melee,
                    range: 0,
                    radius: 0,
                    damage: 12,
                    triggersReactions: true,
                    description: "Combat end test ability.");

                mapDefinition = ScriptableObject.CreateInstance<GridMapDefinition>();
                mapDefinition.Configure(
                    width: 3,
                    depth: 2,
                    defaultHeightY: 0,
                    overrides: Array.Empty<GridMapDefinition.CellOverride>());

                var registryObject = CreateRoot("Combat End Test Registry");
                var gridObject = CreateRoot("Combat End Test Grid");
                var selectionObject = CreateRoot("Combat End Test Selection");
                var routerObject = CreateRoot("Combat End Test Router");
                var managerObject = CreateRoot("Combat End Test Manager");

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

            public AbilityDefinition LethalMeleeSlash { get; }

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
                UnityEngine.Object.DestroyImmediate(LethalMeleeSlash);
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
