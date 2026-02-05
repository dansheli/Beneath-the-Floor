using UnityEngine;
using UnityEditor;
using BeneathTheFloor.TreasureChests;

namespace BeneathTheFloor.Editor
{
    /// <summary>
    /// Editor tools for setting up the Treasure Chest system.
    /// </summary>
    public static class TreasureChestSetup
    {
        private const string CHEST_PREFAB_PATH_1 = "Assets/Art/Treasure_Chests/Chest_1.prefab";
        private const string CHEST_PREFAB_PATH_2 = "Assets/Art/Treasure_Chests/Chest_2.prefab";
        private const string CHEST_PREFAB_PATH_3 = "Assets/Art/Treasure_Chests/Chest_3.prefab";
        private const string REWARDS_FOLDER = "Assets/GameData/TreasureChests";

        #region Menu Items

        [MenuItem("Tools/Treasure Chests/Setup Treasure Chest System", false, 100)]
        public static void SetupTreasureChestSystem()
        {
            // Create manager
            SetupTreasureChestManager();

            // Create reward UI
            SetupRewardUI();

            // Create sample rewards folder
            CreateRewardsFolderStructure();

            Debug.Log("[TreasureChestSetup] Treasure Chest System setup complete!");
            EditorUtility.DisplayDialog("Setup Complete",
                "Treasure Chest System has been set up!\n\n" +
                "- TreasureChestManager added to scene\n" +
                "- TreasureChestRewardUI added to scene\n" +
                "- Rewards folder created at: " + REWARDS_FOLDER + "\n\n" +
                "Use 'Tools > Treasure Chests > Create Sample Rewards' to create example reward assets.",
                "OK");
        }

        [MenuItem("Tools/Treasure Chests/Add TreasureChestManager to Scene", false, 110)]
        public static void SetupTreasureChestManager()
        {
            // Check if already exists
            var existing = Object.FindObjectOfType<TreasureChestManager>();
            if (existing != null)
            {
                Debug.Log("[TreasureChestSetup] TreasureChestManager already exists in scene");
                Selection.activeGameObject = existing.gameObject;
                return;
            }

            // Create new manager
            GameObject managerObj = new GameObject("TreasureChestManager");
            managerObj.AddComponent<TreasureChestManager>();

            Undo.RegisterCreatedObjectUndo(managerObj, "Create TreasureChestManager");
            Selection.activeGameObject = managerObj;

            Debug.Log("[TreasureChestSetup] Created TreasureChestManager");
        }

        [MenuItem("Tools/Treasure Chests/Add TreasureChestRewardUI to Scene", false, 111)]
        public static void SetupRewardUI()
        {
            // Check if already exists
            var existing = Object.FindObjectOfType<TreasureChestRewardUI>();
            if (existing != null)
            {
                Debug.Log("[TreasureChestSetup] TreasureChestRewardUI already exists in scene");
                Selection.activeGameObject = existing.gameObject;
                return;
            }

            // Find or create canvas
            Canvas canvas = Object.FindObjectOfType<Canvas>();
            if (canvas == null)
            {
                Debug.LogWarning("[TreasureChestSetup] No Canvas found in scene. Creating one...");
                GameObject canvasObj = new GameObject("Canvas");
                canvas = canvasObj.AddComponent<Canvas>();
                canvas.renderMode = RenderMode.ScreenSpaceOverlay;
                canvasObj.AddComponent<UnityEngine.UI.CanvasScaler>();
                canvasObj.AddComponent<UnityEngine.UI.GraphicRaycaster>();
                Undo.RegisterCreatedObjectUndo(canvasObj, "Create Canvas");
            }

            // Create reward UI
            GameObject uiObj = new GameObject("TreasureChestRewardUI");
            uiObj.transform.SetParent(canvas.transform, false);
            uiObj.AddComponent<TreasureChestRewardUI>();

            Undo.RegisterCreatedObjectUndo(uiObj, "Create TreasureChestRewardUI");
            Selection.activeGameObject = uiObj;

            Debug.Log("[TreasureChestSetup] Created TreasureChestRewardUI");
        }

        [MenuItem("Tools/Treasure Chests/Create Sample Rewards", false, 120)]
        public static void CreateSampleRewards()
        {
            CreateRewardsFolderStructure();

            // Create sample currency reward
            CreateReward("CurrencyReward_Small", reward =>
            {
                reward.displayName = "Coins!";
                reward.description = "A small stash of coins.";
                reward.rewardType = RewardType.Currency;
                reward.currencyAmount = 100;
                reward.rewardColor = Color.yellow;
            });

            CreateReward("CurrencyReward_Medium", reward =>
            {
                reward.displayName = "Money Bag!";
                reward.description = "A bag full of coins!";
                reward.rewardType = RewardType.Currency;
                reward.currencyAmount = 500;
                reward.rewardColor = Color.yellow;
            });

            CreateReward("CurrencyReward_Large", reward =>
            {
                reward.displayName = "Treasure!";
                reward.description = "A fortune in gold coins!";
                reward.rewardType = RewardType.Currency;
                reward.currencyAmount = 2000;
                reward.rewardColor = new Color(1f, 0.8f, 0f);
            });

            // Create sample note reward
            CreateReward("NoteReward_Hint1", reward =>
            {
                reward.displayName = "Old Note";
                reward.description = "A weathered piece of paper with writing...";
                reward.rewardType = RewardType.Note;
                reward.noteTitle = "Mysterious Note";
                reward.noteContent = "\"The deeper you dig, the more valuable the resources become. " +
                    "But be warned - not everything buried wants to be found...\"";
                reward.rewardColor = new Color(0.9f, 0.85f, 0.7f);
            });

            CreateReward("NoteReward_Hint2", reward =>
            {
                reward.displayName = "Torn Page";
                reward.description = "A page torn from a journal...";
                reward.rewardType = RewardType.Note;
                reward.noteTitle = "Journal Entry";
                reward.noteContent = "\"Day 47: I've found traces of copper at the second depth layer. " +
                    "The radar helps, but I can hear something moving down there...\"";
                reward.rewardColor = new Color(0.9f, 0.85f, 0.7f);
            });

            // Create sample currency + energy drink reward (shows checkbox usage)
            CreateReward("CurrencyReward_WithDrink", reward =>
            {
                reward.displayName = "Refreshing Find!";
                reward.description = "Coins and a drink to keep you going!";
                reward.rewardType = RewardType.Currency;
                reward.currencyAmount = 250;
                reward.includeEnergyDrink = true;
                reward.energyDrinkCount = 1;
                reward.rewardColor = new Color(0.2f, 1f, 0.4f);
            });

            Debug.Log("[TreasureChestSetup] Created sample rewards at: " + REWARDS_FOLDER);
            EditorUtility.DisplayDialog("Rewards Created",
                "Sample reward assets have been created!\n\n" +
                "Location: " + REWARDS_FOLDER + "\n\n" +
                "You can now assign these to treasure chests in the scene.",
                "OK");

            // Select the folder
            var folder = AssetDatabase.LoadAssetAtPath<Object>(REWARDS_FOLDER);
            if (folder != null)
            {
                Selection.activeObject = folder;
                EditorGUIUtility.PingObject(folder);
            }
        }

        [MenuItem("Tools/Treasure Chests/Place Treasure Chest at Selection", false, 130)]
        public static void PlaceTreasureChestAtSelection()
        {
            // Get selection position or scene view center
            Vector3 position = Vector3.zero;

            if (Selection.activeTransform != null)
            {
                position = Selection.activeTransform.position;
            }
            else
            {
                // Use scene view camera position
                SceneView sceneView = SceneView.lastActiveSceneView;
                if (sceneView != null)
                {
                    position = sceneView.pivot;
                }
            }

            PlaceTreasureChest(position, 1);
        }

        [MenuItem("Tools/Treasure Chests/Place Chest Type 1 (Small)", false, 140)]
        public static void PlaceChestType1()
        {
            Vector3 position = GetPlacementPosition();
            PlaceTreasureChest(position, 1);
        }

        [MenuItem("Tools/Treasure Chests/Place Chest Type 2 (Medium)", false, 141)]
        public static void PlaceChestType2()
        {
            Vector3 position = GetPlacementPosition();
            PlaceTreasureChest(position, 2);
        }

        [MenuItem("Tools/Treasure Chests/Place Chest Type 3 (Large)", false, 142)]
        public static void PlaceChestType3()
        {
            Vector3 position = GetPlacementPosition();
            PlaceTreasureChest(position, 3);
        }

        #endregion

        #region Helper Methods

        private static Vector3 GetPlacementPosition()
        {
            if (Selection.activeTransform != null)
            {
                return Selection.activeTransform.position;
            }

            SceneView sceneView = SceneView.lastActiveSceneView;
            if (sceneView != null)
            {
                return sceneView.pivot;
            }

            return Vector3.zero;
        }

        private static void PlaceTreasureChest(Vector3 position, int chestType)
        {
            string prefabPath = chestType switch
            {
                1 => CHEST_PREFAB_PATH_1,
                2 => CHEST_PREFAB_PATH_2,
                3 => CHEST_PREFAB_PATH_3,
                _ => CHEST_PREFAB_PATH_1
            };

            // Load prefab
            GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(prefabPath);

            if (prefab == null)
            {
                Debug.LogError($"[TreasureChestSetup] Could not find chest prefab at: {prefabPath}");

                // Try to create a placeholder cube
                prefab = GameObject.CreatePrimitive(PrimitiveType.Cube);
                prefab.transform.localScale = new Vector3(0.5f, 0.4f, 0.3f);
                Debug.LogWarning("[TreasureChestSetup] Created placeholder cube. Replace with actual chest prefab.");
            }

            // Instantiate
            GameObject chestInstance;
            if (PrefabUtility.IsPartOfPrefabAsset(prefab))
            {
                chestInstance = (GameObject)PrefabUtility.InstantiatePrefab(prefab);
            }
            else
            {
                chestInstance = Object.Instantiate(prefab);
            }

            chestInstance.name = $"TreasureChest_{chestType}_{System.DateTime.Now:HHmmss}";
            chestInstance.transform.position = position;

            // Add Collider if not present (needed for interaction)
            if (chestInstance.GetComponent<Collider>() == null)
            {
                chestInstance.AddComponent<BoxCollider>();
            }

            // Add Rigidbody if not present
            Rigidbody rb = chestInstance.GetComponent<Rigidbody>();
            if (rb == null)
            {
                rb = chestInstance.AddComponent<Rigidbody>();
            }
            rb.isKinematic = true; // Will be enabled when revealed

            // Add BuriedTreasureChest component if not already present
            BuriedTreasureChest chestComponent = chestInstance.GetComponent<BuriedTreasureChest>();
            if (chestComponent == null)
            {
                chestComponent = chestInstance.AddComponent<BuriedTreasureChest>();
            }

            // Configure based on chest type
            SerializedObject serializedChest = new SerializedObject(chestComponent);

            // Set exposure radius based on chest size
            SerializedProperty exposureRadius = serializedChest.FindProperty("exposureCheckRadius");
            if (exposureRadius != null)
            {
                exposureRadius.floatValue = chestType switch
                {
                    1 => 0.3f,  // Small
                    2 => 0.5f,  // Medium
                    3 => 0.7f,  // Large
                    _ => 0.4f
                };
            }

            // Enable debug mode for testing
            SerializedProperty debugMode = serializedChest.FindProperty("debugMode");
            if (debugMode != null)
            {
                debugMode.boolValue = true;
            }

            serializedChest.ApplyModifiedProperties();

            // Add TreasureChestRadarTarget for radar detection
            TreasureChestRadarTarget radarTarget = chestInstance.GetComponent<TreasureChestRadarTarget>();
            if (radarTarget == null)
            {
                radarTarget = chestInstance.AddComponent<TreasureChestRadarTarget>();
            }

            // Configure radar target
            SerializedObject serializedRadar = new SerializedObject(radarTarget);

            SerializedProperty radarPriority = serializedRadar.FindProperty("radarPriority");
            if (radarPriority != null)
            {
                radarPriority.intValue = chestType switch
                {
                    1 => 5,   // Small - lower priority
                    2 => 10,  // Medium
                    3 => 15,  // Large - higher priority
                    _ => 10
                };
            }

            SerializedProperty radarName = serializedRadar.FindProperty("radarDisplayName");
            if (radarName != null)
            {
                radarName.stringValue = chestType switch
                {
                    1 => "Small Treasure",
                    2 => "Treasure Chest",
                    3 => "Large Treasure",
                    _ => "Treasure"
                };
            }

            // Don't require reveal - radar should detect buried chests
            SerializedProperty requireReveal = serializedRadar.FindProperty("requireReveal");
            if (requireReveal != null)
            {
                requireReveal.boolValue = false;
            }

            serializedRadar.ApplyModifiedProperties();

            // Find or create TreasureChests parent
            Transform parent = FindOrCreateChestsParent();
            if (parent != null)
            {
                chestInstance.transform.SetParent(parent);
            }

            Undo.RegisterCreatedObjectUndo(chestInstance, "Place Treasure Chest");
            Selection.activeGameObject = chestInstance;

            Debug.Log($"[TreasureChestSetup] Placed Treasure Chest Type {chestType} at {position}");
            Debug.Log($"  - BuriedTreasureChest: Added (debug mode ON)");
            Debug.Log($"  - TreasureChestRadarTarget: Added (priority {(chestType switch { 1 => 5, 2 => 10, 3 => 15, _ => 10 })})");
        }

        private static Transform FindOrCreateChestsParent()
        {
            // Look for existing parent
            GameObject existing = GameObject.Find("TreasureChests");
            if (existing != null)
            {
                return existing.transform;
            }

            // Create new parent
            GameObject parent = new GameObject("TreasureChests");
            Undo.RegisterCreatedObjectUndo(parent, "Create TreasureChests Parent");

            return parent.transform;
        }

        private static void CreateRewardsFolderStructure()
        {
            // Create main folder
            if (!AssetDatabase.IsValidFolder(REWARDS_FOLDER))
            {
                string parentFolder = System.IO.Path.GetDirectoryName(REWARDS_FOLDER).Replace("\\", "/");
                string folderName = System.IO.Path.GetFileName(REWARDS_FOLDER);

                if (!AssetDatabase.IsValidFolder(parentFolder))
                {
                    AssetDatabase.CreateFolder("Assets", "GameData");
                }

                AssetDatabase.CreateFolder(parentFolder, folderName);
                Debug.Log($"[TreasureChestSetup] Created folder: {REWARDS_FOLDER}");
            }
        }

        private static void CreateReward(string name, System.Action<TreasureChestReward> configure)
        {
            string path = $"{REWARDS_FOLDER}/{name}.asset";

            // Check if already exists
            if (AssetDatabase.LoadAssetAtPath<TreasureChestReward>(path) != null)
            {
                Debug.Log($"[TreasureChestSetup] Reward already exists: {path}");
                return;
            }

            // Create new reward
            TreasureChestReward reward = ScriptableObject.CreateInstance<TreasureChestReward>();
            reward.rewardId = name.ToLower();

            // Configure
            configure?.Invoke(reward);

            // Save asset
            AssetDatabase.CreateAsset(reward, path);
            AssetDatabase.SaveAssets();

            Debug.Log($"[TreasureChestSetup] Created reward: {path}");
        }

        #endregion

        #region Validation

        [MenuItem("Tools/Treasure Chests/Validate All Chests in Scene", false, 200)]
        public static void ValidateAllChests()
        {
            var chests = Object.FindObjectsOfType<BuriedTreasureChest>();

            int validCount = 0;
            int warningCount = 0;
            int errorCount = 0;

            foreach (var chest in chests)
            {
                bool hasErrors = false;
                bool hasWarnings = false;

                // Check for collider
                if (chest.GetComponent<Collider>() == null)
                {
                    Debug.LogError($"[Validation] {chest.name}: Missing Collider!", chest);
                    hasErrors = true;
                }

                // Check for rewards
                var rewardsField = typeof(BuriedTreasureChest).GetField("rewards",
                    System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);

                if (rewardsField != null)
                {
                    var rewards = rewardsField.GetValue(chest) as TreasureChestReward[];
                    if (rewards == null || rewards.Length == 0)
                    {
                        Debug.LogWarning($"[Validation] {chest.name}: No rewards assigned!", chest);
                        hasWarnings = true;
                    }
                }

                // Check for unique chest ID
                string chestId = chest.ChestId;
                if (string.IsNullOrEmpty(chestId))
                {
                    Debug.LogWarning($"[Validation] {chest.name}: Empty chest ID!", chest);
                    hasWarnings = true;
                }

                if (hasErrors)
                    errorCount++;
                else if (hasWarnings)
                    warningCount++;
                else
                    validCount++;
            }

            string message = $"Validated {chests.Length} treasure chests:\n\n" +
                $"  Valid: {validCount}\n" +
                $"  Warnings: {warningCount}\n" +
                $"  Errors: {errorCount}";

            if (errorCount > 0)
            {
                EditorUtility.DisplayDialog("Validation Complete", message + "\n\nCheck Console for details.", "OK");
            }
            else if (warningCount > 0)
            {
                EditorUtility.DisplayDialog("Validation Complete", message + "\n\nCheck Console for details.", "OK");
            }
            else
            {
                EditorUtility.DisplayDialog("Validation Complete", message + "\n\nAll chests are properly configured!", "OK");
            }

            Debug.Log($"[TreasureChestSetup] {message}");
        }

        #endregion
    }
}
