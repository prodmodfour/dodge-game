using NUnit.Framework;
using ReactionTactics.AI;
using ReactionTactics.Input;
using ReactionTactics.Turns;
using UnityEngine;

namespace ReactionTactics.Tests.EditMode
{
    public sealed class CleanupDefaultsTests
    {
        [Test]
        public void ConsoleDebugLogsAreOptInForPrototypeRuntimeComponents()
        {
            var gameObject = new GameObject("Prototype Cleanup Defaults Test");

            try
            {
                var combatManager = gameObject.AddComponent<CombatManager>();
                var gridPicker = gameObject.AddComponent<GridPicker>();
                var aiController = gameObject.AddComponent<AiController>();

                Assert.That(combatManager.LogCombatStart, Is.False);
                Assert.That(combatManager.LogActionFlow, Is.False);
                Assert.That(gridPicker.LogClickedCells, Is.False);
                Assert.That(gridPicker.LogClickedUnits, Is.False);
                Assert.That(aiController.LogDecisions, Is.False);
            }
            finally
            {
                Object.DestroyImmediate(gameObject);
            }
        }
    }
}
