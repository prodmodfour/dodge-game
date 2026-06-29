using System;

namespace ReactionTactics.Grid
{
    /// <summary>
    /// Immutable terrain cell data independent of visual tile GameObjects and runtime occupancy.
    /// </summary>
    public readonly struct GridCell : IEquatable<GridCell>
    {
        public const int DefaultMovementCost = 1;

        public GridCell(GridPosition position)
            : this(
                position,
                walkable: true,
                blocksMovement: false,
                blocksLineOfSight: false,
                movementCost: DefaultMovementCost,
                displayHeight: position.Y)
        {
        }

        public GridCell(
            GridPosition position,
            bool walkable = true,
            bool blocksMovement = false,
            bool blocksLineOfSight = false,
            int movementCost = DefaultMovementCost)
            : this(position, walkable, blocksMovement, blocksLineOfSight, movementCost, position.Y)
        {
        }

        public GridCell(
            GridPosition position,
            bool walkable,
            bool blocksMovement,
            bool blocksLineOfSight,
            int movementCost,
            float displayHeight)
        {
            if (movementCost < 1)
            {
                throw new ArgumentOutOfRangeException(
                    nameof(movementCost),
                    movementCost,
                    "Movement cost must be at least 1.");
            }

            if (float.IsNaN(displayHeight) || float.IsInfinity(displayHeight))
            {
                throw new ArgumentOutOfRangeException(
                    nameof(displayHeight),
                    displayHeight,
                    "Display height must be a finite value.");
            }

            Position = position;
            Walkable = walkable;
            BlocksMovement = blocksMovement;
            BlocksLineOfSight = blocksLineOfSight;
            MovementCost = movementCost;
            DisplayHeight = displayHeight;
        }

        public GridPosition Position { get; }

        public bool Walkable { get; }

        public bool BlocksMovement { get; }

        public bool BlocksLineOfSight { get; }

        public int MovementCost { get; }

        public float DisplayHeight { get; }

        public GridCell Clone()
        {
            return new GridCell(Position, Walkable, BlocksMovement, BlocksLineOfSight, MovementCost, DisplayHeight);
        }

        public GridCell CopyWith(
            GridPosition? position = null,
            bool? walkable = null,
            bool? blocksMovement = null,
            bool? blocksLineOfSight = null,
            int? movementCost = null,
            float? displayHeight = null)
        {
            return new GridCell(
                position ?? Position,
                walkable ?? Walkable,
                blocksMovement ?? BlocksMovement,
                blocksLineOfSight ?? BlocksLineOfSight,
                movementCost ?? MovementCost,
                displayHeight ?? DisplayHeight);
        }

        public GridCell WithPosition(GridPosition position)
        {
            return CopyWith(position: position);
        }

        public GridCell WithWalkable(bool walkable)
        {
            return CopyWith(walkable: walkable);
        }

        public GridCell WithBlocksMovement(bool blocksMovement)
        {
            return CopyWith(blocksMovement: blocksMovement);
        }

        public GridCell WithBlocksLineOfSight(bool blocksLineOfSight)
        {
            return CopyWith(blocksLineOfSight: blocksLineOfSight);
        }

        public GridCell WithMovementCost(int movementCost)
        {
            return CopyWith(movementCost: movementCost);
        }

        public GridCell WithDisplayHeight(float displayHeight)
        {
            return CopyWith(displayHeight: displayHeight);
        }

        public bool Equals(GridCell other)
        {
            return Position == other.Position
                && Walkable == other.Walkable
                && BlocksMovement == other.BlocksMovement
                && BlocksLineOfSight == other.BlocksLineOfSight
                && MovementCost == other.MovementCost
                && DisplayHeight.Equals(other.DisplayHeight);
        }

        public override bool Equals(object obj)
        {
            return obj is GridCell other && Equals(other);
        }

        public override int GetHashCode()
        {
            unchecked
            {
                var hash = 17;
                hash = (hash * 31) + Position.GetHashCode();
                hash = (hash * 31) + Walkable.GetHashCode();
                hash = (hash * 31) + BlocksMovement.GetHashCode();
                hash = (hash * 31) + BlocksLineOfSight.GetHashCode();
                hash = (hash * 31) + MovementCost;
                hash = (hash * 31) + DisplayHeight.GetHashCode();
                return hash;
            }
        }

        public override string ToString()
        {
            return $"GridCell {Position} walkable={Walkable} blocksMovement={BlocksMovement} blocksLineOfSight={BlocksLineOfSight} movementCost={MovementCost} displayHeight={DisplayHeight}";
        }

        public static bool operator ==(GridCell left, GridCell right)
        {
            return left.Equals(right);
        }

        public static bool operator !=(GridCell left, GridCell right)
        {
            return !left.Equals(right);
        }
    }
}
