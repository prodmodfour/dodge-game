using System;
using System.Linq;
using NUnit.Framework;
using ReactionTactics.Grid;
using UnityEngine;

namespace ReactionTactics.Tests.EditMode
{
    public sealed class GridMapDefinitionTests
    {
        [Test]
        public void BuildGridMapCreatesDefaultCellsForEveryHorizontalCoordinate()
        {
            var definition = CreateDefinition(
                width: 2,
                depth: 3,
                defaultHeightY: 1);

            try
            {
                var map = definition.BuildGridMap();

                Assert.That(map.AllCells, Has.Count.EqualTo(6));
                Assert.That(map.MinBounds, Is.EqualTo(new GridPosition(0, 1, 0)));
                Assert.That(map.MaxBounds, Is.EqualTo(new GridPosition(1, 1, 2)));
                Assert.That(map.GetCell(new GridPosition(0, 1, 0)).Walkable, Is.True);
                Assert.That(map.GetCell(new GridPosition(1, 1, 2)).MovementCost, Is.EqualTo(GridCell.DefaultMovementCost));
            }
            finally
            {
                Destroy(definition);
            }
        }

        [Test]
        public void BuildGridMapAppliesCellOverridesAtConfiguredHeight()
        {
            var definition = CreateDefinition(
                width: 3,
                depth: 2,
                defaultHeightY: 0,
                new GridMapDefinition.CellOverride(
                    x: 2,
                    z: 1,
                    heightY: 3,
                    walkable: false,
                    blocksLineOfSight: true,
                    movementCost: 4));

            try
            {
                var map = definition.BuildGridMap();
                var overriddenPosition = new GridPosition(2, 3, 1);
                var overriddenCell = map.GetCell(overriddenPosition);

                Assert.That(map.Contains(new GridPosition(2, 0, 1)), Is.False);
                Assert.That(overriddenCell.Position, Is.EqualTo(overriddenPosition));
                Assert.That(overriddenCell.Walkable, Is.False);
                Assert.That(overriddenCell.BlocksMovement, Is.True);
                Assert.That(overriddenCell.BlocksLineOfSight, Is.True);
                Assert.That(overriddenCell.MovementCost, Is.EqualTo(4));
                Assert.That(overriddenCell.DisplayHeight, Is.EqualTo(3f));
            }
            finally
            {
                Destroy(definition);
            }
        }

        [Test]
        public void BuildGridMapUsesDeterministicRowMajorOrder()
        {
            var definition = CreateDefinition(
                width: 2,
                depth: 2,
                defaultHeightY: 0,
                new GridMapDefinition.CellOverride(
                    x: 1,
                    z: 0,
                    heightY: 2,
                    walkable: true,
                    blocksLineOfSight: false,
                    movementCost: 3));

            try
            {
                var positions = definition.BuildGridMap().AllCells
                    .Select(cell => cell.Position)
                    .ToArray();

                Assert.That(positions, Is.EqualTo(new[]
                {
                    new GridPosition(0, 0, 0),
                    new GridPosition(1, 2, 0),
                    new GridPosition(0, 0, 1),
                    new GridPosition(1, 0, 1),
                }));
            }
            finally
            {
                Destroy(definition);
            }
        }

        [Test]
        public void BuildGridMapRejectsDuplicateHorizontalOverrides()
        {
            var definition = CreateDefinition(
                width: 2,
                depth: 2,
                defaultHeightY: 0,
                new GridMapDefinition.CellOverride(1, 1, 0, true, false, 1),
                new GridMapDefinition.CellOverride(1, 1, 2, false, true, 2));

            try
            {
                var exception = Assert.Throws<InvalidOperationException>(() => definition.BuildGridMap());

                Assert.That(exception.Message, Does.Contain("duplicate overrides"));
                Assert.That(exception.Message, Does.Contain("(1,1)"));
            }
            finally
            {
                Destroy(definition);
            }
        }

        [Test]
        public void BuildGridMapRejectsOutOfBoundsOverrides()
        {
            var definition = CreateDefinition(
                width: 2,
                depth: 2,
                defaultHeightY: 0,
                new GridMapDefinition.CellOverride(2, 0, 0, true, false, 1));

            try
            {
                var exception = Assert.Throws<InvalidOperationException>(() => definition.BuildGridMap());

                Assert.That(exception.Message, Does.Contain("outside map bounds"));
            }
            finally
            {
                Destroy(definition);
            }
        }

        [Test]
        public void ConfigureRejectsInvalidDimensions()
        {
            var definition = ScriptableObject.CreateInstance<GridMapDefinition>();

            try
            {
                Assert.Throws<ArgumentOutOfRangeException>(() => definition.Configure(0, 1, 0, Array.Empty<GridMapDefinition.CellOverride>()));
                Assert.Throws<ArgumentOutOfRangeException>(() => definition.Configure(1, 0, 0, Array.Empty<GridMapDefinition.CellOverride>()));
            }
            finally
            {
                Destroy(definition);
            }
        }

        [Test]
        public void CellOverrideRejectsInvalidMovementCost()
        {
            Assert.Throws<ArgumentOutOfRangeException>(() => new GridMapDefinition.CellOverride(
                x: 0,
                z: 0,
                heightY: 0,
                walkable: true,
                blocksLineOfSight: false,
                movementCost: 0));
        }

        private static GridMapDefinition CreateDefinition(
            int width,
            int depth,
            int defaultHeightY,
            params GridMapDefinition.CellOverride[] overrides)
        {
            var definition = ScriptableObject.CreateInstance<GridMapDefinition>();
            definition.Configure(width, depth, defaultHeightY, overrides);
            return definition;
        }

        private static void Destroy(GridMapDefinition definition)
        {
            UnityEngine.Object.DestroyImmediate(definition);
        }
    }
}
