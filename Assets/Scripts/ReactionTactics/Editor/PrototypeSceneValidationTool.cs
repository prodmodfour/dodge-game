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
    [UnityCliTool(Name = "rt_validate_prototype_scene", Description = "Validate that the main prototype scene has all required content, references, and scenario data assigned.", Group = "reaction-tactics")]
    public static class PrototypeSceneValidationTool
    {
        private const string DefaultScenePath = "Assets/Scenes/MainPrototype.unity";
        private const string DefaultMapPath = "Assets/ScriptableObjects/DefaultPrototypeMap.asset";
        private const string DefaultScenarioPath = "Assets/ScriptableObjects/Scenarios/DefaultSkirmish.asset";
        private const string PrototypeUnitPrefabPath = "Assets/Prefabs/PrototypeUnit.prefab";
        private const string SystemsRootName = "Systems";
        private const string GridRootName = "Grid";
        private const string UnitsRootName = "Units";
        private const string UiRootName = "UI";
        private const string ScenarioRootName = "Scenario";

        private static readonly AbilityRequirement[] RequiredAbilities =
        {
            new AbilityRequirement("move", "Assets/ScriptableObjects/Abilities/Move.asset"),
            new AbilityRequirement("melee_slash", "Assets/ScriptableObjects/Abilities/MeleeSlash.asset"),
            new AbilityRequirement("cone_shot", "Assets/ScriptableObjects/Abilities/ConeShot.asset"),
            new AbilityRequirement("fireball", "Assets/ScriptableObjects/Abilities/Fireball.asset"),
            new AbilityRequirement("brace", "Assets/ScriptableObjects/Abilities/Brace.asset"),
            new AbilityRequirement("pass_reaction", "Assets/ScriptableObjects/Abilities/PassReaction.asset")
        };

        public sealed class Parameters
        {
            [ToolParameter("Scene asset path to validate.", DefaultValue = DefaultScenePath)]
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

                var scene = OpenOrUseScene(scenePath);
                var report = new ValidationReport(scene.path);

                var roots = ValidateRootObjects(scene, report);
                var components = ValidateRequiredComponents(scene, report);
                var assets = ValidateAssets(report);
                var mapSummary = ValidateMap(report, components.GridManager, assets.DefaultMap);
                var scenarioSummary = ValidateScenario(report, components.ScenarioLoader, components.GridManager, assets.DefaultScenario);
                var prefabSummary = ValidateUnitPrefab(report, components.UnitSpawner, assets.PrototypeUnitPrefab);
                ValidateSceneReferences(report, components);
                ValidateAssignedMaterials(report, components.GridTerrainView, assets.PrototypeUnitPrefab);
                ValidateAssignedAbilities(report, assets.AbilitiesByKey, components.ScenarioLoader);

                var data = new
                {
                    scene = scene.path,
                    valid = report.IsValid,
                    errorCount = report.Errors.Count,
                    warningCount = report.Warnings.Count,
                    errors = report.Errors.ToArray(),
                    warnings = report.Warnings.ToArray(),
                    roots,
                    components = components.ToSummary(),
                    assets = assets.ToSummary(),
                    map = mapSummary,
                    scenario = scenarioSummary,
                    unitPrefab = prefabSummary
                };

                if (!report.IsValid)
                {
                    return new ErrorResponse(
                        BuildFailureMessage(report.Errors),
                        data);
                }

                return new SuccessResponse("Prototype scene content validation passed.", data);
            }
            catch (Exception exception)
            {
                return new ErrorResponse($"Failed to validate prototype scene: {exception.Message}");
            }
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

        private static object ValidateRootObjects(Scene scene, ValidationReport report)
        {
            var requiredRootNames = new[]
            {
                SystemsRootName,
                GridRootName,
                UnitsRootName,
                UiRootName,
                ScenarioRootName
            };

            var rootsByName = scene.GetRootGameObjects()
                .GroupBy(root => root.name, StringComparer.Ordinal)
                .ToDictionary(group => group.Key, group => group.ToArray(), StringComparer.Ordinal);
            var rootSummaries = new List<object>();

            foreach (var rootName in requiredRootNames)
            {
                if (!rootsByName.TryGetValue(rootName, out var matches) || matches.Length == 0)
                {
                    report.AddError($"Scene '{scene.path}' is missing required root GameObject '{rootName}'.");
                    rootSummaries.Add(new { name = rootName, exists = false, active = false, duplicateCount = 0 });
                    continue;
                }

                if (matches.Length > 1)
                {
                    report.AddError($"Scene '{scene.path}' contains {matches.Length} root GameObjects named '{rootName}'; expected exactly one.");
                }

                var root = matches[0];
                if (!root.activeInHierarchy)
                {
                    report.AddError($"Required root GameObject '{rootName}' is inactive.");
                }

                rootSummaries.Add(new
                {
                    name = rootName,
                    exists = true,
                    active = root.activeInHierarchy,
                    duplicateCount = matches.Length,
                    childCount = root.transform.childCount
                });
            }

            return new
            {
                required = rootSummaries.ToArray(),
                allRootCount = scene.rootCount,
                allRoots = scene.GetRootGameObjects()
                    .Select(root => root.name)
                    .OrderBy(name => name, StringComparer.Ordinal)
                    .ToArray()
            };
        }

        private static RequiredComponents ValidateRequiredComponents(Scene scene, ValidationReport report)
        {
            var components = new RequiredComponents
            {
                Camera = RequireSingleComponent<Camera>(scene, report, "main camera"),
                DirectionalLight = FindRequiredDirectionalLight(scene, report),
                TacticalCameraController = RequireSingleComponent<TacticalCameraController>(scene, report, "tactical camera controller"),
                GridManager = RequireSingleComponent<GridManager>(scene, report, "grid manager"),
                GridTerrainView = RequireSingleComponent<GridTerrainView>(scene, report, "grid terrain view"),
                GridHighlightManager = RequireSingleComponent<GridHighlightManager>(scene, report, "grid highlight manager"),
                UnitRegistry = RequireSingleComponent<UnitRegistry>(scene, report, "unit registry"),
                UnitSpawner = RequireSingleComponent<UnitSpawner>(scene, report, "unit spawner"),
                ScenarioLoader = RequireSingleComponent<ScenarioLoader>(scene, report, "scenario loader"),
                CombatEventBus = RequireSingleComponent<CombatEventBus>(scene, report, "combat event bus"),
                CombatManager = RequireSingleComponent<CombatManager>(scene, report, "combat manager"),
                AiController = RequireSingleComponent<AiController>(scene, report, "enemy AI controller"),
                GridPicker = RequireSingleComponent<GridPicker>(scene, report, "grid picker"),
                SelectionController = RequireSingleComponent<SelectionController>(scene, report, "selection controller"),
                PlayerCommandRouter = RequireSingleComponent<PlayerCommandRouter>(scene, report, "player command router"),
                HoverGridDebugOverlay = RequireSingleComponent<HoverGridDebugOverlay>(scene, report, "hover debug overlay"),
                CombatHud = RequireSingleComponent<CombatHud>(scene, report, "combat HUD"),
                ActiveActionMenu = RequireSingleComponent<ActiveActionMenu>(scene, report, "active action menu"),
                ReactionMenu = RequireSingleComponent<ReactionMenu>(scene, report, "reaction menu"),
                ActiveMovementPreviewController = RequireSingleComponent<ActiveMovementPreviewController>(scene, report, "active movement preview controller"),
                ActionDangerPreviewController = RequireSingleComponent<ActionDangerPreviewController>(scene, report, "action danger preview controller"),
                ReactionMovementSafetyPreviewController = RequireSingleComponent<ReactionMovementSafetyPreviewController>(scene, report, "reaction movement safety preview controller"),
                CombatLogView = RequireSingleComponent<CombatLogView>(scene, report, "combat log view"),
                CombatEndPanel = RequireSingleComponent<CombatEndPanel>(scene, report, "combat end panel"),
                PrototypeRulesHelpOverlay = RequireSingleComponent<PrototypeRulesHelpOverlay>(scene, report, "prototype rules help overlay")
            };

            ValidateActiveAndEnabled(report, "main camera", components.Camera);
            ValidateActiveAndEnabled(report, "directional light", components.DirectionalLight);
            ValidateActiveAndEnabled(report, "tactical camera controller", components.TacticalCameraController);
            ValidateActiveAndEnabled(report, "grid manager", components.GridManager);
            ValidateActiveAndEnabled(report, "grid terrain view", components.GridTerrainView);
            ValidateActiveAndEnabled(report, "grid highlight manager", components.GridHighlightManager);
            ValidateActiveAndEnabled(report, "unit registry", components.UnitRegistry);
            ValidateActiveAndEnabled(report, "unit spawner", components.UnitSpawner);
            ValidateActiveAndEnabled(report, "scenario loader", components.ScenarioLoader);
            ValidateActiveAndEnabled(report, "combat event bus", components.CombatEventBus);
            ValidateActiveAndEnabled(report, "combat manager", components.CombatManager);
            ValidateActiveAndEnabled(report, "enemy AI controller", components.AiController);
            ValidateActiveAndEnabled(report, "grid picker", components.GridPicker);
            ValidateActiveAndEnabled(report, "selection controller", components.SelectionController);
            ValidateActiveAndEnabled(report, "player command router", components.PlayerCommandRouter);
            ValidateActiveAndEnabled(report, "combat HUD", components.CombatHud);
            ValidateActiveAndEnabled(report, "active action menu", components.ActiveActionMenu);
            ValidateActiveAndEnabled(report, "reaction menu", components.ReactionMenu);
            ValidateActiveAndEnabled(report, "combat log view", components.CombatLogView);
            ValidateActiveAndEnabled(report, "combat end panel", components.CombatEndPanel);
            ValidateActiveAndEnabled(report, "prototype rules help overlay", components.PrototypeRulesHelpOverlay);

            return components;
        }

        private static AssetValidationSummary ValidateAssets(ValidationReport report)
        {
            var defaultMap = LoadRequiredAsset<GridMapDefinition>(report, DefaultMapPath, "default prototype map");
            var defaultScenario = LoadRequiredAsset<ScenarioDefinition>(report, DefaultScenarioPath, "default skirmish scenario");
            var unitPrefab = LoadRequiredAsset<GameObject>(report, PrototypeUnitPrefabPath, "prototype unit prefab");
            var materialPaths = PrototypeVisualsPolishTool.GetTrackedMaterialPaths();
            var materialsByPath = new Dictionary<string, Material>(StringComparer.Ordinal);
            foreach (var materialPath in materialPaths)
            {
                var material = LoadRequiredAsset<Material>(report, materialPath, "prototype material");
                if (material != null)
                {
                    materialsByPath[materialPath] = material;
                }
            }

            var abilitiesByKey = new Dictionary<string, AbilityDefinition>(StringComparer.Ordinal);
            foreach (var requirement in RequiredAbilities)
            {
                var ability = LoadRequiredAsset<AbilityDefinition>(report, requirement.Path, "prototype ability");
                if (ability == null)
                {
                    continue;
                }

                var validationResult = ability.ValidateDefinition();
                if (validationResult.IsFailure)
                {
                    report.AddError($"Ability asset '{requirement.Path}' is invalid: {validationResult.ErrorMessage}");
                }

                if (!string.Equals(ability.AbilityKey, requirement.Key, StringComparison.Ordinal))
                {
                    report.AddError($"Ability asset '{requirement.Path}' has key '{ability.AbilityKey}' but expected '{requirement.Key}'.");
                }

                if (abilitiesByKey.ContainsKey(ability.AbilityKey))
                {
                    report.AddError($"Ability key '{ability.AbilityKey}' is assigned by more than one required ability asset.");
                }
                else
                {
                    abilitiesByKey.Add(ability.AbilityKey, ability);
                }
            }

            return new AssetValidationSummary(defaultMap, defaultScenario, unitPrefab, materialsByPath, abilitiesByKey);
        }

        private static object ValidateMap(ValidationReport report, GridManager gridManager, GridMapDefinition defaultMap)
        {
            GridMapDefinition assignedMap = null;
            var assignedMapPath = string.Empty;
            var cellCount = 0;
            var width = 0;
            var depth = 0;

            if (gridManager == null)
            {
                return new { assigned = false, path = (string)null, width, depth, cellCount };
            }

            assignedMap = gridManager.MapDefinition;
            if (assignedMap == null)
            {
                report.AddError($"{nameof(GridManager)} on '{GetScenePath(gridManager.gameObject)}' has no map definition assigned.");
                return new { assigned = false, path = (string)null, width, depth, cellCount };
            }

            assignedMapPath = AssetDatabase.GetAssetPath(assignedMap);
            if (defaultMap != null && assignedMap != defaultMap)
            {
                report.AddError($"{nameof(GridManager)} is assigned map '{assignedMapPath}' but the default prototype map is '{DefaultMapPath}'.");
            }

            try
            {
                var map = assignedMap.BuildGridMap();
                cellCount = map.AllCells.Count();
                width = assignedMap.Width;
                depth = assignedMap.Depth;
                if (cellCount == 0)
                {
                    report.AddError($"Assigned map '{assignedMapPath}' builds zero cells.");
                }
            }
            catch (Exception exception)
            {
                report.AddError($"Assigned map '{assignedMapPath}' could not build an in-memory grid: {exception.Message}");
            }

            return new
            {
                assigned = true,
                path = assignedMapPath,
                width,
                depth,
                cellCount
            };
        }

        private static object ValidateScenario(
            ValidationReport report,
            ScenarioLoader scenarioLoader,
            GridManager gridManager,
            ScenarioDefinition defaultScenario)
        {
            if (scenarioLoader == null)
            {
                return new { assigned = false, path = (string)null, unitCount = 0, playerCount = 0, enemyCount = 0 };
            }

            var scenario = scenarioLoader.ScenarioDefinition;
            if (scenario == null)
            {
                report.AddError($"{nameof(ScenarioLoader)} on '{GetScenePath(scenarioLoader.gameObject)}' has no scenario definition assigned.");
                return new { assigned = false, path = (string)null, unitCount = 0, playerCount = 0, enemyCount = 0 };
            }

            var scenarioPath = AssetDatabase.GetAssetPath(scenario);
            if (defaultScenario != null && scenario != defaultScenario)
            {
                report.AddError($"{nameof(ScenarioLoader)} is assigned scenario '{scenarioPath}' but the default prototype scenario is '{DefaultScenarioPath}'.");
            }

            var scenarioErrors = scenario.CollectValidationErrors();
            foreach (var error in scenarioErrors)
            {
                report.AddError($"Scenario '{scenarioPath}' validation error: {error}");
            }

            if (gridManager != null
                && gridManager.MapDefinition != null
                && scenario.MapDefinition != null
                && gridManager.MapDefinition != scenario.MapDefinition)
            {
                report.AddError(
                    $"{nameof(GridManager)} map '{AssetDatabase.GetAssetPath(gridManager.MapDefinition)}' does not match scenario map '{AssetDatabase.GetAssetPath(scenario.MapDefinition)}'.");
            }

            var playerCount = 0;
            var enemyCount = 0;
            var spawnCells = new HashSet<GridPosition>();
            var unitSummaries = new List<object>();
            IGridMap scenarioMap = null;
            if (scenario.MapDefinition != null)
            {
                try
                {
                    scenarioMap = scenario.MapDefinition.BuildGridMap();
                }
                catch (Exception exception)
                {
                    report.AddError($"Scenario map '{AssetDatabase.GetAssetPath(scenario.MapDefinition)}' could not build: {exception.Message}");
                }
            }

            for (var i = 0; i < scenario.UnitEntries.Count; i++)
            {
                var entry = scenario.UnitEntries[i];
                if (entry == null)
                {
                    report.AddError($"Scenario '{scenarioPath}' unit entry #{i + 1} is missing.");
                    continue;
                }

                if (entry.Team == TeamId.Player)
                {
                    playerCount++;
                }
                else if (entry.Team == TeamId.Enemy)
                {
                    enemyCount++;
                }

                if (!spawnCells.Add(entry.StartingCell))
                {
                    report.AddError($"Scenario '{scenarioPath}' uses duplicate spawn cell {entry.StartingCell}.");
                }

                if (scenarioMap != null)
                {
                    if (!scenarioMap.TryGetCell(entry.StartingCell, out var cell))
                    {
                        report.AddError($"Scenario unit entry #{i + 1} starts at {entry.StartingCell}, which is not present in the scenario map.");
                    }
                    else if (!cell.Walkable || cell.BlocksMovement)
                    {
                        report.AddError($"Scenario unit entry #{i + 1} starts at blocked or unwalkable cell {entry.StartingCell}.");
                    }
                }

                unitSummaries.Add(new
                {
                    index = i + 1,
                    team = entry.Team.ToString(),
                    stats = entry.Stats != null ? AssetDatabase.GetAssetPath(entry.Stats) : null,
                    statsName = entry.Stats != null ? entry.Stats.DisplayName : null,
                    startingCell = ToPositionSummary(entry.StartingCell),
                    abilityCount = entry.AbilityLoadout.Count,
                    abilities = entry.AbilityLoadout
                        .Where(ability => ability != null)
                        .Select(ability => new
                        {
                            key = ability.AbilityKey,
                            name = ability.DisplayName,
                            path = AssetDatabase.GetAssetPath(ability)
                        })
                        .ToArray()
                });
            }

            if (playerCount == 0)
            {
                report.AddError($"Scenario '{scenarioPath}' has no player-team units.");
            }

            if (enemyCount == 0)
            {
                report.AddError($"Scenario '{scenarioPath}' has no enemy-team units.");
            }

            ValidateScenarioComposition(report, scenario, scenarioPath);

            return new
            {
                assigned = true,
                path = scenarioPath,
                map = scenario.MapDefinition != null ? AssetDatabase.GetAssetPath(scenario.MapDefinition) : null,
                unitCount = scenario.UnitEntries.Count,
                playerCount,
                enemyCount,
                uniqueSpawnCellCount = spawnCells.Count,
                units = unitSummaries.ToArray()
            };
        }

        private static object ValidateUnitPrefab(ValidationReport report, UnitSpawner unitSpawner, GameObject expectedPrefabAsset)
        {
            var prefabPath = expectedPrefabAsset != null ? AssetDatabase.GetAssetPath(expectedPrefabAsset) : PrototypeUnitPrefabPath;
            if (expectedPrefabAsset == null)
            {
                return new { assigned = unitSpawner != null && unitSpawner.UnitPrefab != null, path = (string)null, componentCount = 0 };
            }

            var tacticalUnit = expectedPrefabAsset.GetComponent<TacticalUnit>();
            var pathMover = expectedPrefabAsset.GetComponent<GridPathMover>();
            var collider = expectedPrefabAsset.GetComponent<Collider>();
            var teamVisual = expectedPrefabAsset.GetComponent<UnitTeamVisualView>();
            var highlightView = expectedPrefabAsset.GetComponent<UnitHighlightView>();
            var statusView = expectedPrefabAsset.GetComponent<UnitStatusView>();

            if (tacticalUnit == null)
            {
                report.AddError($"Prototype unit prefab '{prefabPath}' is missing {nameof(TacticalUnit)}.");
            }

            if (pathMover == null)
            {
                report.AddError($"Prototype unit prefab '{prefabPath}' is missing {nameof(GridPathMover)}.");
            }

            if (collider == null)
            {
                report.AddError($"Prototype unit prefab '{prefabPath}' is missing a collider for picking.");
            }

            if (teamVisual == null)
            {
                report.AddError($"Prototype unit prefab '{prefabPath}' is missing {nameof(UnitTeamVisualView)}.");
            }

            if (highlightView == null)
            {
                report.AddError($"Prototype unit prefab '{prefabPath}' is missing {nameof(UnitHighlightView)}.");
            }

            if (statusView == null)
            {
                report.AddError($"Prototype unit prefab '{prefabPath}' is missing {nameof(UnitStatusView)} for HP/AP nameplates.");
            }

            if (unitSpawner != null)
            {
                if (unitSpawner.UnitPrefab == null)
                {
                    report.AddError($"{nameof(UnitSpawner)} on '{GetScenePath(unitSpawner.gameObject)}' has no unit prefab assigned.");
                }
                else
                {
                    var assignedPrefabPath = AssetDatabase.GetAssetPath(unitSpawner.UnitPrefab);
                    if (!string.Equals(assignedPrefabPath, PrototypeUnitPrefabPath, StringComparison.Ordinal))
                    {
                        report.AddError($"{nameof(UnitSpawner)} uses prefab '{assignedPrefabPath}' but expected '{PrototypeUnitPrefabPath}'.");
                    }
                }
            }

            return new
            {
                assigned = unitSpawner != null && unitSpawner.UnitPrefab != null,
                path = prefabPath,
                componentCount = expectedPrefabAsset.GetComponents<Component>().Length,
                hasTacticalUnit = tacticalUnit != null,
                hasGridPathMover = pathMover != null,
                hasCollider = collider != null,
                hasTeamVisual = teamVisual != null,
                hasHighlightView = highlightView != null,
                hasStatusView = statusView != null,
                children = Enumerable.Range(0, expectedPrefabAsset.transform.childCount)
                    .Select(index => expectedPrefabAsset.transform.GetChild(index).name)
                    .OrderBy(name => name, StringComparer.Ordinal)
                    .ToArray()
            };
        }

        private static void ValidateSceneReferences(ValidationReport report, RequiredComponents components)
        {
            RequireReference(report, "TacticalCameraController.GridManager", components.TacticalCameraController, components.TacticalCameraController != null ? components.TacticalCameraController.GridManager : null, components.GridManager);
            RequireReference(report, "GridTerrainView.GridManager", components.GridTerrainView, components.GridTerrainView != null ? components.GridTerrainView.GridManager : null, components.GridManager);
            RequireReference(report, "GridHighlightManager.TerrainView", components.GridHighlightManager, components.GridHighlightManager != null ? components.GridHighlightManager.TerrainView : null, components.GridTerrainView);

            RequireReference(report, "UnitSpawner.Registry", components.UnitSpawner, components.UnitSpawner != null ? components.UnitSpawner.Registry : null, components.UnitRegistry);
            RequireReference(report, "UnitSpawner.GridManager", components.UnitSpawner, components.UnitSpawner != null ? components.UnitSpawner.GridManager : null, components.GridManager);

            RequireReference(report, "ScenarioLoader.GridManager", components.ScenarioLoader, components.ScenarioLoader != null ? components.ScenarioLoader.GridManager : null, components.GridManager);
            RequireReference(report, "ScenarioLoader.UnitSpawner", components.ScenarioLoader, components.ScenarioLoader != null ? components.ScenarioLoader.UnitSpawner : null, components.UnitSpawner);
            RequireReference(report, "ScenarioLoader.CombatManager", components.ScenarioLoader, components.ScenarioLoader != null ? components.ScenarioLoader.CombatManager : null, components.CombatManager);

            RequireReference(report, "CombatManager.UnitRegistry", components.CombatManager, components.CombatManager != null ? components.CombatManager.UnitRegistry : null, components.UnitRegistry);
            RequireReference(report, "CombatManager.GridManager", components.CombatManager, components.CombatManager != null ? components.CombatManager.GridManager : null, components.GridManager);
            RequireReference(report, "CombatManager.InputRouter", components.CombatManager, components.CombatManager != null ? components.CombatManager.InputRouter : null, components.PlayerCommandRouter);
            RequireReference(report, "CombatManager.EventBus", components.CombatManager, components.CombatManager != null ? components.CombatManager.EventBus : null, components.CombatEventBus);
            RequireReference(report, "CombatManager.AiController", components.CombatManager, components.CombatManager != null ? components.CombatManager.AiController : null, components.AiController);

            RequireReference(report, "AiController.CombatManager", components.AiController, components.AiController != null ? components.AiController.CombatManager : null, components.CombatManager);
            if (components.AiController != null)
            {
                if (components.AiController.ControlledTeam != TeamId.Enemy)
                {
                    report.AddError($"{nameof(AiController)} controls {components.AiController.ControlledTeam}; expected {TeamId.Enemy} for the prototype enemy AI.");
                }

                if (!components.AiController.AutomaticDelegationEnabled)
                {
                    report.AddError($"{nameof(AiController)} automatic delegation is disabled.");
                }
            }

            RequireReference(report, "GridPicker.SourceCamera", components.GridPicker, components.GridPicker != null ? components.GridPicker.SourceCamera : null, components.Camera);
            RequireReference(report, "SelectionController.GridPicker", components.SelectionController, components.SelectionController != null ? components.SelectionController.GridPicker : null, components.GridPicker);
            RequireReference(report, "PlayerCommandRouter.SelectionController", components.PlayerCommandRouter, components.PlayerCommandRouter != null ? components.PlayerCommandRouter.SelectionController : null, components.SelectionController);
            RequireReference(report, "PlayerCommandRouter.GridPicker", components.PlayerCommandRouter, components.PlayerCommandRouter != null ? components.PlayerCommandRouter.GridPicker : null, components.GridPicker);
            RequireReference(report, "PlayerCommandRouter.CombatManager", components.PlayerCommandRouter, components.PlayerCommandRouter != null ? components.PlayerCommandRouter.CombatManager : null, components.CombatManager);

            RequireReference(report, "CombatHud.CombatManager", components.CombatHud, components.CombatHud != null ? components.CombatHud.CombatManager : null, components.CombatManager);
            RequireReference(report, "ActiveActionMenu.SelectionController", components.ActiveActionMenu, components.ActiveActionMenu != null ? components.ActiveActionMenu.SelectionController : null, components.SelectionController);
            RequireReference(report, "ActiveActionMenu.CommandRouter", components.ActiveActionMenu, components.ActiveActionMenu != null ? components.ActiveActionMenu.CommandRouter : null, components.PlayerCommandRouter);
            RequireReference(report, "ActiveActionMenu.CombatManager", components.ActiveActionMenu, components.ActiveActionMenu != null ? components.ActiveActionMenu.CombatManager : null, components.CombatManager);
            RequireReference(report, "ReactionMenu.CommandRouter", components.ReactionMenu, components.ReactionMenu != null ? components.ReactionMenu.CommandRouter : null, components.PlayerCommandRouter);
            RequireReference(report, "ReactionMenu.SelectionController", components.ReactionMenu, components.ReactionMenu != null ? components.ReactionMenu.SelectionController : null, components.SelectionController);
            RequireReference(report, "ReactionMenu.CombatManager", components.ReactionMenu, components.ReactionMenu != null ? components.ReactionMenu.CombatManager : null, components.CombatManager);
            RequireReference(report, "CombatLogView.EventBus", components.CombatLogView, components.CombatLogView != null ? components.CombatLogView.EventBus : null, components.CombatEventBus);
            RequireReference(report, "CombatEndPanel.CombatManager", components.CombatEndPanel, components.CombatEndPanel != null ? components.CombatEndPanel.CombatManager : null, components.CombatManager);
            RequireReference(report, "CombatEndPanel.EventBus", components.CombatEndPanel, components.CombatEndPanel != null ? components.CombatEndPanel.EventBus : null, components.CombatEventBus);
            RequireReference(report, "HoverGridDebugOverlay.GridPicker", components.HoverGridDebugOverlay, components.HoverGridDebugOverlay != null ? components.HoverGridDebugOverlay.GridPicker : null, components.GridPicker);
            RequireReference(report, "HoverGridDebugOverlay.GridManager", components.HoverGridDebugOverlay, components.HoverGridDebugOverlay != null ? components.HoverGridDebugOverlay.GridManager : null, components.GridManager);
            RequireReference(report, "HoverGridDebugOverlay.UnitRegistry", components.HoverGridDebugOverlay, components.HoverGridDebugOverlay != null ? components.HoverGridDebugOverlay.UnitRegistry : null, components.UnitRegistry);

            RequireReference(report, "ActiveMovementPreview.SelectionController", components.ActiveMovementPreviewController, components.ActiveMovementPreviewController != null ? components.ActiveMovementPreviewController.SelectionController : null, components.SelectionController);
            RequireReference(report, "ActiveMovementPreview.CombatManager", components.ActiveMovementPreviewController, components.ActiveMovementPreviewController != null ? components.ActiveMovementPreviewController.CombatManager : null, components.CombatManager);
            RequireReference(report, "ActiveMovementPreview.GridManager", components.ActiveMovementPreviewController, components.ActiveMovementPreviewController != null ? components.ActiveMovementPreviewController.GridManager : null, components.GridManager);
            RequireReference(report, "ActiveMovementPreview.UnitRegistry", components.ActiveMovementPreviewController, components.ActiveMovementPreviewController != null ? components.ActiveMovementPreviewController.UnitRegistry : null, components.UnitRegistry);
            RequireReference(report, "ActiveMovementPreview.HighlightManager", components.ActiveMovementPreviewController, components.ActiveMovementPreviewController != null ? components.ActiveMovementPreviewController.HighlightManager : null, components.GridHighlightManager);

            RequireReference(report, "ActionDangerPreview.SelectionController", components.ActionDangerPreviewController, components.ActionDangerPreviewController != null ? components.ActionDangerPreviewController.SelectionController : null, components.SelectionController);
            RequireReference(report, "ActionDangerPreview.GridPicker", components.ActionDangerPreviewController, components.ActionDangerPreviewController != null ? components.ActionDangerPreviewController.GridPicker : null, components.GridPicker);
            RequireReference(report, "ActionDangerPreview.CombatManager", components.ActionDangerPreviewController, components.ActionDangerPreviewController != null ? components.ActionDangerPreviewController.CombatManager : null, components.CombatManager);
            RequireReference(report, "ActionDangerPreview.GridManager", components.ActionDangerPreviewController, components.ActionDangerPreviewController != null ? components.ActionDangerPreviewController.GridManager : null, components.GridManager);
            RequireReference(report, "ActionDangerPreview.UnitRegistry", components.ActionDangerPreviewController, components.ActionDangerPreviewController != null ? components.ActionDangerPreviewController.UnitRegistry : null, components.UnitRegistry);
            RequireReference(report, "ActionDangerPreview.HighlightManager", components.ActionDangerPreviewController, components.ActionDangerPreviewController != null ? components.ActionDangerPreviewController.HighlightManager : null, components.GridHighlightManager);

            RequireReference(report, "ReactionMovementSafetyPreview.SelectionController", components.ReactionMovementSafetyPreviewController, components.ReactionMovementSafetyPreviewController != null ? components.ReactionMovementSafetyPreviewController.SelectionController : null, components.SelectionController);
            RequireReference(report, "ReactionMovementSafetyPreview.CombatManager", components.ReactionMovementSafetyPreviewController, components.ReactionMovementSafetyPreviewController != null ? components.ReactionMovementSafetyPreviewController.CombatManager : null, components.CombatManager);
            RequireReference(report, "ReactionMovementSafetyPreview.GridManager", components.ReactionMovementSafetyPreviewController, components.ReactionMovementSafetyPreviewController != null ? components.ReactionMovementSafetyPreviewController.GridManager : null, components.GridManager);
            RequireReference(report, "ReactionMovementSafetyPreview.UnitRegistry", components.ReactionMovementSafetyPreviewController, components.ReactionMovementSafetyPreviewController != null ? components.ReactionMovementSafetyPreviewController.UnitRegistry : null, components.UnitRegistry);
            RequireReference(report, "ReactionMovementSafetyPreview.HighlightManager", components.ReactionMovementSafetyPreviewController, components.ReactionMovementSafetyPreviewController != null ? components.ReactionMovementSafetyPreviewController.HighlightManager : null, components.GridHighlightManager);
        }

        private static void ValidateAssignedMaterials(ValidationReport report, GridTerrainView terrainView, GameObject unitPrefab)
        {
            if (terrainView != null)
            {
                RequireMaterial(report, "GridTerrainView.WalkableMaterial", terrainView.WalkableMaterial, "Assets/Materials/PrototypeGridWalkable.mat");
                RequireMaterial(report, "GridTerrainView.BlockedMaterial", terrainView.BlockedMaterial, "Assets/Materials/PrototypeGridBlocked.mat");
                RequireMaterial(report, "GridTerrainView.LineOfSightBlockerMaterial", terrainView.LineOfSightBlockerMaterial, "Assets/Materials/PrototypeGridLineOfSightBlocker.mat");
                RequireMaterial(report, "GridTerrainView.HighlightMaterial", terrainView.HighlightMaterial, "Assets/Materials/PrototypeGridHighlight.mat");
                RequireMaterial(report, "GridTerrainView.DangerMaterial", terrainView.DangerMaterial, "Assets/Materials/PrototypeGridDanger.mat");
                RequireMaterial(report, "GridTerrainView.SafeMaterial", terrainView.SafeMaterial, "Assets/Materials/PrototypeGridSafe.mat");
            }

            if (unitPrefab == null)
            {
                return;
            }

            var teamVisual = unitPrefab.GetComponent<UnitTeamVisualView>();
            if (teamVisual != null)
            {
                RequireReference(report, "UnitTeamVisualView.Unit", teamVisual, teamVisual.Unit, unitPrefab.GetComponent<TacticalUnit>());
                RequireReference(report, "UnitTeamVisualView.BodyRenderer", teamVisual, teamVisual.BodyRenderer, FindChildRenderer(unitPrefab.transform, "Body"));
                RequireReference(report, "UnitTeamVisualView.TeamMarkerRenderer", teamVisual, teamVisual.TeamMarkerRenderer, FindChildRenderer(unitPrefab.transform, "TeamSelectionMarker"));
                RequireMaterial(report, "UnitTeamVisualView.PlayerBodyMaterial", teamVisual.PlayerBodyMaterial, "Assets/Materials/PrototypeTeamPlayer.mat");
                RequireMaterial(report, "UnitTeamVisualView.EnemyBodyMaterial", teamVisual.EnemyBodyMaterial, "Assets/Materials/PrototypeTeamEnemy.mat");
                RequireMaterial(report, "UnitTeamVisualView.PlayerMarkerMaterial", teamVisual.PlayerMarkerMaterial, "Assets/Materials/PrototypeTeamPlayerMarker.mat");
                RequireMaterial(report, "UnitTeamVisualView.EnemyMarkerMaterial", teamVisual.EnemyMarkerMaterial, "Assets/Materials/PrototypeTeamEnemyMarker.mat");
            }

            var highlightView = unitPrefab.GetComponent<UnitHighlightView>();
            if (highlightView != null)
            {
                RequireSerializedObjectReference(report, highlightView, "tacticalUnit", "UnitHighlightView.tacticalUnit", unitPrefab.GetComponent<TacticalUnit>());
                RequireSerializedObjectReference(report, highlightView, "activeMarker", "UnitHighlightView.activeMarker");
                RequireSerializedObjectReference(report, highlightView, "activeMarkerRenderer", "UnitHighlightView.activeMarkerRenderer");
                RequireSerializedObjectReference(report, highlightView, "reactionMarker", "UnitHighlightView.reactionMarker");
                RequireSerializedObjectReference(report, highlightView, "reactionMarkerRenderer", "UnitHighlightView.reactionMarkerRenderer");

                var activeRenderer = GetSerializedObjectReference<Renderer>(highlightView, "activeMarkerRenderer");
                var reactionRenderer = GetSerializedObjectReference<Renderer>(highlightView, "reactionMarkerRenderer");
                RequireMaterial(report, "UnitHighlightView active marker material", activeRenderer != null ? activeRenderer.sharedMaterial : null, "Assets/Materials/PrototypeActiveMarker.mat");
                RequireMaterial(report, "UnitHighlightView reaction marker material", reactionRenderer != null ? reactionRenderer.sharedMaterial : null, "Assets/Materials/PrototypeReactionMarker.mat");
            }
        }

        private static void ValidateAssignedAbilities(
            ValidationReport report,
            IReadOnlyDictionary<string, AbilityDefinition> requiredAbilitiesByKey,
            ScenarioLoader scenarioLoader)
        {
            if (scenarioLoader == null || scenarioLoader.ScenarioDefinition == null)
            {
                return;
            }

            var scenario = scenarioLoader.ScenarioDefinition;
            var scenarioPath = AssetDatabase.GetAssetPath(scenario);
            for (var i = 0; i < scenario.UnitEntries.Count; i++)
            {
                var entry = scenario.UnitEntries[i];
                if (entry == null)
                {
                    continue;
                }

                if (entry.AbilityLoadout.Count == 0)
                {
                    report.AddError($"Scenario '{scenarioPath}' unit entry #{i + 1} has no ability loadout assigned.");
                    continue;
                }

                var seenAbilityKeys = new HashSet<string>(StringComparer.Ordinal);
                for (var abilityIndex = 0; abilityIndex < entry.AbilityLoadout.Count; abilityIndex++)
                {
                    var ability = entry.AbilityLoadout[abilityIndex];
                    if (ability == null)
                    {
                        report.AddError($"Scenario '{scenarioPath}' unit entry #{i + 1} ability slot #{abilityIndex + 1} is empty.");
                        continue;
                    }

                    var validationResult = ability.ValidateDefinition();
                    if (validationResult.IsFailure)
                    {
                        report.AddError($"Scenario '{scenarioPath}' unit entry #{i + 1} ability '{ability.name}' is invalid: {validationResult.ErrorMessage}");
                    }

                    if (!seenAbilityKeys.Add(ability.AbilityKey))
                    {
                        report.AddError($"Scenario '{scenarioPath}' unit entry #{i + 1} repeats ability key '{ability.AbilityKey}'.");
                    }

                    if (!requiredAbilitiesByKey.ContainsKey(ability.AbilityKey))
                    {
                        report.AddError($"Scenario '{scenarioPath}' unit entry #{i + 1} uses ability key '{ability.AbilityKey}', which is not one of the required prototype abilities.");
                    }
                }
            }
        }

        private static void ValidateScenarioComposition(ValidationReport report, ScenarioDefinition scenario, string scenarioPath)
        {
            if (scenario == null)
            {
                return;
            }

            foreach (var team in new[] { TeamId.Player, TeamId.Enemy })
            {
                var entries = scenario.UnitEntries
                    .Where(entry => entry != null && entry.Team == team)
                    .ToArray();
                if (entries.Length == 0)
                {
                    continue;
                }

                if (!entries.Any(HasMeleeAction))
                {
                    report.AddError($"Scenario '{scenarioPath}' has no melee-capable {team} unit.");
                }

                if (!entries.Any(HasConeOrAoeAction))
                {
                    report.AddError($"Scenario '{scenarioPath}' has no cone/AoE-capable {team} unit.");
                }
            }
        }

        private static bool HasMeleeAction(ScenarioDefinition.UnitEntry entry)
        {
            return entry.AbilityLoadout.Any(ability => ability != null && ability.CanBeUsedAsAction && ability.Shape == AbilityShape.Melee);
        }

        private static bool HasConeOrAoeAction(ScenarioDefinition.UnitEntry entry)
        {
            return entry.AbilityLoadout.Any(ability => ability != null
                && ability.CanBeUsedAsAction
                && (ability.Shape == AbilityShape.Cone || ability.Shape == AbilityShape.Radius));
        }

        private static T RequireSingleComponent<T>(Scene scene, ValidationReport report, string label)
            where T : Component
        {
            var components = FindComponentsInScene<T>(scene);
            if (components.Count == 0)
            {
                report.AddError($"Scene '{scene.path}' is missing required {label} ({typeof(T).Name}).");
                return null;
            }

            if (components.Count > 1)
            {
                report.AddError(
                    $"Scene '{scene.path}' contains {components.Count} {typeof(T).Name} components for {label}; expected exactly one: "
                    + string.Join(", ", components.Select(component => GetScenePath(component.gameObject))));
            }

            return components[0];
        }

        private static Light FindRequiredDirectionalLight(Scene scene, ValidationReport report)
        {
            var lights = FindComponentsInScene<Light>(scene)
                .Where(light => light != null && light.type == LightType.Directional)
                .ToArray();
            if (lights.Length == 0)
            {
                report.AddError($"Scene '{scene.path}' is missing a directional light.");
                return null;
            }

            if (lights.Length > 1)
            {
                report.AddError(
                    $"Scene '{scene.path}' contains {lights.Length} directional lights; expected exactly one: "
                    + string.Join(", ", lights.Select(light => GetScenePath(light.gameObject))));
            }

            return lights[0];
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

        private static T LoadRequiredAsset<T>(ValidationReport report, string path, string description)
            where T : UnityEngine.Object
        {
            var asset = AssetDatabase.LoadAssetAtPath<T>(path);
            if (asset == null)
            {
                report.AddError($"Required {description} asset was not found at '{path}'.");
            }

            return asset;
        }

        private static void ValidateActiveAndEnabled(ValidationReport report, string label, Component component)
        {
            if (component == null)
            {
                return;
            }

            if (!component.gameObject.activeInHierarchy)
            {
                report.AddError($"Required {label} on '{GetScenePath(component.gameObject)}' is inactive.");
            }

            var behaviour = component as Behaviour;
            if (behaviour != null && !behaviour.enabled)
            {
                report.AddError($"Required {label} on '{GetScenePath(component.gameObject)}' is disabled.");
            }
        }

        private static void RequireReference(
            ValidationReport report,
            string label,
            UnityEngine.Object owner,
            UnityEngine.Object actual,
            UnityEngine.Object expected)
        {
            if (owner == null)
            {
                return;
            }

            if (actual == null)
            {
                report.AddError($"{label} is not assigned on '{DescribeOwner(owner)}'.");
                return;
            }

            if (expected != null && actual != expected)
            {
                report.AddError($"{label} on '{DescribeOwner(owner)}' points to '{DescribeOwner(actual)}' but expected '{DescribeOwner(expected)}'.");
            }
        }

        private static void RequireMaterial(ValidationReport report, string label, Material material, string expectedPath)
        {
            if (material == null)
            {
                report.AddError($"{label} is not assigned.");
                return;
            }

            var actualPath = AssetDatabase.GetAssetPath(material);
            if (!string.Equals(actualPath, expectedPath, StringComparison.Ordinal))
            {
                report.AddError($"{label} is assigned '{actualPath}' but expected '{expectedPath}'.");
            }
        }

        private static void RequireSerializedObjectReference(
            ValidationReport report,
            UnityEngine.Object target,
            string propertyName,
            string label,
            UnityEngine.Object expected = null)
        {
            var actual = GetSerializedObjectReference<UnityEngine.Object>(target, propertyName);
            RequireReference(report, label, target, actual, expected);
        }

        private static T GetSerializedObjectReference<T>(UnityEngine.Object target, string propertyName)
            where T : UnityEngine.Object
        {
            if (target == null)
            {
                return null;
            }

            var serializedObject = new SerializedObject(target);
            var property = serializedObject.FindProperty(propertyName);
            if (property == null)
            {
                return null;
            }

            return property.objectReferenceValue as T;
        }

        private static Renderer FindChildRenderer(Transform parent, string childName)
        {
            var child = parent != null ? parent.Find(childName) : null;
            return child != null ? child.GetComponent<Renderer>() : null;
        }

        private static string BuildFailureMessage(IReadOnlyList<string> errors)
        {
            var preview = string.Join(" ", errors.Take(6));
            if (errors.Count > 6)
            {
                preview += $" (+{errors.Count - 6} more)";
            }

            return $"Prototype scene validation failed with {errors.Count} error(s): {preview}";
        }

        private static string DescribeOwner(UnityEngine.Object owner)
        {
            if (owner == null)
            {
                return "<missing>";
            }

            var component = owner as Component;
            if (component != null)
            {
                return GetSceneOrAssetPath(component.gameObject);
            }

            var gameObject = owner as GameObject;
            if (gameObject != null)
            {
                return GetSceneOrAssetPath(gameObject);
            }

            var assetPath = AssetDatabase.GetAssetPath(owner);
            return string.IsNullOrEmpty(assetPath) ? owner.name : assetPath;
        }

        private static string GetSceneOrAssetPath(GameObject gameObject)
        {
            if (gameObject == null)
            {
                return "<missing>";
            }

            var assetPath = AssetDatabase.GetAssetPath(gameObject);
            return string.IsNullOrEmpty(assetPath) ? GetScenePath(gameObject) : assetPath;
        }

        private static string GetScenePath(GameObject gameObject)
        {
            if (gameObject == null)
            {
                return null;
            }

            var path = gameObject.name;
            var current = gameObject.transform.parent;
            while (current != null)
            {
                path = current.name + "/" + path;
                current = current.parent;
            }

            return path;
        }

        private static object ToComponentSummary(Component component)
        {
            if (component == null)
            {
                return null;
            }

            return new
            {
                path = GetScenePath(component.gameObject),
                active = component.gameObject.activeInHierarchy,
                enabled = !(component is Behaviour behaviour) || behaviour.enabled
            };
        }

        private static object ToPositionSummary(GridPosition position)
        {
            return new
            {
                x = position.X,
                y = position.Y,
                z = position.Z
            };
        }

        private sealed class ValidationReport
        {
            public ValidationReport(string scenePath)
            {
                ScenePath = scenePath;
            }

            public string ScenePath { get; }

            public List<string> Errors { get; } = new List<string>();

            public List<string> Warnings { get; } = new List<string>();

            public bool IsValid
            {
                get { return Errors.Count == 0; }
            }

            public void AddError(string message)
            {
                if (!string.IsNullOrWhiteSpace(message))
                {
                    Errors.Add(message);
                }
            }

            public void AddWarning(string message)
            {
                if (!string.IsNullOrWhiteSpace(message))
                {
                    Warnings.Add(message);
                }
            }
        }

        private sealed class RequiredComponents
        {
            public Camera Camera { get; set; }

            public Light DirectionalLight { get; set; }

            public TacticalCameraController TacticalCameraController { get; set; }

            public GridManager GridManager { get; set; }

            public GridTerrainView GridTerrainView { get; set; }

            public GridHighlightManager GridHighlightManager { get; set; }

            public UnitRegistry UnitRegistry { get; set; }

            public UnitSpawner UnitSpawner { get; set; }

            public ScenarioLoader ScenarioLoader { get; set; }

            public CombatEventBus CombatEventBus { get; set; }

            public CombatManager CombatManager { get; set; }

            public AiController AiController { get; set; }

            public GridPicker GridPicker { get; set; }

            public SelectionController SelectionController { get; set; }

            public PlayerCommandRouter PlayerCommandRouter { get; set; }

            public HoverGridDebugOverlay HoverGridDebugOverlay { get; set; }

            public CombatHud CombatHud { get; set; }

            public ActiveActionMenu ActiveActionMenu { get; set; }

            public ReactionMenu ReactionMenu { get; set; }

            public ActiveMovementPreviewController ActiveMovementPreviewController { get; set; }

            public ActionDangerPreviewController ActionDangerPreviewController { get; set; }

            public ReactionMovementSafetyPreviewController ReactionMovementSafetyPreviewController { get; set; }

            public CombatLogView CombatLogView { get; set; }

            public CombatEndPanel CombatEndPanel { get; set; }

            public PrototypeRulesHelpOverlay PrototypeRulesHelpOverlay { get; set; }

            public object ToSummary()
            {
                return new
                {
                    camera = ToComponentSummary(Camera),
                    directionalLight = ToComponentSummary(DirectionalLight),
                    tacticalCameraController = ToComponentSummary(TacticalCameraController),
                    gridManager = ToComponentSummary(GridManager),
                    gridTerrainView = ToComponentSummary(GridTerrainView),
                    gridHighlightManager = ToComponentSummary(GridHighlightManager),
                    unitRegistry = ToComponentSummary(UnitRegistry),
                    unitSpawner = ToComponentSummary(UnitSpawner),
                    scenarioLoader = ToComponentSummary(ScenarioLoader),
                    combatEventBus = ToComponentSummary(CombatEventBus),
                    combatManager = ToComponentSummary(CombatManager),
                    aiController = ToComponentSummary(AiController),
                    gridPicker = ToComponentSummary(GridPicker),
                    selectionController = ToComponentSummary(SelectionController),
                    playerCommandRouter = ToComponentSummary(PlayerCommandRouter),
                    hoverGridDebugOverlay = ToComponentSummary(HoverGridDebugOverlay),
                    combatHud = ToComponentSummary(CombatHud),
                    activeActionMenu = ToComponentSummary(ActiveActionMenu),
                    reactionMenu = ToComponentSummary(ReactionMenu),
                    activeMovementPreview = ToComponentSummary(ActiveMovementPreviewController),
                    actionDangerPreview = ToComponentSummary(ActionDangerPreviewController),
                    reactionMovementSafetyPreview = ToComponentSummary(ReactionMovementSafetyPreviewController),
                    combatLog = ToComponentSummary(CombatLogView),
                    combatEndPanel = ToComponentSummary(CombatEndPanel),
                    prototypeRulesHelpOverlay = ToComponentSummary(PrototypeRulesHelpOverlay)
                };
            }
        }

        private sealed class AssetValidationSummary
        {
            public AssetValidationSummary(
                GridMapDefinition defaultMap,
                ScenarioDefinition defaultScenario,
                GameObject prototypeUnitPrefab,
                IReadOnlyDictionary<string, Material> materialsByPath,
                IReadOnlyDictionary<string, AbilityDefinition> abilitiesByKey)
            {
                DefaultMap = defaultMap;
                DefaultScenario = defaultScenario;
                PrototypeUnitPrefab = prototypeUnitPrefab;
                MaterialsByPath = materialsByPath;
                AbilitiesByKey = abilitiesByKey;
            }

            public GridMapDefinition DefaultMap { get; }

            public ScenarioDefinition DefaultScenario { get; }

            public GameObject PrototypeUnitPrefab { get; }

            public IReadOnlyDictionary<string, Material> MaterialsByPath { get; }

            public IReadOnlyDictionary<string, AbilityDefinition> AbilitiesByKey { get; }

            public object ToSummary()
            {
                return new
                {
                    defaultMap = DefaultMap != null ? AssetDatabase.GetAssetPath(DefaultMap) : null,
                    defaultScenario = DefaultScenario != null ? AssetDatabase.GetAssetPath(DefaultScenario) : null,
                    prototypeUnitPrefab = PrototypeUnitPrefab != null ? AssetDatabase.GetAssetPath(PrototypeUnitPrefab) : null,
                    materialCount = MaterialsByPath.Count,
                    materials = MaterialsByPath.Keys.OrderBy(path => path, StringComparer.Ordinal).ToArray(),
                    abilityCount = AbilitiesByKey.Count,
                    abilities = AbilitiesByKey.Values
                        .OrderBy(ability => ability.AbilityKey, StringComparer.Ordinal)
                        .Select(ability => new
                        {
                            key = ability.AbilityKey,
                            name = ability.DisplayName,
                            path = AssetDatabase.GetAssetPath(ability)
                        })
                        .ToArray()
                };
            }
        }

        private readonly struct AbilityRequirement
        {
            public AbilityRequirement(string key, string path)
            {
                Key = key;
                Path = path;
            }

            public string Key { get; }

            public string Path { get; }
        }
    }
}
