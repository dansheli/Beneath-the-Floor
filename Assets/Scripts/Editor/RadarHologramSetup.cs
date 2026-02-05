using UnityEngine;
using UnityEditor;
using BeneathTheFloor.Tools;

namespace BeneathTheFloor.Editor
{
    /// <summary>
    /// Editor utility for setting up the radar hologram with a treasure chest prefab.
    /// </summary>
    public static class RadarHologramSetup
    {
        private const string CHEST_PREFAB_PATH = "Assets/Art/Treasure_Chests/Chest_1.prefab";

        [MenuItem("Tools/Beneath The Floor/Radar/Setup Hologram with Chest Prefab")]
        public static void SetupHologramWithChest()
        {
            // Find RadarTool in scene
            RadarTool radarTool = Object.FindObjectOfType<RadarTool>();
            if (radarTool == null)
            {
                // Try to find on main camera
                Camera mainCam = Camera.main;
                if (mainCam != null)
                {
                    radarTool = mainCam.GetComponent<RadarTool>();
                    if (radarTool == null)
                        radarTool = mainCam.gameObject.AddComponent<RadarTool>();
                }
                else
                {
                    EditorUtility.DisplayDialog("Radar Hologram Setup",
                        "No RadarTool found in scene and no main camera available.\n\n" +
                        "Please add a RadarTool component to your player camera first.",
                        "OK");
                    return;
                }
            }

            // Load chest prefab
            GameObject chestPrefab = AssetDatabase.LoadAssetAtPath<GameObject>(CHEST_PREFAB_PATH);
            if (chestPrefab == null)
            {
                // Try alternative paths
                string[] altPaths = {
                    "Assets/Art/Treasure_Chests/Chest_2.prefab",
                    "Assets/Art/Treasure_Chests/Chest_3.prefab"
                };

                foreach (string path in altPaths)
                {
                    chestPrefab = AssetDatabase.LoadAssetAtPath<GameObject>(path);
                    if (chestPrefab != null) break;
                }

                if (chestPrefab == null)
                {
                    EditorUtility.DisplayDialog("Radar Hologram Setup",
                        "Could not find treasure chest prefab.\n\n" +
                        "Looked in: Assets/Art/Treasure_Chests/\n\n" +
                        "Please assign the hologram prefab manually in the RadarTool component.",
                        "OK");
                    return;
                }
            }

            // Assign to RadarTool via SerializedObject for proper undo support
            SerializedObject so = new SerializedObject(radarTool);
            SerializedProperty hologramPrefabProp = so.FindProperty("hologramPrefab");

            if (hologramPrefabProp != null)
            {
                hologramPrefabProp.objectReferenceValue = chestPrefab;
                so.ApplyModifiedProperties();

                EditorUtility.SetDirty(radarTool);

                Debug.Log($"[RadarHologramSetup] Assigned {chestPrefab.name} as hologram prefab to RadarTool.");
                EditorUtility.DisplayDialog("Radar Hologram Setup",
                    $"Successfully assigned {chestPrefab.name} as the hologram prefab!\n\n" +
                    "The hologram will appear as a spinning, glowing treasure chest projecting from the radar.\n\n" +
                    "You can adjust position, scale, spin speed, and colors in the RadarTool Inspector.",
                    "OK");
            }
            else
            {
                EditorUtility.DisplayDialog("Radar Hologram Setup",
                    "Could not find hologramPrefab property on RadarTool.\n\n" +
                    "Make sure you're using the updated RadarTool script.",
                    "OK");
            }
        }

        [MenuItem("Tools/Beneath The Floor/Radar/Configure Hologram Settings")]
        public static void SelectRadarTool()
        {
            RadarTool radarTool = Object.FindObjectOfType<RadarTool>();
            if (radarTool != null)
            {
                Selection.activeGameObject = radarTool.gameObject;
                EditorGUIUtility.PingObject(radarTool);
            }
            else
            {
                EditorUtility.DisplayDialog("Radar Settings",
                    "No RadarTool found in scene.\n\n" +
                    "The RadarTool is usually on the Main Camera.",
                    "OK");
            }
        }
    }
}
