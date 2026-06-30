using NUnit.Framework;
using ReactionTactics.Grid;
using ReactionTactics.Input;
using ReactionTactics.Units;
using UnityEngine;

namespace ReactionTactics.Tests.EditMode.Input
{
    public sealed class SelectionControllerTests
    {
        [Test]
        public void SelectionControllerStoresCurrentUnitActionHoverAndTarget()
        {
            var controllerObject = new GameObject("Selection Controller Test");
            var actorObject = new GameObject("Actor");
            var targetObject = new GameObject("Target");
            var actorStats = CreateStats("Knight");
            var targetStats = CreateStats("Goblin");

            try
            {
                var controller = controllerObject.AddComponent<SelectionController>();
                var actor = CreateUnit(actorObject, new UnitId(1), TeamId.Player, actorStats, new GridPosition(1, 0, 1));
                var target = CreateUnit(targetObject, new UnitId(2), TeamId.Enemy, targetStats, new GridPosition(2, 0, 1));
                var stateChangedCount = 0;
                controller.StateChanged += _ => stateChangedCount++;

                var selectResult = controller.SelectUnit(actor);
                var actionResult = controller.SetActionMode(SelectionActionMode.Melee);
                controller.SetHoveredCell(new GridPosition(2, 0, 1));
                var targetResult = controller.SetTargetUnit(target);

                var state = controller.CurrentState;
                Assert.That(selectResult.IsSuccess, Is.True);
                Assert.That(actionResult.IsSuccess, Is.True);
                Assert.That(targetResult.IsSuccess, Is.True);
                Assert.That(stateChangedCount, Is.EqualTo(4));
                Assert.That(state.SelectedUnit, Is.SameAs(actor));
                Assert.That(state.HasSelectedUnit, Is.True);
                Assert.That(state.SelectedActionMode, Is.EqualTo(SelectionActionMode.Melee));
                Assert.That(state.HasHoveredCell, Is.True);
                Assert.That(state.HoveredCell, Is.EqualTo(new GridPosition(2, 0, 1)));
                Assert.That(state.SelectedTarget.Kind, Is.EqualTo(SelectionTargetKind.Unit));
                Assert.That(state.SelectedTarget.Unit, Is.SameAs(target));
                Assert.That(state.SelectedTarget.CurrentCell, Is.EqualTo(new GridPosition(2, 0, 1)));
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(targetObject);
                UnityEngine.Object.DestroyImmediate(actorObject);
                UnityEngine.Object.DestroyImmediate(controllerObject);
                UnityEngine.Object.DestroyImmediate(targetStats);
                UnityEngine.Object.DestroyImmediate(actorStats);
            }
        }

        [Test]
        public void CellTargetCanBeQueriedWithoutMousePicker()
        {
            var controllerObject = new GameObject("Selection Controller Cell Target Test");

            try
            {
                var controller = controllerObject.AddComponent<SelectionController>();
                var cell = new GridPosition(4, 1, 5);

                controller.SetTargetCell(cell);

                Assert.That(controller.CurrentState.HasSelectedTarget, Is.True);
                Assert.That(controller.SelectedTarget.Kind, Is.EqualTo(SelectionTargetKind.Cell));
                Assert.That(controller.SelectedTarget.Cell, Is.EqualTo(cell));
                Assert.That(controller.SelectedTarget.CurrentCell, Is.EqualTo(cell));
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(controllerObject);
            }
        }

        [Test]
        public void ClearForPhaseChangeClearsSelectionAndReportsReason()
        {
            var controllerObject = new GameObject("Selection Controller Phase Clear Test");
            var actorObject = new GameObject("Actor");
            var stats = CreateStats("Knight");

            try
            {
                var controller = controllerObject.AddComponent<SelectionController>();
                var actor = CreateUnit(actorObject, new UnitId(3), TeamId.Player, stats, GridPosition.Zero);
                var clearCount = 0;
                var clearReason = SelectionClearReason.Manual;
                controller.SelectionCleared += reason =>
                {
                    clearCount++;
                    clearReason = reason;
                };

                Assert.That(controller.SelectUnit(actor).IsSuccess, Is.True);
                Assert.That(controller.SetActionMode(SelectionActionMode.Move).IsSuccess, Is.True);
                controller.SetHoveredCell(new GridPosition(1, 0, 0));
                controller.SetTargetCell(new GridPosition(2, 0, 0));

                controller.ClearForPhaseChange();

                Assert.That(clearCount, Is.EqualTo(1));
                Assert.That(clearReason, Is.EqualTo(SelectionClearReason.PhaseChanged));
                Assert.That(controller.CurrentState.IsEmpty, Is.True);
                Assert.That(controller.SelectedUnit, Is.Null);
                Assert.That(controller.HasHoveredCell, Is.False);
                Assert.That(controller.SelectedActionMode, Is.EqualTo(SelectionActionMode.None));
                Assert.That(controller.SelectedTarget.HasTarget, Is.False);
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(actorObject);
                UnityEngine.Object.DestroyImmediate(controllerObject);
                UnityEngine.Object.DestroyImmediate(stats);
            }
        }

        [Test]
        public void SelectedUnitDeathClearsFullSelection()
        {
            var controllerObject = new GameObject("Selection Controller Death Clear Test");
            var actorObject = new GameObject("Actor");
            var stats = CreateStats("Knight");

            try
            {
                var controller = controllerObject.AddComponent<SelectionController>();
                var actor = CreateUnit(actorObject, new UnitId(4), TeamId.Player, stats, GridPosition.Zero);
                var clearReason = SelectionClearReason.Manual;
                controller.SelectionCleared += reason => clearReason = reason;
                Assert.That(controller.SelectUnit(actor).IsSuccess, Is.True);
                Assert.That(controller.SetActionMode(SelectionActionMode.Move).IsSuccess, Is.True);
                controller.SetTargetCell(new GridPosition(1, 0, 0));

                var damageResult = actor.ApplyDamage(actor.CurrentHP, DamageSource.Environmental("test defeat"));

                Assert.That(damageResult.IsSuccess, Is.True);
                Assert.That(clearReason, Is.EqualTo(SelectionClearReason.UnitDied));
                Assert.That(controller.CurrentState.IsEmpty, Is.True);
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(actorObject);
                UnityEngine.Object.DestroyImmediate(controllerObject);
                UnityEngine.Object.DestroyImmediate(stats);
            }
        }

        [Test]
        public void TargetUnitDeathClearsTargetButKeepsSelectedUnitAndAction()
        {
            var controllerObject = new GameObject("Selection Controller Target Death Test");
            var actorObject = new GameObject("Actor");
            var targetObject = new GameObject("Target");
            var actorStats = CreateStats("Knight");
            var targetStats = CreateStats("Goblin");

            try
            {
                var controller = controllerObject.AddComponent<SelectionController>();
                var actor = CreateUnit(actorObject, new UnitId(5), TeamId.Player, actorStats, GridPosition.Zero);
                var target = CreateUnit(targetObject, new UnitId(6), TeamId.Enemy, targetStats, new GridPosition(1, 0, 0));
                Assert.That(controller.SelectUnit(actor).IsSuccess, Is.True);
                Assert.That(controller.SetActionMode(SelectionActionMode.Melee).IsSuccess, Is.True);
                Assert.That(controller.SetTargetUnit(target).IsSuccess, Is.True);

                var damageResult = target.ApplyDamage(target.CurrentHP, DamageSource.Environmental("test defeat"));

                Assert.That(damageResult.IsSuccess, Is.True);
                Assert.That(controller.SelectedUnit, Is.SameAs(actor));
                Assert.That(controller.SelectedActionMode, Is.EqualTo(SelectionActionMode.Melee));
                Assert.That(controller.SelectedTarget.HasTarget, Is.False);
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(targetObject);
                UnityEngine.Object.DestroyImmediate(actorObject);
                UnityEngine.Object.DestroyImmediate(controllerObject);
                UnityEngine.Object.DestroyImmediate(targetStats);
                UnityEngine.Object.DestroyImmediate(actorStats);
            }
        }

        [Test]
        public void DefeatedUnitsCannotBeSelectedOrTargeted()
        {
            var controllerObject = new GameObject("Selection Controller Reject Dead Test");
            var unitObject = new GameObject("Defeated Unit");
            var stats = CreateStats("Goblin");

            try
            {
                var controller = controllerObject.AddComponent<SelectionController>();
                var unit = CreateUnit(unitObject, new UnitId(7), TeamId.Enemy, stats, GridPosition.Zero);
                Assert.That(unit.ApplyDamage(unit.CurrentHP, DamageSource.Environmental("test defeat")).IsSuccess, Is.True);

                Assert.That(controller.SelectUnit(unit).IsFailure, Is.True);
                Assert.That(controller.SetTargetUnit(unit).IsFailure, Is.True);
                Assert.That(controller.CurrentState.IsEmpty, Is.True);
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(unitObject);
                UnityEngine.Object.DestroyImmediate(controllerObject);
                UnityEngine.Object.DestroyImmediate(stats);
            }
        }

        private static TacticalUnit CreateUnit(
            GameObject gameObject,
            UnitId unitId,
            TeamId team,
            UnitStatsDefinition stats,
            GridPosition position)
        {
            var unit = gameObject.AddComponent<TacticalUnit>();
            unit.Initialize(unitId, team, stats, position);
            return unit;
        }

        private static UnitStatsDefinition CreateStats(string displayName)
        {
            var stats = ScriptableObject.CreateInstance<UnitStatsDefinition>();
            stats.Configure(displayName, 10, 6, 4f, 1, Color.white);
            return stats;
        }
    }
}
