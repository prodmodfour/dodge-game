using NUnit.Framework;
using ReactionTactics.Grid;
using ReactionTactics.UI;
using ReactionTactics.Units;
using UnityEngine;

namespace ReactionTactics.Tests.EditMode.UI
{
    public sealed class HoverGridDebugOverlayTests
    {
        [Test]
        public void TryBuildDebugInfoReturnsTerrainAndOccupantDetails()
        {
            var position = new GridPosition(2, 1, 3);
            var map = new GridMap(new GridCell(
                position,
                walkable: false,
                blocksMovement: true,
                blocksLineOfSight: true,
                movementCost: 3,
                displayHeight: 1.5f));
            var registryObject = new GameObject("Hover Debug Test Registry");
            var unitObject = new GameObject("Hover Debug Test Unit");
            var stats = ScriptableObject.CreateInstance<UnitStatsDefinition>();

            try
            {
                stats.Configure("Hover Goblin", 8, 6, 4f, 1, Color.green);
                var unit = unitObject.AddComponent<TacticalUnit>();
                unit.Initialize(new UnitId(12), TeamId.Enemy, stats, position);

                var registry = registryObject.AddComponent<UnitRegistry>();
                var registerResult = registry.Register(unit);
                Assert.That(registerResult.IsSuccess, Is.True, registerResult.ErrorMessage);

                Assert.That(HoverGridDebugOverlay.TryBuildDebugInfo(map, registry, position, out var info), Is.True);
                Assert.That(info.Position, Is.EqualTo(position));
                Assert.That(info.Walkable, Is.False);
                Assert.That(info.BlocksMovement, Is.True);
                Assert.That(info.BlocksLineOfSight, Is.True);
                Assert.That(info.HeightY, Is.EqualTo(1));
                Assert.That(info.DisplayHeight, Is.EqualTo(1.5f));
                Assert.That(info.MovementCost, Is.EqualTo(3));
                Assert.That(info.HasOccupant, Is.True);
                Assert.That(info.OccupantName, Is.EqualTo("Hover Goblin"));
                Assert.That(info.OccupantId, Is.EqualTo(new UnitId(12)));
                Assert.That(info.OccupantTeam, Is.EqualTo("Enemy"));
                Assert.That(info.OccupantAlive, Is.True);
            }
            finally
            {
                Destroy(unitObject);
                Destroy(registryObject);
                Destroy(stats);
            }
        }

        [Test]
        public void TryBuildDebugInfoReportsNoOccupantWhenCellIsEmpty()
        {
            var position = GridPosition.Zero;
            var map = new GridMap(new GridCell(position, movementCost: 2));

            Assert.That(HoverGridDebugOverlay.TryBuildDebugInfo(map, null, position, out var info), Is.True);
            Assert.That(info.HasOccupant, Is.False);
            Assert.That(info.OccupantLabel, Is.EqualTo("None"));
            Assert.That(info.MovementCost, Is.EqualTo(2));
        }

        [Test]
        public void TryBuildDebugInfoReturnsFalseForMissingMapOrCell()
        {
            var map = new GridMap(new GridCell(GridPosition.Zero));
            var missingPosition = new GridPosition(1, 0, 0);

            Assert.That(HoverGridDebugOverlay.TryBuildDebugInfo(null, null, GridPosition.Zero, out _), Is.False);
            Assert.That(HoverGridDebugOverlay.TryBuildDebugInfo(map, null, missingPosition, out _), Is.False);
        }

        [Test]
        public void FormatInfoIncludesHoverDebugFields()
        {
            var position = new GridPosition(1, 2, 3);
            var info = new HoverGridDebugInfo(new GridCell(
                position,
                walkable: true,
                blocksMovement: false,
                blocksLineOfSight: true,
                movementCost: 4,
                displayHeight: 2f), null);

            var formatted = HoverGridDebugOverlay.FormatInfo(info);

            Assert.That(formatted, Does.Contain("Cell: (1,2,3)"));
            Assert.That(formatted, Does.Contain("Occupant: None"));
            Assert.That(formatted, Does.Contain("Walkable: True"));
            Assert.That(formatted, Does.Contain("Blocks LoS: True"));
            Assert.That(formatted, Does.Contain("Height: 2"));
            Assert.That(formatted, Does.Contain("Movement Cost: 4"));
        }

        private static void Destroy(Object value)
        {
            Object.DestroyImmediate(value);
        }
    }
}
