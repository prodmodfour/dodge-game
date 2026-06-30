using NUnit.Framework;
using ReactionTactics.Grid;
using ReactionTactics.Units;
using UnityEditor;
using UnityEngine;

namespace ReactionTactics.Tests.EditMode.Units
{
    public sealed class UnitSpawnerTests
    {
        [Test]
        public void SpawnInitializesSnapsAndRegistersUnit()
        {
            var fixture = CreateFixture();

            try
            {
                var requestedPosition = new GridPosition(1, 0, 0);
                var spawnPosition = new GridPosition(1, 2, 0);

                var result = fixture.Spawner.Spawn(fixture.Stats, TeamId.Enemy, requestedPosition);

                Assert.That(result.IsSuccess, Is.True, result.ErrorMessage);
                var unit = result.Value;
                Assert.That(unit.UnitId, Is.EqualTo(new UnitId(1)));
                Assert.That(unit.Team, Is.EqualTo(TeamId.Enemy));
                Assert.That(unit.StatsDefinition, Is.SameAs(fixture.Stats));
                Assert.That(unit.CurrentGridPosition, Is.EqualTo(spawnPosition));
                Assert.That(unit.CurrentHP, Is.EqualTo(fixture.Stats.MaxHP));
                Assert.That(unit.CurrentAP, Is.EqualTo(fixture.Stats.MaxAP));
                Assert.That(unit.transform.parent, Is.SameAs(fixture.SpawnParent.transform));
                Assert.That(unit.transform.position, Is.EqualTo(GridMetrics.Default.GridToWorldCenter(spawnPosition)));

                var mover = unit.GetComponent<GridPathMover>();
                Assert.That(mover, Is.Not.Null);
                Assert.That(mover.Metrics.CellSize, Is.EqualTo(GridMetrics.Default.CellSize));
                Assert.That(mover.Metrics.HeightStep, Is.EqualTo(GridMetrics.Default.HeightStep));
                Assert.That(mover.MovementSpeed, Is.EqualTo(fixture.Stats.MovementAnimationSpeed));

                Assert.That(fixture.Registry.RegisteredCount, Is.EqualTo(1));
                Assert.That(fixture.Registry.IsOccupied(spawnPosition), Is.True);
                Assert.That(fixture.Registry.TryGetOccupant(spawnPosition, out var occupantId), Is.True);
                Assert.That(occupantId, Is.EqualTo(unit.UnitId));
            }
            finally
            {
                fixture.Destroy();
            }
        }

        [Test]
        public void SpawnRejectsMissingBlockedAndOccupiedCellsWithoutCreatingExtraUnits()
        {
            var fixture = CreateFixture();

            try
            {
                var missingResult = fixture.Spawner.Spawn(fixture.Stats, TeamId.Player, new GridPosition(9, 0, 9));
                var blockedResult = fixture.Spawner.Spawn(fixture.Stats, TeamId.Player, new GridPosition(0, 0, 1));
                var firstSpawn = fixture.Spawner.Spawn(fixture.Stats, TeamId.Player, GridPosition.Zero);
                var occupiedResult = fixture.Spawner.Spawn(fixture.Stats, TeamId.Enemy, GridPosition.Zero);

                Assert.That(missingResult.IsFailure, Is.True);
                Assert.That(missingResult.ErrorMessage, Does.Contain("No spawn cell"));
                Assert.That(blockedResult.IsFailure, Is.True);
                Assert.That(blockedResult.ErrorMessage, Does.Contain("blocked"));
                Assert.That(firstSpawn.IsSuccess, Is.True, firstSpawn.ErrorMessage);
                Assert.That(occupiedResult.IsFailure, Is.True);
                Assert.That(occupiedResult.ErrorMessage, Does.Contain("occupied"));
                Assert.That(fixture.Registry.RegisteredCount, Is.EqualTo(1));
                Assert.That(fixture.SpawnParent.transform.childCount, Is.EqualTo(1));
                Assert.That(fixture.Registry.IsOccupied(GridPosition.Zero), Is.True);
            }
            finally
            {
                fixture.Destroy();
            }
        }

        private static SpawnerFixture CreateFixture()
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
                        heightY: 2,
                        walkable: true,
                        blocksLineOfSight: false,
                        movementCost: 1),
                    new GridMapDefinition.CellOverride(
                        x: 0,
                        z: 1,
                        heightY: 0,
                        walkable: false,
                        blocksLineOfSight: false,
                        movementCost: 1)
                });

            var stats = ScriptableObject.CreateInstance<UnitStatsDefinition>();
            stats.Configure(
                displayName: "Spawner Test Unit",
                maxHP: 12,
                maxAP: 6,
                movementAnimationSpeed: 5.5f,
                meleeRange: 1,
                teamColorHint: Color.cyan);

            var gridObject = CreateInactiveGameObject("Spawner Grid Manager");
            var gridManager = gridObject.AddComponent<GridManager>();
            AssignMapDefinition(gridManager, mapDefinition);
            Assert.That(gridManager.RebuildMap(), Is.True);

            var registryObject = new GameObject("Spawner Unit Registry");
            var registry = registryObject.AddComponent<UnitRegistry>();

            var prefabObject = new GameObject("Prototype Unit Template");
            var prefab = prefabObject.AddComponent<TacticalUnit>();
            prefabObject.AddComponent<GridPathMover>();

            var spawnParent = new GameObject("Spawned Units Root");
            var spawnerObject = new GameObject("Unit Spawner");
            var spawner = spawnerObject.AddComponent<UnitSpawner>();
            spawner.Configure(prefab, registry, gridManager, spawnParent.transform);

            return new SpawnerFixture(
                mapDefinition,
                stats,
                gridObject,
                registryObject,
                prefabObject,
                spawnParent,
                spawnerObject,
                spawner,
                registry);
        }

        private static GameObject CreateInactiveGameObject(string name)
        {
            var gameObject = new GameObject(name);
            gameObject.SetActive(false);
            return gameObject;
        }

        private static void AssignMapDefinition(GridManager manager, GridMapDefinition definition)
        {
            var serializedObject = new SerializedObject(manager);
            serializedObject.FindProperty("mapDefinition").objectReferenceValue = definition;
            serializedObject.ApplyModifiedPropertiesWithoutUndo();
        }

        private sealed class SpawnerFixture
        {
            private readonly GridMapDefinition mapDefinition;
            private readonly GameObject gridObject;
            private readonly GameObject registryObject;
            private readonly GameObject prefabObject;
            private readonly GameObject spawnerObject;

            public SpawnerFixture(
                GridMapDefinition mapDefinition,
                UnitStatsDefinition stats,
                GameObject gridObject,
                GameObject registryObject,
                GameObject prefabObject,
                GameObject spawnParent,
                GameObject spawnerObject,
                UnitSpawner spawner,
                UnitRegistry registry)
            {
                this.mapDefinition = mapDefinition;
                this.gridObject = gridObject;
                this.registryObject = registryObject;
                this.prefabObject = prefabObject;
                this.spawnerObject = spawnerObject;
                Stats = stats;
                SpawnParent = spawnParent;
                Spawner = spawner;
                Registry = registry;
            }

            public UnitStatsDefinition Stats { get; }

            public GameObject SpawnParent { get; }

            public UnitSpawner Spawner { get; }

            public UnitRegistry Registry { get; }

            public void Destroy()
            {
                Object.DestroyImmediate(spawnerObject);
                Object.DestroyImmediate(SpawnParent);
                Object.DestroyImmediate(prefabObject);
                Object.DestroyImmediate(registryObject);
                Object.DestroyImmediate(gridObject);
                Object.DestroyImmediate(Stats);
                Object.DestroyImmediate(mapDefinition);
            }
        }
    }
}
