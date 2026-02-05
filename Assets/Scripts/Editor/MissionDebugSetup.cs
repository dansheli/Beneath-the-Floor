using UnityEngine;
using UnityEditor;

namespace BeneathTheFloor.Editor
{
    public static class MissionDebugSetup
    {
        [MenuItem("Beneath The Floor/Debug/Add Mission Skip Tool")]
        public static void AddMissionSkipTool()
        {
            // Check if already exists
            var existing = Object.FindFirstObjectByType<DebugTools.MissionDebugSkip>();
            if (existing != null)
            {
                Debug.Log("[MissionDebugSetup] Mission Skip Tool already exists!");
                Selection.activeGameObject = existing.gameObject;
                return;
            }

            // Create new object
            GameObject obj = new GameObject("MissionDebugSkip");
            obj.AddComponent<DebugTools.MissionDebugSkip>();

            // Mark scene dirty
            UnityEditor.SceneManagement.EditorSceneManager.MarkSceneDirty(
                UnityEditor.SceneManagement.EditorSceneManager.GetActiveScene());

            Selection.activeGameObject = obj;

            Debug.Log("[MissionDebugSetup] Mission Skip Tool added!");
            Debug.Log("[MissionDebugSetup] Press F9 to skip current mission");
            Debug.Log("[MissionDebugSetup] Press F10 to open skip menu");
        }
    }
}
