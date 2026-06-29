using System;
using System.Collections.Generic;
using Newtonsoft.Json.Linq;
using UnityCliConnector;
using UnityEditor;

namespace ReactionTactics.Editor
{
    [UnityCliTool(Name = "rt_find_assets", Description = "Find Unity assets by AssetDatabase filter and return compact asset paths.", Group = "reaction-tactics")]
    public static class AssetFinderTool
    {
        private const int DefaultLimit = 100;

        public sealed class Parameters
        {
            [ToolParameter("AssetDatabase filter string, for example t:Scene, t:Material, t:Prefab, t:MonoScript, or name fragments.", Required = true)]
            public string Filter { get; set; }

            [ToolParameter("Maximum number of asset paths to return. Use 0 for no limit.", DefaultValue = "100")]
            public int Limit { get; set; }
        }

        public static object HandleCommand(JObject parameters)
        {
            var toolParams = new ToolParams(parameters ?? new JObject());
            var filter = toolParams.Get("filter", string.Empty).Trim();
            var limit = toolParams.GetInt("limit", DefaultLimit) ?? DefaultLimit;

            if (string.IsNullOrWhiteSpace(filter))
            {
                return new ErrorResponse("Parameter 'filter' is required.");
            }

            if (limit < 0)
            {
                return new ErrorResponse("Parameter 'limit' must be greater than or equal to 0.");
            }

            var guids = AssetDatabase.FindAssets(filter);
            var paths = new List<string>(guids.Length);

            foreach (var guid in guids)
            {
                var path = AssetDatabase.GUIDToAssetPath(guid);
                if (!string.IsNullOrEmpty(path))
                {
                    paths.Add(path);
                }
            }

            paths.Sort(StringComparer.Ordinal);

            var totalCount = paths.Count;
            if (limit > 0 && paths.Count > limit)
            {
                paths = paths.GetRange(0, limit);
            }

            return new SuccessResponse($"Found {totalCount} assets matching '{filter}'", new
            {
                filter,
                limit,
                totalCount,
                returnedCount = paths.Count,
                paths
            });
        }
    }
}
