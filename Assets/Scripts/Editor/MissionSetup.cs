using UnityEngine;
using UnityEditor;
using BeneathTheFloor.Missions;

namespace BeneathTheFloor.Editor
{
    /// <summary>
    /// Editor utility to set up mission system in the scene.
    /// </summary>
    public class MissionSetup
    {
        [MenuItem("Tools/Beneath The Floor/Setup Mission System")]
        public static void SetupMissions()
        {
            // Find or create MissionSystem object
            GameObject missionSystem = GameObject.Find("MissionSystem");
            if (missionSystem == null)
            {
                missionSystem = new GameObject("MissionSystem");
                Debug.Log("[MissionSetup] Created MissionSystem object");
            }

            // Add MissionManager
            var missionManager = missionSystem.GetComponent<MissionManager>();
            if (missionManager == null)
            {
                missionManager = missionSystem.AddComponent<MissionManager>();
                Debug.Log("[MissionSetup] Added MissionManager");
            }

            // Add MissionUI
            var missionUI = missionSystem.GetComponent<MissionUI>();
            if (missionUI == null)
            {
                missionUI = missionSystem.AddComponent<MissionUI>();
                Debug.Log("[MissionSetup] Added MissionUI");
            }

            // Try to load and assign mission data assets
            var missions = AssetDatabase.FindAssets("t:MissionData", new[] { "Assets/GameData/Missions" });
            if (missions.Length > 0)
            {
                Debug.Log($"[MissionSetup] Found {missions.Length} mission(s) in Assets/GameData/Missions");
                Debug.Log("[MissionSetup] Assign them to MissionManager.missions in the Inspector");
            }
            else
            {
                Debug.LogWarning("[MissionSetup] No MissionData assets found in Assets/GameData/Missions");
            }

            // Mark scene dirty
            UnityEditor.SceneManagement.EditorSceneManager.MarkSceneDirty(
                UnityEditor.SceneManagement.EditorSceneManager.GetActiveScene());

            Debug.Log("[MissionSetup] Mission system setup complete!");
            Debug.Log("[MissionSetup] Don't forget to assign missions to MissionManager in the Inspector.");

            // Select the MissionSystem object
            Selection.activeGameObject = missionSystem;
        }

        [MenuItem("Tools/Beneath The Floor/Remove Mission System")]
        public static void RemoveMissions()
        {
            // Remove MissionSystem
            GameObject missionSystem = GameObject.Find("MissionSystem");
            if (missionSystem != null)
            {
                Object.DestroyImmediate(missionSystem);
                Debug.Log("[MissionSetup] Removed MissionSystem object");
            }

            UnityEditor.SceneManagement.EditorSceneManager.MarkSceneDirty(
                UnityEditor.SceneManagement.EditorSceneManager.GetActiveScene());

            Debug.Log("[MissionSetup] Mission system removed.");
        }
    }
}
