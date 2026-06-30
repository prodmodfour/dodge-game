using System.Collections.Generic;
using NUnit.Framework;
using ReactionTactics.Grid;
using ReactionTactics.Input;
using ReactionTactics.Turns;
using ReactionTactics.Units;
using UnityEngine;

namespace ReactionTactics.Tests.EditMode.Input
{
    public sealed class PlayerCommandRouterTests
    {
        [Test]
        public void SelectUnitAndMoveRouteRequestsThroughSelectionState()
        {
            var selectionObject = new GameObject("Selection Controller");
            var routerObject = new GameObject("Player Command Router");
            var actorObject = new GameObject("Actor");
            var stats = CreateStats("Knight");
            var requests = new List<PlayerCommandRequest>();

            try
            {
                var selection = selectionObject.AddComponent<SelectionController>();
                var router = routerObject.AddComponent<PlayerCommandRouter>();
                var actor = CreateUnit(actorObject, new UnitId(1), TeamId.Player, stats, GridPosition.Zero);
                router.SelectionController = selection;
                router.CommandRequested += requests.Add;

                var selectResult = router.SelectUnit(actor);
                var moveResult = router.SelectMove();

                Assert.That(selectResult.IsSuccess, Is.True);
                Assert.That(moveResult.IsSuccess, Is.True);
                Assert.That(requests.Count, Is.EqualTo(2));
                Assert.That(requests[0].CommandType, Is.EqualTo(PlayerCommandType.SelectUnit));
                Assert.That(requests[0].Unit, Is.SameAs(actor));
                Assert.That(requests[1].CommandType, Is.EqualTo(PlayerCommandType.SelectMove));
                Assert.That(requests[1].ActionMode, Is.EqualTo(SelectionActionMode.Move));
                Assert.That(selection.SelectedUnit, Is.SameAs(actor));
                Assert.That(selection.SelectedActionMode, Is.EqualTo(SelectionActionMode.Move));
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(actorObject);
                UnityEngine.Object.DestroyImmediate(routerObject);
                UnityEngine.Object.DestroyImmediate(selectionObject);
                UnityEngine.Object.DestroyImmediate(stats);
            }
        }

        [Test]
        public void SelectAttackAndConfirmTargetRouteActionRequestWithoutExecutingCombat()
        {
            var selectionObject = new GameObject("Selection Controller");
            var routerObject = new GameObject("Player Command Router");
            var actorObject = new GameObject("Actor");
            var stats = CreateStats("Archer");
            var requests = new List<PlayerCommandRequest>();

            try
            {
                var selection = selectionObject.AddComponent<SelectionController>();
                var router = routerObject.AddComponent<PlayerCommandRouter>();
                var actor = CreateUnit(actorObject, new UnitId(2), TeamId.Player, stats, new GridPosition(1, 0, 1));
                var targetCell = new GridPosition(3, 0, 1);
                router.SelectionController = selection;
                router.CommandRequested += requests.Add;

                Assert.That(router.SelectUnit(actor).IsSuccess, Is.True);
                Assert.That(router.SelectConeAttack().IsSuccess, Is.True);
                var targetResult = router.ConfirmTargetCell(targetCell);

                Assert.That(targetResult.IsSuccess, Is.True);
                Assert.That(requests.Count, Is.EqualTo(3));
                Assert.That(requests[1].CommandType, Is.EqualTo(PlayerCommandType.SelectAttack));
                Assert.That(requests[1].ActionMode, Is.EqualTo(SelectionActionMode.Cone));
                Assert.That(requests[2].CommandType, Is.EqualTo(PlayerCommandType.ConfirmTarget));
                Assert.That(requests[2].Target.Kind, Is.EqualTo(SelectionTargetKind.Cell));
                Assert.That(requests[2].Target.Cell, Is.EqualTo(targetCell));
                Assert.That(selection.SelectedTarget.Cell, Is.EqualTo(targetCell));
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(actorObject);
                UnityEngine.Object.DestroyImmediate(routerObject);
                UnityEngine.Object.DestroyImmediate(selectionObject);
                UnityEngine.Object.DestroyImmediate(stats);
            }
        }

        [Test]
        public void InvalidAttackModeIsRejectedWithoutChangingSelection()
        {
            var selectionObject = new GameObject("Selection Controller");
            var routerObject = new GameObject("Player Command Router");
            var actorObject = new GameObject("Actor");
            var stats = CreateStats("Knight");
            var rejectedResults = new List<string>();
            var requestCount = 0;

            try
            {
                var selection = selectionObject.AddComponent<SelectionController>();
                var router = routerObject.AddComponent<PlayerCommandRouter>();
                var actor = CreateUnit(actorObject, new UnitId(3), TeamId.Player, stats, GridPosition.Zero);
                router.SelectionController = selection;
                router.CommandRequested += _ => requestCount++;
                router.CommandRejected += result => rejectedResults.Add(result.ErrorMessage);

                Assert.That(router.SelectUnit(actor).IsSuccess, Is.True);
                var result = router.SelectAttack(SelectionActionMode.Move);

                Assert.That(result.IsFailure, Is.True);
                Assert.That(rejectedResults.Count, Is.EqualTo(1));
                Assert.That(rejectedResults[0], Does.Contain("not a selectable attack mode"));
                Assert.That(requestCount, Is.EqualTo(1));
                Assert.That(selection.SelectedActionMode, Is.EqualTo(SelectionActionMode.None));
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(actorObject);
                UnityEngine.Object.DestroyImmediate(routerObject);
                UnityEngine.Object.DestroyImmediate(selectionObject);
                UnityEngine.Object.DestroyImmediate(stats);
            }
        }

        [Test]
        public void CancelClearsActionModeBeforeSelectedUnitAndEndTurnRoutesStubRequest()
        {
            var selectionObject = new GameObject("Selection Controller");
            var routerObject = new GameObject("Player Command Router");
            var actorObject = new GameObject("Actor");
            var stats = CreateStats("Mage");
            var requests = new List<PlayerCommandRequest>();

            try
            {
                var selection = selectionObject.AddComponent<SelectionController>();
                var router = routerObject.AddComponent<PlayerCommandRouter>();
                var actor = CreateUnit(actorObject, new UnitId(4), TeamId.Player, stats, GridPosition.Zero);
                router.SelectionController = selection;
                router.CommandRequested += requests.Add;

                Assert.That(router.SelectUnit(actor).IsSuccess, Is.True);
                Assert.That(router.SelectAreaOfEffectAttack().IsSuccess, Is.True);
                Assert.That(router.ConfirmTargetCell(new GridPosition(2, 0, 2)).IsSuccess, Is.True);

                var cancelResult = router.Cancel();
                var endTurnResult = router.RequestEndTurn();
                var passReactionResult = router.RequestPassReaction();

                Assert.That(cancelResult.IsSuccess, Is.True);
                Assert.That(endTurnResult.IsSuccess, Is.True);
                Assert.That(passReactionResult.IsSuccess, Is.True);
                Assert.That(selection.SelectedUnit, Is.SameAs(actor));
                Assert.That(selection.SelectedActionMode, Is.EqualTo(SelectionActionMode.None));
                Assert.That(selection.SelectedTarget.HasTarget, Is.False);
                Assert.That(requests[requests.Count - 3].CommandType, Is.EqualTo(PlayerCommandType.Cancel));
                Assert.That(requests[requests.Count - 2].CommandType, Is.EqualTo(PlayerCommandType.EndTurn));
                Assert.That(requests[requests.Count - 2].Unit, Is.SameAs(actor));
                Assert.That(requests[requests.Count - 1].CommandType, Is.EqualTo(PlayerCommandType.PassReaction));
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(actorObject);
                UnityEngine.Object.DestroyImmediate(routerObject);
                UnityEngine.Object.DestroyImmediate(selectionObject);
                UnityEngine.Object.DestroyImmediate(stats);
            }
        }

        [Test]
        public void KeyboardShortcutsRouteThroughPublicCommandMethods()
        {
            var selectionObject = new GameObject("Selection Controller");
            var routerObject = new GameObject("Player Command Router");
            var actorObject = new GameObject("Actor");
            var stats = CreateStats("Knight");
            var requests = new List<PlayerCommandRequest>();

            try
            {
                var selection = selectionObject.AddComponent<SelectionController>();
                var router = routerObject.AddComponent<PlayerCommandRouter>();
                var actor = CreateUnit(actorObject, new UnitId(5), TeamId.Player, stats, GridPosition.Zero);
                router.SelectionController = selection;
                router.CommandRequested += requests.Add;

                Assert.That(router.SelectUnit(actor).IsSuccess, Is.True);

                Assert.That(router.RouteKeyboardShortcut(KeyCode.M).IsSuccess, Is.True);
                Assert.That(requests[requests.Count - 1].CommandType, Is.EqualTo(PlayerCommandType.SelectMove));
                Assert.That(requests[requests.Count - 1].ActionMode, Is.EqualTo(SelectionActionMode.Move));

                Assert.That(router.RouteKeyboardShortcut(KeyCode.Alpha1).IsSuccess, Is.True);
                Assert.That(requests[requests.Count - 1].CommandType, Is.EqualTo(PlayerCommandType.SelectAttack));
                Assert.That(requests[requests.Count - 1].ActionMode, Is.EqualTo(SelectionActionMode.Melee));

                Assert.That(router.RouteKeyboardShortcut(KeyCode.Alpha2).IsSuccess, Is.True);
                Assert.That(requests[requests.Count - 1].CommandType, Is.EqualTo(PlayerCommandType.SelectAttack));
                Assert.That(requests[requests.Count - 1].ActionMode, Is.EqualTo(SelectionActionMode.Cone));

                Assert.That(router.RouteKeyboardShortcut(KeyCode.Alpha3).IsSuccess, Is.True);
                Assert.That(requests[requests.Count - 1].CommandType, Is.EqualTo(PlayerCommandType.SelectAttack));
                Assert.That(requests[requests.Count - 1].ActionMode, Is.EqualTo(SelectionActionMode.AreaOfEffect));

                Assert.That(router.RouteKeyboardShortcut(KeyCode.B).IsSuccess, Is.True);
                Assert.That(requests[requests.Count - 1].CommandType, Is.EqualTo(PlayerCommandType.SelectReaction));
                Assert.That(requests[requests.Count - 1].ActionMode, Is.EqualTo(SelectionActionMode.Brace));

                Assert.That(router.RouteKeyboardShortcut(KeyCode.Space).IsSuccess, Is.True);
                Assert.That(requests[requests.Count - 1].CommandType, Is.EqualTo(PlayerCommandType.EndTurn));

                Assert.That(router.RouteKeyboardShortcut(KeyCode.Escape).IsSuccess, Is.True);
                Assert.That(requests[requests.Count - 1].CommandType, Is.EqualTo(PlayerCommandType.Cancel));
                Assert.That(selection.SelectedActionMode, Is.EqualTo(SelectionActionMode.None));
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(actorObject);
                UnityEngine.Object.DestroyImmediate(routerObject);
                UnityEngine.Object.DestroyImmediate(selectionObject);
                UnityEngine.Object.DestroyImmediate(stats);
            }
        }

        [Test]
        public void MouseClicksRouteThroughPickerIntoCommands()
        {
            var fixture = CreatePickerFixture(new GridPosition(4, 0, 5));
            var selectionObject = new GameObject("Selection Controller");
            var routerObject = new GameObject("Player Command Router");
            var stats = CreateStats("Rogue");
            var unitObject = GameObject.CreatePrimitive(PrimitiveType.Capsule);
            var requests = new List<PlayerCommandRequest>();

            try
            {
                var selection = selectionObject.AddComponent<SelectionController>();
                var router = routerObject.AddComponent<PlayerCommandRouter>();
                var unit = unitObject.AddComponent<TacticalUnit>();
                unitObject.name = "Rogue Unit";
                unitObject.transform.position = new Vector3(0f, 0f, -1.5f);
                unit.Initialize(new UnitId(5), TeamId.Player, stats, new GridPosition(4, 0, 5));
                router.SelectionController = selection;
                router.GridPicker = fixture.Picker;
                router.CommandRequested += requests.Add;
                Physics.SyncTransforms();

                var screenPosition = fixture.Camera.WorldToScreenPoint(fixture.Tile.transform.position);
                Assert.That(fixture.Picker.TryClickScreenPosition(screenPosition, out _, out _), Is.True);

                Assert.That(selection.SelectedUnit, Is.SameAs(unit));
                Assert.That(requests[0].CommandType, Is.EqualTo(PlayerCommandType.SelectUnit));

                unitObject.transform.position = new Vector3(3f, 0f, -1.5f);
                Physics.SyncTransforms();
                Assert.That(router.SelectMove().IsSuccess, Is.True);
                Assert.That(fixture.Picker.TryClickScreenPosition(screenPosition, out _, out _), Is.True);

                var lastRequest = requests[requests.Count - 1];
                Assert.That(lastRequest.CommandType, Is.EqualTo(PlayerCommandType.ConfirmTarget));
                Assert.That(lastRequest.Target.Kind, Is.EqualTo(SelectionTargetKind.Cell));
                Assert.That(lastRequest.Target.Cell, Is.EqualTo(new GridPosition(4, 0, 5)));
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(unitObject);
                UnityEngine.Object.DestroyImmediate(routerObject);
                UnityEngine.Object.DestroyImmediate(selectionObject);
                UnityEngine.Object.DestroyImmediate(stats);
                fixture.Destroy();
            }
        }

        [Test]
        public void ReactionPhaseCommandsUseCurrentReactorAndRejectOutOfOrderSelection()
        {
            var selectionObject = new GameObject("Reaction Selection Controller");
            var routerObject = new GameObject("Reaction Player Command Router");
            var managerObject = new GameObject("Reaction Combat Manager");
            var actorObject = new GameObject("Reaction Actor");
            var reactorObject = new GameObject("Current Reactor");
            var otherObject = new GameObject("Out Of Order Reactor");
            var stats = CreateStats("Reaction Unit");
            var requests = new List<PlayerCommandRequest>();
            var rejections = new List<string>();

            try
            {
                managerObject.SetActive(false);
                var selection = selectionObject.AddComponent<SelectionController>();
                var router = routerObject.AddComponent<PlayerCommandRouter>();
                var manager = managerObject.AddComponent<CombatManager>();
                var actor = CreateUnit(actorObject, new UnitId(10), TeamId.Player, stats, GridPosition.Zero);
                var reactor = CreateUnit(reactorObject, new UnitId(11), TeamId.Enemy, stats, new GridPosition(1, 0, 0));
                var other = CreateUnit(otherObject, new UnitId(12), TeamId.Enemy, stats, new GridPosition(2, 0, 0));
                router.SelectionController = selection;
                router.CombatManager = manager;
                router.CommandRequested += requests.Add;
                router.CommandRejected += result => rejections.Add(result.ErrorMessage);
                manager.CurrentState.SetState(1, CombatPhase.ReactionWindow, actor, reactor, new object());

                var rejectedSelection = router.SelectUnit(other);
                var moveResult = router.SelectMove();
                var braceResult = router.SelectBraceReaction();
                var passResult = router.RequestPassReaction();
                var passOrEndResult = router.RequestEndTurn();

                Assert.That(rejectedSelection.IsFailure, Is.True);
                Assert.That(rejections[0], Does.Contain("Only current reactor"));
                Assert.That(moveResult.IsSuccess, Is.True);
                Assert.That(braceResult.IsSuccess, Is.True);
                Assert.That(passResult.IsSuccess, Is.True);
                Assert.That(passOrEndResult.IsSuccess, Is.True);
                Assert.That(selection.SelectedUnit, Is.SameAs(reactor));
                Assert.That(requests[0].CommandType, Is.EqualTo(PlayerCommandType.SelectReaction));
                Assert.That(requests[0].Unit, Is.SameAs(reactor));
                Assert.That(requests[0].ActionMode, Is.EqualTo(SelectionActionMode.Move));
                Assert.That(requests[1].CommandType, Is.EqualTo(PlayerCommandType.SelectReaction));
                Assert.That(requests[1].Unit, Is.SameAs(reactor));
                Assert.That(requests[1].ActionMode, Is.EqualTo(SelectionActionMode.Brace));
                Assert.That(requests[2].CommandType, Is.EqualTo(PlayerCommandType.PassReaction));
                Assert.That(requests[2].Unit, Is.SameAs(reactor));
                Assert.That(requests[3].CommandType, Is.EqualTo(PlayerCommandType.EndTurn));
                Assert.That(requests[3].Unit, Is.SameAs(reactor));
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(otherObject);
                UnityEngine.Object.DestroyImmediate(reactorObject);
                UnityEngine.Object.DestroyImmediate(actorObject);
                UnityEngine.Object.DestroyImmediate(managerObject);
                UnityEngine.Object.DestroyImmediate(routerObject);
                UnityEngine.Object.DestroyImmediate(selectionObject);
                UnityEngine.Object.DestroyImmediate(stats);
            }
        }

        private static TacticalUnit CreateUnit(
            GameObject gameObject,
            UnitId unitId,
            TeamId team,
            UnitStatsDefinition stats,
            GridPosition position)
        {
            var unit = gameObject.AddComponent<TacticalUnit>();
            unit.Initialize(unitId, team, stats, position);
            return unit;
        }

        private static UnitStatsDefinition CreateStats(string displayName)
        {
            var stats = ScriptableObject.CreateInstance<UnitStatsDefinition>();
            stats.Configure(displayName, 10, 6, 4f, 1, Color.white);
            return stats;
        }

        private static PickerFixture CreatePickerFixture(GridPosition tilePosition)
        {
            var cameraObject = new GameObject("Player Command Router Test Camera");
            var camera = cameraObject.AddComponent<Camera>();
            camera.pixelRect = new Rect(0f, 0f, 640f, 480f);
            camera.nearClipPlane = 0.1f;
            camera.farClipPlane = 100f;
            camera.transform.position = new Vector3(0f, 0f, -5f);
            camera.transform.rotation = Quaternion.identity;

            var tileObject = GameObject.CreatePrimitive(PrimitiveType.Cube);
            tileObject.name = "Player Command Router Test Tile";
            tileObject.transform.position = Vector3.zero;
            var tile = tileObject.AddComponent<GridTileView>();
            tile.SetGridPosition(tilePosition);

            var pickerObject = new GameObject("Player Command Router Test Picker");
            var picker = pickerObject.AddComponent<GridPicker>();
            picker.SourceCamera = camera;
            picker.LogClickedCells = false;
            picker.LogClickedUnits = false;

            Physics.SyncTransforms();
            return new PickerFixture(cameraObject, tileObject, pickerObject, camera, tile, picker);
        }

        private readonly struct PickerFixture
        {
            public PickerFixture(
                GameObject cameraObject,
                GameObject tileObject,
                GameObject pickerObject,
                Camera camera,
                GridTileView tile,
                GridPicker picker)
            {
                CameraObject = cameraObject;
                TileObject = tileObject;
                PickerObject = pickerObject;
                Camera = camera;
                Tile = tile;
                Picker = picker;
            }

            public GameObject CameraObject { get; }

            public GameObject TileObject { get; }

            public GameObject PickerObject { get; }

            public Camera Camera { get; }

            public GridTileView Tile { get; }

            public GridPicker Picker { get; }

            public void Destroy()
            {
                UnityEngine.Object.DestroyImmediate(PickerObject);
                UnityEngine.Object.DestroyImmediate(TileObject);
                UnityEngine.Object.DestroyImmediate(CameraObject);
            }
        }
    }
}
