using UnityEngine;
using UnityEditor;
using UnityEditor.Animations;
using BeneathTheFloor.Digging;

namespace BeneathTheFloor.Editor
{
    /// <summary>
    /// Editor utility for setting up the tool visual system.
    /// </summary>
    public static class ToolVisualSetup
    {
        [MenuItem("Beneath The Floor/Setup/Setup Tool Visual System", false, 120)]
        public static void SetupToolVisualSystem()
        {
            // 1. Create Animator Controller
            CreatePickaxeAnimatorController();

            // 2. Add PlayerToolVisualController to Player
            AddToolControllerToPlayer();

            Debug.Log("[ToolVisualSetup] Tool visual system setup complete!");
        }

        [MenuItem("Beneath The Floor/Setup/Create Pickaxe Animator Controller", false, 121)]
        public static void CreatePickaxeAnimatorController()
        {
            string path = "Assets/Resources/PickaxeAnimator.controller";

            // Check if already exists
            var existing = AssetDatabase.LoadAssetAtPath<AnimatorController>(path);
            if (existing != null)
            {
                Debug.Log("[ToolVisualSetup] Pickaxe Animator Controller already exists");
                return;
            }

            // Create the animator controller
            var controller = AnimatorController.CreateAnimatorControllerAtPath(path);

            // Add parameters
            controller.AddParameter("Dig", AnimatorControllerParameterType.Trigger);
            controller.AddParameter("IsDigging", AnimatorControllerParameterType.Bool);

            // Get the base layer
            var rootStateMachine = controller.layers[0].stateMachine;

            // Create states
            var idleState = rootStateMachine.AddState("Idle", new Vector3(250, 50, 0));
            var digState = rootStateMachine.AddState("Dig", new Vector3(500, 50, 0));

            // Set Idle as default
            rootStateMachine.defaultState = idleState;

            // Create transitions
            // Idle -> Dig (on Dig trigger)
            var toDigTransition = idleState.AddTransition(digState);
            toDigTransition.AddCondition(AnimatorConditionMode.If, 0, "Dig");
            toDigTransition.hasExitTime = false;
            toDigTransition.duration = 0.1f;

            // Dig -> Idle (after animation completes)
            var toIdleTransition = digState.AddTransition(idleState);
            toIdleTransition.hasExitTime = true;
            toIdleTransition.exitTime = 0.9f;
            toIdleTransition.duration = 0.1f;

            EditorUtility.SetDirty(controller);
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();

            Debug.Log($"[ToolVisualSetup] Created Pickaxe Animator Controller at: {path}");
        }

        [MenuItem("Beneath The Floor/Setup/Add Tool Controller to Player", false, 122)]
        public static void AddToolControllerToPlayer()
        {
            // Find the Player object
            GameObject player = GameObject.FindGameObjectWithTag("Player");
            if (player == null)
            {
                player = GameObject.Find("Player");
            }

            if (player == null)
            {
                Debug.LogError("[ToolVisualSetup] Could not find Player object!");
                return;
            }

            // Check if already has the component
            var existingController = player.GetComponent<Player.PlayerToolVisualController>();
            if (existingController != null)
            {
                Debug.Log("[ToolVisualSetup] PlayerToolVisualController already exists on Player");
                return;
            }

            // Add the component
            var controller = player.AddComponent<Player.PlayerToolVisualController>();

            // Find and assign the ToolAnchor
            var mainCam = Camera.main;
            if (mainCam != null)
            {
                var toolAnchor = mainCam.transform.Find("ToolAnchor");
                if (toolAnchor != null)
                {
                    // Use reflection to set the serialized field
                    var field = typeof(Player.PlayerToolVisualController).GetField("toolAnchor",
                        System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
                    if (field != null)
                    {
                        field.SetValue(controller, toolAnchor);
                    }
                }
            }

            // Find and assign DiggingSystem
            var diggingSystem = Object.FindObjectOfType<DiggingSystem>();
            if (diggingSystem != null)
            {
                var field = typeof(Player.PlayerToolVisualController).GetField("diggingSystem",
                    System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
                if (field != null)
                {
                    field.SetValue(controller, diggingSystem);
                }
            }

            EditorUtility.SetDirty(player);
            UnityEditor.SceneManagement.EditorSceneManager.MarkSceneDirty(
                UnityEngine.SceneManagement.SceneManager.GetActiveScene());

            Debug.Log("[ToolVisualSetup] Added PlayerToolVisualController to Player");
        }

        [MenuItem("Beneath The Floor/Setup/Create Placeholder Pickaxe Prefabs", false, 123)]
        public static void CreatePlaceholderPickaxePrefabs()
        {
            string basePath = "Assets/Art/Tools/Pickaxe";

            // Ensure directory exists
            if (!AssetDatabase.IsValidFolder(basePath))
            {
                AssetDatabase.CreateFolder("Assets/Art/Tools", "Pickaxe");
            }

            for (int tier = 1; tier <= 3; tier++)
            {
                CreatePickaxePrefab(tier, basePath);
            }

            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();

            Debug.Log("[ToolVisualSetup] Created placeholder pickaxe prefabs");
        }

        private static void CreatePickaxePrefab(int tier, string basePath)
        {
            string prefabPath = $"{basePath}/Pickaxe_Tier{tier}.prefab";

            // Check if already exists
            if (AssetDatabase.LoadAssetAtPath<GameObject>(prefabPath) != null)
            {
                Debug.Log($"[ToolVisualSetup] Pickaxe Tier {tier} prefab already exists");
                return;
            }

            // Create the pickaxe hierarchy
            GameObject pickaxe = new GameObject($"Pickaxe_Tier{tier}");

            // Create handle
            GameObject handle = GameObject.CreatePrimitive(PrimitiveType.Cube);
            handle.name = "Handle";
            handle.transform.SetParent(pickaxe.transform);
            handle.transform.localPosition = new Vector3(0, 0, 0.15f);
            handle.transform.localRotation = Quaternion.Euler(90, 0, 0);
            handle.transform.localScale = new Vector3(0.03f, 0.3f, 0.03f);
            Object.DestroyImmediate(handle.GetComponent<Collider>());

            // Create head
            GameObject head = GameObject.CreatePrimitive(PrimitiveType.Cube);
            head.name = "Head";
            head.transform.SetParent(pickaxe.transform);
            head.transform.localPosition = new Vector3(0, 0, 0.35f);
            head.transform.localRotation = Quaternion.Euler(0, 0, 45);
            head.transform.localScale = new Vector3(0.15f, 0.04f, 0.04f);
            Object.DestroyImmediate(head.GetComponent<Collider>());

            // Create pick point
            GameObject point = GameObject.CreatePrimitive(PrimitiveType.Cube);
            point.name = "Point";
            point.transform.SetParent(pickaxe.transform);
            point.transform.localPosition = new Vector3(0.1f, 0, 0.35f);
            point.transform.localRotation = Quaternion.Euler(0, 0, 45);
            point.transform.localScale = new Vector3(0.08f, 0.03f, 0.03f);
            Object.DestroyImmediate(point.GetComponent<Collider>());

            // Apply tier-specific materials
            ApplyTierMaterials(pickaxe, tier);

            // Disable shadow casting
            foreach (var renderer in pickaxe.GetComponentsInChildren<Renderer>())
            {
                renderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            }

            // Add animator
            var animator = pickaxe.AddComponent<Animator>();
            var controller = AssetDatabase.LoadAssetAtPath<RuntimeAnimatorController>("Assets/Resources/PickaxeAnimator.controller");
            if (controller != null)
            {
                animator.runtimeAnimatorController = controller;
            }

            // Save as prefab
            PrefabUtility.SaveAsPrefabAsset(pickaxe, prefabPath);

            // Clean up scene object
            Object.DestroyImmediate(pickaxe);

            Debug.Log($"[ToolVisualSetup] Created Pickaxe Tier {tier} prefab at: {prefabPath}");
        }

        private static void ApplyTierMaterials(GameObject pickaxe, int tier)
        {
            Shader urpLit = Shader.Find("Universal Render Pipeline/Lit");
            if (urpLit == null) urpLit = Shader.Find("Standard");

            Color handleColor = new Color(0.4f, 0.25f, 0.1f);
            Color metalColor = tier switch
            {
                1 => new Color(0.5f, 0.5f, 0.5f),
                2 => new Color(0.8f, 0.6f, 0.2f),
                3 => new Color(0.3f, 0.8f, 0.9f),
                _ => new Color(0.5f, 0.5f, 0.5f)
            };

            Material handleMat = new Material(urpLit);
            handleMat.SetColor("_BaseColor", handleColor);

            Material metalMat = new Material(urpLit);
            metalMat.SetColor("_BaseColor", metalColor);
            metalMat.SetFloat("_Metallic", 0.8f);
            metalMat.SetFloat("_Smoothness", 0.6f);

            foreach (Transform child in pickaxe.transform)
            {
                var renderer = child.GetComponent<Renderer>();
                if (renderer != null)
                {
                    renderer.sharedMaterial = child.name == "Handle" ? handleMat : metalMat;
                }
            }
        }
    }
}
