using NUnit.Framework;
using ReactionTactics.Grid;
using UnityEditor;
using UnityEngine;

namespace ReactionTactics.Tests.EditMode
{
    public sealed class GridTerrainViewTests
    {
        private const float Tolerance = 0.0001f;

        [Test]
        public void RegenerateCreatesPrimitiveTilesForEveryMapCell()
        {
            var definition = CreateDefinition();
            var managerObject = new GameObject("Grid Manager Test");
            var gridRoot = new GameObject("Grid Root Test");

            try
            {
                var manager = managerObject.AddComponent<GridManager>();
                AssignObject(manager, "mapDefinition", definition);
                Assert.That(manager.RebuildMap(), Is.True);

                var terrainView = gridRoot.AddComponent<GridTerrainView>();
                AssignObject(terrainView, "gridManager", manager);

                Assert.That(terrainView.Regenerate(), Is.True);

                Assert.That(terrainView.TileCount, Is.EqualTo(4));
                Assert.That(gridRoot.transform.childCount, Is.EqualTo(4));

                var raisedCell = manager.CurrentMap.GetCell(new GridPosition(1, 2, 0));
                Assert.That(terrainView.TryGetTile(raisedCell.Position, out var raisedTile), Is.True);
                Assert.That(raisedTile.GridPosition, Is.EqualTo(raisedCell.Position));
                Assert.That(raisedTile.PickingCollider, Is.Not.Null);
                Assert.That(raisedTile.TargetRenderer, Is.Not.Null);
                Assert.That(raisedTile.transform.parent, Is.SameAs(gridRoot.transform));
                Assert.That(raisedTile.transform.localScale.x, Is.EqualTo(manager.Metrics.CellSize).Within(Tolerance));
                Assert.That(raisedTile.transform.localScale.y, Is.EqualTo(GridTileView.CalculateVisualHeight(raisedCell, manager.Metrics)).Within(Tolerance));
                Assert.That(raisedTile.transform.localScale.z, Is.EqualTo(manager.Metrics.CellSize).Within(Tolerance));
                AssertVector3(raisedTile.transform.position, GridTerrainView.CalculateTileWorldPosition(raisedCell, manager.Metrics));
            }
            finally
            {
                Destroy(gridRoot);
                Destroy(managerObject);
                Destroy(definition);
            }
        }

        [Test]
        public void RegenerateUsesDistinctFallbackMaterialsForBlockedAndWalkableCells()
        {
            var definition = CreateDefinition();
            var managerObject = new GameObject("Grid Manager Material Test");
            var gridRoot = new GameObject("Grid Root Material Test");

            try
            {
                var manager = managerObject.AddComponent<GridManager>();
                AssignObject(manager, "mapDefinition", definition);
                Assert.That(manager.RebuildMap(), Is.True);

                var terrainView = gridRoot.AddComponent<GridTerrainView>();
                AssignObject(terrainView, "gridManager", manager);
                Assert.That(terrainView.Regenerate(), Is.True);

                Assert.That(terrainView.TryGetTile(new GridPosition(0, 0, 0), out var walkableTile), Is.True);
                Assert.That(terrainView.TryGetTile(new GridPosition(0, 1, 1), out var blockedTile), Is.True);

                var walkableMaterial = walkableTile.TargetRenderer.sharedMaterial;
                var blockedMaterial = blockedTile.TargetRenderer.sharedMaterial;

                Assert.That(walkableMaterial, Is.Not.Null);
                Assert.That(blockedMaterial, Is.Not.Null);
                Assert.That(blockedMaterial, Is.Not.SameAs(walkableMaterial));
            }
            finally
            {
                Destroy(gridRoot);
                Destroy(managerObject);
                Destroy(definition);
            }
        }

        private static GridMapDefinition CreateDefinition()
        {
            var definition = ScriptableObject.CreateInstance<GridMapDefinition>();
            definition.Configure(
                width: 2,
                depth: 2,
                defaultHeightY: 0,
                new[]
                {
                    new GridMapDefinition.CellOverride(
                        x: 1,
                        z: 0,
                        heightY: 2,
                        walkable: true,
                        blocksLineOfSight: false,
                        movementCost: 1),
                    new GridMapDefinition.CellOverride(
                        x: 0,
                        z: 1,
                        heightY: 1,
                        walkable: false,
                        blocksLineOfSight: false,
                        movementCost: 1),
                });
            return definition;
        }

        private static void AssignObject(Object target, string propertyName, Object value)
        {
            var serializedObject = new SerializedObject(target);
            serializedObject.FindProperty(propertyName).objectReferenceValue = value;
            serializedObject.ApplyModifiedPropertiesWithoutUndo();
        }

        private static void AssertVector3(Vector3 actual, Vector3 expected)
        {
            Assert.That(actual.x, Is.EqualTo(expected.x).Within(Tolerance));
            Assert.That(actual.y, Is.EqualTo(expected.y).Within(Tolerance));
            Assert.That(actual.z, Is.EqualTo(expected.z).Within(Tolerance));
        }

        private static void Destroy(Object value)
        {
            Object.DestroyImmediate(value);
        }
    }
}
