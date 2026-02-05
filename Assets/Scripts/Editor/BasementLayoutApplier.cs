using UnityEngine;
using UnityEditor;
using UnityEditor.SceneManagement;
using System.IO;
using BeneathTheFloor.Environment;

namespace BeneathTheFloor.Editor
{
    /// <summary>
    /// Editor tool for capturing and applying basement layout snapshots.
    /// - Capture: Works in Play Mode to capture runtime layout
    /// - Apply: Works in Edit Mode to bake the layout into the scene
    /// </summary>
    public class BasementLayoutApplier : EditorWindow
    {
        private static BasementLayoutSnapshot currentSnapshot;
        private static string snapshotPath = "Assets/_BasementSnapshots/BasementLayoutSnapshot_PlayMode.asset";
        private static string snapshotFolder = "Assets/_BasementSnapshots";

        private Vector2 scrollPosition;

        [MenuItem("Tools/Beneath The Floor/Basement/Open Layout Tool Window")]
        public static void ShowWindow()
        {
            var window = GetWindow<BasementLayoutApplier>("Basement Layout");
            window.minSize = new Vector2(400, 500);
            window.Show();
        }

        // =====================================================
        // CAPTURE (Play Mode)
        // =====================================================

        [MenuItem("Tools/Beneath The Floor/Basement/Capture Runtime Layout (Play Mode)")]
        public static void CaptureRuntimeLayout()
        {
            if (!Application.isPlaying)
            {
                EditorUtility.DisplayDialog(
                    "Not in Play Mode",
                    "You must be in Play Mode to capture the runtime layout.\n\n" +
                    "Please enter Play Mode first, then run this command again.",
                    "OK"
                );
                return;
            }

            Debug.Log("[BasementLayoutApplier] ========== CAPTURING RUNTIME LAYOUT ==========");

            // Ensure folder exists
            EnsureSnapshotFolder();

            // Create or load existing snapshot asset
            currentSnapshot = AssetDatabase.LoadAssetAtPath<BasementLayoutSnapshot>(snapshotPath);

            if (currentSnapshot == null)
            {
                currentSnapshot = ScriptableObject.CreateInstance<BasementLayoutSnapshot>();
                AssetDatabase.CreateAsset(currentSnapshot, snapshotPath);
                Debug.Log($"[BasementLayoutApplier] Created new snapshot asset at: {snapshotPath}");
            }

            // Capture the current layout
            currentSnapshot.CaptureCurrentLayout();
            currentSnapshot.notes = "Captured from Play Mode runtime layout";

            // Save the asset
            EditorUtility.SetDirty(currentSnapshot);
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();

            Debug.Log("[BasementLayoutApplier] ========== CAPTURE COMPLETE ==========");
            Debug.Log($"[BasementLayoutApplier] Snapshot saved to: {snapshotPath}");
            Debug.Log("[BasementLayoutApplier] Exit Play Mode and use 'Apply Runtime Snapshot' to bake into scene.");

            EditorUtility.DisplayDialog(
                "Capture Complete",
                $"Runtime layout captured successfully!\n\n" +
                $"Snapshot saved to:\n{snapshotPath}\n\n" +
                "Next steps:\n" +
                "1. Exit Play Mode\n" +
                "2. Use Tools > Beneath The Floor > Basement > Apply Runtime Snapshot\n" +
                "3. Save the scene",
                "OK"
            );
        }

        // =====================================================
        // APPLY (Edit Mode)
        // =====================================================

        [MenuItem("Tools/Beneath The Floor/Basement/Apply Runtime Snapshot (Edit Mode)")]
        public static void ApplyRuntimeSnapshot()
        {
            if (Application.isPlaying)
            {
                EditorUtility.DisplayDialog(
                    "Still in Play Mode",
                    "You must exit Play Mode to apply the snapshot.\n\n" +
                    "Please stop Play Mode first, then run this command again.",
                    "OK"
                );
                return;
            }

            // Load the snapshot
            currentSnapshot = AssetDatabase.LoadAssetAtPath<BasementLayoutSnapshot>(snapshotPath);

            if (currentSnapshot == null)
            {
                EditorUtility.DisplayDialog(
                    "No Snapshot Found",
                    $"No snapshot found at:\n{snapshotPath}\n\n" +
                    "Please capture a runtime layout first (in Play Mode).",
                    "OK"
                );
                return;
            }

            // Confirm with user
            bool confirm = EditorUtility.DisplayDialog(
                "Apply Runtime Snapshot",
                $"This will apply the layout captured at:\n{currentSnapshot.captureTime}\n\n" +
                "Scene objects will be moved/resized to match the snapshot.\n\n" +
                "Continue?",
                "Apply Snapshot",
                "Cancel"
            );

            if (!confirm)
            {
                Debug.Log("[BasementLayoutApplier] Apply cancelled by user.");
                return;
            }

            Debug.Log("[BasementLayoutApplier] ========== APPLYING SNAPSHOT ==========");
            Debug.Log($"[BasementLayoutApplier] Snapshot from: {currentSnapshot.captureTime}");

            // Register undo
            Undo.SetCurrentGroupName("Apply Basement Layout Snapshot");
            int undoGroup = Undo.GetCurrentGroup();

            int updatedCount = 0;
            int missingCount = 0;

            // Apply main structure
            updatedCount += ApplySnapshot(currentSnapshot.basementFloor, ref missingCount);
            updatedCount += ApplySnapshot(currentSnapshot.basementCeiling, ref missingCount);

            // Apply walls
            updatedCount += ApplySnapshot(currentSnapshot.wallNorth, ref missingCount);
            updatedCount += ApplySnapshot(currentSnapshot.wallSouth, ref missingCount);
            updatedCount += ApplySnapshot(currentSnapshot.wallEast, ref missingCount);
            updatedCount += ApplySnapshot(currentSnapshot.wallWest, ref missingCount);

            // Apply floor ring
            updatedCount += ApplySnapshot(currentSnapshot.floorRingRoot, ref missingCount);
            foreach (var segment in currentSnapshot.floorRingSegments)
            {
                updatedCount += ApplySnapshot(segment, ref missingCount);
            }

            // Apply stairs
            updatedCount += ApplySnapshot(currentSnapshot.stairsUp, ref missingCount);
            foreach (var step in currentSnapshot.stairSteps)
            {
                updatedCount += ApplySnapshot(step, ref missingCount);
            }

            // Apply machines
            foreach (var machine in currentSnapshot.machines)
            {
                updatedCount += ApplySnapshot(machine, ref missingCount);
            }

            // Apply lights
            foreach (var light in currentSnapshot.lights)
            {
                updatedCount += ApplySnapshot(light, ref missingCount);
            }

            Undo.CollapseUndoOperations(undoGroup);

            // Mark scene dirty
            EditorSceneManager.MarkSceneDirty(UnityEngine.SceneManagement.SceneManager.GetActiveScene());

            Debug.Log("[BasementLayoutApplier] ========== APPLY COMPLETE ==========");
            Debug.Log($"[BasementLayoutApplier] Objects updated: {updatedCount}");
            Debug.Log($"[BasementLayoutApplier] Objects missing: {missingCount}");
            Debug.Log("[BasementLayoutApplier] Remember to SAVE THE SCENE (Ctrl+S)!");

            EditorUtility.DisplayDialog(
                "Apply Complete",
                $"Snapshot applied successfully!\n\n" +
                $"Objects updated: {updatedCount}\n" +
                $"Objects missing: {missingCount}\n\n" +
                "IMPORTANT: Save the scene (Ctrl+S) to make changes permanent!",
                "OK"
            );
        }

        private static int ApplySnapshot(ObjectSnapshot snapshot, ref int missingCount)
        {
            if (snapshot == null || string.IsNullOrEmpty(snapshot.objectName))
                return 0;

            // Find the object by hierarchy path first, then by name
            GameObject obj = FindObjectByPath(snapshot.hierarchyPath);

            if (obj == null)
            {
                obj = GameObject.Find(snapshot.objectName);
            }

            if (obj != null)
            {
                Undo.RecordObject(obj.transform, $"Apply snapshot to {snapshot.objectName}");

                snapshot.transform.ApplyTo(obj.transform);
                obj.SetActive(snapshot.isActive);

                Debug.Log($"[BasementLayoutApplier] Applied: {snapshot.objectName} -> {snapshot.transform}");
                return 1;
            }
            else
            {
                Debug.LogWarning($"[BasementLayoutApplier] MISSING: {snapshot.objectName} (path: {snapshot.hierarchyPath})");
                missingCount++;
                return 0;
            }
        }

        private static GameObject FindObjectByPath(string path)
        {
            if (string.IsNullOrEmpty(path))
                return null;

            // Try direct find first
            GameObject obj = GameObject.Find(path);
            if (obj != null)
                return obj;

            // Parse path and navigate
            string[] parts = path.Split('/');
            if (parts.Length == 0)
                return null;

            // Find root
            GameObject root = GameObject.Find(parts[0]);
            if (root == null)
                return null;

            if (parts.Length == 1)
                return root;

            // Navigate to child
            Transform current = root.transform;
            for (int i = 1; i < parts.Length; i++)
            {
                Transform child = current.Find(parts[i]);
                if (child == null)
                    return null;
                current = child;
            }

            return current.gameObject;
        }

        // =====================================================
        // UTILITY
        // =====================================================

        private static void EnsureSnapshotFolder()
        {
            if (!AssetDatabase.IsValidFolder(snapshotFolder))
            {
                string parentFolder = Path.GetDirectoryName(snapshotFolder).Replace("\\", "/");
                string folderName = Path.GetFileName(snapshotFolder);

                if (string.IsNullOrEmpty(parentFolder))
                    parentFolder = "Assets";

                AssetDatabase.CreateFolder(parentFolder, folderName);
                Debug.Log($"[BasementLayoutApplier] Created folder: {snapshotFolder}");
            }
        }

        [MenuItem("Tools/Beneath The Floor/Basement/Disable Runtime Resize")]
        public static void DisableRuntimeResize()
        {
            // Find BasementResizer in scene
            BasementResizer resizer = Object.FindObjectOfType<BasementResizer>();

            if (resizer == null)
            {
                // Try to find by component on Basement object
                GameObject basement = GameObject.Find("Basement");
                if (basement != null)
                {
                    resizer = basement.GetComponent<BasementResizer>();
                }
            }

            if (resizer != null)
            {
                Undo.RecordObject(resizer, "Disable Runtime Resize");

                // Use reflection to set the field since it's serialized
                var field = typeof(BasementResizer).GetField("disableRuntimeResize",
                    System.Reflection.BindingFlags.Public |
                    System.Reflection.BindingFlags.NonPublic |
                    System.Reflection.BindingFlags.Instance);

                if (field != null)
                {
                    field.SetValue(resizer, true);
                    EditorUtility.SetDirty(resizer);
                    Debug.Log("[BasementLayoutApplier] disableRuntimeResize set to TRUE on BasementResizer");
                }
                else
                {
                    Debug.LogWarning("[BasementLayoutApplier] Could not find disableRuntimeResize field. Please set it manually in the Inspector.");
                }

                EditorSceneManager.MarkSceneDirty(UnityEngine.SceneManagement.SceneManager.GetActiveScene());

                EditorUtility.DisplayDialog(
                    "Runtime Resize Disabled",
                    "BasementResizer.disableRuntimeResize is now TRUE.\n\n" +
                    "The basement will no longer be resized at runtime.\n\n" +
                    "Remember to SAVE THE SCENE!",
                    "OK"
                );
            }
            else
            {
                EditorUtility.DisplayDialog(
                    "BasementResizer Not Found",
                    "Could not find BasementResizer component in the scene.\n\n" +
                    "Please manually disable runtime resize if needed.",
                    "OK"
                );
            }
        }

        [MenuItem("Tools/Beneath The Floor/Basement/View Current Snapshot")]
        public static void ViewCurrentSnapshot()
        {
            currentSnapshot = AssetDatabase.LoadAssetAtPath<BasementLayoutSnapshot>(snapshotPath);

            if (currentSnapshot != null)
            {
                Selection.activeObject = currentSnapshot;
                EditorGUIUtility.PingObject(currentSnapshot);
                Debug.Log("[BasementLayoutApplier] Current snapshot:\n" + currentSnapshot.GetSummary());
            }
            else
            {
                EditorUtility.DisplayDialog(
                    "No Snapshot Found",
                    $"No snapshot exists at:\n{snapshotPath}",
                    "OK"
                );
            }
        }

        // =====================================================
        // EDITOR WINDOW GUI
        // =====================================================

        private void OnGUI()
        {
            EditorGUILayout.LabelField("Basement Layout Tool", EditorStyles.boldLabel);
            EditorGUILayout.Space();

            // Status
            bool isPlaying = Application.isPlaying;
            EditorGUILayout.BeginHorizontal();
            EditorGUILayout.LabelField("Mode:", GUILayout.Width(50));
            GUI.color = isPlaying ? Color.green : Color.cyan;
            EditorGUILayout.LabelField(isPlaying ? "PLAY MODE" : "EDIT MODE", EditorStyles.boldLabel);
            GUI.color = Color.white;
            EditorGUILayout.EndHorizontal();

            EditorGUILayout.Space();

            // Load current snapshot for display
            currentSnapshot = AssetDatabase.LoadAssetAtPath<BasementLayoutSnapshot>(snapshotPath);

            if (currentSnapshot != null)
            {
                EditorGUILayout.LabelField("Current Snapshot", EditorStyles.boldLabel);
                EditorGUILayout.LabelField($"Captured: {currentSnapshot.captureTime}");
                EditorGUILayout.LabelField($"Scene: {currentSnapshot.sceneName}");
                EditorGUILayout.LabelField($"Floor Ring Segments: {currentSnapshot.floorRingSegments.Count}");
                EditorGUILayout.LabelField($"Machines: {currentSnapshot.machines.Count}");

                if (GUILayout.Button("Select Snapshot Asset"))
                {
                    Selection.activeObject = currentSnapshot;
                    EditorGUIUtility.PingObject(currentSnapshot);
                }
            }
            else
            {
                EditorGUILayout.HelpBox("No snapshot exists yet. Capture one in Play Mode.", MessageType.Info);
            }

            EditorGUILayout.Space();
            EditorGUILayout.LabelField("", GUI.skin.horizontalSlider);
            EditorGUILayout.Space();

            // Actions
            EditorGUILayout.LabelField("Actions", EditorStyles.boldLabel);

            if (isPlaying)
            {
                // Play Mode actions
                GUI.backgroundColor = Color.green;
                if (GUILayout.Button("Capture Runtime Layout", GUILayout.Height(40)))
                {
                    CaptureRuntimeLayout();
                }
                GUI.backgroundColor = Color.white;

                EditorGUILayout.HelpBox(
                    "You are in Play Mode.\n" +
                    "Click 'Capture Runtime Layout' to save the current basement state.\n" +
                    "Then exit Play Mode and apply the snapshot.",
                    MessageType.Info);
            }
            else
            {
                // Edit Mode actions
                GUI.enabled = currentSnapshot != null;
                GUI.backgroundColor = Color.cyan;
                if (GUILayout.Button("Apply Runtime Snapshot", GUILayout.Height(40)))
                {
                    ApplyRuntimeSnapshot();
                }
                GUI.backgroundColor = Color.white;
                GUI.enabled = true;

                EditorGUILayout.Space();

                GUI.backgroundColor = new Color(1f, 0.8f, 0.4f);
                if (GUILayout.Button("Disable Runtime Resize", GUILayout.Height(30)))
                {
                    DisableRuntimeResize();
                }
                GUI.backgroundColor = Color.white;

                EditorGUILayout.HelpBox(
                    "You are in Edit Mode.\n" +
                    "1. Apply the snapshot to bake the layout.\n" +
                    "2. Disable runtime resize to prevent future changes.\n" +
                    "3. Save the scene (Ctrl+S).",
                    MessageType.Info);
            }

            EditorGUILayout.Space();
            EditorGUILayout.LabelField("", GUI.skin.horizontalSlider);
            EditorGUILayout.Space();

            // Snapshot details
            if (currentSnapshot != null)
            {
                EditorGUILayout.LabelField("Snapshot Details", EditorStyles.boldLabel);

                scrollPosition = EditorGUILayout.BeginScrollView(scrollPosition, GUILayout.Height(200));

                if (currentSnapshot.basementFloor != null)
                    EditorGUILayout.LabelField($"Floor: {currentSnapshot.basementFloor.transform}");

                if (currentSnapshot.basementCeiling != null)
                    EditorGUILayout.LabelField($"Ceiling: {currentSnapshot.basementCeiling.transform}");

                EditorGUILayout.Space();
                EditorGUILayout.LabelField("Walls:", EditorStyles.boldLabel);
                if (currentSnapshot.wallNorth != null)
                    EditorGUILayout.LabelField($"  North: {currentSnapshot.wallNorth.transform}");
                if (currentSnapshot.wallSouth != null)
                    EditorGUILayout.LabelField($"  South: {currentSnapshot.wallSouth.transform}");
                if (currentSnapshot.wallEast != null)
                    EditorGUILayout.LabelField($"  East: {currentSnapshot.wallEast.transform}");
                if (currentSnapshot.wallWest != null)
                    EditorGUILayout.LabelField($"  West: {currentSnapshot.wallWest.transform}");

                EditorGUILayout.Space();
                EditorGUILayout.LabelField("Floor Ring:", EditorStyles.boldLabel);
                foreach (var seg in currentSnapshot.floorRingSegments)
                {
                    EditorGUILayout.LabelField($"  {seg.objectName}: {seg.transform}");
                }

                EditorGUILayout.Space();
                EditorGUILayout.LabelField("Machines:", EditorStyles.boldLabel);
                foreach (var machine in currentSnapshot.machines)
                {
                    EditorGUILayout.LabelField($"  {machine.objectName}: pos=({machine.transform.position.x:F1}, {machine.transform.position.y:F1}, {machine.transform.position.z:F1})");
                }

                EditorGUILayout.EndScrollView();
            }
        }

        private void OnInspectorUpdate()
        {
            // Repaint when play mode changes
            Repaint();
        }
    }
}
