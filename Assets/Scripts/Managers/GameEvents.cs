using UnityEngine;
using UnityEngine.Events;

namespace BeneathTheFloor
{
    public static class GameEvents
    {
        // Wall Events
        public static UnityAction OnWallBroken;

        // Resource Events
        public static UnityAction<ResourceType, int> OnResourceCollected;
        public static UnityAction<ResourceType, int> OnResourceUsed;

        // Tool Events
        public static UnityAction<ToolData> OnToolEquipped;
        public static UnityAction<ToolData> OnToolUpgraded;

        // Story Events
        public static UnityAction<StoryItem> OnStoryItemFound;
        public static UnityAction<int> OnDepthReached;

        // UI Events
        public static UnityAction OnInventoryToggled;
        public static UnityAction OnPauseToggled;

        // Inventory Events
        public static System.Action OnInventoryFull; // LEGACY: Parameterless version
        public static System.Action<Crafting.ItemSO, int> OnInventoryFullWithDetails; // V1: Includes item and remaining amount

        // Sell Events
        public static System.Action<int, int> OnItemsSold; // (totalCredits, totalItemCount)
        public static System.Action<float, int> OnDustSold; // (dustAmount, creditsGained)

        // Game State Events
        public static UnityAction OnGameSaved;
        public static UnityAction OnGameLoaded;
        public static UnityAction OnNewGameStarted;

        // Crafting Events
        public static UnityAction<Crafting.CraftingRecipe> OnCraftingStarted;
        public static UnityAction<Crafting.CraftingRecipe, Crafting.ItemSO, int> OnItemCrafted;
        public static UnityAction<Crafting.CraftingRecipe> OnRecipeUnlocked;

        // Machine Events
        public static UnityAction<string> OnMachineActivated;
        public static UnityAction<string> OnMachineDeactivated;

        // Energy Events
        public static UnityAction<float, float> OnEnergyChanged;
        public static UnityAction OnEnergyDepleted;

        // Digging Events
        public static UnityAction<Vector3, int> OnTileDug;
        public static UnityAction<Vector3> OnDigBlocked;
        public static UnityAction<Vector3, int> OnDigProgress;

        // Resource Pickup Events (from world drops)
        public static System.Action<Digging.UndergroundResourceType, int> OnResourcePickedUp;

        // Visual Feedback Events
        public static UnityAction<float> OnScreenShake;
        public static UnityAction<string, Vector3> OnFloatingText;
    }

    public enum ResourceType
    {
        // Raw Materials
        Dirt,
        Stone,
        Clay,
        Coal,
        IronOre,
        Copper,
        Silver,
        Gold,
        AncientArtifact,

        // Refined Materials
        CompressedDirt,
        HardenedClay,
        RefinedCoal,
        IronIngot,
        CopperIngot,
        SilverIngot,
        GoldIngot,

        // Crafted Components
        MetalPlate,
        WoodenPlank,
        GearPart,
        CircuitBoard,
        CopperWire,
        Circuit
    }

    [System.Serializable]
    public class ResourceData
    {
        public ResourceType type;
        public string displayName;
        public Sprite icon;
        public int minDepth;
        public int maxDepth;
        public float spawnChance;
        public int baseValue;
    }

    [System.Serializable]
    public class ToolData
    {
        public string toolName;
        public int tier;
        public float digSpeed;
        public int durability;
        public int maxDurability;
        public int maxDepth;
        public Sprite icon;
        public GameObject prefab;
    }

    [System.Serializable]
    public class StoryItem
    {
        public string itemId;
        public string title;
        [TextArea(3, 10)]
        public string description;
        public Sprite icon;
        public int depthFound;
        public bool isCollected;
    }
}
