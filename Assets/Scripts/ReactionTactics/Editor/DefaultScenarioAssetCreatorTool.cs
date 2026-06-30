using System;
using System.Collections.Generic;
using System.Linq;
using Newtonsoft.Json.Linq;
using ReactionTactics.Actions;
using ReactionTactics.Grid;
using ReactionTactics.Scenarios;
using ReactionTactics.Units;
using UnityCliConnector;
using UnityEditor;
using UnityEngine;

namespace ReactionTactics.Editor
{
    [UnityCliTool(Name = "rt_create_default_scenario", Description = "Create or update the default prototype skirmish scenario asset.", Group = "reaction-tactics")]
    public static class DefaultScenarioAssetCreatorTool
    {
        private const string AssetRootFolderPath = "Assets/ScriptableObjects";
        private const string ScenarioAssetFolderPath = AssetRootFolderPath + "/Scenarios";
        private const string ScenarioAssetPath = ScenarioAssetFolderPath + "/DefaultSkirmish.asset";
        private const string MapAssetPath = AssetRootFolderPath + "/DefaultPrototypeMap.asset";
        private const string UnitAssetFolderPath = AssetRootFolderPath + "/Units";
        private const string AbilityAssetFolderPath = AssetRootFolderPath + "/Abilities";
        private const int ExpectedMapWidth = 8;
        private const int ExpectedMapDepth = 8;
        private const int ExpectedTeamSize = 3;

        private static readonly ScenarioUnitSpec[] DefaultSkirmishUnits =
        {
            new ScenarioUnitSpec("Knight", TeamId.Player, new GridPosition(0, 0, 0)),
            new ScenarioUnitSpec("Archer", TeamId.Player, new GridPosition(0, 0, 1)),
            new ScenarioUnitSpec("Mage", TeamId.Player, new GridPosition(1, 0, 0)),
            new ScenarioUnitSpec("Goblin", TeamId.Enemy, new GridPosition(7, 0, 7)),
            new ScenarioUnitSpec("Shaman", TeamId.Enemy, new GridPosition(7, 0, 6)),
            new ScenarioUnitSpec("Archer", TeamId.Enemy, new GridPosition(6, 0, 7))
        };

        private static readonly AbilityAssetSpec[] RequiredAbilityAssets =
        {
            new AbilityAssetSpec("move", "Move"),
            new AbilityAssetSpec("melee_slash", "MeleeSlash"),
            new AbilityAssetSpec("cone_shot", "ConeShot"),
            new AbilityAssetSpec("fireball", "Fireball"),
            new AbilityAssetSpec("brace", "Brace"),
            new AbilityAssetSpec("pass_reaction", "PassReaction")
        };

        private static readonly UnitAbilityLoadoutSpec[] DefaultAbilityLoadouts =
        {
            new UnitAbilityLoadoutSpec("Knight", "move", "melee_slash", "brace", "pass_reaction"),
            new UnitAbilityLoadoutSpec("Rogue", "move", "melee_slash", "brace", "pass_reaction"),
            new UnitAbilityLoadoutSpec("Archer", "move", "cone_shot", "melee_slash", "brace", "pass_reaction"),
            new UnitAbilityLoadoutSpec("Mage", "move", "fireball", "melee_slash", "brace", "pass_reaction"),
            new UnitAbilityLoadoutSpec("Goblin", "move", "melee_slash", "brace", "pass_reaction"),
            new UnitAbilityLoadoutSpec("Shaman", "move", "fireball", "melee_slash", "brace", "pass_reaction")
        };

        public static object HandleCommand(JObject parameters)
        {
            try
            {
                EnsureAssetFolderExists();

                var mapDefinition = LoadRequiredMap();
                var statsByName = LoadRequiredUnitStats(DefaultSkirmishUnits);
                var abilitiesByKey = LoadRequiredAbilities(RequiredAbilityAssets);
                var loadoutsByStatsName = BuildRequiredLoadouts(DefaultAbilityLoadouts, abilitiesByKey);
                var entries = BuildScenarioEntries(DefaultSkirmishUnits, statsByName, loadoutsByStatsName);

                var compositionErrors = ValidateDefaultComposition(mapDefinition, entries);
                if (compositionErrors.Count > 0)
                {
                    return new ErrorResponse("Default scenario composition validation failed: " + string.Join("; ", compositionErrors));
                }

                var mainAsset = AssetDatabase.LoadMainAssetAtPath(ScenarioAssetPath);
                if (mainAsset != null && !(mainAsset is ScenarioDefinition))
                {
                    return new ErrorResponse($"Cannot create default scenario because '{ScenarioAssetPath}' already contains asset type '{mainAsset.GetType().Name}'.");
                }

                var created = mainAsset == null;
                var scenario = mainAsset as ScenarioDefinition;
                if (scenario == null)
                {
                    scenario = ScriptableObject.CreateInstance<ScenarioDefinition>();
                    scenario.name = "DefaultSkirmish";
                    scenario.Configure(mapDefinition, entries);
                    AssetDatabase.CreateAsset(scenario, ScenarioAssetPath);
                }
                else
                {
                    scenario.name = "DefaultSkirmish";
                    scenario.Configure(mapDefinition, entries);
                }

                EditorUtility.SetDirty(scenario);
                AssetDatabase.SaveAssets();
                AssetDatabase.ImportAsset(ScenarioAssetPath, ImportAssetOptions.ForceUpdate);
                AssetDatabase.Refresh();

                scenario = AssetDatabase.LoadAssetAtPath<ScenarioDefinition>(ScenarioAssetPath);
                if (scenario == null)
                {
                    return new ErrorResponse($"Default scenario asset was not available at '{ScenarioAssetPath}' after save.");
                }

                var scenarioResult = scenario.ValidateScenario();
                if (scenarioResult.IsFailure)
                {
                    return new ErrorResponse($"Default scenario validation failed: {scenarioResult.ErrorMessage}");
                }

                return new SuccessResponse(created ? "Created default skirmish scenario." : "Updated default skirmish scenario.", new
                {
                    path = ScenarioAssetPath,
                    created,
                    map = MapAssetPath,
                    mapWidth = scenario.MapDefinition != null ? scenario.MapDefinition.Width : 0,
                    mapDepth = scenario.MapDefinition != null ? scenario.MapDefinition.Depth : 0,
                    unitCount = scenario.UnitEntries.Count,
                    playerCount = CountTeam(scenario.UnitEntries, TeamId.Player),
                    enemyCount = CountTeam(scenario.UnitEntries, TeamId.Enemy),
                    units = scenario.UnitEntries.Select(ToUnitSummary).ToArray()
                });
            }
            catch (Exception exception)
            {
                return new ErrorResponse($"Failed to create default skirmish scenario: {exception.Message}");
            }
        }

        private static void EnsureAssetFolderExists()
        {
            if (!AssetDatabase.IsValidFolder(AssetRootFolderPath))
            {
                AssetDatabase.CreateFolder("Assets", "ScriptableObjects");
            }

            if (!AssetDatabase.IsValidFolder(ScenarioAssetFolderPath))
            {
                AssetDatabase.CreateFolder(AssetRootFolderPath, "Scenarios");
            }
        }

        private static GridMapDefinition LoadRequiredMap()
        {
            var mapDefinition = AssetDatabase.LoadAssetAtPath<GridMapDefinition>(MapAssetPath);
            if (mapDefinition == null)
            {
                throw new InvalidOperationException($"Required default map asset was not found at '{MapAssetPath}'. Run rt_create_default_map first.");
            }

            if (mapDefinition.Width != ExpectedMapWidth || mapDefinition.Depth != ExpectedMapDepth)
            {
                throw new InvalidOperationException($"Default scenario requires an {ExpectedMapWidth}x{ExpectedMapDepth} map, but '{MapAssetPath}' is {mapDefinition.Width}x{mapDefinition.Depth}. Run rt_create_default_map first.");
            }

            return mapDefinition;
        }

        private static Dictionary<string, UnitStatsDefinition> LoadRequiredUnitStats(IEnumerable<ScenarioUnitSpec> specs)
        {
            var statsByName = new Dictionary<string, UnitStatsDefinition>(StringComparer.Ordinal);
            foreach (var spec in specs)
            {
                if (statsByName.ContainsKey(spec.StatsAssetName))
                {
                    continue;
                }

                var path = spec.StatsAssetPath;
                var stats = AssetDatabase.LoadAssetAtPath<UnitStatsDefinition>(path);
                if (stats == null)
                {
                    throw new InvalidOperationException($"Required default unit stats asset was not found at '{path}'. Run rt_create_default_units first.");
                }

                statsByName.Add(spec.StatsAssetName, stats);
            }

            return statsByName;
        }

        private static Dictionary<string, AbilityDefinition> LoadRequiredAbilities(IEnumerable<AbilityAssetSpec> specs)
        {
            var abilitiesByKey = new Dictionary<string, AbilityDefinition>(StringComparer.Ordinal);
            foreach (var spec in specs)
            {
                var ability = AssetDatabase.LoadAssetAtPath<AbilityDefinition>(spec.AssetPath);
                if (ability == null)
                {
                    throw new InvalidOperationException($"Required default ability asset was not found at '{spec.AssetPath}'. Run rt_create_default_abilities first.");
                }

                var validationResult = ability.ValidateDefinition();
                if (validationResult.IsFailure)
                {
                    throw new InvalidOperationException($"Default ability asset '{spec.AssetPath}' is invalid: {validationResult.ErrorMessage}");
                }

                if (!string.Equals(ability.AbilityKey, spec.AbilityKey, StringComparison.Ordinal))
                {
                    throw new InvalidOperationException($"Default ability asset '{spec.AssetPath}' has key '{ability.AbilityKey}' but expected '{spec.AbilityKey}'. Run rt_create_default_abilities first.");
                }

                if (abilitiesByKey.ContainsKey(ability.AbilityKey))
                {
                    throw new InvalidOperationException($"Default ability key '{ability.AbilityKey}' is loaded more than once.");
                }

                abilitiesByKey.Add(ability.AbilityKey, ability);
            }

            return abilitiesByKey;
        }

        private static Dictionary<string, IReadOnlyList<AbilityDefinition>> BuildRequiredLoadouts(
            IEnumerable<UnitAbilityLoadoutSpec> loadoutSpecs,
            IReadOnlyDictionary<string, AbilityDefinition> abilitiesByKey)
        {
            var loadoutsByStatsName = new Dictionary<string, IReadOnlyList<AbilityDefinition>>(StringComparer.Ordinal);
            foreach (var loadoutSpec in loadoutSpecs)
            {
                if (loadoutsByStatsName.ContainsKey(loadoutSpec.StatsAssetName))
                {
                    throw new InvalidOperationException($"Default loadout for '{loadoutSpec.StatsAssetName}' is defined more than once.");
                }

                var abilities = new List<AbilityDefinition>();
                foreach (var abilityKey in loadoutSpec.AbilityKeys)
                {
                    if (!abilitiesByKey.TryGetValue(abilityKey, out var ability))
                    {
                        throw new InvalidOperationException($"Default loadout for '{loadoutSpec.StatsAssetName}' requires unknown ability key '{abilityKey}'.");
                    }

                    abilities.Add(ability);
                }

                loadoutsByStatsName.Add(loadoutSpec.StatsAssetName, abilities);
            }

            return loadoutsByStatsName;
        }

        private static IReadOnlyList<ScenarioDefinition.UnitEntry> BuildScenarioEntries(
            IEnumerable<ScenarioUnitSpec> specs,
            IReadOnlyDictionary<string, UnitStatsDefinition> statsByName,
            IReadOnlyDictionary<string, IReadOnlyList<AbilityDefinition>> loadoutsByStatsName)
        {
            var entries = new List<ScenarioDefinition.UnitEntry>();
            foreach (var spec in specs)
            {
                if (!statsByName.TryGetValue(spec.StatsAssetName, out var stats))
                {
                    throw new InvalidOperationException($"No default stats asset was loaded for scenario unit '{spec.StatsAssetName}'.");
                }

                if (!loadoutsByStatsName.TryGetValue(spec.StatsAssetName, out var loadout) || loadout.Count == 0)
                {
                    throw new InvalidOperationException($"No default ability loadout is defined for scenario unit '{spec.StatsAssetName}'.");
                }

                entries.Add(new ScenarioDefinition.UnitEntry(spec.Team, stats, spec.Position, loadout));
            }

            return entries;
        }

        private static IReadOnlyList<string> ValidateDefaultComposition(
            GridMapDefinition mapDefinition,
            IReadOnlyList<ScenarioDefinition.UnitEntry> entries)
        {
            var errors = new List<string>();
            var map = mapDefinition.BuildGridMap();
            if (map.AllCells.Count() != ExpectedMapWidth * ExpectedMapDepth)
            {
                errors.Add($"Expected {ExpectedMapWidth * ExpectedMapDepth} map cells but built {map.AllCells.Count()}.");
            }

            foreach (var team in new[] { TeamId.Player, TeamId.Enemy })
            {
                var teamEntries = entries.Where(entry => entry.Team == team).ToArray();
                if (teamEntries.Length != ExpectedTeamSize)
                {
                    errors.Add($"Expected {ExpectedTeamSize} {team} units but found {teamEntries.Length}.");
                    continue;
                }

                if (!teamEntries.Any(HasMeleeAction))
                {
                    errors.Add($"Default scenario must include at least one melee-capable {team} unit.");
                }

                if (!teamEntries.Any(HasRangedOrAoeAction))
                {
                    errors.Add($"Default scenario must include at least one ranged or AoE-capable {team} unit.");
                }
            }

            var scenario = ScriptableObject.CreateInstance<ScenarioDefinition>();
            try
            {
                scenario.name = "DefaultSkirmishValidation";
                scenario.Configure(mapDefinition, entries);
                errors.AddRange(scenario.CollectValidationErrors());
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(scenario);
            }

            return errors;
        }

        private static bool HasMeleeAction(ScenarioDefinition.UnitEntry entry)
        {
            return entry.AbilityLoadout.Any(ability => ability != null && ability.Shape == AbilityShape.Melee && ability.CanBeUsedAsAction);
        }

        private static bool HasRangedOrAoeAction(ScenarioDefinition.UnitEntry entry)
        {
            return entry.AbilityLoadout.Any(ability => ability != null
                && ability.CanBeUsedAsAction
                && (ability.Shape == AbilityShape.Cone || ability.Shape == AbilityShape.Radius));
        }

        private static int CountTeam(IReadOnlyList<ScenarioDefinition.UnitEntry> entries, TeamId team)
        {
            return entries.Count(entry => entry.Team == team);
        }

        private static object ToUnitSummary(ScenarioDefinition.UnitEntry entry)
        {
            return new
            {
                team = entry.Team.ToString(),
                stats = entry.Stats != null ? entry.Stats.DisplayName : string.Empty,
                startingCell = ToPositionSummary(entry.StartingCell),
                abilityCount = entry.AbilityLoadout.Count,
                abilities = entry.AbilityLoadout
                    .Where(ability => ability != null)
                    .Select(ability => new
                    {
                        key = ability.AbilityKey,
                        name = ability.DisplayName,
                        usage = ability.Usage.ToString(),
                        shape = ability.Shape.ToString()
                    })
                    .ToArray()
            };
        }

        private static object ToPositionSummary(GridPosition position)
        {
            return new
            {
                x = position.X,
                y = position.Y,
                z = position.Z
            };
        }

        private readonly struct ScenarioUnitSpec
        {
            public ScenarioUnitSpec(string statsAssetName, TeamId team, GridPosition position)
            {
                StatsAssetName = statsAssetName;
                Team = team;
                Position = position;
            }

            public string StatsAssetName { get; }

            public TeamId Team { get; }

            public GridPosition Position { get; }

            public string StatsAssetPath
            {
                get { return UnitAssetFolderPath + "/" + StatsAssetName + ".asset"; }
            }
        }

        private readonly struct AbilityAssetSpec
        {
            public AbilityAssetSpec(string abilityKey, string assetName)
            {
                AbilityKey = abilityKey;
                AssetName = assetName;
            }

            public string AbilityKey { get; }

            public string AssetName { get; }

            public string AssetPath
            {
                get { return AbilityAssetFolderPath + "/" + AssetName + ".asset"; }
            }
        }

        private sealed class UnitAbilityLoadoutSpec
        {
            public UnitAbilityLoadoutSpec(string statsAssetName, params string[] abilityKeys)
            {
                StatsAssetName = statsAssetName;
                AbilityKeys = abilityKeys ?? Array.Empty<string>();
            }

            public string StatsAssetName { get; }

            public IReadOnlyList<string> AbilityKeys { get; }
        }
    }
}
