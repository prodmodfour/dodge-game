using System;
using System.Collections.Generic;
using NUnit.Framework;
using ReactionTactics.AI;
using ReactionTactics.Actions;
using ReactionTactics.Grid;
using ReactionTactics.Input;
using ReactionTactics.Turns;
using ReactionTactics.Units;
using UnityEditor;
using UnityEngine;

public sealed class AiMeleeActionTests
{
    [Test]
    public void EnemyActiveTurnDeclaresMeleeSlashAndOpensPlayerReactionWindow()
    {
        using (var fixture = new Fixture(maxAP: 6, meleeApCost: 3))
        {
            var enemy = fixture.CreateUnit("AI Melee Enemy", new UnitId(2), TeamId.Enemy, new GridPosition(0, 0, 0));
            var player = fixture.CreateUnit("AI Melee Player", new UnitId(1), TeamId.Player, new GridPosition(1, 0, 0));
            fixture.AssignLoadout(enemy, fixture.MeleeSlash);

            var declaredCount = 0;
            ActionIntent declaredIntent = null;
            fixture.EventBus.ActionDeclared += eventData =>
            {
                declaredCount += 1;
                declaredIntent = eventData.ActionIntent as ActionIntent;
            };

            Assert.That(fixture.Manager.StartCombat().IsSuccess, Is.True);
            Assert.That(fixture.Manager.CurrentState.ActiveUnit, Is.SameAs(player));

            var result = fixture.Manager.EndActiveTurn();

            Assert.That(result.IsSuccess, Is.True, result.ErrorMessage);
            Assert.That(declaredCount, Is.EqualTo(1));
            Assert.That(declaredIntent, Is.Not.Null);
            Assert.That(declaredIntent.Actor, Is.SameAs(enemy));
            Assert.That(declaredIntent.Ability, Is.SameAs(fixture.MeleeSlash));
            Assert.That(declaredIntent.Target.TargetUnit, Is.SameAs(player));
            Assert.That(enemy.CurrentAP, Is.EqualTo(enemy.MaxAP - fixture.MeleeSlash.APCost));
            Assert.That(player.CurrentHP, Is.EqualTo(player.MaxHP), "The melee hit should wait until the reaction window is complete.");
            Assert.That(fixture.Manager.CurrentReactionWindow, Is.Not.Null);
            Assert.That(fixture.Manager.CurrentReactionWindow.IsOpen, Is.True);
            Assert.That(fixture.Manager.CurrentState.Phase, Is.EqualTo(CombatPhase.ReactionWindow));
            Assert.That(fixture.Manager.CurrentState.ActiveUnit, Is.SameAs(enemy));
            Assert.That(fixture.Manager.CurrentState.ReactingUnit, Is.SameAs(player));
            Assert.That(fixture.Manager.CurrentState.PendingActionIntent, Is.SameAs(declaredIntent));

            var passResult = fixture.Manager.PassCurrentReaction(player);

            Assert.That(passResult.IsSuccess, Is.True, passResult.ErrorMessage);
            Assert.That(fixture.Manager.CurrentReactionWindow, Is.Null);
            Assert.That(fixture.Manager.CurrentState.Phase, Is.EqualTo(CombatPhase.ActiveTurn));
            Assert.That(fixture.Manager.CurrentState.ActiveUnit, Is.SameAs(player));
            Assert.That(fixture.Manager.CurrentState.CurrentRound, Is.EqualTo(2));
            Assert.That(fixture.Manager.CurrentState.PendingActionIntent, Is.Null);
            Assert.That(player.CurrentHP, Is.EqualTo(player.MaxHP - fixture.MeleeSlash.Damage));
            Assert.That(enemy.CurrentAP, Is.EqualTo(enemy.MaxAP), "AI handoff should end the enemy turn and refresh AP at the next round start.");
        }
    }

    [Test]
    public void EnemyActiveTurnDoesNotDeclareMeleeSlashWithoutEnoughActionPoints()
    {
        using (var fixture = new Fixture(maxAP: 2, meleeApCost: 3))
        {
            var enemy = fixture.CreateUnit("AI Tired Melee Enemy", new UnitId(2), TeamId.Enemy, new GridPosition(0, 0, 0));
            var player = fixture.CreateUnit("AI Adjacent Player", new UnitId(1), TeamId.Player, new GridPosition(1, 0, 0));
            fixture.AssignLoadout(enemy, fixture.MeleeSlash);

            var declaredCount = 0;
            fixture.EventBus.ActionDeclared += _ => declaredCount += 1;

            Assert.That(fixture.Manager.StartCombat().IsSuccess, Is.True);

            var result = fixture.Manager.EndActiveTurn();

            Assert.That(result.IsSuccess, Is.True, result.ErrorMessage);
            Assert.That(declaredCount, Is.EqualTo(0));
            Assert.That(enemy.CurrentAP, Is.EqualTo(enemy.MaxAP));
            Assert.That(player.CurrentHP, Is.EqualTo(player.MaxHP));
            Assert.That(fixture.Manager.CurrentReactionWindow, Is.Null);
            Assert.That(fixture.Manager.CurrentState.Phase, Is.EqualTo(CombatPhase.ActiveTurn));
            Assert.That(fixture.Manager.CurrentState.ActiveUnit, Is.SameAs(player));
            Assert.That(fixture.Manager.CurrentState.CurrentRound, Is.EqualTo(2));
        }
    }

    private sealed class Fixture : IDisposable
    {
        private readonly List<GameObject> rootObjects = new List<GameObject>();
        private readonly UnitStatsDefinition stats;
        private readonly GridMapDefinition mapDefinition;

        public Fixture(int maxAP, int meleeApCost)
        {
            stats = ScriptableObject.CreateInstance<UnitStatsDefinition>();
            stats.Configure(
                displayName: "AI Melee Test Unit",
                maxHP: 10,
                maxAP: maxAP,
                movementAnimationSpeed: 4f,
                meleeRange: 1,
                teamColorHint: Color.white);

            MeleeSlash = ScriptableObject.CreateInstance<AbilityDefinition>();
            MeleeSlash.Configure(
                abilityKey: AiController.DefaultMeleeSlashAbilityKey,
                displayName: AiController.DefaultMeleeSlashDisplayName,
                apCost: meleeApCost,
                usage: AbilityUsage.Action,
                timing: AbilityTiming.Telegraphed,
                shape: AbilityShape.Melee,
                range: 0,
                radius: 0,
                damage: 4,
                triggersReactions: true,
                description: "AI melee action test ability.");

            mapDefinition = ScriptableObject.CreateInstance<GridMapDefinition>();
            mapDefinition.Configure(
                width: 3,
                depth: 2,
                defaultHeightY: 0,
                overrides: Array.Empty<GridMapDefinition.CellOverride>());

            var registryObject = CreateRoot("AI Melee Test Registry");
            var gridObject = CreateRoot("AI Melee Test Grid");
            var selectionObject = CreateRoot("AI Melee Test Selection");
            var routerObject = CreateRoot("AI Melee Test Router");
            var managerObject = CreateRoot("AI Melee Test Manager");

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
