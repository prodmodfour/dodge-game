using System;
using System.Collections.Generic;
using Newtonsoft.Json.Linq;
using ReactionTactics.Actions;
using UnityCliConnector;
using UnityEditor;
using UnityEngine;

namespace ReactionTactics.Editor
{
    [UnityCliTool(Name = "rt_create_default_abilities", Description = "Create or update default prototype ability assets.", Group = "reaction-tactics")]
    public static class DefaultAbilityAssetCreatorTool
    {
        private const string AssetRootFolderPath = "Assets/ScriptableObjects";
        private const string AbilityAssetFolderPath = AssetRootFolderPath + "/Abilities";

        private static readonly AbilitySpec[] DefaultAbilities =
        {
            new AbilitySpec(
                "move",
                "Move",
                0,
                AbilityUsage.Both,
                AbilityTiming.Immediate,
                AbilityShape.Self,
                0,
                0,
                0,
                false,
                "Reposition on the grid. Active and reaction movement commands spend AP per path tile instead of using a flat ability cost."),
            new AbilitySpec(
                "melee_slash",
                "Melee Slash",
                3,
                AbilityUsage.Action,
                AbilityTiming.Telegraphed,
                AbilityShape.Melee,
                0,
                0,
                4,
                true,
                "Declare a melee attack, allow reactions, then deal deterministic damage only if the target remains in melee range at resolution."),
            new AbilitySpec(
                "cone_shot",
                "Cone Shot",
                4,
                AbilityUsage.Action,
                AbilityTiming.Telegraphed,
                AbilityShape.Cone,
                4,
                0,
                3,
                true,
                "Threaten a cardinal cone up to range 4. Units can avoid damage by moving out of the declared cone before resolution."),
            new AbilitySpec(
                "fireball",
                "Fireball",
                5,
                AbilityUsage.Action,
                AbilityTiming.Telegraphed,
                AbilityShape.Radius,
                5,
                1,
                4,
                true,
                "Threaten a radius-1 area within range 5. Units can avoid damage by leaving the declared area before resolution."),
            new AbilitySpec(
                "brace",
                "Brace",
                2,
                AbilityUsage.Reaction,
                AbilityTiming.Immediate,
                AbilityShape.Self,
                0,
                0,
                0,
                false,
                "Spend AP during a reaction turn to reduce the next incoming damage from the pending action."),
            new AbilitySpec(
                "pass_reaction",
                "Pass Reaction",
                0,
                AbilityUsage.Reaction,
                AbilityTiming.Immediate,
                AbilityShape.Self,
                0,
                0,
                0,
                false,
                "Skip the current reaction turn without spending AP.")
        };

        public static object HandleCommand(JObject parameters)
        {
            try
            {
                EnsureAssetFolderExists();

                var createdAssets = new List<string>();
                var updatedAssets = new List<string>();
                var abilitySummaries = new List<object>();

                foreach (var spec in DefaultAbilities)
                {
                    var path = spec.AssetPath;
                    var mainAsset = AssetDatabase.LoadMainAssetAtPath(path);
                    if (mainAsset != null && !(mainAsset is AbilityDefinition))
                    {
                        return new ErrorResponse($"Cannot create default ability because '{path}' already contains asset type '{mainAsset.GetType().Name}'.");
                    }

                    var created = mainAsset == null;
                    var definition = mainAsset as AbilityDefinition;
                    if (definition == null)
                    {
                        definition = ScriptableObject.CreateInstance<AbilityDefinition>();
                        definition.name = spec.AssetName;
                        spec.ApplyTo(definition);
                        AssetDatabase.CreateAsset(definition, path);
                        createdAssets.Add(path);
                    }
                    else
                    {
                        definition.name = spec.AssetName;
                        spec.ApplyTo(definition);
                        updatedAssets.Add(path);
                    }

                    EditorUtility.SetDirty(definition);
                    abilitySummaries.Add(spec.ToSummary(path, created));
                }

                AssetDatabase.SaveAssets();
                foreach (var spec in DefaultAbilities)
                {
                    AssetDatabase.ImportAsset(spec.AssetPath, ImportAssetOptions.ForceUpdate);
                }

                AssetDatabase.Refresh();

                var validationErrors = ValidateCreatedAssets(DefaultAbilities);
                if (validationErrors.Count > 0)
                {
                    return new ErrorResponse("Default ability validation failed: " + string.Join("; ", validationErrors));
                }

                return new SuccessResponse("Created or updated default prototype abilities.", new
                {
                    folder = AbilityAssetFolderPath,
                    createdCount = createdAssets.Count,
                    updatedCount = updatedAssets.Count,
                    abilityCount = DefaultAbilities.Length,
                    createdAssets,
                    updatedAssets,
                    abilities = abilitySummaries
                });
            }
            catch (Exception exception)
            {
                return new ErrorResponse($"Failed to create default abilities: {exception.Message}");
            }
        }

        private static void EnsureAssetFolderExists()
        {
            if (!AssetDatabase.IsValidFolder(AssetRootFolderPath))
            {
                AssetDatabase.CreateFolder("Assets", "ScriptableObjects");
            }

            if (!AssetDatabase.IsValidFolder(AbilityAssetFolderPath))
            {
                AssetDatabase.CreateFolder(AssetRootFolderPath, "Abilities");
            }
        }

        private static IReadOnlyList<string> ValidateCreatedAssets(IEnumerable<AbilitySpec> specs)
        {
            var errors = new List<string>();
            foreach (var spec in specs)
            {
                var definition = AssetDatabase.LoadAssetAtPath<AbilityDefinition>(spec.AssetPath);
                if (definition == null)
                {
                    errors.Add($"Missing ability asset at '{spec.AssetPath}'.");
                    continue;
                }

                var definitionResult = definition.ValidateDefinition();
                if (definitionResult.IsFailure)
                {
                    errors.Add($"{spec.DisplayName} definition invalid: {definitionResult.ErrorMessage}");
                }

                if (!string.Equals(definition.AbilityKey, spec.AbilityKey, StringComparison.Ordinal))
                {
                    errors.Add($"{spec.DisplayName} ability key expected '{spec.AbilityKey}' but was '{definition.AbilityKey}'.");
                }

                if (!string.Equals(definition.DisplayName, spec.DisplayName, StringComparison.Ordinal))
                {
                    errors.Add($"{spec.DisplayName} display name mismatch.");
                }

                if (definition.APCost != spec.APCost)
                {
                    errors.Add($"{spec.DisplayName} AP cost expected {spec.APCost} but was {definition.APCost}.");
                }

                if (definition.Usage != spec.Usage)
                {
                    errors.Add($"{spec.DisplayName} usage expected {spec.Usage} but was {definition.Usage}.");
                }

                if (definition.Timing != spec.Timing)
                {
                    errors.Add($"{spec.DisplayName} timing expected {spec.Timing} but was {definition.Timing}.");
                }

                if (definition.Shape != spec.Shape)
                {
                    errors.Add($"{spec.DisplayName} shape expected {spec.Shape} but was {definition.Shape}.");
                }

                if (definition.Range != spec.Range)
                {
                    errors.Add($"{spec.DisplayName} range expected {spec.Range} but was {definition.Range}.");
                }

                if (definition.Radius != spec.Radius)
                {
                    errors.Add($"{spec.DisplayName} radius expected {spec.Radius} but was {definition.Radius}.");
                }

                if (definition.Damage != spec.Damage)
                {
                    errors.Add($"{spec.DisplayName} damage expected {spec.Damage} but was {definition.Damage}.");
                }

                if (definition.TriggersReactions != spec.TriggersReactions)
                {
                    errors.Add($"{spec.DisplayName} triggers reactions expected {spec.TriggersReactions} but was {definition.TriggersReactions}.");
                }

                if (!string.Equals(definition.Description, spec.Description, StringComparison.Ordinal))
                {
                    errors.Add($"{spec.DisplayName} description mismatch.");
                }
            }

            return errors;
        }

        private sealed class AbilitySpec
        {
            public AbilitySpec(
                string abilityKey,
                string displayName,
                int apCost,
                AbilityUsage usage,
                AbilityTiming timing,
                AbilityShape shape,
                int range,
                int radius,
                int damage,
                bool triggersReactions,
                string description)
            {
                AbilityKey = abilityKey;
                DisplayName = displayName;
                APCost = apCost;
                Usage = usage;
                Timing = timing;
                Shape = shape;
                Range = range;
                Radius = radius;
                Damage = damage;
                TriggersReactions = triggersReactions;
                Description = description;
            }

            public string AbilityKey { get; }

            public string DisplayName { get; }

            public int APCost { get; }

            public AbilityUsage Usage { get; }

            public AbilityTiming Timing { get; }

            public AbilityShape Shape { get; }

            public int Range { get; }

            public int Radius { get; }

            public int Damage { get; }

            public bool TriggersReactions { get; }

            public string Description { get; }

            public string AssetName
            {
                get { return DisplayName.Replace(" ", string.Empty); }
            }

            public string AssetPath
            {
                get { return AbilityAssetFolderPath + "/" + AssetName + ".asset"; }
            }

            public void ApplyTo(AbilityDefinition definition)
            {
                definition.Configure(
                    AbilityKey,
                    DisplayName,
                    APCost,
                    Usage,
                    Timing,
                    Shape,
                    Range,
                    Radius,
                    Damage,
                    TriggersReactions,
                    Description);
            }

            public object ToSummary(string path, bool created)
            {
                return new
                {
                    abilityKey = AbilityKey,
                    displayName = DisplayName,
                    path,
                    created,
                    apCost = APCost,
                    usage = Usage.ToString(),
                    timing = Timing.ToString(),
                    shape = Shape.ToString(),
                    range = Range,
                    radius = Radius,
                    damage = Damage,
                    triggersReactions = TriggersReactions
                };
            }
        }
    }
}
