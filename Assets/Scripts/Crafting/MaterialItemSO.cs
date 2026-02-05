using UnityEngine;

namespace BeneathTheFloor.Crafting
{
    [CreateAssetMenu(fileName = "New Material", menuName = "Beneath The Floor/Items/Material Item")]
    public class MaterialItemSO : ItemSO
    {
        [Header("Material Properties")]
        public MaterialType materialType = MaterialType.Raw;
        public ResourceType? sourceResource;
        public int craftingValue = 1;

        [Header("Processing")]
        public bool canBeRefined = true;
        public MaterialItemSO refinedOutput;
        public float refineTime = 5f;
        public int refineInputAmount = 1;
        public int refineOutputAmount = 1;

        private void OnEnable()
        {
            category = ItemCategory.Material;
            isStackable = true;
        }

        public override string GetTooltip()
        {
            string tooltip = $"<b>{itemName}</b> [{materialType}]\n{description}";

            if (canBeRefined && refinedOutput != null)
            {
                tooltip += $"\n\nCan be refined into: {refinedOutput.itemName}";
            }

            return tooltip;
        }
    }

    public enum MaterialType
    {
        Raw,
        Refined,
        Processed,
        Component,
        Ancient
    }
}
