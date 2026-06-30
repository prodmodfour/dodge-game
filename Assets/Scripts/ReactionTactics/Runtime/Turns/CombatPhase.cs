namespace ReactionTactics.Turns
{
    /// <summary>
    /// Explicit high-level phases for the tactical combat loop.
    /// Combat systems use these values to decide which commands are legal.
    /// </summary>
    public enum CombatPhase
    {
        NotStarted,
        RoundStart,
        ActiveTurn,
        ActionTargeting,
        ReactionWindow,
        ResolvingAction,
        RoundEnd,
        CombatOver,
    }
}
