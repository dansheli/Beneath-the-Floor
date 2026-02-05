using UnityEngine;
using UnityEditor;
using BeneathTheFloor.Audio;

namespace BeneathTheFloor.Editor
{
    /// <summary>
    /// Editor utility to set up player idle sounds.
    /// </summary>
    public static class PlayerIdleSoundsSetup
    {
        [MenuItem("Beneath The Floor/Audio/Setup Player Idle Sounds")]
        public static void SetupPlayerIdleSounds()
        {
            // Find the player
            GameObject player = GameObject.FindGameObjectWithTag("Player");
            if (player == null)
            {
                player = GameObject.Find("Player");
            }

            if (player == null)
            {
                EditorUtility.DisplayDialog("Player Not Found",
                    "Could not find a Player object in the scene.\n\n" +
                    "Please ensure you have a Player GameObject with the 'Player' tag.",
                    "OK");
                return;
            }

            // Check if already exists
            var existing = player.GetComponent<PlayerIdleSounds>();
            if (existing != null)
            {
                Debug.Log("[PlayerIdleSoundsSetup] PlayerIdleSounds already exists on player!");
                Selection.activeGameObject = player;
                return;
            }

            // Add the component
            var idleSounds = player.AddComponent<PlayerIdleSounds>();

            // Mark scene dirty
            UnityEditor.SceneManagement.EditorSceneManager.MarkSceneDirty(
                UnityEditor.SceneManagement.EditorSceneManager.GetActiveScene());

            Selection.activeGameObject = player;

            Debug.Log("[PlayerIdleSoundsSetup] PlayerIdleSounds added to player!");
            EditorUtility.DisplayDialog("Player Idle Sounds Setup",
                "PlayerIdleSounds component added to Player!\n\n" +
                "Configure in the Inspector:\n\n" +
                "IDLE SOUNDS:\n" +
                "- Idle Sounds: Array of clips (breathing, sighing, shuffling)\n" +
                "- Idle Volume: Volume level (0-1)\n\n" +
                "TIMING:\n" +
                "- Idle Delay Before Start: Seconds before sounds begin\n" +
                "- Min/Max Time Between Sounds: Random interval range\n\n" +
                "DETECTION:\n" +
                "- Movement Threshold: Speed to count as moving\n" +
                "- Check Tool Usage: Include mouse clicks as activity",
                "OK");
        }
    }
}
