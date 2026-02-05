using UnityEngine;
using System;
using System.Collections.Generic;
using System.Linq;

namespace BeneathTheFloor.TreasureChests
{
    /// <summary>
    /// Singleton manager for all treasure chests in the scene.
    /// Provides radar integration and save/load functionality.
    /// </summary>
    public class TreasureChestManager : MonoBehaviour
    {
        public static TreasureChestManager Instance { get; private set; }

        [Header("Settings")]
        [Tooltip("Enable debug logging")]
        [SerializeField] private bool debugMode = false;

        [Header("Radar Integration")]
        [Tooltip("If true, treasure chests will appear on the radar alongside resource nodes")]
        [SerializeField] private bool enableRadarDetection = true;

        [Tooltip("Priority multiplier for radar detection (higher = shows up before nodes at same distance)")]
        [Range(0.1f, 10f)]
        [SerializeField] private float radarPriorityMultiplier = 1.5f;

        // Registered chests
        private List<BuriedTreasureChest> registeredChests = new List<BuriedTreasureChest>();

        // Save data - IDs of opened chests
        private HashSet<string> openedChestIds = new HashSet<string>();

        // Events
        public event Action<BuriedTreasureChest> OnChestRegistered;
        public event Action<BuriedTreasureChest> OnChestUnregistered;
        public event Action<BuriedTreasureChest> OnChestRevealed;
        public event Action<BuriedTreasureChest, TreasureChestReward[]> OnChestOpened;

        // Properties
        public bool EnableRadarDetection => enableRadarDetection;
        public float RadarPriorityMultiplier => radarPriorityMultiplier;
        public int RegisteredChestCount => registeredChests.Count;
        public int UnopenedChestCount => registeredChests.Count(c => !c.IsOpened);

        private void Awake()
        {
            if (Instance == null)
            {
                Instance = this;

                // DontDestroyOnLoad only works on root GameObjects
                if (transform.parent != null)
                {
                    transform.SetParent(null);
                }
                DontDestroyOnLoad(gameObject);

                if (debugMode)
                {
                    Debug.Log("[TreasureChestManager] Initialized");
                }
            }
            else
            {
                Destroy(gameObject);
            }
        }

        private void OnDestroy()
        {
            if (Instance == this)
            {
                Instance = null;
            }
        }

        #region Chest Registration

        /// <summary>
        /// Register a treasure chest with the manager.
        /// Called automatically by BuriedTreasureChest.Start().
        /// </summary>
        public void RegisterChest(BuriedTreasureChest chest)
        {
            if (chest == null || registeredChests.Contains(chest))
                return;

            registeredChests.Add(chest);

            // Check if this chest was previously opened
            if (!string.IsNullOrEmpty(chest.ChestId) && openedChestIds.Contains(chest.ChestId))
            {
                chest.SetOpenedFromSave();
            }

            if (debugMode)
            {
                Debug.Log($"[TreasureChestManager] Registered chest: {chest.ChestId} at {chest.transform.position}");
            }

            OnChestRegistered?.Invoke(chest);
        }

        /// <summary>
        /// Unregister a treasure chest.
        /// Called automatically when chest is destroyed.
        /// </summary>
        public void UnregisterChest(BuriedTreasureChest chest)
        {
            if (chest == null || !registeredChests.Contains(chest))
                return;

            registeredChests.Remove(chest);

            if (debugMode)
            {
                Debug.Log($"[TreasureChestManager] Unregistered chest: {chest.ChestId}");
            }

            OnChestUnregistered?.Invoke(chest);
        }

        /// <summary>
        /// Notify manager that a chest has been revealed (uncovered).
        /// </summary>
        public void NotifyChestRevealed(BuriedTreasureChest chest)
        {
            if (debugMode)
            {
                Debug.Log($"[TreasureChestManager] Chest revealed: {chest.ChestId}");
            }

            OnChestRevealed?.Invoke(chest);
        }

        /// <summary>
        /// Notify manager that a chest has been opened.
        /// </summary>
        public void NotifyChestOpened(BuriedTreasureChest chest, TreasureChestReward[] rewards)
        {
            if (!string.IsNullOrEmpty(chest.ChestId))
            {
                openedChestIds.Add(chest.ChestId);
            }

            if (debugMode)
            {
                Debug.Log($"[TreasureChestManager] Chest opened: {chest.ChestId}, Rewards: {rewards?.Length ?? 0}");
            }

            OnChestOpened?.Invoke(chest, rewards);
        }

        #endregion

        #region Radar Integration

        /// <summary>
        /// Get the nearest unopened and revealed treasure chest within range.
        /// Used by radar system.
        /// </summary>
        /// <param name="fromPosition">Position to measure distance from</param>
        /// <param name="maxRange">Maximum detection range</param>
        /// <returns>Nearest chest or null if none found</returns>
        public BuriedTreasureChest GetNearestUnopenedChest(Vector3 fromPosition, float maxRange)
        {
            if (!enableRadarDetection)
                return null;

            BuriedTreasureChest nearest = null;
            float nearestDist = float.MaxValue;

            foreach (var chest in registeredChests)
            {
                if (chest == null || chest.IsOpened)
                    continue;

                // Only detect if chest has radar detection enabled
                if (!chest.EnableRadarDetection)
                    continue;

                float dist = Vector3.Distance(fromPosition, chest.transform.position);

                // Apply chest-specific range multiplier
                float effectiveRange = maxRange * chest.RadarRangeMultiplier;

                if (dist <= effectiveRange && dist < nearestDist)
                {
                    nearest = chest;
                    nearestDist = dist;
                }
            }

            return nearest;
        }

        /// <summary>
        /// Get all chests within range, sorted by effective distance (considering priority).
        /// </summary>
        public List<BuriedTreasureChest> GetChestsInRange(Vector3 fromPosition, float maxRange)
        {
            var result = new List<BuriedTreasureChest>();

            foreach (var chest in registeredChests)
            {
                if (chest == null || chest.IsOpened)
                    continue;

                if (!chest.EnableRadarDetection)
                    continue;

                float dist = Vector3.Distance(fromPosition, chest.transform.position);
                float effectiveRange = maxRange * chest.RadarRangeMultiplier;

                if (dist <= effectiveRange)
                {
                    result.Add(chest);
                }
            }

            // Sort by effective distance (closer = higher priority)
            result.Sort((a, b) =>
            {
                float distA = Vector3.Distance(fromPosition, a.transform.position) / a.RadarPriorityMultiplier;
                float distB = Vector3.Distance(fromPosition, b.transform.position) / b.RadarPriorityMultiplier;
                return distA.CompareTo(distB);
            });

            return result;
        }

        /// <summary>
        /// Check if there's a treasure chest closer than a given distance.
        /// Used for radar priority comparison with resource nodes.
        /// </summary>
        public bool HasCloserChestThan(Vector3 fromPosition, float nodeDistance, float maxRange)
        {
            if (!enableRadarDetection)
                return false;

            foreach (var chest in registeredChests)
            {
                if (chest == null || chest.IsOpened)
                    continue;

                if (!chest.EnableRadarDetection)
                    continue;

                float dist = Vector3.Distance(fromPosition, chest.transform.position);
                float effectiveRange = maxRange * chest.RadarRangeMultiplier;

                if (dist <= effectiveRange)
                {
                    // Apply priority multiplier - lower effective distance = higher priority
                    float effectiveDist = dist / (radarPriorityMultiplier * chest.RadarPriorityMultiplier);

                    if (effectiveDist < nodeDistance)
                        return true;
                }
            }

            return false;
        }

        #endregion

        #region Save/Load

        /// <summary>
        /// Get save data for all opened chests.
        /// </summary>
        public TreasureChestSaveData GetSaveData()
        {
            return new TreasureChestSaveData
            {
                openedChestIds = openedChestIds.ToArray()
            };
        }

        /// <summary>
        /// Load save data.
        /// </summary>
        public void LoadSaveData(TreasureChestSaveData data)
        {
            openedChestIds.Clear();

            if (data?.openedChestIds != null)
            {
                foreach (var id in data.openedChestIds)
                {
                    openedChestIds.Add(id);
                }
            }

            // Update any already-registered chests
            foreach (var chest in registeredChests)
            {
                if (!string.IsNullOrEmpty(chest.ChestId) && openedChestIds.Contains(chest.ChestId))
                {
                    chest.SetOpenedFromSave();
                }
            }

            if (debugMode)
            {
                Debug.Log($"[TreasureChestManager] Loaded {openedChestIds.Count} opened chest IDs");
            }
        }

        /// <summary>
        /// Reset all chest states (for new game).
        /// </summary>
        public void ResetAllChests()
        {
            openedChestIds.Clear();

            foreach (var chest in registeredChests)
            {
                chest.ResetChest();
            }

            if (debugMode)
            {
                Debug.Log("[TreasureChestManager] Reset all chests");
            }
        }

        #endregion

        #region Debug

        /// <summary>
        /// Get all registered chests (for debugging/editor).
        /// </summary>
        public List<BuriedTreasureChest> GetAllChests()
        {
            return new List<BuriedTreasureChest>(registeredChests);
        }

        /// <summary>
        /// Force reveal all chests (debug only).
        /// </summary>
        [ContextMenu("Debug: Reveal All Chests")]
        public void DebugRevealAllChests()
        {
            foreach (var chest in registeredChests)
            {
                if (chest != null && !chest.IsRevealed)
                {
                    chest.ForceReveal();
                }
            }

            Debug.Log($"[TreasureChestManager] Revealed {registeredChests.Count} chests");
        }

        #endregion
    }

    /// <summary>
    /// Save data structure for treasure chests.
    /// </summary>
    [Serializable]
    public class TreasureChestSaveData
    {
        public string[] openedChestIds;
    }
}
