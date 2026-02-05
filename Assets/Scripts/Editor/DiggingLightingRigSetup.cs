using UnityEngine;
using UnityEditor;

namespace BeneathTheFloor.Editor
{
    /// <summary>
    /// Editor utility to create and configure the DiggingLightingRig.
    /// </summary>
    public static class DiggingLightingRigSetup
    {
        [MenuItem("Beneath The Floor/Lighting/Create Digging Lighting Rig")]
        public static void CreateDiggingLightingRig()
        {
            // Check if one already exists
            var existing = Object.FindObjectOfType<Lighting.DiggingLightingRig>();
            if (existing != null)
            {
                Selection.activeGameObject = existing.gameObject;
                EditorGUIUtility.PingObject(existing.gameObject);
                Debug.Log("[DiggingLightingRig] Already exists in scene. Selected it.");
                return;
            }

            // Find the terrain manager to get position
            var terrainManager = Object.FindObjectOfType<Digging.UndergroundTerrainManager>();
            Vector3 position = Vector3.zero;
            float basementFloorY = -3f;

            if (terrainManager != null)
            {
                position = terrainManager.transform.position;
                basementFloorY = terrainManager.basementFloorY;
            }

            // Create the rig
            var rigGO = new GameObject("DiggingLightingRig");
            rigGO.transform.position = position;

            var rig = rigGO.AddComponent<Lighting.DiggingLightingRig>();

            // Register undo
            Undo.RegisterCreatedObjectUndo(rigGO, "Create Digging Lighting Rig");

            // Select it
            Selection.activeGameObject = rigGO;

            Debug.Log($"[DiggingLightingRig] Created at position {position}, basementFloorY={basementFloorY}. " +
                      "Enter Play Mode to see the lights created automatically.");
        }

        [MenuItem("Beneath The Floor/Lighting/Select Digging Lighting Rig")]
        public static void SelectDiggingLightingRig()
        {
            var rig = Object.FindObjectOfType<Lighting.DiggingLightingRig>();
            if (rig != null)
            {
                Selection.activeGameObject = rig.gameObject;
                EditorGUIUtility.PingObject(rig.gameObject);
            }
            else
            {
                Debug.LogWarning("[DiggingLightingRig] Not found in scene. Use 'Create Digging Lighting Rig' first.");
            }
        }
    }
}
