using UnityEngine;
using UnityEngine.SceneManagement;
using BeneathTheFloor.Digging;
using BeneathTheFloor.Tools;

namespace BeneathTheFloor.GameFlow
{
    /// <summary>
    /// Triggers the demo end screen when the player with the final tool (Drill Pike)
    /// tries to dig beyond its maximum depth limit.
    /// Auto-creates itself when DiggingSystem is found.
    /// </summary>
    public class DemoDepthLimitTrigger : MonoBehaviour
    {
        public static DemoDepthLimitTrigger Instance { get; private set; }

        [Header("Settings")]
        [Tooltip("The tool index that triggers demo end (3 = Drill Pike)")]
        [SerializeField] private int finalToolIndex = 3;

        [Tooltip("Scene to load when depth limit is hit")]
        [SerializeField] private string demoEndSceneName = "DemoEndScene";

        [Tooltip("Only trigger once per session")]
        [SerializeField] private bool triggerOnce = true;

        [Header("Debug")]
        [SerializeField] private bool enableDebugLogs = false;

        private bool hasTriggered = false;
        private DiggingSystem diggingSystem;

        /// <summary>
        /// Ensure the trigger exists. Called by DiggingSystem.
        /// </summary>
        public static void EnsureExists()
        {
            if (Instance != null) return;

            // Check if already exists in scene
            Instance = FindFirstObjectByType<DemoDepthLimitTrigger>();
            if (Instance != null) return;

            // Create it
            GameObject obj = new GameObject("DemoDepthLimitTrigger");
            Instance = obj.AddComponent<DemoDepthLimitTrigger>();
            DontDestroyOnLoad(obj);
            Debug.Log("[DemoDepthLimitTrigger] Auto-created trigger instance");
        }

        private void Awake()
        {
            if (Instance == null)
            {
                Instance = this;
                DontDestroyOnLoad(gameObject);
            }
            else if (Instance != this)
            {
                Destroy(gameObject);
                return;
            }
        }

        private void Start()
        {
            SubscribeToDiggingSystem();
        }

        private void SubscribeToDiggingSystem()
        {
            // Unsubscribe first to avoid double subscription
            if (diggingSystem != null)
            {
                diggingSystem.OnDigBlockedDepthLimit -= OnDigBlockedByDepth;
            }

            // Find DiggingSystem
            diggingSystem = FindFirstObjectByType<DiggingSystem>();
            if (diggingSystem != null)
            {
                diggingSystem.OnDigBlockedDepthLimit += OnDigBlockedByDepth;
                if (enableDebugLogs)
                    Debug.Log("[DemoDepthLimitTrigger] Subscribed to dig depth limit events");
            }
            else
            {
                if (enableDebugLogs)
                    Debug.LogWarning("[DemoDepthLimitTrigger] DiggingSystem not found - will retry on scene load");

                // Subscribe to scene load to retry
                SceneManager.sceneLoaded += OnSceneLoaded;
            }
        }

        private void OnSceneLoaded(Scene scene, LoadSceneMode mode)
        {
            // Retry subscribing when a new scene loads
            SubscribeToDiggingSystem();
        }

        private void OnDestroy()
        {
            if (diggingSystem != null)
            {
                diggingSystem.OnDigBlockedDepthLimit -= OnDigBlockedByDepth;
            }
            SceneManager.sceneLoaded -= OnSceneLoaded;

            if (Instance == this)
            {
                Instance = null;
            }
        }

        private void OnDigBlockedByDepth(float currentDepth, float maxDepth)
        {
            // Check if already triggered
            if (triggerOnce && hasTriggered)
                return;

            // Check if using the final tool
            int currentToolIndex = HeldToolController.Instance?.CurrentToolIndex ?? 0;

            if (enableDebugLogs)
                Debug.Log($"[DemoDepthLimitTrigger] Depth blocked: tool={currentToolIndex}, depth={currentDepth:F1}m, max={maxDepth:F1}m");

            if (currentToolIndex != finalToolIndex)
                return;

            // Final tool hit its depth limit - trigger demo end
            hasTriggered = true;
            TriggerDemoEnd();
        }

        private void TriggerDemoEnd()
        {
            Debug.Log("[DemoDepthLimitTrigger] Final tool depth limit reached - showing demo end screen!");

            // Check if demo end scene is already loaded
            Scene demoScene = SceneManager.GetSceneByName(demoEndSceneName);
            if (demoScene.isLoaded)
            {
                if (enableDebugLogs)
                    Debug.Log("[DemoDepthLimitTrigger] Demo end scene already loaded");
                return;
            }

            // Pause the game
            Time.timeScale = 0f;

            // Show cursor
            Cursor.lockState = CursorLockMode.None;
            Cursor.visible = true;

            // Load demo end scene additively
            SceneManager.LoadScene(demoEndSceneName, LoadSceneMode.Additive);
        }

        /// <summary>
        /// Reset the trigger so it can fire again.
        /// Call this if you want to allow the demo end to show again after dismissing.
        /// </summary>
        public void ResetTrigger()
        {
            hasTriggered = false;
        }
    }
}
