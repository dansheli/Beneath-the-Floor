using UnityEditor;
using UnityEditor.Build.Reporting;
using UnityEngine;
using System.IO;

public class BuildScript
{
    [MenuItem("Build/Build Windows x64")]
    public static void BuildWindows()
    {
        // Get desktop path
        string desktopPath = System.Environment.GetFolderPath(System.Environment.SpecialFolder.Desktop);
        string buildFolder = Path.Combine(desktopPath, "Beneath the Floor");
        string buildPath = Path.Combine(buildFolder, "Beneath the Floor.exe");

        // Create the build folder if it doesn't exist
        if (!Directory.Exists(buildFolder))
        {
            Directory.CreateDirectory(buildFolder);
        }

        // Get scenes from build settings
        string[] scenes = new string[]
        {
            "Assets/Scenes/PressAnyKeyScene.unity",
            "Assets/Scenes/MainMenuScene.unity",
            "Assets/Scenes/HouseBuilding.unity",
            "Assets/Scenes/DemoEndScene.unity"
        };

        // Build options
        BuildPlayerOptions buildPlayerOptions = new BuildPlayerOptions
        {
            scenes = scenes,
            locationPathName = buildPath,
            target = BuildTarget.StandaloneWindows64,
            options = BuildOptions.None
        };

        Debug.Log($"[BuildScript] Starting build to: {buildPath}");

        // Build the player
        BuildReport report = BuildPipeline.BuildPlayer(buildPlayerOptions);
        BuildSummary summary = report.summary;

        if (summary.result == BuildResult.Succeeded)
        {
            Debug.Log($"[BuildScript] Build succeeded! Size: {summary.totalSize / (1024 * 1024)} MB");
            Debug.Log($"[BuildScript] Build location: {buildFolder}");

            // Open the folder
            EditorUtility.RevealInFinder(buildPath);
        }
        else if (summary.result == BuildResult.Failed)
        {
            Debug.LogError($"[BuildScript] Build failed with {summary.totalErrors} errors");
        }
    }
}
