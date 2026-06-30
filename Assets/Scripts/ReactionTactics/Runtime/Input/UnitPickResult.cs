using ReactionTactics.Grid;
using ReactionTactics.Units;
using UnityEngine;

namespace ReactionTactics.Input
{
    /// <summary>
    /// Immutable data describing a successful mouse raycast against a tactical unit collider.
    /// </summary>
    public readonly struct UnitPickResult
    {
        public UnitPickResult(TacticalUnit unit, RaycastHit hit)
        {
            Unit = unit;
            UnitId = unit != null ? unit.UnitId : UnitId.None;
            Team = unit != null ? unit.Team : TeamId.Player;
            Position = unit != null ? unit.CurrentGridPosition : GridPosition.Zero;
            DisplayName = unit != null ? unit.DisplayName : string.Empty;
            Collider = hit.collider;
            WorldPoint = hit.point;
            Distance = hit.distance;
        }

        public TacticalUnit Unit { get; }

        public UnitId UnitId { get; }

        public TeamId Team { get; }

        public GridPosition Position { get; }

        public string DisplayName { get; }

        public Collider Collider { get; }

        public Vector3 WorldPoint { get; }

        public float Distance { get; }
    }
}
