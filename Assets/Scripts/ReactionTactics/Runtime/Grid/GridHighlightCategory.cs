namespace ReactionTactics.Grid
{
    /// <summary>
    /// Logical visual layers that can be applied to grid tiles by the
    /// <see cref="GridHighlightManager" />. The manager resolves overlapping
    /// categories by priority so movement, targeting, and reaction systems can
    /// publish highlights independently without directly fighting over tile materials.
    /// </summary>
    public enum GridHighlightCategory
    {
        MovementRange = 0,
        SelectedPath = 1,
        ActionDanger = 2,
        ReactionSafe = 3,
        ReactionThreatened = 4,
        TargetCell = 5
    }
}
