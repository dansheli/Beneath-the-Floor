using UnityEngine;
using UnityEditor;
using BeneathTheFloor.Digging;

/// <summary>
/// Editor utility to assign the triplanar terrain material to V3 ChunkManager.
/// </summary>
public static class V3TerrainMaterialSetup
{
    private const string TRIPLANAR_MATERIAL_PATH = "Assets/Materials/TriplanarTerrain/M_Terrain_Dirt_08_Triplanar.mat";
    private const string FALLBACK_MATERIAL_PATH = "Assets/Materials/TriplanarSoil.mat";

    [MenuItem("Tools/Beneath The Floor/V3 Terrain/Assign Triplanar Material to ChunkManager")]
    public static void AssignTriplanarMaterial()
    {
        // Find ChunkManager in scene
        var chunkManager = Object.FindObjectOfType<ChunkManager>();
        if (chunkManager == null)
        {
            Debug.LogError("[V3TerrainMaterialSetup] No ChunkManager found in scene!");
            return;
        }

        // Load the triplanar material
        Material material = AssetDatabase.LoadAssetAtPath<Material>(TRIPLANAR_MATERIAL_PATH);
        if (material == null)
        {
            Debug.LogWarning($"[V3TerrainMaterialSetup] Primary material not found at {TRIPLANAR_MATERIAL_PATH}, trying fallback...");
            material = AssetDatabase.LoadAssetAtPath<Material>(FALLBACK_MATERIAL_PATH);
        }

        if (material == null)
        {
            Debug.LogError("[V3TerrainMaterialSetup] No triplanar material found! Create one first.");
            return;
        }

        // Get the serialized property and assign
        SerializedObject so = new SerializedObject(chunkManager);
        SerializedProperty matProp = so.FindProperty("terrainMaterial");

        if (matProp != null)
        {
            matProp.objectReferenceValue = material;
            so.ApplyModifiedProperties();
            EditorUtility.SetDirty(chunkManager);
            Debug.Log($"[V3TerrainMaterialSetup] Assigned '{material.name}' to ChunkManager.terrainMaterial");
        }
        else
        {
            Debug.LogError("[V3TerrainMaterialSetup] Could not find 'terrainMaterial' property on ChunkManager!");
        }
    }

    [MenuItem("Tools/Beneath The Floor/V3 Terrain/Create Dark Dirt Material Variant")]
    public static void CreateDarkDirtMaterial()
    {
        // Load the shader
        Shader shader = Shader.Find("BeneathTheFloor/TriplanarSoil");
        if (shader == null)
        {
            Debug.LogError("[V3TerrainMaterialSetup] TriplanarSoil shader not found!");
            return;
        }

        // Create new material
        Material mat = new Material(shader);
        mat.name = "M_Terrain_DarkDirt_V3";

        // Configure for dark dirt appearance (matching user's reference)
        // Dark brown base
        mat.SetColor("_DirtColor", new Color(0.25f, 0.18f, 0.12f, 1f)); // Darker dirt
        mat.SetColor("_RockColor", new Color(0.2f, 0.17f, 0.14f, 1f));  // Darker rock

        // Low smoothness (high roughness)
        mat.SetFloat("_Smoothness", 0.05f);
        mat.SetFloat("_Metallic", 0f);

        // Triplanar settings
        mat.SetFloat("_TilingScale", 2.5f);  // Slightly larger for more detail
        mat.SetFloat("_TriplanarSharpness", 6f);
        mat.SetFloat("_NormalStrength", 1.5f);

        // Layer blending - more rock on walls
        mat.SetFloat("_WallRockBlendStrength", 2.0f);
        mat.SetFloat("_WallSteepnessThreshold", 0.35f);

        // Macro variation for natural look
        mat.SetFloat("_MacroScale", 0.1f);
        mat.SetFloat("_MacroStrength", 0.08f);
        mat.SetFloat("_MacroRoughnessVar", 0.04f);

        // Detail normal for gravel/pebble texture
        mat.SetFloat("_DetailTiling", 18f);
        mat.SetFloat("_DetailNormalStrength", 0.45f);

        // Depth and cavity darkening
        mat.SetFloat("_DepthDarkenStrength", 0.35f);
        mat.SetFloat("_DepthStart", -5f);
        mat.SetFloat("_DepthRange", 60f);
        mat.SetFloat("_CavityDarkenStrength", 0.3f);
        mat.SetFloat("_CavityAOPower", 1.8f);

        // Try to assign textures from Dirt_08
        TryAssignDirtTextures(mat, "Dirt_08");

        // Save material
        string savePath = "Assets/Materials/TriplanarTerrain/M_Terrain_DarkDirt_V3.mat";
        EnsureDirectoryExists("Assets/Materials/TriplanarTerrain");

        AssetDatabase.CreateAsset(mat, savePath);
        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();

        Selection.activeObject = mat;
        EditorGUIUtility.PingObject(mat);

        Debug.Log($"[V3TerrainMaterialSetup] Created dark dirt material at {savePath}");
    }

    private static void TryAssignDirtTextures(Material mat, string dirtName)
    {
        string basePath = $"Assets/PBR Ground Materials #1 [Dirt& Grass]/Textures/{dirtName}/";

        // Diffuse
        Texture2D diffuse = AssetDatabase.LoadAssetAtPath<Texture2D>(basePath + $"{dirtName}_Diffuse.tga");
        if (diffuse != null)
        {
            mat.SetTexture("_DirtTex", diffuse);
            mat.SetTexture("_RockTex", diffuse); // Use same for rock for now
            Debug.Log($"[V3TerrainMaterialSetup] Assigned {dirtName}_Diffuse");
        }

        // Normal
        Texture2D normal = AssetDatabase.LoadAssetAtPath<Texture2D>(basePath + $"{dirtName}_Normal.tga");
        if (normal != null)
        {
            mat.SetTexture("_DirtBumpMap", normal);
            mat.SetTexture("_RockBumpMap", normal);
            Debug.Log($"[V3TerrainMaterialSetup] Assigned {dirtName}_Normal");
        }
    }

    private static void EnsureDirectoryExists(string path)
    {
        if (!AssetDatabase.IsValidFolder(path))
        {
            string parent = System.IO.Path.GetDirectoryName(path).Replace("\\", "/");
            string folder = System.IO.Path.GetFileName(path);

            if (!AssetDatabase.IsValidFolder(parent))
            {
                EnsureDirectoryExists(parent);
            }

            AssetDatabase.CreateFolder(parent, folder);
        }
    }

    [MenuItem("Tools/Beneath The Floor/V3 Terrain/List Available Dirt Textures")]
    public static void ListDirtTextures()
    {
        string basePath = "Assets/PBR Ground Materials #1 [Dirt& Grass]/Textures";
        string[] folders = AssetDatabase.GetSubFolders(basePath);

        Debug.Log($"[V3TerrainMaterialSetup] Found {folders.Length} texture folders:");
        foreach (string folder in folders)
        {
            string name = System.IO.Path.GetFileName(folder);
            if (name.StartsWith("Dirt_"))
            {
                Debug.Log($"  - {name}");
            }
        }
    }
}
