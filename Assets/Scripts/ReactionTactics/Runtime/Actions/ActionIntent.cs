using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using ReactionTactics.Grid;
using ReactionTactics.Units;

namespace ReactionTactics.Actions
{
    /// <summary>
    /// Immutable record of an ability declared by a unit before the ability resolves.
    /// Reaction windows and action resolvers use the captured declaration data so later
    /// movement can change final positions without rewriting the original intent.
    /// </summary>
    public sealed class ActionIntent
    {
        private readonly ReadOnlyCollection<GridPosition> declaredAffectedCells;

        public ActionIntent(
            TacticalUnit actor,
            AbilityDefinition ability,
            GridPosition originPosition,
            ActionTarget target,
            IEnumerable<GridPosition> declaredAffectedCells,
            int declarationRound,
            int declarationSequence)
        {
            if (actor == null)
            {
                throw new ArgumentNullException(nameof(actor));
            }

            if (ability == null)
            {
                throw new ArgumentNullException(nameof(ability));
            }

            if (declarationRound < 0)
            {
                throw new ArgumentOutOfRangeException(
                    nameof(declarationRound),
                    declarationRound,
                    "Declaration round cannot be negative.");
            }

            if (declarationSequence < 0)
            {
                throw new ArgumentOutOfRangeException(
                    nameof(declarationSequence),
                    declarationSequence,
                    "Declaration sequence cannot be negative.");
            }

            Actor = actor;
            Ability = ability;
            OriginPosition = originPosition;
            ActorPositionAtDeclaration = originPosition;
            Target = target;
            DeclaredTargetUnit = target.TargetUnit;
            DeclarationRound = declarationRound;
            DeclarationSequence = declarationSequence;
            this.declaredAffectedCells = CopyAffectedCells(declaredAffectedCells);
        }

        public TacticalUnit Actor { get; }

        public AbilityDefinition Ability { get; }

        /// <summary>
        /// Grid cell used as the declared origin of the action shape.
        /// </summary>
        public GridPosition OriginPosition { get; }

        /// <summary>
        /// Actor position captured at declaration time for reaction ordering and previews.
        /// </summary>
        public GridPosition ActorPositionAtDeclaration { get; }

        public ActionTarget Target { get; }

        public IReadOnlyList<GridPosition> DeclaredAffectedCells
        {
            get { return declaredAffectedCells; }
        }

        public TacticalUnit DeclaredTargetUnit { get; }

        public bool HasDeclaredTargetUnit
        {
            get { return DeclaredTargetUnit != null; }
        }

        public int DeclarationRound { get; }

        public int DeclarationSequence { get; }

        public bool TriggersReactionWindow
        {
            get { return Ability.TriggersReactions; }
        }

        public bool IsTelegraphed
        {
            get { return Ability.IsTelegraphed; }
        }

        public override string ToString()
        {
            var actorName = Actor != null ? Actor.DisplayName : "none";
            var abilityName = Ability != null ? Ability.DisplayName : "none";
            var targetName = DeclaredTargetUnit != null ? DeclaredTargetUnit.DisplayName : "none";
            return $"ActionIntent(round={DeclarationRound}, sequence={DeclarationSequence}, actor={actorName}, ability={abilityName}, origin={OriginPosition}, target={targetName}, cells={declaredAffectedCells.Count})";
        }

        private static ReadOnlyCollection<GridPosition> CopyAffectedCells(IEnumerable<GridPosition> affectedCells)
        {
            return new ReadOnlyCollection<GridPosition>(
                affectedCells == null ? Array.Empty<GridPosition>() : affectedCells.ToArray());
        }
    }
}
