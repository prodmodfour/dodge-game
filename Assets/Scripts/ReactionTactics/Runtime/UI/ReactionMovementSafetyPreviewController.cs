using System;
using System.Collections.Generic;
using ReactionTactics.Actions;
using ReactionTactics.Grid;
using ReactionTactics.Input;
using ReactionTactics.Reactions;
using ReactionTactics.Turns;
using ReactionTactics.Units;
using UnityEngine;

namespace ReactionTactics.UI
{
    /// <summary>
    /// Runtime bridge between reaction movement selection, reachable-cell search,
    /// <see cref="ReactionSafetyAnalyzer" />, and transient safe/threatened tile highlights.
    /// While the current reactor chooses Reaction Move, this component shows which destinations
    /// physically avoid the pending action and which remain in danger.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class ReactionMovementSafetyPreviewController : MonoBehaviour
    {
        private const float MinimumPanelWidth = 280f;
        private const float MinimumPanelHeight = 88f;

        [SerializeField]
        [Tooltip("Selection state that identifies Reaction Move mode and the hovered destination cell.")]
        private SelectionController selectionController;

        [SerializeField]
        [Tooltip("Combat manager whose reaction phase, pending action, and current reactor drive safety previews.")]
        private CombatManager combatManager;

        [SerializeField]
        [Tooltip("Grid manager that owns the active tactical map used for reaction movement previews.")]
        private GridManager gridManager;

        [SerializeField]
        [Tooltip("Unit registry used as the occupancy query for reaction movement previews.")]
        private UnitRegistry unitRegistry;

        [SerializeField]
        [Tooltip("Tile highlight manager that receives safe and threatened reaction destination cells.")]
        private GridHighlightManager highlightManager;

        [SerializeField]
        [Tooltip("Show the reaction movement safety feedback panel while Reaction Move is selected.")]
        private bool visible = true;

        [SerializeField]
        [Tooltip("Include the reactor's current cell in the safe/threatened highlights so staying put is explained.")]
        private bool includeStartCellInHighlights = true;

        [SerializeField]
        [Tooltip("Screen-space rectangle used for the reaction safety feedback panel.")]
        private Rect panelRect = new Rect(744f, 330f, 390f, 132f);

        [SerializeField]
        [Tooltip("Header text shown at the top of the reaction safety feedback panel.")]
        private string panelTitle = "Reaction Move Safety";

        private readonly ReactionSafetyAnalyzer safetyAnalyzer = new ReactionSafetyAnalyzer();
        private readonly List<ReactionSafetyCell> currentSafetyCells = new List<ReactionSafetyCell>();
        private readonly Dictionary<GridPosition, ReactionSafetyCell> safetyCellsByPosition = new Dictionary<GridPosition, ReactionSafetyCell>();
        private readonly List<GridPosition> safeCellsBuffer = new List<GridPosition>();
        private readonly List<GridPosition> threatenedCellsBuffer = new List<GridPosition>();

        private MovementPreview currentPreview;
        private string lastFeedback = string.Empty;
        private GUIStyle feedbackStyle;

        public SelectionController SelectionController
        {
            get { return selectionController; }
            set { selectionController = value; }
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

        public bool IncludeStartCellInHighlights
        {
            get { return includeStartCellInHighlights; }
            set { includeStartCellInHighlights = value; }
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

        public IReadOnlyList<ReactionSafetyCell> CurrentSafetyCells
        {
            get { return currentSafetyCells; }
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
            this.selectionController = selectionController;
            this.combatManager = combatManager;
            this.gridManager = gridManager;
            this.unitRegistry = unitRegistry;
            this.highlightManager = highlightManager;
        }

        /// <summary>
        /// Recomputes reachable reaction destinations, classifies each one against the pending action,
        /// and reapplies reaction-safe and reaction-threatened highlights.
        /// </summary>
        public bool RefreshPreview()
        {
            ResolveMissingReferences();

            if (!TryBuildPreview(out var preview, out var safetyCells, out var feedback))
            {
                currentPreview = null;
                ReplaceSafetyCells(null);
                lastFeedback = feedback;
                ClearSafetyHighlights();
                return false;
            }

            currentPreview = preview;
            ReplaceSafetyCells(safetyCells);
            lastFeedback = feedback;
            ApplySafetyHighlights(preview);
            return true;
        }

        public bool TryGetSafetyCell(GridPosition position, out ReactionSafetyCell safetyCell)
        {
            return safetyCellsByPosition.TryGetValue(position, out safetyCell);
        }

        public static string FormatRangeFeedback(
            TacticalUnit reactor,
            ActionIntent pendingIntent,
            MovementPreview preview,
            IReadOnlyList<ReactionSafetyCell> safetyCells)
        {
            if (reactor == null || pendingIntent == null || preview == null || safetyCells == null)
            {
                return "No reaction movement safety preview is available.";
            }

            var safeCount = 0;
            var threatenedCount = 0;
            for (var i = 0; i < safetyCells.Count; i += 1)
            {
                if (safetyCells[i].IsSafe)
                {
                    safeCount += 1;
                }
                else
                {
                    threatenedCount += 1;
                }
            }

            var destinationCount = CountReachableDestinations(preview);
            var destinationLabel = destinationCount == 1 ? "destination" : "destinations";
            var pendingName = pendingIntent.Ability != null ? pendingIntent.Ability.DisplayName : "pending action";
            return $"{FormatUnit(reactor)} can reaction move to {destinationCount} {destinationLabel} with {preview.ApBudget} AP while '{pendingName}' is pending. "
                + $"Safe cells: {safeCount}. Still threatened cells: {threatenedCount}. Hover a highlighted cell to see why.";
        }

        public static string FormatHoverFeedback(
            TacticalUnit reactor,
            ActionIntent pendingIntent,
            MovementPreview preview,
            ReactionSafetyCell safetyCell)
        {
            if (reactor == null || pendingIntent == null || preview == null)
            {
                return "No reaction movement safety preview is available.";
            }

            if (!preview.HasHoveredDestination)
            {
                return FormatRangeFeedback(reactor, pendingIntent, preview, new[] { safetyCell });
            }

            var destination = preview.HoveredDestination.Value;
            if (!preview.HasSelectedPath)
            {
                return $"{destination} is not a reachable reaction destination: {preview.SelectedPath.FailureReason}";
            }

            var stepCount = Math.Max(0, preview.SelectedPath.Positions.Count - 1);
            var stepLabel = stepCount == 1 ? "step" : "steps";
            return $"Reaction move to {destination}: path costs {preview.SelectedPath.TotalApCost}/{preview.ApBudget} AP over {stepCount} {stepLabel}. "
                + FormatSafetyTooltip(safetyCell);
        }

        public static string FormatSafetyTooltip(ReactionSafetyCell safetyCell)
        {
            return $"Reaction Safety: {safetyCell.Status} ({safetyCell.TotalApCost} AP). {safetyCell.Reason}";
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
            currentPreview = null;
            ReplaceSafetyCells(null);
            lastFeedback = string.Empty;
            ClearSafetyHighlights();
        }

        private void Update()
        {
            RefreshPreview();
        }

        private void OnGUI()
        {
            if (!visible || !IsReactionMoveModeSelected())
            {
                return;
            }

            EnsureStyles();
            GUILayout.BeginArea(panelRect, panelTitle, GUI.skin.window);
            GUILayout.Label(string.IsNullOrEmpty(lastFeedback) ? "Select Reaction Move to preview safe destinations." : lastFeedback, feedbackStyle);
            GUILayout.EndArea();
        }

        private void OnValidate()
        {
            panelRect = ClampPanelRect(panelRect);
        }

        private bool TryBuildPreview(
            out MovementPreview preview,
            out List<ReactionSafetyCell> safetyCells,
            out string feedback)
        {
            preview = null;
            safetyCells = null;
            feedback = string.Empty;

            var selectionState = selectionController != null ? selectionController.CurrentState : SelectionState.Empty;
            if (selectionState.SelectedActionMode != SelectionActionMode.Move)
            {
                return false;
            }

            var combatState = combatManager != null ? combatManager.CurrentState : null;
            if (combatState == null)
            {
                feedback = "Cannot preview reaction movement because combat has no current state.";
                return false;
            }

            if (!combatState.IsReactionPhase)
            {
                feedback = $"Cannot preview reaction movement during {combatState.Phase}.";
                return false;
            }

            var pendingIntent = combatState.PendingActionIntent as ActionIntent;
            if (pendingIntent == null)
            {
                feedback = "Cannot preview reaction movement because no pending action intent is available.";
                return false;
            }

            var reactor = combatState.CurrentReactor;
            if (reactor == null)
            {
                feedback = "Cannot preview reaction movement because no current reactor is waiting for input.";
                return false;
            }

            if (!selectionState.HasSelectedUnit)
            {
                feedback = $"Select current reactor {FormatUnit(reactor)} before previewing reaction movement.";
                return false;
            }

            if (!ReferenceEquals(selectionState.SelectedUnit, reactor))
            {
                feedback = $"Only current reactor {FormatUnit(reactor)} can preview reaction movement now.";
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
                    reactor.CurrentGridPosition,
                    reactor.CurrentAP,
                    occupancy);
            }
            catch (Exception exception) when (exception is ArgumentException || exception is InvalidOperationException)
            {
                feedback = $"Cannot preview reaction movement for {FormatUnit(reactor)}: {exception.Message}";
                preview = null;
                return false;
            }

            safetyCells = new List<ReactionSafetyCell>(safetyAnalyzer.ClassifyReachableCells(
                reactor,
                pendingIntent,
                preview.ReachableCells.Values));

            if (selectionState.HasHoveredCell)
            {
                preview = preview.RecomputeSelectedPath(selectionState.HoveredCell);
                if (TryFindSafetyCell(safetyCells, selectionState.HoveredCell, out var safetyCell))
                {
                    feedback = FormatHoverFeedback(reactor, pendingIntent, preview, safetyCell);
                    return true;
                }

                feedback = preview.SelectedPath.FailureReason;
                return true;
            }

            feedback = FormatRangeFeedback(reactor, pendingIntent, preview, safetyCells);
            return true;
        }

        private bool TryResolveCurrentMap(out IGridMap map, out string failureReason)
        {
            map = null;
            failureReason = string.Empty;

            if (gridManager == null)
            {
                failureReason = "Cannot preview reaction movement because no GridManager is assigned.";
                return false;
            }

            if (!gridManager.HasCurrentMap && !gridManager.RebuildMap())
            {
                failureReason = "Cannot preview reaction movement because the GridManager could not build a current map.";
                return false;
            }

            if (gridManager.CurrentMap == null)
            {
                failureReason = "Cannot preview reaction movement because the GridManager has no current map.";
                return false;
            }

            map = gridManager.CurrentMap;
            return true;
        }

        private void ApplySafetyHighlights(MovementPreview preview)
        {
            if (highlightManager == null)
            {
                return;
            }

            safeCellsBuffer.Clear();
            threatenedCellsBuffer.Clear();

            for (var i = 0; i < currentSafetyCells.Count; i += 1)
            {
                var safetyCell = currentSafetyCells[i];
                if (!includeStartCellInHighlights && preview != null && safetyCell.Position == preview.StartPosition)
                {
                    continue;
                }

                if (safetyCell.IsSafe)
                {
                    AddDistinct(safeCellsBuffer, safetyCell.Position);
                }
                else
                {
                    AddDistinct(threatenedCellsBuffer, safetyCell.Position);
                }
            }

            highlightManager.SetHighlightedCells(GridHighlightCategory.ReactionSafe, safeCellsBuffer);
            highlightManager.SetHighlightedCells(GridHighlightCategory.ReactionThreatened, threatenedCellsBuffer);
        }

        private void ClearSafetyHighlights()
        {
            if (highlightManager == null)
            {
                return;
            }

            highlightManager.ClearCategory(GridHighlightCategory.ReactionSafe);
            highlightManager.ClearCategory(GridHighlightCategory.ReactionThreatened);
        }

        private void ReplaceSafetyCells(IEnumerable<ReactionSafetyCell> safetyCells)
        {
            currentSafetyCells.Clear();
            safetyCellsByPosition.Clear();

            if (safetyCells == null)
            {
                return;
            }

            foreach (var safetyCell in safetyCells)
            {
                currentSafetyCells.Add(safetyCell);
                safetyCellsByPosition[safetyCell.Position] = safetyCell;
            }
        }

        private bool IsReactionMoveModeSelected()
        {
            ResolveMissingReferences();
            return selectionController != null
                && selectionController.SelectedActionMode == SelectionActionMode.Move
                && combatManager != null
                && combatManager.CurrentState != null
                && combatManager.CurrentState.IsReactionPhase;
        }

        private void ResolveMissingReferences()
        {
            if (selectionController == null)
            {
                selectionController = FindAnyObjectByType<SelectionController>();
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

        private static bool TryFindSafetyCell(
            IEnumerable<ReactionSafetyCell> safetyCells,
            GridPosition position,
            out ReactionSafetyCell safetyCell)
        {
            if (safetyCells != null)
            {
                foreach (var candidate in safetyCells)
                {
                    if (candidate.Position == position)
                    {
                        safetyCell = candidate;
                        return true;
                    }
                }
            }

            safetyCell = default;
            return false;
        }

        private static void AddDistinct(ICollection<GridPosition> destination, GridPosition position)
        {
            if (!destination.Contains(position))
            {
                destination.Add(position);
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
