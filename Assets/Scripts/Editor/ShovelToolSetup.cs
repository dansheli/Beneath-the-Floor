using UnityEngine;
using UnityEditor;

namespace BeneathTheFloor.Editor
{
    /// <summary>
    /// Editor tool to set up the new Shovel tool tiers in the scene.
    /// Removes old tools and places new ones with proper positioning.
    /// </summary>
    public class ShovelToolSetup : EditorWindow
    {
        private static readonly string[] NEW_PREFAB_PATHS = new string[]
        {
            "Assets/Art/Tools/Tool1/Base_tier1_tier2_tier3/Base_Shovel.prefab",
            "Assets/Art/Tools/Tool1/Base_tier1_tier2_tier3/Tier1_Shovel.prefab",
            "Assets/Art/Tools/Tool1/Base_tier1_tier2_tier3/Tier2_Shovel.prefab",
            "Assets/Art/Tools/Tool1/Base_tier1_tier2_tier3/Tier3_Shovel.prefab"
        };

        private static readonly string[] TIER_NAMES = new string[]
        {
            "Tool_Base (Base Shovel)",
            "Tool_Tier1 (Tier 1 Shovel)",
            "Tool_Tier2 (Tier 2 Shovel)",
            "Tool_Tier3 (Tier 3 Shovel)"
        };

        // Default transform values
        private Vector3 toolPosition = new Vector3(0.3f, -0.3f, 0.5f);
        private Vector3 toolRotation = new Vector3(0f, -90f, 0f);
        private float toolScale = 0.5f;

        // Which tier to show for preview
        private int previewTier = 0;

        // Reference to placed tools
        private GameObject[] placedTools = new GameObject[4];

        [MenuItem("Tools/Beneath The Floor/Setup Shovel Tools")]
        public static void ShowWindow()
        {
            var window = GetWindow<ShovelToolSetup>("Shovel Tool Setup");
            window.minSize = new Vector2(400, 500);
        }

        private void OnGUI()
        {
            GUILayout.Label("Shovel Tool Setup", EditorStyles.boldLabel);
            EditorGUILayout.Space(10);

            EditorGUILayout.HelpBox(
                "This tool will:\n" +
                "1. Remove old tool prefabs from under the Main Camera\n" +
                "2. Place the new Shovel tier prefabs\n" +
                "3. Let you adjust their position/rotation/scale",
                MessageType.Info);

            EditorGUILayout.Space(10);

            // Transform settings
            GUILayout.Label("Tool Transform Settings", EditorStyles.boldLabel);
            toolPosition = EditorGUILayout.Vector3Field("Position (Local)", toolPosition);
            toolRotation = EditorGUILayout.Vector3Field("Rotation", toolRotation);
            toolScale = EditorGUILayout.FloatField("Scale", toolScale);

            EditorGUILayout.Space(10);

            // Preview tier selection
            GUILayout.Label("Preview Tier", EditorStyles.boldLabel);
            previewTier = EditorGUILayout.Popup("Show Tier", previewTier, TIER_NAMES);

            EditorGUILayout.Space(10);

            // Action buttons
            GUILayout.Label("Actions", EditorStyles.boldLabel);

            EditorGUILayout.BeginHorizontal();
            if (GUILayout.Button("Remove Old Tools", GUILayout.Height(30)))
            {
                RemoveOldTools();
            }
            if (GUILayout.Button("Find Existing Tools", GUILayout.Height(30)))
            {
                FindExistingTools();
            }
            EditorGUILayout.EndHorizontal();

            EditorGUILayout.Space(5);

            GUI.backgroundColor = Color.green;
            if (GUILayout.Button("Place New Shovel Tools", GUILayout.Height(40)))
            {
                PlaceNewTools();
            }
            GUI.backgroundColor = Color.white;

            EditorGUILayout.Space(5);

            if (GUILayout.Button("Update Tool Transforms", GUILayout.Height(30)))
            {
                UpdateToolTransforms();
            }

            EditorGUILayout.Space(5);

            EditorGUILayout.BeginHorizontal();
            if (GUILayout.Button("Show Selected Tier Only", GUILayout.Height(30)))
            {
                ShowOnlyTier(previewTier);
            }
            if (GUILayout.Button("Hide All Tools", GUILayout.Height(30)))
            {
                HideAllTools();
            }
            EditorGUILayout.EndHorizontal();

            EditorGUILayout.Space(10);

            // Status section
            GUILayout.Label("Current Status", EditorStyles.boldLabel);
            DrawStatusSection();

            EditorGUILayout.Space(10);

            // Copy position from selected object
            GUILayout.Label("Copy Transform", EditorStyles.boldLabel);
            if (GUILayout.Button("Copy Position from Selected Object"))
            {
                CopyTransformFromSelection();
            }
        }

        private void DrawStatusSection()
        {
            Camera mainCam = Camera.main;
            if (mainCam == null)
            {
                EditorGUILayout.HelpBox("No Main Camera found in scene!", MessageType.Error);
                return;
            }

            EditorGUILayout.LabelField("Main Camera:", mainCam.name);

            // Check for old tools
            int oldToolCount = CountOldTools(mainCam.transform);
            if (oldToolCount > 0)
            {
                EditorGUILayout.HelpBox($"Found {oldToolCount} old tool(s) that should be removed.", MessageType.Warning);
            }

            // Check for new tools
            FindExistingToolsQuiet();
            for (int i = 0; i < 4; i++)
            {
                string status = placedTools[i] != null ? "Found" : "Missing";
                Color color = placedTools[i] != null ? Color.green : Color.red;

                EditorGUILayout.BeginHorizontal();
                GUI.color = color;
                EditorGUILayout.LabelField($"Tier {i + 1}:", status);
                GUI.color = Color.white;

                if (placedTools[i] != null)
                {
                    if (GUILayout.Button("Select", GUILayout.Width(60)))
                    {
                        Selection.activeGameObject = placedTools[i];
                    }
                }
                EditorGUILayout.EndHorizontal();
            }
        }

        private void RemoveOldTools()
        {
            Camera mainCam = Camera.main;
            if (mainCam == null)
            {
                EditorUtility.DisplayDialog("Error", "No Main Camera found!", "OK");
                return;
            }

            int removed = 0;
            var toRemove = new System.Collections.Generic.List<GameObject>();

            // Find old tools by name patterns
            foreach (Transform child in mainCam.transform)
            {
                string nameLower = child.name.ToLower();
                if (nameLower.Contains("tool_shovel_tier1") ||
                    nameLower.Contains("tool_hoe") ||
                    nameLower.Contains("tool_picaxe") ||
                    nameLower.Contains("tool_pickaxe") ||
                    nameLower.Contains("toolpack"))
                {
                    toRemove.Add(child.gameObject);
                }
            }

            foreach (var obj in toRemove)
            {
                Undo.DestroyObjectImmediate(obj);
                removed++;
            }

            Debug.Log($"[ShovelToolSetup] Removed {removed} old tool(s)");
            EditorUtility.DisplayDialog("Done", $"Removed {removed} old tool(s)", "OK");
        }

        private int CountOldTools(Transform parent)
        {
            int count = 0;
            foreach (Transform child in parent)
            {
                string nameLower = child.name.ToLower();
                if (nameLower.Contains("tool_shovel_tier1") ||
                    nameLower.Contains("tool_hoe") ||
                    nameLower.Contains("tool_picaxe") ||
                    nameLower.Contains("tool_pickaxe") ||
                    nameLower.Contains("toolpack"))
                {
                    count++;
                }
            }
            return count;
        }

        private void PlaceNewTools()
        {
            Camera mainCam = Camera.main;
            if (mainCam == null)
            {
                EditorUtility.DisplayDialog("Error", "No Main Camera found!", "OK");
                return;
            }

            int placed = 0;

            for (int i = 0; i < NEW_PREFAB_PATHS.Length; i++)
            {
                GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(NEW_PREFAB_PATHS[i]);
                if (prefab == null)
                {
                    Debug.LogError($"[ShovelToolSetup] Could not load prefab: {NEW_PREFAB_PATHS[i]}");
                    continue;
                }

                // Check if already exists
                string toolName = i == 0 ? "Tool_Base" : $"Tool_Tier{i}";
                Transform existing = mainCam.transform.Find(toolName);
                if (existing != null)
                {
                    Debug.Log($"[ShovelToolSetup] {toolName} already exists, skipping");
                    placedTools[i] = existing.gameObject;
                    continue;
                }

                // Instantiate
                GameObject instance = (GameObject)PrefabUtility.InstantiatePrefab(prefab, mainCam.transform);
                instance.name = toolName;
                instance.transform.localPosition = toolPosition;
                instance.transform.localRotation = Quaternion.Euler(toolRotation);
                instance.transform.localScale = Vector3.one * toolScale;

                // Only show the first tier (Base) by default
                instance.SetActive(i == 0);

                placedTools[i] = instance;
                Undo.RegisterCreatedObjectUndo(instance, "Place Shovel Tool");
                placed++;
            }

            Debug.Log($"[ShovelToolSetup] Placed {placed} new tool(s)");
            EditorUtility.DisplayDialog("Done", $"Placed {placed} new Shovel tool(s)\n\nOnly Tier 1 (Base) is visible.\nUse 'Show Selected Tier Only' to preview others.", "OK");
        }

        private void UpdateToolTransforms()
        {
            FindExistingToolsQuiet();

            int updated = 0;
            for (int i = 0; i < placedTools.Length; i++)
            {
                if (placedTools[i] != null)
                {
                    Undo.RecordObject(placedTools[i].transform, "Update Tool Transform");
                    placedTools[i].transform.localPosition = toolPosition;
                    placedTools[i].transform.localRotation = Quaternion.Euler(toolRotation);
                    placedTools[i].transform.localScale = Vector3.one * toolScale;
                    updated++;
                }
            }

            Debug.Log($"[ShovelToolSetup] Updated {updated} tool transform(s)");
        }

        private void ShowOnlyTier(int tier)
        {
            FindExistingToolsQuiet();

            for (int i = 0; i < placedTools.Length; i++)
            {
                if (placedTools[i] != null)
                {
                    Undo.RecordObject(placedTools[i], "Toggle Tool Visibility");
                    placedTools[i].SetActive(i == tier);
                }
            }

            Debug.Log($"[ShovelToolSetup] Showing only {TIER_NAMES[tier]}");
        }

        private void HideAllTools()
        {
            FindExistingToolsQuiet();

            for (int i = 0; i < placedTools.Length; i++)
            {
                if (placedTools[i] != null)
                {
                    Undo.RecordObject(placedTools[i], "Hide Tool");
                    placedTools[i].SetActive(false);
                }
            }

            Debug.Log("[ShovelToolSetup] All tools hidden");
        }

        private void FindExistingTools()
        {
            FindExistingToolsQuiet();

            int found = 0;
            for (int i = 0; i < placedTools.Length; i++)
            {
                if (placedTools[i] != null) found++;
            }

            EditorUtility.DisplayDialog("Found Tools", $"Found {found} of 4 tool tiers", "OK");
        }

        private void FindExistingToolsQuiet()
        {
            Camera mainCam = Camera.main;
            if (mainCam == null) return;

            // Clear array
            for (int i = 0; i < placedTools.Length; i++)
            {
                placedTools[i] = null;
            }

            // Search for tools by name
            foreach (Transform child in mainCam.transform)
            {
                string nameLower = child.name.ToLower();

                if (nameLower.Contains("tool_base") || nameLower.Contains("base_shovel"))
                    placedTools[0] = child.gameObject;
                else if (nameLower.Contains("tool_tier1") || nameLower.Contains("tier1_shovel"))
                    placedTools[1] = child.gameObject;
                else if (nameLower.Contains("tool_tier2") || nameLower.Contains("tier2_shovel"))
                    placedTools[2] = child.gameObject;
                else if (nameLower.Contains("tool_tier3") || nameLower.Contains("tier3_shovel"))
                    placedTools[3] = child.gameObject;
            }
        }

        private void CopyTransformFromSelection()
        {
            if (Selection.activeTransform == null)
            {
                EditorUtility.DisplayDialog("Error", "No object selected!", "OK");
                return;
            }

            Transform selected = Selection.activeTransform;
            toolPosition = selected.localPosition;
            toolRotation = selected.localEulerAngles;
            toolScale = selected.localScale.x;

            Debug.Log($"[ShovelToolSetup] Copied transform from {selected.name}");
        }
    }
}
