using System;
using System.Collections.Generic;
using NUnit.Framework;
using ReactionTactics.Actions;
using ReactionTactics.Units;
using UnityEngine;

namespace ReactionTactics.Tests.EditMode.Actions
{
    public sealed class UnitAbilityLoadoutTests
    {
        [Test]
        public void GetActionAbilitiesReturnsActionAndBothAbilitiesInAssignedOrder()
        {
            var fixture = new LoadoutFixture();

            try
            {
                var move = CreateAbility("move", "Move", AbilityUsage.Both);
                var melee = CreateAbility("melee_slash", "Melee Slash", AbilityUsage.Action);
                var brace = CreateAbility("brace", "Brace", AbilityUsage.Reaction);
                fixture.Track(move, melee, brace);

                fixture.Loadout.SetAbilities(new[] { move, brace, melee });

                var actions = fixture.Loadout.GetActionAbilities();

                Assert.That(actions, Has.Count.EqualTo(2));
                Assert.That(actions[0], Is.SameAs(move));
                Assert.That(actions[1], Is.SameAs(melee));
            }
            finally
            {
                fixture.Dispose();
            }
        }

        [Test]
        public void GetReactionAbilitiesReturnsReactionAndBothAbilitiesInAssignedOrder()
        {
            var fixture = new LoadoutFixture();

            try
            {
                var move = CreateAbility("move", "Move", AbilityUsage.Both);
                var melee = CreateAbility("melee_slash", "Melee Slash", AbilityUsage.Action);
                var pass = CreateAbility("pass_reaction", "Pass Reaction", AbilityUsage.Reaction);
                fixture.Track(move, melee, pass);

                fixture.Loadout.SetAbilities(new[] { move, melee, pass });

                var reactions = fixture.Loadout.GetReactionAbilities();

                Assert.That(reactions, Has.Count.EqualTo(2));
                Assert.That(reactions[0], Is.SameAs(move));
                Assert.That(reactions[1], Is.SameAs(pass));
            }
            finally
            {
                fixture.Dispose();
            }
        }

        [Test]
        public void SetAbilitiesIgnoresNullsAndDuplicateAssets()
        {
            var fixture = new LoadoutFixture();

            try
            {
                var move = CreateAbility("move", "Move", AbilityUsage.Both);
                var brace = CreateAbility("brace", "Brace", AbilityUsage.Reaction);
                fixture.Track(move, brace);

                fixture.Loadout.SetAbilities(new[] { move, null, brace, move });

                var assigned = fixture.Loadout.GetAssignedAbilities();

                Assert.That(assigned, Has.Count.EqualTo(2));
                Assert.That(assigned[0], Is.SameAs(move));
                Assert.That(assigned[1], Is.SameAs(brace));
                Assert.That(fixture.Loadout.AssignedAbilityCount, Is.EqualTo(2));
                Assert.That(fixture.Loadout.HasAbility(move), Is.True);
                Assert.That(fixture.Loadout.HasAbility(null), Is.False);
            }
            finally
            {
                fixture.Dispose();
            }
        }

        [Test]
        public void LoadoutRequiresTacticalUnitComponent()
        {
            var gameObject = new GameObject("Loadout Requirement Test");

            try
            {
                gameObject.AddComponent<UnitAbilityLoadout>();

                Assert.That(gameObject.GetComponent<TacticalUnit>(), Is.Not.Null);
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(gameObject);
            }
        }

        [Test]
        public void SetAbilitiesRejectsNullEnumerable()
        {
            var fixture = new LoadoutFixture();

            try
            {
                Assert.Throws<ArgumentNullException>(() => fixture.Loadout.SetAbilities(null));
            }
            finally
            {
                fixture.Dispose();
            }
        }

        private static AbilityDefinition CreateAbility(string abilityKey, string displayName, AbilityUsage usage)
        {
            var ability = ScriptableObject.CreateInstance<AbilityDefinition>();
            ability.Configure(
                abilityKey,
                displayName,
                0,
                usage,
                AbilityTiming.Immediate,
                AbilityShape.Self,
                0,
                0,
                0,
                false);
            return ability;
        }

        private sealed class LoadoutFixture : IDisposable
        {
            private readonly GameObject gameObject;
            private readonly List<AbilityDefinition> abilities = new List<AbilityDefinition>();

            public LoadoutFixture()
            {
                gameObject = new GameObject("Unit Ability Loadout Test");
                Loadout = gameObject.AddComponent<UnitAbilityLoadout>();
            }

            public UnitAbilityLoadout Loadout { get; }

            public void Track(params AbilityDefinition[] definitions)
            {
                abilities.AddRange(definitions);
            }

            public void Dispose()
            {
                for (var i = 0; i < abilities.Count; i++)
                {
                    if (abilities[i] != null)
                    {
                        UnityEngine.Object.DestroyImmediate(abilities[i]);
                    }
                }

                UnityEngine.Object.DestroyImmediate(gameObject);
            }
        }
    }
}
