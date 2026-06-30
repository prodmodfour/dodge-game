using NUnit.Framework;
using ReactionTactics.Grid;
using ReactionTactics.Input;
using UnityEngine;

namespace ReactionTactics.Tests.EditMode.Input
{
    public sealed class GridPickerTests
    {
        [Test]
        public void TryPickScreenPositionReturnsTileGridPosition()
        {
            var fixture = CreateFixture(new GridPosition(2, 1, 3));

            try
            {
                var screenPosition = fixture.Camera.WorldToScreenPoint(fixture.Tile.transform.position);

                Assert.That(fixture.Picker.TryPickScreenPosition(screenPosition, out var result), Is.True);
                Assert.That(result.Position, Is.EqualTo(new GridPosition(2, 1, 3)));
                Assert.That(result.Tile, Is.SameAs(fixture.Tile));
                Assert.That(result.Collider, Is.SameAs(fixture.Tile.PickingCollider));
            }
            finally
            {
                fixture.Destroy();
            }
        }

        [Test]
        public void HoverStateUpdatesOncePerCellAndClearsOnEmptySpace()
        {
            var fixture = CreateFixture(new GridPosition(4, 0, 5));
            var changedCount = 0;
            var clearedCount = 0;
            GridPickResult lastHover = default;

            try
            {
                fixture.Picker.HoverCellChanged += result =>
                {
                    changedCount++;
                    lastHover = result;
                };
                fixture.Picker.HoverCellCleared += () => clearedCount++;

                var tileScreenPosition = fixture.Camera.WorldToScreenPoint(fixture.Tile.transform.position);

                Assert.That(fixture.Picker.UpdateHoverAtScreenPosition(tileScreenPosition), Is.True);
                Assert.That(fixture.Picker.UpdateHoverAtScreenPosition(tileScreenPosition), Is.True);

                Assert.That(changedCount, Is.EqualTo(1));
                Assert.That(clearedCount, Is.EqualTo(0));
                Assert.That(fixture.Picker.HasCurrentHoverCell, Is.True);
                Assert.That(fixture.Picker.CurrentHoverCell, Is.EqualTo(new GridPosition(4, 0, 5)));
                Assert.That(fixture.Picker.CurrentHoverTile, Is.SameAs(fixture.Tile));
                Assert.That(lastHover.Position, Is.EqualTo(new GridPosition(4, 0, 5)));

                Assert.That(fixture.Picker.UpdateHoverAtScreenPosition(Vector2.zero), Is.False);

                Assert.That(clearedCount, Is.EqualTo(1));
                Assert.That(fixture.Picker.HasCurrentHoverCell, Is.False);
                Assert.That(fixture.Picker.CurrentHoverTile, Is.Null);
            }
            finally
            {
                fixture.Destroy();
            }
        }

        [Test]
        public void TryClickScreenPositionEmitsClickedCellEventOnlyWhenTileIsHit()
        {
            var fixture = CreateFixture(new GridPosition(1, 0, 2));
            var clickedCount = 0;
            GridPickResult clickedResult = default;

            try
            {
                fixture.Picker.LogClickedCells = false;
                fixture.Picker.CellClicked += result =>
                {
                    clickedCount++;
                    clickedResult = result;
                };

                Assert.That(fixture.Picker.TryClickScreenPosition(Vector2.zero, out _), Is.False);
                Assert.That(clickedCount, Is.EqualTo(0));

                var tileScreenPosition = fixture.Camera.WorldToScreenPoint(fixture.Tile.transform.position);

                Assert.That(fixture.Picker.TryClickScreenPosition(tileScreenPosition, out var result), Is.True);

                Assert.That(clickedCount, Is.EqualTo(1));
                Assert.That(result.Position, Is.EqualTo(new GridPosition(1, 0, 2)));
                Assert.That(clickedResult.Position, Is.EqualTo(new GridPosition(1, 0, 2)));
            }
            finally
            {
                fixture.Destroy();
            }
        }

        private static PickerFixture CreateFixture(GridPosition tilePosition)
        {
            var cameraObject = new GameObject("Grid Picker Test Camera");
            var camera = cameraObject.AddComponent<Camera>();
            camera.pixelRect = new Rect(0f, 0f, 640f, 480f);
            camera.nearClipPlane = 0.1f;
            camera.farClipPlane = 100f;
            camera.transform.position = new Vector3(0f, 0f, -5f);
            camera.transform.rotation = Quaternion.identity;

            var tileObject = GameObject.CreatePrimitive(PrimitiveType.Cube);
            tileObject.name = "Grid Picker Test Tile";
            tileObject.transform.position = Vector3.zero;
            var tile = tileObject.AddComponent<GridTileView>();
            tile.SetGridPosition(tilePosition);

            var pickerObject = new GameObject("Grid Picker Test Picker");
            var picker = pickerObject.AddComponent<GridPicker>();
            picker.SourceCamera = camera;
            picker.LogClickedCells = false;

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
                Object.DestroyImmediate(PickerObject);
                Object.DestroyImmediate(TileObject);
                Object.DestroyImmediate(CameraObject);
            }
        }
    }
}
