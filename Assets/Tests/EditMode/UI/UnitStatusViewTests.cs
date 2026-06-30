using NUnit.Framework;
using ReactionTactics.Grid;
using ReactionTactics.Turns;
using ReactionTactics.UI;
using ReactionTactics.Units;
using UnityEngine;

namespace ReactionTactics.Tests.EditMode.UI
{
    public sealed class UnitStatusViewTests
    {
        [Test]
        public void FormatStatusIncludesNameTeamHitPointsAndActionPoints()
        {
            var unitObject = new GameObject("Status Format Unit");
            var stats = ScriptableObject.CreateInstance<UnitStatsDefinition>();

            try
            {
                var unit = CreateInitializedUnit(unitObject, stats, "Status Knight", new UnitId(7), TeamId.Player, 12, 6);

                var status = UnitStatusView.FormatStatus(unit);

                Assert.That(status, Does.Contain("Status Knight Unit#7 [Player]"));
                Assert.That(status, Does.Contain("HP 12/12"));
                Assert.That(status, Does.Contain("AP 6/6"));
            }
            finally
            {
                Destroy(unitObject);
                Destroy(stats);
            }
        }

        [Test]
        public void ActionPointEventRefreshesCachedStatusForTheRepresentedUnitOnly()
        {
            var busObject = new GameObject("Status Event Bus");
            var unitObject = new GameObject("Status AP Unit");
            var otherObject = new GameObject("Other Status AP Unit");
            var stats = ScriptableObject.CreateInstance<UnitStatsDefinition>();
            var otherStats = ScriptableObject.CreateInstance<UnitStatsDefinition>();

            try
            {
                var bus = busObject.AddComponent<CombatEventBus>();
                var unit = CreateInitializedUnit(unitObject, stats, "Status Rogue", new UnitId(8), TeamId.Player, 10, 7);
                var other = CreateInitializedUnit(otherObject, otherStats, "Status Goblin", new UnitId(9), TeamId.Enemy, 8, 6);
                var view = unitObject.AddComponent<UnitStatusView>();
                view.Configure(unit, bus);

                var previousOtherAP = other.CurrentAP;
                Assert.That(other.SpendAP(3).IsSuccess, Is.True);
                bus.PublishActionPointsChanged(other, previousOtherAP, other.CurrentAP);

                Assert.That(view.StatusText, Does.Contain("AP 7/7"));

                var previousAP = unit.CurrentAP;
                Assert.That(unit.SpendAP(2).IsSuccess, Is.True);
                bus.PublishActionPointsChanged(unit, previousAP, unit.CurrentAP);

                Assert.That(view.StatusText, Does.Contain("Status Rogue"));
                Assert.That(view.StatusText, Does.Contain("AP 5/7"));
            }
            finally
            {
                Destroy(otherObject);
                Destroy(unitObject);
                Destroy(busObject);
                Destroy(otherStats);
                Destroy(stats);
            }
        }

        [Test]
        public void DefeatedUnitIsMarkedClearlyInStatusText()
        {
            var busObject = new GameObject("Status Death Event Bus");
            var unitObject = new GameObject("Status Death Unit");
            var stats = ScriptableObject.CreateInstance<UnitStatsDefinition>();

            try
            {
                var bus = busObject.AddComponent<CombatEventBus>();
                var unit = CreateInitializedUnit(unitObject, stats, "Status Shaman", new UnitId(10), TeamId.Enemy, 5, 6);
                var view = unitObject.AddComponent<UnitStatusView>();
                view.Configure(unit, bus);

                var previousHP = unit.CurrentHP;
                var damageSource = DamageSource.Environmental("unit status test");
                Assert.That(unit.ApplyDamage(5, damageSource).IsSuccess, Is.True);
                bus.PublishHitPointsChanged(unit, previousHP, unit.CurrentHP, damageSource);
                bus.PublishUnitDied(unit, damageSource);

                Assert.That(view.IsMarkedDead, Is.True);
                Assert.That(view.StatusText, Does.Contain("DEFEATED"));
                Assert.That(view.StatusText, Does.Contain("HP 0/5"));
                Assert.That(view.StatusText, Does.Contain("AP 6/6"));
            }
            finally
            {
                Destroy(unitObject);
                Destroy(busObject);
                Destroy(stats);
            }
        }

        private static TacticalUnit CreateInitializedUnit(
            GameObject unitObject,
            UnitStatsDefinition stats,
            string displayName,
            UnitId unitId,
            TeamId team,
            int maxHP,
            int maxAP)
        {
            stats.Configure(displayName, maxHP, maxAP, 4f, 1, team == TeamId.Player ? Color.blue : Color.red);
            var unit = unitObject.AddComponent<TacticalUnit>();
            unit.Initialize(unitId, team, stats, GridPosition.Zero);
            return unit;
        }

        private static void Destroy(Object value)
        {
            Object.DestroyImmediate(value);
        }
    }
}
