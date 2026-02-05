using UnityEngine;
using System.Collections.Generic;
using BeneathTheFloor.Inventory;
using BeneathTheFloor.Crafting;

namespace BeneathTheFloor.Economy
{
    /// <summary>
    /// Centralized service for selling items from inventory.
    /// This service is not tied to a specific terminal - any sell point can use it.
    /// </summary>
    public class InventorySellService : MonoBehaviour
    {
        public static InventorySellService Instance { get; private set; }

        [Header("Debug")]
        [SerializeField] private bool enableDebugLogs = false;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetStatics()
        {
            Instance = null;
        }

        private void Awake()
        {
            if (Instance != null && Instance != this)
            {
                Debug.LogWarning("[InventorySellService] Duplicate instance detected, destroying this one.");
                Destroy(gameObject);
                return;
            }

            Instance = this;

            // DontDestroyOnLoad only works on root GameObjects
            if (transform.parent != null)
            {
                transform.SetParent(null);
            }
            DontDestroyOnLoad(gameObject);
        }

        private void OnDestroy()
        {
            if (Instance == this)
            {
                Instance = null;
            }
        }

        /// <summary>
        /// Sells all items from the given inventory that have a sellPrice > 0.
        /// Adds credits via CurrencyManager.
        /// </summary>
        /// <param name="inventory">The inventory to sell from.</param>
        /// <returns>The total credits gained from the sale.</returns>
        public int SellAllSellableItems(InventorySystem inventory)
        {
            if (inventory == null)
            {
                Debug.LogWarning("[InventorySellService] SellAllSellableItems: inventory is null.");
                return 0;
            }

            if (CurrencyManager.Instance == null)
            {
                Debug.LogWarning("[InventorySellService] SellAllSellableItems: CurrencyManager.Instance is null.");
                return 0;
            }

            var stacks = inventory.GetAllStacks();
            int totalCredits = 0;
            int itemTypesSold = 0;
            int totalItemsSold = 0;

            // First pass: collect all sellable items
            var toSell = new List<(ItemSO item, int amount, int pricePerUnit)>();

            foreach (var stack in stacks)
            {
                if (stack.IsEmpty) continue;
                if (stack.item == null) continue;

                int price = stack.item.sellPrice;
                if (price <= 0) continue;
                if (stack.amount <= 0) continue;

                toSell.Add((stack.item, stack.amount, price));
            }

            if (toSell.Count == 0)
            {
                if (enableDebugLogs)
                    Debug.Log("[InventorySellService] SellAllSellableItems: No sellable items found.");
                return 0;
            }

            // Second pass: remove items and add currency
            foreach (var entry in toSell)
            {
                int amount = entry.amount;
                var item = entry.item;
                int pricePerUnit = entry.pricePerUnit;

                bool removed = inventory.TryRemoveItem(item, amount);
                if (!removed)
                {
                    Debug.LogWarning($"[InventorySellService] SellAllSellableItems: Failed to remove {amount}x {item.itemName} from inventory. Skipping.");
                    continue;
                }

                int value = pricePerUnit * amount;
                totalCredits += value;
                totalItemsSold += amount;
                itemTypesSold++;

                CurrencyManager.Instance.Add(value);

                if (enableDebugLogs)
                    Debug.Log($"[InventorySellService] Sold {amount}x {item.itemName} for {value} credits ({pricePerUnit} each)");
            }

            if (enableDebugLogs)
                Debug.Log($"[InventorySellService] SellAll complete: {itemTypesSold} item types, {totalItemsSold} total items, {totalCredits} credits earned.");

            // Fire event for UI/audio feedback
            GameEvents.OnItemsSold?.Invoke(totalCredits, totalItemsSold);

            return totalCredits;
        }

        /// <summary>
        /// Sell a specific item from the given inventory.
        /// </summary>
        /// <param name="inventory">The inventory to sell from.</param>
        /// <param name="item">The item to sell.</param>
        /// <param name="amount">The amount to sell.</param>
        /// <returns>The credits gained from the sale, or 0 if the sale failed.</returns>
        public int SellItem(InventorySystem inventory, ItemSO item, int amount)
        {
            if (inventory == null || item == null || amount <= 0)
            {
                return 0;
            }

            if (CurrencyManager.Instance == null)
            {
                Debug.LogWarning("[InventorySellService] SellItem: CurrencyManager.Instance is null.");
                return 0;
            }

            if (item.sellPrice <= 0)
            {
                Debug.LogWarning($"[InventorySellService] SellItem: {item.itemName} has no sell price (sellPrice={item.sellPrice}).");
                return 0;
            }

            // Check if we have enough
            int available = inventory.GetTotalItemCount(item);
            if (available < amount)
            {
                Debug.LogWarning($"[InventorySellService] SellItem: Not enough {item.itemName}. Have {available}, want to sell {amount}.");
                return 0;
            }

            // Remove from inventory
            bool removed = inventory.TryRemoveItem(item, amount);
            if (!removed)
            {
                Debug.LogError($"[InventorySellService] SellItem: Failed to remove {amount}x {item.itemName} from inventory.");
                return 0;
            }

            // Add currency
            int value = item.sellPrice * amount;
            CurrencyManager.Instance.Add(value);

            if (enableDebugLogs)
                Debug.Log($"[InventorySellService] Sold {amount}x {item.itemName} for {value} credits.");

            // Fire event
            GameEvents.OnItemsSold?.Invoke(value, amount);

            return value;
        }

        /// <summary>
        /// Get the total value of all sellable items in the inventory without actually selling them.
        /// Useful for UI preview.
        /// </summary>
        /// <param name="inventory">The inventory to evaluate.</param>
        /// <returns>The total potential sale value.</returns>
        public int GetTotalSellableValue(InventorySystem inventory)
        {
            if (inventory == null) return 0;

            var stacks = inventory.GetAllStacks();
            int totalValue = 0;

            foreach (var stack in stacks)
            {
                if (stack.IsEmpty || stack.item == null) continue;
                if (stack.item.sellPrice <= 0) continue;

                totalValue += stack.item.sellPrice * stack.amount;
            }

            return totalValue;
        }

        /// <summary>
        /// Count how many sellable items are in the inventory.
        /// </summary>
        /// <param name="inventory">The inventory to count.</param>
        /// <returns>Total number of individual sellable items.</returns>
        public int CountSellableItems(InventorySystem inventory)
        {
            if (inventory == null) return 0;

            var stacks = inventory.GetAllStacks();
            int total = 0;

            foreach (var stack in stacks)
            {
                if (stack.IsEmpty || stack.item == null) continue;
                if (stack.item.sellPrice <= 0) continue;

                total += stack.amount;
            }

            return total;
        }

        // ============================================================
        // RESOURCE-ONLY SELL METHODS (Materials only, excludes tools)
        // ============================================================

        /// <summary>
        /// Check if an item is a sellable resource.
        /// Includes Material, Consumable, and Misc categories.
        /// Excludes Tools and Equipment (things the player uses, not sells).
        /// </summary>
        private bool IsResource(ItemSO item)
        {
            if (item == null) return false;
            if (item.sellPrice <= 0) return false;

            // Exclude tools and equipment - player shouldn't accidentally sell these
            if (item.category == ItemCategory.Tool) return false;
            if (item.category == ItemCategory.Equipment) return false;

            // Everything else with a sell price is considered a sellable resource
            return true;
        }

        /// <summary>
        /// Sells all RESOURCE items (Material category) from the inventory.
        /// Does NOT sell tools, quest items, equipment, etc.
        /// </summary>
        /// <param name="inventory">The inventory to sell from.</param>
        /// <returns>The total credits gained from the sale.</returns>
        public int SellAllResourcesOnly(InventorySystem inventory)
        {
            if (inventory == null)
            {
                Debug.LogWarning("[InventorySellService] SellAllResourcesOnly: inventory is null.");
                return 0;
            }

            if (CurrencyManager.Instance == null)
            {
                Debug.LogWarning("[InventorySellService] SellAllResourcesOnly: CurrencyManager.Instance is null.");
                return 0;
            }

            var stacks = inventory.GetAllStacks();
            int totalCredits = 0;
            int itemTypesSold = 0;
            int totalItemsSold = 0;

            // First pass: collect all sellable resource items
            var toSell = new List<(ItemSO item, int amount, int pricePerUnit)>();

            foreach (var stack in stacks)
            {
                if (stack.IsEmpty) continue;
                if (stack.item == null) continue;
                if (!IsResource(stack.item)) continue; // Only resources
                if (stack.amount <= 0) continue;

                toSell.Add((stack.item, stack.amount, stack.item.sellPrice));
            }

            if (toSell.Count == 0)
            {
                if (enableDebugLogs)
                    Debug.Log("[InventorySellService] SellAllResourcesOnly: No sellable resources found.");
                return 0;
            }

            // Second pass: remove items and add currency
            foreach (var entry in toSell)
            {
                int amount = entry.amount;
                var item = entry.item;
                int pricePerUnit = entry.pricePerUnit;

                bool removed = inventory.TryRemoveItem(item, amount);
                if (!removed)
                {
                    Debug.LogWarning($"[InventorySellService] SellAllResourcesOnly: Failed to remove {amount}x {item.itemName} from inventory. Skipping.");
                    continue;
                }

                int value = pricePerUnit * amount;
                totalCredits += value;
                totalItemsSold += amount;
                itemTypesSold++;

                CurrencyManager.Instance.Add(value);

                if (enableDebugLogs)
                    Debug.Log($"[InventorySellService] Sold resource {amount}x {item.itemName} for {value} credits ({pricePerUnit} each)");
            }

            if (enableDebugLogs)
                Debug.Log($"[InventorySellService] SellAllResourcesOnly complete: {itemTypesSold} types, {totalItemsSold} items, {totalCredits} credits earned.");

            // Fire event for UI/audio feedback
            GameEvents.OnItemsSold?.Invoke(totalCredits, totalItemsSold);

            return totalCredits;
        }

        /// <summary>
        /// Get the total value of all sellable RESOURCE items (Material category only).
        /// Useful for UI preview.
        /// </summary>
        /// <param name="inventory">The inventory to evaluate.</param>
        /// <returns>The total potential sale value of resources only.</returns>
        public int GetTotalResourceValue(InventorySystem inventory)
        {
            if (inventory == null) return 0;

            var stacks = inventory.GetAllStacks();
            int totalValue = 0;

            foreach (var stack in stacks)
            {
                if (stack.IsEmpty || stack.item == null) continue;
                if (!IsResource(stack.item)) continue; // Only resources

                totalValue += stack.item.sellPrice * stack.amount;
            }

            return totalValue;
        }

        /// <summary>
        /// Count how many sellable RESOURCE items (Material category only) are in inventory.
        /// </summary>
        /// <param name="inventory">The inventory to count.</param>
        /// <returns>Total number of individual resource items.</returns>
        public int CountResourceItems(InventorySystem inventory)
        {
            if (inventory == null) return 0;

            var stacks = inventory.GetAllStacks();
            int total = 0;

            foreach (var stack in stacks)
            {
                if (stack.IsEmpty || stack.item == null) continue;
                if (!IsResource(stack.item)) continue; // Only resources

                total += stack.amount;
            }

            return total;
        }
    }
}
