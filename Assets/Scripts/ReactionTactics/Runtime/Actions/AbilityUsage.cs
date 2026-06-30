using System;

namespace ReactionTactics.Actions
{
    /// <summary>
    /// Declares which combat command paths may use an ability.
    /// Flags allow an ability to be legal as an active action, a reaction, or both.
    /// </summary>
    [Flags]
    public enum AbilityUsage
    {
        None = 0,
        Action = 1 << 0,
        Reaction = 1 << 1,
        Both = Action | Reaction
    }
}
