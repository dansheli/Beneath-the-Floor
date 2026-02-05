using UnityEngine;
using UnityEditor;
using BeneathTheFloor.ResourceSystem;

namespace BeneathTheFloor.Editor
{
    /// <summary>
    /// Editor utilities for the Resource System.
    /// </summary>
    public static class ResourceSystemSetupEditor
    {
        [MenuItem("Tools/Beneath The Floor/Resource System/Create Default Config")]
        public static void CreateDefaultConfig()
        {
            // Check if already exists
            var existing = Resources.Load<ResourceSystemConfig>("ResourceSystemConfig");
            if (existing != null)
            {
                Debug.Log("[ResourceSystemSetupEditor] ResourceSystemConfig already exists in Resources folder.");
                Selection.activeObject = existing;
                return;
            }

            // Ensure Resources folder exists
            if (!AssetDatabase.IsValidFolder("Assets/Resources"))
            {
                AssetDatabase.CreateFolder("Assets", "Resources");
            }

            // Create config asset
            var config = ScriptableObject.CreateInstance<ResourceSystemConfig>();

            // Set default values (these match the code defaults)
            config.baseDustPerMass = 10f;
            config.dustSellValue = 1f;
            config.depthDustMultipliers = new float[] { 1f, 1.5f, 2f, 2.5f, 3f, 4f };
            config.nodesPerChunk = 8; // Reasonable density per chunk
            config.minNodeRadius = 0.8f;
            config.maxNodeRadius = 1.5f;
            config.nodeRevealSampleCount = 12;
            config.breakExposureThreshold = 0.70f; // 70% exposed to crumble
            config.minNodeDepth = 0f;
            config.worldSeed = 0; // Random
            config.tierDropCounts = new int[] { 3, 5, 8, 12 };
            config.tierWeightsByDepth = new TierWeightsByLayer[]
            {
                new TierWeightsByLayer { weights = new float[] { 0.8f, 0.15f, 0.05f, 0f } },
                new TierWeightsByLayer { weights = new float[] { 0.6f, 0.3f, 0.08f, 0.02f } },
                new TierWeightsByLayer { weights = new float[] { 0.4f, 0.4f, 0.15f, 0.05f } },
                new TierWeightsByLayer { weights = new float[] { 0.2f, 0.4f, 0.3f, 0.1f } },
                new TierWeightsByLayer { weights = new float[] { 0.1f, 0.3f, 0.4f, 0.2f } },
                new TierWeightsByLayer { weights = new float[] { 0.05f, 0.2f, 0.4f, 0.35f } },
            };

            // Save asset
            string path = "Assets/Resources/ResourceSystemConfig.asset";
            AssetDatabase.CreateAsset(config, path);
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();

            Debug.Log($"[ResourceSystemSetupEditor] Created ResourceSystemConfig at {path}");
            Selection.activeObject = config;
        }

        [MenuItem("Tools/Beneath The Floor/Resource System/Add Setup To Scene")]
        public static void AddSetupToScene()
        {
            // Check if already exists
            var existing = Object.FindObjectOfType<ResourceSystemSetup>();
            if (existing != null)
            {
                Debug.Log("[ResourceSystemSetupEditor] ResourceSystemSetup already exists in scene.");
                Selection.activeGameObject = existing.gameObject;
                return;
            }

            // Create GameObject
            var go = new GameObject("ResourceSystemSetup");
            go.AddComponent<ResourceSystemSetup>();

            // Select it
            Selection.activeGameObject = go;

            Debug.Log("[ResourceSystemSetupEditor] Added ResourceSystemSetup to scene.");

            // Mark scene dirty
            UnityEditor.SceneManagement.EditorSceneManager.MarkSceneDirty(
                UnityEditor.SceneManagement.EditorSceneManager.GetActiveScene());
        }

        [MenuItem("Tools/Beneath The Floor/Resource System/Setup Full System")]
        public static void SetupFullSystem()
        {
            CreateDefaultConfig();
            AddSetupToScene();
            Debug.Log("[ResourceSystemSetupEditor] Full resource system setup complete!");
        }

        [MenuItem("Tools/Beneath The Floor/Resource System/Delete Save File")]
        public static void DeleteSaveFile()
        {
            string savePath = System.IO.Path.Combine(Application.persistentDataPath, "resources_v3.json");

            if (System.IO.File.Exists(savePath))
            {
                System.IO.File.Delete(savePath);
                Debug.Log($"[ResourceSystemSetupEditor] Deleted save file: {savePath}");
            }
            else
            {
                Debug.Log($"[ResourceSystemSetupEditor] No save file found at: {savePath}");
            }
        }

        [MenuItem("Tools/Beneath The Floor/Resource System/Show Save Path")]
        public static void ShowSavePath()
        {
            string savePath = System.IO.Path.Combine(Application.persistentDataPath, "resources_v3.json");
            Debug.Log($"[ResourceSystemSetupEditor] Save path: {savePath}");
            Debug.Log($"[ResourceSystemSetupEditor] File exists: {System.IO.File.Exists(savePath)}");
        }

        [MenuItem("Tools/Beneath The Floor/Resource System/Force Update Config Values")]
        public static void ForceUpdateConfigValues()
        {
            var config = Resources.Load<ResourceSystemConfig>("ResourceSystemConfig");
            if (config == null)
            {
                Debug.LogError("[ResourceSystemSetupEditor] No ResourceSystemConfig found! Run 'Create Default Config' first.");
                return;
            }

            // Force update to new values
            config.nodesPerChunk = 8; // Reduced from 60 - more reasonable density
            config.minNodeRadius = 0.8f;
            config.maxNodeRadius = 1.5f;
            config.breakExposureThreshold = 0.70f; // 70% exposed to crumble
            config.minNodeDepth = 0f;
            config.nodeRevealSampleCount = 12;
            config.tierDropCounts = new int[] { 3, 5, 8, 12 };

            EditorUtility.SetDirty(config);
            AssetDatabase.SaveAssets();

            Debug.Log("[ResourceSystemSetupEditor] Config FORCE UPDATED:");
            Debug.Log($"  nodesPerChunk = {config.nodesPerChunk}");
            Debug.Log($"  minNodeRadius = {config.minNodeRadius}");
            Debug.Log($"  maxNodeRadius = {config.maxNodeRadius}");
            Debug.Log($"  breakExposureThreshold = {config.breakExposureThreshold}");
            Debug.Log($"  minNodeDepth = {config.minNodeDepth}");

            Selection.activeObject = config;
        }
    }
}
