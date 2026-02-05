using UnityEngine;
using UnityEditor;

namespace BeneathTheFloor.Editor
{
    /// <summary>
    /// Editor window to preview and position FPS tools without entering Play mode.
    /// </summary>
    public class ToolPositionPreview : EditorWindow
    {
        private GameObject previewInstance;
        private Transform previewParent;
        private int selectedTier = 1;
        private Vector3 toolPosition = new Vector3(0.3f, -0.35f, 0.5f);
        private Vector3 rotation = new Vector3(15f, -10f, -20f);
        private float scale = 0.25f;

        private GameObject tier1Prefab;
        private GameObject tier2Prefab;
        private GameObject tier3Prefab;

        [MenuItem("Beneath The Floor/Tool Position Preview")]
        public static void ShowWindow()
        {
            GetWindow<ToolPositionPreview>("Tool Position Preview");
        }

        private void OnEnable()
        {
            SceneView.duringSceneGui += OnSceneGUI;
        }

        private void OnDisable()
        {
            SceneView.duringSceneGui -= OnSceneGUI;
            CleanupPreview();
        }

        private void OnDestroy()
        {
            CleanupPreview();
        }

        private void OnGUI()
        {
            EditorGUILayout.LabelField("Tool Position Preview", EditorStyles.boldLabel);
            EditorGUILayout.HelpBox("Drag your tool prefabs here, then click 'Create Preview' to position them in the Scene view.", MessageType.Info);

            EditorGUILayout.Space(10);

            // Prefab references
            EditorGUILayout.LabelField("Tool Prefabs", EditorStyles.boldLabel);
            tier1Prefab = (GameObject)EditorGUILayout.ObjectField("Tier 1 (Shovel)", tier1Prefab, typeof(GameObject), false);
            tier2Prefab = (GameObject)EditorGUILayout.ObjectField("Tier 2 (Hoe)", tier2Prefab, typeof(GameObject), false);
            tier3Prefab = (GameObject)EditorGUILayout.ObjectField("Tier 3 (Pickaxe)", tier3Prefab, typeof(GameObject), false);

            EditorGUILayout.Space(10);

            // Tier selection
            EditorGUILayout.LabelField("Preview Settings", EditorStyles.boldLabel);
            selectedTier = EditorGUILayout.IntSlider("Tier to Preview", selectedTier, 1, 3);

            EditorGUILayout.Space(5);

            // Transform values
            toolPosition = EditorGUILayout.Vector3Field("Position", toolPosition);
            rotation = EditorGUILayout.Vector3Field("Rotation", rotation);
            scale = EditorGUILayout.Slider("Scale", scale, 0.05f, 1f);

            EditorGUILayout.Space(10);

            // Buttons
            EditorGUILayout.BeginHorizontal();

            if (GUILayout.Button("Create Preview", GUILayout.Height(30)))
            {
                CreatePreview();
            }

            if (GUILayout.Button("Update Preview", GUILayout.Height(30)))
            {
                UpdatePreview();
            }

            if (GUILayout.Button("Remove Preview", GUILayout.Height(30)))
            {
                CleanupPreview();
            }

            EditorGUILayout.EndHorizontal();

            EditorGUILayout.Space(10);

            // Copy values button
            if (GUILayout.Button("Copy Values to PlayerToolVisualController", GUILayout.Height(35)))
            {
                CopyValuesToController();
            }

            EditorGUILayout.Space(10);

            // Quick presets
            EditorGUILayout.LabelField("Quick Presets", EditorStyles.boldLabel);
            EditorGUILayout.BeginHorizontal();

            if (GUILayout.Button("Lower Right"))
            {
                toolPosition = new Vector3(0.35f, -0.4f, 0.5f);
                rotation = new Vector3(20f, -15f, -25f);
                scale = 0.25f;
                UpdatePreview();
            }

            if (GUILayout.Button("Center"))
            {
                toolPosition = new Vector3(0f, -0.3f, 0.6f);
                rotation = new Vector3(10f, 0f, 0f);
                scale = 0.3f;
                UpdatePreview();
            }

            if (GUILayout.Button("Classic FPS"))
            {
                toolPosition = new Vector3(0.4f, -0.35f, 0.45f);
                rotation = new Vector3(0f, -20f, -15f);
                scale = 0.2f;
                UpdatePreview();
            }

            EditorGUILayout.EndHorizontal();

            // Instructions
            EditorGUILayout.Space(15);
            EditorGUILayout.HelpBox(
                "Instructions:\n" +
                "1. Drag your tool prefabs into the slots above\n" +
                "2. Click 'Create Preview' to spawn the tool\n" +
                "3. Adjust Position/Rotation/Scale sliders\n" +
                "4. Or select the preview in Scene and use gizmos\n" +
                "5. Click 'Copy Values to PlayerToolVisualController'\n" +
                "6. Remove preview when done",
                MessageType.None);
        }

        private void CreatePreview()
        {
            CleanupPreview();

            // Find camera
            Camera mainCam = Camera.main;
            if (mainCam == null)
            {
                mainCam = FindObjectOfType<Camera>();
            }

            if (mainCam == null)
            {
                EditorUtility.DisplayDialog("Error", "No camera found in scene. Please add a camera first.", "OK");
                return;
            }

            // Get the prefab for selected tier
            GameObject prefab = GetPrefabForTier(selectedTier);
            if (prefab == null)
            {
                EditorUtility.DisplayDialog("Error", $"No prefab assigned for Tier {selectedTier}. Please drag a prefab into the slot.", "OK");
                return;
            }

            // Create parent anchor
            previewParent = new GameObject("__ToolPreview_Anchor__").transform;
            previewParent.SetParent(mainCam.transform);
            previewParent.localPosition = Vector3.zero;
            previewParent.localRotation = Quaternion.identity;

            // Instantiate preview
            previewInstance = (GameObject)PrefabUtility.InstantiatePrefab(prefab);
            previewInstance.name = "__ToolPreview__";
            previewInstance.transform.SetParent(previewParent);

            // Apply transform
            UpdatePreview();

            // Select it for easy manipulation
            Selection.activeGameObject = previewInstance;
            SceneView.lastActiveSceneView?.Focus();

            Debug.Log("[ToolPositionPreview] Preview created. Adjust in Scene view or use sliders.");
        }

        private void UpdatePreview()
        {
            if (previewInstance == null) return;

            previewInstance.transform.localPosition = toolPosition;
            previewInstance.transform.localRotation = Quaternion.Euler(rotation);
            previewInstance.transform.localScale = Vector3.one * scale;

            SceneView.RepaintAll();
        }

        private void CleanupPreview()
        {
            if (previewInstance != null)
            {
                DestroyImmediate(previewInstance);
                previewInstance = null;
            }

            if (previewParent != null)
            {
                DestroyImmediate(previewParent.gameObject);
                previewParent = null;
            }
        }

        private GameObject GetPrefabForTier(int tier)
        {
            return tier switch
            {
                1 => tier1Prefab,
                2 => tier2Prefab,
                3 => tier3Prefab,
                _ => tier1Prefab
            };
        }

        private void CopyValuesToController()
        {
            // Find PlayerToolVisualController in scene
            var controller = FindObjectOfType<Player.PlayerToolVisualController>();
            if (controller == null)
            {
                EditorUtility.DisplayDialog("Error", "PlayerToolVisualController not found in scene.", "OK");
                return;
            }

            // Use SerializedObject to modify the component
            SerializedObject so = new SerializedObject(controller);

            string posField = $"tier{selectedTier}Position";
            string rotField = $"tier{selectedTier}Rotation";
            string scaleField = $"tier{selectedTier}Scale";

            var posProp = so.FindProperty(posField);
            var rotProp = so.FindProperty(rotField);
            var scaleProp = so.FindProperty(scaleField);

            if (posProp != null) posProp.vector3Value = toolPosition;
            if (rotProp != null) rotProp.vector3Value = rotation;
            if (scaleProp != null) scaleProp.floatValue = scale;

            so.ApplyModifiedProperties();
            EditorUtility.SetDirty(controller);

            Debug.Log($"[ToolPositionPreview] Copied Tier {selectedTier} values to PlayerToolVisualController:\n" +
                      $"Position: {toolPosition}\nRotation: {rotation}\nScale: {scale}");

            EditorUtility.DisplayDialog("Success", $"Tier {selectedTier} values copied to PlayerToolVisualController!\n\nDon't forget to save the scene.", "OK");
        }

        private void OnSceneGUI(SceneView sceneView)
        {
            // Sync values from preview if it's selected
            if (previewInstance != null && Selection.activeGameObject == previewInstance)
            {
                if (previewInstance.transform.localPosition != toolPosition ||
                    previewInstance.transform.localRotation != Quaternion.Euler(rotation) ||
                    previewInstance.transform.localScale.x != scale)
                {
                    toolPosition = previewInstance.transform.localPosition;
                    rotation = previewInstance.transform.localRotation.eulerAngles;
                    scale = previewInstance.transform.localScale.x;
                    Repaint();
                }
            }
        }
    }
}
