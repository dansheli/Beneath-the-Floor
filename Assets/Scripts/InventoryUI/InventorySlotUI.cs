using UnityEngine;
using UnityEngine.UI;
using TMPro;
using BeneathTheFloor.Inventory;
using BeneathTheFloor.Crafting;

namespace BeneathTheFloor.InventoryUI
{
    /// <summary>
    /// UI component for a single inventory slot in the Storage Tray style.
    /// Click to select, click again or click elsewhere to deselect.
    /// Supports stacking with stack count display.
    /// Uses SelectGlow (outer) + SelectBorder (inner frame) for warm gold selection highlight.
    /// </summary>
    public class InventorySlotUI : MonoBehaviour
    {
        private Image itemIcon;
        private Image backgroundImage;
        private GameObject selectGlow;    // Outer gold glow
        private GameObject selectBorder;  // Inner gold frame border
        private TextMeshProUGUI stackCountText; // Text to show stack count

        private int slotIndex;
        private InventoryUIManager manager;
        private ItemStack currentStack;
        private bool isSelected = false;

        public int SlotIndex => slotIndex;
        public bool HasItem => !currentStack.IsEmpty;
        public ItemStack CurrentStack => currentStack;

        public void Initialize(int index, InventoryUIManager uiManager)
        {
            slotIndex = index;
            manager = uiManager;

            // Find item icon
            Transform iconTransform = transform.Find("ItemIcon");
            if (iconTransform != null)
            {
                itemIcon = iconTransform.GetComponent<Image>();
            }

            backgroundImage = GetComponent<Image>();

            // Find selection glow (outer glow effect)
            Transform glowTransform = transform.Find("SelectGlow");
            if (glowTransform != null)
            {
                selectGlow = glowTransform.gameObject;
                selectGlow.SetActive(false);
            }

            // Find selection border (inner gold frame)
            Transform borderTransform = transform.Find("SelectBorder");
            if (borderTransform != null)
            {
                selectBorder = borderTransform.gameObject;
                selectBorder.SetActive(false);
            }

            // Find or create stack count text
            Transform countTransform = transform.Find("StackCount");
            if (countTransform != null)
            {
                stackCountText = countTransform.GetComponent<TextMeshProUGUI>();
            }
            else
            {
                // Create stack count text if it doesn't exist
                CreateStackCountText();
            }

            UpdateDisplay(ItemStack.Empty);
        }

        /// <summary>
        /// Create the stack count text element if it doesn't exist in the prefab.
        /// </summary>
        private void CreateStackCountText()
        {
            GameObject countObj = new GameObject("StackCount");
            countObj.transform.SetParent(transform, false);

            stackCountText = countObj.AddComponent<TextMeshProUGUI>();
            stackCountText.fontSize = 14;
            stackCountText.fontStyle = FontStyles.Bold;
            stackCountText.alignment = TextAlignmentOptions.BottomRight;
            stackCountText.color = Color.white;
            stackCountText.enableWordWrapping = false;
            stackCountText.overflowMode = TextOverflowModes.Overflow;

            // Add outline for better visibility
            stackCountText.outlineWidth = 0.2f;
            stackCountText.outlineColor = Color.black;

            // Position in bottom-right corner
            RectTransform rect = countObj.GetComponent<RectTransform>();
            rect.anchorMin = new Vector2(0, 0);
            rect.anchorMax = new Vector2(1, 1);
            rect.offsetMin = new Vector2(2, 2);
            rect.offsetMax = new Vector2(-4, -4);

            // Initially hide
            stackCountText.gameObject.SetActive(false);
        }

        public void UpdateDisplay(ItemStack stack)
        {
            currentStack = stack;

            if (stack.IsEmpty)
            {
                // Empty slot - hide icon and count
                if (itemIcon != null)
                {
                    itemIcon.sprite = null;
                    itemIcon.enabled = false;
                }
                if (stackCountText != null)
                {
                    stackCountText.gameObject.SetActive(false);
                }
            }
            else
            {
                // Has item - ALWAYS show something visual
                if (itemIcon != null)
                {
                    if (stack.item.icon != null)
                    {
                        // Show the actual item icon
                        itemIcon.sprite = stack.item.icon;
                        itemIcon.enabled = true;
                        itemIcon.color = Color.white;
                    }
                    else
                    {
                        // No icon - show colored placeholder based on item category
                        // This ensures items are VISIBLE even without assigned icons
                        itemIcon.sprite = null;
                        itemIcon.enabled = true;
                        itemIcon.color = GetPlaceholderColor(stack.item);
                    }
                }

                // Show stack count if more than 1
                if (stackCountText != null)
                {
                    if (stack.amount > 1)
                    {
                        stackCountText.text = stack.amount.ToString();
                        stackCountText.gameObject.SetActive(true);
                    }
                    else
                    {
                        stackCountText.gameObject.SetActive(false);
                    }
                }
            }

            // Update selection highlight visibility
            UpdateSelectionHighlight();
        }

        /// <summary>
        /// Get a placeholder color based on item category for items without icons.
        /// </summary>
        private Color GetPlaceholderColor(ItemSO item)
        {
            if (item == null) return Color.gray;

            // Use category to determine color
            return item.category switch
            {
                ItemCategory.Material => new Color(0.65f, 0.50f, 0.35f, 1f),    // Brown for raw materials
                ItemCategory.Tool => new Color(0.50f, 0.55f, 0.60f, 1f),        // Steel gray for tools
                ItemCategory.Consumable => new Color(0.45f, 0.70f, 0.45f, 1f),  // Green for consumables
                ItemCategory.Equipment => new Color(0.55f, 0.65f, 0.75f, 1f),   // Blue-gray for equipment
                ItemCategory.Misc => new Color(0.70f, 0.55f, 0.80f, 1f),        // Purple for misc items
                _ => new Color(0.55f, 0.50f, 0.45f, 1f)                          // Default brown-gray
            };
        }

        public void SetSelected(bool selected)
        {
            isSelected = selected;
            UpdateSelectionHighlight();
        }

        private void UpdateSelectionHighlight()
        {
            // Show selection highlight when selected AND has item
            bool showHighlight = isSelected && HasItem;

            if (selectGlow != null)
            {
                selectGlow.SetActive(showHighlight);
            }

            if (selectBorder != null)
            {
                selectBorder.SetActive(showHighlight);
            }
        }
    }
}
