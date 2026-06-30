using System;
using System.Reflection;
using ReactionTactics.Grid;
using ReactionTactics.Units;
using UnityEngine;

namespace ReactionTactics.Input
{
    /// <summary>
    /// Converts mouse hover and click positions into tactical unit or grid tile selections by raycasting from the active camera.
    /// Unit colliders are prioritized over tile colliders when both are under the pointer.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class GridPicker : MonoBehaviour
    {
        private const float DefaultMaxRaycastDistance = 500f;
        private const int MaxRaycastHits = 64;

        [SerializeField]
        [Tooltip("Camera used for mouse-to-world raycasts. If empty, the scene's active/main camera is used.")]
        private Camera sourceCamera;

        [SerializeField]
        [Tooltip("Physics layers included when looking for TacticalUnit and GridTileView colliders.")]
        private LayerMask raycastLayerMask = ~0;

        [SerializeField]
        [Min(0.01f)]
        [Tooltip("Maximum world distance used for grid picking raycasts.")]
        private float maxRaycastDistance = DefaultMaxRaycastDistance;

        [SerializeField]
        [Tooltip("Whether trigger colliders should be considered by grid picking raycasts.")]
        private QueryTriggerInteraction triggerInteraction = QueryTriggerInteraction.Ignore;

        [SerializeField]
        [Tooltip("When true, hovering or clicking over UI returns no pick result.")]
        private bool ignorePointerOverUi = true;

        [SerializeField]
        [Tooltip("Write optional console debug logs when grid tiles are clicked. Keep disabled for normal playtest builds to avoid noisy click logs.")]
        private bool logClickedCells;

        [SerializeField]
        [Tooltip("Write optional console debug logs when tactical units are clicked. Keep disabled for normal playtest builds to avoid noisy click logs.")]
        private bool logClickedUnits;

        private static readonly Type EventSystemType = ResolveEventSystemType();
        private static readonly PropertyInfo CurrentEventSystemProperty = EventSystemType != null
            ? EventSystemType.GetProperty("current", BindingFlags.Public | BindingFlags.Static)
            : null;
        private static readonly MethodInfo IsPointerOverGameObjectMethod = EventSystemType != null
            ? EventSystemType.GetMethod("IsPointerOverGameObject", Type.EmptyTypes)
            : null;
        private static readonly MethodInfo IsPointerOverGameObjectWithIdMethod = EventSystemType != null
            ? EventSystemType.GetMethod("IsPointerOverGameObject", new[] { typeof(int) })
            : null;

        private readonly RaycastHit[] hitBuffer = new RaycastHit[MaxRaycastHits];

        public event Action<GridPickResult> HoverCellChanged;

        public event Action HoverCellCleared;

        public event Action<GridPickResult> CellClicked;

        public event Action<UnitPickResult> HoverUnitChanged;

        public event Action HoverUnitCleared;

        public event Action<UnitPickResult> UnitClicked;

        public Camera SourceCamera
        {
            get { return sourceCamera; }
            set { sourceCamera = value; }
        }

        public LayerMask RaycastLayerMask
        {
            get { return raycastLayerMask; }
            set { raycastLayerMask = value; }
        }

        public float MaxRaycastDistance
        {
            get { return maxRaycastDistance; }
            set { maxRaycastDistance = Mathf.Max(0.01f, value); }
        }

        public QueryTriggerInteraction TriggerInteraction
        {
            get { return triggerInteraction; }
            set { triggerInteraction = value; }
        }

        public bool IgnorePointerOverUi
        {
            get { return ignorePointerOverUi; }
            set { ignorePointerOverUi = value; }
        }

        public bool LogClickedCells
        {
            get { return logClickedCells; }
            set { logClickedCells = value; }
        }

        public bool LogClickedUnits
        {
            get { return logClickedUnits; }
            set { logClickedUnits = value; }
        }

        public bool HasCurrentHoverCell { get; private set; }

        public GridPosition CurrentHoverCell { get; private set; }

        public GridTileView CurrentHoverTile { get; private set; }

        public GridPickResult CurrentHoverResult { get; private set; }

        public bool HasCurrentHoverUnit { get; private set; }

        public TacticalUnit CurrentHoverUnit { get; private set; }

        public UnitPickResult CurrentHoverUnitResult { get; private set; }

        public bool TryPickCurrentPointer(out GridPickResult result)
        {
            return TryPickScreenPosition(UnityEngine.Input.mousePosition, out result);
        }

        public bool TryPickCurrentPointerUnit(out UnitPickResult result)
        {
            return TryPickUnitScreenPosition(UnityEngine.Input.mousePosition, out result);
        }

        public bool TryPickScreenPosition(Vector2 screenPosition, out GridPickResult result)
        {
            if (IsPointerBlockedByUi())
            {
                result = default;
                return false;
            }

            return TryPickScreenPositionIgnoringUi(screenPosition, out result);
        }

        public bool TryPickUnitScreenPosition(Vector2 screenPosition, out UnitPickResult result)
        {
            if (IsPointerBlockedByUi())
            {
                result = default;
                return false;
            }

            return TryPickUnitScreenPositionIgnoringUi(screenPosition, out result);
        }

        public bool TryPickScreenPositionIgnoringUi(Vector2 screenPosition, out GridPickResult result)
        {
            result = default;

            if (!TryRaycastScreenPosition(screenPosition, out var hitCount))
            {
                return false;
            }

            return TryFindNearestTileHit(hitCount, out result);
        }

        public bool TryPickUnitScreenPositionIgnoringUi(Vector2 screenPosition, out UnitPickResult result)
        {
            result = default;

            if (!TryRaycastScreenPosition(screenPosition, out var hitCount))
            {
                return false;
            }

            return TryFindNearestUnitHit(hitCount, out result);
        }

        public bool UpdateHoverAtScreenPosition(Vector2 screenPosition)
        {
            if (IsPointerBlockedByUi() || !TryRaycastScreenPosition(screenPosition, out var hitCount))
            {
                ClearCurrentHover();
                return false;
            }

            if (TryFindNearestUnitHit(hitCount, out var unitResult))
            {
                ClearCurrentCellHover();
                SetCurrentUnitHover(unitResult);
                return true;
            }

            ClearCurrentUnitHover();
            if (TryFindNearestTileHit(hitCount, out var cellResult))
            {
                SetCurrentHover(cellResult);
                return true;
            }

            ClearCurrentCellHover();
            return false;
        }

        public bool TryClickScreenPosition(Vector2 screenPosition, out GridPickResult result)
        {
            return TryClickScreenPosition(screenPosition, out result, out _);
        }

        public bool TryClickScreenPosition(Vector2 screenPosition, out GridPickResult cellResult, out UnitPickResult unitResult)
        {
            cellResult = default;
            unitResult = default;

            if (IsPointerBlockedByUi() || !TryRaycastScreenPosition(screenPosition, out var hitCount))
            {
                return false;
            }

            if (TryFindNearestUnitHit(hitCount, out unitResult))
            {
                UnitClicked?.Invoke(unitResult);
                if (logClickedUnits)
                {
                    Debug.Log(
                        $"GridPicker clicked {unitResult.DisplayName} {unitResult.UnitId} [{unitResult.Team}] at {unitResult.Position}",
                        unitResult.Unit);
                }

                return true;
            }

            if (!TryFindNearestTileHit(hitCount, out cellResult))
            {
                return false;
            }

            CellClicked?.Invoke(cellResult);
            if (logClickedCells)
            {
                Debug.Log($"GridPicker clicked {cellResult.Position}", cellResult.Tile);
            }

            return true;
        }

        public void ClearCurrentHover()
        {
            ClearCurrentCellHover();
            ClearCurrentUnitHover();
        }

        public void ClearCurrentUnitHover()
        {
            if (!HasCurrentHoverUnit)
            {
                return;
            }

            HasCurrentHoverUnit = false;
            CurrentHoverUnit = null;
            CurrentHoverUnitResult = default;
            HoverUnitCleared?.Invoke();
        }

        private void Update()
        {
            UpdateHoverAtScreenPosition(UnityEngine.Input.mousePosition);

            if (UnityEngine.Input.GetMouseButtonDown(0))
            {
                TryClickScreenPosition(UnityEngine.Input.mousePosition, out _, out _);
            }
        }

        private void OnValidate()
        {
            maxRaycastDistance = Mathf.Max(0.01f, maxRaycastDistance);
        }

        private bool TryRaycastScreenPosition(Vector2 screenPosition, out int hitCount)
        {
            hitCount = 0;

            var resolvedCamera = ResolveCamera();
            if (resolvedCamera == null)
            {
                return false;
            }

            var ray = resolvedCamera.ScreenPointToRay(screenPosition);
            hitCount = Physics.RaycastNonAlloc(
                ray,
                hitBuffer,
                maxRaycastDistance,
                raycastLayerMask,
                triggerInteraction);

            return hitCount > 0;
        }

        private bool TryFindNearestTileHit(int hitCount, out GridPickResult result)
        {
            GridTileView nearestTile = null;
            RaycastHit nearestHit = default;
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
                nearestHit = hit;
                nearestDistance = hit.distance;
            }

            if (nearestTile == null)
            {
                result = default;
                return false;
            }

            result = new GridPickResult(nearestTile, nearestHit);
            return true;
        }

        private bool TryFindNearestUnitHit(int hitCount, out UnitPickResult result)
        {
            TacticalUnit nearestUnit = null;
            RaycastHit nearestHit = default;
            var nearestDistance = float.PositiveInfinity;
            for (var index = 0; index < hitCount; index++)
            {
                var hit = hitBuffer[index];
                if (hit.collider == null || hit.distance >= nearestDistance)
                {
                    continue;
                }

                var unit = hit.collider.GetComponentInParent<TacticalUnit>();
                if (unit == null || !unit.isActiveAndEnabled)
                {
                    continue;
                }

                nearestUnit = unit;
                nearestHit = hit;
                nearestDistance = hit.distance;
            }

            if (nearestUnit == null)
            {
                result = default;
                return false;
            }

            result = new UnitPickResult(nearestUnit, nearestHit);
            return true;
        }

        private void ClearCurrentCellHover()
        {
            if (!HasCurrentHoverCell)
            {
                return;
            }

            HasCurrentHoverCell = false;
            CurrentHoverCell = GridPosition.Zero;
            CurrentHoverTile = null;
            CurrentHoverResult = default;
            HoverCellCleared?.Invoke();
        }

        private void SetCurrentHover(GridPickResult result)
        {
            var changed = !HasCurrentHoverCell
                || CurrentHoverCell != result.Position
                || CurrentHoverTile != result.Tile;

            HasCurrentHoverCell = true;
            CurrentHoverCell = result.Position;
            CurrentHoverTile = result.Tile;
            CurrentHoverResult = result;

            if (changed)
            {
                HoverCellChanged?.Invoke(result);
            }
        }

        private void SetCurrentUnitHover(UnitPickResult result)
        {
            var changed = !HasCurrentHoverUnit || CurrentHoverUnit != result.Unit;

            HasCurrentHoverUnit = true;
            CurrentHoverUnit = result.Unit;
            CurrentHoverUnitResult = result;

            if (changed)
            {
                HoverUnitChanged?.Invoke(result);
            }
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
                return mainCamera;
            }

            return FindAnyObjectByType<Camera>();
        }

        private bool IsPointerBlockedByUi()
        {
            if (!ignorePointerOverUi)
            {
                return false;
            }

            var currentEventSystem = GetCurrentEventSystem();
            if (currentEventSystem == null)
            {
                return false;
            }

            if (UnityEngine.Input.touchCount > 0)
            {
                for (var index = 0; index < UnityEngine.Input.touchCount; index++)
                {
                    var touch = UnityEngine.Input.GetTouch(index);
                    if (IsPointerOverUi(currentEventSystem, touch.fingerId))
                    {
                        return true;
                    }
                }
            }

            return IsPointerOverUi(currentEventSystem);
        }

        private static object GetCurrentEventSystem()
        {
            return CurrentEventSystemProperty != null ? CurrentEventSystemProperty.GetValue(null, null) : null;
        }

        private static bool IsPointerOverUi(object eventSystem)
        {
            return IsPointerOverGameObjectMethod != null
                && IsPointerOverGameObjectMethod.Invoke(eventSystem, null) is bool isOverUi
                && isOverUi;
        }

        private static bool IsPointerOverUi(object eventSystem, int pointerId)
        {
            return IsPointerOverGameObjectWithIdMethod != null
                && IsPointerOverGameObjectWithIdMethod.Invoke(eventSystem, new object[] { pointerId }) is bool isOverUi
                && isOverUi;
        }

        private static Type ResolveEventSystemType()
        {
            var loadedAssemblies = AppDomain.CurrentDomain.GetAssemblies();
            for (var index = 0; index < loadedAssemblies.Length; index++)
            {
                var eventSystemType = loadedAssemblies[index].GetType("UnityEngine.EventSystems.EventSystem");
                if (eventSystemType != null)
                {
                    return eventSystemType;
                }
            }

            return null;
        }
    }
}
