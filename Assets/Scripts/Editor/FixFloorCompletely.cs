using UnityEngine;
using UnityEditor;
using UnityEngine.Rendering;

namespace BeneathTheFloor.Editor
{
    public class FixFloorCompletely
    {
        [MenuItem("Tools/Fix Floor Completely")]
        public static void FixFloor()
        {
            // Find the WorldSpaceFloor shader
            Shader worldSpaceShader = Shader.Find("BeneathTheFloor/WorldSpaceFloor");
            if (worldSpaceShader == null)
            {
                Debug.LogError("[FixFloor] WorldSpaceFloor shader not found!");
                return;
            }

            // Load the floor material
            Material floorMat = AssetDatabase.LoadAssetAtPath<Material>(
                "Assets/AtmosphericHouse/Materials/Materials_tiled/Floor_concrete_clean.mat");

            if (floorMat == null)
            {
                Debug.LogError("[FixFloor] Floor_concrete_clean material not found!");
                return;
            }

            // Make sure material uses our shader
            if (floorMat.shader != worldSpaceShader)
            {
                floorMat.shader = worldSpaceShader;
                Debug.Log("[FixFloor] Applied WorldSpaceFloor shader to material");
            }

            // Find all floor objects
            MeshRenderer[] allRenderers = Object.FindObjectsOfType<MeshRenderer>();
            int fixedCount = 0;

            foreach (MeshRenderer renderer in allRenderers)
            {
                string name = renderer.gameObject.name.ToLower();

                if (name.Contains("floor"))
                {
                    // 1. Disable shadow casting (floors don't need to cast shadows on each other)
                    renderer.shadowCastingMode = ShadowCastingMode.Off;

                    // 2. Disable lightmap contribution
                    var flags = GameObjectUtility.GetStaticEditorFlags(renderer.gameObject);
                    flags &= ~StaticEditorFlags.ContributeGI;
                    GameObjectUtility.SetStaticEditorFlags(renderer.gameObject, flags);

                    // 3. Use light probes instead of lightmaps
                    renderer.receiveGI = ReceiveGI.LightProbes;

                    // 4. Make sure the floor uses the correct material
                    Material[] mats = renderer.sharedMaterials;
                    bool changed = false;
                    for (int i = 0; i < mats.Length; i++)
                    {
                        if (mats[i] != null && mats[i].name.ToLower().Contains("floor"))
                        {
                            mats[i] = floorMat;
                            changed = true;
                        }
                    }
                    if (changed)
                    {
                        renderer.sharedMaterials = mats;
                    }

                    EditorUtility.SetDirty(renderer.gameObject);
                    fixedCount++;
                    Debug.Log($"[FixFloor] Fixed: {renderer.gameObject.name}");
                }
            }

            // Save material changes
            EditorUtility.SetDirty(floorMat);
            AssetDatabase.SaveAssets();

            // Mark scene dirty
            UnityEditor.SceneManagement.EditorSceneManager.MarkSceneDirty(
                UnityEditor.SceneManagement.EditorSceneManager.GetActiveScene());

            Debug.Log($"[FixFloor] Complete! Fixed {fixedCount} floor objects:");
            Debug.Log("  - Shadow casting: OFF");
            Debug.Log("  - Lightmap contribution: OFF");
            Debug.Log("  - Using Light Probes for GI");
            Debug.Log("  - Using WorldSpaceFloor shader");
        }
    }
}
