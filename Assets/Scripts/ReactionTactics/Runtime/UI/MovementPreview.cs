using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using ReactionTactics.Grid;
using ReactionTactics.Pathfinding;

namespace ReactionTactics.UI
{
    /// <summary>
    /// Pure runtime/UI bridge model for movement highlighting. It stores the
    /// reachable-cell search result, exposes AP costs by cell, and reconstructs
    /// a selected path when UI hover changes without requiring highlight code to
    /// duplicate pathfinding rules.
    /// </summary>
    public sealed class MovementPreview
    {
        private readonly IReadOnlyDictionary<GridPosition, ReachableCell> reachableCells;
        private readonly IReadOnlyDictionary<GridPosition, int> apCosts;

        public MovementPreview(
            GridPosition startPosition,
            int apBudget,
            IReadOnlyDictionary<GridPosition, ReachableCell> reachableCells)
            : this(
                startPosition,
                apBudget,
                reachableCells,
                null,
                GridPath.Failure("No hovered destination selected."))
        {
        }

        private MovementPreview(
            GridPosition startPosition,
            int apBudget,
            IReadOnlyDictionary<GridPosition, ReachableCell> reachableCells,
            GridPosition? hoveredDestination,
            GridPath selectedPath)
        {
            if (apBudget < 0)
            {
                throw new ArgumentOutOfRangeException(
                    nameof(apBudget),
                    apBudget,
                    "Movement preview AP budget cannot be negative.");
            }

            if (reachableCells == null)
            {
                throw new ArgumentNullException(nameof(reachableCells));
            }

            var reachableCopy = CopyAndValidateReachableCells(
                startPosition,
                apBudget,
                reachableCells,
                out var apCostCopy);

            StartPosition = startPosition;
            ApBudget = apBudget;
            this.reachableCells = new ReadOnlyDictionary<GridPosition, ReachableCell>(reachableCopy);
            apCosts = new ReadOnlyDictionary<GridPosition, int>(apCostCopy);
            HoveredDestination = hoveredDestination;
            SelectedPath = selectedPath ?? GridPath.Failure("No hovered destination selected.");
        }

        public GridPosition StartPosition { get; }

        public int ApBudget { get; }

        public IReadOnlyDictionary<GridPosition, ReachableCell> ReachableCells
        {
            get { return reachableCells; }
        }

        public IReadOnlyDictionary<GridPosition, int> ApCosts
        {
            get { return apCosts; }
        }

        public GridPosition? HoveredDestination { get; }

        public GridPath SelectedPath { get; }

        public bool HasHoveredDestination
        {
            get { return HoveredDestination.HasValue; }
        }

        public bool HasSelectedPath
        {
            get { return SelectedPath.IsValid; }
        }

        /// <summary>
        /// Calculates a complete preview from a grid map using the default movement
        /// pathfinding services and no occupancy blockers.
        /// </summary>
        public static MovementPreview Create(IGridMap map, GridPosition startPosition, int apBudget)
        {
            return Create(map, startPosition, apBudget, NullGridOccupancy.Instance);
        }

        /// <summary>
        /// Calculates a complete preview from a grid map using the default movement
        /// pathfinding services and the supplied occupancy query.
        /// </summary>
        public static MovementPreview Create(
            IGridMap map,
            GridPosition startPosition,
            int apBudget,
            IGridOccupancy occupancy,
            bool ignoreUnits = false)
        {
            if (map == null)
            {
                throw new ArgumentNullException(nameof(map));
            }

            var search = new ReachableCellSearch();
            var reachable = search.FindReachableCells(
                map,
                startPosition,
                apBudget,
                occupancy ?? NullGridOccupancy.Instance,
                ignoreUnits);

            return new MovementPreview(startPosition, apBudget, reachable);
        }

        public bool IsReachable(GridPosition destination)
        {
            return reachableCells.ContainsKey(destination);
        }

        public bool TryGetApCost(GridPosition destination, out int apCost)
        {
            return apCosts.TryGetValue(destination, out apCost);
        }

        /// <summary>
        /// Returns a new preview with <see cref="SelectedPath"/> rebuilt for the
        /// currently hovered destination. Unreachable destinations produce an
        /// invalid path with a player-facing failure reason.
        /// </summary>
        public MovementPreview RecomputeSelectedPath(GridPosition hoveredDestination)
        {
            return new MovementPreview(
                StartPosition,
                ApBudget,
                reachableCells,
                hoveredDestination,
                BuildPathTo(hoveredDestination));
        }

        public MovementPreview ClearSelectedPath()
        {
            return new MovementPreview(StartPosition, ApBudget, reachableCells);
        }

        public override string ToString()
        {
            if (!HasHoveredDestination)
            {
                return $"Movement preview from {StartPosition} with {ReachableCells.Count} reachable cells and {ApBudget} AP.";
            }

            return $"Movement preview from {StartPosition} hovering {HoveredDestination.Value}: {SelectedPath}";
        }

        private GridPath BuildPathTo(GridPosition destination)
        {
            if (!reachableCells.ContainsKey(destination))
            {
                return GridPath.Failure(
                    $"Destination {destination} is not reachable from {StartPosition} within {ApBudget} AP.");
            }

            return ReconstructPath(destination);
        }

        private GridPath ReconstructPath(GridPosition destination)
        {
            var reversedCells = new List<ReachableCell>();
            var visited = new HashSet<GridPosition>();
            var current = destination;

            while (true)
            {
                if (!visited.Add(current))
                {
                    return GridPath.Failure($"Movement preview path reconstruction detected a cycle at {current}.");
                }

                if (!reachableCells.TryGetValue(current, out var reachableCell))
                {
                    return GridPath.Failure(
                        $"Cannot reconstruct movement preview path from {StartPosition} to {destination}: missing reachable cell {current}.");
                }

                reversedCells.Add(reachableCell);

                if (current == StartPosition)
                {
                    break;
                }

                if (!reachableCell.PreviousPosition.HasValue)
                {
                    return GridPath.Failure(
                        $"Cannot reconstruct movement preview path from {StartPosition} to {destination}: {current} has no previous cell.");
                }

                current = reachableCell.PreviousPosition.Value;
            }

            reversedCells.Reverse();

            var steps = new List<PathStep>(reversedCells.Count);
            var previousTotalApCost = 0;
            for (var i = 0; i < reversedCells.Count; i++)
            {
                var cell = reversedCells[i];
                var stepApCost = i == 0 ? 0 : cell.TotalApCost - previousTotalApCost;
                if (stepApCost < 0)
                {
                    return GridPath.Failure(
                        $"Cannot reconstruct movement preview path from {StartPosition} to {destination}: AP totals decrease at {cell.Position}.");
                }

                steps.Add(new PathStep(cell.Position, stepApCost, cell.TotalApCost));
                previousTotalApCost = cell.TotalApCost;
            }

            return GridPath.Success(steps);
        }

        private static Dictionary<GridPosition, ReachableCell> CopyAndValidateReachableCells(
            GridPosition startPosition,
            int apBudget,
            IReadOnlyDictionary<GridPosition, ReachableCell> reachableCells,
            out Dictionary<GridPosition, int> apCostCopy)
        {
            if (!reachableCells.TryGetValue(startPosition, out var startCell))
            {
                throw new ArgumentException(
                    $"Movement preview reachable cells must include the start position {startPosition}.",
                    nameof(reachableCells));
            }

            if (startCell.TotalApCost != 0 || startCell.PreviousPosition.HasValue)
            {
                throw new ArgumentException(
                    $"Movement preview start cell {startPosition} must have zero AP cost and no previous cell.",
                    nameof(reachableCells));
            }

            var reachableCopy = new Dictionary<GridPosition, ReachableCell>();
            apCostCopy = new Dictionary<GridPosition, int>();

            foreach (var pair in reachableCells)
            {
                if (pair.Key != pair.Value.Position)
                {
                    throw new ArgumentException(
                        $"Movement preview reachable cell key {pair.Key} must match cell position {pair.Value.Position}.",
                        nameof(reachableCells));
                }

                if (pair.Value.TotalApCost > apBudget)
                {
                    throw new ArgumentException(
                        $"Movement preview reachable cell {pair.Key} costs {pair.Value.TotalApCost} AP, exceeding budget {apBudget}.",
                        nameof(reachableCells));
                }

                reachableCopy.Add(pair.Key, pair.Value);
                apCostCopy.Add(pair.Key, pair.Value.TotalApCost);
            }

            return reachableCopy;
        }
    }
}
