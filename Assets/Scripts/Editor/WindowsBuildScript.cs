using UnityEditor;
using UnityEditor.Build.Reporting;
using UnityEngine;
using System.IO;
using System.Linq;

namespace BeneathTheFloor.Editor
{
    public static class WindowsBuildScript
    {
        private const string EXE_NAME = "Beneath the Floor.exe";

        private static string GetBuildFolder()
        {
            string desktop = System.Environment.GetFolderPath(System.Environment.SpecialFolder.Desktop);
            return Path.Combine(desktop, "Beneath the Floor");
        }

        [MenuItem("Build/Build Windows")]
        public static void BuildWindows()
        {
            AssetDatabase.SaveAssets();

            string[] scenes = EditorBuildSettings.scenes
                .Where(s => s.enabled)
                .Select(s => s.path)
                .ToArray();

            if (scenes.Length == 0)
            {
                Debug.LogError("[WindowsBuild] No scenes in build settings!");
                return;
            }

            string buildPath = GetBuildFolder();
            if (!Directory.Exists(buildPath))
            {
                Directory.CreateDirectory(buildPath);
            }

            string fullPath = Path.Combine(buildPath, EXE_NAME);

            BuildPlayerOptions buildOptions = new BuildPlayerOptions
            {
                scenes = scenes,
                locationPathName = fullPath,
                target = BuildTarget.StandaloneWindows64,
                options = BuildOptions.None
            };

            BuildReport report = BuildPipeline.BuildPlayer(buildOptions);
            BuildSummary summary = report.summary;

            if (summary.result == BuildResult.Succeeded)
            {
                Debug.Log($"[WindowsBuild] Build succeeded! Output: {fullPath} ({summary.totalSize / (1024 * 1024)} MB, {summary.totalTime.TotalSeconds:F1}s)");
                EditorUtility.RevealInFinder(fullPath);
            }
            else
            {
                Debug.LogError($"[WindowsBuild] Build failed with {summary.totalErrors} errors.");
                foreach (var step in report.steps)
                {
                    foreach (var message in step.messages)
                    {
                        if (message.type == LogType.Error)
                            Debug.LogError($"[WindowsBuild] {message.content}");
                    }
                }
            }
        }
    }
}
