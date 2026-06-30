using System;
using System.Collections.Generic;
using ReactionTactics.Units;
using UnityEngine;

namespace ReactionTactics.Actions
{
    /// <summary>
    /// Component-level catalog of abilities assigned to a tactical unit.
    /// Prototype setup tools and designers can assign the serialized list, while
    /// UI and command systems query filtered action/reaction views.
    /// </summary>
    [DisallowMultipleComponent]
    [RequireComponent(typeof(TacticalUnit))]
    public sealed class UnitAbilityLoadout : MonoBehaviour
    {
        [SerializeField]
        [Tooltip("Ability assets this unit can advertise to active-turn and reaction UI.")]
        private List<AbilityDefinition> abilities = new List<AbilityDefinition>();

        public int AssignedAbilityCount
        {
            get { return GetAssignedAbilities().Count; }
        }

        public IReadOnlyList<AbilityDefinition> GetAssignedAbilities()
        {
            var result = new List<AbilityDefinition>();
            AddUniqueAssignedAbilities(result);
            return result;
        }

        public IReadOnlyList<AbilityDefinition> GetActionAbilities()
        {
            return GetAbilitiesForUsage(AbilityUsage.Action);
        }

        public IReadOnlyList<AbilityDefinition> GetReactionAbilities()
        {
            return GetAbilitiesForUsage(AbilityUsage.Reaction);
        }

        public bool HasAbility(AbilityDefinition ability)
        {
            if (ability == null || abilities == null)
            {
                return false;
            }

            for (var i = 0; i < abilities.Count; i++)
            {
                if (abilities[i] == ability)
                {
                    return true;
                }
            }

            return false;
        }

        /// <summary>
        /// Assigns abilities from editor setup tools or tests. Nulls and duplicate
        /// assets are ignored so advertised abilities stay UI-friendly.
        /// </summary>
        public void SetAbilities(IEnumerable<AbilityDefinition> abilityDefinitions)
        {
            if (abilityDefinitions == null)
            {
                throw new ArgumentNullException(nameof(abilityDefinitions));
            }

            EnsureAbilityList();
            abilities.Clear();

            foreach (var ability in abilityDefinitions)
            {
                AddUniqueAbility(abilities, ability);
            }
        }

        private void OnValidate()
        {
            EnsureAbilityList();
        }

        private IReadOnlyList<AbilityDefinition> GetAbilitiesForUsage(AbilityUsage usage)
        {
            var result = new List<AbilityDefinition>();
            if (abilities == null)
            {
                return result;
            }

            for (var i = 0; i < abilities.Count; i++)
            {
                var ability = abilities[i];
                if (ability != null && ability.SupportsUsage(usage))
                {
                    AddUniqueAbility(result, ability);
                }
            }

            return result;
        }

        private void AddUniqueAssignedAbilities(ICollection<AbilityDefinition> result)
        {
            if (abilities == null)
            {
                return;
            }

            for (var i = 0; i < abilities.Count; i++)
            {
                AddUniqueAbility(result, abilities[i]);
            }
        }

        private void EnsureAbilityList()
        {
            if (abilities == null)
            {
                abilities = new List<AbilityDefinition>();
            }
        }

        private static void AddUniqueAbility(ICollection<AbilityDefinition> result, AbilityDefinition ability)
        {
            if (ability == null || result.Contains(ability))
            {
                return;
            }

            result.Add(ability);
        }
    }
}
