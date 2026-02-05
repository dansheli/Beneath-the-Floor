using UnityEngine;
using UnityEngine.SceneManagement;

namespace BeneathTheFloor.Player
{
    /// <summary>
    /// Ensures the player has a PlayerHeadlamp component.
    /// Attach this to a scene object (like GameManager) or it will auto-run.
    /// </summary>
    public class PlayerHeadlampSetup : MonoBehaviour
    {
        [Header("Settings")]
        [SerializeField] private bool autoSetup = true;
        [SerializeField] private float setupDelay = 0.2f;

        // Debug flag - set to false for production
        private static bool enableDebugLogs = false;

        private static bool hasSetup = false;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetStatics()
        {
            hasSetup = false;
        }

        private void Start()
        {
            if (autoSetup && !hasSetup)
            {
                Invoke(nameof(SetupPlayerHeadlamp), setupDelay);
            }
        }

        /// <summary>
        /// Ensures the player has a PlayerHeadlamp component.
        /// </summary>
        [ContextMenu("Setup Player Headlamp")]
        public void SetupPlayerHeadlamp()
        {
            if (hasSetup) return;

            // Find the player
            var player = FirstPersonController.Instance;
            if (player == null)
            {
                player = FindObjectOfType<FirstPersonController>();
            }

            if (player == null)
            {
                Debug.LogWarning("[PlayerHeadlampSetup] Player not found, cannot setup headlamp.");
                return;
            }

            // Check if PlayerHeadlamp already exists
            var existingHeadlamp = player.GetComponent<PlayerHeadlamp>();
            if (existingHeadlamp != null)
            {
                hasSetup = true;
                return;
            }

            // Add PlayerHeadlamp component
            player.gameObject.AddComponent<PlayerHeadlamp>();
            hasSetup = true;
        }

        /// <summary>
        /// Static bootstrap to register for scene load events.
        /// </summary>
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void AutoBootstrap()
        {
            if (enableDebugLogs) Debug.Log("[PlayerHeadlampSetup] AutoBootstrap - registering for scene loads");

            // Register for scene load events so we can set up headlamp in any scene
            SceneManager.sceneLoaded -= OnSceneLoaded;
            SceneManager.sceneLoaded += OnSceneLoaded;

            // Also try to set up now in case player already exists
            TrySetupHeadlamp();
        }

        private static void OnSceneLoaded(Scene scene, LoadSceneMode mode)
        {
            if (enableDebugLogs) Debug.Log($"[PlayerHeadlampSetup] Scene loaded: {scene.name}");
            hasSetup = false; // Reset so we can try again in new scene
            TrySetupHeadlamp();
        }

        private static void TrySetupHeadlamp()
        {
            if (hasSetup) return;

            if (enableDebugLogs) Debug.Log("[PlayerHeadlampSetup] Trying to setup headlamp...");

            // Check if Basement scene or player exists
            GameObject player = GameObject.FindGameObjectWithTag("Player");
            if (player == null)
            {
                if (enableDebugLogs) Debug.Log("[PlayerHeadlampSetup] No Player tag found, searching for FirstPersonController...");
                var fpc = Object.FindObjectOfType<FirstPersonController>();
                if (fpc != null) player = fpc.gameObject;
            }

            if (player == null)
            {
                if (enableDebugLogs) Debug.Log("[PlayerHeadlampSetup] No player in this scene, skipping.");
                return;
            }

            if (enableDebugLogs) Debug.Log("[PlayerHeadlampSetup] Found player: " + player.name);

            // Check if PlayerHeadlamp already exists
            if (player.GetComponent<PlayerHeadlamp>() != null)
            {
                if (enableDebugLogs) Debug.Log("[PlayerHeadlampSetup] PlayerHeadlamp already exists.");
                hasSetup = true;
                return;
            }

            // Add PlayerHeadlamp
            if (enableDebugLogs) Debug.Log("[PlayerHeadlampSetup] Adding PlayerHeadlamp component to player.");
            player.AddComponent<PlayerHeadlamp>();
            hasSetup = true;
        }
    }
}
