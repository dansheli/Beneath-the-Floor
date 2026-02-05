using System;
using System.Collections.Generic;
using System.IO;
using UnityEngine;

namespace BeneathTheFloor.Save
{
    /// <summary>
    /// Manages the index of all save slots and tracks the active save slot.
    /// The active slot determines where autosaves go.
    /// </summary>
    [Serializable]
    public class SavesIndex
    {
        public const int MAX_SLOTS = 6; // 0 = Autosave, 1-5 = Manual saves
        public const int AUTOSAVE_SLOT = 0;

        private const string INDEX_FILE_NAME = "saves_index.json";

        /// <summary>
        /// The currently active save slot. Autosaves go to this slot.
        /// Defaults to 0 (Autosave slot).
        /// </summary>
        public int activeSlotId = AUTOSAVE_SLOT;

        /// <summary>
        /// Metadata for all save slots.
        /// </summary>
        public List<SaveSlotInfo> slots = new List<SaveSlotInfo>();

        /// <summary>
        /// Version for future migrations.
        /// </summary>
        public int version = 1;

        private static string IndexPath => Path.Combine(Application.persistentDataPath, INDEX_FILE_NAME);

        /// <summary>
        /// Create a new saves index with empty slots.
        /// </summary>
        public SavesIndex()
        {
            InitializeEmptySlots();
        }

        /// <summary>
        /// Initialize all slots as empty.
        /// </summary>
        private void InitializeEmptySlots()
        {
            slots.Clear();
            for (int i = 0; i < MAX_SLOTS; i++)
            {
                var slot = new SaveSlotInfo
                {
                    slotId = i,
                    saveName = i == AUTOSAVE_SLOT ? "Autosave" : "",
                    hasData = false
                };
                slots.Add(slot);
            }
        }

        /// <summary>
        /// Get slot info by ID.
        /// </summary>
        public SaveSlotInfo GetSlot(int slotId)
        {
            if (slotId < 0 || slotId >= MAX_SLOTS) return null;

            // Ensure slots list is properly sized
            while (slots.Count < MAX_SLOTS)
            {
                slots.Add(new SaveSlotInfo { slotId = slots.Count });
            }

            return slots[slotId];
        }

        /// <summary>
        /// Get all slots that have save data.
        /// </summary>
        public List<SaveSlotInfo> GetOccupiedSlots()
        {
            var occupied = new List<SaveSlotInfo>();
            foreach (var slot in slots)
            {
                if (slot.hasData)
                {
                    occupied.Add(slot);
                }
            }
            return occupied;
        }

        /// <summary>
        /// Get all empty slots (excluding autosave).
        /// </summary>
        public List<SaveSlotInfo> GetEmptyManualSlots()
        {
            var empty = new List<SaveSlotInfo>();
            for (int i = 1; i < MAX_SLOTS; i++)
            {
                if (i < slots.Count && !slots[i].hasData)
                {
                    empty.Add(slots[i]);
                }
            }
            return empty;
        }

        /// <summary>
        /// Find the first available empty manual slot.
        /// Returns -1 if all slots are full.
        /// </summary>
        public int FindFirstEmptyManualSlot()
        {
            for (int i = 1; i < MAX_SLOTS; i++)
            {
                if (i < slots.Count && !slots[i].hasData)
                {
                    return i;
                }
            }
            return -1;
        }

        /// <summary>
        /// Check if there are any saves (excluding or including autosave).
        /// </summary>
        public bool HasAnySaves(bool includeAutosave = true)
        {
            int startIndex = includeAutosave ? 0 : 1;
            for (int i = startIndex; i < slots.Count; i++)
            {
                if (slots[i].hasData) return true;
            }
            return false;
        }

        /// <summary>
        /// Update slot info after saving.
        /// </summary>
        public void UpdateSlot(int slotId, string saveName, GameSaveData saveData)
        {
            if (slotId < 0 || slotId >= MAX_SLOTS) return;

            var slot = GetSlot(slotId);
            if (slot != null)
            {
                slot.UpdateFromSaveData(saveName, saveData);
            }
        }

        /// <summary>
        /// Clear a slot (mark as empty).
        /// </summary>
        public void ClearSlot(int slotId)
        {
            if (slotId < 0 || slotId >= MAX_SLOTS) return;

            var slot = GetSlot(slotId);
            if (slot != null)
            {
                slot.saveName = slotId == AUTOSAVE_SLOT ? "Autosave" : "";
                slot.savedAt = "";
                slot.playerScene = "";
                slot.missionIndex = -1;
                slot.playTime = 0f;
                slot.hasData = false;
            }
        }

        /// <summary>
        /// Set the active save slot. Autosaves will go to this slot.
        /// </summary>
        public void SetActiveSlot(int slotId)
        {
            if (slotId >= 0 && slotId < MAX_SLOTS)
            {
                activeSlotId = slotId;
            }
        }

        /// <summary>
        /// Get the currently active slot info.
        /// </summary>
        public SaveSlotInfo GetActiveSlot()
        {
            return GetSlot(activeSlotId);
        }

        /// <summary>
        /// Check if a slot is the currently active slot.
        /// </summary>
        public bool IsActiveSlot(int slotId)
        {
            return slotId == activeSlotId;
        }

        #region File I/O

        /// <summary>
        /// Load the saves index from disk, or create a new one if it doesn't exist.
        /// </summary>
        public static SavesIndex Load()
        {
            if (!File.Exists(IndexPath))
            {
                Debug.Log("[SavesIndex] No index file found, creating new index");
                var newIndex = new SavesIndex();

                // Check for legacy save file migration
                newIndex.MigrateLegacySave();

                return newIndex;
            }

            try
            {
                string json = File.ReadAllText(IndexPath);
                var index = JsonUtility.FromJson<SavesIndex>(json);

                if (index == null)
                {
                    Debug.LogWarning("[SavesIndex] Failed to parse index, creating new one");
                    return new SavesIndex();
                }

                // Ensure all slots exist
                while (index.slots.Count < MAX_SLOTS)
                {
                    index.slots.Add(new SaveSlotInfo { slotId = index.slots.Count });
                }

                Debug.Log($"[SavesIndex] Loaded index with active slot {index.activeSlotId}");
                return index;
            }
            catch (Exception e)
            {
                Debug.LogError($"[SavesIndex] Error loading index: {e.Message}");
                return new SavesIndex();
            }
        }

        /// <summary>
        /// Save the index to disk.
        /// </summary>
        public void Save()
        {
            try
            {
                string json = JsonUtility.ToJson(this, true);
                File.WriteAllText(IndexPath, json);
            }
            catch (Exception e)
            {
                Debug.LogError($"[SavesIndex] Error saving index: {e.Message}");
            }
        }

        /// <summary>
        /// Migrate legacy gamesave.json to slot 0 (Autosave).
        /// </summary>
        private void MigrateLegacySave()
        {
            string legacyPath = Path.Combine(Application.persistentDataPath, "gamesave.json");

            if (!File.Exists(legacyPath))
            {
                return;
            }

            try
            {
                // Read legacy save
                string json = File.ReadAllText(legacyPath);
                var legacyData = JsonUtility.FromJson<GameSaveData>(json);

                if (legacyData != null && legacyData.IsValid())
                {
                    // Copy to slot 0 path
                    string slot0Path = GetSaveFilePath(AUTOSAVE_SLOT);
                    File.WriteAllText(slot0Path, json);

                    // Update slot info
                    var slot = GetSlot(AUTOSAVE_SLOT);
                    slot.UpdateFromSaveData("Autosave", legacyData);

                    // Rename legacy file as backup
                    string backupPath = legacyPath + ".backup";
                    if (File.Exists(backupPath))
                    {
                        File.Delete(backupPath);
                    }
                    File.Move(legacyPath, backupPath);

                    Debug.Log("[SavesIndex] Migrated legacy save to slot 0");

                    // Save the updated index
                    Save();
                }
            }
            catch (Exception e)
            {
                Debug.LogError($"[SavesIndex] Error migrating legacy save: {e.Message}");
            }
        }

        /// <summary>
        /// Get the file path for a save slot.
        /// </summary>
        public static string GetSaveFilePath(int slotId)
        {
            return Path.Combine(Application.persistentDataPath, $"save_slot_{slotId}.json");
        }

        /// <summary>
        /// Check if a save file exists for a slot.
        /// </summary>
        public static bool SaveFileExists(int slotId)
        {
            return File.Exists(GetSaveFilePath(slotId));
        }

        /// <summary>
        /// Delete the save file for a slot.
        /// </summary>
        public static bool DeleteSaveFile(int slotId)
        {
            string path = GetSaveFilePath(slotId);
            if (File.Exists(path))
            {
                try
                {
                    File.Delete(path);
                    return true;
                }
                catch (Exception e)
                {
                    Debug.LogError($"[SavesIndex] Error deleting save file: {e.Message}");
                    return false;
                }
            }
            return true;
        }

        #endregion
    }
}
