using UnityEngine;
using UnityEditor;

namespace BeneathTheFloor.Winch.Editor
{
    /// <summary>
    /// Editor utilities for setting up the Winch System.
    /// </summary>
    public static class WinchSetupEditor
    {
        [MenuItem("Tools/Beneath The Floor/Winch/Setup Player Components")]
        public static void SetupPlayerComponents()
        {
            // Find player
            GameObject player = GameObject.FindGameObjectWithTag("Player");
            if (player == null)
            {
                EditorUtility.DisplayDialog("Winch Setup",
                    "No object with 'Player' tag found in scene.\nPlease ensure your player has the 'Player' tag.",
                    "OK");
                return;
            }

            bool added = false;

            // Add PlayerWinchAttachment
            if (player.GetComponent<PlayerWinchAttachment>() == null)
            {
                player.AddComponent<PlayerWinchAttachment>();
                added = true;
                Debug.Log("[WinchSetup] Added PlayerWinchAttachment to " + player.name);
            }

            // Add WinchMotor (replaces WinchConstraintMotor + WinchPullController)
            if (player.GetComponent<WinchMotor>() == null)
            {
                player.AddComponent<WinchMotor>();
                added = true;
                Debug.Log("[WinchSetup] Added WinchMotor to " + player.name);
            }

            if (added)
            {
                EditorUtility.SetDirty(player);
                EditorUtility.DisplayDialog("Winch Setup",
                    $"Winch components added to {player.name}.\n\nComponents added:\n- PlayerWinchAttachment\n- WinchMotor",
                    "OK");
            }
            else
            {
                EditorUtility.DisplayDialog("Winch Setup",
                    "Player already has all winch components.",
                    "OK");
            }
        }

        [MenuItem("Tools/Beneath The Floor/Winch/Create Winch System in Scene")]
        public static void CreateWinchSystemInScene()
        {
            // Check if one already exists
            WinchAnchor existing = Object.FindObjectOfType<WinchAnchor>();
            if (existing != null)
            {
                if (!EditorUtility.DisplayDialog("Winch Setup",
                    "A WinchAnchor already exists in the scene.\nCreate another one?",
                    "Yes", "No"))
                {
                    Selection.activeGameObject = existing.gameObject;
                    return;
                }
            }

            // Create using builder
            // Create winch anchor object
            Vector3 spawnPos = Vector3.up * 3f;
            SceneView sceneView = SceneView.lastActiveSceneView;
            if (sceneView != null)
                spawnPos = sceneView.pivot + Vector3.up * 3f;

            GameObject winchSystem = new GameObject("WinchAnchor");
            winchSystem.transform.position = spawnPos;
            winchSystem.AddComponent<WinchAnchor>();

            // Create Cable child with WinchCable
            GameObject cableObj = new GameObject("Cable");
            cableObj.transform.SetParent(winchSystem.transform);
            cableObj.transform.localPosition = Vector3.zero;
            cableObj.AddComponent<LineRenderer>();
            cableObj.AddComponent<WinchCable>();

            // Create pit trigger
            GameObject pitTrigger = new GameObject("PitTrigger");
            pitTrigger.transform.SetParent(winchSystem.transform);
            pitTrigger.transform.localPosition = Vector3.down * 2f;
            var col = pitTrigger.AddComponent<BoxCollider>();
            col.isTrigger = true;
            col.size = new Vector3(4f, 4f, 4f);
            pitTrigger.AddComponent<WinchPitTrigger>();
            pitTrigger.tag = "WinchPitTrigger";

            if (winchSystem != null)
            {
                Undo.RegisterCreatedObjectUndo(winchSystem, "Create Winch System");
                Selection.activeGameObject = winchSystem;

                EditorUtility.DisplayDialog("Winch Setup",
                    "Winch System created!\n\nNext steps:\n1. Position the WinchAnchor above your dig pit\n2. Adjust the PitTrigger size and position\n3. Run 'Setup Player Components' to add player scripts",
                    "OK");
            }
        }

        [MenuItem("Tools/Beneath The Floor/Winch/Create Upgrade Config Asset")]
        public static void CreateUpgradeConfigAsset()
        {
            // Create folder if needed
            if (!AssetDatabase.IsValidFolder("Assets/GameData"))
            {
                AssetDatabase.CreateFolder("Assets", "GameData");
            }
            if (!AssetDatabase.IsValidFolder("Assets/GameData/Winch"))
            {
                AssetDatabase.CreateFolder("Assets/GameData", "Winch");
            }

            // Create asset
            WinchUpgradeConfig config = ScriptableObject.CreateInstance<WinchUpgradeConfig>();
            config.ResetToDefaults();

            string path = "Assets/GameData/Winch/WinchUpgradeConfig.asset";
            path = AssetDatabase.GenerateUniqueAssetPath(path);

            AssetDatabase.CreateAsset(config, path);
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();

            EditorUtility.FocusProjectWindow();
            Selection.activeObject = config;

            Debug.Log($"[WinchSetup] Created WinchUpgradeConfig at {path}");
        }

        [MenuItem("Tools/Beneath The Floor/Winch/Add WinchPitTrigger Tag")]
        public static void AddWinchPitTriggerTag()
        {
            // Open TagManager
            SerializedObject tagManager = new SerializedObject(AssetDatabase.LoadAllAssetsAtPath("ProjectSettings/TagManager.asset")[0]);
            SerializedProperty tagsProp = tagManager.FindProperty("tags");

            // Check if tag exists
            bool found = false;
            for (int i = 0; i < tagsProp.arraySize; i++)
            {
                if (tagsProp.GetArrayElementAtIndex(i).stringValue == "WinchPitTrigger")
                {
                    found = true;
                    break;
                }
            }

            if (!found)
            {
                // Add tag
                tagsProp.InsertArrayElementAtIndex(tagsProp.arraySize);
                tagsProp.GetArrayElementAtIndex(tagsProp.arraySize - 1).stringValue = "WinchPitTrigger";
                tagManager.ApplyModifiedProperties();

                Debug.Log("[WinchSetup] Added 'WinchPitTrigger' tag");
                EditorUtility.DisplayDialog("Winch Setup",
                    "'WinchPitTrigger' tag added successfully.",
                    "OK");
            }
            else
            {
                EditorUtility.DisplayDialog("Winch Setup",
                    "'WinchPitTrigger' tag already exists.",
                    "OK");
            }
        }

        [MenuItem("Tools/Beneath The Floor/Winch/Quick Setup (All Steps)")]
        public static void QuickSetupAll()
        {
            // 1. Add tag
            AddWinchPitTriggerTag();

            // 2. Create config asset
            string configPath = "Assets/GameData/Winch/WinchUpgradeConfig.asset";
            WinchUpgradeConfig config = AssetDatabase.LoadAssetAtPath<WinchUpgradeConfig>(configPath);
            if (config == null)
            {
                CreateUpgradeConfigAsset();
                config = AssetDatabase.LoadAssetAtPath<WinchUpgradeConfig>(configPath);
            }

            // 3. Create winch system
            WinchAnchor existingAnchor = Object.FindObjectOfType<WinchAnchor>();
            if (existingAnchor == null)
            {
                CreateWinchSystemInScene();
            }

            // 4. Setup player
            SetupPlayerComponents();

            EditorUtility.DisplayDialog("Winch Setup Complete",
                "Winch system fully set up!\n\nRemember to:\n1. Position the WinchAnchor above your dig pit\n2. Adjust the PitTrigger volume\n3. Assign the WinchUpgradeConfig to the WinchAnchor (if not auto-assigned)",
                "OK");
        }
    }
}
