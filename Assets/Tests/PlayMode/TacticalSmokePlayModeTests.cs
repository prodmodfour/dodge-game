using System.Collections;
using System.Collections.Generic;
using NUnit.Framework;
using ReactionTactics.Grid;
using ReactionTactics.Scenarios;
using ReactionTactics.Turns;
using ReactionTactics.UI;
using ReactionTactics.Units;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;

public sealed class TacticalSmokePlayModeTests
{
    private const string MainPrototypeSceneName = "MainPrototype";
    private const int ScenarioLoadFrameBudget = 60;
    private const int StabilityFrameCount = 5;

    [UnityTest]
    public IEnumerator MainPrototypeStartsWithCoreSystemsAndLivingTeams()
    {
        var errors = new List<string>();
        void CaptureErrors(string condition, string stackTrace, LogType type)
        {
            if (type != LogType.Error && type != LogType.Exception && type != LogType.Assert)
            {
                return;
            }

            errors.Add($"[{type}] {condition}\n{stackTrace}");
        }

        Application.logMessageReceived += CaptureErrors;

        var loadOperation = SceneManager.LoadSceneAsync(MainPrototypeSceneName, LoadSceneMode.Single);
        var loadStarted = loadOperation != null;
        if (loadStarted)
        {
            while (!loadOperation.isDone)
            {
                yield return null;
            }
        }

        UnitRegistry registry = null;
        for (var frame = 0; frame < ScenarioLoadFrameBudget; frame++)
        {
            registry = Object.FindAnyObjectByType<UnitRegistry>();
            if (registry != null && registry.LivingCount > 0)
            {
                break;
            }

            yield return null;
        }

        for (var frame = 0; frame < StabilityFrameCount; frame++)
        {
            yield return null;
        }

        Application.logMessageReceived -= CaptureErrors;

        Assert.IsTrue(loadStarted, $"Expected to start loading scene '{MainPrototypeSceneName}'. Ensure it is registered in build settings.");
        Assert.That(SceneManager.GetActiveScene().name, Is.EqualTo(MainPrototypeSceneName));
        Assert.That(errors, Is.Empty, "MainPrototype logged errors while starting: " + string.Join("\n", errors));

        var gridManager = Object.FindAnyObjectByType<GridManager>();
        var combatManager = Object.FindAnyObjectByType<CombatManager>();
        var scenarioLoader = Object.FindAnyObjectByType<ScenarioLoader>();
        var combatHud = Object.FindAnyObjectByType<CombatHud>();
        var uiRoot = GameObject.Find("UI");
        var camera = Camera.main != null
            ? Camera.main
            : Object.FindAnyObjectByType<Camera>();

        Assert.IsNotNull(gridManager, "MainPrototype should contain a GridManager.");
        Assert.IsNotNull(gridManager.CurrentMap, "GridManager should build a runtime map when the scene starts.");
        Assert.Greater(gridManager.CurrentMap.AllCells.Count, 0, "Runtime grid map should contain cells.");

        Assert.IsNotNull(combatManager, "MainPrototype should contain a CombatManager.");
        Assert.That(combatManager.CurrentState.Phase, Is.Not.EqualTo(CombatPhase.NotStarted), "Combat should start after the scenario loads.");
        Assert.IsNotNull(registry, "MainPrototype should contain a UnitRegistry.");
        Assert.IsNotNull(scenarioLoader, "MainPrototype should contain a ScenarioLoader.");
        Assert.IsNotNull(scenarioLoader.ScenarioDefinition, "ScenarioLoader should reference the default scenario asset.");
        Assert.Greater(scenarioLoader.SpawnedUnitCount, 1, "ScenarioLoader should spawn multiple units from scenario data.");
        Assert.IsNotNull(uiRoot, "MainPrototype should contain the UI root object.");
        Assert.IsNotNull(combatHud, "MainPrototype should contain the combat HUD UI component.");
        Assert.IsNotNull(camera, "MainPrototype should contain an active camera.");
        Assert.IsTrue(camera.enabled, "The scene camera should be enabled.");

        var livingUnits = registry.GetLivingUnits();
        Assert.Greater(livingUnits.Count, 1, "Scenario load should create multiple living units.");
        Assert.GreaterOrEqual(CountLivingTeams(livingUnits), 2, "Scenario load should create living units on at least two teams.");
    }

    private static int CountLivingTeams(IReadOnlyList<TacticalUnit> livingUnits)
    {
        var teams = new HashSet<TeamId>();
        for (var i = 0; i < livingUnits.Count; i++)
        {
            var unit = livingUnits[i];
            if (unit != null && unit.IsAlive)
            {
                teams.Add(unit.Team);
            }
        }

        return teams.Count;
    }
}
