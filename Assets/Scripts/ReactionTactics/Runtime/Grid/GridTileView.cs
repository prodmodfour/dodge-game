using UnityEngine;

namespace ReactionTactics.Grid
{
    /// <summary>
    /// Presentation component for a primitive grid tile. It keeps the represented grid
    /// coordinate available for mouse picking and owns simple material highlight state.
    /// </summary>
    [DisallowMultipleComponent]
    [RequireComponent(typeof(BoxCollider))]
    public sealed class GridTileView : MonoBehaviour
    {
        private const float MinimumVisualHeight = 0.05f;

        [SerializeField]
        [Tooltip("Grid X coordinate represented by this visible tile.")]
        private int gridX;

        [SerializeField]
        [Tooltip("Grid Y/height coordinate represented by this visible tile.")]
        private int gridY;

        [SerializeField]
        [Tooltip("Grid Z coordinate represented by this visible tile.")]
        private int gridZ;

        [SerializeField]
        [Tooltip("Material used when the tile is not highlighted.")]
        private Material baseMaterial;

        [SerializeField]
        [Tooltip("Optional material used while the tile is highlighted.")]
        private Material highlightMaterial;

        [SerializeField]
        [Tooltip("Whether the tile is currently highlighted for movement, targeting, or debug feedback.")]
        private bool highlighted;

        [SerializeField]
        [Tooltip("Renderer whose shared material is swapped for simple highlight feedback.")]
        private Renderer targetRenderer;

        [SerializeField]
        [Tooltip("Collider used by picking raycasts. A BoxCollider is required for primitive tiles.")]
        private Collider pickingCollider;

        public GridPosition GridPosition
        {
            get { return new GridPosition(gridX, gridY, gridZ); }
        }

        public Material BaseMaterial
        {
            get { return baseMaterial; }
        }

        public Material HighlightMaterial
        {
            get { return highlightMaterial; }
        }

        public bool IsHighlighted
        {
            get { return highlighted; }
        }

        public Renderer TargetRenderer
        {
            get { return targetRenderer != null ? targetRenderer : GetComponent<Renderer>(); }
        }

        public Collider PickingCollider
        {
            get { return pickingCollider != null ? pickingCollider : GetComponent<Collider>(); }
        }

        public void Initialize(GridCell cell, GridMetrics metrics, Material material)
        {
            SetGridPosition(cell.Position);
            SetBaseMaterial(material, applyImmediately: false);
            SetHeightScale(cell, metrics);
            CacheSceneReferences();
            ApplyMaterialState();
        }

        public void ApplyCellData(GridCell cell, GridMetrics metrics)
        {
            SetGridPosition(cell.Position);
            SetHeightScale(cell, metrics);
        }

        public void SetGridPosition(GridPosition position)
        {
            gridX = position.X;
            gridY = position.Y;
            gridZ = position.Z;
        }

        public void SetBaseMaterial(Material material)
        {
            SetBaseMaterial(material, applyImmediately: true);
        }

        public void SetHighlightMaterial(Material material)
        {
            highlightMaterial = material;
            ApplyMaterialState();
        }

        public void SetHighlighted(bool value)
        {
            highlighted = value;
            ApplyMaterialState();
        }

        public void SetHeightScale(GridCell cell, GridMetrics metrics)
        {
            var scale = transform.localScale;
            scale.y = CalculateVisualHeight(cell, metrics);
            transform.localScale = scale;
        }

        public static float CalculateVisualHeight(GridCell cell, GridMetrics metrics)
        {
            var heightFromCellData = (cell.DisplayHeight + 1f) * metrics.HeightStep;
            return Mathf.Max(MinimumVisualHeight, heightFromCellData);
        }

        private void Reset()
        {
            CacheSceneReferences();
            CaptureBaseMaterialIfMissing();
            ApplyMaterialState();
        }

        private void Awake()
        {
            CacheSceneReferences();
            CaptureBaseMaterialIfMissing();
            ApplyMaterialState();
        }

        private void OnValidate()
        {
            CacheSceneReferences();
            CaptureBaseMaterialIfMissing();
            ApplyMaterialState();
        }

        private void SetBaseMaterial(Material material, bool applyImmediately)
        {
            baseMaterial = material;
            if (applyImmediately)
            {
                ApplyMaterialState();
            }
        }

        private void CacheSceneReferences()
        {
            if (targetRenderer == null)
            {
                targetRenderer = GetComponent<Renderer>();
            }

            if (pickingCollider == null)
            {
                pickingCollider = GetComponent<Collider>();
            }
        }

        private void CaptureBaseMaterialIfMissing()
        {
            if (baseMaterial == null && targetRenderer != null)
            {
                baseMaterial = targetRenderer.sharedMaterial;
            }
        }

        private void ApplyMaterialState()
        {
            var rendererToUpdate = targetRenderer != null ? targetRenderer : GetComponent<Renderer>();
            if (rendererToUpdate == null)
            {
                return;
            }

            var material = highlighted && highlightMaterial != null ? highlightMaterial : baseMaterial;
            if (material != null && rendererToUpdate.sharedMaterial != material)
            {
                rendererToUpdate.sharedMaterial = material;
            }
        }
    }
}
