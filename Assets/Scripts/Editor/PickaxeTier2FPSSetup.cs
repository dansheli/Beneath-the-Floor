using UnityEngine;
using UnityEditor;
using UnityEditor.Animations;
using System.IO;

namespace BeneathTheFloor.Editor
{
    /// <summary>
    /// Editor utility to set up the Meshy-generated Pickaxe_Tier2 as an FPS-ready prefab.
    /// Creates material, prefab, and connects to PlayerToolVisualController.
    /// </summary>
    public static class PickaxeTier2FPSSetup
    {
        // Paths - Tier 2 Meshy import
        private const string MeshyFBXPath = "Assets/MeshyImports/Meshy_Model_20251215_163001/Meshy_AI_Create_a_stylized_low_1215142957_texture.fbx";
        private const string MeshyTexturePath = "Assets/MeshyImports/Meshy_Model_20251215_163001/Meshy_AI_Create_a_stylized_low_1215142957_texture.png";
        private const string MeshyMetallicPath = "Assets/MeshyImports/Meshy_Model_20251215_163001/Meshy_AI_Create_a_stylized_low_1215142957_texture_metallic.png";
        private const string MeshyNormalPath = "Assets/MeshyImports/Meshy_Model_20251215_163001/Meshy_AI_Create_a_stylized_low_1215142957_texture_normal.png";
        private const string MeshyRoughnessPath = "Assets/MeshyImports/Meshy_Model_20251215_163001/Meshy_AI_Create_a_stylized_low_1215142957_texture_roughness.png";

        // Output paths
        private const string MaterialsFolder = "Assets/Art/Tools/Pickaxe/Materials";
        private const string PrefabsFolder = "Assets/Art/Tools/Pickaxe/Prefabs";
        private const string AnimatorFolder = "Assets/Art/Tools/Pickaxe/Animator";
        private const string AnimationsFolder = "Assets/Art/Tools/Pickaxe/Animations";

        [MenuItem("Beneath The Floor/Setup/Setup Pickaxe Tier 2 FPS Prefab", false, 132)]
        public static void SetupPickaxeTier2FPS()
        {
            Debug.Log("[PickaxeTier2FPSSetup] Starting Pickaxe Tier 2 FPS setup...");

            // 1. Ensure folders exist
            EnsureFoldersExist();

            // 2. Create material with full PBR textures
            Material pickaxeMat = CreatePickaxeMaterial();

            // 3. Get or create Animator Controller (reuse Tier 1's or create new)
            AnimatorController animController = GetOrCreateAnimatorController();

            // 4. Create FPS prefab
            GameObject prefab = CreateFPSPrefab(pickaxeMat, animController);

            // 5. Update PlayerToolVisualController to use the new prefab
            UpdatePlayerToolVisualController(prefab);

            Debug.Log("[PickaxeTier2FPSSetup] Setup complete!");
            PrintSummary(prefab, animController);
        }

        private static void EnsureFoldersExist()
        {
            string[] folders = { MaterialsFolder, PrefabsFolder, AnimatorFolder, AnimationsFolder };

            foreach (string folder in folders)
            {
                if (!AssetDatabase.IsValidFolder(folder))
                {
                    string parent = Path.GetDirectoryName(folder).Replace("\\", "/");
                    string newFolder = Path.GetFileName(folder);
                    AssetDatabase.CreateFolder(parent, newFolder);
                    Debug.Log($"[PickaxeTier2FPSSetup] Created folder: {folder}");
                }
            }
        }

        private static Material CreatePickaxeMaterial()
        {
            string matPath = $"{MaterialsFolder}/Pickaxe_T2_Mat.mat";

            // Check if material already exists
            Material existingMat = AssetDatabase.LoadAssetAtPath<Material>(matPath);
            if (existingMat != null)
            {
                Debug.Log($"[PickaxeTier2FPSSetup] Using existing material: {matPath}");
                return existingMat;
            }

            // Get URP Lit shader
            Shader urpLit = Shader.Find("Universal Render Pipeline/Lit");
            if (urpLit == null)
            {
                urpLit = Shader.Find("Standard");
                Debug.LogWarning("[PickaxeTier2FPSSetup] URP Lit shader not found, using Standard");
            }

            // Create material
            Material mat = new Material(urpLit);
            mat.name = "Pickaxe_T2_Mat";

            // Load the Meshy textures
            Texture2D baseTex = AssetDatabase.LoadAssetAtPath<Texture2D>(MeshyTexturePath);
            if (baseTex != null)
            {
                mat.SetTexture("_BaseMap", baseTex);
                mat.SetColor("_BaseColor", Color.white);
                Debug.Log("[PickaxeTier2FPSSetup] Applied base texture to material");
            }
            else
            {
                // Fallback: iron/copper stylized color
                mat.SetColor("_BaseColor", new Color(0.7f, 0.5f, 0.3f)); // Copper-ish
                Debug.LogWarning("[PickaxeTier2FPSSetup] Base texture not found, using fallback color");
            }

            // Load metallic map
            Texture2D metallicTex = AssetDatabase.LoadAssetAtPath<Texture2D>(MeshyMetallicPath);
            if (metallicTex != null)
            {
                mat.SetTexture("_MetallicGlossMap", metallicTex);
                mat.SetFloat("_Metallic", 1f); // Use texture values
                Debug.Log("[PickaxeTier2FPSSetup] Applied metallic texture");
            }
            else
            {
                mat.SetFloat("_Metallic", 0.5f);
            }

            // Load normal map
            Texture2D normalTex = AssetDatabase.LoadAssetAtPath<Texture2D>(MeshyNormalPath);
            if (normalTex != null)
            {
                mat.SetTexture("_BumpMap", normalTex);
                mat.SetFloat("_BumpScale", 1f);
                mat.EnableKeyword("_NORMALMAP");
                Debug.Log("[PickaxeTier2FPSSetup] Applied normal map");
            }

            // Set smoothness (inverse of roughness)
            Texture2D roughnessTex = AssetDatabase.LoadAssetAtPath<Texture2D>(MeshyRoughnessPath);
            if (roughnessTex != null)
            {
                // URP Lit uses smoothness (1 - roughness), we'll set a reasonable value
                mat.SetFloat("_Smoothness", 0.5f);
                Debug.Log("[PickaxeTier2FPSSetup] Set smoothness based on roughness presence");
            }
            else
            {
                mat.SetFloat("_Smoothness", 0.4f);
            }

            // Save material
            AssetDatabase.CreateAsset(mat, matPath);
            Debug.Log($"[PickaxeTier2FPSSetup] Created material: {matPath}");

            return mat;
        }

        private static AnimatorController GetOrCreateAnimatorController()
        {
            // Try to reuse Tier 1's animator (same animations work for all tiers)
            string tier1ControllerPath = $"{AnimatorFolder}/Pickaxe_FPS_Controller.controller";
            AnimatorController existing = AssetDatabase.LoadAssetAtPath<AnimatorController>(tier1ControllerPath);
            if (existing != null)
            {
                Debug.Log($"[PickaxeTier2FPSSetup] Reusing Tier 1 animator controller: {tier1ControllerPath}");
                return existing;
            }

            // If Tier 1 doesn't exist, create a Tier 2 specific one
            string controllerPath = $"{AnimatorFolder}/Pickaxe_T2_FPS_Controller.controller";
            existing = AssetDatabase.LoadAssetAtPath<AnimatorController>(controllerPath);
            if (existing != null)
            {
                Debug.Log($"[PickaxeTier2FPSSetup] Using existing Tier 2 animator controller: {controllerPath}");
                return existing;
            }

            // Create new controller
            AnimatorController controller = AnimatorController.CreateAnimatorControllerAtPath(controllerPath);

            // Add parameters
            controller.AddParameter("Dig", AnimatorControllerParameterType.Trigger);

            // Get root state machine
            var rootStateMachine = controller.layers[0].stateMachine;

            // Create animation clips
            AnimationClip idleClip = CreateIdleClip();
            AnimationClip digClip = CreateDigClip();

            // Create states
            var idleState = rootStateMachine.AddState("Idle", new Vector3(250, 50, 0));
            idleState.motion = idleClip;

            var digState = rootStateMachine.AddState("Dig", new Vector3(500, 50, 0));
            digState.motion = digClip;

            // Set default state
            rootStateMachine.defaultState = idleState;

            // Create transitions
            var toDigTransition = idleState.AddTransition(digState);
            toDigTransition.AddCondition(AnimatorConditionMode.If, 0, "Dig");
            toDigTransition.hasExitTime = false;
            toDigTransition.duration = 0.05f;

            var toIdleTransition = digState.AddTransition(idleState);
            toIdleTransition.hasExitTime = true;
            toIdleTransition.exitTime = 0.9f;
            toIdleTransition.duration = 0.1f;

            EditorUtility.SetDirty(controller);
            AssetDatabase.SaveAssets();

            Debug.Log($"[PickaxeTier2FPSSetup] Created animator controller: {controllerPath}");
            return controller;
        }

        private static AnimationClip CreateIdleClip()
        {
            string clipPath = $"{AnimationsFolder}/Pickaxe_Idle.anim";

            // Check if exists (reuse from Tier 1)
            AnimationClip existing = AssetDatabase.LoadAssetAtPath<AnimationClip>(clipPath);
            if (existing != null) return existing;

            // Create idle clip - subtle breathing motion
            AnimationClip clip = new AnimationClip();
            clip.name = "Pickaxe_Idle";

            AnimationCurve posCurve = new AnimationCurve();
            posCurve.AddKey(0f, 0f);
            posCurve.AddKey(1f, 0.005f);
            posCurve.AddKey(2f, 0f);
            clip.SetCurve("", typeof(Transform), "localPosition.y", posCurve);

            AnimationClipSettings settings = AnimationUtility.GetAnimationClipSettings(clip);
            settings.loopTime = true;
            AnimationUtility.SetAnimationClipSettings(clip, settings);

            AssetDatabase.CreateAsset(clip, clipPath);
            Debug.Log($"[PickaxeTier2FPSSetup] Created animation clip: {clipPath}");

            return clip;
        }

        private static AnimationClip CreateDigClip()
        {
            string clipPath = $"{AnimationsFolder}/Pickaxe_Dig.anim";

            // Check if exists (reuse from Tier 1)
            AnimationClip existing = AssetDatabase.LoadAssetAtPath<AnimationClip>(clipPath);
            if (existing != null) return existing;

            // Create dig clip - swing down and back
            AnimationClip clip = new AnimationClip();
            clip.name = "Pickaxe_Dig";

            // X rotation (swing)
            AnimationCurve rotXCurve = new AnimationCurve();
            rotXCurve.AddKey(new Keyframe(0f, 0f));
            rotXCurve.AddKey(new Keyframe(0.1f, -25f));
            rotXCurve.AddKey(new Keyframe(0.2f, 35f));
            rotXCurve.AddKey(new Keyframe(0.35f, 0f));
            clip.SetCurve("", typeof(Transform), "localEulerAngles.x", rotXCurve);

            // Z position (forward thrust)
            AnimationCurve posZCurve = new AnimationCurve();
            posZCurve.AddKey(new Keyframe(0f, 0f));
            posZCurve.AddKey(new Keyframe(0.1f, -0.03f));
            posZCurve.AddKey(new Keyframe(0.2f, 0.08f));
            posZCurve.AddKey(new Keyframe(0.35f, 0f));
            clip.SetCurve("", typeof(Transform), "localPosition.z", posZCurve);

            // Y position (down on impact)
            AnimationCurve posYCurve = new AnimationCurve();
            posYCurve.AddKey(new Keyframe(0f, 0f));
            posYCurve.AddKey(new Keyframe(0.1f, 0.03f));
            posYCurve.AddKey(new Keyframe(0.2f, -0.05f));
            posYCurve.AddKey(new Keyframe(0.35f, 0f));
            clip.SetCurve("", typeof(Transform), "localPosition.y", posYCurve);

            AnimationClipSettings settings = AnimationUtility.GetAnimationClipSettings(clip);
            settings.loopTime = false;
            AnimationUtility.SetAnimationClipSettings(clip, settings);

            AssetDatabase.CreateAsset(clip, clipPath);
            Debug.Log($"[PickaxeTier2FPSSetup] Created animation clip: {clipPath}");

            return clip;
        }

        private static GameObject CreateFPSPrefab(Material mat, AnimatorController animController)
        {
            string prefabPath = $"{PrefabsFolder}/Pickaxe_Tier2_FPS.prefab";

            // Check if prefab already exists - update it
            GameObject existingPrefab = AssetDatabase.LoadAssetAtPath<GameObject>(prefabPath);
            if (existingPrefab != null)
            {
                Debug.Log($"[PickaxeTier2FPSSetup] FPS prefab already exists, updating: {prefabPath}");

                GameObject prefabRoot = PrefabUtility.LoadPrefabContents(prefabPath);

                // Update animator controller
                var existingAnimator = prefabRoot.GetComponent<Animator>();
                if (existingAnimator != null)
                {
                    existingAnimator.runtimeAnimatorController = animController;
                }

                // Update mesh child transform
                if (prefabRoot.transform.childCount > 0)
                {
                    Transform meshChild = prefabRoot.transform.GetChild(0);
                    meshChild.localPosition = new Vector3(0f, 0f, 0f);
                    meshChild.localRotation = Quaternion.Euler(-70f, 0f, 80f);
                    meshChild.localScale = new Vector3(50f, 50f, 50f);
                    Debug.Log($"[PickaxeTier2FPSSetup] Updated mesh transform: pos=(0,0,0), scale=(50,50,50), rotation=(-70,0,80)");
                }

                // Update material on all renderers
                var renderers = prefabRoot.GetComponentsInChildren<Renderer>();
                foreach (var renderer in renderers)
                {
                    renderer.sharedMaterial = mat;
                }

                PrefabUtility.SaveAsPrefabAsset(prefabRoot, prefabPath);
                PrefabUtility.UnloadPrefabContents(prefabRoot);

                return AssetDatabase.LoadAssetAtPath<GameObject>(prefabPath);
            }

            // Load the source FBX
            GameObject fbxModel = AssetDatabase.LoadAssetAtPath<GameObject>(MeshyFBXPath);
            if (fbxModel == null)
            {
                Debug.LogError($"[PickaxeTier2FPSSetup] Could not load FBX from: {MeshyFBXPath}");
                return null;
            }

            // Create root GameObject for FPS prefab
            GameObject root = new GameObject("Pickaxe_Tier2_FPS");

            // Instantiate the mesh as a child
            GameObject meshInstance = Object.Instantiate(fbxModel);
            meshInstance.name = "Mesh";
            meshInstance.transform.SetParent(root.transform);

            // Adjust mesh local transform for FPS view
            meshInstance.transform.localPosition = new Vector3(0f, 0f, 0f);
            meshInstance.transform.localRotation = Quaternion.Euler(-70f, 0f, 80f);
            meshInstance.transform.localScale = new Vector3(50f, 50f, 50f);

            // Apply material to all renderers
            var meshRenderers = meshInstance.GetComponentsInChildren<Renderer>();
            foreach (var renderer in meshRenderers)
            {
                renderer.sharedMaterial = mat;
                renderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            }

            // Remove colliders (FPS tool shouldn't have physics)
            var colliders = meshInstance.GetComponentsInChildren<Collider>();
            foreach (var col in colliders)
            {
                Object.DestroyImmediate(col);
            }

            // Add Animator to root
            var animator = root.AddComponent<Animator>();
            animator.runtimeAnimatorController = animController;
            animator.applyRootMotion = false;

            // Add ToolDigProfileLink for DiggingSystem integration
            var profileLink = root.AddComponent<BeneathTheFloor.Digging.ToolDigProfileLink>();

            // Save as prefab
            GameObject prefab = PrefabUtility.SaveAsPrefabAsset(root, prefabPath);

            // Clean up scene instance
            Object.DestroyImmediate(root);

            Debug.Log($"[PickaxeTier2FPSSetup] Created FPS prefab: {prefabPath}");
            return prefab;
        }

        private static void UpdatePlayerToolVisualController(GameObject prefab)
        {
            // Find PlayerToolVisualController in scene
            var controller = Object.FindObjectOfType<Player.PlayerToolVisualController>();
            if (controller == null)
            {
                Debug.LogWarning("[PickaxeTier2FPSSetup] PlayerToolVisualController not found in scene. " +
                                 "You may need to assign the prefab manually in the Inspector.");
                return;
            }

            // Use reflection to set the pickaxeTier2Prefab field
            var field = typeof(Player.PlayerToolVisualController).GetField("pickaxeTier2Prefab",
                System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);

            if (field != null)
            {
                field.SetValue(controller, prefab);
                EditorUtility.SetDirty(controller);
                Debug.Log("[PickaxeTier2FPSSetup] Assigned prefab to PlayerToolVisualController.pickaxeTier2Prefab");
            }
            else
            {
                Debug.LogWarning("[PickaxeTier2FPSSetup] Could not find pickaxeTier2Prefab field. " +
                                 "Please assign the prefab manually.");
            }

            // Mark scene dirty
            UnityEditor.SceneManagement.EditorSceneManager.MarkSceneDirty(
                UnityEngine.SceneManagement.SceneManager.GetActiveScene());
        }

        private static void PrintSummary(GameObject prefab, AnimatorController animController)
        {
            Debug.Log("=== PICKAXE TIER 2 FPS SETUP SUMMARY ===");
            Debug.Log($"Prefab: {AssetDatabase.GetAssetPath(prefab)}");
            Debug.Log($"Animator Controller: {AssetDatabase.GetAssetPath(animController)}");
            Debug.Log($"Material: {MaterialsFolder}/Pickaxe_T2_Mat.mat");
            Debug.Log("");
            Debug.Log("Tier 2 Stats (defined in PlayerToolVisualController):");
            Debug.Log("  digRadius: 0.6");
            Debug.Log("  digPower: 1.2");
            Debug.Log("  digDuration: 0.4s");
            Debug.Log("  maxDigDepth: 25m");
            Debug.Log("");
            Debug.Log("To upgrade from Tier 1 to Tier 2:");
            Debug.Log("  playerToolVisualController.UpgradeTool(2);");
            Debug.Log("==========================================");
        }
    }
}
