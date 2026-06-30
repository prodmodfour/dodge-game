namespace ReactionTactics.Reactions
{
    /// <summary>
    /// Lifecycle checkpoints for a reaction window opened by a declared action.
    /// </summary>
    public enum ReactionWindowPhase
    {
        NotOpened,
        Opened,
        ReactorStarted,
        ReactorCompleted,
        Closed,
    }
}
