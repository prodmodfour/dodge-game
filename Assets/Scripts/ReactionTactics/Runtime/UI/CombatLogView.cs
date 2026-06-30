using System.Collections.Generic;
using System.Collections.ObjectModel;
using ReactionTactics.Turns;
using UnityEngine;

namespace ReactionTactics.UI
{
    /// <summary>
    /// Prototype combat log panel that shows plain-language deterministic combat outcomes.
    /// It subscribes to the scene-scoped <see cref="CombatEventBus" /> and renders a
    /// fixed-size IMGUI scroll panel so hits, movement avoids, brace reductions, deaths,
    /// reaction order, and round starts are visible without reading the Unity console.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class CombatLogView : MonoBehaviour
    {
        private const float MinimumPanelWidth = 280f;
        private const float MinimumPanelHeight = 140f;
        private const int MinimumEntries = 1;
        private const int MaximumEntries = 200;

        [SerializeField]
        [Tooltip("Combat event bus that publishes plain-language combat log entries. If empty, one is found automatically.")]
        private CombatEventBus eventBus;

        [SerializeField]
        [Tooltip("Show the prototype combat log while this component is enabled.")]
        private bool visible = true;

        [SerializeField]
        [Tooltip("Screen-space rectangle used for the combat log panel.")]
        private Rect panelRect = new Rect(744f, 170f, 440f, 330f);

        [SerializeField]
        [Tooltip("Header text shown at the top of the combat log panel.")]
        private string panelTitle = "Combat Log";

        [SerializeField]
        [Range(MinimumEntries, MaximumEntries)]
        [Tooltip("Maximum number of recent combat log entries retained in the panel.")]
        private int maxEntries = 24;

        [SerializeField]
        [Tooltip("Show a compact sequence number before each combat log entry.")]
        private bool showSequenceNumbers = true;

        [SerializeField]
        [Tooltip("When true, the panel jumps to the newest line whenever a new entry arrives.")]
        private bool autoScrollToNewest = true;

        private readonly List<CombatLogEntry> entries = new List<CombatLogEntry>();
        private ReadOnlyCollection<CombatLogEntry> entriesView;
        private CombatEventBus subscribedEventBus;
        private Vector2 scrollPosition;
        private bool shouldScrollToBottom;
        private GUIStyle entryStyle;
        private GUIStyle emptyStyle;

        public CombatEventBus EventBus
        {
            get { return eventBus; }
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

        public int MaxEntries
        {
            get { return maxEntries; }
            set
            {
                maxEntries = Mathf.Clamp(value, MinimumEntries, MaximumEntries);
                TrimToMaxEntries();
            }
        }

        public IReadOnlyList<CombatLogEntry> Entries
        {
            get
            {
                if (entriesView == null)
                {
                    entriesView = entries.AsReadOnly();
                }

                return entriesView;
            }
        }

        public void Configure(CombatEventBus bus)
        {
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

        public void AddEntry(CombatLogEntry entry)
        {
            entries.Add(entry);
            TrimToMaxEntries();
            if (autoScrollToNewest)
            {
                shouldScrollToBottom = true;
            }
        }

        public void ClearEntries()
        {
            entries.Clear();
            scrollPosition = Vector2.zero;
            shouldScrollToBottom = false;
        }

        public static string FormatEntry(CombatLogEntry entry, bool includeSequenceNumber)
        {
            return includeSequenceNumber
                ? $"#{entry.SequenceNumber:00} {entry.Message}"
                : entry.Message;
        }

        private void Awake()
        {
            panelRect = ClampPanelRect(panelRect);
            maxEntries = Mathf.Clamp(maxEntries, MinimumEntries, MaximumEntries);
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

        private void OnGUI()
        {
            if (!visible)
            {
                return;
            }

            ResolveMissingReferences();
            SubscribeToEventBus();
            EnsureStyles();

            if (shouldScrollToBottom)
            {
                scrollPosition.y = float.MaxValue;
                shouldScrollToBottom = false;
            }

            GUILayout.BeginArea(panelRect, panelTitle, GUI.skin.window);
            scrollPosition = GUILayout.BeginScrollView(
                scrollPosition,
                GUILayout.Height(Mathf.Max(40f, panelRect.height - 48f)));

            if (entries.Count == 0)
            {
                GUILayout.Label("No combat log entries yet. Declare an action to see reaction order and deterministic outcomes.", emptyStyle);
            }
            else
            {
                for (var i = 0; i < entries.Count; i += 1)
                {
                    GUILayout.Label(FormatEntry(entries[i], showSequenceNumbers), entryStyle);
                    if (i < entries.Count - 1)
                    {
                        GUILayout.Space(3f);
                    }
                }
            }

            GUILayout.EndScrollView();
            GUILayout.EndArea();
        }

        private void OnValidate()
        {
            panelRect = ClampPanelRect(panelRect);
            maxEntries = Mathf.Clamp(maxEntries, MinimumEntries, MaximumEntries);
            TrimToMaxEntries();
        }

        private void HandleCombatLogMessageAdded(CombatLogEntry entry)
        {
            AddEntry(entry);
        }

        private void ResolveMissingReferences()
        {
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
            subscribedEventBus.CombatLogMessageAdded += HandleCombatLogMessageAdded;
        }

        private void UnsubscribeFromEventBus()
        {
            if (subscribedEventBus == null)
            {
                return;
            }

            subscribedEventBus.CombatLogMessageAdded -= HandleCombatLogMessageAdded;
            subscribedEventBus = null;
        }

        private void TrimToMaxEntries()
        {
            if (entries.Count <= maxEntries)
            {
                return;
            }

            entries.RemoveRange(0, entries.Count - maxEntries);
        }

        private void EnsureStyles()
        {
            if (entryStyle == null)
            {
                entryStyle = new GUIStyle(GUI.skin.label)
                {
                    wordWrap = true,
                    fontSize = 12,
                    normal = { textColor = new Color(0.92f, 0.97f, 1f, 1f) }
                };
            }

            if (emptyStyle == null)
            {
                emptyStyle = new GUIStyle(GUI.skin.label)
                {
                    wordWrap = true,
                    fontSize = 12,
                    fontStyle = FontStyle.Italic,
                    normal = { textColor = new Color(0.75f, 0.82f, 0.9f, 1f) }
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
