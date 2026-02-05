using UnityEngine;
using UnityEngine.Events;
using System.Collections.Generic;
using System.Linq;
using BeneathTheFloor.Crafting;
using BeneathTheFloor.UI;
using BeneathTheFloor.ResourceSystem;

namespace BeneathTheFloor.Inventory
{
    /// <summary>
    /// Maps ResourceType to ItemSO for inventory integration.
    /// </summary>
    [System.Serializable]
    public class ResourceItemMapping
    {
        public ResourceType resourceType;
        public ItemSO item;
    }
    /// <summary>
    /// Sort modes for inventory items.
    /// </summary>
    public enum InventorySortMode
    {
        ByName,
        ByType,
        ByAmount
    }

    /// <summary>
    /// Represents a stack of items in an inventory slot.
    /// </summary>
    [System.Serializable]
    public struct ItemStack
    {
        public ItemSO item;
        public int amount;

        public bool IsEmpty => item == null || amount <= 0;

        public ItemStack(ItemSO item, int amount)
        {
            this.item = item;
            this.amount = amount;
        }

        public static ItemStack Empty => new ItemStack(null, 0);
    }

    public class InventorySystem : MonoBehaviour
    {
        [Header("Inventory Settings")]
        // HARDCODED: Initial slot count is EXACTLY 5
        // Can be expanded via Backpack upgrades at runtime
        private const int INITIAL_MAX_SLOTS = 5;
        private const int UPGRADED_MAX_SLOTS = 10;
        private int maxSlots = INITIAL_MAX_SLOTS;

        // STACKING: Can be upgraded from 1 to 6
        // Upgrade path: First upgrade = slots (5->10), then upgrades 2-6 = stack size (1->6)
        private int currentMaxStackSize = 1;
        private int inventoryUpgradeLevel = 0; // 0 = base, 1 = slots upgraded, 2-6 = stack sizes 2-6
        private const int MAX_STACK_SIZE_UPGRADE_LEVEL = 6;

        [Header("Debug")]
        [SerializeField] private bool enableDebugLogs = false;

        [Header("Starting Resources (Debug)")]
        [SerializeField] private bool giveStartingResources = false; // Disabled by default now
        [SerializeField] private int startingDirt = 50;
        [SerializeField] private int startingClay = 20;
        [SerializeField] private int startingCoal = 10;
        [SerializeField] private int startingIronIngot = 10;
        [SerializeField] private int startingCopperIngot = 10;

        [Header("Resource-to-Item Mapping")]
        [SerializeField] private List<ResourceItemMapping> resourceItemMappings = new List<ResourceItemMapping>();
        [SerializeField] private bool createRuntimeResourceItems = true;

        [Header("Resource System Integration")]
        [Tooltip("Reference to ResourceSystemConfig for icons. If not set, will try to find in Resources folder.")]
        [SerializeField] private ResourceSystemConfig resourceSystemConfig;

        // =====================================================================
        // CORE V1: Primary storage - slot-based inventory for UI grid and trading
        // =====================================================================
        private ItemStack[] slotGrid;

        // =====================================================================
        // LEGACY: These structures are kept for backwards compatibility with
        // crafting system and other systems that use ResourceType-based APIs.
        // Migration path: Systems should transition to ItemSO-based slot grid APIs.
        // =====================================================================

        // LEGACY: ResourceType-based counting (used by crafting, HUD, recipes)
        // Note: Kept for backwards compatibility - use slotGrid-based APIs for new code
        private Dictionary<ResourceType, int> resources = new Dictionary<ResourceType, int>();

        // LEGACY: ResourceType to ItemSO mapping (used by AddResource bridge)
        private Dictionary<ResourceType, ItemSO> resourceToItemMap = new Dictionary<ResourceType, ItemSO>();

        // LEGACY: String-keyed item slots (no longer primary storage, kept for compatibility)
        // Note: Kept for backwards compatibility - use slotGrid and GetAllStacks() instead
        private Dictionary<string, InventorySlot> itemSlots = new Dictionary<string, InventorySlot>();

        // Other state
        private List<StoryItem> collectedStoryItems = new List<StoryItem>();
        private List<ToolData> ownedTools = new List<ToolData>();
        private ToolData currentTool;

        public static InventorySystem Instance { get; private set; }

        // Events
        public UnityAction<ItemSO, int> OnItemAdded;
        public UnityAction<ItemSO, int> OnItemRemoved;
        public UnityAction<ToolData> OnToolAdded;
        public UnityAction OnInventoryChanged; // Fired when any slot changes
        public UnityAction<int, int> OnInventoryCapacityChanged; // Fired when maxSlots changes (oldMax, newMax)

        public int MaxSlots => maxSlots;
        public int SlotCount => slotGrid?.Length ?? 0;
        public ToolData CurrentTool => currentTool;
        public IReadOnlyDictionary<ResourceType, int> Resources => resources;
        public IReadOnlyList<StoryItem> StoryItems => collectedStoryItems;
        public IReadOnlyList<ToolData> OwnedTools => ownedTools;
        public int CurrentMaxStackSize => currentMaxStackSize;
        public int InventoryUpgradeLevel => inventoryUpgradeLevel;
        public int MaxInventoryUpgradeLevel => MAX_STACK_SIZE_UPGRADE_LEVEL;

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

                InitializeInventory();
            }
            else if (Instance != this)
            {
                Debug.LogWarning("[InventorySystem] Duplicate instance detected! Destroying this one. " +
                                 $"Existing: {Instance.gameObject.name}, New: {gameObject.name}");
                Destroy(gameObject);
            }
        }

        private void InitializeInventory()
        {
            // ALWAYS start with exactly 5 slots (override any old serialized values)
            maxSlots = INITIAL_MAX_SLOTS;

            // Initialize all resource types to 0
            foreach (ResourceType type in System.Enum.GetValues(typeof(ResourceType)))
            {
                resources[type] = 0;
            }

            // Initialize slot grid with exactly 5 slots
            slotGrid = new ItemStack[maxSlots];
            for (int i = 0; i < maxSlots; i++)
            {
                slotGrid[i] = ItemStack.Empty;
            }

            // Initialize resource-to-item mapping
            InitializeResourceItemMapping();

            // Give starting resources if enabled (for testing)
            if (giveStartingResources)
            {
                // Use AddResource so items also go to slotGrid
                AddResource(ResourceType.Dirt, startingDirt);
                AddResource(ResourceType.Clay, startingClay);
                AddResource(ResourceType.Coal, startingCoal);
                AddResource(ResourceType.IronIngot, startingIronIngot);
                AddResource(ResourceType.CopperIngot, startingCopperIngot);
                if (enableDebugLogs) Debug.Log($"[InventorySystem] Starting resources added to inventory grid");
            }
        }

        /// <summary>
        /// Initialize resource-to-item mapping. Creates runtime ItemSO objects if needed.
        /// </summary>
        private void InitializeResourceItemMapping()
        {
            resourceToItemMap.Clear();

            // First, use any assigned mappings from Inspector
            foreach (var mapping in resourceItemMappings)
            {
                if (mapping.item != null)
                {
                    resourceToItemMap[mapping.resourceType] = mapping.item;
                }
            }

            // Create runtime items for all resource types that don't have mappings
            if (createRuntimeResourceItems)
            {
                foreach (ResourceType type in System.Enum.GetValues(typeof(ResourceType)))
                {
                    if (!resourceToItemMap.ContainsKey(type))
                    {
                        ItemSO runtimeItem = CreateRuntimeMaterialItem(type);
                        if (runtimeItem != null)
                        {
                            resourceToItemMap[type] = runtimeItem;
                        }
                    }
                }
                if (enableDebugLogs) Debug.Log($"[InventorySystem] Created {resourceToItemMap.Count} resource-to-item mappings");
            }
        }

        /// <summary>
        /// Creates a runtime MaterialItemSO for a ResourceType.
        /// </summary>
        private ItemSO CreateRuntimeMaterialItem(ResourceType type)
        {
            MaterialItemSO item = ScriptableObject.CreateInstance<MaterialItemSO>();

            // Format name: IronOre -> "Iron Ore", CompressedDirt -> "Compressed Dirt"
            string formattedName = FormatResourceName(type.ToString());

            item.itemId = $"underground_{type.ToString().ToLower()}";
            item.itemName = formattedName;
            item.description = GetResourceDescription(type);
            item.icon = LoadResourceIcon(type); // Try to load icon from Resources folder
            item.category = ItemCategory.Material;
            item.isStackable = true;
            item.maxStackSize = 99;
            item.sourceResource = type;

            // Determine material type
            item.materialType = GetMaterialType(type);

            // Set sell price based on resource type
            int sellPrice = GetResourceSellPrice(type);
            item.sellPrice = sellPrice;

            if (enableDebugLogs && item.icon != null)
            {
                Debug.Log($"[InventorySystem] Loaded icon for {type}: {item.icon.name}");
            }

            return item;
        }

        /// <summary>
        /// Try to load a resource icon. First checks ResourceSystemConfig, then falls back to Resources folder.
        /// </summary>
        private Sprite LoadResourceIcon(ResourceType type)
        {
            // FIRST: Try to get icon from ResourceSystemConfig
            if (resourceSystemConfig != null)
            {
                Sprite configIcon = resourceSystemConfig.GetIconByResourceType(type);
                if (configIcon != null)
                {
                    if (enableDebugLogs)
                        Debug.Log($"[InventorySystem] Loaded icon for {type} from ResourceSystemConfig");
                    return configIcon;
                }
            }

            // FALLBACK: Try to load from Resources folder
            // Icons should be placed in: Assets/Resources/Icons/icon_[resourcename].png
            string resourceName = type.ToString().ToLower();
            string iconPath = $"Icons/icon_{resourceName}";

            Sprite icon = UnityEngine.Resources.Load<Sprite>(iconPath);

            if (icon == null && enableDebugLogs)
            {
                Debug.Log($"[InventorySystem] No icon found for {type} (checked ResourceSystemConfig and Resources/{iconPath})");
            }

            return icon;
        }

        /// <summary>
        /// Get the sell price for a resource type.
        /// First checks ResourceSystemConfig, then falls back to hardcoded values.
        /// </summary>
        private int GetResourceSellPrice(ResourceType type)
        {
            // FIRST: Try to get price from ResourceSystemConfig
            if (resourceSystemConfig != null)
            {
                int configPrice = resourceSystemConfig.GetCreditValueByResourceType(type);
                if (configPrice > 0)
                {
                    if (enableDebugLogs)
                        Debug.Log($"[InventorySystem] Using ResourceSystemConfig price for {type}: {configPrice}");
                    return configPrice;
                }
            }

            // FALLBACK: Hardcoded values for resources not in config
            return type switch
            {
                // Raw materials (cheap)
                ResourceType.Dirt => 1,
                ResourceType.Clay => 2,
                ResourceType.Coal => 4,
                ResourceType.IronOre => 6,
                ResourceType.Copper => 5,
                ResourceType.Silver => 15,
                ResourceType.Gold => 30,

                // Refined materials (more valuable)
                ResourceType.CompressedDirt => 3,
                ResourceType.HardenedClay => 5,
                ResourceType.RefinedCoal => 10,
                ResourceType.IronIngot => 15,
                ResourceType.CopperIngot => 12,
                ResourceType.SilverIngot => 40,
                ResourceType.GoldIngot => 80,

                // Processed materials (most valuable)
                ResourceType.WoodenPlank => 4,
                ResourceType.MetalPlate => 25,
                ResourceType.CopperWire => 20,
                ResourceType.Circuit => 50,

                // Special
                ResourceType.AncientArtifact => 100,

                _ => 1 // Default price
            };
        }

        private string FormatResourceName(string name)
        {
            // Insert space before capital letters: "IronOre" -> "Iron Ore"
            var result = new System.Text.StringBuilder();
            for (int i = 0; i < name.Length; i++)
            {
                if (i > 0 && char.IsUpper(name[i]))
                {
                    result.Append(' ');
                }
                result.Append(name[i]);
            }
            return result.ToString();
        }

        private string GetResourceDescription(ResourceType type)
        {
            return type switch
            {
                ResourceType.Dirt => "Common soil from digging. Used for basic crafting.",
                ResourceType.Clay => "Soft clay found underground. Can be refined.",
                ResourceType.Coal => "Combustible mineral. Used for smelting and fuel.",
                ResourceType.IronOre => "Raw iron ore. Must be smelted into ingots.",
                ResourceType.Copper => "Raw copper. Can be refined into ingots.",
                ResourceType.Silver => "Precious silver ore. Valuable when refined.",
                ResourceType.Gold => "Rare gold ore. Extremely valuable.",
                ResourceType.AncientArtifact => "A mysterious relic from the past.",
                ResourceType.CompressedDirt => "Compressed dirt block. More durable.",
                ResourceType.HardenedClay => "Hardened clay. Used for construction.",
                ResourceType.RefinedCoal => "Purified coal. Burns more efficiently.",
                ResourceType.IronIngot => "Refined iron. Used for metal crafting.",
                ResourceType.CopperIngot => "Refined copper. Used for wiring.",
                ResourceType.SilverIngot => "Refined silver. High conductivity.",
                ResourceType.GoldIngot => "Refined gold. Used for advanced components.",
                ResourceType.WoodenPlank => "A basic wooden plank for construction.",
                ResourceType.MetalPlate => "A sturdy metal plate.",
                ResourceType.CopperWire => "Thin copper wires for electronics.",
                ResourceType.Circuit => "A basic electronic circuit.",
                _ => $"A {FormatResourceName(type.ToString()).ToLower()}."
            };
        }

        private MaterialType GetMaterialType(ResourceType type)
        {
            return type switch
            {
                ResourceType.Dirt or ResourceType.Clay or ResourceType.Coal or
                ResourceType.IronOre or ResourceType.Copper or ResourceType.Silver or ResourceType.Gold => MaterialType.Raw,

                ResourceType.CompressedDirt or ResourceType.HardenedClay or ResourceType.RefinedCoal or
                ResourceType.IronIngot or ResourceType.CopperIngot or ResourceType.SilverIngot or ResourceType.GoldIngot => MaterialType.Refined,

                ResourceType.WoodenPlank or ResourceType.MetalPlate or ResourceType.CopperWire or ResourceType.Circuit => MaterialType.Processed,

                ResourceType.AncientArtifact => MaterialType.Ancient,

                _ => MaterialType.Raw
            };
        }

        /// <summary>
        /// Get the ItemSO mapped to a ResourceType.
        /// </summary>
        public ItemSO GetItemForResource(ResourceType type)
        {
            if (resourceToItemMap.TryGetValue(type, out ItemSO item))
            {
                return item;
            }
            Debug.LogWarning($"[InventorySystem] No ItemSO mapping for ResourceType.{type}");
            return null;
        }

        public bool AddResource(ResourceType type, int amount)
        {
            if (!resources.ContainsKey(type))
            {
                resources[type] = 0;
            }

            // Get the item mapping first
            ItemSO resourceItem = GetItemForResource(type);
            if (resourceItem == null)
            {
                if (enableDebugLogs) Debug.LogWarning($"[InventorySystem] AddResource: No ItemSO for {type}");
                return false;
            }

            // Check if there's ENOUGH space for ALL items (all-or-nothing approach)
            // This accounts for stacking - checks both empty slots and partial stacks
            int availableSpace = CalculateAvailableSpaceForItem(resourceItem);
            if (availableSpace < amount)
            {
                if (enableDebugLogs) Debug.Log($"[InventorySystem] AddResource: Not enough space! Need {amount}, have {availableSpace} (maxStack={currentMaxStackSize}). Cannot add {type}");
                GameEvents.OnInventoryFull?.Invoke();
                return false;
            }

            // We have enough space - add all items
            bool addedAny = TryAddItem(resourceItem, amount, out int remaining);
            int actuallyAdded = amount - remaining;

            if (actuallyAdded > 0)
            {
                // Update resources dictionary with the amount added
                resources[type] += actuallyAdded;
                GameEvents.OnResourceCollected?.Invoke(type, actuallyAdded);
                if (enableDebugLogs) Debug.Log($"[InventorySystem] Added {actuallyAdded}x {type}. Total: {resources[type]}");
            }

            // Should always succeed since we checked space first
            return addedAny;
        }

        /// <summary>
        /// Count the number of empty slots in the inventory.
        /// </summary>
        private int CountEmptySlots()
        {
            if (slotGrid == null) return 0;
            int count = 0;
            for (int i = 0; i < slotGrid.Length; i++)
            {
                if (slotGrid[i].IsEmpty)
                {
                    count++;
                }
            }
            return count;
        }

        /// <summary>
        /// Calculate how much space is available for a specific item.
        /// This accounts for both empty slots and partial stacks of the same item.
        /// </summary>
        private int CalculateAvailableSpaceForItem(ItemSO item)
        {
            if (slotGrid == null || item == null) return 0;

            int availableSpace = 0;

            // Count space in existing stacks of same item
            for (int i = 0; i < slotGrid.Length; i++)
            {
                if (!slotGrid[i].IsEmpty && slotGrid[i].item == item)
                {
                    availableSpace += currentMaxStackSize - slotGrid[i].amount;
                }
            }

            // Count space from empty slots
            int emptySlots = CountEmptySlots();
            availableSpace += emptySlots * currentMaxStackSize;

            return availableSpace;
        }

        public bool RemoveResource(ResourceType type, int amount)
        {
            if (!resources.ContainsKey(type) || resources[type] < amount)
            {
                if (enableDebugLogs) Debug.Log($"Not enough {type}!");
                return false;
            }

            resources[type] -= amount;
            GameEvents.OnResourceUsed?.Invoke(type, amount);

            // NEW: Also remove from inventory grid
            ItemSO resourceItem = GetItemForResource(type);
            if (resourceItem != null)
            {
                bool removedFromGrid = TryRemoveItemFromGrid(resourceItem, amount);
                if (enableDebugLogs) Debug.Log($"[InventorySystem] RemoveResource: {amount}x {type} from slotGrid: {(removedFromGrid ? "SUCCESS" : "FAILED")}");
            }

            if (enableDebugLogs) Debug.Log($"[InventorySystem] Removed {amount} {type}. Remaining: {resources[type]}");
            return true;
        }

        public int GetResourceCount(ResourceType type)
        {
            return resources.ContainsKey(type) ? resources[type] : 0;
        }

        public bool HasResources(Dictionary<ResourceType, int> required)
        {
            foreach (var kvp in required)
            {
                if (GetResourceCount(kvp.Key) < kvp.Value)
                {
                    return false;
                }
            }
            return true;
        }

        public void AddStoryItem(StoryItem item)
        {
            if (!collectedStoryItems.Exists(x => x.itemId == item.itemId))
            {
                item.isCollected = true;
                collectedStoryItems.Add(item);
                GameEvents.OnStoryItemFound?.Invoke(item);

                if (enableDebugLogs) Debug.Log($"Found story item: {item.title}");
            }
        }

        public bool HasStoryItem(string itemId)
        {
            return collectedStoryItems.Exists(x => x.itemId == itemId);
        }

        public void EquipTool(ToolData tool)
        {
            currentTool = tool;
            GameEvents.OnToolEquipped?.Invoke(tool);

            if (enableDebugLogs) Debug.Log($"Equipped: {tool.toolName}");
        }

        public void DamageTool(int damage)
        {
            if (currentTool == null) return;

            currentTool.durability -= damage;
            if (currentTool.durability <= 0)
            {
                if (enableDebugLogs) Debug.Log($"{currentTool.toolName} broke!");
                // Could trigger tool break event
                currentTool = null;
            }
        }

        public void ClearInventory()
        {
            InitializeInventory();
            collectedStoryItems.Clear();
            itemSlots.Clear();
            ownedTools.Clear();
            currentTool = null;
        }

        /// <summary>
        /// Clear only tools (for save/load).
        /// </summary>
        public void ClearTools()
        {
            ownedTools.Clear();
            currentTool = null;
        }

        /// <summary>
        /// Get all slots for save system.
        /// </summary>
        public List<InventorySlotData> GetAllSlots()
        {
            var slots = new List<InventorySlotData>();
            if (slotGrid == null) return slots;

            for (int i = 0; i < slotGrid.Length; i++)
            {
                var stack = slotGrid[i];
                slots.Add(new InventorySlotData
                {
                    SlotIndex = i,
                    ItemData = stack.item,
                    Quantity = stack.amount,
                    IsEmpty = stack.IsEmpty
                });
            }
            return slots;
        }

        /// <summary>
        /// Find an ItemSO by its itemId.
        /// Handles legacy ID formats and case-insensitive matching.
        /// </summary>
        public ItemSO FindItemById(string itemId)
        {
            if (string.IsNullOrEmpty(itemId)) return null;

            // Normalize the itemId for comparison
            string normalizedId = NormalizeItemId(itemId);

            // Check resource-to-item mapping first
            foreach (var kvp in resourceToItemMap)
            {
                if (kvp.Value != null &&
                    string.Equals(kvp.Value.itemId, normalizedId, System.StringComparison.OrdinalIgnoreCase))
                {
                    return kvp.Value;
                }
            }

            // Check existing inventory
            foreach (var stack in slotGrid)
            {
                if (!stack.IsEmpty && stack.item != null &&
                    string.Equals(stack.item.itemId, normalizedId, System.StringComparison.OrdinalIgnoreCase))
                {
                    return stack.item;
                }
            }

            // Try to create a runtime item from the ID if it matches resource pattern
            ItemSO runtimeItem = TryCreateItemFromId(normalizedId);
            if (runtimeItem != null)
            {
                return runtimeItem;
            }

            return null;
        }

        /// <summary>
        /// Normalize item ID to handle legacy formats.
        /// Converts "resource_X" to "underground_x" format.
        /// </summary>
        private string NormalizeItemId(string itemId)
        {
            if (string.IsNullOrEmpty(itemId)) return itemId;

            // Handle legacy "resource_X" format
            if (itemId.StartsWith("resource_", System.StringComparison.OrdinalIgnoreCase))
            {
                string resourcePart = itemId.Substring("resource_".Length);
                return $"underground_{resourcePart.ToLower()}";
            }

            // Normalize underground IDs to lowercase
            if (itemId.StartsWith("underground_", System.StringComparison.OrdinalIgnoreCase))
            {
                return itemId.ToLower();
            }

            return itemId;
        }

        /// <summary>
        /// Try to create a runtime item from an ID that looks like a resource.
        /// </summary>
        private ItemSO TryCreateItemFromId(string itemId)
        {
            if (!itemId.StartsWith("underground_")) return null;

            string resourceName = itemId.Substring("underground_".Length);

            // Try to parse as ResourceType enum
            if (System.Enum.TryParse<ResourceType>(resourceName, true, out var resourceType))
            {
                // Use GetItemForResource which handles caching
                return GetItemForResource(resourceType);
            }

            return null;
        }

        /// <summary>
        /// Add item to a specific slot (for save/load).
        /// </summary>
        public bool AddItemToSlot(ItemSO item, int quantity, int slotIndex)
        {
            if (item == null || quantity <= 0) return false;
            if (slotGrid == null || slotIndex < 0 || slotIndex >= slotGrid.Length) return false;

            slotGrid[slotIndex] = new ItemStack(item, quantity);
            OnInventoryChanged?.Invoke();
            return true;
        }

        #region Item Management

        public bool AddItem(ItemSO item, int amount = 1)
        {
            if (item == null || amount <= 0) return false;

            // Use TryAddItemToGrid which properly updates slotGrid (what UI reads)
            bool success = TryAddItemToGrid(item, amount);

            if (success)
            {
                // Also update itemSlots dictionary for legacy/backwards compatibility
                SyncItemToLegacySlots(item, amount);
                OnItemAdded?.Invoke(item, amount);
            }

            if (enableDebugLogs) Debug.Log($"[InventorySystem] AddItem result: item={item.itemName}, amount={amount}, success={success}");
            return success;
        }

        /// <summary>
        /// Sync an item addition to the legacy itemSlots dictionary for backwards compatibility.
        /// The primary storage is now slotGrid.
        /// </summary>
        private void SyncItemToLegacySlots(ItemSO item, int amount)
        {
            string itemId = item.itemId;

            if (itemSlots.ContainsKey(itemId))
            {
                itemSlots[itemId].amount += amount;
            }
            else
            {
                itemSlots[itemId] = new InventorySlot
                {
                    item = item,
                    amount = amount
                };
            }
        }

        public bool RemoveItem(ItemSO item, int amount = 1)
        {
            if (item == null || amount <= 0) return false;

            string itemId = item.itemId;

            if (!itemSlots.ContainsKey(itemId))
            {
                if (enableDebugLogs) Debug.Log($"[Inventory] Don't have item: {item.itemName}");
                return false;
            }

            var slot = itemSlots[itemId];
            if (slot.amount < amount)
            {
                if (enableDebugLogs) Debug.Log($"[Inventory] Not enough {item.itemName}!");
                return false;
            }

            slot.amount -= amount;
            OnItemRemoved?.Invoke(item, amount);

            if (slot.amount <= 0)
            {
                itemSlots.Remove(itemId);
            }

            if (enableDebugLogs) Debug.Log($"[Inventory] Removed {amount}x {item.itemName}");
            return true;
        }

        public bool RemoveItemById(string itemId, int amount = 1)
        {
            if (!itemSlots.ContainsKey(itemId)) return false;
            return RemoveItem(itemSlots[itemId].item, amount);
        }

        public int GetItemCount(ItemSO item)
        {
            if (item == null) return 0;
            return itemSlots.ContainsKey(item.itemId) ? itemSlots[item.itemId].amount : 0;
        }

        public int GetItemCountById(string itemId)
        {
            return itemSlots.ContainsKey(itemId) ? itemSlots[itemId].amount : 0;
        }

        public bool HasItem(ItemSO item, int amount = 1)
        {
            return GetItemCount(item) >= amount;
        }

        /// <summary>
        /// Legacy method - use GetAllStacks() for V1 API.
        /// </summary>
        public List<InventorySlot> GetAllItems()
        {
            return new List<InventorySlot>(itemSlots.Values);
        }

        #endregion

        #region Inventory Core V1 Public API

        /// <summary>
        /// Tries to add the given amount of an item into the inventory.
        /// STACKING ENABLED: Same items stack up to currentMaxStackSize per slot.
        /// Returns true if at least one item was added.
        /// </summary>
        /// <param name="item">The item to add.</param>
        /// <param name="amount">Amount to add.</param>
        /// <param name="remainingAmount">How many could NOT be added due to capacity limits.</param>
        /// <returns>True if at least one item was added.</returns>
        public bool TryAddItem(ItemSO item, int amount, out int remainingAmount)
        {
            remainingAmount = amount;

            if (enableDebugLogs) Debug.Log($"[InventorySystem] TryAddItem: {amount}x {item?.itemName ?? "NULL"}, maxStackSize={currentMaxStackSize}");

            if (item == null || amount <= 0 || slotGrid == null)
            {
                if (enableDebugLogs) Debug.LogWarning($"[InventorySystem] TryAddItem: Invalid - item={item}, amount={amount}, slotGrid={(slotGrid != null ? "EXISTS" : "NULL")}");
                return false;
            }

            int remaining = amount;

            // STEP 1: Try to stack onto existing slots with the same item
            for (int i = 0; i < slotGrid.Length && remaining > 0; i++)
            {
                if (!slotGrid[i].IsEmpty && slotGrid[i].item == item)
                {
                    int currentAmount = slotGrid[i].amount;
                    int canAdd = currentMaxStackSize - currentAmount;

                    if (canAdd > 0)
                    {
                        int toAdd = Mathf.Min(canAdd, remaining);
                        slotGrid[i] = new ItemStack(item, currentAmount + toAdd);
                        remaining -= toAdd;
                        if (enableDebugLogs) Debug.Log($"[InventorySystem] Stacked {toAdd}x {item.itemName} into slot {i}. Slot now has {slotGrid[i].amount}");
                    }
                }
            }

            // STEP 2: Place remaining items into empty slots
            while (remaining > 0)
            {
                int emptySlot = FindFirstEmptySlot();
                if (emptySlot < 0)
                {
                    // No more space
                    break;
                }

                // Add up to currentMaxStackSize to the empty slot
                int toAdd = Mathf.Min(remaining, currentMaxStackSize);
                slotGrid[emptySlot] = new ItemStack(item, toAdd);
                remaining -= toAdd;
                if (enableDebugLogs) Debug.Log($"[InventorySystem] Added {toAdd}x {item.itemName} to empty slot {emptySlot}");
            }

            remainingAmount = remaining;
            int addedAmount = amount - remaining;

            if (addedAmount > 0)
            {
                // Sync to legacy structures for backwards compatibility
                SyncItemToLegacySlots(item, addedAmount);

                if (enableDebugLogs) Debug.Log($"[InventorySystem] Added {addedAmount}x {item.itemName} to inventory (maxStack={currentMaxStackSize})");

                OnItemAdded?.Invoke(item, addedAmount);
                OnInventoryChanged?.Invoke();

                // Show pickup notification
                if (PickupNotificationSystem.Instance != null)
                {
                    PickupNotificationSystem.Instance.ShowPickup(item.itemName, addedAmount);
                }
            }

            // Fire inventory full events if not all items could be added
            if (remaining > 0)
            {
                if (enableDebugLogs) Debug.LogWarning($"[InventorySystem] Inventory full! Could not add {remaining}x {item.itemName}");
                GameEvents.OnInventoryFull?.Invoke();
                GameEvents.OnInventoryFullWithDetails?.Invoke(item, remaining);
            }

            return addedAmount > 0;
        }

        /// <summary>
        /// Tries to remove the given amount of an item from the inventory.
        /// Returns true if the full amount was removed.
        /// </summary>
        /// <param name="item">The item to remove.</param>
        /// <param name="amount">Amount to remove.</param>
        /// <returns>True if the full amount was removed.</returns>
        public bool TryRemoveItem(ItemSO item, int amount)
        {
            if (item == null || amount <= 0 || slotGrid == null)
            {
                return false;
            }

            // First check if we have enough
            int totalAvailable = GetTotalItemCount(item);
            if (totalAvailable < amount)
            {
                if (enableDebugLogs) Debug.LogWarning($"[InventorySystem] TryRemoveItem: Not enough {item.itemName}. Have {totalAvailable}, need {amount}");
                return false;
            }

            int remaining = amount;

            // Find and remove from slots
            for (int i = 0; i < slotGrid.Length && remaining > 0; i++)
            {
                if (!slotGrid[i].IsEmpty && slotGrid[i].item == item)
                {
                    int toRemove = Mathf.Min(slotGrid[i].amount, remaining);
                    int newAmount = slotGrid[i].amount - toRemove;

                    if (newAmount <= 0)
                    {
                        slotGrid[i] = ItemStack.Empty;
                    }
                    else
                    {
                        slotGrid[i] = new ItemStack(item, newAmount);
                    }

                    remaining -= toRemove;
                }
            }

            if (remaining == 0)
            {
                // CRITICAL: Also update the resources dictionary if this item is a resource
                // This keeps resources dict in sync with slotGrid after selling
                if (item is MaterialItemSO materialItem && materialItem.sourceResource.HasValue)
                {
                    ResourceType resourceType = materialItem.sourceResource.Value;
                    if (resources.ContainsKey(resourceType))
                    {
                        resources[resourceType] = Mathf.Max(0, resources[resourceType] - amount);
                        if (enableDebugLogs) Debug.Log($"[InventorySystem] TryRemoveItem: Also updated resources[{resourceType}] = {resources[resourceType]}");
                    }
                }

                // Also sync legacy itemSlots dictionary
                if (itemSlots.ContainsKey(item.itemId))
                {
                    itemSlots[item.itemId].amount -= amount;
                    if (itemSlots[item.itemId].amount <= 0)
                    {
                        itemSlots.Remove(item.itemId);
                    }
                }

                OnItemRemoved?.Invoke(item, amount);
                OnInventoryChanged?.Invoke();
                if (enableDebugLogs) Debug.Log($"[InventorySystem] TryRemoveItem: Removed {amount}x {item.itemName}");
                return true;
            }

            // Should not reach here if totalAvailable check passed
            Debug.LogError($"[InventorySystem] TryRemoveItem: Unexpected state - could not remove all items");
            return false;
        }

        /// <summary>
        /// Returns a read-only view of the internal slot grid for UI and systems like trading.
        /// Some slots may be empty.
        /// </summary>
        public IReadOnlyList<ItemStack> GetAllStacks()
        {
            if (slotGrid == null)
            {
                return System.Array.Empty<ItemStack>();
            }
            return System.Array.AsReadOnly(slotGrid);
        }

        #endregion

        #region Tool Management

        public void AddTool(ToolData tool)
        {
            if (tool == null) return;

            ownedTools.Add(tool);
            OnToolAdded?.Invoke(tool);

            // Auto-equip if no tool equipped
            if (currentTool == null)
            {
                EquipTool(tool);
            }

            if (enableDebugLogs) Debug.Log($"[Inventory] Added tool: {tool.toolName}");
        }

        public bool HasTool(string toolName)
        {
            return ownedTools.Exists(t => t.toolName == toolName);
        }

        public ToolData GetBestTool()
        {
            ToolData best = null;
            foreach (var tool in ownedTools)
            {
                if (tool.durability > 0)
                {
                    if (best == null || tool.tier > best.tier)
                    {
                        best = tool;
                    }
                }
            }
            return best;
        }

        #endregion

        #region Crafting Integration

        public bool CheckCraftRequirements(CraftingRecipe recipe)
        {
            if (recipe == null) return false;

            // Check resource requirements
            foreach (var req in recipe.requiredResources)
            {
                if (GetResourceCount(req.resourceType) < req.amount)
                {
                    return false;
                }
            }

            // Check item requirements
            foreach (var req in recipe.requiredItems)
            {
                if (GetItemCount(req.item) < req.amount)
                {
                    return false;
                }
            }

            return true;
        }

        public void ConsumeCraftingResources(CraftingRecipe recipe)
        {
            if (recipe == null) return;

            foreach (var req in recipe.requiredResources)
            {
                RemoveResource(req.resourceType, req.amount);
            }

            foreach (var req in recipe.requiredItems)
            {
                RemoveItem(req.item, req.amount);
            }
        }

        #endregion

        /// <summary>
        /// Set the maximum number of inventory slots. Resizes the slot grid and notifies UI.
        /// Preserves existing items when expanding; excess items are lost when shrinking.
        /// </summary>
        public void SetMaxSlots(int newMax)
        {
            newMax = Mathf.Max(1, newMax);

            if (newMax == maxSlots && slotGrid != null && slotGrid.Length == maxSlots)
            {
                if (enableDebugLogs) Debug.Log($"[InventorySystem] SetMaxSlots: Already at {maxSlots} slots, no change needed");
                return;
            }

            int oldMax = maxSlots;

            // Create new grid with new size
            ItemStack[] newGrid = new ItemStack[newMax];

            // Copy existing items (up to the smaller of old/new size)
            int copyCount = slotGrid != null ? Mathf.Min(slotGrid.Length, newMax) : 0;
            for (int i = 0; i < copyCount; i++)
            {
                newGrid[i] = slotGrid[i];
            }

            // Initialize any new slots as empty
            for (int i = copyCount; i < newMax; i++)
            {
                newGrid[i] = ItemStack.Empty;
            }

            slotGrid = newGrid;
            maxSlots = newMax;

            // Fire events so UI can rebuild
            OnInventoryCapacityChanged?.Invoke(oldMax, newMax);
            OnInventoryChanged?.Invoke();
        }

        /// <summary>
        /// Expand inventory slots (e.g., when equipping a better bag).
        /// Preserves existing items.
        /// </summary>
        public void ExpandSlots(int additionalSlots)
        {
            if (additionalSlots <= 0) return;

            int newMax = maxSlots + additionalSlots;
            ItemStack[] newGrid = new ItemStack[newMax];

            // Copy existing items
            for (int i = 0; i < slotGrid.Length; i++)
            {
                newGrid[i] = slotGrid[i];
            }

            // Initialize new slots as empty
            for (int i = slotGrid.Length; i < newMax; i++)
            {
                newGrid[i] = ItemStack.Empty;
            }

            slotGrid = newGrid;
            maxSlots = newMax;

            if (enableDebugLogs) Debug.Log($"[InventorySystem] Expanded inventory to {maxSlots} slots (+{additionalSlots})");
            OnInventoryChanged?.Invoke();
        }

        /// <summary>
        /// Upgrade the inventory. Upgrade path:
        /// - Level 0 -> 1: Increase slots from 5 to 10
        /// - Level 1 -> 2: Stack size becomes 2
        /// - Level 2 -> 3: Stack size becomes 3
        /// - Level 3 -> 4: Stack size becomes 4
        /// - Level 4 -> 5: Stack size becomes 5
        /// - Level 5 -> 6: Stack size becomes 6 (max)
        /// </summary>
        /// <returns>True if upgrade was successful, false if already at max level.</returns>
        public bool TryUpgradeInventory()
        {
            if (inventoryUpgradeLevel >= MAX_STACK_SIZE_UPGRADE_LEVEL)
            {
                if (enableDebugLogs) Debug.Log($"[InventorySystem] Already at max upgrade level ({MAX_STACK_SIZE_UPGRADE_LEVEL})");
                return false;
            }

            inventoryUpgradeLevel++;

            if (inventoryUpgradeLevel == 1)
            {
                // First upgrade: Expand slots from 5 to 10
                SetMaxSlots(UPGRADED_MAX_SLOTS);
                if (enableDebugLogs) Debug.Log($"[InventorySystem] Inventory upgraded to level {inventoryUpgradeLevel}: Slots expanded to {UPGRADED_MAX_SLOTS}");
            }
            else
            {
                // Subsequent upgrades: Increase stack size
                currentMaxStackSize = inventoryUpgradeLevel; // Level 2 = stack 2, Level 3 = stack 3, etc.

                // Consolidate existing items to use new stack size
                ConsolidateStacks();

                if (enableDebugLogs) Debug.Log($"[InventorySystem] Inventory upgraded to level {inventoryUpgradeLevel}: Stack size increased to {currentMaxStackSize}");
            }

            OnInventoryChanged?.Invoke();
            return true;
        }

        /// <summary>
        /// Check if inventory can be upgraded further.
        /// </summary>
        public bool CanUpgradeInventory()
        {
            return inventoryUpgradeLevel < MAX_STACK_SIZE_UPGRADE_LEVEL;
        }

        /// <summary>
        /// Set inventory upgrade level from save data.
        /// This restores the inventory capacity/stack size without consuming resources.
        /// </summary>
        public void SetUpgradeLevel(int level)
        {
            level = Mathf.Clamp(level, 0, MAX_STACK_SIZE_UPGRADE_LEVEL);
            inventoryUpgradeLevel = level;

            // Apply the upgrade effects based on level
            if (level >= 1)
            {
                // Level 1+: Expanded slots (10)
                SetMaxSlots(UPGRADED_MAX_SLOTS);
            }
            else
            {
                // Level 0: Base slots (5)
                SetMaxSlots(INITIAL_MAX_SLOTS);
            }

            // Level 2+: Stack size upgrades
            if (level >= 2)
            {
                currentMaxStackSize = level; // Level 2 = stack 2, Level 3 = stack 3, etc.
            }
            else
            {
                currentMaxStackSize = 1;
            }

            if (enableDebugLogs) Debug.Log($"[InventorySystem] SetUpgradeLevel({level}): maxSlots={maxSlots}, maxStackSize={currentMaxStackSize}");
            OnInventoryChanged?.Invoke();
        }

        /// <summary>
        /// Get a description of what the next upgrade will provide.
        /// </summary>
        public string GetNextUpgradeDescription()
        {
            if (inventoryUpgradeLevel >= MAX_STACK_SIZE_UPGRADE_LEVEL)
            {
                return "Max Level";
            }

            int nextLevel = inventoryUpgradeLevel + 1;
            if (nextLevel == 1)
            {
                return $"{INITIAL_MAX_SLOTS}→{UPGRADED_MAX_SLOTS} slots";
            }
            else
            {
                return $"Stack {currentMaxStackSize}→{nextLevel}";
            }
        }

        /// <summary>
        /// Consolidate stacks after stack size upgrade to merge items where possible.
        /// </summary>
        private void ConsolidateStacks()
        {
            if (slotGrid == null) return;

            // Collect all items by type
            Dictionary<ItemSO, int> itemCounts = new Dictionary<ItemSO, int>();
            foreach (var stack in slotGrid)
            {
                if (!stack.IsEmpty && stack.item != null)
                {
                    if (!itemCounts.ContainsKey(stack.item))
                        itemCounts[stack.item] = 0;
                    itemCounts[stack.item] += stack.amount;
                }
            }

            // Clear all slots
            for (int i = 0; i < slotGrid.Length; i++)
            {
                slotGrid[i] = ItemStack.Empty;
            }

            // Re-add all items with new stack size
            int slotIndex = 0;
            foreach (var kvp in itemCounts)
            {
                ItemSO item = kvp.Key;
                int remaining = kvp.Value;

                while (remaining > 0 && slotIndex < slotGrid.Length)
                {
                    int toAdd = Mathf.Min(remaining, currentMaxStackSize);
                    slotGrid[slotIndex] = new ItemStack(item, toAdd);
                    remaining -= toAdd;
                    slotIndex++;
                }

                if (remaining > 0)
                {
                    Debug.LogWarning($"[InventorySystem] ConsolidateStacks: Lost {remaining}x {item.itemName} - not enough slots!");
                }
            }

            if (enableDebugLogs) Debug.Log($"[InventorySystem] Consolidated stacks with new stack size {currentMaxStackSize}");
        }

        /// <summary>
        /// Remove a specific amount from a slot. Returns the actual amount removed.
        /// </summary>
        public int RemoveFromSlot(int slotIndex, int amount)
        {
            if (slotGrid == null || slotIndex < 0 || slotIndex >= slotGrid.Length)
            {
                return 0;
            }

            ItemStack stack = slotGrid[slotIndex];
            if (stack.IsEmpty || amount <= 0)
            {
                return 0;
            }

            int toRemove = Mathf.Min(amount, stack.amount);
            int remaining = stack.amount - toRemove;

            if (remaining <= 0)
            {
                slotGrid[slotIndex] = ItemStack.Empty;
            }
            else
            {
                slotGrid[slotIndex] = new ItemStack(stack.item, remaining);
            }

            if (enableDebugLogs) Debug.Log($"[InventorySystem] Removed {toRemove}x {stack.item.itemName} from slot {slotIndex}. Remaining: {remaining}");
            OnInventoryChanged?.Invoke();

            return toRemove;
        }

        public string GetInventorySummary()
        {
            System.Text.StringBuilder sb = new System.Text.StringBuilder();
            sb.AppendLine("=== Resources ===");

            foreach (var kvp in resources)
            {
                if (kvp.Value > 0)
                {
                    sb.AppendLine($"{kvp.Key}: {kvp.Value}");
                }
            }

            sb.AppendLine("\n=== Items ===");
            foreach (var slot in itemSlots.Values)
            {
                sb.AppendLine($"{slot.item.itemName}: {slot.amount}");
            }

            sb.AppendLine($"\n=== Tools ({ownedTools.Count}) ===");
            foreach (var tool in ownedTools)
            {
                string equipped = tool == currentTool ? " [EQUIPPED]" : "";
                sb.AppendLine($"{tool.toolName} (T{tool.tier}) - {tool.durability}/{tool.maxDurability}{equipped}");
            }

            return sb.ToString();
        }

        #region Slot-Based Inventory (for UI Grid)

        /// <summary>
        /// Get the item stack at a specific slot index.
        /// </summary>
        public ItemStack GetStackAt(int index)
        {
            if (slotGrid == null || index < 0 || index >= slotGrid.Length)
            {
                return ItemStack.Empty;
            }
            return slotGrid[index];
        }

        /// <summary>
        /// Set the item stack at a specific slot index.
        /// </summary>
        public void SetStackAt(int index, ItemStack stack)
        {
            if (slotGrid == null || index < 0 || index >= slotGrid.Length)
            {
                if (enableDebugLogs) Debug.LogWarning($"[InventorySystem] SetStackAt: Invalid index {index}");
                return;
            }

            slotGrid[index] = stack;
            if (enableDebugLogs) Debug.Log($"[InventorySystem] SetStackAt({index}): {(stack.IsEmpty ? "Empty" : $"{stack.amount}x {stack.item.itemName}")}");
            OnInventoryChanged?.Invoke();
        }

        /// <summary>
        /// Try to add an item to the inventory grid.
        /// STACKING ENABLED: Same items stack up to currentMaxStackSize per slot.
        /// If adding multiple items, they stack in existing slots first, then new slots.
        /// </summary>
        public bool TryAddItemToGrid(ItemSO item, int amount = 1)
        {
            if (item == null || amount <= 0 || slotGrid == null)
            {
                if (enableDebugLogs) Debug.LogWarning("[InventorySystem] TryAddItemToGrid: Invalid item or amount");
                return false;
            }

            if (enableDebugLogs) Debug.Log($"[InventorySystem] TryAddItemToGrid: Adding {amount}x {item.itemName}, sellPrice={item.sellPrice}, maxStack={currentMaxStackSize}");

            int remaining = amount;

            // STEP 1: Try to stack onto existing slots with the same item
            for (int i = 0; i < slotGrid.Length && remaining > 0; i++)
            {
                if (!slotGrid[i].IsEmpty && slotGrid[i].item == item)
                {
                    int currentAmount = slotGrid[i].amount;
                    int canAdd = currentMaxStackSize - currentAmount;

                    if (canAdd > 0)
                    {
                        int toAdd = Mathf.Min(canAdd, remaining);
                        slotGrid[i] = new ItemStack(item, currentAmount + toAdd);
                        remaining -= toAdd;
                        if (enableDebugLogs) Debug.Log($"[InventorySystem] TryAddItemToGrid: Stacked {toAdd}x {item.itemName} into slot {i}. Slot now has {slotGrid[i].amount}");
                    }
                }
            }

            // STEP 2: Place remaining items into empty slots
            while (remaining > 0)
            {
                int emptySlot = FindFirstEmptySlot();
                if (emptySlot < 0)
                {
                    if (enableDebugLogs) Debug.LogWarning($"[InventorySystem] No empty slots for {remaining}x {item.itemName}");
                    break;
                }

                // Add up to currentMaxStackSize to the empty slot
                int toAdd = Mathf.Min(remaining, currentMaxStackSize);
                slotGrid[emptySlot] = new ItemStack(item, toAdd);
                remaining -= toAdd;
                if (enableDebugLogs) Debug.Log($"[InventorySystem] TryAddItemToGrid: Added {toAdd}x {item.itemName} to slot {emptySlot}");
            }

            if (remaining < amount)
            {
                int addedAmount = amount - remaining;
                OnItemAdded?.Invoke(item, addedAmount);
                OnInventoryChanged?.Invoke();

                // Show pickup notification
                if (PickupNotificationSystem.Instance != null)
                {
                    PickupNotificationSystem.Instance.ShowPickup(item.itemName, addedAmount);
                }
            }

            return remaining == 0;
        }

        /// <summary>
        /// Try to remove an item from the inventory grid.
        /// </summary>
        public bool TryRemoveItemFromGrid(ItemSO item, int amount = 1)
        {
            if (item == null || amount <= 0 || slotGrid == null)
            {
                return false;
            }

            int remaining = amount;

            // Find and remove from slots
            for (int i = 0; i < slotGrid.Length && remaining > 0; i++)
            {
                if (!slotGrid[i].IsEmpty && slotGrid[i].item == item)
                {
                    int toRemove = Mathf.Min(slotGrid[i].amount, remaining);
                    int newAmount = slotGrid[i].amount - toRemove;

                    if (newAmount <= 0)
                    {
                        slotGrid[i] = ItemStack.Empty;
                    }
                    else
                    {
                        slotGrid[i] = new ItemStack(item, newAmount);
                    }

                    remaining -= toRemove;
                    if (enableDebugLogs) Debug.Log($"[InventorySystem] Removed {toRemove}x {item.itemName} from slot {i}");
                }
            }

            if (remaining < amount)
            {
                OnItemRemoved?.Invoke(item, amount - remaining);
                OnInventoryChanged?.Invoke();
            }

            return remaining == 0;
        }

        /// <summary>
        /// Move a stack from one slot to another, with swap support.
        /// </summary>
        public bool TryMoveStack(int fromIndex, int toIndex)
        {
            if (slotGrid == null) return false;
            if (fromIndex < 0 || fromIndex >= slotGrid.Length) return false;
            if (toIndex < 0 || toIndex >= slotGrid.Length) return false;
            if (fromIndex == toIndex) return false;

            ItemStack fromStack = slotGrid[fromIndex];
            ItemStack toStack = slotGrid[toIndex];

            if (fromStack.IsEmpty)
            {
                if (enableDebugLogs) Debug.Log("[InventorySystem] TryMoveStack: Source slot is empty");
                return false;
            }

            // If destination is empty, just move
            if (toStack.IsEmpty)
            {
                slotGrid[toIndex] = fromStack;
                slotGrid[fromIndex] = ItemStack.Empty;
                if (enableDebugLogs) Debug.Log($"[InventorySystem] Moved {fromStack.amount}x {fromStack.item.itemName} from slot {fromIndex} to {toIndex}");
            }
            // If same item type and stackable, try to merge
            else if (toStack.item == fromStack.item && fromStack.item.isStackable)
            {
                int canAdd = fromStack.item.maxStackSize - toStack.amount;
                if (canAdd >= fromStack.amount)
                {
                    // Full merge
                    slotGrid[toIndex] = new ItemStack(toStack.item, toStack.amount + fromStack.amount);
                    slotGrid[fromIndex] = ItemStack.Empty;
                    if (enableDebugLogs) Debug.Log($"[InventorySystem] Merged {fromStack.amount}x {fromStack.item.itemName} into slot {toIndex}. Total: {slotGrid[toIndex].amount}");
                }
                else if (canAdd > 0)
                {
                    // Partial merge
                    slotGrid[toIndex] = new ItemStack(toStack.item, toStack.item.maxStackSize);
                    slotGrid[fromIndex] = new ItemStack(fromStack.item, fromStack.amount - canAdd);
                    if (enableDebugLogs) Debug.Log($"[InventorySystem] Partial merge: slot {toIndex} now has {slotGrid[toIndex].amount}, slot {fromIndex} has {slotGrid[fromIndex].amount}");
                }
                else
                {
                    // Destination is full, swap
                    slotGrid[fromIndex] = toStack;
                    slotGrid[toIndex] = fromStack;
                    if (enableDebugLogs) Debug.Log($"[InventorySystem] Swapped slots {fromIndex} and {toIndex}");
                }
            }
            // Different items or non-stackable, swap
            else
            {
                slotGrid[fromIndex] = toStack;
                slotGrid[toIndex] = fromStack;
                if (enableDebugLogs) Debug.Log($"[InventorySystem] Swapped slots {fromIndex} and {toIndex}");
            }

            OnInventoryChanged?.Invoke();
            return true;
        }

        /// <summary>
        /// Split a stack, taking a portion out.
        /// </summary>
        public bool TrySplitStack(int index, int amountToSplit, out ItemStack splitStack)
        {
            splitStack = ItemStack.Empty;

            if (slotGrid == null || index < 0 || index >= slotGrid.Length)
            {
                return false;
            }

            ItemStack source = slotGrid[index];
            if (source.IsEmpty || source.amount <= 1 || amountToSplit <= 0)
            {
                return false;
            }

            int actualSplit = Mathf.Min(amountToSplit, source.amount - 1); // Leave at least 1
            if (actualSplit <= 0)
            {
                return false;
            }

            splitStack = new ItemStack(source.item, actualSplit);
            slotGrid[index] = new ItemStack(source.item, source.amount - actualSplit);

            if (enableDebugLogs) Debug.Log($"[InventorySystem] Split slot {index}: took {actualSplit}x {source.item.itemName}, left {slotGrid[index].amount}");
            OnInventoryChanged?.Invoke();
            return true;
        }

        /// <summary>
        /// Place a stack into a slot, with merge/swap logic.
        /// </summary>
        public ItemStack PlaceStackAt(int index, ItemStack stack)
        {
            if (slotGrid == null || index < 0 || index >= slotGrid.Length)
            {
                return stack;
            }

            ItemStack existing = slotGrid[index];

            // Empty destination - just place
            if (existing.IsEmpty)
            {
                slotGrid[index] = stack;
                if (enableDebugLogs) Debug.Log($"[InventorySystem] Placed {stack.amount}x {stack.item.itemName} at slot {index}");
                OnInventoryChanged?.Invoke();
                return ItemStack.Empty;
            }

            // Same item and stackable - merge
            if (stack.item == existing.item && stack.item.isStackable)
            {
                int canAdd = stack.item.maxStackSize - existing.amount;
                if (canAdd >= stack.amount)
                {
                    slotGrid[index] = new ItemStack(existing.item, existing.amount + stack.amount);
                    if (enableDebugLogs) Debug.Log($"[InventorySystem] Merged into slot {index}. Total: {slotGrid[index].amount}");
                    OnInventoryChanged?.Invoke();
                    return ItemStack.Empty;
                }
                else if (canAdd > 0)
                {
                    slotGrid[index] = new ItemStack(existing.item, existing.item.maxStackSize);
                    ItemStack remainder = new ItemStack(stack.item, stack.amount - canAdd);
                    if (enableDebugLogs) Debug.Log($"[InventorySystem] Partial merge at slot {index}. Remainder: {remainder.amount}");
                    OnInventoryChanged?.Invoke();
                    return remainder;
                }
            }

            // Different items or full - swap
            slotGrid[index] = stack;
            if (enableDebugLogs) Debug.Log($"[InventorySystem] Swapped at slot {index}: placed {stack.amount}x {stack.item.itemName}, returned {existing.amount}x {existing.item.itemName}");
            OnInventoryChanged?.Invoke();
            return existing;
        }

        /// <summary>
        /// Clear a specific slot.
        /// </summary>
        public ItemStack ClearSlot(int index)
        {
            if (slotGrid == null || index < 0 || index >= slotGrid.Length)
            {
                return ItemStack.Empty;
            }

            ItemStack stack = slotGrid[index];
            slotGrid[index] = ItemStack.Empty;

            if (!stack.IsEmpty)
            {
                if (enableDebugLogs) Debug.Log($"[InventorySystem] Cleared slot {index}: {stack.amount}x {stack.item.itemName}");
                OnInventoryChanged?.Invoke();
            }

            return stack;
        }

        /// <summary>
        /// Sort the inventory by the specified mode.
        /// </summary>
        public void SortItems(InventorySortMode mode)
        {
            if (slotGrid == null) return;

            // Collect non-empty stacks
            List<ItemStack> items = slotGrid.Where(s => !s.IsEmpty).ToList();

            // Sort based on mode
            switch (mode)
            {
                case InventorySortMode.ByName:
                    items = items.OrderBy(s => s.item.itemName).ToList();
                    break;
                case InventorySortMode.ByType:
                    items = items.OrderBy(s => s.item.category).ThenBy(s => s.item.itemName).ToList();
                    break;
                case InventorySortMode.ByAmount:
                    items = items.OrderByDescending(s => s.amount).ThenBy(s => s.item.itemName).ToList();
                    break;
            }

            // Clear grid and repopulate
            for (int i = 0; i < slotGrid.Length; i++)
            {
                slotGrid[i] = i < items.Count ? items[i] : ItemStack.Empty;
            }

            if (enableDebugLogs) Debug.Log($"[InventorySystem] Sorted inventory by {mode}. {items.Count} items.");
            OnInventoryChanged?.Invoke();
        }

        /// <summary>
        /// Find the first empty slot index.
        /// </summary>
        public int FindFirstEmptySlot()
        {
            if (slotGrid == null) return -1;

            for (int i = 0; i < slotGrid.Length; i++)
            {
                if (slotGrid[i].IsEmpty)
                {
                    return i;
                }
            }
            return -1;
        }

        /// <summary>
        /// Get total count of a specific item across all slots.
        /// </summary>
        public int GetTotalItemCount(ItemSO item)
        {
            if (item == null || slotGrid == null) return 0;

            int total = 0;
            for (int i = 0; i < slotGrid.Length; i++)
            {
                if (!slotGrid[i].IsEmpty && slotGrid[i].item == item)
                {
                    total += slotGrid[i].amount;
                }
            }
            return total;
        }

        /// <summary>
        /// Destroy an item stack completely.
        /// </summary>
        public void DestroyStack(ItemStack stack)
        {
            if (stack.IsEmpty) return;

            if (enableDebugLogs) Debug.Log($"[InventorySystem] Destroyed {stack.amount}x {stack.item.itemName}");
            OnItemRemoved?.Invoke(stack.item, stack.amount);
            OnInventoryChanged?.Invoke();
        }

        #endregion
    }

    [System.Serializable]
    public class InventorySlot
    {
        public ItemSO item;
        public int amount;
    }

    /// <summary>
    /// Data class for save system slot representation.
    /// </summary>
    public class InventorySlotData
    {
        public int SlotIndex;
        public ItemSO ItemData;
        public int Quantity;
        public bool IsEmpty;
    }
}
