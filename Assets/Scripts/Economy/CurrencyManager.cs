using UnityEngine;
using UnityEngine.Events;

namespace BeneathTheFloor.Economy
{
    /// <summary>
    /// Manages the player's currency (Credits/Coins).
    /// Singleton pattern, persists across scenes.
    /// </summary>
    public class CurrencyManager : MonoBehaviour
    {
        [Header("Settings")]
        [SerializeField] private int startingCurrency = 0;
        [SerializeField] private string currencyName = "Credits";

        [Header("Debug")]
        [SerializeField] private bool debugMode = false;

        private int currentAmount;
        private int totalEarned;

        public static CurrencyManager Instance { get; private set; }

        // Events
        public UnityAction<int> OnCurrencyChanged;
        public UnityAction<int> OnCurrencyAdded;
        public UnityAction<int> OnCurrencySpent;

        // Properties
        public int CurrentAmount => currentAmount;
        public int CurrentCurrency => currentAmount; // Alias for SaveManager
        public int TotalEarned => totalEarned;
        public string CurrencyName => currencyName;

        private void Awake()
        {
            if (Instance == null)
            {
                Instance = this;

                // DontDestroyOnLoad only works on root GameObjects
                if (transform.parent != null)
                {
                    transform.SetParent(null);
                }
                DontDestroyOnLoad(gameObject);

                InitializeCurrency();
            }
            else
            {
                Destroy(gameObject);
            }
        }

        private void InitializeCurrency()
        {
            currentAmount = startingCurrency;

            if (debugMode)
            {
                Debug.Log($"[CurrencyManager] Initialized with {currentAmount} {currencyName}");
            }
        }

        /// <summary>
        /// Add currency to the player's balance.
        /// </summary>
        public void Add(int amount)
        {
            if (amount <= 0)
            {
                Debug.LogWarning($"[CurrencyManager] Cannot add non-positive amount: {amount}");
                return;
            }

            int previousAmount = currentAmount;
            currentAmount += amount;

            totalEarned += amount;
            OnCurrencyAdded?.Invoke(amount);
            OnCurrencyChanged?.Invoke(currentAmount);
        }

        /// <summary>
        /// Set currency directly (for loading saves).
        /// </summary>
        public void SetCurrency(int amount)
        {
            SetAmount(amount);
        }

        /// <summary>
        /// Check if the player can afford a specific amount.
        /// </summary>
        public bool CanAfford(int amount)
        {
            return currentAmount >= amount;
        }

        /// <summary>
        /// Spend currency if the player has enough.
        /// Returns true if successful, false if insufficient funds.
        /// </summary>
        public bool Spend(int amount)
        {
            if (amount <= 0)
            {
                Debug.LogWarning($"[CurrencyManager] Cannot spend non-positive amount: {amount}");
                return false;
            }

            if (!CanAfford(amount))
            {
                if (debugMode)
                {
                    Debug.Log($"[CurrencyManager] Cannot afford {amount} {currencyName}. Current balance: {currentAmount}");
                }
                return false;
            }

            int previousAmount = currentAmount;
            currentAmount -= amount;

            if (debugMode)
            {
                Debug.Log($"[CurrencyManager] Spent {amount} {currencyName}. Balance: {previousAmount} -> {currentAmount}");
            }

            OnCurrencySpent?.Invoke(amount);
            OnCurrencyChanged?.Invoke(currentAmount);
            return true;
        }

        /// <summary>
        /// Set the currency to a specific amount (for loading saves, etc.).
        /// </summary>
        public void SetAmount(int amount)
        {
            if (amount < 0)
            {
                Debug.LogWarning($"[CurrencyManager] Cannot set negative amount: {amount}");
                return;
            }

            int previousAmount = currentAmount;
            currentAmount = amount;

            if (debugMode)
            {
                Debug.Log($"[CurrencyManager] Set {currencyName} to {currentAmount} (was {previousAmount})");
            }

            OnCurrencyChanged?.Invoke(currentAmount);
        }

        /// <summary>
        /// Reset currency to starting amount.
        /// </summary>
        public void Reset()
        {
            SetAmount(startingCurrency);
        }

        /// <summary>
        /// Get a formatted string for display (e.g., "1,234 Credits").
        /// </summary>
        public string GetFormattedAmount()
        {
            return $"{currentAmount:N0} {currencyName}";
        }
    }
}
