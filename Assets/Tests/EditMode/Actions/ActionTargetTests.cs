using System;
using NUnit.Framework;
using ReactionTactics.Actions;
using ReactionTactics.Grid;
using ReactionTactics.Units;
using UnityEngine;

namespace ReactionTactics.Tests.EditMode.Actions
{
    public sealed class ActionTargetTests
    {
        [Test]
        public void NoneHasNoOptionalTargetDataAndIsValidForSelfShape()
        {
            var target = ActionTarget.None;

            Assert.That(target.IsEmpty, Is.True);
            Assert.That(target.HasTargetUnit, Is.False);
            Assert.That(target.HasTargetCell, Is.False);
            Assert.That(target.HasDirection, Is.False);
            Assert.That(target.ValidateForShape(AbilityShape.Self).IsSuccess, Is.True);
            Assert.That(target.ValidateForShape(AbilityShape.Radius).IsFailure, Is.True);
        }

        [Test]
        public void CellTargetStoresOnlyTargetCellAndValidatesRadiusShape()
        {
            var cell = new GridPosition(3, 1, 4);
            var target = ActionTarget.ForCell(cell);

            Assert.That(target.IsEmpty, Is.False);
            Assert.That(target.HasTargetCell, Is.True);
            Assert.That(target.TargetCell, Is.EqualTo(cell));
            Assert.That(target.HasTargetUnit, Is.False);
            Assert.That(target.HasDirection, Is.False);
            Assert.That(target.ValidateForShape(AbilityShape.Radius).IsSuccess, Is.True);
            Assert.That(target.ValidateForShape(AbilityShape.Melee).IsFailure, Is.True);
        }

        [Test]
        public void UnitTargetStoresUnitAndCapturedTargetCell()
        {
            var gameObject = new GameObject("Action Target Unit");
            var stats = CreateStats();

            try
            {
                var unit = gameObject.AddComponent<TacticalUnit>();
                var declaredCell = new GridPosition(2, 0, 5);
                unit.Initialize(new UnitId(17), TeamId.Enemy, stats, declaredCell);

                var target = ActionTarget.ForUnit(unit);
                unit.SetGridPosition(new GridPosition(4, 0, 5));

                Assert.That(target.HasTargetUnit, Is.True);
                Assert.That(target.TargetUnit, Is.SameAs(unit));
                Assert.That(target.HasTargetCell, Is.True);
                Assert.That(target.TargetCell, Is.EqualTo(declaredCell));
                Assert.That(unit.CurrentGridPosition, Is.Not.EqualTo(target.TargetCell));
                Assert.That(target.ValidateForShape(AbilityShape.SingleTarget).IsSuccess, Is.True);
                Assert.That(target.ValidateForShape(AbilityShape.Melee).IsSuccess, Is.True);
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(gameObject);
                UnityEngine.Object.DestroyImmediate(stats);
            }
        }

        [Test]
        public void UnitTargetCanUseExplicitCapturedCell()
        {
            var gameObject = new GameObject("Explicit Action Target Unit");
            var stats = CreateStats();

            try
            {
                var unit = gameObject.AddComponent<TacticalUnit>();
                unit.Initialize(new UnitId(18), TeamId.Enemy, stats, new GridPosition(2, 0, 5));
                var explicitCell = new GridPosition(2, 1, 5);

                var target = ActionTarget.ForUnit(unit, explicitCell);

                Assert.That(target.TargetUnit, Is.SameAs(unit));
                Assert.That(target.TargetCell, Is.EqualTo(explicitCell));
                Assert.That(target.ValidateForShape(AbilityShape.Melee).IsSuccess, Is.True);
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(gameObject);
                UnityEngine.Object.DestroyImmediate(stats);
            }
        }

        [Test]
        public void ConeTargetRequiresCellAndCanCarryOptionalDirection()
        {
            var cell = new GridPosition(5, 0, 3);
            var cellOnlyTarget = ActionTarget.ForCell(cell);
            var coneTarget = ActionTarget.ForCellAndDirection(cell, CardinalDirection.East);

            Assert.That(cellOnlyTarget.ValidateForShape(AbilityShape.Cone).IsSuccess, Is.True);
            Assert.That(cellOnlyTarget.HasDirection, Is.False);
            Assert.That(coneTarget.HasTargetCell, Is.True);
            Assert.That(coneTarget.TargetCell, Is.EqualTo(cell));
            Assert.That(coneTarget.HasDirection, Is.True);
            Assert.That(coneTarget.Direction, Is.EqualTo(CardinalDirection.East));
            Assert.That(coneTarget.ValidateForShape(AbilityShape.Cone).IsSuccess, Is.True);
        }

        [Test]
        public void WithDirectionPreservesExistingTargetData()
        {
            var baseTarget = ActionTarget.ForCell(new GridPosition(1, 0, 2));

            var target = baseTarget.WithDirection(CardinalDirection.North);

            Assert.That(target.HasTargetCell, Is.True);
            Assert.That(target.TargetCell, Is.EqualTo(baseTarget.TargetCell));
            Assert.That(target.HasDirection, Is.True);
            Assert.That(target.Direction, Is.EqualTo(CardinalDirection.North));
            Assert.That(target.ValidateForShape(AbilityShape.Cone).IsSuccess, Is.True);
        }

        [Test]
        public void ValidationReportsMissingDataForEachShape()
        {
            var noTarget = ActionTarget.None;
            var cellTarget = ActionTarget.ForCell(new GridPosition(1, 0, 1));

            Assert.That(noTarget.ValidateForShape(AbilityShape.SingleTarget).ErrorMessage, Does.Contain("target unit"));
            Assert.That(noTarget.ValidateForShape(AbilityShape.Melee).ErrorMessage, Does.Contain("target unit"));
            Assert.That(noTarget.ValidateForShape(AbilityShape.Radius).ErrorMessage, Does.Contain("target cell"));
            Assert.That(noTarget.ValidateForShape(AbilityShape.Cone).ErrorMessage, Does.Contain("target cell"));
            Assert.That(cellTarget.ValidateForShape(AbilityShape.SingleTarget).IsFailure, Is.True);
        }

        [Test]
        public void InvalidInputsAreRejected()
        {
            Assert.Throws<ArgumentNullException>(() => ActionTarget.ForUnit(null));
            Assert.Throws<ArgumentNullException>(() => ActionTarget.ForUnit(null, GridPosition.Zero));
            Assert.Throws<ArgumentOutOfRangeException>(() => ActionTarget.ForCellAndDirection(GridPosition.Zero, (CardinalDirection)999));
            Assert.Throws<ArgumentOutOfRangeException>(() => ActionTarget.ForCell(GridPosition.Zero).WithDirection((CardinalDirection)999));

            var invalidShapeResult = ActionTarget.None.ValidateForShape((AbilityShape)999);
            Assert.That(invalidShapeResult.IsFailure, Is.True);
            Assert.That(invalidShapeResult.ErrorMessage, Does.Contain("not supported"));
        }

        private static UnitStatsDefinition CreateStats()
        {
            var stats = ScriptableObject.CreateInstance<UnitStatsDefinition>();
            stats.Configure(
                displayName: "Action Target Test Unit",
                maxHP: 10,
                maxAP: 6,
                movementAnimationSpeed: 4f,
                meleeRange: 1,
                teamColorHint: Color.white);
            return stats;
        }
    }
}
