using System;
using System.Collections.Generic;
using ReactionTactics.Actions;
using ReactionTactics.Core;
using ReactionTactics.Input;
using ReactionTactics.Reactions;
using ReactionTactics.Turns;
using ReactionTactics.Units;
using UnityEngine;

namespace ReactionTactics.UI
{
    /// <summary>
    /// Lightweight IMGUI menu for the currently active reaction turn. It mirrors
    /// keyboard shortcuts by routing button clicks through <see cref="PlayerCommandRouter" />
    /// and keeps reaction command execution inside combat systems.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class ReactionMenu : MonoBehaviour
    {
        private const float MinimumPanelWidth = 260f;
        private const float MinimumPanelHeight = 150f;
        private const string MoveAbilityKey = "move";
        private const string BraceAbilityKey = "brace";
        private const string BraceAbilityDisplayName = "Brace";

        [SerializeField]
        [Tooltip("Command router that receives reaction menu button requests.")]
        private PlayerCommandRouter commandRouter;

        [SerializeField]
        [Tooltip("Combat manager whose reaction phase, current reactor, and pending action decide menu state.")]
        private CombatManager combatManager;

        [SerializeField]
        [Tooltip("Show the reaction menu while this component is enabled. The panel still only draws during reaction windows by default.")]
        private bool visible = true;

        [SerializeField]
        [Tooltip("Draw a compact disabled hint outside reaction windows. Leave off for normal prototype play.")]
        private bool showWhenNotReacting;

        [SerializeField]
        [Tooltip("Screen-space rectangle used for the reaction menu panel.")]
        private Rect panelRect = new Rect(388f, 330f, 340f, 210f);

        [SerializeField]
        [Tooltip("Header text shown at the top of the reaction menu panel.")]
        private string panelTitle = "Reaction Menu";

        [SerializeField]
        [Tooltip("When true, disabled button reasons are shown under the button list.")]
        private bool showDisabledReasons = true;

        private GUIStyle headerStyle;
        private GUIStyle detailStyle;
        private GUIStyle disabledReasonStyle;
        private string lastFeedback = string.Empty;

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

        public bool ShowWhenNotReacting
        {
            get { return showWhenNotReacting; }
            set { showWhenNotReacting = value; }
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

        public static IReadOnlyList<ReactionMenuEntry> BuildMenuEntries(CombatState combatState)
        {
            return BuildMenuEntries(combatState != null ? combatState.CurrentReactor : null, combatState);
        }

        public static IReadOnlyList<ReactionMenuEntry> BuildMenuEntries(
            TacticalUnit currentReactor,
            CombatState combatState)
        {
            var entries = new List<ReactionMenuEntry>();
            var reactionLegality = ValidateCurrentReactionTurn(currentReactor, combatState);

            AddReactionMoveEntry(entries, currentReactor, reactionLegality);
            AddBraceEntry(entries, currentReactor, reactionLegality);
            entries.Add(ReactionMenuEntry.Pass(
                FormatPassLabel(FindPassAbility(currentReactor)),
                string.IsNullOrEmpty(reactionLegality),
                reactionLegality));

            return entries;
        }

        public static string FormatReactionSummary(CombatState combatState)
        {
            if (combatState == null)
            {
                return "Phase: No Combat\nPending: None\nActor: None\nReactor: None";
            }

            var intent = combatState.PendingActionIntent as ActionIntent;
            var pendingLabel = intent != null && intent.Ability != null
                ? intent.Ability.DisplayName
                : "None";
            var actor = intent != null ? intent.Actor : combatState.ActiveUnit;
            var reactor = combatState.CurrentReactor;

            return $"Phase: {combatState.Phase}\n"
                + $"Pending: {pendingLabel}\n"
                + $"Actor: {FormatUnit(actor)}\n"
                + $"Reactor: {FormatUnit(reactor)}\n"
                + $"AP: {FormatAp(reactor)}";
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
            var combatState = combatManager != null ? combatManager.CurrentState : null;
            if (!ShouldDraw(combatState))
            {
                return;
            }

            EnsureStyles();
            var entries = BuildMenuEntries(combatState);

            GUILayout.BeginArea(panelRect, panelTitle, GUI.skin.window);
            GUILayout.Label(FormatReactionSummary(combatState), headerStyle);
            GUILayout.Space(4f);

            for (var i = 0; i < entries.Count; i += 1)
            {
                DrawEntry(entries[i]);
            }

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

        private bool ShouldDraw(CombatState combatState)
        {
            if (combatState != null && combatState.IsReactionPhase)
            {
                return true;
            }

            return showWhenNotReacting;
        }

        private void DrawEntry(ReactionMenuEntry entry)
        {
            var previousEnabled = GUI.enabled;
            GUI.enabled = previousEnabled && entry.IsEnabled;
            if (GUILayout.Button(entry.Label, GUILayout.Height(28f)))
            {
                ExecuteEntry(entry);
            }

            GUI.enabled = previousEnabled;
        }

        private void DrawDisabledReasons(IReadOnlyList<ReactionMenuEntry> entries)
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

        private TacticalResult ExecuteEntry(ReactionMenuEntry entry)
        {
            if (!entry.IsEnabled)
            {
                lastFeedback = entry.DisabledReason;
                return TacticalResult.Failure(entry.DisabledReason);
            }

            var router = ResolveCommandRouter();
            if (router == null)
            {
                lastFeedback = "Reaction menu requires a PlayerCommandRouter.";
                return TacticalResult.Failure(lastFeedback);
            }

            TacticalResult result;
            switch (entry.Kind)
            {
                case ReactionMenuEntryKind.ReactionMove:
                    result = router.SelectMove();
                    break;
                case ReactionMenuEntryKind.Brace:
                    result = router.SelectBraceReaction();
                    break;
                case ReactionMenuEntryKind.Pass:
                    result = router.RequestPassReaction();
                    break;
                default:
                    result = TacticalResult.Failure($"Unsupported reaction menu entry '{entry.Kind}'.");
                    break;
            }

            lastFeedback = result.IsSuccess
                ? FormatSuccessFeedback(entry)
                : result.ErrorMessage;
            return result;
        }

        private static string FormatSuccessFeedback(ReactionMenuEntry entry)
        {
            switch (entry.Kind)
            {
                case ReactionMenuEntryKind.ReactionMove:
                    return "Selected Reaction Move. Click a reachable destination to spend path AP.";
                case ReactionMenuEntryKind.Brace:
                    return "Selected Brace.";
                case ReactionMenuEntryKind.Pass:
                    return "Passed reaction.";
                default:
                    return $"Selected {entry.ShortName}.";
            }
        }

        private void ResolveMissingReferences()
        {
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

        private static void AddReactionMoveEntry(
            ICollection<ReactionMenuEntry> entries,
            TacticalUnit currentReactor,
            string reactionLegality)
        {
            var moveAbility = FindMoveAbility(currentReactor);
            var disabledReason = reactionLegality;
            var enabled = string.IsNullOrEmpty(disabledReason);

            if (enabled && moveAbility == null)
            {
                enabled = false;
                disabledReason = "No reaction movement ability is assigned.";
            }

            if (enabled && currentReactor.CurrentAP <= 0)
            {
                enabled = false;
                disabledReason = "Reaction Move requires AP to spend on a path.";
            }

            entries.Add(ReactionMenuEntry.ReactionMove(
                FormatReactionMoveLabel(moveAbility),
                enabled,
                disabledReason));
        }

        private static void AddBraceEntry(
            ICollection<ReactionMenuEntry> entries,
            TacticalUnit currentReactor,
            string reactionLegality)
        {
            var braceAbility = FindBraceAbility(currentReactor);
            var braceCost = braceAbility != null ? braceAbility.APCost : BraceReactionCommand.DefaultApCost;
            var disabledReason = reactionLegality;
            var enabled = string.IsNullOrEmpty(disabledReason);

            if (enabled && braceAbility == null)
            {
                enabled = false;
                disabledReason = "No Brace reaction ability is assigned.";
            }

            if (enabled && currentReactor.BracedUntilNextHit)
            {
                enabled = false;
                disabledReason = $"{currentReactor.DisplayName} is already braced.";
            }

            if (enabled && currentReactor.CurrentAP < braceCost)
            {
                enabled = false;
                disabledReason = $"Needs {braceCost} AP; {currentReactor.DisplayName} has {currentReactor.CurrentAP} AP.";
            }

            entries.Add(ReactionMenuEntry.Brace(
                FormatBraceLabel(braceAbility, braceCost),
                braceCost,
                enabled,
                disabledReason));
        }

        private static string ValidateCurrentReactionTurn(TacticalUnit currentReactor, CombatState combatState)
        {
            if (combatState == null)
            {
                return "Combat has no current state.";
            }

            if (!combatState.IsReactionPhase)
            {
                return $"Reactions are unavailable during {combatState.Phase}.";
            }

            if (combatState.PendingActionIntent as ActionIntent == null)
            {
                return "No pending action is available for this reaction turn.";
            }

            if (currentReactor == null)
            {
                return "No current reactor is waiting for input.";
            }

            if (!ReferenceEquals(currentReactor, combatState.CurrentReactor))
            {
                return $"Only current reactor {FormatUnit(combatState.CurrentReactor)} may react now.";
            }

            if (!currentReactor.IsAlive)
            {
                return $"{currentReactor.DisplayName} is defeated.";
            }

            return string.Empty;
        }

        private static AbilityDefinition FindMoveAbility(TacticalUnit unit)
        {
            return FindReactionAbility(
                unit,
                ability => string.Equals(ability.AbilityKey, MoveAbilityKey, StringComparison.OrdinalIgnoreCase));
        }

        private static AbilityDefinition FindBraceAbility(TacticalUnit unit)
        {
            return FindReactionAbility(
                unit,
                ability => string.Equals(ability.AbilityKey, BraceAbilityKey, StringComparison.OrdinalIgnoreCase)
                    || string.Equals(ability.DisplayName, BraceAbilityDisplayName, StringComparison.OrdinalIgnoreCase));
        }

        private static AbilityDefinition FindPassAbility(TacticalUnit unit)
        {
            return FindReactionAbility(unit, ReactionEligibilityService.IsZeroCostPassReaction);
        }

        private static AbilityDefinition FindReactionAbility(
            TacticalUnit unit,
            Predicate<AbilityDefinition> predicate)
        {
            if (unit == null || predicate == null)
            {
                return null;
            }

            var loadout = unit.GetComponent<UnitAbilityLoadout>();
            if (loadout == null)
            {
                return null;
            }

            var reactionAbilities = loadout.GetReactionAbilities();
            for (var i = 0; i < reactionAbilities.Count; i += 1)
            {
                var ability = reactionAbilities[i];
                if (ability != null && predicate(ability))
                {
                    return ability;
                }
            }

            return null;
        }

        private static string FormatReactionMoveLabel(AbilityDefinition moveAbility)
        {
            var name = moveAbility != null ? moveAbility.DisplayName : "Reaction Move";
            return string.Equals(name, "Move", StringComparison.OrdinalIgnoreCase)
                ? "Reaction Move (path AP)"
                : $"{name} (path AP)";
        }

        private static string FormatBraceLabel(AbilityDefinition braceAbility, int braceCost)
        {
            var name = braceAbility != null ? braceAbility.DisplayName : "Brace";
            return $"{name} ({braceCost} AP)";
        }

        private static string FormatPassLabel(AbilityDefinition passAbility)
        {
            var name = passAbility != null ? passAbility.DisplayName : "Pass Reaction";
            return $"{name} (0 AP)";
        }

        private static string FormatAp(TacticalUnit unit)
        {
            return unit != null ? $"{unit.CurrentAP}/{unit.MaxAP}" : "--";
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

    public enum ReactionMenuEntryKind
    {
        ReactionMove = 0,
        Brace = 1,
        Pass = 2
    }

    public readonly struct ReactionMenuEntry
    {
        private ReactionMenuEntry(
            ReactionMenuEntryKind kind,
            string label,
            string shortName,
            int apCost,
            bool isEnabled,
            string disabledReason)
        {
            Kind = kind;
            Label = string.IsNullOrWhiteSpace(label) ? kind.ToString() : label.Trim();
            ShortName = string.IsNullOrWhiteSpace(shortName) ? Kind.ToString() : shortName.Trim();
            APCost = Math.Max(0, apCost);
            IsEnabled = isEnabled;
            DisabledReason = isEnabled ? string.Empty : TacticalResult.NormalizeErrorMessage(disabledReason);
        }

        public ReactionMenuEntryKind Kind { get; }

        public string Label { get; }

        public string ShortName { get; }

        public int APCost { get; }

        public bool IsEnabled { get; }

        public string DisabledReason { get; }

        public static ReactionMenuEntry ReactionMove(string label, bool isEnabled, string disabledReason)
        {
            return new ReactionMenuEntry(
                ReactionMenuEntryKind.ReactionMove,
                label,
                "Reaction Move",
                0,
                isEnabled,
                disabledReason);
        }

        public static ReactionMenuEntry Brace(string label, int apCost, bool isEnabled, string disabledReason)
        {
            return new ReactionMenuEntry(
                ReactionMenuEntryKind.Brace,
                label,
                "Brace",
                apCost,
                isEnabled,
                disabledReason);
        }

        public static ReactionMenuEntry Pass(string label, bool isEnabled, string disabledReason)
        {
            return new ReactionMenuEntry(
                ReactionMenuEntryKind.Pass,
                label,
                "Pass",
                0,
                isEnabled,
                disabledReason);
        }
    }
}
