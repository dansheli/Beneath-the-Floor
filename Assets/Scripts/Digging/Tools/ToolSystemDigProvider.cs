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
        [Header("Tool Hardness Settings")]
        [Tooltip("Max terrain hardness for Shovel/Heavy Spade tier.")]
        [SerializeField] private float tier1MaxHardness = 1.0f;

        [Tooltip("Max terrain hardness for Pickaxe tier.")]
        [SerializeField] private float tier2MaxHardness = 1.5f;

        [Tooltip("Max terrain hardness for advanced tools tier.")]
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
        /// Get current tool info based on equipped tool.
        /// Balanced for 0.2m voxels: tight radius + high strength = visible bite marks.
        /// Base digRadius=0.5m, base digStrength=0.8 (in DiggingSystem).
        /// </summary>
        public DigToolInfo? GetCurrentDigTool()
        {
            if (!HasValidDigTool())
                return null;

            int toolIndex = heldToolController.GetCurrentToolIndex();
            ToolData toolData = heldToolController.GetCurrentTool();

            // Per-tool dig profile: small radius + high strength for natural-looking terrain
            // At 0.2m voxels, effective radius in voxels ≈ (base 0.5 × mult) / 0.2
            float radiusMultiplier, strengthMultiplier, maxHardness;
            string toolId, displayName;

            switch (toolIndex)
            {
                case 0: // Shovel - tight scoop, moderate power
                    radiusMultiplier = 0.6f;    // 0.30m = 1.5 voxels
                    strengthMultiplier = 2.5f;   // effective 2.0
                    maxHardness = tier1MaxHardness;
                    toolId = "shovel";
                    displayName = toolData?.toolName ?? "Shovel";
                    break;
                case 1: // Heavy Spade - slightly wider, stronger
                    radiusMultiplier = 0.7f;    // 0.35m = 1.75 voxels
                    strengthMultiplier = 3.0f;   // effective 2.4
                    maxHardness = tier2MaxHardness;
                    toolId = "heavy_spade";
                    displayName = toolData?.toolName ?? "Heavy Spade";
                    break;
                case 2: // Pickaxe - focused strike, high power
                    radiusMultiplier = 0.75f;   // 0.375m = ~1.9 voxels
                    strengthMultiplier = 3.5f;   // effective 2.8
                    maxHardness = tier3MaxHardness;
                    toolId = "pickaxe";
                    displayName = toolData?.toolName ?? "Pickaxe";
                    break;
                case 3: // Drill Pike - piercing thrust, deep and narrow
                    radiusMultiplier = 0.65f;   // 0.325m = ~1.6 voxels (narrow bore)
                    strengthMultiplier = 4.5f;   // effective 3.6 (deepest per hit)
                    maxHardness = 3.0f;
                    toolId = "drill_pike";
                    displayName = toolData?.toolName ?? "Drill Pike";
                    break;
                case 4: // Sonic Pulser - normal dig (quick shot handled separately in ToolVisual)
                    radiusMultiplier = 0.8f;    // 0.40m = 2.0 voxels
                    strengthMultiplier = 4.0f;   // effective 3.2
                    maxHardness = 5.0f;
                    toolId = "sonic_pulser";
                    displayName = toolData?.toolName ?? "Sonic Pulser";
                    break;
                default:
                    radiusMultiplier = 0.6f;
                    strengthMultiplier = 2.5f;
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
