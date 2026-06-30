using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using ReactionTactics.Grid;

namespace ReactionTactics.Pathfinding
{
    /// <summary>
    /// Calculates all legal movement destinations within an AP budget using Dijkstra search.
    /// Terrain movement rules come from the neighbor and movement-cost services; callers may
    /// also provide an <see cref="IGridOccupancy"/> query to reject cells that cannot currently be entered.
    /// </summary>
    public sealed class ReachableCellSearch
    {
        private readonly GridNeighborService defaultNeighborService;
        private readonly MovementCostService defaultMovementCostService;

        public ReachableCellSearch()
            : this(new GridNeighborService())
        {
        }

        public ReachableCellSearch(GridNeighborService neighborService)
            : this(neighborService, null)
        {
        }

        public ReachableCellSearch(GridNeighborService neighborService, MovementCostService movementCostService)
        {
            defaultNeighborService = neighborService ?? throw new ArgumentNullException(nameof(neighborService));
            defaultMovementCostService = movementCostService ?? new MovementCostService(defaultNeighborService);
        }

        /// <summary>
        /// Finds every cell that can be reached from <paramref name="start"/> without exceeding
        /// <paramref name="apBudget"/>. The returned dictionary always includes the start cell
        /// with total AP cost 0.
        /// </summary>
        public IReadOnlyDictionary<GridPosition, ReachableCell> FindReachableCells(
            IGridMap map,
            GridPosition start,
            int apBudget)
        {
            return FindReachableCells(
                map,
                start,
                apBudget,
                defaultNeighborService,
                defaultMovementCostService,
                NullGridOccupancy.Instance);
        }

        /// <summary>
        /// Finds every cell that can be reached from <paramref name="start"/> without exceeding
        /// <paramref name="apBudget"/>, using <paramref name="occupancy"/> to reject occupied cells.
        /// The returned dictionary always includes the start cell with total AP cost 0.
        /// </summary>
        public IReadOnlyDictionary<GridPosition, ReachableCell> FindReachableCells(
            IGridMap map,
            GridPosition start,
            int apBudget,
            IGridOccupancy occupancy)
        {
            return FindReachableCells(
                map,
                start,
                apBudget,
                defaultNeighborService,
                defaultMovementCostService,
                occupancy);
        }

        /// <summary>
        /// Finds the cheapest ordered path from <paramref name="start"/> to
        /// <paramref name="destination"/> without exceeding <paramref name="apBudget"/>.
        /// </summary>
        public GridPath TryFindPath(
            IGridMap map,
            GridPosition start,
            GridPosition destination,
            int apBudget)
        {
            return TryFindPath(
                map,
                start,
                destination,
                apBudget,
                defaultNeighborService,
                defaultMovementCostService,
                NullGridOccupancy.Instance);
        }

        /// <summary>
        /// Finds the cheapest ordered path from <paramref name="start"/> to
        /// <paramref name="destination"/> without exceeding <paramref name="apBudget"/>, using
        /// <paramref name="occupancy"/> to reject occupied cells.
        /// </summary>
        public GridPath TryFindPath(
            IGridMap map,
            GridPosition start,
            GridPosition destination,
            int apBudget,
            IGridOccupancy occupancy)
        {
            return TryFindPath(
                map,
                start,
                destination,
                apBudget,
                defaultNeighborService,
                defaultMovementCostService,
                occupancy);
        }

        /// <summary>
        /// Finds every cell that can be reached from <paramref name="start"/> without exceeding
        /// <paramref name="apBudget"/>. <paramref name="occupancy"/> is optional; null is treated
        /// as <see cref="NullGridOccupancy"/>. The start cell is always allowed so callers can
        /// treat the moving unit as occupying it.
        /// </summary>
        public IReadOnlyDictionary<GridPosition, ReachableCell> FindReachableCells(
            IGridMap map,
            GridPosition start,
            int apBudget,
            GridNeighborService neighborService,
            MovementCostService movementCostService,
            IGridOccupancy occupancy)
        {
            var resolvedOccupancy = occupancy ?? NullGridOccupancy.Instance;
            return FindReachableCells(
                map,
                start,
                apBudget,
                neighborService,
                movementCostService,
                position => resolvedOccupancy.CanEnter(position));
        }

        /// <summary>
        /// Finds every cell that can be reached from <paramref name="start"/> without exceeding
        /// <paramref name="apBudget"/>. <paramref name="canEnterPosition"/> is an optional
        /// occupancy query: return false for occupied or otherwise unavailable destinations.
        /// The start cell is always allowed so callers can treat the moving unit as occupying it.
        /// </summary>
        public IReadOnlyDictionary<GridPosition, ReachableCell> FindReachableCells(
            IGridMap map,
            GridPosition start,
            int apBudget,
            GridNeighborService neighborService,
            MovementCostService movementCostService,
            Func<GridPosition, bool> canEnterPosition)
        {
            if (map == null)
            {
                throw new ArgumentNullException(nameof(map));
            }

            if (neighborService == null)
            {
                throw new ArgumentNullException(nameof(neighborService));
            }

            if (movementCostService == null)
            {
                throw new ArgumentNullException(nameof(movementCostService));
            }

            if (apBudget < 0)
            {
                throw new ArgumentOutOfRangeException(
                    nameof(apBudget),
                    apBudget,
                    "AP budget cannot be negative.");
            }

            if (!map.TryGetCell(start, out var startCell))
            {
                throw new ArgumentException($"Start cell {start} does not exist in the grid map.", nameof(start));
            }

            if (!startCell.Walkable || startCell.BlocksMovement)
            {
                throw new ArgumentException($"Start cell {start} is not walkable.", nameof(start));
            }

            var reachableCells = new Dictionary<GridPosition, ReachableCell>
            {
                { start, new ReachableCell(start, totalApCost: 0) },
            };
            var frontier = new List<GridPosition> { start };
            var finalized = new HashSet<GridPosition>();

            while (frontier.Count > 0)
            {
                var current = PopCheapest(frontier, reachableCells);
                if (!finalized.Add(current))
                {
                    continue;
                }

                var currentCost = reachableCells[current].TotalApCost;
                var neighbors = neighborService.GetNeighbors(map, current);
                foreach (var neighbor in neighbors)
                {
                    if (finalized.Contains(neighbor))
                    {
                        continue;
                    }

                    if (neighbor != start && canEnterPosition != null && !canEnterPosition(neighbor))
                    {
                        continue;
                    }

                    var stepCost = movementCostService.CalculateCost(map, current, neighbor);
                    if (stepCost.IsFailure)
                    {
                        continue;
                    }

                    var totalCost = currentCost + stepCost.Value;
                    if (totalCost > apBudget)
                    {
                        continue;
                    }

                    if (reachableCells.TryGetValue(neighbor, out var existingCell)
                        && existingCell.TotalApCost <= totalCost)
                    {
                        continue;
                    }

                    reachableCells[neighbor] = new ReachableCell(neighbor, totalCost, current);
                    if (!frontier.Contains(neighbor))
                    {
                        frontier.Add(neighbor);
                    }
                }
            }

            return new ReadOnlyDictionary<GridPosition, ReachableCell>(reachableCells);
        }

        /// <summary>
        /// Finds the cheapest ordered path from <paramref name="start"/> to
        /// <paramref name="destination"/> without exceeding <paramref name="apBudget"/>.
        /// <paramref name="occupancy"/> is optional; null is treated as <see cref="NullGridOccupancy"/>.
        /// </summary>
        public GridPath TryFindPath(
            IGridMap map,
            GridPosition start,
            GridPosition destination,
            int apBudget,
            GridNeighborService neighborService,
            MovementCostService movementCostService,
            IGridOccupancy occupancy)
        {
            var resolvedOccupancy = occupancy ?? NullGridOccupancy.Instance;
            return TryFindPath(
                map,
                start,
                destination,
                apBudget,
                neighborService,
                movementCostService,
                position => resolvedOccupancy.CanEnter(position));
        }

        /// <summary>
        /// Finds the cheapest ordered path from <paramref name="start"/> to
        /// <paramref name="destination"/> without exceeding <paramref name="apBudget"/>.
        /// <paramref name="canEnterPosition"/> is an optional occupancy-style predicate matching
        /// the full reachable-cell search overload.
        /// </summary>
        public GridPath TryFindPath(
            IGridMap map,
            GridPosition start,
            GridPosition destination,
            int apBudget,
            GridNeighborService neighborService,
            MovementCostService movementCostService,
            Func<GridPosition, bool> canEnterPosition)
        {
            var reachableCells = FindReachableCells(
                map,
                start,
                apBudget,
                neighborService,
                movementCostService,
                canEnterPosition);

            if (!reachableCells.ContainsKey(destination))
            {
                return GridPath.Failure(
                    $"Destination {destination} is not reachable from {start} within {apBudget} AP.");
            }

            return ReconstructPath(start, destination, reachableCells);
        }

        private static GridPath ReconstructPath(
            GridPosition start,
            GridPosition destination,
            IReadOnlyDictionary<GridPosition, ReachableCell> reachableCells)
        {
            var reversedCells = new List<ReachableCell>();
            var visited = new HashSet<GridPosition>();
            var current = destination;

            while (true)
            {
                if (!visited.Add(current))
                {
                    return GridPath.Failure($"Path reconstruction detected a cycle at {current}.");
                }

                if (!reachableCells.TryGetValue(current, out var reachableCell))
                {
                    return GridPath.Failure(
                        $"Cannot reconstruct path from {start} to {destination}: missing reachable cell {current}.");
                }

                reversedCells.Add(reachableCell);

                if (current == start)
                {
                    break;
                }

                if (!reachableCell.PreviousPosition.HasValue)
                {
                    return GridPath.Failure(
                        $"Cannot reconstruct path from {start} to {destination}: {current} has no previous cell.");
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
                        $"Cannot reconstruct path from {start} to {destination}: AP totals decrease at {cell.Position}.");
                }

                steps.Add(new PathStep(cell.Position, stepApCost, cell.TotalApCost));
                previousTotalApCost = cell.TotalApCost;
            }

            return GridPath.Success(steps);
        }

        private static GridPosition PopCheapest(
            IList<GridPosition> frontier,
            IReadOnlyDictionary<GridPosition, ReachableCell> reachableCells)
        {
            var bestIndex = 0;
            var bestCost = reachableCells[frontier[0]].TotalApCost;

            for (var i = 1; i < frontier.Count; i++)
            {
                var candidateCost = reachableCells[frontier[i]].TotalApCost;
                if (candidateCost < bestCost)
                {
                    bestIndex = i;
                    bestCost = candidateCost;
                }
            }

            var best = frontier[bestIndex];
            frontier.RemoveAt(bestIndex);
            return best;
        }
    }
}
