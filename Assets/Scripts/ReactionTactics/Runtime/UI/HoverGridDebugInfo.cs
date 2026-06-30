using ReactionTactics.Grid;
using ReactionTactics.Units;

namespace ReactionTactics.UI
{
    /// <summary>
    /// Read-only snapshot of the terrain and occupancy data shown by the hover debug overlay.
    /// </summary>
    public readonly struct HoverGridDebugInfo
    {
        public HoverGridDebugInfo(GridCell cell, TacticalUnit occupant)
        {
            Position = cell.Position;
            Walkable = cell.Walkable;
            BlocksMovement = cell.BlocksMovement;
            BlocksLineOfSight = cell.BlocksLineOfSight;
            MovementCost = cell.MovementCost;
            HeightY = cell.Position.Y;
            DisplayHeight = cell.DisplayHeight;

            HasOccupant = occupant != null;
            OccupantName = HasOccupant ? occupant.DisplayName : "None";
            OccupantId = HasOccupant ? occupant.UnitId : UnitId.None;
            OccupantTeam = HasOccupant ? occupant.Team.ToString() : "None";
            OccupantAlive = HasOccupant && occupant.IsAlive;
        }

        public GridPosition Position { get; }

        public bool Walkable { get; }

        public bool BlocksMovement { get; }

        public bool BlocksLineOfSight { get; }

        public int MovementCost { get; }

        public int HeightY { get; }

        public float DisplayHeight { get; }

        public bool HasOccupant { get; }

        public string OccupantName { get; }

        public UnitId OccupantId { get; }

        public string OccupantTeam { get; }

        public bool OccupantAlive { get; }

        public string OccupantLabel
        {
            get
            {
                if (!HasOccupant)
                {
                    return "None";
                }

                var aliveSuffix = OccupantAlive ? string.Empty : " (defeated)";
                return $"{OccupantName} {OccupantId} [{OccupantTeam}]{aliveSuffix}";
            }
        }
    }
}
