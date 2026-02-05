using UnityEngine;
using UnityEditor;
using System.IO;

namespace BeneathTheFloor.Editor
{
    /// <summary>
    /// Editor utility to convert Built-in Standard materials to URP Lit shader.
    /// </summary>
    public class ConvertMaterialsToURP : EditorWindow
    {
        [MenuItem("Tools/Convert PBR Materials to URP")]
        public static void ConvertPBRMaterials()
        {
            string folderPath = "Assets/PBR Ground Materials #1 [Dirt& Grass]/Materials";

            if (!AssetDatabase.IsValidFolder(folderPath))
            {
                Debug.LogError($"Folder not found: {folderPath}");
                return;
            }

            // Find URP Lit shader
            Shader urpLit = Shader.Find("Universal Render Pipeline/Lit");
            if (urpLit == null)
            {
                Debug.LogError("URP Lit shader not found! Make sure URP is installed.");
                return;
            }

            // Find all materials in folder
            string[] guids = AssetDatabase.FindAssets("t:Material", new[] { folderPath });
            int convertedCount = 0;

            foreach (string guid in guids)
            {
                string path = AssetDatabase.GUIDToAssetPath(guid);
                Material mat = AssetDatabase.LoadAssetAtPath<Material>(path);

                if (mat == null) continue;

                // Store original texture references
                Texture mainTex = mat.HasProperty("_MainTex") ? mat.GetTexture("_MainTex") : null;
                Texture bumpMap = mat.HasProperty("_BumpMap") ? mat.GetTexture("_BumpMap") : null;
                Texture occlusionMap = mat.HasProperty("_OcclusionMap") ? mat.GetTexture("_OcclusionMap") : null;
                Texture specGlossMap = mat.HasProperty("_SpecGlossMap") ? mat.GetTexture("_SpecGlossMap") : null;

                Color color = mat.HasProperty("_Color") ? mat.GetColor("_Color") : Color.white;
                float glossiness = mat.HasProperty("_Glossiness") ? mat.GetFloat("_Glossiness") : 0.5f;
                float bumpScale = mat.HasProperty("_BumpScale") ? mat.GetFloat("_BumpScale") : 1f;
                float occlusionStrength = mat.HasProperty("_OcclusionStrength") ? mat.GetFloat("_OcclusionStrength") : 1f;

                Vector2 texScale = mat.HasProperty("_MainTex") ? mat.GetTextureScale("_MainTex") : Vector2.one;
                Vector2 texOffset = mat.HasProperty("_MainTex") ? mat.GetTextureOffset("_MainTex") : Vector2.zero;

                // Change shader to URP Lit
                mat.shader = urpLit;

                // Apply textures with URP property names
                if (mainTex != null)
                {
                    mat.SetTexture("_BaseMap", mainTex);
                    mat.SetTextureScale("_BaseMap", texScale);
                    mat.SetTextureOffset("_BaseMap", texOffset);
                }

                if (bumpMap != null)
                {
                    mat.SetTexture("_BumpMap", bumpMap);
                    mat.SetFloat("_BumpScale", bumpScale);
                    mat.EnableKeyword("_NORMALMAP");
                }

                if (occlusionMap != null)
                {
                    mat.SetTexture("_OcclusionMap", occlusionMap);
                    mat.SetFloat("_OcclusionStrength", occlusionStrength);
                }

                // URP uses smoothness (0-1), convert from glossiness
                mat.SetColor("_BaseColor", color);
                mat.SetFloat("_Smoothness", glossiness * 0.3f); // Reduce glossiness for more natural look

                // Set surface type to Opaque
                mat.SetFloat("_Surface", 0); // Opaque
                mat.SetFloat("_Blend", 0);
                mat.SetFloat("_Cull", 2); // Back
                mat.SetFloat("_ZWrite", 1);

                mat.renderQueue = 2000; // Geometry

                EditorUtility.SetDirty(mat);
                convertedCount++;

                Debug.Log($"Converted: {mat.name}");
            }

            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();

            Debug.Log($"=== Conversion Complete ===");
            Debug.Log($"Converted {convertedCount} materials to URP Lit shader.");

            EditorUtility.DisplayDialog("Conversion Complete",
                $"Successfully converted {convertedCount} materials to URP.\n\nYou can now preview them in the Project window.",
                "OK");
        }

        /// <summary>
        /// Convert all AtmosphericHouse materials from HDRP/broken shaders to URP Lit.
        /// This fixes pink materials by properly remapping textures and properties.
        /// </summary>
        [MenuItem("Tools/Fix AtmosphericHouse Materials (Pink Fix)")]
        public static void FixAtmosphericHouseMaterials()
        {
            string folderPath = "Assets/AtmosphericHouse/Materials";

            if (!AssetDatabase.IsValidFolder(folderPath))
            {
                Debug.LogError($"Folder not found: {folderPath}");
                return;
            }

            // Find URP Lit shader
            Shader urpLit = Shader.Find("Universal Render Pipeline/Lit");
            if (urpLit == null)
            {
                Debug.LogError("URP Lit shader not found! Make sure URP is installed.");
                return;
            }

            // Find all materials in folder (recursive)
            string[] guids = AssetDatabase.FindAssets("t:Material", new[] { folderPath });
            int convertedCount = 0;
            int skippedCount = 0;

            Debug.Log($"[AtmosphericHouse Fix] Found {guids.Length} materials to process...");

            foreach (string guid in guids)
            {
                string path = AssetDatabase.GUIDToAssetPath(guid);
                Material mat = AssetDatabase.LoadAssetAtPath<Material>(path);

                if (mat == null)
                {
                    skippedCount++;
                    continue;
                }

                // Get textures from ALL possible property names (HDRP + URP + Standard)
                Texture albedoTex = GetTextureFromAnyProperty(mat, "_BaseColorMap", "_BaseMap", "_MainTex");
                Texture normalTex = GetTextureFromAnyProperty(mat, "_NormalMap", "_BumpMap");
                Texture maskTex = GetTextureFromAnyProperty(mat, "_MaskMap", "_MetallicGlossMap");
                Texture occlusionTex = GetTextureFromAnyProperty(mat, "_OcclusionMap");
                Texture emissionTex = GetTextureFromAnyProperty(mat, "_EmissiveColorMap", "_EmissionMap");

                // Get color from various property names
                Color baseColor = Color.white;
                if (mat.HasProperty("_BaseColor"))
                    baseColor = mat.GetColor("_BaseColor");
                else if (mat.HasProperty("_Color"))
                    baseColor = mat.GetColor("_Color");

                // Get other properties
                float bumpScale = mat.HasProperty("_BumpScale") ? mat.GetFloat("_BumpScale") :
                                  (mat.HasProperty("_NormalScale") ? mat.GetFloat("_NormalScale") : 1f);
                float metallic = mat.HasProperty("_Metallic") ? mat.GetFloat("_Metallic") : 0f;
                float smoothness = mat.HasProperty("_Smoothness") ? mat.GetFloat("_Smoothness") :
                                   (mat.HasProperty("_Glossiness") ? mat.GetFloat("_Glossiness") : 0.5f);
                float occlusionStrength = mat.HasProperty("_OcclusionStrength") ? mat.GetFloat("_OcclusionStrength") : 1f;

                // Get texture scale/offset
                Vector2 texScale = Vector2.one;
                Vector2 texOffset = Vector2.zero;
                if (mat.HasProperty("_BaseColorMap"))
                {
                    texScale = mat.GetTextureScale("_BaseColorMap");
                    texOffset = mat.GetTextureOffset("_BaseColorMap");
                }
                else if (mat.HasProperty("_BaseMap"))
                {
                    texScale = mat.GetTextureScale("_BaseMap");
                    texOffset = mat.GetTextureOffset("_BaseMap");
                }
                else if (mat.HasProperty("_MainTex"))
                {
                    texScale = mat.GetTextureScale("_MainTex");
                    texOffset = mat.GetTextureOffset("_MainTex");
                }

                // Force shader to URP Lit
                mat.shader = urpLit;

                // Clear all keywords first
                mat.shaderKeywords = new string[0];

                // Apply albedo/base texture
                if (albedoTex != null)
                {
                    mat.SetTexture("_BaseMap", albedoTex);
                    mat.SetTextureScale("_BaseMap", texScale);
                    mat.SetTextureOffset("_BaseMap", texOffset);
                }

                // Apply normal map
                if (normalTex != null)
                {
                    mat.SetTexture("_BumpMap", normalTex);
                    mat.SetFloat("_BumpScale", bumpScale);
                    mat.EnableKeyword("_NORMALMAP");
                }

                // Apply metallic/gloss map (HDRP MaskMap R=Metallic, A=Smoothness)
                if (maskTex != null)
                {
                    mat.SetTexture("_MetallicGlossMap", maskTex);
                    mat.EnableKeyword("_METALLICSPECGLOSSMAP");
                }

                // Apply occlusion (HDRP MaskMap G channel, or separate map)
                if (occlusionTex != null)
                {
                    mat.SetTexture("_OcclusionMap", occlusionTex);
                    mat.SetFloat("_OcclusionStrength", occlusionStrength);
                    mat.EnableKeyword("_OCCLUSIONMAP");
                }

                // Apply emission
                if (emissionTex != null)
                {
                    mat.SetTexture("_EmissionMap", emissionTex);
                    mat.SetColor("_EmissionColor", Color.white);
                    mat.EnableKeyword("_EMISSION");
                    mat.globalIlluminationFlags = MaterialGlobalIlluminationFlags.BakedEmissive;
                }

                // Set base color
                mat.SetColor("_BaseColor", baseColor);

                // Set metallic and smoothness
                mat.SetFloat("_Metallic", metallic);
                // Clamp smoothness to reasonable values (HDRP often has 1.0 which is too shiny)
                mat.SetFloat("_Smoothness", Mathf.Clamp(smoothness, 0f, 0.8f));

                // Set surface properties
                mat.SetFloat("_Surface", 0); // Opaque
                mat.SetFloat("_Blend", 0);
                mat.SetFloat("_Cull", 2); // Back
                mat.SetFloat("_ZWrite", 1);
                mat.SetFloat("_AlphaClip", 0);

                mat.renderQueue = 2000; // Geometry queue

                EditorUtility.SetDirty(mat);
                convertedCount++;

                Debug.Log($"[AtmosphericHouse Fix] Fixed: {mat.name}");
            }

            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();

            Debug.Log($"=== AtmosphericHouse Material Fix Complete ===");
            Debug.Log($"Fixed {convertedCount} materials. Skipped {skippedCount}.");

            EditorUtility.DisplayDialog("AtmosphericHouse Fix Complete",
                $"Successfully fixed {convertedCount} materials.\n\nAll pink materials should now display correctly with URP Lit shader.",
                "OK");
        }

        /// <summary>
        /// Helper to get texture from multiple possible property names
        /// </summary>
        private static Texture GetTextureFromAnyProperty(Material mat, params string[] propertyNames)
        {
            foreach (string prop in propertyNames)
            {
                if (mat.HasProperty(prop))
                {
                    Texture tex = mat.GetTexture(prop);
                    if (tex != null)
                        return tex;
                }
            }
            return null;
        }

        /// <summary>
        /// Convert Mining_Company robot materials from Built-in/Standard to URP Lit.
        /// </summary>
        [MenuItem("Tools/Fix Mining_Company Materials (Pink Fix)")]
        public static void FixMiningCompanyMaterials()
        {
            string folderPath = "Assets/Mining_Company";

            if (!AssetDatabase.IsValidFolder(folderPath))
            {
                Debug.LogError($"Folder not found: {folderPath}");
                return;
            }

            // Find URP Lit shader
            Shader urpLit = Shader.Find("Universal Render Pipeline/Lit");
            if (urpLit == null)
            {
                Debug.LogError("URP Lit shader not found! Make sure URP is installed.");
                return;
            }

            // Find all materials in folder (recursive)
            string[] guids = AssetDatabase.FindAssets("t:Material", new[] { folderPath });
            int convertedCount = 0;
            int skippedCount = 0;

            Debug.Log($"[Mining_Company Fix] Found {guids.Length} materials to process...");

            foreach (string guid in guids)
            {
                string path = AssetDatabase.GUIDToAssetPath(guid);
                Material mat = AssetDatabase.LoadAssetAtPath<Material>(path);

                if (mat == null)
                {
                    skippedCount++;
                    continue;
                }

                // Get textures from ALL possible property names (Standard + URP)
                Texture albedoTex = GetTextureFromAnyProperty(mat, "_BaseColorMap", "_BaseMap", "_MainTex");
                Texture normalTex = GetTextureFromAnyProperty(mat, "_NormalMap", "_BumpMap");
                Texture metallicTex = GetTextureFromAnyProperty(mat, "_MetallicGlossMap", "_MaskMap");
                Texture occlusionTex = GetTextureFromAnyProperty(mat, "_OcclusionMap");
                Texture emissionTex = GetTextureFromAnyProperty(mat, "_EmissiveColorMap", "_EmissionMap");

                // Get color from various property names
                Color baseColor = Color.white;
                if (mat.HasProperty("_BaseColor"))
                    baseColor = mat.GetColor("_BaseColor");
                else if (mat.HasProperty("_Color"))
                    baseColor = mat.GetColor("_Color");

                // Get other properties
                float bumpScale = mat.HasProperty("_BumpScale") ? mat.GetFloat("_BumpScale") :
                                  (mat.HasProperty("_NormalScale") ? mat.GetFloat("_NormalScale") : 1f);
                float metallic = mat.HasProperty("_Metallic") ? mat.GetFloat("_Metallic") : 0f;
                float smoothness = mat.HasProperty("_Smoothness") ? mat.GetFloat("_Smoothness") :
                                   (mat.HasProperty("_Glossiness") ? mat.GetFloat("_Glossiness") : 0.5f);
                float occlusionStrength = mat.HasProperty("_OcclusionStrength") ? mat.GetFloat("_OcclusionStrength") : 1f;

                // Get texture scale/offset
                Vector2 texScale = Vector2.one;
                Vector2 texOffset = Vector2.zero;
                if (mat.HasProperty("_MainTex"))
                {
                    texScale = mat.GetTextureScale("_MainTex");
                    texOffset = mat.GetTextureOffset("_MainTex");
                }
                else if (mat.HasProperty("_BaseMap"))
                {
                    texScale = mat.GetTextureScale("_BaseMap");
                    texOffset = mat.GetTextureOffset("_BaseMap");
                }

                // Force shader to URP Lit
                mat.shader = urpLit;

                // Clear all keywords first
                mat.shaderKeywords = new string[0];

                // Apply albedo/base texture
                if (albedoTex != null)
                {
                    mat.SetTexture("_BaseMap", albedoTex);
                    mat.SetTextureScale("_BaseMap", texScale);
                    mat.SetTextureOffset("_BaseMap", texOffset);
                }

                // Apply normal map
                if (normalTex != null)
                {
                    mat.SetTexture("_BumpMap", normalTex);
                    mat.SetFloat("_BumpScale", bumpScale);
                    mat.EnableKeyword("_NORMALMAP");
                }

                // Apply metallic/gloss map
                if (metallicTex != null)
                {
                    mat.SetTexture("_MetallicGlossMap", metallicTex);
                    mat.EnableKeyword("_METALLICSPECGLOSSMAP");
                }

                // Apply occlusion
                if (occlusionTex != null)
                {
                    mat.SetTexture("_OcclusionMap", occlusionTex);
                    mat.SetFloat("_OcclusionStrength", occlusionStrength);
                    mat.EnableKeyword("_OCCLUSIONMAP");
                }

                // Apply emission
                if (emissionTex != null)
                {
                    mat.SetTexture("_EmissionMap", emissionTex);
                    mat.SetColor("_EmissionColor", Color.white);
                    mat.EnableKeyword("_EMISSION");
                    mat.globalIlluminationFlags = MaterialGlobalIlluminationFlags.BakedEmissive;
                }

                // Set base color
                mat.SetColor("_BaseColor", baseColor);

                // Set metallic and smoothness
                mat.SetFloat("_Metallic", metallic);
                mat.SetFloat("_Smoothness", Mathf.Clamp(smoothness, 0f, 0.8f));

                // Set surface properties
                mat.SetFloat("_Surface", 0); // Opaque
                mat.SetFloat("_Blend", 0);
                mat.SetFloat("_Cull", 2); // Back
                mat.SetFloat("_ZWrite", 1);
                mat.SetFloat("_AlphaClip", 0);

                mat.renderQueue = 2000; // Geometry queue

                EditorUtility.SetDirty(mat);
                convertedCount++;

                Debug.Log($"[Mining_Company Fix] Fixed: {mat.name}");
            }

            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();

            Debug.Log($"=== Mining_Company Material Fix Complete ===");
            Debug.Log($"Fixed {convertedCount} materials. Skipped {skippedCount}.");

            EditorUtility.DisplayDialog("Mining_Company Fix Complete",
                $"Successfully fixed {convertedCount} materials.\n\nAll pink materials should now display correctly with URP Lit shader.",
                "OK");
        }
    }
}
