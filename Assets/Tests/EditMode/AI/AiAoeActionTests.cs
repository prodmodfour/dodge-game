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

public sealed class AiAoeActionTests
{
    [Test]
    public void ChooseAoeAttackPrefersCellWithMostHostilesAndAcceptableFriendlyCount()
    {
        using (var fixture = new Fixture(width: 5, depth: 5))
        {
            var enemy = fixture.CreateUnit("AI Fireball Shaman", new UnitId(10), TeamId.Enemy, new GridPosition(2, 0, 0));
            fixture.CreateUnit("Clustered Player A", new UnitId(1), TeamId.Player, new GridPosition(2, 0, 3));
            fixture.CreateUnit("Clustered Player B", new UnitId(2), TeamId.Player, new GridPosition(1, 0, 3));
            fixture.CreateUnit("Nearby Enemy Friend", new UnitId(11), TeamId.Enemy, new GridPosition(3, 0, 3));
            fixture.CreateUnit("Isolated Player", new UnitId(3), TeamId.Player, new GridPosition(4, 0, 0));
            fixture.AssignLoadout(enemy, fixture.Fireball);

            var choiceResult = fixture.Controller.TryChooseAoeAttack(
                enemy,
                fixture.Registry.GetLivingUnits(),
                fixture.GridManager.CurrentMap,
                new CombatState(1, CombatPhase.ActiveTurn, enemy));

            Assert.That(choiceResult.IsSuccess, Is.True, choiceResult.ErrorMessage);
            Assert.That(choiceResult.Value.Ability, Is.SameAs(fixture.Fireball));
            Assert.That(choiceResult.Value.TargetCell, Is.EqualTo(new GridPosition(1, 0, 3)));
            Assert.That(choiceResult.Value.HostileThreatCount, Is.EqualTo(2));
            Assert.That(choiceResult.Value.FriendlyThreatCount, Is.EqualTo(0));
        }
    }

    [Test]
    public void ChooseAoeAttackRejectsCellsThatThreatenMoreFriendliesThanHostiles()
    {
        using (var fixture = new Fixture(width: 3, depth: 2, fireballRange: 2))
        {
            var enemy = fixture.CreateUnit("AI Cautious Shaman", new UnitId(10), TeamId.Enemy, new GridPosition(0, 0, 0));
            fixture.CreateUnit("Fireball Target", new UnitId(1), TeamId.Player, new GridPosition(2, 0, 0));
            fixture.CreateUnit("Friendly Between", new UnitId(11), TeamId.Enemy, new GridPosition(1, 0, 0));
            fixture.CreateUnit("Friendly Flank", new UnitId(12), TeamId.Enemy, new GridPosition(2, 0, 1));
            fixture.AssignLoadout(enemy, fixture.Fireball);

            var choiceResult = fixture.Controller.TryChooseAoeAttack(
                enemy,
                fixture.Registry.GetLivingUnits(),
                fixture.GridManager.CurrentMap,
                new CombatState(1, CombatPhase.ActiveTurn, enemy));

            Assert.That(choiceResult.IsFailure, Is.True);
            Assert.That(choiceResult.ErrorMessage, Does.Contain("more friendlies than hostiles"));
        }
    }

    [Test]
    public void ChooseAoeAttackRequiresLineOfSightToTargetCell()
    {
        var blockers = new[]
        {
            new GridMapDefinition.CellOverride(2, 0, 0, walkable: true, blocksLineOfSight: true, movementCost: 1)
        };

        using (var fixture = new Fixture(width: 5, depth: 1, fireballRange: 5, overrides: blockers))
        {
            var enemy = fixture.CreateUnit("AI Sight Blocked Shaman", new UnitId(10), TeamId.Enemy, new GridPosition(0, 0, 0));
            fixture.CreateUnit("Blocked Fireball Target", new UnitId(1), TeamId.Player, new GridPosition(4, 0, 0));
            fixture.AssignLoadout(enemy, fixture.Fireball);

            var choiceResult = fixture.Controller.TryChooseAoeAttack(
                enemy,
                fixture.Registry.GetLivingUnits(),
                fixture.GridManager.CurrentMap,
                new CombatState(1, CombatPhase.ActiveTurn, enemy));

            Assert.That(choiceResult.IsFailure, Is.True);
            Assert.That(choiceResult.ErrorMessage, Does.Contain("lacks line of sight"));
        }
    }

    [Test]
    public void EnemyActiveTurnDeclaresFireballAndOpensPlayerReactionWindow()
    {
        using (var fixture = new Fixture(width: 5, depth: 1))
        {
            var player = fixture.CreateUnit("AI Fireball Player", new UnitId(1), TeamId.Player, new GridPosition(3, 0, 0));
            var enemy = fixture.CreateUnit("AI Fireball Enemy", new UnitId(2), TeamId.Enemy, new GridPosition(0, 0, 0));
            fixture.AssignLoadout(enemy, fixture.Fireball);

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
            Assert.That(declaredIntent.Ability, Is.SameAs(fixture.Fireball));
            Assert.That(declaredIntent.Target.HasTargetCell, Is.True);
            Assert.That(declaredIntent.DeclaredAffectedCells, Does.Contain(player.CurrentGridPosition));
            Assert.That(enemy.CurrentAP, Is.EqualTo(enemy.MaxAP - fixture.Fireball.APCost));
            Assert.That(player.CurrentHP, Is.EqualTo(player.MaxHP), "Fireball damage should wait until the reaction window is complete.");
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
            Assert.That(player.CurrentHP, Is.EqualTo(player.MaxHP - fixture.Fireball.Damage));
        }
    }

    private sealed class Fixture : IDisposable
    {
        private readonly List<GameObject> rootObjects = new List<GameObject>();
        private readonly UnitStatsDefinition stats;
        private readonly GridMapDefinition mapDefinition;

        public Fixture(
            int width,
            int depth,
            int fireballRange = 5,
            IEnumerable<GridMapDefinition.CellOverride> overrides = null)
        {
            stats = ScriptableObject.CreateInstance<UnitStatsDefinition>();
            stats.Configure(
                displayName: "AI AoE Test Unit",
                maxHP: 10,
                maxAP: 6,
                movementAnimationSpeed: 4f,
                meleeRange: 1,
                teamColorHint: Color.white);

            Fireball = ScriptableObject.CreateInstance<AbilityDefinition>();
            Fireball.Configure(
                abilityKey: AiController.DefaultFireballAbilityKey,
                displayName: AiController.DefaultFireballDisplayName,
                apCost: 5,
                usage: AbilityUsage.Action,
                timing: AbilityTiming.Telegraphed,
                shape: AbilityShape.Radius,
                range: fireballRange,
                radius: 1,
                damage: 4,
                triggersReactions: true,
                description: "AI AoE action test ability.");

            mapDefinition = ScriptableObject.CreateInstance<GridMapDefinition>();
            mapDefinition.Configure(
                width: width,
                depth: depth,
                defaultHeightY: 0,
                overrides: overrides ?? Array.Empty<GridMapDefinition.CellOverride>());

            var registryObject = CreateRoot("AI AoE Test Registry");
            var gridObject = CreateRoot("AI AoE Test Grid");
            var selectionObject = CreateRoot("AI AoE Test Selection");
            var routerObject = CreateRoot("AI AoE Test Router");
            var managerObject = CreateRoot("AI AoE Test Manager");

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

        public void Dispose()
        {
            for (var i = rootObjects.Count - 1; i >= 0; i -= 1)
            {
                UnityEngine.Object.DestroyImmediate(rootObjects[i]);
            }

            UnityEngine.Object.DestroyImmediate(mapDefinition);
            UnityEngine.Object.DestroyImmediate(Fireball);
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
