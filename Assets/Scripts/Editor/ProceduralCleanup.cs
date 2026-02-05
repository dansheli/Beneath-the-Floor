using UnityEngine;
using UnityEditor;
using UnityEditor.SceneManagement;
using System.Collections.Generic;
using System.IO;

namespace BeneathTheFloor.Editor
{
    /// <summary>
    /// Cleans up scene objects that are duplicates of procedurally generated content.
    /// Run via menu: Tools > Beneath The Floor > Procedural Cleanup
    /// </summary>
    public class ProceduralCleanup : EditorWindow
    {
        private static List<string> cleanupLog = new List<string>();
        private static List<GameObject> objectsToDelete = new List<GameObject>();
        private static List<GameObject> objectsToKeep = new List<GameObject>();
        private Vector2 scrollPosition;

        [MenuItem("Tools/Beneath The Floor/Procedural Cleanup - Analyze")]
        public static void AnalyzeScene()
        {
            AnalyzeSceneInternal(true);
        }

        private static void AnalyzeSceneInternal(bool showWindow)
        {
            cleanupLog.Clear();
            objectsToDelete.Clear();
            objectsToKeep.Clear();

            Log("=== PROCEDURAL CLEANUP ANALYSIS ===");
            Log($"Scene: {UnityEngine.SceneManagement.SceneManager.GetActiveScene().name}");
            Log($"Time: {System.DateTime.Now}");
            Log("");

            // Verify backup exists
            VerifyBackup();

            // Analyze procedural systems
            AnalyzeMachineSetup();
            AnalyzeDiggingV2();
            AnalyzeBasementStructure();

            // Generate summary
            GenerateSummary();

            // Show window only if requested
            if (showWindow)
            {
                var window = GetWindow<ProceduralCleanup>("Procedural Cleanup");
                window.Show();
            }

            // Also log to Unity console
            foreach (var line in cleanupLog)
            {
                if (line.Contains("DELETE") || line.Contains("ERROR"))
                    Debug.LogWarning(line);
                else if (line.Contains("KEEP"))
                    Debug.Log(line);
                else
                    Debug.Log(line);
            }
        }

        [MenuItem("Tools/Beneath The Floor/Procedural Cleanup - Execute")]
        public static void ExecuteCleanup()
        {
            ExecuteCleanupInternal(true);
        }

        [MenuItem("Tools/Beneath The Floor/Procedural Cleanup - Execute (No Confirm)")]
        public static void ExecuteCleanupNoConfirm()
        {
            ExecuteCleanupInternal(false);
        }

        private static void ExecuteCleanupInternal(bool requireConfirm)
        {
            // First run analysis (don't show window)
            AnalyzeSceneInternal(false);

            if (objectsToDelete.Count == 0)
            {
                Debug.Log("[ProceduralCleanup] No objects to delete.");
                return;
            }

            if (requireConfirm)
            {
                // Confirm with user
                bool confirm = EditorUtility.DisplayDialog(
                    "Procedural Cleanup",
                    $"This will delete {objectsToDelete.Count} objects that are duplicates of procedurally generated content.\n\n" +
                    "Make sure you have a backup!\n\n" +
                    "Continue?",
                    "Delete Objects",
                    "Cancel"
                );

                if (!confirm)
                {
                    Debug.Log("[ProceduralCleanup] Cleanup cancelled by user.");
                    return;
                }
            }
            else
            {
                Debug.Log($"[ProceduralCleanup] Auto-executing cleanup of {objectsToDelete.Count} objects (no confirmation)...");
            }

            // Register undo
            Undo.SetCurrentGroupName("Procedural Cleanup");
            int undoGroup = Undo.GetCurrentGroup();

            int deletedCount = 0;
            foreach (var obj in objectsToDelete)
            {
                if (obj != null)
                {
                    string objName = obj.name;
                    Undo.DestroyObjectImmediate(obj);
                    deletedCount++;
                    Debug.Log($"[ProceduralCleanup] Deleted: {objName}");
                }
            }

            Undo.CollapseUndoOperations(undoGroup);

            // Mark scene dirty
            EditorSceneManager.MarkSceneDirty(UnityEngine.SceneManagement.SceneManager.GetActiveScene());

            Debug.Log($"[ProceduralCleanup] Cleanup complete! Deleted {deletedCount} objects.");
            Debug.Log("[ProceduralCleanup] Use Edit > Undo to restore if needed.");

            // Re-analyze to show updated state (don't show window to avoid GUI issues)
            AnalyzeSceneInternal(false);
        }

        private static void VerifyBackup()
        {
            string backupPath = "Assets/_Backup_Before_ProceduralCleanup/BasementScene_PreProceduralCleanup.unity";
            if (File.Exists(Application.dataPath.Replace("Assets", "") + backupPath))
            {
                Log("[BACKUP] Backup scene found at: " + backupPath);
            }
            else
            {
                Log("[WARNING] Backup scene NOT found! Please run backup first.");
            }
            Log("");
        }

        private static void AnalyzeMachineSetup()
        {
            Log("=== MACHINE SETUP ANALYSIS ===");
            Log("BasementMachineSetup.cs creates machines at runtime with setupOnStart=true.");
            Log("It first REMOVES old machines, then creates new ones from scratch.");
            Log("");

            // Objects that BasementMachineSetup creates (and first removes)
            // NOTE: Workbench removed - using UpgradeStation as single upgrade point
            string[] machineNames = {
                "Refinery",
                "UpgradeStation",
                "EnergyGenerator",
                "TradeTerminal"
            };

            foreach (string name in machineNames)
            {
                GameObject obj = GameObject.Find(name);
                if (obj != null)
                {
                    Log($"[DELETE] {name} - Created by BasementMachineSetup at runtime");
                    objectsToDelete.Add(obj);
                }
            }

            // Managers that BasementMachineSetup creates if missing
            string[] managerNames = {
                "EnergyManager",
                "CraftingManager",
                "CurrencyManager",
                "AssetMachinePrefabBuilder"
            };

            foreach (string name in managerNames)
            {
                GameObject obj = GameObject.Find(name);
                if (obj != null)
                {
                    // Check if it's a standalone object or part of another system
                    if (obj.transform.parent == null ||
                        (obj.transform.parent != null && obj.transform.parent.name != "GameManager"))
                    {
                        Log($"[DELETE] {name} - Created by BasementMachineSetup at runtime");
                        objectsToDelete.Add(obj);
                    }
                    else
                    {
                        Log($"[KEEP] {name} - Part of GameManager hierarchy");
                        objectsToKeep.Add(obj);
                    }
                }
            }

            // UI objects created by BasementMachineSetup
            // NOTE: WorkbenchUI and WorkbenchPanel removed - Workbench system deprecated
            string[] uiNames = {
                "MachineUICanvas",
                "RefineryUI",
                "UpgradeStationUI",
                "EnergyUI",
                "TradeTerminalUI",
                "RefineryPanel",
                "UpgradeStationPanel"
            };

            foreach (string name in uiNames)
            {
                GameObject obj = GameObject.Find(name);
                if (obj != null)
                {
                    Log($"[DELETE] {name} - Created by BasementMachineSetup at runtime");
                    objectsToDelete.Add(obj);
                }
            }

            Log("");
        }

        private static void AnalyzeDiggingV2()
        {
            Log("=== DIGGINGV2 ANALYSIS ===");
            Log("DiggingV2RuntimeSetup.cs auto-creates terrain at runtime via [RuntimeInitializeOnLoadMethod].");
            Log("");

            // Objects created by DiggingV2
            string[] diggingObjects = {
                "UndergroundTerrain",
                "TerrainChunk",
                "DiggingManager",
                "DigInventoryBridge",
                "DigFeedbackController"
            };

            foreach (string name in diggingObjects)
            {
                GameObject obj = GameObject.Find(name);
                if (obj != null)
                {
                    Log($"[DELETE] {name} - Created by DiggingV2RuntimeSetup at runtime");
                    objectsToDelete.Add(obj);
                }
            }

            // Floor ring created by BasementDigOpeningSetup
            string[] floorRingObjects = {
                "BasementFloor_Ring",
                "Floor_North",
                "Floor_South",
                "Floor_East",
                "Floor_West"
            };

            foreach (string name in floorRingObjects)
            {
                GameObject obj = GameObject.Find(name);
                if (obj != null)
                {
                    Log($"[DELETE] {name} - Created by BasementDigOpeningSetup at runtime");
                    objectsToDelete.Add(obj);
                }
            }

            // Legacy dig objects (old system)
            string[] legacyDigObjects = {
                "DigArea",
                "DigSurface",
                "DigSurfaceVisual",
                "DigShaftRoot"
            };

            foreach (string name in legacyDigObjects)
            {
                GameObject obj = GameObject.Find(name);
                if (obj != null)
                {
                    Log($"[DELETE] {name} - Legacy digging system object (DiggingV2 replaces this)");
                    objectsToDelete.Add(obj);
                }
            }

            Log("");
        }

        private static void AnalyzeBasementStructure()
        {
            Log("=== BASEMENT STRUCTURE ANALYSIS ===");
            Log("BasementResizer.cs MODIFIES existing objects - does not create them.");
            Log("These objects are authoritative and should be KEPT:");
            Log("");

            // Objects that must be kept (modified by BasementResizer, not created)
            string[] keepObjects = {
                "Basement",
                "BasementFloor",
                "Basement_Wall_North",
                "Basement_Wall_South",
                "Basement_Wall_East",
                "Basement_Wall_West",
                "BasementCeiling",
                "BasementLight_1",
                "BasementLight_2",
                "BasementLight_Stairs",
                "StairsUp",
                "OldMachinery",
                "BasementSpawnPoint",
                "Player",
                "GameManager",
                "HUDCanvas",
                "EventSystem",
                "MachineSetup",
                "BasementMachineSetup",
                "DepthLightingController"
            };

            foreach (string name in keepObjects)
            {
                GameObject obj = GameObject.Find(name);
                if (obj != null)
                {
                    // Make sure it's not in delete list
                    if (!objectsToDelete.Contains(obj))
                    {
                        Log($"[KEEP] {name} - Authoritative scene object");
                        objectsToKeep.Add(obj);
                    }
                }
            }

            // Check for step objects (StairsUp children)
            for (int i = 0; i <= 9; i++)
            {
                GameObject step = GameObject.Find($"Step_{i}");
                if (step != null && !objectsToDelete.Contains(step))
                {
                    Log($"[KEEP] Step_{i} - Static geometry (stairs)");
                    objectsToKeep.Add(step);
                }
            }

            // Check for Machines parent (container)
            GameObject machines = GameObject.Find("Machines");
            if (machines != null)
            {
                // If it's just a container, keep it
                if (machines.transform.childCount == 0 ||
                    machines.GetComponent<MonoBehaviour>() != null)
                {
                    Log($"[KEEP] Machines - Container object");
                    objectsToKeep.Add(machines);
                }
            }

            Log("");
        }

        private static void GenerateSummary()
        {
            Log("=== SUMMARY ===");
            Log($"Objects to DELETE: {objectsToDelete.Count}");
            Log($"Objects to KEEP: {objectsToKeep.Count}");
            Log("");

            if (objectsToDelete.Count > 0)
            {
                Log("DELETE LIST:");
                foreach (var obj in objectsToDelete)
                {
                    if (obj != null)
                        Log($"  - {obj.name}");
                }
            }
            else
            {
                Log("No duplicate objects found - scene is already clean!");
            }

            Log("");
            Log("=== PROCEDURAL OWNERSHIP MAPPING ===");
            Log("DiggingV2RuntimeSetup.cs -> UndergroundTerrain, TerrainChunk");
            Log("BasementDigOpeningSetup.cs -> BasementFloor_Ring, Floor_North/South/East/West");
            Log("BasementMachineSetup.cs -> Refinery, UpgradeStation, EnergyGenerator, TradeTerminal");
            Log("BasementMachineSetup.cs -> EnergyManager, CraftingManager, CurrencyManager (if not in GameManager)");
            Log("BasementMachineSetup.cs -> MachineUICanvas, *UI panels (Workbench removed)");
            Log("");
            Log("=== END ANALYSIS ===");
        }

        private static void Log(string message)
        {
            cleanupLog.Add(message);
        }

        private void OnGUI()
        {
            EditorGUILayout.LabelField("Procedural Cleanup Analysis", EditorStyles.boldLabel);
            EditorGUILayout.Space();

            scrollPosition = EditorGUILayout.BeginScrollView(scrollPosition);

            foreach (var line in cleanupLog)
            {
                if (line.Contains("[DELETE]"))
                {
                    GUI.color = new Color(1f, 0.6f, 0.6f);
                }
                else if (line.Contains("[KEEP]"))
                {
                    GUI.color = new Color(0.6f, 1f, 0.6f);
                }
                else if (line.Contains("[WARNING]") || line.Contains("[ERROR]"))
                {
                    GUI.color = Color.yellow;
                }
                else if (line.Contains("==="))
                {
                    GUI.color = Color.cyan;
                }
                else
                {
                    GUI.color = Color.white;
                }

                EditorGUILayout.LabelField(line);
            }

            GUI.color = Color.white;
            EditorGUILayout.EndScrollView();

            EditorGUILayout.Space();

            EditorGUILayout.BeginHorizontal();
            if (GUILayout.Button("Re-Analyze", GUILayout.Height(30)))
            {
                AnalyzeScene();
            }
            if (objectsToDelete.Count > 0)
            {
                GUI.backgroundColor = new Color(1f, 0.5f, 0.5f);
                if (GUILayout.Button($"Execute Cleanup ({objectsToDelete.Count} objects)", GUILayout.Height(30)))
                {
                    ExecuteCleanup();
                }
                GUI.backgroundColor = Color.white;
            }
            EditorGUILayout.EndHorizontal();
        }
    }
}
