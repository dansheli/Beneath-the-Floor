using UnityEngine;
using UnityEditor;
using BeneathTheFloor.Missions;
using BeneathTheFloor.Tools;

namespace BeneathTheFloor.Editor
{
    /// <summary>
    /// Editor utility to set up Mission 8 (Radar Discovery) in the scene.
    /// </summary>
    public static class Mission8Setup
    {
        private const string MISSION_PATH = "Assets/GameData/Missions/";

        [MenuItem("Beneath The Floor/Missions/Setup Mission 8 - Radar Discovery")]
        public static void SetupMission8()
        {
            // Create all mission assets
            CreateMission8aAsset();
            CreateMission8bAsset();
            CreateMission8cAsset();

            // Setup the radar pickup on the table
            SetupRadarPickup();

            // Refresh asset database
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();

            Debug.Log("[Mission8Setup] Mission 8 setup complete!");
            Debug.Log("[Mission8Setup] Don't forget to add the new missions to the MissionManager's mission list!");
            EditorUtility.DisplayDialog("Mission 8 Setup Complete",
                "Mission 8 assets created:\n\n" +
                "- Mission_08a_ReturnToWorkshop\n" +
                "- Mission_08b_PickupRadar\n" +
                "- Mission_08c_ActivateRadar\n\n" +
                "RadarPickup component added to EMF_Modern_T2 on GrandpasTable.\n\n" +
                "Remember to add these missions to the MissionManager's mission list in order!",
                "OK");
        }

        private static void CreateMission8aAsset()
        {
            string path = MISSION_PATH + "Mission_08a_ReturnToWorkshop.asset";

            // Check if already exists
            var existing = AssetDatabase.LoadAssetAtPath<MissionData>(path);
            if (existing != null)
            {
                Debug.Log("[Mission8Setup] Mission 8a already exists, skipping.");
                return;
            }

            var mission = ScriptableObject.CreateInstance<MissionData>();
            mission.missionId = "mission_08a_return_workshop";
            mission.missionName = "Return to Workshop";
            mission.objectiveText = "Maybe you should take another look at your grandfather's workshop.";
            mission.completionTrigger = MissionTriggerType.LocationReached;
            mission.showMarker = true;
            mission.markerTargetName = "GrandpasTable";
            mission.rewardType = MissionRewardType.None;

            AssetDatabase.CreateAsset(mission, path);
            Debug.Log($"[Mission8Setup] Created: {path}");
        }

        private static void CreateMission8bAsset()
        {
            string path = MISSION_PATH + "Mission_08b_PickupRadar.asset";

            // Check if already exists
            var existing = AssetDatabase.LoadAssetAtPath<MissionData>(path);
            if (existing != null)
            {
                Debug.Log("[Mission8Setup] Mission 8b already exists, skipping.");
                return;
            }

            var mission = ScriptableObject.CreateInstance<MissionData>();
            mission.missionId = "mission_08b_pickup_radar";
            mission.missionName = "Pick Up Radar";
            mission.objectiveText = "What is this tool?\n\nWhat is this radar doing here?";
            mission.completionTrigger = MissionTriggerType.RadarPickedUp;
            mission.showMarker = true;
            mission.markerTargetName = "EMF_Modern_T2";
            mission.rewardType = MissionRewardType.UnlockRadar;
            mission.completionMessage = "Signal detected.\n\nThe device seems to react to something below.";
            mission.completionMessageDuration = 4f;

            AssetDatabase.CreateAsset(mission, path);
            Debug.Log($"[Mission8Setup] Created: {path}");
        }

        private static void CreateMission8cAsset()
        {
            string path = MISSION_PATH + "Mission_08c_ActivateRadar.asset";

            // Check if already exists
            var existing = AssetDatabase.LoadAssetAtPath<MissionData>(path);
            if (existing != null)
            {
                Debug.Log("[Mission8Setup] Mission 8c already exists, skipping.");
                return;
            }

            var mission = ScriptableObject.CreateInstance<MissionData>();
            mission.missionId = "mission_08c_activate_radar";
            mission.missionName = "Activate Radar";
            mission.objectiveText = "Turn the device on.";
            mission.helpText = "Press Q to activate";
            mission.completionTrigger = MissionTriggerType.RadarActivated;
            mission.showMarker = false;
            mission.rewardType = MissionRewardType.None;

            AssetDatabase.CreateAsset(mission, path);
            Debug.Log($"[Mission8Setup] Created: {path}");
        }

        private static void SetupRadarPickup()
        {
            // Find the EMF prefab on GrandpasTable
            GameObject grandpasTable = GameObject.Find("GrandpasTable");
            if (grandpasTable == null)
            {
                Debug.LogWarning("[Mission8Setup] GrandpasTable not found in scene!");
                return;
            }

            // Find EMF_Modern_T2 under GrandpasTable
            Transform emfTransform = FindChildRecursive(grandpasTable.transform, "EMF_Modern_T2");
            if (emfTransform == null)
            {
                Debug.LogWarning("[Mission8Setup] EMF_Modern_T2 not found under GrandpasTable!");
                return;
            }

            // Add RadarPickup component if not already present
            var radarPickup = emfTransform.GetComponent<RadarPickup>();
            if (radarPickup == null)
            {
                radarPickup = emfTransform.gameObject.AddComponent<RadarPickup>();
                Debug.Log("[Mission8Setup] Added RadarPickup component to EMF_Modern_T2");
            }
            else
            {
                Debug.Log("[Mission8Setup] RadarPickup component already exists on EMF_Modern_T2");
            }

            // Mark scene dirty
            UnityEditor.SceneManagement.EditorSceneManager.MarkSceneDirty(
                UnityEditor.SceneManagement.EditorSceneManager.GetActiveScene());

            Selection.activeGameObject = emfTransform.gameObject;
        }

        private static Transform FindChildRecursive(Transform parent, string name)
        {
            foreach (Transform child in parent)
            {
                if (child.name == name)
                    return child;

                Transform found = FindChildRecursive(child, name);
                if (found != null)
                    return found;
            }
            return null;
        }

        [MenuItem("Beneath The Floor/Missions/Add Mission 8 to Manager")]
        public static void AddMission8ToManager()
        {
            var missionManager = Object.FindFirstObjectByType<MissionManager>();
            if (missionManager == null)
            {
                Debug.LogError("[Mission8Setup] MissionManager not found in scene!");
                return;
            }

            // Load mission assets
            var mission8a = AssetDatabase.LoadAssetAtPath<MissionData>(MISSION_PATH + "Mission_08a_ReturnToWorkshop.asset");
            var mission8b = AssetDatabase.LoadAssetAtPath<MissionData>(MISSION_PATH + "Mission_08b_PickupRadar.asset");
            var mission8c = AssetDatabase.LoadAssetAtPath<MissionData>(MISSION_PATH + "Mission_08c_ActivateRadar.asset");

            if (mission8a == null || mission8b == null || mission8c == null)
            {
                Debug.LogError("[Mission8Setup] Mission 8 assets not found! Run 'Setup Mission 8' first.");
                return;
            }

            // Get the missions list via SerializedObject
            SerializedObject so = new SerializedObject(missionManager);
            SerializedProperty missionsProp = so.FindProperty("missions");

            // Check if missions are already added
            bool has8a = false, has8b = false, has8c = false;
            for (int i = 0; i < missionsProp.arraySize; i++)
            {
                var element = missionsProp.GetArrayElementAtIndex(i);
                var missionRef = element.objectReferenceValue as MissionData;
                if (missionRef != null)
                {
                    if (missionRef.missionId == mission8a.missionId) has8a = true;
                    if (missionRef.missionId == mission8b.missionId) has8b = true;
                    if (missionRef.missionId == mission8c.missionId) has8c = true;
                }
            }

            int addedCount = 0;

            if (!has8a)
            {
                missionsProp.InsertArrayElementAtIndex(missionsProp.arraySize);
                missionsProp.GetArrayElementAtIndex(missionsProp.arraySize - 1).objectReferenceValue = mission8a;
                addedCount++;
            }

            if (!has8b)
            {
                missionsProp.InsertArrayElementAtIndex(missionsProp.arraySize);
                missionsProp.GetArrayElementAtIndex(missionsProp.arraySize - 1).objectReferenceValue = mission8b;
                addedCount++;
            }

            if (!has8c)
            {
                missionsProp.InsertArrayElementAtIndex(missionsProp.arraySize);
                missionsProp.GetArrayElementAtIndex(missionsProp.arraySize - 1).objectReferenceValue = mission8c;
                addedCount++;
            }

            so.ApplyModifiedProperties();

            // Mark scene dirty
            UnityEditor.SceneManagement.EditorSceneManager.MarkSceneDirty(
                UnityEditor.SceneManagement.EditorSceneManager.GetActiveScene());

            if (addedCount > 0)
            {
                Debug.Log($"[Mission8Setup] Added {addedCount} mission(s) to MissionManager.");
                Selection.activeGameObject = missionManager.gameObject;
            }
            else
            {
                Debug.Log("[Mission8Setup] All Mission 8 missions are already in MissionManager.");
            }
        }
    }
}
