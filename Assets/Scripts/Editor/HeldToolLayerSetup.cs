using UnityEngine;
using UnityEditor;

namespace BeneathTheFloor.Editor
{
    /// <summary>
    /// Editor utility to set up the HeldTool layer for first-person tool rendering.
    /// </summary>
    public static class HeldToolLayerSetup
    {
        private const string HELD_TOOL_LAYER_NAME = "HeldTool";

        [MenuItem("Beneath The Floor/Setup/Create HeldTool Layer")]
        public static void CreateHeldToolLayer()
        {
            // Check if layer already exists
            int existingLayer = LayerMask.NameToLayer(HELD_TOOL_LAYER_NAME);
            if (existingLayer >= 0)
            {
                Debug.Log($"[HeldToolLayerSetup] Layer '{HELD_TOOL_LAYER_NAME}' already exists at index {existingLayer}");
                EditorUtility.DisplayDialog("Layer Exists",
                    $"The '{HELD_TOOL_LAYER_NAME}' layer already exists at index {existingLayer}.", "OK");
                return;
            }

            // Find an empty layer slot (layers 8-31 are user layers)
            SerializedObject tagManager = new SerializedObject(AssetDatabase.LoadAllAssetsAtPath("ProjectSettings/TagManager.asset")[0]);
            SerializedProperty layersProp = tagManager.FindProperty("layers");

            int newLayerIndex = -1;
            for (int i = 8; i < 32; i++)
            {
                SerializedProperty layerProp = layersProp.GetArrayElementAtIndex(i);
                if (string.IsNullOrEmpty(layerProp.stringValue))
                {
                    layerProp.stringValue = HELD_TOOL_LAYER_NAME;
                    newLayerIndex = i;
                    break;
                }
            }

            if (newLayerIndex >= 0)
            {
                tagManager.ApplyModifiedProperties();
                Debug.Log($"[HeldToolLayerSetup] Created layer '{HELD_TOOL_LAYER_NAME}' at index {newLayerIndex}");
                EditorUtility.DisplayDialog("Layer Created",
                    $"Successfully created '{HELD_TOOL_LAYER_NAME}' layer at index {newLayerIndex}.\n\n" +
                    "The overlay camera system will now work correctly to render tools on top of terrain.", "OK");
            }
            else
            {
                Debug.LogError("[HeldToolLayerSetup] No empty layer slots available!");
                EditorUtility.DisplayDialog("Error",
                    "No empty layer slots available. Please free up a layer slot (8-31) first.", "OK");
            }
        }

        [MenuItem("Beneath The Floor/Setup/Verify HeldTool Layer")]
        public static void VerifyHeldToolLayer()
        {
            int layer = LayerMask.NameToLayer(HELD_TOOL_LAYER_NAME);
            if (layer >= 0)
            {
                Debug.Log($"[HeldToolLayerSetup] ✓ Layer '{HELD_TOOL_LAYER_NAME}' exists at index {layer}");
                EditorUtility.DisplayDialog("Layer Verified",
                    $"✓ The '{HELD_TOOL_LAYER_NAME}' layer exists at index {layer}.\n\n" +
                    "Tools will render on top of terrain correctly.", "OK");
            }
            else
            {
                Debug.LogWarning($"[HeldToolLayerSetup] ✗ Layer '{HELD_TOOL_LAYER_NAME}' does not exist");
                bool create = EditorUtility.DisplayDialog("Layer Missing",
                    $"The '{HELD_TOOL_LAYER_NAME}' layer does not exist.\n\n" +
                    "Without this layer, tools may clip into terrain.\n\n" +
                    "Would you like to create it now?", "Create Layer", "Cancel");

                if (create)
                {
                    CreateHeldToolLayer();
                }
            }
        }

        /// <summary>
        /// Automatically create the layer on editor load if it doesn't exist.
        /// </summary>
        [InitializeOnLoadMethod]
        private static void AutoCheckLayer()
        {
            // Delay check to avoid issues during editor startup
            EditorApplication.delayCall += () =>
            {
                int layer = LayerMask.NameToLayer(HELD_TOOL_LAYER_NAME);
                if (layer < 0)
                {
                    Debug.LogWarning($"[HeldToolLayerSetup] Layer '{HELD_TOOL_LAYER_NAME}' not found. " +
                        $"Run 'Beneath The Floor > Setup > Create HeldTool Layer' to fix tool clipping.");
                }
            };
        }
    }
}
