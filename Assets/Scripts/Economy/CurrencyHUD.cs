using UnityEngine;
using UnityEngine.UI;
using TMPro;

namespace BeneathTheFloor.Economy
{
    /// <summary>
    /// Displays the player's currency on the HUD.
    /// Auto-creates UI if needed, subscribes to CurrencyManager events.
    /// </summary>
    public class CurrencyHUD : MonoBehaviour
    {
        [Header("UI References")]
        [SerializeField] private GameObject currencyPanel;
        [SerializeField] private TextMeshProUGUI currencyText;
        [SerializeField] private Image currencyIcon;

        [Header("Settings")]
        [SerializeField] private bool autoCreateUI = true;
        [SerializeField] private Sprite customIcon;
        [SerializeField] private Color textColor = Color.yellow;
        [SerializeField] private Color panelColor = new Color(0, 0, 0, 0.6f);

        [Header("Position")]
        [Tooltip("Position in top-left")]
        [SerializeField] private Vector2 anchoredPosition = new Vector2(10, -2);

        [Header("Debug")]
        [SerializeField] private bool debugMode = false;

        public static CurrencyHUD Instance { get; private set; }

        private Canvas parentCanvas;

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

            if (autoCreateUI && currencyPanel == null)
            {
                CreateCurrencyUI();
            }

            // FORCE reposition panel to top-left corner (override any serialized values)
            ForceRepositionPanel();

            // Subscribe to currency changes
            if (CurrencyManager.Instance != null)
            {
                CurrencyManager.Instance.OnCurrencyChanged += OnCurrencyChanged;
                // Initialize with current value
                UpdateDisplay(CurrencyManager.Instance.CurrentAmount);
            }
            else
            {
                StartCoroutine(WaitForCurrencyManager());
            }
        }

        /// <summary>
        /// Force reposition the panel to the configured position.
        /// This overrides any serialized scene values.
        /// </summary>
        private void ForceRepositionPanel()
        {
            if (currencyPanel == null) return;

            RectTransform panelRect = currencyPanel.GetComponent<RectTransform>();
            if (panelRect != null)
            {
                panelRect.anchorMin = new Vector2(0, 1); // Top-left
                panelRect.anchorMax = new Vector2(0, 1);
                panelRect.pivot = new Vector2(0, 1);
                panelRect.anchoredPosition = new Vector2(5, -5); // Right at top-left corner
            }

            // Also update text size
            if (currencyText != null)
            {
                currencyText.fontSize = 34;
            }
        }

        private System.Collections.IEnumerator WaitForCurrencyManager()
        {
            float timeout = 5f;
            float elapsed = 0f;

            while (CurrencyManager.Instance == null && elapsed < timeout)
            {
                yield return new WaitForSeconds(0.1f);
                elapsed += 0.1f;
            }

            if (CurrencyManager.Instance != null)
            {
                CurrencyManager.Instance.OnCurrencyChanged += OnCurrencyChanged;
                UpdateDisplay(CurrencyManager.Instance.CurrentAmount);

                if (debugMode)
                {
                    Debug.Log("[CurrencyHUD] Connected to CurrencyManager");
                }
            }
            else
            {
                Debug.LogWarning("[CurrencyHUD] CurrencyManager not found after timeout");
            }
        }

        private void OnDestroy()
        {
            if (CurrencyManager.Instance != null)
            {
                CurrencyManager.Instance.OnCurrencyChanged -= OnCurrencyChanged;
            }
        }

        private void CreateCurrencyUI()
        {
            if (parentCanvas == null)
            {
                Debug.LogError("[CurrencyHUD] No Canvas found!");
                return;
            }

            // Create panel
            currencyPanel = new GameObject("CurrencyPanel");
            currencyPanel.transform.SetParent(parentCanvas.transform, false);

            RectTransform panelRect = currencyPanel.AddComponent<RectTransform>();
            panelRect.anchorMin = new Vector2(0, 1); // Top-left
            panelRect.anchorMax = new Vector2(0, 1);
            panelRect.pivot = new Vector2(0, 1);
            panelRect.anchoredPosition = anchoredPosition;
            panelRect.sizeDelta = new Vector2(180, 30);

            // No background panel - clean text only

            // Create text directly (with diamond/gem prefix)
            GameObject textObj = new GameObject("CurrencyText");
            textObj.transform.SetParent(currencyPanel.transform, false);

            RectTransform textRect = textObj.AddComponent<RectTransform>();
            textRect.anchorMin = Vector2.zero;
            textRect.anchorMax = Vector2.one;
            textRect.offsetMin = Vector2.zero;
            textRect.offsetMax = Vector2.zero;

            currencyText = textObj.AddComponent<TextMeshProUGUI>();
            currencyText.text = "$ 0"; // Currency prefix + value
            currencyText.fontSize = 34;
            currencyText.color = textColor;
            currencyText.alignment = TextAlignmentOptions.Left;
            currencyText.fontStyle = FontStyles.Bold;
            currencyText.raycastTarget = false;

            if (debugMode)
            {
                Debug.Log("[CurrencyHUD] Created currency UI panel");
            }
        }

        private void OnCurrencyChanged(int newAmount)
        {
            UpdateDisplay(newAmount);
        }

        private void UpdateDisplay(int amount)
        {
            if (currencyText != null)
            {
                currencyText.text = $"$ {amount:N0}"; // Currency prefix + value
            }

            if (debugMode)
            {
                Debug.Log($"[CurrencyHUD] Updated display: {amount:N0}");
            }
        }

        /// <summary>
        /// Force refresh the display from CurrencyManager.
        /// </summary>
        public void Refresh()
        {
            if (CurrencyManager.Instance != null)
            {
                UpdateDisplay(CurrencyManager.Instance.CurrentAmount);
            }
        }

        /// <summary>
        /// Show or hide the currency panel.
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

            // Ensure panel exists before trying to show it
            if (currencyPanel == null && visible && parentCanvas != null)
            {
                CreateCurrencyUI();
                ForceRepositionPanel();
            }

            if (currencyPanel != null)
            {
                currencyPanel.SetActive(visible);
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
            Debug.Log("[CurrencyHUD] Creating new HUDCanvas for currency panel");
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
