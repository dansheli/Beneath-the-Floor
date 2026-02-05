using UnityEngine;
using System.Collections.Generic;

namespace BeneathTheFloor.Crafting
{
    [CreateAssetMenu(fileName = "New Recipe", menuName = "Beneath The Floor/Crafting/Recipe")]
    public class CraftingRecipe : ScriptableObject
    {
        [Header("Recipe Info")]
        public string recipeId;
        public string recipeName;
        [TextArea(2, 4)]
        public string description;
        public Sprite icon;

        [Header("Requirements")]
        public List<ResourceRequirement> requiredResources = new List<ResourceRequirement>();
        public List<ItemRequirement> requiredItems = new List<ItemRequirement>();

        [Header("Output")]
        public ItemSO outputItem;
        public int outputAmount = 1;

        [Header("Crafting")]
        public float craftingTime = 2f;
        public int requiredWorkbenchTier = 1;
        public float energyCost = 0f;

        [Header("Unlock")]
        public bool isUnlockedByDefault = true;
        public string unlockCondition;

        public bool CanCraft(Inventory.InventorySystem inventory)
        {
            if (inventory == null) return false;

            // Check resource requirements
            foreach (var req in requiredResources)
            {
                if (inventory.GetResourceCount(req.resourceType) < req.amount)
                {
                    return false;
                }
            }

            // Item requirements would be checked against an item inventory
            // For now, we only use resources

            return true;
        }

        public void ConsumeResources(Inventory.InventorySystem inventory)
        {
            if (inventory == null) return;

            foreach (var req in requiredResources)
            {
                inventory.RemoveResource(req.resourceType, req.amount);
            }
        }

        public string GetRequirementsText()
        {
            System.Text.StringBuilder sb = new System.Text.StringBuilder();
            sb.AppendLine("Requirements:");

            foreach (var req in requiredResources)
            {
                sb.AppendLine($"  - {req.resourceType}: {req.amount}");
            }

            foreach (var req in requiredItems)
            {
                string itemName = req.item != null ? req.item.itemName : "Unknown";
                sb.AppendLine($"  - {itemName}: {req.amount}");
            }

            return sb.ToString();
        }
    }

    [System.Serializable]
    public class ResourceRequirement
    {
        public ResourceType resourceType;
        public int amount;
    }

    [System.Serializable]
    public class ItemRequirement
    {
        public ItemSO item;
        public int amount;
    }
}
