using System;
using System.Collections.Generic;
using System.Text.RegularExpressions;
using NUnit.Framework;
using ReactionTactics.Actions;
using ReactionTactics.Core;
using ReactionTactics.Grid;
using ReactionTactics.Input;
using ReactionTactics.Reactions;
using ReactionTactics.Turns;
using ReactionTactics.Units;
using UnityEditor;
using UnityEngine;
using UnityEngine.TestTools;

public sealed class ReactionAutoPassTests
{
    [Test]
    public void ApEmptyReactorAutoPassesAndOriginalActionResolves()
    {
        using (var fixture = new Fixture(width: 3, depth: 1))
        {
            var actor = fixture.CreateUnit("Auto Pass AP Actor", new UnitId(1), TeamId.Player, new GridPosition(0, 0, 0));
            var target = fixture.CreateUnit("Auto Pass AP Target", new UnitId(2), TeamId.Enemy, new GridPosition(1, 0, 0));
            fixture.AssignLoadout(actor, fixture.MeleeSlash);

            Assert.That(fixture.Manager.StartCombat().IsSuccess, Is.True);
            target.SetAPForTest(0);

            ReactionWindow windowDuringResolution = null;
            fixture.EventBus.ActionResolved += _ => windowDuringResolution = fixture.Manager.CurrentReactionWindow;

            Assert.That(fixture.InputRouter.SelectUnit(actor).IsSuccess, Is.True);
            Assert.That(fixture.InputRouter.SelectMeleeAttack().IsSuccess, Is.True);
            Assert.That(fixture.InputRouter.ConfirmTargetUnit(target).IsSuccess, Is.True);

            Assert.That(windowDuringResolution, Is.Not.Null);
            Assert.That(windowDuringResolution.IsClosed, Is.True);
            Assert.That(windowDuringResolution.SkippedReactors, Is.EqualTo(new[] { target }));
            Assert.That(windowDuringResolution.CompletedReactors, Is.Empty);
            Assert.That(fixture.Manager.CurrentReactionWindow, Is.Null);
            Assert.That(fixture.Manager.CurrentState.Phase, Is.EqualTo(CombatPhase.ActiveTurn));
            Assert.That(fixture.Manager.CurrentState.ReactingUnit, Is.Null);
            Assert.That(target.CurrentAP, Is.EqualTo(0));
            Assert.That(target.CurrentHP, Is.EqualTo(target.MaxHP - fixture.MeleeSlash.Damage));
        }
    }

    [Test]
    public void PassOnlyReactorAutoPassesWhenNoMovementOrBraceIsLegal()
    {
        using (var fixture = new Fixture(width: 2, depth: 1))
        {
            fixture.Manager.LogActionFlow = true;
            var actor = fixture.CreateUnit("Pass Only Actor", new UnitId(1), TeamId.Player, new GridPosition(0, 0, 0));
            var target = fixture.CreateUnit("Pass Only Target", new UnitId(2), TeamId.Enemy, new GridPosition(1, 0, 0));
            fixture.AssignLoadout(actor, fixture.MeleeSlash);

            Assert.That(fixture.Manager.StartCombat().IsSuccess, Is.True);
            target.SetAPForTest(1);

            ReactionWindow windowDuringResolution = null;
            fixture.EventBus.ActionResolved += _ => windowDuringResolution = fixture.Manager.CurrentReactionWindow;
            LogAssert.Expect(
                LogType.Log,
                new Regex(@"\[Combat Log\] Reaction auto-pass: .*no legal reaction commands other than Pass"));

            Assert.That(fixture.InputRouter.SelectUnit(actor).IsSuccess, Is.True);
            Assert.That(fixture.InputRouter.SelectMeleeAttack().IsSuccess, Is.True);
            Assert.That(fixture.InputRouter.ConfirmTargetUnit(target).IsSuccess, Is.True);

            Assert.That(windowDuringResolution, Is.Not.Null);
            Assert.That(windowDuringResolution.IsClosed, Is.True);
            Assert.That(windowDuringResolution.SkippedReactors, Is.EqualTo(new[] { target }));
            Assert.That(windowDuringResolution.CompletedReactors, Is.Empty);
            Assert.That(fixture.Manager.CurrentReactionWindow, Is.Null);
            Assert.That(fixture.Manager.CurrentState.ReactingUnit, Is.Null);
            Assert.That(target.CurrentAP, Is.EqualTo(1));
            Assert.That(target.CurrentHP, Is.EqualTo(target.MaxHP - fixture.MeleeSlash.Damage));
        }
    }

    [Test]
    public void ReactorWithReachableMovementDoesNotAutoPass()
    {
        using (var fixture = new Fixture(width: 3, depth: 1))
        {
            var actor = fixture.CreateUnit("Move Available Actor", new UnitId(1), TeamId.Player, new GridPosition(0, 0, 0));
            var target = fixture.CreateUnit("Move Available Target", new UnitId(2), TeamId.Enemy, new GridPosition(1, 0, 0));
            fixture.AssignLoadout(actor, fixture.MeleeSlash);

            Assert.That(fixture.Manager.StartCombat().IsSuccess, Is.True);
            target.SetAPForTest(1);

            var resolved = false;
            fixture.EventBus.ActionResolved += _ => resolved = true;

            Assert.That(fixture.InputRouter.SelectUnit(actor).IsSuccess, Is.True);
            Assert.That(fixture.InputRouter.SelectMeleeAttack().IsSuccess, Is.True);
            Assert.That(fixture.InputRouter.ConfirmTargetUnit(target).IsSuccess, Is.True);

            Assert.That(resolved, Is.False);
            Assert.That(fixture.Manager.CurrentState.Phase, Is.EqualTo(CombatPhase.ReactionWindow));
            Assert.That(fixture.Manager.CurrentState.ReactingUnit, Is.SameAs(target));
            Assert.That(fixture.Manager.CurrentReactionWindow.CurrentReactor, Is.SameAs(target));
            Assert.That(fixture.Manager.CurrentReactionWindow.SkippedReactors, Is.Empty);

            Assert.That(fixture.Manager.PassCurrentReaction(target).IsSuccess, Is.True);
            Assert.That(resolved, Is.True);
        }
    }

    [Test]
    public void ReactorWithBraceAvailableDoesNotAutoPassEvenWhenMovementIsBlocked()
    {
        using (var fixture = new Fixture(width: 2, depth: 1))
        {
            var actor = fixture.CreateUnit("Brace Available Actor", new UnitId(1), TeamId.Player, new GridPosition(0, 0, 0));
            var target = fixture.CreateUnit("Brace Available Target", new UnitId(2), TeamId.Enemy, new GridPosition(1, 0, 0));
            fixture.AssignLoadout(actor, fixture.MeleeSlash);

            Assert.That(fixture.Manager.StartCombat().IsSuccess, Is.True);
            target.SetAPForTest(BraceReactionCommand.DefaultApCost);

            var resolved = false;
            fixture.EventBus.ActionResolved += _ => resolved = true;

            Assert.That(fixture.InputRouter.SelectUnit(actor).IsSuccess, Is.True);
            Assert.That(fixture.InputRouter.SelectMeleeAttack().IsSuccess, Is.True);
            Assert.That(fixture.InputRouter.ConfirmTargetUnit(target).IsSuccess, Is.True);

            Assert.That(resolved, Is.False);
            Assert.That(fixture.Manager.CurrentState.Phase, Is.EqualTo(CombatPhase.ReactionWindow));
            Assert.That(fixture.Manager.CurrentState.ReactingUnit, Is.SameAs(target));
            Assert.That(fixture.Manager.CurrentReactionWindow.SkippedReactors, Is.Empty);

            Assert.That(fixture.Manager.BraceCurrentReaction(target).IsSuccess, Is.True);
            Assert.That(resolved, Is.True);
            Assert.That(target.CurrentHP, Is.EqualTo(target.MaxHP - (fixture.MeleeSlash.Damage - BraceReactionCommand.DefaultDamageReduction)));
        }
    }

    [Test]
    public void IncapacitatedRuleProducesAutoPassEligibilityReason()
    {
        using (var fixture = new Fixture(width: 2, depth: 1))
        {
            var actor = fixture.CreateUnit("Incapacitated Actor", new UnitId(1), TeamId.Player, new GridPosition(0, 0, 0));
            var reactor = fixture.CreateUnit("Incapacitated Reactor", new UnitId(2), TeamId.Enemy, new GridPosition(1, 0, 0));
            var intent = new ActionIntent(
                actor,
                fixture.MeleeSlash,
                actor.CurrentGridPosition,
                ActionTarget.ForUnit(reactor),
                new[] { reactor.CurrentGridPosition },
                declarationRound: 1,
                declarationSequence: 0);
            var state = new CombatState(1, CombatPhase.ReactionWindow, actor, reactor, intent);
            var service = new ReactionEligibilityService(new[]
            {
                new IncapacitatedRule()
            });

            var result = service.CanUnitReact(reactor, intent, state);

            Assert.That(result.CanReact, Is.False);
            Assert.That(result.ShouldAutoPass, Is.True);
            Assert.That(result.Reason, Does.Contain("incapacitated"));
        }
    }

    private sealed class IncapacitatedRule : IReactionEligibilityRule
    {
        public TacticalResult CanUnitReact(TacticalUnit unit, ActionIntent intent, CombatState combatState)
        {
            return TacticalResult.Failure("incapacitated units cannot take reaction commands");
        }
    }

    private sealed class Fixture : IDisposable
    {
        private readonly List<GameObject> rootObjects = new List<GameObject>();
        private readonly UnitStatsDefinition stats;
        private readonly GridMapDefinition mapDefinition;

        public Fixture(int width, int depth)
        {
            stats = ScriptableObject.CreateInstance<UnitStatsDefinition>();
            stats.Configure(
                displayName: "Reaction Auto Pass Test Unit",
                maxHP: 10,
                maxAP: 6,
                movementAnimationSpeed: 4f,
                meleeRange: 1,
                teamColorHint: Color.white);

            MeleeSlash = ScriptableObject.CreateInstance<AbilityDefinition>();
            MeleeSlash.Configure(
                abilityKey: "melee_slash",
                displayName: "Melee Slash",
                apCost: 3,
                usage: AbilityUsage.Action,
                timing: AbilityTiming.Telegraphed,
                shape: AbilityShape.Melee,
                range: 0,
                radius: 0,
                damage: 4,
                triggersReactions: true,
                description: "Reaction auto-pass test ability.");

            mapDefinition = ScriptableObject.CreateInstance<GridMapDefinition>();
            mapDefinition.Configure(
                width: width,
                depth: depth,
                defaultHeightY: 0,
                overrides: Array.Empty<GridMapDefinition.CellOverride>());

            var registryObject = CreateRoot("Reaction Auto Pass Test Registry");
            var gridObject = CreateRoot("Reaction Auto Pass Test Grid");
            var selectionObject = CreateRoot("Reaction Auto Pass Test Selection");
            var routerObject = CreateRoot("Reaction Auto Pass Test Router");
            var managerObject = CreateRoot("Reaction Auto Pass Test Manager");

            gridObject.SetActive(false);
            Registry = registryObject.AddComponent<UnitRegistry>();
            GridManager = gridObject.AddComponent<GridManager>();
            AssignMapDefinition(GridManager, mapDefinition);
            Assert.That(GridManager.RebuildMap(), Is.True);

            Selection = selectionObject.AddComponent<SelectionController>();
            InputRouter = routerObject.AddComponent<PlayerCommandRouter>();
            InputRouter.SelectionController = Selection;
            EventBus = managerObject.AddComponent<CombatEventBus>();
            Manager = managerObject.AddComponent<CombatManager>();
            Manager.Configure(Registry, GridManager, InputRouter, EventBus);
            Manager.StartCombatOnStart = false;
            Manager.LogCombatStart = false;
            Manager.LogActionFlow = false;
        }

        public UnitRegistry Registry { get; }

        public GridManager GridManager { get; }

        public SelectionController Selection { get; }

        public PlayerCommandRouter InputRouter { get; }

        public CombatEventBus EventBus { get; }

        public CombatManager Manager { get; }

        public AbilityDefinition MeleeSlash { get; }

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

            UnityEngine.Object.DestroyImmediate(mapDefinition);
            UnityEngine.Object.DestroyImmediate(MeleeSlash);
            UnityEngine.Object.DestroyImmediate(stats);
        }

        private GameObject CreateRoot(string name)
        {
            var gameObject = new GameObject(name);
            rootObjects.Add(gameObject);
            return gameObject;
        }

        private static void AssignMapDefinition(GridManager manager, GridMapDefinition definition)
        {
            var serializedObject = new SerializedObject(manager);
            serializedObject.FindProperty("mapDefinition").objectReferenceValue = definition;
            serializedObject.ApplyModifiedPropertiesWithoutUndo();
        }
    }
}
