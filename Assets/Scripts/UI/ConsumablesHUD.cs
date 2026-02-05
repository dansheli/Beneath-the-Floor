using UnityEngine;
using UnityEngine.UI;
using TMPro;
using BeneathTheFloor.Energy;
using BeneathTheFloor.Lighting;

namespace BeneathTheFloor.UI
{
    /// <summary>
    /// Displays the consumables panel in the bottom-right of the HUD.
    /// Shows: Energy Drink (R key) + Lamp (T key) with counts.
    /// Updates via events (not Update loop).
    /// </summary>
    public class ConsumablesHUD : MonoBehaviour
    {
        [Header("UI References")]
        [SerializeField] private GameObject consumablesPanel;
        [SerializeField] private TextMeshProUGUI drinkText;
        [SerializeField] private TextMeshProUGUI lampText;

        [Header("Position")]
        [SerializeField] private Vector2 anchoredPosition = new Vector2(-5, 40);
        [SerializeField] private Vector2 panelSize = new Vector2(160, 110);

        [Header("Colors")]
        [SerializeField] private Color drinkColor = new Color(0.4f, 0.8f, 1f); // Light blue
        [SerializeField] private Color lampColor = new Color(1f, 0.9f, 0.5f); // Warm yellow
        [SerializeField] private Color dimmedColor = new Color(0.5f, 0.5f, 0.5f, 0.6f); // Gray when 0
        [SerializeField] private Color panelColor = new Color(0, 0, 0, 0f); // Transparent - no background

        [Header("Debug")]
        [SerializeField] private bool debugMode = false;

        [Header("Highlight Settings")]
        [SerializeField] private float highlightDuration = 3f;
        [SerializeField] private float pulseSpeed = 4f;
        [SerializeField] private Color highlightColor = new Color(1f, 1f, 0.3f); // Bright yellow

        public static ConsumablesHUD Instance { get; private set; }

        private Canvas parentCanvas;
        private Image drinkIcon;
        private Image lampIcon;

        // Highlight state
        private bool isDrinkHighlighted;
        private float highlightEndTime;
        private GameObject highlightGlow;

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

            // Clean up any old ToolPanel that might be lingering
            CleanupOldPanels();

            if (consumablesPanel == null)
            {
                CreateConsumablesUI();
            }

            // Subscribe to events
            SubscribeToEvents();

            // Initial update
            RefreshDisplay();
        }

        private void OnDestroy()
        {
            if (Instance == this)
            {
                Instance = null;
            }

            UnsubscribeFromEvents();
        }

        private void CleanupOldPanels()
        {
            if (parentCanvas == null) return;

            // Find and destroy old ToolPanel or any other conflicting panels
            string[] panelsToDestroy = { "ToolPanel", "OldConsumablesPanel", "ConsumablesPanel", "SuppliesPanel" };
            foreach (string panelName in panelsToDestroy)
            {
                Transform oldPanel = parentCanvas.transform.Find(panelName);
                if (oldPanel != null)
                {
                    Debug.Log($"[ConsumablesHUD] Destroying old panel: {panelName}");
                    Destroy(oldPanel.gameObject);
                }
            }

            // Also search for any dark background panels in the bottom-right area
            foreach (Transform child in parentCanvas.transform)
            {
                RectTransform rt = child.GetComponent<RectTransform>();
                Image img = child.GetComponent<Image>();

                // Check if it's a dark panel in bottom-right corner (not our panel)
                if (rt != null && img != null && child.name != "ConsumablesPanel")
                {
                    // Check if anchored to bottom-right
                    if (rt.anchorMin.x >= 0.7f && rt.anchorMax.y <= 0.3f)
                    {
                        // Check if it has a dark color
                        if (img.color.r < 0.3f && img.color.g < 0.3f && img.color.b < 0.3f && img.color.a > 0.3f)
                        {
                            Debug.Log($"[ConsumablesHUD] Destroying suspicious dark panel: {child.name}");
                            Destroy(child.gameObject);
                        }
                    }
                }
            }
        }

        private void SubscribeToEvents()
        {
            // Subscribe to drink count changes
            if (EnergyManager.Instance != null)
            {
                EnergyManager.Instance.OnDrinkCountChanged += OnDrinkCountChanged;
                if (debugMode) Debug.Log("[ConsumablesHUD] Subscribed to EnergyManager");
            }
            else
            {
                StartCoroutine(WaitForEnergyManager());
            }

            // LampPlacementController doesn't have an event, so we'll poll in a coroutine
            StartCoroutine(PollLampCount());
        }

        private System.Collections.IEnumerator WaitForEnergyManager()
        {
            float timeout = 5f;
            float elapsed = 0f;

            while (EnergyManager.Instance == null && elapsed < timeout)
            {
                yield return new WaitForSeconds(0.1f);
                elapsed += 0.1f;
            }

            if (EnergyManager.Instance != null)
            {
                EnergyManager.Instance.OnDrinkCountChanged += OnDrinkCountChanged;
                UpdateDrinkDisplay(EnergyManager.Instance.DrinkCount);
                if (debugMode) Debug.Log("[ConsumablesHUD] Connected to EnergyManager");
            }
        }

        private System.Collections.IEnumerator PollLampCount()
        {
            int lastLampCount = -1;

            while (true)
            {
                yield return new WaitForSeconds(0.5f); // Poll every 0.5 seconds

                if (LampPlacementController.Instance != null)
                {
                    int currentCount = LampPlacementController.Instance.LampsAvailable;
                    if (currentCount != lastLampCount)
                    {
                        lastLampCount = currentCount;
                        UpdateLampDisplay(currentCount);
                    }
                }
            }
        }

        private void UnsubscribeFromEvents()
        {
            if (EnergyManager.Instance != null)
            {
                EnergyManager.Instance.OnDrinkCountChanged -= OnDrinkCountChanged;
            }
        }

        private void CreateConsumablesUI()
        {
            if (parentCanvas == null)
            {
                Debug.LogError("[ConsumablesHUD] No Canvas found!");
                return;
            }

            // Create main panel - simple container
            consumablesPanel = new GameObject("ConsumablesPanel");
            consumablesPanel.transform.SetParent(parentCanvas.transform, false);

            RectTransform panelRect = consumablesPanel.AddComponent<RectTransform>();
            panelRect.anchorMin = new Vector2(1, 0); // Bottom-right
            panelRect.anchorMax = new Vector2(1, 0);
            panelRect.pivot = new Vector2(1, 0);
            panelRect.anchoredPosition = new Vector2(-20, 20);
            panelRect.sizeDelta = new Vector2(287, 173);

            // Load sprites
            Sprite drinkSprite = Resources.Load<Sprite>("Icons/icon_energy_drink");
            Sprite lampSprite = Resources.Load<Sprite>("Icons/icon_lamp");

            // Create drink row (top) - Y offset from bottom
            CreateSimpleRow(consumablesPanel.transform, 86, drinkSprite, "R", drinkColor, out drinkIcon, out drinkText);

            // Create lamp row (bottom)
            CreateSimpleRow(consumablesPanel.transform, 9, lampSprite, "T", lampColor, out lampIcon, out lampText);

            if (debugMode) Debug.Log("[ConsumablesHUD] Created simple consumables panel");
        }

        private void CreateSimpleRow(Transform parent, float yPos, Sprite sprite, string key, Color color,
            out Image iconOut, out TextMeshProUGUI textOut)
        {
            // Row dimensions (reduced)
            float iconSize = 61;
            float spacing = 12;
            float keyWidth = 58;
            float countWidth = 67;
            float rowHeight = 69;
            float totalWidth = iconSize + spacing + keyWidth + spacing + countWidth;

            // Icon - rightmost position minus total width
            GameObject iconObj = new GameObject("Icon");
            iconObj.transform.SetParent(parent, false);
            RectTransform iconRect = iconObj.AddComponent<RectTransform>();
            iconRect.anchorMin = new Vector2(1, 0);
            iconRect.anchorMax = new Vector2(1, 0);
            iconRect.pivot = new Vector2(1, 0);
            iconRect.anchoredPosition = new Vector2(-totalWidth + iconSize, yPos);
            iconRect.sizeDelta = new Vector2(iconSize, iconSize);

            iconOut = iconObj.AddComponent<Image>();
            iconOut.preserveAspect = true;
            if (sprite != null)
            {
                iconOut.sprite = sprite;
                iconOut.color = Color.white;
            }
            else
            {
                iconOut.color = color;
            }

            // Key hint [R] or [T]
            GameObject keyObj = new GameObject("Key");
            keyObj.transform.SetParent(parent, false);
            RectTransform keyRect = keyObj.AddComponent<RectTransform>();
            keyRect.anchorMin = new Vector2(1, 0);
            keyRect.anchorMax = new Vector2(1, 0);
            keyRect.pivot = new Vector2(1, 0);
            keyRect.anchoredPosition = new Vector2(-countWidth - spacing, yPos);
            keyRect.sizeDelta = new Vector2(keyWidth, rowHeight);

            TextMeshProUGUI keyText = keyObj.AddComponent<TextMeshProUGUI>();
            keyText.text = $"[{key}]";
            keyText.fontSize = 34;
            keyText.color = new Color(0.75f, 0.75f, 0.75f);
            keyText.alignment = TextAlignmentOptions.Center;
            keyText.fontStyle = FontStyles.Bold;

            // Count text x0
            GameObject countObj = new GameObject("Count");
            countObj.transform.SetParent(parent, false);
            RectTransform countRect = countObj.AddComponent<RectTransform>();
            countRect.anchorMin = new Vector2(1, 0);
            countRect.anchorMax = new Vector2(1, 0);
            countRect.pivot = new Vector2(1, 0);
            countRect.anchoredPosition = new Vector2(0, yPos);
            countRect.sizeDelta = new Vector2(countWidth, rowHeight);

            textOut = countObj.AddComponent<TextMeshProUGUI>();
            textOut.text = "x0";
            textOut.fontSize = 38;
            textOut.color = color;
            textOut.alignment = TextAlignmentOptions.MidlineRight;
            textOut.fontStyle = FontStyles.Bold;
        }

        private void OnDrinkCountChanged(int current, int max)
        {
            UpdateDrinkDisplay(current);
        }

        private void UpdateDrinkDisplay(int count)
        {
            if (drinkText != null)
            {
                drinkText.text = $"x{count}";
                drinkText.color = count > 0 ? drinkColor : dimmedColor;
            }

            if (drinkIcon != null)
            {
                drinkIcon.color = count > 0 ? drinkColor : dimmedColor;
            }
        }

        private void UpdateLampDisplay(int count)
        {
            if (lampText != null)
            {
                lampText.text = $"x{count}";
                lampText.color = count > 0 ? lampColor : dimmedColor;
            }

            if (lampIcon != null)
            {
                lampIcon.color = count > 0 ? lampColor : dimmedColor;
            }
        }

        /// <summary>
        /// Refresh all displays with current values.
        /// </summary>
        public void RefreshDisplay()
        {
            // Ensure panel exists
            if (consumablesPanel == null && parentCanvas != null)
            {
                CreateConsumablesUI();
            }

            if (EnergyManager.Instance != null)
            {
                UpdateDrinkDisplay(EnergyManager.Instance.DrinkCount);
            }

            if (LampPlacementController.Instance != null)
            {
                UpdateLampDisplay(LampPlacementController.Instance.LampsAvailable);
            }
        }

        /// <summary>
        /// Show or hide the consumables panel.
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
            if (consumablesPanel == null && visible && parentCanvas != null)
            {
                CreateConsumablesUI();
            }

            if (consumablesPanel != null)
            {
                consumablesPanel.SetActive(visible);
            }
        }

        private void Update()
        {
            // Handle drink highlight pulsing
            if (isDrinkHighlighted)
            {
                // Stop highlight when R is pressed or time expires
                if (Input.GetKeyDown(KeyCode.R) || Time.time >= highlightEndTime)
                {
                    StopDrinkHighlight();
                }
                else
                {
                    // Pulse the glow
                    if (highlightGlow != null)
                    {
                        float pulse = (Mathf.Sin(Time.time * pulseSpeed) + 1f) / 2f;
                        float alpha = Mathf.Lerp(0.3f, 0.8f, pulse);
                        Image glowImage = highlightGlow.GetComponent<Image>();
                        if (glowImage != null)
                        {
                            glowImage.color = new Color(highlightColor.r, highlightColor.g, highlightColor.b, alpha);
                        }
                    }

                    // Also pulse the drink icon scale
                    if (drinkIcon != null)
                    {
                        float scale = 1f + Mathf.Sin(Time.time * pulseSpeed) * 0.1f;
                        drinkIcon.transform.localScale = Vector3.one * scale;
                    }
                }
            }
        }

        /// <summary>
        /// Highlight the drink icon with a pulsing glow effect.
        /// Called when player tries to dig without energy.
        /// </summary>
        public void HighlightDrink()
        {
            // Only highlight if player has drinks
            if (EnergyManager.Instance == null || EnergyManager.Instance.DrinkCount <= 0)
            {
                return;
            }

            isDrinkHighlighted = true;
            highlightEndTime = Time.time + highlightDuration;

            // Create glow effect if not exists
            if (highlightGlow == null && drinkIcon != null)
            {
                highlightGlow = new GameObject("DrinkHighlightGlow");
                highlightGlow.transform.SetParent(drinkIcon.transform.parent, false);
                highlightGlow.transform.SetSiblingIndex(drinkIcon.transform.GetSiblingIndex());

                RectTransform glowRect = highlightGlow.AddComponent<RectTransform>();
                RectTransform iconRect = drinkIcon.GetComponent<RectTransform>();
                glowRect.anchorMin = iconRect.anchorMin;
                glowRect.anchorMax = iconRect.anchorMax;
                glowRect.pivot = iconRect.pivot;
                glowRect.anchoredPosition = iconRect.anchoredPosition;
                glowRect.sizeDelta = iconRect.sizeDelta + new Vector2(20, 20); // Slightly larger

                Image glowImage = highlightGlow.AddComponent<Image>();
                glowImage.color = new Color(highlightColor.r, highlightColor.g, highlightColor.b, 0.5f);

                // Create a simple glow sprite
                Texture2D tex = new Texture2D(4, 4);
                Color[] colors = new Color[16];
                for (int i = 0; i < colors.Length; i++) colors[i] = Color.white;
                tex.SetPixels(colors);
                tex.Apply();
                glowImage.sprite = Sprite.Create(tex, new Rect(0, 0, 4, 4), new Vector2(0.5f, 0.5f));
            }

            if (highlightGlow != null)
            {
                highlightGlow.SetActive(true);
            }

            if (debugMode) Debug.Log("[ConsumablesHUD] Drink highlighted");
        }

        /// <summary>
        /// Stop the drink highlight effect.
        /// </summary>
        public void StopDrinkHighlight()
        {
            isDrinkHighlighted = false;

            if (highlightGlow != null)
            {
                highlightGlow.SetActive(false);
            }

            // Reset icon scale
            if (drinkIcon != null)
            {
                drinkIcon.transform.localScale = Vector3.one;
            }

            if (debugMode) Debug.Log("[ConsumablesHUD] Drink highlight stopped");
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
            Debug.Log("[ConsumablesHUD] Creating new HUDCanvas for consumables panel");
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
