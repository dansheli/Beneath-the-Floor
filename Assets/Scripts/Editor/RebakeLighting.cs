using UnityEngine;
using UnityEditor;

namespace BeneathTheFloor.Editor
{
    public class RebakeLighting
    {
        [MenuItem("Tools/Rebake Lighting")]
        public static void Rebake()
        {
            Debug.Log("[RebakeLighting] Starting lightmap bake...");

            // Clear existing baked data first
            Lightmapping.Clear();
            Debug.Log("[RebakeLighting] Cleared existing lightmap data.");

            // Start baking
            Lightmapping.BakeAsync();
            Debug.Log("[RebakeLighting] Bake started. This may take a few minutes depending on scene complexity.");
        }

        [MenuItem("Tools/Clear Baked Lighting")]
        public static void ClearBakedData()
        {
            Lightmapping.Clear();
            Lightmapping.ClearDiskCache();
            Debug.Log("[RebakeLighting] Cleared all baked lighting data and disk cache.");
        }
    }
}
