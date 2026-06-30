using System;
using Newtonsoft.Json.Linq;
using ReactionTactics.Grid;
using UnityCliConnector;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace ReactionTactics.Editor
{
    [UnityCliTool(Name = "rt_setup_grid_scene", Description = "Configure the main prototype scene with grid manager, terrain view, map, and terrain materials.", Group = "reaction-tactics")]
    public static class GridSceneSetupTool
    {
        private const string DefaultScenePath = "Assets/Scenes/MainPrototype.unity";
        private const string SystemsRootName = "Systems";
        private const string GridRootName = "Grid";
        private const string DefaultMapPath = "Assets/ScriptableObjects/DefaultPrototypeMap.asset";
        private const string WalkableMaterialPath = "Assets/Materials/PrototypeGridWalkable.mat";
        private const string BlockedMaterialPath = "Assets/Materials/PrototypeGridBlocked.mat";
        private const string LineOfSightBlockerMaterialPath = "Assets/Materials/PrototypeGridLineOfSightBlocker.mat";
        private const string HighlightMaterialPath = "Assets/Materials/PrototypeGridHighlight.mat";
        private const string DangerMaterialPath = "Assets/Materials/PrototypeGridDanger.mat";
        private const string SafeMaterialPath = "Assets/Materials/PrototypeGridSafe.mat";

        public sealed class Parameters
        {
            [ToolParameter("Scene asset path to configure.", DefaultValue = DefaultScenePath)]
            public string ScenePath { get; set; }
        }

        public static object HandleCommand(JObject parameters)
        {
            try
            {
                var toolParams = new ToolParams(parameters ?? new JObject());
                var scenePath = toolParams.Get("scenePath", toolParams.Get("scene_path", DefaultScenePath)).Trim();
                if (string.IsNullOrWhiteSpace(scenePath))
                {
                    return new ErrorResponse("Parameter 'scenePath' cannot be empty.");
                }

                var scene = OpenOrUseScene(scenePath);
                var mapDefinition = LoadRequiredAsset<GridMapDefinition>(DefaultMapPath, "default prototype map");
                var walkableMaterial = LoadRequiredAsset<Material>(WalkableMaterialPath, "walkable grid material");
                var blockedMaterial = LoadRequiredAsset<Material>(BlockedMaterialPath, "blocked grid material");
                var lineOfSightBlockerMaterial = LoadRequiredAsset<Material>(LineOfSightBlockerMaterialPath, "line-of-sight blocker grid material");
                var highlightMaterial = LoadRequiredAsset<Material>(HighlightMaterialPath, "highlight grid material");
                var dangerMaterial = LoadRequiredAsset<Material>(DangerMaterialPath, "danger grid material");
                var safeMaterial = LoadRequiredAsset<Material>(SafeMaterialPath, "safe grid material");

                var systemsRootCreated = false;
                var gridRootCreated = false;
                var gridManagerCreated = false;
                var terrainViewCreated = false;

                var systemsRoot = EnsureRootObject(scene, SystemsRootName, ref systemsRootCreated);
                var gridRoot = EnsureRootObject(scene, GridRootName, ref gridRootCreated);
                var gridManager = EnsureComponent<GridManager>(systemsRoot, ref gridManagerCreated);
                var terrainView = EnsureComponent<GridTerrainView>(gridRoot, ref terrainViewCreated);

                ConfigureGridManager(gridManager, mapDefinition);
                ConfigureTerrainView(
                    terrainView,
                    gridManager,
                    gridRoot.transform,
                    walkableMaterial,
                    blockedMaterial,
                    lineOfSightBlockerMaterial,
                    highlightMaterial,
                    dangerMaterial,
                    safeMaterial);

                if (!gridManager.RebuildMap())
                {
                    return new ErrorResponse($"Configured {nameof(GridManager)} but failed to build the map from '{DefaultMapPath}'. Check the Unity console for details.");
                }

                var cellCount = 0;
                foreach (var _ in gridManager.CurrentMap.AllCells)
                {
                    cellCount++;
                }

                EditorSceneManager.MarkSceneDirty(scene);
                if (!EditorSceneManager.SaveScene(scene))
                {
                    return new ErrorResponse($"Failed to save scene '{scene.path}'.");
                }

                AssetDatabase.SaveAssets();

                return new SuccessResponse("Configured prototype grid scene.", new
                {
                    scene = scene.path,
                    systemsRoot = GetScenePath(systemsRoot),
                    gridRoot = GetScenePath(gridRoot),
                    systemsRootCreated,
                    gridRootCreated,
                    gridManager = GetScenePath(gridManager.gameObject),
                    gridManagerCreated,
                    terrainView = GetScenePath(terrainView.gameObject),
                    terrainViewCreated,
                    map = DefaultMapPath,
                    cellCount,
                    materials = new
                    {
                        walkable = WalkableMaterialPath,
                        blocked = BlockedMaterialPath,
                        lineOfSightBlocker = LineOfSightBlockerMaterialPath,
                        highlight = HighlightMaterialPath,
                        danger = DangerMaterialPath,
                        safe = SafeMaterialPath
                    }
                });
            }
            catch (Exception exception)
            {
                return new ErrorResponse($"Failed to configure prototype grid scene: {exception.Message}");
            }
        }

        private static Scene OpenOrUseScene(string scenePath)
        {
            if (AssetDatabase.LoadAssetAtPath<SceneAsset>(scenePath) == null)
            {
                throw new InvalidOperationException($"Scene asset '{scenePath}' was not found.");
            }

            var activeScene = EditorSceneManager.GetActiveScene();
            if (activeScene.IsValid() && string.Equals(activeScene.path, scenePath, StringComparison.Ordinal))
            {
                return activeScene;
            }

            if (activeScene.IsValid() && activeScene.isDirty)
            {
                throw new InvalidOperationException(
                    $"Active scene '{activeScene.path}' has unsaved changes. Save it before opening '{scenePath}'.");
            }

            return EditorSceneManager.OpenScene(scenePath, OpenSceneMode.Single);
        }

        private static T LoadRequiredAsset<T>(string path, string description)
            where T : UnityEngine.Object
        {
            var asset = AssetDatabase.LoadAssetAtPath<T>(path);
            if (asset == null)
            {
                throw new InvalidOperationException($"Required {description} asset was not found at '{path}'.");
            }

            return asset;
        }

        private static GameObject EnsureRootObject(Scene scene, string rootName, ref bool created)
        {
            foreach (var root in scene.GetRootGameObjects())
            {
                if (string.Equals(root.name, rootName, StringComparison.Ordinal))
                {
                    created = false;
                    return root;
                }
            }

            var gameObject = new GameObject(rootName);
            SceneManager.MoveGameObjectToScene(gameObject, scene);
            created = true;
            return gameObject;
        }

        private static T EnsureComponent<T>(GameObject gameObject, ref bool created)
            where T : Component
        {
            var component = gameObject.GetComponent<T>();
            if (component != null)
            {
                created = false;
                return component;
            }

            created = true;
            return gameObject.AddComponent<T>();
        }

        private static void ConfigureGridManager(GridManager gridManager, GridMapDefinition mapDefinition)
        {
            var serializedObject = new SerializedObject(gridManager);
            serializedObject.Update();
            SetObjectReference(serializedObject, "mapDefinition", mapDefinition);
            serializedObject.ApplyModifiedPropertiesWithoutUndo();
            EditorUtility.SetDirty(gridManager);
        }

        private static void ConfigureTerrainView(
            GridTerrainView terrainView,
            GridManager gridManager,
            Transform tileParent,
            Material walkableMaterial,
            Material blockedMaterial,
            Material lineOfSightBlockerMaterial,
            Material highlightMaterial,
            Material dangerMaterial,
            Material safeMaterial)
        {
            var serializedObject = new SerializedObject(terrainView);
            serializedObject.Update();
            SetObjectReference(serializedObject, "gridManager", gridManager);
            SetObjectReference(serializedObject, "tileParent", tileParent);
            SetBool(serializedObject, "generateOnStart", true);
            SetBool(serializedObject, "clearExistingTiles", true);
            SetObjectReference(serializedObject, "walkableMaterial", walkableMaterial);
            SetObjectReference(serializedObject, "blockedMaterial", blockedMaterial);
            SetObjectReference(serializedObject, "lineOfSightBlockerMaterial", lineOfSightBlockerMaterial);
            SetObjectReference(serializedObject, "highlightMaterial", highlightMaterial);
            SetObjectReference(serializedObject, "dangerMaterial", dangerMaterial);
            SetObjectReference(serializedObject, "safeMaterial", safeMaterial);
            serializedObject.ApplyModifiedPropertiesWithoutUndo();
            EditorUtility.SetDirty(terrainView);
        }

        private static void SetObjectReference(SerializedObject serializedObject, string propertyName, UnityEngine.Object value)
        {
            var property = serializedObject.FindProperty(propertyName);
            if (property == null)
            {
                throw new InvalidOperationException($"Serialized property '{propertyName}' was not found on '{serializedObject.targetObject.name}'.");
            }

            property.objectReferenceValue = value;
        }

        private static void SetBool(SerializedObject serializedObject, string propertyName, bool value)
        {
            var property = serializedObject.FindProperty(propertyName);
            if (property == null)
            {
                throw new InvalidOperationException($"Serialized property '{propertyName}' was not found on '{serializedObject.targetObject.name}'.");
            }

            property.boolValue = value;
        }

        private static string GetScenePath(GameObject gameObject)
        {
            var path = gameObject.name;
            var current = gameObject.transform.parent;
            while (current != null)
            {
                path = current.name + "/" + path;
                current = current.parent;
            }

            return path;
        }
    }
}
