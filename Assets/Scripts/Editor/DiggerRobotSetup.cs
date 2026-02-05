using UnityEngine;
using UnityEditor;
using BeneathTheFloor.Robot;

namespace BeneathTheFloor.Editor
{
    public static class DiggerRobotSetup
    {
        [MenuItem("Tools/Beneath The Floor/Setup Digger Robot and Dock")]
        public static void Setup()
        {
            // --- Find existing scene objects ---
            GameObject robotObj = GameObject.Find("ROBOT_DIGGER");
            GameObject dockObj = GameObject.Find("Platform_Excavator_Robot");

            if (robotObj == null)
            {
                Debug.LogError("[DiggerRobotSetup] Could not find 'ROBOT_DIGGER' in scene!");
                return;
            }
            if (dockObj == null)
            {
                Debug.LogError("[DiggerRobotSetup] Could not find 'Platform_Excavator_Robot' in scene!");
                return;
            }

            // --- Clean up previously generated placeholder objects ---
            GameObject oldDock = GameObject.Find("DiggerRobotDock");
            if (oldDock != null) Undo.DestroyObjectImmediate(oldDock);
            GameObject oldRobot = GameObject.Find("DiggerRobot");
            if (oldRobot != null) Undo.DestroyObjectImmediate(oldRobot);

            Undo.RegisterCompleteObjectUndo(robotObj, "Setup Digger Robot");
            Undo.RegisterCompleteObjectUndo(dockObj, "Setup Digger Robot Dock");

            // --- Create ScriptableObject config if missing ---
            string configPath = "Assets/GameData/DiggerRobotConfig.asset";
            DiggerRobotConfig config = AssetDatabase.LoadAssetAtPath<DiggerRobotConfig>(configPath);
            if (config == null)
            {
                config = ScriptableObject.CreateInstance<DiggerRobotConfig>();
                AssetDatabase.CreateAsset(config, configPath);
                Debug.Log($"[DiggerRobotSetup] Created config asset at {configPath}");
            }
            // Force-update config values to latest tuning
            config.moveSpeed = 1f;
            config.maxBattery = 100f;
            config.drainPerSecond = 0.3f;
            config.drainPerDig = 2f;
            config.rechargeRate = 25f;
            config.digRadius = 0.5f;
            config.digStrength = 0.25f;
            config.digInterval = 0.3f;
            config.digReach = 0.6f;
            config.breadcrumbSpacing = 1.5f;
            config.lowBatteryThreshold = 0.15f;
            config.robotMass = 20f;
            config.robotFriction = 0.3f;
            config.obstacleCheckRadius = 0.5f;
            config.obstacleCheckDist = 2f;
            config.terrainContactDist = 0.4f;
            config.stuckTimeout = 5f;
            config.stuckMinMove = 0.3f;
            config.reverseTime = 1f;
            config.scanRange = 8f;
            config.scanAngleStep = 10f;
            config.digStopDistance = 0.5f;
            config.scanInterval = 0.3f;
            EditorUtility.SetDirty(config);
            AssetDatabase.SaveAssets();

            // --- Setup Dock (Platform_Excavator_Robot) ---
            // Add collider if missing
            if (dockObj.GetComponent<Collider>() == null)
            {
                BoxCollider dockCol = Undo.AddComponent<BoxCollider>(dockObj);
                dockCol.size = new Vector3(1.5f, 1f, 1.5f);
                dockCol.center = new Vector3(0f, 0.5f, 0f);
            }

            // DockPoint child — reuse existing or create
            Transform dockPoint = dockObj.transform.Find("DockPoint");
            if (dockPoint == null)
            {
                GameObject dockPointObj = new GameObject("DockPoint");
                Undo.RegisterCreatedObjectUndo(dockPointObj, "Create DockPoint");
                dockPointObj.transform.SetParent(dockObj.transform, false);
                dockPointObj.transform.localPosition = new Vector3(0f, 0.1f, 0f);
                dockPoint = dockPointObj.transform;
            }

            var dockComp = dockObj.GetComponent<DiggerRobotDock>();
            if (dockComp == null)
                dockComp = Undo.AddComponent<DiggerRobotDock>(dockObj);

            // --- Setup Robot (ROBOT_DIGGER) ---
            // Remove old CharacterController / BoxCollider if leftover
            var oldCC = robotObj.GetComponent<CharacterController>();
            if (oldCC != null) Undo.DestroyObjectImmediate(oldCC);
            var oldBox = robotObj.GetComponent<BoxCollider>();
            if (oldBox != null) Undo.DestroyObjectImmediate(oldBox);

            // CapsuleCollider for physics collisions
            var capsule = robotObj.GetComponent<CapsuleCollider>();
            if (capsule == null)
            {
                capsule = Undo.AddComponent<CapsuleCollider>(robotObj);
                capsule.radius = 0.5f;
                capsule.height = 1.0f;
                capsule.center = new Vector3(0f, 0.5f, 0f);
            }

            // Rigidbody for physics movement
            var rigidbody = robotObj.GetComponent<Rigidbody>();
            if (rigidbody == null)
            {
                rigidbody = Undo.AddComponent<Rigidbody>(robotObj);
            }
            rigidbody.mass = 20f;
            rigidbody.drag = 2f;
            rigidbody.angularDrag = 10f;
            rigidbody.constraints = RigidbodyConstraints.FreezeRotation;
            rigidbody.collisionDetectionMode = CollisionDetectionMode.Continuous;
            rigidbody.interpolation = RigidbodyInterpolation.Interpolate;
            rigidbody.isKinematic = true; // starts docked

            // Core components
            if (robotObj.GetComponent<DiggerRobotBattery>() == null)
                Undo.AddComponent<DiggerRobotBattery>(robotObj);
            if (robotObj.GetComponent<DiggerRobotBreadcrumbs>() == null)
                Undo.AddComponent<DiggerRobotBreadcrumbs>(robotObj);

            var stateMachine = robotObj.GetComponent<DiggerRobotStateMachine>();
            if (stateMachine == null)
                stateMachine = Undo.AddComponent<DiggerRobotStateMachine>(robotObj);

            // HUD + Marker
            if (robotObj.GetComponent<DiggerRobotHUD>() == null)
                Undo.AddComponent<DiggerRobotHUD>(robotObj);
            if (robotObj.GetComponent<DiggerRobotMarker>() == null)
                Undo.AddComponent<DiggerRobotMarker>(robotObj);

            // Visual controller — try to find drill/LED children
            var visual = robotObj.GetComponent<DiggerRobotVisual>();
            if (visual == null)
                visual = Undo.AddComponent<DiggerRobotVisual>(robotObj);

            // --- Wire serialized fields ---
            // StateMachine -> config, dock
            var smSO = new SerializedObject(stateMachine);
            smSO.FindProperty("config").objectReferenceValue = config;
            smSO.FindProperty("dock").objectReferenceValue = dockComp;
            smSO.ApplyModifiedProperties();

            // Dock -> dockPoint, robot
            var dockSO = new SerializedObject(dockComp);
            dockSO.FindProperty("dockPoint").objectReferenceValue = dockPoint;
            dockSO.FindProperty("robot").objectReferenceValue = stateMachine;
            dockSO.ApplyModifiedProperties();

            // Visual -> try to auto-find drill, LED, ball, and arms from children
            Transform drill = FindChildRecursive(robotObj.transform, "drill", "Drill");
            Renderer ledRenderer = FindRendererRecursive(robotObj.transform, "led", "LED", "light", "indicator");
            Transform ballRot = FindChildRecursive(robotObj.transform, "ball_rotation");
            Transform upperArmL = FindChildRecursive(robotObj.transform, "upperarm_l");
            Transform upperArmR = FindChildRecursive(robotObj.transform, "upperarm_r");
            var visSO = new SerializedObject(visual);
            if (drill != null)
                visSO.FindProperty("drillPart").objectReferenceValue = drill;
            if (ledRenderer != null)
                visSO.FindProperty("ledRenderer").objectReferenceValue = ledRenderer;
            if (ballRot != null)
                visSO.FindProperty("ballRotation").objectReferenceValue = ballRot;
            if (upperArmL != null)
                visSO.FindProperty("upperArmL").objectReferenceValue = upperArmL;
            if (upperArmR != null)
                visSO.FindProperty("upperArmR").objectReferenceValue = upperArmR;
            visSO.ApplyModifiedProperties();

            if (drill == null)
                Debug.LogWarning("[DiggerRobotSetup] Could not auto-find drill child. Assign 'drillPart' on DiggerRobotVisual manually.");
            if (ledRenderer == null)
                Debug.LogWarning("[DiggerRobotSetup] Could not auto-find LED child. Assign 'ledRenderer' on DiggerRobotVisual manually.");
            if (ballRot == null)
                Debug.LogWarning("[DiggerRobotSetup] Could not auto-find 'ball_rotation'. Assign 'ballRotation' on DiggerRobotVisual manually.");
            if (upperArmL == null)
                Debug.LogWarning("[DiggerRobotSetup] Could not auto-find 'upperarm_l'. Assign 'upperArmL' on DiggerRobotVisual manually.");
            if (upperArmR == null)
                Debug.LogWarning("[DiggerRobotSetup] Could not auto-find 'upperarm_r'. Assign 'upperArmR' on DiggerRobotVisual manually.");

            // --- Waypoint Path ---
            GameObject wpObj = GameObject.Find("RobotWaypointPath");
            if (wpObj == null)
            {
                wpObj = new GameObject("RobotWaypointPath");
                Undo.RegisterCreatedObjectUndo(wpObj, "Create RobotWaypointPath");
                wpObj.AddComponent<RobotWaypointPath>();

                // Create 3 placeholder waypoints
                Vector3 robotPos = robotObj.transform.position;
                for (int i = 0; i < 3; i++)
                {
                    GameObject wp = new GameObject($"Waypoint_{i}");
                    Undo.RegisterCreatedObjectUndo(wp, $"Create Waypoint {i}");
                    wp.transform.SetParent(wpObj.transform, false);
                    wp.transform.position = robotPos + Vector3.forward * (2f + i * 3f);
                }
                Debug.Log("[DiggerRobotSetup] Created RobotWaypointPath with 3 placeholder waypoints — reposition in Scene view.");
            }
            var waypointPath = wpObj.GetComponent<RobotWaypointPath>();
            if (waypointPath == null)
                waypointPath = wpObj.AddComponent<RobotWaypointPath>();

            // --- Robot Gate ---
            GameObject gateObj = GameObject.Find("RobotGate");
            if (gateObj == null)
            {
                gateObj = new GameObject("RobotGate");
                Undo.RegisterCreatedObjectUndo(gateObj, "Create RobotGate");
                var gateBox = gateObj.AddComponent<BoxCollider>();
                gateBox.size = new Vector3(4f, 3f, 0.3f);
                gateBox.isTrigger = true;
                gateObj.AddComponent<RobotGate>();

                // Position at last waypoint if available
                if (wpObj.transform.childCount > 0)
                    gateObj.transform.position = wpObj.transform.GetChild(wpObj.transform.childCount - 1).position;
                else
                    gateObj.transform.position = robotObj.transform.position + Vector3.forward * 8f;

                Debug.Log("[DiggerRobotSetup] Created RobotGate — reposition at dig area entrance.");
            }
            var robotGate = gateObj.GetComponent<RobotGate>();
            if (robotGate == null)
                robotGate = gateObj.AddComponent<RobotGate>();

            // --- Wire waypointPath and gate on StateMachine ---
            smSO.Update();
            smSO.FindProperty("waypointPath").objectReferenceValue = waypointPath;
            smSO.FindProperty("gate").objectReferenceValue = robotGate;
            smSO.ApplyModifiedProperties();

            // --- Set robot layer to "Robot" (layer 11) recursively ---
            int robotLayer = LayerMask.NameToLayer("Robot");
            if (robotLayer >= 0)
            {
                SetLayerRecursive(robotObj, robotLayer);
                Debug.Log($"[DiggerRobotSetup] Set ROBOT_DIGGER to layer {robotLayer} ('Robot') recursively.");
            }
            else
            {
                Debug.LogWarning("[DiggerRobotSetup] Layer 'Robot' not found — set layer 11 manually in TagManager.");
            }

            // --- Mark dirty ---
            EditorUtility.SetDirty(dockObj);
            EditorUtility.SetDirty(robotObj);
            EditorUtility.SetDirty(wpObj);
            EditorUtility.SetDirty(gateObj);
            UnityEditor.SceneManagement.EditorSceneManager.MarkSceneDirty(
                UnityEditor.SceneManagement.EditorSceneManager.GetActiveScene());

            Selection.activeGameObject = robotObj;
            Debug.Log("[DiggerRobotSetup] Wired components onto ROBOT_DIGGER and Platform_Excavator_Robot.");
            Debug.Log("[DiggerRobotSetup] IMPORTANT: Configure Physics layer matrix — Robot collides with Default + Terrain only.");
            Debug.Log("[DiggerRobotSetup] Purchase 'ACTIVATE DIGGER ROBOT' from the Robots tab to enable.");
        }

        private static void SetLayerRecursive(GameObject obj, int layer)
        {
            obj.layer = layer;
            foreach (Transform child in obj.transform)
                SetLayerRecursive(child.gameObject, layer);
        }

        private static Transform FindChildRecursive(Transform root, params string[] nameParts)
        {
            foreach (Transform child in root.GetComponentsInChildren<Transform>(true))
            {
                string lower = child.name.ToLowerInvariant();
                foreach (string part in nameParts)
                {
                    if (lower.Contains(part.ToLowerInvariant()))
                        return child;
                }
            }
            return null;
        }

        private static Renderer FindRendererRecursive(Transform root, params string[] nameParts)
        {
            foreach (Renderer r in root.GetComponentsInChildren<Renderer>(true))
            {
                string lower = r.gameObject.name.ToLowerInvariant();
                foreach (string part in nameParts)
                {
                    if (lower.Contains(part.ToLowerInvariant()))
                        return r;
                }
            }
            return null;
        }
    }
}
