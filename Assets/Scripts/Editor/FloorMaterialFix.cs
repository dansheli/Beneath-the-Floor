using UnityEngine;
using UnityEditor;

namespace BeneathTheFloor.Editor
{
    /// <summary>
    /// Editor utility to fix floor material seams by applying world-space UV shader.
    /// </summary>
    public class FloorMaterialFix : EditorWindow
    {
        [MenuItem("Tools/Fix Floor Seams")]
        public static void FixFloorSeams()
        {
            // Find the floor material
            string[] materialPaths = new string[]
            {
                "Assets/AtmosphericHouse/Materials/Materials_tiled/Floor_concrete_clean.mat",
                "Assets/Materials/Basement_Floor.mat"
            };

            // Find the WorldSpaceFloor shader
            Shader worldSpaceShader = Shader.Find("BeneathTheFloor/WorldSpaceFloor");
            if (worldSpaceShader == null)
            {
                Debug.LogError("[FloorMaterialFix] Could not find 'BeneathTheFloor/WorldSpaceFloor' shader. Make sure the shader compiles correctly.");
                return;
            }

            int fixedCount = 0;

            foreach (string path in materialPaths)
            {
                Material mat = AssetDatabase.LoadAssetAtPath<Material>(path);
                if (mat == null)
                {
                    Debug.LogWarning($"[FloorMaterialFix] Material not found at: {path}");
                    continue;
                }

                // Store original textures before changing shader
                Texture baseMap = mat.GetTexture("_BaseMap");
                Texture bumpMap = mat.GetTexture("_BumpMap");
                Texture metallicMap = mat.GetTexture("_MetallicGlossMap");
                Texture occlusionMap = mat.GetTexture("_OcclusionMap");
                Color baseColor = mat.GetColor("_BaseColor");
                float smoothness = mat.HasProperty("_Smoothness") ? mat.GetFloat("_Smoothness") : 0.5f;

                // Change shader
                mat.shader = worldSpaceShader;

                // Re-apply textures
                if (baseMap != null) mat.SetTexture("_BaseMap", baseMap);
                if (bumpMap != null) mat.SetTexture("_BumpMap", bumpMap);
                if (metallicMap != null) mat.SetTexture("_MetallicGlossMap", metallicMap);
                if (occlusionMap != null) mat.SetTexture("_OcclusionMap", occlusionMap);
                mat.SetColor("_BaseColor", baseColor);
                mat.SetFloat("_Smoothness", smoothness);

                // Set world-space tiling (adjust this value as needed)
                mat.SetFloat("_TilingScale", 0.25f); // 1 tile per 4 world units

                EditorUtility.SetDirty(mat);
                fixedCount++;

                Debug.Log($"[FloorMaterialFix] Fixed: {mat.name} - Now using world-space UVs");
            }

            AssetDatabase.SaveAssets();
            Debug.Log($"[FloorMaterialFix] Done! Fixed {fixedCount} material(s). Seams should now be gone.");
        }
    }
}
