using UnityEngine;
using UnityEditor;
using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using BeneathTheFloor.Digging;
using BeneathTheFloor.Crafting;

namespace BeneathTheFloor.Editor
{
    /// <summary>
    /// Editor utility to auto-generate MaterialItemSO assets for all UndergroundResourceType values
    /// and populate the DigInventoryBridge mappings.
    /// </summary>
    public static class ResourceItemGenerator
    {
        private const string GeneratedAssetsPath = "Assets/Items/GeneratedResources";

        [MenuItem("Tools/Beneath The Floor/Generate/Create All Resource Items and Mappings")]
        public static void GenerateAllResourceItems()
        {
            Debug.Log("╔════════════════════════════════════════════════════════════════╗");
            Debug.Log("║      RESOURCE ITEM GENERATOR - Starting...                     ║");
            Debug.Log("╚════════════════════════════════════════════════════════════════╝");

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

            Debug.Log($"[Generator] Found {resourceTypes.Count} resource types to process.");

            // Track stats
            int reusedCount = 0;
            int createdCount = 0;
            var mappings = new List<ResourceItemMapping>();
            var report = new StringBuilder();

            // Process each resource type
            foreach (var resourceType in resourceTypes)
            {
                MaterialItemSO item = FindExistingItem(resourceType);

                if (item == null)
                {
                    item = CreateNewItem(resourceType);
                    createdCount++;
                    report.AppendLine($"  [CREATED] {resourceType} → {item.name}.asset");
                }
                else
                {
                    reusedCount++;
                    report.AppendLine($"  [REUSED]  {resourceType} → {item.name}");
                }

                // Create mapping
                var mapping = new ResourceItemMapping
                {
                    undergroundType = resourceType,
                    item = item,
                    defaultAmountPerDig = 1
                };
                mappings.Add(mapping);
            }

            // Find and populate DigInventoryBridge
            var bridge = FindDigInventoryBridge();
            if (bridge != null)
            {
                PopulateBridgeMappings(bridge, mappings);
                Debug.Log($"[Generator] Populated DigInventoryBridge with {mappings.Count} mappings.");
            }
            else
            {
                Debug.LogError("[Generator] Could not find DigInventoryBridge in scene!");
            }

            // Save all assets
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();

            // Print summary
            Debug.Log("╔════════════════════════════════════════════════════════════════╗");
            Debug.Log("║              GENERATION COMPLETE - SUMMARY                     ║");
            Debug.Log("╠════════════════════════════════════════════════════════════════╣");
            Debug.Log($"║  Total Resource Types:    {resourceTypes.Count,-36}║");
            Debug.Log($"║  ItemSO Reused:           {reusedCount,-36}║");
            Debug.Log($"║  ItemSO Created:          {createdCount,-36}║");
            Debug.Log($"║  Mappings Populated:      {mappings.Count,-36}║");
            Debug.Log("╠════════════════════════════════════════════════════════════════╣");
            Debug.Log("║  MAPPING DETAILS:                                              ║");
            Debug.Log(report.ToString());
            Debug.Log("╚════════════════════════════════════════════════════════════════╝");

            // Validate
            ValidateMappings(bridge, resourceTypes.Count);

            // Show dialog
            EditorUtility.DisplayDialog(
                "Resource Item Generation Complete",
                $"Successfully processed {resourceTypes.Count} resource types.\n\n" +
                $"• ItemSO Reused: {reusedCount}\n" +
                $"• ItemSO Created: {createdCount}\n" +
                $"• Mappings Populated: {mappings.Count}\n\n" +
                "Check Console for detailed report.",
                "OK"
            );
        }

        [MenuItem("Tools/Beneath The Floor/Generate/Validate Resource Mappings")]
        public static void ValidateResourceMappings()
        {
            var bridge = FindDigInventoryBridge();
            if (bridge == null)
            {
                Debug.LogError("[Validator] Could not find DigInventoryBridge!");
                return;
            }

            var allTypes = (UndergroundResourceType[])Enum.GetValues(typeof(UndergroundResourceType));
            int expectedCount = 0;
            foreach (var t in allTypes)
            {
                if (t != UndergroundResourceType.None)
                    expectedCount++;
            }

            ValidateMappings(bridge, expectedCount);
        }

        private static void CreateFolderIfNeeded(string path)
        {
            if (!AssetDatabase.IsValidFolder(path))
            {
                string[] parts = path.Split('/');
                string currentPath = parts[0]; // "Assets"

                for (int i = 1; i < parts.Length; i++)
                {
                    string nextPath = currentPath + "/" + parts[i];
                    if (!AssetDatabase.IsValidFolder(nextPath))
                    {
                        AssetDatabase.CreateFolder(currentPath, parts[i]);
                        Debug.Log($"[Generator] Created folder: {nextPath}");
                    }
                    currentPath = nextPath;
                }
            }
        }

        private static MaterialItemSO FindExistingItem(UndergroundResourceType resourceType)
        {
            string typeName = resourceType.ToString();

            // Search patterns
            string[] searchPatterns = new string[]
            {
                typeName,
                typeName + "_Item",
                typeName.ToLower(),
                typeName.ToLower() + "_item",
                FormatResourceName(typeName).Replace(" ", ""),
                FormatResourceName(typeName).Replace(" ", "_")
            };

            // Search for existing assets
            string[] guids = AssetDatabase.FindAssets("t:ItemSO");
            foreach (string guid in guids)
            {
                string assetPath = AssetDatabase.GUIDToAssetPath(guid);
                var item = AssetDatabase.LoadAssetAtPath<ItemSO>(assetPath);

                if (item != null)
                {
                    string assetName = Path.GetFileNameWithoutExtension(assetPath);

                    foreach (string pattern in searchPatterns)
                    {
                        if (string.Equals(assetName, pattern, StringComparison.OrdinalIgnoreCase) ||
                            string.Equals(item.itemId, $"underground_{typeName}", StringComparison.OrdinalIgnoreCase))
                        {
                            if (item is MaterialItemSO materialItem)
                            {
                                return materialItem;
                            }
                        }
                    }
                }
            }

            return null;
        }

        private static MaterialItemSO CreateNewItem(UndergroundResourceType resourceType)
        {
            var item = ScriptableObject.CreateInstance<MaterialItemSO>();

            string typeName = resourceType.ToString();
            string readableName = FormatResourceName(typeName);

            // Set basic properties
            item.itemId = $"underground_{typeName.ToLower()}";
            item.itemName = readableName;
            item.description = $"A {readableName.ToLower()} extracted from underground digging.";
            item.icon = null; // Designer will fill later
            item.category = ItemCategory.Material;
            item.isStackable = true;
            item.maxStackSize = 999;
            item.buyPrice = 0;
            item.sellPrice = GetSellPrice(resourceType);

            // Set material type based on resource
            item.materialType = GetMaterialType(resourceType);

            // Save asset
            string assetPath = $"{GeneratedAssetsPath}/{typeName}_Item.asset";
            AssetDatabase.CreateAsset(item, assetPath);

            return item;
        }

        private static string FormatResourceName(string name)
        {
            var sb = new StringBuilder();
            for (int i = 0; i < name.Length; i++)
            {
                if (i > 0 && char.IsUpper(name[i]))
                {
                    sb.Append(' ');
                }
                sb.Append(name[i]);
            }
            return sb.ToString();
        }

        private static int GetSellPrice(UndergroundResourceType type)
        {
            return type switch
            {
                // Layer 1 - Common (5-20m)
                UndergroundResourceType.Dirt => 1,
                UndergroundResourceType.SoftStone => 2,
                UndergroundResourceType.Clay => 3,
                UndergroundResourceType.IronNugget => 5,
                UndergroundResourceType.CopperFragment => 4,

                // Layer 2 - Uncommon (20-40m)
                UndergroundResourceType.HardSoil => 3,
                UndergroundResourceType.Stone => 4,
                UndergroundResourceType.Sandstone => 5,
                UndergroundResourceType.IronChunk => 8,
                UndergroundResourceType.CopperPiece => 7,
                UndergroundResourceType.Coal => 6,

                // Layer 3 - Rare (40-70m)
                UndergroundResourceType.HardSoilDeep => 5,
                UndergroundResourceType.HardStone => 6,
                UndergroundResourceType.Heatstone => 12,
                UndergroundResourceType.Quartz => 15,
                UndergroundResourceType.CrystalDust => 10,
                UndergroundResourceType.AncientOre => 20,

                // Layer 4 - Epic (70-110m)
                UndergroundResourceType.CrystalShard => 25,
                UndergroundResourceType.PurpleQuartz => 30,
                UndergroundResourceType.CrystalStone => 20,
                UndergroundResourceType.DeepCrystalVein => 35,
                UndergroundResourceType.LuminousDust => 28,

                // Layer 5 - Legendary (110-160m)
                UndergroundResourceType.CrystalCoreFragment => 50,
                UndergroundResourceType.BlueGreyOre => 45,

                // Layer 6 - Mythic (160-220m)
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

        private static DigInventoryBridge FindDigInventoryBridge()
        {
            // Try to find in scene
            var bridge = UnityEngine.Object.FindObjectOfType<DigInventoryBridge>();
            if (bridge != null)
                return bridge;

            // Try to find in all loaded objects (including inactive)
            var allBridges = Resources.FindObjectsOfTypeAll<DigInventoryBridge>();
            foreach (var b in allBridges)
            {
                if (!EditorUtility.IsPersistent(b) && b.gameObject.scene.IsValid())
                {
                    return b;
                }
            }

            return null;
        }

        private static void PopulateBridgeMappings(DigInventoryBridge bridge, List<ResourceItemMapping> mappings)
        {
            // Use SerializedObject to modify the list properly
            var serializedObject = new SerializedObject(bridge);
            var mappingsProperty = serializedObject.FindProperty("resourceMappings");

            if (mappingsProperty == null)
            {
                Debug.LogError("[Generator] Could not find 'resourceMappings' property!");
                return;
            }

            // Clear existing
            mappingsProperty.ClearArray();

            // Add all mappings
            for (int i = 0; i < mappings.Count; i++)
            {
                mappingsProperty.InsertArrayElementAtIndex(i);
                var element = mappingsProperty.GetArrayElementAtIndex(i);

                // Set undergroundType
                var typeProperty = element.FindPropertyRelative("undergroundType");
                if (typeProperty != null)
                {
                    typeProperty.enumValueIndex = (int)mappings[i].undergroundType;
                }

                // Set item reference
                var itemProperty = element.FindPropertyRelative("item");
                if (itemProperty != null)
                {
                    itemProperty.objectReferenceValue = mappings[i].item;
                }

                // Set defaultAmountPerDig
                var amountProperty = element.FindPropertyRelative("defaultAmountPerDig");
                if (amountProperty != null)
                {
                    amountProperty.intValue = mappings[i].defaultAmountPerDig;
                }
            }

            serializedObject.ApplyModifiedProperties();
            EditorUtility.SetDirty(bridge);

            Debug.Log($"[Generator] Applied {mappings.Count} mappings to DigInventoryBridge");
        }

        private static void ValidateMappings(DigInventoryBridge bridge, int expectedCount)
        {
            if (bridge == null)
            {
                Debug.LogError("[Validator] DigInventoryBridge is null!");
                return;
            }

            var report = bridge.ValidateMappings();

            Debug.Log("╔════════════════════════════════════════════════════════════════╗");
            Debug.Log("║                    VALIDATION RESULTS                          ║");
            Debug.Log("╠════════════════════════════════════════════════════════════════╣");
            Debug.Log($"║  Expected Mappings:       {expectedCount,-36}║");
            Debug.Log($"║  Actual Mapped Types:     {report.mappedTypeCount,-36}║");
            Debug.Log($"║  Valid (with ItemSO):     {report.validMappingCount,-36}║");
            Debug.Log($"║  Missing Mappings:        {report.missingTypes.Count,-36}║");
            Debug.Log($"║  Null ItemSO:             {report.nullItemTypes.Count,-36}║");
            Debug.Log($"║  Duplicates:              {report.duplicateTypes.Count,-36}║");
            Debug.Log("╠════════════════════════════════════════════════════════════════╣");

            if (report.IsFullyConfigured)
            {
                Debug.Log("║  ✓ ALL MAPPINGS VALID - 100% COVERAGE                         ║");
            }
            else
            {
                Debug.LogWarning("║  ✗ ISSUES FOUND - See details above                          ║");
            }

            Debug.Log("╚════════════════════════════════════════════════════════════════╝");
        }
    }
}
