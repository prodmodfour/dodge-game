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

public sealed class AiConeActionTests
{
    [Test]
    public void ChooseConeAttackPrefersDirectionWithMostHostilesAndAcceptableFriendlyCount()
    {
        using (var fixture = new Fixture(width: 5, depth: 5))
        {
            var enemy = fixture.CreateUnit("AI Cone Archer", new UnitId(10), TeamId.Enemy, new GridPosition(2, 0, 2));
            fixture.CreateUnit("North Player A", new UnitId(1), TeamId.Player, new GridPosition(2, 0, 3));
            fixture.CreateUnit("North Player B", new UnitId(2), TeamId.Player, new GridPosition(1, 0, 4));
            fixture.CreateUnit("North Enemy Friend", new UnitId(11), TeamId.Enemy, new GridPosition(3, 0, 4));
            fixture.CreateUnit("East Player", new UnitId(3), TeamId.Player, new GridPosition(3, 0, 2));
            fixture.AssignLoadout(enemy, fixture.ConeShot);

            var choiceResult = fixture.Controller.TryChooseConeAttack(
                enemy,
                fixture.Registry.GetLivingUnits(),
                fixture.GridManager.CurrentMap,
                new CombatState(1, CombatPhase.ActiveTurn, enemy));

            Assert.That(choiceResult.IsSuccess, Is.True, choiceResult.ErrorMessage);
            Assert.That(choiceResult.Value.Ability, Is.SameAs(fixture.ConeShot));
            Assert.That(choiceResult.Value.Direction, Is.EqualTo(CardinalDirection.North));
            Assert.That(choiceResult.Value.TargetCell, Is.EqualTo(new GridPosition(2, 0, 3)));
            Assert.That(choiceResult.Value.HostileThreatCount, Is.EqualTo(2));
            Assert.That(choiceResult.Value.FriendlyThreatCount, Is.EqualTo(1));
        }
    }

    [Test]
    public void ChooseConeAttackRejectsDirectionsThatThreatenMoreFriendliesThanHostiles()
    {
        using (var fixture = new Fixture(width: 4, depth: 3))
        {
            var enemy = fixture.CreateUnit("AI Cautious Cone Archer", new UnitId(10), TeamId.Enemy, new GridPosition(0, 0, 1));
            fixture.CreateUnit("Cone Target", new UnitId(1), TeamId.Player, new GridPosition(2, 0, 1));
            fixture.CreateUnit("Friendly In Front", new UnitId(11), TeamId.Enemy, new GridPosition(1, 0, 1));
            fixture.CreateUnit("Friendly On Flank", new UnitId(12), TeamId.Enemy, new GridPosition(2, 0, 0));
            fixture.AssignLoadout(enemy, fixture.ConeShot);

            var choiceResult = fixture.Controller.TryChooseConeAttack(
                enemy,
                fixture.Registry.GetLivingUnits(),
                fixture.GridManager.CurrentMap,
                new CombatState(1, CombatPhase.ActiveTurn, enemy));

            Assert.That(choiceResult.IsFailure, Is.True);
            Assert.That(choiceResult.ErrorMessage, Does.Contain("more friendlies than hostiles"));
        }
    }

    [Test]
    public void EnemyActiveTurnDeclaresConeShotAndOpensPlayerReactionWindow()
    {
        using (var fixture = new Fixture(width: 5, depth: 3))
        {
            var player = fixture.CreateUnit("AI Cone Player", new UnitId(1), TeamId.Player, new GridPosition(3, 0, 1));
            var enemy = fixture.CreateUnit("AI Cone Enemy", new UnitId(2), TeamId.Enemy, new GridPosition(0, 0, 1));
            fixture.AssignLoadout(enemy, fixture.ConeShot);

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
            Assert.That(declaredIntent.Ability, Is.SameAs(fixture.ConeShot));
            Assert.That(declaredIntent.Target.HasDirection, Is.True);
            Assert.That(declaredIntent.Target.Direction, Is.EqualTo(CardinalDirection.East));
            Assert.That(declaredIntent.DeclaredAffectedCells, Does.Contain(player.CurrentGridPosition));
            Assert.That(enemy.CurrentAP, Is.EqualTo(enemy.MaxAP - fixture.ConeShot.APCost));
            Assert.That(player.CurrentHP, Is.EqualTo(player.MaxHP), "Cone damage should wait until the reaction window is complete.");
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
            Assert.That(fixture.Manager.CurrentState.ActiveUnit, Is.SameAs(enemy));
            Assert.That(player.CurrentHP, Is.EqualTo(player.MaxHP - fixture.ConeShot.Damage));
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
                displayName: "AI Cone Test Unit",
                maxHP: 10,
                maxAP: 6,
                movementAnimationSpeed: 4f,
                meleeRange: 1,
                teamColorHint: Color.white);

            ConeShot = ScriptableObject.CreateInstance<AbilityDefinition>();
            ConeShot.Configure(
                abilityKey: AiController.DefaultConeShotAbilityKey,
                displayName: AiController.DefaultConeShotDisplayName,
                apCost: 4,
                usage: AbilityUsage.Action,
                timing: AbilityTiming.Telegraphed,
                shape: AbilityShape.Cone,
                range: 4,
                radius: 0,
                damage: 3,
                triggersReactions: true,
                description: "AI cone action test ability.");

            mapDefinition = ScriptableObject.CreateInstance<GridMapDefinition>();
            mapDefinition.Configure(
                width: width,
                depth: depth,
                defaultHeightY: 0,
                overrides: Array.Empty<GridMapDefinition.CellOverride>());

            var registryObject = CreateRoot("AI Cone Test Registry");
            var gridObject = CreateRoot("AI Cone Test Grid");
            var selectionObject = CreateRoot("AI Cone Test Selection");
            var routerObject = CreateRoot("AI Cone Test Router");
            var managerObject = CreateRoot("AI Cone Test Manager");

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

        public AbilityDefinition ConeShot { get; }

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
            UnityEngine.Object.DestroyImmediate(ConeShot);
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
