using ReactionTactics.Turns;
using ReactionTactics.Units;
using UnityEngine;

namespace ReactionTactics.UI
{
    /// <summary>
    /// Prototype nameplate drawn above a tactical unit. It shows the unit name,
    /// team, HP, and shared AP so players can see which combatants have saved
    /// resources for reactions. The view listens to combat events for HP/AP
    /// changes and also refreshes defensively before drawing in case a unit was
    /// initialized before the event bus was available.
    /// </summary>
    [DisallowMultipleComponent]
    [RequireComponent(typeof(TacticalUnit))]
    public sealed class UnitStatusView : MonoBehaviour
    {
        private const float MinimumPlateWidth = 96f;
        private const float MinimumPlateHeight = 36f;
        private const int MinimumFontSize = 9;
        private const int MaximumFontSize = 24;

        [SerializeField]
        [Tooltip("Unit represented by this status view. Defaults to the TacticalUnit on the same GameObject.")]
        private TacticalUnit tacticalUnit;

        [SerializeField]
        [Tooltip("Optional scene event bus. If unset, the view finds the scene bus at runtime.")]
        private CombatEventBus eventBus;

        [SerializeField]
        [Tooltip("Camera used to project the world-space unit position into a screen-space nameplate. Defaults to Camera.main.")]
        private Camera targetCamera;

        [SerializeField]
        [Tooltip("Show the unit status nameplate while this component is enabled.")]
        private bool visible = true;

        [SerializeField]
        [Tooltip("When true, defeated units are hidden. When false, defeated units are clearly marked as DEFEATED.")]
        private bool hideDeadUnits;

        [SerializeField]
        [Tooltip("Hide the nameplate when the unit is behind the target camera.")]
        private bool hideWhenBehindCamera = true;

        [SerializeField]
        [Tooltip("World-space offset above the unit root used as the anchor for the screen-space nameplate.")]
        private Vector3 worldOffset = new Vector3(0f, 2.25f, 0f);

        [SerializeField]
        [Tooltip("Screen-space size of the nameplate in pixels.")]
        private Vector2 plateSize = new Vector2(152f, 58f);

        [SerializeField]
        [Range(MinimumFontSize, MaximumFontSize)]
        [Tooltip("Font size used by the status text.")]
        private int fontSize = 12;

        [SerializeField]
        [Tooltip("Background tint for player-team units.")]
        private Color playerBackgroundColor = new Color(0.05f, 0.22f, 0.72f, 0.78f);

        [SerializeField]
        [Tooltip("Background tint for enemy-team units.")]
        private Color enemyBackgroundColor = new Color(0.7f, 0.12f, 0.08f, 0.78f);

        [SerializeField]
        [Tooltip("Background tint for defeated units when dead nameplates are shown.")]
        private Color defeatedBackgroundColor = new Color(0.18f, 0.18f, 0.18f, 0.82f);

        [SerializeField]
        [Tooltip("Text color for living unit nameplates.")]
        private Color textColor = Color.white;

        [SerializeField]
        [Tooltip("Text color for defeated unit nameplates.")]
        private Color defeatedTextColor = new Color(1f, 0.75f, 0.75f, 1f);

        private string statusText = "Unit status unavailable";
        private GUIStyle textStyle;
        private GUIStyle borderStyle;
        private bool isSubscribedToBus;
        private bool isSubscribedToDeath;

        public TacticalUnit Unit
        {
            get { return tacticalUnit; }
        }

        public CombatEventBus EventBus
        {
            get { return eventBus; }
        }

        public Camera TargetCamera
        {
            get { return targetCamera; }
        }

        public bool Visible
        {
            get { return visible; }
            set { visible = value; }
        }

        public bool HideDeadUnits
        {
            get { return hideDeadUnits; }
            set { hideDeadUnits = value; }
        }

        public string StatusText
        {
            get { return statusText; }
        }

        public bool IsMarkedDead
        {
            get { return tacticalUnit != null && tacticalUnit.IsDead && !hideDeadUnits; }
        }

        public Vector2 PlateSize
        {
            get { return plateSize; }
            set { plateSize = SanitizePlateSize(value); }
        }

        public void Configure(TacticalUnit unit, CombatEventBus bus, Camera camera = null)
        {
            if (!ReferenceEquals(tacticalUnit, unit))
            {
                UnsubscribeFromDeathEvent();
                tacticalUnit = unit;
            }

            if (!ReferenceEquals(eventBus, bus))
            {
                UnsubscribeFromEventBus();
                eventBus = bus;
            }

            targetCamera = camera;

            if (isActiveAndEnabled)
            {
                SubscribeToDeathEvent();
                SubscribeToEventBus();
            }

            RefreshStatus();
        }

        public void RefreshStatus()
        {
            ResolveLocalReference();
            statusText = FormatStatus(tacticalUnit);
        }

        public static string FormatStatus(TacticalUnit unit)
        {
            if (unit == null)
            {
                return "No Unit\nHP --/--  AP --/--";
            }

            var displayName = string.IsNullOrWhiteSpace(unit.DisplayName) ? unit.name : unit.DisplayName;
            var identity = unit.UnitId.IsAssigned ? $" {unit.UnitId}" : string.Empty;
            var header = $"{displayName}{identity} [{unit.Team}]";
            var resourceLine = $"HP {unit.CurrentHP}/{unit.MaxHP}  AP {unit.CurrentAP}/{unit.MaxAP}";

            if (unit.IsDead)
            {
                return $"{header}\nDEFEATED\n{resourceLine}";
            }

            return $"{header}\n{resourceLine}";
        }

        private void Awake()
        {
            ResolveLocalReference();
            plateSize = SanitizePlateSize(plateSize);
            fontSize = Mathf.Clamp(fontSize, MinimumFontSize, MaximumFontSize);
            RefreshStatus();
        }

        private void OnEnable()
        {
            ResolveMissingReferences();
            SubscribeToDeathEvent();
            SubscribeToEventBus();
            RefreshStatus();
        }

        private void Start()
        {
            ResolveMissingReferences();
            SubscribeToDeathEvent();
            SubscribeToEventBus();
            RefreshStatus();
        }

        private void OnDisable()
        {
            UnsubscribeFromEventBus();
            UnsubscribeFromDeathEvent();
        }

        private void OnDestroy()
        {
            UnsubscribeFromEventBus();
            UnsubscribeFromDeathEvent();
        }

        private void OnGUI()
        {
            if (!visible)
            {
                return;
            }

            ResolveMissingReferences();
            RefreshStatus();

            if (tacticalUnit == null || (hideDeadUnits && tacticalUnit.IsDead))
            {
                return;
            }

            var camera = targetCamera;
            if (camera == null)
            {
                return;
            }

            var screenPoint = camera.WorldToScreenPoint(transform.position + worldOffset);
            if (hideWhenBehindCamera && screenPoint.z <= 0f)
            {
                return;
            }

            EnsureStyles();
            DrawNameplate(screenPoint);
        }

        private void OnValidate()
        {
            if (tacticalUnit == null)
            {
                tacticalUnit = GetComponent<TacticalUnit>();
            }

            plateSize = SanitizePlateSize(plateSize);
            fontSize = Mathf.Clamp(fontSize, MinimumFontSize, MaximumFontSize);
        }

        private void HandleActionPointsChanged(ActionPointsChangedEvent eventData)
        {
            if (ReferenceEquals(eventData.Unit, tacticalUnit))
            {
                RefreshStatus();
            }
        }

        private void HandleHitPointsChanged(HitPointsChangedEvent eventData)
        {
            if (ReferenceEquals(eventData.Unit, tacticalUnit))
            {
                RefreshStatus();
            }
        }

        private void HandleUnitDied(UnitDiedEvent eventData)
        {
            if (ReferenceEquals(eventData.Unit, tacticalUnit))
            {
                RefreshStatus();
            }
        }

        private void HandleLocalUnitDied(TacticalUnit unit, DamageSource source)
        {
            if (ReferenceEquals(unit, tacticalUnit))
            {
                RefreshStatus();
            }
        }

        private void DrawNameplate(Vector3 screenPoint)
        {
            var guiCenter = new Vector2(screenPoint.x, Screen.height - screenPoint.y);
            var rect = new Rect(
                guiCenter.x - (plateSize.x * 0.5f),
                guiCenter.y - plateSize.y,
                plateSize.x,
                plateSize.y);

            var previousColor = GUI.color;
            GUI.color = GetBackgroundColor();
            GUI.DrawTexture(rect, Texture2D.whiteTexture);
            GUI.color = Color.white;
            GUI.Box(rect, GUIContent.none, borderStyle);

            textStyle.normal.textColor = GetTextColor();
            GUI.Label(rect, statusText, textStyle);
            GUI.color = previousColor;
        }

        private Color GetBackgroundColor()
        {
            if (tacticalUnit == null || tacticalUnit.IsDead)
            {
                return defeatedBackgroundColor;
            }

            return tacticalUnit.Team == TeamId.Enemy ? enemyBackgroundColor : playerBackgroundColor;
        }

        private Color GetTextColor()
        {
            return tacticalUnit != null && tacticalUnit.IsDead ? defeatedTextColor : textColor;
        }

        private void ResolveLocalReference()
        {
            if (tacticalUnit == null)
            {
                tacticalUnit = GetComponent<TacticalUnit>();
            }
        }

        private void ResolveMissingReferences()
        {
            ResolveLocalReference();

            if (eventBus == null)
            {
                eventBus = FindAnyObjectByType<CombatEventBus>();
                if (eventBus != null)
                {
                    SubscribeToEventBus();
                }
            }

            if (targetCamera == null)
            {
                targetCamera = Camera.main;
                if (targetCamera == null)
                {
                    targetCamera = FindAnyObjectByType<Camera>();
                }
            }
        }

        private void SubscribeToEventBus()
        {
            if (isSubscribedToBus || eventBus == null)
            {
                return;
            }

            eventBus.ActionPointsChanged += HandleActionPointsChanged;
            eventBus.HitPointsChanged += HandleHitPointsChanged;
            eventBus.UnitDied += HandleUnitDied;
            isSubscribedToBus = true;
        }

        private void UnsubscribeFromEventBus()
        {
            if (!isSubscribedToBus || eventBus == null)
            {
                isSubscribedToBus = false;
                return;
            }

            eventBus.ActionPointsChanged -= HandleActionPointsChanged;
            eventBus.HitPointsChanged -= HandleHitPointsChanged;
            eventBus.UnitDied -= HandleUnitDied;
            isSubscribedToBus = false;
        }

        private void SubscribeToDeathEvent()
        {
            if (isSubscribedToDeath || tacticalUnit == null)
            {
                return;
            }

            tacticalUnit.Died += HandleLocalUnitDied;
            isSubscribedToDeath = true;
        }

        private void UnsubscribeFromDeathEvent()
        {
            if (!isSubscribedToDeath || tacticalUnit == null)
            {
                isSubscribedToDeath = false;
                return;
            }

            tacticalUnit.Died -= HandleLocalUnitDied;
            isSubscribedToDeath = false;
        }

        private void EnsureStyles()
        {
            if (textStyle == null)
            {
                textStyle = new GUIStyle(GUI.skin.label)
                {
                    alignment = TextAnchor.MiddleCenter,
                    fontSize = fontSize,
                    fontStyle = FontStyle.Bold,
                    wordWrap = true,
                    richText = false
                };
            }

            textStyle.fontSize = fontSize;

            if (borderStyle == null)
            {
                borderStyle = new GUIStyle(GUI.skin.box);
            }
        }

        private static Vector2 SanitizePlateSize(Vector2 size)
        {
            return new Vector2(
                Mathf.Max(MinimumPlateWidth, Mathf.Abs(size.x)),
                Mathf.Max(MinimumPlateHeight, Mathf.Abs(size.y)));
        }
    }
}
