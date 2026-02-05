using UnityEngine;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine.SceneManagement;
using System.Collections.Generic;

/// <summary>
/// Editor tool to merge BasementScene functionality into HouseBuilding scene.
/// </summary>
public class SceneMerge : EditorWindow
{
    // Objects to transfer from BasementScene
    private static readonly string[] ObjectsToTransfer = new string[]
    {
        "Player",
        "---MANAGERS---",
        "EventSystem",
        "InventorySystem",
        "StoryManager",
        "UpgradeSystem",
        "WorldDropManager",
        "FXManager",
        "EconomySetup",
        "MachineUICanvas",
        "DiggingManager",
        "DigShaftCenter",
        "ModularShaftWallManager",
        "WorldRoomsManager",
        "AncientRuinsGenerator",
        "BasementSpawnPoint",
        "BasementMachineSetup"
    };

    // Objects to delete from BasementScene (won't be transferred)
    private static readonly string[] ObjectsToDelete = new string[]
    {
        "Basement",
        "Directional Light",
        "BasementLight_1",
        "BasementLight_2",
        "BasementLight_Stairs",
        "DepthLightingController",
        "TempDDOL",
        "MeshyBridge",
        "Meshy_Model",
        "Machines",
        "MachineSetup",
        "DiggingDebugPanel"
    };

    // Machine names to extract from Basement/Machines
    private static readonly string[] MachinesToExtract = new string[]
    {
        "Refinery",
        "UpgradeStation",
        "EnergyGenerator",
        "TradeTerminal"
    };

    [MenuItem("Tools/SceneMerge/Merge BasementScene into HouseBuilding")]
    public static void DoMerge()
    {
        Debug.Log("=== Starting Scene Merge ===");

        // Step 1: Load HouseBuilding as the main scene
        Debug.Log("Step 1: Loading HouseBuilding scene...");
        EditorSceneManager.OpenScene("Assets/Scenes/HouseBuilding.unity", OpenSceneMode.Single);
        Scene houseScene = SceneManager.GetActiveScene();

        // Step 2: Load BasementScene additively
        Debug.Log("Step 2: Loading BasementScene additively...");
        EditorSceneManager.OpenScene("Assets/Scenes/BasementScene.unity", OpenSceneMode.Additive);
        Scene basementScene = SceneManager.GetSceneByPath("Assets/Scenes/BasementScene.unity");

        if (!basementScene.IsValid())
        {
            Debug.LogError("Failed to load BasementScene!");
            return;
        }

        // Step 3: Find the HUDCanvas with InventoryUIManager child (keep this one)
        Debug.Log("Step 3: Finding HUDCanvas with InventoryUIManager...");
        GameObject hudCanvasToKeep = null;
        GameObject[] basementRoots = basementScene.GetRootGameObjects();
        foreach (GameObject root in basementRoots)
        {
            if (root.name == "HUDCanvas")
            {
                // Check if this one has an InventoryUIManager child
                Transform invUI = root.transform.Find("InventoryUIManager");
                if (invUI != null)
                {
                    hudCanvasToKeep = root;
                    Debug.Log("Found HUDCanvas with InventoryUIManager child - will keep this one");
                }
            }
        }

        // Step 4: Extract machines from Basement/Machines before deleting Basement
        Debug.Log("Step 4: Extracting machines...");
        List<GameObject> extractedMachines = new List<GameObject>();
        foreach (GameObject root in basementRoots)
        {
            if (root.name == "Basement")
            {
                Transform machinesParent = root.transform.Find("Machines");
                if (machinesParent != null)
                {
                    foreach (string machineName in MachinesToExtract)
                    {
                        Transform machine = machinesParent.Find(machineName);
                        if (machine != null)
                        {
                            machine.SetParent(null);
                            SceneManager.MoveGameObjectToScene(machine.gameObject, basementScene);
                            extractedMachines.Add(machine.gameObject);
                            Debug.Log("  Extracted: " + machineName);
                        }
                    }
                }
                break;
            }
        }

        // Step 5: Delete unwanted objects from BasementScene
        Debug.Log("Step 5: Deleting unwanted objects...");
        basementRoots = basementScene.GetRootGameObjects();
        List<GameObject> toDelete = new List<GameObject>();

        foreach (GameObject root in basementRoots)
        {
            foreach (string objName in ObjectsToDelete)
            {
                // Use exact matching only - StartsWith caused issues with BasementSpawnPoint/BasementMachineSetup
                if (root.name == objName)
                {
                    if (root.name == "HUDCanvas" && root == hudCanvasToKeep)
                        continue;
                    toDelete.Add(root);
                    break;
                }
            }
        }

        foreach (GameObject obj in toDelete)
        {
            Debug.Log("  Deleting: " + obj.name);
            DestroyImmediate(obj);
        }

        // Step 6: Delete HouseBuilding's standalone Main Camera
        Debug.Log("Step 6: Removing HouseBuilding's standalone camera...");
        GameObject[] houseRoots = houseScene.GetRootGameObjects();
        foreach (GameObject root in houseRoots)
        {
            if (root.name == "Main Camera")
            {
                Debug.Log("  Deleting standalone Main Camera from HouseBuilding");
                DestroyImmediate(root);
                break;
            }
        }

        // Step 7: Transfer objects from BasementScene to HouseBuilding
        Debug.Log("Step 7: Transferring objects to HouseBuilding...");
        basementRoots = basementScene.GetRootGameObjects();
        List<GameObject> toTransfer = new List<GameObject>();

        foreach (GameObject root in basementRoots)
        {
            bool shouldTransfer = false;

            foreach (string objName in ObjectsToTransfer)
            {
                if (root.name == objName)
                {
                    shouldTransfer = true;
                    break;
                }
            }

            if (root == hudCanvasToKeep)
                shouldTransfer = true;

            if (extractedMachines.Contains(root))
                shouldTransfer = true;

            if (shouldTransfer)
                toTransfer.Add(root);
        }

        foreach (GameObject obj in toTransfer)
        {
            Debug.Log("  Transferring: " + obj.name);
            SceneManager.MoveGameObjectToScene(obj, houseScene);
        }

        // Step 8: Create Machines container and parent machines
        Debug.Log("Step 8: Organizing machines...");
        GameObject machinesContainer = new GameObject("---MACHINES---");
        SceneManager.MoveGameObjectToScene(machinesContainer, houseScene);

        foreach (GameObject machine in extractedMachines)
        {
            if (machine != null)
                machine.transform.SetParent(machinesContainer.transform);
        }

        // Step 9: Reposition machines
        Debug.Log("Step 9: Repositioning machines...");
        RepositionMachines(machinesContainer);

        // Step 10: Set up spawn point
        Debug.Log("Step 10: Configuring spawn point...");
        ConfigureSpawnPoint();

        // Step 11: Configure DigShaftCenter
        Debug.Log("Step 11: Configuring dig shaft center...");
        ConfigureDigShaftCenter();

        // Step 12: Unload BasementScene
        Debug.Log("Step 12: Unloading BasementScene...");
        EditorSceneManager.CloseScene(basementScene, true);

        // Step 13: Save
        Debug.Log("Step 13: Saving merged scene...");
        EditorSceneManager.MarkSceneDirty(houseScene);
        EditorSceneManager.SaveScene(houseScene);

        Debug.Log("=== Scene Merge Complete! ===");
        Debug.Log("Please review machine positions, spawn point, and dig shaft center.");
    }

    private static void RepositionMachines(GameObject machinesContainer)
    {
        GameObject basement = GameObject.Find("basement");

        if (basement == null)
        {
            Debug.LogWarning("Could not find 'basement' object. Using default positions.");
            PositionMachine(machinesContainer, "Refinery", new Vector3(5, -2.5f, 3), 180f);
            PositionMachine(machinesContainer, "UpgradeStation", new Vector3(-5, -2.5f, 3), 0f);
            PositionMachine(machinesContainer, "EnergyGenerator", new Vector3(-5, -2.5f, -3), 90f);
            PositionMachine(machinesContainer, "TradeTerminal", new Vector3(5, -2.5f, -3), -90f);
            return;
        }

        Bounds bounds = GetCombinedBounds(basement);
        Debug.Log("Basement bounds: center=" + bounds.center + ", size=" + bounds.size);

        float floorY = bounds.min.y + 0.5f;
        float margin = 1.5f;

        // Position machines in corners/along walls
        float xMin = bounds.min.x + margin;
        float xMax = bounds.max.x - margin;
        float zMin = bounds.min.z + margin;
        float zMax = bounds.max.z - margin;

        PositionMachine(machinesContainer, "Refinery", new Vector3(xMax - 1, floorY, zMax - 2), 180f);
        PositionMachine(machinesContainer, "UpgradeStation", new Vector3(xMin + 1, floorY, zMax - 2), 0f);
        PositionMachine(machinesContainer, "EnergyGenerator", new Vector3(xMin + 1, floorY, zMin + 2), 90f);
        PositionMachine(machinesContainer, "TradeTerminal", new Vector3(xMax - 1, floorY, zMin + 2), -90f);
    }

    private static void PositionMachine(GameObject container, string name, Vector3 pos, float rotY)
    {
        Transform machine = container.transform.Find(name);
        if (machine != null)
        {
            machine.position = pos;
            machine.rotation = Quaternion.Euler(0, rotY, 0);
            Debug.Log("  Positioned " + name + " at " + pos);
        }
        else
        {
            Debug.LogWarning("  Machine not found: " + name);
        }
    }

    private static Bounds GetCombinedBounds(GameObject obj)
    {
        Renderer[] renderers = obj.GetComponentsInChildren<Renderer>();
        if (renderers.Length == 0)
            return new Bounds(obj.transform.position, Vector3.one * 10);

        Bounds bounds = renderers[0].bounds;
        for (int i = 1; i < renderers.Length; i++)
            bounds.Encapsulate(renderers[i].bounds);
        return bounds;
    }

    private static void ConfigureSpawnPoint()
    {
        GameObject spawnPoint = GameObject.Find("BasementSpawnPoint");
        GameObject stairs = GameObject.Find("Stairs_basement");
        GameObject basement = GameObject.Find("basement");

        if (spawnPoint != null)
        {
            if (stairs != null)
            {
                Bounds stairBounds = GetCombinedBounds(stairs);
                spawnPoint.transform.position = new Vector3(
                    stairBounds.center.x,
                    stairBounds.min.y + 0.5f,
                    stairBounds.center.z - 2f
                );
                Debug.Log("  Spawn point near stairs: " + spawnPoint.transform.position);
            }
            else if (basement != null)
            {
                Bounds bounds = GetCombinedBounds(basement);
                spawnPoint.transform.position = new Vector3(
                    bounds.center.x,
                    bounds.min.y + 0.5f,
                    bounds.center.z
                );
                Debug.Log("  Spawn point in center: " + spawnPoint.transform.position);
            }
        }
    }

    private static void ConfigureDigShaftCenter()
    {
        GameObject digCenter = GameObject.Find("DigShaftCenter");
        GameObject basement = GameObject.Find("basement");

        if (digCenter != null && basement != null)
        {
            Bounds bounds = GetCombinedBounds(basement);
            digCenter.transform.position = new Vector3(
                bounds.center.x,
                bounds.min.y + 0.1f,
                bounds.center.z
            );
            Debug.Log("  DigShaftCenter at: " + digCenter.transform.position);
            Debug.Log("  NOTE: Create a hole in the floor at this location!");
        }
    }

    [MenuItem("Tools/SceneMerge/Validate Merged Scene")]
    public static void ValidateMergedScene()
    {
        Debug.Log("=== Validating Merged Scene ===");

        string[] essentials = { "Player", "---MANAGERS---", "EventSystem", "MachineUICanvas", "basement", "Lighting_additional" };
        bool ok = true;

        foreach (string name in essentials)
        {
            if (GameObject.Find(name) == null)
            {
                Debug.LogError("MISSING: " + name);
                ok = false;
            }
            else
            {
                Debug.Log("OK: " + name);
            }
        }

        GameObject machines = GameObject.Find("---MACHINES---");
        if (machines != null)
        {
            foreach (string m in MachinesToExtract)
            {
                Transform t = machines.transform.Find(m);
                if (t == null)
                {
                    Debug.LogError("MISSING MACHINE: " + m);
                    ok = false;
                }
                else
                {
                    Debug.Log("OK MACHINE: " + m + " at " + t.position);
                }
            }
        }

        Camera[] cameras = Object.FindObjectsByType<Camera>(FindObjectsSortMode.None);
        if (cameras.Length > 1)
            Debug.LogWarning("Multiple cameras found: " + cameras.Length);

        Debug.Log(ok ? "=== Validation PASSED ===" : "=== Validation FAILED ===");
    }

    [MenuItem("Tools/SceneMerge/Read Floor Positions")]
    public static void ReadFloorPositions()
    {
        string[] floorNames = { "Floor_North", "Floor_South", "Floor_East", "Floor_West" };
        Vector3 totalPos = Vector3.zero;
        int found = 0;

        foreach (string name in floorNames)
        {
            GameObject floor = GameObject.Find(name);
            if (floor != null)
            {
                Debug.Log($"{name}: Position = {floor.transform.position}");
                totalPos += floor.transform.position;
                found++;
            }
            else
            {
                Debug.LogWarning($"{name}: NOT FOUND");
            }
        }

        if (found > 0)
        {
            Vector3 center = totalPos / found;
            Debug.Log($"=== CENTER OF {found} FLOORS: ({center.x}, {center.y}, {center.z}) ===");
        }
    }

    [MenuItem("Tools/SceneMerge/Close BasementScene (Don't Save)")]
    public static void CloseBasementScene()
    {
        Scene basementScene = SceneManager.GetSceneByPath("Assets/Scenes/BasementScene.unity");
        if (basementScene.IsValid() && basementScene.isLoaded)
        {
            EditorSceneManager.CloseScene(basementScene, true);
            Debug.Log("BasementScene closed without saving.");
        }
        else
        {
            Debug.Log("BasementScene is not loaded.");
        }
    }

    [MenuItem("Tools/SceneMerge/Transfer UndergroundTerrain from BasementScene")]
    public static void TransferUndergroundTerrain()
    {
        Debug.Log("=== Transferring Digging Objects ===");

        // Find HouseBuilding scene (should be active)
        Scene houseScene = SceneManager.GetSceneByPath("Assets/Scenes/HouseBuilding.unity");
        if (!houseScene.IsValid())
        {
            houseScene = SceneManager.GetActiveScene();
        }

        // Find BasementScene
        Scene basementScene = SceneManager.GetSceneByPath("Assets/Scenes/BasementScene.unity");
        if (!basementScene.IsValid())
        {
            Debug.LogError("BasementScene not loaded! Load it additively first.");
            return;
        }

        // List all objects in BasementScene for inspection
        GameObject[] basementRoots = basementScene.GetRootGameObjects();
        Debug.Log("=== Objects in BasementScene ===");
        foreach (GameObject root in basementRoots)
        {
            Debug.Log("  - " + root.name);
        }

        // Transfer digging-related objects
        string[] diggingObjects = { "UndergroundTerrain", "DiggingV2RuntimeSetup", "DiggingSystem", "TerrainManager" };
        int transferred = 0;

        foreach (GameObject root in basementRoots)
        {
            foreach (string objName in diggingObjects)
            {
                if (root.name.Contains(objName))
                {
                    Debug.Log("Transferring: " + root.name);
                    SceneManager.MoveGameObjectToScene(root, houseScene);
                    transferred++;
                    break;
                }
            }
        }

        if (transferred == 0)
        {
            Debug.LogWarning("No digging objects found to transfer. Check console for list of available objects.");
        }
        else
        {
            // Save
            EditorSceneManager.MarkSceneDirty(houseScene);
            EditorSceneManager.SaveScene(houseScene);
            Debug.Log($"=== Transferred {transferred} digging object(s) ===");
        }
    }
}
