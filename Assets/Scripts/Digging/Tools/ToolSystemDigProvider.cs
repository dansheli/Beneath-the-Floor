using UnityEngine;
using BeneathTheFloor.Tools;

namespace BeneathTheFloor.Digging
{
    /// <summary>
    /// Bridges the existing HeldToolController tool system with DiggingSystem.
    ///
    /// USAGE:
    /// - Attach to Player or alongside DiggingSystem
    /// - Automatically finds HeldToolController and registers with DiggingSystem
    /// - Player can dig when a tool is visible/equipped
    ///
    /// TIER MAPPING:
    /// - Tier 1 (Shovel): radius 1.0x, strength 1.0x, maxHardness 1.0
    /// - Tier 2 (Hoe):    radius 1.2x, strength 1.3x, maxHardness 1.5
    /// - Tier 3 (Pickaxe): radius 1.5x, strength 1.6x, maxHardness 2.0
    /// </summary>
    [DefaultExecutionOrder(100)] // Run after HeldToolController and DiggingSystem
    public class ToolSystemDigProvider : MonoBehaviour, IDigToolProvider
    {
        [Header("Tool Tier Settings")]
        [Tooltip("Dig radius multiplier for Tier 1 tools.")]
        [SerializeField] private float tier1RadiusMultiplier = 1.0f;
        [SerializeField] private float tier1StrengthMultiplier = 1.0f;
        [SerializeField] private float tier1MaxHardness = 1.0f;

        [Tooltip("Dig radius multiplier for Tier 2 tools.")]
        [SerializeField] private float tier2RadiusMultiplier = 1.2f;
        [SerializeField] private float tier2StrengthMultiplier = 1.3f;
        [SerializeField] private float tier2MaxHardness = 1.5f;

        [Tooltip("Dig radius multiplier for Tier 3 tools.")]
        [SerializeField] private float tier3RadiusMultiplier = 1.5f;
        [SerializeField] private float tier3StrengthMultiplier = 1.6f;
        [SerializeField] private float tier3MaxHardness = 2.0f;

        [Header("References")]
        [Tooltip("HeldToolController reference. If null, finds automatically.")]
        [SerializeField] private HeldToolController heldToolController;

        [Tooltip("DiggingSystem reference. If null, finds automatically.")]
        [SerializeField] private DiggingSystem diggingSystem;

        [Header("Debug")]
        [SerializeField] private bool enableDebugLogs = false;

        // Flag to force-disable logs at runtime
        private bool _forceDisableLogs = true;

        // Track dig count for stats
        private int _totalDigs = 0;
        private float _totalVolumeRemoved = 0f;

        // Cache last tool state for change detection
        private bool _lastToolVisible = false;
        private int _lastTier = 0;

        private void Start()
        {
            // Find HeldToolController
            if (heldToolController == null)
            {
                heldToolController = HeldToolController.Instance;
            }
            if (heldToolController == null)
            {
                heldToolController = FindObjectOfType<HeldToolController>();
            }

            if (heldToolController == null)
            {
                Debug.LogError("[ToolSystemDigProvider] HeldToolController not found! Tool-gated digging will not work.");
                enabled = false;
                return;
            }

            // Find DiggingSystem
            if (diggingSystem == null)
            {
                diggingSystem = DiggingSystem.Instance;
            }
            if (diggingSystem == null)
            {
                diggingSystem = FindObjectOfType<DiggingSystem>();
            }

            if (diggingSystem == null)
            {
                Debug.LogError("[ToolSystemDigProvider] DiggingSystem not found! Cannot register as tool provider.");
                enabled = false;
                return;
            }

            // Register with DiggingSystem
            diggingSystem.SetToolProvider(this);

            // Subscribe to tool events
            heldToolController.OnToolEquipped += OnToolEquipped;
            heldToolController.OnToolUnequipped += OnToolUnequipped;
        }

        private void OnDestroy()
        {
            if (heldToolController != null)
            {
                heldToolController.OnToolEquipped -= OnToolEquipped;
                heldToolController.OnToolUnequipped -= OnToolUnequipped;
            }
        }

        private void Update()
        {
            // Detect tool visibility changes for debug logging
            if (enableDebugLogs && heldToolController != null)
            {
                bool currentVisible = heldToolController.AreToolsVisible();
                int currentTier = heldToolController.GetCurrentTier();

                if (currentVisible != _lastToolVisible || currentTier != _lastTier)
                {
                    _lastToolVisible = currentVisible;
                    _lastTier = currentTier;

                }
            }
        }

        #region IDigToolProvider Implementation

        /// <summary>
        /// Returns true if player has a visible tool equipped.
        /// </summary>
        public bool HasValidDigTool()
        {
            if (heldToolController == null)
                return false;

            // Tool must be visible (in hand) to dig
            return heldToolController.AreToolsVisible() && heldToolController.HasToolEquipped();
        }

        /// <summary>
        /// Get current tool info based on equipped tier.
        /// </summary>
        public DigToolInfo? GetCurrentDigTool()
        {
            if (!HasValidDigTool())
                return null;

            int tier = heldToolController.GetCurrentTier();
            ToolData toolData = heldToolController.GetCurrentTool();

            // Get tier-specific multipliers
            float radiusMultiplier, strengthMultiplier, maxHardness;
            string toolId, displayName;

            switch (tier)
            {
                case 1:
                    radiusMultiplier = tier1RadiusMultiplier;
                    strengthMultiplier = tier1StrengthMultiplier;
                    maxHardness = tier1MaxHardness;
                    toolId = "shovel_tier1";
                    displayName = toolData?.toolName ?? "Wooden Shovel";
                    break;
                case 2:
                    radiusMultiplier = tier2RadiusMultiplier;
                    strengthMultiplier = tier2StrengthMultiplier;
                    maxHardness = tier2MaxHardness;
                    toolId = "hoe_tier2";
                    displayName = toolData?.toolName ?? "Iron Hoe";
                    break;
                case 3:
                    radiusMultiplier = tier3RadiusMultiplier;
                    strengthMultiplier = tier3StrengthMultiplier;
                    maxHardness = tier3MaxHardness;
                    toolId = "pickaxe_tier3";
                    displayName = toolData?.toolName ?? "Steel Pickaxe";
                    break;
                default:
                    radiusMultiplier = 1.0f;
                    strengthMultiplier = 1.0f;
                    maxHardness = 1.0f;
                    toolId = "unknown";
                    displayName = "Unknown Tool";
                    break;
            }

            // Apply digSpeed from ToolData if available
            if (toolData != null && toolData.digSpeed > 0)
            {
                strengthMultiplier *= toolData.digSpeed;
            }

            return new DigToolInfo
            {
                ToolId = toolId,
                DisplayName = displayName,
                RadiusMultiplier = radiusMultiplier,
                StrengthMultiplier = strengthMultiplier,
                MaxHardness = maxHardness
            };
        }

        /// <summary>
        /// Called when a dig completes successfully.
        /// </summary>
        public void OnDigPerformed(DigResult result)
        {
            _totalDigs++;
            _totalVolumeRemoved += result.VolumeRemoved;

            // Trigger dig animation on the held tool
            if (heldToolController != null)
            {
                heldToolController.PlayDigAnimation();
            }

            if (enableDebugLogs && !_forceDisableLogs)
            {
                int tier = heldToolController?.GetCurrentTier() ?? 0;
                Debug.Log($"[ToolSystemDigProvider] Dig #{_totalDigs} with Tier {tier}: removed {result.VolumeRemoved:F4}m³");
            }
        }

        #endregion

        #region Event Handlers

        private void OnToolEquipped(ToolData tool)
        {
            if (enableDebugLogs && !_forceDisableLogs)
            {
                Debug.Log($"[ToolSystemDigProvider] Tool equipped: {tool.toolName} (Tier {tool.tier}) - DIGGING ENABLED");
            }
        }

        private void OnToolUnequipped()
        {
            if (enableDebugLogs && !_forceDisableLogs)
            {
                Debug.Log("[ToolSystemDigProvider] Tool unequipped - DIGGING DISABLED");
            }
        }

        #endregion

        #region Public API

        /// <summary>
        /// Get total digs performed this session.
        /// </summary>
        public int GetTotalDigs() => _totalDigs;

        /// <summary>
        /// Get total volume removed this session.
        /// </summary>
        public float GetTotalVolumeRemoved() => _totalVolumeRemoved;

        /// <summary>
        /// Check if digging is currently allowed.
        /// </summary>
        public bool CanDig() => HasValidDigTool();

        /// <summary>
        /// Get debug info string.
        /// </summary>
        public string GetDebugInfo()
        {
            bool canDig = HasValidDigTool();
            int tier = heldToolController?.GetCurrentTier() ?? 0;
            bool visible = heldToolController?.AreToolsVisible() ?? false;

            return $"ToolSystemDigProvider:\n" +
                   $"  Can Dig: {canDig}\n" +
                   $"  Tool Visible: {visible}\n" +
                   $"  Current Tier: {tier}\n" +
                   $"  Total Digs: {_totalDigs}\n" +
                   $"  Volume Removed: {_totalVolumeRemoved:F2}m³";
        }

        #endregion
    }
}
