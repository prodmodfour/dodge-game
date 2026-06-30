using System;
using NUnit.Framework;
using ReactionTactics.Grid;
using ReactionTactics.Input;
using UnityEngine;

namespace ReactionTactics.Tests.EditMode.Input
{
    public sealed class TacticalCameraControllerTests
    {
        private const float Tolerance = 0.0001f;

        [Test]
        public void SetViewClampsPitchAndZoomAndAppliesTransform()
        {
            var gameObject = new GameObject("Tactical Camera Controller Test");

            try
            {
                gameObject.AddComponent<Camera>();
                var controller = gameObject.AddComponent<TacticalCameraController>();
                controller.SetZoomLimits(5f, 10f);
                controller.SetPitchLimits(30f, 60f);

                controller.SetView(new Vector3(2f, 3f, 4f), 25f, 90f, 100f);

                Assert.That(controller.PitchDegrees, Is.EqualTo(60f).Within(Tolerance));
                Assert.That(controller.ZoomDistance, Is.EqualTo(10f).Within(Tolerance));
                Assert.That(controller.FocusPoint, Is.EqualTo(new Vector3(2f, 3f, 4f)));

                var expectedRotation = Quaternion.Euler(60f, 25f, 0f);
                var expectedPosition = controller.FocusPoint - (expectedRotation * Vector3.forward * 10f);
                AssertVector3(gameObject.transform.position, expectedPosition);
                AssertQuaternion(gameObject.transform.rotation, expectedRotation);
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(gameObject);
            }
        }

        [Test]
        public void FrameMapFocusesOnGridBoundsAndUsesReadableZoom()
        {
            var gameObject = new GameObject("Tactical Camera Frame Test");

            try
            {
                gameObject.AddComponent<Camera>();
                var controller = gameObject.AddComponent<TacticalCameraController>();
                var metrics = GridMetrics.Default;
                var map = new GridMap(
                    new GridCell(new GridPosition(0, 0, 0)),
                    new GridCell(new GridPosition(7, 2, 7)));

                controller.FrameMap(map, metrics);

                AssertVector3(controller.FocusPoint, new Vector3(4f, 1f, 4f));
                Assert.That(controller.ZoomDistance, Is.GreaterThan(12f));
                Assert.That(controller.ZoomDistance, Is.LessThan(controller.MaximumZoomDistance));
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(gameObject);
            }
        }

        [Test]
        public void MapFrameHelpersRejectMissingMap()
        {
            Assert.Throws<ArgumentNullException>(() => TacticalCameraController.CalculateMapFocusPoint(null, GridMetrics.Default));
            Assert.Throws<ArgumentNullException>(() => TacticalCameraController.CalculateMapZoomDistance(null, GridMetrics.Default, 1f));
        }

        private static void AssertVector3(Vector3 actual, Vector3 expected)
        {
            Assert.That(actual.x, Is.EqualTo(expected.x).Within(Tolerance));
            Assert.That(actual.y, Is.EqualTo(expected.y).Within(Tolerance));
            Assert.That(actual.z, Is.EqualTo(expected.z).Within(Tolerance));
        }

        private static void AssertQuaternion(Quaternion actual, Quaternion expected)
        {
            Assert.That(actual.x, Is.EqualTo(expected.x).Within(Tolerance));
            Assert.That(actual.y, Is.EqualTo(expected.y).Within(Tolerance));
            Assert.That(actual.z, Is.EqualTo(expected.z).Within(Tolerance));
            Assert.That(actual.w, Is.EqualTo(expected.w).Within(Tolerance));
        }
    }
}
