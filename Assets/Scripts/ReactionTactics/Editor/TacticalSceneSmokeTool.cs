using System;
using System.Collections.Generic;
using System.Linq;
using Newtonsoft.Json.Linq;
using ReactionTactics.Actions;
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
    [UnityCliTool(Name = "rt_smoke_tactical_scene", Description = "Run a fast tactical scene smoke check that validates setup, probes scenario bootstrap, and reports key runtime-ready counts.", Group = "reaction-tactics")]
    public static class TacticalSceneSmokeTool
    {
        private const string DefaultScenePath = "Assets/Scenes/MainPrototype.unity";
        private const string AbilityAssetFolderPath = "Assets/ScriptableObjects/Abilities";
        private const string EditorProbeMode = "editor_side_scenario_bootstrap_probe";

        public sealed class Parameters
        {
            [ToolParameter("Scene asset path to smoke-check.", DefaultValue = DefaultScenePath)]
            public string ScenePath { get; set; }

            [ToolParameter("Run an editor-side scenario load and StartCombat probe, then restore the scene from disk. This avoids entering play mode from the CLI handler.", DefaultValue = "true")]
            public bool RunBootstrapProbe { get; set; }
        }

        public static object HandleCommand(JObject parameters)
        {
            try
            {
                var toolParams = new ToolParams(parameters ?? new JObject());
                var scenePath = toolParams.Get("scenePath", toolParams.Get("scene_path", DefaultScenePath)).Trim();
                var runBootstrapProbe = toolParams.GetBool(
                    "runBootstrapProbe",
                    toolParams.GetBool("run_bootstrap_probe", true));

                if (string.IsNullOrWhiteSpace(scenePath))
                {
                    return new ErrorResponse("Parameter 'scenePath' cannot be empty.");
                }

                if (AssetDatabase.LoadAssetAtPath<SceneAsset>(scenePath) == null)
                {
                    return new ErrorResponse($"Scene asset '{scenePath}' was not found.");
                }

                if (Application.isPlaying || EditorApplication.isPlayingOrWillChangePlaymode)
                {
                    return new ErrorResponse($"{nameof(TacticalSceneSmokeTool)} cannot run while the editor is entering or already in play mode.");
                }

                var validationResponse = PrototypeSceneValidationTool.HandleCommand(new JObject
                {
                    ["scenePath"] = scenePath
                });
                var validationSummary = ToResponseSummary(validationResponse);
                var validationError = validationResponse as ErrorResponse;

                var scene = OpenOrUseScene(scenePath);
                var editorSummary = BuildEditorSummary(scene);
                SmokeSection probeSummary = null;
                if (validationError == null && runBootstrapProbe)
                {
                    probeSummary = RunBootstrapProbe(scenePath);
                    scene = OpenOrUseScene(scenePath);
                }

                var failures = new List<string>();
                if (validationError != null)
                {
                    failures.Add(validationError.message);
                }

                failures.AddRange(editorSummary.Errors);
                if (probeSummary != null)
                {
                    failures.AddRange(probeSummary.Errors);
                }

                var topLevel = BuildTopLevelSummary(editorSummary, probeSummary);
                var data = new
                {
                    scene = scene.path,
                    mode = EditorProbeMode,
                    playMode = new
                    {
                        entered = false,
                        reason = "The smoke tool uses editor-side validation and a temporary scenario bootstrap probe instead of entering play mode, because entering play mode from a CLI tool can trigger domain reload before the command can return."
                    },
                    valid = failures.Count == 0,
                    topLevel.gridCellCount,
                    topLevel.livingUnitCount,
                    topLevel.abilityCount,
                    topLevel.currentPhase,
                    validation = validationSummary,
                    editor = editorSummary.Data,
                    bootstrapProbe = probeSummary != null
                        ? probeSummary.Data
                        : new
                        {
                            ran = false,
                            reason = validationError != null
                                ? "Skipped because structural scene validation failed."
                                : "Skipped by runBootstrapProbe=false."
                        },
                    errors = failures.ToArray()
                };

                if (failures.Count > 0)
                {
                    return new ErrorResponse("Tactical scene smoke check failed: " + string.Join(" ", failures), data);
                }

                return new SuccessResponse("Tactical scene smoke check passed.", data);
            }
            catch (Exception exception)
            {
                return new ErrorResponse($"Failed to smoke-check tactical scene: {exception.Message}");
            }
        }

        private static SmokeSection BuildEditorSummary(Scene scene)
        {
            var errors = new List<string>();
            var camera = FindComponentInScene<Camera>(scene);
            var gridManager = FindComponentInScene<GridManager>(scene);
            var terrainView = FindComponentInScene<GridTerrainView>(scene);
            var highlightManager = FindComponentInScene<GridHighlightManager>(scene);
            var unitRegistry = FindComponentInScene<UnitRegistry>(scene);
            var unitSpawner = FindComponentInScene<UnitSpawner>(scene);
            var scenarioLoader = FindComponentInScene<ScenarioLoader>(scene);
            var combatEventBus = FindComponentInScene<CombatEventBus>(scene);
            var combatManager = FindComponentInScene<CombatManager>(scene);
            var aiController = FindComponentInScene<AiController>(scene);
            var gridPicker = FindComponentInScene<GridPicker>(scene);
            var selectionController = FindComponentInScene<SelectionController>(scene);
            var inputRouter = FindComponentInScene<PlayerCommandRouter>(scene);
            var combatHud = FindComponentInScene<CombatHud>(scene);
            var activeActionMenu = FindComponentInScene<ActiveActionMenu>(scene);
            var reactionMenu = FindComponentInScene<ReactionMenu>(scene);
            var combatLogView = FindComponentInScene<CombatLogView>(scene);
            var combatEndPanel = FindComponentInScene<CombatEndPanel>(scene);
            var rulesHelpOverlay = FindComponentInScene<PrototypeRulesHelpOverlay>(scene);

            var requiredReferences = new[]
            {
                RequiredReference("Camera", camera),
                RequiredReference(nameof(GridManager), gridManager),
                RequiredReference(nameof(GridTerrainView), terrainView),
                RequiredReference(nameof(GridHighlightManager), highlightManager),
                RequiredReference(nameof(UnitRegistry), unitRegistry),
                RequiredReference(nameof(UnitSpawner), unitSpawner),
                RequiredReference(nameof(ScenarioLoader), scenarioLoader),
                RequiredReference(nameof(CombatEventBus), combatEventBus),
                RequiredReference(nameof(CombatManager), combatManager),
                RequiredReference(nameof(AiController), aiController),
                RequiredReference(nameof(GridPicker), gridPicker),
                RequiredReference(nameof(SelectionController), selectionController),
                RequiredReference(nameof(PlayerCommandRouter), inputRouter),
                RequiredReference(nameof(CombatHud), combatHud),
                RequiredReference(nameof(ActiveActionMenu), activeActionMenu),
                RequiredReference(nameof(ReactionMenu), reactionMenu),
                RequiredReference(nameof(CombatLogView), combatLogView),
                RequiredReference(nameof(CombatEndPanel), combatEndPanel),
                RequiredReference(nameof(PrototypeRulesHelpOverlay), rulesHelpOverlay)
            };

            foreach (var missing in requiredReferences.Where(reference => !reference.exists))
            {
                errors.Add($"Missing required reference: {missing.name}.");
            }

            var mapSummary = BuildMapSummary(gridManager, errors);
            var scenarioSummary = BuildScenarioSummary(scenarioLoader, errors);
            var abilitySummary = BuildAbilitySummary(scenarioLoader != null ? scenarioLoader.ScenarioDefinition : null, errors);
            var registrySummary = BuildRegistrySummary(unitRegistry);
            var combatSummary = BuildCombatSummary(combatManager);
            var testableReferences = BuildTestableReferenceSummary(
                gridManager,
                terrainView,
                highlightManager,
                unitRegistry,
                unitSpawner,
                scenarioLoader,
                combatEventBus,
                combatManager,
                aiController,
                gridPicker,
                selectionController,
                inputRouter,
                combatHud,
                activeActionMenu,
                reactionMenu,
                combatLogView,
                combatEndPanel,
                rulesHelpOverlay);

            var data = new
            {
                ran = true,
                scene = new
                {
                    name = scene.name,
                    path = scene.path,
                    isLoaded = scene.isLoaded,
                    isDirty = scene.isDirty,
                    rootCount = scene.rootCount,
                    roots = scene.GetRootGameObjects()
                        .Select(root => root.name)
                        .OrderBy(name => name, StringComparer.Ordinal)
                        .ToArray()
                },
                references = requiredReferences,
                testableReferences,
                grid = mapSummary,
                units = registrySummary,
                scenario = scenarioSummary,
                abilities = abilitySummary,
                combat = combatSummary,
                sceneTacticalUnitCount = CountComponentsInScene<TacticalUnit>(scene)
            };

            return new SmokeSection(data, errors);
        }

        private static SmokeSection RunBootstrapProbe(string scenePath)
        {
            var errors = new List<string>();
            object data = null;
            var scene = OpenOrUseScene(scenePath);

            if (scene.isDirty)
            {
                errors.Add($"Cannot run bootstrap probe because scene '{scene.path}' has unsaved changes.");
                data = new
                {
                    ran = false,
                    reason = "Scene has unsaved changes; the bootstrap probe refuses to mutate a dirty scene."
                };
                return new SmokeSection(data, errors);
            }

            try
            {
                var gridManager = FindComponentInScene<GridManager>(scene);
                var unitRegistry = FindComponentInScene<UnitRegistry>(scene);
                var scenarioLoader = FindComponentInScene<ScenarioLoader>(scene);
                var combatManager = FindComponentInScene<CombatManager>(scene);
                var missingReferences = new List<string>();

                AddMissingReference(missingReferences, nameof(GridManager), gridManager);
                AddMissingReference(missingReferences, nameof(UnitRegistry), unitRegistry);
                AddMissingReference(missingReferences, nameof(ScenarioLoader), scenarioLoader);
                AddMissingReference(missingReferences, nameof(CombatManager), combatManager);

                if (missingReferences.Count > 0)
                {
                    errors.AddRange(missingReferences.Select(name => $"Missing required bootstrap reference: {name}."));
                    data = new
                    {
                        ran = false,
                        reason = "Required bootstrap references are missing.",
                        missingReferences = missingReferences.ToArray()
                    };
                    return new SmokeSection(data, errors);
                }

                var loadResult = scenarioLoader.LoadScenario();
                if (loadResult.IsFailure)
                {
                    errors.Add(loadResult.ErrorMessage);
                }

                var gridCellCount = gridManager.CurrentMap != null ? gridManager.CurrentMap.AllCells.Count : 0;
                var livingUnits = unitRegistry.GetLivingUnits();
                var livingTeamCount = CountLivingTeams(livingUnits);
                if (loadResult.IsSuccess && livingUnits.Count == 0)
                {
                    errors.Add("Bootstrap probe loaded the scenario, but the unit registry has no living units.");
                }

                if (loadResult.IsSuccess && livingTeamCount < 2)
                {
                    errors.Add("Bootstrap probe loaded the scenario, but living units do not cover at least two teams.");
                }

                var startCombatSuccess = false;
                string startCombatError = null;
                if (loadResult.IsSuccess)
                {
                    var startResult = combatManager.StartCombat();
                    startCombatSuccess = startResult.IsSuccess;
                    if (startResult.IsFailure)
                    {
                        startCombatError = startResult.ErrorMessage;
                        errors.Add(startResult.ErrorMessage);
                    }
                }

                var currentState = combatManager.CurrentState;
                if (loadResult.IsSuccess && startCombatSuccess && currentState.Phase == CombatPhase.NotStarted)
                {
                    errors.Add("CombatManager remained NotStarted after StartCombat succeeded.");
                }

                data = new
                {
                    ran = true,
                    restoredSceneFromDisk = true,
                    scene = scene.path,
                    loadScenario = new
                    {
                        success = loadResult.IsSuccess,
                        error = loadResult.IsFailure ? loadResult.ErrorMessage : null,
                        spawnedUnitCount = scenarioLoader.SpawnedUnitCount
                    },
                    grid = new
                    {
                        cellCount = gridCellCount,
                        hasCurrentMap = gridManager.CurrentMap != null
                    },
                    livingUnits = new
                    {
                        count = livingUnits.Count,
                        teamCount = livingTeamCount,
                        units = livingUnits.Select(ToUnitSummary).ToArray()
                    },
                    abilities = BuildRuntimeAbilitySummary(livingUnits),
                    combat = new
                    {
                        startCombatSuccess,
                        startCombatError,
                        currentRound = currentState.CurrentRound,
                        currentPhase = currentState.Phase.ToString(),
                        activeUnit = ToUnitLabel(currentState.ActiveUnit),
                        currentReactor = ToUnitLabel(currentState.CurrentReactor),
                        hasPendingAction = currentState.HasPendingActionIntent
                    }
                };

                return new SmokeSection(data, errors);
            }
            catch (Exception exception)
            {
                errors.Add($"Bootstrap probe failed: {exception.Message}");
                data = new
                {
                    ran = true,
                    restoredSceneFromDisk = true,
                    error = exception.Message
                };
                return new SmokeSection(data, errors);
            }
            finally
            {
                EditorSceneManager.OpenScene(scenePath, OpenSceneMode.Single);
            }
        }

        private static object BuildMapSummary(GridManager gridManager, ICollection<string> errors)
        {
            if (gridManager == null)
            {
                return new { assigned = false, path = (string)null, width = 0, depth = 0, cellCount = 0 };
            }

            var mapDefinition = gridManager.MapDefinition;
            if (mapDefinition == null)
            {
                errors.Add($"{nameof(GridManager)} has no map definition assigned.");
                return new { assigned = false, path = (string)null, width = 0, depth = 0, cellCount = 0 };
            }

            var cellCount = 0;
            try
            {
                var map = mapDefinition.BuildGridMap();
                cellCount = map.AllCells.Count;
                if (cellCount == 0)
                {
                    errors.Add($"Map definition '{AssetDatabase.GetAssetPath(mapDefinition)}' builds zero grid cells.");
                }
            }
            catch (Exception exception)
            {
                errors.Add($"Map definition '{AssetDatabase.GetAssetPath(mapDefinition)}' failed to build: {exception.Message}");
            }

            return new
            {
                assigned = true,
                path = AssetDatabase.GetAssetPath(mapDefinition),
                width = mapDefinition.Width,
                depth = mapDefinition.Depth,
                cellCount
            };
        }

        private static object BuildScenarioSummary(ScenarioLoader scenarioLoader, ICollection<string> errors)
        {
            if (scenarioLoader == null)
            {
                return new { assigned = false, path = (string)null, expectedUnitCount = 0, playerCount = 0, enemyCount = 0 };
            }

            var scenario = scenarioLoader.ScenarioDefinition;
            if (scenario == null)
            {
                errors.Add($"{nameof(ScenarioLoader)} has no scenario definition assigned.");
                return new { assigned = false, path = (string)null, expectedUnitCount = 0, playerCount = 0, enemyCount = 0 };
            }

            var validation = scenario.ValidateScenario();
            if (validation.IsFailure)
            {
                errors.Add(validation.ErrorMessage);
            }

            var entries = scenario.UnitEntries;
            return new
            {
                assigned = true,
                path = AssetDatabase.GetAssetPath(scenario),
                map = scenario.MapDefinition != null ? AssetDatabase.GetAssetPath(scenario.MapDefinition) : null,
                expectedUnitCount = entries.Count,
                playerCount = entries.Count(entry => entry != null && entry.Team == TeamId.Player),
                enemyCount = entries.Count(entry => entry != null && entry.Team == TeamId.Enemy),
                spawnCells = entries
                    .Where(entry => entry != null)
                    .Select(entry => ToPositionSummary(entry.StartingCell))
                    .ToArray()
            };
        }

        private static object BuildAbilitySummary(ScenarioDefinition scenario, ICollection<string> errors)
        {
            var abilityAssetPaths = FindAssetPaths("t:AbilityDefinition", AbilityAssetFolderPath);
            var abilityAssets = abilityAssetPaths
                .Select(AssetDatabase.LoadAssetAtPath<AbilityDefinition>)
                .Where(ability => ability != null)
                .ToArray();
            var invalidAbilities = new List<object>();
            foreach (var ability in abilityAssets)
            {
                var validation = ability.ValidateDefinition();
                if (validation.IsFailure)
                {
                    var path = AssetDatabase.GetAssetPath(ability);
                    errors.Add($"Ability asset '{path}' is invalid: {validation.ErrorMessage}");
                    invalidAbilities.Add(new
                    {
                        path,
                        error = validation.ErrorMessage
                    });
                }
            }

            var scenarioAbilities = scenario != null
                ? scenario.UnitEntries
                    .Where(entry => entry != null)
                    .SelectMany(entry => entry.AbilityLoadout)
                    .Where(ability => ability != null)
                    .Distinct()
                    .OrderBy(ability => ability.AbilityKey, StringComparer.Ordinal)
                    .ToArray()
                : Array.Empty<AbilityDefinition>();

            if (scenario != null && scenarioAbilities.Length == 0)
            {
                errors.Add($"Scenario '{AssetDatabase.GetAssetPath(scenario)}' has no assigned ability loadouts.");
            }

            return new
            {
                assetCount = abilityAssets.Length,
                assetPaths = abilityAssetPaths,
                invalidAbilities = invalidAbilities.ToArray(),
                scenarioDistinctAbilityCount = scenarioAbilities.Length,
                scenarioActionAbilityCount = scenarioAbilities.Count(ability => ability.CanBeUsedAsAction),
                scenarioReactionAbilityCount = scenarioAbilities.Count(ability => ability.CanBeUsedAsReaction),
                scenarioAbilities = scenarioAbilities.Select(ToAbilitySummary).ToArray()
            };
        }

        private static object BuildRegistrySummary(UnitRegistry unitRegistry)
        {
            if (unitRegistry == null)
            {
                return new
                {
                    registeredCount = 0,
                    livingCount = 0,
                    deadCount = 0,
                    livingTeamCount = 0,
                    units = Array.Empty<object>()
                };
            }

            var livingUnits = unitRegistry.GetLivingUnits();
            return new
            {
                registeredCount = unitRegistry.RegisteredCount,
                livingCount = unitRegistry.LivingCount,
                deadCount = unitRegistry.DeadCount,
                livingTeamCount = CountLivingTeams(livingUnits),
                units = unitRegistry.GetRegisteredUnits().Select(ToUnitSummary).ToArray()
            };
        }

        private static object BuildCombatSummary(CombatManager combatManager)
        {
            if (combatManager == null)
            {
                return new
                {
                    currentRound = 0,
                    currentPhase = CombatPhase.NotStarted.ToString(),
                    activeUnit = (string)null,
                    currentReactor = (string)null,
                    hasPendingAction = false
                };
            }

            var state = combatManager.CurrentState;
            return new
            {
                currentRound = state.CurrentRound,
                currentPhase = state.Phase.ToString(),
                activeUnit = ToUnitLabel(state.ActiveUnit),
                currentReactor = ToUnitLabel(state.CurrentReactor),
                hasPendingAction = state.HasPendingActionIntent,
                startCombatOnStart = combatManager.StartCombatOnStart,
                hasCombatEndOutcome = combatManager.HasCombatEndOutcome
            };
        }

        private static object BuildRuntimeAbilitySummary(IReadOnlyList<TacticalUnit> livingUnits)
        {
            var perUnit = livingUnits.Select(unit =>
            {
                var loadout = unit != null ? unit.GetComponent<UnitAbilityLoadout>() : null;
                var assignedAbilities = loadout != null
                    ? loadout.GetAssignedAbilities()
                    : Array.Empty<AbilityDefinition>();
                return new
                {
                    unit = ToUnitLabel(unit),
                    assignedAbilityCount = assignedAbilities.Count,
                    actionAbilityCount = assignedAbilities.Count(ability => ability != null && ability.CanBeUsedAsAction),
                    reactionAbilityCount = assignedAbilities.Count(ability => ability != null && ability.CanBeUsedAsReaction),
                    abilities = assignedAbilities
                        .Where(ability => ability != null)
                        .Select(ToAbilitySummary)
                        .ToArray()
                };
            }).ToArray();

            return new
            {
                unitsWithLoadouts = perUnit.Count(unit => unit.assignedAbilityCount > 0),
                distinctAbilityCount = livingUnits
                    .Select(unit => unit != null ? unit.GetComponent<UnitAbilityLoadout>() : null)
                    .Where(loadout => loadout != null)
                    .SelectMany(loadout => loadout.GetAssignedAbilities())
                    .Where(ability => ability != null)
                    .Distinct()
                    .Count(),
                perUnit
            };
        }

        private static object BuildTestableReferenceSummary(
            GridManager gridManager,
            GridTerrainView terrainView,
            GridHighlightManager highlightManager,
            UnitRegistry unitRegistry,
            UnitSpawner unitSpawner,
            ScenarioLoader scenarioLoader,
            CombatEventBus combatEventBus,
            CombatManager combatManager,
            AiController aiController,
            GridPicker gridPicker,
            SelectionController selectionController,
            PlayerCommandRouter inputRouter,
            CombatHud combatHud,
            ActiveActionMenu activeActionMenu,
            ReactionMenu reactionMenu,
            CombatLogView combatLogView,
            CombatEndPanel combatEndPanel,
            PrototypeRulesHelpOverlay rulesHelpOverlay)
        {
            return new
            {
                gridManagerHasMapDefinition = gridManager != null && gridManager.MapDefinition != null,
                terrainViewReferencesGridManager = terrainView != null && terrainView.GridManager == gridManager,
                highlightManagerReferencesTerrainView = highlightManager != null && highlightManager.TerrainView == terrainView,
                unitSpawnerReferencesRegistry = unitSpawner != null && unitSpawner.Registry == unitRegistry,
                unitSpawnerReferencesGridManager = unitSpawner != null && unitSpawner.GridManager == gridManager,
                unitSpawnerHasPrefab = unitSpawner != null && unitSpawner.UnitPrefab != null,
                scenarioLoaderReferencesScenario = scenarioLoader != null && scenarioLoader.ScenarioDefinition != null,
                scenarioLoaderReferencesGridManager = scenarioLoader != null && scenarioLoader.GridManager == gridManager,
                scenarioLoaderReferencesSpawner = scenarioLoader != null && scenarioLoader.UnitSpawner == unitSpawner,
                scenarioLoaderReferencesCombatManager = scenarioLoader != null && scenarioLoader.CombatManager == combatManager,
                combatManagerReferencesRegistry = combatManager != null && combatManager.UnitRegistry == unitRegistry,
                combatManagerReferencesGridManager = combatManager != null && combatManager.GridManager == gridManager,
                combatManagerReferencesInputRouter = combatManager != null && combatManager.InputRouter == inputRouter,
                combatManagerReferencesEventBus = combatManager != null && combatManager.EventBus == combatEventBus,
                combatManagerReferencesAiController = combatManager != null && combatManager.AiController == aiController,
                aiControllerReferencesCombatManager = aiController != null && aiController.CombatManager == combatManager,
                gridPickerHasCamera = gridPicker != null && gridPicker.SourceCamera != null,
                selectionReferencesGridPicker = selectionController != null && selectionController.GridPicker == gridPicker,
                inputRouterReferencesSelection = inputRouter != null && inputRouter.SelectionController == selectionController,
                inputRouterReferencesGridPicker = inputRouter != null && inputRouter.GridPicker == gridPicker,
                inputRouterReferencesCombatManager = inputRouter != null && inputRouter.CombatManager == combatManager,
                combatHudReferencesCombatManager = combatHud != null && combatHud.CombatManager == combatManager,
                activeActionMenuReferencesCombatManager = activeActionMenu != null && activeActionMenu.CombatManager == combatManager,
                reactionMenuReferencesCombatManager = reactionMenu != null && reactionMenu.CombatManager == combatManager,
                combatLogReferencesEventBus = combatLogView != null && combatLogView.EventBus == combatEventBus,
                combatEndPanelReferencesCombatManager = combatEndPanel != null && combatEndPanel.CombatManager == combatManager,
                rulesHelpOverlayPresent = rulesHelpOverlay != null
            };
        }

        private static TopLevelSummary BuildTopLevelSummary(SmokeSection editorSummary, SmokeSection probeSummary)
        {
            var editorData = editorSummary.Data;
            var probeData = probeSummary != null ? probeSummary.Data : null;
            return new TopLevelSummary(
                ExtractInt(probeData, "grid.cellCount", ExtractInt(editorData, "grid.cellCount")),
                ExtractInt(probeData, "livingUnits.count", ExtractInt(editorData, "scenario.expectedUnitCount")),
                ExtractInt(probeData, "abilities.distinctAbilityCount", ExtractInt(editorData, "abilities.scenarioDistinctAbilityCount")),
                ExtractString(probeData, "combat.currentPhase", ExtractString(editorData, "combat.currentPhase", CombatPhase.NotStarted.ToString())));
        }

        private static object ToResponseSummary(object response)
        {
            var success = response as SuccessResponse;
            if (success != null)
            {
                return BuildValidationResponseSummary(true, success.message, success.data);
            }

            var error = response as ErrorResponse;
            if (error != null)
            {
                return BuildValidationResponseSummary(false, error.message, error.data);
            }

            return new
            {
                success = false,
                message = $"Unexpected response type '{response?.GetType().Name ?? "null"}'.",
                scene = (string)null,
                valid = false,
                errorCount = 1,
                warningCount = 0,
                errors = new[] { "Prototype scene validation returned an unexpected response type." },
                warnings = Array.Empty<string>()
            };
        }

        private static object BuildValidationResponseSummary(bool success, string message, object responseData)
        {
            return new
            {
                success,
                message,
                scene = ExtractString(responseData, "scene"),
                valid = ExtractBool(responseData, "valid", success),
                errorCount = ExtractInt(responseData, "errorCount"),
                warningCount = ExtractInt(responseData, "warningCount"),
                errors = ExtractStringArray(responseData, "errors"),
                warnings = ExtractStringArray(responseData, "warnings")
            };
        }

        private static RequiredReferenceSummary RequiredReference(string name, Component component)
        {
            return new RequiredReferenceSummary(name, component != null, ToComponentPath(component));
        }

        private static void AddMissingReference<T>(ICollection<string> missingReferences, string name, T component)
            where T : Component
        {
            if (component == null)
            {
                missingReferences.Add(name);
            }
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

        private static int CountLivingTeams(IReadOnlyList<TacticalUnit> livingUnits)
        {
            var teams = new HashSet<TeamId>();
            foreach (var unit in livingUnits)
            {
                if (unit != null && unit.IsAlive)
                {
                    teams.Add(unit.Team);
                }
            }

            return teams.Count;
        }

        private static object ToUnitSummary(TacticalUnit unit)
        {
            if (unit == null)
            {
                return null;
            }

            var loadout = unit.GetComponent<UnitAbilityLoadout>();
            return new
            {
                name = unit.name,
                displayName = unit.DisplayName,
                unitId = unit.UnitId.ToString(),
                team = unit.Team.ToString(),
                hp = unit.CurrentHP,
                ap = unit.CurrentAP,
                position = ToPositionSummary(unit.CurrentGridPosition),
                abilityCount = loadout != null ? loadout.AssignedAbilityCount : 0
            };
        }

        private static object ToAbilitySummary(AbilityDefinition ability)
        {
            if (ability == null)
            {
                return null;
            }

            return new
            {
                key = ability.AbilityKey,
                name = ability.DisplayName,
                path = AssetDatabase.GetAssetPath(ability),
                usage = ability.Usage.ToString(),
                shape = ability.Shape.ToString(),
                apCost = ability.APCost,
                damage = ability.Damage,
                range = ability.Range,
                radius = ability.Radius,
                triggersReactions = ability.TriggersReactions
            };
        }

        private static object ToPositionSummary(GridPosition position)
        {
            return new
            {
                x = position.X,
                y = position.Y,
                z = position.Z,
                label = position.ToString()
            };
        }

        private static string ToUnitLabel(TacticalUnit unit)
        {
            return unit != null
                ? $"{unit.DisplayName} {unit.UnitId} [{unit.Team}] at {unit.CurrentGridPosition}"
                : null;
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

        private static int ExtractInt(object source, string dottedPath, int defaultValue = 0)
        {
            var value = ExtractValue(source, dottedPath);
            if (value is int intValue)
            {
                return intValue;
            }

            if (value is long longValue)
            {
                return (int)longValue;
            }

            return defaultValue;
        }

        private static string ExtractString(object source, string dottedPath, string defaultValue = null)
        {
            var value = ExtractValue(source, dottedPath);
            return value != null ? value.ToString() : defaultValue;
        }

        private static bool ExtractBool(object source, string dottedPath, bool defaultValue = false)
        {
            var value = ExtractValue(source, dottedPath);
            return value is bool boolValue ? boolValue : defaultValue;
        }

        private static string[] ExtractStringArray(object source, string dottedPath)
        {
            var value = ExtractValue(source, dottedPath);
            if (value == null)
            {
                return Array.Empty<string>();
            }

            var strings = value as IEnumerable<string>;
            if (strings != null)
            {
                return strings.Where(item => item != null).ToArray();
            }

            var array = value as Array;
            if (array == null)
            {
                return Array.Empty<string>();
            }

            return array
                .Cast<object>()
                .Where(item => item != null)
                .Select(item => item.ToString())
                .ToArray();
        }

        private static object ExtractValue(object source, string dottedPath)
        {
            if (source == null || string.IsNullOrWhiteSpace(dottedPath))
            {
                return null;
            }

            object current = source;
            foreach (var part in dottedPath.Split('.'))
            {
                if (current == null)
                {
                    return null;
                }

                var property = current.GetType().GetProperty(part);
                if (property == null)
                {
                    var field = current.GetType().GetField(part);
                    if (field == null)
                    {
                        return null;
                    }

                    current = field.GetValue(current);
                    continue;
                }

                current = property.GetValue(current, null);
            }

            return current;
        }

        private sealed class SmokeSection
        {
            public SmokeSection(object data, IReadOnlyList<string> errors)
            {
                Data = data;
                Errors = errors ?? Array.Empty<string>();
            }

            public object Data { get; }

            public IReadOnlyList<string> Errors { get; }
        }

        private sealed class RequiredReferenceSummary
        {
            public RequiredReferenceSummary(string name, bool exists, string path)
            {
                this.name = name;
                this.exists = exists;
                this.path = path;
            }

            public string name { get; }

            public bool exists { get; }

            public string path { get; }
        }

        private sealed class TopLevelSummary
        {
            public TopLevelSummary(int gridCellCount, int livingUnitCount, int abilityCount, string currentPhase)
            {
                this.gridCellCount = gridCellCount;
                this.livingUnitCount = livingUnitCount;
                this.abilityCount = abilityCount;
                this.currentPhase = currentPhase;
            }

            public int gridCellCount { get; }

            public int livingUnitCount { get; }

            public int abilityCount { get; }

            public string currentPhase { get; }
        }
    }
}
