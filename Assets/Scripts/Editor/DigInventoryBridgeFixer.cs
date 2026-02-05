using UnityEngine;
using UnityEditor;
using UnityEditor.SceneManagement;
using System;
using System.Collections.Generic;
using System.Text;
using System.IO;
using BeneathTheFloor.Digging;
using BeneathTheFloor.Crafting;

namespace BeneathTheFloor.Editor
{
    /// <summary>
    /// Diagnostic and fix utility for DigInventoryBridge mappings.
    /// Ensures the ACTIVE SCENE instance has all mappings populated.
    /// </summary>
    public static class DigInventoryBridgeFixer
    {
        private const string GeneratedAssetsPath = "Assets/Items/GeneratedResources";

        [MenuItem("Tools/Beneath The Floor/Fix/Diagnose and Fix DigInventoryBridge Mappings")]
        public static void DiagnoseAndFix()
        {
            Debug.Log("╔════════════════════════════════════════════════════════════════╗");
            Debug.Log("║     DIG INVENTORY BRIDGE - DIAGNOSTIC AND FIX                  ║");
            Debug.Log("╚════════════════════════════════════════════════════════════════╝");

            // Step 1: Discover all instances
            var allInstances = DiscoverAllInstances();

            // Step 2: Find active scene instance
            var activeScene = EditorSceneManager.GetActiveScene();
            Debug.Log($"\n[STEP 2] Active Scene: {activeScene.name} ({activeScene.path})");

            DigInventoryBridge activeSceneBridge = null;
            foreach (var instance in allInstances)
            {
                if (instance != null && instance.gameObject.scene == activeScene)
                {
                    activeSceneBridge = instance;
                    Debug.Log($"[STEP 2] Found bridge in active scene: {instance.gameObject.name}");
                    break;
                }
            }

            if (activeSceneBridge == null)
            {
                Debug.LogError("[STEP 2] ══════════════════════════════════════════════════════");
                Debug.LogError("[STEP 2] NO DigInventoryBridge FOUND IN ACTIVE SCENE!");
                Debug.LogError("[STEP 2] You need to add a DigInventoryBridge component to a GameObject.");
                Debug.LogError("[STEP 2] ══════════════════════════════════════════════════════");

                if (EditorUtility.DisplayDialog(
                    "No DigInventoryBridge Found",
                    $"No DigInventoryBridge component found in the active scene '{activeScene.name}'.\n\n" +
                    "Would you like to create one on a new GameObject?",
                    "Yes, Create One",
                    "Cancel"))
                {
                    activeSceneBridge = CreateBridgeInActiveScene();
                }
                else
                {
                    return;
                }
            }

            // Step 3: Populate mappings
            Debug.Log($"\n[STEP 3] Populating mappings on: {activeSceneBridge.gameObject.name}");
            var result = PopulateMappings(activeSceneBridge);

            // Step 4: Mark dirty and save
            EditorUtility.SetDirty(activeSceneBridge);
            EditorUtility.SetDirty(activeSceneBridge.gameObject);
            EditorSceneManager.MarkSceneDirty(activeScene);

            Debug.Log($"[STEP 4] Marked bridge and scene as dirty - SAVE THE SCENE to persist changes!");

            // Step 5: Validate
            Debug.Log("\n[STEP 5] Running validation...");
            var report = activeSceneBridge.ValidateMappings();

            // Final summary
            PrintSummary(activeSceneBridge, activeScene, result, report);

            // Prompt to save
            if (EditorUtility.DisplayDialog(
                "Mappings Populated Successfully",
                $"Populated {result.totalMappings} mappings on '{activeSceneBridge.gameObject.name}'.\n\n" +
                $"• ItemSO Reused: {result.reusedCount}\n" +
                $"• ItemSO Created: {result.createdCount}\n\n" +
                "Would you like to save the scene now?",
                "Save Scene",
                "Don't Save Yet"))
            {
                EditorSceneManager.SaveScene(activeScene);
                Debug.Log("[SAVE] Scene saved successfully!");
            }

            // Select the bridge for inspection
            Selection.activeGameObject = activeSceneBridge.gameObject;
            EditorGUIUtility.PingObject(activeSceneBridge);
        }

        [MenuItem("Tools/Beneath The Floor/Fix/Scan All DigInventoryBridge Instances")]
        public static void ScanAllInstances()
        {
            Debug.Log("╔════════════════════════════════════════════════════════════════╗");
            Debug.Log("║     DIG INVENTORY BRIDGE - INSTANCE SCAN                       ║");
            Debug.Log("╚════════════════════════════════════════════════════════════════╝");

            DiscoverAllInstances();

            var activeScene = EditorSceneManager.GetActiveScene();
            Debug.Log($"\nActive Scene: {activeScene.name} ({activeScene.path})");
        }

        private static List<DigInventoryBridge> DiscoverAllInstances()
        {
            Debug.Log("\n[STEP 1] Scanning for all DigInventoryBridge instances...");

            var allBridges = Resources.FindObjectsOfTypeAll<DigInventoryBridge>();
            var validInstances = new List<DigInventoryBridge>();

            Debug.Log($"[STEP 1] Found {allBridges.Length} total DigInventoryBridge references.");

            int index = 0;
            foreach (var bridge in allBridges)
            {
                if (bridge == null) continue;

                index++;
                string location;
                string goName = bridge.gameObject.name;
                int mappingCount = bridge.GetAllMappings()?.Count ?? 0;

                // Determine location
                if (EditorUtility.IsPersistent(bridge))
                {
                    // This is a prefab asset
                    string assetPath = AssetDatabase.GetAssetPath(bridge);
                    location = $"PREFAB: {assetPath}";
                }
                else if (bridge.gameObject.scene.IsValid())
                {
                    // This is in a scene
                    location = $"SCENE: {bridge.gameObject.scene.name} ({bridge.gameObject.scene.path})";
                    validInstances.Add(bridge);
                }
                else
                {
                    location = "DontDestroyOnLoad or Unknown";
                }

                Debug.Log($"[INSTANCE {index}] GO=\"{goName}\", Location={location}, resourceMappings={mappingCount}");
            }

            Debug.Log($"[STEP 1] Found {validInstances.Count} scene instances (excluding prefabs).");

            return validInstances;
        }

        private static DigInventoryBridge CreateBridgeInActiveScene()
        {
            // Try to find a suitable parent (GameManager or similar)
            GameObject host = GameObject.Find("GameManager");
            if (host == null)
                host = GameObject.Find("DiggingManager");
            if (host == null)
            {
                host = new GameObject("DiggingManager");
                Debug.Log("[CREATE] Created new 'DiggingManager' GameObject");
            }

            var bridge = host.AddComponent<DigInventoryBridge>();
            Debug.Log($"[CREATE] Added DigInventoryBridge to '{host.name}'");

            return bridge;
        }

        private static PopulationResult PopulateMappings(DigInventoryBridge bridge)
        {
            var result = new PopulationResult();

            // Ensure folder exists
            CreateFolderIfNeeded(GeneratedAssetsPath);

            // Get all enum values
            var allTypes = (UndergroundResourceType[])Enum.GetValues(typeof(UndergroundResourceType));
            var resourceTypes = new List<UndergroundResourceType>();

            foreach (var type in allTypes)
            {
                if (type != UndergroundResourceType.None)
                {
                    resourceTypes.Add(type);
                }
            }

            result.totalTypes = resourceTypes.Count;
            Debug.Log($"[POPULATE] Found {resourceTypes.Count} UndergroundResourceType values (excluding None)");

            // Build the new mappings list
            var newMappings = new List<ResourceItemMapping>();

            foreach (var resourceType in resourceTypes)
            {
                // Try to find existing item
                MaterialItemSO item = FindOrCreateItem(resourceType, out bool wasCreated);

                if (wasCreated)
                {
                    result.createdCount++;
                    Debug.Log($"  [CREATED] {resourceType} → {item.name}");
                }
                else
                {
                    result.reusedCount++;
                    Debug.Log($"  [REUSED]  {resourceType} → {item.name}");
                }

                var mapping = new ResourceItemMapping
                {
                    undergroundType = resourceType,
                    item = item,
                    defaultAmountPerDig = 1
                };
                newMappings.Add(mapping);
            }

            // Apply to bridge using SerializedObject (ensures proper serialization)
            var serializedObject = new SerializedObject(bridge);
            var mappingsProperty = serializedObject.FindProperty("resourceMappings");

            if (mappingsProperty == null)
            {
                Debug.LogError("[POPULATE] Could not find 'resourceMappings' property!");
                return result;
            }

            // Clear and rebuild
            mappingsProperty.ClearArray();

            for (int i = 0; i < newMappings.Count; i++)
            {
                mappingsProperty.InsertArrayElementAtIndex(i);
                var element = mappingsProperty.GetArrayElementAtIndex(i);

                var typeProperty = element.FindPropertyRelative("undergroundType");
                var itemProperty = element.FindPropertyRelative("item");
                var amountProperty = element.FindPropertyRelative("defaultAmountPerDig");

                if (typeProperty != null)
                    typeProperty.enumValueIndex = (int)newMappings[i].undergroundType;

                if (itemProperty != null)
                    itemProperty.objectReferenceValue = newMappings[i].item;

                if (amountProperty != null)
                    amountProperty.intValue = newMappings[i].defaultAmountPerDig;
            }

            serializedObject.ApplyModifiedProperties();

            result.totalMappings = newMappings.Count;
            Debug.Log($"[POPULATE] Applied {newMappings.Count} mappings via SerializedObject");

            // Save assets
            AssetDatabase.SaveAssets();

            return result;
        }

        private static MaterialItemSO FindOrCreateItem(UndergroundResourceType resourceType, out bool wasCreated)
        {
            wasCreated = false;
            string typeName = resourceType.ToString();

            // Search for existing assets
            string[] guids = AssetDatabase.FindAssets($"t:MaterialItemSO {typeName}");
            foreach (string guid in guids)
            {
                string path = AssetDatabase.GUIDToAssetPath(guid);
                var existing = AssetDatabase.LoadAssetAtPath<MaterialItemSO>(path);
                if (existing != null)
                {
                    string assetName = Path.GetFileNameWithoutExtension(path);
                    if (assetName.Equals(typeName, StringComparison.OrdinalIgnoreCase) ||
                        assetName.Equals($"{typeName}_Item", StringComparison.OrdinalIgnoreCase))
                    {
                        return existing;
                    }
                }
            }

            // Also check the generated folder specifically
            string expectedPath = $"{GeneratedAssetsPath}/{typeName}_Item.asset";
            var existingAtPath = AssetDatabase.LoadAssetAtPath<MaterialItemSO>(expectedPath);
            if (existingAtPath != null)
            {
                return existingAtPath;
            }

            // Create new
            wasCreated = true;
            return CreateNewItem(resourceType);
        }

        private static MaterialItemSO CreateNewItem(UndergroundResourceType resourceType)
        {
            var item = ScriptableObject.CreateInstance<MaterialItemSO>();

            string typeName = resourceType.ToString();
            string readableName = FormatResourceName(typeName);

            item.itemId = $"underground_{typeName.ToLower()}";
            item.itemName = readableName;
            item.description = $"A {readableName.ToLower()} extracted from underground digging.";
            item.icon = null;
            item.category = ItemCategory.Material;
            item.isStackable = true;
            item.maxStackSize = 999;
            item.buyPrice = 0;
            item.sellPrice = GetSellPrice(resourceType);
            item.materialType = GetMaterialType(resourceType);

            string assetPath = $"{GeneratedAssetsPath}/{typeName}_Item.asset";
            AssetDatabase.CreateAsset(item, assetPath);

            return item;
        }

        private static void CreateFolderIfNeeded(string path)
        {
            if (!AssetDatabase.IsValidFolder(path))
            {
                string[] parts = path.Split('/');
                string currentPath = parts[0];

                for (int i = 1; i < parts.Length; i++)
                {
                    string nextPath = currentPath + "/" + parts[i];
                    if (!AssetDatabase.IsValidFolder(nextPath))
                    {
                        AssetDatabase.CreateFolder(currentPath, parts[i]);
                        Debug.Log($"[FOLDER] Created: {nextPath}");
                    }
                    currentPath = nextPath;
                }
            }
        }

        private static string FormatResourceName(string name)
        {
            var sb = new StringBuilder();
            for (int i = 0; i < name.Length; i++)
            {
                if (i > 0 && char.IsUpper(name[i]))
                    sb.Append(' ');
                sb.Append(name[i]);
            }
            return sb.ToString();
        }

        private static int GetSellPrice(UndergroundResourceType type)
        {
            return type switch
            {
                UndergroundResourceType.Dirt => 1,
                UndergroundResourceType.SoftStone => 2,
                UndergroundResourceType.Clay => 3,
                UndergroundResourceType.IronNugget => 5,
                UndergroundResourceType.CopperFragment => 4,
                UndergroundResourceType.HardSoil => 3,
                UndergroundResourceType.Stone => 4,
                UndergroundResourceType.Sandstone => 5,
                UndergroundResourceType.IronChunk => 8,
                UndergroundResourceType.CopperPiece => 7,
                UndergroundResourceType.Coal => 6,
                UndergroundResourceType.HardSoilDeep => 5,
                UndergroundResourceType.HardStone => 6,
                UndergroundResourceType.Heatstone => 12,
                UndergroundResourceType.Quartz => 15,
                UndergroundResourceType.CrystalDust => 10,
                UndergroundResourceType.AncientOre => 20,
                UndergroundResourceType.CrystalShard => 25,
                UndergroundResourceType.PurpleQuartz => 30,
                UndergroundResourceType.CrystalStone => 20,
                UndergroundResourceType.DeepCrystalVein => 35,
                UndergroundResourceType.LuminousDust => 28,
                UndergroundResourceType.CrystalCoreFragment => 50,
                UndergroundResourceType.BlueGreyOre => 45,
                UndergroundResourceType.DeepBlackStone => 40,
                UndergroundResourceType.CoreCrystalChunk => 75,
                UndergroundResourceType.RareMachineParts => 100,
                _ => 5
            };
        }

        private static MaterialType GetMaterialType(UndergroundResourceType type)
        {
            return type switch
            {
                UndergroundResourceType.AncientOre or
                UndergroundResourceType.RareMachineParts => MaterialType.Ancient,

                UndergroundResourceType.CrystalShard or
                UndergroundResourceType.CrystalDust or
                UndergroundResourceType.CrystalStone or
                UndergroundResourceType.CrystalCoreFragment or
                UndergroundResourceType.CoreCrystalChunk or
                UndergroundResourceType.PurpleQuartz or
                UndergroundResourceType.DeepCrystalVein or
                UndergroundResourceType.LuminousDust or
                UndergroundResourceType.Quartz or
                UndergroundResourceType.Heatstone => MaterialType.Refined,

                _ => MaterialType.Raw
            };
        }

        private static void PrintSummary(DigInventoryBridge bridge, UnityEngine.SceneManagement.Scene scene, PopulationResult result, MappingValidationReport report)
        {
            Debug.Log("\n╔════════════════════════════════════════════════════════════════╗");
            Debug.Log("║                    FINAL SUMMARY                               ║");
            Debug.Log("╠════════════════════════════════════════════════════════════════╣");
            Debug.Log($"║  Modified Bridge:         {bridge.gameObject.name,-36}║");
            Debug.Log($"║  Scene:                   {scene.name,-36}║");
            Debug.Log($"║  Total Resource Types:    {result.totalTypes,-36}║");
            Debug.Log($"║  ItemSO Reused:           {result.reusedCount,-36}║");
            Debug.Log($"║  ItemSO Created:          {result.createdCount,-36}║");
            Debug.Log($"║  Final Mapping Count:     {result.totalMappings,-36}║");
            Debug.Log("╠════════════════════════════════════════════════════════════════╣");
            Debug.Log($"║  Validation - Mapped:     {report.mappedTypeCount}/{report.totalTypeCount,-32}║");
            Debug.Log($"║  Validation - Valid:      {report.validMappingCount}/{report.totalTypeCount,-32}║");
            Debug.Log($"║  Validation - Missing:    {report.missingTypes.Count,-36}║");
            Debug.Log($"║  Validation - Null Items: {report.nullItemTypes.Count,-36}║");
            Debug.Log("╠════════════════════════════════════════════════════════════════╣");

            if (report.IsFullyConfigured)
            {
                Debug.Log("║  ✓ ALL MAPPINGS VALID - 100% COVERAGE                         ║");
            }
            else
            {
                Debug.LogWarning("║  ✗ ISSUES REMAIN - Check validation report above             ║");
            }

            Debug.Log("╚════════════════════════════════════════════════════════════════╝");
        }

        private struct PopulationResult
        {
            public int totalTypes;
            public int reusedCount;
            public int createdCount;
            public int totalMappings;
        }
    }
}
