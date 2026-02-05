using UnityEngine;
using UnityEditor;
using BeneathTheFloor.Digging;

namespace BeneathTheFloor.Editor
{
    /// <summary>
    /// Creates DigToolProfile assets for all tools (Shovel, Heavy Spade, etc.).
    /// </summary>
    public class CreateShovelProfiles
    {
        [MenuItem("Tools/Beneath The Floor/Create Shovel Profiles")]
        public static void CreateProfiles()
        {
            string folderPath = "Assets/GameData/ToolProfiles";

            // Ensure folder exists
            if (!AssetDatabase.IsValidFolder(folderPath))
            {
                AssetDatabase.CreateFolder("Assets/GameData", "ToolProfiles");
            }

            // ============================================
            // TOOL 1: SHOVEL - All tiers share same max depth (15m)
            // Tiers are visual progression with power/radius increases
            // ============================================

            float shovelMaxDepth = 15f; // Same for all shovel tiers

            // Create Base Shovel (starting tool - no upgrades)
            CreateProfile(folderPath, "BaseShovel", new ProfileData
            {
                toolId = "base_shovel",
                displayName = "Wooden Shovel",
                digRadius = 0.4f,
                digPower = 0.6f,
                digDuration = 0.6f,
                energyCostPerDig = 5f,
                maxDigDepth = shovelMaxDepth,
                resourceEfficiency = 0.8f,
                hardnessRating = 1.0f
            });

            // Create Tier 1 Shovel (after 1st upgrade)
            CreateProfile(folderPath, "Tier1_Shovel", new ProfileData
            {
                toolId = "tier1_shovel",
                displayName = "Reinforced Shovel",
                digRadius = 0.5f,
                digPower = 0.8f,
                digDuration = 0.55f,
                energyCostPerDig = 5f,
                maxDigDepth = shovelMaxDepth,
                resourceEfficiency = 1.0f,
                hardnessRating = 1.2f
            });

            // Create Tier 2 Shovel (after 2nd upgrade)
            CreateProfile(folderPath, "Tier2_Shovel", new ProfileData
            {
                toolId = "tier2_shovel",
                displayName = "Iron-Tipped Shovel",
                digRadius = 0.6f,
                digPower = 1.0f,
                digDuration = 0.5f,
                energyCostPerDig = 5f,
                maxDigDepth = shovelMaxDepth,
                resourceEfficiency = 1.2f,
                hardnessRating = 1.4f
            });

            // Create Tier 3 Shovel (after 3rd upgrade - max for Tool 1)
            CreateProfile(folderPath, "Tier3_Shovel", new ProfileData
            {
                toolId = "tier3_shovel",
                displayName = "Steel Shovel",
                digRadius = 0.7f,
                digPower = 1.2f,
                digDuration = 0.45f,
                energyCostPerDig = 5f,
                maxDigDepth = shovelMaxDepth,
                resourceEfficiency = 1.5f,
                hardnessRating = 1.5f
            });

            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();

            Debug.Log("[CreateToolProfiles] Created 4 shovel profiles in " + folderPath);
            EditorUtility.DisplayDialog("Done", "Created 4 Shovel DigToolProfile assets:\n\n" +
                "All share same Max Depth (15m)\n" +
                "Power/Radius increase per tier:\n\n" +
                "- BaseShovel (Power: 0.6, Radius: 0.4)\n" +
                "- Tier1_Shovel (Power: 0.8, Radius: 0.5)\n" +
                "- Tier2_Shovel (Power: 1.0, Radius: 0.6)\n" +
                "- Tier3_Shovel (Power: 1.2, Radius: 0.7)\n\n" +
                "Location: " + folderPath, "OK");
        }

        [MenuItem("Tools/Beneath The Floor/Create Heavy Spade Profiles (Tool 2)")]
        public static void CreateHeavySpadeProfiles()
        {
            string folderPath = "Assets/GameData/ToolProfiles";

            // Ensure folder exists
            if (!AssetDatabase.IsValidFolder(folderPath))
            {
                AssetDatabase.CreateFolder("Assets/GameData", "ToolProfiles");
            }

            // ============================================
            // TOOL 2: HEAVY SPADE - All tiers share same max depth (30m)
            // Tiers are visual progression with power/radius increases
            // ============================================

            float heavySpadeMaxDepth = 30f; // Same for all heavy spade tiers

            // Create Base Heavy Spade (starting Tool 2 - after unlocking)
            CreateProfile(folderPath, "HeavySpade_Base", new ProfileData
            {
                toolId = "heavyspade_base",
                displayName = "Heavy Spade",
                digRadius = 0.5f,
                digPower = 1.0f,
                digDuration = 0.55f,
                energyCostPerDig = 6f,
                maxDigDepth = heavySpadeMaxDepth,
                resourceEfficiency = 1.2f,
                hardnessRating = 1.8f
            });

            // Create Tier 1 Heavy Spade (after 1st upgrade)
            CreateProfile(folderPath, "HeavySpade_Tier1", new ProfileData
            {
                toolId = "heavyspade_tier1",
                displayName = "Reinforced Heavy Spade",
                digRadius = 0.6f,
                digPower = 1.3f,
                digDuration = 0.5f,
                energyCostPerDig = 6f,
                maxDigDepth = heavySpadeMaxDepth,
                resourceEfficiency = 1.4f,
                hardnessRating = 2.0f
            });

            // Create Tier 2 Heavy Spade (after 2nd upgrade)
            CreateProfile(folderPath, "HeavySpade_Tier2", new ProfileData
            {
                toolId = "heavyspade_tier2",
                displayName = "Iron Heavy Spade",
                digRadius = 0.7f,
                digPower = 1.6f,
                digDuration = 0.45f,
                energyCostPerDig = 6f,
                maxDigDepth = heavySpadeMaxDepth,
                resourceEfficiency = 1.6f,
                hardnessRating = 2.3f
            });

            // Create Tier 3 Heavy Spade (after 3rd upgrade - max for Tool 2)
            CreateProfile(folderPath, "HeavySpade_Tier3", new ProfileData
            {
                toolId = "heavyspade_tier3",
                displayName = "Steel Heavy Spade",
                digRadius = 0.8f,
                digPower = 2.0f,
                digDuration = 0.4f,
                energyCostPerDig = 6f,
                maxDigDepth = heavySpadeMaxDepth,
                resourceEfficiency = 1.8f,
                hardnessRating = 2.5f
            });

            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();

            Debug.Log("[CreateToolProfiles] Created 4 Heavy Spade profiles in " + folderPath);
            EditorUtility.DisplayDialog("Done", "Created 4 Heavy Spade DigToolProfile assets:\n\n" +
                "All share same Max Depth (30m)\n" +
                "Power/Radius increase per tier:\n\n" +
                "- HeavySpade_Base (Power: 1.0, Radius: 0.5)\n" +
                "- HeavySpade_Tier1 (Power: 1.3, Radius: 0.6)\n" +
                "- HeavySpade_Tier2 (Power: 1.6, Radius: 0.7)\n" +
                "- HeavySpade_Tier3 (Power: 2.0, Radius: 0.8)\n\n" +
                "Location: " + folderPath, "OK");
        }

        [MenuItem("Tools/Beneath The Floor/Create Pickaxe Profiles (Tool 3)")]
        public static void CreatePickaxeProfiles()
        {
            string folderPath = "Assets/GameData/ToolProfiles";

            // Ensure folder exists
            if (!AssetDatabase.IsValidFolder(folderPath))
            {
                AssetDatabase.CreateFolder("Assets/GameData", "ToolProfiles");
            }

            // ============================================
            // TOOL 3: PICKAXE - All tiers share same max depth (50m)
            // Tiers are visual progression with power/radius increases
            // ============================================

            float pickaxeMaxDepth = 50f; // Same for all pickaxe tiers

            // Create Base Pickaxe (starting Tool 3 - after unlocking)
            CreateProfile(folderPath, "Pickaxe_Base", new ProfileData
            {
                toolId = "pickaxe_base",
                displayName = "Pickaxe",
                digRadius = 0.6f,
                digPower = 1.5f,
                digDuration = 0.5f,
                energyCostPerDig = 7f,
                maxDigDepth = pickaxeMaxDepth,
                resourceEfficiency = 1.5f,
                hardnessRating = 2.5f
            });

            // Create Tier 1 Pickaxe (after 1st upgrade)
            CreateProfile(folderPath, "Pickaxe_Tier1", new ProfileData
            {
                toolId = "pickaxe_tier1",
                displayName = "Reinforced Pickaxe",
                digRadius = 0.7f,
                digPower = 1.8f,
                digDuration = 0.45f,
                energyCostPerDig = 7f,
                maxDigDepth = pickaxeMaxDepth,
                resourceEfficiency = 1.7f,
                hardnessRating = 2.8f
            });

            // Create Tier 2 Pickaxe (after 2nd upgrade)
            CreateProfile(folderPath, "Pickaxe_Tier2", new ProfileData
            {
                toolId = "pickaxe_tier2",
                displayName = "Iron Pickaxe",
                digRadius = 0.8f,
                digPower = 2.2f,
                digDuration = 0.4f,
                energyCostPerDig = 7f,
                maxDigDepth = pickaxeMaxDepth,
                resourceEfficiency = 1.9f,
                hardnessRating = 3.0f
            });

            // Create Tier 3 Pickaxe (after 3rd upgrade - max for Tool 3)
            CreateProfile(folderPath, "Pickaxe_Tier3", new ProfileData
            {
                toolId = "pickaxe_tier3",
                displayName = "Steel Pickaxe",
                digRadius = 0.9f,
                digPower = 2.5f,
                digDuration = 0.35f,
                energyCostPerDig = 7f,
                maxDigDepth = pickaxeMaxDepth,
                resourceEfficiency = 2.0f,
                hardnessRating = 3.5f
            });

            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();

            Debug.Log("[CreateToolProfiles] Created 4 Pickaxe profiles in " + folderPath);
            EditorUtility.DisplayDialog("Done", "Created 4 Pickaxe DigToolProfile assets:\n\n" +
                "All share same Max Depth (50m)\n" +
                "Power/Radius increase per tier:\n\n" +
                "- Pickaxe_Base (Power: 1.5, Radius: 0.6)\n" +
                "- Pickaxe_Tier1 (Power: 1.8, Radius: 0.7)\n" +
                "- Pickaxe_Tier2 (Power: 2.2, Radius: 0.8)\n" +
                "- Pickaxe_Tier3 (Power: 2.5, Radius: 0.9)\n\n" +
                "Location: " + folderPath, "OK");
        }

        [MenuItem("Tools/Beneath The Floor/Create All Tool Profiles")]
        public static void CreateAllProfiles()
        {
            CreateProfiles();           // Tool 1: Shovel
            CreateHeavySpadeProfiles(); // Tool 2: Heavy Spade
            CreatePickaxeProfiles();    // Tool 3: Pickaxe

            EditorUtility.DisplayDialog("All Profiles Created",
                "Created profiles for:\n\n" +
                "Tool 1: Shovel (4 tiers, depth 15m)\n" +
                "Tool 2: Heavy Spade (4 tiers, depth 30m)\n" +
                "Tool 3: Pickaxe (4 tiers, depth 50m)\n\n" +
                "Location: Assets/GameData/ToolProfiles/", "OK");
        }

        private static void CreateProfile(string folderPath, string fileName, ProfileData data)
        {
            string assetPath = $"{folderPath}/{fileName}.asset";

            // Check if already exists
            DigToolProfile existing = AssetDatabase.LoadAssetAtPath<DigToolProfile>(assetPath);
            if (existing != null)
            {
                // Update existing
                existing.toolId = data.toolId;
                existing.displayName = data.displayName;
                existing.digRadius = data.digRadius;
                existing.digPower = data.digPower;
                existing.digDuration = data.digDuration;
                existing.energyCostPerDig = data.energyCostPerDig;
                existing.maxDigDepth = data.maxDigDepth;
                existing.resourceEfficiency = data.resourceEfficiency;
                existing.hardnessRating = data.hardnessRating;
                EditorUtility.SetDirty(existing);
                Debug.Log($"[CreateShovelProfiles] Updated existing: {fileName}");
            }
            else
            {
                // Create new
                DigToolProfile profile = ScriptableObject.CreateInstance<DigToolProfile>();
                profile.toolId = data.toolId;
                profile.displayName = data.displayName;
                profile.digRadius = data.digRadius;
                profile.digPower = data.digPower;
                profile.digDuration = data.digDuration;
                profile.energyCostPerDig = data.energyCostPerDig;
                profile.maxDigDepth = data.maxDigDepth;
                profile.resourceEfficiency = data.resourceEfficiency;
                profile.hardnessRating = data.hardnessRating;

                AssetDatabase.CreateAsset(profile, assetPath);
                Debug.Log($"[CreateShovelProfiles] Created: {fileName}");
            }
        }

        private struct ProfileData
        {
            public string toolId;
            public string displayName;
            public float digRadius;
            public float digPower;
            public float digDuration;
            public float energyCostPerDig;
            public float maxDigDepth;
            public float resourceEfficiency;
            public float hardnessRating;
        }
    }
}
