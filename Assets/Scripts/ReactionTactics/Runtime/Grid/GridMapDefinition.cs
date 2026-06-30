using System;
using System.Collections.Generic;
using UnityEngine;

namespace ReactionTactics.Grid
{
    /// <summary>
    /// Serialized prototype terrain definition that can build the pure runtime grid map used by gameplay systems.
    /// </summary>
    [CreateAssetMenu(fileName = "GridMapDefinition", menuName = "Reaction Tactics/Grid Map Definition")]
    public sealed class GridMapDefinition : ScriptableObject
    {
        private const int MinimumDimension = 1;

        [SerializeField]
        [Min(MinimumDimension)]
        private int width = 8;

        [SerializeField]
        [Min(MinimumDimension)]
        private int depth = 8;

        [SerializeField]
        private int defaultHeightY;

        [SerializeField]
        private List<CellOverride> cellOverrides = new List<CellOverride>();

        public int Width
        {
            get { return width; }
        }

        public int Depth
        {
            get { return depth; }
        }

        public int DefaultHeightY
        {
            get { return defaultHeightY; }
        }

        public IReadOnlyList<CellOverride> CellOverrides
        {
            get { return cellOverrides; }
        }

        /// <summary>
        /// Configures this definition in one step. Intended for deterministic editor tooling and tests.
        /// </summary>
        public void Configure(int width, int depth, int defaultHeightY, IEnumerable<CellOverride> overrides)
        {
            if (width < MinimumDimension)
            {
                throw new ArgumentOutOfRangeException(nameof(width), width, "Map width must be at least 1.");
            }

            if (depth < MinimumDimension)
            {
                throw new ArgumentOutOfRangeException(nameof(depth), depth, "Map depth must be at least 1.");
            }

            this.width = width;
            this.depth = depth;
            this.defaultHeightY = defaultHeightY;
            cellOverrides = overrides == null
                ? new List<CellOverride>()
                : new List<CellOverride>(overrides);
        }

        /// <summary>
        /// Builds an in-memory map in deterministic row-major order: z first, then x.
        /// Each horizontal coordinate produces exactly one terrain cell at its configured height.
        /// </summary>
        public GridMap BuildGridMap()
        {
            ValidateDimensions();
            var overridesByHorizontalPosition = BuildOverrideLookup();
            var cells = new List<GridCell>(width * depth);

            for (var z = 0; z < depth; z++)
            {
                for (var x = 0; x < width; x++)
                {
                    var horizontalPosition = new GridPosition(x, 0, z);
                    if (overridesByHorizontalPosition.TryGetValue(horizontalPosition, out var cellOverride))
                    {
                        cells.Add(cellOverride.ToGridCell());
                    }
                    else
                    {
                        var position = new GridPosition(x, defaultHeightY, z);
                        cells.Add(new GridCell(position));
                    }
                }
            }

            return new GridMap(cells);
        }

        private void OnValidate()
        {
            width = Math.Max(MinimumDimension, width);
            depth = Math.Max(MinimumDimension, depth);

            if (cellOverrides == null)
            {
                cellOverrides = new List<CellOverride>();
                return;
            }

            foreach (var cellOverride in cellOverrides)
            {
                cellOverride?.ClampSerializedValues();
            }
        }

        private void ValidateDimensions()
        {
            if (width < MinimumDimension)
            {
                throw new InvalidOperationException($"Grid map definition '{name}' has invalid width {width}. Width must be at least 1.");
            }

            if (depth < MinimumDimension)
            {
                throw new InvalidOperationException($"Grid map definition '{name}' has invalid depth {depth}. Depth must be at least 1.");
            }
        }

        private Dictionary<GridPosition, CellOverride> BuildOverrideLookup()
        {
            var overridesByHorizontalPosition = new Dictionary<GridPosition, CellOverride>();
            if (cellOverrides == null)
            {
                return overridesByHorizontalPosition;
            }

            foreach (var cellOverride in cellOverrides)
            {
                if (cellOverride == null)
                {
                    throw new InvalidOperationException($"Grid map definition '{name}' contains a null cell override.");
                }

                if (cellOverride.X < 0 || cellOverride.X >= width || cellOverride.Z < 0 || cellOverride.Z >= depth)
                {
                    throw new InvalidOperationException(
                        $"Cell override ({cellOverride.X},{cellOverride.Z}) is outside map bounds width={width}, depth={depth}.");
                }

                if (cellOverride.MovementCost < GridCell.DefaultMovementCost)
                {
                    throw new InvalidOperationException(
                        $"Cell override ({cellOverride.X},{cellOverride.Z}) has invalid movement cost {cellOverride.MovementCost}. Movement cost must be at least {GridCell.DefaultMovementCost}.");
                }

                var horizontalPosition = new GridPosition(cellOverride.X, 0, cellOverride.Z);
                if (overridesByHorizontalPosition.ContainsKey(horizontalPosition))
                {
                    throw new InvalidOperationException(
                        $"Grid map definition '{name}' contains duplicate overrides for horizontal cell ({cellOverride.X},{cellOverride.Z}).");
                }

                overridesByHorizontalPosition.Add(horizontalPosition, cellOverride);
            }

            return overridesByHorizontalPosition;
        }

        /// <summary>
        /// Serialized replacement data for one horizontal x/z map cell.
        /// </summary>
        [Serializable]
        public sealed class CellOverride
        {
            [SerializeField]
            private int x;

            [SerializeField]
            private int z;

            [SerializeField]
            private int heightY;

            [SerializeField]
            private bool walkable = true;

            [SerializeField]
            private bool blocksLineOfSight;

            [SerializeField]
            [Min(GridCell.DefaultMovementCost)]
            private int movementCost = GridCell.DefaultMovementCost;

            public CellOverride(int x, int z, int heightY, bool walkable, bool blocksLineOfSight, int movementCost)
            {
                if (movementCost < GridCell.DefaultMovementCost)
                {
                    throw new ArgumentOutOfRangeException(
                        nameof(movementCost),
                        movementCost,
                        $"Movement cost must be at least {GridCell.DefaultMovementCost}.");
                }

                this.x = x;
                this.z = z;
                this.heightY = heightY;
                this.walkable = walkable;
                this.blocksLineOfSight = blocksLineOfSight;
                this.movementCost = movementCost;
            }

            public int X
            {
                get { return x; }
            }

            public int Z
            {
                get { return z; }
            }

            public int HeightY
            {
                get { return heightY; }
            }

            public bool Walkable
            {
                get { return walkable; }
            }

            public bool BlocksLineOfSight
            {
                get { return blocksLineOfSight; }
            }

            public int MovementCost
            {
                get { return movementCost; }
            }

            internal GridCell ToGridCell()
            {
                var position = new GridPosition(x, heightY, z);
                return new GridCell(
                    position,
                    walkable,
                    blocksMovement: !walkable,
                    blocksLineOfSight,
                    movementCost,
                    displayHeight: heightY);
            }

            internal void ClampSerializedValues()
            {
                movementCost = Math.Max(GridCell.DefaultMovementCost, movementCost);
            }
        }
    }
}
