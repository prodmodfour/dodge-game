using System;
using System.Collections.Generic;
using NUnit.Framework;
using ReactionTactics.Actions;
using ReactionTactics.Grid;
using ReactionTactics.Reactions;
using ReactionTactics.Units;
using UnityEngine;

namespace ReactionTactics.Tests.EditMode.Reactions
{
    public sealed class ReactionWindowTests
    {
        [Test]
        public void ConstructorStoresSourceIntentDeclarationPositionAndOrderedReactors()
        {
            using (var battle = new ReactionWindowTestBattle())
            {
                var reactors = new List<TacticalUnit> { battle.NearReactor, battle.FarReactor };

                var window = new ReactionWindow(battle.Intent, reactors);
                battle.Actor.SetGridPosition(new GridPosition(7, 0, 7));
                reactors[0] = battle.FarReactor;
                reactors.Add(battle.Actor);

                Assert.That(window.SourceIntent, Is.SameAs(battle.Intent));
                Assert.That(window.ActionActorPositionAtDeclaration, Is.EqualTo(battle.ActorPosition));
                Assert.That(window.OrderedReactors, Is.EqualTo(new[] { battle.NearReactor, battle.FarReactor }));
                Assert.That(window.Phase, Is.EqualTo(ReactionWindowPhase.NotOpened));
                Assert.That(window.CurrentIndex, Is.EqualTo(-1));
                Assert.That(window.CurrentReactor, Is.Null);
                Assert.That(window.CompletedReactors, Is.Empty);
                Assert.That(window.SkippedReactors, Is.Empty);
                Assert.That(window.ReactorCount, Is.EqualTo(2));
                Assert.That(window.HasRemainingReactors, Is.True);
            }
        }

        [Test]
        public void LifecycleTracksOpenedStartedCompletedSkippedAndClosedPhases()
        {
            using (var battle = new ReactionWindowTestBattle())
            {
                var window = new ReactionWindow(
                    battle.Intent,
                    new[] { battle.NearReactor, battle.FarReactor });

                window.Open();

                Assert.That(window.Phase, Is.EqualTo(ReactionWindowPhase.Opened));
                Assert.That(window.IsOpen, Is.True);

                Assert.That(window.TryStartNextReactor(out var firstReactor), Is.True);
                Assert.That(firstReactor, Is.SameAs(battle.NearReactor));
                Assert.That(window.Phase, Is.EqualTo(ReactionWindowPhase.ReactorStarted));
                Assert.That(window.CurrentIndex, Is.EqualTo(0));
                Assert.That(window.CurrentReactor, Is.SameAs(battle.NearReactor));

                window.CompleteCurrentReactor();

                Assert.That(window.Phase, Is.EqualTo(ReactionWindowPhase.ReactorCompleted));
                Assert.That(window.CurrentReactor, Is.Null);
                Assert.That(window.CompletedReactors, Is.EqualTo(new[] { battle.NearReactor }));
                Assert.That(window.RemainingReactorCount, Is.EqualTo(1));

                Assert.That(window.TryStartNextReactor(out var secondReactor), Is.True);
                Assert.That(secondReactor, Is.SameAs(battle.FarReactor));

                window.SkipCurrentReactor();

                Assert.That(window.SkippedReactors, Is.EqualTo(new[] { battle.FarReactor }));
                Assert.That(window.ProcessedReactorCount, Is.EqualTo(2));
                Assert.That(window.HasRemainingReactors, Is.False);
                Assert.That(window.HasProcessedAllReactors, Is.True);
                Assert.That(window.TryStartNextReactor(out var noReactor), Is.False);
                Assert.That(noReactor, Is.Null);

                window.Close();

                Assert.That(window.Phase, Is.EqualTo(ReactionWindowPhase.Closed));
                Assert.That(window.IsClosed, Is.True);
                Assert.That(window.IsOpen, Is.False);
            }
        }

        [Test]
        public void EmptyReactionWindowCanOpenAndCloseWithoutStartingAReactor()
        {
            using (var battle = new ReactionWindowTestBattle())
            {
                var window = new ReactionWindow(battle.Intent, Array.Empty<TacticalUnit>());

                window.Open();

                Assert.That(window.TryStartNextReactor(out var reactor), Is.False);
                Assert.That(reactor, Is.Null);
                Assert.That(window.HasProcessedAllReactors, Is.True);

                window.Close();

                Assert.That(window.Phase, Is.EqualTo(ReactionWindowPhase.Closed));
            }
        }

        [Test]
        public void ConstructorRejectsMissingSourceNullReactorsAndDuplicateReactors()
        {
            using (var battle = new ReactionWindowTestBattle())
            {
                Assert.Throws<ArgumentNullException>(() => new ReactionWindow(null, Array.Empty<TacticalUnit>()));
                Assert.Throws<ArgumentException>(() => new ReactionWindow(battle.Intent, new TacticalUnit[] { null }));
                Assert.Throws<ArgumentException>(() => new ReactionWindow(
                    battle.Intent,
                    new[] { battle.NearReactor, battle.NearReactor }));
            }
        }

        [Test]
        public void InvalidLifecycleTransitionsThrowClearExceptions()
        {
            using (var battle = new ReactionWindowTestBattle())
            {
                var window = new ReactionWindow(
                    battle.Intent,
                    new[] { battle.NearReactor, battle.FarReactor });

                Assert.Throws<InvalidOperationException>(() => window.TryStartNextReactor(out _));
                Assert.Throws<InvalidOperationException>(() => window.CompleteCurrentReactor());
                Assert.Throws<InvalidOperationException>(() => window.Close());

                window.Open();
                Assert.Throws<InvalidOperationException>(() => window.Open());
                Assert.Throws<InvalidOperationException>(() => window.Close());

                Assert.That(window.TryStartNextReactor(out _), Is.True);
                Assert.Throws<InvalidOperationException>(() => window.TryStartNextReactor(out _));
                Assert.Throws<InvalidOperationException>(() => window.Close());

                window.CompleteCurrentReactor();
                Assert.Throws<InvalidOperationException>(() => window.CompleteCurrentReactor());
            }
        }

        private sealed class ReactionWindowTestBattle : IDisposable
        {
            private readonly GameObject actorObject;
            private readonly GameObject nearReactorObject;
            private readonly GameObject farReactorObject;
            private readonly UnitStatsDefinition actorStats;
            private readonly UnitStatsDefinition nearStats;
            private readonly UnitStatsDefinition farStats;
            private readonly AbilityDefinition ability;

            public ReactionWindowTestBattle()
            {
                ActorPosition = new GridPosition(2, 0, 2);
                actorObject = new GameObject("Reaction Window Actor");
                nearReactorObject = new GameObject("Near Reactor");
                farReactorObject = new GameObject("Far Reactor");
                actorStats = CreateStats("Actor");
                nearStats = CreateStats("Near Reactor");
                farStats = CreateStats("Far Reactor");
                ability = CreateAbility();

                Actor = actorObject.AddComponent<TacticalUnit>();
                NearReactor = nearReactorObject.AddComponent<TacticalUnit>();
                FarReactor = farReactorObject.AddComponent<TacticalUnit>();

                Actor.Initialize(new UnitId(1), TeamId.Player, actorStats, ActorPosition);
                NearReactor.Initialize(new UnitId(2), TeamId.Enemy, nearStats, new GridPosition(3, 0, 2));
                FarReactor.Initialize(new UnitId(3), TeamId.Enemy, farStats, new GridPosition(6, 0, 2));

                Intent = new ActionIntent(
                    Actor,
                    ability,
                    ActorPosition,
                    ActionTarget.ForUnit(NearReactor),
                    new[] { NearReactor.CurrentGridPosition },
                    declarationRound: 1,
                    declarationSequence: 0);
            }

            public TacticalUnit Actor { get; }

            public TacticalUnit NearReactor { get; }

            public TacticalUnit FarReactor { get; }

            public GridPosition ActorPosition { get; }

            public ActionIntent Intent { get; }

            public void Dispose()
            {
                Destroy(ability);
                Destroy(farStats);
                Destroy(nearStats);
                Destroy(actorStats);
                Destroy(farReactorObject);
                Destroy(nearReactorObject);
                Destroy(actorObject);
            }

            private static UnitStatsDefinition CreateStats(string displayName)
            {
                var stats = ScriptableObject.CreateInstance<UnitStatsDefinition>();
                stats.Configure(
                    displayName,
                    maxHP: 10,
                    maxAP: 6,
                    movementAnimationSpeed: 4f,
                    meleeRange: 1,
                    teamColorHint: Color.white);
                return stats;
            }

            private static AbilityDefinition CreateAbility()
            {
                var ability = ScriptableObject.CreateInstance<AbilityDefinition>();
                ability.Configure(
                    "melee_slash",
                    "Melee Slash",
                    apCost: 3,
                    usage: AbilityUsage.Action,
                    timing: AbilityTiming.Telegraphed,
                    shape: AbilityShape.Melee,
                    range: 1,
                    radius: 0,
                    damage: 4,
                    triggersReactions: true);
                return ability;
            }

            private static void Destroy(UnityEngine.Object unityObject)
            {
                if (unityObject != null)
                {
                    UnityEngine.Object.DestroyImmediate(unityObject);
                }
            }
        }
    }
}
