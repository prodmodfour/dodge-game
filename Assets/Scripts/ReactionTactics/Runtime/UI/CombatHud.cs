using ReactionTactics.Actions;
using ReactionTactics.Turns;
using ReactionTactics.Units;
using UnityEngine;

namespace ReactionTactics.UI
{
    /// <summary>
    /// Lightweight prototype-only combat state overlay. It intentionally uses OnGUI so
    /// the first tactical HUD can show phase ownership without introducing durable UI
    /// architecture before the later action-menu and combat-log tickets.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class CombatHud : MonoBehaviour
    {
        private const float MinimumPanelWidth = 240f;
        private const float MinimumPanelHeight = 120f;

        [SerializeField]
        [Tooltip("Combat manager whose state is shown in the HUD. If empty, one is found automatically.")]
        private CombatManager combatManager;

        [SerializeField]
        [Tooltip("Show the prototype HUD while this component is enabled.")]
        private bool visible = true;

        [SerializeField]
        [Tooltip("Show a small placeholder panel when the scene has no combat manager reference yet.")]
        private bool showWhenCombatManagerMissing = true;

        [SerializeField]
        [Tooltip("Screen-space rectangle used for the combat state HUD panel.")]
        private Rect panelRect = new Rect(12f, 170f, 360f, 150f);

        [SerializeField]
        [Tooltip("Header text shown at the top of the combat HUD panel.")]
        private string panelTitle = "Combat HUD";

        private GUIStyle labelStyle;

        public CombatManager CombatManager
        {
            get { return combatManager; }
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

        public void Configure(CombatManager manager)
        {
            combatManager = manager;
        }

        public static string FormatState(CombatState state)
        {
            if (state == null)
            {
                return "Round: --\n"
                    + "Phase: No combat state\n"
                    + "Active Unit: None\n"
                    + "Current Reactor: None\n"
                    + "Pending Action: None";
            }

            return $"Round: {FormatRound(state.CurrentRound)}\n"
                + $"Phase: {state.Phase}\n"
                + $"Active Unit: {FormatUnit(state.ActiveUnit)}\n"
                + $"Current Reactor: {FormatUnit(state.CurrentReactor)}\n"
                + $"Pending Action: {FormatPendingAction(state.PendingActionIntent)}";
        }

        private void Awake()
        {
            ResolveMissingReferences();
            panelRect = ClampPanelRect(panelRect);
        }

        private void OnGUI()
        {
            if (!visible)
            {
                return;
            }

            ResolveMissingReferences();
            if (combatManager == null && !showWhenCombatManagerMissing)
            {
                return;
            }

            EnsureStyles();
            GUILayout.BeginArea(panelRect, panelTitle, GUI.skin.window);
            GUILayout.Label(combatManager != null
                ? FormatState(combatManager.CurrentState)
                : "Round: --\nPhase: Missing CombatManager\nActive Unit: None\nCurrent Reactor: None\nPending Action: None", labelStyle);
            GUILayout.EndArea();
        }

        private void OnValidate()
        {
            panelRect = ClampPanelRect(panelRect);
        }

        private void ResolveMissingReferences()
        {
            if (combatManager == null)
            {
                combatManager = FindAnyObjectByType<CombatManager>();
            }
        }

        private void EnsureStyles()
        {
            if (labelStyle == null)
            {
                labelStyle = new GUIStyle(GUI.skin.label)
                {
                    wordWrap = true,
                    fontSize = 13,
                    normal = { textColor = Color.white }
                };
            }
        }

        private static string FormatRound(int round)
        {
            return round > 0 ? round.ToString() : "--";
        }

        private static string FormatUnit(TacticalUnit unit)
        {
            if (unit == null)
            {
                return "None";
            }

            return $"{unit.DisplayName} {unit.UnitId} [{unit.Team}]";
        }

        private static string FormatPendingAction(object pendingActionIntent)
        {
            if (pendingActionIntent == null)
            {
                return "None";
            }

            var actionIntent = pendingActionIntent as ActionIntent;
            if (actionIntent != null)
            {
                var abilityName = actionIntent.Ability != null ? actionIntent.Ability.DisplayName : "Unknown Ability";
                return $"{abilityName} by {FormatUnit(actionIntent.Actor)}";
            }

            return pendingActionIntent.ToString();
        }

        private static Rect ClampPanelRect(Rect rect)
        {
            rect.width = Mathf.Max(MinimumPanelWidth, rect.width);
            rect.height = Mathf.Max(MinimumPanelHeight, rect.height);
            return rect;
        }
    }
}
