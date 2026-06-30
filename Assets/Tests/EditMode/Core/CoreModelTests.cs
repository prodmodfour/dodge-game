using System;
using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using ReactionTactics.Grid;
using ReactionTactics.Units;
using UnityEngine;

namespace ReactionTactics.Tests.EditMode.Core
{
    public sealed class CoreModelTests
    {
        private const float Tolerance = 0.0001f;

        [Test]
        public void GridPositionEqualityAndHashingSupportDictionaryKeys()
        {
            var position = new GridPosition(3, 1, 5);
            var samePosition = new GridPosition(3, 1, 5);
            var differentHeight = new GridPosition(3, 2, 5);
            var labels = new Dictionary<GridPosition, string>
            {
                [position] = "spawn"
            };

            Assert.That(position, Is.EqualTo(samePosition));
            Assert.That(position == samePosition, Is.True);
            Assert.That(position != differentHeight, Is.True);
            Assert.That(position.GetHashCode(), Is.EqualTo(samePosition.GetHashCode()));
            Assert.That(labels.ContainsKey(samePosition), Is.True);
            Assert.That(labels[samePosition], Is.EqualTo("spawn"));
            Assert.That(position.ToString(), Is.EqualTo("(3,1,5)"));
        }

        [Test]
        public void GridPositionDistanceHelpersAreSymmetricAndRespectVerticalRules()
        {
            var start = new GridPosition(-2, 1, 4);
            var end = new GridPosition(3, 5, -1);

            Assert.That(start.ManhattanDistanceTo(end), Is.EqualTo(14));
            Assert.That(end.ManhattanDistanceTo(start), Is.EqualTo(14));
            Assert.That(start.HorizontalDistanceTo(end), Is.EqualTo(10));
            Assert.That(end.HorizontalDistanceTo(start), Is.EqualTo(10));
            Assert.That(start.TacticalDistanceTo(end, verticalWeight: 2), Is.EqualTo(18));
            Assert.That(end.TacticalDistanceTo(start, verticalWeight: 2), Is.EqualTo(18));
            Assert.That(start.IsFourWayAdjacentTo(new GridPosition(-1, 2, 4), maxHeightDifference: 1), Is.True);
            Assert.That(start.IsFourWayAdjacentTo(new GridPosition(-1, 3, 4), maxHeightDifference: 1), Is.False);
        }

        [Test]
        public void CardinalDirectionHelpersProvideDeterministicOffsetsAndTieBreaks()
        {
            var origin = GridPosition.Zero;

            Assert.That(CardinalDirection.North.ToOffset(), Is.EqualTo(GridPosition.North));
            Assert.That(CardinalDirection.East.ToOffset(), Is.EqualTo(GridPosition.East));
            Assert.That(CardinalDirection.South.ToOffset(), Is.EqualTo(GridPosition.South));
            Assert.That(CardinalDirection.West.ToOffset(), Is.EqualTo(GridPosition.West));
            Assert.That(CardinalDirection.North.Left(), Is.EqualTo(CardinalDirection.West));
            Assert.That(CardinalDirection.North.Right(), Is.EqualTo(CardinalDirection.East));
            Assert.That(CardinalDirection.East.Opposite(), Is.EqualTo(CardinalDirection.West));
            Assert.That(CardinalDirectionMath.FromTo(origin, new GridPosition(3, 0, 3)), Is.EqualTo(CardinalDirection.North));
            Assert.That(CardinalDirectionMath.FromTo(origin, new GridPosition(4, 0, 1)), Is.EqualTo(CardinalDirection.East));
            Assert.That(CardinalDirectionMath.FromTo(origin, new GridPosition(-2, 0, -2)), Is.EqualTo(CardinalDirection.South));
        }

        [Test]
        public void GridMapRejectsDuplicateCellsAndLooksUpStoredCells()
        {
            var origin = new GridCell(GridPosition.Zero);
            var raised = new GridCell(new GridPosition(1, 2, 0), movementCost: 3);
            var blocked = new GridCell(new GridPosition(2, 0, 0), walkable: false, blocksMovement: true, blocksLineOfSight: true, movementCost: 1);
            var map = new GridMap(origin, raised, blocked);

            Assert.That(map.AllCells.ToArray(), Is.EqualTo(new[] { origin, raised, blocked }));
            Assert.That(map.MinBounds, Is.EqualTo(GridPosition.Zero));
            Assert.That(map.MaxBounds, Is.EqualTo(new GridPosition(2, 2, 0)));
            Assert.That(map.Contains(raised.Position), Is.True);
            Assert.That(map.TryGetCell(raised.Position, out var foundCell), Is.True);
            Assert.That(foundCell, Is.EqualTo(raised));
            Assert.That(map.GetCell(blocked.Position), Is.EqualTo(blocked));
            Assert.That(map.IsWalkable(origin.Position), Is.True);
            Assert.That(map.BlocksMovement(blocked.Position), Is.True);
            Assert.That(map.BlocksLineOfSight(blocked.Position), Is.True);
            Assert.Throws<ArgumentException>(() => new GridMap(origin, origin.CopyWith(movementCost: 2)));
        }

        [Test]
        public void GridMetricsDefaultConversionsUseCellCentersAndRoundTrip()
        {
            var metrics = GridMetrics.Default;
            var defaultStructMetrics = default(GridMetrics);
            var elevated = new GridPosition(2, 3, -1);

            AssertVector(metrics.GridToWorldCenter(GridPosition.Zero), new Vector3(0.5f, 0f, 0.5f));
            AssertVector(metrics.GridToWorldCenter(elevated), new Vector3(2.5f, 3f, -0.5f));
            Assert.That(metrics.WorldToApproxGrid(metrics.GridToWorldCenter(elevated)), Is.EqualTo(elevated));
            AssertVector(defaultStructMetrics.GridToWorldCenter(GridPosition.Zero), new Vector3(0.5f, 0f, 0.5f));
            Assert.That(defaultStructMetrics.WorldToApproxGrid(new Vector3(0.99f, 2.49f, 0.01f)), Is.EqualTo(new GridPosition(0, 2, 0)));
        }

        [Test]
        public void UnitIdentityModelsCompareHashAndLogDeterministically()
        {
            var first = UnitId.FromInt(1);
            var sameFirst = new UnitId(1);
            var second = new UnitId(2);
            var ids = new List<UnitId> { second, first };

            ids.Sort();

            Assert.That(TeamId.Player.IsFriendlyTo(TeamId.Player), Is.True);
            Assert.That(TeamId.Player.IsHostileTo(TeamId.Enemy), Is.True);
            Assert.That(first, Is.EqualTo(sameFirst));
            Assert.That(first.GetHashCode(), Is.EqualTo(sameFirst.GetHashCode()));
            Assert.That(ids, Is.EqualTo(new[] { first, second }));
            Assert.That(UnitId.None.IsAssigned, Is.False);
            Assert.That(first.ToString(), Is.EqualTo("Unit#1"));
        }

        private static void AssertVector(Vector3 actual, Vector3 expected)
        {
            Assert.That(actual.x, Is.EqualTo(expected.x).Within(Tolerance));
            Assert.That(actual.y, Is.EqualTo(expected.y).Within(Tolerance));
            Assert.That(actual.z, Is.EqualTo(expected.z).Within(Tolerance));
        }
    }
}
