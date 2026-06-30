using System;
using System.Collections.Generic;
using Newtonsoft.Json.Linq;
using ReactionTactics.AI;
using ReactionTactics.Grid;
using ReactionTactics.Input;
using ReactionTactics.Scenarios;
using ReactionTactics.Turns;
using ReactionTactics.Units;
using UnityCliConnector;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace ReactionTactics.Editor
{
    [UnityCliTool(Name = "rt_setup_scenario_scene", Description = "Configure the main prototype scene to spawn units from the default scenario asset instead of manual scene units.", Group = "reaction-tactics")]
    public static class ScenarioSceneSetupTool
    {
        private const string DefaultScenePath = "Assets/Scenes/MainPrototype.unity";
        private const string DefaultScenarioPath = "Assets/ScriptableObjects/Scenarios/DefaultSkirmish.asset";
        private const string PrototypeUnitPrefabPath = "Assets/Prefabs/PrototypeUnit.prefab";
        private const string SystemsRootName = "Systems";
        private const string UnitsRootName = "Units";
        private const string ScenarioRootName = "Scenario";
        private const string ScenarioUnitsRootName = "Scenario Units";
        private const string LegacyInitialUnitsRootName = "Initial Units";
        private const int FirstUnitId = 1;

        public sealed class Parameters
        {
            [ToolParameter("Scene asset path to configure.", DefaultValue = DefaultScenePath)]
            public string ScenePath { get; set; }

            [ToolParameter("Scenario asset path to assign.", DefaultValue = DefaultScenarioPath)]
            public string ScenarioPath { get; set; }
        }

        public static object HandleCommand(JObject parameters)
        {
            try
            {
                var toolParams = new ToolParams(parameters ?? new JObject());
                var scenePath = toolParams.Get("scenePath", toolParams.Get("scene_path", DefaultScenePath)).Trim();
                var scenarioPath = toolParams.Get("scenarioPath", toolParams.Get("scenario_path", DefaultScenarioPath)).Trim();
                if (string.IsNullOrWhiteSpace(scenePath))
                {
                    return new ErrorResponse("Parameter 'scenePath' cannot be empty.");
                }

                if (string.IsNullOrWhiteSpace(scenarioPath))
                {
                    return new ErrorResponse("Parameter 'scenarioPath' cannot be empty.");
                }

                var scene = OpenOrUseScene(scenePath);
                var scenario = LoadRequiredAsset<ScenarioDefinition>(scenarioPath, "scenario definition");
                var scenarioValidation = scenario.ValidateScenario();
                if (scenarioValidation.IsFailure)
                {
                    return new ErrorResponse($"Scenario asset '{scenarioPath}' is invalid: {scenarioValidation.ErrorMessage}");
                }

                var unitPrefab = LoadRequiredUnitPrefab(PrototypeUnitPrefabPath);

                var systemsRootCreated = false;
                var unitsRootCreated = false;
                var scenarioRootCreated = false;
                var scenarioUnitsRootCreated = false;
                var registryCreated = false;
                var spawnerCreated = false;
                var scenarioLoaderCreated = false;

                var systemsRoot = EnsureRootObject(scene, SystemsRootName, ref systemsRootCreated);
                var unitsRoot = EnsureRootObject(scene, UnitsRootName, ref unitsRootCreated);
                var scenarioRoot = EnsureRootObject(scene, ScenarioRootName, ref scenarioRootCreated);
                var scenarioUnitsRoot = EnsureChild(unitsRoot.transform, ScenarioUnitsRootName, ref scenarioUnitsRootCreated);

                var gridManager = FindComponentInScene<GridManager>(scene);
                if (gridManager == null)
                {
                    return new ErrorResponse($"No {nameof(GridManager)} was found in scene '{scene.path}'. Run rt_setup_grid_scene before rt_setup_scenario_scene.");
                }

                ConfigureGridManager(gridManager, scenario.MapDefinition);
                if (!gridManager.RebuildMap())
                {
                    return new ErrorResponse($"{nameof(GridManager)} on '{GetScenePath(gridManager.gameObject)}' could not build the scenario map. Run rt_create_default_scenario and check the Unity console.");
                }

                var registry = EnsureSceneComponent<UnitRegistry>(scene, unitsRoot, ref registryCreated);
                var spawner = EnsureSceneComponent<UnitSpawner>(scene, unitsRoot, ref spawnerCreated);
                var inputRouter = FindComponentInScene<PlayerCommandRouter>(scene);
                var combatManager = FindComponentInScene<CombatManager>(scene);
                var eventBus = FindComponentInScene<CombatEventBus>(scene);
                var aiController = FindComponentInScene<AiController>(scene);
                var scenarioLoader = EnsureSingleScenarioLoader(scene, scenarioRoot, ref scenarioLoaderCreated, out var duplicateScenarioLoadersRemoved);

                ConfigureSpawner(spawner, unitPrefab, registry, gridManager, scenarioUnitsRoot);
                ConfigureScenarioLoader(scenarioLoader, scenario, gridManager, spawner, combatManager);
                if (combatManager != null)
                {
                    ConfigureCombatManager(combatManager, registry, gridManager, inputRouter, eventBus, aiController);
                }

                var removedManualUnits = RemoveSceneTacticalUnits(scene);
                registry.Clear();
                spawner.ResetUnitIdSequence(FirstUnitId);
                var legacyInitialUnitsRootRemoved = RemoveLegacyInitialUnitsRoot(unitsRoot.transform);
                var sceneTacticalUnitCount = CountComponentsInScene<TacticalUnit>(scene);
                if (sceneTacticalUnitCount != 0)
                {
                    return new ErrorResponse($"Expected no manually placed TacticalUnit objects after scenario setup, but found {sceneTacticalUnitCount}.");
                }

                EditorUtility.SetDirty(systemsRoot);
                EditorUtility.SetDirty(unitsRoot);
                EditorUtility.SetDirty(scenarioRoot);
                EditorUtility.SetDirty(registry);
                EditorUtility.SetDirty(spawner);
                EditorUtility.SetDirty(scenarioLoader);
                if (combatManager != null)
                {
                    EditorUtility.SetDirty(combatManager);
                }

                EditorSceneManager.MarkSceneDirty(scene);
                if (!EditorSceneManager.SaveScene(scene))
                {
                    return new ErrorResponse($"Failed to save scene '{scene.path}'.");
                }

                AssetDatabase.SaveAssets();

                return new SuccessResponse("Configured prototype scenario scene.", new
                {
                    scene = scene.path,
                    scenario = scenarioPath,
                    expectedScenarioUnitCount = scenario.UnitEntries.Count,
                    expectedPlayerCount = CountScenarioTeam(scenario, TeamId.Player),
                    expectedEnemyCount = CountScenarioTeam(scenario, TeamId.Enemy),
                    systemsRoot = GetScenePath(systemsRoot),
                    systemsRootCreated,
                    unitsRoot = GetScenePath(unitsRoot),
                    unitsRootCreated,
                    scenarioRoot = GetScenePath(scenarioRoot),
                    scenarioRootCreated,
                    scenarioUnitsRoot = GetScenePath(scenarioUnitsRoot.gameObject),
                    scenarioUnitsRootCreated,
                    unitRegistry = GetScenePath(registry.gameObject),
                    unitRegistryCreated = registryCreated,
                    unitSpawner = GetScenePath(spawner.gameObject),
                    unitSpawnerCreated = spawnerCreated,
                    scenarioLoader = GetScenePath(scenarioLoader.gameObject),
                    scenarioLoaderCreated,
                    duplicateScenarioLoadersRemoved,
                    manualUnitsRemoved = removedManualUnits,
                    legacyInitialUnitsRootRemoved,
                    sceneTacticalUnitCount,
                    unitPrefab = PrototypeUnitPrefabPath,
                    map = AssetDatabase.GetAssetPath(scenario.MapDefinition),
                    references = new
                    {
                        gridManager = GetScenePath(gridManager.gameObject),
                        combatManager = combatManager != null ? GetScenePath(combatManager.gameObject) : null,
                        inputRouter = inputRouter != null ? GetScenePath(inputRouter.gameObject) : null,
                        eventBus = eventBus != null ? GetScenePath(eventBus.gameObject) : null,
                        aiController = aiController != null ? GetScenePath(aiController.gameObject) : null
                    }
                });
            }
            catch (Exception exception)
            {
                return new ErrorResponse($"Failed to configure prototype scenario scene: {exception.Message}");
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

        private static TacticalUnit LoadRequiredUnitPrefab(string path)
        {
            var prefab = LoadRequiredAsset<GameObject>(path, "prototype unit prefab");
            var tacticalUnit = prefab.GetComponent<TacticalUnit>();
            if (tacticalUnit == null)
            {
                throw new InvalidOperationException($"Prototype unit prefab '{path}' must contain a {nameof(TacticalUnit)} component.");
            }

            if (prefab.GetComponent<GridPathMover>() == null)
            {
                throw new InvalidOperationException($"Prototype unit prefab '{path}' must contain a {nameof(GridPathMover)} component.");
            }

            return tacticalUnit;
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

        private static Transform EnsureChild(Transform parent, string childName, ref bool created)
        {
            var existing = parent.Find(childName);
            if (existing != null)
            {
                created = false;
                existing.localPosition = Vector3.zero;
                existing.localRotation = Quaternion.identity;
                existing.localScale = Vector3.one;
                return existing;
            }

            var gameObject = new GameObject(childName);
            gameObject.transform.SetParent(parent, false);
            created = true;
            return gameObject.transform;
        }

        private static T EnsureSceneComponent<T>(Scene scene, GameObject preferredRoot, ref bool created)
            where T : Component
        {
            var component = preferredRoot.GetComponent<T>();
            if (component != null)
            {
                created = false;
                return component;
            }

            component = FindComponentInScene<T>(scene);
            if (component != null)
            {
                created = false;
                return component;
            }

            created = true;
            return preferredRoot.AddComponent<T>();
        }

        private static ScenarioLoader EnsureSingleScenarioLoader(
            Scene scene,
            GameObject scenarioRoot,
            ref bool created,
            out int duplicateScenarioLoadersRemoved)
        {
            var loader = scenarioRoot.GetComponent<ScenarioLoader>();
            if (loader == null)
            {
                loader = scenarioRoot.AddComponent<ScenarioLoader>();
                created = true;
            }
            else
            {
                created = false;
            }

            duplicateScenarioLoadersRemoved = 0;
            foreach (var candidate in FindComponentsInScene<ScenarioLoader>(scene))
            {
                if (candidate == null || ReferenceEquals(candidate, loader))
                {
                    continue;
                }

                UnityEngine.Object.DestroyImmediate(candidate);
                duplicateScenarioLoadersRemoved++;
            }

            return loader;
        }

        private static T FindComponentInScene<T>(Scene scene)
            where T : Component
        {
            foreach (var component in FindComponentsInScene<T>(scene))
            {
                return component;
            }

            return null;
        }

        private static List<T> FindComponentsInScene<T>(Scene scene)
            where T : Component
        {
            var components = new List<T>();
            foreach (var root in scene.GetRootGameObjects())
            {
                components.AddRange(root.GetComponentsInChildren<T>(includeInactive: true));
            }

            return components;
        }

        private static int CountComponentsInScene<T>(Scene scene)
            where T : Component
        {
            return FindComponentsInScene<T>(scene).Count;
        }

        private static void ConfigureGridManager(GridManager gridManager, GridMapDefinition mapDefinition)
        {
            gridManager.SetMapDefinition(mapDefinition, rebuild: false);
            EditorUtility.SetDirty(gridManager);
        }

        private static void ConfigureSpawner(
            UnitSpawner spawner,
            TacticalUnit unitPrefab,
            UnitRegistry registry,
            GridManager gridManager,
            Transform spawnParent)
        {
            spawner.Configure(unitPrefab, registry, gridManager, spawnParent);
            spawner.ResetUnitIdSequence(FirstUnitId);
            EditorUtility.SetDirty(spawner);
        }

        private static void ConfigureScenarioLoader(
            ScenarioLoader loader,
            ScenarioDefinition scenario,
            GridManager gridManager,
            UnitSpawner spawner,
            CombatManager combatManager)
        {
            var serializedObject = new SerializedObject(loader);
            serializedObject.Update();
            SetObjectReference(serializedObject, "scenarioDefinition", scenario);
            SetObjectReference(serializedObject, "gridManager", gridManager);
            SetObjectReference(serializedObject, "unitSpawner", spawner);
            SetObjectReference(serializedObject, "combatManager", combatManager);
            SetBool(serializedObject, "loadOnAwake", true);
            SetBool(serializedObject, "clearExistingUnitsBeforeLoad", true);
            serializedObject.ApplyModifiedPropertiesWithoutUndo();
            EditorUtility.SetDirty(loader);
        }

        private static void ConfigureCombatManager(
            CombatManager combatManager,
            UnitRegistry registry,
            GridManager gridManager,
            PlayerCommandRouter inputRouter,
            CombatEventBus eventBus,
            AiController aiController)
        {
            combatManager.Configure(registry, gridManager, inputRouter, eventBus, aiController);
            combatManager.StartCombatOnStart = true;
            combatManager.LogCombatStart = false;
            combatManager.LogActionFlow = false;
            EditorUtility.SetDirty(combatManager);
        }

        private static int RemoveSceneTacticalUnits(Scene scene)
        {
            var removedCount = 0;
            foreach (var unit in FindComponentsInScene<TacticalUnit>(scene))
            {
                if (unit == null)
                {
                    continue;
                }

                UnityEngine.Object.DestroyImmediate(unit.gameObject);
                removedCount++;
            }

            return removedCount;
        }

        private static bool RemoveLegacyInitialUnitsRoot(Transform unitsRoot)
        {
            var legacyRoot = unitsRoot != null ? unitsRoot.Find(LegacyInitialUnitsRootName) : null;
            if (legacyRoot == null || legacyRoot.childCount > 0)
            {
                return false;
            }

            UnityEngine.Object.DestroyImmediate(legacyRoot.gameObject);
            return true;
        }

        private static int CountScenarioTeam(ScenarioDefinition scenario, TeamId team)
        {
            var count = 0;
            foreach (var entry in scenario.UnitEntries)
            {
                if (entry != null && entry.Team == team)
                {
                    count++;
                }
            }

            return count;
        }

        private static void SetObjectReference(SerializedObject serializedObject, string propertyName, UnityEngine.Object value)
        {
            var property = serializedObject.FindProperty(propertyName);
            if (property == null)
            {
                throw new InvalidOperationException($"Serialized property '{propertyName}' was not found on {serializedObject.targetObject.GetType().Name}.");
            }

            property.objectReferenceValue = value;
        }

        private static void SetBool(SerializedObject serializedObject, string propertyName, bool value)
        {
            var property = serializedObject.FindProperty(propertyName);
            if (property == null)
            {
                throw new InvalidOperationException($"Serialized property '{propertyName}' was not found on {serializedObject.targetObject.GetType().Name}.");
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
