using System;
using ReactionTactics.Core;
using ReactionTactics.Grid;
using ReactionTactics.Turns;
using ReactionTactics.Units;

namespace ReactionTactics.Actions
{
    /// <summary>
    /// Default UI-independent validation service for prototype ability declarations.
    /// It checks actor AP, action-vs-reaction phase legality, shape target data,
    /// range, team relationship, and map cell validity without spending resources.
    /// </summary>
    public sealed class AbilityTargetValidator : IAbilityTargetValidator
    {
        public static readonly AbilityTargetValidator Instance = new AbilityTargetValidator();

        public TacticalResult ValidateTarget(AbilityTargetValidationContext context)
        {
            var contextResult = ValidateRequiredContext(context);
            if (contextResult.IsFailure)
            {
                return contextResult;
            }

            var definitionResult = context.Ability.ValidateDefinition();
            if (definitionResult.IsFailure)
            {
                return TacticalResult.Failure(
                    $"Cannot use ability '{DescribeAbility(context.Ability)}': {definitionResult.ErrorMessage}");
            }

            var actorResult = ValidateActor(context.Actor);
            if (actorResult.IsFailure)
            {
                return actorResult;
            }

            var usageResult = ValidateUsage(context);
            if (usageResult.IsFailure)
            {
                return usageResult;
            }

            var phaseResult = ValidatePhase(context);
            if (phaseResult.IsFailure)
            {
                return phaseResult;
            }

            var apResult = ValidateActionPoints(context.Actor, context.Ability);
            if (apResult.IsFailure)
            {
                return apResult;
            }

            var actorCellResult = ValidateActorCell(context);
            if (actorCellResult.IsFailure)
            {
                return actorCellResult;
            }

            var targetShapeResult = context.Target.ValidateForShape(context.Ability.Shape);
            if (targetShapeResult.IsFailure)
            {
                return TacticalResult.Failure($"{DescribeAbility(context.Ability)} target is invalid: {targetShapeResult.ErrorMessage}");
            }

            var targetExistenceResult = ValidateTargetExistence(context);
            if (targetExistenceResult.IsFailure)
            {
                return targetExistenceResult;
            }

            var relationshipResult = ValidateTargetRelationship(context);
            if (relationshipResult.IsFailure)
            {
                return relationshipResult;
            }

            var rangeResult = ValidateRange(context);
            if (rangeResult.IsFailure)
            {
                return rangeResult;
            }

            return TacticalResult.Success();
        }

        private static TacticalResult ValidateRequiredContext(AbilityTargetValidationContext context)
        {
            if (context == null)
            {
                return TacticalResult.Failure("Cannot validate ability target because validation context is missing.");
            }

            if (context.Actor == null)
            {
                return TacticalResult.Failure("Cannot validate ability target because no acting unit was provided.");
            }

            if (context.Ability == null)
            {
                return TacticalResult.Failure($"Cannot validate ability target for {DescribeUnit(context.Actor)} because no ability was provided.");
            }

            if (context.CombatState == null)
            {
                return TacticalResult.Failure($"Cannot validate {DescribeAbility(context.Ability)} because combat state is missing.");
            }

            if (context.Map == null)
            {
                return TacticalResult.Failure($"Cannot validate {DescribeAbility(context.Ability)} because no grid map was provided.");
            }

            if (!IsExactRequestedUsage(context.RequestedUsage))
            {
                return TacticalResult.Failure(
                    $"Cannot validate {DescribeAbility(context.Ability)} because requested usage must be exactly {AbilityUsage.Action} or {AbilityUsage.Reaction}.");
            }

            if (!Enum.IsDefined(typeof(AbilityTargetRelationship), context.RequiredRelationship))
            {
                return TacticalResult.Failure(
                    $"Cannot validate {DescribeAbility(context.Ability)} because target relationship '{context.RequiredRelationship}' is not supported.");
            }

            return TacticalResult.Success();
        }

        private static TacticalResult ValidateActor(TacticalUnit actor)
        {
            if (!actor.IsInitialized)
            {
                return TacticalResult.Failure($"{DescribeUnit(actor)} cannot use abilities because it is not initialized.");
            }

            if (!actor.IsAlive)
            {
                return TacticalResult.Failure($"{DescribeUnit(actor)} cannot use abilities because it is defeated.");
            }

            return TacticalResult.Success();
        }

        private static TacticalResult ValidateUsage(AbilityTargetValidationContext context)
        {
            if (context.Ability.SupportsUsage(context.RequestedUsage))
            {
                return TacticalResult.Success();
            }

            return TacticalResult.Failure(
                $"{DescribeAbility(context.Ability)} cannot be used as {DescribeRequestedUsage(context.RequestedUsage)}.");
        }

        private static TacticalResult ValidatePhase(AbilityTargetValidationContext context)
        {
            if (context.RequestedUsage == AbilityUsage.Action)
            {
                return ValidateActiveActionPhase(context);
            }

            return ValidateReactionPhase(context);
        }

        private static TacticalResult ValidateActiveActionPhase(AbilityTargetValidationContext context)
        {
            var state = context.CombatState;
            if (!state.IsActiveUnitPhase)
            {
                return TacticalResult.Failure(
                    $"{DescribeUnit(context.Actor)} cannot use {DescribeAbility(context.Ability)} as an active action while combat phase is {state.Phase}.");
            }

            if (state.ActiveUnit == null)
            {
                return TacticalResult.Failure(
                    $"{DescribeUnit(context.Actor)} cannot use {DescribeAbility(context.Ability)} because no active unit is selected.");
            }

            if (!ReferenceEquals(context.Actor, state.ActiveUnit))
            {
                return TacticalResult.Failure(
                    $"{DescribeUnit(context.Actor)} cannot use {DescribeAbility(context.Ability)} because the active unit is {DescribeUnit(state.ActiveUnit)}.");
            }

            return TacticalResult.Success();
        }

        private static TacticalResult ValidateReactionPhase(AbilityTargetValidationContext context)
        {
            var state = context.CombatState;
            if (!state.IsReactionPhase)
            {
                return TacticalResult.Failure(
                    $"{DescribeUnit(context.Actor)} cannot use {DescribeAbility(context.Ability)} as a reaction while combat phase is {state.Phase}.");
            }

            if (state.ReactingUnit == null)
            {
                return TacticalResult.Failure(
                    $"{DescribeUnit(context.Actor)} cannot use {DescribeAbility(context.Ability)} because no reacting unit is selected.");
            }

            if (!ReferenceEquals(context.Actor, state.ReactingUnit))
            {
                return TacticalResult.Failure(
                    $"{DescribeUnit(context.Actor)} cannot use {DescribeAbility(context.Ability)} because the reacting unit is {DescribeUnit(state.ReactingUnit)}.");
            }

            return TacticalResult.Success();
        }

        private static TacticalResult ValidateActionPoints(TacticalUnit actor, AbilityDefinition ability)
        {
            if (actor.CanSpendAP(ability.APCost))
            {
                return TacticalResult.Success();
            }

            return TacticalResult.Failure(
                $"{DescribeUnit(actor)} cannot use {DescribeAbility(ability)} because it needs {ability.APCost} AP but only has {actor.CurrentAP} AP.");
        }

        private static TacticalResult ValidateActorCell(AbilityTargetValidationContext context)
        {
            var actorPosition = context.Actor.CurrentGridPosition;
            if (!context.Map.TryGetCell(actorPosition, out _))
            {
                return TacticalResult.Failure(
                    $"{DescribeUnit(context.Actor)} cannot use {DescribeAbility(context.Ability)} because its current cell {actorPosition} is not on the grid map.");
            }

            if (!context.Map.IsWalkable(actorPosition))
            {
                return TacticalResult.Failure(
                    $"{DescribeUnit(context.Actor)} cannot use {DescribeAbility(context.Ability)} because its current cell {actorPosition} is not walkable.");
            }

            return TacticalResult.Success();
        }

        private static TacticalResult ValidateTargetExistence(AbilityTargetValidationContext context)
        {
            switch (context.Ability.Shape)
            {
                case AbilityShape.Self:
                    return ValidateSelfTarget(context);
                case AbilityShape.SingleTarget:
                case AbilityShape.Melee:
                    return ValidateUnitTarget(context);
                case AbilityShape.Cone:
                case AbilityShape.Radius:
                    return ValidateCellTarget(context);
                default:
                    return TacticalResult.Failure($"{DescribeAbility(context.Ability)} shape {context.Ability.Shape} is not supported.");
            }
        }

        private static TacticalResult ValidateSelfTarget(AbilityTargetValidationContext context)
        {
            if (context.Target.HasTargetUnit && !ReferenceEquals(context.Target.TargetUnit, context.Actor))
            {
                return TacticalResult.Failure(
                    $"{DescribeAbility(context.Ability)} is a self ability and cannot target {DescribeUnit(context.Target.TargetUnit)}.");
            }

            if (context.Target.HasTargetCell && context.Target.TargetCell != context.Actor.CurrentGridPosition)
            {
                return TacticalResult.Failure(
                    $"{DescribeAbility(context.Ability)} is a self ability, so target cell {context.Target.TargetCell} must match {DescribeUnit(context.Actor)} at {context.Actor.CurrentGridPosition}.");
            }

            return TacticalResult.Success();
        }

        private static TacticalResult ValidateUnitTarget(AbilityTargetValidationContext context)
        {
            var targetUnit = context.Target.TargetUnit;
            if (targetUnit == null)
            {
                return TacticalResult.Failure($"{DescribeAbility(context.Ability)} requires an existing target unit.");
            }

            if (!targetUnit.IsInitialized)
            {
                return TacticalResult.Failure($"{DescribeAbility(context.Ability)} cannot target {DescribeUnit(targetUnit)} because it is not initialized.");
            }

            if (!targetUnit.IsAlive)
            {
                return TacticalResult.Failure($"{DescribeAbility(context.Ability)} cannot target {DescribeUnit(targetUnit)} because it is defeated.");
            }

            if (!context.Target.HasTargetCell)
            {
                return TacticalResult.Failure($"{DescribeAbility(context.Ability)} requires a captured target cell for {DescribeUnit(targetUnit)}.");
            }

            if (context.Target.TargetCell != targetUnit.CurrentGridPosition)
            {
                return TacticalResult.Failure(
                    $"{DescribeAbility(context.Ability)} target cell {context.Target.TargetCell} no longer matches {DescribeUnit(targetUnit)} at {targetUnit.CurrentGridPosition}.");
            }

            if (!context.Map.TryGetCell(context.Target.TargetCell, out _))
            {
                return TacticalResult.Failure(
                    $"{DescribeAbility(context.Ability)} cannot target {DescribeUnit(targetUnit)} because cell {context.Target.TargetCell} is not on the grid map.");
            }

            if (!context.Map.IsWalkable(context.Target.TargetCell))
            {
                return TacticalResult.Failure(
                    $"{DescribeAbility(context.Ability)} cannot target {DescribeUnit(targetUnit)} because cell {context.Target.TargetCell} is not walkable.");
            }

            return TacticalResult.Success();
        }

        private static TacticalResult ValidateCellTarget(AbilityTargetValidationContext context)
        {
            if (!context.Target.HasTargetCell)
            {
                return TacticalResult.Failure($"{DescribeAbility(context.Ability)} requires a target cell.");
            }

            if (!context.Map.TryGetCell(context.Target.TargetCell, out _))
            {
                return TacticalResult.Failure(
                    $"{DescribeAbility(context.Ability)} target cell {context.Target.TargetCell} is not on the grid map.");
            }

            if (context.RequireWalkableTargetCell && !context.Map.IsWalkable(context.Target.TargetCell))
            {
                return TacticalResult.Failure(
                    $"{DescribeAbility(context.Ability)} target cell {context.Target.TargetCell} is not walkable.");
            }

            return TacticalResult.Success();
        }

        private static TacticalResult ValidateTargetRelationship(AbilityTargetValidationContext context)
        {
            var relationship = ResolveTargetRelationship(context);
            switch (relationship)
            {
                case AbilityTargetRelationship.Any:
                    return TacticalResult.Success();
                case AbilityTargetRelationship.Self:
                    return ValidateSelfRelationship(context);
                case AbilityTargetRelationship.Friendly:
                    return ValidateFriendlyRelationship(context);
                case AbilityTargetRelationship.Hostile:
                    return ValidateHostileRelationship(context);
                default:
                    return TacticalResult.Failure(
                        $"{DescribeAbility(context.Ability)} target relationship '{relationship}' is not supported.");
            }
        }

        private static TacticalResult ValidateSelfRelationship(AbilityTargetValidationContext context)
        {
            if (context.Target.HasTargetUnit && !ReferenceEquals(context.Target.TargetUnit, context.Actor))
            {
                return TacticalResult.Failure(
                    $"{DescribeAbility(context.Ability)} must target {DescribeUnit(context.Actor)}, not {DescribeUnit(context.Target.TargetUnit)}.");
            }

            if (context.Target.HasTargetCell && context.Target.TargetCell != context.Actor.CurrentGridPosition)
            {
                return TacticalResult.Failure(
                    $"{DescribeAbility(context.Ability)} must target {DescribeUnit(context.Actor)} at {context.Actor.CurrentGridPosition}, not cell {context.Target.TargetCell}.");
            }

            return TacticalResult.Success();
        }

        private static TacticalResult ValidateFriendlyRelationship(AbilityTargetValidationContext context)
        {
            if (!context.Target.HasTargetUnit)
            {
                return TacticalResult.Failure($"{DescribeAbility(context.Ability)} requires a friendly target unit.");
            }

            if (context.Actor.Team.IsFriendlyTo(context.Target.TargetUnit.Team))
            {
                return TacticalResult.Success();
            }

            return TacticalResult.Failure(
                $"{DescribeAbility(context.Ability)} requires a friendly target, but {DescribeUnit(context.Target.TargetUnit)} is {context.Target.TargetUnit.Team} and {DescribeUnit(context.Actor)} is {context.Actor.Team}.");
        }

        private static TacticalResult ValidateHostileRelationship(AbilityTargetValidationContext context)
        {
            if (!context.Target.HasTargetUnit)
            {
                return TacticalResult.Failure($"{DescribeAbility(context.Ability)} requires a hostile target unit.");
            }

            if (context.Actor.Team.IsHostileTo(context.Target.TargetUnit.Team))
            {
                return TacticalResult.Success();
            }

            return TacticalResult.Failure(
                $"{DescribeAbility(context.Ability)} requires a hostile target, but {DescribeUnit(context.Target.TargetUnit)} is friendly to {DescribeUnit(context.Actor)}.");
        }

        private static TacticalResult ValidateRange(AbilityTargetValidationContext context)
        {
            if (context.Ability.Shape == AbilityShape.Self)
            {
                return TacticalResult.Success();
            }

            if (!context.Target.HasTargetCell)
            {
                return TacticalResult.Success();
            }

            if (context.Ability.Shape == AbilityShape.Melee)
            {
                return ValidateMeleeRange(context);
            }

            if (context.Ability.Shape == AbilityShape.Cone)
            {
                return ValidateConeRange(context);
            }

            var range = GetRangeLimit(context.Actor, context.Ability);
            var distance = context.Actor.CurrentGridPosition.HorizontalDistanceTo(context.Target.TargetCell);
            if (distance <= range)
            {
                return TacticalResult.Success();
            }

            return TacticalResult.Failure(
                $"{DescribeAbility(context.Ability)} target cell {context.Target.TargetCell} is {distance} cells away from {DescribeUnit(context.Actor)}, beyond range {range}.");
        }

        private static TacticalResult ValidateMeleeRange(AbilityTargetValidationContext context)
        {
            var actorPosition = context.Actor.CurrentGridPosition;
            var targetCell = context.Target.TargetCell;
            var distance = actorPosition.HorizontalDistanceTo(targetCell);
            var range = GetRangeLimit(context.Actor, context.Ability);

            if (distance == 0)
            {
                return TacticalResult.Failure(
                    $"{DescribeAbility(context.Ability)} requires an adjacent hostile target; {DescribeUnit(context.Target.TargetUnit)} is in the same cell {targetCell} as {DescribeUnit(context.Actor)}.");
            }

            if (distance <= range)
            {
                return TacticalResult.Success();
            }

            return TacticalResult.Failure(
                $"{DescribeAbility(context.Ability)} requires a hostile target in melee range {range}, but target cell {targetCell} is {distance} cells away from {DescribeUnit(context.Actor)}.");
        }

        private static TacticalResult ValidateConeRange(AbilityTargetValidationContext context)
        {
            var actorPosition = context.Actor.CurrentGridPosition;
            var targetCell = context.Target.TargetCell;
            var distance = actorPosition.HorizontalDistanceTo(targetCell);
            var range = GetRangeLimit(context.Actor, context.Ability);

            if (distance == 0)
            {
                return TacticalResult.Failure(
                    $"{DescribeAbility(context.Ability)} requires a target cell away from {DescribeUnit(context.Actor)} to choose a cone direction.");
            }

            var resolvedDirection = CardinalDirectionMath.FromTo(actorPosition, targetCell);
            if (context.Target.HasDirection && context.Target.Direction != resolvedDirection)
            {
                return TacticalResult.Failure(
                    $"{DescribeAbility(context.Ability)} target cell {targetCell} resolves to {resolvedDirection} from {DescribeUnit(context.Actor)} at {actorPosition}, not {context.Target.Direction}.");
            }

            if (distance <= range)
            {
                return TacticalResult.Success();
            }

            return TacticalResult.Failure(
                $"{DescribeAbility(context.Ability)} target cell {targetCell} is {distance} cells away from {DescribeUnit(context.Actor)}, beyond range {range}.");
        }

        private static AbilityTargetRelationship ResolveTargetRelationship(AbilityTargetValidationContext context)
        {
            if (context.RequiredRelationship != AbilityTargetRelationship.Automatic)
            {
                return context.RequiredRelationship;
            }

            switch (context.Ability.Shape)
            {
                case AbilityShape.Self:
                    return AbilityTargetRelationship.Self;
                case AbilityShape.Melee:
                    return AbilityTargetRelationship.Hostile;
                case AbilityShape.SingleTarget:
                    return context.Ability.Damage > 0
                        ? AbilityTargetRelationship.Hostile
                        : AbilityTargetRelationship.Any;
                default:
                    return AbilityTargetRelationship.Any;
            }
        }

        private static int GetRangeLimit(TacticalUnit actor, AbilityDefinition ability)
        {
            return ability.Shape == AbilityShape.Melee
                ? Math.Max(UnitStatsDefinition.MinimumMeleeRange, actor.MeleeRange)
                : ability.Range;
        }

        private static bool IsExactRequestedUsage(AbilityUsage requestedUsage)
        {
            return requestedUsage == AbilityUsage.Action || requestedUsage == AbilityUsage.Reaction;
        }

        private static string DescribeRequestedUsage(AbilityUsage requestedUsage)
        {
            return requestedUsage == AbilityUsage.Reaction ? "a reaction" : "an active action";
        }

        private static string DescribeAbility(AbilityDefinition ability)
        {
            return ability == null ? "no ability" : ability.DisplayName;
        }

        private static string DescribeUnit(TacticalUnit unit)
        {
            if (unit == null)
            {
                return "no unit";
            }

            return unit.IsInitialized ? $"{unit.DisplayName} {unit.UnitId}" : unit.DisplayName;
        }
    }
}
