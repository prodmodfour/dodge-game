using System;
using NUnit.Framework;
using ReactionTactics.Grid;
using ReactionTactics.Units;
using UnityEngine;

namespace ReactionTactics.Tests.EditMode.Units
{
    public sealed class TacticalUnitTests
    {
        [Test]
        public void NewComponentStartsUninitialized()
        {
            var gameObject = new GameObject("Tactical Unit Test");

            try
            {
                var unit = gameObject.AddComponent<TacticalUnit>();

                Assert.That(unit.UnitId, Is.EqualTo(UnitId.None));
                Assert.That(unit.Team, Is.EqualTo(TeamId.Player));
                Assert.That(unit.StatsDefinition, Is.Null);
                Assert.That(unit.CurrentGridPosition, Is.EqualTo(GridPosition.Zero));
                Assert.That(unit.CurrentHP, Is.EqualTo(0));
                Assert.That(unit.CurrentAP, Is.EqualTo(0));
                Assert.That(unit.IsAlive, Is.False);
                Assert.That(unit.IsDead, Is.True);
                Assert.That(unit.IsInitialized, Is.False);
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(gameObject);
            }
        }

        [Test]
        public void InitializeCopiesStatsIdentityTeamAndPosition()
        {
            var gameObject = new GameObject("Initialized Tactical Unit Test");
            var stats = CreateStats();

            try
            {
                var unit = gameObject.AddComponent<TacticalUnit>();
                var unitId = new UnitId(7);
                var position = new GridPosition(2, 1, 3);

                unit.Initialize(unitId, TeamId.Enemy, stats, position);

                Assert.That(unit.UnitId, Is.EqualTo(unitId));
                Assert.That(unit.Team, Is.EqualTo(TeamId.Enemy));
                Assert.That(unit.StatsDefinition, Is.SameAs(stats));
                Assert.That(unit.DisplayName, Is.EqualTo("Knight"));
                Assert.That(unit.CurrentGridPosition, Is.EqualTo(position));
                Assert.That(unit.CurrentHP, Is.EqualTo(stats.MaxHP));
                Assert.That(unit.CurrentAP, Is.EqualTo(stats.MaxAP));
                Assert.That(unit.MaxHP, Is.EqualTo(stats.MaxHP));
                Assert.That(unit.MaxAP, Is.EqualTo(stats.MaxAP));
                Assert.That(unit.MeleeRange, Is.EqualTo(stats.MeleeRange));
                Assert.That(unit.IsAlive, Is.True);
                Assert.That(unit.IsDead, Is.False);
                Assert.That(unit.IsInitialized, Is.True);
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(gameObject);
                UnityEngine.Object.DestroyImmediate(stats);
            }
        }

        [Test]
        public void SetGridPositionUpdatesOnlyPosition()
        {
            var gameObject = new GameObject("Position Tactical Unit Test");
            var stats = CreateStats();

            try
            {
                var unit = gameObject.AddComponent<TacticalUnit>();
                unit.Initialize(new UnitId(3), TeamId.Player, stats, GridPosition.Zero);
                var hp = unit.CurrentHP;
                var ap = unit.CurrentAP;
                var destination = new GridPosition(4, 2, 5);

                unit.SetGridPosition(destination);

                Assert.That(unit.CurrentGridPosition, Is.EqualTo(destination));
                Assert.That(unit.CurrentHP, Is.EqualTo(hp));
                Assert.That(unit.CurrentAP, Is.EqualTo(ap));
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(gameObject);
                UnityEngine.Object.DestroyImmediate(stats);
            }
        }

        [Test]
        public void InitializeRejectsInvalidInputs()
        {
            var gameObject = new GameObject("Invalid Tactical Unit Test");
            var stats = CreateStats();

            try
            {
                var unit = gameObject.AddComponent<TacticalUnit>();

                Assert.Throws<ArgumentException>(() => unit.Initialize(UnitId.None, TeamId.Player, stats, GridPosition.Zero));
                Assert.Throws<ArgumentOutOfRangeException>(() => unit.Initialize(new UnitId(1), (TeamId)999, stats, GridPosition.Zero));
                Assert.Throws<ArgumentNullException>(() => unit.Initialize(new UnitId(1), TeamId.Player, null, GridPosition.Zero));
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(gameObject);
                UnityEngine.Object.DestroyImmediate(stats);
            }
        }

        private static UnitStatsDefinition CreateStats()
        {
            var stats = ScriptableObject.CreateInstance<UnitStatsDefinition>();
            stats.Configure(
                displayName: "Knight",
                maxHP: 18,
                maxAP: 6,
                movementAnimationSpeed: 4f,
                meleeRange: 1,
                teamColorHint: Color.blue);
            return stats;
        }
    }
}
