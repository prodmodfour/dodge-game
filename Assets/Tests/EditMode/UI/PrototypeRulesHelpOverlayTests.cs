using NUnit.Framework;
using ReactionTactics.UI;
using UnityEngine;

namespace ReactionTactics.Tests.EditMode.UI
{
    public sealed class PrototypeRulesHelpOverlayTests
    {
        [Test]
        public void RulesTextIncludesRequiredPrototypeMechanics()
        {
            var rulesText = PrototypeRulesHelpOverlay.BuildRulesText();

            Assert.That(rulesText, Does.Contain("Actions only on your turn"));
            Assert.That(rulesText, Does.Contain("Reactions only off-turn"));
            Assert.That(rulesText, Does.Contain("ordered by distance"));
            Assert.That(rulesText, Does.Contain("Melee hits only if the target remains in range"));
            Assert.That(rulesText, Does.Contain("Move out of cones or AoEs"));
            Assert.That(rulesText, Does.Contain("No dodge chance"));
            Assert.That(rulesText, Does.Contain("accuracy roll"));
        }

        [Test]
        public void ToggleFlipsOverlayVisibility()
        {
            var overlayObject = new GameObject("Prototype Rules Help Overlay Test");

            try
            {
                var overlay = overlayObject.AddComponent<PrototypeRulesHelpOverlay>();
                overlay.Visible = true;

                overlay.Toggle();
                Assert.That(overlay.Visible, Is.False);

                overlay.Toggle();
                Assert.That(overlay.Visible, Is.True);
            }
            finally
            {
                Object.DestroyImmediate(overlayObject);
            }
        }

        [Test]
        public void ShortcutHintExplainsHiddenAndVisibleStates()
        {
            Assert.That(
                PrototypeRulesHelpOverlay.FormatShortcutHint(KeyCode.H, isVisible: true),
                Is.EqualTo("Press H to hide these rules."));
            Assert.That(
                PrototypeRulesHelpOverlay.FormatShortcutHint(KeyCode.H, isVisible: false),
                Is.EqualTo("Press H for rules."));
        }
    }
}
