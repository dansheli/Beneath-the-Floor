using UnityEngine;
using UnityEditor;
using UnityEditor.SceneManagement;
using System.Collections.Generic;

namespace BeneathTheFloor.Editor
{
    public static class CleanupMissingScripts
    {
        [MenuItem("Beneath The Floor/Cleanup Missing Scripts (Scene)")]
        public static void RemoveMissingScriptsFromScene()
        {
            int totalRemoved = 0;
            HashSet<GameObject> processed = new HashSet<GameObject>();

            var scene = EditorSceneManager.GetActiveScene();
            GameObject[] rootObjects = scene.GetRootGameObjects();

            foreach (GameObject root in rootObjects)
            {
                totalRemoved += CleanupRecursive(root, processed);
            }

            if (totalRemoved > 0)
            {
                Debug.Log($"=== SCENE CLEANUP: Removed {totalRemoved} missing scripts ===");
                EditorSceneManager.MarkSceneDirty(scene);
            }
            else
            {
                Debug.Log("=== No missing scripts found in scene ===");
            }
        }

        [MenuItem("Beneath The Floor/Cleanup Missing Scripts (AtmosphericHouse Prefabs)")]
        public static void RemoveMissingScriptsFromAtmosphericHouse()
        {
            CleanupPrefabsInFolder("Assets/AtmosphericHouse");
        }

        [MenuItem("Beneath The Floor/Cleanup Missing Scripts (All Prefabs)")]
        public static void RemoveMissingScriptsFromAllPrefabs()
        {
            CleanupPrefabsInFolder("Assets");
        }

        private static void CleanupPrefabsInFolder(string folderPath)
        {
            int totalRemoved = 0;
            int prefabsFixed = 0;

            string[] prefabGuids = AssetDatabase.FindAssets("t:Prefab", new[] { folderPath });
            Debug.Log($"Found {prefabGuids.Length} prefabs in {folderPath}");

            foreach (string guid in prefabGuids)
            {
                string path = AssetDatabase.GUIDToAssetPath(guid);
                GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(path);

                if (prefab == null) continue;

                int removed = CleanupPrefab(prefab, path);
                if (removed > 0)
                {
                    totalRemoved += removed;
                    prefabsFixed++;
                }
            }

            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();

            if (totalRemoved > 0)
            {
                Debug.Log($"=== PREFAB CLEANUP: Removed {totalRemoved} missing scripts from {prefabsFixed} prefabs ===");
            }
            else
            {
                Debug.Log($"=== No missing scripts found in prefabs ===");
            }
        }

        private static int CleanupPrefab(GameObject prefab, string path)
        {
            int totalRemoved = 0;

            // Open prefab for editing
            string prefabPath = AssetDatabase.GetAssetPath(prefab);
            using (var editingScope = new PrefabUtility.EditPrefabContentsScope(prefabPath))
            {
                GameObject prefabRoot = editingScope.prefabContentsRoot;
                totalRemoved = CleanupRecursive(prefabRoot, new HashSet<GameObject>());

                if (totalRemoved > 0)
                {
                    Debug.Log($"Removed {totalRemoved} missing script(s) from prefab: {path}");
                }
            }

            return totalRemoved;
        }

        private static int CleanupRecursive(GameObject go, HashSet<GameObject> processed)
        {
            if (go == null || processed.Contains(go)) return 0;
            processed.Add(go);

            int totalRemoved = 0;

            int removed = GameObjectUtility.RemoveMonoBehavioursWithMissingScript(go);
            if (removed > 0)
            {
                totalRemoved += removed;
                EditorUtility.SetDirty(go);
            }

            foreach (Transform child in go.transform)
            {
                totalRemoved += CleanupRecursive(child.gameObject, processed);
            }

            return totalRemoved;
        }
    }
}
