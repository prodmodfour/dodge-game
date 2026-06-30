using System;
using System.Collections.Generic;
using NUnit.Framework;
using ReactionTactics.AI;
using ReactionTactics.Grid;
using ReactionTactics.Units;
using UnityEngine;

namespace ReactionTactics.Tests.EditMode.AI
{
    public sealed class AiTargetSelectionTests
    {
        [Test]
        public void SelectNearestHostileTargetChoosesNearestLivingHostile()
        {
            using (var fixture = new Fixture())
            {
                var actor = fixture.CreateUnit("Enemy Actor", 10, TeamId.Enemy, new GridPosition(0, 0, 0));
                var nearbyFriendly = fixture.CreateUnit("Nearby Friendly", 11, TeamId.Enemy, new GridPosition(1, 0, 0));
                var deadHostile = fixture.CreateUnit("Dead Hostile", 12, TeamId.Player, new GridPosition(1, 0, 0));
                Assert.That(deadHostile.ApplyDamage(99, DamageSource.Environmental("Target selection test")).IsSuccess, Is.True);
                var farHostile = fixture.CreateUnit("Far Hostile", 13, TeamId.Player, new GridPosition(4, 0, 0));
                var nearestHostile = fixture.CreateUnit("Nearest Hostile", 14, TeamId.Player, new GridPosition(2, 0, 0));

                var candidates = new[] { farHostile, nearbyFriendly, deadHostile, nearestHostile };
                var found = fixture.Controller.TrySelectNearestHostileTarget(actor, candidates, out var selected);

                Assert.That(found, Is.True);
                Assert.That(selected, Is.SameAs(nearestHostile));
                Assert.That(fixture.Controller.SelectNearestHostileTarget(actor, candidates), Is.SameAs(nearestHostile));
            }
        }

        [Test]
        public void SelectNearestHostileTargetUsesTacticalDistanceIncludingHeight()
        {
            using (var fixture = new Fixture())
            {
                var actor = fixture.CreateUnit("Enemy Actor", 20, TeamId.Enemy, new GridPosition(0, 0, 0));
                var verticallyFarHostile = fixture.CreateUnit("Vertical Hostile", 21, TeamId.Player, new GridPosition(1, 2, 0));
                var tacticallyNearHostile = fixture.CreateUnit("Flat Hostile", 22, TeamId.Player, new GridPosition(2, 0, 0));

                var selected = fixture.Controller.SelectNearestHostileTarget(
                    actor,
                    new[] { verticallyFarHostile, tacticallyNearHostile });

                Assert.That(selected, Is.SameAs(tacticallyNearHostile));
                Assert.That(fixture.Controller.GetTargetSelectionDistance(actor, verticallyFarHostile), Is.EqualTo(3));
                Assert.That(fixture.Controller.GetTargetSelectionDistance(actor, tacticallyNearHostile), Is.EqualTo(2));
            }
        }

        [Test]
        public void SelectNearestHostileTargetTieBreaksByLowestHitPoints()
        {
            using (var fixture = new Fixture())
            {
                var actor = fixture.CreateUnit("Enemy Actor", 30, TeamId.Enemy, GridPosition.Zero);
                var healthyHostile = fixture.CreateUnit("Healthy Hostile", 31, TeamId.Player, new GridPosition(1, 0, 1));
                var woundedHostile = fixture.CreateUnit("Wounded Hostile", 32, TeamId.Player, new GridPosition(-1, 0, 1));
                Assert.That(woundedHostile.ApplyDamage(5, DamageSource.Environmental("Target selection test")).IsSuccess, Is.True);

                var selected = fixture.Controller.SelectNearestHostileTarget(
                    actor,
                    new[] { healthyHostile, woundedHostile });

                Assert.That(healthyHostile.CurrentHP, Is.GreaterThan(woundedHostile.CurrentHP));
                Assert.That(selected, Is.SameAs(woundedHostile));
            }
        }

        [Test]
        public void SelectNearestHostileTargetTieBreaksEqualHitPointsByUnitId()
        {
            using (var fixture = new Fixture())
            {
                var actor = fixture.CreateUnit("Enemy Actor", 40, TeamId.Enemy, GridPosition.Zero);
                var higherIdHostile = fixture.CreateUnit("Higher Id Hostile", 42, TeamId.Player, new GridPosition(1, 0, 1));
                var lowerIdHostile = fixture.CreateUnit("Lower Id Hostile", 41, TeamId.Player, new GridPosition(-1, 0, 1));

                var selected = fixture.Controller.SelectNearestHostileTarget(
                    actor,
                    new[] { higherIdHostile, lowerIdHostile });

                Assert.That(higherIdHostile.CurrentHP, Is.EqualTo(lowerIdHostile.CurrentHP));
                Assert.That(selected, Is.SameAs(lowerIdHostile));
            }
        }

        [Test]
        public void TrySelectNearestHostileTargetReturnsFalseWhenNoLivingHostileExists()
        {
            using (var fixture = new Fixture())
            {
                var actor = fixture.CreateUnit("Enemy Actor", 50, TeamId.Enemy, GridPosition.Zero);
                var friendly = fixture.CreateUnit("Friendly", 51, TeamId.Enemy, new GridPosition(1, 0, 0));
                var deadHostile = fixture.CreateUnit("Dead Hostile", 52, TeamId.Player, new GridPosition(2, 0, 0));
                Assert.That(deadHostile.ApplyDamage(99, DamageSource.Environmental("Target selection test")).IsSuccess, Is.True);

                var found = fixture.Controller.TrySelectNearestHostileTarget(
                    actor,
                    new TacticalUnit[] { null, friendly, deadHostile },
                    out var selected);

                Assert.That(found, Is.False);
                Assert.That(selected, Is.Null);
            }
        }

        [Test]
        public void SelectNearestHostileTargetRejectsInvalidInputs()
        {
            using (var fixture = new Fixture())
            {
                var actor = fixture.CreateUnit("Enemy Actor", 60, TeamId.Enemy, GridPosition.Zero);

                Assert.That(
                    () => fixture.Controller.SelectNearestHostileTarget(null, Array.Empty<TacticalUnit>()),
                    Throws.ArgumentNullException);
                Assert.That(
                    () => fixture.Controller.SelectNearestHostileTarget(actor, null),
                    Throws.ArgumentNullException);
                Assert.That(
                    () => fixture.Controller.TargetSelectionVerticalWeight = -1,
                    Throws.TypeOf<ArgumentOutOfRangeException>());
            }
        }

        private sealed class Fixture : IDisposable
        {
            private readonly List<GameObject> gameObjects = new List<GameObject>();
            private readonly UnitStatsDefinition stats;

            public Fixture()
            {
                stats = ScriptableObject.CreateInstance<UnitStatsDefinition>();
                stats.Configure(
                    displayName: "AI Target Selection Test Unit",
                    maxHP: 10,
                    maxAP: 6,
                    movementAnimationSpeed: 4f,
                    meleeRange: 1,
                    teamColorHint: Color.white);

                var controllerObject = CreateGameObject("AI Target Selection Controller");
                Controller = controllerObject.AddComponent<AiController>();
                Controller.LogDecisions = false;
            }

            public AiController Controller { get; }

            public TacticalUnit CreateUnit(string name, int unitId, TeamId team, GridPosition position)
            {
                var gameObject = CreateGameObject(name);
                var unit = gameObject.AddComponent<TacticalUnit>();
                unit.Initialize(new UnitId(unitId), team, stats, position);
                return unit;
            }

            public void Dispose()
            {
                for (var i = gameObjects.Count - 1; i >= 0; i -= 1)
                {
                    UnityEngine.Object.DestroyImmediate(gameObjects[i]);
                }

                UnityEngine.Object.DestroyImmediate(stats);
            }

            private GameObject CreateGameObject(string name)
            {
                var gameObject = new GameObject(name);
                gameObjects.Add(gameObject);
                return gameObject;
            }
        }
    }
}
