using UnityEngine;
using System.Collections.Generic;
using BeneathTheFloor.Digging;

namespace BeneathTheFloor.Upgrades
{
    public class UpgradeSystem : MonoBehaviour
    {
        private const string PREFS_KEY_TOOL_TIER = "PlayerToolTier";

        [Header("Tool Tier Profiles")]
        [Tooltip("DigToolProfile for each tier. Index 0 = Tier 1 (Base), Index 1 = Tier 2, etc.")]
        [SerializeField] private DigToolProfile[] tierProfiles = new DigToolProfile[4];

        [Header("Tool Upgrades (Legacy - for UI/costs)")]
        [SerializeField] private List<ToolUpgrade> toolUpgrades = new List<ToolUpgrade>();

        [Header("Starting Tool")]
        [SerializeField] private ToolData startingTool;

        [Header("Debug")]
        [SerializeField] private bool enableDebugLogs = false;

        private Inventory.InventorySystem inventory;
        private int currentToolTier = 1;

        public static UpgradeSystem Instance { get; private set; }

        public int CurrentToolTier => currentToolTier;
        public ToolUpgrade NextUpgrade => GetNextUpgrade();

        /// <summary>
        /// Get the current DigToolProfile based on tier.
        /// </summary>
        public DigToolProfile CurrentToolProfile => GetProfileForTier(currentToolTier);

        private void Awake()
        {
            if (Instance == null)
            {
                Instance = this;
            }
            else
            {
                Destroy(gameObject);
            }
        }

        private void Start()
        {
            inventory = Inventory.InventorySystem.Instance;

            // Auto-load tier profiles if not assigned in Inspector
            EnsureTierProfilesLoaded();

            // Load saved tier from PlayerPrefs
            LoadSavedTier();

            // Equip starting tool (legacy)
            if (startingTool != null && inventory != null)
            {
                inventory.EquipTool(startingTool);
            }

            InitializeDefaultUpgrades();

            // Apply current tier profile to digging system
            ApplyCurrentTierProfile();
        }

        /// <summary>
        /// Auto-load tier profiles from GameData/ToolProfiles if not assigned in Inspector.
        /// </summary>
        private void EnsureTierProfilesLoaded()
        {
            // Check if any profiles are missing
            bool anyMissing = false;
            for (int i = 0; i < tierProfiles.Length; i++)
            {
                if (tierProfiles[i] == null)
                {
                    anyMissing = true;
                    break;
                }
            }

            if (!anyMissing) return;

            // Try to load from Resources folder - 4 shovel tier profiles
            var tier1 = Resources.Load<DigToolProfile>("ToolProfiles/BaseShovel");
            var tier2 = Resources.Load<DigToolProfile>("ToolProfiles/Tier1_Shovel");
            var tier3 = Resources.Load<DigToolProfile>("ToolProfiles/Tier2_Shovel");
            var tier4 = Resources.Load<DigToolProfile>("ToolProfiles/Tier3_Shovel");

            // If Resources.Load worked, assign them
            if (tier1 != null || tier2 != null || tier3 != null || tier4 != null)
            {
                if (tierProfiles[0] == null && tier1 != null) tierProfiles[0] = tier1;
                if (tierProfiles.Length > 1 && tierProfiles[1] == null && tier2 != null) tierProfiles[1] = tier2;
                if (tierProfiles.Length > 2 && tierProfiles[2] == null && tier3 != null) tierProfiles[2] = tier3;
                if (tierProfiles.Length > 3 && tierProfiles[3] == null && tier4 != null) tierProfiles[3] = tier4;

                if (enableDebugLogs)
                    Debug.Log($"[UpgradeSystem] Auto-loaded tier profiles from Resources");
                return;
            }

#if UNITY_EDITOR
            // Editor fallback: try loading directly from AssetDatabase
            tier1 = UnityEditor.AssetDatabase.LoadAssetAtPath<DigToolProfile>("Assets/GameData/ToolProfiles/BaseShovel.asset");
            tier2 = UnityEditor.AssetDatabase.LoadAssetAtPath<DigToolProfile>("Assets/GameData/ToolProfiles/Tier1_Shovel.asset");
            tier3 = UnityEditor.AssetDatabase.LoadAssetAtPath<DigToolProfile>("Assets/GameData/ToolProfiles/Tier2_Shovel.asset");
            tier4 = UnityEditor.AssetDatabase.LoadAssetAtPath<DigToolProfile>("Assets/GameData/ToolProfiles/Tier3_Shovel.asset");

            if (tierProfiles[0] == null && tier1 != null) tierProfiles[0] = tier1;
            if (tierProfiles.Length > 1 && tierProfiles[1] == null && tier2 != null) tierProfiles[1] = tier2;
            if (tierProfiles.Length > 2 && tierProfiles[2] == null && tier3 != null) tierProfiles[2] = tier3;
            if (tierProfiles.Length > 3 && tierProfiles[3] == null && tier4 != null) tierProfiles[3] = tier4;

            if (tier1 == null && tier2 == null && tier3 == null && tier4 == null)
            {
                Debug.LogWarning("[UpgradeSystem] Could not auto-load tier profiles. Please assign them in Inspector.");
            }
#endif
        }

        /// <summary>
        /// Load saved tool tier from PlayerPrefs.
        /// </summary>
        private void LoadSavedTier()
        {
            currentToolTier = PlayerPrefs.GetInt(PREFS_KEY_TOOL_TIER, 1);

            // Clamp to valid range
            if (currentToolTier < 1) currentToolTier = 1;
            if (currentToolTier > tierProfiles.Length) currentToolTier = tierProfiles.Length;
        }

        /// <summary>
        /// Save current tool tier to PlayerPrefs.
        /// </summary>
        private void SaveTier()
        {
            PlayerPrefs.SetInt(PREFS_KEY_TOOL_TIER, currentToolTier);
            PlayerPrefs.Save();

            if (enableDebugLogs)
                Debug.Log($"[UpgradeSystem] Saved tier: {currentToolTier}");
        }

        /// <summary>
        /// Get the DigToolProfile for a given tier (1-indexed).
        /// </summary>
        public DigToolProfile GetProfileForTier(int tier)
        {
            int index = tier - 1; // Convert 1-indexed tier to 0-indexed array
            if (index >= 0 && index < tierProfiles.Length && tierProfiles[index] != null)
            {
                return tierProfiles[index];
            }

            if (enableDebugLogs)
                Debug.LogWarning($"[UpgradeSystem] No profile for tier {tier}. Returning null.");
            return null;
        }

        /// <summary>
        /// Apply the current tier's profile to DiggingSystem.
        /// </summary>
        private void ApplyCurrentTierProfile()
        {
            var profile = GetProfileForTier(currentToolTier);
            if (profile == null)
            {
                Debug.LogError($"[UpgradeSystem] Cannot apply tier {currentToolTier} - no profile assigned!");
                return;
            }

            // Apply to DiggingSystem
            if (DiggingSystem.Instance != null)
            {
                DiggingSystem.Instance.SetActiveToolProfile(profile);

                if (enableDebugLogs)
                    Debug.Log($"[UpgradeSystem] Applied tier {currentToolTier} profile: {profile.displayName} (maxDepth={profile.maxDigDepth}m)");
            }
            else
            {
                // DiggingSystem not ready yet - try again after a delay
                StartCoroutine(ApplyProfileDelayed(profile));
            }
        }

        private System.Collections.IEnumerator ApplyProfileDelayed(DigToolProfile profile)
        {
            // Skip if V3 is active - V3 reads from UpgradeStation static multipliers directly
            if (DiggingSystemSwitch.ShouldUseV3())
            {
                yield break;
            }

            // Wait for DiggingSystem to be available
            float timeout = 3f;
            float elapsed = 0f;

            while (DiggingSystem.Instance == null && elapsed < timeout)
            {
                yield return new WaitForSeconds(0.1f);
                elapsed += 0.1f;
            }

            if (DiggingSystem.Instance != null)
            {
                DiggingSystem.Instance.SetActiveToolProfile(profile);

                if (enableDebugLogs)
                    Debug.Log($"[UpgradeSystem] Applied tier {currentToolTier} profile (delayed): {profile.displayName}");
            }
            else
            {
                Debug.LogError("[UpgradeSystem] DiggingSystem not found after timeout!");
            }
        }

        private void InitializeDefaultUpgrades()
        {
            if (toolUpgrades.Count == 0)
            {
                // Define default shovel upgrades: Base → Tier1 → Tier2 → Tier3
                // Each upgrade changes the visual model AND improves stats
                toolUpgrades.Add(new ToolUpgrade
                {
                    upgradeName = "Tier 1 Shovel",
                    tier = 2,
                    requiredResources = new Dictionary<ResourceType, int> { { ResourceType.Stone, 5 } },
                    resultTool = CreateToolData("Tier 1 Shovel", 2, 1.3f, 120, 10),
                    description = "Reinforced shovel with better durability"
                });

                toolUpgrades.Add(new ToolUpgrade
                {
                    upgradeName = "Tier 2 Shovel",
                    tier = 3,
                    requiredResources = new Dictionary<ResourceType, int>
                    {
                        { ResourceType.Stone, 10 },
                        { ResourceType.IronOre, 5 }
                    },
                    resultTool = CreateToolData("Tier 2 Shovel", 3, 1.7f, 150, 20),
                    description = "Iron-tipped shovel for harder soil"
                });

                toolUpgrades.Add(new ToolUpgrade
                {
                    upgradeName = "Tier 3 Shovel",
                    tier = 4,
                    requiredResources = new Dictionary<ResourceType, int>
                    {
                        { ResourceType.IronOre, 15 },
                        { ResourceType.Coal, 5 }
                    },
                    resultTool = CreateToolData("Tier 3 Shovel", 4, 2.2f, 200, 35),
                    description = "Steel-reinforced shovel - maximum digging power"
                });
            }
        }

        private ToolData CreateToolData(string name, int tier, float speed, int durability, int maxDepth)
        {
            return new ToolData
            {
                toolName = name,
                tier = tier,
                digSpeed = speed,
                durability = durability,
                maxDurability = durability,
                maxDepth = maxDepth
            };
        }

        public ToolUpgrade GetNextUpgrade()
        {
            foreach (var upgrade in toolUpgrades)
            {
                if (upgrade.tier == currentToolTier + 1)
                {
                    return upgrade;
                }
            }
            return null;
        }

        public bool CanAffordUpgrade(ToolUpgrade upgrade)
        {
            if (upgrade == null || inventory == null) return false;

            return inventory.HasResources(upgrade.requiredResources);
        }

        public bool TryUpgrade()
        {
            ToolUpgrade nextUpgrade = GetNextUpgrade();

            if (nextUpgrade == null)
            {
                if (enableDebugLogs) Debug.Log("[UpgradeSystem] No more upgrades available!");
                return false;
            }

            if (!CanAffordUpgrade(nextUpgrade))
            {
                if (enableDebugLogs) Debug.Log("[UpgradeSystem] Not enough resources for upgrade!");
                return false;
            }

            // Consume resources
            foreach (var resource in nextUpgrade.requiredResources)
            {
                inventory.RemoveResource(resource.Key, resource.Value);
            }

            // Apply upgrade - update tier
            int previousTier = currentToolTier;
            currentToolTier = nextUpgrade.tier;

            // Legacy inventory tool equip
            if (inventory != null)
                inventory.EquipTool(nextUpgrade.resultTool);

            // NEW: Apply the DigToolProfile for this tier
            ApplyCurrentTierProfile();

            // Save tier to PlayerPrefs
            SaveTier();

            // Fire event
            GameEvents.OnToolUpgraded?.Invoke(nextUpgrade.resultTool);

            if (enableDebugLogs)
            {
                var profile = GetProfileForTier(currentToolTier);
                Debug.Log($"[UpgradeSystem] Upgraded from Tier {previousTier} to Tier {currentToolTier}: {nextUpgrade.upgradeName}");
                if (profile != null)
                    Debug.Log($"[UpgradeSystem] New tool profile: {profile.displayName}, maxDepth={profile.maxDigDepth}m");
            }

            return true;
        }

        /// <summary>
        /// Force set the tool tier (for testing/debugging).
        /// </summary>
        public void SetToolTier(int tier)
        {
            if (tier < 1 || tier > tierProfiles.Length)
            {
                Debug.LogWarning($"[UpgradeSystem] Invalid tier {tier}. Valid range: 1-{tierProfiles.Length}");
                return;
            }

            int previousTier = currentToolTier;
            currentToolTier = tier;
            ApplyCurrentTierProfile();
            SaveTier();

            if (enableDebugLogs)
                Debug.Log($"[UpgradeSystem] Force set tier from {previousTier} to {currentToolTier}");
        }

        /// <summary>
        /// Reset tool tier to 1 (for new game).
        /// </summary>
        public void ResetToolTier()
        {
            currentToolTier = 1;
            ApplyCurrentTierProfile();
            SaveTier();

            if (enableDebugLogs)
                Debug.Log("[UpgradeSystem] Reset to tier 1");
        }

        public List<ToolUpgrade> GetAllUpgrades()
        {
            return toolUpgrades;
        }

        public bool IsUpgradeAvailable(int tier)
        {
            return tier == currentToolTier + 1;
        }

        /// <summary>
        /// Get list of unlocked upgrade IDs (for save system).
        /// </summary>
        public List<string> GetUnlockedUpgradeIds()
        {
            var unlocked = new List<string>();
            foreach (var upgrade in toolUpgrades)
            {
                if (upgrade.tier <= currentToolTier)
                {
                    unlocked.Add(upgrade.upgradeName);
                }
            }
            return unlocked;
        }

        /// <summary>
        /// Set unlocked upgrades from save data.
        /// </summary>
        public void SetUnlockedUpgrades(List<string> upgradeIds)
        {
            if (upgradeIds == null || upgradeIds.Count == 0)
            {
                currentToolTier = 1;
            }
            else
            {
                // Find highest tier from unlocked upgrades
                int maxTier = 1;
                foreach (var upgrade in toolUpgrades)
                {
                    if (upgradeIds.Contains(upgrade.upgradeName) && upgrade.tier > maxTier)
                    {
                        maxTier = upgrade.tier;
                    }
                }
                currentToolTier = maxTier;
            }

            ApplyCurrentTierProfile();
            SaveTier();

            if (enableDebugLogs)
                Debug.Log($"[UpgradeSystem] Loaded upgrades, set tier to {currentToolTier}");
        }
    }

    [System.Serializable]
    public class ToolUpgrade
    {
        public string upgradeName;
        public int tier;
        public Dictionary<ResourceType, int> requiredResources = new Dictionary<ResourceType, int>();
        public ToolData resultTool;
        [TextArea]
        public string description;
    }
}
