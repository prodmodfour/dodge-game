using ReactionTactics.Turns;
using UnityEngine;
using UnityEngine.Rendering;

namespace ReactionTactics.Units
{
    /// <summary>
    /// Visual-only active-turn marker for a tactical unit. It listens to the scene
    /// combat event bus and enables a simple ground marker while this unit owns the
    /// active turn, then clears the marker when control changes or the unit dies.
    /// </summary>
    [DisallowMultipleComponent]
    [RequireComponent(typeof(TacticalUnit))]
    public sealed class UnitHighlightView : MonoBehaviour
    {
        private const string DefaultMarkerName = "ActiveTurnMarker";

        [SerializeField]
        [Tooltip("Unit represented by this highlight view. Defaults to the TacticalUnit on the same GameObject.")]
        private TacticalUnit tacticalUnit;

        [SerializeField]
        [Tooltip("Optional scene event bus. If unset, the view finds the scene bus at runtime.")]
        private CombatEventBus eventBus;

        [SerializeField]
        [Tooltip("Optional combat manager used to recover the current active unit when this view is enabled after combat starts.")]
        private CombatManager combatManager;

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
        [Tooltip("Local offset for the generated active marker, relative to the unit root.")]
        private Vector3 markerLocalPosition = new Vector3(0f, 0.04f, 0f);

        [SerializeField]
        [Tooltip("Local scale for the generated active marker. The default is wider than the team marker so it remains visible.")]
        private Vector3 markerLocalScale = new Vector3(1.15f, 0.025f, 1.15f);

        private static Material sharedRuntimeMarkerMaterial;
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

        /// <summary>
        /// Applies active-turn visual state. Gameplay ownership remains in CombatManager.
        /// </summary>
        public void SetActiveHighlight(bool highlighted)
        {
            EnsureMarkerExists();

            var shouldShow = highlighted && tacticalUnit != null && tacticalUnit.IsAlive;
            if (activeMarker != null && activeMarker.gameObject.activeSelf != shouldShow)
            {
                activeMarker.gameObject.SetActive(shouldShow);
            }
        }

        private void Awake()
        {
            ResolveLocalReferences();
            EnsureMarkerExists();
            SetActiveHighlight(false);
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
            HideActiveHighlight();
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
            markerLocalPosition = new Vector3(0f, 0.04f, 0f);
            markerLocalScale = new Vector3(1.15f, 0.025f, 1.15f);
        }

        private void OnValidate()
        {
            if (tacticalUnit == null)
            {
                tacticalUnit = GetComponent<TacticalUnit>();
            }

            markerLocalScale = SanitizeMarkerScale(markerLocalScale);
            if (activeMarker != null)
            {
                activeMarker.localPosition = markerLocalPosition;
                activeMarker.localScale = markerLocalScale;
            }
        }

        private void HandleActiveUnitChanged(ActiveUnitChangedEvent eventData)
        {
            SetActiveHighlight(ReferenceEquals(eventData.ActiveUnit, tacticalUnit));
        }

        private void HandleUnitDied(TacticalUnit unit, DamageSource source)
        {
            HideActiveHighlight();
        }

        private void ApplyCurrentCombatState()
        {
            if (combatManager == null || combatManager.CurrentState == null)
            {
                return;
            }

            SetActiveHighlight(ReferenceEquals(combatManager.CurrentState.ActiveUnit, tacticalUnit));
        }

        private void HideActiveHighlight()
        {
            if (activeMarker != null && activeMarker.gameObject.activeSelf)
            {
                activeMarker.gameObject.SetActive(false);
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

        private void EnsureMarkerExists()
        {
            if (activeMarker == null)
            {
                activeMarker = FindExistingMarker();
            }

            if (activeMarker == null)
            {
                activeMarker = CreateDefaultMarker();
            }

            activeMarker.localPosition = markerLocalPosition;
            activeMarker.localRotation = Quaternion.identity;
            activeMarker.localScale = SanitizeMarkerScale(markerLocalScale);

            if (activeMarkerRenderer == null)
            {
                activeMarkerRenderer = activeMarker.GetComponent<Renderer>();
            }

            if (activeMarkerRenderer != null)
            {
                activeMarkerRenderer.sharedMaterial = GetRuntimeMarkerMaterial(activeColor);
                activeMarkerRenderer.shadowCastingMode = ShadowCastingMode.Off;
                activeMarkerRenderer.receiveShadows = false;
            }
        }

        private Transform FindExistingMarker()
        {
            var marker = transform.Find(DefaultMarkerName);
            return marker != null ? marker : null;
        }

        private Transform CreateDefaultMarker()
        {
            var markerObject = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
            markerObject.name = DefaultMarkerName;
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

        private static Material GetRuntimeMarkerMaterial(Color color)
        {
            if (sharedRuntimeMarkerMaterial == null)
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

                sharedRuntimeMarkerMaterial = new Material(shader)
                {
                    name = "Runtime Active Unit Marker"
                };
                sharedRuntimeMarkerMaterial.hideFlags = HideFlags.DontSave;
            }

            ApplyMaterialColor(sharedRuntimeMarkerMaterial, color);
            return sharedRuntimeMarkerMaterial;
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
