using UnityEngine;

namespace ReactionTactics.Units
{
    /// <summary>
    /// Applies simple team-colored prototype materials to the primitive unit body and
    /// always-visible base marker. This keeps team readability independent from HP/AP UI.
    /// </summary>
    [DisallowMultipleComponent]
    [RequireComponent(typeof(TacticalUnit))]
    public sealed class UnitTeamVisualView : MonoBehaviour
    {
        private const string DefaultBodyChildName = "Body";
        private const string DefaultTeamMarkerChildName = "TeamSelectionMarker";

        [SerializeField]
        [Tooltip("Unit whose team controls the selected prototype materials. Defaults to the TacticalUnit on this GameObject.")]
        private TacticalUnit tacticalUnit;

        [SerializeField]
        [Tooltip("Renderer for the main primitive body mesh. If unset, a child named Body is used.")]
        private Renderer bodyRenderer;

        [SerializeField]
        [Tooltip("Renderer for the always-visible base/team marker. If unset, a child named TeamSelectionMarker is used.")]
        private Renderer teamMarkerRenderer;

        [Header("Team Materials")]
        [SerializeField]
        [Tooltip("Material used for player-team unit bodies.")]
        private Material playerBodyMaterial;

        [SerializeField]
        [Tooltip("Material used for enemy-team unit bodies.")]
        private Material enemyBodyMaterial;

        [SerializeField]
        [Tooltip("Material used for player-team base markers. Falls back to the player body material when unset.")]
        private Material playerMarkerMaterial;

        [SerializeField]
        [Tooltip("Material used for enemy-team base markers. Falls back to the enemy body material when unset.")]
        private Material enemyMarkerMaterial;

        [Header("Fallback Colors")]
        [SerializeField]
        [Tooltip("Runtime fallback color for player units when no authored material is assigned.")]
        private Color playerFallbackColor = new Color(0.12f, 0.38f, 1f, 1f);

        [SerializeField]
        [Tooltip("Runtime fallback color for enemy units when no authored material is assigned.")]
        private Color enemyFallbackColor = new Color(0.95f, 0.18f, 0.10f, 1f);

        [SerializeField]
        [Tooltip("Runtime fallback color for marker rings when no marker material is assigned.")]
        private Color markerFallbackColor = new Color(1f, 1f, 1f, 1f);

        [SerializeField]
        [Tooltip("Apply team visuals from Start, after UnitSpawner has initialized the unit's team.")]
        private bool applyOnStart = true;

        private static Material sharedPlayerFallbackMaterial;
        private static Material sharedEnemyFallbackMaterial;
        private static Material sharedPlayerMarkerFallbackMaterial;
        private static Material sharedEnemyMarkerFallbackMaterial;

        public TacticalUnit Unit
        {
            get { return tacticalUnit; }
        }

        public Renderer BodyRenderer
        {
            get { return bodyRenderer; }
        }

        public Renderer TeamMarkerRenderer
        {
            get { return teamMarkerRenderer; }
        }

        public Material PlayerBodyMaterial
        {
            get { return playerBodyMaterial; }
        }

        public Material EnemyBodyMaterial
        {
            get { return enemyBodyMaterial; }
        }

        public Material PlayerMarkerMaterial
        {
            get { return playerMarkerMaterial; }
        }

        public Material EnemyMarkerMaterial
        {
            get { return enemyMarkerMaterial; }
        }

        /// <summary>
        /// Configures references in one step for prefab setup tooling and edit-mode tests.
        /// </summary>
        public void Configure(
            TacticalUnit tacticalUnit,
            Renderer bodyRenderer,
            Renderer teamMarkerRenderer,
            Material playerBodyMaterial,
            Material enemyBodyMaterial,
            Material playerMarkerMaterial,
            Material enemyMarkerMaterial)
        {
            this.tacticalUnit = tacticalUnit;
            this.bodyRenderer = bodyRenderer;
            this.teamMarkerRenderer = teamMarkerRenderer;
            this.playerBodyMaterial = playerBodyMaterial;
            this.enemyBodyMaterial = enemyBodyMaterial;
            this.playerMarkerMaterial = playerMarkerMaterial;
            this.enemyMarkerMaterial = enemyMarkerMaterial;
        }

        /// <summary>
        /// Applies the current unit team to the assigned primitive renderers.
        /// </summary>
        public void ApplyTeamVisuals()
        {
            ResolveReferences();

            if (tacticalUnit == null)
            {
                return;
            }

            ApplyRendererMaterial(bodyRenderer, GetBodyMaterial(tacticalUnit.Team));
            ApplyRendererMaterial(teamMarkerRenderer, GetMarkerMaterial(tacticalUnit.Team));
        }

        private void Awake()
        {
            ResolveReferences();
        }

        private void Start()
        {
            if (applyOnStart)
            {
                ApplyTeamVisuals();
            }
        }

        private void Reset()
        {
            tacticalUnit = GetComponent<TacticalUnit>();
            ResolveReferences();
            playerFallbackColor = new Color(0.12f, 0.38f, 1f, 1f);
            enemyFallbackColor = new Color(0.95f, 0.18f, 0.10f, 1f);
            markerFallbackColor = Color.white;
            applyOnStart = true;
        }

        private void OnValidate()
        {
            if (tacticalUnit == null)
            {
                tacticalUnit = GetComponent<TacticalUnit>();
            }

            if (bodyRenderer == null)
            {
                bodyRenderer = FindChildRenderer(DefaultBodyChildName);
            }

            if (teamMarkerRenderer == null)
            {
                teamMarkerRenderer = FindChildRenderer(DefaultTeamMarkerChildName);
            }

            playerFallbackColor = SanitizeColor(playerFallbackColor, new Color(0.12f, 0.38f, 1f, 1f));
            enemyFallbackColor = SanitizeColor(enemyFallbackColor, new Color(0.95f, 0.18f, 0.10f, 1f));
            markerFallbackColor = SanitizeColor(markerFallbackColor, Color.white);
        }

        private void ResolveReferences()
        {
            if (tacticalUnit == null)
            {
                tacticalUnit = GetComponent<TacticalUnit>();
            }

            if (bodyRenderer == null)
            {
                bodyRenderer = FindChildRenderer(DefaultBodyChildName);
            }

            if (teamMarkerRenderer == null)
            {
                teamMarkerRenderer = FindChildRenderer(DefaultTeamMarkerChildName);
            }
        }

        private Renderer FindChildRenderer(string childName)
        {
            var child = transform.Find(childName);
            if (child == null)
            {
                return null;
            }

            return child.GetComponent<Renderer>();
        }

        private Material GetBodyMaterial(TeamId team)
        {
            switch (team)
            {
                case TeamId.Player:
                    return playerBodyMaterial != null
                        ? playerBodyMaterial
                        : GetRuntimeMaterial(ref sharedPlayerFallbackMaterial, playerFallbackColor, "Runtime Player Team Body");
                case TeamId.Enemy:
                    return enemyBodyMaterial != null
                        ? enemyBodyMaterial
                        : GetRuntimeMaterial(ref sharedEnemyFallbackMaterial, enemyFallbackColor, "Runtime Enemy Team Body");
                default:
                    return null;
            }
        }

        private Material GetMarkerMaterial(TeamId team)
        {
            switch (team)
            {
                case TeamId.Player:
                    if (playerMarkerMaterial != null)
                    {
                        return playerMarkerMaterial;
                    }

                    return playerBodyMaterial != null
                        ? playerBodyMaterial
                        : GetRuntimeMaterial(ref sharedPlayerMarkerFallbackMaterial, markerFallbackColor, "Runtime Player Team Marker");
                case TeamId.Enemy:
                    if (enemyMarkerMaterial != null)
                    {
                        return enemyMarkerMaterial;
                    }

                    return enemyBodyMaterial != null
                        ? enemyBodyMaterial
                        : GetRuntimeMaterial(ref sharedEnemyMarkerFallbackMaterial, markerFallbackColor, "Runtime Enemy Team Marker");
                default:
                    return null;
            }
        }

        private static void ApplyRendererMaterial(Renderer renderer, Material material)
        {
            if (renderer == null || material == null)
            {
                return;
            }

            if (renderer.sharedMaterial != material)
            {
                renderer.sharedMaterial = material;
            }
        }

        private static Material GetRuntimeMaterial(ref Material sharedMaterial, Color color, string materialName)
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
                material.SetColor("_EmissionColor", color * 0.18f);
            }
        }

        private static Color SanitizeColor(Color value, Color fallback)
        {
            return IsFinite(value) ? value : fallback;
        }

        private static bool IsFinite(Color value)
        {
            return !float.IsNaN(value.r) && !float.IsInfinity(value.r)
                && !float.IsNaN(value.g) && !float.IsInfinity(value.g)
                && !float.IsNaN(value.b) && !float.IsInfinity(value.b)
                && !float.IsNaN(value.a) && !float.IsInfinity(value.a);
        }
    }
}
