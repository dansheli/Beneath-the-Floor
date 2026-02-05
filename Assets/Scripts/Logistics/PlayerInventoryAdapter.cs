using BeneathTheFloor.Crafting;
using BeneathTheFloor.Inventory;
using System.Collections.Generic;

namespace BeneathTheFloor.Logistics
{
    public class PlayerInventoryAdapter : IPlayerInventoryAdapter
    {
        public bool TryAddItem(ItemSO item, int amount)
        {
            var inv = InventorySystem.Instance;
            if (inv == null) return false;
            return inv.TryAddItem(item, amount, out _);
        }

        public float GetUsageRatio()
        {
            var inv = InventorySystem.Instance;
            if (inv == null) return 1f;
            var stacks = inv.GetAllStacks();
            int used = 0;
            for (int i = 0; i < stacks.Count; i++)
            {
                if (!stacks[i].IsEmpty) used++;
            }
            return inv.MaxSlots > 0 ? (float)used / inv.MaxSlots : 1f;
        }

        public IReadOnlyList<ItemStack> GetAllStacks()
        {
            var inv = InventorySystem.Instance;
            if (inv == null) return new List<ItemStack>();
            return inv.GetAllStacks();
        }

        public bool TryRemoveItem(ItemSO item, int amount)
        {
            var inv = InventorySystem.Instance;
            if (inv == null) return false;
            return inv.TryRemoveItem(item, amount);
        }
    }
}
