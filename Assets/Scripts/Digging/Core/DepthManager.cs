using UnityEngine;
using System;

namespace BeneathTheFloor.Digging
{
    /// <summary>
    /// Central manager for tracking player height/depth.
    /// Tracks TWO separate values:
    /// - HeightRelativeToBasement: For HUD display (can be + or -, 0 = basement floor)
    /// - DepthBelowSoil: For loot/layers (>= 0, only counts after soil offset)
    /// </summary>
    public class DepthManager : MonoBehaviour
    {
        [Header("References")]
        [Tooltip("Reference to the basement floor transform (optional). If null, uses basementFloorY.")]
        [SerializeField] private Transform basementFloorRef;

        [Tooltip("Reference to the player/camera transform for continuous tracking. Auto-found if null.")]
        [SerializeField] private Transform playerTransform;

        [Header("Configuration")]
        [Tooltip("The Y position of the basement floor (surface level). Used if basementFloorRef is null.")]
        [SerializeField] private float basementFloorY = -3.0f;

        [Tooltip("Depth offset before soil/loot layers begin. Players must dig this far before resources appear.")]
        [SerializeField] private float soilStartOffsetMeters = 5f;

        [Tooltip("Minimum change to trigger events.")]
        [SerializeField] private float changeThreshold = 0.01f;

        [Header("Debug")]
        [Tooltip("Enable debug logging.")]
        [SerializeField] private bool enableDebugLogs = false;

        [Header("Current State (Read Only)")]
        [SerializeField] private float _heightRelativeToBasement;
        [SerializeField] private float _depthBelowSoil;
        [SerializeField] private float _maxDepthBelowSoil;

        // Tracking state
        private float _lastNotifiedHeight;
        private float _lastNotifiedDepthBelowSoil;

        /// <summary>
        /// Height relative to basement floor surface.
        /// - 0 = standing on basement floor
        /// - Positive = above basement (jumping)
        /// - Negative = below basement (digging down)
        /// This is what the HUD should display.
        /// </summary>
        public float HeightRelativeToBasement => _heightRelativeToBasement;

        /// <summary>
        /// Depth below the soil surface (basement + soil offset).
        /// - Always >= 0
        /// - 0 when above or at the soil surface
        /// - Positive when below soil surface
        /// This is what loot/layer systems should use.
        /// </summary>
        public float DepthBelowSoil => _depthBelowSoil;

        /// <summary>
        /// Maximum depth below soil ever reached (for stats/achievements).
        /// </summary>
        public float MaxDepthBelowSoil => _maxDepthBelowSoil;

        /// <summary>
        /// The Y position of the basement floor surface.
        /// </summary>
        public float BasementFloorY => GetBasementFloorY();

        /// <summary>
        /// The depth offset before soil/loot layers begin.
        /// </summary>
        public float SoilStartOffset => soilStartOffsetMeters;

        /// <summary>
        /// Returns true if player has dug past the soil start offset and can find resources.
        /// </summary>
        public bool IsInSoilZone => _depthBelowSoil > 0f;

        // Legacy compatibility properties
        public float CurrentRawDepthMeters => Mathf.Max(0f, -_heightRelativeToBasement);
        public float CurrentAdjustedDepthMeters => _depthBelowSoil;
        public float CurrentDepthMeters => _depthBelowSoil;
        public float MaxRawDepthMeters => _maxDepthBelowSoil + soilStartOffsetMeters;
        public float MaxAdjustedDepthMeters => _maxDepthBelowSoil;

        /// <summary>
        /// Event fired when height relative to basement changes.
        /// Parameter is the height value (can be + or -).
        /// Subscribe to this for HUD updates.
        /// </summary>
        public event Action<float> OnHeightRelativeToBasementChanged;

        /// <summary>
        /// Event fired when depth below soil changes.
        /// Parameter is the depth value (>= 0).
        /// Subscribe to this for loot/layer updates.
        /// </summary>
        public event Action<float> OnDepthBelowSoilChanged;

        /// <summary>
        /// Legacy event - fires with DepthBelowSoil value.
        /// </summary>
        public event Action<float> OnDepthChanged;

        /// <summary>
        /// Singleton instance for easy access.
        /// </summary>
        public static DepthManager Instance { get; private set; }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetStatics()
        {
            Instance = null;
        }

        private void Awake()
        {
            if (Instance != null && Instance != this)
            {
                if (enableDebugLogs) Debug.LogWarning($"[DepthManager] Multiple instances detected! Destroying this one: {gameObject.name}");
                Destroy(gameObject);
                return;
            }
            Instance = this;

            // Configuration is set via serialized fields
            // basementFloorY and soilStartOffsetMeters should be set in the inspector
            if (enableDebugLogs)
                Debug.Log($"[DepthManager] Using config: basementFloorY={basementFloorY}, soilOffset={soilStartOffsetMeters}");
        }

        private void Start()
        {
            // Find player transform if not assigned
            if (playerTransform == null)
            {
                // Try Camera.main first (most common for first-person games)
                if (Camera.main != null)
                {
                    playerTransform = Camera.main.transform;
                    if (enableDebugLogs)
                        Debug.Log($"[DepthManager] Found Camera.main for player tracking: {playerTransform.name}");
                }
                else
                {
                    // Try to find a Player tagged object
                    GameObject player = GameObject.FindGameObjectWithTag("Player");
                    if (player != null)
                    {
                        playerTransform = player.transform;
                        if (enableDebugLogs)
                            Debug.Log($"[DepthManager] Found Player tag for tracking: {playerTransform.name}");
                    }
                }
            }

            if (playerTransform == null)
            {
                if (enableDebugLogs) Debug.LogError("[DepthManager] NO PLAYER TRANSFORM FOUND - depth tracking will NOT work!");
            }

            // Subscribe to dig events
            var diggingSystem = DiggingSystem.Instance ?? FindObjectOfType<DiggingSystem>();
            if (diggingSystem != null)
            {
                diggingSystem.OnDigCompleted += HandleDigCompleted;
            }

            // Initialize from current position
            if (playerTransform != null)
            {
                UpdateHeightAndDepth();
                // Force initial event fire
                _lastNotifiedHeight = _heightRelativeToBasement - 1f; // Ensure different
                _lastNotifiedDepthBelowSoil = _depthBelowSoil - 1f;
                FireEventsIfChanged();
            }

            if (enableDebugLogs)
            {
                Debug.Log($"[DepthManager] ══════════════════════════════════════════════════");
                Debug.Log($"[DepthManager] INITIALIZATION COMPLETE");
                Debug.Log($"[DepthManager]   BasementFloorY = {GetBasementFloorY():F2}");
                Debug.Log($"[DepthManager]   SoilStartOffset = {soilStartOffsetMeters:F2}m");
                Debug.Log($"[DepthManager]   PlayerTransform = {(playerTransform != null ? playerTransform.name : "NULL")}");
                Debug.Log($"[DepthManager]   Initial HeightRelativeToBasement = {_heightRelativeToBasement:F2}m");
                Debug.Log($"[DepthManager]   Initial DepthBelowSoil = {_depthBelowSoil:F2}m");
                Debug.Log($"[DepthManager] ══════════════════════════════════════════════════");
            }
        }

        private void Update()
        {
            if (playerTransform == null) return;

            UpdateHeightAndDepth();
            FireEventsIfChanged();
        }

        private void OnDestroy()
        {
            var diggingSystem = DiggingSystem.Instance;
            if (diggingSystem != null)
            {
                diggingSystem.OnDigCompleted -= HandleDigCompleted;
            }

            if (Instance == this)
            {
                Instance = null;
            }
        }

        /// <summary>
        /// Get the basement floor Y position (from ref or field).
        /// </summary>
        private float GetBasementFloorY()
        {
            if (basementFloorRef != null)
            {
                // Use top surface of floor object
                return basementFloorRef.position.y + (basementFloorRef.localScale.y / 2f);
            }
            return basementFloorY;
        }

        /// <summary>
        /// Update both height and depth values from player position.
        /// </summary>
        private void UpdateHeightAndDepth()
        {
            float basementY = GetBasementFloorY();
            float playerY = playerTransform.position.y;

            // (1) Height relative to basement (for HUD)
            // Positive = above, Negative = below, 0 = on floor
            _heightRelativeToBasement = playerY - basementY;

            // (2) Depth below soil (for loot/layers)
            // Only counts depth AFTER the soil offset
            float rawDepthFromBasement = basementY - playerY; // Positive when below basement
            float depthBelowSoil = rawDepthFromBasement - soilStartOffsetMeters;
            _depthBelowSoil = Mathf.Max(0f, depthBelowSoil);

            // Update max depth
            if (_depthBelowSoil > _maxDepthBelowSoil)
            {
                _maxDepthBelowSoil = _depthBelowSoil;
            }
        }

        /// <summary>
        /// Fire events if values changed significantly.
        /// </summary>
        private void FireEventsIfChanged()
        {
            // Height changed?
            if (Mathf.Abs(_heightRelativeToBasement - _lastNotifiedHeight) >= changeThreshold)
            {
                _lastNotifiedHeight = _heightRelativeToBasement;
                OnHeightRelativeToBasementChanged?.Invoke(_heightRelativeToBasement);

                if (enableDebugLogs)
                    Debug.Log($"[DepthManager] HeightRelativeToBasement = {_heightRelativeToBasement:F2}m");
            }

            // Depth below soil changed?
            if (Mathf.Abs(_depthBelowSoil - _lastNotifiedDepthBelowSoil) >= changeThreshold)
            {
                _lastNotifiedDepthBelowSoil = _depthBelowSoil;
                OnDepthBelowSoilChanged?.Invoke(_depthBelowSoil);
                OnDepthChanged?.Invoke(_depthBelowSoil); // Legacy

                // Notify terrain system to pre-create buffer layers
                NotifyTerrainOfPlayerDepth();

                if (enableDebugLogs)
                    Debug.Log($"[DepthManager] DepthBelowSoil = {_depthBelowSoil:F2}m");
            }
        }

        /// <summary>
        /// Notify the terrain system of the player's current depth for lazy loading.
        /// This pre-creates buffer layers ahead of the player.
        /// </summary>
        private void NotifyTerrainOfPlayerDepth()
        {
            // Currently a no-op - ChunkManager handles its own player tracking
            // This method is kept for potential future use with lazy chunk loading
        }

        /// <summary>
        /// Handle dig completion - update max depth if deeper.
        /// </summary>
        private void HandleDigCompleted(DigResult result)
        {
            if (!result.Success) return;

            // Calculate depth from the dig position
            float digWorldY = result.Operation.WorldPosition.y;
            float depthAtDig = CalculateAdjustedDepthFromWorldY(digWorldY);

            if (depthAtDig > _maxDepthBelowSoil)
            {
                _maxDepthBelowSoil = depthAtDig;
                if (enableDebugLogs)
                    Debug.Log($"[DepthManager] New max depth: {_maxDepthBelowSoil:F1}m");
            }
        }

        /// <summary>
        /// Set the player transform for continuous tracking.
        /// </summary>
        public void SetPlayerTransform(Transform player)
        {
            playerTransform = player;
        }

        /// <summary>
        /// Reset all depth values to zero.
        /// </summary>
        public void ResetDepth()
        {
            _heightRelativeToBasement = 0f;
            _depthBelowSoil = 0f;
            _maxDepthBelowSoil = 0f;
            _lastNotifiedHeight = -999f;
            _lastNotifiedDepthBelowSoil = -999f;

            OnHeightRelativeToBasementChanged?.Invoke(0f);
            OnDepthBelowSoilChanged?.Invoke(0f);
            OnDepthChanged?.Invoke(0f);
        }

        // Legacy compatibility methods

        /// <summary>
        /// Calculate depth below soil from a world Y position.
        /// </summary>
        public float CalculateAdjustedDepthFromWorldY(float worldY)
        {
            float basementY = GetBasementFloorY();
            float rawDepth = basementY - worldY;
            return Mathf.Max(0f, rawDepth - soilStartOffsetMeters);
        }

        /// <summary>
        /// Get adjusted depth for a given raw depth value.
        /// </summary>
        public float GetAdjustedDepth(float rawDepth)
        {
            return Mathf.Max(0f, rawDepth - soilStartOffsetMeters);
        }

        /// <summary>
        /// Get current adjusted depth (alias for DepthBelowSoil).
        /// </summary>
        public float GetAdjustedDepth()
        {
            return _depthBelowSoil;
        }

        /// <summary>
        /// Calculate world Y from depth.
        /// </summary>
        public float CalculateWorldYFromDepth(float depth)
        {
            return GetBasementFloorY() - depth;
        }

        /// <summary>
        /// Legacy: Calculate raw depth from world Y.
        /// </summary>
        public float CalculateRawDepthFromWorldY(float worldY)
        {
            return Mathf.Max(0f, GetBasementFloorY() - worldY);
        }

        /// <summary>
        /// Legacy: Calculate depth from world Y.
        /// </summary>
        public float CalculateDepthFromWorldY(float worldY)
        {
            return CalculateRawDepthFromWorldY(worldY);
        }

        /// <summary>
        /// Check if raw depth is in soil zone.
        /// </summary>
        public bool IsRawDepthInSoilZone(float rawDepth)
        {
            return rawDepth >= soilStartOffsetMeters;
        }

        /// <summary>
        /// Legacy: Set raw depth.
        /// </summary>
        public void SetRawDepth(float rawDepth)
        {
            // This is a legacy method - just update internal state
            float basementY = GetBasementFloorY();
            float playerY = basementY - rawDepth;
            _heightRelativeToBasement = playerY - basementY;
            _depthBelowSoil = Mathf.Max(0f, rawDepth - soilStartOffsetMeters);

            if (_depthBelowSoil > _maxDepthBelowSoil)
                _maxDepthBelowSoil = _depthBelowSoil;

            _lastNotifiedHeight = -999f;
            _lastNotifiedDepthBelowSoil = -999f;
            FireEventsIfChanged();
        }

        /// <summary>
        /// Legacy: Set depth.
        /// </summary>
        public void SetDepth(float depth)
        {
            SetRawDepth(depth);
        }

        /// <summary>
        /// Legacy: Add to depth.
        /// </summary>
        public void AddDepth(float delta)
        {
            SetRawDepth(CurrentRawDepthMeters + delta);
        }

        /// <summary>
        /// Get depth for UI (legacy - returns negative adjusted depth).
        /// </summary>
        public float GetDepthForUI()
        {
            return -_depthBelowSoil;
        }

        /// <summary>
        /// Update depth from position (legacy).
        /// </summary>
        public void UpdateDepthFromPosition(Vector3 position)
        {
            // Temporarily set position and update
            var oldPos = playerTransform?.position ?? position;
            if (playerTransform != null)
            {
                UpdateHeightAndDepth();
                FireEventsIfChanged();
            }
        }
    }
}
