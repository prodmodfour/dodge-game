using System;
using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using ReactionTactics.Actions;
using ReactionTactics.Grid;
using ReactionTactics.Input;
using ReactionTactics.Reactions;
using ReactionTactics.Turns;
using ReactionTactics.UI;
using ReactionTactics.Units;
using UnityEngine;

public sealed class MeleeGameplayFlowTests
{
    [Test]
    public void PlayerCanDeclareMeleeTargetReactsAwayAndMeleeIsAvoidedByMovement()
    {
        using (var fixture = new Fixture())
        {
            var actor = fixture.CreateUnit(
                "Melee Flow Actor",
                new UnitId(1),
                TeamId.Player,
                new GridPosition(0, 0, 0));
            var target = fixture.CreateUnit(
                "Melee Flow Target",
                new UnitId(2),
                TeamId.Enemy,
                new GridPosition(1, 0, 0));
            fixture.AssignLoadout(actor, fixture.Move, fixture.MeleeSlash, fixture.PassReaction);
            fixture.AssignLoadout(target, fixture.Move, fixture.PassReaction);
            Physics.SyncTransforms();

            var combatLog = new List<string>();
            var damageEvents = new List<DamageEvent>();
            var reactionTurns = new List<ReactionTurnStartedEvent>();
            var resolvedActions = new List<ActionResolvedEvent>();
            fixture.EventBus.CombatLogMessageAdded += entry => combatLog.Add(entry.Message);
            fixture.EventBus.DamageApplied += damageEvents.Add;
            fixture.EventBus.ReactionTurnStarted += reactionTurns.Add;
            fixture.EventBus.ActionResolved += resolvedActions.Add;

            Assert.That(fixture.Manager.StartCombat().IsSuccess, Is.True);
            Assert.That(fixture.Router.SelectUnit(actor).IsSuccess, Is.True);
            Assert.That(fixture.Router.SelectMeleeAttack().IsSuccess, Is.True);

            Assert.That(
                fixture.Picker.TryClickScreenPosition(fixture.GetUnitScreenPoint(target), out _, out var unitPick),
                Is.True);
            Assert.That(unitPick.Unit, Is.SameAs(target));
            Assert.That(fixture.Selection.SelectedTarget.Unit, Is.SameAs(target));
            Assert.That(fixture.DangerPreview.RefreshPreview(), Is.True, fixture.DangerPreview.LastFeedback);
            Assert.That(fixture.DangerPreview.CurrentPreview.ThreatenedUnits, Does.Contain(target));
            Assert.That(
                fixture.HighlightManager.GetHighlightedCells(GridHighlightCategory.ActionDanger),
                Does.Contain(target.CurrentGridPosition));

            var targetHpBeforeMelee = target.CurrentHP;
            var targetApBeforeReaction = target.CurrentAP;
            Assert.That(fixture.Router.ConfirmCurrentTarget().IsSuccess, Is.True);

            var pendingIntent = fixture.Manager.CurrentState.PendingActionIntent as ActionIntent;
            Assert.That(pendingIntent, Is.Not.Null);
            Assert.That(pendingIntent.Ability, Is.SameAs(fixture.MeleeSlash));
            Assert.That(pendingIntent.DeclaredTargetUnit, Is.SameAs(target));
            Assert.That(actor.CurrentAP, Is.EqualTo(actor.MaxAP - fixture.MeleeSlash.APCost));
            Assert.That(fixture.Manager.CurrentState.Phase, Is.EqualTo(CombatPhase.ReactionWindow));
            Assert.That(fixture.Manager.CurrentState.CurrentReactor, Is.SameAs(target));
            Assert.That(fixture.Selection.SelectedUnit, Is.SameAs(target));
            Assert.That(reactionTurns.Select(evt => evt.Reactor), Is.EqualTo(new[] { target }));

            Assert.That(fixture.Router.SelectMove().IsSuccess, Is.True);
            var safeDestination = new GridPosition(2, 0, 0);
            fixture.Selection.SetHoveredCell(safeDestination);
            Assert.That(fixture.SafetyPreview.RefreshPreview(), Is.True, fixture.SafetyPreview.LastFeedback);
            Assert.That(fixture.SafetyPreview.TryGetSafetyCell(safeDestination, out var safetyCell), Is.True);
            Assert.That(safetyCell.IsSafe, Is.True, safetyCell.Reason);

            Assert.That(
                fixture.Picker.TryClickScreenPosition(fixture.GetTileScreenPoint(safeDestination), out var cellPick, out _),
                Is.True);
            Assert.That(cellPick.Position, Is.EqualTo(safeDestination));
            Assert.That(fixture.Selection.SelectedTarget.CurrentCell, Is.EqualTo(safeDestination));
            Assert.That(fixture.Router.ConfirmCurrentTarget().IsSuccess, Is.True);

            Assert.That(target.CurrentGridPosition, Is.EqualTo(safeDestination));
            Assert.That(target.transform.position, Is.EqualTo(fixture.GridManager.Metrics.GridToWorldCenter(safeDestination)));
            Assert.That(target.CurrentAP, Is.EqualTo(targetApBeforeReaction - 1));
            Assert.That(target.CurrentHP, Is.EqualTo(targetHpBeforeMelee));
            Assert.That(damageEvents, Is.Empty);
            Assert.That(resolvedActions.Count, Is.EqualTo(1));
            Assert.That(resolvedActions[0].ActionIntent, Is.SameAs(pendingIntent));

            Assert.That(fixture.Manager.CurrentState.Phase, Is.EqualTo(CombatPhase.ActiveTurn));
            Assert.That(fixture.Manager.CurrentState.ActiveUnit, Is.SameAs(actor));
            Assert.That(fixture.Manager.CurrentState.ReactingUnit, Is.Null);
            Assert.That(fixture.Manager.CurrentState.PendingActionIntent, Is.Null);
            Assert.That(fixture.Manager.CurrentReactionWindow, Is.Null);
            Assert.That(fixture.Selection.SelectedUnit, Is.SameAs(actor));
            Assert.That(fixture.Selection.SelectedActionMode, Is.EqualTo(SelectionActionMode.None));
            Assert.That(fixture.Selection.SelectedTarget.HasTarget, Is.False);

            Assert.That(combatLog.Any(line => line.Contains("Reaction order for Melee Slash")), Is.True);
            Assert.That(combatLog.Any(line => line.Contains("reaction-moved") && line.Contains("Melee Slash")), Is.True);
            Assert.That(combatLog.Any(line => line.Contains("avoided Melee Slash") && line.Contains("moving out of range")), Is.True);
        }
    }

    private sealed class Fixture : IDisposable
    {
        private readonly List<GameObject> rootObjects = new List<GameObject>();
        private readonly List<ScriptableObject> assets = new List<ScriptableObject>();
        private readonly Dictionary<GridPosition, GridTileView> tilesByPosition = new Dictionary<GridPosition, GridTileView>();
        private readonly UnitStatsDefinition stats;

        public Fixture()
        {
            stats = ScriptableObject.CreateInstance<UnitStatsDefinition>();
            assets.Add(stats);
            stats.Configure(
                displayName: "Melee Flow Unit",
                maxHP: 10,
                maxAP: 6,
                movementAnimationSpeed: 4f,
                meleeRange: 1,
                teamColorHint: Color.white);

            Move = CreateAbility(
                abilityKey: "move",
                displayName: "Move",
                apCost: 0,
                usage: AbilityUsage.Action | AbilityUsage.Reaction,
                timing: AbilityTiming.Immediate,
                shape: AbilityShape.Self,
                range: 0,
                radius: 0,
                damage: 0,
                triggersReactions: false);
            MeleeSlash = CreateAbility(
                abilityKey: "melee_slash",
                displayName: "Melee Slash",
                apCost: 3,
                usage: AbilityUsage.Action,
                timing: AbilityTiming.Telegraphed,
                shape: AbilityShape.Melee,
                range: 0,
                radius: 0,
                damage: 4,
                triggersReactions: true);
            PassReaction = CreateAbility(
                abilityKey: ReactionEligibilityService.DefaultPassReactionAbilityKey,
                displayName: ReactionEligibilityService.DefaultPassReactionDisplayName,
                apCost: 0,
                usage: AbilityUsage.Reaction,
                timing: AbilityTiming.Immediate,
                shape: AbilityShape.Self,
                range: 0,
                radius: 0,
                damage: 0,
                triggersReactions: false);

            var mapDefinition = ScriptableObject.CreateInstance<GridMapDefinition>();
            assets.Add(mapDefinition);
            mapDefinition.Configure(
                width: 3,
                depth: 1,
                defaultHeightY: 0,
                overrides: Array.Empty<GridMapDefinition.CellOverride>());

            var registryObject = CreateRoot("Melee Gameplay Flow Registry");
            var gridObject = CreateRoot("Melee Gameplay Flow Grid");
            var inputObject = CreateRoot("Melee Gameplay Flow Input");
            var managerObject = CreateRoot("Melee Gameplay Flow Manager");
            var highlightObject = CreateRoot("Melee Gameplay Flow Highlights");
            var dangerPreviewObject = CreateRoot("Melee Gameplay Flow Danger Preview");
            var safetyPreviewObject = CreateRoot("Melee Gameplay Flow Safety Preview");
            var cameraObject = CreateRoot("Melee Gameplay Flow Camera");

            Camera = cameraObject.AddComponent<Camera>();
            Camera.pixelRect = new Rect(0f, 0f, 800f, 600f);
            Camera.nearClipPlane = 0.1f;
            Camera.farClipPlane = 100f;
            Camera.transform.position = new Vector3(1.5f, 0f, -5f);
            Camera.transform.rotation = Quaternion.identity;

            Registry = registryObject.AddComponent<UnitRegistry>();
            GridManager = gridObject.AddComponent<GridManager>();
            Assert.That(GridManager.SetMapDefinition(mapDefinition), Is.True);

            Selection = inputObject.AddComponent<SelectionController>();
            Picker = inputObject.AddComponent<GridPicker>();
            Picker.SourceCamera = Camera;
            Picker.LogClickedCells = false;
            Picker.LogClickedUnits = false;
            Selection.GridPicker = Picker;

            Router = inputObject.AddComponent<PlayerCommandRouter>();
            Router.SelectionController = Selection;
            Router.GridPicker = Picker;
            Router.KeyboardShortcutsEnabled = false;

            EventBus = managerObject.AddComponent<CombatEventBus>();
            Manager = managerObject.AddComponent<CombatManager>();
            Manager.Configure(Registry, GridManager, Router, EventBus);
            Manager.StartCombatOnStart = false;
            Manager.LogCombatStart = false;
            Manager.LogActionFlow = false;

            HighlightManager = highlightObject.AddComponent<GridHighlightManager>();
            HighlightManager.RebuildIndexFromSceneWhenMissing = false;
            RegisterTiles();

            DangerPreview = dangerPreviewObject.AddComponent<ActionDangerPreviewController>();
            DangerPreview.Configure(Selection, Picker, Manager, GridManager, Registry, HighlightManager);
            DangerPreview.Visible = false;

            SafetyPreview = safetyPreviewObject.AddComponent<ReactionMovementSafetyPreviewController>();
            SafetyPreview.Configure(Selection, Manager, GridManager, Registry, HighlightManager);
            SafetyPreview.Visible = false;
        }

        public UnitRegistry Registry { get; }

        public GridManager GridManager { get; }

        public SelectionController Selection { get; }

        public GridPicker Picker { get; }

        public PlayerCommandRouter Router { get; }

        public CombatEventBus EventBus { get; }

        public CombatManager Manager { get; }

        public GridHighlightManager HighlightManager { get; }

        public ActionDangerPreviewController DangerPreview { get; }

        public ReactionMovementSafetyPreviewController SafetyPreview { get; }

        public Camera Camera { get; }

        public AbilityDefinition Move { get; }

        public AbilityDefinition MeleeSlash { get; }

        public AbilityDefinition PassReaction { get; }

        public TacticalUnit CreateUnit(string name, UnitId unitId, TeamId team, GridPosition position)
        {
            var gameObject = GameObject.CreatePrimitive(PrimitiveType.Capsule);
            gameObject.name = name;
            gameObject.transform.SetParent(Registry.transform, worldPositionStays: false);
            gameObject.transform.position = GridManager.Metrics.GridToWorldCenter(position);
            var unit = gameObject.AddComponent<TacticalUnit>();
            unit.Initialize(unitId, team, stats, position);
            gameObject.AddComponent<GridPathMover>();
            return unit;
        }

        public void AssignLoadout(TacticalUnit unit, params AbilityDefinition[] abilities)
        {
            var loadout = unit.gameObject.AddComponent<UnitAbilityLoadout>();
            loadout.SetAbilities(abilities);
        }

        public Vector2 GetUnitScreenPoint(TacticalUnit unit)
        {
            return Camera.WorldToScreenPoint(unit.transform.position);
        }

        public Vector2 GetTileScreenPoint(GridPosition position)
        {
            return Camera.WorldToScreenPoint(tilesByPosition[position].transform.position);
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
                description: "Melee gameplay flow test ability.");
            return ability;
        }

        private void RegisterTiles()
        {
            for (var x = 0; x < 3; x += 1)
            {
                var position = new GridPosition(x, 0, 0);
                var tileObject = GameObject.CreatePrimitive(PrimitiveType.Cube);
                tileObject.name = $"Melee Gameplay Flow Tile {x}";
                tileObject.transform.position = GridManager.Metrics.GridToWorldCenter(position);
                rootObjects.Add(tileObject);
                var tile = tileObject.AddComponent<GridTileView>();
                tile.SetGridPosition(position);
                tilesByPosition.Add(position, tile);
                Assert.That(HighlightManager.RegisterTile(tile), Is.True);
            }

            Physics.SyncTransforms();
        }
    }
}
