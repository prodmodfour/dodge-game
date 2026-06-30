using System;
using System.Collections;
using NUnit.Framework;
using ReactionTactics.Grid;
using ReactionTactics.Pathfinding;
using ReactionTactics.Units;
using UnityEngine;

namespace ReactionTactics.Tests.EditMode.Units
{
    public sealed class GridPathMoverTests
    {
        private const float Tolerance = 0.0001f;

        [Test]
        public void BuildWorldWaypointsUsesPathPositionsAndGridMetrics()
        {
            var start = new GridPosition(0, 0, 0);
            var destination = new GridPosition(2, 3, -1);
            var path = CreatePath(start, destination);
            var metrics = new GridMetrics(2f, 0.5f, new Vector3(10f, 1f, -4f));

            var waypoints = GridPathMover.BuildWorldWaypoints(path, metrics);

            Assert.That(waypoints, Has.Length.EqualTo(2));
            AssertVector3(waypoints[0], metrics.GridToWorldCenter(start));
            AssertVector3(waypoints[1], metrics.GridToWorldCenter(destination));
        }

        [Test]
        public void MoveAlongPathCoroutineSnapsFinalPositionAndInvokesCallbacks()
        {
            var gameObject = new GameObject("Grid Path Mover Test");

            try
            {
                var mover = gameObject.AddComponent<GridPathMover>();
                mover.MovementSpeed = 1000f;
                var start = new GridPosition(0, 0, 0);
                var middle = new GridPosition(1, 0, 0);
                var destination = new GridPosition(1, 2, 1);
                var path = CreatePath(start, middle, destination);
                var metrics = new GridMetrics(1.5f, 0.25f, new Vector3(-2f, 3f, 5f));
                var eventCount = 0;
                var callbackCount = 0;
                GridPath completedPathFromEvent = null;
                GridPath completedPathFromCallback = null;

                mover.MovementCompleted += (source, completedPath) =>
                {
                    Assert.That(source, Is.SameAs(mover));
                    completedPathFromEvent = completedPath;
                    eventCount++;
                };

                RunToCompletion(mover.MoveAlongPathCoroutine(path, metrics, (source, completedPath) =>
                {
                    Assert.That(source, Is.SameAs(mover));
                    completedPathFromCallback = completedPath;
                    callbackCount++;
                }));

                Assert.That(mover.IsMoving, Is.False);
                Assert.That(mover.ActivePath, Is.Null);
                Assert.That(mover.CurrentDestination.HasValue, Is.False);
                Assert.That(eventCount, Is.EqualTo(1));
                Assert.That(callbackCount, Is.EqualTo(1));
                Assert.That(completedPathFromEvent, Is.SameAs(path));
                Assert.That(completedPathFromCallback, Is.SameAs(path));
                AssertVector3(gameObject.transform.position, metrics.GridToWorldCenter(destination));
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(gameObject);
            }
        }

        [Test]
        public void SnapToPlacesMoverAtCellCenter()
        {
            var gameObject = new GameObject("Grid Path Mover Snap Test");

            try
            {
                var mover = gameObject.AddComponent<GridPathMover>();
                var metrics = new GridMetrics(2f, 0.5f, Vector3.one);
                var position = new GridPosition(3, 2, 4);

                mover.SnapTo(position, metrics);

                AssertVector3(gameObject.transform.position, metrics.GridToWorldCenter(position));
                Assert.That(mover.Metrics.CellSize, Is.EqualTo(metrics.CellSize));
                Assert.That(mover.Metrics.HeightStep, Is.EqualTo(metrics.HeightStep));
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(gameObject);
            }
        }

        [Test]
        public void BuildWorldWaypointsRejectsInvalidPaths()
        {
            Assert.Throws<ArgumentNullException>(() => GridPathMover.BuildWorldWaypoints(null, GridMetrics.Default));
            Assert.Throws<ArgumentException>(() => GridPathMover.BuildWorldWaypoints(
                GridPath.Failure("No route."),
                GridMetrics.Default));
        }

        private static GridPath CreatePath(params GridPosition[] positions)
        {
            var steps = new PathStep[positions.Length];
            for (var index = 0; index < positions.Length; index++)
            {
                steps[index] = new PathStep(
                    positions[index],
                    index == 0 ? 0 : 1,
                    index == 0 ? 0 : index);
            }

            return GridPath.Success(steps);
        }

        private static void RunToCompletion(IEnumerator enumerator)
        {
            const int MaxFrames = 100;
            var frameCount = 0;
            while (enumerator.MoveNext())
            {
                frameCount++;
                if (frameCount > MaxFrames)
                {
                    Assert.Fail($"Coroutine did not complete within {MaxFrames} manual frames.");
                }
            }
        }

        private static void AssertVector3(Vector3 actual, Vector3 expected)
        {
            Assert.That(actual.x, Is.EqualTo(expected.x).Within(Tolerance));
            Assert.That(actual.y, Is.EqualTo(expected.y).Within(Tolerance));
            Assert.That(actual.z, Is.EqualTo(expected.z).Within(Tolerance));
        }
    }
}
