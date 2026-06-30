namespace ReactionTactics.Input
{
    /// <summary>
    /// High-level player command requests routed from UI and mouse input.
    /// Command execution and combat legality are handled by later gameplay systems.
    /// </summary>
    public enum PlayerCommandType
    {
        SelectUnit = 0,
        SelectMove = 1,
        SelectAttack = 2,
        ConfirmTarget = 3,
        Cancel = 4,
        EndTurn = 5
    }
}
