using UnityEngine;
using BeneathTheFloor.Tools;
using BeneathTheFloor.Digging;

namespace BeneathTheFloor.Player
{
    /// <summary>
    /// Simplified tool visual controller - delegates to HeldToolController.
    /// Kept for compatibility with UpgradeStation and other systems.
    /// </summary>
    public class PlayerToolVisualController : MonoBehaviour
    {
        public static PlayerToolVisualController Instance { get; private set; }

        private void Awake()
        {
            if (Instance != null && Instance != this)
            {
                Destroy(this);
                return;
            }
            Instance = this;
        }

        private void OnDestroy()
        {
            if (Instance == this) Instance = null;
        }

        /// <summary>
        /// Upgrades the tool to a new tier (delegates to HeldToolController).
        /// </summary>
        public void UpgradeTool(int newTier)
        {
            if (HeldToolController.Instance != null)
            {
                HeldToolController.Instance.SetActiveTier(newTier);
            }
        }

        /// <summary>
        /// Gets the current tool tier.
        /// </summary>
        public int GetCurrentTier()
        {
            return HeldToolController.Instance?.GetCurrentTier() ?? 1;
        }

        /// <summary>
        /// Triggers dig animation on the current tool.
        /// </summary>
        public void PlayDigAnimation()
        {
            HeldToolController.Instance?.StartDigging();
        }

        /// <summary>
        /// Gets the current tool's DigToolProfile for DiggingSystem.
        /// Creates a runtime profile based on current tier.
        /// </summary>
        public DigToolProfile GetCurrentToolProfile()
        {
            int tier = GetCurrentTier();
            return CreateDigToolProfile(tier);
        }

        private DigToolProfile CreateDigToolProfile(int tier)
        {
            var profile = ScriptableObject.CreateInstance<DigToolProfile>();

            // Depth limits per tier:
            // Tier 1: 10m - basic starter tool
            // Tier 2: 25m - mid-game tool
            // Tier 3: 50m (or 0 = unlimited) - end-game tool
            switch (tier)
            {
                case 1:
                    profile.toolId = "shovel_tier1";
                    profile.displayName = "Wooden Shovel";
                    profile.digRadius = 0.5f;
                    profile.digPower = 1.0f;
                    profile.digDuration = 0.5f;
                    profile.energyCostPerDig = 8f;
                    profile.maxDigDepth = 10f;  // Tier 1: 10m depth limit
                    profile.hardnessRating = 1.5f;
                    profile.resourceEfficiency = 1f;
                    break;
                case 2:
                    profile.toolId = "shovel_tier2";
                    profile.displayName = "Iron Shovel";
                    profile.digRadius = 0.6f;
                    profile.digPower = 2.0f;
                    profile.digDuration = 0.4f;
                    profile.energyCostPerDig = 6f;
                    profile.maxDigDepth = 25f;  // Tier 2: 25m depth limit
                    profile.hardnessRating = 2.5f;
                    profile.resourceEfficiency = 1.3f;
                    break;
                case 3:
                    profile.toolId = "shovel_tier3";
                    profile.displayName = "Diamond Shovel";
                    profile.digRadius = 0.7f;
                    profile.digPower = 3.5f;
                    profile.digDuration = 0.3f;
                    profile.energyCostPerDig = 4f;
                    profile.maxDigDepth = 50f;  // Tier 3: 50m depth limit (use 0 for unlimited)
                    profile.hardnessRating = 4.0f;
                    profile.resourceEfficiency = 1.6f;
                    break;
                default:
                    profile.toolId = "shovel_default";
                    profile.displayName = "Shovel";
                    profile.digRadius = 0.5f;
                    profile.digPower = 1.0f;
                    profile.digDuration = 0.5f;
                    profile.energyCostPerDig = 8f;
                    profile.maxDigDepth = 10f;  // Default: same as Tier 1
                    profile.hardnessRating = 1.5f;
                    profile.resourceEfficiency = 1f;
                    break;
            }

            profile.inputMode = DigInputMode.Click;
            return profile;
        }
    }
}
