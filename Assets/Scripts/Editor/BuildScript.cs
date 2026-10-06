using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.Build.Reporting;
using UnityEngine;

namespace TawanOS.EditorTools
{
    /// <summary>
    /// Windows player build (plan task A0). Batch mode:
    /// Unity.exe -batchmode -nographics -quit -projectPath ... -executeMethod TawanOS.EditorTools.BuildScript.BuildWindows -logFile build.log
    /// Builds the scenes that are enabled in Build Settings into Build/Windows/KhwanEuyKhwanMa.exe and
    /// exits with code 1 if the build did not succeed. Build/ is git-ignored.
    /// </summary>
    public static class BuildScript
    {
        public const string OutputExe = "Build/Windows/KhwanEuyKhwanMa.exe";

        // Scenes that must never ship: SampleScene is the unused Unity template scene
        private const string SampleScenePath = "Assets/Scenes/SampleScene.unity";

        [MenuItem("Tools/TawanOS/Build/Windows Build")]
        public static void BuildWindowsFromMenu()
        {
            BuildWindows(exitOnFinish: false);
        }

        // Entry point for -executeMethod
        public static void BuildWindows()
        {
            BuildWindows(exitOnFinish: true);
        }

        [MenuItem("Tools/TawanOS/Build/Remove SampleScene from Build Settings")]
        public static void RemoveSampleScene()
        {
            var kept = EditorBuildSettings.scenes.Where(s => s.path != SampleScenePath).ToArray();
            if (kept.Length == EditorBuildSettings.scenes.Length)
            {
                Debug.Log("[BuildScript] SampleScene is not in Build Settings");
                return;
            }

            EditorBuildSettings.scenes = kept;
            AssetDatabase.SaveAssets();
            Debug.Log($"[BuildScript] Removed SampleScene from Build Settings ({kept.Length} scenes left)");
        }

        private static void BuildWindows(bool exitOnFinish)
        {
            var scenes = EditorBuildSettings.scenes
                .Where(s => s.enabled && s.path != SampleScenePath)
                .Select(s => s.path)
                .ToArray();

            if (scenes.Length == 0)
            {
                Debug.LogError("[BuildScript] No enabled scenes in Build Settings");
                if (exitOnFinish) EditorApplication.Exit(1);
                return;
            }

            string dir = Path.GetDirectoryName(OutputExe);
            if (!string.IsNullOrEmpty(dir)) Directory.CreateDirectory(dir);

            var options = new BuildPlayerOptions
            {
                scenes = scenes,
                locationPathName = OutputExe,
                target = BuildTarget.StandaloneWindows64,
                options = BuildOptions.None
            };

            BuildReport report = BuildPipeline.BuildPlayer(options);
            BuildSummary summary = report.summary;

            Debug.Log($"[BuildScript] Result={summary.result} scenes={scenes.Length} errors={summary.totalErrors} " +
                      $"warnings={summary.totalWarnings} size={summary.totalSize / (1024 * 1024)} MB time={summary.totalTime.TotalSeconds:F0}s output={summary.outputPath}");

            if (exitOnFinish) EditorApplication.Exit(summary.result == BuildResult.Succeeded ? 0 : 1);
        }
    }
}
