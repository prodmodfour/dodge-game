using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using ReactionTactics.Grid;
using UnityEditor;
using UnityEngine;

namespace ReactionTactics.Tests.EditMode
{
    public sealed class GridHighlightManagerTests
    {
        [Test]
        public void OverlappingCategoriesResolveByPriorityWithoutClearingLowerLayers()
        {
            var managerObject = new GameObject("Grid Highlight Manager Test");
            var tileObject = GameObject.CreatePrimitive(PrimitiveType.Cube);
            var baseMaterial = CreateMaterial("Base Tile Material");
            var movementMaterial = CreateMaterial("Movement Highlight Material");
            var dangerMaterial = CreateMaterial("Danger Highlight Material");

            try
            {
                var manager = managerObject.AddComponent<GridHighlightManager>();
                AssignObject(manager, "movementRangeMaterial", movementMaterial);
                AssignObject(manager, "actionDangerMaterial", dangerMaterial);

                var position = new GridPosition(1, 0, 2);
                var tile = ConfigureTile(tileObject, position, baseMaterial);
                Assert.That(manager.RegisterTile(tile), Is.True);

                manager.HighlightCells(GridHighlightCategory.MovementRange, new[] { position });

                Assert.That(tile.IsHighlighted, Is.True);
                Assert.That(tile.TargetRenderer.sharedMaterial, Is.SameAs(movementMaterial));
                Assert.That(manager.TryGetTopCategory(position, out var topCategory), Is.True);
                Assert.That(topCategory, Is.EqualTo(GridHighlightCategory.MovementRange));

                manager.HighlightCells(GridHighlightCategory.ActionDanger, new[] { position });

                Assert.That(tile.TargetRenderer.sharedMaterial, Is.SameAs(dangerMaterial));
                Assert.That(manager.TryGetTopCategory(position, out topCategory), Is.True);
                Assert.That(topCategory, Is.EqualTo(GridHighlightCategory.ActionDanger));

                manager.Clear(GridHighlightCategory.ActionDanger);

                Assert.That(manager.IsHighlighted(position, GridHighlightCategory.MovementRange), Is.True);
                Assert.That(tile.TargetRenderer.sharedMaterial, Is.SameAs(movementMaterial));
            }
            finally
            {
                Destroy(managerObject);
                Destroy(tileObject);
                Destroy(baseMaterial);
                Destroy(movementMaterial);
                Destroy(dangerMaterial);
            }
        }

        [Test]
        public void SetHoverPathUsesSelectedPathLayerAndTargetCellCanOverrideIt()
        {
            var managerObject = new GameObject("Grid Hover Path Manager Test");
            var baseMaterial = CreateMaterial("Hover Base Tile Material");
            var pathMaterial = CreateMaterial("Selected Path Material");
            var targetMaterial = CreateMaterial("Target Cell Material");
            var tileObjects = new List<GameObject>();

            try
            {
                var manager = managerObject.AddComponent<GridHighlightManager>();
                AssignObject(manager, "selectedPathMaterial", pathMaterial);
                AssignObject(manager, "targetCellMaterial", targetMaterial);

                var start = new GridPosition(0, 0, 0);
                var middle = new GridPosition(1, 0, 0);
                var end = new GridPosition(2, 0, 0);
                var startTile = CreateTile(tileObjects, start, baseMaterial);
                var middleTile = CreateTile(tileObjects, middle, baseMaterial);
                var endTile = CreateTile(tileObjects, end, baseMaterial);
                manager.RegisterTile(startTile);
                manager.RegisterTile(middleTile);
                manager.RegisterTile(endTile);

                manager.SetHoverPath(new[] { start, middle, end });

                Assert.That(manager.HoverPath.ToArray(), Is.EqualTo(new[] { start, middle, end }));
                Assert.That(manager.IsHighlighted(middle, GridHighlightCategory.SelectedPath), Is.True);
                Assert.That(startTile.TargetRenderer.sharedMaterial, Is.SameAs(pathMaterial));
                Assert.That(middleTile.TargetRenderer.sharedMaterial, Is.SameAs(pathMaterial));
                Assert.That(endTile.TargetRenderer.sharedMaterial, Is.SameAs(pathMaterial));

                manager.HighlightCell(GridHighlightCategory.TargetCell, middle);

                Assert.That(middleTile.TargetRenderer.sharedMaterial, Is.SameAs(targetMaterial));
                Assert.That(manager.TryGetTopCategory(middle, out var topCategory), Is.True);
                Assert.That(topCategory, Is.EqualTo(GridHighlightCategory.TargetCell));

                manager.ClearCategory(GridHighlightCategory.TargetCell);

                Assert.That(middleTile.TargetRenderer.sharedMaterial, Is.SameAs(pathMaterial));
                Assert.That(manager.TryGetTopCategory(middle, out topCategory), Is.True);
                Assert.That(topCategory, Is.EqualTo(GridHighlightCategory.SelectedPath));

                manager.ClearHoverPath();

                Assert.That(manager.HoverPath, Is.Empty);
                Assert.That(startTile.IsHighlighted, Is.False);
                Assert.That(middleTile.IsHighlighted, Is.False);
                Assert.That(endTile.IsHighlighted, Is.False);
                Assert.That(middleTile.TargetRenderer.sharedMaterial, Is.SameAs(baseMaterial));
            }
            finally
            {
                Destroy(managerObject);
                for (var i = 0; i < tileObjects.Count; i += 1)
                {
                    Destroy(tileObjects[i]);
                }

                Destroy(baseMaterial);
                Destroy(pathMaterial);
                Destroy(targetMaterial);
            }
        }

        [Test]
        public void ClearAllRestoresEveryRegisteredTileToBaseMaterial()
        {
            var managerObject = new GameObject("Grid Clear Highlight Manager Test");
            var baseMaterial = CreateMaterial("Clear Base Tile Material");
            var safeMaterial = CreateMaterial("Safe Highlight Material");
            var threatenedMaterial = CreateMaterial("Threatened Highlight Material");
            var tileObjects = new List<GameObject>();

            try
            {
                var manager = managerObject.AddComponent<GridHighlightManager>();
                AssignObject(manager, "reactionSafeMaterial", safeMaterial);
                AssignObject(manager, "reactionThreatenedMaterial", threatenedMaterial);

                var safe = new GridPosition(0, 0, 1);
                var threatened = new GridPosition(1, 0, 1);
                var safeTile = CreateTile(tileObjects, safe, baseMaterial);
                var threatenedTile = CreateTile(tileObjects, threatened, baseMaterial);
                manager.RegisterTile(safeTile);
                manager.RegisterTile(threatenedTile);

                manager.HighlightCell(GridHighlightCategory.ReactionSafe, safe);
                manager.HighlightCell(GridHighlightCategory.ReactionThreatened, threatened);

                Assert.That(safeTile.TargetRenderer.sharedMaterial, Is.SameAs(safeMaterial));
                Assert.That(threatenedTile.TargetRenderer.sharedMaterial, Is.SameAs(threatenedMaterial));

                manager.ClearAll();

                Assert.That(manager.GetHighlightedCells(GridHighlightCategory.ReactionSafe), Is.Empty);
                Assert.That(manager.GetHighlightedCells(GridHighlightCategory.ReactionThreatened), Is.Empty);
                Assert.That(safeTile.IsHighlighted, Is.False);
                Assert.That(threatenedTile.IsHighlighted, Is.False);
                Assert.That(safeTile.TargetRenderer.sharedMaterial, Is.SameAs(baseMaterial));
                Assert.That(threatenedTile.TargetRenderer.sharedMaterial, Is.SameAs(baseMaterial));
            }
            finally
            {
                Destroy(managerObject);
                for (var i = 0; i < tileObjects.Count; i += 1)
                {
                    Destroy(tileObjects[i]);
                }

                Destroy(baseMaterial);
                Destroy(safeMaterial);
                Destroy(threatenedMaterial);
            }
        }

        private static GridTileView CreateTile(ICollection<GameObject> objects, GridPosition position, Material baseMaterial)
        {
            var tileObject = GameObject.CreatePrimitive(PrimitiveType.Cube);
            objects.Add(tileObject);
            return ConfigureTile(tileObject, position, baseMaterial);
        }

        private static GridTileView ConfigureTile(GameObject tileObject, GridPosition position, Material baseMaterial)
        {
            var tile = tileObject.AddComponent<GridTileView>();
            tile.SetGridPosition(position);
            tile.SetBaseMaterial(baseMaterial);
            tile.SetHighlighted(false);
            return tile;
        }

        private static void AssignObject(Object target, string propertyName, Object value)
        {
            var serializedObject = new SerializedObject(target);
            serializedObject.FindProperty(propertyName).objectReferenceValue = value;
            serializedObject.ApplyModifiedPropertiesWithoutUndo();
        }

        private static Material CreateMaterial(string name)
        {
            var shader = Shader.Find("Universal Render Pipeline/Lit")
                ?? Shader.Find("Standard")
                ?? Shader.Find("Sprites/Default")
                ?? Shader.Find("UI/Default");
            Assert.That(shader, Is.Not.Null, "At least one built-in shader must be available for material tests.");

            return new Material(shader)
            {
                name = name
            };
        }

        private static void Destroy(Object value)
        {
            Object.DestroyImmediate(value);
        }
    }
}
