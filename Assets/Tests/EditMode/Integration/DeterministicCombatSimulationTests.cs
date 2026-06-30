using System;
using System.Collections.Generic;
using NUnit.Framework;
using ReactionTactics.Actions;
using ReactionTactics.Core;
using ReactionTactics.Grid;
using ReactionTactics.Input;
using ReactionTactics.Reactions;
using ReactionTactics.Targeting;
using ReactionTactics.Turns;
using ReactionTactics.Units;
using UnityEngine;

public sealed class DeterministicCombatSimulationTests
{
    [Test]
    public void DeterministicCombatSimulation_MeleeReactionMoveOutPreventsDamage()
    {
        using (var battle = new InMemoryBattle())
        {
            var actor = battle.CreateUnit("Simulation Melee Actor", new UnitId(1), TeamId.Player, new GridPosition(1, 0, 1));
            var target = battle.CreateUnit("Simulation Melee Target", new UnitId(2), TeamId.Enemy, new GridPosition(2, 0, 1));
            battle.AssignLoadout(actor, battle.MeleeSlash);
            var damageEvents = battle.CaptureDamageEvents();

            Assert.That(battle.Manager.StartCombat().IsSuccess, Is.True);
            Assert.That(battle.DeclareMelee(actor, target).IsSuccess, Is.True);
            Assert.That(battle.Manager.CurrentState.Phase, Is.EqualTo(CombatPhase.ReactionWindow));
            Assert.That(battle.Manager.CurrentState.CurrentReactor, Is.SameAs(target));
            Assert.That(battle.Manager.CurrentReactionWindow.OrderedReactors, Is.EqualTo(new[] { target }));

            var targetHpBeforeReaction = target.CurrentHP;
            var destination = new GridPosition(3, 0, 1);
            Assert.That(battle.Manager.MoveCurrentReaction(destination).IsSuccess, Is.True);

            Assert.That(target.CurrentGridPosition, Is.EqualTo(destination));
            Assert.That(target.CurrentHP, Is.EqualTo(targetHpBeforeReaction), "Moving outside final melee range is deterministic avoidance, not a dodge roll.");
            Assert.That(damageEvents, Is.Empty);
            AssertResolvedBackToActiveTurn(battle, actor);
        }
    }

    [Test]
    public void DeterministicCombatSimulation_ConeReactionMoveOutPreventsDamage()
    {
        using (var battle = new InMemoryBattle())
        {
            var actor = battle.CreateUnit("Simulation Cone Actor", new UnitId(1), TeamId.Player, new GridPosition(0, 0, 1));
            var target = battle.CreateUnit("Simulation Cone Target", new UnitId(2), TeamId.Enemy, new GridPosition(2, 0, 1));
            battle.AssignLoadout(actor, battle.ConeShot);
            var damageEvents = battle.CaptureDamageEvents();

            Assert.That(battle.Manager.StartCombat().IsSuccess, Is.True);
            var aimCell = new GridPosition(3, 0, 1);
            Assert.That(battle.DeclareCone(actor, aimCell).IsSuccess, Is.True);

            var pendingIntent = battle.Manager.CurrentState.PendingActionIntent as ActionIntent;
            Assert.That(pendingIntent, Is.Not.Null);
            Assert.That(pendingIntent.Target.Direction, Is.EqualTo(CardinalDirection.East));
            Assert.That(pendingIntent.DeclaredAffectedCells, Has.Member(target.CurrentGridPosition));
            Assert.That(battle.Manager.CurrentState.CurrentReactor, Is.SameAs(target));

            var targetHpBeforeReaction = target.CurrentHP;
            var destination = new GridPosition(2, 0, 3);
            Assert.That(pendingIntent.DeclaredAffectedCells, Has.No.Member(destination));
            Assert.That(battle.Manager.MoveCurrentReaction(destination).IsSuccess, Is.True);

            Assert.That(target.CurrentGridPosition, Is.EqualTo(destination));
            Assert.That(target.CurrentHP, Is.EqualTo(targetHpBeforeReaction), "Cone resolution checks final grid position inside the declared cone.");
            Assert.That(damageEvents, Is.Empty);
            AssertResolvedBackToActiveTurn(battle, actor);
        }
    }

    [Test]
    public void DeterministicCombatSimulation_AoeReactionMoveOutPreventsDamage()
    {
        using (var battle = new InMemoryBattle())
        {
            var actor = battle.CreateUnit("Simulation AoE Actor", new UnitId(1), TeamId.Player, new GridPosition(0, 0, 1));
            var target = battle.CreateUnit("Simulation AoE Target", new UnitId(2), TeamId.Enemy, new GridPosition(2, 0, 1));
            battle.AssignLoadout(actor, battle.Fireball);
            var damageEvents = battle.CaptureDamageEvents();

            Assert.That(battle.Manager.StartCombat().IsSuccess, Is.True);
            var targetCell = new GridPosition(2, 0, 2);
            Assert.That(battle.DeclareAreaOfEffect(actor, targetCell).IsSuccess, Is.True);

            var pendingIntent = battle.Manager.CurrentState.PendingActionIntent as ActionIntent;
            Assert.That(pendingIntent, Is.Not.Null);
            Assert.That(pendingIntent.DeclaredAffectedCells, Is.EquivalentTo(AreaShapeService.GetRadiusCells(targetCell, battle.Fireball.Radius, battle.GridManager.CurrentMap)));
            Assert.That(pendingIntent.DeclaredAffectedCells, Has.Member(target.CurrentGridPosition));
            Assert.That(battle.Manager.CurrentState.CurrentReactor, Is.SameAs(target));

            var targetHpBeforeReaction = target.CurrentHP;
            var destination = new GridPosition(4, 0, 1);
            Assert.That(pendingIntent.DeclaredAffectedCells, Has.No.Member(destination));
            Assert.That(battle.Manager.MoveCurrentReaction(destination).IsSuccess, Is.True);

            Assert.That(target.CurrentGridPosition, Is.EqualTo(destination));
            Assert.That(target.CurrentHP, Is.EqualTo(targetHpBeforeReaction), "AoE resolution checks final grid position inside the declared radius.");
            Assert.That(damageEvents, Is.Empty);
            AssertResolvedBackToActiveTurn(battle, actor);
        }
    }

    [Test]
    public void DeterministicCombatSimulation_MeleeTargetStaysInRangeTakesDamage()
    {
        using (var battle = new InMemoryBattle())
        {
            var actor = battle.CreateUnit("Simulation Hit Actor", new UnitId(1), TeamId.Player, new GridPosition(1, 0, 1));
            var target = battle.CreateUnit("Simulation Hit Target", new UnitId(2), TeamId.Enemy, new GridPosition(2, 0, 1));
            battle.AssignLoadout(actor, battle.MeleeSlash);
            var damageEvents = battle.CaptureDamageEvents();

            Assert.That(battle.Manager.StartCombat().IsSuccess, Is.True);
            Assert.That(battle.DeclareMelee(actor, target).IsSuccess, Is.True);
            Assert.That(battle.Manager.CurrentState.CurrentReactor, Is.SameAs(target));

            var targetHpBeforePass = target.CurrentHP;
            Assert.That(battle.Manager.PassCurrentReaction().IsSuccess, Is.True);

            Assert.That(target.CurrentGridPosition, Is.EqualTo(new GridPosition(2, 0, 1)));
            Assert.That(target.CurrentHP, Is.EqualTo(targetHpBeforePass - battle.MeleeSlash.Damage));
            Assert.That(damageEvents, Has.Count.EqualTo(1));
            Assert.That(damageEvents[0].Attacker, Is.SameAs(actor));
            Assert.That(damageEvents[0].Target, Is.SameAs(target));
            Assert.That(damageEvents[0].Amount, Is.EqualTo(battle.MeleeSlash.Damage));
            Assert.That(damageEvents[0].FinalAmount, Is.EqualTo(battle.MeleeSlash.Damage));
            Assert.That(damageEvents[0].WasBraced, Is.False);
            AssertResolvedBackToActiveTurn(battle, actor);
        }
    }

    private static void AssertResolvedBackToActiveTurn(InMemoryBattle battle, TacticalUnit actor)
    {
        Assert.That(battle.Manager.CurrentState.Phase, Is.EqualTo(CombatPhase.ActiveTurn));
        Assert.That(battle.Manager.CurrentState.ActiveUnit, Is.SameAs(actor));
        Assert.That(battle.Manager.CurrentState.ReactingUnit, Is.Null);
        Assert.That(battle.Manager.CurrentState.PendingActionIntent, Is.Null);
        Assert.That(battle.Manager.CurrentReactionWindow, Is.Null);
    }

    private sealed class InMemoryBattle : IDisposable
    {
        private readonly List<GameObject> rootObjects = new List<GameObject>();
        private readonly List<ScriptableObject> assets = new List<ScriptableObject>();
        private readonly UnitStatsDefinition stats;

        public InMemoryBattle()
        {
            stats = ScriptableObject.CreateInstance<UnitStatsDefinition>();
            stats.Configure(
                displayName: "Deterministic Simulation Unit",
                maxHP: 10,
                maxAP: 8,
                movementAnimationSpeed: 4f,
                meleeRange: 1,
                teamColorHint: Color.white);
            assets.Add(stats);

            MeleeSlash = CreateAbility(
                abilityKey: "melee_slash",
                displayName: "Melee Slash",
                apCost: 3,
                shape: AbilityShape.Melee,
                range: 0,
                radius: 0,
                damage: 4);
            ConeShot = CreateAbility(
                abilityKey: "cone_shot",
                displayName: "Cone Shot",
                apCost: 4,
                shape: AbilityShape.Cone,
                range: 4,
                radius: 0,
                damage: 3);
            Fireball = CreateAbility(
                abilityKey: "fireball",
                displayName: "Fireball",
                apCost: 5,
                shape: AbilityShape.Radius,
                range: 5,
                radius: 1,
                damage: 4);

            var mapDefinition = ScriptableObject.CreateInstance<GridMapDefinition>();
            mapDefinition.Configure(
                width: 5,
                depth: 4,
                defaultHeightY: 0,
                overrides: Array.Empty<GridMapDefinition.CellOverride>());
            assets.Add(mapDefinition);

            var registryObject = CreateRoot("Deterministic Combat Simulation Registry");
            var gridObject = CreateRoot("Deterministic Combat Simulation Grid");
            var selectionObject = CreateRoot("Deterministic Combat Simulation Selection");
            var routerObject = CreateRoot("Deterministic Combat Simulation Router");
            var managerObject = CreateRoot("Deterministic Combat Simulation Manager");

            Registry = registryObject.AddComponent<UnitRegistry>();
            GridManager = gridObject.AddComponent<GridManager>();
            Assert.That(GridManager.SetMapDefinition(mapDefinition), Is.True);

            Selection = selectionObject.AddComponent<SelectionController>();
            Router = routerObject.AddComponent<PlayerCommandRouter>();
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

        public AbilityDefinition ConeShot { get; }

        public AbilityDefinition Fireball { get; }

        public TacticalUnit CreateUnit(string name, UnitId unitId, TeamId team, GridPosition position)
        {
            var unitObject = new GameObject(name);
            unitObject.transform.SetParent(Registry.transform, worldPositionStays: false);
            unitObject.transform.position = GridManager.Metrics.GridToWorldCenter(position);
            var unit = unitObject.AddComponent<TacticalUnit>();
            unit.Initialize(unitId, team, stats, position);
            unitObject.AddComponent<GridPathMover>();
            return unit;
        }

        public void AssignLoadout(TacticalUnit unit, params AbilityDefinition[] abilities)
        {
            var loadout = unit.gameObject.AddComponent<UnitAbilityLoadout>();
            loadout.SetAbilities(abilities);
        }

        public List<DamageEvent> CaptureDamageEvents()
        {
            var damageEvents = new List<DamageEvent>();
            EventBus.DamageApplied += damageEvents.Add;
            return damageEvents;
        }

        public TacticalResult DeclareMelee(TacticalUnit actor, TacticalUnit target)
        {
            var selectResult = Router.SelectUnit(actor);
            if (selectResult.IsFailure)
            {
                return selectResult;
            }

            var modeResult = Router.SelectMeleeAttack();
            if (modeResult.IsFailure)
            {
                return modeResult;
            }

            return Router.ConfirmTargetUnit(target);
        }

        public TacticalResult DeclareCone(TacticalUnit actor, GridPosition targetCell)
        {
            var selectResult = Router.SelectUnit(actor);
            if (selectResult.IsFailure)
            {
                return selectResult;
            }

            var modeResult = Router.SelectConeAttack();
            if (modeResult.IsFailure)
            {
                return modeResult;
            }

            return Router.ConfirmTargetCell(targetCell);
        }

        public TacticalResult DeclareAreaOfEffect(TacticalUnit actor, GridPosition targetCell)
        {
            var selectResult = Router.SelectUnit(actor);
            if (selectResult.IsFailure)
            {
                return selectResult;
            }

            var modeResult = Router.SelectAreaOfEffectAttack();
            if (modeResult.IsFailure)
            {
                return modeResult;
            }

            return Router.ConfirmTargetCell(targetCell);
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
                abilityKey,
                displayName,
                apCost,
                AbilityUsage.Action,
                AbilityTiming.Telegraphed,
                shape,
                range,
                radius,
                damage,
                triggersReactions: true,
                description: "Deterministic combat simulation test ability.");
            assets.Add(ability);
            return ability;
        }
    }
}
