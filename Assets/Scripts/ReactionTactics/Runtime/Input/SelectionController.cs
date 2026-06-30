using System;
using ReactionTactics.Core;
using ReactionTactics.Grid;
using ReactionTactics.Units;
using UnityEngine;

namespace ReactionTactics.Input
{
    /// <summary>
    /// Scene-level owner for player interaction state. It stores selection and targeting data
    /// independently from mouse picking so UI, command routing, and combat systems can query
    /// the same state without depending directly on input events.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class SelectionController : MonoBehaviour
    {
        [SerializeField]
        [Tooltip("Optional picker used only to keep the hovered grid cell in sync with mouse hover events.")]
        private GridPicker gridPicker;

        private TacticalUnit selectedUnit;
        private bool hasHoveredCell;
        private GridPosition hoveredCell;
        private SelectionActionMode selectedActionMode;
        private SelectionTarget selectedTarget;
        private GridPicker subscribedPicker;
        private bool subscribedToSelectedUnitDeath;
        private bool subscribedToTargetUnitDeath;

        public event Action<SelectionState> StateChanged;

        public event Action<SelectionClearReason> SelectionCleared;

        public GridPicker GridPicker
        {
            get { return gridPicker; }
            set
            {
                if (gridPicker == value)
                {
                    return;
                }

                UnsubscribeFromPicker();
                gridPicker = value;

                if (isActiveAndEnabled)
                {
                    SubscribeToPicker();
                }
            }
        }

        public SelectionState CurrentState
        {
            get { return CreateState(); }
        }

        public TacticalUnit SelectedUnit
        {
            get { return selectedUnit != null ? selectedUnit : null; }
        }

        public bool HasSelectedUnit
        {
            get { return selectedUnit != null && selectedUnit.IsAlive; }
        }

        public bool HasHoveredCell
        {
            get { return hasHoveredCell; }
        }

        public GridPosition HoveredCell
        {
            get { return hoveredCell; }
        }

        public SelectionActionMode SelectedActionMode
        {
            get { return selectedActionMode; }
        }

        public SelectionTarget SelectedTarget
        {
            get { return selectedTarget; }
        }

        public TacticalResult SelectUnit(TacticalUnit unit)
        {
            if (unit == null)
            {
                return TacticalResult.Failure("Cannot select a missing tactical unit.");
            }

            if (!unit.IsAlive)
            {
                return TacticalResult.Failure($"Cannot select defeated unit {unit.DisplayName}.");
            }

            if (ReferenceEquals(selectedUnit, unit))
            {
                return TacticalResult.Success();
            }

            UnsubscribeFromSelectedUnitDeath();
            selectedUnit = unit;
            SubscribeToSelectedUnitDeath();
            selectedActionMode = SelectionActionMode.None;
            ClearTargetInternal();
            RaiseStateChanged();
            return TacticalResult.Success();
        }

        public void ClearSelectedUnit()
        {
            if (!HasSelectedUnitReference() && selectedActionMode == SelectionActionMode.None && !selectedTarget.HasTarget)
            {
                return;
            }

            UnsubscribeFromSelectedUnitDeath();
            selectedUnit = null;
            selectedActionMode = SelectionActionMode.None;
            ClearTargetInternal();
            RaiseStateChanged();
        }

        public TacticalResult SetActionMode(SelectionActionMode actionMode)
        {
            if (!Enum.IsDefined(typeof(SelectionActionMode), actionMode))
            {
                return TacticalResult.Failure($"Unknown selection action mode '{actionMode}'.");
            }

            if (selectedActionMode == actionMode && (actionMode != SelectionActionMode.None || !selectedTarget.HasTarget))
            {
                return TacticalResult.Success();
            }

            selectedActionMode = actionMode;
            ClearTargetInternal();
            RaiseStateChanged();
            return TacticalResult.Success();
        }

        public void ClearActionMode()
        {
            if (selectedActionMode == SelectionActionMode.None && !selectedTarget.HasTarget)
            {
                return;
            }

            selectedActionMode = SelectionActionMode.None;
            ClearTargetInternal();
            RaiseStateChanged();
        }

        public void SetHoveredCell(GridPosition cell)
        {
            if (hasHoveredCell && hoveredCell == cell)
            {
                return;
            }

            hasHoveredCell = true;
            hoveredCell = cell;
            RaiseStateChanged();
        }

        public void ClearHoveredCell()
        {
            if (!hasHoveredCell)
            {
                return;
            }

            hasHoveredCell = false;
            hoveredCell = GridPosition.Zero;
            RaiseStateChanged();
        }

        public void SetTargetCell(GridPosition cell)
        {
            if (selectedTarget.Kind == SelectionTargetKind.Cell && selectedTarget.Cell == cell)
            {
                return;
            }

            ClearTargetInternal();
            selectedTarget = SelectionTarget.ForCell(cell);
            RaiseStateChanged();
        }

        public TacticalResult SetTargetUnit(TacticalUnit unit)
        {
            if (unit == null)
            {
                return TacticalResult.Failure("Cannot target a missing tactical unit.");
            }

            if (!unit.IsAlive)
            {
                return TacticalResult.Failure($"Cannot target defeated unit {unit.DisplayName}.");
            }

            if (selectedTarget.Kind == SelectionTargetKind.Unit && ReferenceEquals(selectedTarget.Unit, unit))
            {
                return TacticalResult.Success();
            }

            ClearTargetInternal();
            selectedTarget = SelectionTarget.ForUnit(unit);
            SubscribeToTargetUnitDeath();
            RaiseStateChanged();
            return TacticalResult.Success();
        }

        public void ClearTarget()
        {
            if (!selectedTarget.HasTarget)
            {
                return;
            }

            ClearTargetInternal();
            RaiseStateChanged();
        }

        public void ClearSelection(SelectionClearReason reason = SelectionClearReason.Manual)
        {
            if (!Enum.IsDefined(typeof(SelectionClearReason), reason))
            {
                reason = SelectionClearReason.Manual;
            }

            if (CreateState().IsEmpty && !HasSelectedUnitReference() && !HasTargetUnitReference())
            {
                return;
            }

            UnsubscribeFromSelectedUnitDeath();
            selectedUnit = null;
            hasHoveredCell = false;
            hoveredCell = GridPosition.Zero;
            selectedActionMode = SelectionActionMode.None;
            ClearTargetInternal();
            SelectionCleared?.Invoke(reason);
            RaiseStateChanged();
        }

        public void ClearForPhaseChange()
        {
            ClearSelection(SelectionClearReason.PhaseChanged);
        }

        private void OnEnable()
        {
            SubscribeToPicker();
            SubscribeToSelectedUnitDeath();
            SubscribeToTargetUnitDeath();
        }

        private void OnDisable()
        {
            UnsubscribeFromPicker();
            UnsubscribeFromSelectedUnitDeath();
            UnsubscribeFromTargetUnitDeath();
        }

        private void LateUpdate()
        {
            PruneUnavailableUnitReferences();
        }

        private void Reset()
        {
            gridPicker = FindAnyObjectByType<GridPicker>();
            hoveredCell = GridPosition.Zero;
            selectedActionMode = SelectionActionMode.None;
            selectedTarget = SelectionTarget.None;
        }

        private SelectionState CreateState()
        {
            return new SelectionState(
                SelectedUnit,
                hasHoveredCell,
                hoveredCell,
                selectedActionMode,
                selectedTarget);
        }

        private void HandlePickerHoverCellChanged(GridPickResult result)
        {
            SetHoveredCell(result.Position);
        }

        private void HandlePickerHoverCellCleared()
        {
            ClearHoveredCell();
        }

        private void HandleSelectedUnitDied(TacticalUnit unit, DamageSource source)
        {
            if (ReferenceEquals(unit, selectedUnit))
            {
                ClearSelection(SelectionClearReason.UnitDied);
            }
        }

        private void HandleTargetUnitDied(TacticalUnit unit, DamageSource source)
        {
            if (selectedTarget.Kind != SelectionTargetKind.Unit || !ReferenceEquals(unit, selectedTarget.Unit))
            {
                return;
            }

            ClearTargetInternal();
            RaiseStateChanged();
        }

        private void PruneUnavailableUnitReferences()
        {
            if (HasSelectedUnitReference() && (selectedUnit == null || selectedUnit.IsDead))
            {
                ClearSelection(SelectionClearReason.UnitUnavailable);
                return;
            }

            if (HasTargetUnitReference()
                && (selectedTarget.Unit == null || selectedTarget.Unit.IsDead))
            {
                ClearTargetInternal();
                RaiseStateChanged();
            }
        }

        private void SubscribeToPicker()
        {
            if (gridPicker == null || subscribedPicker == gridPicker)
            {
                return;
            }

            subscribedPicker = gridPicker;
            subscribedPicker.HoverCellChanged += HandlePickerHoverCellChanged;
            subscribedPicker.HoverCellCleared += HandlePickerHoverCellCleared;
        }

        private void UnsubscribeFromPicker()
        {
            if (subscribedPicker == null)
            {
                return;
            }

            subscribedPicker.HoverCellChanged -= HandlePickerHoverCellChanged;
            subscribedPicker.HoverCellCleared -= HandlePickerHoverCellCleared;
            subscribedPicker = null;
        }

        private void SubscribeToSelectedUnitDeath()
        {
            if (subscribedToSelectedUnitDeath || !HasSelectedUnitReference())
            {
                return;
            }

            selectedUnit.Died += HandleSelectedUnitDied;
            subscribedToSelectedUnitDeath = true;
        }

        private void UnsubscribeFromSelectedUnitDeath()
        {
            if (!subscribedToSelectedUnitDeath || !HasSelectedUnitReference())
            {
                subscribedToSelectedUnitDeath = false;
                return;
            }

            selectedUnit.Died -= HandleSelectedUnitDied;
            subscribedToSelectedUnitDeath = false;
        }

        private void SubscribeToTargetUnitDeath()
        {
            if (subscribedToTargetUnitDeath || !HasTargetUnitReference())
            {
                return;
            }

            selectedTarget.Unit.Died += HandleTargetUnitDied;
            subscribedToTargetUnitDeath = true;
        }

        private void UnsubscribeFromTargetUnitDeath()
        {
            if (!subscribedToTargetUnitDeath || !HasTargetUnitReference())
            {
                subscribedToTargetUnitDeath = false;
                return;
            }

            selectedTarget.Unit.Died -= HandleTargetUnitDied;
            subscribedToTargetUnitDeath = false;
        }

        private void ClearTargetInternal()
        {
            UnsubscribeFromTargetUnitDeath();
            selectedTarget = SelectionTarget.None;
        }

        private bool HasSelectedUnitReference()
        {
            return !ReferenceEquals(selectedUnit, null);
        }

        private bool HasTargetUnitReference()
        {
            return selectedTarget.Kind == SelectionTargetKind.Unit && !ReferenceEquals(selectedTarget.Unit, null);
        }

        private void RaiseStateChanged()
        {
            StateChanged?.Invoke(CreateState());
        }
    }
}
