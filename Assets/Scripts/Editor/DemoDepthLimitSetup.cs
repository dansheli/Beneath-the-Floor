using UnityEngine;
using UnityEditor;
using BeneathTheFloor.GameFlow;

namespace BeneathTheFloor.Editor
{
    /// <summary>
    /// Editor utility to setup the demo depth limit trigger.
    /// </summary>
    public static class DemoDepthLimitSetup
    {
        [MenuItem("Tools/Beneath The Floor/Demo/Setup Depth Limit Trigger")]
        public static void SetupDepthLimitTrigger()
        {
            // Check if already exists
            var existing = Object.FindFirstObjectByType<DemoDepthLimitTrigger>();
            if (existing != null)
            {
                Selection.activeGameObject = existing.gameObject;
                EditorGUIUtility.PingObject(existing);
                EditorUtility.DisplayDialog("Demo Depth Limit Trigger",
                    "DemoDepthLimitTrigger already exists in the scene.\n\n" +
                    "Selected the existing component.",
                    "OK");
                return;
            }

            // Create new GameObject with the trigger
            GameObject triggerObj = new GameObject("DemoDepthLimitTrigger");
            var trigger = triggerObj.AddComponent<DemoDepthLimitTrigger>();

            // Register undo
            Undo.RegisterCreatedObjectUndo(triggerObj, "Create Demo Depth Limit Trigger");

            // Select it
            Selection.activeGameObject = triggerObj;
            EditorGUIUtility.PingObject(triggerObj);

            Debug.Log("[DemoDepthLimitSetup] Created DemoDepthLimitTrigger in scene.");
            EditorUtility.DisplayDialog("Demo Depth Limit Trigger",
                "Created DemoDepthLimitTrigger!\n\n" +
                "This will show the demo end screen when the player with the Drill Pike " +
                "(Tool 4) tries to dig beyond 50 meters.\n\n" +
                "Settings:\n" +
                "- Final Tool Index: 3 (Drill Pike)\n" +
                "- Demo End Scene: DemoEndScene\n" +
                "- Trigger Once: true",
                "OK");
        }

        [MenuItem("Tools/Beneath The Floor/Demo/Remove Depth Limit Trigger")]
        public static void RemoveDepthLimitTrigger()
        {
            var existing = Object.FindFirstObjectByType<DemoDepthLimitTrigger>();
            if (existing != null)
            {
                Undo.DestroyObjectImmediate(existing.gameObject);
                Debug.Log("[DemoDepthLimitSetup] Removed DemoDepthLimitTrigger from scene.");
            }
            else
            {
                EditorUtility.DisplayDialog("Demo Depth Limit Trigger",
                    "No DemoDepthLimitTrigger found in the scene.",
                    "OK");
            }
        }
    }
}
