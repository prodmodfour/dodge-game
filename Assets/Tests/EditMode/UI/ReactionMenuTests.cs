using System;
using System.Collections.Generic;
using NUnit.Framework;
using ReactionTactics.Actions;
using ReactionTactics.Grid;
using ReactionTactics.Input;
using ReactionTactics.Turns;
using ReactionTactics.UI;
using ReactionTactics.Units;
using UnityEngine;

namespace ReactionTactics.Tests.EditMode.UI
{
    public sealed class ReactionMenuTests
    {
        [Test]
        public void BuildMenuEntriesListsCurrentReactorOptionsWithApCosts()
        {
            using (var fixture = new Fixture())
            {
                var actor = fixture.CreateUnit("Reaction Menu Actor", new UnitId(1), TeamId.Player, GridPosition.Zero);
                var reactor = fixture.CreateUnit("Reaction Menu Reactor", new UnitId(2), TeamId.Enemy, GridPosition.East);
                fixture.AssignLoadout(reactor, fixture.Move, fixture.Brace, fixture.PassReaction);
                var combatState = fixture.CreateReactionState(actor, reactor);

                var entries = ReactionMenu.BuildMenuEntries(combatState);

                Assert.That(entries.Count, Is.EqualTo(3));
                AssertEntry(entries[0], ReactionMenuEntryKind.ReactionMove, "Reaction Move (path AP)", 0, true);
                AssertEntry(entries[1], ReactionMenuEntryKind.Brace, "Brace (2 AP)", 2, true);
                AssertEntry(entries[2], ReactionMenuEntryKind.Pass, "Pass Reaction (0 AP)", 0, true);
            }
        }

        [Test]
        public void BuildMenuEntriesDisableInsufficientApOptionsButKeepPassAvailable()
        {
            using (var fixture = new Fixture())
            {
                var actor = fixture.CreateUnit("Low AP Reaction Actor", new UnitId(3), TeamId.Player, GridPosition.Zero);
                var reactor = fixture.CreateUnit("Low AP Reaction Reactor", new UnitId(4), TeamId.Enemy, GridPosition.East);
                fixture.AssignLoadout(reactor, fixture.Move, fixture.Brace, fixture.PassReaction);
                reactor.SetAPForTest(0);
                var combatState = fixture.CreateReactionState(actor, reactor);

                var entries = ReactionMenu.BuildMenuEntries(combatState);

                Assert.That(entries[0].Kind, Is.EqualTo(ReactionMenuEntryKind.ReactionMove));
                Assert.That(entries[0].IsEnabled, Is.False);
                Assert.That(entries[0].DisabledReason, Does.Contain("requires AP"));
                Assert.That(entries[1].Kind, Is.EqualTo(ReactionMenuEntryKind.Brace));
                Assert.That(entries[1].IsEnabled, Is.False);
                Assert.That(entries[1].DisabledReason, Does.Contain("Needs 2 AP"));
                Assert.That(entries[2].Kind, Is.EqualTo(ReactionMenuEntryKind.Pass));
                Assert.That(entries[2].IsEnabled, Is.True);
            }
        }

        [Test]
        public void BuildMenuEntriesDisableUnavailableReactionAbilities()
        {
            using (var fixture = new Fixture())
            {
                var actor = fixture.CreateUnit("Missing Ability Actor", new UnitId(5), TeamId.Player, GridPosition.Zero);
                var reactor = fixture.CreateUnit("Missing Ability Reactor", new UnitId(6), TeamId.Enemy, GridPosition.East);
                fixture.AssignLoadout(reactor, fixture.PassReaction);
                var combatState = fixture.CreateReactionState(actor, reactor);

                var entries = ReactionMenu.BuildMenuEntries(combatState);

                Assert.That(entries[0].IsEnabled, Is.False);
                Assert.That(entries[0].DisabledReason, Does.Contain("No reaction movement ability"));
                Assert.That(entries[1].IsEnabled, Is.False);
                Assert.That(entries[1].DisabledReason, Does.Contain("No Brace reaction ability"));
                Assert.That(entries[2].IsEnabled, Is.True);
            }
        }

        [Test]
        public void BuildMenuEntriesDisableAllOptionsOutsideReactionWindow()
        {
            using (var fixture = new Fixture())
            {
                var actor = fixture.CreateUnit("Active Actor", new UnitId(7), TeamId.Player, GridPosition.Zero);
                fixture.AssignLoadout(actor, fixture.Move, fixture.Brace, fixture.PassReaction);
                var combatState = new CombatState(1, CombatPhase.ActiveTurn, actor);

                var entries = ReactionMenu.BuildMenuEntries(actor, combatState);

                Assert.That(entries.Count, Is.EqualTo(3));
                for (var i = 0; i < entries.Count; i += 1)
                {
                    Assert.That(entries[i].IsEnabled, Is.False);
                    Assert.That(entries[i].DisabledReason, Does.Contain(nameof(CombatPhase.ActiveTurn)));
                }
            }
        }

        [Test]
        public void FormatReactionSummaryReportsPendingActionActorReactorAndAp()
        {
            using (var fixture = new Fixture())
            {
                var actor = fixture.CreateUnit("Summary Actor", new UnitId(8), TeamId.Player, GridPosition.Zero);
                var reactor = fixture.CreateUnit("Summary Reactor", new UnitId(9), TeamId.Enemy, GridPosition.East);
                reactor.SetAPForTest(4);
                var combatState = fixture.CreateReactionState(actor, reactor);

                var summary = ReactionMenu.FormatReactionSummary(combatState);

                Assert.That(summary, Does.Contain("Phase: ReactionWindow"));
                Assert.That(summary, Does.Contain("Pending: Melee Slash"));
                Assert.That(summary, Does.Contain("Actor: Reaction Menu Test Unit"));
                Assert.That(summary, Does.Contain("Reactor: Reaction Menu Test Unit"));
                Assert.That(summary, Does.Contain("AP: 4/6"));
                Assert.That(summary, Does.Contain("Selected Reaction: None"));
            }
        }

        [Test]
        public void FormatReactionSummaryReportsSelectedReactionCostAndTarget()
        {
            using (var fixture = new Fixture())
            {
                var actor = fixture.CreateUnit("Selected Reaction Actor", new UnitId(10), TeamId.Player, GridPosition.Zero);
                var reactor = fixture.CreateUnit("Selected Reaction Reactor", new UnitId(11), TeamId.Enemy, GridPosition.East);
                fixture.AssignLoadout(reactor, fixture.Move, fixture.Brace, fixture.PassReaction);
                var targetCell = new GridPosition(2, 0, 0);
                var combatState = fixture.CreateReactionState(actor, reactor);
                var selectionState = new SelectionState(reactor, false, GridPosition.Zero, SelectionActionMode.Move, SelectionTarget.ForCell(targetCell));

                var summary = ReactionMenu.FormatReactionSummary(combatState, selectionState);

                Assert.That(summary, Does.Contain("Selected Reaction: Reaction Move (path AP)"));
                Assert.That(summary, Does.Contain("Target: Cell (2,0,0)"));
            }
        }

        private static void AssertEntry(
            ReactionMenuEntry entry,
            ReactionMenuEntryKind expectedKind,
            string expectedLabel,
            int expectedApCost,
            bool expectedEnabled)
        {
            Assert.That(entry.Kind, Is.EqualTo(expectedKind));
            Assert.That(entry.Label, Is.EqualTo(expectedLabel));
            Assert.That(entry.APCost, Is.EqualTo(expectedApCost));
            Assert.That(entry.IsEnabled, Is.EqualTo(expectedEnabled));
            if (expectedEnabled)
            {
                Assert.That(entry.DisabledReason, Is.Empty);
            }
        }

        private sealed class Fixture : IDisposable
        {
            private readonly List<GameObject> gameObjects = new List<GameObject>();
            private readonly List<ScriptableObject> assets = new List<ScriptableObject>();
            private readonly UnitStatsDefinition stats;

            public Fixture()
            {
                stats = ScriptableObject.CreateInstance<UnitStatsDefinition>();
                assets.Add(stats);
                stats.Configure("Reaction Menu Test Unit", 10, 6, 4f, 1, Color.white);

                Move = CreateAbility("move", "Move", 0, AbilityUsage.Both, AbilityTiming.Immediate, AbilityShape.Self, 0, 0, 0, false);
                Brace = CreateAbility("brace", "Brace", 2, AbilityUsage.Reaction, AbilityTiming.Immediate, AbilityShape.Self, 0, 0, 0, false);
                PassReaction = CreateAbility("pass_reaction", "Pass Reaction", 0, AbilityUsage.Reaction, AbilityTiming.Immediate, AbilityShape.Self, 0, 0, 0, false);
                MeleeSlash = CreateAbility("melee_slash", "Melee Slash", 3, AbilityUsage.Action, AbilityTiming.Telegraphed, AbilityShape.Melee, 0, 0, 4, true);
            }

            public AbilityDefinition Move { get; }

            public AbilityDefinition Brace { get; }

            public AbilityDefinition PassReaction { get; }

            public AbilityDefinition MeleeSlash { get; }

            public TacticalUnit CreateUnit(string name, UnitId unitId, TeamId team, GridPosition position)
            {
                var gameObject = new GameObject(name);
                gameObjects.Add(gameObject);
                var unit = gameObject.AddComponent<TacticalUnit>();
                unit.Initialize(unitId, team, stats, position);
                return unit;
            }

            public void AssignLoadout(TacticalUnit unit, params AbilityDefinition[] abilities)
            {
                var loadout = unit.gameObject.AddComponent<UnitAbilityLoadout>();
                loadout.SetAbilities(abilities);
            }

            public CombatState CreateReactionState(TacticalUnit actor, TacticalUnit reactor)
            {
                var intent = new ActionIntent(
                    actor,
                    MeleeSlash,
                    actor.CurrentGridPosition,
                    ActionTarget.ForUnit(reactor),
                    new[] { reactor.CurrentGridPosition },
                    declarationRound: 1,
                    declarationSequence: 1);
                return new CombatState(1, CombatPhase.ReactionWindow, actor, reactor, intent);
            }

            public void Dispose()
            {
                for (var i = gameObjects.Count - 1; i >= 0; i -= 1)
                {
                    UnityEngine.Object.DestroyImmediate(gameObjects[i]);
                }

                for (var i = assets.Count - 1; i >= 0; i -= 1)
                {
                    UnityEngine.Object.DestroyImmediate(assets[i]);
                }
            }

            private AbilityDefinition CreateAbility(
                string key,
                string displayName,
                int apCost,
                AbilityUsage usage,
                AbilityTiming timing,
                AbilityShape shape,
                int range,
                int radius,
                int damage,
                bool triggersReactions)
            {
                var ability = ScriptableObject.CreateInstance<AbilityDefinition>();
                assets.Add(ability);
                ability.Configure(key, displayName, apCost, usage, timing, shape, range, radius, damage, triggersReactions);
                return ability;
            }
        }
    }
}
