using System;
using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using ReactionTactics.Actions;
using ReactionTactics.Grid;
using ReactionTactics.Input;
using ReactionTactics.Reactions;
using ReactionTactics.Turns;
using ReactionTactics.UI;
using ReactionTactics.Units;
using UnityEditor;
using UnityEngine;

namespace ReactionTactics.Tests.EditMode.UI
{
    public sealed class ReactionMovementSafetyPreviewControllerTests
    {
        [Test]
        public void RefreshPreviewHighlightsReachableReactionDestinationsBySafety()
        {
            using (var fixture = new Fixture())
            {
                fixture.EnterMeleeReactionMove();

                var refreshed = fixture.PreviewController.RefreshPreview();

                Assert.That(refreshed, Is.True, fixture.PreviewController.LastFeedback);
                Assert.That(fixture.PreviewController.HasActivePreview, Is.True);
                Assert.That(fixture.PreviewController.CurrentSafetyCells, Has.Count.EqualTo(3));
                Assert.That(
                    fixture.HighlightManager.GetHighlightedCells(GridHighlightCategory.ReactionThreatened).ToArray(),
                    Is.EquivalentTo(new[] { fixture.Reactor.CurrentGridPosition }));
                Assert.That(
                    fixture.HighlightManager.GetHighlightedCells(GridHighlightCategory.ReactionSafe).ToArray(),
                    Is.EquivalentTo(new[]
                    {
                        new GridPosition(2, 0, 0),
                        new GridPosition(3, 0, 0),
                    }));
                Assert.That(fixture.PreviewController.LastFeedback, Does.Contain("Melee Slash"));
                Assert.That(fixture.PreviewController.LastFeedback, Does.Contain("Safe cells: 2"));
                Assert.That(fixture.PreviewController.LastFeedback, Does.Contain("Still threatened cells: 1"));
            }
        }

        [Test]
        public void HoveredReactionDestinationReportsApCostAndSafetyReason()
        {
            using (var fixture = new Fixture())
            {
                fixture.EnterMeleeReactionMove();
                var safeDestination = new GridPosition(2, 0, 0);
                fixture.Selection.SetHoveredCell(safeDestination);

                var refreshed = fixture.PreviewController.RefreshPreview();

                Assert.That(refreshed, Is.True, fixture.PreviewController.LastFeedback);
                Assert.That(fixture.PreviewController.CurrentPreview.HasSelectedPath, Is.True);
                Assert.That(fixture.PreviewController.CurrentPreview.SelectedPath.TotalApCost, Is.EqualTo(1));
                Assert.That(fixture.PreviewController.TryGetSafetyCell(safeDestination, out var safetyCell), Is.True);
                Assert.That(safetyCell.Status, Is.EqualTo(ReactionSafetyStatus.Safe));
                Assert.That(fixture.PreviewController.LastFeedback, Does.Contain("path costs 1/2 AP"));
                Assert.That(fixture.PreviewController.LastFeedback, Does.Contain("Reaction Safety: Safe"));
                Assert.That(fixture.PreviewController.LastFeedback, Does.Contain("outside melee range"));
            }
        }

        [Test]
        public void RefreshPreviewClearsSafetyHighlightsWhenReactionMoveIsCancelled()
        {
            using (var fixture = new Fixture())
            {
                fixture.EnterMeleeReactionMove();
                Assert.That(fixture.PreviewController.RefreshPreview(), Is.True);
                Assert.That(fixture.HighlightManager.GetHighlightedCells(GridHighlightCategory.ReactionSafe), Is.Not.Empty);
                Assert.That(fixture.HighlightManager.GetHighlightedCells(GridHighlightCategory.ReactionThreatened), Is.Not.Empty);

                fixture.Selection.ClearActionMode();
                var refreshed = fixture.PreviewController.RefreshPreview();

                Assert.That(refreshed, Is.False);
                Assert.That(fixture.PreviewController.HasActivePreview, Is.False);
                Assert.That(fixture.PreviewController.CurrentSafetyCells, Is.Empty);
                Assert.That(fixture.PreviewController.LastFeedback, Is.Empty);
                Assert.That(fixture.HighlightManager.GetHighlightedCells(GridHighlightCategory.ReactionSafe), Is.Empty);
                Assert.That(fixture.HighlightManager.GetHighlightedCells(GridHighlightCategory.ReactionThreatened), Is.Empty);
            }
        }

        private sealed class Fixture : IDisposable
        {
            private readonly List<GameObject> gameObjects = new List<GameObject>();
            private readonly List<UnityEngine.Object> assets = new List<UnityEngine.Object>();
            private readonly GridMapDefinition mapDefinition;
            private readonly UnitStatsDefinition actorStats;
            private readonly UnitStatsDefinition reactorStats;

            public Fixture()
            {
                mapDefinition = ScriptableObject.CreateInstance<GridMapDefinition>();
                assets.Add(mapDefinition);
                mapDefinition.Configure(
                    width: 4,
                    depth: 1,
                    defaultHeightY: 0,
                    overrides: Array.Empty<GridMapDefinition.CellOverride>());

                actorStats = CreateStats("Safety Actor", meleeRange: 1);
                reactorStats = CreateStats("Safety Reactor", meleeRange: 1);
                MeleeSlash = CreateAbility("melee_slash", "Melee Slash", AbilityShape.Melee);

                var registryObject = CreateObject("Reaction Safety Registry");
                var gridObject = CreateObject("Reaction Safety Grid");
                var inputObject = CreateObject("Reaction Safety Input");
                var managerObject = CreateObject("Reaction Safety Manager");
                var highlightObject = CreateObject("Reaction Safety Highlights");
                var previewObject = CreateObject("Reaction Safety Preview Controller");

                Registry = registryObject.AddComponent<UnitRegistry>();
                GridManager = gridObject.AddComponent<GridManager>();
                AssignMapDefinition(GridManager, mapDefinition);
                Assert.That(GridManager.RebuildMap(), Is.True);

                Selection = inputObject.AddComponent<SelectionController>();
                Router = inputObject.AddComponent<PlayerCommandRouter>();
                Router.SelectionController = Selection;
                EventBus = managerObject.AddComponent<CombatEventBus>();
                Manager = managerObject.AddComponent<CombatManager>();
                Manager.Configure(Registry, GridManager, Router, EventBus);
                Manager.StartCombatOnStart = false;
                Manager.LogCombatStart = false;
                Manager.LogActionFlow = false;

                HighlightManager = highlightObject.AddComponent<GridHighlightManager>();
                PreviewController = previewObject.AddComponent<ReactionMovementSafetyPreviewController>();
                PreviewController.Configure(Selection, Manager, GridManager, Registry, HighlightManager);
                PreviewController.Visible = false;

                Actor = CreateRegisteredUnit("Reaction Safety Actor", new UnitId(1), TeamId.Player, actorStats, GridPosition.Zero);
                Reactor = CreateRegisteredUnit("Reaction Safety Reactor", new UnitId(2), TeamId.Enemy, reactorStats, new GridPosition(1, 0, 0));
                Reactor.SetAPForTest(2);
            }

            public UnitRegistry Registry { get; }

            public GridManager GridManager { get; }

            public SelectionController Selection { get; }

            public PlayerCommandRouter Router { get; }

            public CombatEventBus EventBus { get; }

            public CombatManager Manager { get; }

            public GridHighlightManager HighlightManager { get; }

            public ReactionMovementSafetyPreviewController PreviewController { get; }

            public TacticalUnit Actor { get; }

            public TacticalUnit Reactor { get; }

            public AbilityDefinition MeleeSlash { get; }

            public void EnterMeleeReactionMove()
            {
                var intent = new ActionIntent(
                    Actor,
                    MeleeSlash,
                    Actor.CurrentGridPosition,
                    ActionTarget.ForUnit(Reactor),
                    new[] { Reactor.CurrentGridPosition },
                    declarationRound: 1,
                    declarationSequence: 1);

                Manager.CurrentState.SetState(1, CombatPhase.ReactionWindow, Actor, Reactor, intent);
                Assert.That(Selection.SelectUnit(Reactor).IsSuccess, Is.True);
                Assert.That(Selection.SetActionMode(SelectionActionMode.Move).IsSuccess, Is.True);
            }

            public void Dispose()
            {
                for (var i = gameObjects.Count - 1; i >= 0; i -= 1)
                {
                    UnityEngine.Object.DestroyImmediate(gameObjects[i]);
                }

                for (var i = assets.Count - 1; i >= 0; i -= 1)
                {
                    UnityEngine.Object.DestroyImmediate(assets[i]);
                }
            }

            private TacticalUnit CreateRegisteredUnit(
                string name,
                UnitId unitId,
                TeamId team,
                UnitStatsDefinition stats,
                GridPosition position)
            {
                var gameObject = CreateObject(name);
                gameObject.transform.SetParent(Registry.transform, worldPositionStays: false);
                var unit = gameObject.AddComponent<TacticalUnit>();
                unit.Initialize(unitId, team, stats, position);
                var registerResult = Registry.Register(unit);
                Assert.That(registerResult.IsSuccess, Is.True, registerResult.ErrorMessage);
                return unit;
            }

            private GameObject CreateObject(string name)
            {
                var gameObject = new GameObject(name);
                gameObjects.Add(gameObject);
                return gameObject;
            }

            private UnitStatsDefinition CreateStats(string displayName, int meleeRange)
            {
                var stats = ScriptableObject.CreateInstance<UnitStatsDefinition>();
                assets.Add(stats);
                stats.Configure(displayName, maxHP: 10, maxAP: 6, movementAnimationSpeed: 4f, meleeRange: meleeRange, teamColorHint: Color.white);
                return stats;
            }

            private AbilityDefinition CreateAbility(string key, string displayName, AbilityShape shape)
            {
                var ability = ScriptableObject.CreateInstance<AbilityDefinition>();
                assets.Add(ability);
                ability.Configure(
                    key,
                    displayName,
                    apCost: 3,
                    usage: AbilityUsage.Action,
                    timing: AbilityTiming.Telegraphed,
                    shape: shape,
                    range: 0,
                    radius: 0,
                    damage: 4,
                    triggersReactions: true);
                return ability;
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
