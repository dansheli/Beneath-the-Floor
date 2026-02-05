using BeneathTheFloor.Crafting;
using BeneathTheFloor.Inventory;
using System.Collections.Generic;

namespace BeneathTheFloor.Logistics
{
    public interface IPlayerInventoryAdapter
    {
        bool TryAddItem(ItemSO item, int amount);
        float GetUsageRatio();
        IReadOnlyList<ItemStack> GetAllStacks();
        bool TryRemoveItem(ItemSO item, int amount);
    }
}
