using System;
using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using ReactionTactics.Grid;
using ReactionTactics.Input;
using ReactionTactics.Turns;
using ReactionTactics.UI;
using ReactionTactics.Units;
using UnityEditor;
using UnityEngine;

namespace ReactionTactics.Tests.EditMode.UI
{
    public sealed class ActiveMovementPreviewControllerTests
    {
        [Test]
        public void RefreshPreviewHighlightsReachableCellsAndHoveredPathWithApCost()
        {
            using (var fixture = new Fixture())
            {
                var activeUnit = fixture.CreateUnit("Preview Active Unit", new UnitId(1), TeamId.Player, new GridPosition(0, 0, 0));
                Assert.That(fixture.Manager.StartCombat().IsSuccess, Is.True);
                activeUnit.SetAPForTest(2);

                Assert.That(fixture.Selection.SelectUnit(activeUnit).IsSuccess, Is.True);
                Assert.That(fixture.Selection.SetActionMode(SelectionActionMode.Move).IsSuccess, Is.True);
                fixture.Selection.SetHoveredCell(new GridPosition(2, 0, 0));

                var refreshed = fixture.PreviewController.RefreshPreview();

                Assert.That(refreshed, Is.True, fixture.PreviewController.LastFeedback);
                Assert.That(fixture.PreviewController.HasActivePreview, Is.True);
                Assert.That(fixture.PreviewController.CurrentPreview.HasSelectedPath, Is.True);
                Assert.That(
                    fixture.PreviewController.CurrentPreview.SelectedPath.Positions.ToArray(),
                    Is.EqualTo(new[]
                    {
                        new GridPosition(0, 0, 0),
                        new GridPosition(1, 0, 0),
                        new GridPosition(2, 0, 0),
                    }));
                Assert.That(fixture.PreviewController.CurrentPreview.SelectedPath.TotalApCost, Is.EqualTo(2));
                Assert.That(fixture.PreviewController.LastFeedback, Does.Contain("costs 2/2 AP"));

                Assert.That(
                    fixture.HighlightManager.GetHighlightedCells(GridHighlightCategory.MovementRange).ToArray(),
                    Is.EquivalentTo(new[]
                    {
                        new GridPosition(0, 0, 0),
                        new GridPosition(1, 0, 0),
                        new GridPosition(2, 0, 0),
                    }));
                Assert.That(
                    fixture.HighlightManager.HoverPath.ToArray(),
                    Is.EqualTo(new[]
                    {
                        new GridPosition(0, 0, 0),
                        new GridPosition(1, 0, 0),
                        new GridPosition(2, 0, 0),
                    }));
                Assert.That(
                    fixture.HighlightManager.IsHighlighted(new GridPosition(2, 0, 0), GridHighlightCategory.TargetCell),
                    Is.True);
            }
        }

        [Test]
        public void RefreshPreviewUsesSelectedTargetForConfirmationWhenNoCellIsHovered()
        {
            using (var fixture = new Fixture())
            {
                var activeUnit = fixture.CreateUnit("Preview Selected Target Unit", new UnitId(12), TeamId.Player, new GridPosition(0, 0, 0));
                Assert.That(fixture.Manager.StartCombat().IsSuccess, Is.True);
                activeUnit.SetAPForTest(2);

                Assert.That(fixture.Selection.SelectUnit(activeUnit).IsSuccess, Is.True);
                Assert.That(fixture.Selection.SetActionMode(SelectionActionMode.Move).IsSuccess, Is.True);
                fixture.Selection.SetTargetCell(new GridPosition(2, 0, 0));

                var refreshed = fixture.PreviewController.RefreshPreview();

                Assert.That(refreshed, Is.True, fixture.PreviewController.LastFeedback);
                Assert.That(fixture.PreviewController.CurrentPreview.HasSelectedPath, Is.True);
                Assert.That(fixture.PreviewController.CurrentPreview.SelectedPath.Destination, Is.EqualTo(new GridPosition(2, 0, 0)));
                Assert.That(fixture.HighlightManager.IsHighlighted(new GridPosition(2, 0, 0), GridHighlightCategory.TargetCell), Is.True);
            }
        }

        [Test]
        public void RefreshPreviewReportsUnreachableHoverAndKeepsOnlyReachableRangeHighlighted()
        {
            using (var fixture = new Fixture())
            {
                var activeUnit = fixture.CreateUnit("Preview Low AP Unit", new UnitId(2), TeamId.Player, new GridPosition(0, 0, 0));
                Assert.That(fixture.Manager.StartCombat().IsSuccess, Is.True);
                activeUnit.SetAPForTest(1);

                Assert.That(fixture.Selection.SelectUnit(activeUnit).IsSuccess, Is.True);
                Assert.That(fixture.Selection.SetActionMode(SelectionActionMode.Move).IsSuccess, Is.True);
                fixture.Selection.SetHoveredCell(new GridPosition(2, 0, 0));

                var refreshed = fixture.PreviewController.RefreshPreview();

                Assert.That(refreshed, Is.True, fixture.PreviewController.LastFeedback);
                Assert.That(fixture.PreviewController.HasActivePreview, Is.True);
                Assert.That(fixture.PreviewController.CurrentPreview.HasSelectedPath, Is.False);
                Assert.That(fixture.PreviewController.LastFeedback, Does.Contain("not reachable"));
                Assert.That(
                    fixture.HighlightManager.GetHighlightedCells(GridHighlightCategory.MovementRange).ToArray(),
                    Is.EquivalentTo(new[]
                    {
                        new GridPosition(0, 0, 0),
                        new GridPosition(1, 0, 0),
                    }));
                Assert.That(fixture.HighlightManager.GetHighlightedCells(GridHighlightCategory.SelectedPath), Is.Empty);
                Assert.That(fixture.HighlightManager.GetHighlightedCells(GridHighlightCategory.TargetCell), Is.Empty);
            }
        }

        [Test]
        public void RefreshPreviewClearsMovementHighlightsWhenMoveModeIsCancelled()
        {
            using (var fixture = new Fixture())
            {
                var activeUnit = fixture.CreateUnit("Preview Cancel Unit", new UnitId(3), TeamId.Player, new GridPosition(0, 0, 0));
                Assert.That(fixture.Manager.StartCombat().IsSuccess, Is.True);
                activeUnit.SetAPForTest(1);
                Assert.That(fixture.Selection.SelectUnit(activeUnit).IsSuccess, Is.True);
                Assert.That(fixture.Selection.SetActionMode(SelectionActionMode.Move).IsSuccess, Is.True);
                fixture.Selection.SetHoveredCell(new GridPosition(1, 0, 0));
                Assert.That(fixture.PreviewController.RefreshPreview(), Is.True);
                Assert.That(fixture.HighlightManager.GetHighlightedCells(GridHighlightCategory.MovementRange), Is.Not.Empty);

                fixture.Selection.ClearActionMode();
                var refreshed = fixture.PreviewController.RefreshPreview();

                Assert.That(refreshed, Is.False);
                Assert.That(fixture.PreviewController.HasActivePreview, Is.False);
                Assert.That(fixture.PreviewController.LastFeedback, Is.Empty);
                Assert.That(fixture.HighlightManager.GetHighlightedCells(GridHighlightCategory.MovementRange), Is.Empty);
                Assert.That(fixture.HighlightManager.GetHighlightedCells(GridHighlightCategory.SelectedPath), Is.Empty);
                Assert.That(fixture.HighlightManager.GetHighlightedCells(GridHighlightCategory.TargetCell), Is.Empty);
            }
        }

        private sealed class Fixture : IDisposable
        {
            private readonly List<GameObject> gameObjects = new List<GameObject>();
            private readonly UnitStatsDefinition stats;
            private readonly GridMapDefinition mapDefinition;

            public Fixture()
            {
                stats = ScriptableObject.CreateInstance<UnitStatsDefinition>();
                stats.Configure("Preview Test Unit", 10, 6, 4f, 1, Color.white);

                mapDefinition = ScriptableObject.CreateInstance<GridMapDefinition>();
                mapDefinition.Configure(
                    width: 3,
                    depth: 1,
                    defaultHeightY: 0,
                    overrides: Array.Empty<GridMapDefinition.CellOverride>());

                var registryObject = CreateObject("Active Movement Preview Registry");
                var gridObject = CreateObject("Active Movement Preview Grid");
                var inputObject = CreateObject("Active Movement Preview Input");
                var managerObject = CreateObject("Active Movement Preview Manager");
                var highlightObject = CreateObject("Active Movement Preview Highlights");
                var previewObject = CreateObject("Active Movement Preview Controller");

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
                RegisterTiles();

                PreviewController = previewObject.AddComponent<ActiveMovementPreviewController>();
                PreviewController.Configure(Selection, Manager, GridManager, Registry, HighlightManager);
                PreviewController.Visible = false;
            }

            public UnitRegistry Registry { get; }

            public GridManager GridManager { get; }

            public SelectionController Selection { get; }

            public PlayerCommandRouter Router { get; }

            public CombatEventBus EventBus { get; }

            public CombatManager Manager { get; }

            public GridHighlightManager HighlightManager { get; }

            public ActiveMovementPreviewController PreviewController { get; }

            public TacticalUnit CreateUnit(string name, UnitId unitId, TeamId team, GridPosition position)
            {
                var gameObject = CreateObject(name);
                gameObject.transform.SetParent(Registry.transform, worldPositionStays: false);
                var unit = gameObject.AddComponent<TacticalUnit>();
                unit.Initialize(unitId, team, stats, position);
                return unit;
            }

            public void Dispose()
            {
                for (var i = gameObjects.Count - 1; i >= 0; i -= 1)
                {
                    UnityEngine.Object.DestroyImmediate(gameObjects[i]);
                }

                UnityEngine.Object.DestroyImmediate(mapDefinition);
                UnityEngine.Object.DestroyImmediate(stats);
            }

            private GameObject CreateObject(string name)
            {
                var gameObject = new GameObject(name);
                gameObjects.Add(gameObject);
                return gameObject;
            }

            private void RegisterTiles()
            {
                for (var x = 0; x < 3; x += 1)
                {
                    var tileObject = GameObject.CreatePrimitive(PrimitiveType.Cube);
                    tileObject.name = $"Preview Tile {x}";
                    gameObjects.Add(tileObject);
                    var tile = tileObject.AddComponent<GridTileView>();
                    tile.SetGridPosition(new GridPosition(x, 0, 0));
                    Assert.That(HighlightManager.RegisterTile(tile), Is.True);
                }
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
