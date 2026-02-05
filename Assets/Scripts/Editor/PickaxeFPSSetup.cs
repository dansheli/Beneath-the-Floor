using UnityEngine;
using UnityEditor;
using UnityEditor.Animations;
using System.IO;

namespace BeneathTheFloor.Editor
{
    /// <summary>
    /// Editor utility to set up the Meshy-generated Pickaxe_Tier1 as an FPS-ready prefab.
    /// </summary>
    public static class PickaxeFPSSetup
    {
        // Paths
        private const string MeshyFBXPath = "Assets/MeshyImports/Meshy_Model_20251215_154856/Meshy_AI_Create_a_clean_styli_1215134849_texture.fbx";
        private const string MeshyTexturePath = "Assets/MeshyImports/Meshy_Model_20251215_154856/Meshy_AI_Create_a_clean_styli_1215134849_texture.png";
        private const string MaterialsFolder = "Assets/Art/Tools/Pickaxe/Materials";
        private const string PrefabsFolder = "Assets/Art/Tools/Pickaxe/Prefabs";
        private const string AnimatorFolder = "Assets/Art/Tools/Pickaxe/Animator";
        private const string AnimationsFolder = "Assets/Art/Tools/Pickaxe/Animations";

        [MenuItem("Beneath The Floor/Setup/Setup Pickaxe Tier 1 FPS Prefab", false, 130)]
        public static void SetupPickaxeTier1FPS()
        {
            Debug.Log("[PickaxeFPSSetup] Starting Pickaxe Tier 1 FPS setup...");

            // 1. Ensure folders exist
            EnsureFoldersExist();

            // 2. Create material
            Material pickaxeMat = CreatePickaxeMaterial();

            // 3. Create Animator Controller with animation clips
            AnimatorController animController = CreateAnimatorController();

            // 4. Create FPS prefab
            GameObject prefab = CreateFPSPrefab(pickaxeMat, animController);

            // 5. Update PlayerToolVisualController to use the new prefab
            UpdatePlayerToolVisualController(prefab);

            Debug.Log("[PickaxeFPSSetup] Setup complete!");
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
                    Debug.Log($"[PickaxeFPSSetup] Created folder: {folder}");
                }
            }
        }

        private static Material CreatePickaxeMaterial()
        {
            string matPath = $"{MaterialsFolder}/Pickaxe_T1_Mat.mat";

            // Check if material already exists
            Material existingMat = AssetDatabase.LoadAssetAtPath<Material>(matPath);
            if (existingMat != null)
            {
                Debug.Log($"[PickaxeFPSSetup] Using existing material: {matPath}");
                return existingMat;
            }

            // Get URP Lit shader
            Shader urpLit = Shader.Find("Universal Render Pipeline/Lit");
            if (urpLit == null)
            {
                urpLit = Shader.Find("Standard");
                Debug.LogWarning("[PickaxeFPSSetup] URP Lit shader not found, using Standard");
            }

            // Create material
            Material mat = new Material(urpLit);
            mat.name = "Pickaxe_T1_Mat";

            // Load the Meshy texture
            Texture2D tex = AssetDatabase.LoadAssetAtPath<Texture2D>(MeshyTexturePath);
            if (tex != null)
            {
                mat.SetTexture("_BaseMap", tex);
                mat.SetColor("_BaseColor", Color.white);
                Debug.Log("[PickaxeFPSSetup] Applied Meshy texture to material");
            }
            else
            {
                // Fallback: stylized colors
                mat.SetColor("_BaseColor", new Color(0.6f, 0.5f, 0.4f));
                Debug.LogWarning("[PickaxeFPSSetup] Meshy texture not found, using fallback color");
            }

            mat.SetFloat("_Smoothness", 0.3f);

            // Save material
            AssetDatabase.CreateAsset(mat, matPath);
            Debug.Log($"[PickaxeFPSSetup] Created material: {matPath}");

            return mat;
        }

        private static AnimatorController CreateAnimatorController()
        {
            string controllerPath = $"{AnimatorFolder}/Pickaxe_FPS_Controller.controller";

            // Check if already exists
            AnimatorController existing = AssetDatabase.LoadAssetAtPath<AnimatorController>(controllerPath);
            if (existing != null)
            {
                Debug.Log($"[PickaxeFPSSetup] Using existing animator controller: {controllerPath}");
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
            // Idle -> Dig (on Dig trigger)
            var toDigTransition = idleState.AddTransition(digState);
            toDigTransition.AddCondition(AnimatorConditionMode.If, 0, "Dig");
            toDigTransition.hasExitTime = false;
            toDigTransition.duration = 0.05f;

            // Dig -> Idle (after animation)
            var toIdleTransition = digState.AddTransition(idleState);
            toIdleTransition.hasExitTime = true;
            toIdleTransition.exitTime = 0.9f;
            toIdleTransition.duration = 0.1f;

            EditorUtility.SetDirty(controller);
            AssetDatabase.SaveAssets();

            Debug.Log($"[PickaxeFPSSetup] Created animator controller: {controllerPath}");
            return controller;
        }

        private static AnimationClip CreateIdleClip()
        {
            string clipPath = $"{AnimationsFolder}/Pickaxe_Idle.anim";

            // Check if exists
            AnimationClip existing = AssetDatabase.LoadAssetAtPath<AnimationClip>(clipPath);
            if (existing != null) return existing;

            // Create idle clip - subtle breathing motion
            AnimationClip clip = new AnimationClip();
            clip.name = "Pickaxe_Idle";

            // Subtle Y position bob
            AnimationCurve posCurve = new AnimationCurve();
            posCurve.AddKey(0f, 0f);
            posCurve.AddKey(1f, 0.005f);
            posCurve.AddKey(2f, 0f);
            clip.SetCurve("", typeof(Transform), "localPosition.y", posCurve);

            // Make it loop
            AnimationClipSettings settings = AnimationUtility.GetAnimationClipSettings(clip);
            settings.loopTime = true;
            AnimationUtility.SetAnimationClipSettings(clip, settings);

            AssetDatabase.CreateAsset(clip, clipPath);
            Debug.Log($"[PickaxeFPSSetup] Created animation clip: {clipPath}");

            return clip;
        }

        private static AnimationClip CreateDigClip()
        {
            string clipPath = $"{AnimationsFolder}/Pickaxe_Dig.anim";

            // Check if exists
            AnimationClip existing = AssetDatabase.LoadAssetAtPath<AnimationClip>(clipPath);
            if (existing != null) return existing;

            // Create dig clip - swing down and back
            AnimationClip clip = new AnimationClip();
            clip.name = "Pickaxe_Dig";

            // X rotation (swing) - wind up, then swing down
            AnimationCurve rotXCurve = new AnimationCurve();
            rotXCurve.AddKey(new Keyframe(0f, 0f));           // Start
            rotXCurve.AddKey(new Keyframe(0.1f, -25f));       // Wind up (back)
            rotXCurve.AddKey(new Keyframe(0.2f, 35f));        // Swing down (impact)
            rotXCurve.AddKey(new Keyframe(0.35f, 0f));        // Return to idle
            clip.SetCurve("", typeof(Transform), "localEulerAngles.x", rotXCurve);

            // Y position (forward thrust on impact)
            AnimationCurve posZCurve = new AnimationCurve();
            posZCurve.AddKey(new Keyframe(0f, 0f));
            posZCurve.AddKey(new Keyframe(0.1f, -0.03f));     // Pull back
            posZCurve.AddKey(new Keyframe(0.2f, 0.08f));      // Thrust forward
            posZCurve.AddKey(new Keyframe(0.35f, 0f));        // Return
            clip.SetCurve("", typeof(Transform), "localPosition.z", posZCurve);

            // Y position (down on impact)
            AnimationCurve posYCurve = new AnimationCurve();
            posYCurve.AddKey(new Keyframe(0f, 0f));
            posYCurve.AddKey(new Keyframe(0.1f, 0.03f));      // Raise
            posYCurve.AddKey(new Keyframe(0.2f, -0.05f));     // Drop on impact
            posYCurve.AddKey(new Keyframe(0.35f, 0f));        // Return
            clip.SetCurve("", typeof(Transform), "localPosition.y", posYCurve);

            // Don't loop
            AnimationClipSettings settings = AnimationUtility.GetAnimationClipSettings(clip);
            settings.loopTime = false;
            AnimationUtility.SetAnimationClipSettings(clip, settings);

            AssetDatabase.CreateAsset(clip, clipPath);
            Debug.Log($"[PickaxeFPSSetup] Created animation clip: {clipPath}");

            return clip;
        }

        private static GameObject CreateFPSPrefab(Material mat, AnimatorController animController)
        {
            string prefabPath = $"{PrefabsFolder}/Pickaxe_Tier1_FPS.prefab";

            // Check if prefab already exists - update it with new transform values
            GameObject existingPrefab = AssetDatabase.LoadAssetAtPath<GameObject>(prefabPath);
            if (existingPrefab != null)
            {
                Debug.Log($"[PickaxeFPSSetup] FPS prefab already exists, updating transform: {prefabPath}");

                // Open prefab for editing
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
                    meshChild.localRotation = Quaternion.Euler(-150f, 0f, 0f);
                    meshChild.localScale = new Vector3(50f, 50f, 50f);
                    Debug.Log($"[PickaxeFPSSetup] Updated mesh transform: scale=(50,50,50), rotation=(-150,0,0)");
                }

                // Save and unload
                PrefabUtility.SaveAsPrefabAsset(prefabRoot, prefabPath);
                PrefabUtility.UnloadPrefabContents(prefabRoot);

                return AssetDatabase.LoadAssetAtPath<GameObject>(prefabPath);
            }

            // Load the source FBX
            GameObject fbxModel = AssetDatabase.LoadAssetAtPath<GameObject>(MeshyFBXPath);
            if (fbxModel == null)
            {
                Debug.LogError($"[PickaxeFPSSetup] Could not load FBX from: {MeshyFBXPath}");
                return null;
            }

            // Create root GameObject for FPS prefab
            GameObject root = new GameObject("Pickaxe_Tier1_FPS");

            // Instantiate the mesh as a child
            GameObject meshInstance = Object.Instantiate(fbxModel);
            meshInstance.name = "Mesh";
            meshInstance.transform.SetParent(root.transform);

            // Adjust mesh local transform for FPS view
            // Scale up and rotate for proper FPS orientation
            meshInstance.transform.localPosition = new Vector3(0f, 0f, 0f);
            meshInstance.transform.localRotation = Quaternion.Euler(-150f, 0f, 0f);
            meshInstance.transform.localScale = new Vector3(50f, 50f, 50f);

            // Apply material to all renderers
            var renderers = meshInstance.GetComponentsInChildren<Renderer>();
            foreach (var renderer in renderers)
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
            // Profile will be assigned at runtime by PlayerToolVisualController

            // Save as prefab
            GameObject prefab = PrefabUtility.SaveAsPrefabAsset(root, prefabPath);

            // Clean up scene instance
            Object.DestroyImmediate(root);

            Debug.Log($"[PickaxeFPSSetup] Created FPS prefab: {prefabPath}");
            return prefab;
        }

        private static void UpdatePlayerToolVisualController(GameObject prefab)
        {
            // Find PlayerToolVisualController in scene or on Player
            var controller = Object.FindObjectOfType<Player.PlayerToolVisualController>();
            if (controller == null)
            {
                Debug.LogWarning("[PickaxeFPSSetup] PlayerToolVisualController not found in scene. " +
                                 "You may need to assign the prefab manually in the Inspector.");
                return;
            }

            // Use reflection to set the pickaxeTier1Prefab field
            var field = typeof(Player.PlayerToolVisualController).GetField("pickaxeTier1Prefab",
                System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);

            if (field != null)
            {
                field.SetValue(controller, prefab);
                EditorUtility.SetDirty(controller);
                Debug.Log("[PickaxeFPSSetup] Assigned prefab to PlayerToolVisualController.pickaxeTier1Prefab");
            }
            else
            {
                Debug.LogWarning("[PickaxeFPSSetup] Could not find pickaxeTier1Prefab field. " +
                                 "Please assign the prefab manually.");
            }

            // Mark scene dirty
            UnityEditor.SceneManagement.EditorSceneManager.MarkSceneDirty(
                UnityEngine.SceneManagement.SceneManager.GetActiveScene());
        }

        private static void PrintSummary(GameObject prefab, AnimatorController animController)
        {
            Debug.Log("=== PICKAXE TIER 1 FPS SETUP SUMMARY ===");
            Debug.Log($"Prefab: {AssetDatabase.GetAssetPath(prefab)}");
            Debug.Log($"Animator Controller: {AssetDatabase.GetAssetPath(animController)}");
            Debug.Log($"Idle Animation: {AnimationsFolder}/Pickaxe_Idle.anim");
            Debug.Log($"Dig Animation: {AnimationsFolder}/Pickaxe_Dig.anim");
            Debug.Log($"Material: {MaterialsFolder}/Pickaxe_T1_Mat.mat");
            Debug.Log("");
            Debug.Log("To trigger dig animation from code:");
            Debug.Log("  animator.SetTrigger(\"Dig\");");
            Debug.Log("");
            Debug.Log("Recommended ToolAnchor transform values:");
            Debug.Log("  Position: (0.3, -0.35, 0.5)");
            Debug.Log("  Rotation: (0, 0, 0)");
            Debug.Log("==========================================");
        }
    }
}
