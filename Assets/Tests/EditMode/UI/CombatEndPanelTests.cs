using NUnit.Framework;
using ReactionTactics.Turns;
using ReactionTactics.UI;
using ReactionTactics.Units;
using UnityEngine;

namespace ReactionTactics.Tests.EditMode.UI
{
    public sealed class CombatEndPanelTests
    {
        [Test]
        public void FormatOutcomeReportsVictoryForPlayerWinner()
        {
            var text = CombatEndPanel.FormatOutcome(
                hasWinningTeam: true,
                winningTeam: TeamId.Player,
                playerTeam: TeamId.Player);

            Assert.That(text, Does.Contain("Victory"));
            Assert.That(text, Does.Contain("Player team wins"));
        }

        [Test]
        public void FormatOutcomeReportsDefeatWhenEnemyWins()
        {
            var text = CombatEndPanel.FormatOutcome(
                hasWinningTeam: true,
                winningTeam: TeamId.Enemy,
                playerTeam: TeamId.Player);

            Assert.That(text, Does.Contain("Defeat"));
            Assert.That(text, Does.Contain("Enemy team wins"));
        }

        [Test]
        public void ConfiguredPanelCachesCombatEndedEvents()
        {
            var busObject = new GameObject("Combat End Panel Bus Test");
            var panelObject = new GameObject("Combat End Panel Test");

            try
            {
                var bus = busObject.AddComponent<CombatEventBus>();
                var panel = panelObject.AddComponent<CombatEndPanel>();
                panel.Configure(null, bus);

                bus.PublishCombatEnded(TeamId.Player);

                Assert.That(panel.HasCachedEndEvent, Is.True);
                Assert.That(panel.GetDisplayText(), Does.Contain("Victory"));
            }
            finally
            {
                Object.DestroyImmediate(panelObject);
                Object.DestroyImmediate(busObject);
            }
        }
    }
}
