using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Text;
using UnityEngine;

namespace ReactionTactics.UI
{
    /// <summary>
    /// In-game prototype help panel for playtesters. The overlay is intentionally
    /// lightweight IMGUI and is toggled with H so the unusual action/reaction rules
    /// are visible inside editor and standalone builds without adding permanent UI.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class PrototypeRulesHelpOverlay : MonoBehaviour
    {
        private const float MinimumPanelWidth = 320f;
        private const float MinimumPanelHeight = 180f;
        private const float ScreenMargin = 8f;

        private static readonly ReadOnlyCollection<string> CoreRules = new ReadOnlyCollection<string>(new[]
        {
            "Actions only on your turn.",
            "Reactions only off-turn during another unit's action window.",
            "Every action that triggers reactions opens turns for other living units, ordered by distance from the acting unit.",
            "Melee hits only if the target remains in range when the action resolves.",
            "Move out of cones or AoEs during reactions to avoid them.",
            "No dodge chance, accuracy roll, or random hit roll decides whether attacks connect."
        });

        [SerializeField]
        [Tooltip("Show the rules overlay. Press the toggle shortcut to hide or show it while playing.")]
        private bool visible = true;

        [SerializeField]
        [Tooltip("Draw a small shortcut hint when the full rules overlay is hidden.")]
        private bool showClosedHint = true;

        [SerializeField]
        [Tooltip("Keyboard shortcut that toggles the prototype rules overlay.")]
        private KeyCode toggleShortcut = KeyCode.H;

        [SerializeField]
        [Tooltip("Screen-space rectangle used for the prototype rules panel.")]
        private Rect panelRect = new Rect(388f, 12f, 460f, 260f);

        [SerializeField]
        [Tooltip("Screen-space rectangle used for the compact hidden-state shortcut hint.")]
        private Rect hintRect = new Rect(12f, 12f, 160f, 32f);

        [SerializeField]
        [Tooltip("Header text shown at the top of the help panel.")]
        private string panelTitle = "Prototype Rules (H)";

        private GUIStyle bodyStyle;
        private GUIStyle footerStyle;
        private GUIStyle hintStyle;

        public static IReadOnlyList<string> Rules
        {
            get { return CoreRules; }
        }

        public bool Visible
        {
            get { return visible; }
            set { visible = value; }
        }

        public bool ShowClosedHint
        {
            get { return showClosedHint; }
            set { showClosedHint = value; }
        }

        public KeyCode ToggleShortcut
        {
            get { return toggleShortcut; }
            set { toggleShortcut = value; }
        }

        public Rect PanelRect
        {
            get { return panelRect; }
            set { panelRect = ClampPanelRect(value); }
        }

        public Rect HintRect
        {
            get { return hintRect; }
            set { hintRect = ClampHintRect(value); }
        }

        public void Toggle()
        {
            visible = !visible;
        }

        public static string BuildRulesText()
        {
            var builder = new StringBuilder();
            for (var i = 0; i < CoreRules.Count; i += 1)
            {
                builder.Append("• ");
                builder.Append(CoreRules[i]);
                if (i < CoreRules.Count - 1)
                {
                    builder.AppendLine();
                }
            }

            return builder.ToString();
        }

        public static string FormatShortcutHint(KeyCode shortcut, bool isVisible)
        {
            var key = shortcut == KeyCode.None ? "Unbound" : shortcut.ToString();
            return isVisible
                ? $"Press {key} to hide these rules."
                : $"Press {key} for rules.";
        }

        private void Awake()
        {
            panelRect = ClampPanelRect(panelRect);
            hintRect = ClampHintRect(hintRect);
        }

        private void Update()
        {
            if (toggleShortcut != KeyCode.None && UnityEngine.Input.GetKeyDown(toggleShortcut))
            {
                Toggle();
            }
        }

        private void OnGUI()
        {
            EnsureStyles();

            if (visible)
            {
                DrawRulesPanel();
                return;
            }

            if (showClosedHint)
            {
                DrawClosedHint();
            }
        }

        private void OnValidate()
        {
            panelRect = ClampPanelRect(panelRect);
            hintRect = ClampHintRect(hintRect);
        }

        private void DrawRulesPanel()
        {
            GUILayout.BeginArea(FitRectToScreen(panelRect), panelTitle, GUI.skin.window);
            GUILayout.Label(BuildRulesText(), bodyStyle);
            GUILayout.Space(6f);
            GUILayout.Label(FormatShortcutHint(toggleShortcut, isVisible: true), footerStyle);
            GUILayout.EndArea();
        }

        private void DrawClosedHint()
        {
            GUI.Box(FitRectToScreen(hintRect), FormatShortcutHint(toggleShortcut, isVisible: false), hintStyle);
        }

        private void EnsureStyles()
        {
            if (bodyStyle == null)
            {
                bodyStyle = new GUIStyle(GUI.skin.label)
                {
                    wordWrap = true,
                    fontSize = 14,
                    normal = { textColor = Color.white }
                };
            }

            if (footerStyle == null)
            {
                footerStyle = new GUIStyle(GUI.skin.label)
                {
                    wordWrap = true,
                    fontSize = 12,
                    fontStyle = FontStyle.Italic,
                    normal = { textColor = new Color(0.82f, 0.92f, 1f, 1f) }
                };
            }

            if (hintStyle == null)
            {
                hintStyle = new GUIStyle(GUI.skin.box)
                {
                    alignment = TextAnchor.MiddleCenter,
                    fontSize = 12,
                    normal = { textColor = Color.white }
                };
            }
        }

        private static Rect ClampPanelRect(Rect rect)
        {
            rect.width = Mathf.Max(MinimumPanelWidth, rect.width);
            rect.height = Mathf.Max(MinimumPanelHeight, rect.height);
            return rect;
        }

        private static Rect ClampHintRect(Rect rect)
        {
            rect.width = Mathf.Max(120f, rect.width);
            rect.height = Mathf.Max(28f, rect.height);
            return rect;
        }

        private static Rect FitRectToScreen(Rect rect)
        {
            if (Screen.width <= 0 || Screen.height <= 0)
            {
                return rect;
            }

            var maxX = Mathf.Max(ScreenMargin, Screen.width - rect.width - ScreenMargin);
            var maxY = Mathf.Max(ScreenMargin, Screen.height - rect.height - ScreenMargin);
            rect.x = Mathf.Clamp(rect.x, ScreenMargin, maxX);
            rect.y = Mathf.Clamp(rect.y, ScreenMargin, maxY);
            return rect;
        }
    }
}
