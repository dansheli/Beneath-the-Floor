using UnityEngine;
using UnityEngine.Events;
using System.Collections.Generic;
using BeneathTheFloor.Interaction;
using BeneathTheFloor.Crafting;
using BeneathTheFloor.Economy;
using BeneathTheFloor.Digging;
using BeneathTheFloor.Tools;

namespace BeneathTheFloor.Machines
{
    public class UpgradeStation : MonoBehaviour, IInteractable
    {
        [Header("Station Settings")]
        [SerializeField] private string stationName = "Upgrade Station";
        [SerializeField] private bool createDefaultUpgrades = true;

        [Header("DEV Settings")]
        [Tooltip("When true, all upgrades start at Level 0 on Play, ignoring saved data. Use for testing/balancing.")]
        [SerializeField] private bool forceResetUpgradesOnStart = false;
        [Tooltip("Enable debug logging for upgrade operations.")]
        [SerializeField] private bool enableDebugLogs = false;

        [Header("Upgrade Trees")]
        [SerializeField] private List<UpgradeTree> availableTrees = new List<UpgradeTree>();

        // Runtime upgrades (in-memory, no ScriptableObjects needed)
        private List<RuntimeUpgrade> runtimeUpgrades = new List<RuntimeUpgrade>();
        public IReadOnlyList<RuntimeUpgrade> RuntimeUpgrades => runtimeUpgrades;

        [Header("Visual Feedback")]
        [SerializeField] private GameObject highlightEffect;
        [SerializeField] private Light[] panelLights;
        [SerializeField] private Color idleColor = Color.cyan;
        [SerializeField] private Color activeColor = Color.green;
        [SerializeField] private ParticleSystem upgradeParticles;

        [Header("Audio")]
        [SerializeField] private AudioSource audioSource;
        [SerializeField] private AudioClip openSound;
        [SerializeField] private AudioClip upgradeSound;
        [SerializeField] private AudioClip errorSound;

        // State
        private bool isOpen = false;
        private Dictionary<string, int> upgradeLevels = new Dictionary<string, int>();

        // Cached references for upgrades
        private Light cachedPlayerLight;
        private bool playerLightSearched = false;

        // Events
        public UnityAction OnStationOpened;
        public UnityAction OnStationClosed;
        public UnityAction<UpgradeNode> OnUpgradeApplied;
        public UnityAction<UpgradeNode> OnUpgradeFailed;
        public UnityAction<RuntimeUpgrade> OnRuntimeUpgradeApplied;

        public static UpgradeStation Instance { get; private set; }
        public static UpgradeStation CurrentStation { get; private set; }

        // Properties
        public bool CanInteract => true;
        public bool IsOpen => isOpen;
        public string StationName => stationName;
        public IReadOnlyList<UpgradeTree> AvailableTrees => availableTrees;

        private void LogDebug(string message)
        {
            if (enableDebugLogs) Debug.Log(message);
        }

        private void Awake()
        {
            if (Instance == null)
            {
                Instance = this;
            }

            if (audioSource == null)
            {
                audioSource = GetComponent<AudioSource>();
                if (audioSource == null)
                {
                    audioSource = gameObject.AddComponent<AudioSource>();
                }
            }

            // Always create default runtime upgrades if enabled
            if (createDefaultUpgrades)
            {
                CreateDefaultRuntimeUpgrades();
                if (enableDebugLogs) Debug.Log($"[UpgradeStation] Initialized with {runtimeUpgrades.Count} runtime upgrades");
            }

            // === DEV MODE: Force reset to level 0 ===
            if (forceResetUpgradesOnStart)
            {
                DevForceResetAllUpgradesToLevelZero();
            }
            else
            {
                // Normal path: load saved progress
                LoadUpgradeProgress();
                LoadRuntimeUpgradeLevels();
            }

            // Log initial state for debugging
            LogAllRuntimeUpgradeStates("[UpgradeStation] Awake complete");
        }

        private void Start()
        {
            SetPanelLights(idleColor);

            // Apply effects in Start() when other systems are initialized
            if (forceResetUpgradesOnStart)
            {
                // DEV MODE: Apply base-level (level 0) effects
                StartCoroutine(ApplyBaseLevelEffectsWithRetry());
            }
            else
            {
                // Normal path: Apply loaded upgrade effects
                StartCoroutine(ApplyUpgradeEffectsWithRetry());
            }
        }

        private System.Collections.IEnumerator ApplyBaseLevelEffectsWithRetry()
        {
            // Wait one frame to let all systems initialize
            yield return null;

            // Apply base-level effects
            ApplyBaseLevelEffectsForAllUpgrades();

            // Retry after a short delay to catch any late-initializing systems
            yield return new WaitForSeconds(0.5f);

            // Second pass
            ApplyBaseLevelEffectsForAllUpgrades();

            LogAllRuntimeUpgradeStates("[UpgradeStation] Start complete (DEV MODE)");
        }

        private System.Collections.IEnumerator ApplyUpgradeEffectsWithRetry()
        {
            // Wait one frame to let all systems initialize
            yield return null;

            // Try to apply effects
            ApplyAllLoadedUpgradeEffects();

            // If some systems were null, try again after a short delay
            yield return new WaitForSeconds(0.5f);

            // Second pass to catch any late-initializing systems
            ApplyAllLoadedUpgradeEffects();
        }

        private void CreateDefaultRuntimeUpgrades()
        {
            LogDebug("[UpgradeStation] Creating default runtime upgrades (Balance V1)...");

            // ==================== DIG SPEED - REMOVED ====================
            // Dig Speed was removed from the upgrade system as it was redundant with tool power.
            // Player progression now focuses on: tool_power, tool_radius

            // ==================== BACKPACK / INVENTORY ====================
            // NEW UPGRADE PATH:
            // L0: 5 slots, stack size 1 (base)
            // L1: 10 slots, stack size 1 (first upgrade expands slots)
            // L2-L6: 10 slots, stack size 2-6 (subsequent upgrades increase stack size)
            // Costs: L0->L1=100, L1->L2=150, L2->L3=200, L3->L4=300, L4->L5=450, L5->L6=650
            runtimeUpgrades.Add(new RuntimeUpgrade
            {
                upgradeId = "inventory_size",
                upgradeName = "Backpack",
                description = "Upgrade 1: Expand slots. Upgrades 2-6: Increase stack size.",
                category = UpgradeCategory.Player,
                upgradeType = UpgradeType.InventorySize,
                maxLevel = 6,
                currentLevel = 0,
                // Values represent upgrade level (InventorySystem handles the logic)
                valuesPerLevel = new float[] { 0f, 1f, 2f, 3f, 4f, 5f, 6f },
                creditCostsPerLevel = new int[] { 100, 150, 200, 300, 450, 650 },
                // Legacy fallback
                baseValue = 0f,
                valuePerLevel = 1f,
                baseCreditCost = 100,
                creditCostMultiplier = 1.5f,
                baseCosts = new List<ResourceRequirement>()
            });

            // ==================== HEADLAMP (Range + Intensity) ====================
            // Range: L0=8m, L1=12m, L2=16m (visibly noticeable increases)
            // Intensity: L0=2.5, L1=3.5, L2=4.5 (brighter at each level)
            // Costs: L0->L1=80, L1->L2=200
            runtimeUpgrades.Add(new RuntimeUpgrade
            {
                upgradeId = "headlamp",
                upgradeName = "Headlamp",
                description = "Upgrades your headlamp for brighter light and longer range underground.",
                category = UpgradeCategory.Player,
                upgradeType = UpgradeType.LightRadius, // Still uses LightRadius type for apply logic
                maxLevel = 2,
                currentLevel = 0,
                // Balance V1: Explicit per-level arrays (range)
                valuesPerLevel = new float[] { 8f, 12f, 16f },
                // Secondary values: intensity (2.5 base, +1.0 per level for noticeable difference)
                secondaryValuesPerLevel = new float[] { 2.5f, 3.5f, 4.5f },
                creditCostsPerLevel = new int[] { 80, 200 },
                // Legacy fallback
                baseValue = 8f,
                valuePerLevel = 4f,
                baseCreditCost = 80,
                creditCostMultiplier = 2.5f,
                baseCosts = new List<ResourceRequirement>()
            });

            // ==================== MOVE SPEED ====================
            // Speed: L0=5, L1=6, L2=7
            // Costs: L0->L1=150, L1->L2=400
            runtimeUpgrades.Add(new RuntimeUpgrade
            {
                upgradeId = "move_speed",
                upgradeName = "Move Speed",
                description = "Increases your movement speed.",
                category = UpgradeCategory.Player,
                upgradeType = UpgradeType.MoveSpeed,
                maxLevel = 2,
                currentLevel = 0,
                // Balance V1: Explicit per-level arrays
                valuesPerLevel = new float[] { 5f, 6f, 7f },
                creditCostsPerLevel = new int[] { 150, 400 },
                // Legacy fallback
                baseValue = 5f,
                valuePerLevel = 1f,
                baseCreditCost = 150,
                creditCostMultiplier = 2.5f,
                baseCosts = new List<ResourceRequirement>()
            });

            // ==================== ENERGY CAPACITY ====================
            // Capacity: L0=100, L1=125, L2=150, L3=200, L4=275, L5=375
            // Costs: L0->L1=15, L1->L2=150, L2->L3=280, L3->L4=450, L4->L5=700
            runtimeUpgrades.Add(new RuntimeUpgrade
            {
                upgradeId = "energy_capacity",
                upgradeName = "Energy Capacity",
                description = "Increases maximum energy storage.",
                category = UpgradeCategory.Energy,
                upgradeType = UpgradeType.EnergyCapacity,
                maxLevel = 5,
                currentLevel = 0,
                valuesPerLevel = new float[] { 100f, 125f, 150f, 200f, 275f, 375f },
                creditCostsPerLevel = new int[] { 15, 150, 280, 450, 700 },
                baseValue = 100f,
                valuePerLevel = 55f,
                baseCreditCost = 15,
                creditCostMultiplier = 1.8f,
                baseCosts = new List<ResourceRequirement>()
            });

            // ==================== DIG POWER UPGRADE ====================
            // Combined upgrade: affects both dig power AND radius
            // Multipliers: L0=1.0x, L1=1.05x, L2=1.10x, L3=1.15x
            // Per-tier costs (hardcoded in GetNextLevelCreditCost):
            // Tool 1: L0->L1=15, L1->L2=100, L2->L3=220
            // Tool 2: L0->L1=80, L1->L2=180, L2->L3=300
            // Tool 3: L0->L1=150, L1->L2=350, L2->L3=800
            runtimeUpgrades.Add(new RuntimeUpgrade
            {
                upgradeId = "tool_power",
                upgradeName = "Dig Power",
                description = "Increases digging power and radius.",
                category = UpgradeCategory.Tools,
                upgradeType = UpgradeType.ToolPower,
                maxLevel = 3,
                currentLevel = 0,
                valuesPerLevel = new float[] { 1.0f, 1.05f, 1.10f, 1.15f },
                creditCostsPerLevel = new int[] { 15, 100, 220 }, // Tool 1 costs (fallback)
                baseValue = 1.0f,
                valuePerLevel = 0.05f,
                baseCreditCost = 15,
                creditCostMultiplier = 2.0f,
                baseCosts = new List<ResourceRequirement>()
            });

            // ==================== TOOL TIER UPGRADE ====================
            // Upgrade to next tool tier (requires all 4 tool upgrades maxed)
            // Tier bonus: 1.12x global multiplier to power and radius (nerfed from 1.2x)
            // Upgrades are NOT reset - costs scale with tier instead
            // Costs: T1->T2=450, T2->T3=800
            runtimeUpgrades.Add(new RuntimeUpgrade
            {
                upgradeId = "tool_tier",
                upgradeName = "Upgrade Tool Tier",
                description = "Upgrade to a new tool tier. Requires all tool upgrades at max level. Applies 1.12x global bonus.",
                category = UpgradeCategory.Tools,
                upgradeType = UpgradeType.ToolTierUp,
                maxLevel = 2, // T1->T2, T2->T3
                currentLevel = 0,
                valuesPerLevel = new float[] { 1.0f, 1.12f, 1.25f }, // Cumulative: 1.0, 1.12, 1.12*1.12≈1.25 (nerfed from 1.44)
                creditCostsPerLevel = new int[] { 450, 800 },
                baseValue = 1.0f,
                valuePerLevel = 0.12f,
                baseCreditCost = 800,
                creditCostMultiplier = 2.0f,
                baseCosts = new List<ResourceRequirement>()
            });

            // ==================== LAMP PURCHASE (Shop Item) ====================
            // Buy lamps to place underground for permanent lighting
            // Each "level" = 1 lamp purchased, cost is always 50 credits
            runtimeUpgrades.Add(new RuntimeUpgrade
            {
                upgradeId = "lamp_purchase",
                upgradeName = "Buy Lamp",
                description = "Purchase a lamp to place underground. Place with [T] key.",
                category = UpgradeCategory.Lamps,
                upgradeType = UpgradeType.LampPurchase,
                maxLevel = 99, // Essentially unlimited - can buy many lamps
                currentLevel = 0, // Tracks total lamps purchased (but inventory is separate)
                // Each lamp costs 50 credits (flat cost, no scaling)
                valuesPerLevel = new float[] { 1f }, // Value = 1 lamp per purchase
                creditCostsPerLevel = new int[] { 50 }, // Always costs 50
                // Legacy fallback
                baseValue = 1f,
                valuePerLevel = 0f,
                baseCreditCost = 50,
                creditCostMultiplier = 1.0f, // No scaling - always same cost
                baseCosts = new List<ResourceRequirement>()
            });

            // ==================== WINCH CABLE LENGTH ====================
            // Upgrade the winch cable to descend deeper
            // Tiers: 0=Frayed Rope (10m), 1=Hemp (15m), 2=Steel (20m), 3=Reinforced (25m),
            //        4=Master (30m), 5=Deep Reach (40m), 6=Abyss (50m)
            // Costs: 50, 150, 300, 500, 800, 1200
            runtimeUpgrades.Add(new RuntimeUpgrade
            {
                upgradeId = "winch_cable",
                upgradeName = "Winch Cable",
                description = "Upgrade the winch cable to descend deeper into the ground.",
                category = UpgradeCategory.Winch,
                upgradeType = UpgradeType.WinchCableLength,
                maxLevel = 6,
                currentLevel = 0,
                // Combined winch upgrade (length + speed + power per tier)
                // Tier 0: 10m, 1.0x speed, 0% power
                // Tier 1: 15m, 1.2x speed, 15% power
                // Tier 2: 20m, 1.4x speed, 30% power
                // Tier 3: 25m, 1.7x speed, 50% power
                // Tier 4: 30m, 2.0x speed, 70% power
                // Tier 5: 40m, 2.3x speed, 85% power
                // Tier 6: 50m, 2.5x speed, 95% power
                valuesPerLevel = new float[] { 10f, 15f, 20f, 25f, 30f, 40f, 50f },
                creditCostsPerLevel = new int[] { 75, 200, 400, 650, 950, 1400 },
                // Legacy fallback
                baseValue = 10f,
                valuePerLevel = 5f,
                baseCreditCost = 75,
                creditCostMultiplier = 2.0f,
                baseCosts = new List<ResourceRequirement>()
            });

            // NOTE: WinchMotorSpeed and WinchMotorPower upgrades removed
            // Speed and Power are now included in the WinchCableLength upgrade

            // ==================== SONIC PULSER PURCHASE (Tool 5) ====================
            // One-time purchase that appears after ALL Tool 4 upgrades are maxed.
            // Separate from tool_tier to avoid breaking MaxOutToolUpgrades().
            runtimeUpgrades.Add(new RuntimeUpgrade
            {
                upgradeId = "sonic_pulser",
                upgradeName = "Sonic Pulser",
                description = "Purchase the Sonic Pulser - an advanced digging tool.",
                category = UpgradeCategory.Tools,
                upgradeType = UpgradeType.SonicPulserPurchase,
                maxLevel = 1,
                currentLevel = 0,
                valuesPerLevel = new float[] { 0f, 1f },
                creditCostsPerLevel = new int[] { 10000 },
                baseValue = 0f,
                valuePerLevel = 1f,
                baseCreditCost = 10000,
                creditCostMultiplier = 1.0f,
                baseCosts = new List<ResourceRequirement>()
            });

            if (enableDebugLogs) Debug.Log($"[UpgradeStation] Created {runtimeUpgrades.Count} default runtime upgrades (Balance V1)");
        }

        private void Update()
        {
            if (isOpen && Input.GetKeyDown(KeyCode.Escape))
            {
                UI.UIState.ConsumeEscape();
                CloseStation();
            }
        }

        public string GetInteractionText()
        {
            return $"Press E to use {stationName}";
        }

        public void Interact(GameObject interactor)
        {
            if (isOpen)
            {
                CloseStation();
            }
            else
            {
                OpenStation();
            }
        }

        public void OnHoverEnter()
        {
            if (highlightEffect != null)
            {
                highlightEffect.SetActive(true);
            }
        }

        public void OnHoverExit()
        {
            if (highlightEffect != null)
            {
                highlightEffect.SetActive(false);
            }
        }

        public void OpenStation()
        {
            isOpen = true;
            CurrentStation = this;
            UI.UIState.IsMachineUIOpen = true;

            // Tell interaction system that UI is open
            if (Interaction.InteractionSystem.Instance != null)
            {
                Interaction.InteractionSystem.Instance.SetUIOpen(true);
            }

            if (openSound != null && audioSource != null)
            {
                audioSource.PlayOneShot(openSound);
            }

            UpgradeStationUI.Instance?.ShowUI(this);

            if (Player.FirstPersonController.Instance != null)
            {
                Player.FirstPersonController.Instance.CanMove = false;
            }

            Cursor.lockState = CursorLockMode.None;
            Cursor.visible = true;

            SetPanelLights(activeColor);

            OnStationOpened?.Invoke();
            if (enableDebugLogs) Debug.Log($"[UpgradeStation] Opened {stationName}");
        }

        public void CloseStation()
        {
            isOpen = false;
            CurrentStation = null;

            UpgradeStationUI.Instance?.HideUI();

            // Tell interaction system that UI is closed
            if (Interaction.InteractionSystem.Instance != null)
            {
                Interaction.InteractionSystem.Instance.SetUIOpen(false);
            }

            // Re-enable player movement
            if (Player.FirstPersonController.Instance != null)
            {
                Player.FirstPersonController.Instance.CanMove = true;
                Player.FirstPersonController.Instance.enabled = true;
            }

            // Set cursor state
            Cursor.lockState = CursorLockMode.Locked;
            Cursor.visible = false;

            SetPanelLights(idleColor);

            // Clear UIState flag
            UI.UIState.IsMachineUIOpen = false;

            // Ensure game is unpaused when closing the store
            Time.timeScale = 1f;

            OnStationClosed?.Invoke();
            if (enableDebugLogs) Debug.Log($"[UpgradeStation] Closed {stationName}");
        }

        public bool CanPurchaseUpgrade(UpgradeNode upgrade)
        {
            if (upgrade == null || upgrade.IsMaxLevel) return false;

            // Check prerequisites
            foreach (var prereqId in upgrade.requiredUpgradeIds)
            {
                if (!IsUpgradeUnlocked(prereqId))
                {
                    return false;
                }
            }

            // Check cost
            var inventory = Inventory.InventorySystem.Instance;
            if (inventory == null) return false;

            var costs = upgrade.GetCostForNextLevel();
            foreach (var cost in costs)
            {
                if (inventory.GetResourceCount(cost.resourceType) < cost.amount)
                {
                    return false;
                }
            }

            return true;
        }

        public bool PurchaseUpgrade(UpgradeNode upgrade)
        {
            if (!CanPurchaseUpgrade(upgrade))
            {
                if (errorSound != null && audioSource != null)
                {
                    audioSource.PlayOneShot(errorSound);
                }
                OnUpgradeFailed?.Invoke(upgrade);
                return false;
            }

            // Consume resources
            var inventory = Inventory.InventorySystem.Instance;
            var costs = upgrade.GetCostForNextLevel();

            foreach (var cost in costs)
            {
                inventory.RemoveResource(cost.resourceType, cost.amount);
            }

            // Apply upgrade
            upgrade.currentLevel++;
            upgradeLevels[upgrade.upgradeId] = upgrade.currentLevel;

            // Apply effect
            ApplyUpgradeEffect(upgrade);

            // Play effects
            if (upgradeSound != null && audioSource != null)
            {
                audioSource.PlayOneShot(upgradeSound);
            }

            if (upgradeParticles != null)
            {
                upgradeParticles.Play();
            }

            SaveUpgradeProgress();

            OnUpgradeApplied?.Invoke(upgrade);
            if (enableDebugLogs) Debug.Log($"[UpgradeStation] Applied upgrade: {upgrade.upgradeName} Level {upgrade.currentLevel}");

            return true;
        }

        private void ApplyUpgradeEffect(UpgradeNode upgrade)
        {
            float value = upgrade.GetCurrentValue();

            switch (upgrade.upgradeType)
            {
                case UpgradeType.DigSpeed:
                    ApplyDigSpeedUpgrade(value);
                    break;

                case UpgradeType.DigPower:
                    ApplyDigPowerUpgrade(value);
                    break;

                case UpgradeType.InventorySize:
                    ApplyInventorySizeUpgrade(Mathf.RoundToInt(value));
                    break;

                case UpgradeType.LightRadius:
                    ApplyLightRadiusUpgrade(value);
                    break;

                case UpgradeType.RefinerySpeed:
                    ApplyRefinerySpeedUpgrade(value);
                    break;

                case UpgradeType.RefineryEfficiency:
                    ApplyRefineryEfficiencyUpgrade(value);
                    break;

                case UpgradeType.WorkbenchSpeed:
                    ApplyWorkbenchSpeedUpgrade(value);
                    break;

                case UpgradeType.EnergyCapacity:
                    ApplyEnergyCapacityUpgrade(value);
                    break;

                case UpgradeType.EnergyGeneration:
                    ApplyEnergyGenerationUpgrade(value);
                    break;

                default:
                    if (enableDebugLogs) Debug.Log($"[UpgradeStation] Upgrade type {upgrade.upgradeType} not yet implemented");
                    break;
            }
        }

        #region Upgrade Applications

        /// <summary>
        /// Ensures the player light reference is cached. Uses PlayerHeadlamp first, then fallback search.
        /// </summary>
        private void EnsurePlayerLight()
        {
            // If already found and valid, return
            if (cachedPlayerLight != null) return;

            // Try PlayerHeadlamp first (preferred)
            if (Player.PlayerHeadlamp.Instance != null && Player.PlayerHeadlamp.Instance.HeadlampLight != null)
            {
                cachedPlayerLight = Player.PlayerHeadlamp.Instance.HeadlampLight;
                if (enableDebugLogs) Debug.Log($"[UpgradeStation] Player light found via PlayerHeadlamp: {cachedPlayerLight.name}");
                return;
            }

            // Fallback: search in FirstPersonController children
            if (Player.FirstPersonController.Instance != null)
            {
                cachedPlayerLight = Player.FirstPersonController.Instance.GetComponentInChildren<Light>(includeInactive: true);
                if (cachedPlayerLight != null)
                {
                    if (enableDebugLogs) Debug.Log($"[UpgradeStation] Player light found in FirstPersonController children: {cachedPlayerLight.name}");
                    return;
                }
            }

            // Still null - log warning only once
            if (!playerLightSearched)
            {
                playerLightSearched = true;
                if (enableDebugLogs) Debug.LogWarning("[UpgradeStation] Player light not found. Ensure PlayerHeadlamp component is on the player.");
            }
        }

        /// <summary>
        /// Gets the player light, ensuring it's cached first.
        /// </summary>
        private Light GetPlayerLight()
        {
            EnsurePlayerLight();
            return cachedPlayerLight;
        }

        private void ApplyDigSpeedUpgrade(float multiplier)
        {
            // Apply to DiggingSystem
            if (DiggingSystem.Instance != null)
            {
                DiggingSystem.Instance.SetDigSpeedMultiplier(multiplier);
                if (enableDebugLogs) Debug.Log($"[UpgradeStation] Dig speed set to {multiplier}x");
            }
            else
            {
                if (enableDebugLogs) Debug.LogWarning($"[UpgradeStation] DiggingSystem.Instance is null - cannot apply dig speed {multiplier}x");
            }
        }

        private void ApplyDigPowerUpgrade(float value)
        {
            if (enableDebugLogs) Debug.Log($"[UpgradeStation] Dig power set to {value}");
        }

        private void ApplyInventorySizeUpgrade(int upgradeLevel)
        {
            var inventory = Inventory.InventorySystem.Instance;
            if (inventory != null)
            {
                // Sync inventory to the expected upgrade level
                while (inventory.InventoryUpgradeLevel < upgradeLevel && inventory.CanUpgradeInventory())
                {
                    inventory.TryUpgradeInventory();
                }
                if (enableDebugLogs) Debug.Log($"[UpgradeStation] Inventory upgraded to level {upgradeLevel}: slots={inventory.MaxSlots}, stackSize={inventory.CurrentMaxStackSize}");
            }
            else
            {
                if (enableDebugLogs) Debug.LogWarning("[UpgradeStation] Cannot apply inventory upgrade: InventorySystem not ready");
            }
        }

        private void ApplyLightRadiusUpgrade(float radius)
        {
            var playerLight = GetPlayerLight();
            if (playerLight != null)
            {
                float oldRadius = playerLight.range;
                playerLight.range = radius;
                if (enableDebugLogs) Debug.Log($"[UpgradeStation] Light radius set to {radius:F1}m (was {oldRadius:F1}m)");
            }
            else
            {
                if (enableDebugLogs) Debug.LogWarning($"[UpgradeStation] Cannot apply Light Radius {radius:F1}m: Player light not ready");
            }
        }

        private void ApplyRefinerySpeedUpgrade(float multiplier)
        {
            var refinery = FindObjectOfType<Refinery>();
            if (refinery != null)
            {
                refinery.SetSpeedMultiplier(multiplier);
                if (enableDebugLogs) Debug.Log($"[UpgradeStation] Refinery speed set to {multiplier}x");
            }
        }

        private void ApplyRefineryEfficiencyUpgrade(float multiplier)
        {
            var refinery = FindObjectOfType<Refinery>();
            if (refinery != null)
            {
                refinery.SetEfficiencyMultiplier(multiplier);
                if (enableDebugLogs) Debug.Log($"[UpgradeStation] Refinery efficiency set to {multiplier}x");
            }
        }

        private void ApplyWorkbenchSpeedUpgrade(float multiplier)
        {
            // NOTE: Workbench system removed - UpgradeStation is now the single upgrade point
            // This upgrade type is deprecated but kept for save compatibility
            if (enableDebugLogs) Debug.Log($"[UpgradeStation] Workbench speed upgrade not available - Workbench system removed");
        }

        private void ApplyEnergyCapacityUpgrade(float capacity)
        {
            var energyManager = Energy.EnergyManager.Instance;
            if (energyManager != null)
            {
                // Use TryUpgradeEnergy to properly increment upgrade level (affects both capacity AND regen rate)
                energyManager.TryUpgradeEnergy();
                if (enableDebugLogs) Debug.Log($"[UpgradeStation] Energy upgraded - Max: {energyManager.MaxEnergy}, Regen: {energyManager.RegenRate}/s");
            }
        }

        private void ApplyEnergyGenerationUpgrade(float rate)
        {
            if (enableDebugLogs) Debug.Log($"[UpgradeStation] Energy generation rate set to {rate}");
        }

        #endregion

        public bool IsUpgradeUnlocked(string upgradeId)
        {
            return upgradeLevels.ContainsKey(upgradeId) && upgradeLevels[upgradeId] > 0;
        }

        public int GetUpgradeLevel(string upgradeId)
        {
            return upgradeLevels.ContainsKey(upgradeId) ? upgradeLevels[upgradeId] : 0;
        }

        /// <summary>
        /// Gets the current level of a runtime upgrade by ID.
        /// </summary>
        public int GetRuntimeUpgradeLevel(string upgradeId)
        {
            foreach (var upgrade in runtimeUpgrades)
            {
                if (upgrade.upgradeId == upgradeId)
                {
                    return upgrade.currentLevel;
                }
            }
            return 0;
        }

        /// <summary>
        /// Sets a runtime upgrade level directly (used by save system).
        /// This does NOT consume resources - it's for restoring saved progress.
        /// </summary>
        public void SetRuntimeUpgradeLevel(string upgradeId, int level)
        {
            foreach (var upgrade in runtimeUpgrades)
            {
                if (upgrade.upgradeId == upgradeId)
                {
                    level = Mathf.Clamp(level, 0, upgrade.maxLevel);
                    upgrade.currentLevel = level;
                    upgradeLevels[upgradeId] = level;
                    PlayerPrefs.SetInt($"RuntimeUpgrade_{upgradeId}", level);

                    LogDebug($"[UpgradeStation] SetRuntimeUpgradeLevel: {upgradeId} set to level {level}");

                    // Apply the upgrade effect
                    ApplyRuntimeUpgradeEffect(upgrade);
                    break;
                }
            }
            PlayerPrefs.Save();
        }

        public List<UpgradeNode> GetUpgradesForCategory(UpgradeCategory category)
        {
            List<UpgradeNode> result = new List<UpgradeNode>();

            foreach (var tree in availableTrees)
            {
                if (tree.category == category)
                {
                    result.AddRange(tree.upgrades);
                }
            }

            return result;
        }

        private void SetPanelLights(Color color)
        {
            if (panelLights != null)
            {
                foreach (var light in panelLights)
                {
                    if (light != null)
                    {
                        light.color = color;
                    }
                }
            }
        }

        private void SaveUpgradeProgress()
        {
            // Save to PlayerPrefs (simple persistence)
            foreach (var kvp in upgradeLevels)
            {
                PlayerPrefs.SetInt($"Upgrade_{kvp.Key}", kvp.Value);
            }
            PlayerPrefs.Save();
        }

        private void LoadUpgradeProgress()
        {
            upgradeLevels.Clear();

            foreach (var tree in availableTrees)
            {
                foreach (var upgrade in tree.upgrades)
                {
                    string key = $"Upgrade_{upgrade.upgradeId}";
                    if (PlayerPrefs.HasKey(key))
                    {
                        int level = PlayerPrefs.GetInt(key);
                        upgrade.currentLevel = level;
                        upgradeLevels[upgrade.upgradeId] = level;
                    }
                }
            }
        }

        public void ResetAllUpgrades()
        {
            foreach (var tree in availableTrees)
            {
                foreach (var upgrade in tree.upgrades)
                {
                    upgrade.currentLevel = 0;
                    PlayerPrefs.DeleteKey($"Upgrade_{upgrade.upgradeId}");
                }
            }

            foreach (var upgrade in runtimeUpgrades)
            {
                upgrade.currentLevel = 0;
                PlayerPrefs.DeleteKey($"RuntimeUpgrade_{upgrade.upgradeId}");
            }

            upgradeLevels.Clear();
            PlayerPrefs.Save();

            LogDebug("[UpgradeStation] All upgrades reset");
        }

        #region Runtime Upgrade Methods

        /// <summary>
        /// Checks if the player can afford a runtime upgrade.
        /// V1: Requires Credits (money) as primary cost.
        /// Resource costs are checked as secondary/optional requirements.
        /// </summary>
        public bool CanPurchaseRuntimeUpgrade(RuntimeUpgrade upgrade)
        {
            if (upgrade == null) return false;

            // Special handling for tool_power: allow tier transitions when maxed
            if (upgrade.upgradeId == "tool_power")
            {
                if (upgrade.IsMaxLevel)
                {
                    // At max level - check if we can transition to next tool tier
                    if (!CanUpgradeToolTier())
                    {
                        return false; // No more tool tiers available
                    }
                    // Otherwise continue to check tier transition cost
                }
            }
            // Special handling for tool_tier: hidden from UI, only used internally
            else if (upgrade.upgradeId == "tool_tier")
            {
                return false; // Tier transitions now handled via tool_power
            }
            // Special handling for sonic_pulser: only available when all Tool 4 upgrades are maxed
            else if (upgrade.upgradeId == "sonic_pulser")
            {
                if (!IsSonicPulserAvailable()) return false;
                if (upgrade.IsMaxLevel) return false; // Already purchased
            }
            else if (upgrade.IsMaxLevel)
            {
                return false;
            }

            // === PRIMARY CHECK: Credits (Money) ===
            // For tool_power at max level, use tier transition cost
            int creditCost;
            if (upgrade.upgradeId == "tool_power" && upgrade.IsMaxLevel)
            {
                var tierUpgrade = GetRuntimeUpgradeById("tool_tier");
                creditCost = tierUpgrade?.GetNextLevelCreditCost() ?? 450;
            }
            else
            {
                creditCost = upgrade.GetNextLevelCreditCost();
            }
            if (creditCost > 0)
            {
                if (CurrencyManager.Instance == null) return false;

                int currentCredits = CurrencyManager.Instance.CurrentAmount;
                bool canAfford = CurrencyManager.Instance.CanAfford(creditCost);

                if (enableDebugLogs)
                {
                    Debug.Log($"[UpgradeStation] CanPurchase '{upgrade.upgradeName}': cost={creditCost}, have={currentCredits}, canAfford={canAfford}");
                }

                if (!canAfford)
                {
                    return false;
                }
            }

            // === SECONDARY CHECK: Resources (Optional) ===
            // If baseCosts is empty, skip resource check (credits-only mode)
            var resourceCosts = upgrade.GetCostForNextLevel();
            if (resourceCosts.Count > 0)
            {
                var inventory = Inventory.InventorySystem.Instance;
                if (inventory == null) return false;

                foreach (var cost in resourceCosts)
                {
                    if (inventory.GetResourceCount(cost.resourceType) < cost.amount)
                    {
                        return false;
                    }
                }
            }

            return true;
        }

        /// <summary>
        /// Purchases a runtime upgrade.
        /// V1: Spends Credits (money) first, then consumes optional resources.
        /// </summary>
        public bool PurchaseRuntimeUpgrade(RuntimeUpgrade upgrade)
        {
            if (enableDebugLogs) Debug.Log($"[UpgradeStation] Attempting to purchase upgrade: {upgrade.upgradeName}");

            if (!CanPurchaseRuntimeUpgrade(upgrade))
            {
                if (enableDebugLogs) Debug.Log($"[UpgradeStation] Cannot afford upgrade: {upgrade.upgradeName}");
                if (errorSound != null && audioSource != null)
                {
                    audioSource.PlayOneShot(errorSound);
                }
                return false;
            }

            // === STEP 1: Spend Credits (Primary Cost) ===
            // Check if this is a tier transition (tool_power at max level)
            bool isTierTransition = (upgrade.upgradeId == "tool_power") && upgrade.IsMaxLevel && CanUpgradeToolTier();

            int creditCost;
            if (isTierTransition)
            {
                var tierUpgrade = GetRuntimeUpgradeById("tool_tier");
                creditCost = tierUpgrade?.GetNextLevelCreditCost() ?? 450;
            }
            else
            {
                creditCost = upgrade.GetNextLevelCreditCost();
            }

            if (creditCost > 0)
            {
                if (CurrencyManager.Instance == null) return false;

                bool spent = CurrencyManager.Instance.Spend(creditCost);
                if (!spent)
                {
                    if (enableDebugLogs) Debug.LogWarning($"[UpgradeStation] Failed to spend {creditCost} credits for upgrade: {upgrade.upgradeName}");
                    if (errorSound != null && audioSource != null)
                    {
                        audioSource.PlayOneShot(errorSound);
                    }
                    return false;
                }
                if (enableDebugLogs) Debug.Log($"[UpgradeStation] Spent {creditCost} credits for {upgrade.upgradeName}");
            }

            // === STEP 2: Consume Resources (Optional/Secondary Cost) ===
            var resourceCosts = upgrade.GetCostForNextLevel();
            if (resourceCosts.Count > 0)
            {
                var inventory = Inventory.InventorySystem.Instance;
                if (inventory != null)
                {
                    foreach (var cost in resourceCosts)
                    {
                        inventory.RemoveResource(cost.resourceType, cost.amount);
                        if (enableDebugLogs) Debug.Log($"[UpgradeStation] Consumed {cost.amount}x {cost.resourceType}");
                    }
                }
            }

            // === STEP 3: Apply upgrade ===
            bool isSonicPulserPurchase = (upgrade.upgradeId == "sonic_pulser");

            if (isSonicPulserPurchase)
            {
                // Sonic Pulser purchase: set level to 1 and switch to Tool 5
                upgrade.currentLevel = 1;
                upgradeLevels[upgrade.upgradeId] = 1;
                PlayerPrefs.SetInt("RuntimeUpgrade_sonic_pulser", 1);

                // Switch to Tool 5 in HeldToolController
                if (HeldToolController.Instance != null)
                {
                    HeldToolController.Instance.SetActiveTool(4); // Index 4 = Sonic Pulser
                    if (enableDebugLogs) Debug.Log("[UpgradeStation] Switched to Tool 5: Sonic Pulser");
                }

                // Save the tool index so it persists
                PlayerPrefs.SetInt("PlayerToolIndex", 4);
                PlayerPrefs.Save();
            }
            else if (isTierTransition)
            {
                // Tier transition: reset tool_power to 0, increment tool_tier
                upgrade.currentLevel = 0;
                upgradeLevels[upgrade.upgradeId] = 0;

                // Increment tool_tier upgrade level
                var tierUpgrade = GetRuntimeUpgradeById("tool_tier");
                if (tierUpgrade != null)
                {
                    tierUpgrade.currentLevel++;
                    upgradeLevels["tool_tier"] = tierUpgrade.currentLevel;
                    PlayerPrefs.SetInt("RuntimeUpgrade_tool_tier", tierUpgrade.currentLevel);
                }

                // Get the new tier index
                int newTierIndex = tierUpgrade?.currentLevel ?? (GetCurrentToolTierIndex() + 1);
                ToolTier newTier = (ToolTier)newTierIndex;

                if (Digging.DiggingSystem.Instance != null)
                {
                    Digging.DiggingSystem.Instance.SetToolTier(newTier);
                    if (enableDebugLogs) Debug.Log($"[UpgradeStation] Tool tier upgraded to: {newTier} (Tool {newTierIndex + 1})");
                }

                // Swap visual tool (visual tiers are 1-based: 1=Wooden, 2=Basic, 3=Iron)
                int visualTier = newTierIndex + 1;
                if (Player.PlayerToolVisualController.Instance != null)
                {
                    Player.PlayerToolVisualController.Instance.UpgradeTool(visualTier);
                    if (enableDebugLogs) Debug.Log($"[UpgradeStation] Swapped visual tool to tier {visualTier}");
                }

                // Switch to the new tool in HeldToolController
                if (HeldToolController.Instance != null)
                {
                    HeldToolController.Instance.SetActiveTool(newTierIndex);
                    if (enableDebugLogs) Debug.Log($"[UpgradeStation] Switched to Tool {newTierIndex + 1}");
                }
            }
            else
            {
                // Normal level-up
                upgrade.currentLevel++;
                upgradeLevels[upgrade.upgradeId] = upgrade.currentLevel;
            }

            // Apply effect
            ApplyRuntimeUpgradeEffect(upgrade);

            // Play effects
            if (upgradeSound != null && audioSource != null)
            {
                audioSource.PlayOneShot(upgradeSound);
            }

            if (upgradeParticles != null)
            {
                upgradeParticles.Play();
            }

            SaveRuntimeUpgradeProgress();

            if (enableDebugLogs) Debug.Log($"[UpgradeStation] Upgraded {upgrade.upgradeName} to level {upgrade.currentLevel}");

            // Fire event for runtime upgrade
            OnRuntimeUpgradeApplied?.Invoke(upgrade);

            // Show toast notification for feedback
            ShowUpgradeToast(upgrade, isTierTransition);

            return true;
        }

        /// <summary>
        /// Shows a toast notification for the purchased upgrade.
        /// </summary>
        private void ShowUpgradeToast(RuntimeUpgrade upgrade, bool wasTierTransition = false)
        {
            if (UI.PickupNotificationSystem.Instance == null) return;

            string message;
            switch (upgrade.upgradeType)
            {
                case UpgradeType.LampPurchase:
                    int lampsAvailable = Lighting.LampPlacementController.Instance?.LampsAvailable ?? 0;
                    message = $"Lamp purchased! ({lampsAvailable} available)";
                    break;
                case UpgradeType.WinchCableLength:
                    if (Winch.WinchAnchor.Instance != null)
                    {
                        float cableLength = Winch.WinchAnchor.Instance.MaxCableLength;
                        float speedMult = Winch.WinchAnchor.Instance.MotorSpeedMultiplier;
                        message = $"Winch upgraded! Depth: {cableLength:F0}m, Speed: {speedMult:F1}x";
                    }
                    else
                    {
                        message = $"Winch upgraded! Max depth: {upgrade.GetCurrentValue():F0}m";
                    }
                    break;
                case UpgradeType.ToolTier:
                    message = $"{GetToolNameForLevel(upgrade.currentLevel)} unlocked!";
                    break;
                case UpgradeType.ToolLevelUp:
                    if (wasTierTransition)
                    {
                        // Just upgraded to a new tier
                        int currentTier = GetCurrentToolTierIndex();
                        string toolName = GetToolNameForLevel(currentTier);
                        message = $"{toolName} unlocked!";
                    }
                    else
                    {
                        float powerBonus = (upgrade.GetCurrentValue() - 1.0f) * 100f;
                        message = $"Tool power +{powerBonus:F0}%!";
                    }
                    break;
                case UpgradeType.LightRadius:
                    float intensity = upgrade.GetCurrentSecondaryValue();
                    if (intensity > 0f)
                    {
                        message = $"Headlamp upgraded to Lv{upgrade.currentLevel}!";
                    }
                    else
                    {
                        message = $"{upgrade.upgradeName} upgraded to Lv{upgrade.currentLevel}!";
                    }
                    break;
                case UpgradeType.ToolSpeed:
                    // DEPRECATED - kept for save compatibility
                    float speedBonus = (upgrade.GetCurrentValue() - 1.0f) * 100f;
                    message = $"Tool Speed +{speedBonus:F0}%!";
                    break;
                case UpgradeType.ToolPower:
                    float powerBonus2 = (upgrade.GetCurrentValue() - 1.0f) * 100f;
                    message = $"Dig Power +{powerBonus2:F0}%!";
                    break;
                case UpgradeType.ToolTierUp:
                    int newTier = upgrade.currentLevel + 1;
                    message = $"Tool upgraded to Tier {newTier}! All bonuses reset.";
                    break;
                case UpgradeType.SonicPulserPurchase:
                    message = "Sonic Pulser acquired!";
                    break;
                default:
                    message = $"{upgrade.upgradeName} upgraded to Lv{upgrade.currentLevel}!";
                    break;
            }

            UI.PickupNotificationSystem.Instance.ShowNotification(message);
        }

        private void ApplyRuntimeUpgradeEffect(RuntimeUpgrade upgrade)
        {
            float newValue = upgrade.GetCurrentValue();
            float oldValue = upgrade.currentLevel > 0 ? upgrade.GetValueAtLevel(upgrade.currentLevel - 1) : newValue;

            switch (upgrade.upgradeType)
            {
                case UpgradeType.DigSpeed:
                    // Apply to DiggingSystem
                    if (DiggingSystem.Instance != null)
                    {
                        DiggingSystem.Instance.SetDigSpeedMultiplier(newValue);
                        if (enableDebugLogs) Debug.Log($"[UpgradeStation] Dig Speed upgraded: {oldValue:F2}x -> {newValue:F2}x (level {upgrade.currentLevel})");
                    }
                    else
                    {
                        if (enableDebugLogs) Debug.LogWarning($"[UpgradeStation] DiggingSystem.Instance is null - cannot apply dig speed {newValue}x");
                    }
                    break;

                case UpgradeType.InventorySize:
                    var inventory = Inventory.InventorySystem.Instance;
                    if (inventory != null)
                    {
                        // Use TryUpgradeInventory which handles both slot expansion and stack size
                        bool upgraded = inventory.TryUpgradeInventory();
                        if (enableDebugLogs)
                        {
                            if (enableDebugLogs)
                            {
                                if (upgrade.currentLevel == 1)
                                    Debug.Log($"[UpgradeStation] Inventory upgraded to level {upgrade.currentLevel}: Slots expanded to {inventory.MaxSlots}");
                                else
                                    Debug.Log($"[UpgradeStation] Inventory upgraded to level {upgrade.currentLevel}: Stack size now {inventory.CurrentMaxStackSize}");
                            }
                        }
                    }
                    else
                    {
                        if (enableDebugLogs) Debug.LogWarning("[UpgradeStation] InventorySystem.Instance is null - cannot apply inventory upgrade");
                    }
                    break;

                case UpgradeType.LightRadius:
                    // Apply both range AND intensity for headlamp upgrade
                    if (Player.PlayerHeadlamp.Instance != null)
                    {
                        float oldRadius = Player.PlayerHeadlamp.Instance.Range;
                        Player.PlayerHeadlamp.Instance.SetRange(newValue);

                        // Also set intensity if secondary values exist
                        float intensity = upgrade.GetCurrentSecondaryValue();
                        if (intensity > 0f)
                        {
                            Player.PlayerHeadlamp.Instance.SetIntensity(intensity);
                            if (enableDebugLogs) Debug.Log($"[UpgradeStation] Headlamp upgraded: Range {oldRadius:F1}m -> {newValue:F1}m, Intensity -> {intensity:F1} (level {upgrade.currentLevel})");
                        }
                        else
                        {
                            if (enableDebugLogs) Debug.Log($"[UpgradeStation] Light Radius upgraded: {oldRadius:F1}m -> {newValue:F1}m (level {upgrade.currentLevel})");
                        }
                    }
                    else
                    {
                        // Fallback to direct light reference
                        var playerLightRef = GetPlayerLight();
                        if (playerLightRef != null)
                        {
                            float oldRadius = playerLightRef.range;
                            playerLightRef.range = newValue;

                            float intensity = upgrade.GetCurrentSecondaryValue();
                            if (intensity > 0f)
                            {
                                playerLightRef.intensity = intensity;
                            }

                            if (enableDebugLogs) Debug.Log($"[UpgradeStation] Light Radius upgraded: {oldRadius:F1}m -> {newValue:F1}m (level {upgrade.currentLevel})");
                        }
                        else
                        {
                            if (enableDebugLogs) Debug.LogWarning("[UpgradeStation] Cannot apply Light Radius: Player light not ready");
                        }
                    }
                    break;

                case UpgradeType.MoveSpeed:
                    var player = Player.FirstPersonController.Instance;
                    if (player != null)
                    {
                        player.SetMoveSpeed(newValue);
                        if (enableDebugLogs) Debug.Log($"[UpgradeStation] Move Speed upgraded: {oldValue:F1} -> {newValue:F1} (level {upgrade.currentLevel})");
                    }
                    else
                    {
                        if (enableDebugLogs) Debug.LogWarning("[UpgradeStation] FirstPersonController.Instance is null - cannot apply move speed upgrade");
                    }
                    break;

                case UpgradeType.EnergyCapacity:
                    var energyManager = Energy.EnergyManager.Instance;
                    if (energyManager != null)
                    {
                        // Use TryUpgradeEnergy to properly increment upgrade level (affects both capacity AND regen rate)
                        energyManager.TryUpgradeEnergy();
                        if (enableDebugLogs) Debug.Log($"[UpgradeStation] Energy upgraded - Max: {energyManager.MaxEnergy}, Regen: {energyManager.RegenRate}/s (level {energyManager.EnergyUpgradeLevel})");
                    }
                    else
                    {
                        if (enableDebugLogs) Debug.LogWarning("[UpgradeStation] EnergyManager.Instance is null - cannot apply energy capacity upgrade");
                    }
                    break;

                case UpgradeType.ToolTier:
                    ApplyToolTierUpgrade(upgrade);
                    break;

                case UpgradeType.ToolLevelUp:
                    ApplyToolLevelUpgrade(upgrade);
                    break;

                case UpgradeType.LampPurchase:
                    // Add 1 lamp to player's inventory
                    if (Lighting.LampPlacementController.Instance != null)
                    {
                        Lighting.LampPlacementController.Instance.AddLamps(1);
                        if (enableDebugLogs) Debug.Log($"[UpgradeStation] Lamp purchased! Player now has {Lighting.LampPlacementController.Instance.LampsAvailable} lamps.");
                    }
                    break;

                case UpgradeType.WinchCableLength:
                    if (Winch.WinchAnchor.Instance != null)
                    {
                        int oldTier = Winch.WinchAnchor.Instance.CurrentTierIndex;
                        Winch.WinchAnchor.Instance.SetTier(upgrade.currentLevel);
                        float newLength = Winch.WinchAnchor.Instance.MaxCableLength;
                        float speedMult = Winch.WinchAnchor.Instance.MotorSpeedMultiplier;
                        float tensionResist = Winch.WinchAnchor.Instance.TensionResistance;
                        if (enableDebugLogs) Debug.Log($"[UpgradeStation] Winch upgraded: Tier {oldTier} -> {upgrade.currentLevel} (length: {newLength:F0}m, speed: {speedMult:F1}x, power: {tensionResist * 100f:F0}%)");
                    }
                    break;

                case UpgradeType.WinchMotorSpeed:
                case UpgradeType.WinchMotorPower:
                    // DEPRECATED - speed and power now included in WinchCableLength
                    // For backwards compatibility with old saves, redirect to SetTier
                    if (Winch.WinchAnchor.Instance != null)
                    {
                        Winch.WinchAnchor.Instance.SetTier(upgrade.currentLevel);
                        if (enableDebugLogs) Debug.Log($"[UpgradeStation] Legacy winch upgrade redirected to tier {upgrade.currentLevel}");
                    }
                    break;

                case UpgradeType.ToolSpeed:
                    // DEPRECATED - kept for save compatibility
                    ApplyToolSpeedUpgrade(newValue);
                    break;

                case UpgradeType.ToolPower:
                    ApplyToolPowerUpgrade(newValue);
                    break;

                case UpgradeType.SonicPulserPurchase:
                    // Sonic Pulser purchase - switch to Tool 5 visual
                    if (HeldToolController.Instance != null)
                    {
                        HeldToolController.Instance.SetActiveTool(4);
                    }
                    if (enableDebugLogs) Debug.Log($"[UpgradeStation] Sonic Pulser purchased! (level {upgrade.currentLevel})");
                    break;

                case UpgradeType.ToolTierUp:
                    ApplyToolTierUpUpgrade(upgrade);
                    break;

                default:
                    if (enableDebugLogs) Debug.Log($"[UpgradeStation] Upgrade type {upgrade.upgradeType} applied with value: {newValue}");
                    break;
            }
        }

        /// <summary>
        /// Applies the tool tier upgrade to DiggingSystem and swaps the visual tool in player's hands.
        /// </summary>
        private void ApplyToolTierUpgrade(RuntimeUpgrade upgrade)
        {
            if (DiggingSystem.Instance == null) return;

            // Map upgrade level to tool tier
            ToolTier tier;
            string oldToolName = DiggingSystem.Instance.CurrentToolName;
            float oldDigPower = DiggingSystem.Instance.CurrentDigPower;

            switch (upgrade.currentLevel)
            {
                case 0:
                    tier = ToolTier.WoodenDigger;
                    break;
                case 1:
                    tier = ToolTier.BasicPickaxe;
                    break;
                case 2:
                    tier = ToolTier.IronPickaxe;
                    break;
                default:
                    tier = ToolTier.IronPickaxe; // Max tier
                    break;
            }

            DiggingSystem.Instance.SetToolTier(tier);

            // CRITICAL: Also swap the visual tool in the player's hands
            // Visual tier is 1-indexed (tier 1, 2, 3), upgrade level is 0-indexed (0, 1, 2)
            int visualTier = upgrade.currentLevel + 1;
            if (Player.PlayerToolVisualController.Instance != null)
            {
                Player.PlayerToolVisualController.Instance.UpgradeTool(visualTier);
                if (enableDebugLogs) Debug.Log($"[UpgradeStation] Swapped visual tool to tier {visualTier}");
            }
            else
            {
                // Fallback: try to find the controller
                var visualController = FindObjectOfType<Player.PlayerToolVisualController>();
                if (visualController != null)
                {
                    visualController.UpgradeTool(visualTier);
                    if (enableDebugLogs) Debug.Log($"[UpgradeStation] Swapped visual tool to tier {visualTier} (via FindObjectOfType)");
                }
            }

            // Reset tool level to 0 when tier up
            var toolLevelUpgrade = GetRuntimeUpgradeById("tool_level");
            if (toolLevelUpgrade != null && toolLevelUpgrade.currentLevel > 0)
            {
                toolLevelUpgrade.currentLevel = 0;
                upgradeLevels["tool_level"] = 0;
                if (enableDebugLogs) Debug.Log($"[UpgradeStation] Tool level reset to 0 after tier up");
            }

            // Recalculate and apply the combined dig power
            ApplyCombinedToolPower();

            string newToolName = DiggingSystem.Instance.CurrentToolName;
            float newDigPower = DiggingSystem.Instance.CurrentDigPower;

            if (enableDebugLogs) Debug.Log($"[UpgradeStation] Tool Tier upgraded: {oldToolName} (Power {oldDigPower:F2}) -> {newToolName} (Power {newDigPower:F2})");
        }

        /// <summary>
        /// Applies tool level upgrade (within current tier).
        /// </summary>
        private void ApplyToolLevelUpgrade(RuntimeUpgrade upgrade)
        {
            // Recalculate and apply the combined dig power
            ApplyCombinedToolPower();

            if (enableDebugLogs) Debug.Log($"[UpgradeStation] Tool Level upgraded to Lv{upgrade.currentLevel} (multiplier: {upgrade.GetCurrentValue():F2}x)");
        }

        /// <summary>
        /// Calculates and applies the combined dig power from tier base + level multiplier.
        /// Combined Power = TierBasePower * LevelMultiplier
        /// </summary>
        private void ApplyCombinedToolPower()
        {
            if (DiggingSystem.Instance == null) return;

            var toolUpgrade = GetRuntimeUpgradeById("tool_upgrade");

            // Get tier base power from current tool tier (1.0, 1.25, 1.5625)
            int tierIndex = (int)DiggingSystem.Instance.CurrentToolTier;
            float[] tierBasePowers = { 1.0f, 1.25f, 1.5625f };
            float tierBasePower = tierBasePowers[Mathf.Clamp(tierIndex, 0, 2)];

            // Get level multiplier from upgrade (1.0, 1.1, 1.2, 1.3)
            float levelMultiplier = toolUpgrade?.GetCurrentValue() ?? 1.0f;

            float combinedPower = tierBasePower * levelMultiplier;

            // Apply via dig power multiplier
            DiggingSystem.Instance.SetDigPowerMultiplier(combinedPower);

            if (enableDebugLogs) Debug.Log($"[UpgradeStation] Combined tool power: Tier{tierIndex + 1} ({tierBasePower:F2}) x Lv{toolUpgrade?.currentLevel ?? 0} ({levelMultiplier:F2}) = {combinedPower:F2}");
        }

        #region New Tool Upgrade System (Speed, Power, Radius, Tier)

        // Static multipliers that can be read by both V2 and V3 systems
        public static float ToolSpeedMultiplier { get; private set; } = 1.0f;
        public static float ToolPowerMultiplier { get; private set; } = 1.0f;
        public static float ToolRadiusMultiplier { get; private set; } = 1.0f;
        public static float ToolTierMultiplier { get; private set; } = 1.0f;
        public static int CurrentToolTier { get; private set; } = 0;

        // Super Hit Count (for Drill Pike charged attack) - read by both V2 and V3
        public static int SuperHitCount { get; private set; } = 2; // Base: 2 hits

        // First Room Upgrade Station power multiplier (separate from main upgrade station)
        public static float FirstRoomToolPowerMultiplier { get; private set; } = 1.0f;

        /// <summary>
        /// Set the super hit count (for Drill Pike charged attack).
        /// Called by FirstRoomUpgradeStationUI.
        /// </summary>
        public static void SetSuperHitCount(int count)
        {
            SuperHitCount = Mathf.Clamp(count, 1, 10);

            // Apply to V2 if available
            if (DiggingSystem.Instance != null)
            {
                DiggingSystem.Instance.SetSuperHitCount(SuperHitCount);
            }

        }

        /// <summary>
        /// Set the first room upgrade station's tool power multiplier.
        /// This stacks with the main upgrade station's multiplier.
        /// Called by FirstRoomUpgradeStationUI.
        /// </summary>
        public static void SetFirstRoomToolPowerMultiplier(float multiplier)
        {
            FirstRoomToolPowerMultiplier = Mathf.Max(0.1f, multiplier);

            // Calculate combined multiplier (first room + tier)
            float combined = FirstRoomToolPowerMultiplier * ToolTierMultiplier;

            // Apply to V2 if available
            if (DiggingSystem.Instance != null)
            {
                DiggingSystem.Instance.SetDigPowerMultiplier(combined);
            }

            // V3 reads ToolPowerMultiplier directly, so update it
            ToolPowerMultiplier = combined;
            ToolRadiusMultiplier = combined;
        }

        /// <summary>
        /// Applies tool speed upgrade to both V2 and V3 systems.
        /// </summary>
        private void ApplyToolSpeedUpgrade(float multiplier)
        {
            // Get tier multiplier
            var tierUpgrade = GetRuntimeUpgradeById("tool_tier");
            float tierMult = tierUpgrade?.GetCurrentValue() ?? 1.0f;

            // Combined multiplier = upgrade * tier
            float combined = multiplier * tierMult;
            ToolSpeedMultiplier = combined;

            // Apply to V2
            if (DiggingSystem.Instance != null)
            {
                DiggingSystem.Instance.SetDigSpeedMultiplier(combined);
            }

            // V3 will read ToolSpeedMultiplier directly
            if (enableDebugLogs) Debug.Log($"[UpgradeStation] Tool Speed: {multiplier:F2}x (tier {tierMult:F2}x) = {combined:F2}x total");
        }

        /// <summary>
        /// Applies dig power upgrade to both V2 and V3 systems.
        /// This combined upgrade affects power, radius, AND speed.
        /// Higher upgrades dig faster, stronger, and wider.
        /// Also swaps the visual tool model to match the upgrade level.
        /// </summary>
        private void ApplyToolPowerUpgrade(float multiplier)
        {
            // Get tier multiplier
            var tierUpgrade = GetRuntimeUpgradeById("tool_tier");
            float tierMult = tierUpgrade?.GetCurrentValue() ?? 1.0f;

            // Combined multiplier = upgrade * tier
            float combined = multiplier * tierMult;

            // Update power, radius, AND speed (combined upgrade)
            // Speed bonus makes higher tier tools dig faster, not just stronger
            ToolPowerMultiplier = combined;
            ToolRadiusMultiplier = combined;
            ToolSpeedMultiplier = combined;

            // Apply to V2
            if (DiggingSystem.Instance != null)
            {
                DiggingSystem.Instance.SetDigPowerMultiplier(combined);
            }

            // Update visual tool model based on power upgrade level
            // Level 0 = Base_Shovel (tier 1), Level 1 = Tier1_Shovel (tier 2), etc.
            var powerUpgrade = GetRuntimeUpgradeById("tool_power");
            if (powerUpgrade != null && HeldToolController.Instance != null)
            {
                int visualTier = powerUpgrade.currentLevel + 1; // Convert 0-3 to 1-4
                HeldToolController.Instance.SetActiveTier(visualTier);
                if (enableDebugLogs) Debug.Log($"[UpgradeStation] Visual tool updated to tier {visualTier}");
            }

            // V3 will read ToolPowerMultiplier, ToolRadiusMultiplier, and ToolSpeedMultiplier directly
            if (enableDebugLogs) Debug.Log($"[UpgradeStation] Dig Power: {multiplier:F2}x (tier {tierMult:F2}x) = {combined:F2}x total (affects power + radius + speed)");
        }

        /// <summary>
        /// Applies tool tier upgrade (switches to next tool, resets power upgrades).
        /// Tool tier 0 = Tool 1 (Shovel), tier 1 = Tool 2 (Heavy Spade), etc.
        /// </summary>
        private void ApplyToolTierUpUpgrade(RuntimeUpgrade upgrade)
        {
            int newTier = upgrade.currentLevel;
            float tierMult = upgrade.GetCurrentValue();
            ToolTierMultiplier = tierMult;
            CurrentToolTier = newTier;

            // Update tool visual (legacy)
            if (Player.PlayerToolVisualController.Instance != null)
            {
                Player.PlayerToolVisualController.Instance.UpgradeTool(newTier + 1); // 1-indexed for visual
            }

            // Switch to the new tool in HeldToolController
            // tier 0 = Tool 1 (Shovel), tier 1 = Tool 2 (Heavy Spade), etc.
            if (HeldToolController.Instance != null)
            {
                HeldToolController.Instance.SetActiveTool(newTier);
                if (enableDebugLogs) Debug.Log($"[UpgradeStation] Switched to Tool {newTier + 1} (index {newTier})");
            }

            // Update V2 tool tier
            if (DiggingSystem.Instance != null)
            {
                DiggingSystem.Instance.SetToolTier((ToolTier)newTier);
            }

            // Reset the power upgrades to L0 for the new tool
            ResetToolUpgradesForNewTier();

            // Reapply all tool effects with new tier multiplier (now at L0 values)
            ReapplyAllToolUpgrades();

            // Log confirmation of reset and new tier costs
            if (enableDebugLogs) Debug.Log($"[UpgradeStation] Tool upgraded to Tool {newTier + 1}! Power upgrades reset to L0.");
        }

        /// <summary>
        /// Resets the tool upgrades to L0 when upgrading to a new tier.
        /// This starts a new upgrade cycle with tier-scaled costs.
        /// </summary>
        private void ResetToolUpgradesForNewTier()
        {
            string[] upgradeIds = { "tool_power" };
            foreach (var id in upgradeIds)
            {
                var upgrade = GetRuntimeUpgradeById(id);
                if (upgrade != null)
                {
                    upgrade.currentLevel = 0;
                    upgradeLevels[id] = 0;
                    PlayerPrefs.SetInt($"RuntimeUpgrade_{id}", 0);
                }
            }
            PlayerPrefs.Save();

            // Log the new tier costs for verification
            if (enableDebugLogs)
            {
                int currentTier = GetCurrentToolTierIndex();
                var toolPower = GetRuntimeUpgradeById("tool_power");
                if (toolPower != null && enableDebugLogs)
                {
                    int nextCost = toolPower.GetNextLevelCreditCost();
                    Debug.Log($"[UpgradeStation] After reset: tool_power L0, next upgrade cost = {nextCost} (Tier {currentTier + 1} scaling)");
                }
            }
        }

        /// <summary>
        /// Reapplies all tool upgrades (after tier change).
        /// </summary>
        private void ReapplyAllToolUpgrades()
        {
            var powerUpgrade = GetRuntimeUpgradeById("tool_power");
            if (powerUpgrade != null) ApplyToolPowerUpgrade(powerUpgrade.GetCurrentValue());
        }

        /// <summary>
        /// Checks if all tool upgrades are at max level (required for tier up).
        /// </summary>
        public bool AreAllToolUpgradesMaxed()
        {
            string[] upgradeIds = { "tool_power" };
            foreach (var id in upgradeIds)
            {
                var upgrade = GetRuntimeUpgradeById(id);
                if (upgrade == null || upgrade.currentLevel < upgrade.maxLevel)
                    return false;
            }
            return true;
        }

        /// <summary>
        /// Checks if tool tier upgrade can be purchased.
        /// </summary>
        public bool CanUpgradeToolTier()
        {
            var tierUpgrade = GetRuntimeUpgradeById("tool_tier");
            if (tierUpgrade == null) return false;
            if (tierUpgrade.currentLevel >= tierUpgrade.maxLevel) return false;
            return AreAllToolUpgradesMaxed();
        }

        /// <summary>
        /// Returns true when the Sonic Pulser purchase is available:
        /// both tool_tier AND tool_power must be at max level.
        /// </summary>
        public bool IsSonicPulserAvailable()
        {
            var tierUpgrade = GetRuntimeUpgradeById("tool_tier");
            var powerUpgrade = GetRuntimeUpgradeById("tool_power");
            if (tierUpgrade == null || powerUpgrade == null) return false;
            return tierUpgrade.currentLevel >= tierUpgrade.maxLevel
                && powerUpgrade.currentLevel >= powerUpgrade.maxLevel;
        }

        /// <summary>
        /// Returns true when the Sonic Pulser has been purchased.
        /// </summary>
        public bool HasSonicPulser()
        {
            var sonicUpgrade = GetRuntimeUpgradeById("sonic_pulser");
            return sonicUpgrade != null && sonicUpgrade.currentLevel >= 1;
        }

        /// <summary>
        /// Maxes out tool_power and tool_tier upgrades so the upgrade station
        /// cannot downgrade a player who already has Tool 4.
        /// Called when the player picks up the Drill Pike externally.
        /// </summary>
        public void MaxOutToolUpgrades()
        {
            var toolPower = GetRuntimeUpgradeById("tool_power");
            if (toolPower != null)
            {
                toolPower.currentLevel = toolPower.maxLevel;
                upgradeLevels["tool_power"] = toolPower.currentLevel;
                PlayerPrefs.SetInt("RuntimeUpgrade_tool_power", toolPower.currentLevel);
            }

            var toolTier = GetRuntimeUpgradeById("tool_tier");
            if (toolTier != null)
            {
                toolTier.currentLevel = toolTier.maxLevel;
                upgradeLevels["tool_tier"] = toolTier.currentLevel;
                PlayerPrefs.SetInt("RuntimeUpgrade_tool_tier", toolTier.currentLevel);
            }

            PlayerPrefs.Save();
        }

        #endregion

        #region Unified Tool Upgrade Helpers

        /// <summary>
        /// Gets the current tool tier (0-2) from DiggingSystem.
        /// </summary>
        public static int GetCurrentToolTierIndex()
        {
            if (DiggingSystem.Instance == null) return 0;
            return (int)DiggingSystem.Instance.CurrentToolTier;
        }

        /// <summary>
        /// Checks if the tool upgrade is at a tier transition point (Lv3 but not max tier).
        /// </summary>
        public static bool IsToolAtTierTransition(RuntimeUpgrade upgrade)
        {
            if (upgrade == null || upgrade.upgradeId != "tool_upgrade") return false;
            int currentTier = GetCurrentToolTierIndex();
            return upgrade.currentLevel >= upgrade.maxLevel && currentTier < 2; // Tier 0,1 can transition
        }

        /// <summary>
        /// Checks if the tool upgrade is truly maxed (T3 + Lv3).
        /// </summary>
        public static bool IsToolTrulyMaxed(RuntimeUpgrade upgrade)
        {
            if (upgrade == null || upgrade.upgradeId != "tool_upgrade") return false;
            int currentTier = GetCurrentToolTierIndex();
            return upgrade.currentLevel >= upgrade.maxLevel && currentTier >= 2; // T3 (index 2) + Lv3
        }

        /// <summary>
        /// Gets the cost for the next tool upgrade (level or tier transition).
        /// </summary>
        public static int GetToolUpgradeCost(RuntimeUpgrade upgrade)
        {
            if (upgrade == null || upgrade.upgradeId != "tool_upgrade")
                return upgrade?.GetNextLevelCreditCost() ?? 0;

            // At tier transition, use secondary values for tier-up cost
            if (IsToolAtTierTransition(upgrade))
            {
                int currentTier = GetCurrentToolTierIndex();
                if (upgrade.secondaryValuesPerLevel != null && currentTier < upgrade.secondaryValuesPerLevel.Length)
                {
                    return (int)upgrade.secondaryValuesPerLevel[currentTier];
                }
                return 300; // Fallback
            }

            return upgrade.GetNextLevelCreditCost();
        }

        /// <summary>
        /// Gets the display name for the next tool tier.
        /// </summary>
        public static string GetNextToolTierName()
        {
            int currentTier = GetCurrentToolTierIndex();
            return GetToolNameForLevel(currentTier + 1);
        }

        #endregion

        /// <summary>
        /// Gets a runtime upgrade by its ID.
        /// </summary>
        public RuntimeUpgrade GetRuntimeUpgradeById(string upgradeId)
        {
            foreach (var upgrade in runtimeUpgrades)
            {
                if (upgrade.upgradeId == upgradeId)
                    return upgrade;
            }
            return null;
        }

        private void SaveRuntimeUpgradeProgress()
        {
            foreach (var upgrade in runtimeUpgrades)
            {
                PlayerPrefs.SetInt($"RuntimeUpgrade_{upgrade.upgradeId}", upgrade.currentLevel);
            }
            PlayerPrefs.Save();
        }

        /// <summary>
        /// Loads saved upgrade levels from PlayerPrefs. Does NOT apply effects.
        /// Effects are applied separately in ApplyAllLoadedUpgradeEffects().
        /// </summary>
        private void LoadRuntimeUpgradeLevels()
        {
            LogDebug("[UpgradeStation] Loading saved runtime upgrade levels...");

            foreach (var upgrade in runtimeUpgrades)
            {
                string key = $"RuntimeUpgrade_{upgrade.upgradeId}";
                if (PlayerPrefs.HasKey(key))
                {
                    int savedLevel = PlayerPrefs.GetInt(key);
                    // Clamp to valid range
                    savedLevel = Mathf.Clamp(savedLevel, 0, upgrade.maxLevel);
                    upgrade.currentLevel = savedLevel;
                    upgradeLevels[upgrade.upgradeId] = upgrade.currentLevel;

                    if (enableDebugLogs) Debug.Log($"[UpgradeStation] Loaded {upgrade.upgradeName}: Level {upgrade.currentLevel}/{upgrade.maxLevel}");
                }
                else
                {
                    if (enableDebugLogs) Debug.Log($"[UpgradeStation] No saved data for {upgrade.upgradeName}, starting at Level 0/{upgrade.maxLevel}");
                }
            }
        }

        /// <summary>
        /// Tracks which upgrades have had their effects successfully applied.
        /// </summary>
        private HashSet<string> appliedUpgradeEffects = new HashSet<string>();

        /// <summary>
        /// Applies effects for all loaded upgrade levels. Called in Start() after other systems initialize.
        /// Only applies each upgrade once, and logs if systems are unavailable.
        /// </summary>
        private void ApplyAllLoadedUpgradeEffects()
        {
            LogDebug("[UpgradeStation] Applying loaded upgrade effects...");

            int applied = 0;
            int skipped = 0;

            foreach (var upgrade in runtimeUpgrades)
            {
                // Skip if already successfully applied
                if (appliedUpgradeEffects.Contains(upgrade.upgradeId))
                {
                    skipped++;
                    continue;
                }

                bool success = TryApplyUpgradeEffect(upgrade);
                if (success)
                {
                    appliedUpgradeEffects.Add(upgrade.upgradeId);
                    applied++;
                }
            }

            if (enableDebugLogs) Debug.Log($"[UpgradeStation] Applied {applied} upgrade effects, {skipped} already applied, {runtimeUpgrades.Count - applied - skipped} pending");
        }

        /// <summary>
        /// Attempts to apply an upgrade effect. Returns true if successful.
        /// </summary>
        private bool TryApplyUpgradeEffect(RuntimeUpgrade upgrade)
        {
            float value = upgrade.GetCurrentValue();

            switch (upgrade.upgradeType)
            {
                case UpgradeType.DigSpeed:
                    if (DiggingSystem.Instance == null)
                    {
                        if (enableDebugLogs) Debug.LogWarning($"[UpgradeStation] Cannot apply {upgrade.upgradeName}: DiggingSystem not ready");
                        return false;
                    }
                    DiggingSystem.Instance.SetDigSpeedMultiplier(value);
                    if (enableDebugLogs) Debug.Log($"[UpgradeStation] Applied {upgrade.upgradeName} Lv{upgrade.currentLevel}: multiplier = {value:F2}x");
                    return true;

                case UpgradeType.InventorySize:
                    if (Inventory.InventorySystem.Instance == null)
                    {
                        if (enableDebugLogs) Debug.LogWarning($"[UpgradeStation] Cannot apply {upgrade.upgradeName}: InventorySystem not ready");
                        return false;
                    }
                    // Use TryUpgradeInventory which handles both slot expansion and stack size
                    Inventory.InventorySystem.Instance.TryUpgradeInventory();
                    if (enableDebugLogs)
                    {
                        var inv = Inventory.InventorySystem.Instance;
                        Debug.Log($"[UpgradeStation] Applied {upgrade.upgradeName} Lv{upgrade.currentLevel}: slots={inv.MaxSlots}, stackSize={inv.CurrentMaxStackSize}");
                    }
                    return true;

                case UpgradeType.LightRadius:
                    // Apply both range AND intensity for headlamp upgrade
                    if (Player.PlayerHeadlamp.Instance != null)
                    {
                        Player.PlayerHeadlamp.Instance.SetRange(value);

                        // Also set intensity if secondary values exist
                        float intensityLoad = upgrade.GetCurrentSecondaryValue();
                        if (intensityLoad > 0f)
                        {
                            Player.PlayerHeadlamp.Instance.SetIntensity(intensityLoad);
                            if (enableDebugLogs) Debug.Log($"[UpgradeStation] Applied {upgrade.upgradeName} Lv{upgrade.currentLevel}: range = {value:F1}m, intensity = {intensityLoad:F1}");
                        }
                        else
                        {
                            if (enableDebugLogs) Debug.Log($"[UpgradeStation] Applied {upgrade.upgradeName} Lv{upgrade.currentLevel}: range = {value:F1}m");
                        }
                        return true;
                    }
                    else
                    {
                        // Fallback to direct light reference
                        var playerLightTry = GetPlayerLight();
                        if (playerLightTry == null)
                        {
                            return false;
                        }
                        playerLightTry.range = value;

                        float intensityFallback = upgrade.GetCurrentSecondaryValue();
                        if (intensityFallback > 0f)
                        {
                            playerLightTry.intensity = intensityFallback;
                        }

                        if (enableDebugLogs) Debug.Log($"[UpgradeStation] Applied {upgrade.upgradeName} Lv{upgrade.currentLevel}: range = {value:F1}m");
                        return true;
                    }

                case UpgradeType.MoveSpeed:
                    if (Player.FirstPersonController.Instance == null)
                    {
                        if (enableDebugLogs) Debug.LogWarning($"[UpgradeStation] Cannot apply {upgrade.upgradeName}: Player not ready");
                        return false;
                    }
                    Player.FirstPersonController.Instance.SetMoveSpeed(value);
                    if (enableDebugLogs) Debug.Log($"[UpgradeStation] Applied {upgrade.upgradeName} Lv{upgrade.currentLevel}: speed = {value:F1}");
                    return true;

                case UpgradeType.EnergyCapacity:
                    if (Energy.EnergyManager.Instance == null)
                    {
                        if (enableDebugLogs) Debug.LogWarning($"[UpgradeStation] Cannot apply {upgrade.upgradeName}: EnergyManager not ready");
                        return false;
                    }
                    // Use TryUpgradeEnergy to properly increment upgrade level (affects both capacity AND regen rate)
                    Energy.EnergyManager.Instance.TryUpgradeEnergy();
                    if (enableDebugLogs) Debug.Log($"[UpgradeStation] Applied {upgrade.upgradeName} - Max: {Energy.EnergyManager.Instance.MaxEnergy}, Regen: {Energy.EnergyManager.Instance.RegenRate}/s");
                    return true;

                case UpgradeType.ToolTier:
                    if (DiggingSystem.Instance == null)
                    {
                        if (enableDebugLogs) Debug.LogWarning($"[UpgradeStation] Cannot apply {upgrade.upgradeName}: DiggingSystem not ready");
                        return false;
                    }
                    ToolTier tier = (ToolTier)Mathf.Clamp(upgrade.currentLevel, 0, 2);
                    DiggingSystem.Instance.SetToolTier(tier);

                    // Also swap the visual tool (tier is 1-indexed for visuals)
                    int visualTierLoad = upgrade.currentLevel + 1;
                    if (Player.PlayerToolVisualController.Instance != null)
                    {
                        Player.PlayerToolVisualController.Instance.UpgradeTool(visualTierLoad);
                    }

                    if (enableDebugLogs) Debug.Log($"[UpgradeStation] Applied {upgrade.upgradeName} Lv{upgrade.currentLevel}: tier = {tier}");
                    return true;

                case UpgradeType.ToolLevelUp:
                    // Apply combined tool power on load
                    ApplyCombinedToolPower();
                    if (enableDebugLogs) Debug.Log($"[UpgradeStation] Applied tool level {upgrade.currentLevel}");
                    return true;

                case UpgradeType.LampPurchase:
                    // Lamp inventory is managed separately by LampPlacementController
                    // Don't reapply on load - just mark as applied
                    if (enableDebugLogs) Debug.Log($"[UpgradeStation] Lamp purchases: {upgrade.currentLevel} total purchased (inventory managed separately)");
                    return true;

                case UpgradeType.WinchCableLength:
                    // Apply winch cable tier on load
                    if (Winch.WinchAnchor.Instance == null)
                    {
                        if (enableDebugLogs) Debug.LogWarning($"[UpgradeStation] Cannot apply {upgrade.upgradeName}: WinchAnchor not ready");
                        return false;
                    }
                    Winch.WinchAnchor.Instance.SetTier(upgrade.currentLevel);
                    if (enableDebugLogs) Debug.Log($"[UpgradeStation] Applied {upgrade.upgradeName} Lv{upgrade.currentLevel}: tier = {upgrade.currentLevel}, max length = {Winch.WinchAnchor.Instance.MaxCableLength:F0}m");
                    return true;

                // New tool upgrade types (Speed, Power, Radius, Luck, TierUp)
                case UpgradeType.ToolSpeed:
                    // DEPRECATED - kept for save compatibility
                    ApplyToolSpeedUpgrade(value);
                    if (enableDebugLogs) Debug.Log($"[UpgradeStation] Applied Tool Speed Lv{upgrade.currentLevel}: {value:F2}x");
                    return true;

                case UpgradeType.ToolPower:
                    ApplyToolPowerUpgrade(value);
                    if (enableDebugLogs) Debug.Log($"[UpgradeStation] Applied Dig Power Lv{upgrade.currentLevel}: {value:F2}x");
                    return true;

                case UpgradeType.SonicPulserPurchase:
                    // On load, if purchased, switch to Sonic Pulser
                    if (upgrade.currentLevel >= 1 && HeldToolController.Instance != null)
                    {
                        HeldToolController.Instance.SetActiveTool(4);
                        if (enableDebugLogs) Debug.Log("[UpgradeStation] Restored Sonic Pulser (Tool 5) from save");
                    }
                    return true;

                case UpgradeType.ToolTierUp:
                    // Apply tier multiplier and update static properties
                    ToolTierMultiplier = value;
                    CurrentToolTier = upgrade.currentLevel;
                    // Update tool visual on load (legacy)
                    if (Player.PlayerToolVisualController.Instance != null)
                    {
                        Player.PlayerToolVisualController.Instance.UpgradeTool(upgrade.currentLevel + 1);
                    }
                    // Switch to correct tool in HeldToolController on load
                    if (HeldToolController.Instance != null)
                    {
                        HeldToolController.Instance.SetActiveTool(upgrade.currentLevel);
                    }
                    // Update V2 tool tier on load
                    if (DiggingSystem.Instance != null)
                    {
                        DiggingSystem.Instance.SetToolTier((ToolTier)upgrade.currentLevel);
                    }
                    if (enableDebugLogs) Debug.Log($"[UpgradeStation] Applied Tool {upgrade.currentLevel + 1}: {value:F2}x global multiplier");
                    return true;

                default:
                    if (enableDebugLogs) Debug.Log($"[UpgradeStation] Applied {upgrade.upgradeName} Lv{upgrade.currentLevel}: value = {value:F2}");
                    return true;
            }
        }

        /// <summary>
        /// Legacy method name for backwards compatibility.
        /// </summary>
        private void LoadRuntimeUpgradeProgress()
        {
            LoadRuntimeUpgradeLevels();
            ApplyAllLoadedUpgradeEffects();
        }

        /// <summary>
        /// Public method to reload all upgrade progress from PlayerPrefs.
        /// Called by SaveManager after loading a save file.
        /// </summary>
        public void ReloadUpgradeProgress()
        {
            if (enableDebugLogs) Debug.Log("[UpgradeStation] Reloading upgrade progress from SaveManager...");

            // Reload tree upgrades
            LoadUpgradeProgress();

            // Reload runtime upgrades
            LoadRuntimeUpgradeLevels();
            ApplyAllLoadedUpgradeEffects();

            // Sync tool visual with current tier - use tool_power level to determine visual tier
            if (HeldToolController.Instance != null)
            {
                // Visual tier is based on tool_power upgrade level (0=Base, 1=Tier1, 2=Tier2, 3=Tier3)
                var powerUpgrade = GetRuntimeUpgradeById("tool_power");
                int visualTier = (powerUpgrade?.currentLevel ?? 0) + 1; // Convert 0-3 to 1-4
                HeldToolController.Instance.SetActiveTier(visualTier);
                if (enableDebugLogs) Debug.Log($"[UpgradeStation] Synced HeldToolController to visual tier {visualTier} (tool_power level: {powerUpgrade?.currentLevel ?? 0})");
            }

            if (enableDebugLogs) Debug.Log("[UpgradeStation] Upgrade progress reloaded");
        }

        /// <summary>
        /// Gets formatted cost text for display in UI.
        /// V1: Shows Credit cost first (primary), then resource costs (secondary).
        /// </summary>
        public string GetUpgradeCostText(RuntimeUpgrade upgrade)
        {
            if (upgrade == null) return "";

            System.Text.StringBuilder sb = new System.Text.StringBuilder();

            // === PRIMARY: Credit cost ===
            int creditCost = upgrade.GetNextLevelCreditCost();
            if (creditCost > 0)
            {
                int playerCredits = CurrencyManager.Instance?.CurrentAmount ?? 0;
                bool canAffordCredits = playerCredits >= creditCost;
                string creditColor = canAffordCredits ? "green" : "red";
                string currencyName = CurrencyManager.Instance?.CurrencyName ?? "Credits";
                sb.AppendLine($"<color={creditColor}>{currencyName}: {playerCredits}/{creditCost}</color>");
            }

            // === SECONDARY: Resource costs ===
            var resourceCosts = upgrade.GetCostForNextLevel();
            if (resourceCosts.Count > 0)
            {
                var inventory = Inventory.InventorySystem.Instance;
                foreach (var cost in resourceCosts)
                {
                    int have = inventory?.GetResourceCount(cost.resourceType) ?? 0;
                    string color = have >= cost.amount ? "green" : "red";
                    sb.AppendLine($"<color={color}>{cost.resourceType}: {have}/{cost.amount}</color>");
                }
            }

            return sb.ToString();
        }

        /// <summary>
        /// Gets the current level of a runtime upgrade by upgrade type.
        /// </summary>
        public int GetRuntimeUpgradeLevel(UpgradeType type)
        {
            foreach (var upgrade in runtimeUpgrades)
            {
                if (upgrade.upgradeType == type)
                {
                    return upgrade.currentLevel;
                }
            }
            return 0;
        }

        /// <summary>
        /// Gets the tool name for a given tool tier level.
        /// </summary>
        public static string GetToolNameForLevel(int level)
        {
            switch (level)
            {
                case 0: return "Wooden Digger";
                case 1: return "Basic Pickaxe";
                case 2: return "Iron Pickaxe";
                case 3: return "Drill Pike";
                case 4: return "Sonic Pulser";
                default: return "Unknown Tool";
            }
        }

        /// <summary>
        /// Gets the dig power for a given tool tier level.
        /// </summary>
        public static float GetToolPowerForLevel(int level)
        {
            switch (level)
            {
                case 0: return 1.0f;
                case 1: return 1.5f;
                case 2: return 2.2f;
                default: return 1.0f;
            }
        }

        /// <summary>
        /// Gets the max dig depth for a given tool tier level.
        /// </summary>
        public static float GetToolMaxDepthForLevel(int level)
        {
            switch (level)
            {
                case 0: return 10f;
                case 1: return 25f;
                case 2: return 43f;
                default: return 10f;
            }
        }

        #endregion

        #region DEV Tools

        /// <summary>
        /// Called in Awake when forceResetUpgradesOnStart is true.
        /// Sets all upgrades to level 0 and prepares base-level effects to be applied in Start.
        /// Does NOT load from PlayerPrefs.
        /// </summary>
        private void DevForceResetAllUpgradesToLevelZero()
        {
            LogDebug("[UpgradeStation] =========================================");
            LogDebug("[UpgradeStation] DEV MODE: forceResetUpgradesOnStart = TRUE");
            LogDebug("[UpgradeStation] All upgrades will start at Level 0.");
            LogDebug("[UpgradeStation] Saved PlayerPrefs data is being IGNORED.");
            LogDebug("[UpgradeStation] =========================================");

            // Clear any applied effects tracking
            appliedUpgradeEffects.Clear();
            upgradeLevels.Clear();

            // Reset all runtime upgrades to level 0
            foreach (var upgrade in runtimeUpgrades)
            {
                upgrade.currentLevel = 0;
                upgradeLevels[upgrade.upgradeId] = 0;
            }

            // Reset tree-based upgrades too (if any)
            foreach (var tree in availableTrees)
            {
                foreach (var upgrade in tree.upgrades)
                {
                    upgrade.currentLevel = 0;
                    upgradeLevels[upgrade.upgradeId] = 0;
                }
            }

            LogDebug("[UpgradeStation] DEV: All upgrades reset to Level 0 (ignoring saved data).");
        }

        /// <summary>
        /// Called in Start when forceResetUpgradesOnStart is true.
        /// Applies base-level effects for all upgrades (level 0 state).
        /// </summary>
        private void ApplyBaseLevelEffectsForAllUpgrades()
        {
            LogDebug("[UpgradeStation] DEV: Applying base-level effects for all upgrades...");

            // Inventory: Level 0 = 5 slots (Storage Tray style - minimal starting inventory)
            if (Inventory.InventorySystem.Instance != null)
            {
                Inventory.InventorySystem.Instance.SetMaxSlots(5);
                LogDebug("[UpgradeStation] DEV: Set inventory to 5 slots (Level 0)");
            }

            // Dig Speed: Level 0 = 1.0x multiplier
            if (DiggingSystem.Instance != null)
            {
                DiggingSystem.Instance.SetDigSpeedMultiplier(1f);
                LogDebug("[UpgradeStation] DEV: Set dig speed to 1.0x (Level 0)");
            }

            // Tool Tier: Level 0 = Wooden Digger (visual tier 1)
            if (DiggingSystem.Instance != null)
            {
                DiggingSystem.Instance.SetToolTier(ToolTier.WoodenDigger);
                LogDebug("[UpgradeStation] DEV: Set tool to Wooden Digger (Level 0)");
            }
            if (Player.PlayerToolVisualController.Instance != null)
            {
                Player.PlayerToolVisualController.Instance.UpgradeTool(1); // Visual tier 1
                LogDebug("[UpgradeStation] DEV: Set visual tool to tier 1");
            }
            // Also update HeldToolController for the new shovel visual system
            if (HeldToolController.Instance != null)
            {
                HeldToolController.Instance.SetActiveTier(1); // Base_Shovel
                LogDebug("[UpgradeStation] DEV: Set HeldToolController to tier 1 (Base_Shovel)");
            }

            // Headlamp: Level 0 = 8m range, 2.5 intensity
            if (Player.PlayerHeadlamp.Instance != null)
            {
                Player.PlayerHeadlamp.Instance.SetRange(8f);
                Player.PlayerHeadlamp.Instance.SetIntensity(2.5f);
                LogDebug("[UpgradeStation] DEV: Set headlamp to range 8m, intensity 2.5 (Level 0)");
            }
            else
            {
                var playerLightBase = GetPlayerLight();
                if (playerLightBase != null)
                {
                    playerLightBase.range = 8f;
                    playerLightBase.intensity = 2.5f;
                    LogDebug("[UpgradeStation] DEV: Set light radius to 8m, intensity 2.5 (Level 0)");
                }
                else
                {
                    LogDebug("[UpgradeStation] DEV: Headlamp will be set when player light is available");
                }
            }

            // Move Speed: Level 0 = 5
            if (Player.FirstPersonController.Instance != null)
            {
                Player.FirstPersonController.Instance.SetMoveSpeed(5f);
                LogDebug("[UpgradeStation] DEV: Set move speed to 5.0 (Level 0)");
            }

            // Energy Capacity: Level 0 = 100
            if (Energy.EnergyManager.Instance != null)
            {
                Energy.EnergyManager.Instance.SetMaxEnergy(100f);
                LogDebug("[UpgradeStation] DEV: Set energy capacity to 100 (Level 0)");
            }

            // Energy Regen Rate: Base rate is 2/s (handled in EnergyManager defaults)
            // Upgrades are handled via EnergyManager.TryUpgradeEnergy()
            LogDebug("[UpgradeStation] DEV: Energy auto-regen enabled (2/s base, upgrades via EnergyManager)");

            // Energy Dig Cost: Per-tool costs set in EnergyManager
            // Tool 1 (Shovel): 8 energy, Tool 2 (Heavy Spade): 6 energy, Tool 3 (Pickaxe): 4 energy
            LogDebug("[UpgradeStation] DEV: Energy dig costs are per-tool (8/6/4)");

            // Mark all as applied so we don't double-apply
            foreach (var upgrade in runtimeUpgrades)
            {
                appliedUpgradeEffects.Add(upgrade.upgradeId);
            }

            LogDebug("[UpgradeStation] DEV: Base-level effects applied for all upgrades.");
        }

        /// <summary>
        /// Logs the current state of all runtime upgrades with their levels.
        /// </summary>
        private void LogAllRuntimeUpgradeStates(string context)
        {
            if (!enableDebugLogs) return;

            if (runtimeUpgrades == null || runtimeUpgrades.Count == 0)
            {
                Debug.Log($"{context}: No runtime upgrades configured.");
                return;
            }

            Debug.Log($"{context}: Runtime upgrade states ({runtimeUpgrades.Count} upgrades):");

            foreach (var upgrade in runtimeUpgrades)
            {
                string upgradeId = upgrade.upgradeId;
                UpgradeType type = upgrade.upgradeType;
                int level = upgrade.currentLevel;
                int max = upgrade.maxLevel;
                float value = upgrade.GetCurrentValue();

                // Check if saved in PlayerPrefs
                string savedKey = $"RuntimeUpgrade_{upgradeId}";
                string savedInfo = PlayerPrefs.HasKey(savedKey)
                    ? $"saved={PlayerPrefs.GetInt(savedKey)}"
                    : "no save";

                Debug.Log($"  [{upgrade.upgradeName}] ({type}): Level {level}/{max}, Value={value:F2}, {savedInfo}");
            }
        }

        /// <summary>
        /// DEV ONLY: Resets all runtime upgrades to level 0 and clears saved data.
        /// Use this to test the upgrade flow from scratch.
        /// </summary>
        [ContextMenu("DEV: Reset All Upgrades")]
        public void DevResetAllUpgrades()
        {
            LogDebug("[UpgradeStation] DEV RESET: Resetting all upgrades to level 0...");

            // Clear applied effects tracking
            appliedUpgradeEffects.Clear();

            // Reset all runtime upgrade levels
            foreach (var upgrade in runtimeUpgrades)
            {
                upgrade.currentLevel = 0;
                upgradeLevels[upgrade.upgradeId] = 0;

                // Clear from PlayerPrefs
                string key = $"RuntimeUpgrade_{upgrade.upgradeId}";
                if (PlayerPrefs.HasKey(key))
                {
                    PlayerPrefs.DeleteKey(key);
                }

                if (enableDebugLogs) Debug.Log($"[UpgradeStation] Reset {upgrade.upgradeName} to Level 0/{upgrade.maxLevel}");
            }

            // Save cleared PlayerPrefs
            PlayerPrefs.Save();

            // Reset systems to base values
            // Inventory (5 slots = Level 0 for Storage Tray style)
            if (Inventory.InventorySystem.Instance != null)
            {
                Inventory.InventorySystem.Instance.SetMaxSlots(5);
                LogDebug("[UpgradeStation] Reset inventory to 5 slots");
            }

            // Dig Speed
            if (DiggingSystem.Instance != null)
            {
                DiggingSystem.Instance.SetDigSpeedMultiplier(1f);
                DiggingSystem.Instance.SetToolTier(ToolTier.WoodenDigger);
                LogDebug("[UpgradeStation] Reset dig speed to 1.0x and tool to Wooden Digger");
            }

            // Visual Tool
            if (Player.PlayerToolVisualController.Instance != null)
            {
                Player.PlayerToolVisualController.Instance.UpgradeTool(1); // Visual tier 1
                LogDebug("[UpgradeStation] Reset visual tool to tier 1");
            }

            // Headlamp
            if (Player.PlayerHeadlamp.Instance != null)
            {
                Player.PlayerHeadlamp.Instance.SetRange(8f);
                Player.PlayerHeadlamp.Instance.SetIntensity(2.5f);
                LogDebug("[UpgradeStation] Reset headlamp to range 8m, intensity 2.5");
            }
            else
            {
                var playerLightReset = GetPlayerLight();
                if (playerLightReset != null)
                {
                    playerLightReset.range = 8f;
                    playerLightReset.intensity = 2.5f;
                    LogDebug("[UpgradeStation] Reset light radius to 8m, intensity 2.5");
                }
            }

            // Move Speed
            if (Player.FirstPersonController.Instance != null)
            {
                Player.FirstPersonController.Instance.SetMoveSpeed(5f);
                LogDebug("[UpgradeStation] Reset move speed to 5.0");
            }

            // Energy
            if (Energy.EnergyManager.Instance != null)
            {
                Energy.EnergyManager.Instance.SetMaxEnergy(100f);
                LogDebug("[UpgradeStation] Reset energy capacity to 100");
            }

            LogDebug("[UpgradeStation] DEV RESET COMPLETE: All upgrades set to level 0 and base stats restored.");
        }

        /// <summary>
        /// DEV ONLY: Clears all saved upgrade data from PlayerPrefs.
        /// </summary>
        [ContextMenu("DEV: Clear Saved Upgrade Data")]
        public void DevClearSavedUpgradeData()
        {
            LogDebug("[UpgradeStation] DEV: Clearing all saved upgrade data from PlayerPrefs...");

            foreach (var upgrade in runtimeUpgrades)
            {
                string key = $"RuntimeUpgrade_{upgrade.upgradeId}";
                if (PlayerPrefs.HasKey(key))
                {
                    PlayerPrefs.DeleteKey(key);
                    if (enableDebugLogs) Debug.Log($"[UpgradeStation] Deleted key: {key}");
                }
            }

            PlayerPrefs.Save();
            LogDebug("[UpgradeStation] DEV: Saved upgrade data cleared. Restart Play Mode to see fresh state.");
        }

        /// <summary>
        /// DEV ONLY: Logs the current state of all upgrades.
        /// </summary>
#if UNITY_EDITOR
        [ContextMenu("DEV: Log Upgrade States")]
        public void DevLogUpgradeStates()
        {
            Debug.Log("========== UPGRADE STATES ==========");
            foreach (var upgrade in runtimeUpgrades)
            {
                string savedKey = $"RuntimeUpgrade_{upgrade.upgradeId}";
                int savedValue = PlayerPrefs.HasKey(savedKey) ? PlayerPrefs.GetInt(savedKey) : -1;
                float currentValue = upgrade.GetCurrentValue();

                Debug.Log($"[{upgrade.upgradeName}] " +
                         $"Level: {upgrade.currentLevel}/{upgrade.maxLevel}, " +
                         $"Value: {currentValue:F2}, " +
                         $"SavedInPrefs: {(savedValue >= 0 ? savedValue.ToString() : "none")}");
            }
            Debug.Log("=====================================");
        }
#endif

        #endregion
    }

    /// <summary>
    /// Runtime upgrade class for in-memory upgrades.
    /// V1: Uses explicit per-level arrays for values and costs (data-driven balance).
    /// Resource costs are kept as optional secondary requirements.
    /// </summary>
    [System.Serializable]
    public class RuntimeUpgrade
    {
        public string upgradeId;
        public string upgradeName;
        public string description;
        public UpgradeCategory category;
        public UpgradeType upgradeType;
        public int maxLevel = 3;
        public int currentLevel = 0;

        [Header("Per-Level Data (Balance V1)")]
        [Tooltip("Values for each level (index 0 = level 0, index 1 = level 1, etc.)")]
        public float[] valuesPerLevel;

        [Tooltip("Secondary values for each level (used for headlamp intensity, etc.)")]
        public float[] secondaryValuesPerLevel;

        [Tooltip("Credit costs to upgrade TO each level (index 0 = cost to reach L0 (usually 0), index 1 = cost L0->L1, etc.)")]
        public int[] creditCostsPerLevel;

        [Header("Legacy Fields (for backwards compatibility)")]
        public float baseValue;
        public float valuePerLevel;
        public int baseCreditCost = 100;
        public float creditCostMultiplier = 1.5f;

        [Header("Resource Costs (Optional/Secondary)")]
        [Tooltip("Optional resource costs in addition to credits. Set empty to use credits only.")]
        public List<ResourceRequirement> baseCosts = new List<ResourceRequirement>();
        public float costMultiplier = 1.5f;

        public bool IsMaxLevel => currentLevel >= maxLevel;

        /// <summary>
        /// Gets the value at the current level using per-level array.
        /// Falls back to formula if array not set.
        /// </summary>
        public float GetCurrentValue()
        {
            // Use per-level array if available
            if (valuesPerLevel != null && valuesPerLevel.Length > 0)
            {
                int index = Mathf.Clamp(currentLevel, 0, valuesPerLevel.Length - 1);
                return valuesPerLevel[index];
            }
            // Fallback to legacy formula
            return baseValue + (valuePerLevel * currentLevel);
        }

        /// <summary>
        /// Gets the value at the next level using per-level array.
        /// Falls back to formula if array not set.
        /// </summary>
        public float GetNextValue()
        {
            if (IsMaxLevel) return GetCurrentValue();

            // Use per-level array if available
            if (valuesPerLevel != null && valuesPerLevel.Length > 0)
            {
                int nextIndex = Mathf.Clamp(currentLevel + 1, 0, valuesPerLevel.Length - 1);
                return valuesPerLevel[nextIndex];
            }
            // Fallback to legacy formula
            return baseValue + (valuePerLevel * (currentLevel + 1));
        }

        /// <summary>
        /// Gets the credit cost for upgrading to the next level.
        /// Uses per-level array if available, otherwise falls back to formula.
        /// Tool power upgrades use per-tier hardcoded costs (no multiplier).
        /// </summary>
        /// <returns>Credit cost for next level, or 0 if at max level.</returns>
        public int GetNextLevelCreditCost()
        {
            if (IsMaxLevel) return 0;

            // For tool_power, use per-tier hardcoded costs
            if (upgradeId == "tool_power")
            {
                int currentTier = UpgradeStation.GetCurrentToolTierIndex();
                // Per-tier cost arrays: [L0->L1, L1->L2, L2->L3]
                int[] tier1Costs = new int[] { 15, 100, 220 };   // Tool 1
                int[] tier2Costs = new int[] { 80, 180, 300 };   // Tool 2
                int[] tier3Costs = new int[] { 150, 350, 800 };  // Tool 3

                int[] costsToUse = currentTier switch
                {
                    0 => tier1Costs,
                    1 => tier2Costs,
                    2 => tier3Costs,
                    _ => tier1Costs
                };

                if (currentLevel < costsToUse.Length)
                {
                    return costsToUse[currentLevel];
                }
                return 0;
            }

            int baseCost = 0;

            // Use per-level costs array if available
            // creditCostsPerLevel[currentLevel] = cost to go from currentLevel to currentLevel+1
            if (creditCostsPerLevel != null && creditCostsPerLevel.Length > currentLevel)
            {
                baseCost = creditCostsPerLevel[currentLevel];
            }
            else
            {
                // Fallback to legacy formula
                float multiplier = Mathf.Pow(creditCostMultiplier, currentLevel);
                baseCost = Mathf.CeilToInt(this.baseCreditCost * multiplier);
            }

            return baseCost;
        }

        /// <summary>
        /// Gets the value at a specific level (for UI preview).
        /// </summary>
        public float GetValueAtLevel(int level)
        {
            if (valuesPerLevel != null && valuesPerLevel.Length > 0)
            {
                int index = Mathf.Clamp(level, 0, valuesPerLevel.Length - 1);
                return valuesPerLevel[index];
            }
            return baseValue + (valuePerLevel * level);
        }

        /// <summary>
        /// Gets the secondary value at the current level (for headlamp intensity, etc.).
        /// </summary>
        public float GetCurrentSecondaryValue()
        {
            if (secondaryValuesPerLevel != null && secondaryValuesPerLevel.Length > 0)
            {
                int index = Mathf.Clamp(currentLevel, 0, secondaryValuesPerLevel.Length - 1);
                return secondaryValuesPerLevel[index];
            }
            return 0f; // No secondary value
        }

        /// <summary>
        /// Gets the secondary value at a specific level (for UI preview).
        /// </summary>
        public float GetSecondaryValueAtLevel(int level)
        {
            if (secondaryValuesPerLevel != null && secondaryValuesPerLevel.Length > 0)
            {
                int index = Mathf.Clamp(level, 0, secondaryValuesPerLevel.Length - 1);
                return secondaryValuesPerLevel[index];
            }
            return 0f;
        }

        public List<ResourceRequirement> GetCostForNextLevel()
        {
            List<ResourceRequirement> costs = new List<ResourceRequirement>();

            if (IsMaxLevel) return costs;

            float multiplier = Mathf.Pow(costMultiplier, currentLevel);

            foreach (var baseCost in baseCosts)
            {
                costs.Add(new ResourceRequirement
                {
                    resourceType = baseCost.resourceType,
                    amount = Mathf.CeilToInt(baseCost.amount * multiplier)
                });
            }

            return costs;
        }
    }
}
