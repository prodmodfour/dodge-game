using System;
using System.Collections.Generic;
using NUnit.Framework;
using ReactionTactics.Actions;
using UnityEditor;
using UnityEngine;

namespace ReactionTactics.Tests.EditMode.Actions
{
    public sealed class AbilityDefinitionTests
    {
        [Test]
        public void DefaultAbilityDefinitionIsValidSelfAction()
        {
            var definition = ScriptableObject.CreateInstance<AbilityDefinition>();

            try
            {
                Assert.That(definition.ValidateDefinition().IsSuccess, Is.True);
                Assert.That(definition.AbilityKey, Is.EqualTo("ability"));
                Assert.That(definition.DisplayName, Is.EqualTo("Ability"));
                Assert.That(definition.APCost, Is.EqualTo(0));
                Assert.That(definition.Usage, Is.EqualTo(AbilityUsage.Action));
                Assert.That(definition.Timing, Is.EqualTo(AbilityTiming.Immediate));
                Assert.That(definition.Shape, Is.EqualTo(AbilityShape.Self));
                Assert.That(definition.CanBeUsedAsAction, Is.True);
                Assert.That(definition.CanBeUsedAsReaction, Is.False);
                Assert.That(definition.TriggersReactions, Is.False);
            }
            finally
            {
                Destroy(definition);
            }
        }

        [Test]
        public void ConfigureStoresPrototypeAbilityData()
        {
            var definition = ScriptableObject.CreateInstance<AbilityDefinition>();

            try
            {
                definition.Configure(
                    abilityKey: "fireball",
                    displayName: "Fireball",
                    apCost: 5,
                    usage: AbilityUsage.Action,
                    timing: AbilityTiming.Telegraphed,
                    shape: AbilityShape.Radius,
                    range: 5,
                    radius: 2,
                    damage: 4,
                    triggersReactions: true,
                    description: "Telegraphed area attack.");

                Assert.That(definition.ValidateDefinition().IsSuccess, Is.True);
                Assert.That(definition.AbilityKey, Is.EqualTo("fireball"));
                Assert.That(definition.DisplayName, Is.EqualTo("Fireball"));
                Assert.That(definition.APCost, Is.EqualTo(5));
                Assert.That(definition.Usage, Is.EqualTo(AbilityUsage.Action));
                Assert.That(definition.Timing, Is.EqualTo(AbilityTiming.Telegraphed));
                Assert.That(definition.Shape, Is.EqualTo(AbilityShape.Radius));
                Assert.That(definition.Range, Is.EqualTo(5));
                Assert.That(definition.Radius, Is.EqualTo(2));
                Assert.That(definition.Damage, Is.EqualTo(4));
                Assert.That(definition.TriggersReactions, Is.True);
                Assert.That(definition.IsTelegraphed, Is.True);
                Assert.That(definition.Description, Is.EqualTo("Telegraphed area attack."));
            }
            finally
            {
                Destroy(definition);
            }
        }

        [Test]
        public void AbilityDefinitionsCanRepresentPrototypeAbilitySet()
        {
            var definitions = new List<AbilityDefinition>();

            try
            {
                definitions.Add(CreateConfigured("move", "Move", 0, AbilityUsage.Both, AbilityTiming.Immediate, AbilityShape.Self, 0, 0, 0, false));
                definitions.Add(CreateConfigured("melee_slash", "Melee Slash", 3, AbilityUsage.Action, AbilityTiming.Telegraphed, AbilityShape.Melee, 0, 0, 4, true));
                definitions.Add(CreateConfigured("cone_shot", "Cone Shot", 4, AbilityUsage.Action, AbilityTiming.Telegraphed, AbilityShape.Cone, 4, 0, 3, true));
                definitions.Add(CreateConfigured("fireball", "Fireball", 5, AbilityUsage.Action, AbilityTiming.Telegraphed, AbilityShape.Radius, 5, 1, 4, true));
                definitions.Add(CreateConfigured("brace", "Brace", 2, AbilityUsage.Reaction, AbilityTiming.Immediate, AbilityShape.Self, 0, 0, 0, false));
                definitions.Add(CreateConfigured("pass_reaction", "Pass Reaction", 0, AbilityUsage.Reaction, AbilityTiming.Immediate, AbilityShape.Self, 0, 0, 0, false));

                foreach (var definition in definitions)
                {
                    Assert.That(definition.ValidateDefinition().IsSuccess, Is.True, definition.DisplayName);
                }

                Assert.That(definitions[0].CanBeUsedAsAction, Is.True);
                Assert.That(definitions[0].CanBeUsedAsReaction, Is.True);
                Assert.That(definitions[1].TriggersReactions, Is.True);
                Assert.That(definitions[4].CanBeUsedAsReaction, Is.True);
                Assert.That(definitions[5].APCost, Is.EqualTo(0));
            }
            finally
            {
                foreach (var definition in definitions)
                {
                    Destroy(definition);
                }
            }
        }

        [Test]
        public void ValidateDefinitionReportsMissingAndInvalidData()
        {
            var definition = ScriptableObject.CreateInstance<AbilityDefinition>();

            try
            {
                var serializedObject = new SerializedObject(definition);
                serializedObject.FindProperty("abilityKey").stringValue = " ";
                serializedObject.FindProperty("displayName").stringValue = " ";
                serializedObject.FindProperty("usage").intValue = (int)AbilityUsage.None;
                serializedObject.FindProperty("shape").intValue = (int)AbilityShape.Radius;
                serializedObject.ApplyModifiedPropertiesWithoutUndo();

                var result = definition.ValidateDefinition();
                var errors = definition.CollectValidationErrors();

                Assert.That(result.IsFailure, Is.True);
                Assert.That(errors, Has.Some.Contains("ability key"));
                Assert.That(errors, Has.Some.Contains("display name"));
                Assert.That(errors, Has.Some.Contains("usage"));
                Assert.That(errors, Has.Some.Contains("range"));
                Assert.That(errors, Has.Some.Contains("radius"));
            }
            finally
            {
                Destroy(definition);
            }
        }

        [Test]
        public void ConfigureRejectsInvalidAbilityData()
        {
            var definition = ScriptableObject.CreateInstance<AbilityDefinition>();

            try
            {
                Assert.Throws<ArgumentException>(() => definition.Configure(" ", "Ability", 0, AbilityUsage.Action, AbilityTiming.Immediate, AbilityShape.Self, 0, 0, 0, false));
                Assert.Throws<ArgumentException>(() => definition.Configure("ability", " ", 0, AbilityUsage.Action, AbilityTiming.Immediate, AbilityShape.Self, 0, 0, 0, false));
                Assert.Throws<ArgumentException>(() => definition.Configure("ability", "Ability", -1, AbilityUsage.Action, AbilityTiming.Immediate, AbilityShape.Self, 0, 0, 0, false));
                Assert.Throws<ArgumentException>(() => definition.Configure("ability", "Ability", 0, AbilityUsage.None, AbilityTiming.Immediate, AbilityShape.Self, 0, 0, 0, false));
                Assert.Throws<ArgumentException>(() => definition.Configure("cone", "Cone", 4, AbilityUsage.Action, AbilityTiming.Telegraphed, AbilityShape.Cone, 0, 0, 3, true));
                Assert.Throws<ArgumentException>(() => definition.Configure("aoe", "AoE", 5, AbilityUsage.Action, AbilityTiming.Telegraphed, AbilityShape.Radius, 5, 0, 4, true));
            }
            finally
            {
                Destroy(definition);
            }
        }

        private static AbilityDefinition CreateConfigured(
            string abilityKey,
            string displayName,
            int apCost,
            AbilityUsage usage,
            AbilityTiming timing,
            AbilityShape shape,
            int range,
            int radius,
            int damage,
            bool triggersReactions)
        {
            var definition = ScriptableObject.CreateInstance<AbilityDefinition>();
            definition.Configure(
                abilityKey,
                displayName,
                apCost,
                usage,
                timing,
                shape,
                range,
                radius,
                damage,
                triggersReactions);
            return definition;
        }

        private static void Destroy(AbilityDefinition definition)
        {
            if (definition != null)
            {
                UnityEngine.Object.DestroyImmediate(definition);
            }
        }
    }
}
