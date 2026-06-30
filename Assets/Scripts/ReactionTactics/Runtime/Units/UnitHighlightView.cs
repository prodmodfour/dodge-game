using ReactionTactics.Turns;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Serialization;

namespace ReactionTactics.Units
{
    /// <summary>
    /// Visual-only control markers for a tactical unit. It listens to the scene
    /// combat event bus and enables one marker while this unit owns the active turn
    /// and a separate marker while this unit is the current reactor.
    /// </summary>
    [DisallowMultipleComponent]
    [RequireComponent(typeof(TacticalUnit))]
    public sealed class UnitHighlightView : MonoBehaviour
    {
        private const string DefaultActiveMarkerName = "ActiveTurnMarker";
        private const string DefaultReactionMarkerName = "ReactionTurnMarker";

        [SerializeField]
        [Tooltip("Unit represented by this highlight view. Defaults to the TacticalUnit on the same GameObject.")]
        private TacticalUnit tacticalUnit;

        [SerializeField]
        [Tooltip("Optional scene event bus. If unset, the view finds the scene bus at runtime.")]
        private CombatEventBus eventBus;

        [SerializeField]
        [Tooltip("Optional combat manager used to recover the current active unit and current reactor when this view is enabled after combat starts.")]
        private CombatManager combatManager;

        [Header("Active Turn Marker")]
        [SerializeField]
        [Tooltip("Marker transform enabled while this unit is active. If unset, a flat cylinder marker is created at runtime.")]
        private Transform activeMarker;

        [SerializeField]
        [Tooltip("Renderer for the active marker. If unset, the view uses the renderer on the marker transform.")]
        private Renderer activeMarkerRenderer;

        [SerializeField]
        [Tooltip("World-space color used by the runtime active-unit marker.")]
        private Color activeColor = new Color(1f, 0.88f, 0.05f, 1f);

        [SerializeField]
        [FormerlySerializedAs("markerLocalPosition")]
        [Tooltip("Local offset for the generated active marker, relative to the unit root.")]
        private Vector3 activeMarkerLocalPosition = new Vector3(0f, 0.04f, 0f);

        [SerializeField]
        [FormerlySerializedAs("markerLocalScale")]
        [Tooltip("Local scale for the generated active marker. The default is wider than the team marker so it remains visible.")]
        private Vector3 activeMarkerLocalScale = new Vector3(1.15f, 0.025f, 1.15f);

        [Header("Reaction Turn Marker")]
        [SerializeField]
        [Tooltip("Marker transform enabled while this unit is the current reactor. If unset, a flat cylinder marker is created at runtime.")]
        private Transform reactionMarker;

        [SerializeField]
        [Tooltip("Renderer for the reaction marker. If unset, the view uses the renderer on the marker transform.")]
        private Renderer reactionMarkerRenderer;

        [SerializeField]
        [Tooltip("World-space color used by the runtime current-reactor marker.")]
        private Color reactionColor = new Color(0.1f, 0.95f, 1f, 1f);

        [SerializeField]
        [Tooltip("Local offset for the generated current-reactor marker, relative to the unit root.")]
        private Vector3 reactionMarkerLocalPosition = new Vector3(0f, 0.075f, 0f);

        [SerializeField]
        [Tooltip("Local scale for the generated current-reactor marker. The default sits inside the active marker so the two states are visually distinct.")]
        private Vector3 reactionMarkerLocalScale = new Vector3(0.82f, 0.03f, 0.82f);

        private static Material sharedActiveRuntimeMarkerMaterial;
        private static Material sharedReactionRuntimeMarkerMaterial;
        private bool isSubscribedToBus;
        private bool isSubscribedToDeath;

        public TacticalUnit Unit
        {
            get { return tacticalUnit; }
        }

        public bool IsHighlighted
        {
            get { return activeMarker != null && activeMarker.gameObject.activeSelf; }
        }

        public bool IsReactionHighlighted
        {
            get { return reactionMarker != null && reactionMarker.gameObject.activeSelf; }
        }

        /// <summary>
        /// Applies active-turn visual state. Gameplay ownership remains in CombatManager.
        /// </summary>
        public void SetActiveHighlight(bool highlighted)
        {
            EnsureActiveMarkerExists();

            var shouldShow = highlighted && tacticalUnit != null && tacticalUnit.IsAlive;
            if (activeMarker != null && activeMarker.gameObject.activeSelf != shouldShow)
            {
                activeMarker.gameObject.SetActive(shouldShow);
            }
        }

        /// <summary>
        /// Applies current-reactor visual state. Only the current reactor may use reaction commands.
        /// </summary>
        public void SetReactionHighlight(bool highlighted)
        {
            EnsureReactionMarkerExists();

            var shouldShow = highlighted && tacticalUnit != null && tacticalUnit.IsAlive;
            if (reactionMarker != null && reactionMarker.gameObject.activeSelf != shouldShow)
            {
                reactionMarker.gameObject.SetActive(shouldShow);
            }
        }

        private void Awake()
        {
            ResolveLocalReferences();
            EnsureActiveMarkerExists();
            EnsureReactionMarkerExists();
            SetActiveHighlight(false);
            SetReactionHighlight(false);
        }

        private void OnEnable()
        {
            ResolveLocalReferences();
            SubscribeToDeathEvent();
            SubscribeToEventBus();
            ApplyCurrentCombatState();
        }

        private void Start()
        {
            ResolveSceneReferences();
            SubscribeToEventBus();
            ApplyCurrentCombatState();
        }

        private void OnDisable()
        {
            UnsubscribeFromEventBus();
            UnsubscribeFromDeathEvent();
            HideAllHighlights();
        }

        private void OnDestroy()
        {
            UnsubscribeFromEventBus();
            UnsubscribeFromDeathEvent();
        }

        private void Reset()
        {
            tacticalUnit = GetComponent<TacticalUnit>();
            activeColor = new Color(1f, 0.88f, 0.05f, 1f);
            activeMarkerLocalPosition = new Vector3(0f, 0.04f, 0f);
            activeMarkerLocalScale = new Vector3(1.15f, 0.025f, 1.15f);
            reactionColor = new Color(0.1f, 0.95f, 1f, 1f);
            reactionMarkerLocalPosition = new Vector3(0f, 0.075f, 0f);
            reactionMarkerLocalScale = new Vector3(0.82f, 0.03f, 0.82f);
        }

        private void OnValidate()
        {
            if (tacticalUnit == null)
            {
                tacticalUnit = GetComponent<TacticalUnit>();
            }

            activeMarkerLocalScale = SanitizeMarkerScale(activeMarkerLocalScale);
            reactionMarkerLocalScale = SanitizeMarkerScale(reactionMarkerLocalScale);

            if (activeMarker != null)
            {
                activeMarker.localPosition = activeMarkerLocalPosition;
                activeMarker.localScale = activeMarkerLocalScale;
            }

            if (reactionMarker != null)
            {
                reactionMarker.localPosition = reactionMarkerLocalPosition;
                reactionMarker.localScale = reactionMarkerLocalScale;
            }
        }

        private void HandleActiveUnitChanged(ActiveUnitChangedEvent eventData)
        {
            SetActiveHighlight(ReferenceEquals(eventData.ActiveUnit, tacticalUnit));
        }

        private void HandleReactionTurnStarted(ReactionTurnStartedEvent eventData)
        {
            SetReactionHighlight(ReferenceEquals(eventData.Reactor, tacticalUnit));
        }

        private void HandleActionResolved(ActionResolvedEvent eventData)
        {
            SetReactionHighlight(false);
        }

        private void HandleUnitDied(TacticalUnit unit, DamageSource source)
        {
            HideAllHighlights();
        }

        private void ApplyCurrentCombatState()
        {
            if (combatManager == null || combatManager.CurrentState == null)
            {
                return;
            }

            SetActiveHighlight(ReferenceEquals(combatManager.CurrentState.ActiveUnit, tacticalUnit));
            SetReactionHighlight(
                combatManager.CurrentState.IsReactionPhase
                && ReferenceEquals(combatManager.CurrentState.CurrentReactor, tacticalUnit));
        }

        private void HideAllHighlights()
        {
            HideActiveHighlight();
            HideReactionHighlight();
        }

        private void HideActiveHighlight()
        {
            if (activeMarker != null && activeMarker.gameObject.activeSelf)
            {
                activeMarker.gameObject.SetActive(false);
            }
        }

        private void HideReactionHighlight()
        {
            if (reactionMarker != null && reactionMarker.gameObject.activeSelf)
            {
                reactionMarker.gameObject.SetActive(false);
            }
        }

        private void ResolveLocalReferences()
        {
            if (tacticalUnit == null)
            {
                tacticalUnit = GetComponent<TacticalUnit>();
            }

            if (activeMarkerRenderer == null && activeMarker != null)
            {
                activeMarkerRenderer = activeMarker.GetComponent<Renderer>();
            }

            if (reactionMarkerRenderer == null && reactionMarker != null)
            {
                reactionMarkerRenderer = reactionMarker.GetComponent<Renderer>();
            }

            ResolveSceneReferences();
        }

        private void ResolveSceneReferences()
        {
            if (eventBus == null)
            {
                eventBus = FindAnyObjectByType<CombatEventBus>();
            }

            if (combatManager == null)
            {
                combatManager = eventBus != null ? eventBus.GetComponent<CombatManager>() : FindAnyObjectByType<CombatManager>();
            }
        }

        private void SubscribeToEventBus()
        {
            if (isSubscribedToBus)
            {
                return;
            }

            ResolveSceneReferences();
            if (eventBus == null)
            {
                return;
            }

            eventBus.ActiveUnitChanged += HandleActiveUnitChanged;
            eventBus.ReactionTurnStarted += HandleReactionTurnStarted;
            eventBus.ActionResolved += HandleActionResolved;
            isSubscribedToBus = true;
        }

        private void UnsubscribeFromEventBus()
        {
            if (!isSubscribedToBus || eventBus == null)
            {
                isSubscribedToBus = false;
                return;
            }

            eventBus.ActiveUnitChanged -= HandleActiveUnitChanged;
            eventBus.ReactionTurnStarted -= HandleReactionTurnStarted;
            eventBus.ActionResolved -= HandleActionResolved;
            isSubscribedToBus = false;
        }

        private void SubscribeToDeathEvent()
        {
            if (isSubscribedToDeath || tacticalUnit == null)
            {
                return;
            }

            tacticalUnit.Died += HandleUnitDied;
            isSubscribedToDeath = true;
        }

        private void UnsubscribeFromDeathEvent()
        {
            if (!isSubscribedToDeath || tacticalUnit == null)
            {
                isSubscribedToDeath = false;
                return;
            }

            tacticalUnit.Died -= HandleUnitDied;
            isSubscribedToDeath = false;
        }

        private void EnsureActiveMarkerExists()
        {
            EnsureMarkerExists(
                DefaultActiveMarkerName,
                ref activeMarker,
                ref activeMarkerRenderer,
                activeMarkerLocalPosition,
                activeMarkerLocalScale,
                ref sharedActiveRuntimeMarkerMaterial,
                activeColor,
                "Runtime Active Unit Marker");
        }

        private void EnsureReactionMarkerExists()
        {
            EnsureMarkerExists(
                DefaultReactionMarkerName,
                ref reactionMarker,
                ref reactionMarkerRenderer,
                reactionMarkerLocalPosition,
                reactionMarkerLocalScale,
                ref sharedReactionRuntimeMarkerMaterial,
                reactionColor,
                "Runtime Reaction Unit Marker");
        }

        private void EnsureMarkerExists(
            string markerName,
            ref Transform marker,
            ref Renderer markerRenderer,
            Vector3 localPosition,
            Vector3 localScale,
            ref Material sharedMaterial,
            Color markerColor,
            string materialName)
        {
            if (marker == null)
            {
                marker = FindExistingMarker(markerName);
            }

            if (marker == null)
            {
                marker = CreateDefaultMarker(markerName);
            }

            marker.localPosition = localPosition;
            marker.localRotation = Quaternion.identity;
            marker.localScale = SanitizeMarkerScale(localScale);

            if (markerRenderer == null)
            {
                markerRenderer = marker.GetComponent<Renderer>();
            }

            if (markerRenderer != null)
            {
                markerRenderer.sharedMaterial = GetRuntimeMarkerMaterial(ref sharedMaterial, markerColor, materialName);
                markerRenderer.shadowCastingMode = ShadowCastingMode.Off;
                markerRenderer.receiveShadows = false;
            }
        }

        private Transform FindExistingMarker(string markerName)
        {
            var marker = transform.Find(markerName);
            return marker != null ? marker : null;
        }

        private Transform CreateDefaultMarker(string markerName)
        {
            var markerObject = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
            markerObject.name = markerName;
            markerObject.transform.SetParent(transform, false);

            var collider = markerObject.GetComponent<Collider>();
            if (collider != null)
            {
                collider.enabled = false;
                if (Application.isPlaying)
                {
                    Destroy(collider);
                }
                else
                {
                    DestroyImmediate(collider);
                }
            }

            return markerObject.transform;
        }

        private static Material GetRuntimeMarkerMaterial(ref Material sharedMaterial, Color color, string materialName)
        {
            if (sharedMaterial == null)
            {
                var shader = Shader.Find("Standard")
                    ?? Shader.Find("Universal Render Pipeline/Lit")
                    ?? Shader.Find("Unlit/Color")
                    ?? Shader.Find("Sprites/Default")
                    ?? Shader.Find("Hidden/InternalErrorShader");
                if (shader == null)
                {
                    return null;
                }

                sharedMaterial = new Material(shader)
                {
                    name = materialName,
                    hideFlags = HideFlags.DontSave
                };
            }

            ApplyMaterialColor(sharedMaterial, color);
            return sharedMaterial;
        }

        private static void ApplyMaterialColor(Material material, Color color)
        {
            if (material == null)
            {
                return;
            }

            if (material.HasProperty("_Color"))
            {
                material.SetColor("_Color", color);
            }

            if (material.HasProperty("_BaseColor"))
            {
                material.SetColor("_BaseColor", color);
            }

            if (material.HasProperty("_EmissionColor"))
            {
                material.EnableKeyword("_EMISSION");
                material.SetColor("_EmissionColor", color * 0.35f);
            }
        }

        private static Vector3 SanitizeMarkerScale(Vector3 scale)
        {
            return new Vector3(
                Mathf.Max(0.01f, Mathf.Abs(scale.x)),
                Mathf.Max(0.001f, Mathf.Abs(scale.y)),
                Mathf.Max(0.01f, Mathf.Abs(scale.z)));
        }
    }
}
