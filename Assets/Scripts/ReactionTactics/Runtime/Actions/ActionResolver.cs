using ReactionTactics.Core;
using ReactionTactics.Turns;
using UnityEngine;

namespace ReactionTactics.Actions
{
    /// <summary>
    /// Resolves declared action intents after any reaction window has completed.
    /// The current shell only records that resolution happened; shape-specific
    /// damage and positional checks are added through the protected hooks below.
    /// </summary>
    public class ActionResolver
    {
        private readonly CombatEventBus eventBus;
        private readonly Object logContext;
        private readonly bool logResolutions;

        public ActionResolver(CombatEventBus eventBus = null, Object logContext = null, bool logResolutions = true)
        {
            this.eventBus = eventBus;
            this.logContext = logContext;
            this.logResolutions = logResolutions;
        }

        /// <summary>
        /// Resolves a declared action intent and notifies listeners that the intent is complete.
        /// Damage, shape re-checks, and avoidance outcomes are intentionally deferred to later tickets.
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
                    $"Resolved action '{intent.Ability.DisplayName}' by {DescribeActor(intent)} using {intent.Ability.Shape} shell resolver.",
                    logContext);
            }

            eventBus?.PublishActionResolved(intent.Actor, intent);
            return TacticalResult.Success();
        }

        /// <summary>
        /// Dispatches to shape-specific resolution hooks. Later tickets replace these no-op hooks
        /// with final-position melee, cone, and radius resolution rules.
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
            return TacticalResult.Success();
        }

        protected virtual TacticalResult ResolveCone(ActionIntent intent)
        {
            return TacticalResult.Success();
        }

        protected virtual TacticalResult ResolveRadius(ActionIntent intent)
        {
            return TacticalResult.Success();
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

        private static string DescribeActor(ActionIntent intent)
        {
            var actor = intent.Actor;
            return actor.IsInitialized ? $"{actor.DisplayName} {actor.UnitId}" : actor.DisplayName;
        }
    }
}
