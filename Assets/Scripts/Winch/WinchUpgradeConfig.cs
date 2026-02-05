using UnityEngine;
using System;

namespace BeneathTheFloor.Winch
{
    /// <summary>
    /// ScriptableObject defining winch cable length and motor tiers.
    /// Data-driven upgrade system for the Grandfather Winch.
    /// </summary>
    [CreateAssetMenu(fileName = "WinchUpgradeConfig", menuName = "Beneath The Floor/Winch/Upgrade Config")]
    public class WinchUpgradeConfig : ScriptableObject
    {
        [Serializable]
        public class CableTier
        {
            public string tierName;
            public float maxCableLength;

            [Header("Motor Speed (included in upgrade)")]
            [Tooltip("Multiplier applied to base pull speed (1.0 = normal, 2.0 = double speed).")]
            public float speedMultiplier = 1f;

            [Header("Motor Power (included in upgrade)")]
            [Tooltip("Reduces soft-zone damping (0 = no improvement, 1 = full power at max tension).")]
            [Range(0f, 1f)]
            public float tensionResistance = 0f;
            [Tooltip("Reduces energy cost for reeling (1 = normal, 0.5 = half energy).")]
            [Range(0.1f, 1f)]
            public float energyEfficiency = 1f;

            [Header("Cost")]
            [Tooltip("Credit cost to upgrade TO this tier.")]
            public int upgradeCost;
            [TextArea(1, 2)]
            public string description;
        }

        // Legacy classes kept for compatibility but no longer used
        [Serializable]
        public class MotorSpeedTier
        {
            public string tierName;
            public float speedMultiplier = 1f;
            public int upgradeCost;
            public string description;
        }

        [Serializable]
        public class MotorPowerTier
        {
            public string tierName;
            [Range(0f, 1f)]
            public float tensionResistance = 0f;
            [Range(0.1f, 1f)]
            public float energyEfficiency = 1f;
            public int upgradeCost;
            public string description;
        }

        [Header("Winch Tiers (Length + Speed + Power combined)")]
        [Tooltip("Define winch tiers. Each tier upgrades length, speed, and power together. Index 0 = starting tier.")]
        public CableTier[] tiers = new CableTier[]
        {
            new CableTier { tierName = "Frayed Rope", maxCableLength = 10f, speedMultiplier = 1.0f, tensionResistance = 0f, energyEfficiency = 1f, upgradeCost = 0, description = "Old rope from the attic. Barely holds." },
            new CableTier { tierName = "Hemp Cable", maxCableLength = 15f, speedMultiplier = 1.2f, tensionResistance = 0.15f, energyEfficiency = 0.9f, upgradeCost = 75, description = "Stronger cable with improved motor." },
            new CableTier { tierName = "Steel Cable", maxCableLength = 20f, speedMultiplier = 1.4f, tensionResistance = 0.3f, energyEfficiency = 0.8f, upgradeCost = 200, description = "Industrial steel cable with gear assist." },
            new CableTier { tierName = "Reinforced Cable", maxCableLength = 25f, speedMultiplier = 1.7f, tensionResistance = 0.5f, energyEfficiency = 0.7f, upgradeCost = 400, description = "Double-braided steel with electric motor." },
            new CableTier { tierName = "Master Cable", maxCableLength = 30f, speedMultiplier = 2.0f, tensionResistance = 0.7f, energyEfficiency = 0.6f, upgradeCost = 650, description = "Grandfather's finest work. Powerful motor." },
            new CableTier { tierName = "Deep Reach Cable", maxCableLength = 40f, speedMultiplier = 2.3f, tensionResistance = 0.85f, energyEfficiency = 0.5f, upgradeCost = 950, description = "Extended length with industrial motor." },
            new CableTier { tierName = "Abyss Cable", maxCableLength = 50f, speedMultiplier = 2.5f, tensionResistance = 0.95f, energyEfficiency = 0.4f, upgradeCost = 1400, description = "The ultimate winch. Reaches the deepest depths." }
        };

        [Header("Legacy (unused - kept for compatibility)")]
        [HideInInspector]
        public MotorSpeedTier[] motorSpeedTiers = new MotorSpeedTier[0];
        [HideInInspector]
        public MotorPowerTier[] motorPowerTiers = new MotorPowerTier[0];

        [Header("Base Settings")]
        [Tooltip("Soft zone starts at this fraction of max length (0.85 = 85%).")]
        [Range(0.5f, 0.95f)]
        public float softZoneStartFraction = 0.85f;

        [Tooltip("Maximum damping applied in soft zone (0-1).")]
        [Range(0f, 1f)]
        public float maxDampingStrength = 0.8f;

        [Tooltip("Base speed when pulling up with cable (m/s). Modified by motor speed tier.")]
        public float pullSpeed = 3f;

        [Tooltip("Speed multiplier when at max tension (before motor power modifier).")]
        [Range(0f, 0.5f)]
        public float tensionSpeedMultiplier = 0.1f;

        /// <summary>
        /// Get the max cable length for a given tier index.
        /// </summary>
        public float GetMaxLength(int tierIndex)
        {
            if (tiers == null || tiers.Length == 0) return 10f;
            tierIndex = Mathf.Clamp(tierIndex, 0, tiers.Length - 1);
            return tiers[tierIndex].maxCableLength;
        }

        /// <summary>
        /// Get tier info by index.
        /// </summary>
        public CableTier GetTier(int tierIndex)
        {
            if (tiers == null || tiers.Length == 0) return null;
            tierIndex = Mathf.Clamp(tierIndex, 0, tiers.Length - 1);
            return tiers[tierIndex];
        }

        /// <summary>
        /// Get total number of cable tiers.
        /// </summary>
        public int TierCount => tiers?.Length ?? 0;

        /// <summary>
        /// Get total number of motor speed tiers (now same as cable tiers).
        /// </summary>
        public int MotorSpeedTierCount => TierCount;

        /// <summary>
        /// Get total number of motor power tiers (now same as cable tiers).
        /// </summary>
        public int MotorPowerTierCount => TierCount;

        /// <summary>
        /// Get the upgrade cost for a cable tier.
        /// </summary>
        public int GetCableTierCost(int tierIndex)
        {
            if (tiers == null || tierIndex < 0 || tierIndex >= tiers.Length) return 0;
            return tiers[tierIndex].upgradeCost;
        }

        /// <summary>
        /// Get the upgrade cost for a motor speed tier.
        /// </summary>
        public int GetMotorSpeedTierCost(int tierIndex)
        {
            if (motorSpeedTiers == null || tierIndex < 0 || tierIndex >= motorSpeedTiers.Length) return 0;
            return motorSpeedTiers[tierIndex].upgradeCost;
        }

        /// <summary>
        /// Get the upgrade cost for a motor power tier.
        /// </summary>
        public int GetMotorPowerTierCost(int tierIndex)
        {
            if (motorPowerTiers == null || tierIndex < 0 || tierIndex >= motorPowerTiers.Length) return 0;
            return motorPowerTiers[tierIndex].upgradeCost;
        }

        #region Motor Speed Methods (now uses cable tier)

        /// <summary>
        /// Get motor speed tier info by index. Now returns cable tier speed data.
        /// </summary>
        public MotorSpeedTier GetMotorSpeedTier(int tierIndex)
        {
            var cableTier = GetTier(tierIndex);
            if (cableTier == null) return null;
            return new MotorSpeedTier
            {
                tierName = cableTier.tierName,
                speedMultiplier = cableTier.speedMultiplier,
                upgradeCost = cableTier.upgradeCost,
                description = cableTier.description
            };
        }

        /// <summary>
        /// Get the speed multiplier for a given tier. Now uses cable tier.
        /// </summary>
        public float GetMotorSpeedMultiplier(int tierIndex)
        {
            if (tiers == null || tiers.Length == 0) return 1f;
            tierIndex = Mathf.Clamp(tierIndex, 0, tiers.Length - 1);
            return tiers[tierIndex].speedMultiplier;
        }

        /// <summary>
        /// Get effective pull speed (base speed * cable tier speed multiplier).
        /// </summary>
        public float GetEffectivePullSpeed(int cableTierIndex)
        {
            return pullSpeed * GetMotorSpeedMultiplier(cableTierIndex);
        }

        #endregion

        #region Motor Power Methods (now uses cable tier)

        /// <summary>
        /// Get motor power tier info by index. Now returns cable tier power data.
        /// </summary>
        public MotorPowerTier GetMotorPowerTier(int tierIndex)
        {
            var cableTier = GetTier(tierIndex);
            if (cableTier == null) return null;
            return new MotorPowerTier
            {
                tierName = cableTier.tierName,
                tensionResistance = cableTier.tensionResistance,
                energyEfficiency = cableTier.energyEfficiency,
                upgradeCost = cableTier.upgradeCost,
                description = cableTier.description
            };
        }

        /// <summary>
        /// Get the tension resistance for a given tier. Now uses cable tier.
        /// </summary>
        public float GetTensionResistance(int tierIndex)
        {
            if (tiers == null || tiers.Length == 0) return 0f;
            tierIndex = Mathf.Clamp(tierIndex, 0, tiers.Length - 1);
            return tiers[tierIndex].tensionResistance;
        }

        /// <summary>
        /// Get the energy efficiency for a given tier. Now uses cable tier.
        /// </summary>
        public float GetEnergyEfficiency(int tierIndex)
        {
            if (tiers == null || tiers.Length == 0) return 1f;
            tierIndex = Mathf.Clamp(tierIndex, 0, tiers.Length - 1);
            return tiers[tierIndex].energyEfficiency;
        }

        /// <summary>
        /// Get effective damping at max tension (reduced by cable tier power).
        /// </summary>
        public float GetEffectiveDamping(int cableTierIndex)
        {
            float tensionResistance = GetTensionResistance(cableTierIndex);
            // Higher tension resistance = less damping (motor fights through it)
            return maxDampingStrength * (1f - tensionResistance);
        }

        #endregion

        /// <summary>
        /// Initialize all tiers to defaults.
        /// </summary>
        [ContextMenu("Reset to Default Tiers")]
        public void ResetToDefaults()
        {
            tiers = new CableTier[]
            {
                new CableTier { tierName = "Frayed Rope", maxCableLength = 10f, speedMultiplier = 1.0f, tensionResistance = 0f, energyEfficiency = 1f, upgradeCost = 0, description = "Old rope from the attic. Barely holds." },
                new CableTier { tierName = "Hemp Cable", maxCableLength = 15f, speedMultiplier = 1.2f, tensionResistance = 0.15f, energyEfficiency = 0.9f, upgradeCost = 75, description = "Stronger cable with improved motor." },
                new CableTier { tierName = "Steel Cable", maxCableLength = 20f, speedMultiplier = 1.4f, tensionResistance = 0.3f, energyEfficiency = 0.8f, upgradeCost = 200, description = "Industrial steel cable with gear assist." },
                new CableTier { tierName = "Reinforced Cable", maxCableLength = 25f, speedMultiplier = 1.7f, tensionResistance = 0.5f, energyEfficiency = 0.7f, upgradeCost = 400, description = "Double-braided steel with electric motor." },
                new CableTier { tierName = "Master Cable", maxCableLength = 30f, speedMultiplier = 2.0f, tensionResistance = 0.7f, energyEfficiency = 0.6f, upgradeCost = 650, description = "Grandfather's finest work. Powerful motor." },
                new CableTier { tierName = "Deep Reach Cable", maxCableLength = 40f, speedMultiplier = 2.3f, tensionResistance = 0.85f, energyEfficiency = 0.5f, upgradeCost = 950, description = "Extended length with industrial motor." },
                new CableTier { tierName = "Abyss Cable", maxCableLength = 50f, speedMultiplier = 2.5f, tensionResistance = 0.95f, energyEfficiency = 0.4f, upgradeCost = 1400, description = "The ultimate winch. Reaches the deepest depths." }
            };

            // Legacy arrays cleared
            motorSpeedTiers = new MotorSpeedTier[0];
            motorPowerTiers = new MotorPowerTier[0];
        }
    }
}
