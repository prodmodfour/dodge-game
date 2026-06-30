using System;
using System.Collections.Generic;
using UnityEngine;

namespace ReactionTactics.Grid
{
    /// <summary>
    /// Central owner for transient grid tile highlights. Callers publish cells into
    /// named categories, and this component resolves the highest-priority category
    /// per tile before touching the tile view's material state.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class GridHighlightManager : MonoBehaviour
    {
        [SerializeField]
        [Tooltip("Terrain view whose generated tiles should be highlighted. If left empty, the scene is searched lazily.")]
        private GridTerrainView terrainView;

        [SerializeField]
        [Tooltip("Search the scene for GridTileView components when no terrain view tiles are registered.")]
        private bool rebuildIndexFromSceneWhenMissing = true;

        [Header("Highlight Materials")]
        [SerializeField]
        [Tooltip("Material used for cells reachable by active movement.")]
        private Material movementRangeMaterial;

        [SerializeField]
        [Tooltip("Material used for the currently hovered or selected movement path.")]
        private Material selectedPathMaterial;

        [SerializeField]
        [Tooltip("Material used for cells threatened by a selected or pending action.")]
        private Material actionDangerMaterial;

        [SerializeField]
        [Tooltip("Material used for reaction destinations that avoid the pending action.")]
        private Material reactionSafeMaterial;

        [SerializeField]
        [Tooltip("Material used for reaction destinations that remain threatened by the pending action.")]
        private Material reactionThreatenedMaterial;

        [SerializeField]
        [Tooltip("Material used for the current target cell. This has the highest priority.")]
        private Material targetCellMaterial;

        [Header("Fallback Colors")]
        [SerializeField]
        private Color movementRangeColor = new Color(0.25f, 0.65f, 1f, 1f);

        [SerializeField]
        private Color selectedPathColor = new Color(1f, 0.92f, 0.25f, 1f);

        [SerializeField]
        private Color actionDangerColor = new Color(1f, 0.2f, 0.12f, 1f);

        [SerializeField]
        private Color reactionSafeColor = new Color(0.2f, 0.95f, 0.45f, 1f);

        [SerializeField]
        private Color reactionThreatenedColor = new Color(1f, 0.48f, 0.08f, 1f);

        [SerializeField]
        private Color targetCellColor = new Color(1f, 1f, 1f, 1f);

        private static readonly GridPosition[] NoPositions = new GridPosition[0];

        private readonly Dictionary<GridPosition, GridTileView> tilesByPosition = new Dictionary<GridPosition, GridTileView>();
        private readonly Dictionary<GridHighlightCategory, HashSet<GridPosition>> highlightedCellsByCategory = new Dictionary<GridHighlightCategory, HashSet<GridPosition>>();
        private readonly Dictionary<GridHighlightCategory, Material> generatedMaterialsByCategory = new Dictionary<GridHighlightCategory, Material>();
        private readonly List<GridPosition> hoverPath = new List<GridPosition>();

        public GridTerrainView TerrainView
        {
            get { return terrainView; }
            set
            {
                if (terrainView == value)
                {
                    return;
                }

                terrainView = value;
                RebuildTileIndex();
            }
        }

        public bool RebuildIndexFromSceneWhenMissing
        {
            get { return rebuildIndexFromSceneWhenMissing; }
            set { rebuildIndexFromSceneWhenMissing = value; }
        }

        public int RegisteredTileCount
        {
            get { return tilesByPosition.Count; }
        }

        public IReadOnlyList<GridPosition> HoverPath
        {
            get { return hoverPath; }
        }

        public void RebuildTileIndex()
        {
            tilesByPosition.Clear();

            var resolvedTerrainView = ResolveTerrainView();
            if (resolvedTerrainView != null)
            {
                RegisterTiles(resolvedTerrainView.GeneratedTiles, applyImmediately: false);

                var childTiles = resolvedTerrainView.GetComponentsInChildren<GridTileView>(includeInactive: true);
                RegisterTiles(childTiles, applyImmediately: false);
            }

            if (tilesByPosition.Count == 0 && rebuildIndexFromSceneWhenMissing)
            {
                var sceneTiles = UnityEngine.Object.FindObjectsByType<GridTileView>(
                    FindObjectsInactive.Include,
                    FindObjectsSortMode.None);
                RegisterTiles(sceneTiles, applyImmediately: false);
            }

            ApplyHighlightsToRegisteredTiles();
        }

        public bool RegisterTile(GridTileView tileView)
        {
            if (tileView == null)
            {
                return false;
            }

            tilesByPosition[tileView.GridPosition] = tileView;
            ApplyHighlightToTile(tileView);
            return true;
        }

        public int RegisterTiles(IEnumerable<GridTileView> tileViews)
        {
            return RegisterTiles(tileViews, applyImmediately: true);
        }

        public bool UnregisterTile(GridPosition position)
        {
            if (!tilesByPosition.TryGetValue(position, out var tileView))
            {
                return false;
            }

            if (tileView != null)
            {
                tileView.SetHighlighted(false);
            }

            tilesByPosition.Remove(position);
            return true;
        }

        public bool TryGetTile(GridPosition position, out GridTileView tileView)
        {
            EnsureTileIndex();
            return tilesByPosition.TryGetValue(position, out tileView) && tileView != null;
        }

        public void ClearAll()
        {
            highlightedCellsByCategory.Clear();
            hoverPath.Clear();
            ApplyHighlightsToRegisteredTiles();
        }

        public void Clear(GridHighlightCategory category)
        {
            ClearCategory(category);
        }

        public void ClearCategory(GridHighlightCategory category)
        {
            EnsureKnownCategory(category);
            highlightedCellsByCategory.Remove(category);
            if (category == GridHighlightCategory.SelectedPath)
            {
                hoverPath.Clear();
            }

            ApplyHighlightsToRegisteredTiles();
        }

        public void HighlightCell(GridHighlightCategory category, GridPosition position)
        {
            SetHighlightedCells(category, new[] { position });
        }

        public void HighlightCells(GridHighlightCategory category, IEnumerable<GridPosition> cells)
        {
            SetHighlightedCells(category, cells);
        }

        public void SetHighlightedCells(GridHighlightCategory category, IEnumerable<GridPosition> cells)
        {
            EnsureKnownCategory(category);
            var positions = CopyPositions(cells);
            SetCategoryCells(category, positions);

            if (category == GridHighlightCategory.SelectedPath)
            {
                ReplaceHoverPath(positions);
            }

            ApplyHighlightsToRegisteredTiles();
        }

        public void AddHighlightedCells(GridHighlightCategory category, IEnumerable<GridPosition> cells)
        {
            EnsureKnownCategory(category);
            if (cells == null)
            {
                return;
            }

            if (!highlightedCellsByCategory.TryGetValue(category, out var categoryCells))
            {
                categoryCells = new HashSet<GridPosition>();
                highlightedCellsByCategory[category] = categoryCells;
            }

            foreach (var cell in cells)
            {
                categoryCells.Add(cell);
                if (category == GridHighlightCategory.SelectedPath && !hoverPath.Contains(cell))
                {
                    hoverPath.Add(cell);
                }
            }

            ApplyHighlightsToRegisteredTiles();
        }

        public void SetHoverPath(IEnumerable<GridPosition> path)
        {
            EnsureKnownCategory(GridHighlightCategory.SelectedPath);
            var positions = CopyPositions(path);
            ReplaceHoverPath(positions);
            SetCategoryCells(GridHighlightCategory.SelectedPath, positions);
            ApplyHighlightsToRegisteredTiles();
        }

        public void ClearHoverPath()
        {
            hoverPath.Clear();
            highlightedCellsByCategory.Remove(GridHighlightCategory.SelectedPath);
            ApplyHighlightsToRegisteredTiles();
        }

        public IReadOnlyCollection<GridPosition> GetHighlightedCells(GridHighlightCategory category)
        {
            EnsureKnownCategory(category);
            if (highlightedCellsByCategory.TryGetValue(category, out var cells))
            {
                return new List<GridPosition>(cells).AsReadOnly();
            }

            return NoPositions;
        }

        public bool IsHighlighted(GridPosition position, GridHighlightCategory category)
        {
            EnsureKnownCategory(category);
            return highlightedCellsByCategory.TryGetValue(category, out var cells) && cells.Contains(position);
        }

        public bool TryGetTopCategory(GridPosition position, out GridHighlightCategory category)
        {
            if (IsHighlighted(position, GridHighlightCategory.TargetCell))
            {
                category = GridHighlightCategory.TargetCell;
                return true;
            }

            if (IsHighlighted(position, GridHighlightCategory.SelectedPath))
            {
                category = GridHighlightCategory.SelectedPath;
                return true;
            }

            if (IsHighlighted(position, GridHighlightCategory.ReactionThreatened))
            {
                category = GridHighlightCategory.ReactionThreatened;
                return true;
            }

            if (IsHighlighted(position, GridHighlightCategory.ActionDanger))
            {
                category = GridHighlightCategory.ActionDanger;
                return true;
            }

            if (IsHighlighted(position, GridHighlightCategory.ReactionSafe))
            {
                category = GridHighlightCategory.ReactionSafe;
                return true;
            }

            if (IsHighlighted(position, GridHighlightCategory.MovementRange))
            {
                category = GridHighlightCategory.MovementRange;
                return true;
            }

            category = default;
            return false;
        }

        public Material GetMaterialForCategory(GridHighlightCategory category)
        {
            EnsureKnownCategory(category);
            switch (category)
            {
                case GridHighlightCategory.MovementRange:
                    return movementRangeMaterial != null
                        ? movementRangeMaterial
                        : GetTerrainHighlightMaterialOrFallback(category, movementRangeColor);
                case GridHighlightCategory.SelectedPath:
                    return selectedPathMaterial != null
                        ? selectedPathMaterial
                        : GetTerrainHighlightMaterialOrFallback(category, selectedPathColor);
                case GridHighlightCategory.ActionDanger:
                    return actionDangerMaterial != null
                        ? actionDangerMaterial
                        : GetTerrainDangerMaterialOrFallback(category, actionDangerColor);
                case GridHighlightCategory.ReactionSafe:
                    return reactionSafeMaterial != null
                        ? reactionSafeMaterial
                        : GetTerrainSafeMaterialOrFallback(category, reactionSafeColor);
                case GridHighlightCategory.ReactionThreatened:
                    return reactionThreatenedMaterial != null
                        ? reactionThreatenedMaterial
                        : GetTerrainDangerMaterialOrFallback(category, reactionThreatenedColor);
                case GridHighlightCategory.TargetCell:
                    return targetCellMaterial != null
                        ? targetCellMaterial
                        : GetTerrainHighlightMaterialOrFallback(category, targetCellColor);
                default:
                    throw new ArgumentOutOfRangeException(nameof(category), category, "Unknown grid highlight category.");
            }
        }

        private void Awake()
        {
            RebuildTileIndex();
        }

        private void OnDisable()
        {
            ClearAll();
        }

        private void OnDestroy()
        {
            foreach (var material in generatedMaterialsByCategory.Values)
            {
                DestroyObject(material);
            }

            generatedMaterialsByCategory.Clear();
        }

        private GridTerrainView ResolveTerrainView()
        {
            if (terrainView != null)
            {
                return terrainView;
            }

            terrainView = GetComponent<GridTerrainView>();
            if (terrainView != null)
            {
                return terrainView;
            }

            terrainView = GetComponentInParent<GridTerrainView>();
            if (terrainView != null)
            {
                return terrainView;
            }

            terrainView = UnityEngine.Object.FindFirstObjectByType<GridTerrainView>();
            return terrainView;
        }

        private void EnsureTileIndex()
        {
            if (tilesByPosition.Count == 0)
            {
                RebuildTileIndex();
            }
        }

        private int RegisterTiles(IEnumerable<GridTileView> tileViews, bool applyImmediately)
        {
            if (tileViews == null)
            {
                return 0;
            }

            var count = 0;
            foreach (var tileView in tileViews)
            {
                if (tileView == null)
                {
                    continue;
                }

                tilesByPosition[tileView.GridPosition] = tileView;
                count += 1;
            }

            if (applyImmediately && count > 0)
            {
                ApplyHighlightsToRegisteredTiles();
            }

            return count;
        }

        private void SetCategoryCells(GridHighlightCategory category, IEnumerable<GridPosition> positions)
        {
            var newCells = new HashSet<GridPosition>();
            foreach (var position in positions)
            {
                newCells.Add(position);
            }

            if (newCells.Count == 0)
            {
                highlightedCellsByCategory.Remove(category);
            }
            else
            {
                highlightedCellsByCategory[category] = newCells;
            }
        }

        private void ReplaceHoverPath(IEnumerable<GridPosition> positions)
        {
            hoverPath.Clear();
            hoverPath.AddRange(positions);
        }

        private void ApplyHighlightsToRegisteredTiles()
        {
            EnsureTileIndexWithoutApplying();

            foreach (var pair in tilesByPosition)
            {
                ApplyHighlightToTile(pair.Value);
            }
        }

        private void EnsureTileIndexWithoutApplying()
        {
            if (tilesByPosition.Count != 0)
            {
                return;
            }

            var resolvedTerrainView = ResolveTerrainView();
            if (resolvedTerrainView != null)
            {
                RegisterTiles(resolvedTerrainView.GeneratedTiles, applyImmediately: false);

                var childTiles = resolvedTerrainView.GetComponentsInChildren<GridTileView>(includeInactive: true);
                RegisterTiles(childTiles, applyImmediately: false);
            }

            if (tilesByPosition.Count == 0 && rebuildIndexFromSceneWhenMissing)
            {
                var sceneTiles = UnityEngine.Object.FindObjectsByType<GridTileView>(
                    FindObjectsInactive.Include,
                    FindObjectsSortMode.None);
                RegisterTiles(sceneTiles, applyImmediately: false);
            }
        }

        private void ApplyHighlightToTile(GridTileView tileView)
        {
            if (tileView == null)
            {
                return;
            }

            if (TryGetTopCategory(tileView.GridPosition, out var category))
            {
                tileView.SetHighlightMaterial(GetMaterialForCategory(category));
                tileView.SetHighlighted(true);
                return;
            }

            tileView.SetHighlighted(false);
        }

        private Material GetTerrainHighlightMaterialOrFallback(GridHighlightCategory category, Color fallbackColor)
        {
            var resolvedTerrainView = ResolveTerrainView();
            if (resolvedTerrainView != null && resolvedTerrainView.HighlightMaterial != null)
            {
                return resolvedTerrainView.HighlightMaterial;
            }

            return GetOrCreateGeneratedMaterial(category, fallbackColor);
        }

        private Material GetTerrainDangerMaterialOrFallback(GridHighlightCategory category, Color fallbackColor)
        {
            var resolvedTerrainView = ResolveTerrainView();
            if (resolvedTerrainView != null && resolvedTerrainView.DangerMaterial != null)
            {
                return resolvedTerrainView.DangerMaterial;
            }

            return GetOrCreateGeneratedMaterial(category, fallbackColor);
        }

        private Material GetTerrainSafeMaterialOrFallback(GridHighlightCategory category, Color fallbackColor)
        {
            var resolvedTerrainView = ResolveTerrainView();
            if (resolvedTerrainView != null && resolvedTerrainView.SafeMaterial != null)
            {
                return resolvedTerrainView.SafeMaterial;
            }

            return GetOrCreateGeneratedMaterial(category, fallbackColor);
        }

        private Material GetOrCreateGeneratedMaterial(GridHighlightCategory category, Color color)
        {
            if (generatedMaterialsByCategory.TryGetValue(category, out var material) && material != null)
            {
                return material;
            }

            material = CreateRuntimeMaterial($"Generated {category} Grid Highlight", color);
            if (material != null)
            {
                generatedMaterialsByCategory[category] = material;
            }

            return material;
        }

        private static List<GridPosition> CopyPositions(IEnumerable<GridPosition> positions)
        {
            var copy = new List<GridPosition>();
            if (positions == null)
            {
                return copy;
            }

            foreach (var position in positions)
            {
                copy.Add(position);
            }

            return copy;
        }

        private static void EnsureKnownCategory(GridHighlightCategory category)
        {
            if (!IsKnownCategory(category))
            {
                throw new ArgumentOutOfRangeException(nameof(category), category, "Unknown grid highlight category.");
            }
        }

        private static bool IsKnownCategory(GridHighlightCategory category)
        {
            switch (category)
            {
                case GridHighlightCategory.MovementRange:
                case GridHighlightCategory.SelectedPath:
                case GridHighlightCategory.ActionDanger:
                case GridHighlightCategory.ReactionSafe:
                case GridHighlightCategory.ReactionThreatened:
                case GridHighlightCategory.TargetCell:
                    return true;
                default:
                    return false;
            }
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

        private static void DestroyObject(UnityEngine.Object value)
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
