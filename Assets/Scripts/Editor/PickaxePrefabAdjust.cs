using UnityEngine;
using UnityEditor;

namespace BeneathTheFloor.Editor
{
    public static class PickaxePrefabAdjust
    {
        [MenuItem("Beneath The Floor/Setup/Adjust Pickaxe Tier 1 Transform", false, 131)]
        public static void AdjustPickaxeTransform()
        {
            string prefabPath = "Assets/Art/Tools/Pickaxe/Prefabs/Pickaxe_Tier1_FPS.prefab";

            GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(prefabPath);
            if (prefab == null)
            {
                Debug.LogError($"[PickaxePrefabAdjust] Could not load prefab at: {prefabPath}");
                return;
            }

            // Open prefab for editing
            string assetPath = AssetDatabase.GetAssetPath(prefab);
            GameObject prefabRoot = PrefabUtility.LoadPrefabContents(assetPath);

            // Find the mesh child (should be named "Mesh" or first child)
            Transform meshChild = prefabRoot.transform.childCount > 0
                ? prefabRoot.transform.GetChild(0)
                : null;

            if (meshChild == null)
            {
                Debug.LogError("[PickaxePrefabAdjust] No mesh child found in prefab!");
                PrefabUtility.UnloadPrefabContents(prefabRoot);
                return;
            }

            // Apply new transform values
            meshChild.localScale = new Vector3(50f, 50f, 50f);
            meshChild.localRotation = Quaternion.Euler(-150f, 0f, 0f);

            Debug.Log($"[PickaxePrefabAdjust] Updated '{meshChild.name}':");
            Debug.Log($"  Scale: {meshChild.localScale}");
            Debug.Log($"  Rotation: {meshChild.localEulerAngles}");

            // Save changes
            PrefabUtility.SaveAsPrefabAsset(prefabRoot, assetPath);
            PrefabUtility.UnloadPrefabContents(prefabRoot);

            Debug.Log("[PickaxePrefabAdjust] Prefab saved successfully!");
        }
    }
}
