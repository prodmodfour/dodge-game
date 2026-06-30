using System;
using System.Collections.Generic;
using ReactionTactics.Actions;
using ReactionTactics.Grid;
using ReactionTactics.Input;
using ReactionTactics.Turns;
using ReactionTactics.Units;
using UnityEngine;

namespace ReactionTactics.UI
{
    /// <summary>
    /// Bridges selected attack modes, side-effect-free intent previews, and transient
    /// danger highlights. While the active unit is targeting melee, cone, or radius
    /// attacks, this component previews threatened cells from the current hover target
    /// without declaring the action or spending AP.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class ActionDangerPreviewController : MonoBehaviour
    {
        private const float MinimumPanelWidth = 260f;
        private const float MinimumPanelHeight = 72f;

        [SerializeField]
        [Tooltip("Selection state that identifies the active unit, selected action mode, and hovered cell.")]
        private SelectionController selectionController;

        [SerializeField]
        [Tooltip("Optional picker used to preview melee targets while the pointer is over a unit collider.")]
        private GridPicker gridPicker;

        [SerializeField]
        [Tooltip("Combat manager whose active unit and phase make action previews legal.")]
        private CombatManager combatManager;

        [SerializeField]
        [Tooltip("Grid manager that owns the current map used for shape previews.")]
        private GridManager gridManager;

        [SerializeField]
        [Tooltip("Unit registry used to find hovered melee targets and currently threatened units.")]
        private UnitRegistry unitRegistry;

        [SerializeField]
        [Tooltip("Tile highlight manager that receives action danger cells.")]
        private GridHighlightManager highlightManager;

        [SerializeField]
        [Tooltip("Show the action danger preview feedback panel while an attack mode is selected.")]
        private bool visible = true;

        [SerializeField]
        [Tooltip("Screen-space rectangle used for the action danger preview feedback panel.")]
        private Rect panelRect = new Rect(384f, 560f, 380f, 96f);

        [SerializeField]
        [Tooltip("Header text shown at the top of the action danger preview panel.")]
        private string panelTitle = "Danger Preview";

        private readonly IntentPreviewService previewService = new IntentPreviewService();
        private readonly List<GridPosition> dangerCellsBuffer = new List<GridPosition>();
        private ActionIntentPreview currentPreview;
        private bool appliedTargetCellHighlight;
        private string lastFeedback = string.Empty;
        private GUIStyle feedbackStyle;

        public SelectionController SelectionController
        {
            get { return selectionController; }
            set { selectionController = value; }
        }

        public GridPicker GridPicker
        {
            get { return gridPicker; }
            set { gridPicker = value; }
        }

        public CombatManager CombatManager
        {
            get { return combatManager; }
            set { combatManager = value; }
        }

        public GridManager GridManager
        {
            get { return gridManager; }
            set { gridManager = value; }
        }

        public UnitRegistry UnitRegistry
        {
            get { return unitRegistry; }
            set { unitRegistry = value; }
        }

        public GridHighlightManager HighlightManager
        {
            get { return highlightManager; }
            set { highlightManager = value; }
        }

        public bool Visible
        {
            get { return visible; }
            set { visible = value; }
        }

        public Rect PanelRect
        {
            get { return panelRect; }
            set { panelRect = ClampPanelRect(value); }
        }

        public ActionIntentPreview CurrentPreview
        {
            get { return currentPreview; }
        }

        public bool HasActivePreview
        {
            get { return currentPreview != null; }
        }

        public string LastFeedback
        {
            get { return lastFeedback; }
        }

        public void Configure(
            SelectionController selectionController,
            GridPicker gridPicker,
            CombatManager combatManager,
            GridManager gridManager,
            UnitRegistry unitRegistry,
            GridHighlightManager highlightManager)
        {
            this.selectionController = selectionController;
            this.gridPicker = gridPicker;
            this.combatManager = combatManager;
            this.gridManager = gridManager;
            this.unitRegistry = unitRegistry;
            this.highlightManager = highlightManager;
        }

        /// <summary>
        /// Recomputes the current attack preview and reapplies danger highlights.
        /// Returns false when no attack mode is selected or no preview target is hovered.
        /// </summary>
        public bool RefreshPreview()
        {
            ResolveMissingReferences();

            if (!TryBuildPreview(out var preview, out var feedback))
            {
                currentPreview = null;
                lastFeedback = feedback;
                ClearActionHighlights();
                return false;
            }

            currentPreview = preview;
            lastFeedback = feedback;
            ApplyPreviewHighlights(preview);
            return true;
        }

        public static string FormatPreviewFeedback(ActionIntentPreview preview)
        {
            if (preview == null)
            {
                return "Hover a valid target to preview danger cells.";
            }

            var abilityName = preview.Ability != null ? preview.Ability.DisplayName : "Action";
            if (preview.IsInvalid)
            {
                return preview.InvalidReason;
            }

            var cellLabel = preview.AffectedCells.Count == 1 ? "cell" : "cells";
            var threatLabel = preview.ThreatenedUnits.Count == 1 ? "unit" : "units";
            var threatenedText = FormatThreatenedUnits(preview.ThreatenedUnits);
            return $"{abilityName} threatens {preview.AffectedCells.Count} {cellLabel} and {preview.ThreatenedUnits.Count} {threatLabel}. {threatenedText}";
        }

        private void Awake()
        {
            ResolveMissingReferences();
            panelRect = ClampPanelRect(panelRect);
        }

        private void OnEnable()
        {
            ResolveMissingReferences();
            RefreshPreview();
        }

        private void OnDisable()
        {
            ClearActionHighlights();
            currentPreview = null;
            lastFeedback = string.Empty;
        }

        private void Update()
        {
            RefreshPreview();
        }

        private void OnGUI()
        {
            if (!visible || !IsAttackModeSelected())
            {
                return;
            }

            EnsureStyles();
            GUILayout.BeginArea(panelRect, panelTitle, GUI.skin.window);
            GUILayout.Label(string.IsNullOrEmpty(lastFeedback) ? "Hover a valid target to preview danger cells." : lastFeedback, feedbackStyle);
            GUILayout.EndArea();
        }

        private void OnValidate()
        {
            panelRect = ClampPanelRect(panelRect);
        }

        private bool TryBuildPreview(out ActionIntentPreview preview, out string feedback)
        {
            preview = null;
            feedback = string.Empty;

            var selectionState = selectionController != null ? selectionController.CurrentState : SelectionState.Empty;
            if (!IsAttackMode(selectionState.SelectedActionMode))
            {
                return false;
            }

            if (!selectionState.HasSelectedUnit)
            {
                feedback = "Select the active unit before previewing an attack.";
                return false;
            }

            var actor = selectionState.SelectedUnit;
            var combatState = combatManager != null ? combatManager.CurrentState : null;
            if (combatState == null)
            {
                feedback = "Cannot preview attacks because combat has no current state.";
                return false;
            }

            if (!combatState.IsActiveUnitPhase)
            {
                feedback = $"Cannot preview active attacks during {combatState.Phase}.";
                return false;
            }

            if (combatState.ActiveUnit == null)
            {
                feedback = "Cannot preview attacks because combat has no active unit.";
                return false;
            }

            if (!ReferenceEquals(actor, combatState.ActiveUnit))
            {
                feedback = $"Select active unit {FormatUnit(combatState.ActiveUnit)} to preview attacks.";
                return false;
            }

            if (!TryResolveCurrentMap(out var map, out feedback))
            {
                return false;
            }

            if (!TryResolveAbility(actor, selectionState.SelectedActionMode, out var ability, out feedback))
            {
                return false;
            }

            if (!TryCreateTarget(actor, ability, selectionState, out var target, out feedback))
            {
                return false;
            }

            preview = previewService.CreatePreview(
                actor,
                ability,
                target,
                combatState,
                map,
                GetPotentialUnits());
            feedback = FormatPreviewFeedback(preview);
            return true;
        }

        private bool TryResolveCurrentMap(out IGridMap map, out string failureReason)
        {
            map = null;
            failureReason = string.Empty;

            if (gridManager == null)
            {
                failureReason = "Cannot preview attacks because no GridManager is assigned.";
                return false;
            }

            if (!gridManager.HasCurrentMap && !gridManager.RebuildMap())
            {
                failureReason = "Cannot preview attacks because the GridManager could not build a current map.";
                return false;
            }

            if (gridManager.CurrentMap == null)
            {
                failureReason = "Cannot preview attacks because the GridManager has no current map.";
                return false;
            }

            map = gridManager.CurrentMap;
            return true;
        }

        private static bool TryResolveAbility(
            TacticalUnit actor,
            SelectionActionMode actionMode,
            out AbilityDefinition ability,
            out string failureReason)
        {
            ability = null;
            failureReason = string.Empty;

            if (!TryGetAbilityShape(actionMode, out var shape))
            {
                failureReason = $"Selection mode {actionMode} does not preview an attack shape.";
                return false;
            }

            var loadout = actor != null ? actor.GetComponent<UnitAbilityLoadout>() : null;
            if (loadout == null)
            {
                failureReason = $"{FormatUnit(actor)} has no ability loadout for {actionMode}.";
                return false;
            }

            var actionAbilities = loadout.GetActionAbilities();
            for (var i = 0; i < actionAbilities.Count; i += 1)
            {
                var candidate = actionAbilities[i];
                if (candidate != null && candidate.Shape == shape)
                {
                    ability = candidate;
                    return true;
                }
            }

            failureReason = $"{FormatUnit(actor)} has no active {actionMode} ability assigned.";
            return false;
        }

        private bool TryCreateTarget(
            TacticalUnit actor,
            AbilityDefinition ability,
            SelectionState selectionState,
            out ActionTarget target,
            out string failureReason)
        {
            target = ActionTarget.None;
            failureReason = string.Empty;

            switch (ability.Shape)
            {
                case AbilityShape.Melee:
                    if (!TryGetSelectedOrHoveredUnit(selectionState, out var hoveredUnit))
                    {
                        failureReason = $"Hover or select a hostile unit in melee range to preview {ability.DisplayName}.";
                        return false;
                    }

                    target = ActionTarget.ForUnit(hoveredUnit);
                    return true;
                case AbilityShape.Cone:
                    if (!TryGetSelectedOrHoveredCell(selectionState, out var coneCell))
                    {
                        failureReason = $"Hover or select a grid cell to aim {ability.DisplayName}.";
                        return false;
                    }

                    target = ActionTarget.ForCell(coneCell);
                    if (coneCell != actor.CurrentGridPosition)
                    {
                        target = target.WithDirection(CardinalDirectionMath.FromTo(actor.CurrentGridPosition, coneCell));
                    }

                    return true;
                case AbilityShape.Radius:
                    if (!TryGetSelectedOrHoveredCell(selectionState, out var radiusCell))
                    {
                        failureReason = $"Hover or select a grid cell to preview {ability.DisplayName}.";
                        return false;
                    }

                    target = ActionTarget.ForCell(radiusCell);
                    return true;
                default:
                    failureReason = $"{ability.DisplayName} does not use an action danger preview shape.";
                    return false;
            }
        }

        private bool TryGetSelectedOrHoveredUnit(SelectionState selectionState, out TacticalUnit unit)
        {
            unit = null;
            if (selectionState.SelectedTarget.HasUnit)
            {
                unit = selectionState.SelectedTarget.Unit;
                return unit != null;
            }

            if (gridPicker != null && gridPicker.HasCurrentHoverUnit && gridPicker.CurrentHoverUnit != null)
            {
                unit = gridPicker.CurrentHoverUnit;
                return true;
            }

            if (TryGetSelectedOrHoveredCell(selectionState, out var cell)
                && unitRegistry != null
                && unitRegistry.TryGetUnitAt(cell, out var occupant))
            {
                unit = occupant;
                return true;
            }

            return false;
        }

        private bool TryGetSelectedOrHoveredCell(SelectionState selectionState, out GridPosition cell)
        {
            if (selectionState.SelectedTarget.HasCell)
            {
                cell = selectionState.SelectedTarget.CurrentCell;
                return true;
            }

            return TryGetHoveredCell(selectionState, out cell);
        }

        private bool TryGetHoveredCell(SelectionState selectionState, out GridPosition cell)
        {
            if (gridPicker != null && gridPicker.HasCurrentHoverUnit && gridPicker.CurrentHoverUnit != null)
            {
                cell = gridPicker.CurrentHoverUnit.CurrentGridPosition;
                return true;
            }

            if (gridPicker != null && gridPicker.HasCurrentHoverCell)
            {
                cell = gridPicker.CurrentHoverCell;
                return true;
            }

            if (selectionState.HasHoveredCell)
            {
                cell = selectionState.HoveredCell;
                return true;
            }

            cell = GridPosition.Zero;
            return false;
        }

        private IReadOnlyList<TacticalUnit> GetPotentialUnits()
        {
            if (unitRegistry != null)
            {
                return unitRegistry.GetLivingUnits();
            }

            var sceneUnits = FindObjectsByType<TacticalUnit>(FindObjectsInactive.Exclude);
            return TurnOrderService.BuildTurnOrder(sceneUnits);
        }

        private void ApplyPreviewHighlights(ActionIntentPreview preview)
        {
            if (highlightManager == null)
            {
                return;
            }

            if (preview == null || preview.IsInvalid)
            {
                ClearActionHighlights();
                return;
            }

            dangerCellsBuffer.Clear();
            AddDistinct(dangerCellsBuffer, preview.AffectedCells);
            for (var i = 0; i < preview.ThreatenedUnits.Count; i += 1)
            {
                var unit = preview.ThreatenedUnits[i];
                if (unit != null && unit.IsAlive)
                {
                    AddDistinct(dangerCellsBuffer, unit.CurrentGridPosition);
                }
            }

            highlightManager.SetHighlightedCells(GridHighlightCategory.ActionDanger, dangerCellsBuffer);
            if (preview.Target.HasTargetCell)
            {
                highlightManager.HighlightCell(GridHighlightCategory.TargetCell, preview.Target.TargetCell);
                appliedTargetCellHighlight = true;
            }
            else
            {
                ClearOwnedTargetCellHighlight();
            }
        }

        private void ClearActionHighlights()
        {
            if (highlightManager == null)
            {
                return;
            }

            highlightManager.ClearCategory(GridHighlightCategory.ActionDanger);
            ClearOwnedTargetCellHighlight();
        }

        private void ClearOwnedTargetCellHighlight()
        {
            if (!appliedTargetCellHighlight || highlightManager == null)
            {
                return;
            }

            highlightManager.ClearCategory(GridHighlightCategory.TargetCell);
            appliedTargetCellHighlight = false;
        }

        private bool IsAttackModeSelected()
        {
            ResolveMissingReferences();
            return selectionController != null
                && IsAttackMode(selectionController.SelectedActionMode);
        }

        private void ResolveMissingReferences()
        {
            if (selectionController == null)
            {
                selectionController = FindAnyObjectByType<SelectionController>();
            }

            if (gridPicker == null)
            {
                gridPicker = selectionController != null && selectionController.GridPicker != null
                    ? selectionController.GridPicker
                    : FindAnyObjectByType<GridPicker>();
            }

            if (combatManager == null)
            {
                combatManager = FindAnyObjectByType<CombatManager>();
            }

            if (gridManager == null)
            {
                gridManager = FindAnyObjectByType<GridManager>();
            }

            if (unitRegistry == null)
            {
                unitRegistry = FindAnyObjectByType<UnitRegistry>();
            }

            if (highlightManager == null)
            {
                highlightManager = FindAnyObjectByType<GridHighlightManager>();
            }
        }

        private void EnsureStyles()
        {
            if (feedbackStyle == null)
            {
                feedbackStyle = new GUIStyle(GUI.skin.label)
                {
                    wordWrap = true,
                    fontSize = 12,
                    normal = { textColor = Color.white }
                };
            }
        }

        private static void AddDistinct(ICollection<GridPosition> destination, IEnumerable<GridPosition> positions)
        {
            if (positions == null)
            {
                return;
            }

            foreach (var position in positions)
            {
                AddDistinct(destination, position);
            }
        }

        private static void AddDistinct(ICollection<GridPosition> destination, GridPosition position)
        {
            if (!destination.Contains(position))
            {
                destination.Add(position);
            }
        }

        private static bool IsAttackMode(SelectionActionMode actionMode)
        {
            return actionMode == SelectionActionMode.Melee
                || actionMode == SelectionActionMode.Cone
                || actionMode == SelectionActionMode.AreaOfEffect;
        }

        private static bool TryGetAbilityShape(SelectionActionMode actionMode, out AbilityShape shape)
        {
            switch (actionMode)
            {
                case SelectionActionMode.Melee:
                    shape = AbilityShape.Melee;
                    return true;
                case SelectionActionMode.Cone:
                    shape = AbilityShape.Cone;
                    return true;
                case SelectionActionMode.AreaOfEffect:
                    shape = AbilityShape.Radius;
                    return true;
                default:
                    shape = default;
                    return false;
            }
        }

        private static string FormatThreatenedUnits(IReadOnlyList<TacticalUnit> threatenedUnits)
        {
            if (threatenedUnits == null || threatenedUnits.Count == 0)
            {
                return "No units are currently threatened.";
            }

            var names = new List<string>();
            for (var i = 0; i < threatenedUnits.Count; i += 1)
            {
                names.Add(FormatUnit(threatenedUnits[i]));
            }

            return "Threatened: " + string.Join(", ", names) + ".";
        }

        private static string FormatUnit(TacticalUnit unit)
        {
            if (unit == null)
            {
                return "None";
            }

            var identity = unit.UnitId.IsAssigned ? $" {unit.UnitId}" : string.Empty;
            return $"{unit.DisplayName}{identity}";
        }

        private static Rect ClampPanelRect(Rect rect)
        {
            rect.width = Mathf.Max(MinimumPanelWidth, rect.width);
            rect.height = Mathf.Max(MinimumPanelHeight, rect.height);
            return rect;
        }
    }
}
