using System;
using ReactionTactics.Grid;
using ReactionTactics.Units;

namespace ReactionTactics.Input
{
    /// <summary>
    /// Immutable description of the unit or cell currently targeted by player interaction.
    /// </summary>
    public readonly struct SelectionTarget
    {
        private SelectionTarget(SelectionTargetKind kind, TacticalUnit unit, GridPosition cell)
        {
            Kind = kind;
            Unit = unit;
            Cell = cell;
        }

        public SelectionTargetKind Kind { get; }

        public TacticalUnit Unit { get; }

        public GridPosition Cell { get; }

        public bool HasTarget
        {
            get { return Kind != SelectionTargetKind.None; }
        }

        public bool HasCell
        {
            get { return Kind == SelectionTargetKind.Cell || Kind == SelectionTargetKind.Unit; }
        }

        public bool HasUnit
        {
            get { return Kind == SelectionTargetKind.Unit && Unit != null; }
        }

        public GridPosition CurrentCell
        {
            get { return HasUnit ? Unit.CurrentGridPosition : Cell; }
        }

        public static SelectionTarget None
        {
            get { return default; }
        }

        public static SelectionTarget ForCell(GridPosition cell)
        {
            return new SelectionTarget(SelectionTargetKind.Cell, null, cell);
        }

        public static SelectionTarget ForUnit(TacticalUnit unit)
        {
            if (unit == null)
            {
                throw new ArgumentNullException(nameof(unit));
            }

            return new SelectionTarget(SelectionTargetKind.Unit, unit, unit.CurrentGridPosition);
        }

        public override string ToString()
        {
            switch (Kind)
            {
                case SelectionTargetKind.Cell:
                    return $"Cell {Cell}";
                case SelectionTargetKind.Unit:
                    return HasUnit ? $"Unit {Unit.DisplayName} {Unit.UnitId} at {CurrentCell}" : "Missing unit target";
                default:
                    return "No target";
            }
        }
    }
}
