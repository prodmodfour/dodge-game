namespace ReactionTactics.Actions
{
    /// <summary>
    /// Describes the targeting footprint used by an ability declaration and resolver.
    /// Shape-specific validation and preview systems decide what target data is required.
    /// </summary>
    public enum AbilityShape
    {
        Self = 0,
        SingleTarget = 1,
        Melee = 2,
        Cone = 3,
        Radius = 4
    }
}
