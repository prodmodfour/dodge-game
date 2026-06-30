using System;
using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using ReactionTactics.Actions;
using ReactionTactics.Grid;
using ReactionTactics.Pathfinding;
using ReactionTactics.Reactions;
using ReactionTactics.Units;
using UnityEngine;

public sealed class ReactionSafetyAnalyzerTests
{
    [Test]
    public void MeleeTargetIsSafeOnlyOutsideActorMeleeRange()
    {
        using (var fixture = new Fixture())
        {
            var actor = fixture.CreateUnit("Safety Melee Actor", new UnitId(1), TeamId.Player, GridPosition.Zero, meleeRange: 2);
            var target = fixture.CreateUnit("Safety Melee Target", new UnitId(2), TeamId.Enemy, GridPosition.East);
            var ability = fixture.CreateAbility("melee_slash", "Melee Slash", AbilityShape.Melee);
            var intent = fixture.CreateIntent(actor, ability, ActionTarget.ForUnit(target), new[] { target.CurrentGridPosition });
            var reachableCells = new[]
            {
                new ReachableCell(new GridPosition(2, 0, 0), totalApCost: 1),
                new ReachableCell(new GridPosition(3, 0, 0), totalApCost: 2),
            };

            var results = new ReactionSafetyAnalyzer()
                .ClassifyReachableCells(target, intent, reachableCells)
                .ToDictionary(cell => cell.Position);

            Assert.That(results[new GridPosition(2, 0, 0)].IsThreatened, Is.True);
            Assert.That(results[new GridPosition(2, 0, 0)].TotalApCost, Is.EqualTo(1));
            Assert.That(results[new GridPosition(2, 0, 0)].Reason, Does.Contain("within melee range 2"));

            Assert.That(results[new GridPosition(3, 0, 0)].IsSafe, Is.True);
            Assert.That(results[new GridPosition(3, 0, 0)].TotalApCost, Is.EqualTo(2));
            Assert.That(results[new GridPosition(3, 0, 0)].Reason, Does.Contain("outside melee range 2"));
        }
    }

    [Test]
    public void MeleeNonTargetReactorIsSafeBecauseDeclaredMeleeDoesNotRetarget()
    {
        using (var fixture = new Fixture())
        {
            var actor = fixture.CreateUnit("Safety Melee Actor", new UnitId(1), TeamId.Player, GridPosition.Zero);
            var declaredTarget = fixture.CreateUnit("Safety Melee Target", new UnitId(2), TeamId.Enemy, GridPosition.East);
            var otherReactor = fixture.CreateUnit("Safety Other Reactor", new UnitId(3), TeamId.Enemy, GridPosition.North);
            var ability = fixture.CreateAbility("melee_slash", "Melee Slash", AbilityShape.Melee);
            var intent = fixture.CreateIntent(actor, ability, ActionTarget.ForUnit(declaredTarget), new[] { declaredTarget.CurrentGridPosition });
            var reachableCells = new[]
            {
                new ReachableCell(GridPosition.East, totalApCost: 1),
                new ReachableCell(new GridPosition(2, 0, 0), totalApCost: 2),
            };

            var results = new ReactionSafetyAnalyzer().ClassifyReachableCells(otherReactor, intent, reachableCells);

            Assert.That(results, Has.Count.EqualTo(2));
            Assert.That(results.All(cell => cell.IsSafe), Is.True);
            Assert.That(results.All(cell => cell.Reason.Contains("not the declared target")), Is.True);
        }
    }

    [Test]
    public void ConeSafetyUsesDeclaredAffectedCells()
    {
        using (var fixture = new Fixture())
        {
            var actor = fixture.CreateUnit("Safety Cone Actor", new UnitId(1), TeamId.Player, GridPosition.Zero);
            var reactor = fixture.CreateUnit("Safety Cone Reactor", new UnitId(2), TeamId.Enemy, new GridPosition(0, 0, 2));
            var ability = fixture.CreateAbility("cone_shot", "Cone Shot", AbilityShape.Cone, range: 4);
            var declaredCells = new[]
            {
                new GridPosition(0, 0, 1),
                new GridPosition(-1, 0, 2),
                new GridPosition(0, 0, 2),
                new GridPosition(1, 0, 2),
            };
            var intent = fixture.CreateIntent(
                actor,
                ability,
                ActionTarget.ForCellAndDirection(new GridPosition(0, 0, 4), CardinalDirection.North),
                declaredCells);
            var reachableCells = new[]
            {
                new ReachableCell(new GridPosition(0, 0, 2), totalApCost: 0),
                new ReachableCell(new GridPosition(2, 0, 2), totalApCost: 1),
            };

            var results = new ReactionSafetyAnalyzer()
                .ClassifyReachableCells(reactor, intent, reachableCells)
                .ToDictionary(cell => cell.Position);

            Assert.That(results[new GridPosition(0, 0, 2)].IsThreatened, Is.True);
            Assert.That(results[new GridPosition(0, 0, 2)].Reason, Does.Contain("inside the declared cone"));
            Assert.That(results[new GridPosition(2, 0, 2)].IsSafe, Is.True);
            Assert.That(results[new GridPosition(2, 0, 2)].Reason, Does.Contain("outside the declared cone"));
        }
    }

    [Test]
    public void RadiusSafetyUsesDeclaredAffectedCellsAndProvidesReasons()
    {
        using (var fixture = new Fixture())
        {
            var actor = fixture.CreateUnit("Safety AoE Actor", new UnitId(1), TeamId.Player, GridPosition.Zero);
            var reactor = fixture.CreateUnit("Safety AoE Reactor", new UnitId(2), TeamId.Enemy, new GridPosition(2, 0, 1));
            var ability = fixture.CreateAbility("fireball", "Fireball", AbilityShape.Radius, range: 5, radius: 1);
            var declaredCells = new[]
            {
                new GridPosition(2, 0, 1),
                new GridPosition(1, 0, 1),
                new GridPosition(3, 0, 1),
                new GridPosition(2, 0, 0),
                new GridPosition(2, 0, 2),
            };
            var intent = fixture.CreateIntent(actor, ability, ActionTarget.ForCell(new GridPosition(2, 0, 1)), declaredCells);
            var reachableCells = new[]
            {
                new ReachableCell(new GridPosition(2, 0, 2), totalApCost: 1),
                new ReachableCell(new GridPosition(4, 0, 1), totalApCost: 2),
            };

            var results = new ReactionSafetyAnalyzer()
                .ClassifyReachableCells(reactor, intent, reachableCells)
                .ToDictionary(cell => cell.Position);

            Assert.That(results[new GridPosition(2, 0, 2)].Status, Is.EqualTo(ReactionSafetyStatus.Threatened));
            Assert.That(results[new GridPosition(2, 0, 2)].Reason, Does.Contain("inside the declared AoE"));
            Assert.That(results[new GridPosition(4, 0, 1)].Status, Is.EqualTo(ReactionSafetyStatus.Safe));
            Assert.That(results[new GridPosition(4, 0, 1)].Reason, Does.Contain("outside the declared AoE"));
            Assert.That(results.Values.All(cell => !string.IsNullOrWhiteSpace(cell.Reason)), Is.True);
        }
    }

    [Test]
    public void MissingInputsAreRejectedClearly()
    {
        using (var fixture = new Fixture())
        {
            var actor = fixture.CreateUnit("Safety Input Actor", new UnitId(1), TeamId.Player, GridPosition.Zero);
            var reactor = fixture.CreateUnit("Safety Input Reactor", new UnitId(2), TeamId.Enemy, GridPosition.East);
            var ability = fixture.CreateAbility("melee_slash", "Melee Slash", AbilityShape.Melee);
            var intent = fixture.CreateIntent(actor, ability, ActionTarget.ForUnit(reactor), new[] { reactor.CurrentGridPosition });
            var analyzer = new ReactionSafetyAnalyzer();

            Assert.Throws<ArgumentNullException>(() => analyzer.ClassifyReachableCells(null, intent, Array.Empty<ReachableCell>()));
            Assert.Throws<ArgumentNullException>(() => analyzer.ClassifyReachableCells(reactor, null, Array.Empty<ReachableCell>()));
            Assert.Throws<ArgumentNullException>(() => analyzer.ClassifyReachableCells(reactor, intent, null));
        }
    }

    private sealed class Fixture : IDisposable
    {
        private readonly List<GameObject> unitObjects = new List<GameObject>();
        private readonly List<UnityEngine.Object> assets = new List<UnityEngine.Object>();

        public TacticalUnit CreateUnit(
            string displayName,
            UnitId unitId,
            TeamId team,
            GridPosition position,
            int meleeRange = 1)
        {
            var stats = ScriptableObject.CreateInstance<UnitStatsDefinition>();
            stats.Configure(
                displayName,
                maxHP: 10,
                maxAP: 6,
                movementAnimationSpeed: 4f,
                meleeRange: meleeRange,
                teamColorHint: Color.white);
            assets.Add(stats);

            var gameObject = new GameObject(displayName);
            var unit = gameObject.AddComponent<TacticalUnit>();
            unit.Initialize(unitId, team, stats, position);
            unitObjects.Add(gameObject);
            return unit;
        }

        public AbilityDefinition CreateAbility(
            string abilityKey,
            string displayName,
            AbilityShape shape,
            int range = 0,
            int radius = 0)
        {
            var ability = ScriptableObject.CreateInstance<AbilityDefinition>();
            ability.Configure(
                abilityKey,
                displayName,
                apCost: 0,
                usage: AbilityUsage.Action,
                timing: AbilityTiming.Telegraphed,
                shape: shape,
                range: range,
                radius: radius,
                damage: 4,
                triggersReactions: true,
                description: "Reaction safety analyzer test ability.");
            assets.Add(ability);
            return ability;
        }

        public ActionIntent CreateIntent(
            TacticalUnit actor,
            AbilityDefinition ability,
            ActionTarget target,
            IEnumerable<GridPosition> declaredAffectedCells)
        {
            return new ActionIntent(
                actor,
                ability,
                actor.CurrentGridPosition,
                target,
                declaredAffectedCells,
                declarationRound: 1,
                declarationSequence: 0);
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
    }
}
