using System;
using ReactionTactics.Core;
using ReactionTactics.Grid;
using ReactionTactics.Turns;
using ReactionTactics.Units;
using UnityEngine;

namespace ReactionTactics.Input
{
    /// <summary>
    /// Centralizes player UI, keyboard, and mouse input as high-level command requests.
    /// The router updates selection state and emits request events, but it intentionally
    /// does not execute movement, attacks, turn advancement, or combat legality.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class PlayerCommandRouter : MonoBehaviour
    {
        [SerializeField]
        [Tooltip("Selection state updated by routed player input.")]
        private SelectionController selectionController;

        [SerializeField]
        [Tooltip("Optional picker whose click events are routed into selection and target requests.")]
        private GridPicker gridPicker;

        [SerializeField]
        [Tooltip("Optional combat manager used to keep reaction input locked to the current reactor.")]
        private CombatManager combatManager;

        [SerializeField]
        [Tooltip("Poll prototype keyboard shortcuts in Update and route them through the same methods as UI buttons.")]
        private bool keyboardShortcutsEnabled = true;

        [SerializeField]
        [Tooltip("Shortcut for selecting active movement.")]
        private KeyCode moveShortcut = KeyCode.M;

        [SerializeField]
        [Tooltip("Shortcut for selecting the melee attack.")]
        private KeyCode meleeShortcut = KeyCode.Alpha1;

        [SerializeField]
        [Tooltip("Shortcut for selecting the cone attack.")]
        private KeyCode coneShortcut = KeyCode.Alpha2;

        [SerializeField]
        [Tooltip("Shortcut for selecting the area-of-effect attack.")]
        private KeyCode areaOfEffectShortcut = KeyCode.Alpha3;

        [SerializeField]
        [Tooltip("Shortcut for selecting Brace during a reaction turn.")]
        private KeyCode braceShortcut = KeyCode.B;

        [SerializeField]
        [Tooltip("Shortcut for pass/end turn. Combat phase systems decide whether this passes or ends the active turn.")]
        private KeyCode passOrEndTurnShortcut = KeyCode.Space;

        [SerializeField]
        [Tooltip("Shortcut for canceling current selection or targeting.")]
        private KeyCode cancelShortcut = KeyCode.Escape;

        [SerializeField]
        [Tooltip("Write concise logs whenever a command request is routed or rejected.")]
        private bool logRoutedCommands;

        private GridPicker subscribedPicker;

        public event Action<PlayerCommandRequest> CommandRequested;

        public event Action<TacticalResult> CommandRejected;

        public SelectionController SelectionController
        {
            get { return selectionController; }
            set { selectionController = value; }
        }

        public GridPicker GridPicker
        {
            get { return gridPicker; }
            set
            {
                if (gridPicker == value)
                {
                    if (isActiveAndEnabled)
                    {
                        SubscribeToPicker();
                    }

                    return;
                }

                UnsubscribeFromPicker();
                gridPicker = value;

                if (isActiveAndEnabled)
                {
                    SubscribeToPicker();
                }
            }
        }

        public CombatManager CombatManager
        {
            get { return combatManager; }
            set { combatManager = value; }
        }

        public bool LogRoutedCommands
        {
            get { return logRoutedCommands; }
            set { logRoutedCommands = value; }
        }

        public bool KeyboardShortcutsEnabled
        {
            get { return keyboardShortcutsEnabled; }
            set { keyboardShortcutsEnabled = value; }
        }

        public TacticalResult SelectUnit(TacticalUnit unit)
        {
            var controller = ResolveSelectionController();
            if (controller == null)
            {
                return Reject("PlayerCommandRouter requires a SelectionController before selecting units.");
            }

            var reactionSelectionResult = ValidateUnitCanBeSelectedDuringReaction(unit);
            if (reactionSelectionResult.IsFailure)
            {
                return Reject(reactionSelectionResult);
            }

            var result = controller.SelectUnit(unit);
            if (result.IsFailure)
            {
                return Reject(result);
            }

            return Route(PlayerCommandType.SelectUnit, controller.CurrentState);
        }

        public TacticalResult SelectMove()
        {
            if (IsReactionControlActive())
            {
                return SelectReactionMode(SelectionActionMode.Move);
            }

            if (!TryGetControllerWithSelectedUnit(out var controller, out var failure))
            {
                return Reject(failure);
            }

            var result = controller.SetActionMode(SelectionActionMode.Move);
            if (result.IsFailure)
            {
                return Reject(result);
            }

            return Route(PlayerCommandType.SelectMove, controller.CurrentState);
        }

        public TacticalResult SelectAttack(SelectionActionMode attackMode)
        {
            if (!IsAttackMode(attackMode))
            {
                return Reject($"'{attackMode}' is not a selectable attack mode.");
            }

            if (IsReactionControlActive())
            {
                return Reject(
                    $"Cannot select active attack '{attackMode}' during a reaction turn. "
                    + $"Only the current reactor {DescribeUnit(GetCurrentReactor())} may choose reaction movement, Brace, Pass, or Cancel.");
            }

            if (!TryGetControllerWithSelectedUnit(out var controller, out var failure))
            {
                return Reject(failure);
            }

            var result = controller.SetActionMode(attackMode);
            if (result.IsFailure)
            {
                return Reject(result);
            }

            return Route(PlayerCommandType.SelectAttack, controller.CurrentState);
        }

        public TacticalResult SelectMeleeAttack()
        {
            return SelectAttack(SelectionActionMode.Melee);
        }

        public TacticalResult SelectConeAttack()
        {
            return SelectAttack(SelectionActionMode.Cone);
        }

        public TacticalResult SelectAreaOfEffectAttack()
        {
            return SelectAttack(SelectionActionMode.AreaOfEffect);
        }

        public TacticalResult SelectBraceReaction()
        {
            if (IsReactionControlActive())
            {
                return SelectReactionMode(SelectionActionMode.Brace);
            }

            if (combatManager != null
                && combatManager.CurrentState != null
                && combatManager.CurrentState.Phase != CombatPhase.NotStarted)
            {
                return Reject("Brace can only be selected during the current unit's reaction turn.");
            }

            if (!TryGetControllerWithSelectedUnit(out var controller, out var failure))
            {
                return Reject(failure);
            }

            var result = controller.SetActionMode(SelectionActionMode.Brace);
            if (result.IsFailure)
            {
                return Reject(result);
            }

            return Route(PlayerCommandType.SelectReaction, controller.CurrentState);
        }

        public TacticalResult ConfirmTargetCell(GridPosition cell)
        {
            return ConfirmTarget(SelectionTarget.ForCell(cell));
        }

        public TacticalResult ConfirmTargetUnit(TacticalUnit unit)
        {
            if (unit == null)
            {
                return Reject("Cannot confirm a missing tactical unit target.");
            }

            return ConfirmTarget(SelectionTarget.ForUnit(unit));
        }

        public TacticalResult ConfirmCurrentTarget()
        {
            var controller = ResolveSelectionController();
            if (controller == null)
            {
                return Reject("PlayerCommandRouter requires a SelectionController before confirming targets.");
            }

            if (controller.SelectedTarget.HasTarget)
            {
                return ConfirmTarget(controller.SelectedTarget);
            }

            if (controller.HasHoveredCell)
            {
                return ConfirmTargetCell(controller.HoveredCell);
            }

            return Reject("No target is selected or hovered to confirm.");
        }

        public TacticalResult SelectTargetCellForConfirmation(GridPosition cell)
        {
            return SelectTargetForConfirmation(SelectionTarget.ForCell(cell));
        }

        public TacticalResult SelectTargetUnitForConfirmation(TacticalUnit unit)
        {
            if (unit == null)
            {
                return Reject("Cannot select a missing tactical unit target.");
            }

            return SelectTargetForConfirmation(SelectionTarget.ForUnit(unit));
        }

        public TacticalResult SelectTargetForConfirmation(SelectionTarget target)
        {
            if (IsReactionControlActive())
            {
                var focusResult = FocusCurrentReactorSelection();
                if (focusResult.IsFailure)
                {
                    return Reject(focusResult);
                }
            }

            if (!TryGetControllerWithSelectedUnit(out var controller, out var failure))
            {
                return Reject(failure);
            }

            if (controller.SelectedActionMode == SelectionActionMode.None)
            {
                return Reject("Select a move, attack, or reaction command before choosing a target.");
            }

            var result = ApplyTargetSelection(controller, target);
            if (result.IsFailure)
            {
                return Reject(result);
            }

            if (logRoutedCommands)
            {
                Debug.Log(
                    $"PlayerCommandRouter selected target for confirmation: unit={DescribeUnit(controller.SelectedUnit)} mode={controller.SelectedActionMode} target={controller.SelectedTarget}",
                    this);
            }

            return TacticalResult.Success();
        }

        public TacticalResult ConfirmTarget(SelectionTarget target)
        {
            if (IsReactionControlActive())
            {
                var focusResult = FocusCurrentReactorSelection();
                if (focusResult.IsFailure)
                {
                    return Reject(focusResult);
                }
            }

            if (!TryGetControllerWithSelectedUnit(out var controller, out var failure))
            {
                return Reject(failure);
            }

            if (controller.SelectedActionMode == SelectionActionMode.None)
            {
                return Reject("Select a move or attack command before confirming a target.");
            }

            var result = ApplyTargetSelection(controller, target);
            if (result.IsFailure)
            {
                return Reject(result);
            }

            return Route(
                IsReactionControlActive() ? PlayerCommandType.SelectReaction : PlayerCommandType.ConfirmTarget,
                controller.CurrentState);
        }

        public TacticalResult Cancel()
        {
            var controller = ResolveSelectionController();
            if (controller != null)
            {
                var state = controller.CurrentState;
                if (state.HasSelectedActionMode || state.HasSelectedTarget)
                {
                    controller.ClearActionMode();
                }
                else if (state.HasSelectedUnit)
                {
                    controller.ClearSelectedUnit();
                }
            }

            return Route(PlayerCommandType.Cancel, controller != null ? controller.CurrentState : SelectionState.Empty);
        }

        public TacticalResult RequestEndTurn()
        {
            if (IsReactionControlActive())
            {
                var focusResult = FocusCurrentReactorSelection();
                if (focusResult.IsFailure)
                {
                    return Reject(focusResult);
                }
            }

            var controller = ResolveSelectionController();
            return Route(PlayerCommandType.EndTurn, controller != null ? controller.CurrentState : SelectionState.Empty);
        }

        public TacticalResult RequestPassReaction()
        {
            if (IsReactionControlActive())
            {
                var focusResult = FocusCurrentReactorSelection();
                if (focusResult.IsFailure)
                {
                    return Reject(focusResult);
                }
            }

            var controller = ResolveSelectionController();
            return Route(PlayerCommandType.PassReaction, controller != null ? controller.CurrentState : SelectionState.Empty);
        }

        public TacticalResult RequestPassOrEndTurn()
        {
            return RequestEndTurn();
        }

        /// <summary>
        /// Updates selection to the current reactor without emitting a player command.
        /// Combat flow uses this when a reaction turn starts so UI and hotkeys point at
        /// the unit that is legally allowed to react.
        /// </summary>
        public TacticalResult FocusCurrentReactorSelection()
        {
            if (!TryGetCurrentReactor(out var reactor, out var failure))
            {
                return failure;
            }

            var controller = ResolveSelectionController();
            if (controller == null)
            {
                return TacticalResult.Failure("PlayerCommandRouter requires a SelectionController before focusing the current reactor.");
            }

            return controller.SelectUnit(reactor);
        }

        public TacticalResult RouteKeyboardShortcut(KeyCode keyCode)
        {
            if (keyCode == KeyCode.None)
            {
                return Reject("Cannot route an unassigned keyboard shortcut.");
            }

            if (keyCode == moveShortcut)
            {
                return SelectMove();
            }

            if (keyCode == meleeShortcut)
            {
                return SelectMeleeAttack();
            }

            if (keyCode == coneShortcut)
            {
                return SelectConeAttack();
            }

            if (keyCode == areaOfEffectShortcut)
            {
                return SelectAreaOfEffectAttack();
            }

            if (keyCode == braceShortcut)
            {
                return SelectBraceReaction();
            }

            if (keyCode == passOrEndTurnShortcut)
            {
                return RequestPassOrEndTurn();
            }

            if (keyCode == cancelShortcut)
            {
                return Cancel();
            }

            return Reject($"No player command shortcut is bound to '{keyCode}'.");
        }

        private void OnEnable()
        {
            ResolveSelectionController();
            ResolveGridPicker();
            ResolveCombatManager();
            SubscribeToPicker();
        }

        private void OnDisable()
        {
            UnsubscribeFromPicker();
        }

        private void Update()
        {
            if (!keyboardShortcutsEnabled)
            {
                return;
            }

            if (TryRoutePressedShortcut(cancelShortcut)
                || TryRoutePressedShortcut(moveShortcut)
                || TryRoutePressedShortcut(meleeShortcut)
                || TryRoutePressedShortcut(coneShortcut)
                || TryRoutePressedShortcut(areaOfEffectShortcut)
                || TryRoutePressedShortcut(braceShortcut)
                || TryRoutePressedShortcut(passOrEndTurnShortcut))
            {
                return;
            }
        }

        private void Reset()
        {
            selectionController = FindAnyObjectByType<SelectionController>();
            gridPicker = FindAnyObjectByType<GridPicker>();
            combatManager = FindAnyObjectByType<CombatManager>();
            keyboardShortcutsEnabled = true;
            moveShortcut = KeyCode.M;
            meleeShortcut = KeyCode.Alpha1;
            coneShortcut = KeyCode.Alpha2;
            areaOfEffectShortcut = KeyCode.Alpha3;
            braceShortcut = KeyCode.B;
            passOrEndTurnShortcut = KeyCode.Space;
            cancelShortcut = KeyCode.Escape;
            logRoutedCommands = false;
        }

        private void HandlePickerUnitClicked(UnitPickResult result)
        {
            if (result.Unit == null)
            {
                Reject("Clicked unit result did not contain a tactical unit.");
                return;
            }

            if (IsReactionControlActive())
            {
                if (!TryGetCurrentReactor(out var reactor, out var failure))
                {
                    Reject(failure);
                    return;
                }

                if (!ReferenceEquals(result.Unit, reactor))
                {
                    Reject(
                        $"Cannot select {DescribeUnit(result.Unit)} during this reaction turn. "
                        + $"Only current reactor {DescribeUnit(reactor)} may react now.");
                    return;
                }

                SelectUnit(reactor);
                return;
            }

            var controller = ResolveSelectionController();
            if (controller != null && IsAttackMode(controller.SelectedActionMode))
            {
                SelectOrConfirmPickedTarget(SelectionTarget.ForUnit(result.Unit));
                return;
            }

            SelectUnit(result.Unit);
        }

        private void HandlePickerCellClicked(GridPickResult result)
        {
            var controller = ResolveSelectionController();
            if (controller == null)
            {
                Reject("PlayerCommandRouter requires a SelectionController before routing cell clicks.");
                return;
            }

            if (controller.SelectedActionMode == SelectionActionMode.None)
            {
                controller.SetHoveredCell(result.Position);
                return;
            }

            if (controller.SelectedActionMode == SelectionActionMode.Move && !IsReactionControlActive())
            {
                ConfirmTargetCell(result.Position);
                return;
            }

            SelectOrConfirmPickedTarget(SelectionTarget.ForCell(result.Position));
        }

        private void SelectOrConfirmPickedTarget(SelectionTarget target)
        {
            var controller = ResolveSelectionController();
            if (controller == null)
            {
                Reject("PlayerCommandRouter requires a SelectionController before routing target clicks.");
                return;
            }

            if (controller.SelectedTarget.HasTarget && TargetsMatch(controller.SelectedTarget, target))
            {
                ConfirmTarget(target);
                return;
            }

            SelectTargetForConfirmation(target);
        }

        private TacticalResult SelectReactionMode(SelectionActionMode reactionMode)
        {
            if (!TryGetCurrentReactor(out var reactor, out var failure))
            {
                return Reject(failure);
            }

            var controller = ResolveSelectionController();
            if (controller == null)
            {
                return Reject("PlayerCommandRouter requires a SelectionController before routing reaction commands.");
            }

            var selectResult = controller.SelectUnit(reactor);
            if (selectResult.IsFailure)
            {
                return Reject(selectResult);
            }

            var modeResult = controller.SetActionMode(reactionMode);
            if (modeResult.IsFailure)
            {
                return Reject(modeResult);
            }

            return Route(PlayerCommandType.SelectReaction, controller.CurrentState);
        }

        private TacticalResult ValidateUnitCanBeSelectedDuringReaction(TacticalUnit unit)
        {
            if (!IsReactionControlActive())
            {
                return TacticalResult.Success();
            }

            if (!TryGetCurrentReactor(out var reactor, out var failure))
            {
                return failure;
            }

            if (!ReferenceEquals(unit, reactor))
            {
                return TacticalResult.Failure(
                    $"Cannot select {DescribeUnit(unit)} during this reaction turn. "
                    + $"Only current reactor {DescribeUnit(reactor)} may react now.");
            }

            return TacticalResult.Success();
        }

        private bool IsReactionControlActive()
        {
            var manager = ResolveCombatManager();
            return manager != null
                && manager.CurrentState != null
                && manager.CurrentState.IsReactionPhase;
        }

        private bool TryGetCurrentReactor(out TacticalUnit reactor, out TacticalResult failure)
        {
            reactor = GetCurrentReactor();
            if (reactor == null)
            {
                failure = TacticalResult.Failure("No current reactor is available for this reaction command.");
                return false;
            }

            if (!reactor.IsAlive)
            {
                failure = TacticalResult.Failure($"Current reactor {DescribeUnit(reactor)} is defeated and cannot react.");
                return false;
            }

            failure = TacticalResult.Success();
            return true;
        }

        private TacticalUnit GetCurrentReactor()
        {
            var manager = ResolveCombatManager();
            return manager != null && manager.CurrentState != null ? manager.CurrentState.CurrentReactor : null;
        }

        private bool TryGetControllerWithSelectedUnit(out SelectionController controller, out TacticalResult failure)
        {
            controller = ResolveSelectionController();
            if (controller == null)
            {
                failure = TacticalResult.Failure("PlayerCommandRouter requires a SelectionController before routing commands.");
                return false;
            }

            if (!controller.HasSelectedUnit)
            {
                failure = TacticalResult.Failure("Select a living tactical unit before issuing a command.");
                return false;
            }

            failure = TacticalResult.Success();
            return true;
        }

        private TacticalResult Route(PlayerCommandType commandType, SelectionState state)
        {
            var request = new PlayerCommandRequest(
                commandType,
                state.SelectedUnit,
                state.SelectedActionMode,
                state.SelectedTarget,
                state);

            CommandRequested?.Invoke(request);

            if (logRoutedCommands)
            {
                Debug.Log($"PlayerCommandRouter routed {request}", this);
            }

            return TacticalResult.Success();
        }

        private static TacticalResult ApplyTargetSelection(SelectionController controller, SelectionTarget target)
        {
            switch (target.Kind)
            {
                case SelectionTargetKind.Cell:
                    controller.SetTargetCell(target.Cell);
                    return TacticalResult.Success();
                case SelectionTargetKind.Unit:
                    return controller.SetTargetUnit(target.Unit);
                default:
                    return TacticalResult.Failure("Cannot select or confirm an empty target.");
            }
        }

        private TacticalResult Reject(string errorMessage)
        {
            return Reject(TacticalResult.Failure(errorMessage));
        }

        private TacticalResult Reject(TacticalResult result)
        {
            CommandRejected?.Invoke(result);

            if (logRoutedCommands)
            {
                Debug.LogWarning($"PlayerCommandRouter rejected command: {result.ErrorMessage}", this);
            }

            return result;
        }

        private SelectionController ResolveSelectionController()
        {
            if (selectionController == null)
            {
                selectionController = FindAnyObjectByType<SelectionController>();
            }

            return selectionController;
        }

        private GridPicker ResolveGridPicker()
        {
            if (gridPicker == null)
            {
                gridPicker = FindAnyObjectByType<GridPicker>();
            }

            return gridPicker;
        }

        private CombatManager ResolveCombatManager()
        {
            if (combatManager == null)
            {
                combatManager = FindAnyObjectByType<CombatManager>();
            }

            return combatManager;
        }

        private bool TryRoutePressedShortcut(KeyCode shortcut)
        {
            if (shortcut == KeyCode.None || !UnityEngine.Input.GetKeyDown(shortcut))
            {
                return false;
            }

            RouteKeyboardShortcut(shortcut);
            return true;
        }

        private void SubscribeToPicker()
        {
            if (gridPicker == null || subscribedPicker == gridPicker)
            {
                return;
            }

            subscribedPicker = gridPicker;
            subscribedPicker.UnitClicked += HandlePickerUnitClicked;
            subscribedPicker.CellClicked += HandlePickerCellClicked;
        }

        private void UnsubscribeFromPicker()
        {
            if (subscribedPicker == null)
            {
                return;
            }

            subscribedPicker.UnitClicked -= HandlePickerUnitClicked;
            subscribedPicker.CellClicked -= HandlePickerCellClicked;
            subscribedPicker = null;
        }

        private static bool IsAttackMode(SelectionActionMode actionMode)
        {
            return actionMode == SelectionActionMode.Melee
                || actionMode == SelectionActionMode.Cone
                || actionMode == SelectionActionMode.AreaOfEffect;
        }

        private static bool TargetsMatch(SelectionTarget currentTarget, SelectionTarget clickedTarget)
        {
            if (currentTarget.Kind != clickedTarget.Kind)
            {
                return false;
            }

            switch (currentTarget.Kind)
            {
                case SelectionTargetKind.Cell:
                    return currentTarget.Cell == clickedTarget.Cell;
                case SelectionTargetKind.Unit:
                    return ReferenceEquals(currentTarget.Unit, clickedTarget.Unit);
                default:
                    return true;
            }
        }

        private static string DescribeUnit(TacticalUnit unit)
        {
            if (unit == null)
            {
                return "no unit";
            }

            return unit.IsInitialized ? $"{unit.DisplayName} {unit.UnitId}" : unit.DisplayName;
        }
    }
}
