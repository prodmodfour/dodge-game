using System;
using System.Collections.Generic;
using NUnit.Framework;
using ReactionTactics.AI;
using ReactionTactics.Actions;
using ReactionTactics.Core;
using ReactionTactics.Grid;
using ReactionTactics.Input;
using ReactionTactics.Reactions;
using ReactionTactics.Turns;
using ReactionTactics.Units;
using UnityEditor;
using UnityEngine;

namespace Ai
{
    public sealed class AiIntegrationTests
    {
        [Test]
        public void AiSelectsNearestTargetAndMovesTowardItWhenNoAttackIsAvailable()
        {
            using (var fixture = new Fixture(width: 7, depth: 1))
            {
                var farPlayer = fixture.CreateUnit("AI Integration Far Player", new UnitId(1), TeamId.Player, new GridPosition(6, 0, 0));
                var enemy = fixture.CreateUnit("AI Integration Enemy Mover", new UnitId(2), TeamId.Enemy, new GridPosition(0, 0, 0));
                var nearPlayer = fixture.CreateUnit("AI Integration Near Player", new UnitId(3), TeamId.Player, new GridPosition(5, 0, 0));

                var target = fixture.Controller.SelectNearestHostileTarget(enemy, fixture.Registry.GetLivingUnits());

                Assert.That(target, Is.SameAs(nearPlayer));
                Assert.That(target, Is.Not.SameAs(farPlayer));
                Assert.That(fixture.Manager.StartCombat().IsSuccess, Is.True);
                Assert.That(fixture.Manager.CurrentState.ActiveUnit, Is.SameAs(farPlayer));

                Assert.That(fixture.Manager.EndActiveTurn().IsSuccess, Is.True);
                Assert.That(fixture.Manager.CurrentState.ActiveUnit, Is.SameAs(nearPlayer));
                var result = fixture.Manager.EndActiveTurn();

                Assert.That(result.IsSuccess, Is.True, result.ErrorMessage);
                Assert.That(enemy.CurrentGridPosition, Is.EqualTo(new GridPosition(4, 0, 0)));
                Assert.That(enemy.CurrentAP, Is.EqualTo(enemy.MaxAP), "The delegated enemy turn should end and round-start AP refresh should run after movement.");
                Assert.That(fixture.Registry.IsOccupied(new GridPosition(0, 0, 0)), Is.False);
                Assert.That(fixture.Registry.TryGetOccupant(enemy.CurrentGridPosition, out var occupantId), Is.True);
                Assert.That(occupantId, Is.EqualTo(enemy.UnitId));
                Assert.That(fixture.Manager.CurrentReactionWindow, Is.Null);
                Assert.That(fixture.Manager.CurrentState.Phase, Is.EqualTo(CombatPhase.ActiveTurn));
                Assert.That(fixture.Manager.CurrentState.ActiveUnit, Is.SameAs(farPlayer));
                Assert.That(fixture.Manager.CurrentState.CurrentRound, Is.EqualTo(2));
            }
        }

        [Test]
        public void AiActiveTurnDeclaresAttackThroughNormalIntentFlow()
        {
            using (var fixture = new Fixture(width: 3, depth: 1))
            {
                var player = fixture.CreateUnit("AI Integration Melee Target", new UnitId(1), TeamId.Player, new GridPosition(1, 0, 0));
                var enemy = fixture.CreateUnit("AI Integration Melee Attacker", new UnitId(2), TeamId.Enemy, new GridPosition(0, 0, 0));
                fixture.AssignLoadout(enemy, fixture.MeleeSlash);
                var declarations = fixture.CaptureActionDeclarations();

                Assert.That(fixture.Manager.StartCombat().IsSuccess, Is.True);
                var result = fixture.Manager.EndActiveTurn();

                Assert.That(result.IsSuccess, Is.True, result.ErrorMessage);
                Assert.That(declarations, Has.Count.EqualTo(1));
                var intent = declarations[0];
                Assert.That(intent.Actor, Is.SameAs(enemy));
                Assert.That(intent.Ability, Is.SameAs(fixture.MeleeSlash));
                Assert.That(intent.Target.TargetUnit, Is.SameAs(player));
                Assert.That(enemy.CurrentAP, Is.EqualTo(enemy.MaxAP - fixture.MeleeSlash.APCost));
                Assert.That(player.CurrentHP, Is.EqualTo(player.MaxHP), "The declared AI attack should wait for the player reaction before resolving.");
                Assert.That(fixture.Manager.CurrentReactionWindow, Is.Not.Null);
                Assert.That(fixture.Manager.CurrentReactionWindow.SourceIntent, Is.SameAs(intent));
                Assert.That(fixture.Manager.CurrentState.Phase, Is.EqualTo(CombatPhase.ReactionWindow));
                Assert.That(fixture.Manager.CurrentState.ActiveUnit, Is.SameAs(enemy));
                Assert.That(fixture.Manager.CurrentState.ReactingUnit, Is.SameAs(player));
                Assert.That(fixture.Manager.CurrentState.PendingActionIntent, Is.SameAs(intent));
            }
        }

        [Test]
        public void AiReactionMovementAvoidsPendingActionWhenPossible()
        {
            using (var fixture = new Fixture(width: 4, depth: 1))
            {
                var actor = fixture.CreateUnit("AI Integration Player Actor", new UnitId(1), TeamId.Player, new GridPosition(0, 0, 0));
                var enemyReactor = fixture.CreateUnit("AI Integration Moving Reactor", new UnitId(2), TeamId.Enemy, new GridPosition(1, 0, 0));
                fixture.AssignLoadout(actor, fixture.MeleeSlash);

                Assert.That(fixture.Manager.StartCombat().IsSuccess, Is.True);
                var start = enemyReactor.CurrentGridPosition;
                var apBeforeReaction = enemyReactor.CurrentAP;
                var declarationResult = fixture.DeclareMelee(actor, enemyReactor);

                Assert.That(declarationResult.IsSuccess, Is.True, declarationResult.ErrorMessage);
                Assert.That(enemyReactor.CurrentGridPosition, Is.EqualTo(new GridPosition(2, 0, 0)));
                Assert.That(enemyReactor.CurrentAP, Is.EqualTo(apBeforeReaction - 1));
                Assert.That(enemyReactor.CurrentHP, Is.EqualTo(enemyReactor.MaxHP), "AI movement should make the original melee miss by final position.");
                Assert.That(fixture.Registry.IsOccupied(start), Is.False);
                Assert.That(fixture.Registry.IsOccupied(enemyReactor.CurrentGridPosition), Is.True);
                Assert.That(fixture.Manager.CurrentReactionWindow, Is.Null);
                Assert.That(fixture.Manager.CurrentState.Phase, Is.EqualTo(CombatPhase.ActiveTurn));
                Assert.That(fixture.Manager.CurrentState.ActiveUnit, Is.SameAs(actor));
                Assert.That(fixture.Manager.CurrentState.PendingActionIntent, Is.Null);
            }
        }

        [Test]
        public void AiBracesWhenThreatenedAndNoSafeReactionMoveExists()
        {
            using (var fixture = new Fixture(width: 2, depth: 1))
            {
                var actor = fixture.CreateUnit("AI Integration Brace Actor", new UnitId(1), TeamId.Player, new GridPosition(0, 0, 0));
                var enemyReactor = fixture.CreateUnit("AI Integration Brace Reactor", new UnitId(2), TeamId.Enemy, new GridPosition(1, 0, 0));
                fixture.AssignLoadout(actor, fixture.MeleeSlash);
                var damageEvents = fixture.CaptureDamageEvents();

                Assert.That(fixture.Manager.StartCombat().IsSuccess, Is.True);
                var start = enemyReactor.CurrentGridPosition;
                var apBeforeReaction = enemyReactor.CurrentAP;
                var declarationResult = fixture.DeclareMelee(actor, enemyReactor);

                Assert.That(declarationResult.IsSuccess, Is.True, declarationResult.ErrorMessage);
                Assert.That(enemyReactor.CurrentGridPosition, Is.EqualTo(start));
                Assert.That(enemyReactor.CurrentAP, Is.EqualTo(apBeforeReaction - BraceReactionCommand.DefaultApCost));
                Assert.That(enemyReactor.CurrentHP, Is.EqualTo(enemyReactor.MaxHP - (fixture.MeleeSlash.Damage - BraceReactionCommand.DefaultDamageReduction)));
                Assert.That(enemyReactor.BracedUntilNextHit, Is.False);
                Assert.That(damageEvents, Has.Count.EqualTo(1));
                Assert.That(damageEvents[0].Target, Is.SameAs(enemyReactor));
                Assert.That(damageEvents[0].WasBraced, Is.True);
                Assert.That(damageEvents[0].FinalAmount, Is.EqualTo(fixture.MeleeSlash.Damage - BraceReactionCommand.DefaultDamageReduction));
                Assert.That(fixture.Manager.CurrentReactionWindow, Is.Null);
                Assert.That(fixture.Manager.CurrentState.Phase, Is.EqualTo(CombatPhase.ActiveTurn));
                Assert.That(fixture.Manager.CurrentState.ActiveUnit, Is.SameAs(actor));
            }
        }

        private sealed class Fixture : IDisposable
        {
            private readonly List<GameObject> rootObjects = new List<GameObject>();
            private readonly List<AbilityDefinition> abilities = new List<AbilityDefinition>();
            private readonly UnitStatsDefinition stats;
            private readonly GridMapDefinition mapDefinition;

            public Fixture(int width, int depth)
            {
                stats = ScriptableObject.CreateInstance<UnitStatsDefinition>();
                stats.Configure(
                    displayName: "AI Integration Test Unit",
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

                mapDefinition = ScriptableObject.CreateInstance<GridMapDefinition>();
                mapDefinition.Configure(
                    width: width,
                    depth: depth,
                    defaultHeightY: 0,
                    overrides: Array.Empty<GridMapDefinition.CellOverride>());

                var registryObject = CreateRoot("AI Integration Test Registry");
                var gridObject = CreateRoot("AI Integration Test Grid");
                var selectionObject = CreateRoot("AI Integration Test Selection");
                var routerObject = CreateRoot("AI Integration Test Router");
                var managerObject = CreateRoot("AI Integration Test Manager");

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
                Controller.SkipDecisionPacing = true;
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

            public void AssignLoadout(TacticalUnit unit, params AbilityDefinition[] assignedAbilities)
            {
                var loadout = unit.gameObject.AddComponent<UnitAbilityLoadout>();
                loadout.SetAbilities(assignedAbilities);
            }

            public List<ActionIntent> CaptureActionDeclarations()
            {
                var declarations = new List<ActionIntent>();
                EventBus.ActionDeclared += eventData => declarations.Add(eventData.ActionIntent as ActionIntent);
                return declarations;
            }

            public List<DamageEvent> CaptureDamageEvents()
            {
                var events = new List<DamageEvent>();
                EventBus.DamageApplied += eventData => events.Add(eventData);
                return events;
            }

            public TacticalResult DeclareMelee(TacticalUnit actor, TacticalUnit target)
            {
                var selectUnitResult = Router.SelectUnit(actor);
                if (selectUnitResult.IsFailure)
                {
                    return selectUnitResult;
                }

                var selectMeleeResult = Router.SelectMeleeAttack();
                if (selectMeleeResult.IsFailure)
                {
                    return selectMeleeResult;
                }

                return Router.ConfirmTargetUnit(target);
            }

            public void Dispose()
            {
                for (var i = rootObjects.Count - 1; i >= 0; i -= 1)
                {
                    UnityEngine.Object.DestroyImmediate(rootObjects[i]);
                }

                UnityEngine.Object.DestroyImmediate(mapDefinition);
                for (var i = abilities.Count - 1; i >= 0; i -= 1)
                {
                    UnityEngine.Object.DestroyImmediate(abilities[i]);
                }

                UnityEngine.Object.DestroyImmediate(stats);
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
                    description: "AI integration test ability.");
                abilities.Add(ability);
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
}
