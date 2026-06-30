using System;
using System.Collections.Generic;
using NUnit.Framework;
using ReactionTactics.Actions;
using ReactionTactics.Grid;
using ReactionTactics.Input;
using ReactionTactics.Reactions;
using ReactionTactics.Turns;
using ReactionTactics.Units;
using UnityEditor;
using UnityEngine;

public sealed class BraceReactionTests
{
    [Test]
    public void BracedDefenseReducesNextPositiveDamageAndThenClears()
    {
        var gameObject = new GameObject("Brace Defense Unit");
        var stats = CreateStats("Brace Defense Stats");

        try
        {
            var unit = gameObject.AddComponent<TacticalUnit>();
            unit.Initialize(new UnitId(1), TeamId.Player, stats, GridPosition.Zero);

            var braceResult = unit.BraceUntilNextHit(BraceReactionCommand.DefaultDamageReduction);
            var previewDamage = unit.PreviewIncomingDamageAfterDefense(4);
            var damageResult = unit.ApplyDamageWithReport(4, DamageSource.Environmental("Brace test hit"));
            var followUpResult = unit.ApplyDamageWithReport(3, DamageSource.Environmental("Follow-up hit"));

            Assert.That(braceResult.IsSuccess, Is.True, braceResult.ErrorMessage);
            Assert.That(unit.DefenseState, Is.EqualTo(DefenseState.None));
            Assert.That(previewDamage, Is.EqualTo(2));
            Assert.That(damageResult.IsSuccess, Is.True, damageResult.ErrorMessage);
            Assert.That(damageResult.Value.WasBraced, Is.True);
            Assert.That(damageResult.Value.OriginalAmount, Is.EqualTo(4));
            Assert.That(damageResult.Value.FinalAmount, Is.EqualTo(2));
            Assert.That(damageResult.Value.PreventedAmount, Is.EqualTo(2));
            Assert.That(unit.CurrentHP, Is.EqualTo(unit.MaxHP - 5));
            Assert.That(unit.BracedUntilNextHit, Is.False);
            Assert.That(followUpResult.IsSuccess, Is.True, followUpResult.ErrorMessage);
            Assert.That(followUpResult.Value.WasBraced, Is.False);
            Assert.That(followUpResult.Value.FinalAmount, Is.EqualTo(3));
        }
        finally
        {
            UnityEngine.Object.DestroyImmediate(gameObject);
            UnityEngine.Object.DestroyImmediate(stats);
        }
    }

    [Test]
    public void BraceReactionCostsApCompletesReactorAndReducesPendingMeleeDamage()
    {
        using (var fixture = new Fixture())
        {
            var actor = fixture.CreateUnit("Brace Actor", new UnitId(1), TeamId.Player, new GridPosition(0, 0, 0));
            var targetReactor = fixture.CreateUnit("Brace Target Reactor", new UnitId(2), TeamId.Enemy, new GridPosition(1, 0, 0));
            fixture.AssignLoadout(actor, fixture.MeleeSlash);

            Assert.That(fixture.Manager.StartCombat().IsSuccess, Is.True);

            ActionIntent declaredIntent = null;
            ReactionWindow windowDuringResolution = null;
            var targetApEvents = new List<ActionPointsChangedEvent>();
            fixture.EventBus.ActionDeclared += eventData => declaredIntent = eventData.ActionIntent as ActionIntent;
            fixture.EventBus.ActionResolved += _ => windowDuringResolution = fixture.Manager.CurrentReactionWindow;
            fixture.EventBus.ActionPointsChanged += eventData =>
            {
                if (ReferenceEquals(eventData.Unit, targetReactor))
                {
                    targetApEvents.Add(eventData);
                }
            };

            Assert.That(fixture.InputRouter.SelectUnit(actor).IsSuccess, Is.True);
            Assert.That(fixture.InputRouter.SelectMeleeAttack().IsSuccess, Is.True);
            Assert.That(fixture.InputRouter.ConfirmTargetUnit(targetReactor).IsSuccess, Is.True);

            Assert.That(declaredIntent, Is.Not.Null);
            Assert.That(fixture.Manager.CurrentState.Phase, Is.EqualTo(CombatPhase.ReactionWindow));
            Assert.That(fixture.Manager.CurrentState.ReactingUnit, Is.SameAs(targetReactor));

            var commandResult = BraceReactionCommand.TryCreate(
                targetReactor,
                declaredIntent,
                fixture.Manager.CurrentState,
                fixture.Manager.CurrentReactionWindow);
            Assert.That(commandResult.IsSuccess, Is.True, commandResult.ErrorMessage);
            Assert.That(commandResult.Value.Cost, Is.EqualTo(BraceReactionCommand.DefaultApCost));
            Assert.That(commandResult.Value.DamageReduction, Is.EqualTo(BraceReactionCommand.DefaultDamageReduction));

            var apBeforeBrace = targetReactor.CurrentAP;
            var braceResult = fixture.Manager.BraceCurrentReaction(targetReactor);

            Assert.That(braceResult.IsSuccess, Is.True, braceResult.ErrorMessage);
            Assert.That(targetReactor.CurrentAP, Is.EqualTo(apBeforeBrace - BraceReactionCommand.DefaultApCost));
            Assert.That(targetApEvents.Count, Is.EqualTo(1));
            Assert.That(targetApEvents[0].PreviousAP, Is.EqualTo(apBeforeBrace));
            Assert.That(targetApEvents[0].CurrentAP, Is.EqualTo(apBeforeBrace - BraceReactionCommand.DefaultApCost));
            Assert.That(targetReactor.CurrentHP, Is.EqualTo(targetReactor.MaxHP - (fixture.MeleeSlash.Damage - BraceReactionCommand.DefaultDamageReduction)));
            Assert.That(targetReactor.BracedUntilNextHit, Is.False);
            Assert.That(windowDuringResolution, Is.Not.Null);
            Assert.That(windowDuringResolution.IsClosed, Is.True);
            Assert.That(windowDuringResolution.CompletedReactors, Is.EqualTo(new[] { targetReactor }));
            Assert.That(fixture.Manager.CurrentReactionWindow, Is.Null);
            Assert.That(fixture.Manager.CurrentState.Phase, Is.EqualTo(CombatPhase.ActiveTurn));
            Assert.That(fixture.Manager.CurrentState.ActiveUnit, Is.SameAs(actor));
            Assert.That(fixture.Manager.CurrentState.PendingActionIntent, Is.Null);
        }
    }

    [Test]
    public void BraceReactionIsRejectedOutsideCurrentReactorTurnWrongUnitOrInsufficientAp()
    {
        using (var fixture = new Fixture())
        {
            var actor = fixture.CreateUnit("Brace Reject Actor", new UnitId(1), TeamId.Player, new GridPosition(0, 0, 0));
            var currentReactor = fixture.CreateUnit("Brace Reject Current Reactor", new UnitId(2), TeamId.Enemy, new GridPosition(1, 0, 0));
            var wrongReactor = fixture.CreateUnit("Brace Reject Wrong Reactor", new UnitId(3), TeamId.Enemy, new GridPosition(2, 0, 0));
            fixture.AssignLoadout(actor, fixture.MeleeSlash);

            Assert.That(fixture.Manager.StartCombat().IsSuccess, Is.True);

            var outsideResult = fixture.Manager.BraceCurrentReaction(actor);
            Assert.That(outsideResult.IsFailure, Is.True);

            Assert.That(fixture.InputRouter.SelectUnit(actor).IsSuccess, Is.True);
            Assert.That(fixture.InputRouter.SelectMeleeAttack().IsSuccess, Is.True);
            Assert.That(fixture.InputRouter.ConfirmTargetUnit(currentReactor).IsSuccess, Is.True);

            var wrongUnitResult = fixture.Manager.BraceCurrentReaction(wrongReactor);
            Assert.That(wrongUnitResult.IsFailure, Is.True);
            Assert.That(wrongUnitResult.ErrorMessage, Does.Contain("current reactor"));

            currentReactor.SetAPForTest(1);
            var currentAp = currentReactor.CurrentAP;
            var insufficientApResult = fixture.Manager.BraceCurrentReaction(currentReactor);

            Assert.That(insufficientApResult.IsFailure, Is.True);
            Assert.That(insufficientApResult.ErrorMessage, Does.Contain("needs 2 AP"));
            Assert.That(currentReactor.CurrentAP, Is.EqualTo(currentAp));
            Assert.That(currentReactor.BracedUntilNextHit, Is.False);
            Assert.That(fixture.Manager.CurrentState.Phase, Is.EqualTo(CombatPhase.ReactionWindow));
            Assert.That(fixture.Manager.CurrentState.ReactingUnit, Is.SameAs(currentReactor));
            Assert.That(fixture.Manager.CurrentReactionWindow.CompletedReactors, Is.Empty);
        }
    }

    [Test]
    public void BraceClearsAfterPendingActionResolvesWhenNoDamageConsumesIt()
    {
        using (var fixture = new Fixture())
        {
            var actor = fixture.CreateUnit("Brace Expiry Actor", new UnitId(1), TeamId.Player, new GridPosition(0, 0, 0));
            var meleeTarget = fixture.CreateUnit("Brace Expiry Melee Target", new UnitId(2), TeamId.Enemy, new GridPosition(1, 0, 0));
            var farReactor = fixture.CreateUnit("Brace Expiry Far Reactor", new UnitId(3), TeamId.Enemy, new GridPosition(2, 0, 0));
            fixture.AssignLoadout(actor, fixture.MeleeSlash);

            Assert.That(fixture.Manager.StartCombat().IsSuccess, Is.True);
            Assert.That(fixture.InputRouter.SelectUnit(actor).IsSuccess, Is.True);
            Assert.That(fixture.InputRouter.SelectMeleeAttack().IsSuccess, Is.True);
            Assert.That(fixture.InputRouter.ConfirmTargetUnit(meleeTarget).IsSuccess, Is.True);

            Assert.That(fixture.Manager.CurrentState.ReactingUnit, Is.SameAs(meleeTarget));
            Assert.That(fixture.Manager.PassCurrentReaction(meleeTarget).IsSuccess, Is.True);
            Assert.That(fixture.Manager.CurrentState.ReactingUnit, Is.SameAs(farReactor));

            var farApBeforeBrace = farReactor.CurrentAP;
            var braceResult = fixture.Manager.BraceCurrentReaction(farReactor);

            Assert.That(braceResult.IsSuccess, Is.True, braceResult.ErrorMessage);
            Assert.That(farReactor.CurrentAP, Is.EqualTo(farApBeforeBrace - BraceReactionCommand.DefaultApCost));
            Assert.That(farReactor.CurrentHP, Is.EqualTo(farReactor.MaxHP));
            Assert.That(farReactor.BracedUntilNextHit, Is.False);
            Assert.That(meleeTarget.CurrentHP, Is.EqualTo(meleeTarget.MaxHP - fixture.MeleeSlash.Damage));
            Assert.That(fixture.Manager.CurrentState.Phase, Is.EqualTo(CombatPhase.ActiveTurn));
            Assert.That(fixture.Manager.CurrentReactionWindow, Is.Null);
        }
    }

    private static UnitStatsDefinition CreateStats(string displayName)
    {
        var stats = ScriptableObject.CreateInstance<UnitStatsDefinition>();
        stats.Configure(
            displayName: displayName,
            maxHP: 10,
            maxAP: 6,
            movementAnimationSpeed: 4f,
            meleeRange: 1,
            teamColorHint: Color.white);
        return stats;
    }

    private sealed class Fixture : IDisposable
    {
        private readonly List<GameObject> rootObjects = new List<GameObject>();
        private readonly UnitStatsDefinition stats;
        private readonly GridMapDefinition mapDefinition;

        public Fixture()
        {
            stats = CreateStats("Brace Reaction Test Unit");

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
                description: "Brace reaction test ability.");

            mapDefinition = ScriptableObject.CreateInstance<GridMapDefinition>();
            mapDefinition.Configure(
                width: 4,
                depth: 1,
                defaultHeightY: 0,
                overrides: Array.Empty<GridMapDefinition.CellOverride>());

            var registryObject = CreateRoot("Brace Reaction Test Registry");
            var gridObject = CreateRoot("Brace Reaction Test Grid");
            var selectionObject = CreateRoot("Brace Reaction Test Selection");
            var routerObject = CreateRoot("Brace Reaction Test Router");
            var managerObject = CreateRoot("Brace Reaction Test Manager");

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
