using UnityEngine;

namespace BeneathTheFloor.Crafting
{
    public enum ItemCategory
    {
        Tool,
        Material,
        Consumable,
        Equipment,
        Misc
    }

    [CreateAssetMenu(fileName = "New Item", menuName = "Beneath The Floor/Items/Base Item")]
    public class ItemSO : ScriptableObject
    {
        [Header("Basic Info")]
        public string itemId;
        public string itemName;
        [TextArea(2, 5)]
        public string description;
        public Sprite icon;
        public ItemCategory category;

        [Header("Stacking")]
        public bool isStackable = true;
        public int maxStackSize = 99;

        [Header("Value")]
        public int buyPrice = 0;
        public int sellPrice = 0;

        [Header("Visuals")]
        public GameObject worldPrefab;
        public GameObject heldPrefab;

        public virtual string GetTooltip()
        {
            return $"<b>{itemName}</b>\n{description}";
        }
    }
}
