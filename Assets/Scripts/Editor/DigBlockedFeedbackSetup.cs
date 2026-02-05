using UnityEngine;
using UnityEditor;
using BeneathTheFloor.Digging;

namespace BeneathTheFloor.Editor
{
    /// <summary>
    /// Editor utility to set up the DigBlockedFeedback system in the scene.
    /// </summary>
    public static class DigBlockedFeedbackSetup
    {
        [MenuItem("Beneath The Floor/Setup/Add Dig Blocked Feedback")]
        public static void AddDigBlockedFeedback()
        {
            // Check if already exists
            DigBlockedFeedback existing = Object.FindObjectOfType<DigBlockedFeedback>();
            if (existing != null)
            {
                Debug.Log("[Setup] DigBlockedFeedback already exists in the scene.");
                Selection.activeGameObject = existing.gameObject;
                return;
            }

            // Create new GameObject
            GameObject feedbackObj = new GameObject("DigBlockedFeedback");

            // Add component
            DigBlockedFeedback feedback = feedbackObj.AddComponent<DigBlockedFeedback>();

            // Mark scene dirty
            EditorUtility.SetDirty(feedbackObj);
            UnityEditor.SceneManagement.EditorSceneManager.MarkSceneDirty(
                UnityEditor.SceneManagement.EditorSceneManager.GetActiveScene());

            // Select the new object
            Selection.activeGameObject = feedbackObj;

            Debug.Log("[Setup] Created DigBlockedFeedback. Assign a blocked sound clip in the inspector.");
        }
    }
}
