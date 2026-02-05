using UnityEngine;
using BeneathTheFloor.Crafting;

namespace BeneathTheFloor.TreasureChests
{
    /// <summary>
    /// ScriptableObject defining possible rewards from a treasure chest.
    /// Create via: Assets > Create > Beneath the Floor > Treasure Chest Reward
    /// </summary>
    [CreateAssetMenu(fileName = "NewChestReward", menuName = "Beneath the Floor/Treasure Chest Reward")]
    public class TreasureChestReward : ScriptableObject
    {
        [Header("Identification")]
        [Tooltip("Unique ID for this reward (used for save/load)")]
        public string rewardId;

        [Tooltip("Display name shown in the reward popup")]
        public string displayName = "Mystery Reward";

        [Tooltip("Description shown in the reward popup")]
        [TextArea(2, 4)]
        public string description = "";

        [Header("Reward Type")]
        [Tooltip("What type of reward this is")]
        public RewardType rewardType = RewardType.Item;

        [Header("Item Reward")]
        [Tooltip("Item to give (if rewardType is Item or Tool)")]
        public ItemSO itemReward;

        [Tooltip("Quantity of items to give")]
        [Range(1, 99)]
        public int itemQuantity = 1;

        [Header("Currency Reward")]
        [Tooltip("Amount of currency to give (if rewardType is Currency)")]
        [Range(0, 100000)]
        public int currencyAmount = 0;

        [Header("Energy Drink Bonus")]
        [Tooltip("Include energy drinks with this reward?")]
        public bool includeEnergyDrink = false;

        [Tooltip("Number of energy drinks to give")]
        [Range(1, 5)]
        public int energyDrinkCount = 1;

        [Header("Note Reward")]
        [Tooltip("Note title (if rewardType is Note)")]
        public string noteTitle = "";

        [Tooltip("Note content/hint text (if rewardType is Note)")]
        [TextArea(3, 10)]
        public string noteContent = "";

        [Header("Achievement Reward")]
        [Tooltip("Achievement ID to unlock (if rewardType is Achievement)")]
        public string achievementId = "";

        [Header("Visual")]
        [Tooltip("Icon to show in the reward popup (optional, falls back to item icon)")]
        public Sprite customIcon;

        [Tooltip("Color tint for the reward popup")]
        public Color rewardColor = Color.white;

        [Header("Audio")]
        [Tooltip("Sound to play when this reward is given")]
        public AudioClip rewardSound;

        [Tooltip("Volume for the reward sound")]
        [Range(0f, 1f)]
        public float rewardVolume = 1f;

        /// <summary>
        /// Get the icon to display (custom or item's icon).
        /// </summary>
        public Sprite GetIcon()
        {
            if (customIcon != null)
                return customIcon;

            if (itemReward != null && itemReward.icon != null)
                return itemReward.icon;

            return null;
        }

        /// <summary>
        /// Get a formatted description of this reward.
        /// </summary>
        public string GetFormattedDescription()
        {
            string baseDesc;

            switch (rewardType)
            {
                case RewardType.Item:
                case RewardType.Tool:
                    if (itemReward != null)
                    {
                        if (itemQuantity > 1)
                            baseDesc = $"{itemQuantity}x {itemReward.itemName}";
                        else
                            baseDesc = itemReward.itemName;
                    }
                    else
                        baseDesc = displayName;
                    break;

                case RewardType.Currency:
                    baseDesc = $"${currencyAmount:N0}";
                    break;

                case RewardType.EnergyDrink:
                    if (energyDrinkCount > 1)
                        baseDesc = $"{energyDrinkCount}x Energy Drinks";
                    else
                        baseDesc = "Energy Drink";
                    break;

                case RewardType.Note:
                    baseDesc = noteTitle;
                    break;

                case RewardType.Achievement:
                    baseDesc = $"Achievement: {displayName}";
                    break;

                default:
                    baseDesc = displayName;
                    break;
            }

            // Add energy drink bonus to description if enabled (and not already an EnergyDrink type)
            if (includeEnergyDrink && energyDrinkCount > 0 && rewardType != RewardType.EnergyDrink)
            {
                string drinkText = energyDrinkCount > 1 ? $"{energyDrinkCount}x Energy Drinks" : "Energy Drink";
                baseDesc += $" + {drinkText}";
            }

            return baseDesc;
        }

        private void OnValidate()
        {
            // Auto-generate reward ID if empty
            if (string.IsNullOrEmpty(rewardId))
            {
                rewardId = name.ToLower().Replace(" ", "_");
            }
        }
    }

    /// <summary>
    /// Types of rewards a treasure chest can give.
    /// </summary>
    public enum RewardType
    {
        /// <summary>Generic item (resources, consumables, etc.)</summary>
        Item,

        /// <summary>Tool item (pickaxe, radar, etc.)</summary>
        Tool,

        /// <summary>In-game currency</summary>
        Currency,

        /// <summary>Energy drink consumable</summary>
        EnergyDrink,

        /// <summary>A note with hint/lore text</summary>
        Note,

        /// <summary>Unlock an achievement</summary>
        Achievement,

        /// <summary>Multiple rewards combined</summary>
        Bundle
    }
}
