using System;
using ReactionTactics.Grid;
using UnityEngine;

namespace ReactionTactics.Units
{
    /// <summary>
    /// Scene component that represents one combatant on the tactical grid.
    /// It stores identity, team, copied runtime resources, and current grid
    /// position while leaving turn flow, AP spending, and damage resolution to
    /// later combat systems.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class TacticalUnit : MonoBehaviour
    {
        [SerializeField]
        [Min(0)]
        [Tooltip("Stable runtime unit identifier. Zero means unassigned until initialized by a spawner or setup tool.")]
        private int unitIdValue;

        [SerializeField]
        [Tooltip("Prototype combat side this unit belongs to.")]
        private TeamId team = TeamId.Player;

        [SerializeField]
        [Tooltip("Archetype stats copied into runtime HP/AP when the unit is initialized.")]
        private UnitStatsDefinition statsDefinition;

        [SerializeField]
        [Tooltip("Current discrete grid coordinate for this unit.")]
        private Vector3Int currentGridPosition;

        [SerializeField]
        [Min(0)]
        [Tooltip("Current runtime hit points copied from stats at initialization.")]
        private int currentHP;

        [SerializeField]
        [Min(0)]
        [Tooltip("Current shared action points copied from stats at initialization.")]
        private int currentAP;

        [SerializeField]
        [Tooltip("True while the unit is available to combat systems. Damage/death rules are implemented in later tickets.")]
        private bool isAlive;

        public UnitId UnitId
        {
            get { return unitIdValue > 0 ? new UnitId(unitIdValue) : UnitId.None; }
        }

        public TeamId Team
        {
            get { return team; }
        }

        public UnitStatsDefinition StatsDefinition
        {
            get { return statsDefinition; }
        }

        public string DisplayName
        {
            get { return statsDefinition != null ? statsDefinition.DisplayName : name; }
        }

        public GridPosition CurrentGridPosition
        {
            get { return ToGridPosition(currentGridPosition); }
        }

        public int CurrentHP
        {
            get { return currentHP; }
        }

        public int CurrentAP
        {
            get { return currentAP; }
        }

        public int MaxHP
        {
            get { return statsDefinition != null ? statsDefinition.MaxHP : 0; }
        }

        public int MaxAP
        {
            get { return statsDefinition != null ? statsDefinition.MaxAP : 0; }
        }

        public int MeleeRange
        {
            get { return statsDefinition != null ? statsDefinition.MeleeRange : 0; }
        }

        public bool IsAlive
        {
            get { return isAlive && currentHP > 0; }
        }

        public bool IsDead
        {
            get { return !IsAlive; }
        }

        public bool IsInitialized
        {
            get { return UnitId.IsAssigned && statsDefinition != null; }
        }

        /// <summary>
        /// Initializes this scene unit from a stats asset and starting grid cell.
        /// Runtime HP and AP are copied from the stats definition immediately.
        /// </summary>
        public void Initialize(
            UnitId unitId,
            TeamId team,
            UnitStatsDefinition statsDefinition,
            GridPosition startingPosition)
        {
            ValidateAssignedUnitId(unitId);
            ValidateTeam(team);
            if (statsDefinition == null)
            {
                throw new ArgumentNullException(nameof(statsDefinition));
            }

            unitIdValue = unitId.Value;
            this.team = team;
            this.statsDefinition = statsDefinition;
            currentGridPosition = ToVector3Int(startingPosition);
            currentHP = statsDefinition.MaxHP;
            currentAP = statsDefinition.MaxAP;
            isAlive = true;
        }

        /// <summary>
        /// Initializes this scene unit using its serialized team and stats reference.
        /// </summary>
        public void Initialize(UnitId unitId, GridPosition startingPosition)
        {
            Initialize(unitId, team, statsDefinition, startingPosition);
        }

        /// <summary>
        /// Updates the unit's authoritative grid position without changing HP/AP.
        /// Movement command execution and occupancy updates are implemented separately.
        /// </summary>
        public void SetGridPosition(GridPosition position)
        {
            currentGridPosition = ToVector3Int(position);
        }

        public override string ToString()
        {
            return IsInitialized
                ? $"{DisplayName} {UnitId} [{team}] at {CurrentGridPosition} HP {currentHP}/{MaxHP} AP {currentAP}/{MaxAP}"
                : $"Uninitialized TacticalUnit '{name}' at {CurrentGridPosition}";
        }

        private void Reset()
        {
            unitIdValue = 0;
            team = TeamId.Player;
            statsDefinition = null;
            currentGridPosition = Vector3Int.zero;
            currentHP = 0;
            currentAP = 0;
            isAlive = false;
        }

        private void OnValidate()
        {
            if (unitIdValue < 0)
            {
                unitIdValue = 0;
            }

            if (!Enum.IsDefined(typeof(TeamId), team))
            {
                team = TeamId.Player;
            }

            currentHP = Math.Max(0, currentHP);
            currentAP = Math.Max(0, currentAP);

            if (statsDefinition != null)
            {
                currentHP = Math.Min(currentHP, statsDefinition.MaxHP);
                currentAP = Math.Min(currentAP, statsDefinition.MaxAP);
            }

            if (currentHP == 0)
            {
                isAlive = false;
            }
        }

        private static void ValidateAssignedUnitId(UnitId unitId)
        {
            if (!unitId.IsAssigned)
            {
                throw new ArgumentException("Tactical units require an assigned unit ID.", nameof(unitId));
            }
        }

        private static void ValidateTeam(TeamId team)
        {
            if (!Enum.IsDefined(typeof(TeamId), team))
            {
                throw new ArgumentOutOfRangeException(nameof(team), team, "Unknown tactical unit team.");
            }
        }

        private static GridPosition ToGridPosition(Vector3Int value)
        {
            return new GridPosition(value.x, value.y, value.z);
        }

        private static Vector3Int ToVector3Int(GridPosition value)
        {
            return new Vector3Int(value.X, value.Y, value.Z);
        }
    }
}
