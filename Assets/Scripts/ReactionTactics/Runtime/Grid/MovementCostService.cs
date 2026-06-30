using System;
using ReactionTactics.Core;

namespace ReactionTactics.Grid
{
    /// <summary>
    /// Calculates deterministic AP costs for moving between neighboring grid cells.
    /// </summary>
    public sealed class MovementCostService
    {
        public const int DefaultUphillSurchargePerHeight = 0;

        private readonly GridNeighborService neighborService;

        public MovementCostService()
            : this(new GridNeighborService(), DefaultUphillSurchargePerHeight)
        {
        }

        public MovementCostService(int uphillSurchargePerHeight)
            : this(new GridNeighborService(), uphillSurchargePerHeight)
        {
        }

        public MovementCostService(
            GridNeighborService neighborService,
            int uphillSurchargePerHeight = DefaultUphillSurchargePerHeight)
        {
            this.neighborService = neighborService ?? throw new ArgumentNullException(nameof(neighborService));

            if (uphillSurchargePerHeight < 0)
            {
                throw new ArgumentOutOfRangeException(
                    nameof(uphillSurchargePerHeight),
                    uphillSurchargePerHeight,
                    "Uphill movement surcharge cannot be negative.");
            }

            UphillSurchargePerHeight = uphillSurchargePerHeight;
        }

        public int UphillSurchargePerHeight { get; }

        /// <summary>
        /// Returns the AP cost to move from origin to destination, or a failure if the move is not legal.
        /// Base cost is the destination cell's movement cost. Optional uphill surcharge is applied per height climbed.
        /// </summary>
        public TacticalResult<int> CalculateCost(IGridMap map, GridPosition origin, GridPosition destination)
        {
            if (map == null)
            {
                throw new ArgumentNullException(nameof(map));
            }

            if (!map.TryGetCell(origin, out var originCell))
            {
                return TacticalResult<int>.Failure($"Cannot move from {origin}: origin cell does not exist.");
            }

            if (!map.TryGetCell(destination, out var destinationCell))
            {
                return TacticalResult<int>.Failure($"Cannot move to {destination}: destination cell does not exist.");
            }

            if (!neighborService.IsValidNeighbor(map, origin, destination))
            {
                return TacticalResult<int>.Failure(
                    $"Cannot move from {origin} to {destination}: destination is not a valid movement neighbor.");
            }

            var uphillHeight = Math.Max(0, destinationCell.Position.Y - originCell.Position.Y);
            var cost = destinationCell.MovementCost + (uphillHeight * UphillSurchargePerHeight);
            return TacticalResult<int>.Success(cost);
        }
    }
}
