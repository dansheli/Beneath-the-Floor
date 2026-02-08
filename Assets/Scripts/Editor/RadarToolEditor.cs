using UnityEngine;
using UnityEditor;
using BeneathTheFloor.Tools;

namespace BeneathTheFloor.Editor
{
    /// <summary>
    /// Custom editor for RadarTool that allows previewing holograms in Edit mode.
    /// Supports switching between Treasure Chest and Core Shard hologram modes.
    /// </summary>
    [CustomEditor(typeof(RadarTool))]
    public class RadarToolEditor : UnityEditor.Editor
    {
        private GameObject previewHologram;
        private bool isPreviewing = false;
        private int previewModeIndex = 0; // 0 = Treasure Chest, 1 = Core Shard

        private static readonly string[] modeNames = { "Treasure Chest", "Core Shard" };

        // Treasure Chest hologram properties
        private SerializedProperty hologramPrefabProp;
        private SerializedProperty hologramScaleProp;
        private SerializedProperty hologramOffsetProp;
        private SerializedProperty hologramBaseRotationProp;

        // Core Shard hologram properties
        private SerializedProperty coreShardPrefabProp;
        private SerializedProperty coreShardOffsetProp;
        private SerializedProperty coreShardScaleProp;

        // Shared
        private SerializedProperty renderOnTopProp;

        private void OnEnable()
        {
            hologramPrefabProp = serializedObject.FindProperty("hologramPrefab");
            hologramScaleProp = serializedObject.FindProperty("hologramScale");
            hologramOffsetProp = serializedObject.FindProperty("hologramOffset");
            hologramBaseRotationProp = serializedObject.FindProperty("hologramBaseRotation");

            coreShardPrefabProp = serializedObject.FindProperty("coreShardHologramPrefab");
            coreShardOffsetProp = serializedObject.FindProperty("coreShardHologramOffset");
            coreShardScaleProp = serializedObject.FindProperty("coreShardHologramScale");

            renderOnTopProp = serializedObject.FindProperty("renderOnTop");
        }

        private void OnDisable()
        {
            ClearPreview();
        }

        // Helpers to get active mode properties
        private SerializedProperty ActivePrefabProp => previewModeIndex == 0 ? hologramPrefabProp : coreShardPrefabProp;
        private SerializedProperty ActiveOffsetProp => previewModeIndex == 0 ? hologramOffsetProp : coreShardOffsetProp;
        private float ActiveScale => previewModeIndex == 0 ? hologramScaleProp.floatValue : coreShardScaleProp.floatValue;

        public override void OnInspectorGUI()
        {
            DrawDefaultInspector();

            EditorGUILayout.Space(10);
            EditorGUILayout.LabelField("Hologram Preview", EditorStyles.boldLabel);

            RadarTool radarTool = (RadarTool)target;
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

                // Mode selector
                EditorGUI.BeginChangeCheck();
                int newMode = GUILayout.Toolbar(previewModeIndex, modeNames, GUILayout.Height(28));
                if (EditorGUI.EndChangeCheck() && newMode != previewModeIndex)
                {
                    previewModeIndex = newMode;
                    // Rebuild preview with new mode if active
                    if (isPreviewing)
                    {
                        ClearPreview();
                        CreatePreview(radarInstance);
                    }
                }

                EditorGUILayout.Space(5);

                // Show which prefab + fields are active for this mode
                string modeName = modeNames[previewModeIndex];
                GameObject activePrefab = ActivePrefabProp.objectReferenceValue as GameObject;
                string prefabName = activePrefab != null ? activePrefab.name : "(none)";

                EditorGUILayout.BeginVertical("box");
                EditorGUILayout.LabelField($"Mode: {modeName}", EditorStyles.boldLabel);
                EditorGUILayout.LabelField($"Prefab: {prefabName}");
                EditorGUILayout.LabelField($"Scale: {ActiveScale:F3}");
                EditorGUILayout.LabelField($"Offset: {ActiveOffsetProp.vector3Value}");
                EditorGUILayout.EndVertical();

                EditorGUILayout.Space(5);

                // Preview buttons
                EditorGUILayout.BeginHorizontal();

                if (!isPreviewing)
                {
                    GUI.backgroundColor = Color.green;
                    if (GUILayout.Button($"Show {modeName} Preview", GUILayout.Height(30)))
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
                    string offsetFieldName = previewModeIndex == 0 ? "Hologram Offset" : "Core Shard Hologram Offset";
                    string scaleFieldName = previewModeIndex == 0 ? "Hologram Scale" : "Core Shard Hologram Scale";

                    EditorGUILayout.HelpBox(
                        $"Previewing: {modeName} hologram\n\n" +
                        $"- Adjust {offsetFieldName} to move it\n" +
                        $"- Adjust {scaleFieldName} to resize\n" +
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

                // Quick assign buttons
                if (previewModeIndex == 0 && hologramPrefabProp.objectReferenceValue == null)
                {
                    GUI.backgroundColor = Color.cyan;
                    if (GUILayout.Button("Assign Treasure Chest Prefab"))
                    {
                        AssignPrefab(hologramPrefabProp, new[] {
                            "Assets/Art/Treasure_Chests/Chest_1.prefab",
                            "Assets/Art/Treasure_Chests/Chest_2.prefab",
                            "Assets/Art/Treasure_Chests/Chest_3.prefab"
                        });
                    }
                    GUI.backgroundColor = Color.white;
                }
                else if (previewModeIndex == 1 && coreShardPrefabProp.objectReferenceValue == null)
                {
                    GUI.backgroundColor = Color.cyan;
                    if (GUILayout.Button("Assign Core Shard Prefab"))
                    {
                        AssignPrefab(coreShardPrefabProp, new[] {
                            "Assets/Art/Core Shard/Core Shard.prefab",
                            "Assets/Prefabs/Core Shard.prefab",
                            "Assets/Art/CoreShard/CoreShard.prefab"
                        });
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
            var instanceProp = serializedObject.FindProperty("radarInstance");
            if (instanceProp != null && instanceProp.objectReferenceValue != null)
            {
                return instanceProp.objectReferenceValue as GameObject;
            }

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

            GameObject instance = (GameObject)PrefabUtility.InstantiatePrefab(prefab, mainCam.transform);
            instance.name = "RadarTool_EMF";
            instance.transform.localPosition = new Vector3(-0.35f, -0.25f, 0.4f);
            instance.transform.localRotation = Quaternion.Euler(10f, 15f, -5f);
            instance.transform.localScale = Vector3.one * 0.8f;

            var instanceProp = serializedObject.FindProperty("radarInstance");
            if (instanceProp != null)
            {
                instanceProp.objectReferenceValue = instance;
                serializedObject.ApplyModifiedProperties();
            }

            EditorUtility.SetDirty(radarTool);
            Selection.activeGameObject = instance;
        }

        private void CreatePreview(GameObject radarInstance)
        {
            if (radarInstance == null) return;

            ClearPreview();

            previewHologram = new GameObject("RadarHologram_Preview");
            previewHologram.transform.SetParent(radarInstance.transform);
            previewHologram.hideFlags = HideFlags.DontSave;

            // Use active mode's transform settings
            previewHologram.transform.localPosition = ActiveOffsetProp.vector3Value;
            previewHologram.transform.localRotation = Quaternion.Euler(hologramBaseRotationProp.vector3Value);
            previewHologram.transform.localScale = Vector3.one * ActiveScale;

            GameObject prefab = ActivePrefabProp.objectReferenceValue as GameObject;

            if (prefab != null)
            {
                GameObject model = (GameObject)PrefabUtility.InstantiatePrefab(prefab, previewHologram.transform);
                model.name = "HologramModel_Preview";
                model.transform.localPosition = Vector3.zero;
                model.transform.localRotation = Quaternion.identity;
                model.transform.localScale = Vector3.one;
                model.hideFlags = HideFlags.DontSave;

                // Keep original prefab materials - just disable shadows
                foreach (var renderer in model.GetComponentsInChildren<Renderer>())
                {
                    renderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
                    renderer.receiveShadows = false;
                }
            }
            else
            {
                CreateFallbackPreviewShape();
            }

            isPreviewing = true;
            SceneView.RepaintAll();
        }

        private void CreateFallbackPreviewShape()
        {
            if (previewHologram == null) return;

            GameObject body = GameObject.CreatePrimitive(PrimitiveType.Cube);
            body.name = "ChestBody_Preview";
            body.transform.SetParent(previewHologram.transform);
            body.transform.localPosition = Vector3.zero;
            body.transform.localScale = new Vector3(1f, 0.6f, 0.7f);
            body.hideFlags = HideFlags.DontSave;
            DestroyImmediate(body.GetComponent<Collider>());

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

            Color holoColor = new Color(0.4f, 1f, 1f, 0.85f); // Cyan fallback

            foreach (var renderer in obj.GetComponentsInChildren<Renderer>())
            {
                Shader shader = Shader.Find("Universal Render Pipeline/Lit");
                if (shader == null)
                    shader = Shader.Find("Standard");

                Material mat = new Material(shader);
                mat.SetFloat("_Surface", 1);
                mat.SetFloat("_Blend", 0);
                mat.EnableKeyword("_SURFACE_TYPE_TRANSPARENT");
                mat.SetColor("_BaseColor", holoColor);
                mat.renderQueue = 3000;

                renderer.sharedMaterial = mat;
                renderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            }
        }

        private void UpdatePreviewTransform(GameObject radarInstance)
        {
            if (previewHologram == null || radarInstance == null) return;

            previewHologram.transform.localPosition = ActiveOffsetProp.vector3Value;
            previewHologram.transform.localRotation = Quaternion.Euler(hologramBaseRotationProp.vector3Value);
            previewHologram.transform.localScale = Vector3.one * ActiveScale;

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

        private void AssignPrefab(SerializedProperty prop, string[] searchPaths)
        {
            foreach (string path in searchPaths)
            {
                GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(path);
                if (prefab != null)
                {
                    prop.objectReferenceValue = prefab;
                    serializedObject.ApplyModifiedProperties();
                    Debug.Log($"[RadarToolEditor] Assigned {prefab.name}");

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

            // If not found, search whole project for likely matches
            string keyword = prop == coreShardPrefabProp ? "Core Shard" : "Chest";
            string[] guids = AssetDatabase.FindAssets($"{keyword} t:Prefab");
            if (guids.Length > 0)
            {
                string path = AssetDatabase.GUIDToAssetPath(guids[0]);
                GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(path);
                if (prefab != null)
                {
                    prop.objectReferenceValue = prefab;
                    serializedObject.ApplyModifiedProperties();
                    Debug.Log($"[RadarToolEditor] Found and assigned {prefab.name} from {path}");

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

            EditorUtility.DisplayDialog("Assign Prefab",
                $"Could not find a matching prefab.\nDrag one manually into the field.",
                "OK");
        }
    }
}
