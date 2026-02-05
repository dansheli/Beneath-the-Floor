using UnityEngine;
using UnityEditor;
using BeneathTheFloor.Player;

namespace BeneathTheFloor.Editor
{
    /// <summary>
    /// Quick setup for FPS Arms without dialogs.
    /// </summary>
    public static class FPArmsQuickSetup
    {
        [MenuItem("Beneath The Floor/Setup/Add Controller to PlayerFP_Arms", false, 103)]
        public static void AddControllerToArms()
        {
            // Find the PlayerFP_Arms object
            GameObject arms = GameObject.Find("PlayerFP_Arms");
            if (arms == null)
            {
                arms = GameObject.Find("v1"); // Original name
            }

            if (arms == null)
            {
                Debug.LogError("[FPArmsQuickSetup] Could not find PlayerFP_Arms or v1 in scene!");
                return;
            }

            // Add controller if not present
            var controller = arms.GetComponent<PlayerFPArmsController>();
            if (controller == null)
            {
                controller = arms.AddComponent<PlayerFPArmsController>();
                Debug.Log("[FPArmsQuickSetup] Added PlayerFPArmsController to " + arms.name);
            }
            else
            {
                Debug.Log("[FPArmsQuickSetup] PlayerFPArmsController already exists on " + arms.name);
            }

            // Disable shadow casting on all renderers
            var renderers = arms.GetComponentsInChildren<Renderer>();
            foreach (var renderer in renderers)
            {
                renderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            }
            Debug.Log($"[FPArmsQuickSetup] Disabled shadows on {renderers.Length} renderers");

            // Mark scene dirty
            UnityEditor.SceneManagement.EditorSceneManager.MarkSceneDirty(
                UnityEngine.SceneManagement.SceneManager.GetActiveScene());

            Selection.activeGameObject = arms;
            Debug.Log("[FPArmsQuickSetup] Setup complete!");
        }

        [MenuItem("Beneath The Floor/Setup/Configure Arms Position (Default FPS)", false, 104)]
        public static void ConfigureArmsPosition()
        {
            GameObject arms = GameObject.Find("PlayerFP_Arms");
            if (arms == null)
            {
                arms = GameObject.Find("v1");
            }

            if (arms == null)
            {
                Debug.LogError("[FPArmsQuickSetup] Could not find PlayerFP_Arms in scene!");
                return;
            }

            // Set default FPS position
            arms.transform.localPosition = new Vector3(0f, -0.5f, 0.3f);
            arms.transform.localRotation = Quaternion.identity;
            arms.transform.localScale = Vector3.one;

            Debug.Log("[FPArmsQuickSetup] Arms position configured for FPS view");

            UnityEditor.SceneManagement.EditorSceneManager.MarkSceneDirty(
                UnityEngine.SceneManagement.SceneManager.GetActiveScene());
        }

        [MenuItem("Beneath The Floor/Setup/Log Arms Hierarchy", false, 130)]
        public static void LogArmsHierarchy()
        {
            GameObject arms = GameObject.Find("PlayerFP_Arms");
            if (arms == null)
            {
                arms = GameObject.Find("v1");
            }

            if (arms == null)
            {
                Debug.LogError("[FPArmsQuickSetup] Could not find PlayerFP_Arms in scene!");
                return;
            }

            Debug.Log("=== FPS Arms Hierarchy ===");
            LogTransformRecursive(arms.transform, "");
        }

        private static void LogTransformRecursive(Transform t, string indent)
        {
            string components = "";
            var comps = t.GetComponents<Component>();
            foreach (var c in comps)
            {
                if (c != null && !(c is Transform))
                {
                    components += $"[{c.GetType().Name}] ";
                }
            }

            Debug.Log($"{indent}{t.name} {components}");

            foreach (Transform child in t)
            {
                LogTransformRecursive(child, indent + "  ");
            }
        }
    }
}
