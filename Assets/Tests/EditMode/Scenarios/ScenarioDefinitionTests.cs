using System.Collections.Generic;
using NUnit.Framework;
using ReactionTactics.Actions;
using ReactionTactics.Grid;
using ReactionTactics.Scenarios;
using ReactionTactics.Units;
using UnityEngine;

namespace ReactionTactics.Tests.EditMode.Scenarios
{
    public sealed class ScenarioDefinitionTests
    {
        private readonly List<UnityEngine.Object> createdAssets = new List<UnityEngine.Object>();

        [TearDown]
        public void TearDown()
        {
            for (var i = 0; i < createdAssets.Count; i++)
            {
                UnityEngine.Object.DestroyImmediate(createdAssets[i]);
            }

            createdAssets.Clear();
        }

        [Test]
        public void ConfigureStoresBattleDataAndValidationSucceeds()
        {
            var scenario = CreateAsset<ScenarioDefinition>();
            var map = CreateMap(width: 3, depth: 3, defaultHeightY: 0);
            var stats = CreateStats("Knight");
            var ability = CreateAbility("move", "Move");

            scenario.Configure(
                map,
                new[]
                {
                    new ScenarioDefinition.UnitEntry(
                        TeamId.Player,
                        stats,
                        new GridPosition(0, 0, 0),
                        new[] { ability }),
                    new ScenarioDefinition.UnitEntry(
                        TeamId.Enemy,
                        stats,
                        new GridPosition(2, 0, 2)),
                });

            var result = scenario.ValidateScenario();

            Assert.That(result.IsSuccess, Is.True, result.ErrorMessage);
            Assert.That(scenario.MapDefinition, Is.SameAs(map));
            Assert.That(scenario.UnitEntries, Has.Count.EqualTo(2));
            Assert.That(scenario.UnitEntries[0].Team, Is.EqualTo(TeamId.Player));
            Assert.That(scenario.UnitEntries[0].Stats, Is.SameAs(stats));
            Assert.That(scenario.UnitEntries[0].StartingCell, Is.EqualTo(new GridPosition(0, 0, 0)));
            Assert.That(scenario.UnitEntries[0].HasAbilityLoadoutOverride, Is.True);
            Assert.That(scenario.UnitEntries[0].AbilityLoadout, Is.EqualTo(new[] { ability }));
            Assert.That(scenario.UnitEntries[1].HasAbilityLoadoutOverride, Is.False);
        }

        [Test]
        public void ValidateScenarioRejectsDuplicateSpawnCells()
        {
            var scenario = CreateAsset<ScenarioDefinition>();
            var map = CreateMap(width: 2, depth: 2, defaultHeightY: 0);
            var stats = CreateStats("Rogue");

            scenario.Configure(
                map,
                new[]
                {
                    new ScenarioDefinition.UnitEntry(TeamId.Player, stats, new GridPosition(1, 0, 1)),
                    new ScenarioDefinition.UnitEntry(TeamId.Enemy, stats, new GridPosition(1, 0, 1)),
                });

            var result = scenario.ValidateScenario();

            Assert.That(result.IsFailure, Is.True);
            Assert.That(result.ErrorMessage, Does.Contain("duplicate spawn cell"));
            Assert.That(result.ErrorMessage, Does.Contain("(1,0,1)"));
        }

        [Test]
        public void ValidateScenarioRejectsStartingCellOutsideMap()
        {
            var scenario = CreateAsset<ScenarioDefinition>();
            var map = CreateMap(width: 2, depth: 2, defaultHeightY: 0);
            var stats = CreateStats("Archer");

            scenario.Configure(
                map,
                new[]
                {
                    new ScenarioDefinition.UnitEntry(TeamId.Player, stats, new GridPosition(2, 0, 0)),
                });

            var result = scenario.ValidateScenario();

            Assert.That(result.IsFailure, Is.True);
            Assert.That(result.ErrorMessage, Does.Contain("does not exist in the map"));
        }

        [Test]
        public void ValidateScenarioRejectsStartingCellWithWrongHeight()
        {
            var scenario = CreateAsset<ScenarioDefinition>();
            var map = CreateMap(width: 2, depth: 2, defaultHeightY: 1);
            var stats = CreateStats("Mage");

            scenario.Configure(
                map,
                new[]
                {
                    new ScenarioDefinition.UnitEntry(TeamId.Player, stats, new GridPosition(0, 0, 0)),
                });

            var result = scenario.ValidateScenario();

            Assert.That(result.IsFailure, Is.True);
            Assert.That(result.ErrorMessage, Does.Contain("does not exist in the map"));
            Assert.That(result.ErrorMessage, Does.Contain("height y"));
        }

        [Test]
        public void ValidateScenarioRejectsBlockedSpawnCells()
        {
            var scenario = CreateAsset<ScenarioDefinition>();
            var map = CreateMap(
                width: 2,
                depth: 2,
                defaultHeightY: 0,
                new GridMapDefinition.CellOverride(
                    x: 1,
                    z: 0,
                    heightY: 0,
                    walkable: false,
                    blocksLineOfSight: false,
                    movementCost: 1));
            var stats = CreateStats("Goblin");

            scenario.Configure(
                map,
                new[]
                {
                    new ScenarioDefinition.UnitEntry(TeamId.Enemy, stats, new GridPosition(1, 0, 0)),
                });

            var result = scenario.ValidateScenario();

            Assert.That(result.IsFailure, Is.True);
            Assert.That(result.ErrorMessage, Does.Contain("not walkable"));
        }

        [Test]
        public void ValidateScenarioReportsMissingRequiredReferences()
        {
            var scenario = CreateAsset<ScenarioDefinition>();

            scenario.Configure(
                mapDefinition: null,
                unitEntries: new[]
                {
                    new ScenarioDefinition.UnitEntry(TeamId.Player, stats: null, startingCell: GridPosition.Zero),
                });

            var errors = scenario.CollectValidationErrors();

            Assert.That(errors, Has.Some.Contains("map definition is required"));
            Assert.That(errors, Has.Some.Contains("missing unit stats"));
        }

        private T CreateAsset<T>() where T : ScriptableObject
        {
            var asset = ScriptableObject.CreateInstance<T>();
            createdAssets.Add(asset);
            return asset;
        }

        private GridMapDefinition CreateMap(
            int width,
            int depth,
            int defaultHeightY,
            params GridMapDefinition.CellOverride[] overrides)
        {
            var map = CreateAsset<GridMapDefinition>();
            map.Configure(width, depth, defaultHeightY, overrides);
            return map;
        }

        private UnitStatsDefinition CreateStats(string displayName)
        {
            var stats = CreateAsset<UnitStatsDefinition>();
            stats.Configure(
                displayName,
                maxHP: 10,
                maxAP: 6,
                movementAnimationSpeed: UnitStatsDefinition.DefaultMovementAnimationSpeed,
                meleeRange: UnitStatsDefinition.MinimumMeleeRange,
                teamColorHint: Color.white);
            return stats;
        }

        private AbilityDefinition CreateAbility(string key, string displayName)
        {
            var ability = CreateAsset<AbilityDefinition>();
            ability.Configure(
                key,
                displayName,
                apCost: 0,
                usage: AbilityUsage.Both,
                timing: AbilityTiming.Immediate,
                shape: AbilityShape.Self,
                range: 0,
                radius: 0,
                damage: 0,
                triggersReactions: false);
            return ability;
        }
    }
}
