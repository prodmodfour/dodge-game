using System;
using System.Collections.Generic;
using ReactionTactics.Core;
using ReactionTactics.Grid;
using ReactionTactics.Turns;
using ReactionTactics.Units;
using UnityEngine;

namespace ReactionTactics.Actions
{
    /// <summary>
    /// Resolves declared action intents after any reaction window has completed.
    /// Shape-specific hooks apply deterministic final-position checks and effects
    /// before notifying listeners that the original action is complete.
    /// </summary>
    public class ActionResolver
    {
        private const bool ConeFriendlyFireEnabled = false;

        private readonly CombatEventBus eventBus;
        private readonly UnityEngine.Object logContext;
        private readonly bool logResolutions;
        private readonly Func<IReadOnlyList<TacticalUnit>> combatantProvider;

        public ActionResolver(
            CombatEventBus eventBus = null,
            UnityEngine.Object logContext = null,
            bool logResolutions = true,
            Func<IReadOnlyList<TacticalUnit>> combatantProvider = null)
        {
            this.eventBus = eventBus;
            this.logContext = logContext;
            this.logResolutions = logResolutions;
            this.combatantProvider = combatantProvider;
        }

        /// <summary>
        /// Resolves a declared action intent and notifies listeners that the intent is complete.
        /// Melee resolution uses Option A timing: declared target first, reactions may move units,
        /// then damage is applied only when the target remains in final melee range. Cone and AoE
        /// resolution use declared affected cells and final unit positions without any random roll.
        /// </summary>
        public TacticalResult Resolve(ActionIntent intent)
        {
            var validationResult = ValidateIntent(intent);
            if (validationResult.IsFailure)
            {
                return validationResult;
            }

            var shapeResult = ResolveByShape(intent);
            if (shapeResult.IsFailure)
            {
                return shapeResult;
            }

            if (logResolutions)
            {
                Debug.Log(
                    $"Resolved action '{intent.Ability.DisplayName}' by {DescribeUnit(intent.Actor)} using {intent.Ability.Shape} resolver.",
                    logContext);
            }

            eventBus?.PublishActionResolved(intent.Actor, intent);
            return TacticalResult.Success();
        }

        /// <summary>
        /// Dispatches to shape-specific resolution hooks.
        /// </summary>
        protected virtual TacticalResult ResolveByShape(ActionIntent intent)
        {
            switch (intent.Ability.Shape)
            {
                case AbilityShape.Self:
                    return ResolveSelf(intent);
                case AbilityShape.SingleTarget:
                    return ResolveSingleTarget(intent);
                case AbilityShape.Melee:
                    return ResolveMelee(intent);
                case AbilityShape.Cone:
                    return ResolveCone(intent);
                case AbilityShape.Radius:
                    return ResolveRadius(intent);
                default:
                    return TacticalResult.Failure($"Cannot resolve action '{intent.Ability.DisplayName}' because shape {intent.Ability.Shape} is not supported.");
            }
        }

        protected virtual TacticalResult ResolveSelf(ActionIntent intent)
        {
            return TacticalResult.Success();
        }

        protected virtual TacticalResult ResolveSingleTarget(ActionIntent intent)
        {
            return TacticalResult.Success();
        }

        protected virtual TacticalResult ResolveMelee(ActionIntent intent)
        {
            var target = intent.DeclaredTargetUnit;
            if (target == null)
            {
                return TacticalResult.Failure($"Cannot resolve melee action '{intent.Ability.DisplayName}' because it has no declared target unit.");
            }

            if (!target.IsAlive)
            {
                PresentMeleeResolution(intent, target);
                LogMeleeAvoided(intent, target, $"{DescribeUnit(target)} is no longer alive at resolution.");
                return TacticalResult.Success();
            }

            var meleeRange = GetMeleeRange(intent.Actor);
            var finalDistance = intent.Actor.CurrentGridPosition.HorizontalDistanceTo(target.CurrentGridPosition);
            if (finalDistance > meleeRange)
            {
                PresentMeleeResolution(intent, target);
                LogMeleeAvoided(
                    intent,
                    target,
                    $"{DescribeUnit(target)} moved to {target.CurrentGridPosition}, {finalDistance} cells from {DescribeUnit(intent.Actor)} at {intent.Actor.CurrentGridPosition}; melee range is {meleeRange}.");
                return TacticalResult.Success();
            }

            PresentMeleeResolution(intent, target);
            var source = CreateDamageSource(intent);
            var previousHP = target.CurrentHP;
            var damageResult = ApplyDamageAndPublishEvents(target, intent.Ability.Damage, source, intent);
            if (damageResult.IsFailure)
            {
                return damageResult;
            }

            LogMeleeHit(intent, target, previousHP, target.CurrentHP, meleeRange, finalDistance);
            return TacticalResult.Success();
        }

        protected virtual TacticalResult ResolveCone(ActionIntent intent)
        {
            if (intent.DeclaredAffectedCells == null || intent.DeclaredAffectedCells.Count == 0)
            {
                return TacticalResult.Failure($"Cannot resolve cone action '{intent.Ability.DisplayName}' because it has no declared affected cells.");
            }

            var affectedCells = new HashSet<GridPosition>(intent.DeclaredAffectedCells);
            var livingCombatants = GetLivingCombatants();
            var source = CreateDamageSource(intent);
            var hitUnits = new List<string>();
            var avoidedUnits = new List<string>();
            var ignoredFriendlyUnits = new List<string>();

            for (var i = 0; i < livingCombatants.Count; i += 1)
            {
                var unit = livingCombatants[i];
                if (unit == null || !unit.IsAlive)
                {
                    continue;
                }

                var finalPosition = unit.CurrentGridPosition;
                var isInsideCone = affectedCells.Contains(finalPosition);
                var isHostile = intent.Actor.Team.IsHostileTo(unit.Team);

                if (!isHostile && !ConeFriendlyFireEnabled)
                {
                    if (isInsideCone)
                    {
                        ignoredFriendlyUnits.Add($"{DescribeUnit(unit)} at {finalPosition}");
                    }

                    continue;
                }

                if (!isInsideCone)
                {
                    avoidedUnits.Add($"{DescribeUnit(unit)} at {finalPosition}");
                    continue;
                }

                var damageResult = ApplyDamageAndPublishEvents(unit, intent.Ability.Damage, source, intent);
                if (damageResult.IsFailure)
                {
                    return damageResult;
                }

                hitUnits.Add($"{DescribeUnit(unit)} at {finalPosition}");
            }

            LogConeOutcome(intent, affectedCells.Count, hitUnits, avoidedUnits, ignoredFriendlyUnits);
            return TacticalResult.Success();
        }

        protected virtual TacticalResult ResolveRadius(ActionIntent intent)
        {
            if (intent.DeclaredAffectedCells == null || intent.DeclaredAffectedCells.Count == 0)
            {
                return TacticalResult.Failure($"Cannot resolve AoE action '{intent.Ability.DisplayName}' because it has no declared affected cells.");
            }

            var affectedCells = new HashSet<GridPosition>(intent.DeclaredAffectedCells);
            var livingCombatants = GetLivingCombatants();
            var source = CreateDamageSource(intent);
            var affectedUnits = new List<string>();
            var avoidedUnits = new List<string>();

            for (var i = 0; i < livingCombatants.Count; i += 1)
            {
                var unit = livingCombatants[i];
                if (unit == null || !unit.IsAlive)
                {
                    continue;
                }

                var finalPosition = unit.CurrentGridPosition;
                if (!affectedCells.Contains(finalPosition))
                {
                    avoidedUnits.Add($"{DescribeUnit(unit)} at {finalPosition}");
                    continue;
                }

                var damageResult = ApplyDamageAndPublishEvents(unit, intent.Ability.Damage, source, intent);
                if (damageResult.IsFailure)
                {
                    return damageResult;
                }

                affectedUnits.Add($"{DescribeUnit(unit)} at {finalPosition}");
            }

            LogRadiusOutcome(intent, affectedCells.Count, affectedUnits, avoidedUnits);
            return TacticalResult.Success();
        }

        private IReadOnlyList<TacticalUnit> GetLivingCombatants()
        {
            var combatants = combatantProvider != null
                ? combatantProvider()
                : UnityEngine.Object.FindObjectsByType<TacticalUnit>(FindObjectsSortMode.None);

            return combatants == null
                ? Array.Empty<TacticalUnit>()
                : TurnOrderService.BuildTurnOrder(combatants);
        }

        private TacticalResult ApplyDamageAndPublishEvents(TacticalUnit target, int amount, DamageSource source, ActionIntent intent)
        {
            var finalAmount = amount;
            var previousHP = target.CurrentHP;
            var wasAlive = target.IsAlive;
            var damageResult = target.ApplyDamage(finalAmount, source);
            if (damageResult.IsFailure)
            {
                return damageResult;
            }

            eventBus?.PublishDamageApplied(intent, intent.Actor, target, amount, wasBraced: false, finalAmount: finalAmount);

            if (target.CurrentHP != previousHP)
            {
                eventBus?.PublishHitPointsChanged(target, previousHP, target.CurrentHP, source);
            }

            if (wasAlive && target.IsDead)
            {
                eventBus?.PublishUnitDied(target, source);
            }

            return TacticalResult.Success();
        }

        private static int GetMeleeRange(TacticalUnit actor)
        {
            return Math.Max(UnitStatsDefinition.MinimumMeleeRange, actor.MeleeRange);
        }

        private static DamageSource CreateDamageSource(ActionIntent intent)
        {
            return intent.Actor.UnitId.IsAssigned
                ? DamageSource.FromUnit(intent.Actor.UnitId, intent.Ability.DisplayName)
                : DamageSource.Environmental(intent.Ability.DisplayName);
        }

        private void LogMeleeHit(
            ActionIntent intent,
            TacticalUnit target,
            int previousHP,
            int currentHP,
            int meleeRange,
            int finalDistance)
        {
            if (!logResolutions)
            {
                return;
            }

            Debug.Log(
                $"[Combat Log] {DescribeUnit(intent.Actor)} resolved melee '{intent.Ability.DisplayName}' against {DescribeUnit(target)}: hit for {previousHP - currentHP} damage at final distance {finalDistance} within melee range {meleeRange}.",
                logContext);
        }

        private void LogMeleeAvoided(ActionIntent intent, TacticalUnit target, string reason)
        {
            if (!logResolutions)
            {
                return;
            }

            Debug.Log(
                $"[Combat Log] {DescribeUnit(intent.Actor)} resolved melee '{intent.Ability.DisplayName}' against {DescribeUnit(target)}: avoided by movement/position — {reason}",
                logContext);
        }

        private void LogConeOutcome(
            ActionIntent intent,
            int affectedCellCount,
            IReadOnlyList<string> hitUnits,
            IReadOnlyList<string> avoidedUnits,
            IReadOnlyList<string> ignoredFriendlyUnits)
        {
            if (!logResolutions)
            {
                return;
            }

            var hitText = hitUnits.Count > 0 ? string.Join(", ", hitUnits) : "none";
            var avoidedText = avoidedUnits.Count > 0 ? string.Join(", ", avoidedUnits) : "none";
            var ignoredFriendlyText = ignoredFriendlyUnits.Count > 0 ? string.Join(", ", ignoredFriendlyUnits) : "none";
            Debug.Log(
                $"[Combat Log] {DescribeUnit(intent.Actor)} resolved cone '{intent.Ability.DisplayName}' over {affectedCellCount} declared cells: hit hostiles: {hitText}; avoided hostiles: {avoidedText}; ignored friendlies (friendly fire disabled): {ignoredFriendlyText}. Damage used final grid positions with no dodge or accuracy roll.",
                logContext);
        }

        private void LogRadiusOutcome(
            ActionIntent intent,
            int affectedCellCount,
            IReadOnlyList<string> affectedUnits,
            IReadOnlyList<string> avoidedUnits)
        {
            if (!logResolutions)
            {
                return;
            }

            var affectedText = affectedUnits.Count > 0 ? string.Join(", ", affectedUnits) : "none";
            var avoidedText = avoidedUnits.Count > 0 ? string.Join(", ", avoidedUnits) : "none";
            Debug.Log(
                $"[Combat Log] {DescribeUnit(intent.Actor)} resolved AoE '{intent.Ability.DisplayName}' over {affectedCellCount} declared cells: affected: {affectedText}; avoided: {avoidedText}. Damage used final grid positions with no dodge or accuracy roll.",
                logContext);
        }

        private static void PresentMeleeResolution(ActionIntent intent, TacticalUnit target)
        {
            MeleeAttackPresentation.Play(intent.Actor, target);
        }

        private static TacticalResult ValidateIntent(ActionIntent intent)
        {
            if (intent == null)
            {
                return TacticalResult.Failure("Cannot resolve an action because no action intent was provided.");
            }

            if (intent.Actor == null)
            {
                return TacticalResult.Failure("Cannot resolve an action because the acting unit is missing.");
            }

            if (intent.Ability == null)
            {
                return TacticalResult.Failure("Cannot resolve an action because the ability definition is missing.");
            }

            return TacticalResult.Success();
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
