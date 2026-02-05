using UnityEngine;
using UnityEditor;
using BeneathTheFloor.Lighting;

namespace BeneathTheFloor.Editor
{
    /// <summary>
    /// Sets up the Core Shard prefab with glowing crystal effect.
    /// </summary>
    public class CoreShardSetup
    {
        [MenuItem("BeneathTheFloor/Setup/Add Glow to Core Shard")]
        public static void SetupCoreShard()
        {
            // Find the Core Shard prefab
            string[] guids = AssetDatabase.FindAssets("Core Shard t:Prefab");

            if (guids.Length == 0)
            {
                Debug.LogError("[CoreShardSetup] Could not find Core Shard prefab!");
                return;
            }

            string path = AssetDatabase.GUIDToAssetPath(guids[0]);
            GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(path);

            if (prefab == null)
            {
                Debug.LogError($"[CoreShardSetup] Failed to load prefab at {path}");
                return;
            }

            // Open prefab for editing
            string prefabPath = AssetDatabase.GetAssetPath(prefab);
            GameObject prefabRoot = PrefabUtility.LoadPrefabContents(prefabPath);

            // Check if CrystalGlow already exists
            CrystalGlow existingGlow = prefabRoot.GetComponent<CrystalGlow>();
            if (existingGlow == null)
            {
                existingGlow = prefabRoot.GetComponentInChildren<CrystalGlow>();
            }

            if (existingGlow != null)
            {
                Debug.Log("[CoreShardSetup] CrystalGlow already exists on Core Shard. Updating settings...");
            }
            else
            {
                // Add CrystalGlow component
                existingGlow = prefabRoot.AddComponent<CrystalGlow>();
                Debug.Log("[CoreShardSetup] Added CrystalGlow component to Core Shard");
            }

            // Configure for a mystical core shard appearance
            SerializedObject so = new SerializedObject(existingGlow);

            // Pulse settings - uses material's own emission color
            so.FindProperty("pulseSpeed").floatValue = 0.5f;
            so.FindProperty("minBrightness").floatValue = 0.4f;
            so.FindProperty("maxBrightness").floatValue = 1.3f;

            // Flicker
            so.FindProperty("enableFlicker").boolValue = true;
            so.FindProperty("flickerAmount").floatValue = 0.1f;

            // Light settings
            so.FindProperty("createLight").boolValue = true;
            so.FindProperty("lightIntensity").floatValue = 2f;
            so.FindProperty("lightRange").floatValue = 3f;

            so.ApplyModifiedProperties();

            // Add CoreShardPickup component for interaction
            CoreShardPickup pickup = prefabRoot.GetComponent<CoreShardPickup>();
            if (pickup == null)
            {
                pickup = prefabRoot.AddComponent<CoreShardPickup>();
                Debug.Log("[CoreShardSetup] Added CoreShardPickup component");
            }

            // Make sure there's a collider for interaction
            Collider col = prefabRoot.GetComponent<Collider>();
            if (col == null)
            {
                col = prefabRoot.GetComponentInChildren<Collider>();
            }
            if (col == null)
            {
                // Add a sphere collider
                SphereCollider sphere = prefabRoot.AddComponent<SphereCollider>();
                sphere.radius = 0.5f;
                sphere.isTrigger = false;
                Debug.Log("[CoreShardSetup] Added SphereCollider for interaction");
            }

            // Save prefab
            PrefabUtility.SaveAsPrefabAsset(prefabRoot, prefabPath);
            PrefabUtility.UnloadPrefabContents(prefabRoot);

            Debug.Log($"[CoreShardSetup] Core Shard setup complete! Prefab saved at {prefabPath}");

            // Select the prefab in project window
            Selection.activeObject = prefab;
            EditorGUIUtility.PingObject(prefab);
        }

        [MenuItem("BeneathTheFloor/Setup/Add Glow to Selected Object")]
        public static void AddGlowToSelected()
        {
            GameObject selected = Selection.activeGameObject;
            if (selected == null)
            {
                Debug.LogError("[CoreShardSetup] No GameObject selected!");
                return;
            }

            CrystalGlow glow = selected.GetComponent<CrystalGlow>();
            if (glow == null)
            {
                glow = selected.AddComponent<CrystalGlow>();
                Debug.Log($"[CoreShardSetup] Added CrystalGlow to {selected.name}");
            }
            else
            {
                Debug.Log($"[CoreShardSetup] CrystalGlow already exists on {selected.name}");
            }

            EditorUtility.SetDirty(selected);
        }
    }
}
