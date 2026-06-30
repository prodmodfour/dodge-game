namespace ReactionTactics.Input
{
    /// <summary>
    /// Explains why the current interaction selection was cleared.
    /// </summary>
    public enum SelectionClearReason
    {
        Manual = 0,
        PhaseChanged = 1,
        UnitDied = 2,
        UnitUnavailable = 3
    }
}
