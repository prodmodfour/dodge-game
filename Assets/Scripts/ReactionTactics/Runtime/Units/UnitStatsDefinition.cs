using System;
using UnityEngine;

namespace ReactionTactics.Units
{
    /// <summary>
    /// Serialized prototype archetype stats for tactical units. Runtime unit instances
    /// copy values from this asset when they are initialized.
    /// </summary>
    [CreateAssetMenu(fileName = "UnitStatsDefinition", menuName = "Reaction Tactics/Units/Unit Stats Definition")]
    public sealed class UnitStatsDefinition : ScriptableObject
    {
        public const int MinimumMaxHP = 1;
        public const int MinimumMaxAP = 1;
        public const int MinimumMeleeRange = 1;
        public const float DefaultMovementAnimationSpeed = 4f;
        public const float MinimumMovementAnimationSpeed = 0.01f;

        private const string DefaultDisplayName = "Prototype Unit";

        [SerializeField]
        [Tooltip("Name shown in prototype UI and combat logs.")]
        private string displayName = DefaultDisplayName;

        [SerializeField]
        [Min(MinimumMaxHP)]
        [Tooltip("Maximum hit points for units spawned from this archetype.")]
        private int maxHP = 10;

        [SerializeField]
        [Min(MinimumMaxAP)]
        [Tooltip("Maximum shared action points available to active actions and reactions each round.")]
        private int maxAP = 6;

        [SerializeField]
        [Min(MinimumMovementAnimationSpeed)]
        [Tooltip("World units per second used by the prototype movement animation.")]
        private float movementAnimationSpeed = DefaultMovementAnimationSpeed;

        [SerializeField]
        [Min(MinimumMeleeRange)]
        [Tooltip("Horizontal grid distance used for melee targeting and resolution.")]
        private int meleeRange = MinimumMeleeRange;

        [SerializeField]
        [Tooltip("Fallback color hint for team or archetype visuals when no material is assigned.")]
        private Color teamColorHint = Color.white;

        [SerializeField]
        [Tooltip("Optional material hint for primitive prototype unit visuals.")]
        private Material teamMaterialHint;

        public string DisplayName
        {
            get { return string.IsNullOrWhiteSpace(displayName) ? DefaultDisplayName : displayName; }
        }

        public int MaxHP
        {
            get { return Math.Max(MinimumMaxHP, maxHP); }
        }

        public int MaxAP
        {
            get { return Math.Max(MinimumMaxAP, maxAP); }
        }

        public float MovementAnimationSpeed
        {
            get
            {
                return IsPositiveFinite(movementAnimationSpeed)
                    ? Math.Max(MinimumMovementAnimationSpeed, movementAnimationSpeed)
                    : DefaultMovementAnimationSpeed;
            }
        }

        public int MeleeRange
        {
            get { return Math.Max(MinimumMeleeRange, meleeRange); }
        }

        public Color TeamColorHint
        {
            get { return IsFinite(teamColorHint) ? teamColorHint : Color.white; }
        }

        public Material TeamMaterialHint
        {
            get { return teamMaterialHint; }
        }

        /// <summary>
        /// Configures the asset in one step for deterministic editor tooling and tests.
        /// </summary>
        public void Configure(
            string displayName,
            int maxHP,
            int maxAP,
            float movementAnimationSpeed,
            int meleeRange,
            Color teamColorHint,
            Material teamMaterialHint = null)
        {
            ValidateDisplayName(displayName);
            ValidateAtLeast(maxHP, MinimumMaxHP, nameof(maxHP), "Maximum HP");
            ValidateAtLeast(maxAP, MinimumMaxAP, nameof(maxAP), "Maximum AP");
            ValidatePositiveFinite(movementAnimationSpeed, nameof(movementAnimationSpeed));
            ValidateAtLeast(meleeRange, MinimumMeleeRange, nameof(meleeRange), "Melee range");
            ValidateFinite(teamColorHint, nameof(teamColorHint));

            this.displayName = displayName;
            this.maxHP = maxHP;
            this.maxAP = maxAP;
            this.movementAnimationSpeed = movementAnimationSpeed;
            this.meleeRange = meleeRange;
            this.teamColorHint = teamColorHint;
            this.teamMaterialHint = teamMaterialHint;
        }

        private void OnValidate()
        {
            if (string.IsNullOrWhiteSpace(displayName))
            {
                displayName = DefaultDisplayName;
            }

            maxHP = Math.Max(MinimumMaxHP, maxHP);
            maxAP = Math.Max(MinimumMaxAP, maxAP);

            if (!IsPositiveFinite(movementAnimationSpeed))
            {
                movementAnimationSpeed = DefaultMovementAnimationSpeed;
            }
            else if (movementAnimationSpeed < MinimumMovementAnimationSpeed)
            {
                movementAnimationSpeed = MinimumMovementAnimationSpeed;
            }

            meleeRange = Math.Max(MinimumMeleeRange, meleeRange);

            if (!IsFinite(teamColorHint))
            {
                teamColorHint = Color.white;
            }
        }

        private static void ValidateDisplayName(string value)
        {
            if (string.IsNullOrWhiteSpace(value))
            {
                throw new ArgumentException("Unit stats display name cannot be blank.", nameof(value));
            }
        }

        private static void ValidateAtLeast(int value, int minimum, string parameterName, string label)
        {
            if (value < minimum)
            {
                throw new ArgumentOutOfRangeException(
                    parameterName,
                    value,
                    $"{label} must be at least {minimum}.");
            }
        }

        private static void ValidatePositiveFinite(float value, string parameterName)
        {
            if (!IsPositiveFinite(value))
            {
                throw new ArgumentOutOfRangeException(
                    parameterName,
                    value,
                    "Movement animation speed must be a positive finite value.");
            }
        }

        private static void ValidateFinite(Color value, string parameterName)
        {
            if (!IsFinite(value))
            {
                throw new ArgumentOutOfRangeException(
                    parameterName,
                    value,
                    "Team color hint must contain only finite values.");
            }
        }

        private static bool IsPositiveFinite(float value)
        {
            return !float.IsNaN(value) && !float.IsInfinity(value) && value > 0f;
        }

        private static bool IsFinite(Color value)
        {
            return !float.IsNaN(value.r) && !float.IsInfinity(value.r)
                && !float.IsNaN(value.g) && !float.IsInfinity(value.g)
                && !float.IsNaN(value.b) && !float.IsInfinity(value.b)
                && !float.IsNaN(value.a) && !float.IsInfinity(value.a);
        }
    }
}
