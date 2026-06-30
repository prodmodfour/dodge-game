using NUnit.Framework;
using ReactionTactics.Actions;
using ReactionTactics.Grid;
using ReactionTactics.Turns;
using ReactionTactics.UI;
using ReactionTactics.Units;
using UnityEngine;

namespace ReactionTactics.Tests.EditMode.UI
{
    public sealed class CombatHudTests
    {
        [Test]
        public void FormatStateIncludesRoundPhaseUnitsAndPendingAction()
        {
            var activeObject = new GameObject("Combat HUD Active Unit");
            var reactorObject = new GameObject("Combat HUD Reactor Unit");
            var activeStats = ScriptableObject.CreateInstance<UnitStatsDefinition>();
            var reactorStats = ScriptableObject.CreateInstance<UnitStatsDefinition>();
            var ability = ScriptableObject.CreateInstance<AbilityDefinition>();

            try
            {
                activeStats.Configure("Knight", 12, 6, 4f, 1, Color.blue);
                reactorStats.Configure("Rogue", 10, 7, 4f, 1, Color.cyan);
                ability.Configure(
                    "melee_slash",
                    "Melee Slash",
                    3,
                    AbilityUsage.Action,
                    AbilityTiming.Telegraphed,
                    AbilityShape.Melee,
                    1,
                    0,
                    4,
                    triggersReactions: true);

                var activeUnit = activeObject.AddComponent<TacticalUnit>();
                activeUnit.Initialize(new UnitId(1), TeamId.Player, activeStats, GridPosition.Zero);

                var reactor = reactorObject.AddComponent<TacticalUnit>();
                reactor.Initialize(new UnitId(2), TeamId.Enemy, reactorStats, new GridPosition(1, 0, 0));

                var intent = new ActionIntent(
                    activeUnit,
                    ability,
                    activeUnit.CurrentGridPosition,
                    ActionTarget.ForUnit(reactor),
                    new[] { reactor.CurrentGridPosition },
                    declarationRound: 2,
                    declarationSequence: 4);
                var state = new CombatState(2, CombatPhase.ReactionWindow, activeUnit, reactor, intent);

                var formatted = CombatHud.FormatState(state);

                Assert.That(formatted, Does.Contain("Round: 2"));
                Assert.That(formatted, Does.Contain("Phase: ReactionWindow"));
                Assert.That(formatted, Does.Contain("Active Unit: Knight"));
                Assert.That(formatted, Does.Contain("Current Reactor: Rogue"));
                Assert.That(formatted, Does.Contain("Pending Action: Melee Slash by Knight"));
            }
            finally
            {
                Destroy(ability);
                Destroy(reactorStats);
                Destroy(activeStats);
                Destroy(reactorObject);
                Destroy(activeObject);
            }
        }

        [Test]
        public void FormatStateReportsMissingCombatStateSafely()
        {
            var formatted = CombatHud.FormatState(null);

            Assert.That(formatted, Does.Contain("Round: --"));
            Assert.That(formatted, Does.Contain("Phase: No combat state"));
            Assert.That(formatted, Does.Contain("Active Unit: None"));
            Assert.That(formatted, Does.Contain("Current Reactor: None"));
            Assert.That(formatted, Does.Contain("Pending Action: None"));
        }

        private static void Destroy(Object value)
        {
            Object.DestroyImmediate(value);
        }
    }
}
