using UnityEngine;
using UnityEngine.UI;
using TMPro;
using BeneathTheFloor.Inventory;
using BeneathTheFloor.Crafting;
using BeneathTheFloor.ResourceSystem;

namespace BeneathTheFloor.UI
{
    /// <summary>
    /// Displays the unified "Estimated Value" on the HUD.
    /// Combines: Dust value (in credits) + Inventory sellable items value.
    /// Positioned in top-left, below Credits display.
    /// Updates via events (not Update loop).
    /// </summary>
    public class EstimatedValueHUD : MonoBehaviour
    {
        [Header("UI References")]
        [SerializeField] private TextMeshProUGUI estimatedValueText;

        [Header("Position")]
        [SerializeField] private Vector2 anchoredPosition = new Vector2(10, -38);

        [Header("Colors")]
        [SerializeField] private Color valueColor = new Color(0.7f, 0.85f, 0.7f, 0.85f); // Dimmer than credits

        [Header("Debug")]
        [SerializeField] private bool debugMode = false;

        public static EstimatedValueHUD Instance { get; private set; }

        private Canvas parentCanvas;
        private int lastDisplayedValue = -1;

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
            // Find parent canvas - AVOID LoadingOverlay canvas!
            parentCanvas = GetComponentInParent<Canvas>();
            if (parentCanvas == null || IsLoadingOverlay(parentCanvas))
            {
                parentCanvas = FindHUDCanvas();
            }

            if (estimatedValueText == null)
            {
                CreateEstimatedValueUI();
            }

            // FORCE reposition to top-left (override any serialized values)
            ForceRepositionText();

            // Subscribe to events
            SubscribeToEvents();

            // Initial update
            RefreshDisplay();
        }

        /// <summary>
        /// Force reposition the text to the configured position.
        /// This overrides any serialized scene values.
        /// </summary>
        private void ForceRepositionText()
        {
            if (estimatedValueText == null) return;

            RectTransform textRect = estimatedValueText.GetComponent<RectTransform>();
            if (textRect != null)
            {
                textRect.anchorMin = new Vector2(0, 1); // Top-left
                textRect.anchorMax = new Vector2(0, 1);
                textRect.pivot = new Vector2(0, 1);
                textRect.anchoredPosition = new Vector2(5, -42); // Below currency, at top-left
            }

            // Also update text size
            estimatedValueText.fontSize = 28;
        }

        private void OnDestroy()
        {
            if (Instance == this)
            {
                Instance = null;
            }

            UnsubscribeFromEvents();
        }

        private void SubscribeToEvents()
        {
            // Subscribe to dust changes
            if (DustManager.Instance != null)
            {
                DustManager.Instance.OnDustChanged += OnDustChanged;
                if (debugMode) Debug.Log("[EstimatedValueHUD] Subscribed to DustManager");
            }
            else
            {
                StartCoroutine(WaitForDustManager());
            }

            // Subscribe to inventory changes
            if (InventorySystem.Instance != null)
            {
                InventorySystem.Instance.OnInventoryChanged += OnInventoryChanged;
                if (debugMode) Debug.Log("[EstimatedValueHUD] Subscribed to InventorySystem");
            }
            else
            {
                StartCoroutine(WaitForInventorySystem());
            }
        }

        private System.Collections.IEnumerator WaitForDustManager()
        {
            float timeout = 5f;
            float elapsed = 0f;

            while (DustManager.Instance == null && elapsed < timeout)
            {
                yield return new WaitForSeconds(0.1f);
                elapsed += 0.1f;
            }

            if (DustManager.Instance != null)
            {
                DustManager.Instance.OnDustChanged += OnDustChanged;
                RefreshDisplay();
                if (debugMode) Debug.Log("[EstimatedValueHUD] Connected to DustManager");
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
                if (debugMode) Debug.Log("[EstimatedValueHUD] Connected to InventorySystem");
            }
        }

        private void UnsubscribeFromEvents()
        {
            if (DustManager.Instance != null)
            {
                DustManager.Instance.OnDustChanged -= OnDustChanged;
            }

            if (InventorySystem.Instance != null)
            {
                InventorySystem.Instance.OnInventoryChanged -= OnInventoryChanged;
            }
        }

        private void CreateEstimatedValueUI()
        {
            if (parentCanvas == null)
            {
                Debug.LogError("[EstimatedValueHUD] No Canvas found!");
                return;
            }

            // Create text object
            GameObject textObj = new GameObject("EstimatedValueText");
            textObj.transform.SetParent(parentCanvas.transform, false);

            RectTransform textRect = textObj.AddComponent<RectTransform>();
            textRect.anchorMin = new Vector2(0, 1); // Top-left
            textRect.anchorMax = new Vector2(0, 1);
            textRect.pivot = new Vector2(0, 1);
            textRect.anchoredPosition = anchoredPosition;
            textRect.sizeDelta = new Vector2(200, 25);

            estimatedValueText = textObj.AddComponent<TextMeshProUGUI>();
            estimatedValueText.text = "\u2248 0"; // ≈ symbol
            estimatedValueText.fontSize = 28;
            estimatedValueText.color = valueColor;
            estimatedValueText.alignment = TextAlignmentOptions.Left;
            estimatedValueText.fontStyle = FontStyles.Normal;
            estimatedValueText.raycastTarget = false;

            if (debugMode)
            {
                Debug.Log("[EstimatedValueHUD] Created estimated value UI");
            }
        }

        private void OnDustChanged(float dustAmount)
        {
            RefreshDisplay();
        }

        private void OnInventoryChanged()
        {
            RefreshDisplay();
        }

        /// <summary>
        /// Refresh the display with the current estimated value.
        /// </summary>
        public void RefreshDisplay()
        {
            int totalValue = CalculateEstimatedValue();

            // Only update UI if value changed
            if (totalValue == lastDisplayedValue)
                return;

            lastDisplayedValue = totalValue;
            UpdateDisplay(totalValue);
        }

        /// <summary>
        /// Calculate the total estimated value (dust + inventory).
        /// Uses same logic as TradeTerminal/InventorySellService.
        /// </summary>
        private int CalculateEstimatedValue()
        {
            int dustValue = 0;
            int inventoryValue = 0;

            // Get dust value
            if (DustManager.Instance != null)
            {
                dustValue = DustManager.Instance.GetDustValue();
            }

            // Get inventory sell value (same logic as BagValueHUD/TradeTerminalUI)
            if (InventorySystem.Instance != null)
            {
                int slotCount = InventorySystem.Instance.MaxSlots;
                for (int i = 0; i < slotCount; i++)
                {
                    ItemStack stack = InventorySystem.Instance.GetStackAt(i);
                    if (stack.IsEmpty || stack.item == null) continue;
                    if (stack.item.sellPrice <= 0) continue;

                    // Skip tools and equipment (same as TradeTerminalUI)
                    if (stack.item.category == ItemCategory.Tool) continue;
                    if (stack.item.category == ItemCategory.Equipment) continue;

                    inventoryValue += stack.item.sellPrice * stack.amount;
                }
            }

            int total = dustValue + inventoryValue;

            if (debugMode)
            {
                Debug.Log($"[EstimatedValueHUD] Dust: {dustValue}, Inventory: {inventoryValue}, Total: {total}");
            }

            return total;
        }

        private void UpdateDisplay(int value)
        {
            if (estimatedValueText != null)
            {
                // Use ≈ symbol for "approximately"
                estimatedValueText.text = $"\u2248 {value:N0}";
            }
        }

        /// <summary>
        /// Get the current estimated value.
        /// </summary>
        public int GetCurrentEstimatedValue()
        {
            return CalculateEstimatedValue();
        }

        /// <summary>
        /// Show or hide the estimated value display.
        /// </summary>
        public void SetVisible(bool visible)
        {
            // Find canvas if not yet found - AVOID LoadingOverlay canvas!
            if (parentCanvas == null || IsLoadingOverlay(parentCanvas))
            {
                parentCanvas = GetComponentInParent<Canvas>();
                if (parentCanvas == null || IsLoadingOverlay(parentCanvas))
                {
                    parentCanvas = FindHUDCanvas();
                }
            }

            // Ensure UI exists before showing
            if (estimatedValueText == null && visible && parentCanvas != null)
            {
                CreateEstimatedValueUI();
                ForceRepositionText();
            }

            if (estimatedValueText != null)
            {
                estimatedValueText.gameObject.SetActive(visible);
            }
        }

        /// <summary>
        /// Check if a canvas is the LoadingOverlay (should be avoided for HUD elements).
        /// </summary>
        private bool IsLoadingOverlay(Canvas canvas)
        {
            if (canvas == null) return false;
            return canvas.name == "LoadingOverlay" ||
                   canvas.name.Contains("Loading") ||
                   canvas.sortingOrder >= 9000;
        }

        /// <summary>
        /// Find the proper HUD canvas, skipping LoadingOverlay and other non-HUD canvases.
        /// </summary>
        private Canvas FindHUDCanvas()
        {
            // First, try to find a canvas by preferred name
            string[] preferredCanvasNames = { "HUDCanvas", "GameCanvas", "MachineUICanvas", "MainCanvas" };
            foreach (var canvasName in preferredCanvasNames)
            {
                var canvasObj = GameObject.Find(canvasName);
                if (canvasObj != null)
                {
                    var canvas = canvasObj.GetComponent<Canvas>();
                    if (canvas != null && !IsLoadingOverlay(canvas))
                    {
                        return canvas;
                    }
                }
            }

            // Fall back to finding any canvas that's NOT the loading overlay
            Canvas[] allCanvases = FindObjectsOfType<Canvas>();
            foreach (var canvas in allCanvases)
            {
                if (IsLoadingOverlay(canvas)) continue;

                // Prefer screen space overlay canvases
                if (canvas.renderMode == RenderMode.ScreenSpaceOverlay)
                {
                    return canvas;
                }
            }

            // Last resort: create a new HUD canvas
            Debug.Log("[EstimatedValueHUD] Creating new HUDCanvas");
            var newCanvasObj = new GameObject("HUDCanvas");
            var newCanvas = newCanvasObj.AddComponent<Canvas>();
            newCanvas.renderMode = RenderMode.ScreenSpaceOverlay;
            newCanvas.sortingOrder = 100;
            newCanvasObj.AddComponent<UnityEngine.UI.CanvasScaler>();
            newCanvasObj.AddComponent<UnityEngine.UI.GraphicRaycaster>();
            return newCanvas;
        }
    }
}
