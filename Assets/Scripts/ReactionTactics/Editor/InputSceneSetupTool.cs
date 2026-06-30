using System;
using Newtonsoft.Json.Linq;
using ReactionTactics.Grid;
using ReactionTactics.Input;
using ReactionTactics.Turns;
using ReactionTactics.UI;
using ReactionTactics.Units;
using UnityCliConnector;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace ReactionTactics.Editor
{
    [UnityCliTool(Name = "rt_setup_input_scene", Description = "Configure the main prototype scene with camera, picker, selection, and player command input components.", Group = "reaction-tactics")]
    public static class InputSceneSetupTool
    {
        private const string DefaultScenePath = "Assets/Scenes/MainPrototype.unity";
        private const string SystemsRootName = "Systems";
        private const string UiRootName = "UI";
        private const string MainCameraName = "Main Camera";
        private const string MainCameraTag = "MainCamera";

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

                var systemsRootCreated = false;
                var uiRootCreated = false;
                var cameraCreated = false;
                var audioListenerCreated = false;
                var cameraControllerCreated = false;
                var gridPickerCreated = false;
                var selectionControllerCreated = false;
                var commandRouterCreated = false;
                var hoverOverlayCreated = false;
                var activeActionMenuCreated = false;

                var systemsRoot = EnsureRootObject(scene, SystemsRootName, ref systemsRootCreated);
                var uiRoot = EnsureRootObject(scene, UiRootName, ref uiRootCreated);
                var camera = EnsureMainCamera(scene, ref cameraCreated, ref audioListenerCreated);
                var cameraController = EnsureComponent<TacticalCameraController>(camera.gameObject, ref cameraControllerCreated);
                var gridPicker = EnsureSceneComponent<GridPicker>(scene, systemsRoot, ref gridPickerCreated);
                var selectionController = EnsureSceneComponent<SelectionController>(scene, systemsRoot, ref selectionControllerCreated);
                var commandRouter = EnsureSceneComponent<PlayerCommandRouter>(scene, systemsRoot, ref commandRouterCreated);
                var hoverOverlay = EnsureSceneComponent<HoverGridDebugOverlay>(scene, uiRoot, ref hoverOverlayCreated);
                var activeActionMenu = EnsureSceneComponent<ActiveActionMenu>(scene, uiRoot, ref activeActionMenuCreated);
                var gridManager = FindComponentInScene<GridManager>(scene);
                var unitRegistry = FindComponentInScene<UnitRegistry>(scene);
                var combatManager = FindComponentInScene<CombatManager>(scene);

                ConfigureCameraController(cameraController, gridManager);
                ConfigureGridPicker(gridPicker, camera);
                ConfigureSelectionController(selectionController, gridPicker);
                ConfigureCommandRouter(commandRouter, selectionController, gridPicker, combatManager);
                ConfigureHoverOverlay(hoverOverlay, gridPicker, gridManager, unitRegistry, camera);
                ConfigureActiveActionMenu(activeActionMenu, selectionController, commandRouter, combatManager);

                if (gridManager != null && gridManager.RebuildMap())
                {
                    cameraController.TryFrameGrid();
                }
                else
                {
                    cameraController.ApplyView();
                }

                EditorUtility.SetDirty(camera.gameObject);
                EditorUtility.SetDirty(systemsRoot);
                EditorUtility.SetDirty(uiRoot);
                EditorSceneManager.MarkSceneDirty(scene);
                if (!EditorSceneManager.SaveScene(scene))
                {
                    return new ErrorResponse($"Failed to save scene '{scene.path}'.");
                }

                AssetDatabase.SaveAssets();

                return new SuccessResponse("Configured prototype input scene.", new
                {
                    scene = scene.path,
                    systemsRoot = GetScenePath(systemsRoot),
                    systemsRootCreated,
                    uiRoot = GetScenePath(uiRoot),
                    uiRootCreated,
                    camera = GetScenePath(camera.gameObject),
                    cameraCreated,
                    audioListenerCreated,
                    cameraController = GetScenePath(cameraController.gameObject),
                    cameraControllerCreated,
                    gridPicker = GetScenePath(gridPicker.gameObject),
                    gridPickerCreated,
                    selectionController = GetScenePath(selectionController.gameObject),
                    selectionControllerCreated,
                    commandRouter = GetScenePath(commandRouter.gameObject),
                    commandRouterCreated,
                    hoverOverlay = GetScenePath(hoverOverlay.gameObject),
                    hoverOverlayCreated,
                    activeActionMenu = GetScenePath(activeActionMenu.gameObject),
                    activeActionMenuCreated,
                    references = new
                    {
                        gridManager = gridManager != null ? GetScenePath(gridManager.gameObject) : null,
                        unitRegistry = unitRegistry != null ? GetScenePath(unitRegistry.gameObject) : null,
                        combatManager = combatManager != null ? GetScenePath(combatManager.gameObject) : null
                    }
                });
            }
            catch (Exception exception)
            {
                return new ErrorResponse($"Failed to configure prototype input scene: {exception.Message}");
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

        private static Camera EnsureMainCamera(Scene scene, ref bool cameraCreated, ref bool audioListenerCreated)
        {
            Camera camera = null;
            foreach (var root in scene.GetRootGameObjects())
            {
                camera = root.GetComponentInChildren<Camera>(includeInactive: true);
                if (camera != null)
                {
                    break;
                }
            }

            if (camera == null)
            {
                var cameraObject = new GameObject(MainCameraName);
                SceneManager.MoveGameObjectToScene(cameraObject, scene);
                camera = cameraObject.AddComponent<Camera>();
                cameraObject.transform.SetPositionAndRotation(new Vector3(-2f, 12f, -2f), Quaternion.Euler(55f, 45f, 0f));
                cameraCreated = true;
            }
            else
            {
                cameraCreated = false;
            }

            camera.gameObject.name = MainCameraName;
            if (camera.gameObject.tag != MainCameraTag)
            {
                camera.gameObject.tag = MainCameraTag;
            }

            if (FindComponentInScene<AudioListener>(scene) == null)
            {
                camera.gameObject.AddComponent<AudioListener>();
                audioListenerCreated = true;
            }
            else
            {
                audioListenerCreated = false;
            }

            EditorUtility.SetDirty(camera);
            return camera;
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
            component = gameObject.AddComponent<T>();
            EditorUtility.SetDirty(component);
            return component;
        }

        private static T EnsureSceneComponent<T>(Scene scene, GameObject preferredGameObject, ref bool created)
            where T : Component
        {
            var component = FindComponentInScene<T>(scene);
            if (component != null)
            {
                created = false;
                return component;
            }

            return EnsureComponent<T>(preferredGameObject, ref created);
        }

        private static T FindComponentInScene<T>(Scene scene)
            where T : Component
        {
            foreach (var root in scene.GetRootGameObjects())
            {
                var component = root.GetComponentInChildren<T>(includeInactive: true);
                if (component != null)
                {
                    return component;
                }
            }

            return null;
        }

        private static void ConfigureCameraController(TacticalCameraController cameraController, GridManager gridManager)
        {
            var serializedObject = new SerializedObject(cameraController);
            serializedObject.Update();
            SetObjectReference(serializedObject, "gridManager", gridManager);
            SetBool(serializedObject, "frameGridOnStart", true);
            serializedObject.ApplyModifiedPropertiesWithoutUndo();
            EditorUtility.SetDirty(cameraController);
        }

        private static void ConfigureGridPicker(GridPicker gridPicker, Camera camera)
        {
            var serializedObject = new SerializedObject(gridPicker);
            serializedObject.Update();
            SetObjectReference(serializedObject, "sourceCamera", camera);
            SetBool(serializedObject, "ignorePointerOverUi", true);
            SetBool(serializedObject, "logClickedCells", true);
            SetBool(serializedObject, "logClickedUnits", true);
            serializedObject.ApplyModifiedPropertiesWithoutUndo();
            EditorUtility.SetDirty(gridPicker);
        }

        private static void ConfigureSelectionController(SelectionController selectionController, GridPicker gridPicker)
        {
            var serializedObject = new SerializedObject(selectionController);
            serializedObject.Update();
            SetObjectReference(serializedObject, "gridPicker", gridPicker);
            serializedObject.ApplyModifiedPropertiesWithoutUndo();
            EditorUtility.SetDirty(selectionController);
        }

        private static void ConfigureCommandRouter(
            PlayerCommandRouter commandRouter,
            SelectionController selectionController,
            GridPicker gridPicker,
            CombatManager combatManager)
        {
            var serializedObject = new SerializedObject(commandRouter);
            serializedObject.Update();
            SetObjectReference(serializedObject, "selectionController", selectionController);
            SetObjectReference(serializedObject, "gridPicker", gridPicker);
            SetObjectReference(serializedObject, "combatManager", combatManager);
            SetBool(serializedObject, "keyboardShortcutsEnabled", true);
            SetBool(serializedObject, "logRoutedCommands", false);
            serializedObject.ApplyModifiedPropertiesWithoutUndo();
            EditorUtility.SetDirty(commandRouter);
        }

        private static void ConfigureHoverOverlay(
            HoverGridDebugOverlay hoverOverlay,
            GridPicker gridPicker,
            GridManager gridManager,
            UnitRegistry unitRegistry,
            Camera camera)
        {
            var serializedObject = new SerializedObject(hoverOverlay);
            serializedObject.Update();
            SetObjectReference(serializedObject, "gridPicker", gridPicker);
            SetObjectReference(serializedObject, "gridManager", gridManager);
            SetObjectReference(serializedObject, "unitRegistry", unitRegistry);
            SetObjectReference(serializedObject, "sourceCamera", camera);
            serializedObject.ApplyModifiedPropertiesWithoutUndo();
            EditorUtility.SetDirty(hoverOverlay);
        }

        private static void ConfigureActiveActionMenu(
            ActiveActionMenu activeActionMenu,
            SelectionController selectionController,
            PlayerCommandRouter commandRouter,
            CombatManager combatManager)
        {
            var serializedObject = new SerializedObject(activeActionMenu);
            serializedObject.Update();
            SetObjectReference(serializedObject, "selectionController", selectionController);
            SetObjectReference(serializedObject, "commandRouter", commandRouter);
            SetObjectReference(serializedObject, "combatManager", combatManager);
            SetBool(serializedObject, "visible", true);
            serializedObject.ApplyModifiedPropertiesWithoutUndo();
            EditorUtility.SetDirty(activeActionMenu);
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
