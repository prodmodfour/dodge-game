using System;
using UnityEngine;

namespace ReactionTactics.Grid
{
    /// <summary>
    /// Converts between discrete grid coordinates and Unity world-space positions.
    /// X/Z use cell centers while Y represents the terrain height level.
    /// </summary>
    [Serializable]
    public struct GridMetrics
    {
        public const float DefaultCellSize = 1f;
        public const float DefaultHeightStep = 1f;

        [SerializeField]
        private float cellSize;

        [SerializeField]
        private float heightStep;

        [SerializeField]
        private Vector3 origin;

        public GridMetrics(float cellSize, float heightStep)
            : this(cellSize, heightStep, Vector3.zero)
        {
        }

        public GridMetrics(float cellSize, float heightStep, Vector3 origin)
        {
            ValidatePositiveFinite(cellSize, nameof(cellSize));
            ValidatePositiveFinite(heightStep, nameof(heightStep));
            ValidateFinite(origin, nameof(origin));

            this.cellSize = cellSize;
            this.heightStep = heightStep;
            this.origin = origin;
        }

        public static GridMetrics Default
        {
            get { return new GridMetrics(DefaultCellSize, DefaultHeightStep, Vector3.zero); }
        }

        public float CellSize
        {
            get { return IsPositiveFinite(cellSize) ? cellSize : DefaultCellSize; }
        }

        public float HeightStep
        {
            get { return IsPositiveFinite(heightStep) ? heightStep : DefaultHeightStep; }
        }

        public Vector3 Origin
        {
            get { return IsFinite(origin) ? origin : Vector3.zero; }
        }

        public Vector3 GridToWorldCenter(GridPosition position)
        {
            var effectiveOrigin = Origin;
            var effectiveCellSize = CellSize;
            var effectiveHeightStep = HeightStep;

            return effectiveOrigin + new Vector3(
                (position.X + 0.5f) * effectiveCellSize,
                position.Y * effectiveHeightStep,
                (position.Z + 0.5f) * effectiveCellSize);
        }

        public GridPosition WorldToApproxGrid(Vector3 worldPosition)
        {
            var localPosition = worldPosition - Origin;
            return new GridPosition(
                Mathf.FloorToInt(localPosition.x / CellSize),
                Mathf.RoundToInt(localPosition.y / HeightStep),
                Mathf.FloorToInt(localPosition.z / CellSize));
        }

        private static void ValidatePositiveFinite(float value, string paramName)
        {
            if (!IsPositiveFinite(value))
            {
                throw new ArgumentOutOfRangeException(
                    paramName,
                    value,
                    "Grid metric values must be positive finite numbers.");
            }
        }

        private static void ValidateFinite(Vector3 value, string paramName)
        {
            if (!IsFinite(value))
            {
                throw new ArgumentOutOfRangeException(
                    paramName,
                    value,
                    "Grid metric origin must contain only finite values.");
            }
        }

        private static bool IsPositiveFinite(float value)
        {
            return !float.IsNaN(value) && !float.IsInfinity(value) && value > 0f;
        }

        private static bool IsFinite(Vector3 value)
        {
            return !float.IsNaN(value.x) && !float.IsInfinity(value.x)
                && !float.IsNaN(value.y) && !float.IsInfinity(value.y)
                && !float.IsNaN(value.z) && !float.IsInfinity(value.z);
        }
    }
}
