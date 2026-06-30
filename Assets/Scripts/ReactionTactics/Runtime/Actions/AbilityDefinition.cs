using System;
using System.Collections.Generic;
using ReactionTactics.Core;
using UnityEngine;

namespace ReactionTactics.Actions
{
    /// <summary>
    /// Serialized prototype ability data used by active actions and reactions.
    /// The asset stores deterministic costs, targeting shape, timing, and effect
    /// values; runtime validators and resolvers decide whether a specific unit may
    /// use the ability in the current combat state.
    /// </summary>
    [CreateAssetMenu(fileName = "AbilityDefinition", menuName = "Reaction Tactics/Abilities/Ability Definition")]
    public sealed class AbilityDefinition : ScriptableObject
    {
        public const int MinimumAPCost = 0;
        public const int MinimumRange = 0;
        public const int MinimumRadius = 0;
        public const int MinimumDamage = 0;

        private const string DefaultAbilityKey = "ability";
        private const string DefaultDisplayName = "Ability";
        private const AbilityUsage AllowedUsageMask = AbilityUsage.Action | AbilityUsage.Reaction;

        [SerializeField]
        [Tooltip("Stable string ID used by setup tools, saves, logs, and tests. Keep this value stable after creating an ability asset.")]
        private string abilityKey = DefaultAbilityKey;

        [SerializeField]
        [Tooltip("Player-facing name shown in prototype UI and combat logs.")]
        private string displayName = DefaultDisplayName;

        [SerializeField]
        [Min(MinimumAPCost)]
        [Tooltip("Shared AP spent when this ability is declared or used.")]
        private int apCost;

        [SerializeField]
        [Tooltip("Whether this ability is available as an active action, an off-turn reaction, or both.")]
        private AbilityUsage usage = AbilityUsage.Action;

        [SerializeField]
        [Tooltip("Whether this ability resolves immediately or after a reaction window.")]
        private AbilityTiming timing = AbilityTiming.Immediate;

        [SerializeField]
        [Tooltip("Targeting footprint used by declaration, preview, and resolution systems.")]
        private AbilityShape shape = AbilityShape.Self;

        [SerializeField]
        [Min(MinimumRange)]
        [Tooltip("Maximum horizontal grid range for targeted shapes. Melee may use the acting unit's melee range instead.")]
        private int range;

        [SerializeField]
        [Min(MinimumRadius)]
        [Tooltip("Radius for area abilities. Radius shapes require at least 1.")]
        private int radius;

        [SerializeField]
        [Min(MinimumDamage)]
        [Tooltip("Deterministic damage applied by attack resolvers. Non-damaging abilities use 0.")]
        private int damage;

        [SerializeField]
        [Tooltip("If true, declaring this ability opens a reaction window before resolution.")]
        private bool triggersReactions;

        [SerializeField]
        [TextArea(2, 5)]
        [Tooltip("Short rules text for UI, tooltips, and designer notes.")]
        private string description = string.Empty;

        public string AbilityKey
        {
            get { return NormalizeString(abilityKey); }
        }

        public string DisplayName
        {
            get { return NormalizeString(displayName); }
        }

        public int APCost
        {
            get { return Math.Max(MinimumAPCost, apCost); }
        }

        public AbilityUsage Usage
        {
            get { return usage; }
        }

        public AbilityTiming Timing
        {
            get { return timing; }
        }

        public AbilityShape Shape
        {
            get { return shape; }
        }

        public int Range
        {
            get { return Math.Max(MinimumRange, range); }
        }

        public int Radius
        {
            get { return Math.Max(MinimumRadius, radius); }
        }

        public int Damage
        {
            get { return Math.Max(MinimumDamage, damage); }
        }

        public bool TriggersReactions
        {
            get { return triggersReactions; }
        }

        public string Description
        {
            get { return description ?? string.Empty; }
        }

        public bool IsTelegraphed
        {
            get { return timing == AbilityTiming.Telegraphed; }
        }

        public bool CanBeUsedAsAction
        {
            get { return SupportsUsage(AbilityUsage.Action); }
        }

        public bool CanBeUsedAsReaction
        {
            get { return SupportsUsage(AbilityUsage.Reaction); }
        }

        public bool SupportsUsage(AbilityUsage requestedUsage)
        {
            return requestedUsage != AbilityUsage.None
                && IsValidUsage(requestedUsage)
                && IsValidUsage(usage)
                && (usage & requestedUsage) == requestedUsage;
        }

        /// <summary>
        /// Configures the asset in one step for deterministic editor tooling and tests.
        /// </summary>
        public void Configure(
            string abilityKey,
            string displayName,
            int apCost,
            AbilityUsage usage,
            AbilityTiming timing,
            AbilityShape shape,
            int range,
            int radius,
            int damage,
            bool triggersReactions,
            string description = "")
        {
            var validationErrors = CollectValidationErrors(
                abilityKey,
                displayName,
                apCost,
                usage,
                timing,
                shape,
                range,
                radius,
                damage,
                GetDebugName());

            if (validationErrors.Count > 0)
            {
                throw new ArgumentException(string.Join(" ", validationErrors), nameof(abilityKey));
            }

            this.abilityKey = abilityKey.Trim();
            this.displayName = displayName.Trim();
            this.apCost = apCost;
            this.usage = usage;
            this.timing = timing;
            this.shape = shape;
            this.range = range;
            this.radius = radius;
            this.damage = damage;
            this.triggersReactions = triggersReactions;
            this.description = description ?? string.Empty;
        }

        /// <summary>
        /// Returns success only when required serialized ability data is present and internally valid.
        /// </summary>
        public TacticalResult ValidateDefinition()
        {
            var validationErrors = CollectValidationErrors();
            return validationErrors.Count == 0
                ? TacticalResult.Success()
                : TacticalResult.Failure(string.Join(" ", validationErrors));
        }

        /// <summary>
        /// Lists all validation errors so editor tools can report every problem at once.
        /// </summary>
        public IReadOnlyList<string> CollectValidationErrors()
        {
            return CollectValidationErrors(
                abilityKey,
                displayName,
                apCost,
                usage,
                timing,
                shape,
                range,
                radius,
                damage,
                GetDebugName()).AsReadOnly();
        }

        private void OnValidate()
        {
            apCost = Math.Max(MinimumAPCost, apCost);
            range = Math.Max(MinimumRange, range);
            radius = Math.Max(MinimumRadius, radius);
            damage = Math.Max(MinimumDamage, damage);

            if (description == null)
            {
                description = string.Empty;
            }
        }

        private string GetDebugName()
        {
            return string.IsNullOrWhiteSpace(name) ? "AbilityDefinition" : name;
        }

        private static List<string> CollectValidationErrors(
            string abilityKey,
            string displayName,
            int apCost,
            AbilityUsage usage,
            AbilityTiming timing,
            AbilityShape shape,
            int range,
            int radius,
            int damage,
            string debugName)
        {
            var errors = new List<string>();
            var label = string.IsNullOrWhiteSpace(debugName) ? "AbilityDefinition" : debugName.Trim();

            if (string.IsNullOrWhiteSpace(abilityKey))
            {
                errors.Add($"{label}: ability key cannot be blank.");
            }

            if (string.IsNullOrWhiteSpace(displayName))
            {
                errors.Add($"{label}: display name cannot be blank.");
            }

            if (apCost < MinimumAPCost)
            {
                errors.Add($"{label}: AP cost must be at least {MinimumAPCost}.");
            }

            if (!IsValidUsage(usage))
            {
                errors.Add($"{label}: usage must include Action, Reaction, or Both with no unknown flags.");
            }

            if (!Enum.IsDefined(typeof(AbilityTiming), timing))
            {
                errors.Add($"{label}: timing value '{timing}' is not supported.");
            }

            var hasValidShape = Enum.IsDefined(typeof(AbilityShape), shape);
            if (!hasValidShape)
            {
                errors.Add($"{label}: shape value '{shape}' is not supported.");
            }

            if (range < MinimumRange)
            {
                errors.Add($"{label}: range must be at least {MinimumRange}.");
            }

            if (radius < MinimumRadius)
            {
                errors.Add($"{label}: radius must be at least {MinimumRadius}.");
            }

            if (damage < MinimumDamage)
            {
                errors.Add($"{label}: damage must be at least {MinimumDamage}.");
            }

            if (hasValidShape)
            {
                AddShapeSpecificValidationErrors(shape, range, radius, label, errors);
            }

            return errors;
        }

        private static void AddShapeSpecificValidationErrors(
            AbilityShape shape,
            int range,
            int radius,
            string label,
            ICollection<string> errors)
        {
            switch (shape)
            {
                case AbilityShape.SingleTarget:
                    if (range < 1)
                    {
                        errors.Add($"{label}: single-target abilities require range at least 1.");
                    }

                    break;

                case AbilityShape.Cone:
                    if (range < 1)
                    {
                        errors.Add($"{label}: cone abilities require range at least 1.");
                    }

                    break;

                case AbilityShape.Radius:
                    if (range < 1)
                    {
                        errors.Add($"{label}: radius abilities require range at least 1.");
                    }

                    if (radius < 1)
                    {
                        errors.Add($"{label}: radius abilities require radius at least 1.");
                    }

                    break;
            }
        }

        private static bool SupportsAnyKnownUsage(AbilityUsage value)
        {
            return (value & AllowedUsageMask) != AbilityUsage.None;
        }

        private static bool ContainsOnlyKnownUsageFlags(AbilityUsage value)
        {
            return (value & ~AllowedUsageMask) == AbilityUsage.None;
        }

        private static bool IsValidUsage(AbilityUsage value)
        {
            return SupportsAnyKnownUsage(value) && ContainsOnlyKnownUsageFlags(value);
        }

        private static string NormalizeString(string value)
        {
            return string.IsNullOrWhiteSpace(value) ? string.Empty : value.Trim();
        }
    }
}
