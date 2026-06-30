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

public sealed class AiBraceReactionTests
{
    [Test]
    public void EnemyReactionBracesWhenThreatenedAndNoSafeDestinationExists()
    {
        using (var fixture = new Fixture(width: 2, depth: 1, fireballRadius: 1))
        {
            var actor = fixture.CreateUnit("AI Brace Melee Actor", new UnitId(1), TeamId.Player, new GridPosition(0, 0, 0));
            var enemyReactor = fixture.CreateUnit("AI Brace Melee Enemy", new UnitId(2), TeamId.Enemy, new GridPosition(1, 0, 0));
            fixture.AssignLoadout(actor, fixture.MeleeSlash);
            var damageEvents = fixture.CaptureDamageEvents();

            Assert.That(fixture.Manager.StartCombat().IsSuccess, Is.True);

            var start = enemyReactor.CurrentGridPosition;
            var apBeforeReaction = enemyReactor.CurrentAP;
            Assert.That(fixture.Router.SelectUnit(actor).IsSuccess, Is.True);
            Assert.That(fixture.Router.SelectMeleeAttack().IsSuccess, Is.True);
            var declareResult = fixture.Router.ConfirmTargetUnit(enemyReactor);

            Assert.That(declareResult.IsSuccess, Is.True, declareResult.ErrorMessage);
            Assert.That(enemyReactor.CurrentGridPosition, Is.EqualTo(start));
            Assert.That(enemyReactor.CurrentAP, Is.EqualTo(apBeforeReaction - BraceReactionCommand.DefaultApCost));
            Assert.That(enemyReactor.CurrentHP, Is.EqualTo(enemyReactor.MaxHP - (fixture.MeleeSlash.Damage - BraceReactionCommand.DefaultDamageReduction)));
            Assert.That(enemyReactor.BracedUntilNextHit, Is.False);
            Assert.That(damageEvents, Has.Count.EqualTo(1));
            Assert.That(damageEvents[0].WasBraced, Is.True);
            Assert.That(damageEvents[0].FinalAmount, Is.EqualTo(fixture.MeleeSlash.Damage - BraceReactionCommand.DefaultDamageReduction));
            Assert.That(fixture.Manager.CurrentReactionWindow, Is.Null);
            Assert.That(fixture.Manager.CurrentState.Phase, Is.EqualTo(CombatPhase.ActiveTurn));
            Assert.That(fixture.Manager.CurrentState.ActiveUnit, Is.SameAs(actor));
        }
    }

    [Test]
    public void EnemyReactionPassesWhenThreatenedCannotEscapeAndBraceApIsInsufficient()
    {
        using (var fixture = new Fixture(width: 4, depth: 1, fireballRadius: 1))
        {
            var actor = fixture.CreateUnit("AI Brace Pass Actor", new UnitId(1), TeamId.Player, new GridPosition(0, 0, 0));
            var enemyReactor = fixture.CreateUnit("AI Brace Pass Enemy", new UnitId(2), TeamId.Enemy, new GridPosition(2, 0, 0));
            fixture.AssignLoadout(actor, fixture.Fireball);
            var damageEvents = fixture.CaptureDamageEvents();

            Assert.That(fixture.Manager.StartCombat().IsSuccess, Is.True);
            enemyReactor.SetAPForTest(BraceReactionCommand.DefaultApCost - 1);

            var start = enemyReactor.CurrentGridPosition;
            var apBeforeReaction = enemyReactor.CurrentAP;
            Assert.That(fixture.Router.SelectUnit(actor).IsSuccess, Is.True);
            Assert.That(fixture.Router.SelectAreaOfEffectAttack().IsSuccess, Is.True);
            var declareResult = fixture.Router.ConfirmTargetCell(enemyReactor.CurrentGridPosition);

            Assert.That(declareResult.IsSuccess, Is.True, declareResult.ErrorMessage);
            Assert.That(enemyReactor.CurrentGridPosition, Is.EqualTo(start));
            Assert.That(enemyReactor.CurrentAP, Is.EqualTo(apBeforeReaction));
            Assert.That(enemyReactor.CurrentHP, Is.EqualTo(enemyReactor.MaxHP - fixture.Fireball.Damage));
            Assert.That(enemyReactor.BracedUntilNextHit, Is.False);
            Assert.That(damageEvents, Has.Count.EqualTo(1));
            Assert.That(damageEvents[0].Target, Is.SameAs(enemyReactor));
            Assert.That(damageEvents[0].WasBraced, Is.False);
            Assert.That(damageEvents[0].FinalAmount, Is.EqualTo(fixture.Fireball.Damage));
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

        public Fixture(int width, int depth, int fireballRadius)
        {
            stats = ScriptableObject.CreateInstance<UnitStatsDefinition>();
            stats.Configure(
                displayName: "AI Brace Test Unit",
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
            Fireball = CreateAbility(
                AiController.DefaultFireballAbilityKey,
                AiController.DefaultFireballDisplayName,
                apCost: 5,
                shape: AbilityShape.Radius,
                range: 5,
                radius: fireballRadius,
                damage: 4);

            mapDefinition = ScriptableObject.CreateInstance<GridMapDefinition>();
            mapDefinition.Configure(
                width: width,
                depth: depth,
                defaultHeightY: 0,
                overrides: Array.Empty<GridMapDefinition.CellOverride>());

            var registryObject = CreateRoot("AI Brace Test Registry");
            var gridObject = CreateRoot("AI Brace Test Grid");
            var selectionObject = CreateRoot("AI Brace Test Selection");
            var routerObject = CreateRoot("AI Brace Test Router");
            var managerObject = CreateRoot("AI Brace Test Manager");

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

        public List<DamageEvent> CaptureDamageEvents()
        {
            var events = new List<DamageEvent>();
            EventBus.DamageApplied += eventData => events.Add(eventData);
            return events;
        }

        public void Dispose()
        {
            for (var i = rootObjects.Count - 1; i >= 0; i -= 1)
            {
                UnityEngine.Object.DestroyImmediate(rootObjects[i]);
            }

            UnityEngine.Object.DestroyImmediate(mapDefinition);
            UnityEngine.Object.DestroyImmediate(MeleeSlash);
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
                description: "AI brace reaction test ability.");
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
