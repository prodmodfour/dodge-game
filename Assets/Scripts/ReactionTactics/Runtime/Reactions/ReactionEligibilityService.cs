using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using ReactionTactics.Actions;
using ReactionTactics.Core;
using ReactionTactics.Grid;
using ReactionTactics.Pathfinding;
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

        /// <summary>
        /// Determines whether <paramref name="unit" /> should stop the reaction window
        /// for player or AI input. A reactor with no legal meaningful command besides
        /// zero-cost pass is auto-passed so the window does not stall on units that
        /// cannot move or brace.
        /// </summary>
        public ReactionEligibilityResult CanUnitReact(
            TacticalUnit unit,
            ActionIntent intent,
            CombatState combatState,
            ReactionWindow reactionWindow,
            IGridMap map,
            IGridOccupancy occupancy)
        {
            var baseEligibility = CanUnitReact(unit, intent, combatState);
            if (!baseEligibility.IsEligible || baseEligibility.ShouldAutoPass)
            {
                return baseEligibility;
            }

            var hasMove = HasLegalReactionMove(unit, map, occupancy, out var moveReason);
            var hasBrace = HasLegalBrace(unit, intent, combatState, reactionWindow, out var braceReason);
            if (hasMove || hasBrace)
            {
                return ReactionEligibilityResult.Eligible(
                    $"{DescribeUnit(unit)} can react with {unit.CurrentAP} AP available. Legal reactions: {DescribeLegalCommands(hasMove, hasBrace)}.");
            }

            return ReactionEligibilityResult.AutoPass(
                $"{DescribeUnit(unit)} has {unit.CurrentAP} AP but no legal reaction commands other than Pass. "
                + $"Reaction Move unavailable: {moveReason} Brace unavailable: {braceReason}");
        }

        public TacticalResult ValidateUnitCanReact(
            TacticalUnit unit,
            ActionIntent intent,
            CombatState combatState)
        {
            return CanUnitReact(unit, intent, combatState).ToTacticalResult();
        }

        private static bool HasLegalReactionMove(
            TacticalUnit unit,
            IGridMap map,
            IGridOccupancy occupancy,
            out string reason)
        {
            if (unit == null)
            {
                reason = "no tactical unit was provided.";
                return false;
            }

            if (unit.CurrentAP <= 0)
            {
                reason = $"{DescribeUnit(unit)} has 0 AP.";
                return false;
            }

            if (map == null)
            {
                reason = "no grid map is available.";
                return false;
            }

            try
            {
                var start = unit.CurrentGridPosition;
                var reachableCells = new ReachableCellSearch().FindReachableCells(
                    map,
                    start,
                    unit.CurrentAP,
                    occupancy);

                foreach (var pair in reachableCells)
                {
                    if (pair.Key != start)
                    {
                        reason = $"{DescribeUnit(unit)} can reaction move to {pair.Key} for {pair.Value.TotalApCost} AP.";
                        return true;
                    }
                }

                reason = $"{DescribeUnit(unit)} has no reachable destination beyond its current cell {start}.";
                return false;
            }
            catch (ArgumentException exception)
            {
                reason = exception.Message;
                return false;
            }
            catch (InvalidOperationException exception)
            {
                reason = exception.Message;
                return false;
            }
        }

        private static bool HasLegalBrace(
            TacticalUnit unit,
            ActionIntent intent,
            CombatState combatState,
            ReactionWindow reactionWindow,
            out string reason)
        {
            var braceResult = BraceReactionCommand.TryCreate(
                unit,
                intent,
                combatState,
                reactionWindow);
            if (braceResult.IsSuccess)
            {
                reason = $"{DescribeUnit(unit)} can Brace for {braceResult.Value.Cost} AP.";
                return true;
            }

            reason = braceResult.ErrorMessage;
            return false;
        }

        private static string DescribeLegalCommands(bool hasMove, bool hasBrace)
        {
            var commands = new List<string>();
            if (hasMove)
            {
                commands.Add("Reaction Move");
            }

            if (hasBrace)
            {
                commands.Add("Brace");
            }

            commands.Add("Pass");
            return string.Join(", ", commands);
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
