using UnityEngine;
using System;
using System.Collections.Generic;
using System.Text;
using BeneathTheFloor.Crafting;
using BeneathTheFloor.Inventory;

namespace BeneathTheFloor.Digging
{
    /// <summary>
    /// Bridges the digging system with the inventory system.
    /// Converts DigResult resources into inventory items via explicit mappings.
    /// </summary>
    public class DigInventoryBridge : MonoBehaviour
    {
        public static DigInventoryBridge Instance { get; private set; }

        [Header("Resource Mappings")]
        [Tooltip("Map underground resource types to inventory items. Each UndergroundResourceType should have exactly one mapping.")]
        [SerializeField]
        private List<ResourceItemMapping> resourceMappings = new List<ResourceItemMapping>();

        [Header("Runtime Fallback")]
        [Tooltip("If enabled, automatically creates temporary ItemSO objects for unmapped resource types. Useful for testing.")]
        [SerializeField]
        private bool enableRuntimeFallback = true;

        [Header("Debug")]
        [SerializeField]
        private bool enableDebugLogs = false;

        /// <summary>
        /// Enable or disable debug logging at runtime.
        /// </summary>
        public bool EnableDebugLogs
        {
            get => enableDebugLogs;
            set => enableDebugLogs = value;
        }

        /// <summary>
        /// Enable or disable runtime fallback item creation.
        /// </summary>
        public bool EnableRuntimeFallback
        {
            get => enableRuntimeFallback;
            set => enableRuntimeFallback = value;
        }

        // Cached lookup dictionary built at runtime for O(1) access
        private Dictionary<UndergroundResourceType, ResourceItemMapping> _mappingByType;

        // Cache for runtime-generated fallback items
        private Dictionary<UndergroundResourceType, MaterialItemSO> _runtimeFallbackItems;

        // Track if we've been initialized
        private bool _isInitialized;

        #region Unity Lifecycle

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetStaticInstance()
        {
            Instance = null;
        }

        private void Awake()
        {
            if (Instance == null)
            {
                Instance = this;
                InitializeMappings();
            }
            else
            {
                // Duplicate instance - silently destroy
                Destroy(this);
            }
        }

        private void OnDestroy()
        {
            if (Instance == this)
            {
                Instance = null;
            }
        }

#if UNITY_EDITOR
        private void OnValidate()
        {
            // Rebuild and validate in editor when values change
            ValidateMappingsInternal(logToConsole: false);
        }
#endif

        #endregion

        #region Initialization

        /// <summary>
        /// Build the lookup dictionary from the mapping list.
        /// Logs warnings for duplicates and null items but does NOT crash.
        /// </summary>
        private void InitializeMappings()
        {
            _mappingByType = new Dictionary<UndergroundResourceType, ResourceItemMapping>();
            _runtimeFallbackItems = new Dictionary<UndergroundResourceType, MaterialItemSO>();
            _isInitialized = true;

            int validCount = 0;
            int duplicateCount = 0;
            int nullItemCount = 0;

            foreach (var mapping in resourceMappings)
            {
                // Skip None type
                if (mapping.undergroundType == UndergroundResourceType.None)
                    continue;

                // Check for duplicates
                if (_mappingByType.ContainsKey(mapping.undergroundType))
                {
                    Debug.LogWarning($"[DigInventoryBridge] Duplicate mapping for {mapping.undergroundType}. Using first occurrence.");
                    duplicateCount++;
                    continue;
                }

                // Warn about null items but still add the mapping
                if (mapping.item == null)
                {
                    Debug.LogWarning($"[DigInventoryBridge] Mapping for {mapping.undergroundType} has NULL ItemSO.");
                    nullItemCount++;
                }

                _mappingByType[mapping.undergroundType] = mapping;
                if (mapping.item != null)
                    validCount++;
            }

            if (enableDebugLogs)
            {
                Debug.Log($"[DigInventoryBridge] Initialized: {validCount} valid mappings, {nullItemCount} null items, {duplicateCount} duplicates skipped.");
            }
        }

        /// <summary>
        /// Ensures the lookup dictionary is available (lazy init for editor use).
        /// </summary>
        private void EnsureInitialized()
        {
            if (!_isInitialized || _mappingByType == null)
            {
                _mappingByType = new Dictionary<UndergroundResourceType, ResourceItemMapping>();
                foreach (var mapping in resourceMappings)
                {
                    if (mapping.undergroundType != UndergroundResourceType.None && !_mappingByType.ContainsKey(mapping.undergroundType))
                    {
                        _mappingByType[mapping.undergroundType] = mapping;
                    }
                }
                _isInitialized = true;
            }
        }

        /// <summary>
        /// Get or create a runtime fallback MaterialItemSO for an unmapped resource type.
        /// </summary>
        private MaterialItemSO GetOrCreateFallbackItem(UndergroundResourceType type)
        {
            // Check cache first
            if (_runtimeFallbackItems == null)
                _runtimeFallbackItems = new Dictionary<UndergroundResourceType, MaterialItemSO>();

            if (_runtimeFallbackItems.TryGetValue(type, out var cached))
            {
                return cached;
            }

            // Create a new runtime item
            var item = ScriptableObject.CreateInstance<MaterialItemSO>();

            // Format name: IronNugget -> "Iron Nugget"
            string formattedName = FormatResourceName(type.ToString());

            item.itemId = $"underground_{type.ToString().ToLower()}";
            item.itemName = formattedName;
            item.description = $"A {formattedName.ToLower()} dug from underground.";

            // Try to load icon from Resources
            item.icon = TryLoadIconForResource(type);
            item.category = ItemCategory.Material;
            item.isStackable = true;
            item.maxStackSize = 99;
            item.sellPrice = GetFallbackSellPrice(type);
            item.materialType = GetFallbackMaterialType(type);

            // Cache it
            _runtimeFallbackItems[type] = item;

            if (enableDebugLogs)
                Debug.Log($"[DigInventoryBridge] Created runtime fallback item: {formattedName} (sell: {item.sellPrice})");

            return item;
        }

        /// <summary>
        /// Format resource name: "IronNugget" -> "Iron Nugget"
        /// </summary>
        private string FormatResourceName(string name)
        {
            var sb = new StringBuilder();
            for (int i = 0; i < name.Length; i++)
            {
                if (i > 0 && char.IsUpper(name[i]))
                {
                    sb.Append(' ');
                }
                sb.Append(name[i]);
            }
            return sb.ToString();
        }

        /// <summary>
        /// Get a reasonable sell price based on the resource type and implied depth/rarity.
        /// </summary>
        private int GetFallbackSellPrice(UndergroundResourceType type)
        {
            return type switch
            {
                // Layer 1 - Common (5-20m)
                UndergroundResourceType.Dirt => 1,
                UndergroundResourceType.SoftStone => 2,
                UndergroundResourceType.Clay => 3,
                UndergroundResourceType.IronNugget => 5,
                UndergroundResourceType.CopperFragment => 4,

                // Layer 2 - Uncommon (20-40m)
                UndergroundResourceType.HardSoil => 3,
                UndergroundResourceType.Stone => 4,
                UndergroundResourceType.Sandstone => 5,
                UndergroundResourceType.IronChunk => 8,
                UndergroundResourceType.CopperPiece => 7,
                UndergroundResourceType.Coal => 6,

                // Layer 3 - Rare (40-70m)
                UndergroundResourceType.HardSoilDeep => 5,
                UndergroundResourceType.HardStone => 6,
                UndergroundResourceType.Heatstone => 12,
                UndergroundResourceType.Quartz => 15,
                UndergroundResourceType.CrystalDust => 10,
                UndergroundResourceType.AncientOre => 20,
                UndergroundResourceType.Gold => 75,

                // Layer 4 - Epic (70-110m)
                UndergroundResourceType.CrystalShard => 25,
                UndergroundResourceType.PurpleQuartz => 30,
                UndergroundResourceType.CrystalStone => 20,
                UndergroundResourceType.DeepCrystalVein => 35,
                UndergroundResourceType.LuminousDust => 28,

                // Layer 5 - Legendary (110-160m)
                UndergroundResourceType.CrystalCoreFragment => 50,
                UndergroundResourceType.BlueGreyOre => 45,

                // Layer 6 - Mythic (160-220m)
                UndergroundResourceType.DeepBlackStone => 40,
                UndergroundResourceType.CoreCrystalChunk => 75,
                UndergroundResourceType.RareMachineParts => 100,

                _ => 5 // Default
            };
        }

        /// <summary>
        /// Get the material type based on the resource.
        /// </summary>
        private MaterialType GetFallbackMaterialType(UndergroundResourceType type)
        {
            return type switch
            {
                UndergroundResourceType.AncientOre or
                UndergroundResourceType.RareMachineParts => MaterialType.Ancient,

                UndergroundResourceType.CrystalShard or
                UndergroundResourceType.CrystalDust or
                UndergroundResourceType.CrystalStone or
                UndergroundResourceType.CrystalCoreFragment or
                UndergroundResourceType.CoreCrystalChunk or
                UndergroundResourceType.PurpleQuartz or
                UndergroundResourceType.DeepCrystalVein or
                UndergroundResourceType.LuminousDust => MaterialType.Refined,

                _ => MaterialType.Raw
            };
        }

        /// <summary>
        /// Try to load an icon sprite for a resource type from Resources folder.
        /// </summary>
        private Sprite TryLoadIconForResource(UndergroundResourceType type)
        {
            // Map resource type to simpler icon names
            string iconName = type switch
            {
                UndergroundResourceType.Stone or UndergroundResourceType.SoftStone or
                UndergroundResourceType.HardStone or UndergroundResourceType.Sandstone => "stone",

                UndergroundResourceType.IronNugget or UndergroundResourceType.IronChunk => "iron",

                UndergroundResourceType.CopperFragment or UndergroundResourceType.CopperPiece => "copper",

                UndergroundResourceType.Coal => "coal",

                UndergroundResourceType.Dirt or UndergroundResourceType.HardSoil or
                UndergroundResourceType.HardSoilDeep => "dirt",

                UndergroundResourceType.Clay => "clay",

                UndergroundResourceType.Quartz or UndergroundResourceType.PurpleQuartz => "quartz",

                UndergroundResourceType.CrystalShard or UndergroundResourceType.CrystalDust or
                UndergroundResourceType.CrystalStone or UndergroundResourceType.CrystalCoreFragment or
                UndergroundResourceType.CoreCrystalChunk or UndergroundResourceType.DeepCrystalVein or
                UndergroundResourceType.LuminousDust => "crystal",

                UndergroundResourceType.Gold => "gold",

                _ => type.ToString().ToLower()
            };

            // Try multiple paths
            string[] paths = {
                $"Icons/icon_{iconName}",
                $"UI/Icons/icon_{iconName}",
                $"icon_{iconName}"
            };

            foreach (var path in paths)
            {
                var sprite = Resources.Load<Sprite>(path);
                if (sprite != null)
                {
                    if (enableDebugLogs)
                        Debug.Log($"[DigInventoryBridge] Loaded icon for {type} from {path}");
                    return sprite;
                }
            }

            if (enableDebugLogs)
                Debug.LogWarning($"[DigInventoryBridge] No icon found for {type} (tried icon_{iconName})");

            return null;
        }

        #endregion

        #region Public Mapping API

        /// <summary>
        /// Try to get the mapping for a given underground resource type.
        /// </summary>
        /// <param name="type">The underground resource type to look up.</param>
        /// <param name="mapping">The mapping if found.</param>
        /// <returns>True if a mapping exists, false otherwise.</returns>
        public bool TryGetMapping(UndergroundResourceType type, out ResourceItemMapping mapping)
        {
            EnsureInitialized();

            // Fast path: use dictionary
            if (_mappingByType != null && _mappingByType.TryGetValue(type, out mapping))
            {
                return true;
            }

            // Fallback: linear search (shouldn't normally happen)
            foreach (var m in resourceMappings)
            {
                if (m.undergroundType == type)
                {
                    mapping = m;
                    return true;
                }
            }

            mapping = default;
            return false;
        }

        /// <summary>
        /// Try to get the ItemSO and default amount for an underground resource type.
        /// If no valid mapping exists and runtime fallback is enabled, creates a temporary item.
        /// </summary>
        /// <param name="type">The underground resource type.</param>
        /// <param name="item">The mapped ItemSO, or null if not found.</param>
        /// <param name="defaultAmountPerDig">The default amount multiplier from the mapping.</param>
        /// <returns>True if a valid mapping with non-null item exists (or fallback was created).</returns>
        public bool TryGetItemForResource(UndergroundResourceType type, out ItemSO item, out int defaultAmountPerDig)
        {
            item = null;
            defaultAmountPerDig = 1;

            if (type == UndergroundResourceType.None)
            {
                return false;
            }

            // Try to get from explicit mapping first
            if (TryGetMapping(type, out var mapping) && mapping.item != null)
            {
                item = mapping.item;
                defaultAmountPerDig = Mathf.Max(1, mapping.defaultAmountPerDig);
                return true;
            }

            // No valid mapping - try runtime fallback if enabled
            if (enableRuntimeFallback)
            {
                item = GetOrCreateFallbackItem(type);
                if (item != null)
                {
                    defaultAmountPerDig = 1;
                    return true;
                }
            }

            // No mapping and no fallback
            Debug.LogWarning($"[DigInventoryBridge] No valid mapping for UndergroundResourceType.{type} (fallback: {(enableRuntimeFallback ? "enabled but failed" : "disabled")})");
            return false;
        }

        /// <summary>
        /// Check if a mapping exists for a given type (does not check if ItemSO is null).
        /// </summary>
        public bool HasMapping(UndergroundResourceType type)
        {
            EnsureInitialized();
            return _mappingByType != null && _mappingByType.ContainsKey(type);
        }

        /// <summary>
        /// Get all currently configured mappings (read-only).
        /// </summary>
        public IReadOnlyList<ResourceItemMapping> GetAllMappings()
        {
            return resourceMappings.AsReadOnly();
        }

        #endregion

        #region Award Resources

        /// <summary>
        /// Check if a resource CAN be awarded (inventory has enough space).
        /// Does NOT actually add the item - just checks if there's room.
        /// </summary>
        /// <param name="type">The underground resource type to check.</param>
        /// <param name="amount">The amount to check for.</param>
        /// <returns>True if inventory has enough space, false otherwise.</returns>
        public bool CanAwardResource(UndergroundResourceType type, int amount)
        {
            if (type == UndergroundResourceType.None || amount <= 0)
                return false;

            // Try to get the item mapping
            if (!TryGetItemForResource(type, out var item, out int defaultAmountPerDig))
                return false;

            // Calculate final amount
            int finalAmount = Mathf.Max(1, amount * defaultAmountPerDig);

            // Get inventory system
            var inventory = GetInventorySystem();
            if (inventory == null)
                return false;

            // Count empty slots
            int emptySlots = 0;
            var stacks = inventory.GetAllStacks();
            foreach (var stack in stacks)
            {
                if (stack.IsEmpty)
                    emptySlots++;
            }

            return emptySlots >= finalAmount;
        }

        /// <summary>
        /// Award a specific underground resource type and amount to the inventory.
        /// This is the NEW primary method used by ResourcePickup for manual pickup.
        /// Uses the existing mapping (UndergroundResourceType -> ItemSO).
        /// Uses all-or-nothing approach - only awards if ALL items can fit.
        /// </summary>
        /// <param name="type">The underground resource type to award.</param>
        /// <param name="amount">The amount to award.</param>
        /// <returns>True if ALL items were successfully added, false if inventory doesn't have enough space.</returns>
        public bool AwardResource(UndergroundResourceType type, int amount)
        {
            if (type == UndergroundResourceType.None)
            {
                if (enableDebugLogs)
                    Debug.Log("[DigInventoryBridge] AwardResource: type is None, skipping.");
                return false;
            }

            if (amount <= 0)
            {
                if (enableDebugLogs)
                    Debug.Log($"[DigInventoryBridge] AwardResource: amount <= 0 for {type}, skipping.");
                return false;
            }

            // Try to get the item mapping
            if (!TryGetItemForResource(type, out var item, out int defaultAmountPerDig))
            {
                // Warning already logged by TryGetItemForResource
                return false;
            }

            // Calculate final amount: amount * mapping multiplier
            int finalAmount = Mathf.Max(1, amount * defaultAmountPerDig);

            // Get inventory system
            var inventory = GetInventorySystem();
            if (inventory == null)
            {
                Debug.LogWarning("[DigInventoryBridge] No InventorySystem instance found, cannot award resource.");
                return false;
            }

            // Check if there's enough space for ALL items BEFORE adding (all-or-nothing)
            // This prevents pickups from being destroyed while only some items go to inventory
            int emptySlots = 0;
            var stacks = inventory.GetAllStacks();
            foreach (var stack in stacks)
            {
                if (stack.IsEmpty)
                    emptySlots++;
            }

            if (emptySlots < finalAmount)
            {
                if (enableDebugLogs)
                    Debug.Log($"[DigInventoryBridge] Inventory full: need {finalAmount} slots for {item.itemName}, only {emptySlots} available.");
                GameEvents.OnInventoryFull?.Invoke();
                return false;
            }

            // We have enough space - add all items
            bool addedAny = inventory.TryAddItem(item, finalAmount, out int remaining);

            if (addedAny && remaining == 0)
            {
                if (enableDebugLogs)
                    Debug.Log($"[DigInventoryBridge] Awarded {finalAmount}x {item.itemName} (from {type} pickup)");

                // Forward to GameEvents.OnResourceCollected if this is a MaterialItemSO with a sourceResource
                ForwardResourceCollectedEvent(item, finalAmount);

                // Fire pickup event
                GameEvents.OnResourcePickedUp?.Invoke(type, finalAmount);

                return true;
            }
            else
            {
                // Something went wrong - shouldn't happen since we checked space
                Debug.LogWarning($"[DigInventoryBridge] Unexpected: checked space but failed to add {item.itemName} x{finalAmount}. Remaining: {remaining}");
                return false;
            }
        }

        #endregion

        #region Award Dig Result (DEPRECATED)

        /// <summary>
        /// [DEPRECATED] Award the resources from a dig result to the player's inventory.
        /// This method is no longer called automatically. Resources are now picked up manually
        /// via ResourcePickup components on world drops.
        /// Use AwardResource(type, amount) instead.
        /// </summary>
        /// <param name="result">The dig result.</param>
        [System.Obsolete("Use AwardResource(type, amount) instead. V3 system uses ResourceSpawner for resource drops.")]
        public void AwardDigResult(DigResult result)
        {
            // This method is obsolete. In the V3 system, resources are spawned by ResourceSpawner
            // and collected by the player as pickups, not awarded directly from dig results.
            // The DigResult struct no longer contains ResourceType/ResourceAmount.
            if (enableDebugLogs)
                Debug.LogWarning("[DigInventoryBridge] AwardDigResult is obsolete. Use AwardResource(type, amount) or let ResourceSpawner handle drops.");
        }

        /// <summary>
        /// Get the InventorySystem instance, with fallback to FindObjectOfType.
        /// </summary>
        private InventorySystem GetInventorySystem()
        {
            if (InventorySystem.Instance != null)
            {
                return InventorySystem.Instance;
            }

            // Fallback: try to find it (slower, but more robust)
            var inventory = FindObjectOfType<InventorySystem>();
            if (inventory != null)
            {
                return inventory;
            }

            return null;
        }

        /// <summary>
        /// If the item is a MaterialItemSO with a valid sourceResource,
        /// forward to GameEvents.OnResourceCollected for legacy systems.
        /// </summary>
        private void ForwardResourceCollectedEvent(ItemSO item, int amount)
        {
            // Only forward if item is MaterialItemSO and has a sourceResource
            if (item is MaterialItemSO materialItem && materialItem.sourceResource.HasValue)
            {
                GameEvents.OnResourceCollected?.Invoke(materialItem.sourceResource.Value, amount);

                if (enableDebugLogs)
                    Debug.Log($"[DigInventoryBridge] Forwarded OnResourceCollected: {materialItem.sourceResource.Value} x{amount}");
            }
        }

        #endregion

        #region Runtime Mapping Management

        /// <summary>
        /// Add or update a resource mapping at runtime.
        /// </summary>
        public void SetMapping(UndergroundResourceType type, ItemSO item, int defaultAmount = 1)
        {
            if (type == UndergroundResourceType.None)
            {
                Debug.LogWarning("[DigInventoryBridge] Cannot set mapping for UndergroundResourceType.None");
                return;
            }

            EnsureInitialized();

            var mapping = new ResourceItemMapping
            {
                undergroundType = type,
                item = item,
                defaultAmountPerDig = Mathf.Max(1, defaultAmount)
            };

            // Update or add to list
            int existingIndex = resourceMappings.FindIndex(m => m.undergroundType == type);
            if (existingIndex >= 0)
            {
                resourceMappings[existingIndex] = mapping;
            }
            else
            {
                resourceMappings.Add(mapping);
            }

            // Update lookup
            _mappingByType[type] = mapping;

            if (enableDebugLogs)
                Debug.Log($"[DigInventoryBridge] Set mapping: {type} -> {item?.itemName ?? "null"} (x{mapping.defaultAmountPerDig})");
        }

        /// <summary>
        /// Remove a mapping for a specific type.
        /// </summary>
        public bool RemoveMapping(UndergroundResourceType type)
        {
            EnsureInitialized();

            int index = resourceMappings.FindIndex(m => m.undergroundType == type);
            if (index >= 0)
            {
                resourceMappings.RemoveAt(index);
                _mappingByType?.Remove(type);
                return true;
            }
            return false;
        }

        #endregion

        #region Validation

        /// <summary>
        /// Validate all mappings and return a detailed report.
        /// Can be called from editor scripts or at runtime.
        /// </summary>
        /// <returns>A MappingValidationReport with all findings.</returns>
        public MappingValidationReport ValidateMappings()
        {
            return ValidateMappingsInternal(logToConsole: true);
        }

        private MappingValidationReport ValidateMappingsInternal(bool logToConsole)
        {
            var report = new MappingValidationReport();

            // Get all UndergroundResourceType values (except None)
            var allTypes = (UndergroundResourceType[])Enum.GetValues(typeof(UndergroundResourceType));
            var mappedTypes = new HashSet<UndergroundResourceType>();

            // Analyze current mappings
            foreach (var mapping in resourceMappings)
            {
                if (mapping.undergroundType == UndergroundResourceType.None)
                    continue;

                // Check for duplicates
                if (mappedTypes.Contains(mapping.undergroundType))
                {
                    report.duplicateTypes.Add(mapping.undergroundType);
                }
                else
                {
                    mappedTypes.Add(mapping.undergroundType);
                }

                // Check for null items
                if (mapping.item == null)
                {
                    report.nullItemTypes.Add(mapping.undergroundType);
                }
                else
                {
                    report.validMappingCount++;
                }
            }

            // Find missing types
            foreach (var type in allTypes)
            {
                if (type == UndergroundResourceType.None)
                    continue;

                report.totalTypeCount++;

                if (!mappedTypes.Contains(type))
                {
                    report.missingTypes.Add(type);
                }
            }

            report.mappedTypeCount = mappedTypes.Count;

            // Log if requested
            if (logToConsole)
            {
                LogValidationReport(report);
            }

            return report;
        }

        private void LogValidationReport(MappingValidationReport report)
        {
            var sb = new StringBuilder();
            sb.AppendLine("╔════════════════════════════════════════════════════════════════╗");
            sb.AppendLine("║          DIG INVENTORY BRIDGE - MAPPING VALIDATION             ║");
            sb.AppendLine("╠════════════════════════════════════════════════════════════════╣");
            sb.AppendLine($"║  Total UndergroundResourceTypes: {report.totalTypeCount,-28}║");
            sb.AppendLine($"║  Mapped Types:                   {report.mappedTypeCount,-28}║");
            sb.AppendLine($"║  Valid Mappings (with ItemSO):   {report.validMappingCount,-28}║");
            sb.AppendLine($"║  Missing Mappings:               {report.missingTypes.Count,-28}║");
            sb.AppendLine($"║  Null ItemSO Mappings:           {report.nullItemTypes.Count,-28}║");
            sb.AppendLine($"║  Duplicate Mappings:             {report.duplicateTypes.Count,-28}║");
            sb.AppendLine("╚════════════════════════════════════════════════════════════════╝");

            Debug.Log(sb.ToString());

            // Log details for issues
            if (report.missingTypes.Count > 0)
            {
                sb.Clear();
                sb.AppendLine("[DigInventoryBridge] MISSING MAPPINGS:");
                foreach (var type in report.missingTypes)
                {
                    sb.AppendLine($"  - {type}");
                }
                Debug.LogWarning(sb.ToString());
            }

            if (report.nullItemTypes.Count > 0)
            {
                sb.Clear();
                sb.AppendLine("[DigInventoryBridge] MAPPINGS WITH NULL ItemSO:");
                foreach (var type in report.nullItemTypes)
                {
                    sb.AppendLine($"  - {type}");
                }
                Debug.LogWarning(sb.ToString());
            }

            if (report.duplicateTypes.Count > 0)
            {
                sb.Clear();
                sb.AppendLine("[DigInventoryBridge] DUPLICATE MAPPINGS:");
                foreach (var type in report.duplicateTypes)
                {
                    sb.AppendLine($"  - {type}");
                }
                Debug.LogWarning(sb.ToString());
            }

            if (report.IsFullyConfigured)
            {
                Debug.Log("[DigInventoryBridge] All mappings are valid and complete!");
            }
        }

        #endregion

        #region Context Menu Utilities

        [ContextMenu("Initialize All Mappings (Empty)")]
        private void InitializeAllMappingsEmpty()
        {
            resourceMappings.Clear();

            var allTypes = (UndergroundResourceType[])Enum.GetValues(typeof(UndergroundResourceType));
            foreach (var type in allTypes)
            {
                if (type == UndergroundResourceType.None)
                    continue;

                resourceMappings.Add(new ResourceItemMapping
                {
                    undergroundType = type,
                    item = null,
                    defaultAmountPerDig = 1
                });
            }

            Debug.Log($"[DigInventoryBridge] Created {resourceMappings.Count} empty mappings for all UndergroundResourceTypes.");

#if UNITY_EDITOR
            UnityEditor.EditorUtility.SetDirty(this);
#endif
        }

        [ContextMenu("Validate Mappings")]
        private void ValidateMappingsContextMenu()
        {
            ValidateMappings();
        }

        [ContextMenu("Log Current Mappings")]
        private void LogCurrentMappings()
        {
            var sb = new StringBuilder();
            sb.AppendLine($"[DigInventoryBridge] Current mappings ({resourceMappings.Count} total):");

            foreach (var mapping in resourceMappings)
            {
                string itemName = mapping.item != null ? mapping.item.itemName : "(null)";
                sb.AppendLine($"  {mapping.undergroundType} -> {itemName} (x{mapping.defaultAmountPerDig})");
            }

            Debug.Log(sb.ToString());
        }

        #endregion
    }

    #region Data Structures

    /// <summary>
    /// Maps an underground resource type to an inventory item.
    /// </summary>
    [System.Serializable]
    public struct ResourceItemMapping
    {
        [Tooltip("The underground resource type from digging.")]
        public UndergroundResourceType undergroundType;

        [Tooltip("The inventory item to award when this resource is dug.")]
        public ItemSO item;

        [Tooltip("Amount multiplier per dig. Final amount = digResult.amount * this value.")]
        [Min(1)]
        public int defaultAmountPerDig;

        public ResourceItemMapping(UndergroundResourceType type, ItemSO item, int amount = 1)
        {
            this.undergroundType = type;
            this.item = item;
            this.defaultAmountPerDig = Mathf.Max(1, amount);
        }
    }

    /// <summary>
    /// Results from validating the DigInventoryBridge mappings.
    /// </summary>
    public class MappingValidationReport
    {
        public int totalTypeCount;
        public int mappedTypeCount;
        public int validMappingCount;
        public List<UndergroundResourceType> missingTypes = new List<UndergroundResourceType>();
        public List<UndergroundResourceType> nullItemTypes = new List<UndergroundResourceType>();
        public List<UndergroundResourceType> duplicateTypes = new List<UndergroundResourceType>();

        public bool IsFullyConfigured =>
            missingTypes.Count == 0 &&
            nullItemTypes.Count == 0 &&
            duplicateTypes.Count == 0;

        public bool HasIssues =>
            missingTypes.Count > 0 ||
            nullItemTypes.Count > 0 ||
            duplicateTypes.Count > 0;
    }

    #endregion
}
