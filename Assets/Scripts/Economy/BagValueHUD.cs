using UnityEngine;
using UnityEngine.UI;
using TMPro;
using BeneathTheFloor.Inventory;
using BeneathTheFloor.Crafting;

namespace BeneathTheFloor.Economy
{
    /// <summary>
    /// Calculates the total sell value of resources in the player's bag.
    /// NOTE: HUD display is now disabled - use EstimatedValueHUD for unified display.
    /// This class is kept for the CalculateBagValue() method used by other systems.
    /// </summary>
    public class BagValueHUD : MonoBehaviour
    {
        [Header("UI References (DEPRECATED - use EstimatedValueHUD)")]
        [SerializeField] private GameObject bagValuePanel;
        [SerializeField] private TextMeshProUGUI valueText;
        [SerializeField] private Image bagIcon;

        [Header("Settings")]
        [SerializeField] private bool autoCreateUI = false; // DISABLED - HUD moved to EstimatedValueHUD
        [SerializeField] private Sprite customBagIcon;
        [SerializeField] private Color textColor = new Color(0.6f, 0.9f, 0.6f); // Light green
        [SerializeField] private Color panelColor = new Color(0, 0, 0, 0.6f);
        [SerializeField] private string valuePrefix = "Bag: ";
        [SerializeField] private string valueSuffix = " cr";

        [Header("Position")]
        [Tooltip("Position relative to top-right corner. Below currency HUD by default.")]
        [SerializeField] private Vector2 anchoredPosition = new Vector2(-20, -125);

        [Header("Visibility")]
        [Tooltip("Hide when bag value is 0")]
        [SerializeField] private bool hideWhenEmpty = false;

        [Header("Debug")]
        [SerializeField] private bool debugMode = false;

        public static BagValueHUD Instance { get; private set; }

        private Canvas parentCanvas;
        private int lastDisplayedValue = -1;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetStatics()
        {
            Instance = null;
        }

        private void Awake()
        {
            if (Instance == null)
            {
                Instance = this;
            }
            else
            {
                Destroy(gameObject);
                return;
            }
        }

        private void Start()
        {
            // Find parent canvas
            parentCanvas = GetComponentInParent<Canvas>();
            if (parentCanvas == null)
            {
                parentCanvas = FindObjectOfType<Canvas>();
            }

            if (autoCreateUI && bagValuePanel == null)
            {
                CreateBagValueUI();
            }

            // Subscribe to inventory changes
            SubscribeToInventory();

            // Initial update
            RefreshDisplay();
        }

        private void OnDestroy()
        {
            if (Instance == this)
            {
                Instance = null;
            }

            UnsubscribeFromInventory();
        }

        private void SubscribeToInventory()
        {
            if (InventorySystem.Instance != null)
            {
                InventorySystem.Instance.OnInventoryChanged += OnInventoryChanged;
                if (debugMode)
                {
                    Debug.Log("[BagValueHUD] Subscribed to InventorySystem");
                }
            }
            else
            {
                StartCoroutine(WaitForInventorySystem());
            }
        }

        private System.Collections.IEnumerator WaitForInventorySystem()
        {
            float timeout = 5f;
            float elapsed = 0f;

            while (InventorySystem.Instance == null && elapsed < timeout)
            {
                yield return new WaitForSeconds(0.1f);
                elapsed += 0.1f;
            }

            if (InventorySystem.Instance != null)
            {
                InventorySystem.Instance.OnInventoryChanged += OnInventoryChanged;
                RefreshDisplay();

                if (debugMode)
                {
                    Debug.Log("[BagValueHUD] Connected to InventorySystem");
                }
            }
            else
            {
                Debug.LogWarning("[BagValueHUD] InventorySystem not found after timeout");
            }
        }

        private void UnsubscribeFromInventory()
        {
            if (InventorySystem.Instance != null)
            {
                InventorySystem.Instance.OnInventoryChanged -= OnInventoryChanged;
            }
        }

        private void CreateBagValueUI()
        {
            if (parentCanvas == null)
            {
                Debug.LogError("[BagValueHUD] No Canvas found!");
                return;
            }

            // Create panel
            bagValuePanel = new GameObject("BagValuePanel");
            bagValuePanel.transform.SetParent(parentCanvas.transform, false);

            RectTransform panelRect = bagValuePanel.AddComponent<RectTransform>();
            panelRect.anchorMin = new Vector2(1, 1); // Top-right
            panelRect.anchorMax = new Vector2(1, 1);
            panelRect.pivot = new Vector2(1, 1);
            panelRect.anchoredPosition = anchoredPosition;
            panelRect.sizeDelta = new Vector2(160, 40);

            Image panelBg = bagValuePanel.AddComponent<Image>();
            panelBg.color = panelColor;

            // Add horizontal layout
            HorizontalLayoutGroup layout = bagValuePanel.AddComponent<HorizontalLayoutGroup>();
            layout.padding = new RectOffset(10, 10, 5, 5);
            layout.spacing = 8;
            layout.childAlignment = TextAnchor.MiddleCenter;
            layout.childControlWidth = false;
            layout.childControlHeight = true;
            layout.childForceExpandWidth = false;
            layout.childForceExpandHeight = false;

            // Create bag icon
            GameObject iconObj = new GameObject("BagIcon");
            iconObj.transform.SetParent(bagValuePanel.transform, false);

            RectTransform iconRect = iconObj.AddComponent<RectTransform>();
            iconRect.sizeDelta = new Vector2(24, 24);

            bagIcon = iconObj.AddComponent<Image>();
            if (customBagIcon != null)
            {
                bagIcon.sprite = customBagIcon;
            }
            else
            {
                // Create a simple square as bag placeholder (brown/tan color)
                bagIcon.color = new Color(0.65f, 0.50f, 0.35f);
            }

            LayoutElement iconLayout = iconObj.AddComponent<LayoutElement>();
            iconLayout.minWidth = 24;
            iconLayout.minHeight = 24;
            iconLayout.preferredWidth = 24;

            // Create text
            GameObject textObj = new GameObject("ValueText");
            textObj.transform.SetParent(bagValuePanel.transform, false);

            valueText = textObj.AddComponent<TextMeshProUGUI>();
            valueText.text = $"{valuePrefix}0{valueSuffix}";
            valueText.fontSize = 18;
            valueText.color = textColor;
            valueText.alignment = TextAlignmentOptions.MidlineLeft;
            valueText.fontStyle = FontStyles.Normal;

            LayoutElement textLayout = textObj.AddComponent<LayoutElement>();
            textLayout.minWidth = 100;
            textLayout.flexibleWidth = 1;

            if (debugMode)
            {
                Debug.Log("[BagValueHUD] Created bag value UI panel");
            }
        }

        private void OnInventoryChanged()
        {
            RefreshDisplay();
        }

        /// <summary>
        /// Refresh the display with the current bag value.
        /// </summary>
        public void RefreshDisplay()
        {
            int totalValue = CalculateBagValue();

            // Only update UI if value changed
            if (totalValue == lastDisplayedValue)
                return;

            lastDisplayedValue = totalValue;
            UpdateDisplay(totalValue);

            // Handle visibility when empty
            if (hideWhenEmpty && bagValuePanel != null)
            {
                bagValuePanel.SetActive(totalValue > 0);
            }
        }

        private int CalculateBagValue()
        {
            // Use InventorySellService if available for accurate calculation
            if (InventorySellService.Instance != null && InventorySystem.Instance != null)
            {
                return InventorySellService.Instance.GetTotalResourceValue(InventorySystem.Instance);
            }

            // Fallback: calculate manually
            if (InventorySystem.Instance == null) return 0;

            var stacks = InventorySystem.Instance.GetAllStacks();
            int totalValue = 0;

            foreach (var stack in stacks)
            {
                if (stack.IsEmpty || stack.item == null) continue;
                if (stack.item.sellPrice <= 0) continue;

                // Skip tools and equipment
                if (stack.item.category == ItemCategory.Tool) continue;
                if (stack.item.category == ItemCategory.Equipment) continue;

                totalValue += stack.item.sellPrice * stack.amount;
            }

            return totalValue;
        }

        private void UpdateDisplay(int value)
        {
            if (valueText != null)
            {
                valueText.text = $"{valuePrefix}{value:N0}{valueSuffix}";
            }

            if (debugMode)
            {
                Debug.Log($"[BagValueHUD] Updated display: {value:N0} credits");
            }
        }

        /// <summary>
        /// Show or hide the bag value panel.
        /// </summary>
        public void SetVisible(bool visible)
        {
            if (bagValuePanel != null)
            {
                bagValuePanel.SetActive(visible);
            }
        }

        /// <summary>
        /// Get the current calculated bag value.
        /// </summary>
        public int GetCurrentBagValue()
        {
            return CalculateBagValue();
        }
    }
}
