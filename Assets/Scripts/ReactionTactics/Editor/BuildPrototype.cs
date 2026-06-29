using System;
using System.IO;
using UnityEditor;
using UnityEditor.Build.Reporting;
using UnityEngine;

namespace ReactionTactics.Editor
{
    public static class BuildPrototype
    {
        private const string ScenePath = "Assets/Scenes/MainPrototype.unity";
        private const string OutputPath = "Build/ReactionTacticsPrototype";

        public static void PerformBuild()
        {
            if (AssetDatabase.LoadAssetAtPath<SceneAsset>(ScenePath) == null)
            {
                throw new FileNotFoundException($"Build scene was not found: {ScenePath}", ScenePath);
            }

            var outputDirectory = Path.GetDirectoryName(OutputPath);
            if (!string.IsNullOrEmpty(outputDirectory))
            {
                Directory.CreateDirectory(outputDirectory);
            }

            var buildOptions = new BuildPlayerOptions
            {
                scenes = new[] { ScenePath },
                locationPathName = OutputPath,
                target = BuildTarget.StandaloneLinux64,
                options = BuildOptions.None
            };

            Debug.Log($"Starting Reaction Tactics prototype build: scene={ScenePath}, target={buildOptions.target}, output={OutputPath}");

            var report = BuildPipeline.BuildPlayer(buildOptions);
            LogBuildSummary(report);

            if (report.summary.result != BuildResult.Succeeded)
            {
                throw new InvalidOperationException($"Reaction Tactics prototype build failed with result {report.summary.result}.");
            }
        }

        private static void LogBuildSummary(BuildReport report)
        {
            var summary = report.summary;
            Debug.Log($"Reaction Tactics prototype build result: {summary.result}");
            Debug.Log(
                "Reaction Tactics prototype build summary: " +
                $"outputPath={summary.outputPath}, " +
                $"platform={summary.platform}, " +
                $"totalSizeBytes={summary.totalSize}, " +
                $"totalTime={summary.totalTime}, " +
                $"warnings={summary.totalWarnings}, " +
                $"errors={summary.totalErrors}");
        }
    }
}
