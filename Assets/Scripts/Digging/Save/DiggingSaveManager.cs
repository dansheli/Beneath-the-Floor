using UnityEngine;
using System.IO;

namespace BeneathTheFloor.Digging
{
    /// <summary>
    /// Handles saving and loading of voxel terrain data.
    /// Integration point for the main SaveManager.
    /// </summary>
    public static class DiggingSaveManager
    {
        private static TerrainSaveData currentSaveData;

        /// <summary>
        /// Get or set the current save data in memory.
        /// </summary>
        public static TerrainSaveData CurrentSave
        {
            get => currentSaveData;
            set => currentSaveData = value;
        }

        /// <summary>
        /// Default save file name for terrain.
        /// </summary>
        public const string DefaultFileName = "terrain.json";

        /// <summary>
        /// Get the default save path.
        /// </summary>
        public static string GetDefaultSavePath()
        {
            return Path.Combine(Application.persistentDataPath, DefaultFileName);
        }

        /// <summary>
        /// Capture current terrain state into CurrentSave.
        /// </summary>
        public static bool CaptureCurrentTerrain()
        {
            // Find the ChunkManager in the scene
            var chunkManager = Object.FindObjectOfType<ChunkManager>();
            if (chunkManager == null)
            {
                Debug.LogWarning("[DiggingSaveManager] No ChunkManager found in scene - nothing to save");
                return false;
            }

            currentSaveData = chunkManager.CaptureSaveData();
            return currentSaveData != null;
        }

        /// <summary>
        /// Apply CurrentSave to the terrain.
        /// </summary>
        public static bool ApplyToTerrain()
        {
            if (currentSaveData == null)
            {
                Debug.LogWarning("[DiggingSaveManager] No save data to apply!");
                return false;
            }

            // Find the ChunkManager in the scene
            var chunkManager = Object.FindObjectOfType<ChunkManager>();
            if (chunkManager == null)
            {
                Debug.LogError("[DiggingSaveManager] No ChunkManager found in scene!");
                return false;
            }

            chunkManager.ApplySaveData(currentSaveData);
            return true;
        }

        /// <summary>
        /// Save terrain data to a JSON file.
        /// </summary>
        public static bool SaveToJsonFile(string path = null)
        {
            if (string.IsNullOrEmpty(path))
                path = GetDefaultSavePath();

            // Capture current state if not already captured
            if (currentSaveData == null)
            {
                if (!CaptureCurrentTerrain())
                    return false;
            }

            try
            {
                // Convert to JSON
                string json = JsonUtility.ToJson(currentSaveData, true);

                // Ensure directory exists
                string directory = Path.GetDirectoryName(path);
                if (!string.IsNullOrEmpty(directory) && !Directory.Exists(directory))
                {
                    Directory.CreateDirectory(directory);
                }

                // Write file
                File.WriteAllText(path, json);

                return true;
            }
            catch (System.Exception e)
            {
                Debug.LogError($"[DiggingSaveManager] Failed to save: {e.Message}");
                return false;
            }
        }

        /// <summary>
        /// Load terrain data from a JSON file.
        /// </summary>
        public static bool LoadFromJsonFile(string path = null)
        {
            if (string.IsNullOrEmpty(path))
                path = GetDefaultSavePath();

            if (!File.Exists(path))
            {
                return false;
            }

            try
            {
                // Read file
                string json = File.ReadAllText(path);

                // Parse JSON
                currentSaveData = JsonUtility.FromJson<TerrainSaveData>(json);

                if (currentSaveData == null || !currentSaveData.IsValid())
                {
                    Debug.LogError("[DiggingSaveManager] Loaded data is invalid!");
                    currentSaveData = null;
                    return false;
                }

                return true;
            }
            catch (System.Exception e)
            {
                Debug.LogError($"[DiggingSaveManager] Failed to load: {e.Message}");
                return false;
            }
        }

        /// <summary>
        /// Check if a save file exists.
        /// </summary>
        public static bool SaveFileExists(string path = null)
        {
            if (string.IsNullOrEmpty(path))
                path = GetDefaultSavePath();

            return File.Exists(path);
        }

        /// <summary>
        /// Delete the save file.
        /// </summary>
        public static bool DeleteSaveFile(string path = null)
        {
            if (string.IsNullOrEmpty(path))
                path = GetDefaultSavePath();

            if (File.Exists(path))
            {
                try
                {
                    File.Delete(path);
                    return true;
                }
                catch (System.Exception e)
                {
                    Debug.LogError($"[DiggingSaveManager] Failed to delete: {e.Message}");
                    return false;
                }
            }

            return false;
        }

        /// <summary>
        /// Clear the current in-memory save data.
        /// </summary>
        public static void ClearCurrentSave()
        {
            currentSaveData = null;
        }

        /// <summary>
        /// Integration point: Call this from your main save system.
        /// </summary>
        public static void OnGameSave()
        {
            CaptureCurrentTerrain();
            SaveToJsonFile();
        }

        /// <summary>
        /// Integration point: Call this from your main load system.
        /// </summary>
        public static void OnGameLoad()
        {
            if (LoadFromJsonFile())
            {
                ApplyToTerrain();
            }
        }
    }

#if UNITY_EDITOR
    /// <summary>
    /// Editor utilities for save/load testing.
    /// </summary>
    public static class DiggingSaveManagerEditor
    {
        [UnityEditor.MenuItem("Tools/Digging/Save Terrain Now")]
        public static void SaveTerrainNow()
        {
            if (!Application.isPlaying)
            {
                Debug.LogWarning("[DiggingSaveManager] Must be in play mode to save terrain!");
                return;
            }

            DiggingSaveManager.OnGameSave();
        }

        [UnityEditor.MenuItem("Tools/Digging/Load Terrain Now")]
        public static void LoadTerrainNow()
        {
            if (!Application.isPlaying)
            {
                Debug.LogWarning("[DiggingSaveManager] Must be in play mode to load terrain!");
                return;
            }

            DiggingSaveManager.OnGameLoad();
        }

        [UnityEditor.MenuItem("Tools/Digging/Delete Save File")]
        public static void DeleteSaveFile()
        {
            DiggingSaveManager.DeleteSaveFile();
        }

        [UnityEditor.MenuItem("Tools/Digging/Open Save Folder")]
        public static void OpenSaveFolder()
        {
            string path = Application.persistentDataPath;
            Debug.Log($"[DiggingSaveManager] Save folder: {path}");

#if UNITY_EDITOR_WIN
            System.Diagnostics.Process.Start("explorer.exe", path.Replace("/", "\\"));
#elif UNITY_EDITOR_OSX
            System.Diagnostics.Process.Start("open", path);
#endif
        }
    }
#endif
}
