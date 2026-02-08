using UnityEngine;
using System;
using BeneathTheFloor.Economy;
using BeneathTheFloor.Digging;
using BeneathTheFloor.Machines;

namespace BeneathTheFloor.ResourceSystem
{
    /// <summary>
    /// Manages the Dust economy - dust accumulates from digging and is sold for credits.
    /// Dust is NOT an inventory item - it's a separate counter.
    /// </summary>
    public class DustManager : MonoBehaviour
    {
        public static DustManager Instance { get; private set; }

        [Header("Configuration")]
        [SerializeField] private ResourceSystemConfig config;

        // Runtime state - not serialized, always starts at 0
        private float currentDust = 0f;

        [Header("Depth Reference")]
        [SerializeField] private float basementFloorY = -3f;

        [Header("Debug")]
        [SerializeField] private bool enableDebugLogs = false;

        // Events
        public event Action<float> OnDustChanged; // Fired when dust amount changes
        public event Action<float, int> OnDustSold; // (dustAmount, creditsGained)

        // Properties
        public float CurrentDust => currentDust;
        public ResourceSystemConfig Config => config;

        private void Awake()
        {
            if (Instance != null && Instance != this)
            {
                Debug.LogWarning("[DustManager] Duplicate instance, destroying.");
                Destroy(gameObject);
                return;
            }
            Instance = this;

            // Always start with 0 dust (ignore scene-serialized value)
            currentDust = 0f;

            // Don't destroy on load for persistence
            if (transform.parent != null)
            {
                transform.SetParent(null);
            }
            DontDestroyOnLoad(gameObject);
        }

        private void Start()
        {
            // Load config if not assigned
            if (config == null)
            {
                config = Resources.Load<ResourceSystemConfig>("ResourceSystemConfig");
                if (config == null)
                {
                    Debug.LogWarning("[DustManager] No ResourceSystemConfig found. Creating default.");
                    config = ScriptableObject.CreateInstance<ResourceSystemConfig>();
                }
            }

            // Get basement floor Y from DepthManager if available
            var depthManager = FindObjectOfType<DepthManager>();
            if (depthManager != null)
            {
                basementFloorY = depthManager.BasementFloorY;
            }

            // Subscribe to dig events
            SubscribeToDigEvents();
        }

        private void OnDestroy()
        {
            if (Instance == this)
            {
                Instance = null;
            }

            UnsubscribeFromDigEvents();
        }

        private void SubscribeToDigEvents()
        {
            if (DiggingSystem.Instance != null)
            {
                DiggingSystem.Instance.OnDigCompleted += HandleDigCompleted;
            }
            else
            {
                // Try again later
                StartCoroutine(DelayedSubscribe());
            }
        }

        private System.Collections.IEnumerator DelayedSubscribe()
        {
            yield return new WaitForSeconds(0.5f);

            if (DiggingSystem.Instance != null)
            {
                DiggingSystem.Instance.OnDigCompleted += HandleDigCompleted;
            }
        }

        private void UnsubscribeFromDigEvents()
        {
            if (DiggingSystem.Instance != null)
            {
                DiggingSystem.Instance.OnDigCompleted -= HandleDigCompleted;
            }
        }

        /// <summary>
        /// Handle dig completion - calculate and add dust based on removed mass.
        /// </summary>
        private void HandleDigCompleted(Digging.DigResult result)
        {
            if (!result.Success)
                return;

            // VolumeRemoved is approximately the removed mass (density * volume)
            // In a proper implementation, we'd track actual density reduction
            float removedMass = result.VolumeRemoved;

            if (removedMass <= 0.001f)
            {
                if (enableDebugLogs)
                    Debug.Log("[DustManager] No mass removed (hit air)");
                return;
            }

            // Calculate depth
            float depth = basementFloorY - result.Operation.WorldPosition.y;
            if (depth < 0f) depth = 0f;

            // Get depth multiplier
            float depthMultiplier = config.GetDepthMultiplier(depth);

            // Calculate dust gained: removedMass × baseDustPerMass × depthMultiplier
            float dustGained = removedMass * config.baseDustPerMass * depthMultiplier;

            // Enforce minimum dust per dig so weak tools don't feel unrewarding
            if (dustGained < config.minDustPerDig)
                dustGained = config.minDustPerDig;

            if (dustGained > 0f)
            {
                AddDust(dustGained);

                if (enableDebugLogs)
                {
                    Debug.Log($"[DustManager] Gained {dustGained:F2} dust (mass={removedMass:F3}, depth={depth:F1}m, depthMult={depthMultiplier:F1}x)");
                }
            }
        }

        /// <summary>
        /// Add dust to the counter.
        /// </summary>
        public void AddDust(float amount)
        {
            if (amount <= 0f) return;

            currentDust += amount;
            OnDustChanged?.Invoke(currentDust);
        }

        /// <summary>
        /// Get current dust amount.
        /// </summary>
        public float GetDust()
        {
            return currentDust;
        }

        /// <summary>
        /// Clear all dust (after selling or for reset).
        /// </summary>
        public void ClearDust()
        {
            currentDust = 0f;
            OnDustChanged?.Invoke(currentDust);
        }

        /// <summary>
        /// Set dust amount (for loading saves).
        /// </summary>
        public void SetDust(float amount)
        {
            currentDust = Mathf.Max(0f, amount);
            OnDustChanged?.Invoke(currentDust);
        }

        /// <summary>
        /// Sell all dust for credits.
        /// Returns the credits gained.
        /// </summary>
        public int SellAllDust()
        {
            if (currentDust <= 0f)
                return 0;

            if (CurrencyManager.Instance == null)
            {
                Debug.LogWarning("[DustManager] CurrencyManager not found, cannot sell dust.");
                return 0;
            }

            // Calculate credits
            int credits = Mathf.FloorToInt(currentDust * config.dustSellValue);

            if (credits > 0)
            {
                float soldDust = currentDust;
                CurrencyManager.Instance.Add(credits);
                ClearDust();

                OnDustSold?.Invoke(soldDust, credits);

                if (enableDebugLogs)
                    Debug.Log($"[DustManager] Sold {soldDust:F1} dust for {credits} credits");

                // Fire game event
                GameEvents.OnDustSold?.Invoke(soldDust, credits);
            }

            return credits;
        }

        /// <summary>
        /// Get the credit value of current dust without selling.
        /// </summary>
        public int GetDustValue()
        {
            if (config == null) return 0;
            return Mathf.FloorToInt(currentDust * config.dustSellValue);
        }
    }
}
