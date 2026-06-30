using NUnit.Framework;
using ReactionTactics.Grid;
using ReactionTactics.Units;
using UnityEditor;
using UnityEngine;

public sealed class Unit
{
    [Test]
    public void ActionPointWalletSpendsRollbackOnFailureAndRefreshesToMax()
    {
        var gameObject = new GameObject("Unit Model AP Test");
        var stats = CreateStats(maxHP: 10, maxAP: 5);

        try
        {
            var unit = gameObject.AddComponent<TacticalUnit>();
            unit.Initialize(new UnitId(1), TeamId.Player, stats, GridPosition.Zero);

            var spendResult = unit.SpendAP(3);
            var failedSpendResult = unit.SpendAP(4);

            Assert.That(spendResult.IsSuccess, Is.True);
            Assert.That(unit.CurrentAP, Is.EqualTo(2));
            Assert.That(failedSpendResult.IsFailure, Is.True);
            Assert.That(unit.CurrentAP, Is.EqualTo(2), "Failed AP spends must not debit the shared wallet.");

            unit.RefreshAP();

            Assert.That(unit.CurrentAP, Is.EqualTo(stats.MaxAP));
            Assert.That(unit.CanSpendAP(stats.MaxAP), Is.True);
        }
        finally
        {
            Object.DestroyImmediate(gameObject);
            Object.DestroyImmediate(stats);
        }
    }

    [Test]
    public void DamageTracksHitPointsDeathStateAndSingleDeathEvent()
    {
        var gameObject = new GameObject("Unit Model HP Test");
        var stats = CreateStats(maxHP: 9, maxAP: 6);

        try
        {
            var unit = gameObject.AddComponent<TacticalUnit>();
            unit.Initialize(new UnitId(2), TeamId.Enemy, stats, GridPosition.Zero);
            var deathEvents = 0;
            DamageSource observedSource = DamageSource.Unspecified;
            unit.Died += (_, source) =>
            {
                deathEvents += 1;
                observedSource = source;
            };
            var source = DamageSource.FromUnit(new UnitId(99), "Unit model damage");

            var nonlethalResult = unit.ApplyDamage(4, source);
            var lethalResult = unit.ApplyDamage(99, source);
            var repeatedDamageResult = unit.ApplyDamage(1, source);

            Assert.That(nonlethalResult.IsSuccess, Is.True);
            Assert.That(lethalResult.IsSuccess, Is.True);
            Assert.That(repeatedDamageResult.IsFailure, Is.True);
            Assert.That(unit.CurrentHP, Is.EqualTo(0));
            Assert.That(unit.IsAlive, Is.False);
            Assert.That(unit.IsDead, Is.True);
            Assert.That(deathEvents, Is.EqualTo(1));
            Assert.That(observedSource, Is.EqualTo(source));
        }
        finally
        {
            Object.DestroyImmediate(gameObject);
            Object.DestroyImmediate(stats);
        }
    }

    [Test]
    public void RegistryOccupancyFollowsRegisteredUnitGridPositionChanges()
    {
        var stats = CreateStats(maxHP: 10, maxAP: 6);
        var registryObject = new GameObject("Unit Model Registry Test");
        var unitObject = new GameObject("Registered Moving Unit");

        try
        {
            var registry = registryObject.AddComponent<UnitRegistry>();
            var start = new GridPosition(1, 0, 1);
            var destination = new GridPosition(2, 1, 3);
            var unit = unitObject.AddComponent<TacticalUnit>();
            unit.Initialize(new UnitId(3), TeamId.Player, stats, start);

            Assert.That(registry.Register(unit).IsSuccess, Is.True);
            Assert.That(registry.IsOccupied(start), Is.True);
            Assert.That(registry.TryGetOccupant(start, out var startOccupant), Is.True);
            Assert.That(startOccupant, Is.EqualTo(unit.UnitId));

            unit.SetGridPosition(destination);

            Assert.That(registry.IsOccupied(start), Is.False);
            Assert.That(registry.CanEnter(start), Is.True);
            Assert.That(registry.TryGetOccupant(start, out var emptyOccupant), Is.False);
            Assert.That(emptyOccupant, Is.EqualTo(UnitId.None));
            Assert.That(registry.IsOccupied(destination), Is.True);
            Assert.That(registry.TryGetOccupant(destination, out var movedOccupant), Is.True);
            Assert.That(movedOccupant, Is.EqualTo(unit.UnitId));
            Assert.That(registry.CanEnter(destination), Is.False);
        }
        finally
        {
            Object.DestroyImmediate(unitObject);
            Object.DestroyImmediate(registryObject);
            Object.DestroyImmediate(stats);
        }
    }

    [Test]
    public void SpawnerValidationRejectsOccupiedCellsWithoutCreatingUnits()
    {
        var fixture = CreateSpawnerFixture();

        try
        {
            var firstSpawn = fixture.Spawner.Spawn(fixture.Stats, TeamId.Player, GridPosition.Zero);

            var validationResult = fixture.Spawner.ValidateSpawnCell(GridPosition.Zero);
            var duplicateSpawn = fixture.Spawner.Spawn(fixture.Stats, TeamId.Enemy, GridPosition.Zero);

            Assert.That(firstSpawn.IsSuccess, Is.True, firstSpawn.ErrorMessage);
            Assert.That(validationResult.IsFailure, Is.True);
            Assert.That(validationResult.ErrorMessage, Does.Contain("occupied"));
            Assert.That(duplicateSpawn.IsFailure, Is.True);
            Assert.That(duplicateSpawn.ErrorMessage, Does.Contain("occupied"));
            Assert.That(fixture.Registry.RegisteredCount, Is.EqualTo(1));
            Assert.That(fixture.SpawnParent.transform.childCount, Is.EqualTo(1));
        }
        finally
        {
            fixture.Destroy();
        }
    }

    private static UnitStatsDefinition CreateStats(int maxHP, int maxAP)
    {
        var stats = ScriptableObject.CreateInstance<UnitStatsDefinition>();
        stats.Configure(
            displayName: "Unit Model Test Unit",
            maxHP: maxHP,
            maxAP: maxAP,
            movementAnimationSpeed: 4f,
            meleeRange: 1,
            teamColorHint: Color.white);
        return stats;
    }

    private static SpawnerFixture CreateSpawnerFixture()
    {
        var mapDefinition = ScriptableObject.CreateInstance<GridMapDefinition>();
        mapDefinition.Configure(width: 2, depth: 1, defaultHeightY: 0, overrides: null);

        var stats = CreateStats(maxHP: 10, maxAP: 6);

        var gridObject = new GameObject("Unit Model Grid Manager");
        gridObject.SetActive(false);
        var gridManager = gridObject.AddComponent<GridManager>();
        AssignMapDefinition(gridManager, mapDefinition);
        Assert.That(gridManager.RebuildMap(), Is.True);

        var registryObject = new GameObject("Unit Model Unit Registry");
        var registry = registryObject.AddComponent<UnitRegistry>();

        var prefabObject = new GameObject("Unit Model Unit Prefab");
        var prefab = prefabObject.AddComponent<TacticalUnit>();
        prefabObject.AddComponent<GridPathMover>();

        var spawnParent = new GameObject("Unit Model Spawn Parent");
        var spawnerObject = new GameObject("Unit Model Unit Spawner");
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
