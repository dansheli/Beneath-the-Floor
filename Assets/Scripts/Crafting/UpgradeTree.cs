using UnityEngine;
using System.Collections.Generic;

namespace BeneathTheFloor.Crafting
{
    [CreateAssetMenu(fileName = "New Upgrade Tree", menuName = "Beneath The Floor/Upgrades/Upgrade Tree")]
    public class UpgradeTree : ScriptableObject
    {
        [Header("Tree Info")]
        public string treeId;
        public string treeName;
        public UpgradeCategory category;
        public Sprite icon;

        [Header("Upgrades")]
        public List<UpgradeNode> upgrades = new List<UpgradeNode>();
    }

    [System.Serializable]
    public class UpgradeNode
    {
        [Header("Basic Info")]
        public string upgradeId;
        public string upgradeName;
        [TextArea(2, 4)]
        public string description;
        public Sprite icon;

        [Header("Tier")]
        public int tier = 1;
        public int maxLevel = 3;
        public int currentLevel = 0;

        [Header("Effect")]
        public UpgradeType upgradeType;
        public float baseValue;
        public float valuePerLevel;

        [Header("Cost")]
        public List<ResourceRequirement> baseCost = new List<ResourceRequirement>();
        public float costMultiplierPerLevel = 1.5f;

        [Header("Prerequisites")]
        public List<string> requiredUpgradeIds = new List<string>();

        public bool IsUnlocked => currentLevel > 0;
        public bool IsMaxLevel => currentLevel >= maxLevel;

        public float GetCurrentValue()
        {
            return baseValue + (valuePerLevel * currentLevel);
        }

        public float GetNextValue()
        {
            if (IsMaxLevel) return GetCurrentValue();
            return baseValue + (valuePerLevel * (currentLevel + 1));
        }

        public List<ResourceRequirement> GetCostForNextLevel()
        {
            List<ResourceRequirement> costs = new List<ResourceRequirement>();

            float multiplier = Mathf.Pow(costMultiplierPerLevel, currentLevel);

            foreach (var baseCostItem in baseCost)
            {
                costs.Add(new ResourceRequirement
                {
                    resourceType = baseCostItem.resourceType,
                    amount = Mathf.CeilToInt(baseCostItem.amount * multiplier)
                });
            }

            return costs;
        }
    }

    public enum UpgradeCategory
    {
        Player,
        Tools,
        Machines,
        Energy,
        Exploration,
        Lamps,
        Winch
    }

    public enum UpgradeType
    {
        // Player upgrades
        DigSpeed,
        DigPower,
        InventorySize,
        LightRadius,
        MoveSpeed,
        MaxHealth,
        OxygenCapacity,

        // Machine upgrades
        RefinerySpeed,
        RefineryEfficiency,
        WorkbenchSpeed,
        WorkbenchAutoCraft,

        // Energy upgrades
        EnergyCapacity,
        EnergyGeneration,
        EnergyEfficiency,

        // Tool upgrades
        ToolDurability,
        ToolMaxDepth,
        ToolTier,
        ToolLevelUp, // Level up within current tier (+10% dig power per level)
        ToolSpeed,   // DEPRECATED - kept for save compatibility
        ToolPower,   // Combined dig power + radius upgrade
        ToolRadius,  // DEPRECATED - merged into ToolPower
        ToolTierUp,  // Upgrade to next tool tier

        // Shop items (one-time purchases that can be repeated)
        LampPurchase,
        EnergyDrinkPurchase,  // Buy energy drinks

        // Jetpack upgrades
        JetpackEfficiency, // Reduces jetpack energy drain per second

        // Winch upgrades
        WinchCableLength,  // Combined: length + speed + power
        WinchMotorSpeed,   // DEPRECATED - now included in WinchCableLength
        WinchMotorPower,   // DEPRECATED - now included in WinchCableLength

        // Sonic Pulser purchase
        SonicPulserPurchase  // One-time purchase of Tool 5
    }
}
