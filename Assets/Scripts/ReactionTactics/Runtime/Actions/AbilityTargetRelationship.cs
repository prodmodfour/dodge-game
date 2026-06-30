namespace ReactionTactics.Actions
{
    /// <summary>
    /// Relationship rule used by ability target validation when an ability needs a
    /// unit target to be self, friendly, hostile, or unconstrained.
    /// Automatic lets the validator infer the prototype default from the ability shape.
    /// </summary>
    public enum AbilityTargetRelationship
    {
        Automatic = 0,
        Any = 1,
        Self = 2,
        Friendly = 3,
        Hostile = 4
    }
}
