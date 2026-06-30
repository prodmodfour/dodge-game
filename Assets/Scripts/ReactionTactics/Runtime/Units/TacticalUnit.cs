using System;
using ReactionTactics.Core;
using ReactionTactics.Grid;
using UnityEngine;

namespace ReactionTactics.Units
{
    /// <summary>
    /// Scene component that represents one combatant on the tactical grid.
    /// It stores identity, team, copied runtime resources, and current grid
    /// position while owning the shared AP wallet and deterministic HP state.
    /// Turn flow and damage resolution orchestration are implemented by later combat systems.
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
        [Tooltip("True while the unit is alive and available to combat systems.")]
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
        /// Raised once when damage reduces this unit's HP to zero.
        /// </summary>
        public event Action<TacticalUnit, DamageSource> Died;

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

        /// <summary>
        /// Applies deterministic HP damage. Hit, miss, dodge, and positional-avoidance rules
        /// are handled by action resolution before this method is called.
        /// </summary>
        public TacticalResult ApplyDamage(int amount, DamageSource source)
        {
            if (amount < 0)
            {
                return TacticalResult.Failure("Damage amount cannot be negative.");
            }

            if (amount == 0)
            {
                return TacticalResult.Success();
            }

            if (IsDead)
            {
                return TacticalResult.Failure($"{DisplayName} is already defeated.");
            }

            currentHP = Math.Max(0, currentHP - amount);
            if (currentHP == 0)
            {
                MarkDead(source);
            }

            return TacticalResult.Success();
        }

        /// <summary>
        /// Returns whether this unit's shared AP wallet can pay the requested cost.
        /// Phase, action, reaction, and alive-state legality are validated by combat systems.
        /// </summary>
        public bool CanSpendAP(int amount)
        {
            return amount >= 0 && currentAP >= amount;
        }

        /// <summary>
        /// Spends AP from the shared action/reaction pool when enough points are available.
        /// Expected player-facing failures are reported as tactical results rather than exceptions.
        /// </summary>
        public TacticalResult SpendAP(int amount)
        {
            if (amount < 0)
            {
                return TacticalResult.Failure("AP spend amount cannot be negative.");
            }

            if (currentAP < amount)
            {
                return TacticalResult.Failure($"{DisplayName} needs {amount} AP but only has {currentAP} AP.");
            }

            currentAP -= amount;
            return TacticalResult.Success();
        }

        /// <summary>
        /// Restores the shared AP wallet to the unit's current maximum AP for a new round.
        /// </summary>
        public void RefreshAP()
        {
            currentAP = MaxAP;
        }

        /// <summary>
        /// Sets AP directly for deterministic tests while preserving wallet invariants.
        /// Gameplay code should spend or refresh AP instead of calling this helper.
        /// </summary>
        public void SetAPForTest(int amount)
        {
            if (amount < 0)
            {
                throw new ArgumentOutOfRangeException(nameof(amount), amount, "Test AP cannot be negative.");
            }

            if (amount > MaxAP)
            {
                throw new ArgumentOutOfRangeException(nameof(amount), amount, $"Test AP cannot exceed MaxAP ({MaxAP}).");
            }

            currentAP = amount;
        }

        public override string ToString()
        {
            return IsInitialized
                ? $"{DisplayName} {UnitId} [{team}] at {CurrentGridPosition} HP {currentHP}/{MaxHP} AP {currentAP}/{MaxAP}"
                : $"Uninitialized TacticalUnit '{name}' at {CurrentGridPosition}";
        }

        private void MarkDead(DamageSource source)
        {
            currentHP = 0;
            if (!isAlive)
            {
                return;
            }

            isAlive = false;
            Died?.Invoke(this, source);
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
