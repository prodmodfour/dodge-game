using System;
using NUnit.Framework;
using ReactionTactics.Grid;
using ReactionTactics.Units;
using UnityEngine;

namespace ReactionTactics.Tests.EditMode.Units
{
    public sealed class ActionPointTests
    {
        [Test]
        public void CanSpendAPRejectsNegativeAndInsufficientCosts()
        {
            var gameObject = new GameObject("AP Wallet Can Spend Test");
            var stats = CreateStats(maxAP: 6);

            try
            {
                var unit = gameObject.AddComponent<TacticalUnit>();
                unit.Initialize(new UnitId(1), TeamId.Player, stats, GridPosition.Zero);
                unit.SetAPForTest(3);

                Assert.That(unit.CanSpendAP(-1), Is.False);
                Assert.That(unit.CanSpendAP(0), Is.True);
                Assert.That(unit.CanSpendAP(3), Is.True);
                Assert.That(unit.CanSpendAP(4), Is.False);
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(gameObject);
                UnityEngine.Object.DestroyImmediate(stats);
            }
        }

        [Test]
        public void SpendAPConsumesSharedPoolAndCanReachZero()
        {
            var gameObject = new GameObject("AP Wallet Spend Test");
            var stats = CreateStats(maxAP: 6);

            try
            {
                var unit = gameObject.AddComponent<TacticalUnit>();
                unit.Initialize(new UnitId(2), TeamId.Player, stats, GridPosition.Zero);

                var firstSpend = unit.SpendAP(4);
                var secondSpend = unit.SpendAP(2);

                Assert.That(firstSpend.IsSuccess, Is.True);
                Assert.That(secondSpend.IsSuccess, Is.True);
                Assert.That(unit.CurrentAP, Is.EqualTo(0));
                Assert.That(unit.CanSpendAP(1), Is.False);
                Assert.That(unit.CanSpendAP(0), Is.True);
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(gameObject);
                UnityEngine.Object.DestroyImmediate(stats);
            }
        }

        [Test]
        public void SpendAPFailsWithoutChangingBalanceWhenInsufficient()
        {
            var gameObject = new GameObject("AP Wallet Insufficient Test");
            var stats = CreateStats(maxAP: 6);

            try
            {
                var unit = gameObject.AddComponent<TacticalUnit>();
                unit.Initialize(new UnitId(3), TeamId.Player, stats, GridPosition.Zero);
                unit.SetAPForTest(2);

                var result = unit.SpendAP(3);

                Assert.That(result.IsFailure, Is.True);
                Assert.That(result.ErrorMessage, Does.Contain("needs 3 AP"));
                Assert.That(result.ErrorMessage, Does.Contain("only has 2 AP"));
                Assert.That(unit.CurrentAP, Is.EqualTo(2));
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(gameObject);
                UnityEngine.Object.DestroyImmediate(stats);
            }
        }

        [Test]
        public void SpendAPRejectsNegativeCostsWithoutChangingBalance()
        {
            var gameObject = new GameObject("AP Wallet Negative Spend Test");
            var stats = CreateStats(maxAP: 6);

            try
            {
                var unit = gameObject.AddComponent<TacticalUnit>();
                unit.Initialize(new UnitId(4), TeamId.Player, stats, GridPosition.Zero);

                var result = unit.SpendAP(-1);

                Assert.That(result.IsFailure, Is.True);
                Assert.That(result.ErrorMessage, Does.Contain("cannot be negative"));
                Assert.That(unit.CurrentAP, Is.EqualTo(stats.MaxAP));
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(gameObject);
                UnityEngine.Object.DestroyImmediate(stats);
            }
        }

        [Test]
        public void RefreshAPRestoresMaxAPAfterSpending()
        {
            var gameObject = new GameObject("AP Wallet Refresh Test");
            var stats = CreateStats(maxAP: 7);

            try
            {
                var unit = gameObject.AddComponent<TacticalUnit>();
                unit.Initialize(new UnitId(5), TeamId.Player, stats, GridPosition.Zero);
                Assert.That(unit.SpendAP(5).IsSuccess, Is.True);

                unit.RefreshAP();

                Assert.That(unit.CurrentAP, Is.EqualTo(stats.MaxAP));
                Assert.That(unit.CanSpendAP(stats.MaxAP), Is.True);
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(gameObject);
                UnityEngine.Object.DestroyImmediate(stats);
            }
        }

        [Test]
        public void SetAPForTestSetsWalletWithinBoundsAndRejectsInvalidValues()
        {
            var gameObject = new GameObject("AP Wallet Test Setter Test");
            var stats = CreateStats(maxAP: 6);

            try
            {
                var unit = gameObject.AddComponent<TacticalUnit>();
                unit.Initialize(new UnitId(6), TeamId.Player, stats, GridPosition.Zero);

                unit.SetAPForTest(1);

                Assert.That(unit.CurrentAP, Is.EqualTo(1));
                Assert.Throws<ArgumentOutOfRangeException>(() => unit.SetAPForTest(-1));
                Assert.Throws<ArgumentOutOfRangeException>(() => unit.SetAPForTest(stats.MaxAP + 1));
                Assert.That(unit.CurrentAP, Is.EqualTo(1));
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(gameObject);
                UnityEngine.Object.DestroyImmediate(stats);
            }
        }

        private static UnitStatsDefinition CreateStats(int maxAP)
        {
            var stats = ScriptableObject.CreateInstance<UnitStatsDefinition>();
            stats.Configure(
                displayName: "AP Test Unit",
                maxHP: 12,
                maxAP: maxAP,
                movementAnimationSpeed: 4f,
                meleeRange: 1,
                teamColorHint: Color.cyan);
            return stats;
        }
    }
}
