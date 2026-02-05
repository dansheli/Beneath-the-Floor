using UnityEngine;
using UnityEditor;
using UnityEditor.SceneManagement;

namespace BeneathTheFloor.Editor
{
    /// <summary>
    /// Editor script that automatically fixes FPS Arms asset issues.
    /// Removes the legacy Animation component that causes the "must be marked as Legacy" error.
    /// </summary>
    [InitializeOnLoad]
    public static class FPArmsEditorFix
    {
        static FPArmsEditorFix()
        {
            // Subscribe to scene opened event
            EditorSceneManager.sceneOpened += OnSceneOpened;

            // Also run on editor startup for current scene
            EditorApplication.delayCall += FixCurrentScene;
        }

        private static void OnSceneOpened(UnityEngine.SceneManagement.Scene scene, OpenSceneMode mode)
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode) return;
            FixFPArmsInScene();
        }

        private static void FixCurrentScene()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode) return;
            FixFPArmsInScene();
        }

        [MenuItem("Beneath The Floor/Setup/Fix FPS Arms Animation Component", false, 105)]
        public static void FixFPArmsInScene()
        {
            if (EditorApplication.isPlaying) return;
            // Find all GameObjects that might be FPS hands
            string[] possibleNames = { "PlayerFP_Arms", "v1", "v2", "v3", "v4", "v5", "fps_hands" };

            int fixedCount = 0;

            foreach (string name in possibleNames)
            {
                GameObject obj = GameObject.Find(name);
                if (obj != null)
                {
                    fixedCount += FixAnimationComponents(obj);
                }
            }

            // Also search by component - find all Animation components and check if they're on FPS hands
            var allAnimations = Object.FindObjectsOfType<Animation>(true);
            foreach (var anim in allAnimations)
            {
                if (anim == null) continue;

                // Check if this looks like FPS hands (has Animator sibling or in hierarchy)
                if (anim.GetComponent<Animator>() != null ||
                    anim.GetComponentInChildren<Animator>() != null ||
                    anim.GetComponentInParent<Animator>() != null)
                {
                    // This Animation component is alongside an Animator - likely FPS hands
                    // Remove or disable it
                    if (anim.enabled)
                    {
                        anim.enabled = false;
                        fixedCount++;
                        Debug.Log($"[FPArmsEditorFix] Disabled legacy Animation on: {anim.gameObject.name}");
                    }
                }
            }

            if (fixedCount > 0)
            {
                Debug.Log($"[FPArmsEditorFix] Fixed {fixedCount} Animation component(s)");
                EditorSceneManager.MarkSceneDirty(UnityEngine.SceneManagement.SceneManager.GetActiveScene());
            }
        }

        private static int FixAnimationComponents(GameObject root)
        {
            int count = 0;

            // Get all Animation components in this hierarchy
            var animations = root.GetComponentsInChildren<Animation>(true);
            foreach (var anim in animations)
            {
                if (anim.enabled)
                {
                    anim.enabled = false;
                    count++;
                    Debug.Log($"[FPArmsEditorFix] Disabled legacy Animation on: {anim.gameObject.name}");
                }
            }

            return count;
        }

        [MenuItem("Beneath The Floor/Setup/Fix FPS Arms Material", false, 107)]
        public static void FixFPArmsMaterial()
        {
            // Find the FPS arms
            string[] possibleNames = { "PlayerFP_Arms", "v1", "v2", "v3", "v4", "v5" };
            GameObject armsObj = null;

            foreach (string name in possibleNames)
            {
                armsObj = GameObject.Find(name);
                if (armsObj != null) break;
            }

            if (armsObj == null)
            {
                Debug.LogError("[FPArmsEditorFix] Could not find FPS arms in scene!");
                return;
            }

            // Load the v1 material
            Material armsMaterial = AssetDatabase.LoadAssetAtPath<Material>("Assets/animated_fps_hands_v3/materials/v1.mat");
            if (armsMaterial == null)
            {
                Debug.LogError("[FPArmsEditorFix] Could not find v1.mat material!");
                return;
            }

            // Find all renderers and assign the material (force assign to all)
            var renderers = armsObj.GetComponentsInChildren<Renderer>(true);
            int fixedCount = 0;

            Debug.Log($"[FPArmsEditorFix] Found {renderers.Length} renderers in {armsObj.name}");

            foreach (var renderer in renderers)
            {
                // Force assign material to all renderers (hands)
                renderer.sharedMaterial = armsMaterial;
                fixedCount++;
                Debug.Log($"[FPArmsEditorFix] Assigned material to: {renderer.gameObject.name}");
            }

            if (fixedCount > 0)
            {
                Debug.Log($"[FPArmsEditorFix] Fixed materials on {fixedCount} renderer(s)");
                EditorSceneManager.MarkSceneDirty(UnityEngine.SceneManagement.SceneManager.GetActiveScene());
            }
            else
            {
                Debug.Log("[FPArmsEditorFix] All renderers already have materials assigned");
            }
        }

        [MenuItem("Beneath The Floor/Setup/Remove FPS Arms Animation Component (Destructive)", false, 108)]
        public static void RemoveAnimationComponents()
        {
            string[] possibleNames = { "PlayerFP_Arms", "v1", "v2", "v3", "v4", "v5", "fps_hands" };

            int removedCount = 0;

            foreach (string name in possibleNames)
            {
                GameObject obj = GameObject.Find(name);
                if (obj != null)
                {
                    var animations = obj.GetComponentsInChildren<Animation>(true);
                    foreach (var anim in animations)
                    {
                        Object.DestroyImmediate(anim);
                        removedCount++;
                    }
                }
            }

            if (removedCount > 0)
            {
                Debug.Log($"[FPArmsEditorFix] Removed {removedCount} Animation component(s)");
                EditorSceneManager.MarkSceneDirty(UnityEngine.SceneManagement.SceneManager.GetActiveScene());
            }
            else
            {
                Debug.Log("[FPArmsEditorFix] No Animation components found to remove");
            }
        }
    }
}
