using UnityEngine;
using UnityEditor;
using System.Reflection;

namespace BeneathTheFloor.Editor
{
    /// <summary>
    /// Sets up an overlay camera system so held tools always render on top of world geometry.
    /// This prevents the tool from clipping through walls.
    /// </summary>
    public class HeldToolCameraSetup : EditorWindow
    {
        private const string LAYER_NAME = "HeldTool";

        [MenuItem("Tools/Beneath The Floor/Setup Held Tool Camera (Fix Wall Clipping)")]
        public static void ShowWindow()
        {
            var window = GetWindow<HeldToolCameraSetup>("Held Tool Camera Setup");
            window.minSize = new Vector2(400, 300);
        }

        private void OnGUI()
        {
            GUILayout.Label("Held Tool Camera Setup", EditorStyles.boldLabel);
            EditorGUILayout.Space(10);

            EditorGUILayout.HelpBox(
                "This tool sets up an overlay camera system so the held tool always renders on top of walls and other geometry.\n\n" +
                "It will:\n" +
                "1. Create a 'HeldTool' layer\n" +
                "2. Create an overlay camera under Main Camera\n" +
                "3. Set all tool objects to the HeldTool layer\n" +
                "4. Configure camera stacking (URP)",
                MessageType.Info);

            EditorGUILayout.Space(10);

            GUI.backgroundColor = Color.green;
            if (GUILayout.Button("Setup Held Tool Camera System", GUILayout.Height(40)))
            {
                SetupHeldToolCamera();
            }
            GUI.backgroundColor = Color.white;

            EditorGUILayout.Space(10);

            if (GUILayout.Button("Just Set Tool Layer (if camera already exists)", GUILayout.Height(30)))
            {
                SetToolLayers();
            }
        }

        [MenuItem("Tools/Beneath The Floor/Setup Held Tool Camera (Fix Wall Clipping)", true)]
        public static bool ValidateSetup()
        {
            return Camera.main != null;
        }

        public static void SetupHeldToolCamera()
        {
            // Step 1: Ensure layer exists
            EnsureLayerExists();

            // Step 2: Get main camera
            Camera mainCam = Camera.main;
            if (mainCam == null)
            {
                EditorUtility.DisplayDialog("Error", "No Main Camera found!", "OK");
                return;
            }

            // Step 3: Check if overlay camera already exists
            Transform existingOverlay = mainCam.transform.Find("HeldToolCamera");
            Camera overlayCam;

            if (existingOverlay != null)
            {
                overlayCam = existingOverlay.GetComponent<Camera>();
                Debug.Log("[HeldToolCameraSetup] Found existing HeldToolCamera");
            }
            else
            {
                // Create overlay camera
                GameObject overlayCamObj = new GameObject("HeldToolCamera");
                overlayCamObj.transform.SetParent(mainCam.transform);
                overlayCamObj.transform.localPosition = Vector3.zero;
                overlayCamObj.transform.localRotation = Quaternion.identity;
                overlayCamObj.transform.localScale = Vector3.one;

                overlayCam = overlayCamObj.AddComponent<Camera>();
                Undo.RegisterCreatedObjectUndo(overlayCamObj, "Create HeldTool Camera");
                Debug.Log("[HeldToolCameraSetup] Created new HeldToolCamera");
            }

            // Step 4: Configure overlay camera
            ConfigureOverlayCamera(overlayCam, mainCam);

            // Step 5: Set tool layers
            SetToolLayers();

            // Step 6: Configure main camera to exclude HeldTool layer
            ConfigureMainCamera(mainCam);

            EditorUtility.DisplayDialog("Success",
                "Held Tool Camera system setup complete!\n\n" +
                "The tool will now always render on top of walls.",
                "OK");
        }

        private static void EnsureLayerExists()
        {
            SerializedObject tagManager = new SerializedObject(AssetDatabase.LoadAllAssetsAtPath("ProjectSettings/TagManager.asset")[0]);
            SerializedProperty layers = tagManager.FindProperty("layers");

            // Check if layer already exists
            for (int i = 0; i < layers.arraySize; i++)
            {
                SerializedProperty layer = layers.GetArrayElementAtIndex(i);
                if (layer.stringValue == LAYER_NAME)
                {
                    Debug.Log($"[HeldToolCameraSetup] Layer '{LAYER_NAME}' already exists at index {i}");
                    return;
                }
            }

            // Find an empty slot in user layers (8-31)
            int targetIndex = -1;
            for (int i = 8; i < layers.arraySize; i++)
            {
                SerializedProperty layer = layers.GetArrayElementAtIndex(i);
                if (string.IsNullOrEmpty(layer.stringValue))
                {
                    targetIndex = i;
                    break;
                }
            }

            if (targetIndex == -1)
            {
                Debug.LogError("[HeldToolCameraSetup] No empty layer slots available!");
                return;
            }

            // Set the layer
            SerializedProperty targetLayer = layers.GetArrayElementAtIndex(targetIndex);
            targetLayer.stringValue = LAYER_NAME;
            tagManager.ApplyModifiedProperties();

            Debug.Log($"[HeldToolCameraSetup] Created layer '{LAYER_NAME}' at index {targetIndex}");
        }

        private static void ConfigureOverlayCamera(Camera overlayCam, Camera mainCam)
        {
            // Get the URP additional camera data component using reflection to avoid assembly issues
            var urpDataType = System.Type.GetType("UnityEngine.Rendering.Universal.UniversalAdditionalCameraData, Unity.RenderPipelines.Universal.Runtime");

            if (urpDataType == null)
            {
                Debug.LogError("[HeldToolCameraSetup] URP not found! Make sure Universal Render Pipeline is installed.");
                return;
            }

            // Get or add URP camera data to overlay camera
            var overlayData = overlayCam.GetComponent(urpDataType);
            if (overlayData == null)
            {
                overlayData = overlayCam.gameObject.AddComponent(urpDataType);
            }

            // Set render type to Overlay (1 = Overlay)
            var renderTypeProperty = urpDataType.GetProperty("renderType");
            if (renderTypeProperty != null)
            {
                // CameraRenderType.Overlay = 1
                renderTypeProperty.SetValue(overlayData, 1);
            }

            // Set culling mask to only HeldTool layer
            int heldToolLayer = LayerMask.NameToLayer(LAYER_NAME);
            if (heldToolLayer == -1)
            {
                Debug.LogError($"[HeldToolCameraSetup] Layer '{LAYER_NAME}' not found!");
                return;
            }
            overlayCam.cullingMask = 1 << heldToolLayer;

            // Configure camera settings
            overlayCam.clearFlags = CameraClearFlags.Depth;
            overlayCam.depth = mainCam.depth + 1;
            overlayCam.fieldOfView = mainCam.fieldOfView;
            overlayCam.nearClipPlane = 0.01f;
            overlayCam.farClipPlane = 10f;

            // Add to main camera's stack
            var mainCamData = mainCam.GetComponent(urpDataType);
            if (mainCamData != null)
            {
                var cameraStackProperty = urpDataType.GetProperty("cameraStack");
                if (cameraStackProperty != null)
                {
                    var cameraStack = cameraStackProperty.GetValue(mainCamData) as System.Collections.Generic.List<Camera>;
                    if (cameraStack != null && !cameraStack.Contains(overlayCam))
                    {
                        cameraStack.Add(overlayCam);
                        Debug.Log("[HeldToolCameraSetup] Added overlay camera to main camera stack");
                    }
                }
            }

            EditorUtility.SetDirty(overlayCam);
            Debug.Log("[HeldToolCameraSetup] Configured overlay camera");
        }

        private static void ConfigureMainCamera(Camera mainCam)
        {
            int heldToolLayer = LayerMask.NameToLayer(LAYER_NAME);
            if (heldToolLayer == -1) return;

            // Remove HeldTool layer from main camera's culling mask
            mainCam.cullingMask &= ~(1 << heldToolLayer);

            EditorUtility.SetDirty(mainCam);
            Debug.Log("[HeldToolCameraSetup] Main camera now excludes HeldTool layer");
        }

        private static void SetToolLayers()
        {
            int heldToolLayer = LayerMask.NameToLayer(LAYER_NAME);
            if (heldToolLayer == -1)
            {
                Debug.LogError($"[HeldToolCameraSetup] Layer '{LAYER_NAME}' not found! Run full setup first.");
                return;
            }

            Camera mainCam = Camera.main;
            if (mainCam == null) return;

            int toolsUpdated = 0;

            // Find all tools under camera
            string[] toolNames = new string[]
            {
                "Tool1_Base", "Tool1_Tier1", "Tool1_Tier2", "Tool1_Tier3",
                "Tool2_Base", "Tool2_Tier1", "Tool2_Tier2", "Tool2_Tier3",
                "Tool3_Base", "Tool3_Tier1", "Tool3_Tier2", "Tool3_Tier3",
                "Base_Shovel", "Tier1_Shovel", "Tier2_Shovel", "Tier3_Shovel",
                "Heavy_Spade_Base", "Heavy_Spade_Tier1", "Heavy_Spade_Tier2", "Heavy_Spade_Tier3",
                "pickaxe_Base", "pickaxe_Tier1", "pickaxe_Tier2", "pickaxe_Tier3"
            };

            foreach (Transform child in mainCam.transform)
            {
                foreach (string toolName in toolNames)
                {
                    if (child.name.Contains(toolName) || child.name.ToLower().Contains(toolName.ToLower()))
                    {
                        SetLayerRecursively(child.gameObject, heldToolLayer);
                        toolsUpdated++;
                        break;
                    }
                }
            }

            Debug.Log($"[HeldToolCameraSetup] Updated {toolsUpdated} tool(s) to HeldTool layer");
        }

        private static void SetLayerRecursively(GameObject obj, int layer)
        {
            obj.layer = layer;
            foreach (Transform child in obj.transform)
            {
                SetLayerRecursively(child.gameObject, layer);
            }
        }
    }
}
