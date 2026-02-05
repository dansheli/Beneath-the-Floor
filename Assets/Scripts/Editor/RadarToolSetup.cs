using UnityEngine;
using UnityEditor;

namespace BeneathTheFloor.Editor
{
    /// <summary>
    /// Editor utility to set up the RadarTool in the scene.
    /// </summary>
    public static class RadarToolSetup
    {
        [MenuItem("Beneath The Floor/Setup/Add Radar Tool to Scene")]
        public static void AddRadarToolToScene()
        {
            // Check if RadarTool already exists
            var existing = Object.FindFirstObjectByType<Tools.RadarTool>();
            if (existing != null)
            {
                Debug.Log("[RadarToolSetup] RadarTool already exists in scene!");
                Selection.activeGameObject = existing.gameObject;
                return;
            }

            // Find the player or camera to attach to
            Camera mainCam = Camera.main;
            if (mainCam == null)
            {
                Debug.LogError("[RadarToolSetup] No main camera found! Please ensure a camera is tagged as MainCamera.");
                return;
            }

            // Create RadarTool manager object
            GameObject radarManager = new GameObject("RadarToolManager");
            var radarTool = radarManager.AddComponent<Tools.RadarTool>();

            // Load and instantiate the EMF prefab
            string prefabPath = "Assets/lathiel/EMF/Prefabs/EMF_Modern_T2.prefab";
            GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(prefabPath);

            if (prefab == null)
            {
                Debug.LogError($"[RadarToolSetup] Could not find EMF prefab at: {prefabPath}");
                Object.DestroyImmediate(radarManager);
                return;
            }

            // Instantiate under camera
            GameObject radarInstance = (GameObject)PrefabUtility.InstantiatePrefab(prefab, mainCam.transform);
            radarInstance.name = "RadarTool_EMF";

            // Position for left hand
            radarInstance.transform.localPosition = new Vector3(-0.35f, -0.25f, 0.4f);
            radarInstance.transform.localRotation = Quaternion.Euler(10f, 15f, -5f);
            radarInstance.transform.localScale = Vector3.one * 0.8f;

            // Disable shadows
            foreach (var renderer in radarInstance.GetComponentsInChildren<Renderer>())
            {
                renderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            }

            // Hide by default (missions will show it)
            radarInstance.SetActive(false);

            // Set reference in RadarTool via SerializedObject
            SerializedObject so = new SerializedObject(radarTool);
            so.FindProperty("radarInstance").objectReferenceValue = radarInstance;
            so.ApplyModifiedProperties();

            // Mark scene dirty
            UnityEditor.SceneManagement.EditorSceneManager.MarkSceneDirty(
                UnityEditor.SceneManagement.EditorSceneManager.GetActiveScene());

            Selection.activeGameObject = radarManager;

            Debug.Log("[RadarToolSetup] Radar tool added to scene! The EMF device is now attached to the camera's left hand position.");
            Debug.Log("[RadarToolSetup] Press 'R' to toggle the radar visibility (hidden by default).");
        }

        [MenuItem("Beneath The Floor/Setup/Adjust Radar Position")]
        public static void AdjustRadarPosition()
        {
            var radarTool = Object.FindFirstObjectByType<Tools.RadarTool>();
            if (radarTool == null)
            {
                Debug.LogError("[RadarToolSetup] No RadarTool found in scene! Run 'Add Radar Tool to Scene' first.");
                return;
            }

            // Find the radar instance
            Camera mainCam = Camera.main;
            if (mainCam == null) return;

            Transform radar = null;
            foreach (Transform child in mainCam.transform)
            {
                if (child.name.Contains("EMF") || child.name.Contains("Radar"))
                {
                    radar = child;
                    break;
                }
            }

            if (radar == null)
            {
                Debug.LogError("[RadarToolSetup] No radar instance found under camera!");
                return;
            }

            // Select it for manual adjustment
            Selection.activeTransform = radar;
            SceneView.FrameLastActiveSceneView();

            Debug.Log("[RadarToolSetup] Selected radar for adjustment. Use the Transform handles to position it.");
            Debug.Log($"[RadarToolSetup] Current Position: {radar.localPosition}");
            Debug.Log($"[RadarToolSetup] Current Rotation: {radar.localRotation.eulerAngles}");
            Debug.Log($"[RadarToolSetup] Current Scale: {radar.localScale}");
        }
    }
}
