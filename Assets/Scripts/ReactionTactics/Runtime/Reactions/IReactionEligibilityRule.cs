using ReactionTactics.Actions;
using ReactionTactics.Core;
using ReactionTactics.Turns;
using ReactionTactics.Units;

namespace ReactionTactics.Reactions
{
    /// <summary>
    /// Extension point for future statuses, conditions, or special rules that can
    /// disable a unit's reaction turn without changing the core eligibility checks.
    /// </summary>
    public interface IReactionEligibilityRule
    {
        TacticalResult CanUnitReact(TacticalUnit unit, ActionIntent intent, CombatState combatState);
    }
}
