using System;
using System.Collections.Generic;
using ReactionTactics.Grid;
using ReactionTactics.Input;
using ReactionTactics.Turns;
using ReactionTactics.Units;
using UnityEngine;

namespace ReactionTactics.UI
{
    /// <summary>
    /// Runtime bridge between active movement selection, pathfinding preview data, and
    /// transient tile highlights. When the active unit chooses Move, this component
    /// highlights every reachable cell, updates the hovered path, and reports AP cost
    /// or unreachable reasons in a small prototype panel.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class ActiveMovementPreviewController : MonoBehaviour
    {
        private const float MinimumPanelWidth = 240f;
        private const float MinimumPanelHeight = 72f;

        [SerializeField]
        [Tooltip("Selection state that identifies the selected unit, Move mode, and hovered cell.")]
        private SelectionController selectionController;

        [SerializeField]
        [Tooltip("Combat manager whose current active unit and phase make active movement legal.")]
        private CombatManager combatManager;

        [SerializeField]
        [Tooltip("Grid manager that owns the active tactical map used for path previews.")]
        private GridManager gridManager;

        [SerializeField]
        [Tooltip("Unit registry used as the occupancy query for active movement previews.")]
        private UnitRegistry unitRegistry;

        [SerializeField]
        [Tooltip("Tile highlight manager that receives movement range and hovered path cells.")]
        private GridHighlightManager highlightManager;

        [SerializeField]
        [Tooltip("Show the active movement preview feedback panel while Move is selected.")]
        private bool visible = true;

        [SerializeField]
        [Tooltip("Include the unit's current cell in the movement range highlight.")]
        private bool includeStartCellInRange = true;

        [SerializeField]
        [Tooltip("Screen-space rectangle used for the movement preview feedback panel.")]
        private Rect panelRect = new Rect(12f, 560f, 360f, 92f);

        [SerializeField]
        [Tooltip("Header text shown at the top of the active movement preview panel.")]
        private string panelTitle = "Move Preview";

        private readonly List<GridPosition> movementRangeBuffer = new List<GridPosition>();
        private MovementPreview currentPreview;
        private SelectionController subscribedSelectionController;
        private bool appliedHoverPathHighlight;
        private bool appliedTargetCellHighlight;
        private string lastFeedback = string.Empty;
        private GUIStyle feedbackStyle;

        public SelectionController SelectionController
        {
            get { return selectionController; }
            set
            {
                if (selectionController == value)
                {
                    if (isActiveAndEnabled)
                    {
                        SubscribeToSelectionController();
                    }

                    return;
                }

                UnsubscribeFromSelectionController();
                selectionController = value;

                if (isActiveAndEnabled)
                {
                    SubscribeToSelectionController();
                    RefreshPreview();
                }
            }
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

        public bool IncludeStartCellInRange
        {
            get { return includeStartCellInRange; }
            set { includeStartCellInRange = value; }
        }

        public Rect PanelRect
        {
            get { return panelRect; }
            set { panelRect = ClampPanelRect(value); }
        }

        public MovementPreview CurrentPreview
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
            CombatManager combatManager,
            GridManager gridManager,
            UnitRegistry unitRegistry,
            GridHighlightManager highlightManager)
        {
            this.combatManager = combatManager;
            this.gridManager = gridManager;
            this.unitRegistry = unitRegistry;
            this.highlightManager = highlightManager;
            SelectionController = selectionController;

            if (isActiveAndEnabled)
            {
                RefreshPreview();
            }
        }

        /// <summary>
        /// Recomputes the current movement preview and reapplies movement/path highlights.
        /// Returns false when Move preview is inactive or dependencies make it unavailable.
        /// </summary>
        public bool RefreshPreview()
        {
            ResolveMissingReferences();

            if (!TryBuildPreview(out var preview, out var feedback))
            {
                currentPreview = null;
                lastFeedback = feedback;
                ClearMovementHighlights();
                return false;
            }

            currentPreview = preview;
            lastFeedback = feedback;
            ApplyPreviewHighlights(preview);
            return true;
        }

        public static string FormatRangeFeedback(TacticalUnit unit, MovementPreview preview)
        {
            if (unit == null || preview == null)
            {
                return "No active movement preview is available.";
            }

            var reachableDestinationCount = CountReachableDestinations(preview);
            var destinationLabel = reachableDestinationCount == 1 ? "destination" : "destinations";
            return $"{unit.DisplayName} can reach {reachableDestinationCount} {destinationLabel} with {preview.ApBudget} AP. Hover a highlighted cell to preview its path.";
        }

        public static string FormatHoverFeedback(TacticalUnit unit, MovementPreview preview)
        {
            if (unit == null || preview == null || !preview.HasHoveredDestination)
            {
                return "Hover a highlighted cell to preview a movement path.";
            }

            var destination = preview.HoveredDestination.Value;
            if (!preview.HasSelectedPath)
            {
                return preview.SelectedPath.FailureReason;
            }

            var stepCount = Math.Max(0, preview.SelectedPath.Positions.Count - 1);
            var stepLabel = stepCount == 1 ? "step" : "steps";
            return $"Move to {destination}: path costs {preview.SelectedPath.TotalApCost}/{preview.ApBudget} AP over {stepCount} {stepLabel}.";
        }

        private void Awake()
        {
            ResolveMissingReferences();
            panelRect = ClampPanelRect(panelRect);
        }

        private void OnEnable()
        {
            ResolveMissingReferences();
            SubscribeToSelectionController();
            RefreshPreview();
        }

        private void OnDisable()
        {
            UnsubscribeFromSelectionController();
            ClearMovementHighlights();
            currentPreview = null;
            lastFeedback = string.Empty;
        }

        private void Update()
        {
            RefreshPreview();
        }

        private void OnGUI()
        {
            if (!visible || !IsMoveModeSelected())
            {
                return;
            }

            EnsureStyles();
            GUILayout.BeginArea(panelRect, panelTitle, GUI.skin.window);
            GUILayout.Label(string.IsNullOrEmpty(lastFeedback) ? "Move preview is unavailable." : lastFeedback, feedbackStyle);
            GUILayout.EndArea();
        }

        private void OnValidate()
        {
            panelRect = ClampPanelRect(panelRect);
        }

        private bool TryBuildPreview(out MovementPreview preview, out string feedback)
        {
            preview = null;
            feedback = string.Empty;

            var selectionState = selectionController != null ? selectionController.CurrentState : SelectionState.Empty;
            if (selectionState.SelectedActionMode != SelectionActionMode.Move)
            {
                return false;
            }

            if (!selectionState.HasSelectedUnit)
            {
                feedback = "Select the active unit before previewing movement.";
                return false;
            }

            var selectedUnit = selectionState.SelectedUnit;
            var combatState = combatManager != null ? combatManager.CurrentState : null;
            if (combatState == null)
            {
                feedback = "Cannot preview movement because combat has no current state.";
                return false;
            }

            if (combatState.Phase != CombatPhase.ActiveTurn)
            {
                feedback = $"Cannot preview active movement during {combatState.Phase}.";
                return false;
            }

            if (combatState.ActiveUnit == null)
            {
                feedback = "Cannot preview movement because combat has no active unit.";
                return false;
            }

            if (!ReferenceEquals(selectedUnit, combatState.ActiveUnit))
            {
                feedback = $"Select active unit {FormatUnit(combatState.ActiveUnit)} to preview movement.";
                return false;
            }

            if (!TryResolveCurrentMap(out var map, out feedback))
            {
                return false;
            }

            try
            {
                var occupancy = unitRegistry != null ? (IGridOccupancy)unitRegistry : NullGridOccupancy.Instance;
                preview = MovementPreview.Create(
                    map,
                    selectedUnit.CurrentGridPosition,
                    selectedUnit.CurrentAP,
                    occupancy);
            }
            catch (Exception exception) when (exception is ArgumentException || exception is InvalidOperationException)
            {
                feedback = $"Cannot preview movement for {FormatUnit(selectedUnit)}: {exception.Message}";
                preview = null;
                return false;
            }

            if (TryGetPreviewDestination(selectionState, out var previewDestination))
            {
                preview = preview.RecomputeSelectedPath(previewDestination);
                feedback = FormatHoverFeedback(selectedUnit, preview);
                return true;
            }

            feedback = FormatRangeFeedback(selectedUnit, preview);
            return true;
        }

        private bool TryResolveCurrentMap(out IGridMap map, out string failureReason)
        {
            map = null;
            failureReason = string.Empty;

            if (gridManager == null)
            {
                failureReason = "Cannot preview movement because no GridManager is assigned.";
                return false;
            }

            if (!gridManager.HasCurrentMap && !gridManager.RebuildMap())
            {
                failureReason = "Cannot preview movement because the GridManager could not build a current map.";
                return false;
            }

            if (gridManager.CurrentMap == null)
            {
                failureReason = "Cannot preview movement because the GridManager has no current map.";
                return false;
            }

            map = gridManager.CurrentMap;
            return true;
        }

        private void ApplyPreviewHighlights(MovementPreview preview)
        {
            if (highlightManager == null)
            {
                return;
            }

            movementRangeBuffer.Clear();
            foreach (var position in preview.ReachableCells.Keys)
            {
                if (!includeStartCellInRange && position == preview.StartPosition)
                {
                    continue;
                }

                movementRangeBuffer.Add(position);
            }

            highlightManager.SetHighlightedCells(GridHighlightCategory.MovementRange, movementRangeBuffer);

            if (preview.HasSelectedPath)
            {
                highlightManager.SetHoverPath(preview.SelectedPath.Positions);
                appliedHoverPathHighlight = true;
                highlightManager.HighlightCell(GridHighlightCategory.TargetCell, preview.SelectedPath.Destination);
                appliedTargetCellHighlight = true;
            }
            else
            {
                ClearOwnedHoverPathHighlight();
                ClearOwnedTargetCellHighlight();
            }
        }

        private void ClearMovementHighlights()
        {
            if (highlightManager == null)
            {
                return;
            }

            highlightManager.ClearCategory(GridHighlightCategory.MovementRange);
            ClearOwnedHoverPathHighlight();
            ClearOwnedTargetCellHighlight();
        }

        private void ClearOwnedHoverPathHighlight()
        {
            if (!appliedHoverPathHighlight || highlightManager == null)
            {
                return;
            }

            highlightManager.ClearHoverPath();
            appliedHoverPathHighlight = false;
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

        private static bool TryGetPreviewDestination(SelectionState selectionState, out GridPosition destination)
        {
            if (selectionState.SelectedTarget.HasCell)
            {
                destination = selectionState.SelectedTarget.CurrentCell;
                return true;
            }

            if (selectionState.HasHoveredCell)
            {
                destination = selectionState.HoveredCell;
                return true;
            }

            destination = GridPosition.Zero;
            return false;
        }

        private bool IsMoveModeSelected()
        {
            ResolveMissingReferences();
            return selectionController != null
                && selectionController.SelectedActionMode == SelectionActionMode.Move;
        }

        private void ResolveMissingReferences()
        {
            if (selectionController == null)
            {
                SelectionController = FindAnyObjectByType<SelectionController>();
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

        private void SubscribeToSelectionController()
        {
            if (selectionController == null || subscribedSelectionController == selectionController)
            {
                return;
            }

            UnsubscribeFromSelectionController();
            subscribedSelectionController = selectionController;
            subscribedSelectionController.StateChanged += HandleSelectionStateChanged;
        }

        private void UnsubscribeFromSelectionController()
        {
            if (subscribedSelectionController == null)
            {
                return;
            }

            subscribedSelectionController.StateChanged -= HandleSelectionStateChanged;
            subscribedSelectionController = null;
        }

        private void HandleSelectionStateChanged(SelectionState state)
        {
            RefreshPreview();
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

        private static int CountReachableDestinations(MovementPreview preview)
        {
            var count = 0;
            foreach (var position in preview.ReachableCells.Keys)
            {
                if (position != preview.StartPosition)
                {
                    count += 1;
                }
            }

            return count;
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
