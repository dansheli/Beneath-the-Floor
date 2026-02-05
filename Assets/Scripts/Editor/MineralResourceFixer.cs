using UnityEngine;
using UnityEditor;
using System.Collections.Generic;
using System.IO;
using BeneathTheFloor.Digging;

namespace BeneathTheFloor.Editor
{
    /// <summary>
    /// Fixes pink materials on mineral resource prefabs and wires up the drop system.
    /// </summary>
    public class MineralResourceFixer : EditorWindow
    {
        private const string ROCK_ASSET_PATH = "Assets/Octablade/Fantasy Minerals - Rock";
        private const string ROCK_MATERIALS_PATH = ROCK_ASSET_PATH + "/Materials";
        private const string ROCK_PREFABS_PATH = ROCK_ASSET_PATH + "/Prefabs";

        private const string ORES_PREFABS_PATH = "Assets/Art/Resources/Ores/Prefabs";
        private const string CRYSTALS_PREFABS_PATH = "Assets/Art/Resources/Crystals/Prefabs";
        private const string COMMON_PREFABS_PATH = "Assets/Art/Resources/Common/Prefabs";

        // Mapping: prefab name -> (rock shape, material code)
        private static readonly Dictionary<string, (string rockShape, string materialCode)> PrefabMappings = new Dictionary<string, (string, string)>
        {
            // Ores
            { "Ore_Iron_Nugget", ("Rock2", "Adam") },
            { "Ore_Iron_Chunk", ("Rock4", "Adam") },
            { "Ore_Copper_Fragment", ("Rock1", "Copp") },
            { "Ore_Copper_Piece", ("Rock5", "Copp") },
            { "Ore_Ancient_Ore", ("Rock7", "Oric") },
            { "Ore_BlueGrey_Ore", ("Rock6", "Azur") },
            { "Ore_Coal", ("Rock3", "Adam") },

            // Crystals
            { "Crystal_Quartz", ("Rock8", "Plat") },
            { "Crystal_PurpleQuartz", ("Rock9", "Myth") },
            { "Crystal_Dust", ("Rock1", "Plat") },
            { "Crystal_Shard", ("Rock3", "Myth") },
            { "Crystal_LuminousDust", ("Rock2", "Gold") },
            { "Crystal_CoreFragment", ("Rock5", "Oric") },
            { "Crystal_CoreChunk", ("Rock8", "Myth") },
            { "Crystal_Stone", ("Rock10", "Jade") },
            { "Crystal_DeepVein", ("Rock6", "Myth") },
            { "Crystal_Heatstone", ("Rock4", "Gold") },

            // Common
            { "Common_Dirt", ("Rock1", "Oliv") },
            { "Common_Clay", ("Rock2", "Copp") },
            { "Common_SoftStone", ("Rock3", "Silv") },
            { "Common_Stone", ("Rock4", "Silv") },
            { "Common_Sandstone", ("Rock5", "Gold") },
            { "Common_HardSoil", ("Rock2", "Adam") },
            { "Common_HardSoilDeep", ("Rock3", "Adam") },
            { "Common_HardStone", ("Rock6", "Silv") },
            { "Common_DeepBlackStone", ("Rock7", "Adam") },
        };

        // Material code to folder name
        private static readonly Dictionary<string, string> MaterialFolders = new Dictionary<string, string>
        {
            { "Adam", "Adamantine" },
            { "Azur", "Azurite" },
            { "Copp", "Copper" },
            { "Gold", "Gold" },
            { "Jade", "Jade" },
            { "Myth", "Mythril" },
            { "Oliv", "Olivite" },
            { "Oric", "Orichalcum" },
            { "Plat", "Platinum" },
            { "Silv", "Silver" },
        };

        [MenuItem("Beneath The Floor/Setup/Fix Resource Prefab Scales")]
        public static void FixPrefabScales()
        {
            Debug.Log("=== Fixing Resource Prefab Scales ===");

            // All prefabs use scale 1.0
            var scaleByCategory = new Dictionary<string, float>
            {
                { "Ores", 1.0f },
                { "Crystals", 1.0f },
                { "Common", 1.0f }
            };

            string[] prefabFolders = { ORES_PREFABS_PATH, CRYSTALS_PREFABS_PATH, COMMON_PREFABS_PATH };
            int updated = 0;

            foreach (string folder in prefabFolders)
            {
                if (!AssetDatabase.IsValidFolder(folder)) continue;

                // Determine scale based on folder
                float scale = 0.5f;
                if (folder.Contains("Ores")) scale = scaleByCategory["Ores"];
                else if (folder.Contains("Crystals")) scale = scaleByCategory["Crystals"];
                else if (folder.Contains("Common")) scale = scaleByCategory["Common"];

                string[] prefabGuids = AssetDatabase.FindAssets("t:Prefab", new[] { folder });

                foreach (string guid in prefabGuids)
                {
                    string prefabPath = AssetDatabase.GUIDToAssetPath(guid);
                    GameObject prefabInstance = PrefabUtility.LoadPrefabContents(prefabPath);

                    if (prefabInstance != null)
                    {
                        prefabInstance.transform.localScale = Vector3.one * scale;
                        PrefabUtility.SaveAsPrefabAsset(prefabInstance, prefabPath);
                        PrefabUtility.UnloadPrefabContents(prefabInstance);
                        updated++;
                        Debug.Log($"  Scaled: {Path.GetFileName(prefabPath)} -> {scale}");
                    }
                }
            }

            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();

            Debug.Log($"=== Updated {updated} prefab scales ===");
        }

        [MenuItem("Beneath The Floor/Setup/Fix Mineral Resource Materials")]
        public static void RunFix()
        {
            Debug.Log("=== Starting Mineral Resource Material Fix ===");

            // FIRST: Upgrade all Octablade source materials to URP
            int upgradedMaterials = UpgradeOctabladeMaterialsToURP();
            Debug.Log($"Upgraded {upgradedMaterials} Octablade materials to URP.");

            int fixedCount = 0;
            int skippedCount = 0;
            List<string> fixedPrefabs = new List<string>();

            // Process all prefab folders
            string[] prefabFolders = { ORES_PREFABS_PATH, CRYSTALS_PREFABS_PATH, COMMON_PREFABS_PATH };

            foreach (string folder in prefabFolders)
            {
                if (!AssetDatabase.IsValidFolder(folder))
                {
                    Debug.LogWarning($"Folder not found: {folder}");
                    continue;
                }

                string[] prefabGuids = AssetDatabase.FindAssets("t:Prefab", new[] { folder });

                foreach (string guid in prefabGuids)
                {
                    string prefabPath = AssetDatabase.GUIDToAssetPath(guid);
                    string prefabName = Path.GetFileNameWithoutExtension(prefabPath);

                    if (PrefabMappings.TryGetValue(prefabName, out var mapping))
                    {
                        bool fixed_ = FixPrefabMaterial(prefabPath, prefabName, mapping.rockShape, mapping.materialCode);
                        if (fixed_)
                        {
                            fixedCount++;
                            fixedPrefabs.Add(prefabName);
                        }
                        else
                        {
                            skippedCount++;
                        }
                    }
                    else
                    {
                        Debug.LogWarning($"No mapping found for prefab: {prefabName}");
                        skippedCount++;
                    }
                }
            }

            // Update ResourceDropConfig
            UpdateResourceDropConfig();

            // Wire DigWorldDropSpawner
            WireDigWorldDropSpawner();

            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();

            // Print summary
            Debug.Log($"\n========== MINERAL RESOURCE FIX SUMMARY ==========");
            Debug.Log($"Rock Asset Root: {ROCK_ASSET_PATH}");
            Debug.Log($"Materials Upgraded to URP: {upgradedMaterials}");
            Debug.Log($"Prefabs Fixed: {fixedCount}");
            Debug.Log($"Prefabs Skipped: {skippedCount}");
            Debug.Log($"\nFixed Prefabs:");
            foreach (var name in fixedPrefabs)
            {
                Debug.Log($"  - {name}");
            }
            Debug.Log($"\nResourceDropConfig: Assets/ScriptableObjects/ResourceDropConfig.asset");
            Debug.Log($"DigWorldDropSpawner: Configured to use ResourceDropConfig");
            Debug.Log($"====================================================\n");

            Debug.Log("=== Mineral Resource Material Fix Complete ===");
        }

        private static int UpgradeOctabladeMaterialsToURP()
        {
            int count = 0;
            Shader urpLit = Shader.Find("Universal Render Pipeline/Lit");
            if (urpLit == null)
            {
                Debug.LogError("URP Lit shader not found! Make sure URP is installed.");
                // Try alternative URP shader names
                urpLit = Shader.Find("Universal Render Pipeline/Simple Lit");
                if (urpLit == null)
                {
                    urpLit = Shader.Find("Shader Graphs/Lit");
                }
                if (urpLit == null)
                {
                    Debug.LogError("No URP shaders found at all!");
                    return 0;
                }
            }
            Debug.Log($"Using shader: {urpLit.name}");

            // Find all materials in Octablade folder
            Debug.Log($"Searching for materials in: {ROCK_MATERIALS_PATH}");
            string[] matGuids = AssetDatabase.FindAssets("t:Material", new[] { ROCK_MATERIALS_PATH });
            Debug.Log($"Found {matGuids.Length} materials to check.");

            foreach (string guid in matGuids)
            {
                string path = AssetDatabase.GUIDToAssetPath(guid);
                Material mat = AssetDatabase.LoadAssetAtPath<Material>(path);
                if (mat == null)
                {
                    Debug.LogWarning($"Failed to load material: {path}");
                    continue;
                }

                string shaderName = mat.shader != null ? mat.shader.name : "NULL";

                // Skip if already URP
                if (shaderName.Contains("Universal Render Pipeline"))
                {
                    Debug.Log($"Already URP: {path}");
                    continue;
                }

                // Check if needs upgrade - upgrade ALL non-URP shaders
                bool needsUpgrade = !shaderName.Contains("Universal Render Pipeline");
                Debug.Log($"Checking: {path} - Shader: {shaderName} - NeedsUpgrade: {needsUpgrade}");

                if (needsUpgrade)
                {
                    // Backup current values
                    Color baseColor = Color.white;
                    Texture baseMap = null;
                    Texture normalMap = null;
                    Texture metallicMap = null;
                    Texture emissionMap = null;
                    Color emissionColor = Color.black;
                    float metallic = 0f;
                    float smoothness = 0.5f;
                    bool hasEmission = mat.IsKeywordEnabled("_EMISSION");

                    if (mat.HasProperty("_Color"))
                        baseColor = mat.GetColor("_Color");
                    if (mat.HasProperty("_MainTex"))
                        baseMap = mat.GetTexture("_MainTex");
                    if (mat.HasProperty("_BumpMap"))
                        normalMap = mat.GetTexture("_BumpMap");
                    if (mat.HasProperty("_MetallicGlossMap"))
                        metallicMap = mat.GetTexture("_MetallicGlossMap");
                    if (mat.HasProperty("_EmissionMap"))
                        emissionMap = mat.GetTexture("_EmissionMap");
                    if (mat.HasProperty("_EmissionColor"))
                        emissionColor = mat.GetColor("_EmissionColor");
                    if (mat.HasProperty("_Metallic"))
                        metallic = mat.GetFloat("_Metallic");
                    if (mat.HasProperty("_Glossiness"))
                        smoothness = mat.GetFloat("_Glossiness");

                    // Change shader
                    mat.shader = urpLit;

                    // Re-apply values with URP property names
                    mat.SetColor("_BaseColor", baseColor);
                    if (baseMap != null)
                        mat.SetTexture("_BaseMap", baseMap);
                    if (normalMap != null)
                        mat.SetTexture("_BumpMap", normalMap);
                    if (metallicMap != null)
                        mat.SetTexture("_MetallicGlossMap", metallicMap);
                    mat.SetFloat("_Metallic", metallic);
                    mat.SetFloat("_Smoothness", smoothness);

                    if (hasEmission)
                    {
                        mat.EnableKeyword("_EMISSION");
                        mat.SetColor("_EmissionColor", emissionColor);
                        if (emissionMap != null)
                            mat.SetTexture("_EmissionMap", emissionMap);
                    }

                    EditorUtility.SetDirty(mat);
                    count++;
                    Debug.Log($"  Upgraded: {path}");
                }
            }

            return count;
        }

        private static bool FixPrefabMaterial(string prefabPath, string prefabName, string rockShape, string materialCode)
        {
            // Load the prefab
            GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(prefabPath);
            if (prefab == null)
            {
                Debug.LogError($"Failed to load prefab: {prefabPath}");
                return false;
            }

            // Find the correct source material
            string materialFolder = MaterialFolders.GetValueOrDefault(materialCode, materialCode);
            string materialPath = $"{ROCK_MATERIALS_PATH}/{materialFolder}/{rockShape}_{materialCode}_Mat.mat";

            Material sourceMaterial = AssetDatabase.LoadAssetAtPath<Material>(materialPath);
            if (sourceMaterial == null)
            {
                // Try alternate naming conventions
                materialPath = $"{ROCK_MATERIALS_PATH}/{materialFolder}/{rockShape}_{materialCode}.mat";
                sourceMaterial = AssetDatabase.LoadAssetAtPath<Material>(materialPath);
            }

            if (sourceMaterial == null)
            {
                Debug.LogWarning($"Material not found for {prefabName}: tried {materialPath}");

                // Try to find ANY material with the rock shape and material code
                string searchPattern = $"{rockShape}_{materialCode}";
                string[] matGuids = AssetDatabase.FindAssets($"t:Material {searchPattern}", new[] { ROCK_MATERIALS_PATH });
                if (matGuids.Length > 0)
                {
                    materialPath = AssetDatabase.GUIDToAssetPath(matGuids[0]);
                    sourceMaterial = AssetDatabase.LoadAssetAtPath<Material>(materialPath);
                    Debug.Log($"  Found alternate material: {materialPath}");
                }
            }

            if (sourceMaterial == null)
            {
                Debug.LogError($"Could not find any material for {prefabName} ({rockShape}_{materialCode})");
                return false;
            }

            // Check if material uses URP shader
            if (sourceMaterial.shader.name.Contains("Standard") || sourceMaterial.shader.name.Contains("Legacy"))
            {
                // Need to convert to URP - create a URP version
                sourceMaterial = ConvertToURPMaterial(sourceMaterial, prefabName, materialCode);
            }

            // Open prefab for editing
            string prefabAssetPath = prefabPath;
            GameObject prefabInstance = PrefabUtility.LoadPrefabContents(prefabAssetPath);

            bool materialApplied = false;

            // Find all renderers and apply material
            MeshRenderer[] renderers = prefabInstance.GetComponentsInChildren<MeshRenderer>(true);
            foreach (var renderer in renderers)
            {
                Material[] materials = renderer.sharedMaterials;
                bool needsUpdate = false;

                for (int i = 0; i < materials.Length; i++)
                {
                    if (materials[i] == null || materials[i].shader.name.Contains("Hidden/InternalErrorShader") ||
                        materials[i].shader.name.Contains("Standard"))
                    {
                        materials[i] = sourceMaterial;
                        needsUpdate = true;
                        materialApplied = true;
                    }
                }

                if (needsUpdate)
                {
                    renderer.sharedMaterials = materials;
                }
            }

            // Also check for any renderer at root
            var rootRenderer = prefabInstance.GetComponent<MeshRenderer>();
            if (rootRenderer != null)
            {
                Material[] materials = rootRenderer.sharedMaterials;
                bool needsUpdate = false;

                for (int i = 0; i < materials.Length; i++)
                {
                    if (materials[i] == null || materials[i].shader.name.Contains("Hidden/InternalErrorShader") ||
                        materials[i].shader.name.Contains("Standard"))
                    {
                        materials[i] = sourceMaterial;
                        needsUpdate = true;
                        materialApplied = true;
                    }
                }

                if (needsUpdate)
                {
                    rootRenderer.sharedMaterials = materials;
                }
            }

            // Ensure prefab has proper components for pickup
            EnsurePickupComponents(prefabInstance);

            // Save the prefab
            PrefabUtility.SaveAsPrefabAsset(prefabInstance, prefabAssetPath);
            PrefabUtility.UnloadPrefabContents(prefabInstance);

            if (materialApplied)
            {
                Debug.Log($"  Fixed: {prefabName} <- {Path.GetFileName(materialPath)}");
            }
            else
            {
                Debug.Log($"  Already OK: {prefabName}");
            }

            return true;
        }

        private static Material ConvertToURPMaterial(Material source, string prefabName, string materialCode)
        {
            // Create a new URP material based on the source
            Shader urpLit = Shader.Find("Universal Render Pipeline/Lit");
            if (urpLit == null)
            {
                urpLit = Shader.Find("Universal Render Pipeline/Simple Lit");
            }
            if (urpLit == null)
            {
                Debug.LogWarning($"URP Lit shader not found, using source material as-is for {prefabName}");
                return source;
            }

            // Check if we already have a converted material
            string targetFolder = prefabName.StartsWith("Ore_") ? "Assets/Art/Resources/Ores/Materials" :
                                  prefabName.StartsWith("Crystal_") ? "Assets/Art/Resources/Crystals/Materials" :
                                  "Assets/Art/Resources/Common/Materials";

            EnsureFolder(targetFolder);

            string newMatPath = $"{targetFolder}/{prefabName}_Mat.mat";
            Material existingMat = AssetDatabase.LoadAssetAtPath<Material>(newMatPath);
            if (existingMat != null && existingMat.shader == urpLit)
            {
                return existingMat;
            }

            // Create new URP material
            Material newMat = new Material(urpLit);
            newMat.name = $"{prefabName}_Mat";

            // Copy properties from source
            if (source.HasProperty("_Color"))
                newMat.SetColor("_BaseColor", source.GetColor("_Color"));
            else if (source.HasProperty("_BaseColor"))
                newMat.SetColor("_BaseColor", source.GetColor("_BaseColor"));

            if (source.HasProperty("_MainTex"))
                newMat.SetTexture("_BaseMap", source.GetTexture("_MainTex"));
            else if (source.HasProperty("_BaseMap"))
                newMat.SetTexture("_BaseMap", source.GetTexture("_BaseMap"));

            if (source.HasProperty("_BumpMap"))
                newMat.SetTexture("_BumpMap", source.GetTexture("_BumpMap"));

            if (source.HasProperty("_MetallicGlossMap"))
                newMat.SetTexture("_MetallicGlossMap", source.GetTexture("_MetallicGlossMap"));

            if (source.HasProperty("_Metallic"))
                newMat.SetFloat("_Metallic", source.GetFloat("_Metallic"));

            if (source.HasProperty("_Smoothness"))
                newMat.SetFloat("_Smoothness", source.GetFloat("_Smoothness"));
            else if (source.HasProperty("_Glossiness"))
                newMat.SetFloat("_Smoothness", source.GetFloat("_Glossiness"));

            // Handle emission
            if (source.IsKeywordEnabled("_EMISSION") || source.HasProperty("_EmissionColor"))
            {
                newMat.EnableKeyword("_EMISSION");
                if (source.HasProperty("_EmissionColor"))
                    newMat.SetColor("_EmissionColor", source.GetColor("_EmissionColor"));
                if (source.HasProperty("_EmissionMap"))
                    newMat.SetTexture("_EmissionMap", source.GetTexture("_EmissionMap"));
            }

            // Save the new material
            AssetDatabase.CreateAsset(newMat, newMatPath);
            Debug.Log($"  Created URP material: {newMatPath}");

            return newMat;
        }

        private static void EnsurePickupComponents(GameObject prefab)
        {
            // Ensure collider exists
            if (prefab.GetComponent<Collider>() == null)
            {
                var col = prefab.AddComponent<SphereCollider>();
                col.radius = 0.5f;
                col.isTrigger = false;
            }

            // Ensure Rigidbody exists
            if (prefab.GetComponent<Rigidbody>() == null)
            {
                var rb = prefab.AddComponent<Rigidbody>();
                rb.mass = 0.1f;
                rb.drag = 1f;
                rb.angularDrag = 0.5f;
                rb.collisionDetectionMode = CollisionDetectionMode.Continuous;
            }
        }

        private static void EnsureFolder(string path)
        {
            if (AssetDatabase.IsValidFolder(path)) return;

            string[] parts = path.Split('/');
            string current = parts[0];
            for (int i = 1; i < parts.Length; i++)
            {
                string next = current + "/" + parts[i];
                if (!AssetDatabase.IsValidFolder(next))
                {
                    AssetDatabase.CreateFolder(current, parts[i]);
                }
                current = next;
            }
        }

        private static void UpdateResourceDropConfig()
        {
            Debug.Log("\n[MineralResourceFixer] Updating ResourceDropConfig...");

            string configPath = "Assets/ScriptableObjects/ResourceDropConfig.asset";
            ResourceDropConfig config = AssetDatabase.LoadAssetAtPath<ResourceDropConfig>(configPath);

            if (config == null)
            {
                // Create if doesn't exist
                EnsureFolder("Assets/ScriptableObjects");
                config = ScriptableObject.CreateInstance<ResourceDropConfig>();
                AssetDatabase.CreateAsset(config, configPath);
                Debug.Log($"  Created new ResourceDropConfig at: {configPath}");
            }

            if (config.entries == null)
                config.entries = new List<ResourceDropEntry>();

            // Map resource types to prefab paths
            var resourceToPrefab = new Dictionary<UndergroundResourceType, string>
            {
                { UndergroundResourceType.IronNugget, $"{ORES_PREFABS_PATH}/Ore_Iron_Nugget.prefab" },
                { UndergroundResourceType.IronChunk, $"{ORES_PREFABS_PATH}/Ore_Iron_Chunk.prefab" },
                { UndergroundResourceType.CopperFragment, $"{ORES_PREFABS_PATH}/Ore_Copper_Fragment.prefab" },
                { UndergroundResourceType.CopperPiece, $"{ORES_PREFABS_PATH}/Ore_Copper_Piece.prefab" },
                { UndergroundResourceType.AncientOre, $"{ORES_PREFABS_PATH}/Ore_Ancient_Ore.prefab" },
                { UndergroundResourceType.BlueGreyOre, $"{ORES_PREFABS_PATH}/Ore_BlueGrey_Ore.prefab" },
                { UndergroundResourceType.Coal, $"{ORES_PREFABS_PATH}/Ore_Coal.prefab" },

                { UndergroundResourceType.Quartz, $"{CRYSTALS_PREFABS_PATH}/Crystal_Quartz.prefab" },
                { UndergroundResourceType.PurpleQuartz, $"{CRYSTALS_PREFABS_PATH}/Crystal_PurpleQuartz.prefab" },
                { UndergroundResourceType.CrystalDust, $"{CRYSTALS_PREFABS_PATH}/Crystal_Dust.prefab" },
                { UndergroundResourceType.CrystalShard, $"{CRYSTALS_PREFABS_PATH}/Crystal_Shard.prefab" },
                { UndergroundResourceType.LuminousDust, $"{CRYSTALS_PREFABS_PATH}/Crystal_LuminousDust.prefab" },
                { UndergroundResourceType.CrystalCoreFragment, $"{CRYSTALS_PREFABS_PATH}/Crystal_CoreFragment.prefab" },
                { UndergroundResourceType.CoreCrystalChunk, $"{CRYSTALS_PREFABS_PATH}/Crystal_CoreChunk.prefab" },
                { UndergroundResourceType.CrystalStone, $"{CRYSTALS_PREFABS_PATH}/Crystal_Stone.prefab" },
                { UndergroundResourceType.DeepCrystalVein, $"{CRYSTALS_PREFABS_PATH}/Crystal_DeepVein.prefab" },
                { UndergroundResourceType.Heatstone, $"{CRYSTALS_PREFABS_PATH}/Crystal_Heatstone.prefab" },

                { UndergroundResourceType.Dirt, $"{COMMON_PREFABS_PATH}/Common_Dirt.prefab" },
                { UndergroundResourceType.Clay, $"{COMMON_PREFABS_PATH}/Common_Clay.prefab" },
                { UndergroundResourceType.SoftStone, $"{COMMON_PREFABS_PATH}/Common_SoftStone.prefab" },
                { UndergroundResourceType.Stone, $"{COMMON_PREFABS_PATH}/Common_Stone.prefab" },
                { UndergroundResourceType.Sandstone, $"{COMMON_PREFABS_PATH}/Common_Sandstone.prefab" },
                { UndergroundResourceType.HardSoil, $"{COMMON_PREFABS_PATH}/Common_HardSoil.prefab" },
                { UndergroundResourceType.HardSoilDeep, $"{COMMON_PREFABS_PATH}/Common_HardSoilDeep.prefab" },
                { UndergroundResourceType.HardStone, $"{COMMON_PREFABS_PATH}/Common_HardStone.prefab" },
                { UndergroundResourceType.DeepBlackStone, $"{COMMON_PREFABS_PATH}/Common_DeepBlackStone.prefab" },
            };

            int updated = 0;
            foreach (var kvp in resourceToPrefab)
            {
                var resourceType = kvp.Key;
                var prefabPath = kvp.Value;

                GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(prefabPath);
                if (prefab == null)
                {
                    Debug.LogWarning($"  Prefab not found: {prefabPath}");
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

                entry.prefab = prefab;
                entry.scale = 1f;
                updated++;
            }

            EditorUtility.SetDirty(config);
            Debug.Log($"  Updated {updated} entries in ResourceDropConfig.");
        }

        private static void WireDigWorldDropSpawner()
        {
            Debug.Log("\n[MineralResourceFixer] Wiring DigWorldDropSpawner...");

            string configPath = "Assets/ScriptableObjects/ResourceDropConfig.asset";
            ResourceDropConfig config = AssetDatabase.LoadAssetAtPath<ResourceDropConfig>(configPath);

            if (config == null)
            {
                Debug.LogError("  ResourceDropConfig not found!");
                return;
            }

            // Find DigWorldDropSpawner in scene
            var spawner = Object.FindObjectOfType<DigWorldDropSpawner>();
            if (spawner != null)
            {
                var so = new SerializedObject(spawner);
                var prop = so.FindProperty("dropConfig");
                if (prop != null)
                {
                    prop.objectReferenceValue = config;
                    so.ApplyModifiedProperties();
                    EditorUtility.SetDirty(spawner);
                    Debug.Log($"  Assigned ResourceDropConfig to DigWorldDropSpawner in scene.");
                }

                // Disable the runtime fallback sphere creation
                var fallbackProp = so.FindProperty("createDefaultPrefabIfMissing");
                if (fallbackProp != null)
                {
                    fallbackProp.boolValue = false;
                    so.ApplyModifiedProperties();
                    Debug.Log($"  Disabled fallback sphere creation.");
                }
            }
            else
            {
                Debug.LogWarning("  DigWorldDropSpawner not found in scene. Please manually assign the ResourceDropConfig.");
            }

            // Also try to find it in prefabs
            string[] prefabGuids = AssetDatabase.FindAssets("t:Prefab DigWorldDropSpawner");
            foreach (string guid in prefabGuids)
            {
                string path = AssetDatabase.GUIDToAssetPath(guid);
                GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(path);
                if (prefab != null)
                {
                    var spawnerComp = prefab.GetComponent<DigWorldDropSpawner>();
                    if (spawnerComp != null)
                    {
                        var so = new SerializedObject(spawnerComp);
                        var prop = so.FindProperty("dropConfig");
                        if (prop != null)
                        {
                            prop.objectReferenceValue = config;
                            so.ApplyModifiedProperties();
                            EditorUtility.SetDirty(prefab);
                            Debug.Log($"  Assigned ResourceDropConfig to DigWorldDropSpawner prefab: {path}");
                        }
                    }
                }
            }
        }
    }
}
