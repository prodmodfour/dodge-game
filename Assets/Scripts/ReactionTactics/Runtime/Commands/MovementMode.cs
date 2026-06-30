namespace ReactionTactics.Commands
{
    /// <summary>
    /// Identifies which combat timing path produced a movement command.
    /// Active movement happens on the unit's own turn; reaction movement happens
    /// during another unit's reaction window.
    /// </summary>
    public enum MovementMode
    {
        Active = 0,
        Reaction = 1
    }
}
