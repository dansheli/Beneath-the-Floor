using UnityEngine;
using UnityEngine.SceneManagement;
using System.Collections.Generic;
using BeneathTheFloor.Save;
using BeneathTheFloor.UI;

namespace BeneathTheFloor.Managers
{
    public class GameManager : MonoBehaviour
    {
        [Header("Game Settings")]
        [SerializeField] private bool isPaused = false;

        [Header("Scene References")]
        [SerializeField] private string mainMenuScene = "MainMenu";
        [SerializeField] private string houseScene = "HouseScene";
        [SerializeField] private string basementScene = "BasementScene";

        [Header("Spawn Points - Fallback Positions")]
        [Tooltip("Fallback position if spawn point not found. For basement, Y should be at floor level (-3 for basement)")]
        [SerializeField] private Vector3 basementSpawnPosition = new Vector3(0f, -2.9f, 3f);
        [SerializeField] private Vector3 basementSpawnRotation = new Vector3(0f, 180f, 0f);
        [SerializeField] private Vector3 houseSpawnPosition = new Vector3(0f, 1f, 0f);
        [SerializeField] private Vector3 houseSpawnRotation = Vector3.zero;

        [Header("Spawn Point Names")]
        [SerializeField] private string basementSpawnPointName = "BasementSpawnPoint";
        [SerializeField] private string houseSpawnPointName = "HouseSpawnPoint";

        [Header("Spawn Settings")]
        [SerializeField] private LayerMask groundLayerMask = -1; // Which layers count as ground
        [SerializeField] private float spawnGroundCheckHeight = 2f; // How high above spawn to start raycast (reduced to avoid hitting ceiling)
        [SerializeField] private float spawnGroundCheckDistance = 5f; // How far down to raycast (reduced for indoor spaces)

        [Header("Debug")]
        [SerializeField] private bool debugSpawning = false;

        public static GameManager Instance { get; private set; }

        public bool IsPaused => isPaused;
        public int CurrentDepth { get; private set; } = 0;
        public int MaxDepthReached { get; private set; } = 0;

        private void Awake()
        {
            if (Instance == null)
            {
                Instance = this;

                // DontDestroyOnLoad only works on root GameObjects
                // Unparent first if we have a parent
                if (transform.parent != null)
                {
                    transform.SetParent(null);
                }
                DontDestroyOnLoad(gameObject);

                InitializeGame();
            }
            else
            {
                Destroy(gameObject);
            }
        }

        private void OnEnable()
        {
            SceneManager.sceneLoaded += OnSceneLoaded;
        }

        private void OnDisable()
        {
            SceneManager.sceneLoaded -= OnSceneLoaded;
        }

        private void OnSceneLoaded(Scene scene, LoadSceneMode mode)
        {
            if (debugSpawning)
            {
                Debug.Log($"[GameManager] Scene loaded: {scene.name}");
            }

            // Handle player spawning based on requested spawn point
            HandlePlayerSpawn(scene.name);

            // Ensure ControlsHintUI exists in gameplay scenes (fallback for RuntimeInitializeOnLoadMethod)
            EnsureControlsHintUI(scene.name);
        }

        private void EnsureControlsHintUI(string sceneName)
        {
            // Skip menu scenes
            string lowerName = sceneName.ToLower();
            if (lowerName.Contains("menu") || lowerName.Contains("press"))
                return;

            // Check if ControlsHintUI already exists
            if (ControlsHintUI.Instance != null)
                return;

            // Create it as a fallback
            if (debugSpawning) Debug.Log("[GameManager] Creating ControlsHintUI as fallback");
            GameObject hintObj = new GameObject("ControlsHintUI_Fallback");
            hintObj.AddComponent<ControlsHintUI>();
        }

        private void HandlePlayerSpawn(string sceneName)
        {
            string requestedSpawn = PlayerPrefs.GetString("SpawnPoint", "");

            if (debugSpawning)
            {
                Debug.Log($"[GameManager] Checking spawn. Scene: {sceneName}, RequestedSpawn: '{requestedSpawn}'");
            }

            if (string.IsNullOrEmpty(requestedSpawn))
            {
                if (debugSpawning)
                {
                    Debug.Log("[GameManager] No spawn point requested, skipping teleport.");
                }
                return;
            }

            // Determine spawn position based on scene and spawn point
            Vector3 targetPosition = Vector3.zero;
            Quaternion targetRotation = Quaternion.identity;
            bool shouldTeleport = false;

            if (sceneName == basementScene && requestedSpawn == "BasementEntrance")
            {
                // Try to find spawn point transform first
                GameObject spawnPoint = GameObject.Find(basementSpawnPointName);
                if (spawnPoint != null)
                {
                    targetPosition = spawnPoint.transform.position;
                    targetRotation = spawnPoint.transform.rotation;
                    if (debugSpawning)
                    {
                        Debug.Log($"[GameManager] Found {basementSpawnPointName} at {targetPosition}");
                    }
                }
                else
                {
                    // Fallback to hardcoded position
                    targetPosition = basementSpawnPosition;
                    targetRotation = Quaternion.Euler(basementSpawnRotation);
                    if (debugSpawning)
                    {
                        Debug.LogWarning($"[GameManager] {basementSpawnPointName} not found, using fallback position {targetPosition}");
                    }
                }
                shouldTeleport = true;
            }
            else if (sceneName == houseScene && requestedSpawn == "HouseEntrance")
            {
                // Try to find spawn point transform first
                GameObject spawnPoint = GameObject.Find(houseSpawnPointName);
                if (spawnPoint != null)
                {
                    targetPosition = spawnPoint.transform.position;
                    targetRotation = spawnPoint.transform.rotation;
                    if (debugSpawning)
                    {
                        Debug.Log($"[GameManager] Found {houseSpawnPointName} at {targetPosition}");
                    }
                }
                else
                {
                    // Fallback to hardcoded position
                    targetPosition = houseSpawnPosition;
                    targetRotation = Quaternion.Euler(houseSpawnRotation);
                    if (debugSpawning)
                    {
                        Debug.LogWarning($"[GameManager] {houseSpawnPointName} not found, using fallback position {targetPosition}");
                    }
                }
                shouldTeleport = true;
            }

            if (shouldTeleport)
            {
                // Use a coroutine to wait for scene to fully load
                StartCoroutine(TeleportPlayerDelayed(targetPosition, targetRotation));

                // Clear the spawn point
                PlayerPrefs.DeleteKey("SpawnPoint");
                PlayerPrefs.Save();
            }
        }

        private System.Collections.IEnumerator TeleportPlayerDelayed(Vector3 position, Quaternion rotation)
        {
            // Wait for scene to fully initialize - need multiple frames for physics
            yield return null;
            yield return null;
            yield return new WaitForFixedUpdate();
            yield return new WaitForFixedUpdate();

            TeleportPlayer(position, rotation);

            // Double-check position after a short delay
            yield return new WaitForSeconds(0.1f);

            GameObject player = GameObject.FindGameObjectWithTag("Player");
            if (player != null)
            {
                // If player is too high (on roof) or too low (falling), retry teleport
                if (player.transform.position.y > position.y + 3f || player.transform.position.y < position.y - 3f)
                {
                    if (debugSpawning)
                    {
                        Debug.LogWarning($"[GameManager] Player at wrong height ({player.transform.position.y}), retrying teleport to {position.y}");
                    }
                    TeleportPlayer(position, rotation);
                }
            }
        }

        private void TeleportPlayer(Vector3 position, Quaternion rotation)
        {
            GameObject player = GameObject.FindGameObjectWithTag("Player");
            if (player == null)
            {
                Debug.LogError("[GameManager] Could not find player with 'Player' tag!");
                return;
            }

            // For basement, skip ground check and use position directly (floor is at Y=-3)
            // Ground check can hit ceiling in enclosed spaces
            Vector3 finalPosition = position;

            // Only do ground check if position Y is reasonable (above -10)
            if (position.y > -10f)
            {
                Vector3 groundedPosition = GetGroundedPosition(position);
                // Only use grounded position if it's close to original (within 2 units)
                if (Mathf.Abs(groundedPosition.y - position.y) < 2f)
                {
                    finalPosition = groundedPosition;
                }
            }

            // Disable CharacterController temporarily
            CharacterController cc = player.GetComponent<CharacterController>();
            if (cc != null)
            {
                cc.enabled = false;
            }

            // Set position and rotation
            player.transform.position = finalPosition;
            player.transform.rotation = rotation;

            // Re-enable CharacterController
            if (cc != null)
            {
                cc.enabled = true;
            }

            // Ensure player movement is enabled
            Player.FirstPersonController fpc = player.GetComponent<Player.FirstPersonController>();
            if (fpc != null)
            {
                fpc.CanMove = true;
                if (debugSpawning)
                {
                    Debug.Log($"[GameManager] Player movement enabled. CanMove: {fpc.CanMove}");
                }
            }

            // Ensure game is not paused
            if (isPaused)
            {
                SetPause(false);
            }

            if (debugSpawning)
            {
                Debug.Log($"[GameManager] Teleported player to {finalPosition} (original target: {position})");
            }
        }

        private Vector3 GetGroundedPosition(Vector3 startPosition)
        {
            // Raycast down to find the ground
            // Start from significantly above to ensure we're above any roof
            Vector3 rayStart = startPosition + Vector3.up * spawnGroundCheckHeight;

            // Cast multiple rays to find the LOWEST valid ground (the floor, not the roof)
            RaycastHit[] hits = Physics.RaycastAll(rayStart, Vector3.down, spawnGroundCheckDistance, groundLayerMask);

            if (hits.Length > 0)
            {
                // Find the hit closest to the target Y position (likely the floor)
                RaycastHit bestHit = hits[0];
                float bestDistance = Mathf.Abs(hits[0].point.y - startPosition.y);

                foreach (var hit in hits)
                {
                    float distance = Mathf.Abs(hit.point.y - startPosition.y);
                    // Prefer hits that are at or below the start position Y
                    if (hit.point.y <= startPosition.y + 0.5f && distance < bestDistance)
                    {
                        bestHit = hit;
                        bestDistance = distance;
                    }
                }

                // Place player slightly above the ground
                Vector3 groundedPos = bestHit.point + Vector3.up * 0.1f;

                if (debugSpawning)
                {
                    Debug.Log($"[GameManager] Found {hits.Length} surfaces. Best ground at {bestHit.point.y} (object: {bestHit.collider.name}), placing player at {groundedPos.y}");
                }

                return groundedPos;
            }
            else
            {
                if (debugSpawning)
                {
                    Debug.LogWarning($"[GameManager] No ground found below spawn position {startPosition}! Using original position.");
                }
                return startPosition;
            }
        }

        private void InitializeGame()
        {
            // Initialize graphics to highest quality and native resolution
            InitializeGraphicsSettings();

            // Initialize game systems
            LoadGameData();
        }

        /// <summary>
        /// Initialize graphics settings to optimal defaults.
        /// Sets highest quality level and native fullscreen resolution.
        /// </summary>
        private void InitializeGraphicsSettings()
        {
            // Set to highest quality level
            int highestQuality = QualitySettings.names.Length - 1;
            QualitySettings.SetQualityLevel(highestQuality, true);
            if (debugSpawning) Debug.Log($"[GameManager] Set quality to: {QualitySettings.names[highestQuality]}");

            // Set to native resolution in fullscreen
            Resolution nativeRes = Screen.currentResolution;

            // If current resolution is lower than native, reset to native
            if (Screen.width < nativeRes.width || Screen.height < nativeRes.height || !Screen.fullScreen)
            {
                Screen.SetResolution(nativeRes.width, nativeRes.height, FullScreenMode.FullScreenWindow);
                if (debugSpawning) Debug.Log($"[GameManager] Set resolution to native: {nativeRes.width}x{nativeRes.height} fullscreen");
            }

            // Save these as the defaults
            PlayerPrefs.SetInt("Quality", highestQuality);
            PlayerPrefs.SetInt("Fullscreen", 1);
            PlayerPrefs.Save();
        }

        private void Update()
        {
            HandlePauseInput();
        }

        private void HandlePauseInput()
        {
            if (Input.GetKeyDown(KeyCode.Escape))
            {
                TogglePause();
            }
        }

        public void TogglePause()
        {
            isPaused = !isPaused;
            Time.timeScale = isPaused ? 0f : 1f;

            // Show/hide cursor
            Cursor.lockState = isPaused ? CursorLockMode.None : CursorLockMode.Locked;
            Cursor.visible = isPaused;

            GameEvents.OnPauseToggled?.Invoke();
        }

        public void SetPause(bool pause)
        {
            if (isPaused != pause)
            {
                TogglePause();
            }
        }

        public void UpdateDepth(int depth)
        {
            CurrentDepth = depth;
            if (depth > MaxDepthReached)
            {
                MaxDepthReached = depth;
                GameEvents.OnDepthReached?.Invoke(depth);
                CheckDepthMilestones(depth);
            }
        }

        private void CheckDepthMilestones(int depth)
        {
            // Check for story milestones at certain depths
            int[] milestones = { 10, 25, 40, 50 };
            foreach (int milestone in milestones)
            {
                if (depth >= milestone)
                {
                    // Trigger milestone event (no log)
                }
            }
        }

        public void LoadScene(string sceneName)
        {
            SetPause(false);
            SceneManager.LoadScene(sceneName);
        }

        public void LoadHouseScene()
        {
            LoadScene(houseScene);
        }

        public void LoadBasementScene()
        {
            LoadScene(basementScene);
        }

        public void LoadMainMenu()
        {
            LoadScene(mainMenuScene);
        }

        public void NewGame()
        {
            ResetGameData();
            GameEvents.OnNewGameStarted?.Invoke();
            LoadHouseScene();
        }

        public void SaveGame()
        {
            // Save depth data to PlayerPrefs (legacy)
            SaveGameData();

            // Use SaveManager for full game state save
            if (SaveManager.Instance != null)
            {
                SaveManager.Instance.SaveGame();
            }

            GameEvents.OnGameSaved?.Invoke();
        }

        public void QuitGame()
        {
            SaveGame();
            #if UNITY_EDITOR
            UnityEditor.EditorApplication.isPlaying = false;
            #else
            Application.Quit();
            #endif
        }

        private void SaveGameData()
        {
            PlayerPrefs.SetInt("MaxDepth", MaxDepthReached);
            PlayerPrefs.SetInt("CurrentDepth", CurrentDepth);
            PlayerPrefs.Save();
        }

        private void LoadGameData()
        {
            MaxDepthReached = PlayerPrefs.GetInt("MaxDepth", 0);
            CurrentDepth = PlayerPrefs.GetInt("CurrentDepth", 0);
        }

        private void ResetGameData()
        {
            MaxDepthReached = 0;
            CurrentDepth = 0;
            PlayerPrefs.DeleteAll();

            // Reset static winch lock so New Game starts with winch available
            BeneathTheFloor.Winch.WinchExitTrigger.ResetWinchLock();
        }

        private void OnApplicationQuit()
        {
            SaveGame();
        }
    }
}
