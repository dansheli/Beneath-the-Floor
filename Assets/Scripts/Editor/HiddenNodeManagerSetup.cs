using UnityEngine;
using UnityEditor;
using BeneathTheFloor.ResourceSystem;

namespace BeneathTheFloor.Editor
{
    public static class HiddenNodeManagerSetup
    {
        [MenuItem("Tools/Beneath The Floor/Resource System/Add HiddenNodeManager to Scene")]
        public static void AddHiddenNodeManager()
        {
            // Check if one already exists
            var existing = Object.FindObjectOfType<HiddenNodeManager>();
            if (existing != null)
            {
                Debug.Log("[HiddenNodeManagerSetup] HiddenNodeManager already exists in scene. Selecting it.");
                Selection.activeGameObject = existing.gameObject;
                return;
            }

            // Create new GameObject
            GameObject go = new GameObject("HiddenNodeManager");
            var manager = go.AddComponent<HiddenNodeManager>();

            // Try to load config
            var config = Resources.Load<ResourceSystemConfig>("ResourceSystemConfig");
            if (config != null)
            {
                // Config will be auto-loaded by the manager in Start()
                Debug.Log("[HiddenNodeManagerSetup] ResourceSystemConfig found in Resources folder.");
            }
            else
            {
                Debug.LogWarning("[HiddenNodeManagerSetup] No ResourceSystemConfig found. Create one at Assets/Resources/ResourceSystemConfig.asset");
            }

            // Also add ProximityCuller if not present
            EnsureProximityCuller();

            // Register undo
            Undo.RegisterCreatedObjectUndo(go, "Add HiddenNodeManager");

            // Select the new object
            Selection.activeGameObject = go;

            Debug.Log("[HiddenNodeManagerSetup] Created HiddenNodeManager. Position it where you want (usually near GuidedPitController).");
        }

        [MenuItem("Tools/Beneath The Floor/Resource System/Add ProximityCuller to Scene")]
        public static void AddProximityCuller()
        {
            EnsureProximityCuller();
        }

        private static void EnsureProximityCuller()
        {
            var existing = Object.FindObjectOfType<ProximityCuller>();
            if (existing != null)
            {
                Debug.Log("[Setup] ProximityCuller already exists in scene.");
                Selection.activeGameObject = existing.gameObject;
                return;
            }

            GameObject go = new GameObject("ProximityCuller");
            go.AddComponent<ProximityCuller>();
            Undo.RegisterCreatedObjectUndo(go, "Add ProximityCuller");
            Selection.activeGameObject = go;
            Debug.Log("[Setup] Created ProximityCuller in scene.");
        }
    }
}
