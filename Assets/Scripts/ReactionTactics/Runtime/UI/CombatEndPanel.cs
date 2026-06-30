using ReactionTactics.Turns;
using ReactionTactics.Units;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace ReactionTactics.UI
{
    /// <summary>
    /// Prototype IMGUI panel shown once combat reaches its terminal CombatOver phase.
    /// It reports Victory or Defeat from the player team's perspective and offers a
    /// lightweight scene restart control for playtesting loops.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class CombatEndPanel : MonoBehaviour
    {
        private const float MinimumPanelWidth = 280f;
        private const float MinimumPanelHeight = 150f;

        [SerializeField]
        [Tooltip("Combat manager whose terminal state is shown. If empty, one is found automatically.")]
        private CombatManager combatManager;

        [SerializeField]
        [Tooltip("Combat event bus used to cache the latest combat-ended event. If empty, one is found automatically.")]
        private CombatEventBus eventBus;

        [SerializeField]
        [Tooltip("Team treated as the player side when labeling Victory or Defeat.")]
        private TeamId playerTeam = TeamId.Player;

        [SerializeField]
        [Tooltip("Show the prototype combat end panel while this component is enabled.")]
        private bool visible = true;

        [SerializeField]
        [Tooltip("Screen-space rectangle used for the victory/defeat panel.")]
        private Rect panelRect = new Rect(390f, 70f, 420f, 180f);

        [SerializeField]
        [Tooltip("Header text shown above the victory/defeat message.")]
        private string panelTitle = "Combat Complete";

        [SerializeField]
        [Tooltip("Show a Restart Scene button when the panel is visible.")]
        private bool showRestartButton = true;

        [SerializeField]
        [Tooltip("Allow the restart shortcut while the combat end panel is visible.")]
        private bool restartHotkeyEnabled = true;

        [SerializeField]
        [Tooltip("Shortcut used to restart the active scene from the combat end panel.")]
        private KeyCode restartShortcut = KeyCode.R;

        private CombatEventBus subscribedEventBus;
        private bool hasCachedEndEvent;
        private bool cachedHasWinningTeam;
        private TeamId cachedWinningTeam = TeamId.Player;
        private GUIStyle titleStyle;
        private GUIStyle messageStyle;
        private GUIStyle hintStyle;

        public CombatManager CombatManager
        {
            get { return combatManager; }
            set { combatManager = value; }
        }

        public CombatEventBus EventBus
        {
            get { return eventBus; }
        }

        public TeamId PlayerTeam
        {
            get { return playerTeam; }
            set { playerTeam = value; }
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

        public bool HasCachedEndEvent
        {
            get { return hasCachedEndEvent; }
        }

        public void Configure(CombatManager manager, CombatEventBus bus)
        {
            combatManager = manager;
            if (eventBus == bus)
            {
                if (isActiveAndEnabled)
                {
                    SubscribeToEventBus();
                }

                return;
            }

            UnsubscribeFromEventBus();
            eventBus = bus;
            if (isActiveAndEnabled)
            {
                SubscribeToEventBus();
            }
        }

        public static string FormatOutcome(bool hasWinningTeam, TeamId winningTeam, TeamId playerTeam)
        {
            if (!hasWinningTeam)
            {
                return "Combat ended\nNo team has living units.\nResult: Stalemate";
            }

            var outcome = winningTeam == playerTeam ? "Victory" : "Defeat";
            return $"{outcome}\n{winningTeam} team wins.";
        }

        public string GetDisplayText()
        {
            if (TryGetCurrentOutcome(out var hasWinningTeam, out var winningTeam))
            {
                return FormatOutcome(hasWinningTeam, winningTeam, playerTeam);
            }

            return "Combat is still in progress.";
        }

        private void Awake()
        {
            panelRect = ClampPanelRect(panelRect);
            ResolveMissingReferences();
        }

        private void OnEnable()
        {
            ResolveMissingReferences();
            SubscribeToEventBus();
        }

        private void OnDisable()
        {
            UnsubscribeFromEventBus();
        }

        private void Update()
        {
            if (!visible || !restartHotkeyEnabled || restartShortcut == KeyCode.None || !ShouldDraw())
            {
                return;
            }

            if (UnityEngine.Input.GetKeyDown(restartShortcut))
            {
                RestartActiveScene();
            }
        }

        private void OnGUI()
        {
            if (!visible || !ShouldDraw())
            {
                return;
            }

            EnsureStyles();
            GUILayout.BeginArea(panelRect, panelTitle, GUI.skin.window);
            var displayText = GetDisplayText();
            var lines = displayText.Split('\n');
            if (lines.Length > 0)
            {
                GUILayout.Label(lines[0], titleStyle);
                for (var i = 1; i < lines.Length; i += 1)
                {
                    GUILayout.Label(lines[i], messageStyle);
                }
            }

            GUILayout.Space(8f);
            GUILayout.Label($"Actions and reactions are disabled during {CombatPhase.CombatOver}.", hintStyle);
            if (showRestartButton)
            {
                GUILayout.Space(6f);
                if (GUILayout.Button($"Restart Scene ({restartShortcut})", GUILayout.Height(30f)))
                {
                    RestartActiveScene();
                }
            }

            GUILayout.EndArea();
        }

        private void OnValidate()
        {
            panelRect = ClampPanelRect(panelRect);
        }

        private bool ShouldDraw()
        {
            ResolveMissingReferences();
            SubscribeToEventBus();
            return TryGetCurrentOutcome(out _, out _);
        }

        private bool TryGetCurrentOutcome(out bool hasWinningTeam, out TeamId winningTeam)
        {
            if (combatManager != null
                && combatManager.CurrentState != null
                && combatManager.CurrentState.IsCombatOver
                && combatManager.HasCombatEndOutcome)
            {
                hasWinningTeam = combatManager.HasWinningTeam;
                winningTeam = combatManager.WinningTeam;
                return true;
            }

            if (hasCachedEndEvent)
            {
                hasWinningTeam = cachedHasWinningTeam;
                winningTeam = cachedWinningTeam;
                return true;
            }

            hasWinningTeam = false;
            winningTeam = TeamId.Player;
            return false;
        }

        private void HandleCombatEnded(CombatEndedEvent eventData)
        {
            hasCachedEndEvent = true;
            cachedHasWinningTeam = eventData.HasWinningTeam;
            cachedWinningTeam = eventData.WinningTeam;
        }

        private void ResolveMissingReferences()
        {
            if (combatManager == null)
            {
                combatManager = FindAnyObjectByType<CombatManager>();
            }

            if (eventBus == null)
            {
                eventBus = FindAnyObjectByType<CombatEventBus>();
            }
        }

        private void SubscribeToEventBus()
        {
            if (eventBus == null || subscribedEventBus == eventBus)
            {
                return;
            }

            UnsubscribeFromEventBus();
            subscribedEventBus = eventBus;
            subscribedEventBus.CombatEnded += HandleCombatEnded;
        }

        private void UnsubscribeFromEventBus()
        {
            if (subscribedEventBus == null)
            {
                return;
            }

            subscribedEventBus.CombatEnded -= HandleCombatEnded;
            subscribedEventBus = null;
        }

        private static void RestartActiveScene()
        {
            var activeScene = SceneManager.GetActiveScene();
            if (!activeScene.IsValid())
            {
                return;
            }

            if (activeScene.buildIndex >= 0)
            {
                SceneManager.LoadScene(activeScene.buildIndex);
                return;
            }

            SceneManager.LoadScene(activeScene.name);
        }

        private void EnsureStyles()
        {
            if (titleStyle == null)
            {
                titleStyle = new GUIStyle(GUI.skin.label)
                {
                    alignment = TextAnchor.MiddleCenter,
                    fontSize = 26,
                    fontStyle = FontStyle.Bold,
                    normal = { textColor = Color.white }
                };
            }

            if (messageStyle == null)
            {
                messageStyle = new GUIStyle(GUI.skin.label)
                {
                    alignment = TextAnchor.MiddleCenter,
                    wordWrap = true,
                    fontSize = 15,
                    normal = { textColor = new Color(0.92f, 0.97f, 1f, 1f) }
                };
            }

            if (hintStyle == null)
            {
                hintStyle = new GUIStyle(GUI.skin.label)
                {
                    alignment = TextAnchor.MiddleCenter,
                    wordWrap = true,
                    fontSize = 12,
                    normal = { textColor = new Color(1f, 0.86f, 0.55f, 1f) }
                };
            }
        }

        private static Rect ClampPanelRect(Rect rect)
        {
            rect.width = Mathf.Max(MinimumPanelWidth, rect.width);
            rect.height = Mathf.Max(MinimumPanelHeight, rect.height);
            return rect;
        }
    }
}
