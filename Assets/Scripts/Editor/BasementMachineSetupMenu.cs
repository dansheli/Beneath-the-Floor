using UnityEngine;
using UnityEditor;
using BeneathTheFloor.Machines;

namespace BeneathTheFloor.Editor
{
    public static class BasementMachineSetupMenu
    {
        [MenuItem("Beneath The Floor/Setup Basement Machines")]
        public static void SetupBasementMachines()
        {
            // Find or create the setup object
            var setupObj = GameObject.Find("BasementMachineSetup");
            if (setupObj == null)
            {
                setupObj = new GameObject("BasementMachineSetup");
            }

            var setup = setupObj.GetComponent<BasementMachineSetup>();
            if (setup == null)
            {
                setup = setupObj.AddComponent<BasementMachineSetup>();
            }

            // Run the setup
            setup.SetupBasementMachines();

            // Mark scene dirty
            UnityEditor.SceneManagement.EditorSceneManager.MarkSceneDirty(
                UnityEditor.SceneManagement.EditorSceneManager.GetActiveScene()
            );

            Debug.Log("[Editor] Basement machines setup complete!");
        }

        [MenuItem("Beneath The Floor/Create Machine Prefabs Folder")]
        public static void CreateMachinePrefabsFolder()
        {
            string path = "Assets/Prefabs/Machines";
            if (!AssetDatabase.IsValidFolder("Assets/Prefabs"))
            {
                AssetDatabase.CreateFolder("Assets", "Prefabs");
            }
            if (!AssetDatabase.IsValidFolder(path))
            {
                AssetDatabase.CreateFolder("Assets/Prefabs", "Machines");
            }
            AssetDatabase.Refresh();
            Debug.Log($"[Editor] Created folder: {path}");
        }

        [MenuItem("Beneath The Floor/Save Machine Prefabs")]
        public static void SaveMachinePrefabs()
        {
            // Ensure folder exists
            CreateMachinePrefabsFolder();

            // Find machines in scene and save as prefabs
            // NOTE: Workbench removed - using UpgradeStation as single upgrade point
            string[] machineNames = { "Refinery", "UpgradeStation", "EnergyGenerator" };

            foreach (string name in machineNames)
            {
                GameObject machine = GameObject.Find(name);
                if (machine != null)
                {
                    string prefabPath = $"Assets/Prefabs/Machines/{name}.prefab";

                    // Check if prefab already exists
                    GameObject existingPrefab = AssetDatabase.LoadAssetAtPath<GameObject>(prefabPath);

                    if (existingPrefab != null)
                    {
                        // Update existing prefab
                        PrefabUtility.SaveAsPrefabAsset(machine, prefabPath);
                        Debug.Log($"[Editor] Updated prefab: {prefabPath}");
                    }
                    else
                    {
                        // Create new prefab
                        PrefabUtility.SaveAsPrefabAsset(machine, prefabPath);
                        Debug.Log($"[Editor] Created prefab: {prefabPath}");
                    }
                }
                else
                {
                    Debug.LogWarning($"[Editor] Machine '{name}' not found in scene");
                }
            }

            AssetDatabase.Refresh();
            Debug.Log("[Editor] Machine prefabs saved!");
        }
    }
}
