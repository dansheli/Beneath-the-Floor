using UnityEditor;
using UnityEditor.Build.Reporting;
using UnityEngine;
using System.IO;
using System.Linq;

namespace BeneathTheFloor.Editor
{
    /// <summary>
    /// Editor script for building Mac version of the game.
    /// Configures proper architecture and settings for Mac compatibility.
    /// </summary>
    public static class MacBuildScript
    {
        private const string APP_NAME = "Beneath the Floor.app";

        // Build to desktop
        private static string GetBuildFolder()
        {
            string desktop = System.Environment.GetFolderPath(System.Environment.SpecialFolder.Desktop);
            return Path.Combine(desktop, "BeneathTheFloor_Mac");
        }

        [MenuItem("Build/Build Mac (Universal)")]
        public static void BuildMacUniversal()
        {
            BuildMac(OSXUniversal: true);
        }

        [MenuItem("Build/Build Mac (Intel Only)")]
        public static void BuildMacIntel()
        {
            BuildMac(OSXUniversal: false);
        }

        private static void BuildMac(bool OSXUniversal)
        {
            Debug.Log("[MacBuild] Starting Mac build process...");

            // Save all assets first
            AssetDatabase.SaveAssets();

            // Get scenes from build settings
            string[] scenes = EditorBuildSettings.scenes
                .Where(s => s.enabled)
                .Select(s => s.path)
                .ToArray();

            if (scenes.Length == 0)
            {
                Debug.LogError("[MacBuild] No scenes in build settings!");
                return;
            }

            Debug.Log($"[MacBuild] Building with {scenes.Length} scenes:");
            foreach (var scene in scenes)
            {
                Debug.Log($"  - {scene}");
            }

            // Create build folder on desktop
            string buildPath = GetBuildFolder();
            if (!Directory.Exists(buildPath))
            {
                Directory.CreateDirectory(buildPath);
                Debug.Log($"[MacBuild] Created build folder: {buildPath}");
            }

            string fullPath = Path.Combine(buildPath, APP_NAME);

            // Configure build options
            BuildPlayerOptions buildOptions = new BuildPlayerOptions
            {
                scenes = scenes,
                locationPathName = fullPath,
                target = BuildTarget.StandaloneOSX,
                options = BuildOptions.None
            };

            // Set architecture - Universal for both Intel and Apple Silicon
            if (OSXUniversal)
            {
                // Universal build supports both Intel and Apple Silicon Macs
                PlayerSettings.SetArchitecture(BuildTargetGroup.Standalone, 2); // 2 = Universal
                Debug.Log("[MacBuild] Architecture: Universal (Intel + Apple Silicon)");
            }
            else
            {
                // Intel only
                PlayerSettings.SetArchitecture(BuildTargetGroup.Standalone, 0); // 0 = Intel 64-bit
                Debug.Log("[MacBuild] Architecture: Intel 64-bit only");
            }

            // Ensure Metal is enabled for Mac (required for modern macOS)
            // Note: On macOS, Metal is the default and preferred graphics API

            Debug.Log("[MacBuild] Starting build...");

            // Perform build
            BuildReport report = BuildPipeline.BuildPlayer(buildOptions);
            BuildSummary summary = report.summary;

            if (summary.result == BuildResult.Succeeded)
            {
                Debug.Log($"[MacBuild] Build succeeded!");
                Debug.Log($"[MacBuild] Output: {fullPath}");
                Debug.Log($"[MacBuild] Size: {summary.totalSize / (1024 * 1024)} MB");
                Debug.Log($"[MacBuild] Time: {summary.totalTime.TotalSeconds:F1} seconds");

                // Open the build folder
                EditorUtility.RevealInFinder(fullPath);
            }
            else if (summary.result == BuildResult.Failed)
            {
                Debug.LogError($"[MacBuild] Build failed!");
                Debug.LogError($"[MacBuild] Errors: {summary.totalErrors}");
                Debug.LogError($"[MacBuild] Warnings: {summary.totalWarnings}");

                // Log individual errors
                foreach (var step in report.steps)
                {
                    foreach (var message in step.messages)
                    {
                        if (message.type == LogType.Error)
                        {
                            Debug.LogError($"[MacBuild] {message.content}");
                        }
                    }
                }
            }
            else
            {
                Debug.LogWarning($"[MacBuild] Build result: {summary.result}");
            }
        }

        [MenuItem("Build/Verify Mac Build Settings")]
        public static void VerifyMacSettings()
        {
            Debug.Log("=== Mac Build Settings Verification ===");

            // Check if Mac build support is installed
            bool macSupport = BuildPipeline.IsBuildTargetSupported(BuildTargetGroup.Standalone, BuildTarget.StandaloneOSX);
            Debug.Log($"Mac Build Support Installed: {(macSupport ? "YES" : "NO - Please install Mac Build Support from Unity Hub!")}");

            if (!macSupport)
            {
                Debug.LogError("[MacBuild] Mac Build Support is NOT installed!");
                Debug.LogError("[MacBuild] Please install it from Unity Hub:");
                Debug.LogError("[MacBuild] 1. Open Unity Hub");
                Debug.LogError("[MacBuild] 2. Go to Installs");
                Debug.LogError("[MacBuild] 3. Click the gear icon on your Unity version");
                Debug.LogError("[MacBuild] 4. Add Modules -> Mac Build Support (Mono)");
                return;
            }

            // Check scenes
            var enabledScenes = EditorBuildSettings.scenes.Where(s => s.enabled).ToList();
            Debug.Log($"Enabled Scenes: {enabledScenes.Count}");
            foreach (var scene in enabledScenes)
            {
                Debug.Log($"  - {scene.path}");
            }

            // Check product name
            Debug.Log($"Product Name: {PlayerSettings.productName}");
            Debug.Log($"Company Name: {PlayerSettings.companyName}");
            Debug.Log($"Bundle Version: {PlayerSettings.bundleVersion}");

            // Check architecture
            int arch = PlayerSettings.GetArchitecture(BuildTargetGroup.Standalone);
            string archName = arch switch
            {
                0 => "Intel 64-bit",
                1 => "Apple Silicon",
                2 => "Universal (Intel + Apple Silicon)",
                _ => $"Unknown ({arch})"
            };
            Debug.Log($"Architecture: {archName}");

            // Check graphics APIs
            var graphicsAPIs = PlayerSettings.GetGraphicsAPIs(BuildTarget.StandaloneOSX);
            Debug.Log($"Graphics APIs: {string.Join(", ", graphicsAPIs)}");

            // Check scripting backend
            var scriptingBackend = PlayerSettings.GetScriptingBackend(BuildTargetGroup.Standalone);
            Debug.Log($"Scripting Backend: {scriptingBackend}");

            Debug.Log("=== End Verification ===");
        }
    }
}
