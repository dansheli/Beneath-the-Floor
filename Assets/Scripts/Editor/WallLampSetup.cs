using UnityEngine;
using UnityEditor;
using BeneathTheFloor.Lighting;

namespace BeneathTheFloor.Editor
{
    /// <summary>
    /// Editor utility to create wall lamp hierarchy for manual placement.
    /// </summary>
    public class WallLampSetup
    {
        [MenuItem("Tools/Beneath The Floor/Create Wall Lamp")]
        public static void CreateSingleWallLamp()
        {
            // Create at scene view camera position or origin
            Vector3 position = Vector3.zero;
            if (SceneView.lastActiveSceneView != null)
            {
                position = SceneView.lastActiveSceneView.camera.transform.position +
                           SceneView.lastActiveSceneView.camera.transform.forward * 3f;
            }

            GameObject lamp = new GameObject("WallLamp");
            lamp.transform.position = position;
            lamp.AddComponent<WallLamp>();

            Selection.activeGameObject = lamp;
            Undo.RegisterCreatedObjectUndo(lamp, "Create Wall Lamp");

            Debug.Log("[WallLampSetup] Created WallLamp. Position it manually in the scene.");
        }

        [MenuItem("Tools/Beneath The Floor/Create Wall Lamp Holder (8 lamps)")]
        public static void CreateWallLampHolder()
        {
            // Find or create parent
            GameObject holder = GameObject.Find("ExcavationLamps");
            if (holder == null)
            {
                holder = new GameObject("ExcavationLamps");
            }

            // Create 8 lamps as children - user will position them
            string[] lampNames = {
                "WallLamp_North_L", "WallLamp_North_R",
                "WallLamp_South_L", "WallLamp_South_R",
                "WallLamp_East_L", "WallLamp_East_R",
                "WallLamp_West_L", "WallLamp_West_R"
            };

            // Default spread positions (user will adjust)
            Vector3[] defaultPositions = {
                new Vector3(-1, -4, 2),   // North L
                new Vector3(1, -4, 2),    // North R
                new Vector3(-1, -4, -2),  // South L
                new Vector3(1, -4, -2),   // South R
                new Vector3(2, -4, -1),   // East L
                new Vector3(2, -4, 1),    // East R
                new Vector3(-2, -4, -1),  // West L
                new Vector3(-2, -4, 1),   // West R
            };

            for (int i = 0; i < lampNames.Length; i++)
            {
                // Skip if already exists
                if (holder.transform.Find(lampNames[i]) != null) continue;

                GameObject lamp = new GameObject(lampNames[i]);
                lamp.transform.SetParent(holder.transform);
                lamp.transform.localPosition = defaultPositions[i];
                lamp.AddComponent<WallLamp>();
            }

            UnityEditor.SceneManagement.EditorSceneManager.MarkSceneDirty(
                UnityEditor.SceneManagement.EditorSceneManager.GetActiveScene());

            Selection.activeGameObject = holder;
            Debug.Log("[WallLampSetup] Created ExcavationLamps with 8 child lamps. Position them manually.");
        }

        [MenuItem("Tools/Beneath The Floor/Remove All Wall Lamps")]
        public static void RemoveAllWallLamps()
        {
            GameObject holder = GameObject.Find("ExcavationLamps");
            if (holder != null)
            {
                Undo.DestroyObjectImmediate(holder);
                Debug.Log("[WallLampSetup] Removed ExcavationLamps");
            }
        }

        [MenuItem("Tools/Beneath The Floor/Create Ceiling Spotlight")]
        public static void CreateCeilingSpotlight()
        {
            // Find or create under ExcavationLamps
            GameObject holder = GameObject.Find("ExcavationLamps");
            if (holder == null)
            {
                holder = new GameObject("ExcavationLamps");
            }

            // Create spotlight above excavation
            GameObject spotlight = new GameObject("CeilingSpotlight");
            spotlight.transform.SetParent(holder.transform);
            spotlight.transform.localPosition = new Vector3(0, 0, 0); // User will position
            spotlight.AddComponent<CeilingSpotlight>();

            // Create light child immediately so it works in editor
            GameObject lightObj = new GameObject("SpotLight");
            lightObj.transform.SetParent(spotlight.transform, false);
            lightObj.transform.localPosition = Vector3.down * 0.1f;
            lightObj.transform.localRotation = Quaternion.Euler(90f, 0f, 0f);

            Light light = lightObj.AddComponent<Light>();
            light.type = LightType.Spot;
            light.range = 15f;
            light.spotAngle = 60f;
            light.innerSpotAngle = 30f;
            light.intensity = 2.5f;
            light.color = new Color(1f, 0.95f, 0.85f);
            light.shadows = LightShadows.Soft;
            light.shadowStrength = 0.6f;

            Selection.activeGameObject = spotlight;
            Undo.RegisterCreatedObjectUndo(spotlight, "Create Ceiling Spotlight");

            UnityEditor.SceneManagement.EditorSceneManager.MarkSceneDirty(
                UnityEditor.SceneManagement.EditorSceneManager.GetActiveScene());

            Debug.Log("[WallLampSetup] Created CeilingSpotlight with light. Position it above the excavation area.");
        }
    }
}
