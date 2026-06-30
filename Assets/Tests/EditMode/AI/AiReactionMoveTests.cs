using System;
using System.Collections.Generic;
using NUnit.Framework;
using ReactionTactics.AI;
using ReactionTactics.Actions;
using ReactionTactics.Grid;
using ReactionTactics.Input;
using ReactionTactics.Reactions;
using ReactionTactics.Turns;
using ReactionTactics.Units;
using UnityEditor;
using UnityEngine;

public sealed class AiReactionMoveTests
{
    [Test]
    public void ChooseReactionMoveDestinationPicksLowestCostSafeCellWhenThreatened()
    {
        using (var fixture = new Fixture(width: 4, depth: 1))
        {
            var actor = fixture.CreateUnit("AI Reaction Choice Actor", new UnitId(1), TeamId.Player, new GridPosition(0, 0, 0));
            var enemyReactor = fixture.CreateUnit("AI Reaction Choice Enemy", new UnitId(2), TeamId.Enemy, new GridPosition(1, 0, 0));
            var intent = fixture.CreateIntent(actor, fixture.MeleeSlash, ActionTarget.ForUnit(enemyReactor), new[] { enemyReactor.CurrentGridPosition });

            var choiceResult = fixture.Controller.TryChooseReactionMoveDestination(
                enemyReactor,
                intent,
                fixture.GridManager.CurrentMap,
                fixture.Registry,
                fixture.Registry.GetLivingUnits());

            Assert.That(choiceResult.IsSuccess, Is.True, choiceResult.ErrorMessage);
            Assert.That(choiceResult.Value.Position, Is.EqualTo(new GridPosition(2, 0, 0)));
            Assert.That(choiceResult.Value.TotalApCost, Is.EqualTo(1));
            Assert.That(choiceResult.Value.Status, Is.EqualTo(ReactionSafetyStatus.Safe));
            Assert.That(choiceResult.Value.Reason, Does.Contain("outside melee range"));
        }
    }

    [Test]
    public void EnemyReactionMoveAvoidsPlayerMeleeAndConservesOriginalActionTiming()
    {
        using (var fixture = new Fixture(width: 4, depth: 1))
        {
            var actor = fixture.CreateUnit("AI Reaction Melee Actor", new UnitId(1), TeamId.Player, new GridPosition(0, 0, 0));
            var enemyReactor = fixture.CreateUnit("AI Reaction Melee Enemy", new UnitId(2), TeamId.Enemy, new GridPosition(1, 0, 0));
            fixture.AssignLoadout(actor, fixture.MeleeSlash);

            Assert.That(fixture.Manager.StartCombat().IsSuccess, Is.True);

            var start = enemyReactor.CurrentGridPosition;
            var apBeforeReaction = enemyReactor.CurrentAP;
            Assert.That(fixture.Router.SelectUnit(actor).IsSuccess, Is.True);
            Assert.That(fixture.Router.SelectMeleeAttack().IsSuccess, Is.True);
            var declareResult = fixture.Router.ConfirmTargetUnit(enemyReactor);

            Assert.That(declareResult.IsSuccess, Is.True, declareResult.ErrorMessage);
            Assert.That(enemyReactor.CurrentGridPosition, Is.EqualTo(new GridPosition(2, 0, 0)));
            Assert.That(enemyReactor.CurrentAP, Is.EqualTo(apBeforeReaction - 1));
            Assert.That(enemyReactor.CurrentHP, Is.EqualTo(enemyReactor.MaxHP), "Melee should miss positionally after the AI reaction move.");
            Assert.That(fixture.Registry.IsOccupied(start), Is.False);
            Assert.That(fixture.Registry.IsOccupied(enemyReactor.CurrentGridPosition), Is.True);
            Assert.That(fixture.Manager.CurrentReactionWindow, Is.Null);
            Assert.That(fixture.Manager.CurrentState.Phase, Is.EqualTo(CombatPhase.ActiveTurn));
            Assert.That(fixture.Manager.CurrentState.ActiveUnit, Is.SameAs(actor));
            Assert.That(fixture.Manager.CurrentState.PendingActionIntent, Is.Null);
        }
    }

    [Test]
    public void EnemyReactionMoveAvoidsPlayerAoeBeforeDamageResolves()
    {
        using (var fixture = new Fixture(width: 5, depth: 1))
        {
            var actor = fixture.CreateUnit("AI Reaction AoE Actor", new UnitId(1), TeamId.Player, new GridPosition(0, 0, 0));
            var enemyReactor = fixture.CreateUnit("AI Reaction AoE Enemy", new UnitId(2), TeamId.Enemy, new GridPosition(2, 0, 0));
            fixture.AssignLoadout(actor, fixture.Fireball);

            Assert.That(fixture.Manager.StartCombat().IsSuccess, Is.True);

            var apBeforeReaction = enemyReactor.CurrentAP;
            Assert.That(fixture.Router.SelectUnit(actor).IsSuccess, Is.True);
            Assert.That(fixture.Router.SelectAreaOfEffectAttack().IsSuccess, Is.True);
            var declareResult = fixture.Router.ConfirmTargetCell(enemyReactor.CurrentGridPosition);

            Assert.That(declareResult.IsSuccess, Is.True, declareResult.ErrorMessage);
            Assert.That(enemyReactor.CurrentGridPosition, Is.EqualTo(new GridPosition(4, 0, 0)));
            Assert.That(enemyReactor.CurrentAP, Is.EqualTo(apBeforeReaction - 2));
            Assert.That(enemyReactor.CurrentHP, Is.EqualTo(enemyReactor.MaxHP), "AoE damage should use final positions after AI reaction movement.");
            Assert.That(fixture.Manager.CurrentReactionWindow, Is.Null);
            Assert.That(fixture.Manager.CurrentState.Phase, Is.EqualTo(CombatPhase.ActiveTurn));
            Assert.That(fixture.Manager.CurrentState.ActiveUnit, Is.SameAs(actor));
        }
    }

    [Test]
    public void EnemyReactionMoveAvoidsPlayerConeBeforeDamageResolves()
    {
        using (var fixture = new Fixture(width: 5, depth: 3))
        {
            var actor = fixture.CreateUnit("AI Reaction Cone Actor", new UnitId(1), TeamId.Player, new GridPosition(0, 0, 1));
            var enemyReactor = fixture.CreateUnit("AI Reaction Cone Enemy", new UnitId(2), TeamId.Enemy, new GridPosition(2, 0, 1));
            fixture.AssignLoadout(actor, fixture.ConeShot);

            Assert.That(fixture.Manager.StartCombat().IsSuccess, Is.True);

            var apBeforeReaction = enemyReactor.CurrentAP;
            Assert.That(fixture.Router.SelectUnit(actor).IsSuccess, Is.True);
            Assert.That(fixture.Router.SelectConeAttack().IsSuccess, Is.True);
            var declareResult = fixture.Router.ConfirmTargetCell(new GridPosition(4, 0, 1));

            Assert.That(declareResult.IsSuccess, Is.True, declareResult.ErrorMessage);
            Assert.That(enemyReactor.CurrentGridPosition, Is.EqualTo(new GridPosition(1, 0, 0)));
            Assert.That(enemyReactor.CurrentAP, Is.EqualTo(apBeforeReaction - 2));
            Assert.That(enemyReactor.CurrentHP, Is.EqualTo(enemyReactor.MaxHP), "Cone damage should use final positions after AI reaction movement.");
            Assert.That(fixture.Manager.CurrentReactionWindow, Is.Null);
            Assert.That(fixture.Manager.CurrentState.Phase, Is.EqualTo(CombatPhase.ActiveTurn));
            Assert.That(fixture.Manager.CurrentState.ActiveUnit, Is.SameAs(actor));
        }
    }

    [Test]
    public void EnemyReactionPassesAndConservesApWhenOutsidePendingThreat()
    {
        using (var fixture = new Fixture(width: 5, depth: 1))
        {
            var actor = fixture.CreateUnit("AI Reaction Safe Actor", new UnitId(1), TeamId.Player, new GridPosition(0, 0, 0));
            var enemyReactor = fixture.CreateUnit("AI Reaction Safe Enemy", new UnitId(2), TeamId.Enemy, new GridPosition(4, 0, 0));
            fixture.AssignLoadout(actor, fixture.Fireball);

            Assert.That(fixture.Manager.StartCombat().IsSuccess, Is.True);

            var start = enemyReactor.CurrentGridPosition;
            var apBeforeReaction = enemyReactor.CurrentAP;
            Assert.That(fixture.Router.SelectUnit(actor).IsSuccess, Is.True);
            Assert.That(fixture.Router.SelectAreaOfEffectAttack().IsSuccess, Is.True);
            var declareResult = fixture.Router.ConfirmTargetCell(new GridPosition(2, 0, 0));

            Assert.That(declareResult.IsSuccess, Is.True, declareResult.ErrorMessage);
            Assert.That(enemyReactor.CurrentGridPosition, Is.EqualTo(start));
            Assert.That(enemyReactor.CurrentAP, Is.EqualTo(apBeforeReaction));
            Assert.That(enemyReactor.CurrentHP, Is.EqualTo(enemyReactor.MaxHP));
            Assert.That(fixture.Manager.CurrentReactionWindow, Is.Null);
            Assert.That(fixture.Manager.CurrentState.Phase, Is.EqualTo(CombatPhase.ActiveTurn));
            Assert.That(fixture.Manager.CurrentState.ActiveUnit, Is.SameAs(actor));
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
                displayName: "AI Reaction Test Unit",
                maxHP: 10,
                maxAP: 6,
                movementAnimationSpeed: 4f,
                meleeRange: 1,
                teamColorHint: Color.white);

            MeleeSlash = CreateAbility(
                AiController.DefaultMeleeSlashAbilityKey,
                AiController.DefaultMeleeSlashDisplayName,
                apCost: 3,
                shape: AbilityShape.Melee,
                range: 0,
                radius: 0,
                damage: 4);
            ConeShot = CreateAbility(
                AiController.DefaultConeShotAbilityKey,
                AiController.DefaultConeShotDisplayName,
                apCost: 4,
                shape: AbilityShape.Cone,
                range: 4,
                radius: 0,
                damage: 3);
            Fireball = CreateAbility(
                AiController.DefaultFireballAbilityKey,
                AiController.DefaultFireballDisplayName,
                apCost: 5,
                shape: AbilityShape.Radius,
                range: 5,
                radius: 1,
                damage: 4);

            mapDefinition = ScriptableObject.CreateInstance<GridMapDefinition>();
            mapDefinition.Configure(
                width: width,
                depth: depth,
                defaultHeightY: 0,
                overrides: Array.Empty<GridMapDefinition.CellOverride>());

            var registryObject = CreateRoot("AI Reaction Test Registry");
            var gridObject = CreateRoot("AI Reaction Test Grid");
            var selectionObject = CreateRoot("AI Reaction Test Selection");
            var routerObject = CreateRoot("AI Reaction Test Router");
            var managerObject = CreateRoot("AI Reaction Test Manager");

            gridObject.SetActive(false);
            Registry = registryObject.AddComponent<UnitRegistry>();
            GridManager = gridObject.AddComponent<GridManager>();
            AssignMapDefinition(GridManager, mapDefinition);
            Assert.That(GridManager.RebuildMap(), Is.True);

            Selection = selectionObject.AddComponent<SelectionController>();
            Router = routerObject.AddComponent<PlayerCommandRouter>();
            Router.SelectionController = Selection;
            EventBus = managerObject.AddComponent<CombatEventBus>();
            Controller = managerObject.AddComponent<AiController>();
            Controller.LogDecisions = false;
            Manager = managerObject.AddComponent<CombatManager>();
            Manager.Configure(Registry, GridManager, Router, EventBus, Controller);
            Manager.StartCombatOnStart = false;
            Manager.LogCombatStart = false;
            Manager.LogActionFlow = false;
        }

        public UnitRegistry Registry { get; }

        public GridManager GridManager { get; }

        public SelectionController Selection { get; }

        public PlayerCommandRouter Router { get; }

        public CombatEventBus EventBus { get; }

        public AiController Controller { get; }

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
            Assert.That(Registry.Register(unit).IsSuccess, Is.True);
            return unit;
        }

        public void AssignLoadout(TacticalUnit unit, params AbilityDefinition[] abilities)
        {
            var loadout = unit.gameObject.AddComponent<UnitAbilityLoadout>();
            loadout.SetAbilities(abilities);
        }

        public ActionIntent CreateIntent(
            TacticalUnit actor,
            AbilityDefinition ability,
            ActionTarget target,
            IEnumerable<GridPosition> declaredAffectedCells)
        {
            return new ActionIntent(
                actor,
                ability,
                actor.CurrentGridPosition,
                target,
                declaredAffectedCells,
                declarationRound: 1,
                declarationSequence: 0);
        }

        public void Dispose()
        {
            for (var i = rootObjects.Count - 1; i >= 0; i -= 1)
            {
                UnityEngine.Object.DestroyImmediate(rootObjects[i]);
            }

            UnityEngine.Object.DestroyImmediate(mapDefinition);
            UnityEngine.Object.DestroyImmediate(MeleeSlash);
            UnityEngine.Object.DestroyImmediate(ConeShot);
            UnityEngine.Object.DestroyImmediate(Fireball);
            UnityEngine.Object.DestroyImmediate(stats);
        }

        private GameObject CreateRoot(string name)
        {
            var gameObject = new GameObject(name);
            rootObjects.Add(gameObject);
            return gameObject;
        }

        private static AbilityDefinition CreateAbility(
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
                description: "AI reaction movement test ability.");
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
