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
using UnityEngine;

public sealed class PlayerAiHandoffTests
{
    [Test]
    public void EnemyAiActionHandsTurnBackToPlayerAfterPlayerReactionPasses()
    {
        using (var fixture = new Fixture(width: 2, depth: 1))
        {
            var player = fixture.CreateUnit("Handoff Player", new UnitId(1), TeamId.Player, new GridPosition(1, 0, 0));
            var enemy = fixture.CreateUnit("Handoff Enemy", new UnitId(2), TeamId.Enemy, new GridPosition(0, 0, 0));
            fixture.AssignLoadout(enemy, fixture.MeleeSlash);
            var damageEvents = fixture.CaptureDamageEvents();

            Assert.That(fixture.Manager.StartCombat().IsSuccess, Is.True);
            Assert.That(fixture.Manager.CurrentState.ActiveUnit, Is.SameAs(player));

            var playerEndTurnResult = fixture.Manager.EndActiveTurn();

            Assert.That(playerEndTurnResult.IsSuccess, Is.True, playerEndTurnResult.ErrorMessage);
            Assert.That(fixture.Manager.CurrentState.Phase, Is.EqualTo(CombatPhase.ReactionWindow));
            Assert.That(fixture.Manager.CurrentState.ActiveUnit, Is.SameAs(enemy));
            Assert.That(fixture.Manager.CurrentState.CurrentReactor, Is.SameAs(player));
            Assert.That(fixture.Manager.CurrentState.PendingActionIntent, Is.TypeOf<ActionIntent>());

            var passResult = fixture.Router.RequestPassReaction();

            Assert.That(passResult.IsSuccess, Is.True, passResult.ErrorMessage);
            Assert.That(damageEvents, Has.Count.EqualTo(1));
            Assert.That(damageEvents[0].Attacker, Is.SameAs(enemy));
            Assert.That(damageEvents[0].Target, Is.SameAs(player));
            Assert.That(player.CurrentHP, Is.EqualTo(player.MaxHP - fixture.MeleeSlash.Damage));
            Assert.That(fixture.Manager.CurrentReactionWindow, Is.Null);
            Assert.That(fixture.Manager.CurrentState.Phase, Is.EqualTo(CombatPhase.ActiveTurn));
            Assert.That(fixture.Manager.CurrentState.ActiveUnit, Is.SameAs(player));
            Assert.That(fixture.Manager.CurrentState.CurrentRound, Is.EqualTo(2));
            Assert.That(fixture.Manager.CurrentState.PendingActionIntent, Is.Null);
            Assert.That(enemy.CurrentAP, Is.EqualTo(enemy.MaxAP), "Round-start AP refresh should run after the AI turn is completed automatically.");
        }
    }

    [Test]
    public void PlayerActionAllowsEnemyAiReactionThenKeepsPlayerTurn()
    {
        using (var fixture = new Fixture(width: 3, depth: 1))
        {
            var player = fixture.CreateUnit("Player Actor", new UnitId(1), TeamId.Player, new GridPosition(0, 0, 0));
            var enemy = fixture.CreateUnit("Enemy Reactor", new UnitId(2), TeamId.Enemy, new GridPosition(1, 0, 0));
            fixture.AssignLoadout(player, fixture.MeleeSlash);
            var enemyStart = enemy.CurrentGridPosition;

            Assert.That(fixture.Manager.StartCombat().IsSuccess, Is.True);
            Assert.That(fixture.Router.SelectUnit(player).IsSuccess, Is.True);
            Assert.That(fixture.Router.SelectMeleeAttack().IsSuccess, Is.True);

            var declarationResult = fixture.Router.ConfirmTargetUnit(enemy);

            Assert.That(declarationResult.IsSuccess, Is.True, declarationResult.ErrorMessage);
            Assert.That(enemy.CurrentGridPosition, Is.Not.EqualTo(enemyStart), "Enemy AI should spend its own reaction turn rather than waiting for player input.");
            Assert.That(enemy.CurrentHP, Is.EqualTo(enemy.MaxHP), "The enemy AI reaction move should avoid the melee by final position.");
            Assert.That(fixture.Manager.CurrentReactionWindow, Is.Null);
            Assert.That(fixture.Manager.CurrentState.Phase, Is.EqualTo(CombatPhase.ActiveTurn));
            Assert.That(fixture.Manager.CurrentState.ActiveUnit, Is.SameAs(player));
            Assert.That(fixture.Manager.CurrentState.CurrentRound, Is.EqualTo(1));
            Assert.That(fixture.Manager.CurrentState.PendingActionIntent, Is.Null);
        }
    }

    [Test]
    public void RouterRejectsPlayerActiveCommandsDuringAiControlledEnemyTurn()
    {
        using (var fixture = new Fixture(width: 2, depth: 1))
        {
            var player = fixture.CreateUnit("Waiting Player", new UnitId(1), TeamId.Player, new GridPosition(1, 0, 0));
            var enemy = fixture.CreateUnit("AI Controlled Enemy", new UnitId(2), TeamId.Enemy, new GridPosition(0, 0, 0));
            fixture.AssignLoadout(enemy, fixture.MeleeSlash);
            fixture.Manager.CurrentState.SetState(1, CombatPhase.ActiveTurn, enemy);
            Assert.That(fixture.Selection.SelectUnit(enemy).IsSuccess, Is.True);
            var requests = new List<PlayerCommandRequest>();
            var rejections = new List<string>();
            fixture.Router.CommandRequested += requests.Add;
            fixture.Router.CommandRejected += result => rejections.Add(result.ErrorMessage);

            var attackResult = fixture.Router.SelectMeleeAttack();
            var endTurnResult = fixture.Router.RequestEndTurn();

            Assert.That(player.IsAlive, Is.True);
            Assert.That(attackResult.IsFailure, Is.True);
            Assert.That(endTurnResult.IsFailure, Is.True);
            Assert.That(rejections, Has.Count.EqualTo(2));
            Assert.That(rejections[0], Does.Contain("controlled by AI"));
            Assert.That(rejections[1], Does.Contain("controlled by AI"));
            Assert.That(requests, Is.Empty);
            Assert.That(fixture.Manager.CurrentState.Phase, Is.EqualTo(CombatPhase.ActiveTurn));
            Assert.That(fixture.Manager.CurrentState.ActiveUnit, Is.SameAs(enemy));
        }
    }

    [Test]
    public void EnemyAiActionThatDefeatsLastPlayerEntersCombatOverWithoutFurtherHandoff()
    {
        using (var fixture = new Fixture(width: 2, depth: 1))
        {
            var player = fixture.CreateUnit("Last Player", new UnitId(1), TeamId.Player, new GridPosition(1, 0, 0));
            var enemy = fixture.CreateUnit("Winning Enemy", new UnitId(2), TeamId.Enemy, new GridPosition(0, 0, 0));
            fixture.AssignLoadout(enemy, fixture.MeleeSlash);
            Assert.That(player.ApplyDamage(player.MaxHP - fixture.MeleeSlash.Damage, DamageSource.Environmental("handoff setup")).IsSuccess, Is.True);

            Assert.That(fixture.Manager.StartCombat().IsSuccess, Is.True);
            Assert.That(fixture.Manager.EndActiveTurn().IsSuccess, Is.True);
            Assert.That(fixture.Manager.CurrentState.Phase, Is.EqualTo(CombatPhase.ReactionWindow));

            var passResult = fixture.Router.RequestPassReaction();

            Assert.That(passResult.IsSuccess, Is.True, passResult.ErrorMessage);
            Assert.That(player.IsDead, Is.True);
            Assert.That(fixture.Manager.CurrentState.Phase, Is.EqualTo(CombatPhase.CombatOver));
            Assert.That(fixture.Manager.CurrentState.ActiveUnit, Is.Null);
            Assert.That(fixture.Manager.CurrentReactionWindow, Is.Null);
            Assert.That(fixture.Manager.HasCombatEndOutcome, Is.True);
            Assert.That(fixture.Manager.HasWinningTeam, Is.True);
            Assert.That(fixture.Manager.WinningTeam, Is.EqualTo(TeamId.Enemy));
        }
    }

    private sealed class Fixture : IDisposable
    {
        private readonly List<GameObject> rootObjects = new List<GameObject>();
        private readonly List<ScriptableObject> assets = new List<ScriptableObject>();
        private readonly UnitStatsDefinition stats;

        public Fixture(int width, int depth)
        {
            stats = ScriptableObject.CreateInstance<UnitStatsDefinition>();
            assets.Add(stats);
            stats.Configure(
                displayName: "Player/AI Handoff Test Unit",
                maxHP: 10,
                maxAP: 6,
                movementAnimationSpeed: 4f,
                meleeRange: 1,
                teamColorHint: Color.white);

            MeleeSlash = CreateAbility(
                AiController.DefaultMeleeSlashAbilityKey,
                AiController.DefaultMeleeSlashDisplayName,
                apCost: 4,
                usage: AbilityUsage.Action,
                timing: AbilityTiming.Telegraphed,
                shape: AbilityShape.Melee,
                range: 0,
                radius: 0,
                damage: 4,
                triggersReactions: true);

            var mapDefinition = ScriptableObject.CreateInstance<GridMapDefinition>();
            assets.Add(mapDefinition);
            mapDefinition.Configure(
                width: width,
                depth: depth,
                defaultHeightY: 0,
                overrides: Array.Empty<GridMapDefinition.CellOverride>());

            var registryObject = CreateRoot("Player AI Handoff Registry");
            var gridObject = CreateRoot("Player AI Handoff Grid");
            var inputObject = CreateRoot("Player AI Handoff Input");
            var managerObject = CreateRoot("Player AI Handoff Manager");

            Registry = registryObject.AddComponent<UnitRegistry>();
            GridManager = gridObject.AddComponent<GridManager>();
            Assert.That(GridManager.SetMapDefinition(mapDefinition), Is.True);

            Selection = inputObject.AddComponent<SelectionController>();
            Router = inputObject.AddComponent<PlayerCommandRouter>();
            Router.SelectionController = Selection;
            Router.KeyboardShortcutsEnabled = false;

            EventBus = managerObject.AddComponent<CombatEventBus>();
            Controller = managerObject.AddComponent<AiController>();
            Controller.SkipDecisionPacing = true;
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
            gameObject.transform.position = GridManager.Metrics.GridToWorldCenter(position);
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
            var damageEvents = new List<DamageEvent>();
            EventBus.DamageApplied += damageEvents.Add;
            return damageEvents;
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
                description: "Player/AI handoff test ability.");
            return ability;
        }
    }
}
