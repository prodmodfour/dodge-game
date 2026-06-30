using System;
using ReactionTactics.Core;
using ReactionTactics.Grid;
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

            var result = controller.SelectUnit(unit);
            if (result.IsFailure)
            {
                return Reject(result);
            }

            return Route(PlayerCommandType.SelectUnit, controller.CurrentState);
        }

        public TacticalResult SelectMove()
        {
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

        public TacticalResult ConfirmTarget(SelectionTarget target)
        {
            if (!TryGetControllerWithSelectedUnit(out var controller, out var failure))
            {
                return Reject(failure);
            }

            if (controller.SelectedActionMode == SelectionActionMode.None)
            {
                return Reject("Select a move or attack command before confirming a target.");
            }

            TacticalResult result;
            switch (target.Kind)
            {
                case SelectionTargetKind.Cell:
                    controller.SetTargetCell(target.Cell);
                    result = TacticalResult.Success();
                    break;
                case SelectionTargetKind.Unit:
                    result = controller.SetTargetUnit(target.Unit);
                    break;
                default:
                    result = TacticalResult.Failure("Cannot confirm an empty target.");
                    break;
            }

            if (result.IsFailure)
            {
                return Reject(result);
            }

            return Route(PlayerCommandType.ConfirmTarget, controller.CurrentState);
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
            var controller = ResolveSelectionController();
            return Route(PlayerCommandType.EndTurn, controller != null ? controller.CurrentState : SelectionState.Empty);
        }

        public TacticalResult RequestPassReaction()
        {
            var controller = ResolveSelectionController();
            return Route(PlayerCommandType.PassReaction, controller != null ? controller.CurrentState : SelectionState.Empty);
        }

        public TacticalResult RequestPassOrEndTurn()
        {
            return RequestEndTurn();
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

            var controller = ResolveSelectionController();
            if (controller != null && IsAttackMode(controller.SelectedActionMode))
            {
                ConfirmTargetUnit(result.Unit);
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

            ConfirmTargetCell(result.Position);
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
    }
}
