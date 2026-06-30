using System;
using System.Collections.Generic;
using ReactionTactics.Actions;
using ReactionTactics.Core;
using ReactionTactics.Input;
using ReactionTactics.Turns;
using ReactionTactics.Units;
using UnityEngine;

namespace ReactionTactics.UI
{
    /// <summary>
    /// Lightweight IMGUI action menu for active turns. It mirrors the existing
    /// keyboard shortcuts by routing button clicks through <see cref="PlayerCommandRouter" />
    /// instead of executing combat commands directly.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class ActiveActionMenu : MonoBehaviour
    {
        private const float MinimumPanelWidth = 260f;
        private const float MinimumPanelHeight = 150f;
        private const string MoveAbilityKey = "move";

        [SerializeField]
        [Tooltip("Selection state used to choose which unit's active abilities are listed.")]
        private SelectionController selectionController;

        [SerializeField]
        [Tooltip("Command router that receives menu button requests.")]
        private PlayerCommandRouter commandRouter;

        [SerializeField]
        [Tooltip("Combat manager whose phase and active unit decide which buttons are legal.")]
        private CombatManager combatManager;

        [SerializeField]
        [Tooltip("Show the active action menu while this component is enabled.")]
        private bool visible = true;

        [SerializeField]
        [Tooltip("Screen-space rectangle used for the active action menu panel.")]
        private Rect panelRect = new Rect(12f, 330f, 360f, 220f);

        [SerializeField]
        [Tooltip("Header text shown at the top of the active action menu.")]
        private string panelTitle = "Active Actions";

        [SerializeField]
        [Tooltip("When true, disabled button reasons are shown under the button list.")]
        private bool showDisabledReasons = true;

        private GUIStyle headerStyle;
        private GUIStyle detailStyle;
        private GUIStyle disabledReasonStyle;
        private string lastFeedback = string.Empty;

        public SelectionController SelectionController
        {
            get { return selectionController; }
            set { selectionController = value; }
        }

        public PlayerCommandRouter CommandRouter
        {
            get { return commandRouter; }
            set { commandRouter = value; }
        }

        public CombatManager CombatManager
        {
            get { return combatManager; }
            set { combatManager = value; }
        }

        public bool Visible
        {
            get { return visible; }
            set { visible = value; }
        }

        public Rect PanelRect
        {
            get { return panelRect; }
            set { panelRect = ClampPanelRect(value); }
        }

        public string LastFeedback
        {
            get { return lastFeedback; }
        }

        public static IReadOnlyList<ActiveActionMenuEntry> BuildMenuEntries(
            SelectionState selectionState,
            CombatState combatState)
        {
            return BuildMenuEntries(selectionState.SelectedUnit, combatState);
        }

        public static IReadOnlyList<ActiveActionMenuEntry> BuildMenuEntries(
            TacticalUnit selectedUnit,
            CombatState combatState)
        {
            var entries = new List<ActiveActionMenuEntry>();
            var activeLegality = ValidateSelectedActiveUnit(selectedUnit, combatState);
            var moveAbility = FindMoveAbility(selectedUnit);
            var moveReason = activeLegality;
            var moveEnabled = string.IsNullOrEmpty(moveReason);
            if (moveEnabled && selectedUnit.CurrentAP <= 0)
            {
                moveEnabled = false;
                moveReason = "Move requires AP to spend on a path.";
            }

            entries.Add(ActiveActionMenuEntry.Move(
                FormatMoveLabel(moveAbility),
                moveEnabled,
                moveReason));

            AddAbilityEntries(entries, selectedUnit, activeLegality);

            var endTurnReason = ValidateEndTurnAvailable(combatState);
            entries.Add(ActiveActionMenuEntry.EndTurn(
                "End Turn (0 AP)",
                string.IsNullOrEmpty(endTurnReason),
                endTurnReason));

            return entries;
        }

        public static string FormatSelectedUnitSummary(SelectionState selectionState, CombatState combatState)
        {
            var selectedUnit = selectionState.SelectedUnit;
            var activeUnit = combatState != null ? combatState.ActiveUnit : null;
            var phaseLabel = combatState != null ? combatState.Phase.ToString() : "No Combat";
            var activeLabel = activeUnit != null ? FormatUnit(activeUnit) : "None";

            if (selectedUnit == null)
            {
                return $"Phase: {phaseLabel}\nActive: {activeLabel}\nSelected: None\nSelected Action: None";
            }

            return $"Phase: {phaseLabel}\nActive: {activeLabel}\nSelected: {FormatUnit(selectedUnit)}\nAP: {selectedUnit.CurrentAP}/{selectedUnit.MaxAP}\n{FormatSelectedActionSummary(selectionState)}";
        }

        private void Awake()
        {
            ResolveMissingReferences();
            panelRect = ClampPanelRect(panelRect);
        }

        private void OnEnable()
        {
            ResolveMissingReferences();
        }

        private void OnGUI()
        {
            if (!visible)
            {
                return;
            }

            ResolveMissingReferences();
            EnsureStyles();

            var selectionState = selectionController != null ? selectionController.CurrentState : SelectionState.Empty;
            var combatState = combatManager != null ? combatManager.CurrentState : null;
            var entries = BuildMenuEntries(selectionState, combatState);

            GUILayout.BeginArea(panelRect, panelTitle, GUI.skin.window);
            GUILayout.Label(FormatSelectedUnitSummary(selectionState, combatState), headerStyle);
            GUILayout.Space(4f);

            for (var i = 0; i < entries.Count; i += 1)
            {
                DrawEntry(entries[i]);
            }

            DrawSelectedActionControls(selectionState);

            if (!string.IsNullOrEmpty(lastFeedback))
            {
                GUILayout.Space(4f);
                GUILayout.Label(lastFeedback, detailStyle);
            }

            if (showDisabledReasons)
            {
                DrawDisabledReasons(entries);
            }

            GUILayout.EndArea();
        }

        private void OnValidate()
        {
            panelRect = ClampPanelRect(panelRect);
        }

        private void DrawEntry(ActiveActionMenuEntry entry)
        {
            var previousEnabled = GUI.enabled;
            GUI.enabled = previousEnabled && entry.IsEnabled;
            if (GUILayout.Button(entry.Label, GUILayout.Height(28f)))
            {
                ExecuteEntry(entry);
            }

            GUI.enabled = previousEnabled;
        }

        private void DrawDisabledReasons(IReadOnlyList<ActiveActionMenuEntry> entries)
        {
            for (var i = 0; i < entries.Count; i += 1)
            {
                var entry = entries[i];
                if (entry.IsEnabled || string.IsNullOrEmpty(entry.DisabledReason))
                {
                    continue;
                }

                GUILayout.Label($"{entry.ShortName}: {entry.DisabledReason}", disabledReasonStyle);
            }
        }

        private void DrawSelectedActionControls(SelectionState selectionState)
        {
            if (!selectionState.HasSelectedActionMode || !IsConfirmableActionMode(selectionState.SelectedActionMode))
            {
                return;
            }

            GUILayout.Space(4f);
            GUILayout.Label(FormatTargetingInstructions(selectionState), detailStyle);

            var router = ResolveCommandRouter();
            var previousEnabled = GUI.enabled;
            var moveExecutesOnClick = selectionState.SelectedActionMode == SelectionActionMode.Move;
            if (!moveExecutesOnClick)
            {
                GUI.enabled = previousEnabled && router != null && selectionState.HasSelectedTarget;
                if (GUILayout.Button("Confirm Target", GUILayout.Height(26f)))
                {
                    var result = router.ConfirmCurrentTarget();
                    lastFeedback = result.IsSuccess ? "Confirmed target." : result.ErrorMessage;
                }
            }

            GUI.enabled = previousEnabled && router != null;
            if (GUILayout.Button(moveExecutesOnClick ? "Cancel Move (Esc)" : "Cancel Selection (Esc)", GUILayout.Height(24f)))
            {
                var result = router.Cancel();
                lastFeedback = result.IsSuccess ? "Canceled selected action." : result.ErrorMessage;
            }

            GUI.enabled = previousEnabled;
        }

        private TacticalResult ExecuteEntry(ActiveActionMenuEntry entry)
        {
            if (!entry.IsEnabled)
            {
                lastFeedback = entry.DisabledReason;
                return TacticalResult.Failure(entry.DisabledReason);
            }

            var router = ResolveCommandRouter();
            if (router == null)
            {
                lastFeedback = "Active action menu requires a PlayerCommandRouter.";
                return TacticalResult.Failure(lastFeedback);
            }

            TacticalResult result;
            switch (entry.Kind)
            {
                case ActiveActionMenuEntryKind.Move:
                    result = router.SelectMove();
                    break;
                case ActiveActionMenuEntryKind.Ability:
                    result = SelectAbility(router, entry.ActionMode);
                    break;
                case ActiveActionMenuEntryKind.EndTurn:
                    result = router.RequestEndTurn();
                    break;
                default:
                    result = TacticalResult.Failure($"Unsupported active action menu entry '{entry.Kind}'.");
                    break;
            }

            lastFeedback = result.IsSuccess
                ? FormatSelectionFeedback(entry)
                : result.ErrorMessage;
            return result;
        }

        private static string FormatSelectionFeedback(ActiveActionMenuEntry entry)
        {
            if (entry.Kind == ActiveActionMenuEntryKind.EndTurn)
            {
                return "Requested End Turn.";
            }

            if (entry.Kind == ActiveActionMenuEntryKind.Move)
            {
                return "Selected Move. Hover highlighted cells to preview a path; click a reachable destination to move. Esc cancels.";
            }

            return $"Selected {entry.ShortName}. Click a target to mark it, then Confirm Target or click the same target again. Esc cancels.";
        }

        private TacticalResult SelectAbility(PlayerCommandRouter router, SelectionActionMode actionMode)
        {
            switch (actionMode)
            {
                case SelectionActionMode.Melee:
                    return router.SelectMeleeAttack();
                case SelectionActionMode.Cone:
                    return router.SelectConeAttack();
                case SelectionActionMode.AreaOfEffect:
                    return router.SelectAreaOfEffectAttack();
                default:
                    return TacticalResult.Failure($"No active action menu route exists for selection mode '{actionMode}'.");
            }
        }

        private void ResolveMissingReferences()
        {
            if (selectionController == null)
            {
                selectionController = FindAnyObjectByType<SelectionController>();
            }

            ResolveCommandRouter();

            if (combatManager == null)
            {
                combatManager = FindAnyObjectByType<CombatManager>();
            }
        }

        private PlayerCommandRouter ResolveCommandRouter()
        {
            if (commandRouter == null)
            {
                commandRouter = FindAnyObjectByType<PlayerCommandRouter>();
            }

            return commandRouter;
        }

        private void EnsureStyles()
        {
            if (headerStyle == null)
            {
                headerStyle = new GUIStyle(GUI.skin.label)
                {
                    wordWrap = true,
                    fontSize = 13,
                    normal = { textColor = Color.white }
                };
            }

            if (detailStyle == null)
            {
                detailStyle = new GUIStyle(GUI.skin.label)
                {
                    wordWrap = true,
                    fontSize = 12,
                    normal = { textColor = new Color(0.85f, 0.92f, 1f, 1f) }
                };
            }

            if (disabledReasonStyle == null)
            {
                disabledReasonStyle = new GUIStyle(GUI.skin.label)
                {
                    wordWrap = true,
                    fontSize = 11,
                    normal = { textColor = new Color(1f, 0.78f, 0.45f, 1f) }
                };
            }
        }

        private static void AddAbilityEntries(
            ICollection<ActiveActionMenuEntry> entries,
            TacticalUnit selectedUnit,
            string activeLegality)
        {
            var loadout = selectedUnit != null ? selectedUnit.GetComponent<UnitAbilityLoadout>() : null;
            if (loadout == null)
            {
                return;
            }

            var actionAbilities = loadout.GetActionAbilities();
            for (var i = 0; i < actionAbilities.Count; i += 1)
            {
                var ability = actionAbilities[i];
                if (ability == null || IsMoveAbility(ability))
                {
                    continue;
                }

                var canRoute = TryGetActionMode(ability.Shape, out var actionMode);
                var disabledReason = activeLegality;
                var enabled = string.IsNullOrEmpty(disabledReason);

                if (enabled && !canRoute)
                {
                    enabled = false;
                    disabledReason = $"No active menu route exists for {ability.Shape} abilities.";
                }

                if (enabled && selectedUnit.CurrentAP < ability.APCost)
                {
                    enabled = false;
                    disabledReason = $"Needs {ability.APCost} AP; {selectedUnit.DisplayName} has {selectedUnit.CurrentAP} AP.";
                }

                entries.Add(ActiveActionMenuEntry.ForAbility(
                    ability,
                    actionMode,
                    FormatAbilityLabel(ability),
                    enabled,
                    disabledReason));
            }
        }

        private static string ValidateSelectedActiveUnit(TacticalUnit selectedUnit, CombatState combatState)
        {
            if (selectedUnit == null)
            {
                return "Select the active unit to choose active-turn actions.";
            }

            if (!selectedUnit.IsAlive)
            {
                return $"{selectedUnit.DisplayName} is defeated.";
            }

            if (combatState == null)
            {
                return "Combat has no current state.";
            }

            if (!combatState.IsActiveUnitPhase)
            {
                return $"Active actions are unavailable during {combatState.Phase}.";
            }

            if (combatState.ActiveUnit == null)
            {
                return "Combat has no active unit.";
            }

            if (!ReferenceEquals(selectedUnit, combatState.ActiveUnit))
            {
                return $"Select active unit {FormatUnit(combatState.ActiveUnit)} to use active-turn actions.";
            }

            return string.Empty;
        }

        private static string ValidateEndTurnAvailable(CombatState combatState)
        {
            if (combatState == null)
            {
                return "Combat has no current state.";
            }

            if (!combatState.IsActiveUnitPhase)
            {
                return $"End Turn is unavailable during {combatState.Phase}.";
            }

            if (combatState.ActiveUnit == null)
            {
                return "Combat has no active unit to end.";
            }

            return string.Empty;
        }

        private static bool TryGetActionMode(AbilityShape shape, out SelectionActionMode actionMode)
        {
            switch (shape)
            {
                case AbilityShape.Melee:
                    actionMode = SelectionActionMode.Melee;
                    return true;
                case AbilityShape.Cone:
                    actionMode = SelectionActionMode.Cone;
                    return true;
                case AbilityShape.Radius:
                    actionMode = SelectionActionMode.AreaOfEffect;
                    return true;
                default:
                    actionMode = SelectionActionMode.None;
                    return false;
            }
        }

        private static AbilityDefinition FindMoveAbility(TacticalUnit selectedUnit)
        {
            var loadout = selectedUnit != null ? selectedUnit.GetComponent<UnitAbilityLoadout>() : null;
            if (loadout == null)
            {
                return null;
            }

            var actionAbilities = loadout.GetActionAbilities();
            for (var i = 0; i < actionAbilities.Count; i += 1)
            {
                var ability = actionAbilities[i];
                if (IsMoveAbility(ability))
                {
                    return ability;
                }
            }

            return null;
        }

        private static bool IsMoveAbility(AbilityDefinition ability)
        {
            return ability != null
                && string.Equals(ability.AbilityKey, MoveAbilityKey, StringComparison.OrdinalIgnoreCase);
        }

        private static string FormatMoveLabel(AbilityDefinition moveAbility)
        {
            var name = moveAbility != null ? moveAbility.DisplayName : "Move";
            return $"{name} (path AP)";
        }

        private static string FormatSelectedActionSummary(SelectionState selectionState)
        {
            if (!selectionState.HasSelectedActionMode)
            {
                return "Selected Action: None";
            }

            var actionLabel = FormatSelectedActionLabel(selectionState.SelectedUnit, selectionState.SelectedActionMode);
            var targetLabel = selectionState.HasSelectedTarget ? selectionState.SelectedTarget.ToString() : "No target selected";
            return $"Selected Action: {actionLabel}\nTarget: {targetLabel}";
        }

        private static string FormatTargetingInstructions(SelectionState selectionState)
        {
            if (selectionState.SelectedActionMode == SelectionActionMode.Move)
            {
                return $"{FormatSelectedActionSummary(selectionState)}\nHover highlighted cells to preview their path. Click a reachable destination to move immediately. Escape cancels movement.";
            }

            var targetPrompt = selectionState.HasSelectedTarget
                ? "Confirm Target or click the same target again to declare."
                : "Click a target cell or unit to mark it for confirmation.";
            return $"{FormatSelectedActionSummary(selectionState)}\n{targetPrompt} Escape cancels targeting.";
        }

        private static string FormatSelectedActionLabel(TacticalUnit unit, SelectionActionMode actionMode)
        {
            switch (actionMode)
            {
                case SelectionActionMode.Move:
                    return FormatMoveLabel(FindMoveAbility(unit));
                case SelectionActionMode.Melee:
                case SelectionActionMode.Cone:
                case SelectionActionMode.AreaOfEffect:
                    var ability = FindAbilityForActionMode(unit, actionMode);
                    return ability != null ? FormatAbilityLabel(ability) : actionMode.ToString();
                default:
                    return actionMode.ToString();
            }
        }

        private static AbilityDefinition FindAbilityForActionMode(TacticalUnit unit, SelectionActionMode actionMode)
        {
            if (!TryGetActionModeShape(actionMode, out var shape))
            {
                return null;
            }

            var loadout = unit != null ? unit.GetComponent<UnitAbilityLoadout>() : null;
            if (loadout == null)
            {
                return null;
            }

            var actionAbilities = loadout.GetActionAbilities();
            for (var i = 0; i < actionAbilities.Count; i += 1)
            {
                var ability = actionAbilities[i];
                if (ability != null && ability.Shape == shape)
                {
                    return ability;
                }
            }

            return null;
        }

        private static bool IsConfirmableActionMode(SelectionActionMode actionMode)
        {
            return actionMode == SelectionActionMode.Move
                || actionMode == SelectionActionMode.Melee
                || actionMode == SelectionActionMode.Cone
                || actionMode == SelectionActionMode.AreaOfEffect;
        }

        private static bool TryGetActionModeShape(SelectionActionMode actionMode, out AbilityShape shape)
        {
            switch (actionMode)
            {
                case SelectionActionMode.Melee:
                    shape = AbilityShape.Melee;
                    return true;
                case SelectionActionMode.Cone:
                    shape = AbilityShape.Cone;
                    return true;
                case SelectionActionMode.AreaOfEffect:
                    shape = AbilityShape.Radius;
                    return true;
                default:
                    shape = default;
                    return false;
            }
        }

        private static string FormatAbilityLabel(AbilityDefinition ability)
        {
            if (ability == null)
            {
                return "Missing Ability";
            }

            return $"{ability.DisplayName} ({ability.APCost} AP)";
        }

        private static string FormatUnit(TacticalUnit unit)
        {
            if (unit == null)
            {
                return "None";
            }

            var identity = unit.UnitId.IsAssigned ? $" {unit.UnitId}" : string.Empty;
            return $"{unit.DisplayName}{identity} [{unit.Team}]";
        }

        private static Rect ClampPanelRect(Rect rect)
        {
            rect.width = Mathf.Max(MinimumPanelWidth, rect.width);
            rect.height = Mathf.Max(MinimumPanelHeight, rect.height);
            return rect;
        }
    }

    public enum ActiveActionMenuEntryKind
    {
        Move = 0,
        Ability = 1,
        EndTurn = 2
    }

    public readonly struct ActiveActionMenuEntry
    {
        private ActiveActionMenuEntry(
            ActiveActionMenuEntryKind kind,
            AbilityDefinition ability,
            SelectionActionMode actionMode,
            string label,
            string shortName,
            bool isEnabled,
            string disabledReason)
        {
            Kind = kind;
            Ability = ability;
            ActionMode = actionMode;
            Label = string.IsNullOrWhiteSpace(label) ? kind.ToString() : label.Trim();
            ShortName = string.IsNullOrWhiteSpace(shortName) ? Kind.ToString() : shortName.Trim();
            IsEnabled = isEnabled;
            DisabledReason = isEnabled ? string.Empty : TacticalResult.NormalizeErrorMessage(disabledReason);
        }

        public ActiveActionMenuEntryKind Kind { get; }

        public AbilityDefinition Ability { get; }

        public SelectionActionMode ActionMode { get; }

        public string Label { get; }

        public string ShortName { get; }

        public bool IsEnabled { get; }

        public string DisabledReason { get; }

        public static ActiveActionMenuEntry Move(string label, bool isEnabled, string disabledReason)
        {
            return new ActiveActionMenuEntry(
                ActiveActionMenuEntryKind.Move,
                null,
                SelectionActionMode.Move,
                label,
                "Move",
                isEnabled,
                disabledReason);
        }

        public static ActiveActionMenuEntry ForAbility(
            AbilityDefinition ability,
            SelectionActionMode actionMode,
            string label,
            bool isEnabled,
            string disabledReason)
        {
            var shortName = ability != null ? ability.DisplayName : "Ability";
            return new ActiveActionMenuEntry(
                ActiveActionMenuEntryKind.Ability,
                ability,
                actionMode,
                label,
                shortName,
                isEnabled,
                disabledReason);
        }

        public static ActiveActionMenuEntry EndTurn(string label, bool isEnabled, string disabledReason)
        {
            return new ActiveActionMenuEntry(
                ActiveActionMenuEntryKind.EndTurn,
                null,
                SelectionActionMode.None,
                label,
                "End Turn",
                isEnabled,
                disabledReason);
        }
    }
}
