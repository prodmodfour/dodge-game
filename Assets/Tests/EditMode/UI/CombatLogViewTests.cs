using NUnit.Framework;
using ReactionTactics.Turns;
using ReactionTactics.UI;
using UnityEngine;

namespace ReactionTactics.Tests.EditMode.UI
{
    public sealed class CombatLogViewTests
    {
        [Test]
        public void AddEntryRetainsNewestEntriesAndFormatsPlainLanguage()
        {
            var viewObject = new GameObject("Combat Log View Test");

            try
            {
                var view = viewObject.AddComponent<CombatLogView>();
                view.MaxEntries = 2;

                view.AddEntry(new CombatLogEntry(1, "Round 1 started.", 0f));
                view.AddEntry(new CombatLogEntry(2, "Rogue can react to Melee Slash.", 0f));
                view.AddEntry(new CombatLogEntry(3, "Rogue avoided Melee Slash by moving out of range.", 0f));

                Assert.That(view.Entries.Count, Is.EqualTo(2));
                Assert.That(view.Entries[0].Message, Is.EqualTo("Rogue can react to Melee Slash."));
                Assert.That(view.Entries[1].Message, Is.EqualTo("Rogue avoided Melee Slash by moving out of range."));
                Assert.That(
                    CombatLogView.FormatEntry(view.Entries[1], includeSequenceNumber: true),
                    Is.EqualTo("#03 Rogue avoided Melee Slash by moving out of range."));
                Assert.That(
                    CombatLogView.FormatEntry(view.Entries[1], includeSequenceNumber: false),
                    Is.EqualTo("Rogue avoided Melee Slash by moving out of range."));
            }
            finally
            {
                Object.DestroyImmediate(viewObject);
            }
        }

        [Test]
        public void ConfiguredViewSubscribesToCombatLogEvents()
        {
            var busObject = new GameObject("Combat Log Event Bus Test");
            var viewObject = new GameObject("Combat Log Event View Test");

            try
            {
                var bus = busObject.AddComponent<CombatEventBus>();
                var view = viewObject.AddComponent<CombatLogView>();
                view.Configure(bus);

                bus.PublishCombatLog("Knight declared Melee Slash. Reactions resolve before it hits.");

                Assert.That(view.Entries.Count, Is.EqualTo(1));
                Assert.That(view.Entries[0].SequenceNumber, Is.EqualTo(1));
                Assert.That(view.Entries[0].Message, Is.EqualTo("Knight declared Melee Slash. Reactions resolve before it hits."));
            }
            finally
            {
                Object.DestroyImmediate(viewObject);
                Object.DestroyImmediate(busObject);
            }
        }
    }
}
