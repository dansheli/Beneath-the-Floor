using UnityEngine;
using UnityEditor;
using System.Linq;
using BeneathTheFloor.Digging;

namespace BeneathTheFloor.Editor
{
    /// <summary>
    /// Editor utility for validating DigInventoryBridge mappings.
    /// Provides menu items to quickly check mapping configuration.
    /// </summary>
    public static class DigInventoryBridgeValidator
    {
        private const string MenuPath = "Tools/Beneath The Floor/Validate/";

        [MenuItem(MenuPath + "DigInventoryBridge Mappings")]
        public static void ValidateMappings()
        {
            var bridge = FindDigInventoryBridge();
            if (bridge == null)
            {
                Debug.LogError("[DigInventoryBridgeValidator] No DigInventoryBridge found in scene or assets!");
                EditorUtility.DisplayDialog(
                    "Validation Failed",
                    "No DigInventoryBridge component found in the current scene.\n\n" +
                    "Make sure you have a GameObject with DigInventoryBridge attached.",
                    "OK"
                );
                return;
            }

            var report = bridge.ValidateMappings();
            ShowValidationDialog(report);
        }

        [MenuItem(MenuPath + "DigInventoryBridge Mappings", true)]
        public static bool ValidateMappings_Validate()
        {
            // Always enabled in editor
            return true;
        }

        [MenuItem(MenuPath + "Initialize All Mappings (Empty)")]
        public static void InitializeAllMappings()
        {
            var bridge = FindDigInventoryBridge();
            if (bridge == null)
            {
                Debug.LogError("[DigInventoryBridgeValidator] No DigInventoryBridge found in scene!");
                EditorUtility.DisplayDialog(
                    "Initialization Failed",
                    "No DigInventoryBridge component found in the current scene.\n\n" +
                    "Make sure you have a GameObject with DigInventoryBridge attached.",
                    "OK"
                );
                return;
            }

            bool confirm = EditorUtility.DisplayDialog(
                "Initialize Mappings",
                "This will clear all existing mappings and create empty entries for all UndergroundResourceType values.\n\n" +
                "Are you sure you want to continue?",
                "Yes, Initialize",
                "Cancel"
            );

            if (confirm)
            {
                // Use reflection to call the private context menu method
                var method = typeof(DigInventoryBridge).GetMethod(
                    "InitializeAllMappingsEmpty",
                    System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance
                );

                if (method != null)
                {
                    method.Invoke(bridge, null);
                    EditorUtility.SetDirty(bridge);

                    // Validate after initialization
                    var report = bridge.ValidateMappings();
                    EditorUtility.DisplayDialog(
                        "Initialization Complete",
                        $"Created {report.mappedTypeCount} empty mappings.\n\n" +
                        "Now assign ItemSO references in the Inspector for each mapping.",
                        "OK"
                    );
                }
            }
        }

        [MenuItem(MenuPath + "Select DigInventoryBridge")]
        public static void SelectBridge()
        {
            var bridge = FindDigInventoryBridge();
            if (bridge != null)
            {
                Selection.activeGameObject = bridge.gameObject;
                EditorGUIUtility.PingObject(bridge);
                Debug.Log($"[DigInventoryBridgeValidator] Selected '{bridge.gameObject.name}' in Hierarchy.");
            }
            else
            {
                Debug.LogWarning("[DigInventoryBridgeValidator] No DigInventoryBridge found in scene!");
                EditorUtility.DisplayDialog(
                    "Not Found",
                    "No DigInventoryBridge component found in the current scene.",
                    "OK"
                );
            }
        }

        [MenuItem(MenuPath + "Quick Report (Console Only)")]
        public static void QuickReport()
        {
            var bridge = FindDigInventoryBridge();
            if (bridge == null)
            {
                Debug.LogError("[DigInventoryBridgeValidator] No DigInventoryBridge found in scene!");
                return;
            }

            // Just run validation which logs to console
            bridge.ValidateMappings();
        }

        /// <summary>
        /// Find the DigInventoryBridge in the scene or prefabs.
        /// </summary>
        private static DigInventoryBridge FindDigInventoryBridge()
        {
            // First, check if there's a singleton instance
            if (DigInventoryBridge.Instance != null)
            {
                return DigInventoryBridge.Instance;
            }

            // Try to find in scene
            var bridge = Object.FindObjectOfType<DigInventoryBridge>();
            if (bridge != null)
            {
                return bridge;
            }

            // Try to find in all loaded objects (including inactive)
            var allBridges = Resources.FindObjectsOfTypeAll<DigInventoryBridge>();
            if (allBridges.Length > 0)
            {
                // Filter out prefabs in project (we want scene objects)
                var sceneBridge = allBridges.FirstOrDefault(b =>
                    !EditorUtility.IsPersistent(b) &&
                    b.gameObject.scene.IsValid()
                );

                if (sceneBridge != null)
                {
                    return sceneBridge;
                }

                // If no scene bridge, return any found
                return allBridges[0];
            }

            return null;
        }

        /// <summary>
        /// Show a dialog with validation results.
        /// </summary>
        private static void ShowValidationDialog(MappingValidationReport report)
        {
            string status;
            if (report.IsFullyConfigured)
            {
                status = "All mappings are valid and complete!";
            }
            else
            {
                status = "Issues found - see Console for details.";
            }

            string message = $"Validation Results:\n\n" +
                $"Total Resource Types: {report.totalTypeCount}\n" +
                $"Mapped Types: {report.mappedTypeCount}\n" +
                $"Valid Mappings: {report.validMappingCount}\n\n" +
                $"Missing Mappings: {report.missingTypes.Count}\n" +
                $"Null ItemSO: {report.nullItemTypes.Count}\n" +
                $"Duplicates: {report.duplicateTypes.Count}\n\n" +
                status;

            EditorUtility.DisplayDialog(
                report.IsFullyConfigured ? "Validation Passed" : "Validation Issues",
                message,
                "OK"
            );
        }
    }

    /// <summary>
    /// Custom inspector enhancement for DigInventoryBridge.
    /// Adds validation buttons directly to the component.
    /// </summary>
    [CustomEditor(typeof(DigInventoryBridge))]
    public class DigInventoryBridgeEditor : UnityEditor.Editor
    {
        private bool _showValidationFoldout = true;

        public override void OnInspectorGUI()
        {
            // Draw default inspector
            DrawDefaultInspector();

            var bridge = (DigInventoryBridge)target;

            EditorGUILayout.Space(10);

            // Validation section
            _showValidationFoldout = EditorGUILayout.Foldout(_showValidationFoldout, "Validation Tools", true, EditorStyles.foldoutHeader);

            if (_showValidationFoldout)
            {
                EditorGUILayout.BeginVertical(EditorStyles.helpBox);

                // Quick stats
                var mappings = bridge.GetAllMappings();
                int totalTypes = System.Enum.GetValues(typeof(UndergroundResourceType)).Length - 1; // Exclude None
                int mappedCount = mappings.Count;
                int validCount = mappings.Count(m => m.item != null);

                EditorGUILayout.LabelField($"Mapped: {mappedCount}/{totalTypes}", EditorStyles.miniLabel);
                EditorGUILayout.LabelField($"Valid (with ItemSO): {validCount}/{mappedCount}", EditorStyles.miniLabel);

                // Progress bar
                float progress = (float)validCount / Mathf.Max(1, totalTypes);
                Rect rect = EditorGUILayout.GetControlRect(false, 18);
                EditorGUI.ProgressBar(rect, progress, $"{Mathf.RoundToInt(progress * 100)}% Complete");

                EditorGUILayout.Space(5);

                // Buttons
                EditorGUILayout.BeginHorizontal();

                if (GUILayout.Button("Validate Mappings", GUILayout.Height(25)))
                {
                    bridge.ValidateMappings();
                }

                if (GUILayout.Button("Log Current", GUILayout.Height(25)))
                {
                    // Invoke context menu method via reflection
                    var method = typeof(DigInventoryBridge).GetMethod(
                        "LogCurrentMappings",
                        System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance
                    );
                    method?.Invoke(bridge, null);
                }

                EditorGUILayout.EndHorizontal();

                // Warning if issues detected
                if (validCount < totalTypes)
                {
                    EditorGUILayout.Space(5);
                    EditorGUILayout.HelpBox(
                        $"{totalTypes - validCount} resource types are missing valid ItemSO mappings. " +
                        "Digging these resources will not award items to inventory.",
                        MessageType.Warning
                    );
                }

                EditorGUILayout.EndVertical();
            }
        }
    }
}
