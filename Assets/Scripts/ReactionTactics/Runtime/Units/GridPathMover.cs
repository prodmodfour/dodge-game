using System;
using System.Collections;
using ReactionTactics.Grid;
using ReactionTactics.Pathfinding;
using UnityEngine;

namespace ReactionTactics.Units
{
    /// <summary>
    /// Visual-only mover for unit GameObjects. It follows a precomputed grid path by
    /// translating through world-space cell centers and snaps exactly to the final
    /// destination when movement completes. Gameplay state should be updated by the
    /// command system, not by this presentation component mid-step.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class GridPathMover : MonoBehaviour
    {
        private const float DefaultMovementSpeed = 4f;
        private const float MinimumMovementSpeed = 0.01f;
        private const float SnapDistance = 0.001f;
        private const float FallbackDeltaTime = 1f / 60f;

        [SerializeField]
        [Tooltip("Grid-to-world conversion used when animating path positions. Scene setup should keep this in sync with GridManager metrics.")]
        private GridMetrics gridMetrics = GridMetrics.Default;

        [SerializeField]
        [Min(MinimumMovementSpeed)]
        [Tooltip("World units per second used while moving between adjacent grid cell centers.")]
        private float movementSpeed = DefaultMovementSpeed;

        [SerializeField]
        [Tooltip("Snap to the first path cell center before starting segment animation. This keeps movement cell-center to cell-center.")]
        private bool snapToPathStart = true;

        private Coroutine activeCoroutine;
        private bool isMoving;

        public event Action<GridPathMover, GridPath> MovementCompleted;

        public GridMetrics Metrics
        {
            get { return gridMetrics; }
            set { gridMetrics = value; }
        }

        public float MovementSpeed
        {
            get { return GetEffectiveMovementSpeed(); }
            set { movementSpeed = IsPositiveFinite(value) ? Mathf.Max(MinimumMovementSpeed, value) : DefaultMovementSpeed; }
        }

        public bool SnapToPathStart
        {
            get { return snapToPathStart; }
            set { snapToPathStart = value; }
        }

        public bool IsMoving
        {
            get { return isMoving; }
        }

        public GridPath ActivePath { get; private set; }

        public GridPosition? CurrentDestination { get; private set; }

        public Coroutine MoveAlongPath(GridPath path)
        {
            return MoveAlongPath(path, gridMetrics, null);
        }

        public Coroutine MoveAlongPath(GridPath path, Action<GridPathMover, GridPath> onCompleted)
        {
            return MoveAlongPath(path, gridMetrics, onCompleted);
        }

        public Coroutine MoveAlongPath(GridPath path, GridMetrics metrics)
        {
            return MoveAlongPath(path, metrics, null);
        }

        public Coroutine MoveAlongPath(
            GridPath path,
            GridMetrics metrics,
            Action<GridPathMover, GridPath> onCompleted)
        {
            if (isMoving)
            {
                throw new InvalidOperationException($"{nameof(GridPathMover)} on '{name}' is already moving.");
            }

            ValidatePath(path);
            activeCoroutine = StartCoroutine(MoveAlongPathCoroutine(path, metrics, onCompleted));
            return activeCoroutine;
        }

        public IEnumerator MoveAlongPathCoroutine(GridPath path)
        {
            return MoveAlongPathCoroutine(path, gridMetrics, null);
        }

        public IEnumerator MoveAlongPathCoroutine(GridPath path, Action<GridPathMover, GridPath> onCompleted)
        {
            return MoveAlongPathCoroutine(path, gridMetrics, onCompleted);
        }

        public IEnumerator MoveAlongPathCoroutine(GridPath path, GridMetrics metrics)
        {
            return MoveAlongPathCoroutine(path, metrics, null);
        }

        public IEnumerator MoveAlongPathCoroutine(
            GridPath path,
            GridMetrics metrics,
            Action<GridPathMover, GridPath> onCompleted)
        {
            if (isMoving)
            {
                throw new InvalidOperationException($"{nameof(GridPathMover)} on '{name}' is already moving.");
            }

            var waypoints = BuildWorldWaypoints(path, metrics);
            isMoving = true;
            ActivePath = path;

            try
            {
                if (snapToPathStart || waypoints.Length == 1)
                {
                    transform.position = waypoints[0];
                }
                else
                {
                    CurrentDestination = path.Positions[0];
                    while (!IsAtTarget(transform.position, waypoints[0]))
                    {
                        MoveOneFrameToward(waypoints[0]);
                        yield return null;
                    }

                    transform.position = waypoints[0];
                }

                for (var index = 1; index < waypoints.Length; index++)
                {
                    CurrentDestination = path.Positions[index];
                    while (!IsAtTarget(transform.position, waypoints[index]))
                    {
                        MoveOneFrameToward(waypoints[index]);
                        yield return null;
                    }

                    transform.position = waypoints[index];
                }

                CurrentDestination = path.Destination;
                transform.position = waypoints[waypoints.Length - 1];
                MovementCompleted?.Invoke(this, path);
                onCompleted?.Invoke(this, path);
            }
            finally
            {
                isMoving = false;
                ActivePath = null;
                CurrentDestination = null;
                activeCoroutine = null;
            }
        }

        public void SnapTo(GridPosition position)
        {
            SnapTo(position, gridMetrics);
        }

        public void SnapTo(GridPosition position, GridMetrics metrics)
        {
            gridMetrics = metrics;
            transform.position = metrics.GridToWorldCenter(position);
        }

        public void StopMovement()
        {
            if (activeCoroutine != null)
            {
                StopCoroutine(activeCoroutine);
            }

            isMoving = false;
            ActivePath = null;
            CurrentDestination = null;
            activeCoroutine = null;
        }

        public static Vector3[] BuildWorldWaypoints(GridPath path, GridMetrics metrics)
        {
            ValidatePath(path);

            var waypoints = new Vector3[path.Positions.Count];
            for (var index = 0; index < path.Positions.Count; index++)
            {
                waypoints[index] = metrics.GridToWorldCenter(path.Positions[index]);
            }

            return waypoints;
        }

        private void Reset()
        {
            gridMetrics = GridMetrics.Default;
            movementSpeed = DefaultMovementSpeed;
            snapToPathStart = true;
        }

        private void OnValidate()
        {
            if (!IsPositiveFinite(movementSpeed))
            {
                movementSpeed = DefaultMovementSpeed;
            }
            else if (movementSpeed < MinimumMovementSpeed)
            {
                movementSpeed = MinimumMovementSpeed;
            }
        }

        private void MoveOneFrameToward(Vector3 target)
        {
            var maxDistanceDelta = GetEffectiveMovementSpeed() * GetFrameDeltaTime();
            transform.position = Vector3.MoveTowards(transform.position, target, maxDistanceDelta);
        }

        private float GetEffectiveMovementSpeed()
        {
            return IsPositiveFinite(movementSpeed) ? Mathf.Max(MinimumMovementSpeed, movementSpeed) : DefaultMovementSpeed;
        }

        private static bool IsAtTarget(Vector3 current, Vector3 target)
        {
            return (current - target).sqrMagnitude <= SnapDistance * SnapDistance;
        }

        private static float GetFrameDeltaTime()
        {
            return IsPositiveFinite(Time.deltaTime) ? Time.deltaTime : FallbackDeltaTime;
        }

        private static void ValidatePath(GridPath path)
        {
            if (path == null)
            {
                throw new ArgumentNullException(nameof(path));
            }

            if (path.IsInvalid)
            {
                throw new ArgumentException($"Cannot animate invalid grid path: {path.FailureReason}", nameof(path));
            }

            if (path.Positions.Count == 0)
            {
                throw new ArgumentException("Cannot animate an empty grid path.", nameof(path));
            }
        }

        private static bool IsPositiveFinite(float value)
        {
            return !float.IsNaN(value) && !float.IsInfinity(value) && value > 0f;
        }
    }
}
