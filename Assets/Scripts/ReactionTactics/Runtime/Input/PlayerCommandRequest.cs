using ReactionTactics.Units;

namespace ReactionTactics.Input
{
    /// <summary>
    /// Immutable payload emitted by <see cref="PlayerCommandRouter"/> whenever input is
    /// translated into a high-level player command request.
    /// </summary>
    public readonly struct PlayerCommandRequest
    {
        public PlayerCommandRequest(
            PlayerCommandType commandType,
            TacticalUnit unit,
            SelectionActionMode actionMode,
            SelectionTarget target,
            SelectionState selectionState)
        {
            CommandType = commandType;
            Unit = unit;
            ActionMode = actionMode;
            Target = target;
            SelectionState = selectionState;
        }

        public PlayerCommandType CommandType { get; }

        public TacticalUnit Unit { get; }

        public bool HasUnit
        {
            get { return Unit != null && Unit.IsAlive; }
        }

        public SelectionActionMode ActionMode { get; }

        public SelectionTarget Target { get; }

        public bool HasTarget
        {
            get { return Target.HasTarget; }
        }

        public SelectionState SelectionState { get; }

        public override string ToString()
        {
            var unitLabel = HasUnit ? Unit.DisplayName : "no unit";
            var targetLabel = HasTarget ? Target.ToString() : "no target";
            return $"{CommandType} unit={unitLabel} mode={ActionMode} target={targetLabel}";
        }
    }
}
