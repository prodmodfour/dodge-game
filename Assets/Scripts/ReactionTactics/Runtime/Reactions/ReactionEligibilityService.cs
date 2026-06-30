using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using ReactionTactics.Actions;
using ReactionTactics.Core;
using ReactionTactics.Turns;
using ReactionTactics.Units;

namespace ReactionTactics.Reactions
{
    /// <summary>
    /// Validates whether an off-turn unit can receive a reaction turn for a pending
    /// action. The service reports clear auto-pass reasons so future reaction-window
    /// flow can skip invalid reactors without stalling the window.
    /// </summary>
    public sealed class ReactionEligibilityService
    {
        public const string DefaultPassReactionAbilityKey = "pass_reaction";
        public const string DefaultPassReactionDisplayName = "Pass Reaction";

        private readonly List<IReactionEligibilityRule> statusEffectRules;
        private readonly ReadOnlyCollection<IReactionEligibilityRule> statusEffectRulesView;

        public ReactionEligibilityService()
            : this(null)
        {
        }

        public ReactionEligibilityService(IEnumerable<IReactionEligibilityRule> statusEffectRules)
        {
            this.statusEffectRules = new List<IReactionEligibilityRule>();
            statusEffectRulesView = new ReadOnlyCollection<IReactionEligibilityRule>(this.statusEffectRules);

            if (statusEffectRules == null)
            {
                return;
            }

            foreach (var rule in statusEffectRules)
            {
                AddStatusEffectRule(rule);
            }
        }

        public IReadOnlyList<IReactionEligibilityRule> StatusEffectRules
        {
            get { return statusEffectRulesView; }
        }

        /// <summary>
        /// Adds a future status-effect rule that can veto reaction eligibility.
        /// Null rules are rejected so reaction validation remains deterministic.
        /// </summary>
        public void AddStatusEffectRule(IReactionEligibilityRule rule)
        {
            if (rule == null)
            {
                throw new ArgumentNullException(nameof(rule));
            }

            statusEffectRules.Add(rule);
        }

        public bool HasZeroCostPassReaction(TacticalUnit unit)
        {
            return TryGetZeroCostPassReaction(unit, out _);
        }

        public bool TryGetZeroCostPassReaction(TacticalUnit unit, out AbilityDefinition passAbility)
        {
            passAbility = null;
            if (unit == null)
            {
                return false;
            }

            var loadout = unit.GetComponent<UnitAbilityLoadout>();
            if (loadout == null)
            {
                return false;
            }

            var reactionAbilities = loadout.GetReactionAbilities();
            for (var i = 0; i < reactionAbilities.Count; i += 1)
            {
                var ability = reactionAbilities[i];
                if (IsZeroCostPassReaction(ability))
                {
                    passAbility = ability;
                    return true;
                }
            }

            return false;
        }

        /// <summary>
        /// Returns true for the prototype pass reaction asset: a zero-cost self
        /// reaction identified by its stable key or display name.
        /// </summary>
        public static bool IsZeroCostPassReaction(AbilityDefinition ability)
        {
            if (ability == null
                || !ability.CanBeUsedAsReaction
                || ability.APCost != 0
                || ability.Shape != AbilityShape.Self
                || ability.Damage != 0
                || ability.TriggersReactions)
            {
                return false;
            }

            return string.Equals(ability.AbilityKey, DefaultPassReactionAbilityKey, StringComparison.OrdinalIgnoreCase)
                || string.Equals(ability.DisplayName, DefaultPassReactionDisplayName, StringComparison.OrdinalIgnoreCase);
        }

        /// <summary>
        /// Determines whether <paramref name="unit" /> can take a reaction turn for
        /// <paramref name="intent" /> under the current combat phase and AP state.
        /// </summary>
        public ReactionEligibilityResult CanUnitReact(
            TacticalUnit unit,
            ActionIntent intent,
            CombatState combatState)
        {
            if (unit == null)
            {
                return ReactionEligibilityResult.Ineligible(
                    "Cannot start a reaction turn because no tactical unit was provided.");
            }

            if (intent == null)
            {
                return ReactionEligibilityResult.Ineligible(
                    $"{DescribeUnit(unit)} cannot react because the source action intent is missing.");
            }

            if (combatState == null)
            {
                return ReactionEligibilityResult.Ineligible(
                    $"{DescribeUnit(unit)} cannot react because combat state is missing.");
            }

            if (combatState.Phase != CombatPhase.ReactionWindow)
            {
                return ReactionEligibilityResult.Ineligible(
                    $"{DescribeUnit(unit)} cannot react while combat phase is {combatState.Phase}. "
                    + $"Reactions are only legal during {CombatPhase.ReactionWindow}.");
            }

            if (ReferenceEquals(unit, intent.Actor))
            {
                return ReactionEligibilityResult.Ineligible(
                    $"{DescribeUnit(unit)} cannot react to its own declared action.");
            }

            if (combatState.ActiveUnit != null && ReferenceEquals(unit, combatState.ActiveUnit))
            {
                return ReactionEligibilityResult.Ineligible(
                    $"{DescribeUnit(unit)} cannot react because it is the current active action unit.");
            }

            if (!unit.IsAlive)
            {
                return ReactionEligibilityResult.Ineligible(
                    $"{DescribeUnit(unit)} cannot react because it is defeated.");
            }

            var statusResult = ValidateStatusEffectRules(unit, intent, combatState);
            if (statusResult.IsFailure)
            {
                return ReactionEligibilityResult.Ineligible(
                    $"{DescribeUnit(unit)} cannot react: {statusResult.ErrorMessage}");
            }

            if (unit.CurrentAP > 0)
            {
                return ReactionEligibilityResult.Eligible(
                    $"{DescribeUnit(unit)} can react with {unit.CurrentAP} AP available.");
            }

            if (TryGetZeroCostPassReaction(unit, out var passAbility))
            {
                return ReactionEligibilityResult.AutoPass(
                    $"{DescribeUnit(unit)} has 0 AP and can only use zero-cost {passAbility.DisplayName}; auto-pass this reaction turn.");
            }

            return ReactionEligibilityResult.Ineligible(
                $"{DescribeUnit(unit)} has 0 AP and no zero-cost pass reaction; auto-pass this reaction turn.");
        }

        public TacticalResult ValidateUnitCanReact(
            TacticalUnit unit,
            ActionIntent intent,
            CombatState combatState)
        {
            return CanUnitReact(unit, intent, combatState).ToTacticalResult();
        }

        private TacticalResult ValidateStatusEffectRules(
            TacticalUnit unit,
            ActionIntent intent,
            CombatState combatState)
        {
            for (var i = 0; i < statusEffectRules.Count; i += 1)
            {
                var result = statusEffectRules[i].CanUnitReact(unit, intent, combatState);
                if (result.IsFailure)
                {
                    return result;
                }
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
