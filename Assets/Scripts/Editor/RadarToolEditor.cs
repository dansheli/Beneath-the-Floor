using UnityEngine;
using UnityEditor;
using BeneathTheFloor.Tools;

namespace BeneathTheFloor.Editor
{
    /// <summary>
    /// Custom editor for RadarTool that allows previewing the hologram in Edit mode.
    /// </summary>
    [CustomEditor(typeof(RadarTool))]
    public class RadarToolEditor : UnityEditor.Editor
    {
        private GameObject previewHologram;
        private bool isPreviewing = false;

        // Serialized properties for hologram
        private SerializedProperty hologramPrefabProp;
        private SerializedProperty hologramScaleProp;
        private SerializedProperty hologramOffsetProp;
        private SerializedProperty hologramBaseRotationProp;
        private SerializedProperty hologramSpinSpeedProp;
        private SerializedProperty hologramBobAmplitudeProp;
        private SerializedProperty hologramColorProp;
        private SerializedProperty hologramEmissionProp;
        private SerializedProperty renderOnTopProp;

        private void OnEnable()
        {
            hologramPrefabProp = serializedObject.FindProperty("hologramPrefab");
            hologramScaleProp = serializedObject.FindProperty("hologramScale");
            hologramOffsetProp = serializedObject.FindProperty("hologramOffset");
            hologramBaseRotationProp = serializedObject.FindProperty("hologramBaseRotation");
            hologramSpinSpeedProp = serializedObject.FindProperty("hologramSpinSpeed");
            hologramBobAmplitudeProp = serializedObject.FindProperty("hologramBobAmplitude");
            hologramColorProp = serializedObject.FindProperty("hologramColor");
            hologramEmissionProp = serializedObject.FindProperty("hologramEmission");
            renderOnTopProp = serializedObject.FindProperty("renderOnTop");
        }

        private void OnDisable()
        {
            // Clean up preview when deselecting
            ClearPreview();
        }

        public override void OnInspectorGUI()
        {
            // Draw default inspector
            DrawDefaultInspector();

            EditorGUILayout.Space(10);
            EditorGUILayout.LabelField("Edit Mode Preview", EditorStyles.boldLabel);

            RadarTool radarTool = (RadarTool)target;

            // Find radar instance in scene
            GameObject radarInstance = FindRadarInstance(radarTool);

            if (radarInstance == null)
            {
                EditorGUILayout.HelpBox(
                    "No radar instance found. The radar is created at runtime under the Main Camera.\n\n" +
                    "To preview, you can:\n" +
                    "1. Enter Play mode briefly to create the radar\n" +
                    "2. Or manually place the EMF prefab under the camera",
                    MessageType.Info);

                if (GUILayout.Button("Find/Create Radar Instance"))
                {
                    CreateRadarInstanceForPreview(radarTool);
                    radarInstance = FindRadarInstance(radarTool);
                }
            }

            if (radarInstance != null)
            {
                EditorGUILayout.Space(5);

                // Preview buttons
                EditorGUILayout.BeginHorizontal();

                if (!isPreviewing)
                {
                    GUI.backgroundColor = Color.green;
                    if (GUILayout.Button("Show Hologram Preview", GUILayout.Height(30)))
                    {
                        CreatePreview(radarInstance);
                    }
                }
                else
                {
                    GUI.backgroundColor = Color.yellow;
                    if (GUILayout.Button("Update Preview", GUILayout.Height(30)))
                    {
                        ClearPreview();
                        CreatePreview(radarInstance);
                    }

                    GUI.backgroundColor = Color.red;
                    if (GUILayout.Button("Hide Preview", GUILayout.Height(30)))
                    {
                        ClearPreview();
                    }
                }

                GUI.backgroundColor = Color.white;
                EditorGUILayout.EndHorizontal();

                if (isPreviewing && previewHologram != null)
                {
                    EditorGUILayout.HelpBox(
                        "Hologram preview is active!\n\n" +
                        "- Adjust Hologram Offset to move it\n" +
                        "- Adjust Hologram Scale to resize\n" +
                        "- Adjust Hologram Base Rotation to rotate\n" +
                        "- Or select 'RadarHologram_Preview' in hierarchy and use gizmos",
                        MessageType.Info);

                    // Live update preview when values change
                    if (GUI.changed)
                    {
                        serializedObject.ApplyModifiedProperties();
                        UpdatePreviewTransform(radarInstance);
                    }
                }

                EditorGUILayout.Space(5);

                // Quick assign chest prefab button
                if (hologramPrefabProp.objectReferenceValue == null)
                {
                    GUI.backgroundColor = Color.cyan;
                    if (GUILayout.Button("Assign Treasure Chest Prefab"))
                    {
                        AssignChestPrefab();
                    }
                    GUI.backgroundColor = Color.white;
                }
            }

            // Apply changes
            if (GUI.changed)
            {
                serializedObject.ApplyModifiedProperties();
                if (isPreviewing && previewHologram != null)
                {
                    UpdatePreviewTransform(radarInstance);
                }
            }
        }

        private GameObject FindRadarInstance(RadarTool radarTool)
        {
            // First check if RadarTool has a reference
            var instanceProp = serializedObject.FindProperty("radarInstance");
            if (instanceProp != null && instanceProp.objectReferenceValue != null)
            {
                return instanceProp.objectReferenceValue as GameObject;
            }

            // Search under main camera
            Camera mainCam = Camera.main;
            if (mainCam != null)
            {
                foreach (Transform child in mainCam.transform)
                {
                    string nameLower = child.name.ToLower();
                    if (nameLower.Contains("emf") || nameLower.Contains("radar"))
                    {
                        return child.gameObject;
                    }
                }
            }

            // Search in scene
            var emfObjects = GameObject.FindObjectsOfType<Transform>();
            foreach (var t in emfObjects)
            {
                if (t.name.Contains("EMF") || t.name.Contains("Radar"))
                {
                    if (t.GetComponent<MeshRenderer>() != null || t.GetComponentInChildren<MeshRenderer>() != null)
                    {
                        return t.gameObject;
                    }
                }
            }

            return null;
        }

        private void CreateRadarInstanceForPreview(RadarTool radarTool)
        {
            string prefabPath = "Assets/lathiel/EMF/Prefabs/EMF_Modern_T2.prefab";
            GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(prefabPath);

            if (prefab == null)
            {
                EditorUtility.DisplayDialog("Radar Preview",
                    $"Could not find radar prefab at:\n{prefabPath}",
                    "OK");
                return;
            }

            Camera mainCam = Camera.main;
            if (mainCam == null)
            {
                EditorUtility.DisplayDialog("Radar Preview",
                    "No Main Camera found in scene.",
                    "OK");
                return;
            }

            // Instantiate under camera
            GameObject instance = (GameObject)PrefabUtility.InstantiatePrefab(prefab, mainCam.transform);
            instance.name = "RadarTool_EMF";
            instance.transform.localPosition = new Vector3(-0.35f, -0.25f, 0.4f);
            instance.transform.localRotation = Quaternion.Euler(10f, 15f, -5f);
            instance.transform.localScale = Vector3.one * 0.8f;

            // Assign to RadarTool
            var instanceProp = serializedObject.FindProperty("radarInstance");
            if (instanceProp != null)
            {
                instanceProp.objectReferenceValue = instance;
                serializedObject.ApplyModifiedProperties();
            }

            EditorUtility.SetDirty(radarTool);
            Selection.activeGameObject = instance;

            Debug.Log("[RadarToolEditor] Created radar instance under Main Camera for preview.");
        }

        private void CreatePreview(GameObject radarInstance)
        {
            if (radarInstance == null) return;

            ClearPreview();

            // Create preview container
            previewHologram = new GameObject("RadarHologram_Preview");
            previewHologram.transform.SetParent(radarInstance.transform);
            previewHologram.hideFlags = HideFlags.DontSave; // Don't save with scene

            // Apply transform from settings
            previewHologram.transform.localPosition = hologramOffsetProp.vector3Value;
            previewHologram.transform.localRotation = Quaternion.Euler(hologramBaseRotationProp.vector3Value);
            previewHologram.transform.localScale = Vector3.one * hologramScaleProp.floatValue;

            // Instantiate prefab or create fallback
            GameObject hologramPrefab = hologramPrefabProp.objectReferenceValue as GameObject;

            if (hologramPrefab != null)
            {
                GameObject model = (GameObject)PrefabUtility.InstantiatePrefab(hologramPrefab, previewHologram.transform);
                model.name = "HologramModel_Preview";
                model.transform.localPosition = Vector3.zero;
                model.transform.localRotation = Quaternion.identity;
                model.transform.localScale = Vector3.one;
                model.hideFlags = HideFlags.DontSave;

                ApplyPreviewMaterial(model);
            }
            else
            {
                // Create fallback shape
                CreateFallbackPreviewShape();
            }

            isPreviewing = true;
            SceneView.RepaintAll();

            Debug.Log("[RadarToolEditor] Created hologram preview. Adjust settings and see changes in Scene view.");
        }

        private void CreateFallbackPreviewShape()
        {
            if (previewHologram == null) return;

            // Body
            GameObject body = GameObject.CreatePrimitive(PrimitiveType.Cube);
            body.name = "ChestBody_Preview";
            body.transform.SetParent(previewHologram.transform);
            body.transform.localPosition = Vector3.zero;
            body.transform.localScale = new Vector3(1f, 0.6f, 0.7f);
            body.hideFlags = HideFlags.DontSave;
            DestroyImmediate(body.GetComponent<Collider>());

            // Lid
            GameObject lid = GameObject.CreatePrimitive(PrimitiveType.Cube);
            lid.name = "ChestLid_Preview";
            lid.transform.SetParent(previewHologram.transform);
            lid.transform.localPosition = new Vector3(0f, 0.4f, 0f);
            lid.transform.localScale = new Vector3(1.05f, 0.3f, 0.75f);
            lid.hideFlags = HideFlags.DontSave;
            DestroyImmediate(lid.GetComponent<Collider>());

            ApplyPreviewMaterial(body);
            ApplyPreviewMaterial(lid);
        }

        private void ApplyPreviewMaterial(GameObject obj)
        {
            if (obj == null) return;

            Color holoColor = hologramColorProp.colorValue;
            float emission = hologramEmissionProp.floatValue;

            foreach (var renderer in obj.GetComponentsInChildren<Renderer>())
            {
                // Create simple preview material
                Material mat = new Material(Shader.Find("Universal Render Pipeline/Lit"));
                if (mat.shader == null)
                    mat = new Material(Shader.Find("Standard"));

                // Make it semi-transparent and glowing
                mat.SetFloat("_Surface", 1); // Transparent
                mat.SetFloat("_Blend", 0);
                mat.EnableKeyword("_SURFACE_TYPE_TRANSPARENT");
                mat.SetColor("_BaseColor", holoColor);
                mat.EnableKeyword("_EMISSION");
                mat.SetColor("_EmissionColor", new Color(holoColor.r * emission, holoColor.g * emission, holoColor.b * emission));
                mat.renderQueue = 3000;

                renderer.sharedMaterial = mat;
                renderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            }
        }

        private void UpdatePreviewTransform(GameObject radarInstance)
        {
            if (previewHologram == null || radarInstance == null) return;

            previewHologram.transform.localPosition = hologramOffsetProp.vector3Value;
            previewHologram.transform.localRotation = Quaternion.Euler(hologramBaseRotationProp.vector3Value);
            previewHologram.transform.localScale = Vector3.one * hologramScaleProp.floatValue;

            // Update material color
            Color holoColor = hologramColorProp.colorValue;
            float emission = hologramEmissionProp.floatValue;

            foreach (var renderer in previewHologram.GetComponentsInChildren<Renderer>())
            {
                if (renderer.sharedMaterial != null)
                {
                    renderer.sharedMaterial.SetColor("_BaseColor", holoColor);
                    renderer.sharedMaterial.SetColor("_EmissionColor",
                        new Color(holoColor.r * emission, holoColor.g * emission, holoColor.b * emission));
                }
            }

            SceneView.RepaintAll();
        }

        private void ClearPreview()
        {
            if (previewHologram != null)
            {
                DestroyImmediate(previewHologram);
                previewHologram = null;
            }
            isPreviewing = false;
            SceneView.RepaintAll();
        }

        private void AssignChestPrefab()
        {
            string[] paths = {
                "Assets/Art/Treasure_Chests/Chest_1.prefab",
                "Assets/Art/Treasure_Chests/Chest_2.prefab",
                "Assets/Art/Treasure_Chests/Chest_3.prefab"
            };

            foreach (string path in paths)
            {
                GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(path);
                if (prefab != null)
                {
                    hologramPrefabProp.objectReferenceValue = prefab;
                    serializedObject.ApplyModifiedProperties();
                    Debug.Log($"[RadarToolEditor] Assigned {prefab.name} as hologram prefab.");

                    // Update preview if active
                    if (isPreviewing)
                    {
                        GameObject radarInstance = FindRadarInstance((RadarTool)target);
                        if (radarInstance != null)
                        {
                            ClearPreview();
                            CreatePreview(radarInstance);
                        }
                    }
                    return;
                }
            }

            EditorUtility.DisplayDialog("Assign Chest Prefab",
                "Could not find treasure chest prefab in:\nAssets/Art/Treasure_Chests/",
                "OK");
        }
    }
}
