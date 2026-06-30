using System;
using NUnit.Framework;
using ReactionTactics.Grid;
using UnityEngine;

namespace ReactionTactics.Tests.EditMode
{
    public sealed class GridMetricsTests
    {
        private const float Tolerance = 0.0001f;

        [Test]
        public void DefaultMetricsPlaceGridCellsAtUnitWorldCenters()
        {
            var metrics = GridMetrics.Default;

            AssertDefaultWorldPlacement(metrics);
        }

        [Test]
        public void DefaultStructMetricsFallBackToPrototypeDefaults()
        {
            var metrics = default(GridMetrics);

            AssertDefaultWorldPlacement(metrics);
        }

        [Test]
        public void CustomMetricsApplyCellSizeHeightStepAndOrigin()
        {
            var metrics = new GridMetrics(2f, 0.5f, new Vector3(10f, 1f, -4f));

            Assert.That(metrics.CellSize, Is.EqualTo(2f));
            Assert.That(metrics.HeightStep, Is.EqualTo(0.5f));
            AssertVector(metrics.Origin, new Vector3(10f, 1f, -4f));
            AssertVector(metrics.GridToWorldCenter(new GridPosition(3, 4, -2)), new Vector3(17f, 3f, -7f));
        }

        [Test]
        public void WorldToApproxGridReturnsCellContainingWorldPoint()
        {
            var metrics = new GridMetrics(2f, 0.5f, new Vector3(10f, 1f, -4f));

            var result = metrics.WorldToApproxGrid(new Vector3(13.99f, 2.24f, -2.01f));

            Assert.That(result, Is.EqualTo(new GridPosition(1, 2, 0)));
        }

        [Test]
        public void WorldToApproxGridHandlesNegativeCellsByFlooringHorizontalAxes()
        {
            var metrics = GridMetrics.Default;

            var result = metrics.WorldToApproxGrid(new Vector3(-0.01f, -1.49f, -1.01f));

            Assert.That(result, Is.EqualTo(new GridPosition(-1, -1, -2)));
        }

        [Test]
        public void GridCentersRoundTripThroughWorldToApproxGrid()
        {
            var metrics = new GridMetrics(1.25f, 0.75f, new Vector3(-3f, 2f, 5f));
            var positions = new[]
            {
                GridPosition.Zero,
                new GridPosition(2, 3, 4),
                new GridPosition(-2, -1, -3)
            };

            foreach (var position in positions)
            {
                var worldCenter = metrics.GridToWorldCenter(position);

                Assert.That(metrics.WorldToApproxGrid(worldCenter), Is.EqualTo(position));
            }
        }

        [Test]
        public void ConstructorRejectsInvalidMetricValues()
        {
            Assert.Throws<ArgumentOutOfRangeException>(() => new GridMetrics(0f, 1f, Vector3.zero));
            Assert.Throws<ArgumentOutOfRangeException>(() => new GridMetrics(-1f, 1f, Vector3.zero));
            Assert.Throws<ArgumentOutOfRangeException>(() => new GridMetrics(float.NaN, 1f, Vector3.zero));
            Assert.Throws<ArgumentOutOfRangeException>(() => new GridMetrics(1f, 0f, Vector3.zero));
            Assert.Throws<ArgumentOutOfRangeException>(() => new GridMetrics(1f, float.PositiveInfinity, Vector3.zero));
        }

        [Test]
        public void ConstructorRejectsInvalidOriginValues()
        {
            Assert.Throws<ArgumentOutOfRangeException>(() => new GridMetrics(1f, 1f, new Vector3(float.NaN, 0f, 0f)));
            Assert.Throws<ArgumentOutOfRangeException>(() => new GridMetrics(1f, 1f, new Vector3(0f, float.NegativeInfinity, 0f)));
            Assert.Throws<ArgumentOutOfRangeException>(() => new GridMetrics(1f, 1f, new Vector3(0f, 0f, float.PositiveInfinity)));
        }

        private static void AssertDefaultWorldPlacement(GridMetrics metrics)
        {
            Assert.That(metrics.CellSize, Is.EqualTo(1f));
            Assert.That(metrics.HeightStep, Is.EqualTo(1f));
            AssertVector(metrics.Origin, Vector3.zero);
            AssertVector(metrics.GridToWorldCenter(GridPosition.Zero), new Vector3(0.5f, 0f, 0.5f));
            AssertVector(metrics.GridToWorldCenter(new GridPosition(2, 3, -1)), new Vector3(2.5f, 3f, -0.5f));
        }

        private static void AssertVector(Vector3 actual, Vector3 expected)
        {
            Assert.That(actual.x, Is.EqualTo(expected.x).Within(Tolerance));
            Assert.That(actual.y, Is.EqualTo(expected.y).Within(Tolerance));
            Assert.That(actual.z, Is.EqualTo(expected.z).Within(Tolerance));
        }
    }
}
