using System;
using ReactionTactics.Core;
using ReactionTactics.Grid;
using ReactionTactics.Units;

namespace ReactionTactics.Actions
{
    /// <summary>
    /// Immutable target payload for declared abilities. UI systems can translate clicks
    /// into this model without leaking selection state into action declaration, preview,
    /// or resolution systems.
    /// </summary>
    public readonly struct ActionTarget
    {
        private readonly GridPosition targetCell;
        private readonly CardinalDirection direction;

        private ActionTarget(
            TacticalUnit targetUnit,
            bool hasTargetCell,
            GridPosition targetCell,
            bool hasDirection,
            CardinalDirection direction)
        {
            TargetUnit = targetUnit;
            HasTargetCell = hasTargetCell;
            this.targetCell = targetCell;
            HasDirection = hasDirection;
            this.direction = direction;
        }

        public TacticalUnit TargetUnit { get; }

        public bool HasTargetUnit
        {
            get { return TargetUnit != null; }
        }

        public bool HasTargetCell { get; }

        /// <summary>
        /// Cell captured for the action target. Unit targets capture the unit's grid
        /// position when the target is created so declarations can preserve intent even
        /// if the unit moves later during reactions.
        /// </summary>
        public GridPosition TargetCell
        {
            get { return targetCell; }
        }

        public bool HasDirection { get; }

        public CardinalDirection Direction
        {
            get { return direction; }
        }

        public bool IsEmpty
        {
            get { return !HasTargetUnit && !HasTargetCell && !HasDirection; }
        }

        public static ActionTarget None
        {
            get { return default; }
        }

        public static ActionTarget ForCell(GridPosition targetCell)
        {
            return new ActionTarget(null, true, targetCell, false, default);
        }

        public static ActionTarget ForUnit(TacticalUnit targetUnit)
        {
            if (targetUnit == null)
            {
                throw new ArgumentNullException(nameof(targetUnit));
            }

            return ForUnit(targetUnit, targetUnit.CurrentGridPosition);
        }

        public static ActionTarget ForUnit(TacticalUnit targetUnit, GridPosition targetCell)
        {
            if (targetUnit == null)
            {
                throw new ArgumentNullException(nameof(targetUnit));
            }

            return new ActionTarget(targetUnit, true, targetCell, false, default);
        }

        public static ActionTarget ForCellAndDirection(GridPosition targetCell, CardinalDirection direction)
        {
            ValidateDirection(direction);
            return new ActionTarget(null, true, targetCell, true, direction);
        }

        public ActionTarget WithDirection(CardinalDirection direction)
        {
            ValidateDirection(direction);
            return new ActionTarget(TargetUnit, HasTargetCell, targetCell, true, direction);
        }

        public TacticalResult ValidateForShape(AbilityShape shape)
        {
            if (!Enum.IsDefined(typeof(AbilityShape), shape))
            {
                return TacticalResult.Failure($"Ability shape '{shape}' is not supported by action targets.");
            }

            switch (shape)
            {
                case AbilityShape.Self:
                    return TacticalResult.Success();
                case AbilityShape.SingleTarget:
                    return RequireTargetUnit("Single-target abilities");
                case AbilityShape.Melee:
                    return RequireTargetUnit("Melee abilities");
                case AbilityShape.Cone:
                    return ValidateConeTarget();
                case AbilityShape.Radius:
                    return RequireTargetCell("Radius abilities");
                default:
                    return TacticalResult.Failure($"Ability shape '{shape}' is not supported by action targets.");
            }
        }

        public static TacticalResult ValidateForShape(AbilityShape shape, ActionTarget target)
        {
            return target.ValidateForShape(shape);
        }

        public override string ToString()
        {
            if (IsEmpty)
            {
                return "No action target";
            }

            var unitText = HasTargetUnit ? $"unit={TargetUnit.DisplayName} {TargetUnit.UnitId}" : "unit=none";
            var cellText = HasTargetCell ? $"cell={TargetCell}" : "cell=none";
            var directionText = HasDirection ? $"direction={Direction}" : "direction=none";
            return $"ActionTarget({unitText}, {cellText}, {directionText})";
        }

        private TacticalResult RequireTargetUnit(string label)
        {
            return HasTargetUnit
                ? TacticalResult.Success()
                : TacticalResult.Failure($"{label} require a target unit.");
        }

        private TacticalResult RequireTargetCell(string label)
        {
            return HasTargetCell
                ? TacticalResult.Success()
                : TacticalResult.Failure($"{label} require a target cell.");
        }

        private TacticalResult ValidateConeTarget()
        {
            return HasTargetCell
                ? TacticalResult.Success()
                : TacticalResult.Failure("Cone abilities require a target cell.");
        }

        private static void ValidateDirection(CardinalDirection direction)
        {
            if (!Enum.IsDefined(typeof(CardinalDirection), direction))
            {
                throw new ArgumentOutOfRangeException(nameof(direction), direction, "Unknown cardinal direction.");
            }
        }
    }
}
