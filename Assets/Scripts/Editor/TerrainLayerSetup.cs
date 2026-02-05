using UnityEngine;
using UnityEditor;
using System.IO;
using BeneathTheFloor.Digging;

namespace BeneathTheFloor.Editor
{
    /// <summary>
    /// Editor script to set up the terrain layer system.
    /// Creates default config and adds components to scene.
    /// </summary>
    public static class TerrainLayerSetup
    {
        private const string CONFIG_PATH = "Assets/Resources/TerrainLayerSettings.asset";

        [MenuItem("Beneath The Floor/Setup/Create Terrain Layer Config")]
        public static void CreateTerrainLayerSettings()
        {
            // Ensure Resources folder exists
            if (!Directory.Exists("Assets/Resources"))
            {
                Directory.CreateDirectory("Assets/Resources");
                AssetDatabase.Refresh();
            }

            // Check if config already exists
            var existingConfig = AssetDatabase.LoadAssetAtPath<TerrainLayerSettings>(CONFIG_PATH);
            if (existingConfig != null)
            {
                Debug.Log("[TerrainLayerSetup] Config already exists. Selecting it.");
                Selection.activeObject = existingConfig;
                EditorGUIUtility.PingObject(existingConfig);
                return;
            }

            // Create new config
            var config = ScriptableObject.CreateInstance<TerrainLayerSettings>();

            // Save to Resources folder
            AssetDatabase.CreateAsset(config, CONFIG_PATH);
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();

            Debug.Log($"[TerrainLayerSetup] Created TerrainLayerSettings at {CONFIG_PATH}");
            Debug.Log("[TerrainLayerSetup] Default layers:");
            Debug.Log("  - Topsoil (0 to -8m) - Rich brown, hardness 1.0");
            Debug.Log("  - Clay (-8 to -20m) - Orange-brown, hardness 1.8");
            Debug.Log("  - Gravel (-20 to -40m) - Gray-brown, hardness 2.5");
            Debug.Log("  - Bedrock (-40m+) - Dark gray, hardness 4.0");

            Selection.activeObject = config;
            EditorGUIUtility.PingObject(config);
        }

        [MenuItem("Beneath The Floor/Setup/Add Layer System to Scene")]
        public static void AddLayerSystemToScene()
        {
            // Check if already exists
            var existingTracker = Object.FindObjectOfType<ToolEffectivenessTracker>();
            var existingUpdater = Object.FindObjectOfType<TerrainLayerShaderUpdater>();

            if (existingTracker != null && existingUpdater != null)
            {
                Debug.Log("[TerrainLayerSetup] Layer system components already exist in scene.");
                Selection.activeGameObject = existingTracker.gameObject;
                return;
            }

            // Create parent object
            GameObject layerSystem = new GameObject("TerrainLayerSystem");
            Undo.RegisterCreatedObjectUndo(layerSystem, "Create Terrain Layer System");

            // Add components
            if (existingTracker == null)
            {
                layerSystem.AddComponent<ToolEffectivenessTracker>();
            }

            if (existingUpdater == null)
            {
                var updater = layerSystem.AddComponent<TerrainLayerShaderUpdater>();

                // Try to find and assign config
                var config = Resources.Load<TerrainLayerSettings>("TerrainLayerSettings");
                if (config != null)
                {
                    // Use SerializedObject to set the field
                    var so = new SerializedObject(updater);
                    var configProp = so.FindProperty("layerConfig");
                    if (configProp != null)
                    {
                        configProp.objectReferenceValue = config;
                        so.ApplyModifiedProperties();
                    }
                }
            }

            Debug.Log("[TerrainLayerSetup] Added layer system components to scene.");
            Debug.Log("  - ToolEffectivenessTracker: Tracks tool exhaustion");
            Debug.Log("  - TerrainLayerShaderUpdater: Syncs layer colors to shader");

            Selection.activeGameObject = layerSystem;
        }

        [MenuItem("Beneath The Floor/Setup/Update Tool Profiles with Hardness")]
        public static void UpdateToolProfilesWithHardness()
        {
            // Find all DigToolProfile assets
            string[] guids = AssetDatabase.FindAssets("t:DigToolProfile");

            int updated = 0;
            foreach (string guid in guids)
            {
                string path = AssetDatabase.GUIDToAssetPath(guid);
                var profile = AssetDatabase.LoadAssetAtPath<Digging.DigToolProfile>(path);

                if (profile != null)
                {
                    // Determine hardness based on name/tier
                    float hardness = DetermineHardnessFromProfile(profile);

                    // Use SerializedObject to modify
                    var so = new SerializedObject(profile);
                    var hardnessProp = so.FindProperty("hardnessRating");

                    if (hardnessProp != null)
                    {
                        float currentHardness = hardnessProp.floatValue;

                        // Only update if using default value
                        if (Mathf.Approximately(currentHardness, 1.5f) || currentHardness < 0.5f)
                        {
                            hardnessProp.floatValue = hardness;
                            so.ApplyModifiedProperties();
                            EditorUtility.SetDirty(profile);
                            updated++;
                            Debug.Log($"[TerrainLayerSetup] Updated {profile.name}: hardnessRating = {hardness}");
                        }
                    }
                }
            }

            if (updated > 0)
            {
                AssetDatabase.SaveAssets();
                Debug.Log($"[TerrainLayerSetup] Updated {updated} tool profiles with hardness ratings.");
            }
            else
            {
                Debug.Log("[TerrainLayerSetup] No tool profiles needed updating (already configured or not found).");
            }
        }

        private static float DetermineHardnessFromProfile(Digging.DigToolProfile profile)
        {
            string name = profile.name.ToLower();
            string id = profile.toolId?.ToLower() ?? "";

            // Check for tier indicators
            if (name.Contains("tier3") || name.Contains("tier_3") || name.Contains("t3") ||
                id.Contains("tier3") || id.Contains("t3"))
            {
                return 4.0f;
            }
            else if (name.Contains("tier2") || name.Contains("tier_2") || name.Contains("t2") ||
                     id.Contains("tier2") || id.Contains("t2"))
            {
                return 2.5f;
            }
            else if (name.Contains("tier1") || name.Contains("tier_1") || name.Contains("t1") ||
                     id.Contains("tier1") || id.Contains("t1"))
            {
                return 1.5f;
            }

            // Default to Tier 1
            return 1.5f;
        }

        [MenuItem("Beneath The Floor/Setup/Set Tool Depth Limits")]
        public static void SetToolDepthLimits()
        {
            // Find all DigToolProfile assets
            string[] guids = AssetDatabase.FindAssets("t:DigToolProfile");

            int updated = 0;
            foreach (string guid in guids)
            {
                string path = AssetDatabase.GUIDToAssetPath(guid);
                var profile = AssetDatabase.LoadAssetAtPath<Digging.DigToolProfile>(path);

                if (profile != null)
                {
                    // Determine depth limit based on tier
                    float depthLimit = DetermineDepthLimitFromProfile(profile);

                    // Use SerializedObject to modify
                    var so = new SerializedObject(profile);
                    var depthProp = so.FindProperty("maxDigDepth");

                    if (depthProp != null)
                    {
                        depthProp.floatValue = depthLimit;
                        so.ApplyModifiedProperties();
                        EditorUtility.SetDirty(profile);
                        updated++;
                        Debug.Log($"[TerrainLayerSetup] Updated {profile.name}: maxDigDepth = {depthLimit}m");
                    }
                }
            }

            if (updated > 0)
            {
                AssetDatabase.SaveAssets();
                Debug.Log($"[TerrainLayerSetup] Updated {updated} tool profiles with depth limits.");
                Debug.Log("  Tier 1: 10m max depth");
                Debug.Log("  Tier 2: 25m max depth");
                Debug.Log("  Tier 3: 50m max depth (or unlimited)");
            }
            else
            {
                Debug.Log("[TerrainLayerSetup] No tool profiles found to update.");
            }
        }

        private static float DetermineDepthLimitFromProfile(Digging.DigToolProfile profile)
        {
            string name = profile.name.ToLower();
            string id = profile.toolId?.ToLower() ?? "";

            // Check for tier indicators
            if (name.Contains("tier3") || name.Contains("tier_3") || name.Contains("t3") ||
                id.Contains("tier3") || id.Contains("t3"))
            {
                return 50f; // Tier 3: 50m depth (or use 0 for unlimited)
            }
            else if (name.Contains("tier2") || name.Contains("tier_2") || name.Contains("t2") ||
                     id.Contains("tier2") || id.Contains("t2"))
            {
                return 25f; // Tier 2: 25m depth
            }
            else if (name.Contains("tier1") || name.Contains("tier_1") || name.Contains("t1") ||
                     id.Contains("tier1") || id.Contains("t1"))
            {
                return 10f; // Tier 1: 10m depth
            }

            // Default to Tier 1 depth
            return 10f;
        }

        [MenuItem("Beneath The Floor/Setup/Validate Layer System")]
        public static void ValidateLayerSystem()
        {
            Debug.Log("=== Terrain Layer System Validation ===");

            // Check config
            var config = Resources.Load<TerrainLayerSettings>("TerrainLayerSettings");
            if (config == null)
            {
                Debug.LogError("MISSING: TerrainLayerSettings not found in Resources. Run 'Create Terrain Layer Config'.");
            }
            else
            {
                Debug.Log($"OK: TerrainLayerSettings found with {config.layers?.Length ?? 0} layers");
                if (config.layers != null)
                {
                    foreach (var layer in config.layers)
                    {
                        Debug.Log($"  - {layer.layerName}: Y {layer.depthStart} to {layer.depthEnd}, hardness {layer.hardness}");
                    }
                }
            }

            // Check scene components
            var tracker = Object.FindObjectOfType<ToolEffectivenessTracker>();
            if (tracker == null)
            {
                Debug.LogWarning("MISSING: ToolEffectivenessTracker not in scene. Run 'Add Layer System to Scene'.");
            }
            else
            {
                Debug.Log("OK: ToolEffectivenessTracker found in scene");
            }

            var updater = Object.FindObjectOfType<TerrainLayerShaderUpdater>();
            if (updater == null)
            {
                Debug.LogWarning("MISSING: TerrainLayerShaderUpdater not in scene. Run 'Add Layer System to Scene'.");
            }
            else
            {
                Debug.Log("OK: TerrainLayerShaderUpdater found in scene");
            }

            // Check tool profiles
            string[] guids = AssetDatabase.FindAssets("t:DigToolProfile");
            Debug.Log($"Found {guids.Length} tool profiles:");

            foreach (string guid in guids)
            {
                string path = AssetDatabase.GUIDToAssetPath(guid);
                var profile = AssetDatabase.LoadAssetAtPath<Digging.DigToolProfile>(path);
                if (profile != null)
                {
                    var so = new SerializedObject(profile);
                    var hardnessProp = so.FindProperty("hardnessRating");
                    var depthProp = so.FindProperty("maxDigDepth");
                    float hardness = hardnessProp?.floatValue ?? 1.5f;
                    float maxDepth = depthProp?.floatValue ?? 0f;
                    string depthStr = maxDepth > 0 ? $"{maxDepth}m" : "unlimited";
                    Debug.Log($"  - {profile.name}: hardness={hardness}, maxDepth={depthStr}");
                }
            }

            Debug.Log("=== End Validation ===");
        }
    }
}
