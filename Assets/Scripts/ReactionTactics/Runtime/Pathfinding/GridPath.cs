using System;
using System.Collections.Generic;
using System.Linq;
using ReactionTactics.Core;
using ReactionTactics.Grid;

namespace ReactionTactics.Pathfinding
{
    /// <summary>
    /// Represents either a valid ordered grid path with cumulative AP cost or a
    /// failed path query with a player-facing failure reason.
    /// </summary>
    public sealed class GridPath
    {
        private readonly PathStep[] steps;
        private readonly GridPosition[] positions;
        private readonly IReadOnlyList<PathStep> readOnlySteps;
        private readonly IReadOnlyList<GridPosition> readOnlyPositions;

        private GridPath(bool isValid, PathStep[] steps, string failureReason)
        {
            IsValid = isValid;
            this.steps = steps ?? Array.Empty<PathStep>();
            positions = this.steps.Select(step => step.Position).ToArray();
            readOnlySteps = Array.AsReadOnly(this.steps);
            readOnlyPositions = Array.AsReadOnly(positions);
            FailureReason = failureReason ?? string.Empty;
            TotalApCost = this.steps.Length == 0 ? 0 : this.steps[this.steps.Length - 1].TotalApCost;
        }

        public bool IsValid { get; }

        public bool IsInvalid
        {
            get { return !IsValid; }
        }

        public IReadOnlyList<PathStep> Steps
        {
            get { return readOnlySteps; }
        }

        public IReadOnlyList<GridPosition> Positions
        {
            get { return readOnlyPositions; }
        }

        /// <summary>
        /// Total AP required to traverse the path. Invalid paths report zero.
        /// </summary>
        public int TotalApCost { get; }

        /// <summary>
        /// Player-facing reason for an invalid path. Valid paths use an empty string.
        /// </summary>
        public string FailureReason { get; }

        public GridPosition Start
        {
            get
            {
                EnsureValidPathHasSteps();
                return steps[0].Position;
            }
        }

        public GridPosition Destination
        {
            get
            {
                EnsureValidPathHasSteps();
                return steps[steps.Length - 1].Position;
            }
        }

        public static GridPath Success(IEnumerable<PathStep> steps)
        {
            if (steps == null)
            {
                throw new ArgumentNullException(nameof(steps));
            }

            var stepArray = steps.ToArray();
            ValidateSuccessfulPathSteps(stepArray);
            return new GridPath(true, stepArray, string.Empty);
        }

        public static GridPath Failure(string failureReason)
        {
            return new GridPath(
                false,
                Array.Empty<PathStep>(),
                TacticalResult.Failure(failureReason).ErrorMessage);
        }

        public override string ToString()
        {
            if (IsInvalid)
            {
                return $"Invalid path: {FailureReason}";
            }

            return $"Path totalAP={TotalApCost}: {string.Join(" -> ", positions.Select(position => position.ToString()))}";
        }

        private static void ValidateSuccessfulPathSteps(IReadOnlyList<PathStep> steps)
        {
            if (steps.Count == 0)
            {
                throw new ArgumentException("A successful grid path must include at least the starting step.", nameof(steps));
            }

            if (steps[0].StepApCost != 0 || steps[0].TotalApCost != 0)
            {
                throw new ArgumentException("The first path step must represent the starting cell with zero AP cost.", nameof(steps));
            }

            var previousTotal = 0;
            for (var i = 1; i < steps.Count; i++)
            {
                var expectedTotal = previousTotal + steps[i].StepApCost;
                if (steps[i].TotalApCost != expectedTotal)
                {
                    throw new ArgumentException(
                        $"Path step {i} has total AP cost {steps[i].TotalApCost}, but expected {expectedTotal}.",
                        nameof(steps));
                }

                previousTotal = steps[i].TotalApCost;
            }
        }

        private void EnsureValidPathHasSteps()
        {
            if (IsInvalid || steps.Length == 0)
            {
                throw new InvalidOperationException("Cannot read endpoints from an invalid grid path.");
            }
        }
    }
}
