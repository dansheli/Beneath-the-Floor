using UnityEngine;
using UnityEditor;

namespace BeneathTheFloor.Editor
{
    public static class UpgradeFPArmsMaterials
    {
        [MenuItem("Beneath The Floor/Setup/Upgrade FPS Arms Materials to URP", false, 109)]
        public static void UpgradeMaterials()
        {
            string[] materialPaths = {
                "Assets/animated_fps_hands_v3/materials/v1.mat",
                "Assets/animated_fps_hands_v3/materials/v2.mat",
                "Assets/animated_fps_hands_v3/materials/v3.mat",
                "Assets/animated_fps_hands_v3/materials/v4.mat",
                "Assets/animated_fps_hands_v3/materials/v5.mat",
                "Assets/animated_fps_hands_v3/materials/plane.mat",
            };

            // Get URP Lit shader
            Shader urpLit = Shader.Find("Universal Render Pipeline/Lit");
            if (urpLit == null)
            {
                Debug.LogError("[UpgradeFPArmsMaterials] Could not find URP Lit shader!");
                return;
            }

            int upgradedCount = 0;

            foreach (string path in materialPaths)
            {
                Material mat = AssetDatabase.LoadAssetAtPath<Material>(path);
                if (mat == null)
                {
                    Debug.LogWarning($"[UpgradeFPArmsMaterials] Could not load material: {path}");
                    continue;
                }

                // Check if already URP
                if (mat.shader.name.Contains("Universal Render Pipeline"))
                {
                    Debug.Log($"[UpgradeFPArmsMaterials] {mat.name} already uses URP shader");
                    continue;
                }

                // Store texture references before changing shader
                Texture mainTex = mat.HasProperty("_MainTex") ? mat.GetTexture("_MainTex") : null;
                Texture normalMap = mat.HasProperty("_BumpMap") ? mat.GetTexture("_BumpMap") : null;
                Color color = mat.HasProperty("_Color") ? mat.GetColor("_Color") : Color.white;

                // Change to URP Lit shader
                mat.shader = urpLit;

                // Restore textures with URP property names
                if (mainTex != null)
                {
                    mat.SetTexture("_BaseMap", mainTex);
                }
                if (normalMap != null)
                {
                    mat.SetTexture("_BumpMap", normalMap);
                }
                mat.SetColor("_BaseColor", color);

                EditorUtility.SetDirty(mat);
                upgradedCount++;
                Debug.Log($"[UpgradeFPArmsMaterials] Upgraded: {mat.name}");
            }

            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();

            Debug.Log($"[UpgradeFPArmsMaterials] Upgraded {upgradedCount} materials to URP");
        }

        [MenuItem("Beneath The Floor/Setup/Create Fresh URP Skin Material", false, 110)]
        public static void CreateFreshMaterial()
        {
            Shader urpLit = Shader.Find("Universal Render Pipeline/Lit");
            if (urpLit == null)
            {
                Debug.LogError("Could not find URP Lit shader!");
                return;
            }

            // Create new material
            Material skinMat = new Material(urpLit);
            skinMat.name = "FPArms_Skin_URP";
            skinMat.SetColor("_BaseColor", new Color(0.87f, 0.72f, 0.60f)); // Skin tone

            // Load textures from v1 folder
            Texture2D baseTex = AssetDatabase.LoadAssetAtPath<Texture2D>("Assets/animated_fps_hands_v3/textures/v1/v1_basecolor.png");
            Texture2D normalTex = AssetDatabase.LoadAssetAtPath<Texture2D>("Assets/animated_fps_hands_v3/textures/v1/v1_normal.png");

            if (baseTex != null)
            {
                skinMat.SetTexture("_BaseMap", baseTex);
                skinMat.SetColor("_BaseColor", Color.white); // Use texture color instead
                Debug.Log("[CreateFreshMaterial] Applied base texture");
            }

            if (normalTex != null)
            {
                skinMat.SetTexture("_BumpMap", normalTex);
                skinMat.EnableKeyword("_NORMALMAP");
                Debug.Log("[CreateFreshMaterial] Applied normal map");
            }

            // Save to Resources
            string savePath = "Assets/Resources/FPArmsMaterial.mat";
            AssetDatabase.CreateAsset(skinMat, savePath);
            AssetDatabase.SaveAssets();

            Debug.Log($"[CreateFreshMaterial] Created new URP material at: {savePath}");

            // Also assign to hands in scene
            AssignMaterialToHands(skinMat);
        }

        private static void AssignMaterialToHands(Material mat)
        {
            string[] possibleNames = { "PlayerFP_Arms", "v1", "v2", "v3", "v4", "v5" };
            GameObject armsObj = null;

            foreach (string name in possibleNames)
            {
                armsObj = GameObject.Find(name);
                if (armsObj != null) break;
            }

            if (armsObj == null) return;

            var renderers = armsObj.GetComponentsInChildren<Renderer>(true);
            foreach (var renderer in renderers)
            {
                renderer.sharedMaterial = mat;
                Debug.Log($"[CreateFreshMaterial] Assigned to: {renderer.gameObject.name}");
            }

            UnityEditor.SceneManagement.EditorSceneManager.MarkSceneDirty(
                UnityEngine.SceneManagement.SceneManager.GetActiveScene());
        }
    }
}
