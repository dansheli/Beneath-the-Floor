using UnityEngine;
using UnityEditor;
using System.Collections.Generic;
using System.IO;
using BeneathTheFloor.Digging;

namespace BeneathTheFloor.Editor
{
    [InitializeOnLoad]
    /// <summary>
    /// Editor tool to set up mineral rock prefabs for the resource drop system.
    /// Maps game resources to the "Low Poly Fantasy Minerals - Rocks" asset.
    /// </summary>
    public class MineralResourceSetup : EditorWindow
    {
        // Source asset path
        private const string ROCK_ASSET_PATH = "Assets/Octablade/Fantasy Minerals - Rock";
        private const string ROCK_PREFABS_PATH = ROCK_ASSET_PATH + "/Prefabs";

        // Target paths
        private const string ORES_PATH = "Assets/Art/Resources/Ores";
        private const string CRYSTALS_PATH = "Assets/Art/Resources/Crystals";

        // Mapping of game resources to rock asset combinations
        private static readonly Dictionary<UndergroundResourceType, ResourceMapping> ResourceMappings = new Dictionary<UndergroundResourceType, ResourceMapping>
        {
            // === ORES / METALS ===
            // Iron Nugget - small compact rock with dark metallic (Adamantine = dark purple-black metallic)
            { UndergroundResourceType.IronNugget, new ResourceMapping("Rock2", "Adam", 0.15f, "Ore_Iron_Nugget", true) },

            // Iron Chunk - larger version of iron
            { UndergroundResourceType.IronChunk, new ResourceMapping("Rock4", "Adam", 0.22f, "Ore_Iron_Chunk", true) },

            // Copper Fragment - small sharp rock with Copper material
            { UndergroundResourceType.CopperFragment, new ResourceMapping("Rock1", "Copp", 0.14f, "Ore_Copper_Fragment", true) },

            // Copper Piece - larger copper rock
            { UndergroundResourceType.CopperPiece, new ResourceMapping("Rock5", "Copp", 0.20f, "Ore_Copper_Piece", true) },

            // Ancient Ore - special looking with Orichalcum (gold-orange magical)
            { UndergroundResourceType.AncientOre, new ResourceMapping("Rock7", "Oric", 0.25f, "Ore_Ancient_Ore", true) },

            // Blue-Grey Ore - Azurite (blue crystal material)
            { UndergroundResourceType.BlueGreyOre, new ResourceMapping("Rock6", "Azur", 0.22f, "Ore_BlueGrey_Ore", true) },

            // Coal - dark stone (use Adamantine which is darkest)
            { UndergroundResourceType.Coal, new ResourceMapping("Rock3", "Adam", 0.18f, "Ore_Coal", true) },

            // === CRYSTALS / SPECIAL ===
            // Quartz - light bright stone (Platinum = white/silver crystal)
            { UndergroundResourceType.Quartz, new ResourceMapping("Rock8", "Plat", 0.20f, "Crystal_Quartz", false) },

            // Purple Quartz - Jade has a purple-ish tint, or use Mythril (blue-purple)
            { UndergroundResourceType.PurpleQuartz, new ResourceMapping("Rock9", "Myth", 0.22f, "Crystal_PurpleQuartz", false) },

            // Crystal Dust - small rock scaled down
            { UndergroundResourceType.CrystalDust, new ResourceMapping("Rock1", "Plat", 0.08f, "Crystal_Dust", false) },

            // Crystal Shard - small-medium crystal
            { UndergroundResourceType.CrystalShard, new ResourceMapping("Rock3", "Myth", 0.14f, "Crystal_Shard", false) },

            // Luminous Dust - small glowing (Gold for warm glow)
            { UndergroundResourceType.LuminousDust, new ResourceMapping("Rock2", "Gold", 0.09f, "Crystal_LuminousDust", false) },

            // Crystal Core Fragment - medium special crystal (Orichalcum)
            { UndergroundResourceType.CrystalCoreFragment, new ResourceMapping("Rock5", "Oric", 0.18f, "Crystal_CoreFragment", false) },

            // Core Crystal Chunk - larger impressive (Mythril)
            { UndergroundResourceType.CoreCrystalChunk, new ResourceMapping("Rock8", "Myth", 0.28f, "Crystal_CoreChunk", false) },

            // Crystal Stone - large crystal rock
            { UndergroundResourceType.CrystalStone, new ResourceMapping("Rock10", "Jade", 0.24f, "Crystal_Stone", false) },

            // Deep Crystal Vein - elongated rock (Rock6 is more elongated)
            { UndergroundResourceType.DeepCrystalVein, new ResourceMapping("Rock6", "Myth", 0.30f, "Crystal_DeepVein", false) },

            // Heatstone - warm fire-like (Gold material for warm orange)
            { UndergroundResourceType.Heatstone, new ResourceMapping("Rock4", "Gold", 0.20f, "Crystal_Heatstone", false) },

            // === COMMON MATERIALS (use simpler rocks) ===
            { UndergroundResourceType.Dirt, new ResourceMapping("Rock1", "Oliv", 0.12f, "Common_Dirt", true) },
            { UndergroundResourceType.Clay, new ResourceMapping("Rock2", "Copp", 0.13f, "Common_Clay", true) },
            { UndergroundResourceType.SoftStone, new ResourceMapping("Rock3", "Silv", 0.14f, "Common_SoftStone", true) },
            { UndergroundResourceType.Stone, new ResourceMapping("Rock4", "Silv", 0.16f, "Common_Stone", true) },
            { UndergroundResourceType.Sandstone, new ResourceMapping("Rock5", "Gold", 0.15f, "Common_Sandstone", true) },
            { UndergroundResourceType.HardSoil, new ResourceMapping("Rock2", "Adam", 0.14f, "Common_HardSoil", true) },
            { UndergroundResourceType.HardSoilDeep, new ResourceMapping("Rock3", "Adam", 0.15f, "Common_HardSoilDeep", true) },
            { UndergroundResourceType.HardStone, new ResourceMapping("Rock6", "Silv", 0.18f, "Common_HardStone", true) },
            { UndergroundResourceType.DeepBlackStone, new ResourceMapping("Rock7", "Adam", 0.22f, "Common_DeepBlackStone", true) },
        };

        [MenuItem("Beneath The Floor/Setup/Setup Mineral Resource Prefabs (Window)")]
        public static void ShowWindow()
        {
            GetWindow<MineralResourceSetup>("Mineral Resource Setup");
        }

        [MenuItem("Beneath The Floor/Setup/Run Mineral Resource Setup")]
        public static void RunSetupFromMenu()
        {
            RunFullSetup();
        }

        private Vector2 scrollPos;
        private bool showMappings = true;

        private void OnGUI()
        {
            EditorGUILayout.LabelField("Mineral Resource Prefab Setup", EditorStyles.boldLabel);
            EditorGUILayout.Space();

            EditorGUILayout.HelpBox(
                "This tool creates resource drop prefabs from the 'Low Poly Fantasy Minerals - Rocks' asset.\n\n" +
                "It will:\n" +
                "1. Create folder structure (Assets/Art/Resources/Ores & Crystals)\n" +
                "2. Create pickup-ready prefabs with proper scale and colliders\n" +
                "3. Create/Update ResourceDropConfig asset\n" +
                "4. Wire everything together",
                MessageType.Info);

            EditorGUILayout.Space();

            // Check if rock asset exists
            bool assetExists = AssetDatabase.IsValidFolder(ROCK_ASSET_PATH);
            if (!assetExists)
            {
                EditorGUILayout.HelpBox(
                    $"Rock asset not found at:\n{ROCK_ASSET_PATH}\n\nPlease import the 'Low Poly Fantasy Minerals - Rocks' asset first.",
                    MessageType.Error);
                return;
            }

            EditorGUILayout.LabelField($"Rock Asset Found: {ROCK_ASSET_PATH}", EditorStyles.miniLabel);
            EditorGUILayout.Space();

            // Show mappings
            showMappings = EditorGUILayout.Foldout(showMappings, $"Resource Mappings ({ResourceMappings.Count})");
            if (showMappings)
            {
                scrollPos = EditorGUILayout.BeginScrollView(scrollPos, GUILayout.Height(300));
                EditorGUI.indentLevel++;
                foreach (var kvp in ResourceMappings)
                {
                    var m = kvp.Value;
                    EditorGUILayout.LabelField($"{kvp.Key} -> {m.rockShape}_{m.materialCode} (scale: {m.scale:F2})");
                }
                EditorGUI.indentLevel--;
                EditorGUILayout.EndScrollView();
            }

            EditorGUILayout.Space();

            GUI.backgroundColor = Color.green;
            if (GUILayout.Button("Run Full Setup", GUILayout.Height(40)))
            {
                RunFullSetup();
            }
            GUI.backgroundColor = Color.white;

            EditorGUILayout.Space();

            if (GUILayout.Button("Create Folders Only"))
            {
                CreateFolderStructure();
            }

            if (GUILayout.Button("Create Prefabs Only"))
            {
                CreateAllPrefabs();
            }

            if (GUILayout.Button("Update ResourceDropConfig Only"))
            {
                UpdateResourceDropConfig();
            }
        }

        private static void RunFullSetup()
        {
            Debug.Log("=== Starting Mineral Resource Setup ===");

            CreateFolderStructure();
            CreateAllPrefabs();
            UpdateResourceDropConfig();

            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();

            PrintSummary();

            Debug.Log("=== Mineral Resource Setup Complete ===");
        }

        private static void CreateFolderStructure()
        {
            Debug.Log("[MineralResourceSetup] Creating folder structure...");

            // Create base Art folder if needed
            EnsureFolder("Assets", "Art");
            EnsureFolder("Assets/Art", "Resources");

            // Ores
            EnsureFolder("Assets/Art/Resources", "Ores");
            EnsureFolder(ORES_PATH, "Prefabs");
            EnsureFolder(ORES_PATH, "Materials");

            // Crystals
            EnsureFolder("Assets/Art/Resources", "Crystals");
            EnsureFolder(CRYSTALS_PATH, "Prefabs");
            EnsureFolder(CRYSTALS_PATH, "Materials");

            // Common (for dirt, stone, etc.)
            EnsureFolder("Assets/Art/Resources", "Common");
            EnsureFolder("Assets/Art/Resources/Common", "Prefabs");

            Debug.Log("[MineralResourceSetup] Folder structure created.");
        }

        private static void EnsureFolder(string parent, string folderName)
        {
            string fullPath = $"{parent}/{folderName}";
            if (!AssetDatabase.IsValidFolder(fullPath))
            {
                AssetDatabase.CreateFolder(parent, folderName);
                Debug.Log($"  Created: {fullPath}");
            }
        }

        private static void CreateAllPrefabs()
        {
            Debug.Log("[MineralResourceSetup] Creating prefabs...");

            int created = 0;
            int skipped = 0;

            foreach (var kvp in ResourceMappings)
            {
                var resourceType = kvp.Key;
                var mapping = kvp.Value;

                // Find source prefab
                string sourcePath = $"{ROCK_PREFABS_PATH}/{GetMaterialFolder(mapping.materialCode)}/{mapping.rockShape}_{mapping.materialCode}.prefab";
                GameObject sourcePrefab = AssetDatabase.LoadAssetAtPath<GameObject>(sourcePath);

                if (sourcePrefab == null)
                {
                    Debug.LogWarning($"  Source prefab not found: {sourcePath}");
                    skipped++;
                    continue;
                }

                // Determine target path
                string targetFolder;
                if (mapping.prefabName.StartsWith("Ore_"))
                    targetFolder = $"{ORES_PATH}/Prefabs";
                else if (mapping.prefabName.StartsWith("Crystal_"))
                    targetFolder = $"{CRYSTALS_PATH}/Prefabs";
                else
                    targetFolder = "Assets/Art/Resources/Common/Prefabs";

                string targetPath = $"{targetFolder}/{mapping.prefabName}.prefab";

                // Check if already exists
                if (AssetDatabase.LoadAssetAtPath<GameObject>(targetPath) != null)
                {
                    Debug.Log($"  Prefab exists, skipping: {mapping.prefabName}");
                    skipped++;
                    continue;
                }

                // Create the pickup prefab
                CreatePickupPrefab(sourcePrefab, targetPath, mapping, resourceType);
                created++;
            }

            Debug.Log($"[MineralResourceSetup] Prefabs: {created} created, {skipped} skipped.");
        }

        private static string GetMaterialFolder(string code)
        {
            return code switch
            {
                "Adam" => "Adamantine",
                "Azur" => "Azurite",
                "Copp" => "Copper",
                "Gold" => "Gold",
                "Jade" => "Jade",
                "Myth" => "Mythril",
                "Oliv" => "Olivite",
                "Oric" => "Orichalcum",
                "Plat" => "Platinum",
                "Silv" => "Silver",
                _ => code
            };
        }

        private static void CreatePickupPrefab(GameObject sourcePrefab, string targetPath, ResourceMapping mapping, UndergroundResourceType resourceType)
        {
            // Instantiate and configure
            GameObject instance = (GameObject)PrefabUtility.InstantiatePrefab(sourcePrefab);

            // Reset transform
            instance.transform.position = Vector3.zero;
            instance.transform.rotation = Quaternion.identity;

            // Apply scale
            instance.transform.localScale = Vector3.one * mapping.scale;

            // Ensure it has a collider for pickup
            if (instance.GetComponent<Collider>() == null)
            {
                // Add a sphere collider sized appropriately
                var collider = instance.AddComponent<SphereCollider>();
                collider.radius = 0.5f; // Will be scaled with the object
                collider.isTrigger = false; // Physical collider for physics
            }

            // Add Rigidbody for physics drops
            if (instance.GetComponent<Rigidbody>() == null)
            {
                var rb = instance.AddComponent<Rigidbody>();
                rb.mass = 0.1f;
                rb.drag = 1f;
                rb.angularDrag = 0.5f;
                rb.collisionDetectionMode = CollisionDetectionMode.Continuous;
            }

            // Set layer to "Pickup" if it exists, otherwise "Default"
            int pickupLayer = LayerMask.NameToLayer("Pickup");
            if (pickupLayer >= 0)
            {
                instance.layer = pickupLayer;
            }

            // Save as prefab variant
            GameObject prefab = PrefabUtility.SaveAsPrefabAsset(instance, targetPath);

            // Clean up instance
            DestroyImmediate(instance);

            Debug.Log($"  Created: {mapping.prefabName} -> {targetPath}");
        }

        private static void UpdateResourceDropConfig()
        {
            Debug.Log("[MineralResourceSetup] Updating ResourceDropConfig...");

            // Find or create the config asset
            string configPath = "Assets/ScriptableObjects/ResourceDropConfig.asset";
            ResourceDropConfig config = AssetDatabase.LoadAssetAtPath<ResourceDropConfig>(configPath);

            if (config == null)
            {
                // Create folder if needed
                EnsureFolder("Assets", "ScriptableObjects");

                // Create new config
                config = ScriptableObject.CreateInstance<ResourceDropConfig>();
                AssetDatabase.CreateAsset(config, configPath);
                Debug.Log($"  Created new ResourceDropConfig at: {configPath}");
            }

            // Initialize entries if empty
            if (config.entries == null)
                config.entries = new List<ResourceDropEntry>();

            // Update entries with our prefabs
            int updated = 0;
            foreach (var kvp in ResourceMappings)
            {
                var resourceType = kvp.Key;
                var mapping = kvp.Value;

                // Find the prefab
                string targetFolder;
                if (mapping.prefabName.StartsWith("Ore_"))
                    targetFolder = $"{ORES_PATH}/Prefabs";
                else if (mapping.prefabName.StartsWith("Crystal_"))
                    targetFolder = $"{CRYSTALS_PATH}/Prefabs";
                else
                    targetFolder = "Assets/Art/Resources/Common/Prefabs";

                string prefabPath = $"{targetFolder}/{mapping.prefabName}.prefab";
                GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(prefabPath);

                if (prefab == null)
                {
                    Debug.LogWarning($"  Prefab not found for config: {prefabPath}");
                    continue;
                }

                // Find or create entry
                ResourceDropEntry entry = null;
                foreach (var e in config.entries)
                {
                    if (e.resourceType == resourceType)
                    {
                        entry = e;
                        break;
                    }
                }

                if (entry == null)
                {
                    entry = new ResourceDropEntry();
                    entry.resourceType = resourceType;
                    config.entries.Add(entry);
                }

                // Update entry
                entry.prefab = prefab;
                entry.scale = 1f; // Scale is baked into prefab
                // Keep existing color or use default based on type
                if (entry.overrideColor.a < 0.01f)
                {
                    entry.overrideColor = GetDefaultColor(resourceType);
                }

                updated++;
            }

            EditorUtility.SetDirty(config);
            Debug.Log($"[MineralResourceSetup] Updated {updated} entries in ResourceDropConfig.");

            // Also try to assign to DigWorldDropSpawner if found
            AssignConfigToSpawner(config);
        }

        private static Color GetDefaultColor(UndergroundResourceType type)
        {
            // Colors that complement the rock materials
            return type switch
            {
                UndergroundResourceType.IronNugget => new Color(0.5f, 0.45f, 0.5f),
                UndergroundResourceType.IronChunk => new Color(0.45f, 0.4f, 0.45f),
                UndergroundResourceType.CopperFragment => new Color(0.85f, 0.55f, 0.35f),
                UndergroundResourceType.CopperPiece => new Color(0.9f, 0.6f, 0.4f),
                UndergroundResourceType.Coal => new Color(0.2f, 0.2f, 0.25f),
                UndergroundResourceType.AncientOre => new Color(0.7f, 0.55f, 0.3f),
                UndergroundResourceType.BlueGreyOre => new Color(0.4f, 0.5f, 0.7f),
                UndergroundResourceType.Quartz => new Color(0.95f, 0.95f, 1f),
                UndergroundResourceType.PurpleQuartz => new Color(0.6f, 0.4f, 0.8f),
                UndergroundResourceType.CrystalDust => new Color(0.9f, 0.9f, 1f),
                UndergroundResourceType.CrystalShard => new Color(0.5f, 0.4f, 0.8f),
                UndergroundResourceType.LuminousDust => new Color(1f, 0.95f, 0.6f),
                UndergroundResourceType.CrystalCoreFragment => new Color(0.8f, 0.5f, 0.3f),
                UndergroundResourceType.CoreCrystalChunk => new Color(0.5f, 0.5f, 0.9f),
                UndergroundResourceType.CrystalStone => new Color(0.4f, 0.6f, 0.5f),
                UndergroundResourceType.DeepCrystalVein => new Color(0.4f, 0.4f, 0.8f),
                UndergroundResourceType.Heatstone => new Color(1f, 0.7f, 0.3f),
                _ => new Color(0.6f, 0.55f, 0.5f)
            };
        }

        private static void AssignConfigToSpawner(ResourceDropConfig config)
        {
            // Try to find DigWorldDropSpawner in scene and assign config
            var spawner = Object.FindObjectOfType<DigWorldDropSpawner>();
            if (spawner != null)
            {
                // Use SerializedObject to set the field
                var so = new SerializedObject(spawner);
                var prop = so.FindProperty("dropConfig");
                if (prop != null)
                {
                    prop.objectReferenceValue = config;
                    so.ApplyModifiedProperties();
                    EditorUtility.SetDirty(spawner);
                    Debug.Log("[MineralResourceSetup] Assigned ResourceDropConfig to DigWorldDropSpawner in scene.");
                }
            }
            else
            {
                Debug.Log("[MineralResourceSetup] Note: DigWorldDropSpawner not found in scene. Manually assign the config.");
            }
        }

        private static void PrintSummary()
        {
            Debug.Log("\n========== MINERAL RESOURCE SETUP SUMMARY ==========");
            Debug.Log($"Rock Asset Root: {ROCK_ASSET_PATH}");
            Debug.Log($"Available Materials: Adamantine, Azurite, Copper, Gold, Jade, Mythril, Olivite, Orichalcum, Platinum, Silver");
            Debug.Log($"Rock Shapes: Rock1 through Rock10");
            Debug.Log("");
            Debug.Log("Resource -> Prefab Mappings:");

            foreach (var kvp in ResourceMappings)
            {
                string folder;
                if (kvp.Value.prefabName.StartsWith("Ore_"))
                    folder = ORES_PATH + "/Prefabs";
                else if (kvp.Value.prefabName.StartsWith("Crystal_"))
                    folder = CRYSTALS_PATH + "/Prefabs";
                else
                    folder = "Assets/Art/Resources/Common/Prefabs";

                Debug.Log($"  {kvp.Key} -> {folder}/{kvp.Value.prefabName}.prefab");
            }

            Debug.Log("");
            Debug.Log("Created Prefab Folders:");
            Debug.Log($"  {ORES_PATH}/Prefabs/");
            Debug.Log($"  {CRYSTALS_PATH}/Prefabs/");
            Debug.Log("  Assets/Art/Resources/Common/Prefabs/");
            Debug.Log("");
            Debug.Log("Config Asset: Assets/ScriptableObjects/ResourceDropConfig.asset");
            Debug.Log("");
            Debug.Log("NEXT STEPS:");
            Debug.Log("1. Verify prefabs look correct in the Project window");
            Debug.Log("2. Ensure DigWorldDropSpawner has the ResourceDropConfig assigned");
            Debug.Log("3. Enter Play Mode and test digging to see the new rock drops");
            Debug.Log("4. Adjust scales in ResourceDropConfig if needed");
            Debug.Log("====================================================\n");
        }

        /// <summary>
        /// Mapping data for a resource type to a rock asset combination.
        /// </summary>
        private class ResourceMapping
        {
            public string rockShape;      // e.g., "Rock1", "Rock2", etc.
            public string materialCode;   // e.g., "Adam", "Copp", etc.
            public float scale;           // Scale multiplier
            public string prefabName;     // Output prefab name
            public bool isOre;            // true for ores, false for crystals

            public ResourceMapping(string shape, string material, float scaleValue, string name, bool ore)
            {
                rockShape = shape;
                materialCode = material;
                scale = scaleValue;
                prefabName = name;
                isOre = ore;
            }
        }
    }
}
