using System;
using System.Collections.Generic;
using Newtonsoft.Json.Linq;
using ReactionTactics.Units;
using UnityCliConnector;
using UnityEditor;
using UnityEngine;

namespace ReactionTactics.Editor
{
    [UnityCliTool(Name = "rt_create_default_units", Description = "Create or update default prototype unit stat assets.", Group = "reaction-tactics")]
    public static class DefaultUnitStatsAssetCreatorTool
    {
        private const string AssetRootFolderPath = "Assets/ScriptableObjects";
        private const string UnitAssetFolderPath = AssetRootFolderPath + "/Units";
        private const float DefaultMovementAnimationSpeed = UnitStatsDefinition.DefaultMovementAnimationSpeed;
        private const int DefaultMeleeRange = UnitStatsDefinition.MinimumMeleeRange;

        private static readonly UnitStatsSpec[] DefaultUnitStats =
        {
            new UnitStatsSpec("Knight", 16, 6, 3.75f, DefaultMeleeRange, new Color(0.28f, 0.43f, 0.95f, 1f)),
            new UnitStatsSpec("Rogue", 11, 7, 5.00f, DefaultMeleeRange, new Color(0.22f, 0.78f, 0.42f, 1f)),
            new UnitStatsSpec("Archer", 10, 6, 4.25f, DefaultMeleeRange, new Color(0.86f, 0.66f, 0.24f, 1f)),
            new UnitStatsSpec("Mage", 8, 6, DefaultMovementAnimationSpeed, DefaultMeleeRange, new Color(0.62f, 0.36f, 0.95f, 1f)),
            new UnitStatsSpec("Goblin", 7, 6, 4.50f, DefaultMeleeRange, new Color(0.72f, 0.24f, 0.18f, 1f)),
            new UnitStatsSpec("Shaman", 8, 6, DefaultMovementAnimationSpeed, DefaultMeleeRange, new Color(0.20f, 0.70f, 0.68f, 1f))
        };

        public static object HandleCommand(JObject parameters)
        {
            try
            {
                EnsureAssetFolderExists();

                var createdAssets = new List<string>();
                var updatedAssets = new List<string>();
                var unitSummaries = new List<object>();

                foreach (var spec in DefaultUnitStats)
                {
                    var path = spec.AssetPath;
                    var mainAsset = AssetDatabase.LoadMainAssetAtPath(path);
                    if (mainAsset != null && !(mainAsset is UnitStatsDefinition))
                    {
                        return new ErrorResponse($"Cannot create default unit stats because '{path}' already contains asset type '{mainAsset.GetType().Name}'.");
                    }

                    var created = mainAsset == null;
                    var definition = mainAsset as UnitStatsDefinition;
                    if (definition == null)
                    {
                        definition = ScriptableObject.CreateInstance<UnitStatsDefinition>();
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
                    unitSummaries.Add(spec.ToSummary(path, created));
                }

                AssetDatabase.SaveAssets();
                foreach (var spec in DefaultUnitStats)
                {
                    AssetDatabase.ImportAsset(spec.AssetPath, ImportAssetOptions.ForceUpdate);
                }

                AssetDatabase.Refresh();

                var validationErrors = ValidateCreatedAssets(DefaultUnitStats);
                if (validationErrors.Count > 0)
                {
                    return new ErrorResponse("Default unit stats validation failed: " + string.Join("; ", validationErrors));
                }

                return new SuccessResponse("Created or updated default prototype unit stats.", new
                {
                    folder = UnitAssetFolderPath,
                    createdCount = createdAssets.Count,
                    updatedCount = updatedAssets.Count,
                    unitCount = DefaultUnitStats.Length,
                    createdAssets,
                    updatedAssets,
                    units = unitSummaries
                });
            }
            catch (Exception exception)
            {
                return new ErrorResponse($"Failed to create default unit stats: {exception.Message}");
            }
        }

        private static void EnsureAssetFolderExists()
        {
            if (!AssetDatabase.IsValidFolder(AssetRootFolderPath))
            {
                AssetDatabase.CreateFolder("Assets", "ScriptableObjects");
            }

            if (!AssetDatabase.IsValidFolder(UnitAssetFolderPath))
            {
                AssetDatabase.CreateFolder(AssetRootFolderPath, "Units");
            }
        }

        private static IReadOnlyList<string> ValidateCreatedAssets(IEnumerable<UnitStatsSpec> specs)
        {
            var errors = new List<string>();
            foreach (var spec in specs)
            {
                var definition = AssetDatabase.LoadAssetAtPath<UnitStatsDefinition>(spec.AssetPath);
                if (definition == null)
                {
                    errors.Add($"Missing unit stats asset at '{spec.AssetPath}'.");
                    continue;
                }

                if (!string.Equals(definition.DisplayName, spec.DisplayName, StringComparison.Ordinal))
                {
                    errors.Add($"{spec.DisplayName} display name mismatch.");
                }

                if (definition.MaxHP != spec.MaxHP)
                {
                    errors.Add($"{spec.DisplayName} MaxHP expected {spec.MaxHP} but was {definition.MaxHP}.");
                }

                if (definition.MaxAP != spec.MaxAP)
                {
                    errors.Add($"{spec.DisplayName} MaxAP expected {spec.MaxAP} but was {definition.MaxAP}.");
                }

                if (Math.Abs(definition.MovementAnimationSpeed - spec.MovementAnimationSpeed) > 0.001f)
                {
                    errors.Add($"{spec.DisplayName} movement animation speed expected {spec.MovementAnimationSpeed} but was {definition.MovementAnimationSpeed}.");
                }

                if (definition.MeleeRange != spec.MeleeRange)
                {
                    errors.Add($"{spec.DisplayName} melee range expected {spec.MeleeRange} but was {definition.MeleeRange}.");
                }
            }

            return errors;
        }

        private sealed class UnitStatsSpec
        {
            public UnitStatsSpec(
                string displayName,
                int maxHP,
                int maxAP,
                float movementAnimationSpeed,
                int meleeRange,
                Color colorHint)
            {
                DisplayName = displayName;
                MaxHP = maxHP;
                MaxAP = maxAP;
                MovementAnimationSpeed = movementAnimationSpeed;
                MeleeRange = meleeRange;
                ColorHint = colorHint;
            }

            public string DisplayName { get; }

            public int MaxHP { get; }

            public int MaxAP { get; }

            public float MovementAnimationSpeed { get; }

            public int MeleeRange { get; }

            public Color ColorHint { get; }

            public string AssetName
            {
                get { return DisplayName.Replace(" ", string.Empty); }
            }

            public string AssetPath
            {
                get { return UnitAssetFolderPath + "/" + AssetName + ".asset"; }
            }

            public void ApplyTo(UnitStatsDefinition definition)
            {
                definition.Configure(
                    DisplayName,
                    MaxHP,
                    MaxAP,
                    MovementAnimationSpeed,
                    MeleeRange,
                    ColorHint);
            }

            public object ToSummary(string path, bool created)
            {
                return new
                {
                    displayName = DisplayName,
                    path,
                    created,
                    maxHP = MaxHP,
                    maxAP = MaxAP,
                    movementAnimationSpeed = MovementAnimationSpeed,
                    meleeRange = MeleeRange,
                    color = "#" + ColorUtility.ToHtmlStringRGBA(ColorHint)
                };
            }
        }
    }
}
