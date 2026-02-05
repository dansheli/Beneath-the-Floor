using UnityEngine;
using UnityEditor;
using BeneathTheFloor.Logistics;

namespace BeneathTheFloor.Editor
{
    public static class LogisticsRobotSetup
    {
        [MenuItem("BeneathTheFloor/Setup/Setup Logistics Robot")]
        public static void Setup()
        {
            // --- Find scene objects ---
            GameObject robotObj = GameObject.Find("M3_Collector");
            GameObject dockObj = GameObject.Find("Platform_Picking_Robot");
            GameObject containerObj = GameObject.Find("Box_container_Picking_Robot");

            if (robotObj == null)
            {
                Debug.LogError("[LogisticsRobotSetup] Could not find 'M3_Collector' in scene!");
                return;
            }
            if (dockObj == null)
            {
                Debug.LogWarning("[LogisticsRobotSetup] Could not find 'Platform_Picking_Robot' -- dock will not be set up.");
            }
            if (containerObj == null)
            {
                Debug.LogWarning("[LogisticsRobotSetup] Could not find 'Box_container_Picking_Robot' -- container will not be set up.");
            }

            Undo.RegisterCompleteObjectUndo(robotObj, "Setup Logistics Robot");
            if (dockObj != null) Undo.RegisterCompleteObjectUndo(dockObj, "Setup Logistics Dock");
            if (containerObj != null) Undo.RegisterCompleteObjectUndo(containerObj, "Setup Logistics Container");

            // --- Config asset ---
            string configPath = "Assets/GameData/LogisticsRobotConfig.asset";
            LogisticsRobotConfig config = AssetDatabase.LoadAssetAtPath<LogisticsRobotConfig>(configPath);
            if (config == null)
            {
                config = ScriptableObject.CreateInstance<LogisticsRobotConfig>();
                AssetDatabase.CreateAsset(config, configPath);
                Debug.Log($"[LogisticsRobotSetup] Created config asset at {configPath}");
            }
            EditorUtility.SetDirty(config);
            AssetDatabase.SaveAssets();

            // --- Setup Robot (M3_Collector) ---
            // Rigidbody - kinematic
            var rigidbody = robotObj.GetComponent<Rigidbody>();
            if (rigidbody == null)
                rigidbody = Undo.AddComponent<Rigidbody>(robotObj);
            rigidbody.isKinematic = true;
            rigidbody.useGravity = false;

            // Collider for interaction raycasts — use a large sphere for easy targeting
            // Remove any existing box collider
            var oldBoxCol = robotObj.GetComponent<BoxCollider>();
            if (oldBoxCol != null)
                Undo.DestroyObjectImmediate(oldBoxCol);

            var sphereCol = robotObj.GetComponent<SphereCollider>();
            if (sphereCol == null)
                sphereCol = Undo.AddComponent<SphereCollider>(robotObj);
            sphereCol.isTrigger = false;
            sphereCol.radius = 0.8f;
            sphereCol.center = new Vector3(0f, 0.4f, 0f);

            // Core components
            if (robotObj.GetComponent<LogisticsBattery>() == null)
                Undo.AddComponent<LogisticsBattery>(robotObj);
            if (robotObj.GetComponent<RobotCargoBuffer>() == null)
                Undo.AddComponent<RobotCargoBuffer>(robotObj);
            if (robotObj.GetComponent<LootScanner>() == null)
                Undo.AddComponent<LootScanner>(robotObj);
            if (robotObj.GetComponent<LogisticsArmAnimator>() == null)
                Undo.AddComponent<LogisticsArmAnimator>(robotObj);

            var controller = robotObj.GetComponent<LogisticsRobotController>();
            if (controller == null)
                controller = Undo.AddComponent<LogisticsRobotController>(robotObj);

            // HUD + Marker
            if (robotObj.GetComponent<LogisticsRobotHUD>() == null)
                Undo.AddComponent<LogisticsRobotHUD>(robotObj);
            if (robotObj.GetComponent<LogisticsRobotMarker>() == null)
                Undo.AddComponent<LogisticsRobotMarker>(robotObj);

            // --- Setup Dock ---
            LogisticsRobotDock dockComp = null;
            if (dockObj != null)
            {
                if (dockObj.GetComponent<Collider>() == null)
                {
                    BoxCollider dockCol = Undo.AddComponent<BoxCollider>(dockObj);
                    dockCol.size = new Vector3(1.5f, 1f, 1.5f);
                    dockCol.center = new Vector3(0f, 0.5f, 0f);
                }

                Transform dockPoint = dockObj.transform.Find("DockPoint");
                if (dockPoint == null)
                {
                    GameObject dockPointObj = new GameObject("DockPoint");
                    Undo.RegisterCreatedObjectUndo(dockPointObj, "Create DockPoint");
                    dockPointObj.transform.SetParent(dockObj.transform, false);
                    dockPointObj.transform.localPosition = new Vector3(0f, 0.1f, 0f);
                    dockPoint = dockPointObj.transform;
                }

                dockComp = dockObj.GetComponent<LogisticsRobotDock>();
                if (dockComp == null)
                    dockComp = Undo.AddComponent<LogisticsRobotDock>(dockObj);

                var dockSO = new SerializedObject(dockComp);
                dockSO.FindProperty("dockPoint").objectReferenceValue = dockPoint;
                dockSO.FindProperty("robot").objectReferenceValue = controller;
                dockSO.ApplyModifiedProperties();
            }

            // --- Setup Container ---
            DropOffContainer containerComp = null;
            if (containerObj != null)
            {
                if (containerObj.GetComponent<BoxCollider>() == null)
                {
                    BoxCollider cCol = Undo.AddComponent<BoxCollider>(containerObj);
                    cCol.size = new Vector3(1.5f, 1f, 1f);
                    cCol.center = new Vector3(0f, 0.5f, 0f);
                }

                containerComp = containerObj.GetComponent<DropOffContainer>();
                if (containerComp == null)
                    containerComp = Undo.AddComponent<DropOffContainer>(containerObj);

                // Add the UI component too
                if (containerObj.GetComponent<DropOffContainerUI>() == null)
                    Undo.AddComponent<DropOffContainerUI>(containerObj);
            }

            // --- Wire controller serialized references ---
            var ctrlSO = new SerializedObject(controller);
            ctrlSO.FindProperty("config").objectReferenceValue = config;
            if (dockComp != null)
                ctrlSO.FindProperty("dock").objectReferenceValue = dockComp;
            if (containerComp != null)
                ctrlSO.FindProperty("container").objectReferenceValue = containerComp;
            ctrlSO.ApplyModifiedProperties();

            // --- Set layer to Robot (11) ---
            int robotLayer = LayerMask.NameToLayer("Robot");
            if (robotLayer >= 0)
            {
                SetLayerRecursive(robotObj, robotLayer);
                Debug.Log($"[LogisticsRobotSetup] Set M3_Collector to layer {robotLayer} ('Robot') recursively.");
            }
            else
            {
                Debug.LogWarning("[LogisticsRobotSetup] Layer 'Robot' not found -- set layer 11 manually.");
            }

            // --- Add UI manager objects ---
            GameObject cmdUIObj = GameObject.Find("LogisticsCommandUI");
            if (cmdUIObj == null)
            {
                cmdUIObj = new GameObject("LogisticsCommandUI");
                Undo.RegisterCreatedObjectUndo(cmdUIObj, "Create LogisticsCommandUI");
            }
            if (cmdUIObj.GetComponent<RobotCommandUI>() == null)
                Undo.AddComponent<RobotCommandUI>(cmdUIObj);
            if (cmdUIObj.GetComponent<LogisticsRobotPopupUI>() == null)
                Undo.AddComponent<LogisticsRobotPopupUI>(cmdUIObj);

            // --- Mark dirty ---
            EditorUtility.SetDirty(robotObj);
            if (dockObj != null) EditorUtility.SetDirty(dockObj);
            if (containerObj != null) EditorUtility.SetDirty(containerObj);
            UnityEditor.SceneManagement.EditorSceneManager.MarkSceneDirty(
                UnityEditor.SceneManagement.EditorSceneManager.GetActiveScene());

            Selection.activeGameObject = robotObj;
            Debug.Log("[LogisticsRobotSetup] Setup complete. Components wired onto M3_Collector, Platform_Picking_Robot, Box_container_Picking_Robot.");
            Debug.Log("[LogisticsRobotSetup] Purchase 'ACTIVATE LOGISTICS ROBOT' from the Robots tab (2000 credits) to enable.");
        }

        [MenuItem("BeneathTheFloor/Setup/Reset Logistics Robot Upgrades")]
        public static void ResetUpgrades()
        {
            PlayerPrefs.DeleteKey("logistics_robot_activated");
            PlayerPrefs.DeleteKey("logistics_autonomous");
            PlayerPrefs.DeleteKey("logistics_capacity_level");
            PlayerPrefs.DeleteKey("logistics_advanced_module");
            PlayerPrefs.Save();
            Debug.Log("[LogisticsRobotSetup] Logistics robot upgrades reset.");
        }

        [MenuItem("BeneathTheFloor/Setup/Reset ALL First Room Upgrades")]
        public static void ResetAllUpgrades()
        {
            // Tools
            PlayerPrefs.DeleteKey("tool_power");
            PlayerPrefs.DeleteKey("super_hit");
            // Jetpack
            PlayerPrefs.DeleteKey("jetpack_efficiency");
            // Digger robot
            PlayerPrefs.DeleteKey("robot_activated");
            PlayerPrefs.DeleteKey("robot_smart_stop");
            PlayerPrefs.DeleteKey("robot_efficiency");
            // Logistics robot
            PlayerPrefs.DeleteKey("logistics_robot_activated");
            PlayerPrefs.DeleteKey("logistics_autonomous");
            PlayerPrefs.DeleteKey("logistics_capacity_level");
            PlayerPrefs.DeleteKey("logistics_advanced_module");
            PlayerPrefs.Save();
            Debug.Log("[LogisticsRobotSetup] ALL first room upgrades reset.");
        }

        private static void SetLayerRecursive(GameObject obj, int layer)
        {
            obj.layer = layer;
            foreach (Transform child in obj.transform)
                SetLayerRecursive(child.gameObject, layer);
        }
    }
}
