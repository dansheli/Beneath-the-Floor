using UnityEngine;
using UnityEditor;
using BeneathTheFloor.Missions;

namespace BeneathTheFloor.Editor
{
    /// <summary>
    /// Editor utility to set up Mission 9 (Follow the Signal) in the scene.
    /// </summary>
    public static class Mission9Setup
    {
        private const string MISSION_PATH = "Assets/GameData/Missions/";

        [MenuItem("Beneath The Floor/Missions/Setup Mission 9 - Follow Signal")]
        public static void SetupMission9()
        {
            CreateMission9Asset();

            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();

            Debug.Log("[Mission9Setup] Mission 9 setup complete!");
            EditorUtility.DisplayDialog("Mission 9 Setup Complete",
                "Mission 9 asset created:\n\n" +
                "- Mission_09_FollowSignal\n\n" +
                "Completion trigger: LocationReached (RadarTarget_TestRoom)\n\n" +
                "Remember to add this mission to the MissionManager's mission list!",
                "OK");
        }

        private static void CreateMission9Asset()
        {
            string path = MISSION_PATH + "Mission_09_FollowSignal.asset";

            // Check if already exists
            var existing = AssetDatabase.LoadAssetAtPath<MissionData>(path);
            if (existing != null)
            {
                Debug.Log("[Mission9Setup] Mission 9 already exists, skipping.");
                return;
            }

            var mission = ScriptableObject.CreateInstance<MissionData>();
            mission.missionId = "mission_09_follow_signal";
            mission.missionName = "Follow the Signal";
            mission.objectiveText = "Try reaching whatever the signal is pointing at.";
            mission.helpText = "Follow the signal.";
            mission.completionTrigger = MissionTriggerType.LocationReached;
            mission.showMarker = false; // Radar guides the player, not UI marker
            mission.markerTargetName = "RadarTarget_TestRoom";
            mission.rewardType = MissionRewardType.None;

            AssetDatabase.CreateAsset(mission, path);
            Debug.Log($"[Mission9Setup] Created: {path}");
        }

        [MenuItem("Beneath The Floor/Missions/Add Mission 9 to Manager")]
        public static void AddMission9ToManager()
        {
            var missionManager = Object.FindFirstObjectByType<MissionManager>();
            if (missionManager == null)
            {
                Debug.LogError("[Mission9Setup] MissionManager not found in scene!");
                return;
            }

            // Load mission asset
            var mission9 = AssetDatabase.LoadAssetAtPath<MissionData>(MISSION_PATH + "Mission_09_FollowSignal.asset");

            if (mission9 == null)
            {
                Debug.LogError("[Mission9Setup] Mission 9 asset not found! Run 'Setup Mission 9' first.");
                return;
            }

            // Get the missions list via SerializedObject
            SerializedObject so = new SerializedObject(missionManager);
            SerializedProperty missionsProp = so.FindProperty("missions");

            // Check if mission is already added
            bool hasMission = false;
            for (int i = 0; i < missionsProp.arraySize; i++)
            {
                var element = missionsProp.GetArrayElementAtIndex(i);
                var missionRef = element.objectReferenceValue as MissionData;
                if (missionRef != null && missionRef.missionId == mission9.missionId)
                {
                    hasMission = true;
                    break;
                }
            }

            if (!hasMission)
            {
                missionsProp.InsertArrayElementAtIndex(missionsProp.arraySize);
                missionsProp.GetArrayElementAtIndex(missionsProp.arraySize - 1).objectReferenceValue = mission9;
                so.ApplyModifiedProperties();

                // Mark scene dirty
                UnityEditor.SceneManagement.EditorSceneManager.MarkSceneDirty(
                    UnityEditor.SceneManagement.EditorSceneManager.GetActiveScene());

                Debug.Log("[Mission9Setup] Added Mission 9 to MissionManager.");
                Selection.activeGameObject = missionManager.gameObject;
            }
            else
            {
                Debug.Log("[Mission9Setup] Mission 9 is already in MissionManager.");
            }
        }
    }
}
