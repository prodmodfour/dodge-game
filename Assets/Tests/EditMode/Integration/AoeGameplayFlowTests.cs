using System;
using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using ReactionTactics.Actions;
using ReactionTactics.Grid;
using ReactionTactics.Input;
using ReactionTactics.Reactions;
using ReactionTactics.Targeting;
using ReactionTactics.Turns;
using ReactionTactics.UI;
using ReactionTactics.Units;
using UnityEngine;

public sealed class AoeGameplayFlowTests
{
    [Test]
    public void PlayerCanPreviewDeclareAoeTargetReactsOutAndAoeIsAvoidedByMovement()
    {
        using (var fixture = new Fixture())
        {
            var actor = fixture.CreateUnit(
                "AoE Flow Actor",
                new UnitId(1),
                TeamId.Player,
                new GridPosition(0, 0, 1));
            var target = fixture.CreateUnit(
                "AoE Flow Target",
                new UnitId(2),
                TeamId.Enemy,
                new GridPosition(2, 0, 1));
            fixture.AssignLoadout(actor, fixture.Move, fixture.Fireball, fixture.PassReaction);
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
            Assert.That(fixture.Router.SelectAreaOfEffectAttack().IsSuccess, Is.True);

            var aimCell = new GridPosition(2, 0, 2);
            fixture.Selection.SetHoveredCell(aimCell);
            Assert.That(fixture.DangerPreview.RefreshPreview(), Is.True, fixture.DangerPreview.LastFeedback);
            var expectedAoeCells = AreaShapeService.GetRadiusCells(
                aimCell,
                fixture.Fireball.Radius,
                fixture.GridManager.CurrentMap).ToArray();
            Assert.That(fixture.DangerPreview.CurrentPreview.Ability, Is.SameAs(fixture.Fireball));
            Assert.That(fixture.DangerPreview.CurrentPreview.Target.TargetCell, Is.EqualTo(aimCell));
            Assert.That(fixture.DangerPreview.CurrentPreview.AffectedCells.ToArray(), Is.EqualTo(expectedAoeCells));
            Assert.That(fixture.DangerPreview.CurrentPreview.ThreatenedUnits, Does.Contain(target));
            Assert.That(
                fixture.HighlightManager.GetHighlightedCells(GridHighlightCategory.ActionDanger).ToArray(),
                Is.EquivalentTo(expectedAoeCells));
            Assert.That(fixture.DangerPreview.LastFeedback, Does.Contain("Fireball"));

            Assert.That(
                fixture.Picker.TryClickScreenPosition(fixture.GetTileScreenPoint(aimCell), out var aimPick, out _),
                Is.True);
            Assert.That(aimPick.Position, Is.EqualTo(aimCell));
            Assert.That(fixture.Selection.SelectedTarget.CurrentCell, Is.EqualTo(aimCell));

            var targetHpBeforeAoe = target.CurrentHP;
            var targetApBeforeReaction = target.CurrentAP;
            Assert.That(fixture.Router.ConfirmCurrentTarget().IsSuccess, Is.True);

            var pendingIntent = fixture.Manager.CurrentState.PendingActionIntent as ActionIntent;
            Assert.That(pendingIntent, Is.Not.Null);
            Assert.That(pendingIntent.Ability, Is.SameAs(fixture.Fireball));
            Assert.That(pendingIntent.Target.TargetCell, Is.EqualTo(aimCell));
            Assert.That(pendingIntent.DeclaredAffectedCells.ToArray(), Is.EqualTo(expectedAoeCells));
            Assert.That(actor.CurrentAP, Is.EqualTo(actor.MaxAP - fixture.Fireball.APCost));
            Assert.That(fixture.Manager.CurrentState.Phase, Is.EqualTo(CombatPhase.ReactionWindow));
            Assert.That(fixture.Manager.CurrentState.CurrentReactor, Is.SameAs(target));
            Assert.That(fixture.Selection.SelectedUnit, Is.SameAs(target));
            Assert.That(reactionTurns.Select(evt => evt.Reactor), Is.EqualTo(new[] { target }));

            Assert.That(fixture.Router.SelectMove().IsSuccess, Is.True);
            var safeDestination = new GridPosition(4, 0, 1);
            fixture.Selection.SetHoveredCell(safeDestination);
            Assert.That(fixture.SafetyPreview.RefreshPreview(), Is.True, fixture.SafetyPreview.LastFeedback);
            Assert.That(fixture.SafetyPreview.TryGetSafetyCell(safeDestination, out var safetyCell), Is.True);
            Assert.That(safetyCell.IsSafe, Is.True, safetyCell.Reason);
            Assert.That(safetyCell.Reason, Does.Contain("outside the declared AoE"));

            Assert.That(
                fixture.Picker.TryClickScreenPosition(fixture.GetTileScreenPoint(safeDestination), out var safePick, out _),
                Is.True);
            Assert.That(safePick.Position, Is.EqualTo(safeDestination));
            Assert.That(fixture.Selection.SelectedTarget.CurrentCell, Is.EqualTo(safeDestination));
            Assert.That(fixture.Router.ConfirmCurrentTarget().IsSuccess, Is.True);

            Assert.That(target.CurrentGridPosition, Is.EqualTo(safeDestination));
            Assert.That(target.transform.position, Is.EqualTo(fixture.GridManager.Metrics.GridToWorldCenter(safeDestination)));
            Assert.That(target.CurrentAP, Is.EqualTo(targetApBeforeReaction - 2));
            Assert.That(target.CurrentHP, Is.EqualTo(targetHpBeforeAoe));
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

            Assert.That(combatLog.Any(line => line.Contains("Reaction order for Fireball")), Is.True);
            Assert.That(combatLog.Any(line => line.Contains("reaction-moved") && line.Contains("Fireball")), Is.True);
            Assert.That(combatLog.Any(line => line.Contains("resolved as an AoE") && line.Contains("avoided by movement")), Is.True);
            Assert.That(combatLog.Any(line => line.Contains("no dodge or accuracy roll")), Is.True);
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
                displayName: "AoE Flow Unit",
                maxHP: 10,
                maxAP: 7,
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
            Fireball = CreateAbility(
                abilityKey: "fireball",
                displayName: "Fireball",
                apCost: 5,
                usage: AbilityUsage.Action,
                timing: AbilityTiming.Telegraphed,
                shape: AbilityShape.Radius,
                range: 5,
                radius: 1,
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
                width: 5,
                depth: 4,
                defaultHeightY: 0,
                overrides: Array.Empty<GridMapDefinition.CellOverride>());

            var registryObject = CreateRoot("AoE Gameplay Flow Registry");
            var gridObject = CreateRoot("AoE Gameplay Flow Grid");
            var inputObject = CreateRoot("AoE Gameplay Flow Input");
            var managerObject = CreateRoot("AoE Gameplay Flow Manager");
            var highlightObject = CreateRoot("AoE Gameplay Flow Highlights");
            var dangerPreviewObject = CreateRoot("AoE Gameplay Flow Danger Preview");
            var safetyPreviewObject = CreateRoot("AoE Gameplay Flow Safety Preview");
            var cameraObject = CreateRoot("AoE Gameplay Flow Camera");

            Camera = cameraObject.AddComponent<Camera>();
            Camera.pixelRect = new Rect(0f, 0f, 800f, 600f);
            Camera.orthographic = true;
            Camera.orthographicSize = 4f;
            Camera.nearClipPlane = 0.1f;
            Camera.farClipPlane = 100f;
            Camera.transform.position = new Vector3(2f, 10f, 1.5f);
            Camera.transform.rotation = Quaternion.Euler(90f, 0f, 0f);

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

        public AbilityDefinition Fireball { get; }

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
                description: "AoE gameplay flow test ability.");
            return ability;
        }

        private void RegisterTiles()
        {
            for (var x = 0; x < 5; x += 1)
            {
                for (var z = 0; z < 4; z += 1)
                {
                    var position = new GridPosition(x, 0, z);
                    var tileObject = GameObject.CreatePrimitive(PrimitiveType.Cube);
                    tileObject.name = $"AoE Gameplay Flow Tile {x},{z}";
                    tileObject.transform.position = GridManager.Metrics.GridToWorldCenter(position);
                    rootObjects.Add(tileObject);
                    var tile = tileObject.AddComponent<GridTileView>();
                    tile.SetGridPosition(position);
                    tilesByPosition.Add(position, tile);
                    Assert.That(HighlightManager.RegisterTile(tile), Is.True);
                }
            }

            Physics.SyncTransforms();
        }
    }
}
