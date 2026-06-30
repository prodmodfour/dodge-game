using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using ReactionTactics.Grid;

namespace ReactionTactics.Pathfinding
{
    /// <summary>
    /// Calculates all legal movement destinations within an AP budget using Dijkstra search.
    /// Terrain movement rules come from the neighbor and movement-cost services; callers may
    /// also provide an occupancy-style predicate to reject cells that cannot currently be entered.
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
                null);
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
