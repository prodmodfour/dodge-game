using System;
using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using ReactionTactics.Grid;
using ReactionTactics.Input;
using ReactionTactics.Turns;
using ReactionTactics.UI;
using ReactionTactics.Units;
using UnityEngine;

public sealed class ActiveMovementIntegrationTests
{
    [Test]
    public void PlayerCanSelectMoveClickReachableCellSpendApClearHighlightsAndKeepActing()
    {
        using (var fixture = new Fixture())
        {
            var activeUnit = fixture.CreateUnit(
                "Integrated Active Mover",
                new UnitId(1),
                TeamId.Player,
                new GridPosition(0, 0, 0));
            fixture.CreateUnit(
                "Integrated Enemy Anchor",
                new UnitId(2),
                TeamId.Enemy,
                new GridPosition(2, 0, 0));
            var statusView = fixture.AttachStatusView(activeUnit);

            Assert.That(fixture.Manager.StartCombat().IsSuccess, Is.True);
            statusView.RefreshStatus();
            Assert.That(statusView.StatusText, Does.Contain($"AP {activeUnit.MaxAP}/{activeUnit.MaxAP}"));

            Assert.That(fixture.Router.SelectUnit(activeUnit).IsSuccess, Is.True);
            Assert.That(fixture.Router.SelectMove().IsSuccess, Is.True);

            var destination = new GridPosition(1, 0, 0);
            fixture.Selection.SetHoveredCell(destination);
            Assert.That(fixture.PreviewController.RefreshPreview(), Is.True, fixture.PreviewController.LastFeedback);
            Assert.That(fixture.PreviewController.CurrentPreview.IsReachable(destination), Is.True);
            Assert.That(
                fixture.HighlightManager.GetHighlightedCells(GridHighlightCategory.MovementRange),
                Does.Contain(destination));
            Assert.That(
                fixture.HighlightManager.HoverPath.ToArray(),
                Is.EqualTo(new[] { new GridPosition(0, 0, 0), destination }));

            var actionPointEvents = new List<ActionPointsChangedEvent>();
            fixture.EventBus.ActionPointsChanged += actionPointEvents.Add;
            var apBeforeMove = activeUnit.CurrentAP;

            Assert.That(
                fixture.Picker.TryClickScreenPosition(fixture.GetScreenPoint(destination), out _, out _),
                Is.True);

            Assert.That(activeUnit.CurrentGridPosition, Is.EqualTo(destination));
            Assert.That(activeUnit.CurrentAP, Is.EqualTo(apBeforeMove - 1));
            Assert.That(actionPointEvents.Count, Is.EqualTo(1));
            Assert.That(actionPointEvents[0].Unit, Is.SameAs(activeUnit));
            Assert.That(actionPointEvents[0].PreviousAP, Is.EqualTo(apBeforeMove));
            Assert.That(actionPointEvents[0].CurrentAP, Is.EqualTo(apBeforeMove - 1));
            Assert.That(statusView.StatusText, Does.Contain($"AP {activeUnit.CurrentAP}/{activeUnit.MaxAP}"));

            Assert.That(fixture.Selection.SelectedUnit, Is.SameAs(activeUnit));
            Assert.That(fixture.Selection.SelectedActionMode, Is.EqualTo(SelectionActionMode.None));
            Assert.That(fixture.Selection.SelectedTarget.HasTarget, Is.False);
            Assert.That(fixture.Manager.CurrentState.Phase, Is.EqualTo(CombatPhase.ActiveTurn));
            Assert.That(fixture.Manager.CurrentState.ActiveUnit, Is.SameAs(activeUnit));
            Assert.That(fixture.Manager.CurrentState.ReactingUnit, Is.Null);
            Assert.That(fixture.Manager.CurrentState.PendingActionIntent, Is.Null);
            Assert.That(fixture.Manager.CurrentReactionWindow, Is.Null);
            Assert.That(fixture.Manager.ValidateUnitCanTakeAction(activeUnit).IsSuccess, Is.True);

            Assert.That(fixture.HighlightManager.GetHighlightedCells(GridHighlightCategory.MovementRange), Is.Empty);
            Assert.That(fixture.HighlightManager.GetHighlightedCells(GridHighlightCategory.SelectedPath), Is.Empty);
            Assert.That(fixture.HighlightManager.GetHighlightedCells(GridHighlightCategory.TargetCell), Is.Empty);

            Assert.That(fixture.Router.SelectMove().IsSuccess, Is.True);
            Assert.That(fixture.Selection.SelectedActionMode, Is.EqualTo(SelectionActionMode.Move));
            Assert.That(fixture.PreviewController.RefreshPreview(), Is.True, fixture.PreviewController.LastFeedback);
            Assert.That(fixture.PreviewController.CurrentPreview.StartPosition, Is.EqualTo(destination));
        }
    }

    private sealed class Fixture : IDisposable
    {
        private readonly List<GameObject> rootObjects = new List<GameObject>();
        private readonly List<ScriptableObject> assets = new List<ScriptableObject>();
        private readonly Dictionary<GridPosition, GridTileView> tilesByPosition = new Dictionary<GridPosition, GridTileView>();
        private readonly UnitStatsDefinition stats;

        public Fixture()
        {
            stats = ScriptableObject.CreateInstance<UnitStatsDefinition>();
            assets.Add(stats);
            stats.Configure(
                displayName: "Active Movement Integration Unit",
                maxHP: 10,
                maxAP: 6,
                movementAnimationSpeed: 4f,
                meleeRange: 1,
                teamColorHint: Color.white);

            var mapDefinition = ScriptableObject.CreateInstance<GridMapDefinition>();
            assets.Add(mapDefinition);
            mapDefinition.Configure(
                width: 3,
                depth: 1,
                defaultHeightY: 0,
                overrides: Array.Empty<GridMapDefinition.CellOverride>());

            var registryObject = CreateRoot("Active Movement Integration Registry");
            var gridObject = CreateRoot("Active Movement Integration Grid");
            var inputObject = CreateRoot("Active Movement Integration Input");
            var managerObject = CreateRoot("Active Movement Integration Manager");
            var highlightObject = CreateRoot("Active Movement Integration Highlights");
            var previewObject = CreateRoot("Active Movement Integration Preview");
            var cameraObject = CreateRoot("Active Movement Integration Camera");

            Camera = cameraObject.AddComponent<Camera>();
            Camera.pixelRect = new Rect(0f, 0f, 800f, 600f);
            Camera.nearClipPlane = 0.1f;
            Camera.farClipPlane = 100f;
            Camera.transform.position = new Vector3(1.5f, 0f, -5f);
            Camera.transform.rotation = Quaternion.identity;

            Registry = registryObject.AddComponent<UnitRegistry>();
            GridManager = gridObject.AddComponent<GridManager>();
            Assert.That(GridManager.SetMapDefinition(mapDefinition), Is.True);

            Selection = inputObject.AddComponent<SelectionController>();
            Picker = inputObject.AddComponent<GridPicker>();
            Picker.SourceCamera = Camera;
            Picker.LogClickedCells = false;
            Picker.LogClickedUnits = false;
            Selection.GridPicker = Picker;

            Router = inputObject.AddComponent<PlayerCommandRouter>();
            Router.SelectionController = Selection;
            Router.GridPicker = Picker;
            Router.KeyboardShortcutsEnabled = false;

            EventBus = managerObject.AddComponent<CombatEventBus>();
            Manager = managerObject.AddComponent<CombatManager>();
            Manager.Configure(Registry, GridManager, Router, EventBus);
            Manager.StartCombatOnStart = false;
            Manager.LogCombatStart = false;
            Manager.LogActionFlow = false;

            HighlightManager = highlightObject.AddComponent<GridHighlightManager>();
            HighlightManager.RebuildIndexFromSceneWhenMissing = false;
            RegisterTiles();

            PreviewController = previewObject.AddComponent<ActiveMovementPreviewController>();
            PreviewController.Configure(Selection, Manager, GridManager, Registry, HighlightManager);
            PreviewController.Visible = false;
        }

        public UnitRegistry Registry { get; }

        public GridManager GridManager { get; }

        public SelectionController Selection { get; }

        public GridPicker Picker { get; }

        public PlayerCommandRouter Router { get; }

        public CombatEventBus EventBus { get; }

        public CombatManager Manager { get; }

        public GridHighlightManager HighlightManager { get; }

        public ActiveMovementPreviewController PreviewController { get; }

        public Camera Camera { get; }

        public TacticalUnit CreateUnit(string name, UnitId unitId, TeamId team, GridPosition position)
        {
            var gameObject = new GameObject(name);
            gameObject.transform.SetParent(Registry.transform, worldPositionStays: false);
            gameObject.transform.position = GridManager.Metrics.GridToWorldCenter(position);
            var unit = gameObject.AddComponent<TacticalUnit>();
            unit.Initialize(unitId, team, stats, position);
            return unit;
        }

        public UnitStatusView AttachStatusView(TacticalUnit unit)
        {
            var statusView = unit.gameObject.AddComponent<UnitStatusView>();
            statusView.Configure(unit, EventBus, Camera);
            return statusView;
        }

        public Vector2 GetScreenPoint(GridPosition position)
        {
            return Camera.WorldToScreenPoint(tilesByPosition[position].transform.position);
        }

        public void Dispose()
        {
            for (var i = rootObjects.Count - 1; i >= 0; i -= 1)
            {
                UnityEngine.Object.DestroyImmediate(rootObjects[i]);
            }

            for (var i = assets.Count - 1; i >= 0; i -= 1)
            {
                UnityEngine.Object.DestroyImmediate(assets[i]);
            }
        }

        private GameObject CreateRoot(string name)
        {
            var gameObject = new GameObject(name);
            rootObjects.Add(gameObject);
            return gameObject;
        }

        private void RegisterTiles()
        {
            for (var x = 0; x < 3; x += 1)
            {
                var position = new GridPosition(x, 0, 0);
                var tileObject = GameObject.CreatePrimitive(PrimitiveType.Cube);
                tileObject.name = $"Active Movement Integration Tile {x}";
                tileObject.transform.position = GridManager.Metrics.GridToWorldCenter(position);
                rootObjects.Add(tileObject);
                var tile = tileObject.AddComponent<GridTileView>();
                tile.SetGridPosition(position);
                tilesByPosition.Add(position, tile);
                Assert.That(HighlightManager.RegisterTile(tile), Is.True);
            }

            Physics.SyncTransforms();
        }
    }
}
