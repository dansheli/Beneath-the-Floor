using UnityEngine;
using UnityEngine.Events;
using System.Collections.Generic;
using BeneathTheFloor.UI;

namespace BeneathTheFloor.Energy
{
    public class EnergyManager : MonoBehaviour
    {
        [Header("Energy Settings")]
        [SerializeField] private float maxEnergy = 100f;
        [SerializeField] private float currentEnergy = 100f;

        [Header("Free Regen (Safety Net)")]
        [Tooltip("Free regen only activates when energy is at or below this value")]
        [SerializeField] private float freeRegenCap = 10f;
        [Tooltip("Regen rate when below freeRegenCap")]
        [SerializeField] private float baseRegenRate = 4f;
        [Tooltip("Seconds after last dig before free regen starts")]
        [SerializeField] private float regenCooldownAfterDig = 2f;
        private float timeSinceLastDig = 0f;

        [Header("Energy Upgrades")]
        [SerializeField] private int energyUpgradeLevel = 0;
        [SerializeField] private float maxEnergyPerUpgrade = 50f;  // +50 max energy per upgrade
        [SerializeField] private int maxEnergyUpgradeLevel = 7;  // L0-L7

        [Header("Dig Energy Cost")]
        [SerializeField] private float digEnergyCost = 2f;  // Flat cost per dig (all tools)

        [Header("Energy Drinks")]
        [SerializeField] private int energyDrinkCount = 0;
        [SerializeField] private int maxEnergyDrinks = 5;
        [SerializeField] private int energyDrinkCost = 10;  // Credits per drink
        [SerializeField] private KeyCode useDrinkKey = KeyCode.R;

        [Header("Low Energy Warning")]
        [SerializeField] private float lowEnergyThreshold = 20f;

        [Header("Debug")]
        [SerializeField] private bool enableDebugLogs = false;

        public static EnergyManager Instance { get; private set; }

        // Events
        public UnityAction<float, float> OnEnergyChanged; // current, max
        public UnityAction OnEnergyDepleted;
        public UnityAction OnEnergyRestored;
        public UnityAction OnLowEnergy;
        public UnityAction<int, int> OnDrinkCountChanged; // current, max
        public UnityAction OnDrinkUsed;

        // Registered energy sources and consumers
        private List<EnergySource> energySources = new List<EnergySource>();
        private List<EnergyConsumer> energyConsumers = new List<EnergyConsumer>();

        // Jetpack energy block - when true, energy cannot be replenished
        private bool energyReplenishBlocked = false;

        /// <summary>
        /// Block or unblock energy replenishment (used by jetpack).
        /// While blocked, AddEnergy, generators, and auto-regen have no effect.
        /// </summary>
        public void SetEnergyReplenishBlocked(bool blocked)
        {
            energyReplenishBlocked = blocked;
        }

        public bool IsEnergyReplenishBlocked => energyReplenishBlocked;

        // Properties
        public float CurrentEnergy => currentEnergy;
        public float MaxEnergy => GetEffectiveMaxEnergy();
        public float EnergyPercent => GetEffectiveMaxEnergy() > 0 ? currentEnergy / GetEffectiveMaxEnergy() : 0f;
        public bool HasPower => currentEnergy > 0;
        public bool IsLowEnergy => currentEnergy <= lowEnergyThreshold;
        public float DigEnergyCost => GetCurrentToolDigCost();
        public float RegenRate => GetEffectiveRegenRate();
        public bool IsRegenerating => timeSinceLastDig >= regenCooldownAfterDig && currentEnergy < freeRegenCap;
        public int DrinkCount => energyDrinkCount;
        public int MaxDrinks => maxEnergyDrinks;
        public int DrinkCost => energyDrinkCost;
        public bool CanUseDrink => energyDrinkCount > 0 && currentEnergy < GetEffectiveMaxEnergy();
        public bool CanBuyDrink => energyDrinkCount < maxEnergyDrinks;
        public int EnergyUpgradeLevel => energyUpgradeLevel;
        public int MaxEnergyUpgradeLevel => maxEnergyUpgradeLevel;
        public bool CanUpgradeEnergy => energyUpgradeLevel < maxEnergyUpgradeLevel;

        private bool wasLowEnergy = false;
        private bool wasDepleted = false;

        private void Awake()
        {
            if (Instance == null)
            {
                Instance = this;

                // Force values (override any serialized Inspector values from old system)
                baseRegenRate = 4f;
                regenCooldownAfterDig = 2f;
                freeRegenCap = 10f;
                maxEnergyPerUpgrade = 50f;
                maxEnergyUpgradeLevel = 7;
                digEnergyCost = 2f;
                energyDrinkCost = 10;
                if (enableDebugLogs) Debug.Log($"[EnergyManager] Initialized - digCost: {digEnergyCost}, freeRegenCap: {freeRegenCap}, maxLevel: {maxEnergyUpgradeLevel}");

                // DontDestroyOnLoad only works on root GameObjects
                if (transform.parent != null)
                {
                    transform.SetParent(null);
                }
                DontDestroyOnLoad(gameObject);
            }
            else
            {
                Destroy(gameObject);
            }
        }

        private void Update()
        {
            // Track time since last dig
            timeSinceLastDig += Time.deltaTime;

            // Get effective max energy (with upgrades)
            float effectiveMaxEnergy = GetEffectiveMaxEnergy();

            // Generate energy from registered sources
            float totalGeneration = 0f;
            foreach (var source in energySources)
            {
                if (source != null && source.IsActive)
                {
                    totalGeneration += source.GetGenerationRate();
                }
            }

            // Add energy from registered sources
            if (totalGeneration > 0)
            {
                AddEnergy(totalGeneration * Time.deltaTime);
            }

            // Free regen: only when energy is below freeRegenCap and cooldown has passed
            // Caps at freeRegenCap (NOT max energy) - player must use drinks to go higher
            if (!energyReplenishBlocked && timeSinceLastDig >= regenCooldownAfterDig && currentEnergy < freeRegenCap)
            {
                float regenAmount = baseRegenRate * Time.deltaTime;
                float newEnergy = Mathf.Min(currentEnergy + regenAmount, freeRegenCap);
                if (newEnergy > currentEnergy)
                {
                    currentEnergy = newEnergy;
                    OnEnergyChanged?.Invoke(currentEnergy, effectiveMaxEnergy);
                }
            }

            // Check for low energy state changes
            CheckEnergyStates();

            // Handle energy drink input (only when UI is not open)
            if (Input.GetKeyDown(useDrinkKey))
            {
                // Skip if any machine UI is open
                if (UIState.IsMachineUIOpen)
                {
                    return;
                }

                bool success = TryUseDrink();
                if (!success && energyDrinkCount <= 0)
                {
                    // Optional: show feedback that no drinks available
                }
            }
        }

        private void CheckEnergyStates()
        {
            // Check low energy
            if (IsLowEnergy && !wasLowEnergy)
            {
                wasLowEnergy = true;
                OnLowEnergy?.Invoke();
                if (enableDebugLogs) Debug.Log("[EnergyManager] Low energy warning!");
            }
            else if (!IsLowEnergy && wasLowEnergy)
            {
                wasLowEnergy = false;
            }

            // Check depleted
            if (!HasPower && !wasDepleted)
            {
                wasDepleted = true;
                OnEnergyDepleted?.Invoke();
                if (enableDebugLogs) Debug.Log("[EnergyManager] Energy depleted!");
            }
            else if (HasPower && wasDepleted)
            {
                wasDepleted = false;
                OnEnergyRestored?.Invoke();
                if (enableDebugLogs) Debug.Log("[EnergyManager] Energy restored!");
            }
        }

        public bool HasEnergy(float amount)
        {
            return currentEnergy >= amount;
        }

        public bool ConsumeEnergy(float amount)
        {
            if (amount <= 0) return true;

            if (currentEnergy >= amount)
            {
                currentEnergy -= amount;
                OnEnergyChanged?.Invoke(currentEnergy, GetEffectiveMaxEnergy());
                return true;
            }

            return false;
        }

        public void AddEnergy(float amount)
        {
            if (amount <= 0) return;

            // Jetpack blocks all energy replenishment while active
            if (energyReplenishBlocked) return;

            float effectiveMax = GetEffectiveMaxEnergy();
            float previousEnergy = currentEnergy;
            currentEnergy = Mathf.Min(currentEnergy + amount, effectiveMax);

            if (currentEnergy != previousEnergy)
            {
                OnEnergyChanged?.Invoke(currentEnergy, effectiveMax);
            }
        }

        public void SetEnergy(float amount)
        {
            float effectiveMax = GetEffectiveMaxEnergy();
            currentEnergy = Mathf.Clamp(amount, 0, effectiveMax);
            OnEnergyChanged?.Invoke(currentEnergy, effectiveMax);
        }

        public void SetMaxEnergy(float newMax)
        {
            maxEnergy = Mathf.Max(0, newMax);
            float effectiveMax = GetEffectiveMaxEnergy();
            currentEnergy = Mathf.Min(currentEnergy, effectiveMax);
            OnEnergyChanged?.Invoke(currentEnergy, effectiveMax);
        }

        public void SetBaseRegenRate(float rate)
        {
            baseRegenRate = Mathf.Max(0, rate);
        }

        /// <summary>
        /// Get the dig energy cost (flat for all tools).
        /// </summary>
        private float GetCurrentToolDigCost()
        {
            return digEnergyCost;
        }

        /// <summary>
        /// Check if player has enough energy to dig.
        /// </summary>
        public bool HasEnergyToDig()
        {
            return currentEnergy >= GetCurrentToolDigCost();
        }

        /// <summary>
        /// Attempt to consume energy for a dig action.
        /// Returns true if successful, false if not enough energy.
        /// Resets regen cooldown timer.
        /// </summary>
        public bool TryConsumeDigEnergy()
        {
            float digCost = GetCurrentToolDigCost();
            if (currentEnergy >= digCost)
            {
                currentEnergy -= digCost;
                timeSinceLastDig = 0f;  // Reset regen cooldown
                OnEnergyChanged?.Invoke(currentEnergy, GetEffectiveMaxEnergy());
                return true;
            }
            return false;
        }

        /// <summary>
        /// Attempt to consume a specific amount of energy.
        /// Returns true if successful, false if not enough energy.
        /// </summary>
        public bool TryConsumeEnergy(float amount)
        {
            if (currentEnergy >= amount)
            {
                currentEnergy -= amount;
                OnEnergyChanged?.Invoke(currentEnergy, GetEffectiveMaxEnergy());
                return true;
            }
            return false;
        }

        /// <summary>
        /// Reset the regen cooldown timer (e.g. when Sonic Pulser drains energy during charge).
        /// </summary>
        public void ResetRegenCooldown()
        {
            timeSinceLastDig = 0f;
        }

        /// <summary>
        /// Get the dig energy cost for the current tool (for calculations).
        /// </summary>
        public float GetDigEnergyCost()
        {
            return GetCurrentToolDigCost();
        }

        /// <summary>
        /// Get dig energy cost for a specific tool index (flat for all tools).
        /// </summary>
        public float GetDigEnergyCostForTool(int toolIndex)
        {
            return digEnergyCost;
        }

        /// <summary>
        /// Get effective max energy including upgrades.
        /// </summary>
        public float GetEffectiveMaxEnergy()
        {
            return maxEnergy + (energyUpgradeLevel * maxEnergyPerUpgrade);
        }

        /// <summary>
        /// Get effective regen rate (fixed, no upgrades).
        /// Free regen only applies up to freeRegenCap.
        /// </summary>
        public float GetEffectiveRegenRate()
        {
            return baseRegenRate;
        }

        /// <summary>
        /// Get the free regen energy cap.
        /// </summary>
        public float FreeRegenCap => freeRegenCap;

        /// <summary>
        /// Notify that a dig action occurred (resets regen cooldown).
        /// Call this from digging systems.
        /// </summary>
        public void NotifyDigAction()
        {
            timeSinceLastDig = 0f;
        }

        /// <summary>
        /// Upgrade energy capacity and regen rate.
        /// Returns true if successful.
        /// </summary>
        public bool TryUpgradeEnergy()
        {
            if (energyUpgradeLevel >= maxEnergyUpgradeLevel)
            {
                if (enableDebugLogs) Debug.Log("[EnergyManager] Energy already at max upgrade level!");
                return false;
            }

            energyUpgradeLevel++;

            // Refill energy to new max on upgrade
            RefillToMax();

            if (enableDebugLogs)
            {
                Debug.Log($"[EnergyManager] Energy upgraded to level {energyUpgradeLevel}! Refilled to {GetEffectiveMaxEnergy()}");
            }
            return true;
        }

        /// <summary>
        /// Get the cost for the next energy upgrade (for UI).
        /// </summary>
        public int GetEnergyUpgradeCost()
        {
            // Base cost 50, increases by 25 per level
            return 50 + (energyUpgradeLevel * 25);
        }

        /// <summary>
        /// Set upgrade level directly (for save/load).
        /// </summary>
        public void SetEnergyUpgradeLevel(int level)
        {
            energyUpgradeLevel = Mathf.Clamp(level, 0, maxEnergyUpgradeLevel);
            OnEnergyChanged?.Invoke(currentEnergy, GetEffectiveMaxEnergy());
        }

        public void RegisterSource(EnergySource source)
        {
            if (source != null && !energySources.Contains(source))
            {
                energySources.Add(source);
                if (enableDebugLogs) Debug.Log($"[EnergyManager] Registered energy source: {source.SourceName}");
            }
        }

        public void UnregisterSource(EnergySource source)
        {
            if (energySources.Contains(source))
            {
                energySources.Remove(source);
                if (enableDebugLogs) Debug.Log($"[EnergyManager] Unregistered energy source: {source.SourceName}");
            }
        }

        public void RegisterConsumer(EnergyConsumer consumer)
        {
            if (consumer != null && !energyConsumers.Contains(consumer))
            {
                energyConsumers.Add(consumer);
                if (enableDebugLogs) Debug.Log($"[EnergyManager] Registered energy consumer: {consumer.ConsumerName}");
            }
        }

        public void UnregisterConsumer(EnergyConsumer consumer)
        {
            if (energyConsumers.Contains(consumer))
            {
                energyConsumers.Remove(consumer);
                if (enableDebugLogs) Debug.Log($"[EnergyManager] Unregistered energy consumer: {consumer.ConsumerName}");
            }
        }

        public float GetTotalGenerationRate()
        {
            float total = 0f;

            foreach (var source in energySources)
            {
                if (source != null && source.IsActive)
                {
                    total += source.GetGenerationRate();
                }
            }

            return total;
        }

        public float GetTotalConsumptionRate()
        {
            float total = 0;

            foreach (var consumer in energyConsumers)
            {
                if (consumer != null && consumer.IsConsuming)
                {
                    total += consumer.GetConsumptionRate();
                }
            }

            return total;
        }

        public float GetNetEnergyRate()
        {
            return GetTotalGenerationRate() - GetTotalConsumptionRate();
        }

        public void RefillToMax()
        {
            float effectiveMax = GetEffectiveMaxEnergy();
            currentEnergy = effectiveMax;
            OnEnergyChanged?.Invoke(currentEnergy, effectiveMax);
        }

        public void DrainAll()
        {
            currentEnergy = 0;
            OnEnergyChanged?.Invoke(currentEnergy, GetEffectiveMaxEnergy());
        }

        #region Energy Drinks

        /// <summary>
        /// Try to use an energy drink. Returns true if successful.
        /// </summary>
        public bool TryUseDrink()
        {
            if (energyDrinkCount <= 0)
            {
                if (enableDebugLogs) Debug.Log("[EnergyManager] No energy drinks available!");
                return false;
            }

            float effectiveMax = GetEffectiveMaxEnergy();
            if (currentEnergy >= effectiveMax)
            {
                if (enableDebugLogs) Debug.Log("[EnergyManager] Energy already full!");
                return false;
            }

            energyDrinkCount--;
            RefillToMax();
            OnDrinkCountChanged?.Invoke(energyDrinkCount, maxEnergyDrinks);
            OnDrinkUsed?.Invoke();
            if (enableDebugLogs) Debug.Log($"[EnergyManager] Used energy drink! Remaining: {energyDrinkCount}/{maxEnergyDrinks}");
            return true;
        }

        /// <summary>
        /// Try to buy an energy drink using credits. Returns true if successful.
        /// </summary>
        public bool TryBuyDrink()
        {
            if (energyDrinkCount >= maxEnergyDrinks)
            {
                if (enableDebugLogs) Debug.Log("[EnergyManager] Already at max energy drinks!");
                return false;
            }

            // Check if player has enough credits
            var currencyManager = Economy.CurrencyManager.Instance;
            if (currencyManager == null)
            {
                Debug.LogWarning("[EnergyManager] CurrencyManager not found!");
                return false;
            }

            if (currencyManager.CurrentAmount < energyDrinkCost)
            {
                if (enableDebugLogs) Debug.Log($"[EnergyManager] Not enough credits! Need {energyDrinkCost}, have {currencyManager.CurrentAmount}");
                return false;
            }

            // Deduct credits and add drink
            if (currencyManager.Spend(energyDrinkCost))
            {
                energyDrinkCount++;
                OnDrinkCountChanged?.Invoke(energyDrinkCount, maxEnergyDrinks);
                if (enableDebugLogs) Debug.Log($"[EnergyManager] Bought energy drink for {energyDrinkCost} credits! Now have: {energyDrinkCount}/{maxEnergyDrinks}");
                return true;
            }

            return false;
        }

        /// <summary>
        /// Add drinks directly (for testing/cheats).
        /// </summary>
        public void AddDrinks(int count)
        {
            energyDrinkCount = Mathf.Clamp(energyDrinkCount + count, 0, maxEnergyDrinks);
            OnDrinkCountChanged?.Invoke(energyDrinkCount, maxEnergyDrinks);
        }

        /// <summary>
        /// Set drink count directly.
        /// </summary>
        public void SetDrinkCount(int count)
        {
            energyDrinkCount = Mathf.Clamp(count, 0, maxEnergyDrinks);
            OnDrinkCountChanged?.Invoke(energyDrinkCount, maxEnergyDrinks);
        }

        #endregion

        public string GetEnergyStatus()
        {
            string regenStatus = IsRegenerating ? $"(Free regen to {freeRegenCap})" : currentEnergy <= freeRegenCap ? $"(Cooldown: {Mathf.Max(0, regenCooldownAfterDig - timeSinceLastDig):F1}s)" : "";
            return $"Energy: {currentEnergy:F1}/{GetEffectiveMaxEnergy():F1} ({EnergyPercent * 100:F0}%) {regenStatus}\n" +
                   $"Dig Cost: {digEnergyCost} | Drinks: {energyDrinkCount}/{maxEnergyDrinks} ({energyDrinkCost} credits)\n" +
                   $"Capacity Lv.{energyUpgradeLevel}";
        }
    }
}
