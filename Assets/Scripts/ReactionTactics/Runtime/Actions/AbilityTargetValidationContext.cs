using ReactionTactics.Grid;
using ReactionTactics.Turns;
using ReactionTactics.Units;

namespace ReactionTactics.Actions
{
    /// <summary>
    /// Immutable input bundle for validating whether an actor may use an ability on a
    /// target in the current combat phase and map state.
    /// </summary>
    public sealed class AbilityTargetValidationContext
    {
        public AbilityTargetValidationContext(
            TacticalUnit actor,
            AbilityDefinition ability,
            ActionTarget target,
            AbilityUsage requestedUsage,
            CombatState combatState,
            IGridMap map,
            AbilityTargetRelationship requiredRelationship = AbilityTargetRelationship.Automatic,
            bool requireWalkableTargetCell = false)
        {
            Actor = actor;
            Ability = ability;
            Target = target;
            RequestedUsage = requestedUsage;
            CombatState = combatState;
            Map = map;
            RequiredRelationship = requiredRelationship;
            RequireWalkableTargetCell = requireWalkableTargetCell;
        }

        public TacticalUnit Actor { get; }

        public AbilityDefinition Ability { get; }

        public ActionTarget Target { get; }

        public AbilityUsage RequestedUsage { get; }

        public CombatState CombatState { get; }

        public IGridMap Map { get; }

        /// <summary>
        /// Target relationship to enforce. Automatic applies prototype defaults:
        /// self abilities target self, melee and damaging single-target abilities target hostiles,
        /// and cell-shaped abilities are unconstrained by team.
        /// </summary>
        public AbilityTargetRelationship RequiredRelationship { get; }

        /// <summary>
        /// When true, cell-targeted shapes such as radius or cone abilities must choose
        /// a walkable, non-movement-blocking target cell. Unit targets always require a
        /// valid walkable occupied cell.
        /// </summary>
        public bool RequireWalkableTargetCell { get; }
    }
}
