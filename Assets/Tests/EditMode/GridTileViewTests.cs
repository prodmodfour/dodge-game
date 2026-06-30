using NUnit.Framework;
using ReactionTactics.Grid;
using UnityEngine;

namespace ReactionTactics.Tests.EditMode
{
    public sealed class GridTileViewTests
    {
        private const float Tolerance = 0.0001f;

        [Test]
        public void AddedTileViewIncludesPickingColliderAndStoresGridPosition()
        {
            var gameObject = new GameObject("Tile View Test");

            try
            {
                var view = gameObject.AddComponent<GridTileView>();
                var position = new GridPosition(2, 1, 3);

                view.SetGridPosition(position);

                Assert.That(view.GridPosition, Is.EqualTo(position));
                Assert.That(gameObject.GetComponent<BoxCollider>(), Is.Not.Null);
                Assert.That(view.PickingCollider, Is.Not.Null);
                Assert.That(view.PickingCollider.enabled, Is.True);
            }
            finally
            {
                Destroy(gameObject);
            }
        }

        [Test]
        public void ApplyCellDataStoresPositionAndScalesHeightFromCellDisplayData()
        {
            var gameObject = new GameObject("Height Tile View Test");

            try
            {
                gameObject.transform.localScale = new Vector3(2f, 1f, 3f);
                var view = gameObject.AddComponent<GridTileView>();
                var cell = new GridCell(
                    new GridPosition(4, 2, 5),
                    walkable: true,
                    blocksMovement: false,
                    blocksLineOfSight: false,
                    movementCost: 1,
                    displayHeight: 2f);
                var metrics = new GridMetrics(cellSize: 1.5f, heightStep: 0.5f);

                view.ApplyCellData(cell, metrics);

                Assert.That(view.GridPosition, Is.EqualTo(cell.Position));
                Assert.That(gameObject.transform.localScale.x, Is.EqualTo(2f).Within(Tolerance));
                Assert.That(gameObject.transform.localScale.y, Is.EqualTo(1.5f).Within(Tolerance));
                Assert.That(gameObject.transform.localScale.z, Is.EqualTo(3f).Within(Tolerance));
                Assert.That(GridTileView.CalculateVisualHeight(cell, metrics), Is.EqualTo(1.5f).Within(Tolerance));
            }
            finally
            {
                Destroy(gameObject);
            }
        }

        [Test]
        public void HighlightStateSwapsBetweenBaseAndHighlightMaterials()
        {
            var gameObject = GameObject.CreatePrimitive(PrimitiveType.Cube);
            var baseMaterial = CreateMaterial("Tile Base Material");
            var highlightMaterial = CreateMaterial("Tile Highlight Material");

            try
            {
                var renderer = gameObject.GetComponent<Renderer>();
                var view = gameObject.AddComponent<GridTileView>();

                view.SetBaseMaterial(baseMaterial);
                view.SetHighlightMaterial(highlightMaterial);

                Assert.That(view.BaseMaterial, Is.SameAs(baseMaterial));
                Assert.That(view.HighlightMaterial, Is.SameAs(highlightMaterial));
                Assert.That(view.IsHighlighted, Is.False);
                Assert.That(renderer.sharedMaterial, Is.SameAs(baseMaterial));

                view.SetHighlighted(true);

                Assert.That(view.IsHighlighted, Is.True);
                Assert.That(renderer.sharedMaterial, Is.SameAs(highlightMaterial));

                view.SetHighlighted(false);

                Assert.That(view.IsHighlighted, Is.False);
                Assert.That(renderer.sharedMaterial, Is.SameAs(baseMaterial));
            }
            finally
            {
                Destroy(gameObject);
                Destroy(baseMaterial);
                Destroy(highlightMaterial);
            }
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
