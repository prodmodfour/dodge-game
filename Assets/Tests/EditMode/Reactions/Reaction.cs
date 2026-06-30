using System;
using System.Collections.Generic;
using System.Linq;
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

public sealed class Reaction
{
    [Test]
    public void ReactionWindowGivesEveryOtherLivingUnitATurnInDistanceOrderAndResolvesOnlyAfterClose()
    {
        using (var fixture = new Fixture())
        {
            var actor = fixture.CreateUnit("System Actor", new UnitId(1), TeamId.Player, new GridPosition(1, 0, 1));
            var target = fixture.CreateUnit("System Target", new UnitId(4), TeamId.Enemy, new GridPosition(2, 0, 1));
            var ally = fixture.CreateUnit("System Ally Reactor", new UnitId(2), TeamId.Player, new GridPosition(1, 0, 3));
            var farEnemy = fixture.CreateUnit("System Far Reactor", new UnitId(3), TeamId.Enemy, new GridPosition(4, 0, 1));
            fixture.AssignLoadout(actor, fixture.MeleeSlash);

            var startedReactors = new List<TacticalUnit>();
            ActionIntent declaredIntent = null;
            var resolved = false;
            var resolvedEvents = 0;
            ReactionWindow windowDuringResolution = null;
            fixture.EventBus.ActionDeclared += eventData => declaredIntent = eventData.ActionIntent as ActionIntent;
            fixture.EventBus.ReactionTurnStarted += eventData => startedReactors.Add(eventData.Reactor);
            fixture.EventBus.ActionResolved += _ =>
            {
                resolved = true;
                resolvedEvents += 1;
                windowDuringResolution = fixture.Manager.CurrentReactionWindow;
            };

            Assert.That(fixture.Manager.StartCombat().IsSuccess, Is.True);
            Assert.That(fixture.DeclareMelee(actor, target).IsSuccess, Is.True);

            var expectedOrder = new[] { target, ally, farEnemy };
            Assert.That(declaredIntent, Is.Not.Null);
            Assert.That(fixture.Manager.CurrentReactionWindow, Is.Not.Null);
            Assert.That(fixture.Manager.CurrentReactionWindow.OrderedReactors.ToArray(), Is.EqualTo(expectedOrder));
            Assert.That(startedReactors.ToArray(), Is.EqualTo(new[] { target }));
            Assert.That(fixture.Manager.CurrentState.ReactingUnit, Is.SameAs(target));
            Assert.That(resolved, Is.False, "The original action must wait for all reactors before resolving.");
            Assert.That(target.CurrentHP, Is.EqualTo(target.MaxHP));

            Assert.That(fixture.Manager.PassCurrentReaction().IsSuccess, Is.True);
            Assert.That(startedReactors.ToArray(), Is.EqualTo(new[] { target, ally }));
            Assert.That(fixture.Manager.CurrentReactionWindow.CompletedReactors.ToArray(), Is.EqualTo(new[] { target }));
            Assert.That(fixture.Manager.CurrentState.ReactingUnit, Is.SameAs(ally));
            Assert.That(resolved, Is.False, "Passing the first reactor should advance the window, not resolve the action.");
            Assert.That(target.CurrentHP, Is.EqualTo(target.MaxHP));

            Assert.That(fixture.Manager.PassCurrentReaction().IsSuccess, Is.True);
            Assert.That(startedReactors.ToArray(), Is.EqualTo(new[] { target, ally, farEnemy }));
            Assert.That(fixture.Manager.CurrentReactionWindow.CompletedReactors.ToArray(), Is.EqualTo(new[] { target, ally }));
            Assert.That(fixture.Manager.CurrentState.ReactingUnit, Is.SameAs(farEnemy));
            Assert.That(resolved, Is.False, "The original action should still wait until the final reactor completes.");
            Assert.That(target.CurrentHP, Is.EqualTo(target.MaxHP));

            Assert.That(fixture.Manager.PassCurrentReaction().IsSuccess, Is.True);

            Assert.That(resolved, Is.True);
            Assert.That(resolvedEvents, Is.EqualTo(1));
            Assert.That(startedReactors.ToArray(), Is.EqualTo(expectedOrder));
            Assert.That(windowDuringResolution, Is.Not.Null);
            Assert.That(windowDuringResolution.IsClosed, Is.True);
            Assert.That(windowDuringResolution.CompletedReactors.ToArray(), Is.EqualTo(expectedOrder));
            Assert.That(fixture.Manager.CurrentReactionWindow, Is.Null);
            Assert.That(fixture.Manager.CurrentState.Phase, Is.EqualTo(CombatPhase.ActiveTurn));
            Assert.That(fixture.Manager.CurrentState.ActiveUnit, Is.SameAs(actor));
            Assert.That(fixture.Manager.CurrentState.ReactingUnit, Is.Null);
            Assert.That(fixture.Manager.CurrentState.PendingActionIntent, Is.Null);
            Assert.That(target.CurrentHP, Is.EqualTo(target.MaxHP - fixture.MeleeSlash.Damage));
        }
    }

    [Test]
    public void ReactionMoveSpendsApUpdatesOccupancyAndAvoidsMeleeByFinalPosition()
    {
        using (var fixture = new Fixture())
        {
            var actor = fixture.CreateUnit("System Melee Actor", new UnitId(1), TeamId.Player, new GridPosition(1, 0, 1));
            var target = fixture.CreateUnit("System Melee Target", new UnitId(2), TeamId.Enemy, new GridPosition(2, 0, 1));
            fixture.AssignLoadout(actor, fixture.MeleeSlash);

            var resolved = false;
            fixture.EventBus.ActionResolved += _ => resolved = true;

            Assert.That(fixture.Manager.StartCombat().IsSuccess, Is.True);
            Assert.That(fixture.DeclareMelee(actor, target).IsSuccess, Is.True);
            Assert.That(fixture.Manager.CurrentState.ReactingUnit, Is.SameAs(target));

            var start = target.CurrentGridPosition;
            var destination = new GridPosition(3, 0, 1);
            var apBeforeMove = target.CurrentAP;
            Assert.That(fixture.Registry.IsOccupied(start), Is.True);
            Assert.That(fixture.Registry.IsOccupied(destination), Is.False);

            var moveResult = fixture.Manager.MoveCurrentReaction(destination);

            Assert.That(moveResult.IsSuccess, Is.True, moveResult.ErrorMessage);
            Assert.That(resolved, Is.True);
            Assert.That(target.CurrentGridPosition, Is.EqualTo(destination));
            Assert.That(target.CurrentAP, Is.EqualTo(apBeforeMove - 1));
            Assert.That(fixture.Registry.IsOccupied(start), Is.False);
            Assert.That(fixture.Registry.IsOccupied(destination), Is.True);
            Assert.That(fixture.Registry.TryGetOccupant(destination, out var occupantId), Is.True);
            Assert.That(occupantId, Is.EqualTo(target.UnitId));
            Assert.That(target.CurrentHP, Is.EqualTo(target.MaxHP), "Moving outside final melee range is the deterministic avoidance.");
        }
    }

    [Test]
    public void ReactionMoveAvoidsConeByEndingOutsideDeclaredConeCells()
    {
        using (var fixture = new Fixture())
        {
            var actor = fixture.CreateUnit("System Cone Actor", new UnitId(1), TeamId.Player, new GridPosition(2, 0, 1));
            var target = fixture.CreateUnit("System Cone Target", new UnitId(2), TeamId.Enemy, new GridPosition(2, 0, 3));
            fixture.AssignLoadout(actor, fixture.ConeShot);

            ActionIntent declaredIntent = null;
            var resolved = false;
            fixture.EventBus.ActionDeclared += eventData => declaredIntent = eventData.ActionIntent as ActionIntent;
            fixture.EventBus.ActionResolved += _ => resolved = true;

            Assert.That(fixture.Manager.StartCombat().IsSuccess, Is.True);
            Assert.That(fixture.DeclareCone(actor, target.CurrentGridPosition).IsSuccess, Is.True);

            Assert.That(declaredIntent, Is.Not.Null);
            Assert.That(declaredIntent.DeclaredAffectedCells, Has.Member(target.CurrentGridPosition));
            Assert.That(fixture.Manager.CurrentState.ReactingUnit, Is.SameAs(target));

            var start = target.CurrentGridPosition;
            var destination = new GridPosition(4, 0, 3);
            var apBeforeMove = target.CurrentAP;

            var moveResult = fixture.Manager.MoveCurrentReaction(destination);

            Assert.That(moveResult.IsSuccess, Is.True, moveResult.ErrorMessage);
            Assert.That(resolved, Is.True);
            Assert.That(declaredIntent.DeclaredAffectedCells, Has.No.Member(destination));
            Assert.That(target.CurrentGridPosition, Is.EqualTo(destination));
            Assert.That(target.CurrentAP, Is.EqualTo(apBeforeMove - 2));
            Assert.That(fixture.Registry.IsOccupied(start), Is.False);
            Assert.That(fixture.Registry.IsOccupied(destination), Is.True);
            Assert.That(target.CurrentHP, Is.EqualTo(target.MaxHP), "Cone damage checks the final grid position inside the declared cone.");
        }
    }

    [Test]
    public void ReactionMoveAvoidsAoeByEndingOutsideDeclaredRadiusCells()
    {
        using (var fixture = new Fixture())
        {
            var actor = fixture.CreateUnit("System AoE Actor", new UnitId(1), TeamId.Player, new GridPosition(0, 0, 0));
            var target = fixture.CreateUnit("System AoE Target", new UnitId(2), TeamId.Enemy, new GridPosition(2, 0, 1));
            fixture.AssignLoadout(actor, fixture.Fireball);

            ActionIntent declaredIntent = null;
            var resolved = false;
            fixture.EventBus.ActionDeclared += eventData => declaredIntent = eventData.ActionIntent as ActionIntent;
            fixture.EventBus.ActionResolved += _ => resolved = true;

            Assert.That(fixture.Manager.StartCombat().IsSuccess, Is.True);
            Assert.That(fixture.DeclareAreaOfEffect(actor, target.CurrentGridPosition).IsSuccess, Is.True);

            Assert.That(declaredIntent, Is.Not.Null);
            Assert.That(declaredIntent.DeclaredAffectedCells, Has.Member(target.CurrentGridPosition));
            Assert.That(fixture.Manager.CurrentState.ReactingUnit, Is.SameAs(target));

            var start = target.CurrentGridPosition;
            var destination = new GridPosition(4, 0, 1);
            var apBeforeMove = target.CurrentAP;

            var moveResult = fixture.Manager.MoveCurrentReaction(destination);

            Assert.That(moveResult.IsSuccess, Is.True, moveResult.ErrorMessage);
            Assert.That(resolved, Is.True);
            Assert.That(declaredIntent.DeclaredAffectedCells, Has.No.Member(destination));
            Assert.That(target.CurrentGridPosition, Is.EqualTo(destination));
            Assert.That(target.CurrentAP, Is.EqualTo(apBeforeMove - 2));
            Assert.That(fixture.Registry.IsOccupied(start), Is.False);
            Assert.That(fixture.Registry.IsOccupied(destination), Is.True);
            Assert.That(target.CurrentHP, Is.EqualTo(target.MaxHP), "AoE damage checks the final grid position inside the declared radius.");
        }
    }

    private sealed class Fixture : IDisposable
    {
        private readonly List<GameObject> rootObjects = new List<GameObject>();
        private readonly List<UnityEngine.Object> assets = new List<UnityEngine.Object>();
        private readonly UnitStatsDefinition stats;
        private readonly GridMapDefinition mapDefinition;

        public Fixture()
        {
            stats = ScriptableObject.CreateInstance<UnitStatsDefinition>();
            stats.Configure(
                displayName: "Reaction System Test Unit",
                maxHP: 12,
                maxAP: 8,
                movementAnimationSpeed: 4f,
                meleeRange: 1,
                teamColorHint: Color.white);
            assets.Add(stats);

            MeleeSlash = CreateAbility(
                "melee_slash",
                "Melee Slash",
                apCost: 3,
                shape: AbilityShape.Melee,
                range: 0,
                radius: 0,
                damage: 4);
            ConeShot = CreateAbility(
                "cone_shot",
                "Cone Shot",
                apCost: 4,
                shape: AbilityShape.Cone,
                range: 4,
                radius: 0,
                damage: 3);
            Fireball = CreateAbility(
                "fireball",
                "Fireball",
                apCost: 5,
                shape: AbilityShape.Radius,
                range: 5,
                radius: 1,
                damage: 4);

            mapDefinition = ScriptableObject.CreateInstance<GridMapDefinition>();
            mapDefinition.Configure(
                width: 6,
                depth: 6,
                defaultHeightY: 0,
                overrides: Array.Empty<GridMapDefinition.CellOverride>());
            assets.Add(mapDefinition);

            var registryObject = CreateRoot("Reaction System Test Registry");
            var gridObject = CreateRoot("Reaction System Test Grid");
            var selectionObject = CreateRoot("Reaction System Test Selection");
            var routerObject = CreateRoot("Reaction System Test Router");
            var managerObject = CreateRoot("Reaction System Test Manager");

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

        public AbilityDefinition ConeShot { get; }

        public AbilityDefinition Fireball { get; }

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

        public TacticalResult DeclareMelee(TacticalUnit actor, TacticalUnit target)
        {
            var selectResult = InputRouter.SelectUnit(actor);
            if (selectResult.IsFailure)
            {
                return selectResult;
            }

            var modeResult = InputRouter.SelectMeleeAttack();
            if (modeResult.IsFailure)
            {
                return modeResult;
            }

            return InputRouter.ConfirmTargetUnit(target);
        }

        public TacticalResult DeclareCone(TacticalUnit actor, GridPosition targetCell)
        {
            var selectResult = InputRouter.SelectUnit(actor);
            if (selectResult.IsFailure)
            {
                return selectResult;
            }

            var modeResult = InputRouter.SelectConeAttack();
            if (modeResult.IsFailure)
            {
                return modeResult;
            }

            return InputRouter.ConfirmTargetCell(targetCell);
        }

        public TacticalResult DeclareAreaOfEffect(TacticalUnit actor, GridPosition targetCell)
        {
            var selectResult = InputRouter.SelectUnit(actor);
            if (selectResult.IsFailure)
            {
                return selectResult;
            }

            var modeResult = InputRouter.SelectAreaOfEffectAttack();
            if (modeResult.IsFailure)
            {
                return modeResult;
            }

            return InputRouter.ConfirmTargetCell(targetCell);
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
            AbilityShape shape,
            int range,
            int radius,
            int damage)
        {
            var ability = ScriptableObject.CreateInstance<AbilityDefinition>();
            ability.Configure(
                abilityKey: abilityKey,
                displayName: displayName,
                apCost: apCost,
                usage: AbilityUsage.Action,
                timing: AbilityTiming.Telegraphed,
                shape: shape,
                range: range,
                radius: radius,
                damage: damage,
                triggersReactions: true,
                description: "Reaction system integration test ability.");
            assets.Add(ability);
            return ability;
        }

        private static void AssignMapDefinition(GridManager manager, GridMapDefinition definition)
        {
            var serializedObject = new SerializedObject(manager);
            serializedObject.FindProperty("mapDefinition").objectReferenceValue = definition;
            serializedObject.ApplyModifiedPropertiesWithoutUndo();
        }
    }
}
