using System;
using NUnit.Framework;
using ReactionTactics.Turns;
using ReactionTactics.Units;
using UnityEngine;

namespace ReactionTactics.Tests.EditMode.Turns
{
    public sealed class CombatStateTests
    {
        [Test]
        public void NewStateDefaultsToNotStarted()
        {
            var state = new CombatState();

            Assert.That(state.CurrentRound, Is.EqualTo(0));
            Assert.That(state.Phase, Is.EqualTo(CombatPhase.NotStarted));
            Assert.That(state.ActiveUnit, Is.Null);
            Assert.That(state.ReactingUnit, Is.Null);
            Assert.That(state.PendingActionIntent, Is.Null);
            Assert.That(state.HasActiveUnit, Is.False);
            Assert.That(state.HasReactingUnit, Is.False);
            Assert.That(state.HasPendingActionIntent, Is.False);
            Assert.That(state.IsCombatOver, Is.False);
        }

        [Test]
        public void SetStateStoresRoundPhaseParticipantsAndPendingIntent()
        {
            var activeObject = new GameObject("Active Unit");
            var reactingObject = new GameObject("Reacting Unit");
            var pendingIntent = new object();

            try
            {
                var activeUnit = activeObject.AddComponent<TacticalUnit>();
                var reactingUnit = reactingObject.AddComponent<TacticalUnit>();
                var state = new CombatState();

                state.SetState(2, CombatPhase.ReactionWindow, activeUnit, reactingUnit, pendingIntent);

                Assert.That(state.CurrentRound, Is.EqualTo(2));
                Assert.That(state.Phase, Is.EqualTo(CombatPhase.ReactionWindow));
                Assert.That(state.ActiveUnit, Is.SameAs(activeUnit));
                Assert.That(state.ReactingUnit, Is.SameAs(reactingUnit));
                Assert.That(state.PendingActionIntent, Is.SameAs(pendingIntent));
                Assert.That(state.HasActiveUnit, Is.True);
                Assert.That(state.HasReactingUnit, Is.True);
                Assert.That(state.HasPendingActionIntent, Is.True);
                Assert.That(state.IsReactionPhase, Is.True);

                state.ClearReactingUnit();
                state.ClearPendingActionIntent();

                Assert.That(state.ReactingUnit, Is.Null);
                Assert.That(state.PendingActionIntent, Is.Null);
                Assert.That(state.HasReactingUnit, Is.False);
                Assert.That(state.HasPendingActionIntent, Is.False);
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(reactingObject);
                UnityEngine.Object.DestroyImmediate(activeObject);
            }
        }

        [Test]
        public void PhaseHelpersExposeLegalDecisionBuckets()
        {
            var state = new CombatState(1, CombatPhase.ActiveTurn);
            Assert.That(state.IsActiveUnitPhase, Is.True);
            Assert.That(state.IsReactionPhase, Is.False);
            Assert.That(state.IsResolvingAction, Is.False);

            state.SetPhase(CombatPhase.ActionTargeting);
            Assert.That(state.IsActiveUnitPhase, Is.True);

            state.SetPhase(CombatPhase.ReactionWindow);
            Assert.That(state.IsActiveUnitPhase, Is.False);
            Assert.That(state.IsReactionPhase, Is.True);

            state.SetPhase(CombatPhase.ResolvingAction);
            Assert.That(state.IsReactionPhase, Is.False);
            Assert.That(state.IsResolvingAction, Is.True);

            state.SetPhase(CombatPhase.CombatOver);
            Assert.That(state.IsResolvingAction, Is.False);
            Assert.That(state.IsCombatOver, Is.True);
        }

        [Test]
        public void CombatStateRejectsInvalidRoundAndPhase()
        {
            Assert.Throws<ArgumentOutOfRangeException>(() => new CombatState(-1, CombatPhase.NotStarted));
            Assert.Throws<ArgumentOutOfRangeException>(() => new CombatState(0, (CombatPhase)99));

            var state = new CombatState();
            Assert.Throws<ArgumentOutOfRangeException>(() => state.SetCurrentRound(-1));
            Assert.Throws<ArgumentOutOfRangeException>(() => state.SetPhase((CombatPhase)99));
        }
    }
}
