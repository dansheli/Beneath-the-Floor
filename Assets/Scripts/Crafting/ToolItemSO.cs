using UnityEngine;

namespace BeneathTheFloor.Crafting
{
    [CreateAssetMenu(fileName = "New Tool", menuName = "Beneath The Floor/Items/Tool Item")]
    public class ToolItemSO : ItemSO
    {
        [Header("Tool Properties")]
        public int tier = 1;
        public float digSpeed = 1f;
        public float digPower = 1f;
        public int maxDurability = 100;
        public int maxDigDepth = 10;

        [Header("Tool Type")]
        public ToolType toolType = ToolType.Shovel;

        private void OnEnable()
        {
            category = ItemCategory.Tool;
            isStackable = false;
            maxStackSize = 1;
        }

        public override string GetTooltip()
        {
            return $"<b>{itemName}</b> (Tier {tier})\n" +
                   $"{description}\n\n" +
                   $"Dig Speed: {digSpeed:F1}x\n" +
                   $"Dig Power: {digPower:F1}\n" +
                   $"Durability: {maxDurability}\n" +
                   $"Max Depth: {maxDigDepth}m";
        }
    }

    public enum ToolType
    {
        Shovel,
        Pickaxe,
        Drill,
        Hammer
    }
}
