using UnityEngine;
using UnityEditor;
using BeneathTheFloor.Player;

namespace BeneathTheFloor.Editor
{
    /// <summary>
    /// Editor utility to set up the FPS Arms in the scene.
    /// </summary>
    public static class FPArmsSetup
    {
        private const string FPS_HANDS_PREFAB_PATH = "Assets/animated_fps_hands_v3/prefabs/v1.prefab";
        private const string FPS_HANDS_ANCHOR_NAME = "FP_Arms_Anchor";
        private const string PLAYER_FP_ARMS_NAME = "PlayerFP_Arms";

        [MenuItem("Beneath The Floor/Setup/Setup FPS Arms on Camera", false, 100)]
        public static void SetupFPSArms()
        {
            // Find the main camera
            Camera mainCamera = Camera.main;
            if (mainCamera == null)
            {
                // Try to find by tag
                GameObject camObj = GameObject.FindGameObjectWithTag("MainCamera");
                if (camObj != null)
                {
                    mainCamera = camObj.GetComponent<Camera>();
                }
            }

            if (mainCamera == null)
            {
                EditorUtility.DisplayDialog("FPS Arms Setup",
                    "No Main Camera found in scene. Please ensure a camera with 'MainCamera' tag exists.",
                    "OK");
                return;
            }

            Debug.Log($"[FPArmsSetup] Found main camera: {mainCamera.name}");

            // Check if arms already exist
            var existingController = Object.FindObjectOfType<PlayerFPArmsController>();
            if (existingController != null)
            {
                bool replace = EditorUtility.DisplayDialog("FPS Arms Setup",
                    "FPS Arms already exist in scene. Replace them?",
                    "Replace", "Cancel");

                if (!replace)
                    return;

                // Remove existing
                Object.DestroyImmediate(existingController.gameObject);
                Debug.Log("[FPArmsSetup] Removed existing FPS arms");
            }

            // Load the FPS hands prefab
            GameObject fpsPrefab = AssetDatabase.LoadAssetAtPath<GameObject>(FPS_HANDS_PREFAB_PATH);
            if (fpsPrefab == null)
            {
                EditorUtility.DisplayDialog("FPS Arms Setup",
                    $"Could not find FPS hands prefab at:\n{FPS_HANDS_PREFAB_PATH}\n\nPlease verify the asset is imported.",
                    "OK");
                return;
            }

            Debug.Log($"[FPArmsSetup] Loaded prefab: {fpsPrefab.name}");

            // Create anchor under camera
            Transform anchor = mainCamera.transform.Find(FPS_HANDS_ANCHOR_NAME);
            if (anchor == null)
            {
                GameObject anchorObj = new GameObject(FPS_HANDS_ANCHOR_NAME);
                anchorObj.transform.SetParent(mainCamera.transform);
                anchorObj.transform.localPosition = Vector3.zero;
                anchorObj.transform.localRotation = Quaternion.identity;
                anchor = anchorObj.transform;
                Debug.Log("[FPArmsSetup] Created FP_Arms_Anchor");
            }

            // Instantiate the FPS hands
            GameObject armsInstance = (GameObject)PrefabUtility.InstantiatePrefab(fpsPrefab);
            armsInstance.name = PLAYER_FP_ARMS_NAME;
            armsInstance.transform.SetParent(anchor);

            // Position the arms for FPS view
            // These values may need tweaking based on the specific model
            armsInstance.transform.localPosition = new Vector3(0f, -0.5f, 0.3f);
            armsInstance.transform.localRotation = Quaternion.identity;
            armsInstance.transform.localScale = Vector3.one;

            Debug.Log("[FPArmsSetup] Instantiated FPS arms");

            // Add the controller script
            PlayerFPArmsController controller = armsInstance.GetComponent<PlayerFPArmsController>();
            if (controller == null)
            {
                controller = armsInstance.AddComponent<PlayerFPArmsController>();
                Debug.Log("[FPArmsSetup] Added PlayerFPArmsController component");
            }

            // Configure renderer shadows (optional - disable shadow casting for FP view)
            ConfigureRenderers(armsInstance);

            // Select the arms in hierarchy
            Selection.activeGameObject = armsInstance;

            // Mark scene dirty
            UnityEditor.SceneManagement.EditorSceneManager.MarkSceneDirty(
                UnityEngine.SceneManagement.SceneManager.GetActiveScene());

            EditorUtility.DisplayDialog("FPS Arms Setup",
                $"FPS Arms setup complete!\n\n" +
                $"Arms: {armsInstance.name}\n" +
                $"Parent: {anchor.parent.name}/{anchor.name}\n\n" +
                "You may need to adjust the position/rotation in the Inspector for best appearance.",
                "OK");

            Debug.Log("[FPArmsSetup] Setup complete!");
        }

        [MenuItem("Beneath The Floor/Setup/Remove FPS Arms", false, 101)]
        public static void RemoveFPSArms()
        {
            var controller = Object.FindObjectOfType<PlayerFPArmsController>();
            if (controller != null)
            {
                Object.DestroyImmediate(controller.gameObject);

                // Also try to remove anchor if empty
                Camera mainCamera = Camera.main;
                if (mainCamera != null)
                {
                    Transform anchor = mainCamera.transform.Find(FPS_HANDS_ANCHOR_NAME);
                    if (anchor != null && anchor.childCount == 0)
                    {
                        Object.DestroyImmediate(anchor.gameObject);
                    }
                }

                UnityEditor.SceneManagement.EditorSceneManager.MarkSceneDirty(
                    UnityEngine.SceneManagement.SceneManager.GetActiveScene());

                Debug.Log("[FPArmsSetup] FPS Arms removed");
            }
            else
            {
                EditorUtility.DisplayDialog("FPS Arms Setup", "No FPS Arms found in scene.", "OK");
            }
        }

        [MenuItem("Beneath The Floor/Setup/Adjust FPS Arms Position", false, 102)]
        public static void AdjustFPSArmsPosition()
        {
            var controller = Object.FindObjectOfType<PlayerFPArmsController>();
            if (controller == null)
            {
                EditorUtility.DisplayDialog("FPS Arms Setup", "No FPS Arms found in scene. Run 'Setup FPS Arms' first.", "OK");
                return;
            }

            Selection.activeGameObject = controller.gameObject;
            SceneView.lastActiveSceneView?.FrameSelected();
        }

        /// <summary>
        /// Configures renderers on the arms for FPS view.
        /// </summary>
        private static void ConfigureRenderers(GameObject armsRoot)
        {
            var renderers = armsRoot.GetComponentsInChildren<Renderer>();
            foreach (var renderer in renderers)
            {
                // Disable shadow casting for FPS arms (usually looks better)
                renderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;

                // Could also set layer here if using a separate FPS arms layer
                // renderer.gameObject.layer = LayerMask.NameToLayer("FirstPersonArms");
            }

            if (renderers.Length > 0)
            {
                Debug.Log($"[FPArmsSetup] Configured {renderers.Length} renderers (shadows disabled)");
            }
        }

        [MenuItem("Beneath The Floor/Setup/Find Right Hand Bone", false, 120)]
        public static void FindRightHandBone()
        {
            var controller = Object.FindObjectOfType<PlayerFPArmsController>();
            if (controller == null)
            {
                EditorUtility.DisplayDialog("FPS Arms Setup", "No FPS Arms found in scene.", "OK");
                return;
            }

            // Search for hand bones
            string[] possibleNames = { "RightHand", "Hand_R", "hand_R", "R_Hand", "hand.R", "mixamorig:RightHand" };
            Transform found = null;

            foreach (string name in possibleNames)
            {
                found = FindDeepChild(controller.transform, name);
                if (found != null)
                {
                    Debug.Log($"[FPArmsSetup] Found hand bone: {found.name} at path: {GetFullPath(found)}");
                    Selection.activeGameObject = found.gameObject;
                    SceneView.lastActiveSceneView?.FrameSelected();
                    return;
                }
            }

            // List all bones for debugging
            Debug.Log("[FPArmsSetup] Could not find standard hand bone. Listing all transforms:");
            ListAllTransforms(controller.transform, "");

            EditorUtility.DisplayDialog("FPS Arms Setup",
                "Could not find right hand bone automatically.\nCheck Console for full transform list.",
                "OK");
        }

        private static Transform FindDeepChild(Transform parent, string name)
        {
            foreach (Transform child in parent)
            {
                if (child.name.Equals(name, System.StringComparison.OrdinalIgnoreCase))
                    return child;

                Transform found = FindDeepChild(child, name);
                if (found != null)
                    return found;
            }
            return null;
        }

        private static void ListAllTransforms(Transform parent, string indent)
        {
            Debug.Log($"{indent}{parent.name}");
            foreach (Transform child in parent)
            {
                ListAllTransforms(child, indent + "  ");
            }
        }

        private static string GetFullPath(Transform t)
        {
            string path = t.name;
            while (t.parent != null)
            {
                t = t.parent;
                path = t.name + "/" + path;
            }
            return path;
        }
    }
}
