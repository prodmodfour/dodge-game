using System;
using System.Collections.Generic;
using ReactionTactics.Actions;
using ReactionTactics.Grid;
using ReactionTactics.Units;

namespace ReactionTactics.Reactions
{
    /// <summary>
    /// Calculates the deterministic order in which living off-turn units receive
    /// reaction turns after an action is declared. Ordering is based on the acting
    /// unit's declaration position, not its later position, so reaction windows stay
    /// stable while movement and resolution happen.
    /// </summary>
    public sealed class ReactionOrderService
    {
        public const int DefaultVerticalWeight = 1;

        private readonly int verticalWeight;

        public ReactionOrderService()
            : this(DefaultVerticalWeight)
        {
        }

        public ReactionOrderService(int verticalWeight)
        {
            if (verticalWeight < 0)
            {
                throw new ArgumentOutOfRangeException(
                    nameof(verticalWeight),
                    verticalWeight,
                    "Reaction order vertical distance weight cannot be negative.");
            }

            this.verticalWeight = verticalWeight;
        }

        public int VerticalWeight
        {
            get { return verticalWeight; }
        }

        /// <summary>
        /// Returns every living unit except the source action actor, sorted by tactical
        /// distance from the actor's captured declaration cell and then by stable unit ID.
        /// </summary>
        public IReadOnlyList<TacticalUnit> BuildReactionOrder(
            TacticalUnit actionActor,
            IEnumerable<TacticalUnit> allUnits,
            ActionIntent sourceIntent)
        {
            if (actionActor == null)
            {
                throw new ArgumentNullException(nameof(actionActor));
            }

            if (sourceIntent == null)
            {
                throw new ArgumentNullException(nameof(sourceIntent));
            }

            if (allUnits == null)
            {
                throw new ArgumentNullException(nameof(allUnits));
            }

            if (!ReferenceEquals(actionActor, sourceIntent.Actor))
            {
                throw new ArgumentException(
                    "Reaction order action actor must match the source intent actor.",
                    nameof(actionActor));
            }

            var reactors = new List<TacticalUnit>();
            foreach (var unit in allUnits)
            {
                if (IsOrderedReactor(unit, actionActor))
                {
                    reactors.Add(unit);
                }
            }

            var declarationPosition = sourceIntent.ActorPositionAtDeclaration;
            reactors.Sort((left, right) => CompareReactors(left, right, declarationPosition));
            return reactors.AsReadOnly();
        }

        public IReadOnlyList<TacticalUnit> BuildReactionOrder(
            ActionIntent sourceIntent,
            IEnumerable<TacticalUnit> allUnits)
        {
            if (sourceIntent == null)
            {
                throw new ArgumentNullException(nameof(sourceIntent));
            }

            return BuildReactionOrder(sourceIntent.Actor, allUnits, sourceIntent);
        }

        public int GetReactionDistance(TacticalUnit reactor, ActionIntent sourceIntent)
        {
            if (reactor == null)
            {
                throw new ArgumentNullException(nameof(reactor));
            }

            if (sourceIntent == null)
            {
                throw new ArgumentNullException(nameof(sourceIntent));
            }

            return sourceIntent.ActorPositionAtDeclaration.TacticalDistanceTo(
                reactor.CurrentGridPosition,
                verticalWeight);
        }

        private int CompareReactors(TacticalUnit left, TacticalUnit right, GridPosition declarationPosition)
        {
            if (ReferenceEquals(left, right))
            {
                return 0;
            }

            if (left == null)
            {
                return 1;
            }

            if (right == null)
            {
                return -1;
            }

            var distanceComparison = declarationPosition
                .TacticalDistanceTo(left.CurrentGridPosition, verticalWeight)
                .CompareTo(declarationPosition.TacticalDistanceTo(right.CurrentGridPosition, verticalWeight));
            if (distanceComparison != 0)
            {
                return distanceComparison;
            }

            var idComparison = left.UnitId.CompareTo(right.UnitId);
            if (idComparison != 0)
            {
                return idComparison;
            }

            var teamComparison = ((int)left.Team).CompareTo((int)right.Team);
            if (teamComparison != 0)
            {
                return teamComparison;
            }

            var nameComparison = string.Compare(left.name, right.name, StringComparison.Ordinal);
            if (nameComparison != 0)
            {
                return nameComparison;
            }

            return left.GetInstanceID().CompareTo(right.GetInstanceID());
        }

        private static bool IsOrderedReactor(TacticalUnit unit, TacticalUnit actionActor)
        {
            return unit != null && unit.IsAlive && !ReferenceEquals(unit, actionActor);
        }
    }
}
