using System;
using System.Collections.Generic;
using System.Linq;
using Newtonsoft.Json.Linq;
using ReactionTactics.Grid;
using UnityCliConnector;
using UnityEditor;

namespace ReactionTactics.Editor
{
    [UnityCliTool(Name = "rt_create_default_map", Description = "Create or update the default 8x8 prototype grid map asset.", Group = "reaction-tactics")]
    public static class DefaultMapAssetCreatorTool
    {
        private const string AssetFolderPath = "Assets/ScriptableObjects";
        private const string AssetPath = AssetFolderPath + "/DefaultPrototypeMap.asset";
        private const int MapWidth = 8;
        private const int MapDepth = 8;
        private const int DefaultHeightY = 0;

        private static readonly int[,] HeightMap =
        {
            { 0, 0, 0, 0, 0, 1, 1, 1 },
            { 0, 0, 0, 0, 1, 1, 1, 1 },
            { 0, 0, 1, 1, 1, 2, 1, 1 },
            { 0, 1, 1, 2, 2, 2, 1, 0 },
            { 0, 1, 1, 2, 2, 1, 1, 0 },
            { 0, 0, 1, 1, 1, 1, 0, 0 },
            { 0, 0, 0, 1, 1, 0, 0, 0 },
            { 0, 0, 0, 0, 0, 0, 0, 0 }
        };

        private static readonly GridPosition[] LineOfSightBlockers =
        {
            new GridPosition(3, 0, 3),
            new GridPosition(4, 0, 3),
            new GridPosition(4, 0, 4)
        };

        private static readonly GridPosition[] MovementBlockers =
        {
            new GridPosition(3, 0, 3),
            new GridPosition(4, 0, 3),
            new GridPosition(4, 0, 4),
            new GridPosition(2, 0, 5)
        };

        private static readonly GridPosition[] RoughTerrainCells =
        {
            new GridPosition(1, 0, 3),
            new GridPosition(2, 0, 2),
            new GridPosition(5, 0, 5),
            new GridPosition(6, 0, 2),
            new GridPosition(5, 0, 6)
        };

        private static readonly GridPosition[] ReservedStartCells =
        {
            new GridPosition(0, 0, 0),
            new GridPosition(0, 0, 1),
            new GridPosition(1, 0, 0),
            new GridPosition(7, 0, 7),
            new GridPosition(7, 0, 6),
            new GridPosition(6, 0, 7)
        };

        public static object HandleCommand(JObject parameters)
        {
            try
            {
                EnsureAssetFolderExists();

                var overrides = CreateDefaultOverrides();
                var mainAsset = AssetDatabase.LoadMainAssetAtPath(AssetPath);
                if (mainAsset != null && !(mainAsset is GridMapDefinition))
                {
                    return new ErrorResponse($"Cannot create default map because '{AssetPath}' already contains asset type '{mainAsset.GetType().Name}'.");
                }

                var created = mainAsset == null;
                var definition = mainAsset as GridMapDefinition;
                if (definition == null)
                {
                    definition = UnityEngine.ScriptableObject.CreateInstance<GridMapDefinition>();
                    definition.name = "DefaultPrototypeMap";
                    definition.Configure(MapWidth, MapDepth, DefaultHeightY, overrides);
                    AssetDatabase.CreateAsset(definition, AssetPath);
                }
                else
                {
                    definition.name = "DefaultPrototypeMap";
                    definition.Configure(MapWidth, MapDepth, DefaultHeightY, overrides);
                }

                EditorUtility.SetDirty(definition);
                AssetDatabase.SaveAssets();
                AssetDatabase.ImportAsset(AssetPath);

                definition = AssetDatabase.LoadAssetAtPath<GridMapDefinition>(AssetPath);
                if (definition == null)
                {
                    return new ErrorResponse($"Default map asset was not available at '{AssetPath}' after save.");
                }

                var map = definition.BuildGridMap();
                if (!TryValidateDefaultMap(map, out var validationError))
                {
                    return new ErrorResponse(validationError);
                }

                var cells = map.AllCells.ToArray();
                var heightLevels = cells
                    .Select(cell => cell.Position.Y)
                    .Distinct()
                    .OrderBy(height => height)
                    .ToArray();

                return new SuccessResponse(created ? "Created default prototype map." : "Updated default prototype map.", new
                {
                    path = AssetPath,
                    created,
                    width = definition.Width,
                    depth = definition.Depth,
                    defaultHeightY = definition.DefaultHeightY,
                    cellCount = cells.Length,
                    overrideCount = definition.CellOverrides.Count,
                    heightLevels,
                    nonWalkableCount = cells.Count(cell => !cell.Walkable || cell.BlocksMovement),
                    lineOfSightBlockerCount = cells.Count(cell => cell.BlocksLineOfSight),
                    roughTerrainCount = cells.Count(cell => cell.MovementCost > GridCell.DefaultMovementCost),
                    reservedStartCells = ReservedStartCells.Select(ToHorizontalString).ToArray()
                });
            }
            catch (Exception exception)
            {
                return new ErrorResponse($"Failed to create default prototype map: {exception.Message}");
            }
        }

        private static void EnsureAssetFolderExists()
        {
            if (!AssetDatabase.IsValidFolder(AssetFolderPath))
            {
                AssetDatabase.CreateFolder("Assets", "ScriptableObjects");
            }
        }

        private static IReadOnlyList<GridMapDefinition.CellOverride> CreateDefaultOverrides()
        {
            var overrides = new List<GridMapDefinition.CellOverride>();

            for (var z = 0; z < MapDepth; z++)
            {
                for (var x = 0; x < MapWidth; x++)
                {
                    var heightY = HeightMap[z, x];
                    var walkable = !ContainsHorizontal(MovementBlockers, x, z);
                    var blocksLineOfSight = ContainsHorizontal(LineOfSightBlockers, x, z);
                    var movementCost = walkable && ContainsHorizontal(RoughTerrainCells, x, z)
                        ? 2
                        : GridCell.DefaultMovementCost;

                    if (heightY == DefaultHeightY
                        && walkable
                        && !blocksLineOfSight
                        && movementCost == GridCell.DefaultMovementCost)
                    {
                        continue;
                    }

                    overrides.Add(new GridMapDefinition.CellOverride(
                        x,
                        z,
                        heightY,
                        walkable,
                        blocksLineOfSight,
                        movementCost));
                }
            }

            return overrides;
        }

        private static bool TryValidateDefaultMap(GridMap map, out string error)
        {
            var cells = map.AllCells.ToArray();
            if (cells.Length != MapWidth * MapDepth)
            {
                error = $"Default map expected {MapWidth * MapDepth} cells but built {cells.Length}.";
                return false;
            }

            var distinctHeightCount = cells
                .Select(cell => cell.Position.Y)
                .Distinct()
                .Count();
            if (distinctHeightCount < 3)
            {
                error = "Default map must contain at least three distinct height levels.";
                return false;
            }

            var blockerCount = cells.Count(cell => !cell.Walkable || cell.BlocksMovement);
            if (blockerCount < 3)
            {
                error = "Default map must contain at least three movement blockers.";
                return false;
            }

            foreach (var startCell in ReservedStartCells)
            {
                if (!TryFindCellAtHorizontal(map, startCell.X, startCell.Z, out var cell))
                {
                    error = $"Reserved start cell {ToHorizontalString(startCell)} is missing.";
                    return false;
                }

                if (!cell.Walkable || cell.BlocksMovement)
                {
                    error = $"Reserved start cell {ToHorizontalString(startCell)} is blocked.";
                    return false;
                }

                if (!HasWalkableHorizontalNeighbor(map, cell.Position.X, cell.Position.Z))
                {
                    error = $"Reserved start cell {ToHorizontalString(startCell)} has no walkable neighbor.";
                    return false;
                }
            }

            error = string.Empty;
            return true;
        }

        private static bool HasWalkableHorizontalNeighbor(GridMap map, int x, int z)
        {
            return IsWalkableAtHorizontal(map, x + 1, z)
                || IsWalkableAtHorizontal(map, x - 1, z)
                || IsWalkableAtHorizontal(map, x, z + 1)
                || IsWalkableAtHorizontal(map, x, z - 1);
        }

        private static bool IsWalkableAtHorizontal(GridMap map, int x, int z)
        {
            return TryFindCellAtHorizontal(map, x, z, out var cell)
                && cell.Walkable
                && !cell.BlocksMovement;
        }

        private static bool TryFindCellAtHorizontal(GridMap map, int x, int z, out GridCell cell)
        {
            foreach (var candidate in map.AllCells)
            {
                if (candidate.Position.X == x && candidate.Position.Z == z)
                {
                    cell = candidate;
                    return true;
                }
            }

            cell = default;
            return false;
        }

        private static bool ContainsHorizontal(IEnumerable<GridPosition> positions, int x, int z)
        {
            foreach (var position in positions)
            {
                if (position.X == x && position.Z == z)
                {
                    return true;
                }
            }

            return false;
        }

        private static string ToHorizontalString(GridPosition position)
        {
            return $"({position.X},{position.Z})";
        }
    }
}
