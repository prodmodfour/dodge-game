using System;
using System.Collections.Generic;
using System.Linq;
using ReactionTactics.Core;
using ReactionTactics.Grid;
using ReactionTactics.Turns;
using ReactionTactics.Units;

namespace ReactionTactics.Actions
{
    /// <summary>
    /// Creates declared active-action intents after target validation and AP payment.
    /// This service does not resolve damage or start reaction windows; later combat flow
    /// systems consume the returned <see cref="ActionIntent" />.
    /// </summary>
    public sealed class ActionDeclarationService
    {
        private readonly IAbilityTargetValidator targetValidator;
        private int nextDeclarationSequence;

        public ActionDeclarationService()
            : this(AbilityTargetValidator.Instance)
        {
        }

        public ActionDeclarationService(IAbilityTargetValidator targetValidator)
        {
            this.targetValidator = targetValidator ?? throw new ArgumentNullException(nameof(targetValidator));
        }

        public int NextDeclarationSequence
        {
            get { return nextDeclarationSequence; }
        }

        public TacticalResult<ActionIntent> DeclareAction(
            TacticalUnit actor,
            AbilityDefinition ability,
            ActionTarget target,
            CombatState combatState,
            IGridMap map,
            IEnumerable<GridPosition> declaredAffectedCells = null,
            AbilityTargetRelationship requiredRelationship = AbilityTargetRelationship.Automatic,
            bool requireWalkableTargetCell = false)
        {
            var context = new AbilityTargetValidationContext(
                actor,
                ability,
                target,
                AbilityUsage.Action,
                combatState,
                map,
                requiredRelationship,
                requireWalkableTargetCell);

            return DeclareAction(context, declaredAffectedCells);
        }

        public TacticalResult<ActionIntent> DeclareAction(
            AbilityTargetValidationContext context,
            IEnumerable<GridPosition> declaredAffectedCells = null)
        {
            var actionContextResult = ValidateActionDeclarationContext(context);
            if (actionContextResult.IsFailure)
            {
                return TacticalResult<ActionIntent>.Failure(actionContextResult.ErrorMessage);
            }

            var validationResult = targetValidator.ValidateTarget(context);
            if (validationResult.IsFailure)
            {
                return TacticalResult<ActionIntent>.Failure(validationResult.ErrorMessage);
            }

            var affectedCells = CopyDeclaredAffectedCells(declaredAffectedCells);
            var spendResult = context.Actor.SpendAP(context.Ability.APCost);
            if (spendResult.IsFailure)
            {
                return TacticalResult<ActionIntent>.Failure(spendResult.ErrorMessage);
            }

            var sequence = nextDeclarationSequence;
            var intent = new ActionIntent(
                context.Actor,
                context.Ability,
                context.Actor.CurrentGridPosition,
                context.Target,
                affectedCells,
                context.CombatState.CurrentRound,
                sequence);

            nextDeclarationSequence += 1;
            return TacticalResult<ActionIntent>.Success(intent);
        }

        private static TacticalResult ValidateActionDeclarationContext(AbilityTargetValidationContext context)
        {
            if (context == null)
            {
                return TacticalResult.Failure("Cannot declare an action because validation context is missing.");
            }

            if (context.Actor == null)
            {
                return TacticalResult.Failure("Cannot declare an action because no acting unit was provided.");
            }

            if (context.Ability == null)
            {
                return TacticalResult.Failure("Cannot declare an action because no ability was provided.");
            }

            if (context.CombatState == null)
            {
                return TacticalResult.Failure("Cannot declare an action because combat state is missing.");
            }

            if (context.RequestedUsage != AbilityUsage.Action)
            {
                return TacticalResult.Failure(
                    $"Cannot declare an active action with requested usage {context.RequestedUsage}. Action declarations must request {AbilityUsage.Action}.");
            }

            return TacticalResult.Success();
        }

        private static GridPosition[] CopyDeclaredAffectedCells(IEnumerable<GridPosition> declaredAffectedCells)
        {
            return declaredAffectedCells == null ? Array.Empty<GridPosition>() : declaredAffectedCells.ToArray();
        }
    }
}
