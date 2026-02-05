using System;
using UnityEngine;

namespace BeneathTheFloor.Save
{
    /// <summary>
    /// Lightweight metadata about a save slot for display in UI.
    /// This is stored in the saves index, not in each save file.
    /// </summary>
    [Serializable]
    public class SaveSlotInfo
    {
        /// <summary>
        /// Slot ID (0 = Autosave, 1-5 = Manual saves)
        /// </summary>
        public int slotId;

        /// <summary>
        /// Display name for this save.
        /// Slot 0 always shows "Autosave".
        /// Manual saves show custom name or date/time if no name given.
        /// </summary>
        public string saveName;

        /// <summary>
        /// When this save was last updated.
        /// </summary>
        public string savedAt;

        /// <summary>
        /// Scene the player was in (for preview).
        /// </summary>
        public string playerScene;

        /// <summary>
        /// Current mission index (for preview).
        /// </summary>
        public int missionIndex;

        /// <summary>
        /// Total play time in seconds (for preview).
        /// </summary>
        public float playTime;

        /// <summary>
        /// Whether this slot has data (not empty).
        /// </summary>
        public bool hasData;

        /// <summary>
        /// Create empty slot info.
        /// </summary>
        public SaveSlotInfo()
        {
            slotId = -1;
            saveName = "";
            savedAt = "";
            playerScene = "";
            missionIndex = -1;
            playTime = 0f;
            hasData = false;
        }

        /// <summary>
        /// Create slot info with data.
        /// </summary>
        public SaveSlotInfo(int id, string name, GameSaveData saveData)
        {
            slotId = id;
            saveName = name;
            savedAt = DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss");
            playerScene = saveData?.player?.currentScene ?? "Unknown";
            missionIndex = saveData?.currentMissionIndex ?? -1;
            playTime = saveData?.totalPlayTime ?? 0f;
            hasData = true;
        }

        /// <summary>
        /// Check if this is the autosave slot.
        /// </summary>
        public bool IsAutosave => slotId == 0;

        /// <summary>
        /// Get formatted display name.
        /// </summary>
        public string GetDisplayName()
        {
            if (slotId == 0) return "Autosave";
            if (string.IsNullOrEmpty(saveName)) return $"Save {slotId}";
            return saveName;
        }

        /// <summary>
        /// Get formatted date/time for display.
        /// </summary>
        public string GetFormattedDate()
        {
            if (string.IsNullOrEmpty(savedAt)) return "Empty";

            if (DateTime.TryParse(savedAt, out DateTime date))
            {
                return date.ToString("MMM dd, yyyy - HH:mm");
            }
            return savedAt;
        }

        /// <summary>
        /// Get formatted play time (e.g., "2h 30m").
        /// </summary>
        public string GetFormattedPlayTime()
        {
            if (playTime <= 0) return "0m";

            int totalMinutes = Mathf.FloorToInt(playTime / 60f);
            int hours = totalMinutes / 60;
            int minutes = totalMinutes % 60;

            if (hours > 0)
                return $"{hours}h {minutes}m";
            return $"{minutes}m";
        }

        /// <summary>
        /// Update this slot info from save data.
        /// </summary>
        public void UpdateFromSaveData(string name, GameSaveData saveData)
        {
            saveName = name;
            savedAt = DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss");
            playerScene = saveData?.player?.currentScene ?? "Unknown";
            missionIndex = saveData?.currentMissionIndex ?? -1;
            playTime = saveData?.totalPlayTime ?? 0f;
            hasData = true;
        }
    }
}
