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
    public sealed class ActiveActionMenuTests
    {
        [Test]
        public void BuildMenuEntriesListsSelectedActiveUnitActionsWithApCosts()
        {
            using (var fixture = new Fixture())
            {
                var actor = fixture.CreateUnit("Action Menu Knight", new UnitId(1), TeamId.Player);
                fixture.AssignLoadout(actor, fixture.Move, fixture.MeleeSlash, fixture.ConeShot, fixture.Fireball);
                var selection = new SelectionState(actor, false, GridPosition.Zero, SelectionActionMode.None, SelectionTarget.None);
                var combatState = new CombatState(1, CombatPhase.ActiveTurn, actor);

                var entries = ActiveActionMenu.BuildMenuEntries(selection, combatState);

                Assert.That(entries.Count, Is.EqualTo(5));
                AssertEntry(entries[0], ActiveActionMenuEntryKind.Move, SelectionActionMode.Move, "Move (path AP)", true);
                AssertEntry(entries[1], ActiveActionMenuEntryKind.Ability, SelectionActionMode.Melee, "Melee Slash (3 AP)", true);
                Assert.That(entries[1].Ability, Is.SameAs(fixture.MeleeSlash));
                AssertEntry(entries[2], ActiveActionMenuEntryKind.Ability, SelectionActionMode.Cone, "Cone Shot (4 AP)", true);
                Assert.That(entries[2].Ability, Is.SameAs(fixture.ConeShot));
                AssertEntry(entries[3], ActiveActionMenuEntryKind.Ability, SelectionActionMode.AreaOfEffect, "Fireball (5 AP)", true);
                Assert.That(entries[3].Ability, Is.SameAs(fixture.Fireball));
                AssertEntry(entries[4], ActiveActionMenuEntryKind.EndTurn, SelectionActionMode.None, "End Turn (0 AP)", true);
            }
        }

        [Test]
        public void BuildMenuEntriesDisableExpensiveActionsButKeepMoveAndEndTurnAvailable()
        {
            using (var fixture = new Fixture())
            {
                var actor = fixture.CreateUnit("Low AP Action Menu Unit", new UnitId(2), TeamId.Player);
                actor.SetAPForTest(2);
                fixture.AssignLoadout(actor, fixture.Move, fixture.MeleeSlash, fixture.ConeShot, fixture.Fireball);
                var combatState = new CombatState(1, CombatPhase.ActiveTurn, actor);

                var entries = ActiveActionMenu.BuildMenuEntries(actor, combatState);

                Assert.That(entries[0].Kind, Is.EqualTo(ActiveActionMenuEntryKind.Move));
                Assert.That(entries[0].IsEnabled, Is.True);
                Assert.That(entries[1].Label, Is.EqualTo("Melee Slash (3 AP)"));
                Assert.That(entries[1].IsEnabled, Is.False);
                Assert.That(entries[1].DisabledReason, Does.Contain("Needs 3 AP"));
                Assert.That(entries[2].IsEnabled, Is.False);
                Assert.That(entries[2].DisabledReason, Does.Contain("Needs 4 AP"));
                Assert.That(entries[3].IsEnabled, Is.False);
                Assert.That(entries[3].DisabledReason, Does.Contain("Needs 5 AP"));
                Assert.That(entries[4].Kind, Is.EqualTo(ActiveActionMenuEntryKind.EndTurn));
                Assert.That(entries[4].IsEnabled, Is.True);
            }
        }

        [Test]
        public void BuildMenuEntriesDisableActiveActionsButKeepEndTurnAvailableWhenSelectedUnitIsNotActive()
        {
            using (var fixture = new Fixture())
            {
                var active = fixture.CreateUnit("Current Active Unit", new UnitId(3), TeamId.Player);
                var waiting = fixture.CreateUnit("Waiting Unit", new UnitId(4), TeamId.Player);
                fixture.AssignLoadout(waiting, fixture.Move, fixture.MeleeSlash);
                var combatState = new CombatState(1, CombatPhase.ActiveTurn, active);

                var entries = ActiveActionMenu.BuildMenuEntries(waiting, combatState);

                Assert.That(entries.Count, Is.EqualTo(3));
                Assert.That(entries[0].IsEnabled, Is.False);
                Assert.That(entries[0].DisabledReason, Does.Contain("Select active unit"));
                Assert.That(entries[0].DisabledReason, Does.Contain(active.DisplayName));
                Assert.That(entries[1].IsEnabled, Is.False);
                Assert.That(entries[1].DisabledReason, Does.Contain("Select active unit"));
                Assert.That(entries[1].DisabledReason, Does.Contain(active.DisplayName));
                Assert.That(entries[2].Kind, Is.EqualTo(ActiveActionMenuEntryKind.EndTurn));
                Assert.That(entries[2].IsEnabled, Is.True);
                Assert.That(entries[2].DisabledReason, Is.Empty);
            }
        }

        [Test]
        public void FormatSelectedUnitSummaryReportsPhaseActiveSelectionAndAp()
        {
            using (var fixture = new Fixture())
            {
                var actor = fixture.CreateUnit("Summary Unit", new UnitId(5), TeamId.Player);
                actor.SetAPForTest(4);
                var selection = new SelectionState(actor, false, GridPosition.Zero, SelectionActionMode.None, SelectionTarget.None);
                var combatState = new CombatState(2, CombatPhase.ActiveTurn, actor);

                var summary = ActiveActionMenu.FormatSelectedUnitSummary(selection, combatState);

                Assert.That(summary, Does.Contain("Phase: ActiveTurn"));
                Assert.That(summary, Does.Contain("Active: Action Menu Test Unit"));
                Assert.That(summary, Does.Contain("Selected: Action Menu Test Unit"));
                Assert.That(summary, Does.Contain("AP: 4/6"));
            }
        }

        private static void AssertEntry(
            ActiveActionMenuEntry entry,
            ActiveActionMenuEntryKind expectedKind,
            SelectionActionMode expectedMode,
            string expectedLabel,
            bool expectedEnabled)
        {
            Assert.That(entry.Kind, Is.EqualTo(expectedKind));
            Assert.That(entry.ActionMode, Is.EqualTo(expectedMode));
            Assert.That(entry.Label, Is.EqualTo(expectedLabel));
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
                stats.Configure("Action Menu Test Unit", 10, 6, 4f, 1, Color.white);

                Move = CreateAbility("move", "Move", 0, AbilityUsage.Both, AbilityTiming.Immediate, AbilityShape.Self, 0, 0, 0, false);
                MeleeSlash = CreateAbility("melee_slash", "Melee Slash", 3, AbilityUsage.Action, AbilityTiming.Telegraphed, AbilityShape.Melee, 0, 0, 4, true);
                ConeShot = CreateAbility("cone_shot", "Cone Shot", 4, AbilityUsage.Action, AbilityTiming.Telegraphed, AbilityShape.Cone, 4, 0, 3, true);
                Fireball = CreateAbility("fireball", "Fireball", 5, AbilityUsage.Action, AbilityTiming.Telegraphed, AbilityShape.Radius, 5, 1, 4, true);
            }

            public AbilityDefinition Move { get; }

            public AbilityDefinition MeleeSlash { get; }

            public AbilityDefinition ConeShot { get; }

            public AbilityDefinition Fireball { get; }

            public TacticalUnit CreateUnit(string name, UnitId unitId, TeamId team)
            {
                var gameObject = new GameObject(name);
                gameObjects.Add(gameObject);
                var unit = gameObject.AddComponent<TacticalUnit>();
                unit.Initialize(unitId, team, stats, GridPosition.Zero);
                return unit;
            }

            public void AssignLoadout(TacticalUnit unit, params AbilityDefinition[] abilities)
            {
                var loadout = unit.gameObject.AddComponent<UnitAbilityLoadout>();
                loadout.SetAbilities(abilities);
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
