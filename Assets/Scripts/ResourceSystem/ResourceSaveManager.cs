using UnityEngine;
using System;
using System.IO;
using System.Collections.Generic;
using BeneathTheFloor.GameFlow;

namespace BeneathTheFloor.ResourceSystem
{
    /// <summary>
    /// Handles saving and loading of resource system data.
    /// Saves dust counter and broken node states.
    /// </summary>
    public class ResourceSaveManager : MonoBehaviour
    {
        public static ResourceSaveManager Instance { get; private set; }

        [Header("Settings")]
        [SerializeField] private string saveFileName = "resources_v3.json";
        [SerializeField] private bool autoSaveOnQuit = true;

        [Header("Debug")]
        [SerializeField] private bool enableDebugLogs = false;

        private string SavePath => Path.Combine(Application.persistentDataPath, saveFileName);

        private void Awake()
        {
            if (Instance != null && Instance != this)
            {
                Destroy(gameObject);
                return;
            }
            Instance = this;

            if (transform.parent != null)
            {
                transform.SetParent(null);
            }
            DontDestroyOnLoad(gameObject);

            // Register for application quit - more reliable than OnApplicationQuit
            Application.wantsToQuit += OnWantsToQuit;
        }

        private void Start()
        {
            // Check if this is a new game - if so, delete the save file and don't load
            int newGameFlag = PlayerPrefs.GetInt(IntroCinematicController.NEW_GAME_FLAG_KEY, 0);
            if (newGameFlag == 1)
            {
                if (enableDebugLogs)
                    Debug.Log("[ResourceSaveManager] New game detected - deleting resource save file.");
                DeleteSave();
                return; // Don't load old data
            }

            // Attempt to load on start (only for continuing games)
            Load();
        }

        private void OnApplicationQuit()
        {
            if (autoSaveOnQuit && ShouldSaveOnQuit())
            {
                Save();
            }
        }

        private void OnApplicationPause(bool pause)
        {
            // Save when app is paused (mobile)
            if (pause && autoSaveOnQuit)
            {
                Save();
            }
        }

        private void OnDestroy()
        {
            if (Instance == this)
            {
                Application.wantsToQuit -= OnWantsToQuit;
            }
        }

        /// <summary>
        /// Called when application is about to quit. More reliable than OnApplicationQuit.
        /// </summary>
        private bool OnWantsToQuit()
        {
            if (autoSaveOnQuit && ShouldSaveOnQuit())
            {
                if (enableDebugLogs)
                    Debug.Log("[ResourceSaveManager] Application wants to quit - saving...");
                Save();
            }
            return true; // Allow quit to proceed
        }

        /// <summary>
        /// Check if we should save on quit (only in gameplay scenes).
        /// </summary>
        private bool ShouldSaveOnQuit()
        {
            string currentScene = UnityEngine.SceneManagement.SceneManager.GetActiveScene().name;

            // Don't save from main menu or press any key scenes
            if (currentScene.Contains("MainMenu") || currentScene.Contains("PressAnyKey"))
            {
                return false;
            }

            // Only save if there's a player (we're in gameplay)
            return GameObject.FindGameObjectWithTag("Player") != null;
        }

        /// <summary>
        /// Save all resource system data.
        /// </summary>
        public void Save()
        {
            try
            {
                var saveData = new ResourceSystemSaveData();

                // Save dust
                if (DustManager.Instance != null)
                {
                    saveData.dustAmount = DustManager.Instance.GetDust();
                }

                // Save nodes
                if (HiddenNodeManager.Instance != null)
                {
                    saveData.worldSeed = HiddenNodeManager.Instance.GetWorldSeed();
                    saveData.chunkNodes = HiddenNodeManager.Instance.GetSaveData();
                }

                saveData.savedAt = DateTime.Now.ToString("o");
                saveData.version = 1;

                // Serialize and write
                string json = JsonUtility.ToJson(saveData, true);
                File.WriteAllText(SavePath, json);

                if (enableDebugLogs)
                {
                    Debug.Log($"[ResourceSaveManager] Saved to {SavePath}");
                    Debug.Log($"[ResourceSaveManager] Dust: {saveData.dustAmount:F1}, Broken chunks: {saveData.chunkNodes?.Count ?? 0}");
                }
            }
            catch (Exception e)
            {
                Debug.LogError($"[ResourceSaveManager] Save failed: {e.Message}");
            }
        }

        /// <summary>
        /// Load resource system data.
        /// </summary>
        public bool Load()
        {
            if (!File.Exists(SavePath))
            {
                if (enableDebugLogs)
                    Debug.Log($"[ResourceSaveManager] No save file found at {SavePath}");
                return false;
            }

            try
            {
                string json = File.ReadAllText(SavePath);
                var saveData = JsonUtility.FromJson<ResourceSystemSaveData>(json);

                if (saveData == null)
                {
                    Debug.LogWarning("[ResourceSaveManager] Failed to parse save data.");
                    return false;
                }

                // Load dust
                if (DustManager.Instance != null)
                {
                    DustManager.Instance.SetDust(saveData.dustAmount);
                }

                // Load nodes
                if (HiddenNodeManager.Instance != null)
                {
                    HiddenNodeManager.Instance.LoadSaveData(saveData.chunkNodes, saveData.worldSeed);
                }

                if (enableDebugLogs)
                {
                    Debug.Log($"[ResourceSaveManager] Loaded from {SavePath}");
                    Debug.Log($"[ResourceSaveManager] Dust: {saveData.dustAmount:F1}, Broken chunks: {saveData.chunkNodes?.Count ?? 0}");
                }

                return true;
            }
            catch (Exception e)
            {
                Debug.LogError($"[ResourceSaveManager] Load failed: {e.Message}");
                return false;
            }
        }

        /// <summary>
        /// Delete save file.
        /// </summary>
        public void DeleteSave()
        {
            if (File.Exists(SavePath))
            {
                File.Delete(SavePath);
                if (enableDebugLogs)
                    Debug.Log($"[ResourceSaveManager] Deleted save at {SavePath}");
            }
        }

        /// <summary>
        /// Check if save file exists.
        /// </summary>
        public bool SaveExists()
        {
            return File.Exists(SavePath);
        }
    }

    /// <summary>
    /// Save data structure for resource system.
    /// </summary>
    [Serializable]
    public class ResourceSystemSaveData
    {
        public int version = 1;
        public string savedAt;
        public float dustAmount;
        public int worldSeed;
        public List<ChunkNodesSaveData> chunkNodes;
    }
}
