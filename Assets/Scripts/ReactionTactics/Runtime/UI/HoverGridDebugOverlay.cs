using ReactionTactics.Grid;
using ReactionTactics.Input;
using ReactionTactics.Reactions;
using ReactionTactics.Units;
using UnityEngine;

namespace ReactionTactics.UI
{
    /// <summary>
    /// Lightweight prototype-only overlay for inspecting the currently hovered grid cell.
    /// It uses <see cref="GridPicker"/> hover state when available and falls back to a
    /// direct tile raycast so the debug panel remains useful before the full input scene
    /// setup tool exists.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class HoverGridDebugOverlay : MonoBehaviour
    {
        private const float DefaultMaxRaycastDistance = 500f;
        private const float MinimumPanelWidth = 180f;
        private const float MinimumPanelHeight = 80f;
        private const float CompactPanelHeight = 42f;
        private const int MaxRaycastHits = 64;

        [SerializeField]
        [Tooltip("Grid picker whose hover state drives the debug panel. If empty, one is found automatically.")]
        private GridPicker gridPicker;

        [SerializeField]
        [Tooltip("Grid manager that owns terrain data for the hovered cell. If empty, one is found automatically.")]
        private GridManager gridManager;

        [SerializeField]
        [Tooltip("Unit registry used to show the occupant of the hovered cell. If empty, one is found automatically.")]
        private UnitRegistry unitRegistry;

        [SerializeField]
        [Tooltip("Camera used by fallback debug raycasts when no GridPicker is present.")]
        private Camera sourceCamera;

        [SerializeField]
        [Tooltip("Optional reaction movement safety preview whose hover reasons are appended to this debug panel.")]
        private ReactionMovementSafetyPreviewController reactionSafetyPreview;

        [SerializeField]
        [Tooltip("Physics layers included by fallback debug raycasts when no GridPicker is present.")]
        private LayerMask fallbackRaycastLayerMask = ~0;

        [SerializeField]
        [Min(0.01f)]
        [Tooltip("Maximum world distance for fallback debug raycasts.")]
        private float fallbackMaxRaycastDistance = DefaultMaxRaycastDistance;

        [SerializeField]
        [Tooltip("When true, the overlay directly raycasts GridTileView colliders if no GridPicker is available.")]
        private bool fallbackRaycastWhenPickerMissing = true;

        [SerializeField]
        [Tooltip("Show a compact one-line panel instead of hiding entirely when no grid cell is hovered.")]
        private bool compactWhenNoCellHovered = true;

        [SerializeField]
        [Tooltip("Screen-space rectangle used for the expanded hover debug panel.")]
        private Rect panelRect = new Rect(12f, 12f, 320f, 150f);

        [SerializeField]
        [Tooltip("Header text shown at the top of the debug panel.")]
        private string panelTitle = "Hover Debug";

        private readonly RaycastHit[] hitBuffer = new RaycastHit[MaxRaycastHits];

        private bool hasHoveredPosition;
        private GridPosition hoveredPosition;
        private bool hasCurrentInfo;
        private HoverGridDebugInfo currentInfo;
        private string currentFailureReason = string.Empty;
        private GUIStyle labelStyle;

        public GridPicker GridPicker
        {
            get { return gridPicker; }
        }

        public GridManager GridManager
        {
            get { return gridManager; }
        }

        public UnitRegistry UnitRegistry
        {
            get { return unitRegistry; }
        }

        public ReactionMovementSafetyPreviewController ReactionSafetyPreview
        {
            get { return reactionSafetyPreview; }
            set { reactionSafetyPreview = value; }
        }

        public bool HasHoveredPosition
        {
            get { return hasHoveredPosition; }
        }

        public GridPosition HoveredPosition
        {
            get { return hoveredPosition; }
        }

        public bool HasCurrentInfo
        {
            get { return hasCurrentInfo; }
        }

        public HoverGridDebugInfo CurrentInfo
        {
            get { return currentInfo; }
        }

        public bool CompactWhenNoCellHovered
        {
            get { return compactWhenNoCellHovered; }
            set { compactWhenNoCellHovered = value; }
        }

        public Rect PanelRect
        {
            get { return panelRect; }
            set { panelRect = ClampPanelRect(value); }
        }

        public void Configure(
            GridPicker picker,
            GridManager manager,
            UnitRegistry registry,
            Camera camera,
            ReactionMovementSafetyPreviewController safetyPreview = null)
        {
            gridPicker = picker;
            gridManager = manager;
            unitRegistry = registry;
            sourceCamera = camera;
            reactionSafetyPreview = safetyPreview;
        }

        public bool TryGetDebugInfo(GridPosition position, out HoverGridDebugInfo info)
        {
            ResolveMissingReferences();
            var map = gridManager != null ? gridManager.CurrentMap : null;
            return TryBuildDebugInfo(map, unitRegistry, position, out info);
        }

        public static bool TryBuildDebugInfo(
            IGridMap map,
            UnitRegistry registry,
            GridPosition position,
            out HoverGridDebugInfo info)
        {
            if (map == null || !map.TryGetCell(position, out var cell))
            {
                info = default;
                return false;
            }

            TacticalUnit occupant = null;
            if (registry != null)
            {
                registry.TryGetUnitAt(position, out occupant, livingOnly: false);
            }

            info = new HoverGridDebugInfo(cell, occupant);
            return true;
        }

        public static string FormatInfo(HoverGridDebugInfo info)
        {
            return $"Cell: {info.Position}\n"
                + $"Occupant: {info.OccupantLabel}\n"
                + $"Walkable: {info.Walkable}  Blocks Move: {info.BlocksMovement}\n"
                + $"Blocks LoS: {info.BlocksLineOfSight}\n"
                + $"Height: {info.HeightY}  Display: {info.DisplayHeight:0.##}\n"
                + $"Movement Cost: {info.MovementCost}";
        }

        public static string FormatInfo(HoverGridDebugInfo info, ReactionSafetyCell safetyCell)
        {
            return FormatInfo(info) + "\n" + ReactionMovementSafetyPreviewController.FormatSafetyTooltip(safetyCell);
        }

        private void Awake()
        {
            ResolveMissingReferences();
            panelRect = ClampPanelRect(panelRect);
        }

        private void Update()
        {
            ResolveMissingReferences();
            RefreshHoverState();
        }

        private void OnGUI()
        {
            if (!hasHoveredPosition)
            {
                if (!compactWhenNoCellHovered)
                {
                    return;
                }

                DrawCompactNoHoverPanel();
                return;
            }

            DrawHoverPanel();
        }

        private void OnValidate()
        {
            fallbackMaxRaycastDistance = Mathf.Max(0.01f, fallbackMaxRaycastDistance);
            panelRect = ClampPanelRect(panelRect);
        }

        private void RefreshHoverState()
        {
            if (gridPicker != null && gridPicker.isActiveAndEnabled)
            {
                if (gridPicker.HasCurrentHoverCell)
                {
                    SetHoveredPosition(gridPicker.CurrentHoverCell);
                    return;
                }

                if (gridPicker.HasCurrentHoverUnit && gridPicker.CurrentHoverUnit != null)
                {
                    SetHoveredPosition(gridPicker.CurrentHoverUnit.CurrentGridPosition);
                    return;
                }

                ClearHoverState();
                return;
            }

            if (fallbackRaycastWhenPickerMissing && TryFallbackPickTile(out var fallbackPosition))
            {
                SetHoveredPosition(fallbackPosition);
                return;
            }

            ClearHoverState();
        }

        private void SetHoveredPosition(GridPosition position)
        {
            hasHoveredPosition = true;
            hoveredPosition = position;

            if (TryGetDebugInfo(position, out currentInfo))
            {
                hasCurrentInfo = true;
                currentFailureReason = string.Empty;
                return;
            }

            hasCurrentInfo = false;
            currentInfo = default;
            currentFailureReason = gridManager == null || gridManager.CurrentMap == null
                ? "No active grid map is available."
                : "Hovered position is not present in the active grid map.";
        }

        private void ClearHoverState()
        {
            hasHoveredPosition = false;
            hoveredPosition = GridPosition.Zero;
            hasCurrentInfo = false;
            currentInfo = default;
            currentFailureReason = string.Empty;
        }

        private bool TryFallbackPickTile(out GridPosition position)
        {
            position = GridPosition.Zero;

            var cameraToUse = ResolveCamera();
            if (cameraToUse == null)
            {
                return false;
            }

            var ray = cameraToUse.ScreenPointToRay(UnityEngine.Input.mousePosition);
            var hitCount = Physics.RaycastNonAlloc(
                ray,
                hitBuffer,
                fallbackMaxRaycastDistance,
                fallbackRaycastLayerMask,
                QueryTriggerInteraction.Ignore);

            GridTileView nearestTile = null;
            var nearestDistance = float.PositiveInfinity;
            for (var index = 0; index < hitCount; index++)
            {
                var hit = hitBuffer[index];
                if (hit.collider == null || hit.distance >= nearestDistance)
                {
                    continue;
                }

                var tile = hit.collider.GetComponentInParent<GridTileView>();
                if (tile == null)
                {
                    continue;
                }

                nearestTile = tile;
                nearestDistance = hit.distance;
            }

            if (nearestTile == null)
            {
                return false;
            }

            position = nearestTile.GridPosition;
            return true;
        }

        private Camera ResolveCamera()
        {
            if (sourceCamera != null && sourceCamera.isActiveAndEnabled)
            {
                return sourceCamera;
            }

            var mainCamera = Camera.main;
            if (mainCamera != null && mainCamera.isActiveAndEnabled)
            {
                sourceCamera = mainCamera;
                return sourceCamera;
            }

            sourceCamera = FindAnyObjectByType<Camera>();
            return sourceCamera;
        }

        private void ResolveMissingReferences()
        {
            if (gridPicker == null)
            {
                gridPicker = FindAnyObjectByType<GridPicker>();
            }

            if (gridManager == null)
            {
                gridManager = FindAnyObjectByType<GridManager>();
            }

            if (unitRegistry == null)
            {
                unitRegistry = FindAnyObjectByType<UnitRegistry>();
            }

            if (sourceCamera == null)
            {
                sourceCamera = Camera.main ?? FindAnyObjectByType<Camera>();
            }

            if (reactionSafetyPreview == null)
            {
                reactionSafetyPreview = FindAnyObjectByType<ReactionMovementSafetyPreviewController>();
            }
        }

        private void DrawCompactNoHoverPanel()
        {
            EnsureStyles();
            var compactRect = new Rect(panelRect.x, panelRect.y, panelRect.width, CompactPanelHeight);
            GUILayout.BeginArea(compactRect, panelTitle, GUI.skin.window);
            GUILayout.Label("No grid cell hovered", labelStyle);
            GUILayout.EndArea();
        }

        private void DrawHoverPanel()
        {
            EnsureStyles();
            GUILayout.BeginArea(panelRect, panelTitle, GUI.skin.window);
            var panelText = hasCurrentInfo
                ? FormatInfo(currentInfo)
                : $"Cell: {hoveredPosition}\n{currentFailureReason}";

            if (reactionSafetyPreview != null
                && reactionSafetyPreview.TryGetSafetyCell(hoveredPosition, out var safetyCell))
            {
                panelText += "\n" + ReactionMovementSafetyPreviewController.FormatSafetyTooltip(safetyCell);
            }

            GUILayout.Label(panelText, labelStyle);
            GUILayout.EndArea();
        }

        private void EnsureStyles()
        {
            if (labelStyle == null)
            {
                labelStyle = new GUIStyle(GUI.skin.label)
                {
                    wordWrap = true,
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
    }
}
