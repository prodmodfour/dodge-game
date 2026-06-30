using ReactionTactics.Grid;
using UnityEngine;

namespace ReactionTactics.Input
{
    /// <summary>
    /// Immutable data describing a successful mouse raycast against a visible grid tile.
    /// </summary>
    public readonly struct GridPickResult
    {
        public GridPickResult(GridTileView tile, RaycastHit hit)
        {
            Tile = tile;
            Position = tile != null ? tile.GridPosition : GridPosition.Zero;
            Collider = hit.collider;
            WorldPoint = hit.point;
            Distance = hit.distance;
        }

        public GridTileView Tile { get; }

        public GridPosition Position { get; }

        public Collider Collider { get; }

        public Vector3 WorldPoint { get; }

        public float Distance { get; }
    }
}
