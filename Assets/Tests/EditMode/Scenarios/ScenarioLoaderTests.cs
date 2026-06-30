using System.Collections.Generic;
using NUnit.Framework;
using ReactionTactics.Actions;
using ReactionTactics.Grid;
using ReactionTactics.Scenarios;
using ReactionTactics.Turns;
using ReactionTactics.Units;
using UnityEngine;

namespace ReactionTactics.Tests.EditMode.Scenarios
{
    public sealed class ScenarioLoaderTests
    {
        [Test]
        public void LoadScenarioAssignsMapSpawnsUnitsAndAppliesLoadouts()
        {
            var fixture = CreateFixture();

            try
            {
                var result = fixture.Loader.LoadScenario();

                Assert.That(result.IsSuccess, Is.True, result.ErrorMessage);
                Assert.That(fixture.GridManager.MapDefinition, Is.SameAs(fixture.MapDefinition));
                Assert.That(fixture.GridManager.CurrentMap, Is.Not.Null);
                Assert.That(fixture.Registry.RegisteredCount, Is.EqualTo(2));
                Assert.That(fixture.Loader.SpawnedUnitCount, Is.EqualTo(2));
                Assert.That(fixture.SpawnParent.transform.childCount, Is.EqualTo(2));

                var units = fixture.Registry.GetRegisteredUnits();
                Assert.That(units[0].UnitId, Is.EqualTo(new UnitId(1)));
                Assert.That(units[0].Team, Is.EqualTo(TeamId.Player));
                Assert.That(units[0].CurrentGridPosition, Is.EqualTo(new GridPosition(0, 0, 0)));
                Assert.That(units[1].UnitId, Is.EqualTo(new UnitId(2)));
                Assert.That(units[1].Team, Is.EqualTo(TeamId.Enemy));
                Assert.That(units[1].CurrentGridPosition, Is.EqualTo(new GridPosition(1, 1, 0)));

                var loadout = units[0].GetComponent<UnitAbilityLoadout>();
                Assert.That(loadout, Is.Not.Null);
                Assert.That(loadout.GetAssignedAbilities(), Is.EqualTo(new[] { fixture.MoveAbility, fixture.BraceAbility }));
                Assert.That(fixture.CombatManager.CurrentState.Phase, Is.EqualTo(CombatPhase.NotStarted));
            }
            finally
            {
                fixture.Destroy();
            }
        }

        [Test]
        public void LoadScenarioIsIdempotentAndResetsSpawnedRoster()
        {
            var fixture = CreateFixture();

            try
            {
                var firstResult = fixture.Loader.LoadScenario();
                Assert.That(firstResult.IsSuccess, Is.True, firstResult.ErrorMessage);
                var firstSpawnedUnit = fixture.Registry.GetRegisteredUnits()[0];

                var secondResult = fixture.Loader.LoadScenario();

                Assert.That(secondResult.IsSuccess, Is.True, secondResult.ErrorMessage);
                Assert.That(firstSpawnedUnit == null, Is.True, "The previous scenario unit should be destroyed before reload.");
                Assert.That(fixture.Registry.RegisteredCount, Is.EqualTo(2));
                Assert.That(fixture.Loader.SpawnedUnitCount, Is.EqualTo(2));
                Assert.That(fixture.SpawnParent.transform.childCount, Is.EqualTo(2));

                var units = fixture.Registry.GetRegisteredUnits();
                Assert.That(units[0].UnitId, Is.EqualTo(new UnitId(1)));
                Assert.That(units[1].UnitId, Is.EqualTo(new UnitId(2)));
                Assert.That(fixture.Registry.IsOccupied(new GridPosition(0, 0, 0)), Is.True);
                Assert.That(fixture.Registry.IsOccupied(new GridPosition(1, 1, 0)), Is.True);
            }
            finally
            {
                fixture.Destroy();
            }
        }

        [Test]
        public void LoadScenarioRejectsInvalidScenarioWithoutSpawningUnits()
        {
            var fixture = CreateFixtureWithInvalidScenario();

            try
            {
                var result = fixture.Loader.LoadScenario();

                Assert.That(result.IsFailure, Is.True);
                Assert.That(result.ErrorMessage, Does.Contain("Cannot load scenario"));
                Assert.That(result.ErrorMessage, Does.Contain("duplicate spawn cell"));
                Assert.That(fixture.Registry.RegisteredCount, Is.EqualTo(0));
                Assert.That(fixture.SpawnParent.transform.childCount, Is.EqualTo(0));
            }
            finally
            {
                fixture.Destroy();
            }
        }

        private static LoaderFixture CreateFixture()
        {
            var mapDefinition = ScriptableObject.CreateInstance<GridMapDefinition>();
            mapDefinition.Configure(
                width: 2,
                depth: 2,
                defaultHeightY: 0,
                new[]
                {
                    new GridMapDefinition.CellOverride(
                        x: 1,
                        z: 0,
                        heightY: 1,
                        walkable: true,
                        blocksLineOfSight: false,
                        movementCost: 1)
                });

            var playerStats = CreateStats("Loader Knight");
            var enemyStats = CreateStats("Loader Goblin");
            var moveAbility = CreateAbility("move", "Move", AbilityUsage.Both);
            var braceAbility = CreateAbility("brace", "Brace", AbilityUsage.Reaction);

            var scenario = ScriptableObject.CreateInstance<ScenarioDefinition>();
            scenario.Configure(
                mapDefinition,
                new[]
                {
                    new ScenarioDefinition.UnitEntry(
                        TeamId.Player,
                        playerStats,
                        new GridPosition(0, 0, 0),
                        new[] { moveAbility, braceAbility }),
                    new ScenarioDefinition.UnitEntry(
                        TeamId.Enemy,
                        enemyStats,
                        new GridPosition(1, 1, 0),
                        new[] { moveAbility })
                });

            return CreateFixtureFromScenario(mapDefinition, scenario, playerStats, enemyStats, moveAbility, braceAbility);
        }

        private static LoaderFixture CreateFixtureWithInvalidScenario()
        {
            var fixture = CreateFixture();
            Object.DestroyImmediate(fixture.ScenarioDefinition);

            var scenario = ScriptableObject.CreateInstance<ScenarioDefinition>();
            scenario.Configure(
                fixture.MapDefinition,
                new[]
                {
                    new ScenarioDefinition.UnitEntry(TeamId.Player, fixture.PlayerStats, new GridPosition(0, 0, 0)),
                    new ScenarioDefinition.UnitEntry(TeamId.Enemy, fixture.EnemyStats, new GridPosition(0, 0, 0))
                });
            fixture.ReplaceScenarioDefinition(scenario);
            return fixture;
        }

        private static LoaderFixture CreateFixtureFromScenario(
            GridMapDefinition mapDefinition,
            ScenarioDefinition scenario,
            UnitStatsDefinition playerStats,
            UnitStatsDefinition enemyStats,
            AbilityDefinition moveAbility,
            AbilityDefinition braceAbility)
        {
            var gridObject = new GameObject("Scenario Loader Grid Manager");
            var gridManager = gridObject.AddComponent<GridManager>();

            var registryObject = new GameObject("Scenario Loader Unit Registry");
            var registry = registryObject.AddComponent<UnitRegistry>();

            var prefabObject = new GameObject("Scenario Loader Unit Prefab");
            var prefab = prefabObject.AddComponent<TacticalUnit>();
            prefabObject.AddComponent<GridPathMover>();

            var spawnParent = new GameObject("Scenario Loader Spawn Parent");
            var spawnerObject = new GameObject("Scenario Loader Unit Spawner");
            var spawner = spawnerObject.AddComponent<UnitSpawner>();
            spawner.Configure(prefab, registry, gridManager, spawnParent.transform);

            var combatManagerObject = new GameObject("Scenario Loader Combat Manager");
            var combatManager = combatManagerObject.AddComponent<CombatManager>();
            combatManager.StartCombatOnStart = false;

            var loaderObject = new GameObject("Scenario Loader");
            var loader = loaderObject.AddComponent<ScenarioLoader>();
            loader.Configure(scenario, gridManager, spawner, combatManager);

            return new LoaderFixture(
                mapDefinition,
                scenario,
                playerStats,
                enemyStats,
                moveAbility,
                braceAbility,
                gridObject,
                registryObject,
                prefabObject,
                spawnParent,
                spawnerObject,
                combatManagerObject,
                loaderObject,
                gridManager,
                registry,
                loader,
                combatManager);
        }

        private static UnitStatsDefinition CreateStats(string displayName)
        {
            var stats = ScriptableObject.CreateInstance<UnitStatsDefinition>();
            stats.Configure(
                displayName,
                maxHP: 10,
                maxAP: 6,
                movementAnimationSpeed: UnitStatsDefinition.DefaultMovementAnimationSpeed,
                meleeRange: UnitStatsDefinition.MinimumMeleeRange,
                teamColorHint: Color.white);
            return stats;
        }

        private static AbilityDefinition CreateAbility(string key, string displayName, AbilityUsage usage)
        {
            var ability = ScriptableObject.CreateInstance<AbilityDefinition>();
            ability.Configure(
                key,
                displayName,
                apCost: 0,
                usage: usage,
                timing: AbilityTiming.Immediate,
                shape: AbilityShape.Self,
                range: 0,
                radius: 0,
                damage: 0,
                triggersReactions: false);
            return ability;
        }

        private sealed class LoaderFixture
        {
            private readonly List<Object> ownedObjects = new List<Object>();

            public LoaderFixture(
                GridMapDefinition mapDefinition,
                ScenarioDefinition scenarioDefinition,
                UnitStatsDefinition playerStats,
                UnitStatsDefinition enemyStats,
                AbilityDefinition moveAbility,
                AbilityDefinition braceAbility,
                params Object[] objects)
            {
                MapDefinition = mapDefinition;
                ScenarioDefinition = scenarioDefinition;
                PlayerStats = playerStats;
                EnemyStats = enemyStats;
                MoveAbility = moveAbility;
                BraceAbility = braceAbility;
                ownedObjects.Add(mapDefinition);
                ownedObjects.Add(scenarioDefinition);
                ownedObjects.Add(playerStats);
                ownedObjects.Add(enemyStats);
                ownedObjects.Add(moveAbility);
                ownedObjects.Add(braceAbility);

                for (var i = 0; i < objects.Length; i++)
                {
                    ownedObjects.Add(objects[i]);
                }

                GridManager = (GridManager)objects[7];
                Registry = (UnitRegistry)objects[8];
                Loader = (ScenarioLoader)objects[9];
                CombatManager = (CombatManager)objects[10];
                SpawnParent = (GameObject)objects[3];
            }

            public GridMapDefinition MapDefinition { get; }

            public ScenarioDefinition ScenarioDefinition { get; private set; }

            public UnitStatsDefinition PlayerStats { get; }

            public UnitStatsDefinition EnemyStats { get; }

            public AbilityDefinition MoveAbility { get; }

            public AbilityDefinition BraceAbility { get; }

            public GridManager GridManager { get; }

            public UnitRegistry Registry { get; }

            public ScenarioLoader Loader { get; }

            public CombatManager CombatManager { get; }

            public GameObject SpawnParent { get; }

            public void ReplaceScenarioDefinition(ScenarioDefinition scenarioDefinition)
            {
                ScenarioDefinition = scenarioDefinition;
                ownedObjects.Add(scenarioDefinition);
                Loader.Configure(scenarioDefinition, GridManager, Loader.UnitSpawner, CombatManager);
            }

            public void Destroy()
            {
                for (var i = ownedObjects.Count - 1; i >= 0; i--)
                {
                    if (ownedObjects[i] != null)
                    {
                        Object.DestroyImmediate(ownedObjects[i]);
                    }
                }

                ownedObjects.Clear();
            }
        }
    }
}
