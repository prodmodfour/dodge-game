using System.Text.RegularExpressions;
using NUnit.Framework;
using ReactionTactics.Grid;
using UnityEditor;
using UnityEngine;
using UnityEngine.TestTools;

namespace ReactionTactics.Tests.EditMode
{
    public sealed class GridManagerTests
    {
        [Test]
        public void RebuildMapBuildsCurrentMapFromAssignedDefinition()
        {
            var definition = CreateDefinition();
            var gameObject = CreateInactiveGameObject("Grid Manager Test");

            try
            {
                var manager = gameObject.AddComponent<GridManager>();
                AssignMapDefinition(manager, definition);

                Assert.That(manager.RebuildMap(), Is.True);

                Assert.That(manager.MapDefinition, Is.SameAs(definition));
                Assert.That(manager.HasCurrentMap, Is.True);
                Assert.That(manager.CurrentMap, Is.Not.Null);
                Assert.That(manager.CurrentMap.AllCells, Has.Count.EqualTo(4));
                Assert.That(manager.CurrentMap.GetCell(new GridPosition(1, 2, 0)).BlocksLineOfSight, Is.True);
            }
            finally
            {
                Destroy(gameObject);
                Destroy(definition);
            }
        }

        [Test]
        public void MissingMapDefinitionLogsClearErrorAndLeavesNoCurrentMap()
        {
            var gameObject = CreateInactiveGameObject("Missing Map Grid Manager");

            try
            {
                var manager = gameObject.AddComponent<GridManager>();
                LogAssert.Expect(
                    LogType.Error,
                    new Regex($"{nameof(GridManager)}.*{nameof(GridMapDefinition)}.*assigned"));

                Assert.That(manager.RebuildMap(), Is.False);

                Assert.That(manager.HasCurrentMap, Is.False);
                Assert.That(manager.CurrentMap, Is.Null);
            }
            finally
            {
                Destroy(gameObject);
            }
        }

        [Test]
        public void MetricsDefaultToPrototypeGridValues()
        {
            var gameObject = CreateInactiveGameObject("Grid Metrics Manager Test");

            try
            {
                var manager = gameObject.AddComponent<GridManager>();

                Assert.That(manager.Metrics.CellSize, Is.EqualTo(GridMetrics.Default.CellSize));
                Assert.That(manager.Metrics.HeightStep, Is.EqualTo(GridMetrics.Default.HeightStep));
                Assert.That(manager.Metrics.Origin, Is.EqualTo(GridMetrics.Default.Origin));
            }
            finally
            {
                Destroy(gameObject);
            }
        }

        private static GridMapDefinition CreateDefinition()
        {
            var definition = ScriptableObject.CreateInstance<GridMapDefinition>();
            definition.Configure(
                width: 2,
                depth: 2,
                defaultHeightY: 1,
                new[]
                {
                    new GridMapDefinition.CellOverride(
                        x: 1,
                        z: 0,
                        heightY: 2,
                        walkable: true,
                        blocksLineOfSight: true,
                        movementCost: 3)
                });
            return definition;
        }

        private static GameObject CreateInactiveGameObject(string name)
        {
            var gameObject = new GameObject(name);
            gameObject.SetActive(false);
            return gameObject;
        }

        private static void AssignMapDefinition(GridManager manager, GridMapDefinition definition)
        {
            var serializedObject = new SerializedObject(manager);
            serializedObject.FindProperty("mapDefinition").objectReferenceValue = definition;
            serializedObject.ApplyModifiedPropertiesWithoutUndo();
        }

        private static void Destroy(Object value)
        {
            Object.DestroyImmediate(value);
        }
    }
}
