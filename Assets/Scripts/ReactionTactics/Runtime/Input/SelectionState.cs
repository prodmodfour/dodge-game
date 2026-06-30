using ReactionTactics.Grid;
using ReactionTactics.Units;

namespace ReactionTactics.Input
{
    /// <summary>
    /// Immutable snapshot of the current tactical interaction state.
    /// </summary>
    public readonly struct SelectionState
    {
        public SelectionState(
            TacticalUnit selectedUnit,
            bool hasHoveredCell,
            GridPosition hoveredCell,
            SelectionActionMode selectedActionMode,
            SelectionTarget selectedTarget)
        {
            SelectedUnit = selectedUnit;
            HasHoveredCell = hasHoveredCell;
            HoveredCell = hoveredCell;
            SelectedActionMode = selectedActionMode;
            SelectedTarget = selectedTarget;
        }

        public TacticalUnit SelectedUnit { get; }

        public bool HasSelectedUnit
        {
            get { return SelectedUnit != null && SelectedUnit.IsAlive; }
        }

        public bool HasHoveredCell { get; }

        public GridPosition HoveredCell { get; }

        public SelectionActionMode SelectedActionMode { get; }

        public bool HasSelectedActionMode
        {
            get { return SelectedActionMode != SelectionActionMode.None; }
        }

        public SelectionTarget SelectedTarget { get; }

        public bool HasSelectedTarget
        {
            get { return SelectedTarget.HasTarget; }
        }

        public bool IsEmpty
        {
            get
            {
                return !HasSelectedUnit
                    && !HasHoveredCell
                    && !HasSelectedActionMode
                    && !HasSelectedTarget;
            }
        }

        public static SelectionState Empty
        {
            get
            {
                return new SelectionState(
                    null,
                    false,
                    GridPosition.Zero,
                    SelectionActionMode.None,
                    SelectionTarget.None);
            }
        }
    }
}
