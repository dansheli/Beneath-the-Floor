using UnityEngine;
using UnityEditor;

namespace BeneathTheFloor.Editor
{
    /// <summary>
    /// Editor utility to set up the RadarPointer component on the radar.
    /// </summary>
    public static class RadarPointerSetup
    {
        [MenuItem("Beneath The Floor/Setup/Add Radar Pointer Component")]
        public static void AddRadarPointerToRadar()
        {
            // Find the radar instance
            Camera mainCam = Camera.main;
            if (mainCam == null)
            {
                Debug.LogError("[RadarPointerSetup] No main camera found!");
                return;
            }

            Transform radarTransform = null;
            foreach (Transform child in mainCam.transform)
            {
                if (child.name.Contains("EMF") || child.name.Contains("Radar"))
                {
                    radarTransform = child;
                    break;
                }
            }

            if (radarTransform == null)
            {
                Debug.LogError("[RadarPointerSetup] No radar found under camera! Run 'Add Radar Tool to Scene' first.");
                return;
            }

            // Check if RadarPointer already exists
            var existingPointer = radarTransform.GetComponent<Tools.RadarPointer>();
            if (existingPointer != null)
            {
                Debug.Log("[RadarPointerSetup] RadarPointer already exists on radar!");
                Selection.activeGameObject = radarTransform.gameObject;
                return;
            }

            // Add RadarPointer component
            var radarPointer = radarTransform.gameObject.AddComponent<Tools.RadarPointer>();

            // Find the pointer child object
            Transform pointerTransform = FindPointerRecursive(radarTransform);
            if (pointerTransform != null)
            {
                // Set the pointer reference via SerializedObject
                SerializedObject so = new SerializedObject(radarPointer);
                so.FindProperty("pointer").objectReferenceValue = pointerTransform;
                so.ApplyModifiedProperties();

                Debug.Log($"[RadarPointerSetup] Found and assigned pointer: {pointerTransform.name}");
            }
            else
            {
                Debug.LogWarning("[RadarPointerSetup] Could not find pointer child. Please assign it manually.");
            }

            // Mark scene dirty
            UnityEditor.SceneManagement.EditorSceneManager.MarkSceneDirty(
                UnityEditor.SceneManagement.EditorSceneManager.GetActiveScene());

            Selection.activeGameObject = radarTransform.gameObject;

            Debug.Log("[RadarPointerSetup] RadarPointer component added to radar!");
            Debug.Log("[RadarPointerSetup] Now add a RadarTarget component to the room you want the radar to point to.");
        }

        private static Transform FindPointerRecursive(Transform parent)
        {
            foreach (Transform child in parent)
            {
                string nameLower = child.name.ToLower();
                if (nameLower.Contains("pointer") || nameLower.Contains("needle") || nameLower.Contains("arrow"))
                {
                    return child;
                }

                Transform found = FindPointerRecursive(child);
                if (found != null) return found;
            }
            return null;
        }

        [MenuItem("Beneath The Floor/Setup/Create Test Radar Target")]
        public static void CreateTestRadarTarget()
        {
            // Create a test target object
            GameObject targetObj = new GameObject("RadarTarget_TestRoom");
            targetObj.AddComponent<Tools.RadarTarget>();

            // Position it somewhere underground
            targetObj.transform.position = new Vector3(0f, -10f, 10f);

            // Mark scene dirty
            UnityEditor.SceneManagement.EditorSceneManager.MarkSceneDirty(
                UnityEditor.SceneManagement.EditorSceneManager.GetActiveScene());

            Selection.activeGameObject = targetObj;

            Debug.Log("[RadarPointerSetup] Test RadarTarget created at (0, -10, 10)");
            Debug.Log("[RadarPointerSetup] Move this to where your underground room is, or add RadarTarget to your actual room.");
        }
    }
}
