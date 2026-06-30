using NUnit.Framework;
using ReactionTactics.Grid;
using ReactionTactics.Units;
using UnityEngine;

namespace ReactionTactics.Tests.EditMode.Units
{
    public sealed class UnitHighlightViewTests
    {
        [Test]
        public void ActiveAndReactionMarkersCanBeControlledSeparately()
        {
            var unitObject = new GameObject("Highlighted Reactor Unit");
            var stats = ScriptableObject.CreateInstance<UnitStatsDefinition>();

            try
            {
                stats.Configure("Highlighted Unit", 10, 6, 4f, 1, Color.white);
                var unit = unitObject.AddComponent<TacticalUnit>();
                unit.Initialize(new UnitId(21), TeamId.Player, stats, GridPosition.Zero);
                var highlight = unitObject.AddComponent<UnitHighlightView>();

                highlight.SetActiveHighlight(true);
                highlight.SetReactionHighlight(true);

                Assert.That(highlight.IsHighlighted, Is.True);
                Assert.That(highlight.IsReactionHighlighted, Is.True);

                highlight.SetActiveHighlight(false);

                Assert.That(highlight.IsHighlighted, Is.False);
                Assert.That(highlight.IsReactionHighlighted, Is.True);

                highlight.SetReactionHighlight(false);

                Assert.That(highlight.IsReactionHighlighted, Is.False);
            }
            finally
            {
                Object.DestroyImmediate(unitObject);
                Object.DestroyImmediate(stats);
            }
        }
    }
}
