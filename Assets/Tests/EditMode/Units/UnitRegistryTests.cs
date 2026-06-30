using System.Linq;
using NUnit.Framework;
using ReactionTactics.Grid;
using ReactionTactics.Pathfinding;
using ReactionTactics.Units;
using UnityEngine;

public sealed class UnitRegistryTests
{
    [Test]
    public void RegisterAddsUnitLookupTeamAliveAndOccupancyQueries()
    {
        var stats = CreateStats();
        var registryObject = new GameObject("Unit Registry Test");
        var playerObject = new GameObject("Player Unit");
        var enemyObject = new GameObject("Enemy Unit");

        try
        {
            var registry = registryObject.AddComponent<UnitRegistry>();
            var player = CreateUnit(playerObject, new UnitId(1), TeamId.Player, stats, new GridPosition(0, 0, 0));
            var enemy = CreateUnit(enemyObject, new UnitId(2), TeamId.Enemy, stats, new GridPosition(2, 1, 3));

            Assert.That(registry.Register(player).IsSuccess, Is.True);
            Assert.That(registry.Register(enemy).IsSuccess, Is.True);

            Assert.That(registry.RegisteredCount, Is.EqualTo(2));
            Assert.That(registry.LivingCount, Is.EqualTo(2));
            Assert.That(registry.DeadCount, Is.EqualTo(0));
            Assert.That(registry.TryGetUnit(new UnitId(1), out var foundPlayer), Is.True);
            Assert.That(foundPlayer, Is.SameAs(player));
            Assert.That(registry.TryGetLivingUnit(new UnitId(2), out var foundEnemy), Is.True);
            Assert.That(foundEnemy, Is.SameAs(enemy));
            Assert.That(registry.GetUnitsByTeam(TeamId.Player), Is.EquivalentTo(new[] { player }));
            Assert.That(registry.GetUnitsByTeam(TeamId.Enemy), Is.EquivalentTo(new[] { enemy }));
            Assert.That(registry.GetLivingUnits().ToArray(), Is.EqualTo(new[] { player, enemy }));
            Assert.That(registry.GetUnitsAt(enemy.CurrentGridPosition), Is.EquivalentTo(new[] { enemy }));

            Assert.That(registry.IsOccupied(player.CurrentGridPosition), Is.True);
            Assert.That(registry.TryGetOccupant(player.CurrentGridPosition, out var occupantId), Is.True);
            Assert.That(occupantId, Is.EqualTo(player.UnitId));
            Assert.That(registry.CanEnter(player.CurrentGridPosition), Is.False);
            Assert.That(registry.CanEnter(GridPosition.North), Is.True);
        }
        finally
        {
            UnityEngine.Object.DestroyImmediate(playerObject);
            UnityEngine.Object.DestroyImmediate(enemyObject);
            UnityEngine.Object.DestroyImmediate(registryObject);
            UnityEngine.Object.DestroyImmediate(stats);
        }
    }

    [Test]
    public void RegisterRejectsUninitializedDuplicateIdAndOccupiedLivingCell()
    {
        var stats = CreateStats();
        var registryObject = new GameObject("Unit Registry Validation Test");
        var firstObject = new GameObject("First Unit");
        var duplicateIdObject = new GameObject("Duplicate ID Unit");
        var occupiedCellObject = new GameObject("Occupied Cell Unit");
        var uninitializedObject = new GameObject("Uninitialized Unit");

        try
        {
            var registry = registryObject.AddComponent<UnitRegistry>();
            var first = CreateUnit(firstObject, new UnitId(7), TeamId.Player, stats, new GridPosition(1, 0, 1));
            var duplicateId = CreateUnit(duplicateIdObject, new UnitId(7), TeamId.Enemy, stats, new GridPosition(2, 0, 2));
            var occupiedCell = CreateUnit(occupiedCellObject, new UnitId(8), TeamId.Enemy, stats, first.CurrentGridPosition);
            var uninitialized = uninitializedObject.AddComponent<TacticalUnit>();

            Assert.That(registry.Register(first).IsSuccess, Is.True);

            var uninitializedResult = registry.Register(uninitialized);
            var duplicateResult = registry.Register(duplicateId);
            var occupiedResult = registry.Register(occupiedCell);

            Assert.That(uninitializedResult.IsFailure, Is.True);
            Assert.That(uninitializedResult.ErrorMessage, Does.Contain("uninitialized"));
            Assert.That(duplicateResult.IsFailure, Is.True);
            Assert.That(duplicateResult.ErrorMessage, Does.Contain("already registered"));
            Assert.That(occupiedResult.IsFailure, Is.True);
            Assert.That(occupiedResult.ErrorMessage, Does.Contain("already occupied"));
            Assert.That(registry.RegisteredCount, Is.EqualTo(1));
        }
        finally
        {
            UnityEngine.Object.DestroyImmediate(uninitializedObject);
            UnityEngine.Object.DestroyImmediate(occupiedCellObject);
            UnityEngine.Object.DestroyImmediate(duplicateIdObject);
            UnityEngine.Object.DestroyImmediate(firstObject);
            UnityEngine.Object.DestroyImmediate(registryObject);
            UnityEngine.Object.DestroyImmediate(stats);
        }
    }

    [Test]
    public void OccupancyUpdatesWhenRegisteredUnitMoves()
    {
        var stats = CreateStats();
        var registryObject = new GameObject("Moving Unit Registry Test");
        var unitObject = new GameObject("Moving Unit");

        try
        {
            var registry = registryObject.AddComponent<UnitRegistry>();
            var start = new GridPosition(0, 0, 0);
            var destination = new GridPosition(3, 2, 4);
            var unit = CreateUnit(unitObject, new UnitId(3), TeamId.Player, stats, start);
            Assert.That(registry.Register(unit).IsSuccess, Is.True);

            unit.SetGridPosition(destination);

            Assert.That(registry.IsOccupied(start), Is.False);
            Assert.That(registry.TryGetOccupant(start, out var emptyOccupant), Is.False);
            Assert.That(emptyOccupant, Is.EqualTo(UnitId.None));
            Assert.That(registry.CanEnter(start), Is.True);
            Assert.That(registry.IsOccupied(destination), Is.True);
            Assert.That(registry.TryGetOccupant(destination, out var movedOccupant), Is.True);
            Assert.That(movedOccupant, Is.EqualTo(unit.UnitId));
            Assert.That(registry.GetUnitsAt(destination), Is.EquivalentTo(new[] { unit }));
        }
        finally
        {
            UnityEngine.Object.DestroyImmediate(unitObject);
            UnityEngine.Object.DestroyImmediate(registryObject);
            UnityEngine.Object.DestroyImmediate(stats);
        }
    }

    [Test]
    public void DeadUnitsRemainQueryableButDoNotBlockOccupancy()
    {
        var stats = CreateStats();
        var registryObject = new GameObject("Dead Unit Registry Test");
        var unitObject = new GameObject("Dead Unit");

        try
        {
            var registry = registryObject.AddComponent<UnitRegistry>();
            var position = new GridPosition(2, 0, 2);
            var unit = CreateUnit(unitObject, new UnitId(4), TeamId.Enemy, stats, position);
            Assert.That(registry.Register(unit).IsSuccess, Is.True);

            Assert.That(unit.ApplyDamage(unit.MaxHP, DamageSource.Environmental("Registry test")).IsSuccess, Is.True);

            Assert.That(registry.LivingCount, Is.EqualTo(0));
            Assert.That(registry.DeadCount, Is.EqualTo(1));
            Assert.That(registry.GetLivingUnits(), Is.Empty);
            Assert.That(registry.GetDeadUnits(), Is.EquivalentTo(new[] { unit }));
            Assert.That(registry.GetUnitsByAliveStatus(false), Is.EquivalentTo(new[] { unit }));
            Assert.That(registry.GetUnitsByTeam(TeamId.Enemy, livingOnly: false), Is.EquivalentTo(new[] { unit }));
            Assert.That(registry.TryGetUnit(unit.UnitId, out var foundDeadUnit), Is.True);
            Assert.That(foundDeadUnit, Is.SameAs(unit));
            Assert.That(registry.TryGetLivingUnit(unit.UnitId, out _), Is.False);
            Assert.That(registry.GetUnitsAt(position), Is.Empty);
            Assert.That(registry.GetUnitsAt(position, livingOnly: false), Is.EquivalentTo(new[] { unit }));
            Assert.That(registry.IsOccupied(position), Is.False);
            Assert.That(registry.CanEnter(position), Is.True);
        }
        finally
        {
            UnityEngine.Object.DestroyImmediate(unitObject);
            UnityEngine.Object.DestroyImmediate(registryObject);
            UnityEngine.Object.DestroyImmediate(stats);
        }
    }

    [Test]
    public void UnregisterRemovesLookupAndOccupancy()
    {
        var stats = CreateStats();
        var registryObject = new GameObject("Unregister Unit Registry Test");
        var unitObject = new GameObject("Registered Unit");

        try
        {
            var registry = registryObject.AddComponent<UnitRegistry>();
            var position = new GridPosition(4, 0, 1);
            var unit = CreateUnit(unitObject, new UnitId(5), TeamId.Player, stats, position);
            Assert.That(registry.Register(unit).IsSuccess, Is.True);

            Assert.That(registry.Unregister(unit), Is.True);

            Assert.That(registry.RegisteredCount, Is.EqualTo(0));
            Assert.That(registry.TryGetUnit(unit.UnitId, out _), Is.False);
            Assert.That(registry.IsOccupied(position), Is.False);
            Assert.That(registry.CanEnter(position), Is.True);
            Assert.That(registry.Unregister(unit), Is.False);
            Assert.That(registry.Unregister(UnitId.None), Is.False);
        }
        finally
        {
            UnityEngine.Object.DestroyImmediate(unitObject);
            UnityEngine.Object.DestroyImmediate(registryObject);
            UnityEngine.Object.DestroyImmediate(stats);
        }
    }

    [Test]
    public void RegistryOccupancyBlocksPathfindingDestinations()
    {
        var stats = CreateStats();
        var registryObject = new GameObject("Pathfinding Unit Registry Test");
        var blockerObject = new GameObject("Path Blocker Unit");

        try
        {
            var registry = registryObject.AddComponent<UnitRegistry>();
            var start = new GridPosition(0, 0, 0);
            var occupied = new GridPosition(1, 0, 0);
            var beyond = new GridPosition(2, 0, 0);
            var map = new GridMap(new GridCell(start), new GridCell(occupied), new GridCell(beyond));
            var blocker = CreateUnit(blockerObject, new UnitId(6), TeamId.Enemy, stats, occupied);
            Assert.That(registry.Register(blocker).IsSuccess, Is.True);

            var reachable = new ReachableCellSearch().FindReachableCells(map, start, 3, registry);

            Assert.That(reachable.Keys, Is.EquivalentTo(new[] { start }));
        }
        finally
        {
            UnityEngine.Object.DestroyImmediate(blockerObject);
            UnityEngine.Object.DestroyImmediate(registryObject);
            UnityEngine.Object.DestroyImmediate(stats);
        }
    }

    [Test]
    public void RegisteredUnitsAreReturnedInStableUnitIdOrder()
    {
        var stats = CreateStats();
        var registryObject = new GameObject("Ordered Unit Registry Test");
        var laterObject = new GameObject("Later Unit");
        var earlierObject = new GameObject("Earlier Unit");

        try
        {
            var registry = registryObject.AddComponent<UnitRegistry>();
            var later = CreateUnit(laterObject, new UnitId(10), TeamId.Player, stats, new GridPosition(0, 0, 0));
            var earlier = CreateUnit(earlierObject, new UnitId(2), TeamId.Enemy, stats, new GridPosition(1, 0, 0));

            Assert.That(registry.Register(later).IsSuccess, Is.True);
            Assert.That(registry.Register(earlier).IsSuccess, Is.True);

            Assert.That(registry.GetRegisteredUnits().ToArray(), Is.EqualTo(new[] { earlier, later }));
            Assert.That(registry.GetLivingUnits().ToArray(), Is.EqualTo(new[] { earlier, later }));
        }
        finally
        {
            UnityEngine.Object.DestroyImmediate(earlierObject);
            UnityEngine.Object.DestroyImmediate(laterObject);
            UnityEngine.Object.DestroyImmediate(registryObject);
            UnityEngine.Object.DestroyImmediate(stats);
        }
    }

    private static TacticalUnit CreateUnit(
        GameObject gameObject,
        UnitId unitId,
        TeamId team,
        UnitStatsDefinition stats,
        GridPosition position)
    {
        var unit = gameObject.AddComponent<TacticalUnit>();
        unit.Initialize(unitId, team, stats, position);
        return unit;
    }

    private static UnitStatsDefinition CreateStats()
    {
        var stats = ScriptableObject.CreateInstance<UnitStatsDefinition>();
        stats.Configure(
            displayName: "Registry Test Unit",
            maxHP: 10,
            maxAP: 6,
            movementAnimationSpeed: 4f,
            meleeRange: 1,
            teamColorHint: Color.white);
        return stats;
    }
}
