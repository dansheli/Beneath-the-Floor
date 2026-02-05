using UnityEngine;
using UnityEngine.UI;
using TMPro;
using BeneathTheFloor.Crafting;

namespace BeneathTheFloor.InventoryUI
{
    /// <summary>
    /// Displays item information when hovering over inventory slots.
    /// </summary>
    public class InventoryTooltip : MonoBehaviour
    {
        [Header("UI References")]
        [SerializeField] private TextMeshProUGUI itemNameText;
        [SerializeField] private TextMeshProUGUI descriptionText;
        [SerializeField] private TextMeshProUGUI stackInfoText;

        [Header("Settings")]
        [SerializeField] private Vector2 offset = new Vector2(15, -15);
        [SerializeField] private float padding = 10f;

        private RectTransform rectTransform;
        private Canvas parentCanvas;
        private bool isShowing = false;

        private void Awake()
        {
            rectTransform = GetComponent<RectTransform>();

            // Find parent canvas
            parentCanvas = GetComponentInParent<Canvas>();
            if (parentCanvas == null)
            {
                parentCanvas = FindObjectOfType<Canvas>();
            }
        }

        public void Initialize(TextMeshProUGUI nameText, TextMeshProUGUI descText, TextMeshProUGUI stackText)
        {
            itemNameText = nameText;
            descriptionText = descText;
            stackInfoText = stackText;
        }

        private void Update()
        {
            if (isShowing)
            {
                UpdatePosition();
            }
        }

        public void Show(ItemSO item, int amount)
        {
            if (item == null)
            {
                Hide();
                return;
            }

            // Set item name
            if (itemNameText != null)
            {
                itemNameText.text = item.itemName;

                // Color by category
                itemNameText.color = GetCategoryColor(item.category);
            }

            // Set description
            if (descriptionText != null)
            {
                descriptionText.text = !string.IsNullOrEmpty(item.description) ? item.description : "No description.";
            }

            // Set stack info
            if (stackInfoText != null)
            {
                string categoryName = item.category.ToString();
                string stackable = item.isStackable ? $"Stack: {amount}/{item.maxStackSize}" : "Not stackable";
                stackInfoText.text = $"{categoryName} | {stackable}";
            }

            gameObject.SetActive(true);
            isShowing = true;
            UpdatePosition();

            Debug.Log($"[InventoryTooltip] Showing tooltip for {item.itemName}");
        }

        public void Hide()
        {
            gameObject.SetActive(false);
            isShowing = false;
        }

        private void UpdatePosition()
        {
            if (rectTransform == null || parentCanvas == null) return;

            Vector2 mousePos;
            RectTransformUtility.ScreenPointToLocalPointInRectangle(
                parentCanvas.transform as RectTransform,
                Input.mousePosition,
                parentCanvas.worldCamera,
                out mousePos
            );

            // Apply offset
            Vector2 tooltipPos = mousePos + offset;

            // Keep tooltip on screen
            Vector2 canvasSize = (parentCanvas.transform as RectTransform).sizeDelta;
            Vector2 tooltipSize = rectTransform.sizeDelta;

            // Clamp to canvas bounds
            float maxX = canvasSize.x / 2 - tooltipSize.x - padding;
            float minX = -canvasSize.x / 2 + padding;
            float maxY = canvasSize.y / 2 - padding;
            float minY = -canvasSize.y / 2 + tooltipSize.y + padding;

            tooltipPos.x = Mathf.Clamp(tooltipPos.x, minX, maxX);
            tooltipPos.y = Mathf.Clamp(tooltipPos.y, minY, maxY);

            rectTransform.anchoredPosition = tooltipPos;
        }

        private Color GetCategoryColor(ItemCategory category)
        {
            switch (category)
            {
                case ItemCategory.Tool:
                    return new Color(0.4f, 0.7f, 1f); // Light blue
                case ItemCategory.Material:
                    return new Color(0.8f, 0.8f, 0.8f); // Gray
                case ItemCategory.Consumable:
                    return new Color(0.4f, 1f, 0.4f); // Green
                case ItemCategory.Equipment:
                    return new Color(1f, 0.8f, 0.4f); // Orange
                default:
                    return Color.white;
            }
        }
    }
}
