using UnityEngine;
using UnityEditor;
using BeneathTheFloor.GameFlow;
using BeneathTheFloor.UI;

namespace BeneathTheFloor.Editor
{
    public class ObjectiveMarkerSetup
    {
        [MenuItem("Tools/Setup/Add Objective Marker to Note")]
        public static void AddMarkerToNote()
        {
            // Find the ReadableNote in the scene
            ReadableNote note = Object.FindObjectOfType<ReadableNote>();

            if (note == null)
            {
                Debug.LogError("[ObjectiveMarkerSetup] No ReadableNote found in scene!");
                return;
            }

            // Check if marker already exists
            ObjectiveMarker existingMarker = note.GetComponentInChildren<ObjectiveMarker>();
            if (existingMarker != null)
            {
                Debug.Log("[ObjectiveMarkerSetup] Objective marker already exists on note.");
                Selection.activeGameObject = existingMarker.gameObject;
                return;
            }

            // Create marker as child of note
            GameObject markerObj = new GameObject("ObjectiveMarker");
            markerObj.transform.SetParent(note.transform);
            markerObj.transform.localPosition = Vector3.zero;

            ObjectiveMarker marker = markerObj.AddComponent<ObjectiveMarker>();

            // Mark scene dirty
            UnityEditor.SceneManagement.EditorSceneManager.MarkSceneDirty(
                UnityEditor.SceneManagement.EditorSceneManager.GetActiveScene());

            Selection.activeGameObject = markerObj;

            Debug.Log($"[ObjectiveMarkerSetup] Added objective marker to '{note.gameObject.name}'");
        }
    }
}
