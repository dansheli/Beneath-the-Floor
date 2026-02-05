# DevClaude Coding Conventions

Standards and patterns used in this project. Follow these when adding new code.

---

## Namespace Structure

```
BeneathTheFloor
├── Crafting          // ItemSO, CraftingRecipe, etc.
├── DiggingV2         // Underground terrain, digging mechanics
├── Economy           // CurrencyManager, TradeTerminal, InventorySellService
├── Environment       // ShaftLadderSetup, environmental systems
├── Interaction       // IInteractable, interaction systems
├── Inventory         // InventorySystem, ItemStack
└── (root)            // GameEvents, ResourceType, ToolData
```

---

## Singleton Pattern

Use for managers and services that need global access:

```csharp
public class MyManager : MonoBehaviour
{
    public static MyManager Instance { get; private set; }

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void ResetStatics()
    {
        Instance = null;  // Domain reload safety
    }

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Debug.LogWarning("[MyManager] Duplicate instance, destroying.");
            Destroy(gameObject);
            return;
        }
        Instance = this;

        // Optional: persist across scenes
        if (transform.parent != null)
            transform.SetParent(null);
        DontDestroyOnLoad(gameObject);
    }

    private void OnDestroy()
    {
        if (Instance == this)
            Instance = null;
    }
}
```

### Existing Singletons
- `InventorySystem.Instance`
- `CurrencyManager.Instance`
- `InventorySellService.Instance`
- `TradeTerminalUI.Instance`
- `DepthManager.Instance`
- `UndergroundTerrainManager.Instance`

---

## V1 API Pattern (Inventory)

For inventory operations, use the Try-pattern with out parameters:

```csharp
// Adding items - returns true if ANY were added
bool TryAddItem(ItemSO item, int amount, out int remainingAmount);

// Removing items - returns true if ALL were removed
bool TryRemoveItem(ItemSO item, int amount);

// Reading - returns immutable view
IReadOnlyList<ItemStack> GetAllStacks();
```

### Usage Example
```csharp
if (inventory.TryAddItem(item, 10, out int remaining))
{
    int added = 10 - remaining;
    Debug.Log($"Added {added} items");

    if (remaining > 0)
    {
        // Inventory full, handle overflow
        GameEvents.OnInventoryFullWithDetails?.Invoke(item, remaining);
    }
}
```

---

## Auto-Creation Pattern

Services should create themselves if missing:

```csharp
public void OpenTerminal()
{
    // Ensure dependency exists
    if (TradeTerminalUI.Instance == null)
    {
        EnsureTradeTerminalUI();
    }

    // Now safe to use
    TradeTerminalUI.Instance?.ShowUI(this);
}

private void EnsureTradeTerminalUI()
{
    // 1. Try to find existing
    var existing = FindObjectOfType<TradeTerminalUI>();
    if (existing != null) return;

    // 2. Try to use setup system
    var setup = FindObjectOfType<EconomySetup>();
    if (setup != null)
    {
        setup.SetupEconomy();
        return;
    }

    // 3. Last resort: create minimal version
    CreateMinimalUI();
}
```

---

## Event Naming

In `GameEvents.cs`:

```csharp
// Format: On[Subject][Action]
public static UnityAction OnWallBroken;
public static UnityAction<ResourceType, int> OnResourceCollected;
public static System.Action<int, int> OnItemsSold;

// With details suffix for extended versions
public static System.Action OnInventoryFull;                        // Legacy
public static System.Action<ItemSO, int> OnInventoryFullWithDetails; // V1
```

---

## Debug Logging

Use consistent prefixes for filtering:

```csharp
Debug.Log("[ClassName] Method: message");
Debug.LogWarning("[ClassName] Method: warning message");
Debug.LogError("[ClassName] Method: error message");
```

### Examples
```csharp
Debug.Log("[TradeTerminal] OpenTerminal called");
Debug.LogWarning("[InventorySellService] CurrencyManager.Instance is null");
Debug.LogError("[TradeTerminalUI] Failed to create UI hierarchy");
```

---

## UI Self-Healing Pattern

UI components should rebuild themselves if hierarchy is missing:

```csharp
private void OnEnable()
{
    // Try to find existing hierarchy
    itemListContent = FindItemListContent();

    if (itemListContent == null)
    {
        Debug.LogWarning("[MyUI] Hierarchy missing, creating default...");
        CreateDefaultUIHierarchy();
    }
}

private void CreateDefaultUIHierarchy()
{
    // Create required RectTransforms
    // Set anchors, pivots, sizes
    // Add required components (LayoutGroups, etc.)
}
```

---

## File Locations

| Type | Location |
|------|----------|
| Scripts | `Assets/Scripts/{Namespace}/` |
| ScriptableObjects | `Assets/ScriptableObjects/{Type}/` |
| Prefabs | `Assets/Prefabs/{Category}/` |
| Materials | `Assets/Materials/` |
| DevClaude docs | `Assets/BeneathTheFloor/DevClaude/` |

---

## IInteractable Interface

For objects the player can interact with:

```csharp
public interface IInteractable
{
    bool CanInteract { get; }
    string GetInteractionText();
    void Interact(GameObject interactor);
    void OnHoverEnter();
    void OnHoverExit();
}
```

---

## ItemSO Properties

Standard properties on item ScriptableObjects:

```csharp
public class ItemSO : ScriptableObject
{
    public string itemName;
    public Sprite icon;
    public int maxStackSize;
    public int sellPrice;        // 0 = not sellable
    public ItemCategory category;
    // ... other properties
}
```

---

## Safety Checklist

Before modifying existing code:

1. [ ] Check CHANGELOG for previous modifications
2. [ ] Understand existing patterns (singleton, events, etc.)
3. [ ] Don't break V1 API contracts
4. [ ] Update CHANGELOG after changes
5. [ ] Test that existing systems still work

---
*Last updated: 2024-12-13*
