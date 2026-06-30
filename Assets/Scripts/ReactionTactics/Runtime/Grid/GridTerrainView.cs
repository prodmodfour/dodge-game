using System.Collections.Generic;
using UnityEngine;

namespace ReactionTactics.Grid
{
    /// <summary>
    /// Runtime visualizer that turns the active grid map into primitive stepped terrain tiles.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class GridTerrainView : MonoBehaviour
    {
        private const string TileNamePrefix = "Grid Tile";

        [SerializeField]
        [Tooltip("Grid manager that owns the active map and grid metrics. If left empty, the scene is searched at generation time.")]
        private GridManager gridManager;

        [SerializeField]
        [Tooltip("Optional parent for generated tile primitives. Defaults to this transform, which should usually be the Grid root.")]
        private Transform tileParent;

        [SerializeField]
        [Tooltip("Generate terrain automatically when the scene starts playing.")]
        private bool generateOnStart = true;

        [SerializeField]
        [Tooltip("Remove previously generated tile children before generating a fresh terrain view.")]
        private bool clearExistingTiles = true;

        [SerializeField]
        [Tooltip("Authored material for walkable terrain cells. A generated fallback color is used when this is empty.")]
        private Material walkableMaterial;

        [SerializeField]
        [Tooltip("Authored material for blocked or unwalkable terrain cells that do not block line of sight. A generated fallback color is used when this is empty.")]
        private Material blockedMaterial;

        [SerializeField]
        [Tooltip("Authored material for terrain cells that block line of sight. This takes visual priority over the general blocked material.")]
        private Material lineOfSightBlockerMaterial;

        [SerializeField]
        [Tooltip("Default material applied to tile views while they are highlighted for movement, targeting, or debug feedback.")]
        private Material highlightMaterial;

        [SerializeField]
        [Tooltip("Prototype material reserved for threatened or dangerous grid cells.")]
        private Material dangerMaterial;

        [SerializeField]
        [Tooltip("Prototype material reserved for safe reaction movement cells.")]
        private Material safeMaterial;

        [SerializeField]
        [Tooltip("Fallback color used for generated walkable tile materials when no authored walkable material is assigned.")]
        private Color walkableColor = new Color(0.38f, 0.62f, 0.36f, 1f);

        [SerializeField]
        [Tooltip("Fallback color used for generated blocked or unwalkable tile materials when no authored blocked material is assigned.")]
        private Color blockedColor = new Color(0.33f, 0.28f, 0.25f, 1f);

        [SerializeField]
        [Tooltip("Fallback color used for generated line-of-sight blocker tile materials when no authored sight blocker material is assigned.")]
        private Color lineOfSightBlockerColor = new Color(0.46f, 0.22f, 0.68f, 1f);

        private readonly List<GridTileView> generatedTiles = new List<GridTileView>();
        private Material generatedWalkableMaterial;
        private Material generatedBlockedMaterial;
        private Material generatedLineOfSightBlockerMaterial;

        public GridManager GridManager
        {
            get { return gridManager; }
        }

        public Transform TileParent
        {
            get { return tileParent != null ? tileParent : transform; }
        }

        public IReadOnlyList<GridTileView> GeneratedTiles
        {
            get { return generatedTiles; }
        }

        public Material WalkableMaterial
        {
            get { return walkableMaterial; }
        }

        public Material BlockedMaterial
        {
            get { return blockedMaterial; }
        }

        public Material LineOfSightBlockerMaterial
        {
            get { return lineOfSightBlockerMaterial; }
        }

        public Material HighlightMaterial
        {
            get { return highlightMaterial; }
        }

        public Material DangerMaterial
        {
            get { return dangerMaterial; }
        }

        public Material SafeMaterial
        {
            get { return safeMaterial; }
        }

        public int TileCount
        {
            get { return generatedTiles.Count; }
        }

        public bool Regenerate()
        {
            var resolvedGridManager = ResolveGridManager();
            if (resolvedGridManager == null)
            {
                Debug.LogError(
                    $"{nameof(GridTerrainView)} on '{name}' cannot generate terrain because no {nameof(GridManager)} is assigned or available in the scene.",
                    this);
                return false;
            }

            if (!resolvedGridManager.HasCurrentMap && !resolvedGridManager.RebuildMap())
            {
                return false;
            }

            if (resolvedGridManager.CurrentMap == null)
            {
                Debug.LogError(
                    $"{nameof(GridTerrainView)} on '{name}' cannot generate terrain because {nameof(GridManager)} '{resolvedGridManager.name}' has no current map.",
                    this);
                return false;
            }

            if (clearExistingTiles)
            {
                ClearGeneratedTiles();
            }
            else
            {
                generatedTiles.Clear();
            }

            var parent = TileParent;
            var metrics = resolvedGridManager.Metrics;
            foreach (var cell in resolvedGridManager.CurrentMap.AllCells)
            {
                generatedTiles.Add(CreateTile(cell, metrics, parent));
            }

            return true;
        }

        public void ClearGeneratedTiles()
        {
            generatedTiles.Clear();

            var parent = TileParent;
            for (var index = parent.childCount - 1; index >= 0; index--)
            {
                var child = parent.GetChild(index);
                if (child.GetComponent<GridTileView>() == null)
                {
                    continue;
                }

                DestroyObject(child.gameObject);
            }
        }

        public bool TryGetTile(GridPosition position, out GridTileView tileView)
        {
            for (var index = 0; index < generatedTiles.Count; index++)
            {
                var candidate = generatedTiles[index];
                if (candidate != null && candidate.GridPosition == position)
                {
                    tileView = candidate;
                    return true;
                }
            }

            tileView = null;
            return false;
        }

        public static Vector3 CalculateTileWorldPosition(GridCell cell, GridMetrics metrics)
        {
            var surfaceCenter = metrics.GridToWorldCenter(cell.Position);
            var visualHeight = GridTileView.CalculateVisualHeight(cell, metrics);
            return new Vector3(surfaceCenter.x, surfaceCenter.y - (visualHeight * 0.5f), surfaceCenter.z);
        }

        private void Start()
        {
            if (generateOnStart)
            {
                Regenerate();
            }
        }

        private void OnDestroy()
        {
            DestroyOwnedMaterial(ref generatedWalkableMaterial);
            DestroyOwnedMaterial(ref generatedBlockedMaterial);
            DestroyOwnedMaterial(ref generatedLineOfSightBlockerMaterial);
        }

        private GridManager ResolveGridManager()
        {
            if (gridManager != null)
            {
                return gridManager;
            }

            gridManager = GetComponentInParent<GridManager>();
            if (gridManager != null)
            {
                return gridManager;
            }

            gridManager = Object.FindFirstObjectByType<GridManager>();
            return gridManager;
        }

        private GridTileView CreateTile(GridCell cell, GridMetrics metrics, Transform parent)
        {
            var tileObject = GameObject.CreatePrimitive(PrimitiveType.Cube);
            tileObject.name = $"{TileNamePrefix} {cell.Position}";
            tileObject.transform.SetParent(parent, worldPositionStays: false);
            tileObject.transform.localScale = new Vector3(metrics.CellSize, 1f, metrics.CellSize);
            tileObject.transform.position = CalculateTileWorldPosition(cell, metrics);

            var tileView = tileObject.AddComponent<GridTileView>();
            tileView.Initialize(cell, metrics, GetMaterialForCell(cell));
            tileView.SetHighlightMaterial(highlightMaterial);
            return tileView;
        }

        private Material GetMaterialForCell(GridCell cell)
        {
            if (cell.BlocksLineOfSight)
            {
                return lineOfSightBlockerMaterial != null
                    ? lineOfSightBlockerMaterial
                    : GetOrCreateLineOfSightBlockerMaterial();
            }

            if (IsBlockedOrUnwalkable(cell))
            {
                return blockedMaterial != null ? blockedMaterial : GetOrCreateBlockedMaterial();
            }

            return walkableMaterial != null ? walkableMaterial : GetOrCreateWalkableMaterial();
        }

        private static bool IsBlockedOrUnwalkable(GridCell cell)
        {
            return !cell.Walkable || cell.BlocksMovement;
        }

        private Material GetOrCreateWalkableMaterial()
        {
            if (generatedWalkableMaterial == null)
            {
                generatedWalkableMaterial = CreateRuntimeMaterial("Generated Walkable Grid Tile", walkableColor);
            }

            return generatedWalkableMaterial;
        }

        private Material GetOrCreateBlockedMaterial()
        {
            if (generatedBlockedMaterial == null)
            {
                generatedBlockedMaterial = CreateRuntimeMaterial("Generated Blocked Grid Tile", blockedColor);
            }

            return generatedBlockedMaterial;
        }

        private Material GetOrCreateLineOfSightBlockerMaterial()
        {
            if (generatedLineOfSightBlockerMaterial == null)
            {
                generatedLineOfSightBlockerMaterial = CreateRuntimeMaterial("Generated Line Of Sight Blocker Grid Tile", lineOfSightBlockerColor);
            }

            return generatedLineOfSightBlockerMaterial;
        }

        private static Material CreateRuntimeMaterial(string materialName, Color color)
        {
            var shader = Shader.Find("Universal Render Pipeline/Lit")
                ?? Shader.Find("Standard")
                ?? Shader.Find("Sprites/Default")
                ?? Shader.Find("UI/Default");
            if (shader == null)
            {
                return null;
            }

            var material = new Material(shader)
            {
                name = materialName,
                color = color,
                hideFlags = HideFlags.DontSave
            };

            if (material.HasProperty("_BaseColor"))
            {
                material.SetColor("_BaseColor", color);
            }

            if (material.HasProperty("_Color"))
            {
                material.SetColor("_Color", color);
            }

            return material;
        }

        private static void DestroyOwnedMaterial(ref Material material)
        {
            if (material == null)
            {
                return;
            }

            DestroyObject(material);
            material = null;
        }

        private static void DestroyObject(Object value)
        {
            if (value == null)
            {
                return;
            }

            if (Application.isPlaying)
            {
                Destroy(value);
            }
            else
            {
                DestroyImmediate(value);
            }
        }
    }
}
