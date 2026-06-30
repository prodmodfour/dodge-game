using System;
using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using ReactionTactics.Actions;
using ReactionTactics.Grid;
using ReactionTactics.Input;
using ReactionTactics.Reactions;
using ReactionTactics.Turns;
using ReactionTactics.UI;
using ReactionTactics.Units;
using UnityEngine;

public sealed class BraceGameplayFlowTests
{
    [Test]
    public void PlayerCanBraceFromReactionMenuRouterAndReducePendingMeleeDamage()
    {
        using (var fixture = new Fixture())
        {
            var actor = fixture.CreateUnit(
                "Brace Flow Actor",
                new UnitId(1),
                TeamId.Player,
                new GridPosition(0, 0, 0));
            var target = fixture.CreateUnit(
                "Brace Flow Target",
                new UnitId(2),
                TeamId.Enemy,
                new GridPosition(1, 0, 0));
            var spectator = fixture.CreateUnit(
                "Brace Flow Spectator",
                new UnitId(3),
                TeamId.Enemy,
                new GridPosition(2, 0, 0));
            fixture.AssignLoadout(actor, fixture.MeleeSlash, fixture.PassReaction);
            fixture.AssignLoadout(target, fixture.Brace, fixture.PassReaction);
            fixture.AssignLoadout(spectator, fixture.PassReaction);

            Assert.That(fixture.Manager.StartCombat().IsSuccess, Is.True);

            var combatLog = new List<string>();
            var damageEvents = new List<DamageEvent>();
            var apEvents = new List<ActionPointsChangedEvent>();
            var resolvedActions = new List<ActionResolvedEvent>();
            fixture.EventBus.CombatLogMessageAdded += entry => combatLog.Add(entry.Message);
            fixture.EventBus.DamageApplied += damageEvents.Add;
            fixture.EventBus.ActionPointsChanged += eventData =>
            {
                if (ReferenceEquals(eventData.Unit, target))
                {
                    apEvents.Add(eventData);
                }
            };
            fixture.EventBus.ActionResolved += resolvedActions.Add;

            Assert.That(fixture.Router.SelectUnit(actor).IsSuccess, Is.True);
            Assert.That(fixture.Router.SelectMeleeAttack().IsSuccess, Is.True);
            Assert.That(fixture.Router.ConfirmTargetUnit(target).IsSuccess, Is.True);

            var pendingIntent = fixture.Manager.CurrentState.PendingActionIntent as ActionIntent;
            Assert.That(pendingIntent, Is.Not.Null);
            Assert.That(pendingIntent.Ability, Is.SameAs(fixture.MeleeSlash));
            Assert.That(fixture.Manager.CurrentState.Phase, Is.EqualTo(CombatPhase.ReactionWindow));
            Assert.That(fixture.Manager.CurrentState.CurrentReactor, Is.SameAs(target));
            Assert.That(fixture.Selection.SelectedUnit, Is.SameAs(target));

            var menuEntries = ReactionMenu.BuildMenuEntries(fixture.Manager.CurrentState);
            Assert.That(menuEntries.Count, Is.EqualTo(3));
            Assert.That(menuEntries.Any(entry => entry.Kind == ReactionMenuEntryKind.Brace && entry.IsEnabled && entry.APCost == fixture.Brace.APCost), Is.True);
            Assert.That(menuEntries.Any(entry => entry.Kind == ReactionMenuEntryKind.Pass && entry.IsEnabled), Is.True);

            var targetHpBeforeBrace = target.CurrentHP;
            var targetApBeforeBrace = target.CurrentAP;
            var braceResult = fixture.Router.SelectBraceReaction();

            Assert.That(braceResult.IsSuccess, Is.True, braceResult.ErrorMessage);
            Assert.That(target.CurrentAP, Is.EqualTo(targetApBeforeBrace - fixture.Brace.APCost));
            Assert.That(apEvents.Count, Is.EqualTo(1));
            Assert.That(apEvents[0].PreviousAP, Is.EqualTo(targetApBeforeBrace));
            Assert.That(apEvents[0].CurrentAP, Is.EqualTo(targetApBeforeBrace - fixture.Brace.APCost));
            Assert.That(target.CurrentHP, Is.EqualTo(targetHpBeforeBrace));
            Assert.That(target.BracedUntilNextHit, Is.True);
            Assert.That(target.BraceDamageReduction, Is.EqualTo(BraceReactionCommand.DefaultDamageReduction));

            Assert.That(fixture.Manager.CurrentState.Phase, Is.EqualTo(CombatPhase.ReactionWindow));
            Assert.That(fixture.Manager.CurrentState.CurrentReactor, Is.SameAs(spectator));
            Assert.That(fixture.Selection.SelectedUnit, Is.SameAs(spectator));
            Assert.That(fixture.Manager.CurrentReactionWindow.CompletedReactors, Is.EqualTo(new[] { target }));

            var moveAfterBraceResult = fixture.Manager.MoveCurrentReaction(target, target.CurrentGridPosition);
            Assert.That(moveAfterBraceResult.IsFailure, Is.True);
            Assert.That(moveAfterBraceResult.ErrorMessage, Does.Contain("current reactor"));
            Assert.That(target.CurrentGridPosition, Is.EqualTo(new GridPosition(1, 0, 0)));
            Assert.That(target.BracedUntilNextHit, Is.True);

            Assert.That(fixture.Router.RequestPassReaction().IsSuccess, Is.True);

            Assert.That(target.CurrentHP, Is.EqualTo(targetHpBeforeBrace - (fixture.MeleeSlash.Damage - BraceReactionCommand.DefaultDamageReduction)));
            Assert.That(target.BracedUntilNextHit, Is.False);
            Assert.That(damageEvents.Count, Is.EqualTo(1));
            Assert.That(damageEvents[0].SourceIntent, Is.SameAs(pendingIntent));
            Assert.That(damageEvents[0].Target, Is.SameAs(target));
            Assert.That(damageEvents[0].Amount, Is.EqualTo(fixture.MeleeSlash.Damage));
            Assert.That(damageEvents[0].WasBraced, Is.True);
            Assert.That(damageEvents[0].FinalAmount, Is.EqualTo(fixture.MeleeSlash.Damage - BraceReactionCommand.DefaultDamageReduction));
            Assert.That(resolvedActions.Count, Is.EqualTo(1));
            Assert.That(resolvedActions[0].ActionIntent, Is.SameAs(pendingIntent));

            Assert.That(fixture.Manager.CurrentState.Phase, Is.EqualTo(CombatPhase.ActiveTurn));
            Assert.That(fixture.Manager.CurrentState.ActiveUnit, Is.SameAs(actor));
            Assert.That(fixture.Manager.CurrentState.ReactingUnit, Is.Null);
            Assert.That(fixture.Manager.CurrentState.PendingActionIntent, Is.Null);
            Assert.That(fixture.Manager.CurrentReactionWindow, Is.Null);
            Assert.That(fixture.Selection.SelectedUnit, Is.SameAs(actor));
            Assert.That(fixture.Selection.SelectedActionMode, Is.EqualTo(SelectionActionMode.None));
            Assert.That(fixture.Selection.SelectedTarget.HasTarget, Is.False);

            Assert.That(combatLog.Any(line => line.Contains("braced against Melee Slash for 2 AP")), Is.True);
            Assert.That(combatLog.Any(line => line.Contains("reducing damage from 4 to 2")), Is.True);
        }
    }

    private sealed class Fixture : IDisposable
    {
        private readonly List<GameObject> rootObjects = new List<GameObject>();
        private readonly List<ScriptableObject> assets = new List<ScriptableObject>();
        private readonly UnitStatsDefinition stats;

        public Fixture()
        {
            stats = ScriptableObject.CreateInstance<UnitStatsDefinition>();
            assets.Add(stats);
            stats.Configure(
                displayName: "Brace Flow Unit",
                maxHP: 10,
                maxAP: 6,
                movementAnimationSpeed: 4f,
                meleeRange: 1,
                teamColorHint: Color.white);

            MeleeSlash = CreateAbility(
                abilityKey: "melee_slash",
                displayName: "Melee Slash",
                apCost: 3,
                usage: AbilityUsage.Action,
                timing: AbilityTiming.Telegraphed,
                shape: AbilityShape.Melee,
                range: 0,
                radius: 0,
                damage: 4,
                triggersReactions: true);
            Brace = CreateAbility(
                abilityKey: "brace",
                displayName: "Brace",
                apCost: BraceReactionCommand.DefaultApCost,
                usage: AbilityUsage.Reaction,
                timing: AbilityTiming.Immediate,
                shape: AbilityShape.Self,
                range: 0,
                radius: 0,
                damage: 0,
                triggersReactions: false);
            PassReaction = CreateAbility(
                abilityKey: ReactionEligibilityService.DefaultPassReactionAbilityKey,
                displayName: ReactionEligibilityService.DefaultPassReactionDisplayName,
                apCost: 0,
                usage: AbilityUsage.Reaction,
                timing: AbilityTiming.Immediate,
                shape: AbilityShape.Self,
                range: 0,
                radius: 0,
                damage: 0,
                triggersReactions: false);

            var mapDefinition = ScriptableObject.CreateInstance<GridMapDefinition>();
            assets.Add(mapDefinition);
            mapDefinition.Configure(
                width: 3,
                depth: 1,
                defaultHeightY: 0,
                overrides: Array.Empty<GridMapDefinition.CellOverride>());

            var registryObject = CreateRoot("Brace Gameplay Flow Registry");
            var gridObject = CreateRoot("Brace Gameplay Flow Grid");
            var inputObject = CreateRoot("Brace Gameplay Flow Input");
            var managerObject = CreateRoot("Brace Gameplay Flow Manager");

            Registry = registryObject.AddComponent<UnitRegistry>();
            GridManager = gridObject.AddComponent<GridManager>();
            Assert.That(GridManager.SetMapDefinition(mapDefinition), Is.True);

            Selection = inputObject.AddComponent<SelectionController>();
            Router = inputObject.AddComponent<PlayerCommandRouter>();
            Router.SelectionController = Selection;
            Router.KeyboardShortcutsEnabled = false;

            EventBus = managerObject.AddComponent<CombatEventBus>();
            Manager = managerObject.AddComponent<CombatManager>();
            Manager.Configure(Registry, GridManager, Router, EventBus);
            Manager.StartCombatOnStart = false;
            Manager.LogCombatStart = false;
            Manager.LogActionFlow = false;
        }

        public UnitRegistry Registry { get; }

        public GridManager GridManager { get; }

        public SelectionController Selection { get; }

        public PlayerCommandRouter Router { get; }

        public CombatEventBus EventBus { get; }

        public CombatManager Manager { get; }

        public AbilityDefinition MeleeSlash { get; }

        public AbilityDefinition Brace { get; }

        public AbilityDefinition PassReaction { get; }

        public TacticalUnit CreateUnit(string name, UnitId unitId, TeamId team, GridPosition position)
        {
            var gameObject = new GameObject(name);
            gameObject.transform.SetParent(Registry.transform, worldPositionStays: false);
            var unit = gameObject.AddComponent<TacticalUnit>();
            unit.Initialize(unitId, team, stats, position);
            return unit;
        }

        public void AssignLoadout(TacticalUnit unit, params AbilityDefinition[] abilities)
        {
            var loadout = unit.gameObject.AddComponent<UnitAbilityLoadout>();
            loadout.SetAbilities(abilities);
        }

        public void Dispose()
        {
            for (var i = rootObjects.Count - 1; i >= 0; i -= 1)
            {
                UnityEngine.Object.DestroyImmediate(rootObjects[i]);
            }

            for (var i = assets.Count - 1; i >= 0; i -= 1)
            {
                UnityEngine.Object.DestroyImmediate(assets[i]);
            }
        }

        private GameObject CreateRoot(string name)
        {
            var gameObject = new GameObject(name);
            rootObjects.Add(gameObject);
            return gameObject;
        }

        private AbilityDefinition CreateAbility(
            string abilityKey,
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
            ability.Configure(
                abilityKey,
                displayName,
                apCost,
                usage,
                timing,
                shape,
                range,
                radius,
                damage,
                triggersReactions,
                description: "Brace gameplay flow test ability.");
            return ability;
        }
    }
}
