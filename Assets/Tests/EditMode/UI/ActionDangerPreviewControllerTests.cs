using System;
using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using ReactionTactics.Actions;
using ReactionTactics.Grid;
using ReactionTactics.Input;
using ReactionTactics.Targeting;
using ReactionTactics.Turns;
using ReactionTactics.UI;
using ReactionTactics.Units;
using UnityEditor;
using UnityEngine;

namespace ReactionTactics.Tests.EditMode.UI
{
    public sealed class ActionDangerPreviewControllerTests
    {
        [Test]
        public void RefreshPreviewHighlightsValidMeleeTargetEnemyWithoutSpendingAP()
        {
            using (var fixture = new Fixture())
            {
                fixture.CreateStandardUnits();
                Assert.That(fixture.Manager.StartCombat().IsSuccess, Is.True);
                var startingAP = fixture.Actor.CurrentAP;
                Assert.That(fixture.Selection.SelectUnit(fixture.Actor).IsSuccess, Is.True);
                Assert.That(fixture.Selection.SetActionMode(SelectionActionMode.Melee).IsSuccess, Is.True);
                fixture.Selection.SetHoveredCell(fixture.Enemy.CurrentGridPosition);

                var refreshed = fixture.PreviewController.RefreshPreview();

                Assert.That(refreshed, Is.True, fixture.PreviewController.LastFeedback);
                Assert.That(fixture.PreviewController.HasActivePreview, Is.True);
                Assert.That(fixture.PreviewController.CurrentPreview.IsValid, Is.True, fixture.PreviewController.CurrentPreview.InvalidReason);
                Assert.That(fixture.PreviewController.CurrentPreview.ThreatenedUnits.ToArray(), Is.EqualTo(new[] { fixture.Enemy }));
                Assert.That(
                    fixture.HighlightManager.GetHighlightedCells(GridHighlightCategory.ActionDanger).ToArray(),
                    Is.EquivalentTo(new[] { fixture.Enemy.CurrentGridPosition }));
                Assert.That(fixture.PreviewController.LastFeedback, Does.Contain("Melee Slash"));
                Assert.That(fixture.PreviewController.LastFeedback, Does.Contain(fixture.Enemy.DisplayName));
                Assert.That(fixture.Actor.CurrentAP, Is.EqualTo(startingAP));
            }
        }

        [Test]
        public void RefreshPreviewHighlightsConeCellsFromHoveredTargetCell()
        {
            using (var fixture = new Fixture())
            {
                fixture.CreateStandardUnits();
                Assert.That(fixture.Manager.StartCombat().IsSuccess, Is.True);
                Assert.That(fixture.Selection.SelectUnit(fixture.Actor).IsSuccess, Is.True);
                Assert.That(fixture.Selection.SetActionMode(SelectionActionMode.Cone).IsSuccess, Is.True);
                var targetCell = new GridPosition(3, 0, 1);
                fixture.Selection.SetHoveredCell(targetCell);

                var refreshed = fixture.PreviewController.RefreshPreview();

                var expectedDangerCells = AreaShapeService.GetConeCells(
                    fixture.Actor.CurrentGridPosition,
                    CardinalDirection.East,
                    fixture.Cone.Range,
                    fixture.GridManager.CurrentMap).ToArray();
                Assert.That(refreshed, Is.True, fixture.PreviewController.LastFeedback);
                Assert.That(fixture.PreviewController.CurrentPreview.IsValid, Is.True, fixture.PreviewController.CurrentPreview.InvalidReason);
                Assert.That(
                    fixture.HighlightManager.GetHighlightedCells(GridHighlightCategory.ActionDanger).ToArray(),
                    Is.EquivalentTo(expectedDangerCells));
                Assert.That(fixture.PreviewController.CurrentPreview.ThreatenedUnits.ToArray(), Has.Member(fixture.Enemy));
                Assert.That(fixture.PreviewController.LastFeedback, Does.Contain("Cone Shot"));
            }
        }

        [Test]
        public void RefreshPreviewHighlightsRadiusCellsAndThreatenedUnitsFromHoveredTargetCell()
        {
            using (var fixture = new Fixture())
            {
                fixture.CreateStandardUnits();
                Assert.That(fixture.Manager.StartCombat().IsSuccess, Is.True);
                Assert.That(fixture.Selection.SelectUnit(fixture.Actor).IsSuccess, Is.True);
                Assert.That(fixture.Selection.SetActionMode(SelectionActionMode.AreaOfEffect).IsSuccess, Is.True);
                var targetCell = fixture.Enemy.CurrentGridPosition;
                fixture.Selection.SetHoveredCell(targetCell);

                var refreshed = fixture.PreviewController.RefreshPreview();

                var expectedDangerCells = AreaShapeService.GetRadiusCells(
                    targetCell,
                    fixture.Fireball.Radius,
                    fixture.GridManager.CurrentMap).ToArray();
                Assert.That(refreshed, Is.True, fixture.PreviewController.LastFeedback);
                Assert.That(fixture.PreviewController.CurrentPreview.IsValid, Is.True, fixture.PreviewController.CurrentPreview.InvalidReason);
                Assert.That(
                    fixture.HighlightManager.GetHighlightedCells(GridHighlightCategory.ActionDanger).ToArray(),
                    Is.EquivalentTo(expectedDangerCells));
                Assert.That(fixture.PreviewController.CurrentPreview.ThreatenedUnits.ToArray(), Has.Member(fixture.Enemy));
                Assert.That(fixture.PreviewController.LastFeedback, Does.Contain("Fireball"));
            }
        }

        [Test]
        public void RefreshPreviewClearsDangerHighlightsWhenAttackModeIsCancelled()
        {
            using (var fixture = new Fixture())
            {
                fixture.CreateStandardUnits();
                Assert.That(fixture.Manager.StartCombat().IsSuccess, Is.True);
                Assert.That(fixture.Selection.SelectUnit(fixture.Actor).IsSuccess, Is.True);
                Assert.That(fixture.Selection.SetActionMode(SelectionActionMode.AreaOfEffect).IsSuccess, Is.True);
                fixture.Selection.SetHoveredCell(fixture.Enemy.CurrentGridPosition);
                Assert.That(fixture.PreviewController.RefreshPreview(), Is.True);
                Assert.That(fixture.HighlightManager.GetHighlightedCells(GridHighlightCategory.ActionDanger), Is.Not.Empty);

                fixture.Selection.ClearActionMode();
                var refreshed = fixture.PreviewController.RefreshPreview();

                Assert.That(refreshed, Is.False);
                Assert.That(fixture.PreviewController.HasActivePreview, Is.False);
                Assert.That(fixture.PreviewController.LastFeedback, Is.Empty);
                Assert.That(fixture.HighlightManager.GetHighlightedCells(GridHighlightCategory.ActionDanger), Is.Empty);
            }
        }

        private sealed class Fixture : IDisposable
        {
            private readonly List<GameObject> gameObjects = new List<GameObject>();
            private readonly List<UnityEngine.Object> assets = new List<UnityEngine.Object>();
            private readonly GridMapDefinition mapDefinition;
            private readonly UnitStatsDefinition actorStats;
            private readonly UnitStatsDefinition enemyStats;
            private readonly UnitStatsDefinition allyStats;

            public Fixture()
            {
                mapDefinition = ScriptableObject.CreateInstance<GridMapDefinition>();
                assets.Add(mapDefinition);
                mapDefinition.Configure(
                    width: 5,
                    depth: 3,
                    defaultHeightY: 0,
                    overrides: Array.Empty<GridMapDefinition.CellOverride>());

                actorStats = CreateStats("Preview Actor");
                enemyStats = CreateStats("Preview Enemy");
                allyStats = CreateStats("Preview Ally");

                Melee = CreateAbility("melee_slash", "Melee Slash", AbilityShape.Melee, apCost: 3, range: 0, radius: 0, damage: 4);
                Cone = CreateAbility("cone_shot", "Cone Shot", AbilityShape.Cone, apCost: 4, range: 4, radius: 0, damage: 3);
                Fireball = CreateAbility("fireball", "Fireball", AbilityShape.Radius, apCost: 5, range: 5, radius: 1, damage: 4);

                var registryObject = CreateObject("Action Danger Registry");
                var gridObject = CreateObject("Action Danger Grid");
                var inputObject = CreateObject("Action Danger Input");
                var managerObject = CreateObject("Action Danger Manager");
                var highlightObject = CreateObject("Action Danger Highlights");
                var previewObject = CreateObject("Action Danger Preview Controller");

                Registry = registryObject.AddComponent<UnitRegistry>();
                GridManager = gridObject.AddComponent<GridManager>();
                AssignMapDefinition(GridManager, mapDefinition);
                Assert.That(GridManager.RebuildMap(), Is.True);

                Selection = inputObject.AddComponent<SelectionController>();
                Router = inputObject.AddComponent<PlayerCommandRouter>();
                Router.SelectionController = Selection;
                EventBus = managerObject.AddComponent<CombatEventBus>();
                Manager = managerObject.AddComponent<CombatManager>();
                Manager.Configure(Registry, GridManager, Router, EventBus);
                Manager.StartCombatOnStart = false;
                Manager.LogCombatStart = false;
                Manager.LogActionFlow = false;

                HighlightManager = highlightObject.AddComponent<GridHighlightManager>();
                PreviewController = previewObject.AddComponent<ActionDangerPreviewController>();
                PreviewController.Configure(Selection, null, Manager, GridManager, Registry, HighlightManager);
                PreviewController.Visible = false;
            }

            public UnitRegistry Registry { get; }

            public GridManager GridManager { get; }

            public SelectionController Selection { get; }

            public PlayerCommandRouter Router { get; }

            public CombatEventBus EventBus { get; }

            public CombatManager Manager { get; }

            public GridHighlightManager HighlightManager { get; }

            public ActionDangerPreviewController PreviewController { get; }

            public TacticalUnit Actor { get; private set; }

            public TacticalUnit Enemy { get; private set; }

            public TacticalUnit Ally { get; private set; }

            public AbilityDefinition Melee { get; }

            public AbilityDefinition Cone { get; }

            public AbilityDefinition Fireball { get; }

            public void CreateStandardUnits()
            {
                Actor = CreateUnit("Action Danger Actor", new UnitId(1), TeamId.Player, actorStats, new GridPosition(0, 0, 1));
                Enemy = CreateUnit("Action Danger Enemy", new UnitId(2), TeamId.Enemy, enemyStats, new GridPosition(1, 0, 1));
                Ally = CreateUnit("Action Danger Ally", new UnitId(3), TeamId.Player, allyStats, new GridPosition(2, 0, 1));
                var loadout = Actor.gameObject.AddComponent<UnitAbilityLoadout>();
                loadout.SetAbilities(new[] { Melee, Cone, Fireball });
            }

            public void Dispose()
            {
                for (var i = gameObjects.Count - 1; i >= 0; i -= 1)
                {
                    UnityEngine.Object.DestroyImmediate(gameObjects[i]);
                }

                for (var i = assets.Count - 1; i >= 0; i -= 1)
                {
                    UnityEngine.Object.DestroyImmediate(assets[i]);
                }
            }

            private TacticalUnit CreateUnit(
                string name,
                UnitId unitId,
                TeamId team,
                UnitStatsDefinition stats,
                GridPosition position)
            {
                var gameObject = CreateObject(name);
                gameObject.transform.SetParent(Registry.transform, worldPositionStays: false);
                var unit = gameObject.AddComponent<TacticalUnit>();
                unit.Initialize(unitId, team, stats, position);
                return unit;
            }

            private GameObject CreateObject(string name)
            {
                var gameObject = new GameObject(name);
                gameObjects.Add(gameObject);
                return gameObject;
            }

            private UnitStatsDefinition CreateStats(string displayName)
            {
                var stats = ScriptableObject.CreateInstance<UnitStatsDefinition>();
                assets.Add(stats);
                stats.Configure(displayName, 10, 6, 4f, 1, Color.white);
                return stats;
            }

            private AbilityDefinition CreateAbility(
                string abilityKey,
                string displayName,
                AbilityShape shape,
                int apCost,
                int range,
                int radius,
                int damage)
            {
                var ability = ScriptableObject.CreateInstance<AbilityDefinition>();
                assets.Add(ability);
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
                    triggersReactions: true);
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
