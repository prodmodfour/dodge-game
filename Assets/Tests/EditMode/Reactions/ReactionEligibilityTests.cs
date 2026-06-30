using System;
using System.Collections.Generic;
using NUnit.Framework;
using ReactionTactics.Actions;
using ReactionTactics.Core;
using ReactionTactics.Grid;
using ReactionTactics.Reactions;
using ReactionTactics.Turns;
using ReactionTactics.Units;
using UnityEngine;

public sealed class ReactionEligibilityTests
{
    [Test]
    public void LivingOffTurnUnitWithApCanReactDuringReactionWindow()
    {
        using (var fixture = new ReactionEligibilityFixture())
        {
            var state = fixture.CreateReactionState();
            var service = new ReactionEligibilityService();

            var result = service.CanUnitReact(fixture.Reactor, fixture.Intent, state);

            Assert.That(result.CanReact, Is.True, result.Reason);
            Assert.That(result.IsEligible, Is.True);
            Assert.That(result.ShouldAutoPass, Is.False);
            Assert.That(result.Reason, Does.Contain("AP available"));
            Assert.That(result.ToTacticalResult().IsSuccess, Is.True);
        }
    }

    [Test]
    public void ActorAndCurrentActiveActionUnitCannotReact()
    {
        using (var fixture = new ReactionEligibilityFixture())
        {
            var service = new ReactionEligibilityService();
            var actorState = fixture.CreateReactionState();

            var actorResult = service.CanUnitReact(fixture.Actor, fixture.Intent, actorState);

            Assert.That(actorResult.CanReact, Is.False);
            Assert.That(actorResult.ShouldAutoPass, Is.True);
            Assert.That(actorResult.Reason, Does.Contain("own declared action"));

            var otherActiveUnit = fixture.CreateUnit(
                "Other Active Unit",
                new UnitId(3),
                TeamId.Player,
                new GridPosition(0, 0, 1));
            var invalidActiveState = fixture.CreateReactionState(activeUnit: otherActiveUnit);

            var activeResult = service.CanUnitReact(otherActiveUnit, fixture.Intent, invalidActiveState);

            Assert.That(activeResult.CanReact, Is.False);
            Assert.That(activeResult.ShouldAutoPass, Is.True);
            Assert.That(activeResult.Reason, Does.Contain("current active action unit"));
        }
    }

    [Test]
    public void UnitCannotReactOutsideReactionWindowPhase()
    {
        using (var fixture = new ReactionEligibilityFixture())
        {
            var state = new CombatState(
                1,
                CombatPhase.ActiveTurn,
                fixture.Actor,
                reactingUnit: null,
                pendingActionIntent: fixture.Intent);
            var service = new ReactionEligibilityService();

            var result = service.CanUnitReact(fixture.Reactor, fixture.Intent, state);

            Assert.That(result.CanReact, Is.False);
            Assert.That(result.ShouldAutoPass, Is.True);
            Assert.That(result.Reason, Does.Contain(nameof(CombatPhase.ActiveTurn)));
            Assert.That(result.Reason, Does.Contain(nameof(CombatPhase.ReactionWindow)));
            Assert.That(result.ToTacticalResult().IsFailure, Is.True);
        }
    }

    [Test]
    public void DefeatedUnitCannotReactAndReportsAutoPassReason()
    {
        using (var fixture = new ReactionEligibilityFixture())
        {
            Assert.That(
                fixture.Reactor.ApplyDamage(fixture.Reactor.MaxHP, DamageSource.Environmental("reaction eligibility test")).IsSuccess,
                Is.True);
            var state = fixture.CreateReactionState();
            var service = new ReactionEligibilityService();

            var result = service.CanUnitReact(fixture.Reactor, fixture.Intent, state);

            Assert.That(result.CanReact, Is.False);
            Assert.That(result.ShouldAutoPass, Is.True);
            Assert.That(result.Reason, Does.Contain("defeated"));
        }
    }

    [Test]
    public void ZeroApRequiresZeroCostPassReactionToRemainEligible()
    {
        using (var fixture = new ReactionEligibilityFixture())
        {
            fixture.Reactor.SetAPForTest(0);
            var state = fixture.CreateReactionState();
            var service = new ReactionEligibilityService();

            var noPassResult = service.CanUnitReact(fixture.Reactor, fixture.Intent, state);

            Assert.That(noPassResult.CanReact, Is.False);
            Assert.That(noPassResult.ShouldAutoPass, Is.True);
            Assert.That(noPassResult.Reason, Does.Contain("no zero-cost pass reaction"));

            var passAbility = fixture.CreatePassReactionAbility();
            var loadout = fixture.Reactor.gameObject.AddComponent<UnitAbilityLoadout>();
            loadout.SetAbilities(new[] { passAbility });

            var passOnlyResult = service.CanUnitReact(fixture.Reactor, fixture.Intent, state);

            Assert.That(service.HasZeroCostPassReaction(fixture.Reactor), Is.True);
            Assert.That(passOnlyResult.CanReact, Is.True, passOnlyResult.Reason);
            Assert.That(passOnlyResult.ShouldAutoPass, Is.True);
            Assert.That(passOnlyResult.Reason, Does.Contain("0 AP"));
            Assert.That(passOnlyResult.Reason, Does.Contain("Pass Reaction"));
            Assert.That(passOnlyResult.ToTacticalResult().IsSuccess, Is.True);
        }
    }

    [Test]
    public void StatusEffectRuleCanDisableReactionEligibility()
    {
        using (var fixture = new ReactionEligibilityFixture())
        {
            var state = fixture.CreateReactionState();
            var service = new ReactionEligibilityService(new[]
            {
                new BlockingReactionRule("stunned units cannot react")
            });

            var result = service.CanUnitReact(fixture.Reactor, fixture.Intent, state);

            Assert.That(result.CanReact, Is.False);
            Assert.That(result.ShouldAutoPass, Is.True);
            Assert.That(result.Reason, Does.Contain(fixture.Reactor.DisplayName));
            Assert.That(result.Reason, Does.Contain("stunned units cannot react"));
        }
    }

    [Test]
    public void MissingInputsReturnClearIneligibleResults()
    {
        using (var fixture = new ReactionEligibilityFixture())
        {
            var state = fixture.CreateReactionState();
            var service = new ReactionEligibilityService();

            Assert.That(service.CanUnitReact(null, fixture.Intent, state).CanReact, Is.False);
            Assert.That(service.CanUnitReact(fixture.Reactor, null, state).Reason, Does.Contain("source action intent"));
            Assert.That(service.CanUnitReact(fixture.Reactor, fixture.Intent, null).Reason, Does.Contain("combat state"));
            Assert.Throws<ArgumentNullException>(() => new ReactionEligibilityService(new IReactionEligibilityRule[] { null }));
        }
    }

    private sealed class BlockingReactionRule : IReactionEligibilityRule
    {
        private readonly string reason;

        public BlockingReactionRule(string reason)
        {
            this.reason = reason;
        }

        public TacticalResult CanUnitReact(TacticalUnit unit, ActionIntent intent, CombatState combatState)
        {
            return TacticalResult.Failure(reason);
        }
    }

    private sealed class ReactionEligibilityFixture : IDisposable
    {
        private readonly List<GameObject> unitObjects = new List<GameObject>();
        private readonly List<UnityEngine.Object> assets = new List<UnityEngine.Object>();
        private readonly UnitStatsDefinition stats;
        private readonly AbilityDefinition actionAbility;

        public ReactionEligibilityFixture()
        {
            stats = CreateStats("Reaction Eligibility Unit");
            actionAbility = CreateActionAbility();
            assets.Add(stats);
            assets.Add(actionAbility);

            Actor = CreateUnit("Actor", new UnitId(1), TeamId.Player, GridPosition.Zero);
            Reactor = CreateUnit("Reactor", new UnitId(2), TeamId.Enemy, new GridPosition(1, 0, 0));
            Intent = new ActionIntent(
                Actor,
                actionAbility,
                Actor.CurrentGridPosition,
                ActionTarget.ForUnit(Reactor),
                new[] { Reactor.CurrentGridPosition },
                declarationRound: 1,
                declarationSequence: 0);
        }

        public TacticalUnit Actor { get; }

        public TacticalUnit Reactor { get; }

        public ActionIntent Intent { get; }

        public TacticalUnit CreateUnit(string name, UnitId unitId, TeamId team, GridPosition position)
        {
            var gameObject = new GameObject(name);
            unitObjects.Add(gameObject);
            var unit = gameObject.AddComponent<TacticalUnit>();
            unit.Initialize(unitId, team, stats, position);
            return unit;
        }

        public CombatState CreateReactionState(TacticalUnit activeUnit = null)
        {
            return new CombatState(
                1,
                CombatPhase.ReactionWindow,
                activeUnit ?? Actor,
                Reactor,
                Intent);
        }

        public AbilityDefinition CreatePassReactionAbility()
        {
            var ability = ScriptableObject.CreateInstance<AbilityDefinition>();
            ability.Configure(
                ReactionEligibilityService.DefaultPassReactionAbilityKey,
                ReactionEligibilityService.DefaultPassReactionDisplayName,
                apCost: 0,
                usage: AbilityUsage.Reaction,
                timing: AbilityTiming.Immediate,
                shape: AbilityShape.Self,
                range: 0,
                radius: 0,
                damage: 0,
                triggersReactions: false);
            assets.Add(ability);
            return ability;
        }

        public void Dispose()
        {
            for (var i = unitObjects.Count - 1; i >= 0; i -= 1)
            {
                UnityEngine.Object.DestroyImmediate(unitObjects[i]);
            }

            for (var i = assets.Count - 1; i >= 0; i -= 1)
            {
                UnityEngine.Object.DestroyImmediate(assets[i]);
            }
        }

        private static UnitStatsDefinition CreateStats(string displayName)
        {
            var stats = ScriptableObject.CreateInstance<UnitStatsDefinition>();
            stats.Configure(
                displayName,
                maxHP: 10,
                maxAP: 6,
                movementAnimationSpeed: 4f,
                meleeRange: 1,
                teamColorHint: Color.white);
            return stats;
        }

        private static AbilityDefinition CreateActionAbility()
        {
            var ability = ScriptableObject.CreateInstance<AbilityDefinition>();
            ability.Configure(
                "melee_slash",
                "Melee Slash",
                apCost: 3,
                usage: AbilityUsage.Action,
                timing: AbilityTiming.Telegraphed,
                shape: AbilityShape.Melee,
                range: 1,
                radius: 0,
                damage: 4,
                triggersReactions: true);
            return ability;
        }
    }
}
