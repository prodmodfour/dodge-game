using System;
using NUnit.Framework;
using ReactionTactics.Units;
using UnityEngine;

namespace ReactionTactics.Tests.EditMode.Units
{
    public sealed class UnitStatsDefinitionTests
    {
        [Test]
        public void DefaultStatsAreUsableForPrototypeUnits()
        {
            var definition = ScriptableObject.CreateInstance<UnitStatsDefinition>();

            try
            {
                Assert.That(definition.DisplayName, Is.EqualTo("Prototype Unit"));
                Assert.That(definition.MaxHP, Is.GreaterThanOrEqualTo(UnitStatsDefinition.MinimumMaxHP));
                Assert.That(definition.MaxAP, Is.GreaterThanOrEqualTo(UnitStatsDefinition.MinimumMaxAP));
                Assert.That(definition.MovementAnimationSpeed, Is.GreaterThan(0f));
                Assert.That(definition.MeleeRange, Is.GreaterThanOrEqualTo(UnitStatsDefinition.MinimumMeleeRange));
                Assert.That(definition.TeamColorHint, Is.EqualTo(Color.white));
                Assert.That(definition.TeamMaterialHint, Is.Null);
            }
            finally
            {
                Destroy(definition);
            }
        }

        [Test]
        public void ConfigureStoresPrototypeUnitStats()
        {
            var definition = ScriptableObject.CreateInstance<UnitStatsDefinition>();
            var colorHint = new Color(0.2f, 0.4f, 0.8f, 1f);

            try
            {
                definition.Configure(
                    displayName: "Knight",
                    maxHP: 18,
                    maxAP: 6,
                    movementAnimationSpeed: 5.5f,
                    meleeRange: 2,
                    teamColorHint: colorHint);

                Assert.That(definition.DisplayName, Is.EqualTo("Knight"));
                Assert.That(definition.MaxHP, Is.EqualTo(18));
                Assert.That(definition.MaxAP, Is.EqualTo(6));
                Assert.That(definition.MovementAnimationSpeed, Is.EqualTo(5.5f));
                Assert.That(definition.MeleeRange, Is.EqualTo(2));
                Assert.That(definition.TeamColorHint, Is.EqualTo(colorHint));
                Assert.That(definition.TeamMaterialHint, Is.Null);
            }
            finally
            {
                Destroy(definition);
            }
        }

        [Test]
        public void ConfigureRejectsInvalidStats()
        {
            var definition = ScriptableObject.CreateInstance<UnitStatsDefinition>();

            try
            {
                Assert.Throws<ArgumentException>(() => definition.Configure(" ", 10, 6, 4f, 1, Color.white));
                Assert.Throws<ArgumentOutOfRangeException>(() => definition.Configure("Unit", 0, 6, 4f, 1, Color.white));
                Assert.Throws<ArgumentOutOfRangeException>(() => definition.Configure("Unit", 10, 0, 4f, 1, Color.white));
                Assert.Throws<ArgumentOutOfRangeException>(() => definition.Configure("Unit", 10, 6, float.NaN, 1, Color.white));
                Assert.Throws<ArgumentOutOfRangeException>(() => definition.Configure("Unit", 10, 6, 4f, 0, Color.white));
                Assert.Throws<ArgumentOutOfRangeException>(() => definition.Configure("Unit", 10, 6, 4f, 1, new Color(float.NaN, 1f, 1f, 1f)));
            }
            finally
            {
                Destroy(definition);
            }
        }

        private static void Destroy(UnitStatsDefinition definition)
        {
            UnityEngine.Object.DestroyImmediate(definition);
        }
    }
}
