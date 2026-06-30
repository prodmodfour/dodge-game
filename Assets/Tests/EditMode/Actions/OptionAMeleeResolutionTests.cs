using System;
using System.Text.RegularExpressions;
using NUnit.Framework;
using ReactionTactics.Actions;
using ReactionTactics.Grid;
using ReactionTactics.Turns;
using ReactionTactics.Units;
using UnityEngine;
using UnityEngine.TestTools;

namespace ReactionTactics.Tests.EditMode.Actions
{
    public sealed class OptionAMeleeResolutionTests
    {
        [Test]
        public void MeleeHitsAutomaticallyWhenTargetRemainsInFinalRange()
        {
            using (var battle = new TestBattle())
            {
                var ability = CreateMeleeAbility(damage: 4);

                try
                {
                    var intent = CreateMeleeIntent(battle, ability);
                    var hpEvents = 0;
                    var resolvedEvents = 0;
                    var observedHpEvent = default(HitPointsChangedEvent);
                    battle.EventBus.HitPointsChanged += eventData =>
                    {
                        hpEvents += 1;
                        observedHpEvent = eventData;
                    };

                    battle.EventBus.ActionResolved += _ => resolvedEvents += 1;

                    var resolver = new ActionResolver(battle.EventBus, battle.BusObject, logResolutions: false);
                    var result = resolver.Resolve(intent);

                    Assert.That(result.IsSuccess, Is.True, result.ErrorMessage);
                    Assert.That(battle.Enemy.CurrentHP, Is.EqualTo(6));
                    Assert.That(battle.Enemy.IsAlive, Is.True);
                    Assert.That(hpEvents, Is.EqualTo(1));
                    Assert.That(observedHpEvent.Unit, Is.SameAs(battle.Enemy));
                    Assert.That(observedHpEvent.PreviousHP, Is.EqualTo(10));
                    Assert.That(observedHpEvent.CurrentHP, Is.EqualTo(6));
                    Assert.That(observedHpEvent.Source.SourceUnitId, Is.EqualTo(battle.Actor.UnitId));
                    Assert.That(observedHpEvent.Source.Description, Is.EqualTo("Melee Slash"));
                    Assert.That(resolvedEvents, Is.EqualTo(1));
                }
                finally
                {
                    Destroy(ability);
                }
            }
        }

        [Test]
        public void MeleeDoesNotHitWhenTargetMovedOutOfFinalRange()
        {
            using (var battle = new TestBattle())
            {
                var ability = CreateMeleeAbility(damage: 4);

                try
                {
                    var intent = CreateMeleeIntent(battle, ability);
                    battle.Enemy.SetGridPosition(new GridPosition(2, 0, 0));
                    var hpEvents = 0;
                    var resolvedEvents = 0;
                    battle.EventBus.HitPointsChanged += _ => hpEvents += 1;
                    battle.EventBus.ActionResolved += _ => resolvedEvents += 1;

                    var resolver = new ActionResolver(battle.EventBus, battle.BusObject, logResolutions: false);
                    var result = resolver.Resolve(intent);

                    Assert.That(result.IsSuccess, Is.True, result.ErrorMessage);
                    Assert.That(battle.Enemy.CurrentHP, Is.EqualTo(battle.Enemy.MaxHP));
                    Assert.That(battle.Enemy.IsAlive, Is.True);
                    Assert.That(hpEvents, Is.EqualTo(0));
                    Assert.That(resolvedEvents, Is.EqualTo(1));
                }
                finally
                {
                    Destroy(ability);
                }
            }
        }

        [Test]
        public void MeleePresentationFacesActorTowardTargetOnHit()
        {
            using (var battle = new TestBattle())
            {
                var ability = CreateMeleeAbility(damage: 4);

                try
                {
                    battle.Actor.transform.rotation = Quaternion.LookRotation(Vector3.forward, Vector3.up);
                    var intent = CreateMeleeIntent(battle, ability);

                    var resolver = new ActionResolver(battle.EventBus, battle.BusObject, logResolutions: false);
                    var result = resolver.Resolve(intent);

                    Assert.That(result.IsSuccess, Is.True, result.ErrorMessage);
                    AssertFaces(battle.Actor.transform, Vector3.right);
                }
                finally
                {
                    Destroy(ability);
                }
            }
        }

        [Test]
        public void MeleePresentationFacesActorTowardFinalTargetCellWhenAvoided()
        {
            using (var battle = new TestBattle())
            {
                var ability = CreateMeleeAbility(damage: 4);

                try
                {
                    battle.Actor.transform.rotation = Quaternion.LookRotation(Vector3.right, Vector3.up);
                    var intent = CreateMeleeIntent(battle, ability);
                    battle.Enemy.SetGridPosition(new GridPosition(0, 0, -2));

                    var resolver = new ActionResolver(battle.EventBus, battle.BusObject, logResolutions: false);
                    var result = resolver.Resolve(intent);

                    Assert.That(result.IsSuccess, Is.True, result.ErrorMessage);
                    AssertFaces(battle.Actor.transform, Vector3.back);
                }
                finally
                {
                    Destroy(ability);
                }
            }
        }

        [Test]
        public void MeleeResolutionWritesCombatLogOutcome()
        {
            using (var battle = new TestBattle())
            {
                var ability = CreateMeleeAbility(damage: 4);

                try
                {
                    var intent = CreateMeleeIntent(battle, ability);
                    LogAssert.Expect(
                        LogType.Log,
                        new Regex(@"\[Combat Log\].*resolved melee 'Melee Slash'.*: hit for 4 damage"));

                    var resolver = new ActionResolver(battle.EventBus, battle.BusObject, logResolutions: true);
                    var result = resolver.Resolve(intent);

                    Assert.That(result.IsSuccess, Is.True, result.ErrorMessage);
                }
                finally
                {
                    Destroy(ability);
                }
            }
        }

        [Test]
        public void MeleeUsesActorMeleeRangeAtFinalResolution()
        {
            using (var battle = new TestBattle(actorMeleeRange: 2))
            {
                var ability = CreateMeleeAbility(damage: 4);

                try
                {
                    var intent = CreateMeleeIntent(battle, ability);
                    battle.Enemy.SetGridPosition(new GridPosition(2, 0, 0));

                    var resolver = new ActionResolver(battle.EventBus, battle.BusObject, logResolutions: false);
                    var result = resolver.Resolve(intent);

                    Assert.That(result.IsSuccess, Is.True, result.ErrorMessage);
                    Assert.That(battle.Enemy.CurrentHP, Is.EqualTo(6));
                }
                finally
                {
                    Destroy(ability);
                }
            }
        }

        [Test]
        public void MeleeDoesNotDamageTargetThatIsAlreadyDefeatedAtResolution()
        {
            using (var battle = new TestBattle())
            {
                var ability = CreateMeleeAbility(damage: 4);

                try
                {
                    var intent = CreateMeleeIntent(battle, ability);
                    battle.Enemy.ApplyDamage(99, DamageSource.Environmental("Test setup defeat"));
                    var hpEvents = 0;
                    battle.EventBus.HitPointsChanged += _ => hpEvents += 1;

                    var resolver = new ActionResolver(battle.EventBus, battle.BusObject, logResolutions: false);
                    var result = resolver.Resolve(intent);

                    Assert.That(result.IsSuccess, Is.True, result.ErrorMessage);
                    Assert.That(battle.Enemy.CurrentHP, Is.EqualTo(0));
                    Assert.That(battle.Enemy.IsDead, Is.True);
                    Assert.That(hpEvents, Is.EqualTo(0));
                }
                finally
                {
                    Destroy(ability);
                }
            }
        }

        [Test]
        public void MeleeResolutionRejectsIntentWithoutDeclaredTargetUnit()
        {
            using (var battle = new TestBattle())
            {
                var ability = CreateMeleeAbility(damage: 4);

                try
                {
                    var intent = new ActionIntent(
                        battle.Actor,
                        ability,
                        battle.Actor.CurrentGridPosition,
                        ActionTarget.None,
                        Array.Empty<GridPosition>(),
                        declarationRound: 1,
                        declarationSequence: 0);

                    var resolver = new ActionResolver(battle.EventBus, battle.BusObject, logResolutions: false);
                    var result = resolver.Resolve(intent);

                    Assert.That(result.IsFailure, Is.True);
                    Assert.That(result.ErrorMessage, Does.Contain("no declared target unit"));
                    Assert.That(battle.Enemy.CurrentHP, Is.EqualTo(battle.Enemy.MaxHP));
                }
                finally
                {
                    Destroy(ability);
                }
            }
        }

        private static ActionIntent CreateMeleeIntent(TestBattle battle, AbilityDefinition ability)
        {
            return new ActionIntent(
                battle.Actor,
                ability,
                battle.Actor.CurrentGridPosition,
                ActionTarget.ForUnit(battle.Enemy),
                new[] { battle.Enemy.CurrentGridPosition },
                declarationRound: 1,
                declarationSequence: 0);
        }

        private static AbilityDefinition CreateMeleeAbility(int damage)
        {
            var ability = ScriptableObject.CreateInstance<AbilityDefinition>();
            ability.Configure(
                abilityKey: "melee_slash",
                displayName: "Melee Slash",
                apCost: 3,
                usage: AbilityUsage.Action,
                timing: AbilityTiming.Telegraphed,
                shape: AbilityShape.Melee,
                range: 0,
                radius: 0,
                damage: damage,
                triggersReactions: true,
                description: "Option A melee resolution test ability.");
            return ability;
        }

        private static UnitStatsDefinition CreateStats(string displayName, int meleeRange)
        {
            var stats = ScriptableObject.CreateInstance<UnitStatsDefinition>();
            stats.Configure(
                displayName: displayName,
                maxHP: 10,
                maxAP: 6,
                movementAnimationSpeed: 4f,
                meleeRange: meleeRange,
                teamColorHint: Color.white);
            return stats;
        }

        private static void AssertFaces(Transform transform, Vector3 expectedForward)
        {
            var actualForward = transform.forward;
            actualForward.y = 0f;
            actualForward.Normalize();

            Assert.That(Vector3.Dot(actualForward, expectedForward.normalized), Is.GreaterThan(0.99f));
        }

        private static void Destroy(UnityEngine.Object asset)
        {
            if (asset != null)
            {
                UnityEngine.Object.DestroyImmediate(asset);
            }
        }

        private sealed class TestBattle : IDisposable
        {
            private readonly GameObject actorObject;
            private readonly GameObject enemyObject;
            private readonly UnitStatsDefinition actorStats;
            private readonly UnitStatsDefinition enemyStats;

            public TestBattle(int actorMeleeRange = UnitStatsDefinition.MinimumMeleeRange)
            {
                BusObject = new GameObject("Option A Melee Event Bus");
                EventBus = BusObject.AddComponent<CombatEventBus>();
                actorObject = new GameObject("Option A Melee Actor");
                enemyObject = new GameObject("Option A Melee Enemy");
                actorStats = CreateStats("Actor", actorMeleeRange);
                enemyStats = CreateStats("Enemy", UnitStatsDefinition.MinimumMeleeRange);

                Actor = actorObject.AddComponent<TacticalUnit>();
                Enemy = enemyObject.AddComponent<TacticalUnit>();
                Actor.Initialize(new UnitId(1), TeamId.Player, actorStats, GridPosition.Zero);
                Enemy.Initialize(new UnitId(2), TeamId.Enemy, enemyStats, GridPosition.East);
            }

            public GameObject BusObject { get; }

            public CombatEventBus EventBus { get; }

            public TacticalUnit Actor { get; }

            public TacticalUnit Enemy { get; }

            public void Dispose()
            {
                Destroy(enemyObject);
                Destroy(actorObject);
                Destroy(BusObject);
                Destroy(enemyStats);
                Destroy(actorStats);
            }
        }
    }
}
