using UnityEngine;
using System;
using System.Collections.Generic;

namespace BeneathTheFloor.Digging
{
    /// <summary>
    /// Tracks tool effectiveness and exhaustion state.
    /// Manages weak dig counts and determines when tools become exhausted.
    /// </summary>
    public class ToolEffectivenessTracker : MonoBehaviour
    {
        [Header("Runtime State (Read-Only)")]
        [SerializeField, Tooltip("Current weak dig count for active tool")]
        private int _currentWeakDigs = 0;

        [SerializeField, Tooltip("Current tool ID being tracked")]
        private string _currentToolId = "";

#pragma warning disable CS0414 // Field is assigned but never used (inspector display)
        [SerializeField, Tooltip("Is current tool exhausted?")]
        private bool _isExhausted = false;
#pragma warning restore CS0414

        // Dictionary to track exhaustion per tool
        private Dictionary<string, int> _toolWeakDigCounts = new Dictionary<string, int>();
        private Dictionary<string, bool> _toolExhaustedStates = new Dictionary<string, bool>();

        // Events
        public static event Action<string> OnToolExhausted;
        public static event Action<string, int, int> OnWeakDigRecorded; // toolId, current, max
        public static event Action<string> OnToolRecovered;

        // Singleton
        private static ToolEffectivenessTracker _instance;
        public static ToolEffectivenessTracker Instance
        {
            get
            {
                if (_instance == null)
                {
                    _instance = FindObjectOfType<ToolEffectivenessTracker>();
                    if (_instance == null)
                    {
                        // Create one if none exists
                        var go = new GameObject("ToolEffectivenessTracker");
                        _instance = go.AddComponent<ToolEffectivenessTracker>();
                        DontDestroyOnLoad(go);
                    }
                }
                return _instance;
            }
        }

        private void Awake()
        {
            if (_instance != null && _instance != this)
            {
                Destroy(gameObject);
                return;
            }
            _instance = this;
        }

        /// <summary>
        /// Get weak dig count for a specific tool.
        /// </summary>
        public int GetWeakDigCount(string toolId)
        {
            if (string.IsNullOrEmpty(toolId)) return 0;
            return _toolWeakDigCounts.TryGetValue(toolId, out int count) ? count : 0;
        }

        /// <summary>
        /// Check if a tool is exhausted.
        /// </summary>
        public bool IsToolExhausted(string toolId)
        {
            if (string.IsNullOrEmpty(toolId)) return false;
            return _toolExhaustedStates.TryGetValue(toolId, out bool exhausted) && exhausted;
        }

        /// <summary>
        /// Record a weak dig for a tool.
        /// Returns true if tool became exhausted from this dig.
        /// </summary>
        public bool RecordWeakDig(string toolId)
        {
            if (string.IsNullOrEmpty(toolId)) return false;

            var config = TerrainLayerDetector.Config;
            int maxWeakDigs = config != null ? config.weakDigsBeforeExhaustion : 8;

            // Initialize if needed
            if (!_toolWeakDigCounts.ContainsKey(toolId))
            {
                _toolWeakDigCounts[toolId] = 0;
                _toolExhaustedStates[toolId] = false;
            }

            // Already exhausted
            if (_toolExhaustedStates[toolId])
            {
                return false;
            }

            // Increment count
            _toolWeakDigCounts[toolId]++;
            int currentCount = _toolWeakDigCounts[toolId];

            // Update inspector display
            _currentToolId = toolId;
            _currentWeakDigs = currentCount;

            // Fire event
            OnWeakDigRecorded?.Invoke(toolId, currentCount, maxWeakDigs);

            Debug.Log($"[ToolEffectivenessTracker] Tool '{toolId}' weak dig: {currentCount}/{maxWeakDigs}");

            // Check for exhaustion
            if (currentCount >= maxWeakDigs)
            {
                _toolExhaustedStates[toolId] = true;
                _isExhausted = true;

                Debug.LogWarning($"[ToolEffectivenessTracker] Tool '{toolId}' is now EXHAUSTED! Upgrade required.");
                OnToolExhausted?.Invoke(toolId);

                return true;
            }

            return false;
        }

        /// <summary>
        /// Reset a tool's exhaustion (call after upgrade).
        /// </summary>
        public void ResetToolExhaustion(string toolId)
        {
            if (string.IsNullOrEmpty(toolId)) return;

            _toolWeakDigCounts[toolId] = 0;
            _toolExhaustedStates[toolId] = false;

            if (_currentToolId == toolId)
            {
                _currentWeakDigs = 0;
                _isExhausted = false;
            }

            Debug.Log($"[ToolEffectivenessTracker] Tool '{toolId}' exhaustion reset.");
            OnToolRecovered?.Invoke(toolId);
        }

        /// <summary>
        /// Reset all tool exhaustion states.
        /// </summary>
        public void ResetAllTools()
        {
            _toolWeakDigCounts.Clear();
            _toolExhaustedStates.Clear();
            _currentWeakDigs = 0;
            _currentToolId = "";
            _isExhausted = false;

            Debug.Log("[ToolEffectivenessTracker] All tool exhaustion states reset.");
        }

        /// <summary>
        /// Get exhaustion progress (0-1) for a tool.
        /// </summary>
        public float GetExhaustionProgress(string toolId)
        {
            if (string.IsNullOrEmpty(toolId)) return 0f;

            var config = TerrainLayerDetector.Config;
            int maxWeakDigs = config != null ? config.weakDigsBeforeExhaustion : 8;
            int currentCount = GetWeakDigCount(toolId);

            return Mathf.Clamp01((float)currentCount / maxWeakDigs);
        }

        /// <summary>
        /// Get remaining weak digs before exhaustion.
        /// </summary>
        public int GetRemainingWeakDigs(string toolId)
        {
            if (string.IsNullOrEmpty(toolId)) return 0;

            var config = TerrainLayerDetector.Config;
            int maxWeakDigs = config != null ? config.weakDigsBeforeExhaustion : 8;
            int currentCount = GetWeakDigCount(toolId);

            return Mathf.Max(0, maxWeakDigs - currentCount);
        }

        // ===== Save/Load Support =====

        [System.Serializable]
        public class ToolExhaustionSaveData
        {
            public List<string> toolIds = new List<string>();
            public List<int> weakDigCounts = new List<int>();
            public List<bool> exhaustedStates = new List<bool>();
        }

        /// <summary>
        /// Get save data for all tool exhaustion states.
        /// </summary>
        public ToolExhaustionSaveData GetSaveData()
        {
            var data = new ToolExhaustionSaveData();

            foreach (var kvp in _toolWeakDigCounts)
            {
                data.toolIds.Add(kvp.Key);
                data.weakDigCounts.Add(kvp.Value);
                data.exhaustedStates.Add(_toolExhaustedStates.TryGetValue(kvp.Key, out bool exhausted) && exhausted);
            }

            return data;
        }

        /// <summary>
        /// Load save data for all tool exhaustion states.
        /// </summary>
        public void LoadSaveData(ToolExhaustionSaveData data)
        {
            if (data == null) return;

            _toolWeakDigCounts.Clear();
            _toolExhaustedStates.Clear();

            for (int i = 0; i < data.toolIds.Count; i++)
            {
                string toolId = data.toolIds[i];
                _toolWeakDigCounts[toolId] = data.weakDigCounts[i];
                _toolExhaustedStates[toolId] = data.exhaustedStates[i];
            }

            Debug.Log($"[ToolEffectivenessTracker] Loaded exhaustion data for {data.toolIds.Count} tools.");
        }
    }
}
