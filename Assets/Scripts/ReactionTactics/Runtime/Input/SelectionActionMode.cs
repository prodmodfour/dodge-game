namespace ReactionTactics.Input
{
    /// <summary>
    /// Prototype interaction modes selected by input or UI before a command is confirmed.
    /// These labels describe player intent only; command execution and legality live in later systems.
    /// </summary>
    public enum SelectionActionMode
    {
        None = 0,
        Move = 1,
        Melee = 2,
        Cone = 3,
        AreaOfEffect = 4,
        Brace = 5
    }
}
