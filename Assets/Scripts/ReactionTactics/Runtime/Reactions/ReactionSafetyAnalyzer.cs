using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using ReactionTactics.Actions;
using ReactionTactics.Grid;
using ReactionTactics.Pathfinding;
using ReactionTactics.Units;

namespace ReactionTactics.Reactions
{
    /// <summary>
    /// Safety category for a reachable reaction destination relative to the pending action.
    /// </summary>
    public enum ReactionSafetyStatus
    {
        Safe = 0,
        Threatened = 1
    }

    /// <summary>
    /// UI-facing classification for one reachable reaction movement destination.
    /// The reason text is intentionally player-readable so hover tooltips can explain
    /// how physical movement avoids, or fails to avoid, the pending deterministic action.
    /// </summary>
    public readonly struct ReactionSafetyCell : IEquatable<ReactionSafetyCell>
    {
        private readonly string reason;

        public ReactionSafetyCell(ReachableCell reachableCell, ReactionSafetyStatus status, string reason)
        {
            if (!Enum.IsDefined(typeof(ReactionSafetyStatus), status))
            {
                throw new ArgumentOutOfRangeException(nameof(status), status, "Unknown reaction safety status.");
            }

            if (string.IsNullOrWhiteSpace(reason))
            {
                throw new ArgumentException("Reaction safety classifications require a reason for UI tooltips.", nameof(reason));
            }

            ReachableCell = reachableCell;
            Status = status;
            this.reason = reason.Trim();
        }

        public ReachableCell ReachableCell { get; }

        public GridPosition Position
        {
            get { return ReachableCell.Position; }
        }

        public int TotalApCost
        {
            get { return ReachableCell.TotalApCost; }
        }

        public ReactionSafetyStatus Status { get; }

        public bool IsSafe
        {
            get { return Status == ReactionSafetyStatus.Safe; }
        }

        public bool IsThreatened
        {
            get { return Status == ReactionSafetyStatus.Threatened; }
        }

        public string Reason
        {
            get { return reason ?? string.Empty; }
        }

        public bool Equals(ReactionSafetyCell other)
        {
            return ReachableCell.Equals(other.ReachableCell)
                && Status == other.Status
                && string.Equals(Reason, other.Reason, StringComparison.Ordinal);
        }

        public override bool Equals(object obj)
        {
            return obj is ReactionSafetyCell other && Equals(other);
        }

        public override int GetHashCode()
        {
            unchecked
            {
                var hash = 17;
                hash = (hash * 31) + ReachableCell.GetHashCode();
                hash = (hash * 31) + Status.GetHashCode();
                hash = (hash * 31) + StringComparer.Ordinal.GetHashCode(Reason);
                return hash;
            }
        }

        public override string ToString()
        {
            return $"{Position} cost={TotalApCost} {Status}: {Reason}";
        }

        public static bool operator ==(ReactionSafetyCell left, ReactionSafetyCell right)
        {
            return left.Equals(right);
        }

        public static bool operator !=(ReactionSafetyCell left, ReactionSafetyCell right)
        {
            return !left.Equals(right);
        }
    }

    /// <summary>
    /// Classifies reachable reaction movement destinations against the pending declared action.
    /// It mirrors the prototype resolution rules: melee checks final distance to the original
    /// melee target, while cone and radius attacks check whether the destination remains inside
    /// the declared affected cells. No random dodge or accuracy concept is involved.
    /// </summary>
    public sealed class ReactionSafetyAnalyzer
    {
        public IReadOnlyList<ReactionSafetyCell> ClassifyReachableCells(
            TacticalUnit reactor,
            ActionIntent pendingIntent,
            IEnumerable<ReachableCell> reachableCells)
        {
            if (reactor == null)
            {
                throw new ArgumentNullException(nameof(reactor));
            }

            if (pendingIntent == null)
            {
                throw new ArgumentNullException(nameof(pendingIntent));
            }

            if (reachableCells == null)
            {
                throw new ArgumentNullException(nameof(reachableCells));
            }

            var affectedCells = CreateAffectedCellSet(pendingIntent);
            var results = new List<ReactionSafetyCell>();
            foreach (var reachableCell in reachableCells)
            {
                results.Add(ClassifyReachableCell(reactor, pendingIntent, reachableCell, affectedCells));
            }

            return new ReadOnlyCollection<ReactionSafetyCell>(results);
        }

        public ReactionSafetyCell ClassifyReachableCell(
            TacticalUnit reactor,
            ActionIntent pendingIntent,
            ReachableCell reachableCell)
        {
            if (reactor == null)
            {
                throw new ArgumentNullException(nameof(reactor));
            }

            if (pendingIntent == null)
            {
                throw new ArgumentNullException(nameof(pendingIntent));
            }

            return ClassifyReachableCell(
                reactor,
                pendingIntent,
                reachableCell,
                CreateAffectedCellSet(pendingIntent));
        }

        private static ReactionSafetyCell ClassifyReachableCell(
            TacticalUnit reactor,
            ActionIntent pendingIntent,
            ReachableCell reachableCell,
            ISet<GridPosition> affectedCells)
        {
            switch (pendingIntent.Ability.Shape)
            {
                case AbilityShape.Melee:
                    return ClassifyMelee(reactor, pendingIntent, reachableCell);
                case AbilityShape.Cone:
                    return ClassifyArea(
                        reachableCell,
                        pendingIntent,
                        affectedCells,
                        threatenedText: "inside the declared cone",
                        safeText: "outside the declared cone");
                case AbilityShape.Radius:
                    return ClassifyArea(
                        reachableCell,
                        pendingIntent,
                        affectedCells,
                        threatenedText: "inside the declared AoE",
                        safeText: "outside the declared AoE");
                case AbilityShape.Self:
                    return Safe(
                        reachableCell,
                        $"{DescribeUnit(reactor)} is safe at {reachableCell.Position}: '{pendingIntent.Ability.DisplayName}' is a self ability and does not threaten reaction destinations.");
                case AbilityShape.SingleTarget:
                    return ClassifySingleTarget(reactor, pendingIntent, reachableCell);
                default:
                    return Threatened(
                        reachableCell,
                        $"{DescribeUnit(reactor)} should treat {reachableCell.Position} as threatened because ability shape '{pendingIntent.Ability.Shape}' cannot be classified safely.");
            }
        }

        private static ReactionSafetyCell ClassifyMelee(
            TacticalUnit reactor,
            ActionIntent pendingIntent,
            ReachableCell reachableCell)
        {
            var target = pendingIntent.DeclaredTargetUnit;
            if (target == null)
            {
                return Threatened(
                    reachableCell,
                    $"{DescribeUnit(reactor)} should treat {reachableCell.Position} as threatened because melee '{pendingIntent.Ability.DisplayName}' has no declared target to compare against.");
            }

            if (!ReferenceEquals(reactor, target))
            {
                return Safe(
                    reachableCell,
                    $"{DescribeUnit(reactor)} is safe at {reachableCell.Position}: they are not the declared target of melee '{pendingIntent.Ability.DisplayName}'.");
            }

            var actorPosition = pendingIntent.Actor.CurrentGridPosition;
            var meleeRange = Math.Max(UnitStatsDefinition.MinimumMeleeRange, pendingIntent.Actor.MeleeRange);
            var finalDistance = actorPosition.HorizontalDistanceTo(reachableCell.Position);
            if (finalDistance > meleeRange)
            {
                return Safe(
                    reachableCell,
                    $"{DescribeUnit(reactor)} is safe at {reachableCell.Position}: final distance {finalDistance} is outside melee range {meleeRange} from {DescribeUnit(pendingIntent.Actor)} at {actorPosition}.");
            }

            return Threatened(
                reachableCell,
                $"{DescribeUnit(reactor)} is threatened at {reachableCell.Position}: final distance {finalDistance} is within melee range {meleeRange} from {DescribeUnit(pendingIntent.Actor)} at {actorPosition}.");
        }

        private static ReactionSafetyCell ClassifyArea(
            ReachableCell reachableCell,
            ActionIntent pendingIntent,
            ISet<GridPosition> affectedCells,
            string threatenedText,
            string safeText)
        {
            if (affectedCells.Count == 0)
            {
                return Safe(
                    reachableCell,
                    $"{reachableCell.Position} is safe from '{pendingIntent.Ability.DisplayName}' because it has no declared affected cells.");
            }

            if (affectedCells.Contains(reachableCell.Position))
            {
                return Threatened(
                    reachableCell,
                    $"{reachableCell.Position} is threatened by '{pendingIntent.Ability.DisplayName}' because it is {threatenedText} affected cells.");
            }

            return Safe(
                reachableCell,
                $"{reachableCell.Position} is safe from '{pendingIntent.Ability.DisplayName}' because it is {safeText} affected cells.");
        }

        private static ReactionSafetyCell ClassifySingleTarget(
            TacticalUnit reactor,
            ActionIntent pendingIntent,
            ReachableCell reachableCell)
        {
            if (ReferenceEquals(reactor, pendingIntent.DeclaredTargetUnit))
            {
                return Threatened(
                    reachableCell,
                    $"{DescribeUnit(reactor)} remains threatened at {reachableCell.Position}: single-target '{pendingIntent.Ability.DisplayName}' follows the declared target rather than an avoidable area.");
            }

            return Safe(
                reachableCell,
                $"{DescribeUnit(reactor)} is safe at {reachableCell.Position}: they are not the declared target of single-target '{pendingIntent.Ability.DisplayName}'.");
        }

        private static ReactionSafetyCell Safe(ReachableCell reachableCell, string reason)
        {
            return new ReactionSafetyCell(reachableCell, ReactionSafetyStatus.Safe, reason);
        }

        private static ReactionSafetyCell Threatened(ReachableCell reachableCell, string reason)
        {
            return new ReactionSafetyCell(reachableCell, ReactionSafetyStatus.Threatened, reason);
        }

        private static ISet<GridPosition> CreateAffectedCellSet(ActionIntent pendingIntent)
        {
            return pendingIntent.DeclaredAffectedCells == null
                ? new HashSet<GridPosition>()
                : new HashSet<GridPosition>(pendingIntent.DeclaredAffectedCells);
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
