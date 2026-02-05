using UnityEngine;
using UnityEditor;
using UnityEditor.SceneManagement;
using System.Collections.Generic;

namespace BeneathTheFloor.Editor
{
    /// <summary>
    /// Editor utility to clean up old digging system remnants before DiggingV2 implementation.
    /// </summary>
    public class DiggingV2Cleanup : EditorWindow
    {
        [MenuItem("Tools/DiggingV2/Phase 0 - Cleanup Old System")]
        public static void RunCleanup()
        {
            Debug.Log("=== DiggingV2 Cleanup: Starting Phase 0 ===");

            int deletedObjects = 0;
            int removedComponents = 0;

            // 1. Delete DepthManager GameObject
            GameObject depthManager = GameObject.Find("DepthManager");
            if (depthManager != null)
            {
                Debug.Log("[Cleanup] Deleting DepthManager GameObject");
                Undo.DestroyObjectImmediate(depthManager);
                deletedObjects++;
            }
            else
            {
                Debug.Log("[Cleanup] DepthManager not found (already deleted)");
            }

            // 2. Delete UndergroundLayerManager GameObject
            GameObject undergroundLayerManager = GameObject.Find("UndergroundLayerManager");
            if (undergroundLayerManager != null)
            {
                Debug.Log("[Cleanup] Deleting UndergroundLayerManager GameObject");
                Undo.DestroyObjectImmediate(undergroundLayerManager);
                deletedObjects++;
            }
            else
            {
                Debug.Log("[Cleanup] UndergroundLayerManager not found (already deleted)");
            }

            // 3. Remove missing script components from Player
            GameObject player = GameObject.FindGameObjectWithTag("Player");
            if (player != null)
            {
                removedComponents += RemoveMissingScripts(player);

                // Also check children
                foreach (Transform child in player.GetComponentsInChildren<Transform>(true))
                {
                    if (child.gameObject != player)
                    {
                        removedComponents += RemoveMissingScripts(child.gameObject);
                    }
                }
            }
            else
            {
                Debug.LogWarning("[Cleanup] Player not found! Cannot clean missing scripts.");
            }

            // Mark scene dirty and save
            EditorSceneManager.MarkSceneDirty(EditorSceneManager.GetActiveScene());
            EditorSceneManager.SaveOpenScenes();

            Debug.Log($"=== DiggingV2 Cleanup Complete ===");
            Debug.Log($"  Deleted GameObjects: {deletedObjects}");
            Debug.Log($"  Removed missing components: {removedComponents}");
            Debug.Log("  Scene saved.");
        }

        private static int RemoveMissingScripts(GameObject go)
        {
            int count = GameObjectUtility.GetMonoBehavioursWithMissingScriptCount(go);
            if (count > 0)
            {
                Debug.Log($"[Cleanup] Removing {count} missing script(s) from '{go.name}'");
                Undo.RegisterCompleteObjectUndo(go, "Remove Missing Scripts");
                GameObjectUtility.RemoveMonoBehavioursWithMissingScript(go);
            }
            return count;
        }

        [MenuItem("Tools/DiggingV2/Verify Cleanup")]
        public static void VerifyCleanup()
        {
            Debug.Log("=== Verifying DiggingV2 Cleanup ===");

            bool allClean = true;

            // Check for DepthManager
            if (GameObject.Find("DepthManager") != null)
            {
                Debug.LogWarning("[Verify] DepthManager still exists!");
                allClean = false;
            }

            // Check for UndergroundLayerManager
            if (GameObject.Find("UndergroundLayerManager") != null)
            {
                Debug.LogWarning("[Verify] UndergroundLayerManager still exists!");
                allClean = false;
            }

            // Check Player for missing scripts
            GameObject player = GameObject.FindGameObjectWithTag("Player");
            if (player != null)
            {
                int missing = GameObjectUtility.GetMonoBehavioursWithMissingScriptCount(player);
                if (missing > 0)
                {
                    Debug.LogWarning($"[Verify] Player still has {missing} missing script(s)!");
                    allClean = false;
                }
            }

            if (allClean)
            {
                Debug.Log("[Verify] All clean! Ready for DiggingV2 implementation.");
            }
            else
            {
                Debug.LogError("[Verify] Cleanup incomplete. Run 'Phase 0 - Cleanup Old System' first.");
            }
        }
    }
}
