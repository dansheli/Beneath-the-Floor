using UnityEngine;
using UnityEditor;
using System.Collections.Generic;

namespace BeneathTheFloor.Editor
{
    /// <summary>
    /// Editor tool to set up all tool prefabs in the scene.
    /// Supports individual transform settings per tool group.
    /// </summary>
    public class ToolSetupEditor : EditorWindow
    {
        // Tool 1: Shovel paths
        private static readonly string[] TOOL1_PATHS = new string[]
        {
            "Assets/Art/Tools/Tool1/Base_tier1_tier2_tier3/Base_Shovel.prefab",
            "Assets/Art/Tools/Tool1/Base_tier1_tier2_tier3/Tier1_Shovel.prefab",
            "Assets/Art/Tools/Tool1/Base_tier1_tier2_tier3/Tier2_Shovel.prefab",
            "Assets/Art/Tools/Tool1/Base_tier1_tier2_tier3/Tier3_Shovel.prefab"
        };

        // Tool 2: Heavy Spade paths
        private static readonly string[] TOOL2_PATHS = new string[]
        {
            "Assets/Art/Tools/Tool2/Heavy_Spade_Base.prefab",
            "Assets/Art/Tools/Tool2/Heavy_Spade_Tier1.prefab",
            "Assets/Art/Tools/Tool2/Heavy_Spade_Tier2.prefab",
            "Assets/Art/Tools/Tool2/Heavy_Spade_Tier3.prefab"
        };

        // Tool 3: Pickaxe paths
        private static readonly string[] TOOL3_PATHS = new string[]
        {
            "Assets/Art/Tools/Tool3/pickaxe_Base.prefab",
            "Assets/Art/Tools/Tool3/pickaxe_Tier1.prefab",
            "Assets/Art/Tools/Tool3/pickaxe_Tier2.prefab",
            "Assets/Art/Tools/Tool3/pickaxe_Tier3.prefab"
        };

        private static readonly string[] TOOL1_NAMES = { "Tool1_Base", "Tool1_Tier1", "Tool1_Tier2", "Tool1_Tier3" };
        private static readonly string[] TOOL2_NAMES = { "Tool2_Base", "Tool2_Tier1", "Tool2_Tier2", "Tool2_Tier3" };
        private static readonly string[] TOOL3_NAMES = { "Tool3_Base", "Tool3_Tier1", "Tool3_Tier2", "Tool3_Tier3" };

        // Individual transform settings per tool group
        [System.Serializable]
        private class ToolTransform
        {
            public Vector3 position = new Vector3(0.3f, -0.3f, 0.5f);
            public Vector3 rotation = new Vector3(0f, -90f, 0f);
            public float scale = 0.5f;
        }

        private ToolTransform tool1Transform = new ToolTransform();
        private ToolTransform tool2Transform = new ToolTransform();
        private ToolTransform tool3Transform = new ToolTransform();

        // Foldout states
        private bool tool1Foldout = true;
        private bool tool2Foldout = true;
        private bool tool3Foldout = true;

        // Preview selection
        private int selectedTool = 0;
        private int selectedTier = 0;

        // Placed tools
        private GameObject[] tool1Objects = new GameObject[4];
        private GameObject[] tool2Objects = new GameObject[4];
        private GameObject[] tool3Objects = new GameObject[4];

        // Scroll position
        private Vector2 scrollPosition;

        [MenuItem("Tools/Beneath The Floor/Setup All Tools")]
        public static void ShowWindow()
        {
            var window = GetWindow<ToolSetupEditor>("Tool Setup");
            window.minSize = new Vector2(480, 700);
        }

        private void OnGUI()
        {
            scrollPosition = EditorGUILayout.BeginScrollView(scrollPosition);

            GUILayout.Label("Tool Setup Editor", EditorStyles.boldLabel);
            EditorGUILayout.Space(5);

            EditorGUILayout.HelpBox(
                "Each tool group has its own transform settings.\n" +
                "Adjust position/rotation/scale per tool type.",
                MessageType.Info);

            EditorGUILayout.Space(10);

            // ==================== TOOL 1: SHOVEL ====================
            DrawToolSection(1, "Tool 1: Shovel (Depth 15m)", ref tool1Foldout, tool1Transform,
                tool1Objects, TOOL1_PATHS, TOOL1_NAMES, Color.green);

            EditorGUILayout.Space(5);

            // ==================== TOOL 2: HEAVY SPADE ====================
            DrawToolSection(2, "Tool 2: Heavy Spade (Depth 30m)", ref tool2Foldout, tool2Transform,
                tool2Objects, TOOL2_PATHS, TOOL2_NAMES, Color.yellow);

            EditorGUILayout.Space(5);

            // ==================== TOOL 3: PICKAXE ====================
            DrawToolSection(3, "Tool 3: Pickaxe (Depth 50m)", ref tool3Foldout, tool3Transform,
                tool3Objects, TOOL3_PATHS, TOOL3_NAMES, Color.cyan);

            EditorGUILayout.Space(10);

            // ==================== GLOBAL ACTIONS ====================
            GUILayout.Label("Global Actions", EditorStyles.boldLabel);

            EditorGUILayout.BeginHorizontal();
            GUI.backgroundColor = Color.green;
            if (GUILayout.Button("Place ALL Tools", GUILayout.Height(35)))
            {
                PlaceTool(1, tool1Transform);
                PlaceTool(2, tool2Transform);
                PlaceTool(3, tool3Transform);
            }
            GUI.backgroundColor = Color.red;
            if (GUILayout.Button("Remove Old Tools", GUILayout.Height(35)))
            {
                RemoveOldTools();
            }
            GUI.backgroundColor = Color.white;
            EditorGUILayout.EndHorizontal();

            EditorGUILayout.Space(5);

            // Preview selection
            GUILayout.Label("Preview", EditorStyles.boldLabel);
            EditorGUILayout.BeginHorizontal();
            selectedTool = EditorGUILayout.Popup(selectedTool, new string[] { "Tool 1: Shovel", "Tool 2: Heavy Spade", "Tool 3: Pickaxe" });
            selectedTier = EditorGUILayout.Popup(selectedTier, new string[] { "Base", "Tier 1", "Tier 2", "Tier 3" });
            EditorGUILayout.EndHorizontal();

            EditorGUILayout.BeginHorizontal();
            if (GUILayout.Button("Show Selected Only", GUILayout.Height(25)))
            {
                ShowOnlySelected();
            }
            if (GUILayout.Button("Hide All Tools", GUILayout.Height(25)))
            {
                HideAllTools();
            }
            EditorGUILayout.EndHorizontal();

            EditorGUILayout.EndScrollView();
        }

        private void DrawToolSection(int toolNum, string title, ref bool foldout, ToolTransform transform,
            GameObject[] objects, string[] paths, string[] names, Color color)
        {
            FindAllTools();

            // Count found tools
            int foundCount = 0;
            foreach (var obj in objects) if (obj != null) foundCount++;

            // Header with color
            GUI.backgroundColor = color;
            EditorGUILayout.BeginVertical(EditorStyles.helpBox);
            GUI.backgroundColor = Color.white;

            // Foldout header
            EditorGUILayout.BeginHorizontal();
            foldout = EditorGUILayout.Foldout(foldout, title, true, EditorStyles.foldoutHeader);
            GUILayout.Label($"[{foundCount}/4]", GUILayout.Width(40));
            EditorGUILayout.EndHorizontal();

            if (foldout)
            {
                EditorGUI.indentLevel++;

                // Transform settings
                EditorGUILayout.LabelField("Transform", EditorStyles.miniBoldLabel);
                transform.position = EditorGUILayout.Vector3Field("Position", transform.position);
                transform.rotation = EditorGUILayout.Vector3Field("Rotation", transform.rotation);
                transform.scale = EditorGUILayout.FloatField("Scale", transform.scale);

                EditorGUILayout.Space(3);

                // Action buttons
                EditorGUILayout.BeginHorizontal();
                if (GUILayout.Button("Place", GUILayout.Height(22)))
                {
                    PlaceTool(toolNum, transform);
                }
                if (GUILayout.Button("Apply Transform", GUILayout.Height(22)))
                {
                    ApplyTransformToGroup(objects, transform);
                }
                if (GUILayout.Button("Copy from Selected", GUILayout.Height(22)))
                {
                    CopyTransformFromSelection(transform);
                }
                EditorGUILayout.EndHorizontal();

                EditorGUILayout.Space(3);

                // Status
                EditorGUILayout.LabelField("Status", EditorStyles.miniBoldLabel);
                for (int i = 0; i < 4; i++)
                {
                    EditorGUILayout.BeginHorizontal();
                    GUI.color = objects[i] != null ? Color.green : Color.red;
                    EditorGUILayout.LabelField($"  {names[i]}", objects[i] != null ? "Found" : "Missing", GUILayout.Width(200));
                    GUI.color = Color.white;
                    if (objects[i] != null)
                    {
                        if (GUILayout.Button("Select", GUILayout.Width(50)))
                        {
                            Selection.activeGameObject = objects[i];
                        }
                        if (GUILayout.Button("Apply", GUILayout.Width(45)))
                        {
                            ApplyTransformToSingle(objects[i], transform);
                        }
                    }
                    EditorGUILayout.EndHorizontal();
                }

                EditorGUI.indentLevel--;
            }

            EditorGUILayout.EndVertical();
        }

        private void ApplyTransformToGroup(GameObject[] objects, ToolTransform transform)
        {
            int updated = 0;
            foreach (var obj in objects)
            {
                if (obj != null)
                {
                    Undo.RecordObject(obj.transform, "Apply Transform");
                    obj.transform.localPosition = transform.position;
                    obj.transform.localRotation = Quaternion.Euler(transform.rotation);
                    obj.transform.localScale = Vector3.one * transform.scale;
                    updated++;
                }
            }
            Debug.Log($"[ToolSetup] Applied transform to {updated} tool(s)");
        }

        private void ApplyTransformToSingle(GameObject obj, ToolTransform transform)
        {
            if (obj == null) return;
            Undo.RecordObject(obj.transform, "Apply Transform");
            obj.transform.localPosition = transform.position;
            obj.transform.localRotation = Quaternion.Euler(transform.rotation);
            obj.transform.localScale = Vector3.one * transform.scale;
            Debug.Log($"[ToolSetup] Applied transform to {obj.name}");
        }

        private void CopyTransformFromSelection(ToolTransform transform)
        {
            if (Selection.activeTransform == null)
            {
                EditorUtility.DisplayDialog("Error", "No object selected!", "OK");
                return;
            }

            Transform selected = Selection.activeTransform;
            transform.position = selected.localPosition;
            transform.rotation = selected.localEulerAngles;
            transform.scale = selected.localScale.x;

            Debug.Log($"[ToolSetup] Copied transform from {selected.name}");
        }

        private void RemoveOldTools()
        {
            Camera mainCam = Camera.main;
            if (mainCam == null) return;

            var toRemove = new List<GameObject>();

            foreach (Transform child in mainCam.transform)
            {
                string nameLower = child.name.ToLower();
                if (nameLower.Contains("tool_shovel_tier1") ||
                    nameLower.Contains("tool_hoe") ||
                    nameLower.Contains("tool_picaxe") ||
                    nameLower.Contains("tool_pickaxe") ||
                    nameLower.Contains("toolpack") ||
                    nameLower.Contains("tool_base") ||
                    nameLower.Contains("tool_tier"))
                {
                    toRemove.Add(child.gameObject);
                }
            }

            foreach (var obj in toRemove)
            {
                Undo.DestroyObjectImmediate(obj);
            }

            Debug.Log($"[ToolSetup] Removed {toRemove.Count} old tool(s)");
            EditorUtility.DisplayDialog("Done", $"Removed {toRemove.Count} old tool(s)", "OK");
        }

        private void PlaceTool(int toolNum, ToolTransform transform)
        {
            Camera mainCam = Camera.main;
            if (mainCam == null)
            {
                EditorUtility.DisplayDialog("Error", "No Main Camera found!", "OK");
                return;
            }

            string[] paths = toolNum == 1 ? TOOL1_PATHS : (toolNum == 2 ? TOOL2_PATHS : TOOL3_PATHS);
            string[] names = toolNum == 1 ? TOOL1_NAMES : (toolNum == 2 ? TOOL2_NAMES : TOOL3_NAMES);
            string toolName = toolNum == 1 ? "Shovel" : (toolNum == 2 ? "Heavy Spade" : "Pickaxe");

            int placed = 0;

            for (int i = 0; i < paths.Length; i++)
            {
                GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(paths[i]);
                if (prefab == null)
                {
                    Debug.LogError($"[ToolSetup] Could not load prefab: {paths[i]}");
                    continue;
                }

                // Check if already exists
                Transform existing = mainCam.transform.Find(names[i]);
                if (existing != null)
                {
                    Debug.Log($"[ToolSetup] {names[i]} already exists, skipping");
                    continue;
                }

                // Instantiate
                GameObject instance = (GameObject)PrefabUtility.InstantiatePrefab(prefab, mainCam.transform);
                instance.name = names[i];
                instance.transform.localPosition = transform.position;
                instance.transform.localRotation = Quaternion.Euler(transform.rotation);
                instance.transform.localScale = Vector3.one * transform.scale;

                // Only show base of Tool 1 by default
                instance.SetActive(toolNum == 1 && i == 0);

                Undo.RegisterCreatedObjectUndo(instance, $"Place {toolName}");
                placed++;
            }

            Debug.Log($"[ToolSetup] Placed {placed} {toolName} variant(s)");
        }

        private void ShowOnlySelected()
        {
            FindAllTools();

            // Hide all
            foreach (var obj in tool1Objects) if (obj != null) obj.SetActive(false);
            foreach (var obj in tool2Objects) if (obj != null) obj.SetActive(false);
            foreach (var obj in tool3Objects) if (obj != null) obj.SetActive(false);

            // Show selected
            GameObject[] targetArray = selectedTool == 0 ? tool1Objects : (selectedTool == 1 ? tool2Objects : tool3Objects);
            if (targetArray[selectedTier] != null)
            {
                targetArray[selectedTier].SetActive(true);
            }

            string toolName = selectedTool == 0 ? "Shovel" : (selectedTool == 1 ? "Heavy Spade" : "Pickaxe");
            Debug.Log($"[ToolSetup] Showing {toolName} Tier {selectedTier}");
        }

        private void HideAllTools()
        {
            FindAllTools();

            foreach (var obj in tool1Objects) if (obj != null) obj.SetActive(false);
            foreach (var obj in tool2Objects) if (obj != null) obj.SetActive(false);
            foreach (var obj in tool3Objects) if (obj != null) obj.SetActive(false);

            Debug.Log("[ToolSetup] All tools hidden");
        }

        private void FindAllTools()
        {
            Camera mainCam = Camera.main;
            if (mainCam == null) return;

            // Clear arrays
            for (int i = 0; i < 4; i++)
            {
                tool1Objects[i] = null;
                tool2Objects[i] = null;
                tool3Objects[i] = null;
            }

            // Find tools
            foreach (Transform child in mainCam.transform)
            {
                string name = child.name;

                // Tool 1
                if (name == "Tool1_Base") tool1Objects[0] = child.gameObject;
                else if (name == "Tool1_Tier1") tool1Objects[1] = child.gameObject;
                else if (name == "Tool1_Tier2") tool1Objects[2] = child.gameObject;
                else if (name == "Tool1_Tier3") tool1Objects[3] = child.gameObject;

                // Tool 2
                else if (name == "Tool2_Base") tool2Objects[0] = child.gameObject;
                else if (name == "Tool2_Tier1") tool2Objects[1] = child.gameObject;
                else if (name == "Tool2_Tier2") tool2Objects[2] = child.gameObject;
                else if (name == "Tool2_Tier3") tool2Objects[3] = child.gameObject;

                // Tool 3
                else if (name == "Tool3_Base") tool3Objects[0] = child.gameObject;
                else if (name == "Tool3_Tier1") tool3Objects[1] = child.gameObject;
                else if (name == "Tool3_Tier2") tool3Objects[2] = child.gameObject;
                else if (name == "Tool3_Tier3") tool3Objects[3] = child.gameObject;
            }
        }
    }
}
