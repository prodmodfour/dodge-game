using System;
using ReactionTactics.Grid;
using UnityEngine;

namespace ReactionTactics.Input
{
    /// <summary>
    /// Keyboard-and-mouse tactical board camera for the prototype scene.
    /// The controller orbits around a movable focus point so the camera can pan,
    /// rotate, and zoom while keeping the 3D grid readable.
    /// </summary>
    [DisallowMultipleComponent]
    [RequireComponent(typeof(Camera))]
    public sealed class TacticalCameraController : MonoBehaviour
    {
        private const float DefaultPanSpeed = 6f;
        private const float DefaultRotationSpeed = 90f;
        private const float DefaultZoomSpeed = 4f;
        private const float DefaultMinimumZoomDistance = 4f;
        private const float DefaultMaximumZoomDistance = 24f;
        private const float DefaultPitchDegrees = 55f;
        private const float DefaultMinimumPitchDegrees = 35f;
        private const float DefaultMaximumPitchDegrees = 70f;
        private const float DefaultYawDegrees = 45f;
        private const float DefaultFramePaddingCells = 1.5f;
        private const float DefaultFrameZoomMultiplier = 1.35f;
        private const float MinimumPositiveValue = 0.01f;
        private const float MinimumDeltaTime = 1f / 120f;

        [SerializeField]
        [Tooltip("Grid manager used to frame the board at play-mode start. If empty, the scene is searched at runtime.")]
        private GridManager gridManager;

        [SerializeField]
        [Tooltip("Frame the active grid map when play mode starts so the whole default board is visible.")]
        private bool frameGridOnStart = true;

        [SerializeField]
        [Tooltip("World-space point the camera orbits around and pans across.")]
        private Vector3 focusPoint = new Vector3(4f, 0f, 4f);

        [SerializeField]
        [Tooltip("Initial yaw around the board in degrees. Q/E rotate this value during play.")]
        private float yawDegrees = DefaultYawDegrees;

        [SerializeField]
        [Tooltip("Downward camera pitch in degrees. Clamped between the configured pitch limits.")]
        private float pitchDegrees = DefaultPitchDegrees;

        [SerializeField]
        [Tooltip("Minimum allowed downward camera pitch in degrees.")]
        private float minimumPitchDegrees = DefaultMinimumPitchDegrees;

        [SerializeField]
        [Tooltip("Maximum allowed downward camera pitch in degrees.")]
        private float maximumPitchDegrees = DefaultMaximumPitchDegrees;

        [SerializeField]
        [Tooltip("Camera distance from the focus point. Mouse wheel changes this value during play.")]
        private float zoomDistance = 12f;

        [SerializeField]
        [Min(MinimumPositiveValue)]
        [Tooltip("Closest allowed camera distance from the focus point.")]
        private float minimumZoomDistance = DefaultMinimumZoomDistance;

        [SerializeField]
        [Min(MinimumPositiveValue)]
        [Tooltip("Farthest allowed camera distance from the focus point.")]
        private float maximumZoomDistance = DefaultMaximumZoomDistance;

        [SerializeField]
        [Min(0f)]
        [Tooltip("World units per second for WASD/arrow-key panning.")]
        private float panSpeed = DefaultPanSpeed;

        [SerializeField]
        [Min(0f)]
        [Tooltip("Degrees per second for Q/E rotation.")]
        private float rotationSpeed = DefaultRotationSpeed;

        [SerializeField]
        [Min(0f)]
        [Tooltip("Zoom-distance units changed per mouse-wheel tick.")]
        private float zoomSpeed = DefaultZoomSpeed;

        [SerializeField]
        [Min(0f)]
        [Tooltip("Extra horizontal cells included when automatically framing the grid.")]
        private float framePaddingCells = DefaultFramePaddingCells;

        public GridManager GridManager
        {
            get { return gridManager; }
            set { gridManager = value; }
        }

        public bool FrameGridOnStart
        {
            get { return frameGridOnStart; }
            set { frameGridOnStart = value; }
        }

        public Vector3 FocusPoint
        {
            get { return focusPoint; }
        }

        public float YawDegrees
        {
            get { return yawDegrees; }
        }

        public float PitchDegrees
        {
            get { return pitchDegrees; }
        }

        public float ZoomDistance
        {
            get { return zoomDistance; }
        }

        public float MinimumZoomDistance
        {
            get { return minimumZoomDistance; }
        }

        public float MaximumZoomDistance
        {
            get { return maximumZoomDistance; }
        }

        public float MinimumPitchDegrees
        {
            get { return minimumPitchDegrees; }
        }

        public float MaximumPitchDegrees
        {
            get { return maximumPitchDegrees; }
        }

        /// <summary>
        /// Sets a camera view using the same clamping rules as runtime input.
        /// </summary>
        public void SetView(Vector3 focus, float yaw, float pitch, float zoom)
        {
            focusPoint = focus;
            yawDegrees = yaw;
            pitchDegrees = pitch;
            zoomDistance = zoom;
            ClampSerializedValues();
            ApplyView();
        }

        public void SetZoomLimits(float minimumDistance, float maximumDistance)
        {
            if (!IsPositiveFinite(minimumDistance))
            {
                throw new ArgumentOutOfRangeException(
                    nameof(minimumDistance),
                    minimumDistance,
                    "Minimum zoom distance must be a positive finite value.");
            }

            if (!IsPositiveFinite(maximumDistance))
            {
                throw new ArgumentOutOfRangeException(
                    nameof(maximumDistance),
                    maximumDistance,
                    "Maximum zoom distance must be a positive finite value.");
            }

            minimumZoomDistance = minimumDistance;
            maximumZoomDistance = Mathf.Max(maximumDistance, minimumZoomDistance);
            ClampSerializedValues();
            ApplyView();
        }

        public void SetPitchLimits(float minimumPitch, float maximumPitch)
        {
            if (!IsFinite(minimumPitch))
            {
                throw new ArgumentOutOfRangeException(
                    nameof(minimumPitch),
                    minimumPitch,
                    "Minimum pitch must be a finite value.");
            }

            if (!IsFinite(maximumPitch))
            {
                throw new ArgumentOutOfRangeException(
                    nameof(maximumPitch),
                    maximumPitch,
                    "Maximum pitch must be a finite value.");
            }

            minimumPitchDegrees = Mathf.Clamp(minimumPitch, 1f, 89f);
            maximumPitchDegrees = Mathf.Clamp(Mathf.Max(maximumPitch, minimumPitchDegrees), minimumPitchDegrees, 89f);
            ClampSerializedValues();
            ApplyView();
        }

        public bool TryFrameGrid()
        {
            var resolvedGridManager = ResolveGridManager();
            if (resolvedGridManager == null)
            {
                return false;
            }

            if (!resolvedGridManager.HasCurrentMap && !resolvedGridManager.RebuildMap())
            {
                return false;
            }

            if (resolvedGridManager.CurrentMap == null)
            {
                return false;
            }

            FrameMap(resolvedGridManager.CurrentMap, resolvedGridManager.Metrics);
            return true;
        }

        public void FrameMap(IGridMap map, GridMetrics metrics)
        {
            if (map == null)
            {
                throw new ArgumentNullException(nameof(map));
            }

            focusPoint = CalculateMapFocusPoint(map, metrics);
            zoomDistance = CalculateMapZoomDistance(map, metrics, framePaddingCells);
            ClampSerializedValues();
            ApplyView();
        }

        public void ApplyView()
        {
            ClampSerializedValues();
            var rotation = Quaternion.Euler(pitchDegrees, yawDegrees, 0f);
            var position = focusPoint - (rotation * Vector3.forward * zoomDistance);
            transform.SetPositionAndRotation(position, rotation);
        }

        public static Vector3 CalculateMapFocusPoint(IGridMap map, GridMetrics metrics)
        {
            if (map == null)
            {
                throw new ArgumentNullException(nameof(map));
            }

            var minWorld = metrics.GridToWorldCenter(map.MinBounds);
            var maxWorld = metrics.GridToWorldCenter(map.MaxBounds);
            return (minWorld + maxWorld) * 0.5f;
        }

        public static float CalculateMapZoomDistance(IGridMap map, GridMetrics metrics, float paddingCells)
        {
            if (map == null)
            {
                throw new ArgumentNullException(nameof(map));
            }

            var minWorld = metrics.GridToWorldCenter(map.MinBounds);
            var maxWorld = metrics.GridToWorldCenter(map.MaxBounds);
            var width = Mathf.Abs(maxWorld.x - minWorld.x) + metrics.CellSize;
            var depth = Mathf.Abs(maxWorld.z - minWorld.z) + metrics.CellSize;
            var height = Mathf.Abs(maxWorld.y - minWorld.y) + metrics.HeightStep;
            var horizontalSpan = Mathf.Max(width, depth) + (Mathf.Max(0f, paddingCells) * metrics.CellSize);
            return Mathf.Max(DefaultMinimumZoomDistance, (horizontalSpan * DefaultFrameZoomMultiplier) + (height * 0.5f));
        }

        private void Reset()
        {
            gridManager = null;
            frameGridOnStart = true;
            focusPoint = new Vector3(4f, 0f, 4f);
            yawDegrees = DefaultYawDegrees;
            pitchDegrees = DefaultPitchDegrees;
            minimumPitchDegrees = DefaultMinimumPitchDegrees;
            maximumPitchDegrees = DefaultMaximumPitchDegrees;
            zoomDistance = 12f;
            minimumZoomDistance = DefaultMinimumZoomDistance;
            maximumZoomDistance = DefaultMaximumZoomDistance;
            panSpeed = DefaultPanSpeed;
            rotationSpeed = DefaultRotationSpeed;
            zoomSpeed = DefaultZoomSpeed;
            framePaddingCells = DefaultFramePaddingCells;
            ApplyView();
        }

        private void Start()
        {
            if (!frameGridOnStart || !TryFrameGrid())
            {
                ApplyView();
            }
        }

        private void LateUpdate()
        {
            var deltaTime = GetFrameDeltaTime();
            HandleRotationInput(deltaTime);
            HandleZoomInput();
            HandlePanInput(deltaTime);
            ApplyView();
        }

        private void OnValidate()
        {
            ClampSerializedValues();
        }

        private void HandlePanInput(float deltaTime)
        {
            var horizontal = 0f;
            if (UnityEngine.Input.GetKey(KeyCode.D) || UnityEngine.Input.GetKey(KeyCode.RightArrow))
            {
                horizontal += 1f;
            }

            if (UnityEngine.Input.GetKey(KeyCode.A) || UnityEngine.Input.GetKey(KeyCode.LeftArrow))
            {
                horizontal -= 1f;
            }

            var vertical = 0f;
            if (UnityEngine.Input.GetKey(KeyCode.W) || UnityEngine.Input.GetKey(KeyCode.UpArrow))
            {
                vertical += 1f;
            }

            if (UnityEngine.Input.GetKey(KeyCode.S) || UnityEngine.Input.GetKey(KeyCode.DownArrow))
            {
                vertical -= 1f;
            }

            var input = new Vector2(horizontal, vertical);
            if (input.sqrMagnitude <= Mathf.Epsilon)
            {
                return;
            }

            input = Vector2.ClampMagnitude(input, 1f);
            var yawRotation = Quaternion.Euler(0f, yawDegrees, 0f);
            var right = yawRotation * Vector3.right;
            var forward = yawRotation * Vector3.forward;
            focusPoint += ((right * input.x) + (forward * input.y)) * (panSpeed * deltaTime);
        }

        private void HandleRotationInput(float deltaTime)
        {
            var rotationInput = 0f;
            if (UnityEngine.Input.GetKey(KeyCode.E))
            {
                rotationInput += 1f;
            }

            if (UnityEngine.Input.GetKey(KeyCode.Q))
            {
                rotationInput -= 1f;
            }

            if (Mathf.Abs(rotationInput) > Mathf.Epsilon)
            {
                yawDegrees += rotationInput * rotationSpeed * deltaTime;
            }
        }

        private void HandleZoomInput()
        {
            var scroll = UnityEngine.Input.mouseScrollDelta.y;
            if (Mathf.Abs(scroll) > Mathf.Epsilon)
            {
                zoomDistance -= scroll * zoomSpeed;
            }
        }

        private GridManager ResolveGridManager()
        {
            if (gridManager != null)
            {
                return gridManager;
            }

            gridManager = FindAnyObjectByType<GridManager>();
            return gridManager;
        }

        private void ClampSerializedValues()
        {
            if (!IsPositiveFinite(minimumZoomDistance))
            {
                minimumZoomDistance = DefaultMinimumZoomDistance;
            }

            if (!IsPositiveFinite(maximumZoomDistance))
            {
                maximumZoomDistance = DefaultMaximumZoomDistance;
            }

            if (maximumZoomDistance < minimumZoomDistance)
            {
                maximumZoomDistance = minimumZoomDistance;
            }

            if (!IsFinite(minimumPitchDegrees))
            {
                minimumPitchDegrees = DefaultMinimumPitchDegrees;
            }

            if (!IsFinite(maximumPitchDegrees))
            {
                maximumPitchDegrees = DefaultMaximumPitchDegrees;
            }

            minimumPitchDegrees = Mathf.Clamp(minimumPitchDegrees, 1f, 89f);
            maximumPitchDegrees = Mathf.Clamp(Mathf.Max(maximumPitchDegrees, minimumPitchDegrees), minimumPitchDegrees, 89f);

            if (!IsFinite(pitchDegrees))
            {
                pitchDegrees = DefaultPitchDegrees;
            }

            if (!IsPositiveFinite(zoomDistance))
            {
                zoomDistance = Mathf.Max(minimumZoomDistance, 12f);
            }

            pitchDegrees = Mathf.Clamp(pitchDegrees, minimumPitchDegrees, maximumPitchDegrees);
            zoomDistance = Mathf.Clamp(zoomDistance, minimumZoomDistance, maximumZoomDistance);
            panSpeed = Mathf.Max(0f, IsFinite(panSpeed) ? panSpeed : DefaultPanSpeed);
            rotationSpeed = Mathf.Max(0f, IsFinite(rotationSpeed) ? rotationSpeed : DefaultRotationSpeed);
            zoomSpeed = Mathf.Max(0f, IsFinite(zoomSpeed) ? zoomSpeed : DefaultZoomSpeed);
            framePaddingCells = Mathf.Max(0f, IsFinite(framePaddingCells) ? framePaddingCells : DefaultFramePaddingCells);
        }

        private static float GetFrameDeltaTime()
        {
            return Time.unscaledDeltaTime > 0f && IsFinite(Time.unscaledDeltaTime)
                ? Time.unscaledDeltaTime
                : MinimumDeltaTime;
        }

        private static bool IsPositiveFinite(float value)
        {
            return IsFinite(value) && value > 0f;
        }

        private static bool IsFinite(float value)
        {
            return !float.IsNaN(value) && !float.IsInfinity(value);
        }
    }
}
