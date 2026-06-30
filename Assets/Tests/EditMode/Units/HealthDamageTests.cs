using NUnit.Framework;
using ReactionTactics.Grid;
using ReactionTactics.Units;
using UnityEngine;

namespace ReactionTactics.Tests.EditMode.Units
{
    public sealed class HealthDamageTests
    {
        [Test]
        public void ApplyDamageReducesHitPointsWithoutKilling()
        {
            var gameObject = new GameObject("Health Damage Nonlethal Test");
            var stats = CreateStats(maxHP: 12);

            try
            {
                var unit = gameObject.AddComponent<TacticalUnit>();
                unit.Initialize(new UnitId(1), TeamId.Player, stats, GridPosition.Zero);
                var deathEvents = 0;
                unit.Died += (_, __) => deathEvents += 1;

                var result = unit.ApplyDamage(5, DamageSource.Environmental("Trap"));

                Assert.That(result.IsSuccess, Is.True);
                Assert.That(unit.CurrentHP, Is.EqualTo(7));
                Assert.That(unit.IsAlive, Is.True);
                Assert.That(unit.IsDead, Is.False);
                Assert.That(deathEvents, Is.EqualTo(0));
            }
            finally
            {
                Object.DestroyImmediate(gameObject);
                Object.DestroyImmediate(stats);
            }
        }

        [Test]
        public void ApplyDamageClampsHitPointsAtZeroMarksDeadAndRaisesDeathEventOnce()
        {
            var gameObject = new GameObject("Health Damage Lethal Test");
            var stats = CreateStats(maxHP: 10);

            try
            {
                var unit = gameObject.AddComponent<TacticalUnit>();
                unit.Initialize(new UnitId(2), TeamId.Enemy, stats, GridPosition.Zero);
                var source = DamageSource.FromUnit(new UnitId(99), "Melee Slash");
                var deathEvents = 0;
                TacticalUnit deadUnit = null;
                var observedSource = DamageSource.Unspecified;
                unit.Died += (dead, damageSource) =>
                {
                    deathEvents += 1;
                    deadUnit = dead;
                    observedSource = damageSource;
                };

                var result = unit.ApplyDamage(99, source);
                var repeatedResult = unit.ApplyDamage(1, source);

                Assert.That(result.IsSuccess, Is.True);
                Assert.That(repeatedResult.IsFailure, Is.True);
                Assert.That(unit.CurrentHP, Is.EqualTo(0));
                Assert.That(unit.IsAlive, Is.False);
                Assert.That(unit.IsDead, Is.True);
                Assert.That(deathEvents, Is.EqualTo(1));
                Assert.That(deadUnit, Is.SameAs(unit));
                Assert.That(observedSource, Is.EqualTo(source));
            }
            finally
            {
                Object.DestroyImmediate(gameObject);
                Object.DestroyImmediate(stats);
            }
        }

        [Test]
        public void ApplyDamageAllowsZeroDamageWithoutChangingState()
        {
            var gameObject = new GameObject("Health Damage Zero Test");
            var stats = CreateStats(maxHP: 8);

            try
            {
                var unit = gameObject.AddComponent<TacticalUnit>();
                unit.Initialize(new UnitId(3), TeamId.Player, stats, GridPosition.Zero);
                var deathEvents = 0;
                unit.Died += (_, __) => deathEvents += 1;

                var result = unit.ApplyDamage(0, DamageSource.Unspecified);

                Assert.That(result.IsSuccess, Is.True);
                Assert.That(unit.CurrentHP, Is.EqualTo(stats.MaxHP));
                Assert.That(unit.IsAlive, Is.True);
                Assert.That(deathEvents, Is.EqualTo(0));
            }
            finally
            {
                Object.DestroyImmediate(gameObject);
                Object.DestroyImmediate(stats);
            }
        }

        [Test]
        public void ApplyDamageRejectsNegativeAmountsWithoutChangingState()
        {
            var gameObject = new GameObject("Health Damage Negative Test");
            var stats = CreateStats(maxHP: 9);

            try
            {
                var unit = gameObject.AddComponent<TacticalUnit>();
                unit.Initialize(new UnitId(4), TeamId.Player, stats, GridPosition.Zero);
                var deathEvents = 0;
                unit.Died += (_, __) => deathEvents += 1;

                var result = unit.ApplyDamage(-1, DamageSource.Environmental("Invalid test damage"));

                Assert.That(result.IsFailure, Is.True);
                Assert.That(result.ErrorMessage, Does.Contain("cannot be negative"));
                Assert.That(unit.CurrentHP, Is.EqualTo(stats.MaxHP));
                Assert.That(unit.IsAlive, Is.True);
                Assert.That(deathEvents, Is.EqualTo(0));
            }
            finally
            {
                Object.DestroyImmediate(gameObject);
                Object.DestroyImmediate(stats);
            }
        }

        [Test]
        public void DamageSourceNormalizesDescriptionsAndRejectsUnassignedUnitSources()
        {
            var source = DamageSource.FromUnit(new UnitId(5), "  Cone Shot  ");
            var environmental = DamageSource.Environmental("  Falling rock  ");

            Assert.That(source.SourceUnitId, Is.EqualTo(new UnitId(5)));
            Assert.That(source.HasSourceUnit, Is.True);
            Assert.That(source.Description, Is.EqualTo("Cone Shot"));
            Assert.That(source.ToString(), Is.EqualTo("Cone Shot from Unit#5"));
            Assert.That(environmental.HasSourceUnit, Is.False);
            Assert.That(environmental.Description, Is.EqualTo("Falling rock"));
            Assert.That(DamageSource.Unspecified.Description, Is.EqualTo("Unspecified damage"));
            Assert.That(() => DamageSource.FromUnit(UnitId.None, "Invalid"), Throws.ArgumentException);
        }

        private static UnitStatsDefinition CreateStats(int maxHP)
        {
            var stats = ScriptableObject.CreateInstance<UnitStatsDefinition>();
            stats.Configure(
                displayName: "Health Test Unit",
                maxHP: maxHP,
                maxAP: 6,
                movementAnimationSpeed: 4f,
                meleeRange: 1,
                teamColorHint: Color.magenta);
            return stats;
        }
    }
}
