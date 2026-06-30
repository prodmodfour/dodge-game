using NUnit.Framework;
using ReactionTactics.Grid;
using ReactionTactics.Input;
using ReactionTactics.Units;
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
        public void TryPickUnitScreenPositionReturnsUnitIdentityAndTeam()
        {
            var fixture = CreateFixture(new GridPosition(2, 0, 3));
            var stats = CreateStats("Enemy Rogue");
            var unitObject = GameObject.CreatePrimitive(PrimitiveType.Capsule);
            TacticalUnit unit = null;

            try
            {
                unitObject.name = "Enemy Rogue Unit";
                unitObject.transform.position = new Vector3(0f, 0f, -1.5f);
                unit = unitObject.AddComponent<TacticalUnit>();
                unit.Initialize(new UnitId(42), TeamId.Enemy, stats, new GridPosition(2, 0, 3));
                Physics.SyncTransforms();

                var screenPosition = fixture.Camera.WorldToScreenPoint(fixture.Tile.transform.position);

                Assert.That(fixture.Picker.TryPickUnitScreenPosition(screenPosition, out var result), Is.True);
                Assert.That(result.Unit, Is.SameAs(unit));
                Assert.That(result.UnitId, Is.EqualTo(new UnitId(42)));
                Assert.That(result.Team, Is.EqualTo(TeamId.Enemy));
                Assert.That(result.Position, Is.EqualTo(new GridPosition(2, 0, 3)));
                Assert.That(result.DisplayName, Is.EqualTo("Enemy Rogue"));
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(unitObject);
                UnityEngine.Object.DestroyImmediate(stats);
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
        public void UnitHoverStateUpdatesOncePerUnitAndClearsOnEmptySpace()
        {
            var fixture = CreateFixture(new GridPosition(5, 0, 1));
            var stats = CreateStats("Player Knight");
            var unitObject = GameObject.CreatePrimitive(PrimitiveType.Capsule);
            var changedCount = 0;
            var clearedCount = 0;
            UnitPickResult lastHover = default;

            try
            {
                unitObject.name = "Player Knight Unit";
                unitObject.transform.position = new Vector3(0f, 0f, -1.5f);
                var unit = unitObject.AddComponent<TacticalUnit>();
                unit.Initialize(new UnitId(7), TeamId.Player, stats, new GridPosition(5, 0, 1));
                Physics.SyncTransforms();

                fixture.Picker.HoverUnitChanged += result =>
                {
                    changedCount++;
                    lastHover = result;
                };
                fixture.Picker.HoverUnitCleared += () => clearedCount++;

                var screenPosition = fixture.Camera.WorldToScreenPoint(fixture.Tile.transform.position);

                Assert.That(fixture.Picker.UpdateHoverAtScreenPosition(screenPosition), Is.True);
                Assert.That(fixture.Picker.UpdateHoverAtScreenPosition(screenPosition), Is.True);

                Assert.That(changedCount, Is.EqualTo(1));
                Assert.That(clearedCount, Is.EqualTo(0));
                Assert.That(fixture.Picker.HasCurrentHoverUnit, Is.True);
                Assert.That(fixture.Picker.CurrentHoverUnit, Is.SameAs(unit));
                Assert.That(fixture.Picker.HasCurrentHoverCell, Is.False);
                Assert.That(lastHover.Unit, Is.SameAs(unit));
                Assert.That(lastHover.Team, Is.EqualTo(TeamId.Player));

                Assert.That(fixture.Picker.UpdateHoverAtScreenPosition(Vector2.zero), Is.False);

                Assert.That(clearedCount, Is.EqualTo(1));
                Assert.That(fixture.Picker.HasCurrentHoverUnit, Is.False);
                Assert.That(fixture.Picker.CurrentHoverUnit, Is.Null);
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(unitObject);
                UnityEngine.Object.DestroyImmediate(stats);
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

        [Test]
        public void TryClickScreenPositionPrioritizesUnitOverTileWhenBothAreHit()
        {
            var fixture = CreateFixture(new GridPosition(3, 0, 4));
            var stats = CreateStats("Enemy Goblin");
            var unitObject = GameObject.CreatePrimitive(PrimitiveType.Capsule);
            var cellClickedCount = 0;
            var unitClickedCount = 0;
            UnitPickResult clickedUnit = default;

            try
            {
                unitObject.name = "Enemy Goblin Unit";
                unitObject.transform.position = new Vector3(0f, 0f, -1.5f);
                var unit = unitObject.AddComponent<TacticalUnit>();
                unit.Initialize(new UnitId(99), TeamId.Enemy, stats, new GridPosition(3, 0, 4));
                Physics.SyncTransforms();

                fixture.Picker.LogClickedCells = false;
                fixture.Picker.LogClickedUnits = false;
                fixture.Picker.CellClicked += _ => cellClickedCount++;
                fixture.Picker.UnitClicked += result =>
                {
                    unitClickedCount++;
                    clickedUnit = result;
                };

                var screenPosition = fixture.Camera.WorldToScreenPoint(fixture.Tile.transform.position);

                Assert.That(
                    fixture.Picker.TryClickScreenPosition(screenPosition, out var cellResult, out var unitResult),
                    Is.True);

                Assert.That(cellClickedCount, Is.EqualTo(0));
                Assert.That(unitClickedCount, Is.EqualTo(1));
                Assert.That(cellResult.Tile, Is.Null);
                Assert.That(unitResult.Unit, Is.SameAs(unit));
                Assert.That(clickedUnit.Unit, Is.SameAs(unit));
                Assert.That(clickedUnit.Team, Is.EqualTo(TeamId.Enemy));
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(unitObject);
                UnityEngine.Object.DestroyImmediate(stats);
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
            picker.LogClickedUnits = false;

            Physics.SyncTransforms();
            return new PickerFixture(cameraObject, tileObject, pickerObject, camera, tile, picker);
        }

        private static UnitStatsDefinition CreateStats(string displayName)
        {
            var stats = ScriptableObject.CreateInstance<UnitStatsDefinition>();
            stats.Configure(displayName, 10, 6, 4f, 1, Color.white);
            return stats;
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
