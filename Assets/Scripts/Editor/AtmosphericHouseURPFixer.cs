using UnityEngine;
using UnityEditor;
using System.IO;

namespace BeneathTheFloor.Editor
{
    /// <summary>
    /// Upgrades all AtmosphericHouse materials from Standard shader to URP Lit shader.
    /// This fixes the pink materials issue when using URP.
    /// </summary>
    public class AtmosphericHouseURPFixer : EditorWindow
    {
        private const string ATMOSPHERIC_HOUSE_PATH = "Assets/AtmosphericHouse";

        [MenuItem("Beneath The Floor/Setup/Fix AtmosphericHouse Materials (URP)")]
        public static void UpgradeAllMaterials()
        {
            Debug.Log("=== Starting AtmosphericHouse URP Material Upgrade ===");

            // Find URP Lit shader
            Shader urpLit = Shader.Find("Universal Render Pipeline/Lit");
            if (urpLit == null)
            {
                urpLit = Shader.Find("Universal Render Pipeline/Simple Lit");
            }
            if (urpLit == null)
            {
                Debug.LogError("URP Lit shader not found! Make sure URP is installed.");
                return;
            }
            Debug.Log($"Using shader: {urpLit.name}");

            // Find all materials in AtmosphericHouse folder
            string[] matGuids = AssetDatabase.FindAssets("t:Material", new[] { ATMOSPHERIC_HOUSE_PATH });
            Debug.Log($"Found {matGuids.Length} materials to check.");

            int upgradedCount = 0;
            int skippedCount = 0;
            int errorCount = 0;

            foreach (string guid in matGuids)
            {
                string path = AssetDatabase.GUIDToAssetPath(guid);
                Material mat = AssetDatabase.LoadAssetAtPath<Material>(path);

                if (mat == null)
                {
                    Debug.LogWarning($"Failed to load material: {path}");
                    errorCount++;
                    continue;
                }

                string shaderName = mat.shader != null ? mat.shader.name : "NULL";

                // Skip if already URP
                if (shaderName.Contains("Universal Render Pipeline"))
                {
                    skippedCount++;
                    continue;
                }

                // Skip skybox materials
                if (shaderName.Contains("Skybox"))
                {
                    Debug.Log($"Skipping skybox: {path}");
                    skippedCount++;
                    continue;
                }

                // Upgrade this material
                bool success = UpgradeMaterialToURP(mat, urpLit, path);
                if (success)
                {
                    upgradedCount++;
                    Debug.Log($"  Upgraded: {path}");
                }
                else
                {
                    errorCount++;
                }
            }

            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();

            Debug.Log($"\n========== ATMOSPHERIC HOUSE URP UPGRADE SUMMARY ==========");
            Debug.Log($"Total materials found: {matGuids.Length}");
            Debug.Log($"Materials upgraded: {upgradedCount}");
            Debug.Log($"Materials skipped (already URP or skybox): {skippedCount}");
            Debug.Log($"Errors: {errorCount}");
            Debug.Log($"============================================================\n");
        }

        private static bool UpgradeMaterialToURP(Material mat, Shader urpLit, string path)
        {
            try
            {
                // Backup current property values
                Color baseColor = Color.white;
                Texture baseMap = null;
                Texture normalMap = null;
                Texture metallicMap = null;
                Texture occlusionMap = null;
                Texture emissionMap = null;
                Color emissionColor = Color.black;
                float metallic = 0f;
                float smoothness = 0.5f;
                float normalScale = 1f;
                float occlusionStrength = 1f;
                bool hasEmission = mat.IsKeywordEnabled("_EMISSION");
                Vector2 tiling = Vector2.one;
                Vector2 offset = Vector2.zero;

                // Read Standard shader properties
                if (mat.HasProperty("_Color"))
                    baseColor = mat.GetColor("_Color");
                if (mat.HasProperty("_MainTex"))
                {
                    baseMap = mat.GetTexture("_MainTex");
                    tiling = mat.GetTextureScale("_MainTex");
                    offset = mat.GetTextureOffset("_MainTex");
                }
                if (mat.HasProperty("_BumpMap"))
                    normalMap = mat.GetTexture("_BumpMap");
                if (mat.HasProperty("_BumpScale"))
                    normalScale = mat.GetFloat("_BumpScale");
                if (mat.HasProperty("_MetallicGlossMap"))
                    metallicMap = mat.GetTexture("_MetallicGlossMap");
                if (mat.HasProperty("_OcclusionMap"))
                    occlusionMap = mat.GetTexture("_OcclusionMap");
                if (mat.HasProperty("_OcclusionStrength"))
                    occlusionStrength = mat.GetFloat("_OcclusionStrength");
                if (mat.HasProperty("_EmissionMap"))
                    emissionMap = mat.GetTexture("_EmissionMap");
                if (mat.HasProperty("_EmissionColor"))
                    emissionColor = mat.GetColor("_EmissionColor");
                if (mat.HasProperty("_Metallic"))
                    metallic = mat.GetFloat("_Metallic");
                if (mat.HasProperty("_Glossiness"))
                    smoothness = mat.GetFloat("_Glossiness");
                else if (mat.HasProperty("_Smoothness"))
                    smoothness = mat.GetFloat("_Smoothness");

                // Change shader to URP Lit
                mat.shader = urpLit;

                // Apply values with URP property names
                mat.SetColor("_BaseColor", baseColor);

                if (baseMap != null)
                {
                    mat.SetTexture("_BaseMap", baseMap);
                    mat.SetTextureScale("_BaseMap", tiling);
                    mat.SetTextureOffset("_BaseMap", offset);
                }

                if (normalMap != null)
                {
                    mat.SetTexture("_BumpMap", normalMap);
                    mat.SetFloat("_BumpScale", normalScale);
                    mat.EnableKeyword("_NORMALMAP");
                }

                if (metallicMap != null)
                    mat.SetTexture("_MetallicGlossMap", metallicMap);

                mat.SetFloat("_Metallic", metallic);
                mat.SetFloat("_Smoothness", smoothness);

                if (occlusionMap != null)
                {
                    mat.SetTexture("_OcclusionMap", occlusionMap);
                    mat.SetFloat("_OcclusionStrength", occlusionStrength);
                }

                if (hasEmission || emissionColor != Color.black)
                {
                    mat.EnableKeyword("_EMISSION");
                    mat.SetColor("_EmissionColor", emissionColor);
                    if (emissionMap != null)
                        mat.SetTexture("_EmissionMap", emissionMap);
                }

                EditorUtility.SetDirty(mat);
                return true;
            }
            catch (System.Exception ex)
            {
                Debug.LogError($"Error upgrading material {path}: {ex.Message}");
                return false;
            }
        }
    }
}
