using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using ReactionTactics.Actions;
using ReactionTactics.Grid;
using ReactionTactics.Units;

namespace ReactionTactics.UI
{
    /// <summary>
    /// Immutable UI-facing preview for an ability before it is declared. Previews are
    /// intentionally side-effect free: they never spend AP, advance declaration sequence
    /// numbers, or create an <see cref="ActionIntent" />.
    /// </summary>
    public sealed class ActionIntentPreview
    {
        private readonly ReadOnlyCollection<GridPosition> affectedCells;
        private readonly ReadOnlyCollection<TacticalUnit> threatenedUnits;
        private readonly ReadOnlyCollection<GridPosition> safeCells;

        private ActionIntentPreview(
            TacticalUnit actor,
            AbilityDefinition ability,
            ActionTarget target,
            IEnumerable<GridPosition> affectedCells,
            IEnumerable<TacticalUnit> threatenedUnits,
            IEnumerable<GridPosition> safeCells,
            string invalidReason)
        {
            Actor = actor;
            Ability = ability;
            Target = target;
            this.affectedCells = CopyDistinctPositions(affectedCells);
            this.threatenedUnits = CopyDistinctUnits(threatenedUnits);
            this.safeCells = CopyDistinctPositions(safeCells);
            InvalidReason = NormalizeOptionalText(invalidReason);
        }

        public TacticalUnit Actor { get; }

        public AbilityDefinition Ability { get; }

        public ActionTarget Target { get; }

        public IReadOnlyList<GridPosition> AffectedCells
        {
            get { return affectedCells; }
        }

        public IReadOnlyList<TacticalUnit> ThreatenedUnits
        {
            get { return threatenedUnits; }
        }

        public IReadOnlyList<GridPosition> SafeCells
        {
            get { return safeCells; }
        }

        public string InvalidReason { get; }

        public bool IsValid
        {
            get { return string.IsNullOrEmpty(InvalidReason); }
        }

        public bool IsInvalid
        {
            get { return !IsValid; }
        }

        public bool HasAffectedCells
        {
            get { return affectedCells.Count > 0; }
        }

        public bool HasThreatenedUnits
        {
            get { return threatenedUnits.Count > 0; }
        }

        public bool HasSafeCells
        {
            get { return safeCells.Count > 0; }
        }

        public static ActionIntentPreview Valid(
            TacticalUnit actor,
            AbilityDefinition ability,
            ActionTarget target,
            IEnumerable<GridPosition> affectedCells = null,
            IEnumerable<TacticalUnit> threatenedUnits = null,
            IEnumerable<GridPosition> safeCells = null)
        {
            if (actor == null)
            {
                throw new ArgumentNullException(nameof(actor));
            }

            if (ability == null)
            {
                throw new ArgumentNullException(nameof(ability));
            }

            return new ActionIntentPreview(
                actor,
                ability,
                target,
                affectedCells,
                threatenedUnits,
                safeCells,
                invalidReason: null);
        }

        public static ActionIntentPreview Invalid(
            TacticalUnit actor,
            AbilityDefinition ability,
            ActionTarget target,
            string invalidReason)
        {
            var reason = string.IsNullOrWhiteSpace(invalidReason)
                ? "Action intent preview is invalid."
                : invalidReason.Trim();

            return new ActionIntentPreview(
                actor,
                ability,
                target,
                affectedCells: null,
                threatenedUnits: null,
                safeCells: null,
                invalidReason: reason);
        }

        public override string ToString()
        {
            var actorName = Actor != null ? Actor.DisplayName : "none";
            var abilityName = Ability != null ? Ability.DisplayName : "none";
            if (IsInvalid)
            {
                return $"Invalid action preview for {actorName} using {abilityName}: {InvalidReason}";
            }

            return $"Action preview for {actorName} using {abilityName}: {affectedCells.Count} affected cells, {threatenedUnits.Count} threatened units, {safeCells.Count} safe cells.";
        }

        private static ReadOnlyCollection<GridPosition> CopyDistinctPositions(IEnumerable<GridPosition> positions)
        {
            if (positions == null)
            {
                return new ReadOnlyCollection<GridPosition>(Array.Empty<GridPosition>());
            }

            var copy = new List<GridPosition>();
            var seen = new HashSet<GridPosition>();
            foreach (var position in positions)
            {
                if (seen.Add(position))
                {
                    copy.Add(position);
                }
            }

            return new ReadOnlyCollection<GridPosition>(copy);
        }

        private static ReadOnlyCollection<TacticalUnit> CopyDistinctUnits(IEnumerable<TacticalUnit> units)
        {
            if (units == null)
            {
                return new ReadOnlyCollection<TacticalUnit>(Array.Empty<TacticalUnit>());
            }

            var copy = new List<TacticalUnit>();
            foreach (var unit in units)
            {
                if (unit == null || ContainsReference(copy, unit))
                {
                    continue;
                }

                copy.Add(unit);
            }

            return new ReadOnlyCollection<TacticalUnit>(copy);
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

        private static string NormalizeOptionalText(string value)
        {
            return string.IsNullOrWhiteSpace(value) ? string.Empty : value.Trim();
        }
    }
}
