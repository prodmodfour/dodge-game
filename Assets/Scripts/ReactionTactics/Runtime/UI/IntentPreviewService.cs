using System;
using System.Collections.Generic;
using ReactionTactics.Actions;
using ReactionTactics.Grid;
using ReactionTactics.Targeting;
using ReactionTactics.Turns;
using ReactionTactics.Units;

namespace ReactionTactics.UI
{
    /// <summary>
    /// Produces side-effect-free previews for ability targeting UI. The service reuses
    /// ability target validation so invalid previews can report the same player-facing
    /// reasons as action declarations without spending AP or creating intents.
    /// </summary>
    public sealed class IntentPreviewService
    {
        private readonly IAbilityTargetValidator targetValidator;

        public IntentPreviewService()
            : this(AbilityTargetValidator.Instance)
        {
        }

        public IntentPreviewService(IAbilityTargetValidator targetValidator)
        {
            this.targetValidator = targetValidator ?? throw new ArgumentNullException(nameof(targetValidator));
        }

        public ActionIntentPreview CreatePreview(
            TacticalUnit actor,
            AbilityDefinition ability,
            ActionTarget target,
            CombatState combatState,
            IGridMap map,
            IEnumerable<TacticalUnit> potentialUnits = null,
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

            return CreatePreview(context, potentialUnits);
        }

        public ActionIntentPreview CreatePreview(
            AbilityTargetValidationContext context,
            IEnumerable<TacticalUnit> potentialUnits = null)
        {
            if (context == null)
            {
                return ActionIntentPreview.Invalid(
                    actor: null,
                    ability: null,
                    target: ActionTarget.None,
                    invalidReason: "Cannot preview action intent because validation context is missing.");
            }

            var validationResult = targetValidator.ValidateTarget(context);
            if (validationResult.IsFailure)
            {
                return ActionIntentPreview.Invalid(
                    context.Actor,
                    context.Ability,
                    context.Target,
                    validationResult.ErrorMessage);
            }

            var affectedCells = CreateAffectedCells(context);
            var threatenedUnits = CreateThreatenedUnits(context, affectedCells, potentialUnits);
            return ActionIntentPreview.Valid(
                context.Actor,
                context.Ability,
                context.Target,
                affectedCells,
                threatenedUnits);
        }

        private static IReadOnlyList<GridPosition> CreateAffectedCells(AbilityTargetValidationContext context)
        {
            switch (context.Ability.Shape)
            {
                case AbilityShape.Self:
                    return new[]
                    {
                        context.Target.HasTargetCell ? context.Target.TargetCell : context.Actor.CurrentGridPosition
                    };
                case AbilityShape.SingleTarget:
                case AbilityShape.Melee:
                    return context.Target.HasTargetCell
                        ? new[] { context.Target.TargetCell }
                        : Array.Empty<GridPosition>();
                case AbilityShape.Cone:
                    var direction = CardinalDirectionMath.FromTo(
                        context.Actor.CurrentGridPosition,
                        context.Target.TargetCell);
                    return AreaShapeService.GetConeCells(
                        context.Actor.CurrentGridPosition,
                        direction,
                        context.Ability.Range,
                        context.Map);
                case AbilityShape.Radius:
                    return AreaShapeService.GetRadiusCells(
                        context.Target.TargetCell,
                        context.Ability.Radius,
                        context.Map);
                default:
                    return Array.Empty<GridPosition>();
            }
        }

        private static IReadOnlyList<TacticalUnit> CreateThreatenedUnits(
            AbilityTargetValidationContext context,
            IReadOnlyCollection<GridPosition> affectedCells,
            IEnumerable<TacticalUnit> potentialUnits)
        {
            if (context.Ability.Damage <= 0)
            {
                return Array.Empty<TacticalUnit>();
            }

            var threatened = new List<TacticalUnit>();
            AddThreatenedUnit(threatened, context.Target.TargetUnit);

            if (affectedCells == null || affectedCells.Count == 0 || potentialUnits == null)
            {
                return threatened;
            }

            var affectedSet = new HashSet<GridPosition>(affectedCells);
            foreach (var unit in potentialUnits)
            {
                if (unit == null || !unit.IsAlive || ReferenceEquals(unit, context.Actor))
                {
                    continue;
                }

                if (affectedSet.Contains(unit.CurrentGridPosition))
                {
                    AddThreatenedUnit(threatened, unit);
                }
            }

            return threatened;
        }

        private static void AddThreatenedUnit(ICollection<TacticalUnit> threatened, TacticalUnit unit)
        {
            if (unit == null || !unit.IsAlive || ContainsReference(threatened, unit))
            {
                return;
            }

            threatened.Add(unit);
        }

        private static bool ContainsReference(IEnumerable<TacticalUnit> units, TacticalUnit candidate)
        {
            foreach (var unit in units)
            {
                if (ReferenceEquals(unit, candidate))
                {
                    return true;
                }
            }

            return false;
        }
    }
}
