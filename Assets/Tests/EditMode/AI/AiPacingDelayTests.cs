using System;
using NUnit.Framework;
using ReactionTactics.AI;
using UnityEngine;

namespace ReactionTactics.Tests.EditMode.AI
{
    public sealed class AiPacingDelayTests
    {
        [Test]
        public void DecisionDelayDefaultsToShortReadablePlayModePacing()
        {
            var controller = CreateController();
            try
            {
                Assert.That(controller.DecisionDelaySeconds, Is.EqualTo(AiController.DefaultDecisionDelaySeconds));
                Assert.That(controller.DecisionDelaySeconds, Is.GreaterThan(0f));
                Assert.That(controller.DecisionDelaySeconds, Is.LessThanOrEqualTo(1f));
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(controller.gameObject);
            }
        }

        [Test]
        public void DecisionPacingIsSkippedOutsidePlayModeForEditModeTests()
        {
            var controller = CreateController();
            try
            {
                controller.DecisionDelaySeconds = 0.25f;

                Assert.That(Application.isPlaying, Is.False);
                Assert.That(controller.ShouldUseDecisionPacing, Is.False);
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(controller.gameObject);
            }
        }

        [Test]
        public void DecisionDelayCanBeExplicitlySkippedForScriptedValidation()
        {
            var controller = CreateController();
            try
            {
                controller.DecisionDelaySeconds = 0.25f;
                controller.SkipDecisionPacing = true;

                Assert.That(controller.SkipDecisionPacing, Is.True);
                Assert.That(controller.ShouldUseDecisionPacing, Is.False);
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(controller.gameObject);
            }
        }

        [Test]
        public void DecisionDelayRejectsNegativeValues()
        {
            var controller = CreateController();
            try
            {
                Assert.Throws<ArgumentOutOfRangeException>(() => controller.DecisionDelaySeconds = -0.01f);
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(controller.gameObject);
            }
        }

        private static AiController CreateController()
        {
            return new GameObject("AI Pacing Delay Test Controller").AddComponent<AiController>();
        }
    }
}
