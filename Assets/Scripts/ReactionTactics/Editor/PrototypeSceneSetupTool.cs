using System;
using System.Collections.Generic;
using System.Linq;
using Newtonsoft.Json.Linq;
using ReactionTactics.AI;
using ReactionTactics.Grid;
using ReactionTactics.Input;
using ReactionTactics.Scenarios;
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
    [UnityCliTool(Name = "rt_setup_prototype_scene", Description = "Create/update default prototype data and configure the main playable prototype scene in one command.", Group = "reaction-tactics")]
    public static class PrototypeSceneSetupTool
    {
        private const string DefaultScenePath = "Assets/Scenes/MainPrototype.unity";
        private const string DefaultScenarioPath = "Assets/ScriptableObjects/Scenarios/DefaultSkirmish.asset";
        private const string DefaultMapPath = "Assets/ScriptableObjects/DefaultPrototypeMap.asset";
        private const string UnitAssetFolderPath = "Assets/ScriptableObjects/Units";
        private const string AbilityAssetFolderPath = "Assets/ScriptableObjects/Abilities";
        private const string ScenarioAssetFolderPath = "Assets/ScriptableObjects/Scenarios";
        private const string SystemsRootName = "Systems";
        private const string UiRootName = "UI";

        private static readonly string[] TrackedAssetPaths =
        {
            DefaultMapPath,
            UnitAssetFolderPath + "/Knight.asset",
            UnitAssetFolderPath + "/Rogue.asset",
            UnitAssetFolderPath + "/Archer.asset",
            UnitAssetFolderPath + "/Mage.asset",
            UnitAssetFolderPath + "/Goblin.asset",
            UnitAssetFolderPath + "/Shaman.asset",
            AbilityAssetFolderPath + "/Move.asset",
            AbilityAssetFolderPath + "/MeleeSlash.asset",
            AbilityAssetFolderPath + "/ConeShot.asset",
            AbilityAssetFolderPath + "/Fireball.asset",
            AbilityAssetFolderPath + "/Brace.asset",
            AbilityAssetFolderPath + "/PassReaction.asset",
            DefaultScenarioPath
        };

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

                if (AssetDatabase.LoadAssetAtPath<SceneAsset>(scenePath) == null)
                {
                    return new ErrorResponse($"Scene asset '{scenePath}' was not found.");
                }

                var existedBefore = CaptureTrackedAssetExistence();
                var steps = new List<StepSummary>();

                steps.Add(RunStep("defaultMap", () => DefaultMapAssetCreatorTool.HandleCommand(new JObject())));
                steps.Add(RunStep("defaultUnits", () => DefaultUnitStatsAssetCreatorTool.HandleCommand(new JObject())));
                steps.Add(RunStep("defaultAbilities", () => DefaultAbilityAssetCreatorTool.HandleCommand(new JObject())));
                steps.Add(RunStep("defaultScenario", () => DefaultScenarioAssetCreatorTool.HandleCommand(new JObject())));
                steps.Add(RunStep("gridScene", () => GridSceneSetupTool.HandleCommand(BuildSceneParameters(scenePath))));

                var initialCombatSystems = EnsurePrototypeCombatSystems(scenePath);
                steps.Add(new StepSummary("combatSystems", initialCombatSystems.Message));

                steps.Add(RunStep(
                    "scenarioScene",
                    () => ScenarioSceneSetupTool.HandleCommand(BuildScenarioSceneParameters(scenePath, DefaultScenarioPath))));
                steps.Add(RunStep("inputAndUiScene", () => InputSceneSetupTool.HandleCommand(BuildSceneParameters(scenePath))));

                var hudSummary = EnsureCombatHud(scenePath);
                steps.Add(new StepSummary("combatHud", hudSummary.Message));

                var finalCombatSystems = EnsurePrototypeCombatSystems(scenePath);
                steps.Add(new StepSummary("combatSystemsFinal", finalCombatSystems.Message));

                var scene = OpenOrUseScene(scenePath);
                EditorSceneManager.MarkSceneDirty(scene);
                if (!EditorSceneManager.SaveScene(scene))
                {
                    return new ErrorResponse($"Failed to save scene '{scene.path}'.");
                }

                AssetDatabase.SaveAssets();
                var projectSaveInvoked = EditorApplication.ExecuteMenuItem("File/Save Project");
                AssetDatabase.Refresh();

                var assetSummary = BuildAssetSummary(existedBefore);
                var sceneSummary = BuildSceneSummary(scene);

                return new SuccessResponse("Configured playable prototype scene.", new
                {
                    scene = scene.path,
                    steps = steps.Select(step => new
                    {
                        name = step.Name,
                        message = step.Message
                    }).ToArray(),
                    assets = assetSummary,
                    sceneObjects = sceneSummary,
                    combatSystems = finalCombatSystems.Data,
                    combatHud = hudSummary.Data,
                    savedScene = scene.path,
                    projectSaveInvoked
                });
            }
            catch (Exception exception)
            {
                return new ErrorResponse($"Failed to configure playable prototype scene: {exception.Message}");
            }
        }

        private static StepSummary RunStep(string name, Func<object> command)
        {
            var response = command();
            var errorResponse = response as ErrorResponse;
            if (errorResponse != null)
            {
                throw new InvalidOperationException($"{name} failed: {errorResponse.message}");
            }

            var successResponse = response as SuccessResponse;
            if (successResponse == null)
            {
                throw new InvalidOperationException($"{name} returned unexpected response type '{response?.GetType().Name ?? "null"}'.");
            }

            return new StepSummary(name, successResponse.message);
        }

        private static JObject BuildSceneParameters(string scenePath)
        {
            return new JObject
            {
                ["scenePath"] = scenePath
            };
        }

        private static JObject BuildScenarioSceneParameters(string scenePath, string scenarioPath)
        {
            return new JObject
            {
                ["scenePath"] = scenePath,
                ["scenarioPath"] = scenarioPath
            };
        }

        private static CombatSetupSummary EnsurePrototypeCombatSystems(string scenePath)
        {
            var scene = OpenOrUseScene(scenePath);
            var systemsRootCreated = false;
            var systemsRoot = EnsureRootObject(scene, SystemsRootName, ref systemsRootCreated);
            var eventBusCreated = false;
            var combatManagerCreated = false;
            var aiControllerCreated = false;

            var eventBus = EnsureSceneComponent<CombatEventBus>(scene, systemsRoot, ref eventBusCreated);
            var combatManager = EnsureSceneComponent<CombatManager>(scene, systemsRoot, ref combatManagerCreated);
            var aiController = EnsureSceneComponent<AiController>(scene, systemsRoot, ref aiControllerCreated);
            var gridManager = FindComponentInScene<GridManager>(scene);
            var unitRegistry = FindComponentInScene<UnitRegistry>(scene);
            var inputRouter = FindComponentInScene<PlayerCommandRouter>(scene);

            aiController.ControlledTeam = TeamId.Enemy;
            aiController.AutomaticDelegationEnabled = true;
            aiController.CombatManager = combatManager;
            aiController.DecisionDelaySeconds = AiController.DefaultDecisionDelaySeconds;
            aiController.SkipDecisionPacing = false;
            aiController.LogDecisions = true;

            combatManager.Configure(unitRegistry, gridManager, inputRouter, eventBus, aiController);
            combatManager.StartCombatOnStart = true;
            combatManager.LogCombatStart = true;
            combatManager.LogActionFlow = true;

            EditorUtility.SetDirty(systemsRoot);
            EditorUtility.SetDirty(eventBus);
            EditorUtility.SetDirty(combatManager);
            EditorUtility.SetDirty(aiController);
            EditorSceneManager.MarkSceneDirty(scene);
            if (!EditorSceneManager.SaveScene(scene))
            {
                throw new InvalidOperationException($"Failed to save scene '{scene.path}' after combat system setup.");
            }

            AssetDatabase.SaveAssets();

            return new CombatSetupSummary(
                "Ensured combat manager, event bus, and enemy AI controller.",
                new
                {
                    systemsRoot = GetScenePath(systemsRoot),
                    systemsRootCreated,
                    combatEventBus = GetScenePath(eventBus.gameObject),
                    combatEventBusCreated = eventBusCreated,
                    combatManager = GetScenePath(combatManager.gameObject),
                    combatManagerCreated,
                    aiController = GetScenePath(aiController.gameObject),
                    aiControllerCreated,
                    references = new
                    {
                        gridManager = gridManager != null ? GetScenePath(gridManager.gameObject) : null,
                        unitRegistry = unitRegistry != null ? GetScenePath(unitRegistry.gameObject) : null,
                        inputRouter = inputRouter != null ? GetScenePath(inputRouter.gameObject) : null
                    }
                });
        }

        private static HudSetupSummary EnsureCombatHud(string scenePath)
        {
            var scene = OpenOrUseScene(scenePath);
            var uiRootCreated = false;
            var hudCreated = false;
            var uiRoot = EnsureRootObject(scene, UiRootName, ref uiRootCreated);
            var combatManager = FindComponentInScene<CombatManager>(scene);
            var combatHud = EnsureSceneComponent<CombatHud>(scene, uiRoot, ref hudCreated);
            combatHud.Configure(combatManager);
            combatHud.Visible = true;

            EditorUtility.SetDirty(uiRoot);
            EditorUtility.SetDirty(combatHud);
            EditorSceneManager.MarkSceneDirty(scene);
            if (!EditorSceneManager.SaveScene(scene))
            {
                throw new InvalidOperationException($"Failed to save scene '{scene.path}' after combat HUD setup.");
            }

            AssetDatabase.SaveAssets();

            return new HudSetupSummary(
                "Ensured combat HUD overlay is present and references the combat manager.",
                new
                {
                    uiRoot = GetScenePath(uiRoot),
                    uiRootCreated,
                    combatHud = GetScenePath(combatHud.gameObject),
                    combatHudCreated = hudCreated,
                    combatManager = combatManager != null ? GetScenePath(combatManager.gameObject) : null
                });
        }

        private static Dictionary<string, bool> CaptureTrackedAssetExistence()
        {
            var result = new Dictionary<string, bool>(StringComparer.Ordinal);
            foreach (var path in TrackedAssetPaths)
            {
                result[path] = AssetDatabase.LoadMainAssetAtPath(path) != null;
            }

            return result;
        }

        private static object BuildAssetSummary(IReadOnlyDictionary<string, bool> existedBefore)
        {
            var existingTrackedPaths = TrackedAssetPaths
                .Where(path => AssetDatabase.LoadMainAssetAtPath(path) != null)
                .ToArray();
            var createdPaths = existingTrackedPaths
                .Where(path => existedBefore.TryGetValue(path, out var existed) && !existed)
                .ToArray();
            var updatedPaths = existingTrackedPaths
                .Where(path => !existedBefore.TryGetValue(path, out var existed) || existed)
                .ToArray();
            var missingTrackedPaths = TrackedAssetPaths
                .Where(path => AssetDatabase.LoadMainAssetAtPath(path) == null)
                .ToArray();
            var unitStats = FindAssetPaths("t:UnitStatsDefinition", UnitAssetFolderPath);
            var abilities = FindAssetPaths("t:AbilityDefinition", AbilityAssetFolderPath);
            var scenarios = FindAssetPaths("t:ScenarioDefinition", ScenarioAssetFolderPath);
            var defaultScenario = AssetDatabase.LoadAssetAtPath<ScenarioDefinition>(DefaultScenarioPath);

            return new
            {
                createdCount = createdPaths.Length,
                updatedCount = updatedPaths.Length,
                missingTrackedCount = missingTrackedPaths.Length,
                createdPaths,
                updatedPaths,
                missingTrackedPaths,
                map = AssetDatabase.LoadMainAssetAtPath(DefaultMapPath) != null ? DefaultMapPath : null,
                unitStatsCount = unitStats.Length,
                abilityCount = abilities.Length,
                scenarioCount = scenarios.Length,
                defaultScenario = defaultScenario != null
                    ? new
                    {
                        path = DefaultScenarioPath,
                        unitCount = defaultScenario.UnitEntries.Count,
                        playerCount = defaultScenario.UnitEntries.Count(entry => entry != null && entry.Team == TeamId.Player),
                        enemyCount = defaultScenario.UnitEntries.Count(entry => entry != null && entry.Team == TeamId.Enemy)
                    }
                    : null
            };
        }

        private static object BuildSceneSummary(Scene scene)
        {
            var gridManager = FindComponentInScene<GridManager>(scene);
            var mapCellCount = 0;
            if (gridManager != null && gridManager.RebuildMap() && gridManager.CurrentMap != null)
            {
                mapCellCount = gridManager.CurrentMap.AllCells.Count();
                EditorUtility.SetDirty(gridManager);
            }

            var scenarioLoader = FindComponentInScene<ScenarioLoader>(scene);
            var scenario = scenarioLoader != null ? scenarioLoader.ScenarioDefinition : null;

            return new
            {
                rootCount = scene.rootCount,
                roots = scene.GetRootGameObjects()
                    .Select(root => root.name)
                    .OrderBy(name => name, StringComparer.Ordinal)
                    .ToArray(),
                camera = ToComponentPath(FindComponentInScene<Camera>(scene)),
                gridManager = ToComponentPath(gridManager),
                gridTerrainView = ToComponentPath(FindComponentInScene<GridTerrainView>(scene)),
                gridHighlightManager = ToComponentPath(FindComponentInScene<GridHighlightManager>(scene)),
                unitRegistry = ToComponentPath(FindComponentInScene<UnitRegistry>(scene)),
                unitSpawner = ToComponentPath(FindComponentInScene<UnitSpawner>(scene)),
                playerCommandRouter = ToComponentPath(FindComponentInScene<PlayerCommandRouter>(scene)),
                combatEventBus = ToComponentPath(FindComponentInScene<CombatEventBus>(scene)),
                combatManager = ToComponentPath(FindComponentInScene<CombatManager>(scene)),
                aiController = ToComponentPath(FindComponentInScene<AiController>(scene)),
                scenarioLoader = ToComponentPath(scenarioLoader),
                combatHud = ToComponentPath(FindComponentInScene<CombatHud>(scene)),
                activeActionMenu = ToComponentPath(FindComponentInScene<ActiveActionMenu>(scene)),
                reactionMenu = ToComponentPath(FindComponentInScene<ReactionMenu>(scene)),
                combatLog = ToComponentPath(FindComponentInScene<CombatLogView>(scene)),
                combatEndPanel = ToComponentPath(FindComponentInScene<CombatEndPanel>(scene)),
                rulesHelpOverlay = ToComponentPath(FindComponentInScene<PrototypeRulesHelpOverlay>(scene)),
                editModeTacticalUnitCount = CountComponentsInScene<TacticalUnit>(scene),
                mapCellCount,
                scenarioUnitCount = scenario != null ? scenario.UnitEntries.Count : 0,
                scenarioPath = scenario != null ? AssetDatabase.GetAssetPath(scenario) : null
            };
        }

        private static string[] FindAssetPaths(string filter, string folder)
        {
            if (!AssetDatabase.IsValidFolder(folder))
            {
                return Array.Empty<string>();
            }

            return AssetDatabase.FindAssets(filter, new[] { folder })
                .Select(AssetDatabase.GUIDToAssetPath)
                .OrderBy(path => path, StringComparer.Ordinal)
                .ToArray();
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
            component = preferredRoot.AddComponent<T>();
            EditorUtility.SetDirty(component);
            return component;
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

        private static int CountComponentsInScene<T>(Scene scene)
            where T : Component
        {
            var count = 0;
            foreach (var root in scene.GetRootGameObjects())
            {
                count += root.GetComponentsInChildren<T>(includeInactive: true).Length;
            }

            return count;
        }

        private static string ToComponentPath(Component component)
        {
            return component != null ? GetScenePath(component.gameObject) : null;
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

        private sealed class StepSummary
        {
            public StepSummary(string name, string message)
            {
                Name = name;
                Message = message;
            }

            public string Name { get; }

            public string Message { get; }
        }

        private sealed class CombatSetupSummary
        {
            public CombatSetupSummary(string message, object data)
            {
                Message = message;
                Data = data;
            }

            public string Message { get; }

            public object Data { get; }
        }

        private sealed class HudSetupSummary
        {
            public HudSetupSummary(string message, object data)
            {
                Message = message;
                Data = data;
            }

            public string Message { get; }

            public object Data { get; }
        }
    }
}
