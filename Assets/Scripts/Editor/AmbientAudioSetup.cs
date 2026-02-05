using UnityEngine;
using UnityEditor;
using BeneathTheFloor.Audio;

namespace BeneathTheFloor.Editor
{
    /// <summary>
    /// Editor utility to set up ambient audio zones in the scene.
    /// </summary>
    public static class AmbientAudioSetup
    {
        [MenuItem("Beneath The Floor/Audio/Setup Ambient Audio Zones")]
        public static void SetupAmbientAudioZones()
        {
            // Check if already exists
            var existing = Object.FindFirstObjectByType<AmbientAudioZoneManager>();
            if (existing != null)
            {
                Debug.Log("[AmbientAudioSetup] AmbientAudioZoneManager already exists!");
                Selection.activeGameObject = existing.gameObject;
                return;
            }

            // Create the manager
            GameObject managerObj = new GameObject("AmbientAudioZoneManager");
            var manager = managerObj.AddComponent<AmbientAudioZoneManager>();

            // Mark scene dirty
            UnityEditor.SceneManagement.EditorSceneManager.MarkSceneDirty(
                UnityEditor.SceneManagement.EditorSceneManager.GetActiveScene());

            Selection.activeGameObject = managerObj;

            Debug.Log("[AmbientAudioSetup] AmbientAudioZoneManager created!");
            Debug.Log("[AmbientAudioSetup] Assign your audio clips in the Inspector:");
            Debug.Log("  - Basement Ambient 1: Primary basement sound");
            Debug.Log("  - Basement Ambient 2: Secondary basement sound (optional)");
            Debug.Log("  - Underground Ambient 1-3: Your excavation area sounds");

            EditorUtility.DisplayDialog("Ambient Audio Setup",
                "AmbientAudioZoneManager created!\n\n" +
                "Now assign your audio clips in the Inspector:\n\n" +
                "BASEMENT:\n" +
                "- Basement Ambient 1: Primary basement sound\n" +
                "- Basement Ambient 2: Secondary sound (plays together)\n\n" +
                "UNDERGROUND/EXCAVATION:\n" +
                "- Underground Ambient 1: Primary excavation sound\n" +
                "- Underground Ambient 2: Secondary sound (plays together)\n" +
                "- Underground Ambient 3: Third sound (plays together)\n\n" +
                "Each sound has its own independent volume control.\n" +
                "Adjust 'Underground Threshold Y' to set the transition depth.",
                "OK");
        }
    }
}
