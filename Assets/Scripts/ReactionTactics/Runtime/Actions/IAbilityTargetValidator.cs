using ReactionTactics.Core;

namespace ReactionTactics.Actions
{
    /// <summary>
    /// Validates ability use and target legality without depending on UI selection state.
    /// Implementations must report expected player-facing failures as TacticalResult values.
    /// </summary>
    public interface IAbilityTargetValidator
    {
        TacticalResult ValidateTarget(AbilityTargetValidationContext context);
    }
}
