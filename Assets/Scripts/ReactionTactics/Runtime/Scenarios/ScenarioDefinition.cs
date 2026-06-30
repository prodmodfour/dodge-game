using System;
using System.Collections.Generic;
using ReactionTactics.Actions;
using ReactionTactics.Core;
using ReactionTactics.Grid;
using ReactionTactics.Units;
using UnityEngine;

namespace ReactionTactics.Scenarios
{
    /// <summary>
    /// Serialized battle setup data for a prototype encounter. A scenario names the
    /// map to use and the units that should be spawned onto valid, unique grid cells.
    /// </summary>
    [CreateAssetMenu(fileName = "ScenarioDefinition", menuName = "Reaction Tactics/Scenarios/Scenario Definition")]
    public sealed class ScenarioDefinition : ScriptableObject
    {
        [SerializeField]
        [Tooltip("Grid map asset used by this battle setup.")]
        private GridMapDefinition mapDefinition;

        [SerializeField]
        [Tooltip("Unit spawn entries for the battle. Each starting cell must exist in the map and be unique.")]
        private List<UnitEntry> unitEntries = new List<UnitEntry>();

        public GridMapDefinition MapDefinition
        {
            get { return mapDefinition; }
        }

        public IReadOnlyList<UnitEntry> UnitEntries
        {
            get
            {
                EnsureUnitEntries();
                return unitEntries;
            }
        }

        /// <summary>
        /// Configures this scenario in one step for deterministic editor tooling and tests.
        /// </summary>
        public void Configure(GridMapDefinition mapDefinition, IEnumerable<UnitEntry> unitEntries)
        {
            if (unitEntries == null)
            {
                throw new ArgumentNullException(nameof(unitEntries));
            }

            this.mapDefinition = mapDefinition;
            this.unitEntries = new List<UnitEntry>();

            foreach (var entry in unitEntries)
            {
                if (entry == null)
                {
                    throw new ArgumentException("Scenario unit entries cannot contain null entries.", nameof(unitEntries));
                }

                entry.EnsureSerializedCollections();
                this.unitEntries.Add(entry);
            }
        }

        /// <summary>
        /// Returns success only when the scenario can describe a complete battle setup.
        /// </summary>
        public TacticalResult ValidateScenario()
        {
            var errors = CollectValidationErrors();
            return errors.Count == 0
                ? TacticalResult.Success()
                : TacticalResult.Failure(string.Join(" ", errors));
        }

        /// <summary>
        /// Lists every validation problem so editor tools can show all setup issues at once.
        /// </summary>
        public IReadOnlyList<string> CollectValidationErrors()
        {
            var errors = new List<string>();
            var label = GetDebugName();

            var map = BuildMapForValidation(label, errors);
            AddUnitEntryValidationErrors(label, map, errors);

            return errors.AsReadOnly();
        }

        private void OnValidate()
        {
            EnsureUnitEntries();

            foreach (var unitEntry in unitEntries)
            {
                unitEntry?.EnsureSerializedCollections();
            }
        }

        private string GetDebugName()
        {
            return string.IsNullOrWhiteSpace(name) ? "ScenarioDefinition" : name.Trim();
        }

        private GridMap BuildMapForValidation(string label, ICollection<string> errors)
        {
            if (mapDefinition == null)
            {
                errors.Add($"{label}: map definition is required.");
                return null;
            }

            try
            {
                return mapDefinition.BuildGridMap();
            }
            catch (Exception exception)
            {
                errors.Add($"{label}: map definition '{mapDefinition.name}' is invalid: {exception.Message}");
                return null;
            }
        }

        private void AddUnitEntryValidationErrors(string label, GridMap map, ICollection<string> errors)
        {
            if (unitEntries == null)
            {
                errors.Add($"{label}: unit entries list is missing.");
                return;
            }

            if (unitEntries.Count == 0)
            {
                errors.Add($"{label}: at least one unit entry is required.");
                return;
            }

            var occupiedStarts = new Dictionary<GridPosition, int>();
            for (var i = 0; i < unitEntries.Count; i++)
            {
                var entry = unitEntries[i];
                var entryLabel = $"{label}: unit entry #{i + 1}";

                if (entry == null)
                {
                    errors.Add($"{entryLabel} is missing.");
                    continue;
                }

                entry.AddValidationErrors(entryLabel, errors);
                AddDuplicateSpawnValidationError(label, occupiedStarts, entry.StartingCell, i, errors);

                if (map != null)
                {
                    AddMapCellValidationErrors(entryLabel, map, entry.StartingCell, errors);
                }
            }
        }

        private static void AddDuplicateSpawnValidationError(
            string label,
            IDictionary<GridPosition, int> occupiedStarts,
            GridPosition startingCell,
            int unitEntryIndex,
            ICollection<string> errors)
        {
            if (occupiedStarts.TryGetValue(startingCell, out var firstEntryIndex))
            {
                errors.Add(
                    $"{label}: duplicate spawn cell {startingCell} used by unit entries #{firstEntryIndex + 1} and #{unitEntryIndex + 1}.");
                return;
            }

            occupiedStarts.Add(startingCell, unitEntryIndex);
        }

        private static void AddMapCellValidationErrors(
            string entryLabel,
            GridMap map,
            GridPosition startingCell,
            ICollection<string> errors)
        {
            if (!map.TryGetCell(startingCell, out var cell))
            {
                errors.Add(
                    $"{entryLabel} starts at {startingCell}, which does not exist in the map. Verify x/z and height y match the terrain cell.");
                return;
            }

            if (!cell.Walkable || cell.BlocksMovement)
            {
                errors.Add($"{entryLabel} starts at {startingCell}, which is not walkable.");
            }
        }

        private void EnsureUnitEntries()
        {
            if (unitEntries == null)
            {
                unitEntries = new List<UnitEntry>();
            }
        }

        /// <summary>
        /// Serialized spawn data for one unit in a scenario.
        /// </summary>
        [Serializable]
        public sealed class UnitEntry
        {
            [SerializeField]
            [Tooltip("Team that controls this unit.")]
            private TeamId team = TeamId.Player;

            [SerializeField]
            [Tooltip("Stats asset used when spawning this unit.")]
            private UnitStatsDefinition stats;

            [SerializeField]
            [Tooltip("Starting grid X coordinate.")]
            private int startingX;

            [SerializeField]
            [Tooltip("Starting grid Y height. This must match the map cell height.")]
            private int startingY;

            [SerializeField]
            [Tooltip("Starting grid Z coordinate.")]
            private int startingZ;

            [SerializeField]
            [Tooltip("Optional ability override for this unit. Empty means use loader/default setup rules.")]
            private List<AbilityDefinition> abilityLoadout = new List<AbilityDefinition>();

            public UnitEntry()
            {
                EnsureSerializedCollections();
            }

            public UnitEntry(
                TeamId team,
                UnitStatsDefinition stats,
                GridPosition startingCell,
                IEnumerable<AbilityDefinition> abilityLoadout = null)
            {
                this.team = team;
                this.stats = stats;
                startingX = startingCell.X;
                startingY = startingCell.Y;
                startingZ = startingCell.Z;
                this.abilityLoadout = new List<AbilityDefinition>();

                if (abilityLoadout != null)
                {
                    SetAbilityLoadout(abilityLoadout);
                }
            }

            public TeamId Team
            {
                get { return team; }
            }

            public UnitStatsDefinition Stats
            {
                get { return stats; }
            }

            public GridPosition StartingCell
            {
                get { return new GridPosition(startingX, startingY, startingZ); }
            }

            public IReadOnlyList<AbilityDefinition> AbilityLoadout
            {
                get
                {
                    EnsureSerializedCollections();
                    return abilityLoadout;
                }
            }

            public bool HasAbilityLoadoutOverride
            {
                get { return AbilityLoadout.Count > 0; }
            }

            public void SetAbilityLoadout(IEnumerable<AbilityDefinition> abilities)
            {
                if (abilities == null)
                {
                    throw new ArgumentNullException(nameof(abilities));
                }

                EnsureSerializedCollections();
                abilityLoadout.Clear();

                foreach (var ability in abilities)
                {
                    AddUniqueAbility(abilityLoadout, ability);
                }
            }

            internal void EnsureSerializedCollections()
            {
                if (abilityLoadout == null)
                {
                    abilityLoadout = new List<AbilityDefinition>();
                }
            }

            internal void AddValidationErrors(string entryLabel, ICollection<string> errors)
            {
                if (!Enum.IsDefined(typeof(TeamId), team))
                {
                    errors.Add($"{entryLabel} has unsupported team '{team}'.");
                }

                if (stats == null)
                {
                    errors.Add($"{entryLabel} is missing unit stats.");
                }
            }

            private static void AddUniqueAbility(ICollection<AbilityDefinition> abilities, AbilityDefinition ability)
            {
                if (ability == null || abilities.Contains(ability))
                {
                    return;
                }

                abilities.Add(ability);
            }
        }
    }
}
