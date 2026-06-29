using System;
using System.Collections.Generic;
using Newtonsoft.Json.Linq;
using UnityCliConnector;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace ReactionTactics.Editor
{
    [UnityCliTool(Name = "rt_hierarchy", Description = "Dump the active scene hierarchy as compact JSON-like data.", Group = "reaction-tactics")]
    public static class HierarchyDumpTool
    {
        public sealed class Parameters
        {
            [ToolParameter("Include inactive scene objects in the hierarchy dump.", DefaultValue = "false")]
            public bool IncludeInactive { get; set; }
        }

        public static object HandleCommand(JObject parameters)
        {
            var toolParams = new ToolParams(parameters ?? new JObject());
            var includeInactive = toolParams.GetBool("includeInactive", toolParams.GetBool("include_inactive"));
            var scene = EditorSceneManager.GetActiveScene();
            var entries = new List<object>();

            foreach (var root in scene.GetRootGameObjects())
            {
                AddObject(root, depth: 0, path: root.name, includeInactive, entries);
            }

            return new SuccessResponse("Active scene hierarchy", new
            {
                scene = new
                {
                    name = scene.name,
                    path = scene.path,
                    isLoaded = scene.isLoaded,
                    rootCount = scene.rootCount
                },
                includeInactive,
                objects = entries
            });
        }

        private static void AddObject(GameObject gameObject, int depth, string path, bool includeInactive, List<object> entries)
        {
            if (!includeInactive && !gameObject.activeInHierarchy)
            {
                return;
            }

            entries.Add(new
            {
                name = gameObject.name,
                path,
                depth,
                activeSelf = gameObject.activeSelf,
                activeInHierarchy = gameObject.activeInHierarchy,
                position = ToCompactPosition(gameObject.transform.position),
                components = GetComponentNames(gameObject)
            });

            var transform = gameObject.transform;
            for (var index = 0; index < transform.childCount; index++)
            {
                var child = transform.GetChild(index).gameObject;
                AddObject(child, depth + 1, $"{path}/{child.name}", includeInactive, entries);
            }
        }

        private static object ToCompactPosition(Vector3 position)
        {
            return new
            {
                x = Math.Round(position.x, 3),
                y = Math.Round(position.y, 3),
                z = Math.Round(position.z, 3)
            };
        }

        private static IReadOnlyList<string> GetComponentNames(GameObject gameObject)
        {
            var components = gameObject.GetComponents<Component>();
            var names = new List<string>(components.Length);

            foreach (var component in components)
            {
                names.Add(component == null ? "MissingScript" : component.GetType().Name);
            }

            return names;
        }
    }
}
