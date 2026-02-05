using UnityEngine;
using UnityEngine.UI;
using UnityEngine.EventSystems;
using TMPro;
using System.Collections.Generic;
using BeneathTheFloor.Managers;
using BeneathTheFloor.Winch;
using BeneathTheFloor.Energy;

namespace BeneathTheFloor.UI
{
    public class HUDController : MonoBehaviour
    {
        [Header("Auto Setup")]
        [SerializeField] private bool autoCreateUI = true;
        [SerializeField] private bool debugMode = false;

        [Header("Resource Display (DEPRECATED - Using PickupNotificationSystem)")]
        [SerializeField] private bool enableLegacyResourcePanel = false; // Disabled by default
        [SerializeField] private Transform resourceContainer;
        [SerializeField] private GameObject resourceItemPrefab;

        [Header("Tool Display")]
        [SerializeField] private Image toolIcon;
        [SerializeField] private TextMeshProUGUI toolNameText;
        [SerializeField] private Slider durabilitySlider;

        [Header("Depth Display")]
        [SerializeField] private bool enableDepthDisplay = false; // Disabled - using DepthHUD_YBased instead
        [SerializeField] private TextMeshProUGUI depthText;
        [SerializeField] private Slider depthSlider;
        [SerializeField] private float maxDepthDisplay = 100f;
        [SerializeField] private TextMeshProUGUI layerText;

        [Header("Cable Display")]
        [SerializeField] private TextMeshProUGUI cableLengthText;
        [SerializeField] private GameObject cableLengthPanel;

        [Header("Dig Progress")]
        [SerializeField] private Slider digProgressSlider;
        [SerializeField] private CanvasGroup digProgressGroup;

        [Header("Interaction Prompt")]
        [SerializeField] private GameObject interactionPromptPanel;
        [SerializeField] private TextMeshProUGUI interactionPromptText;

        [Header("Crosshair")]
        [SerializeField] private Image crosshair;
        [SerializeField] private Color normalColor = Color.white;
        [SerializeField] private Color interactColor = Color.green;

        private Dictionary<ResourceType, TextMeshProUGUI> resourceTexts = new Dictionary<ResourceType, TextMeshProUGUI>();
        // Depth is now tracked via GameManager.UpdateDepth() and GameEvents.OnDepthReached
        // which DiggingSystem calls after each successful dig.
        private Canvas mainCanvas;
        private GameObject crosshairContainer;
        private TextMeshProUGUI resourceDisplayText; // Single text for all resources when auto-created

        public static HUDController Instance { get; private set; }

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

            // Auto-create UI if needed
            if (autoCreateUI)
            {
                EnsureUIExists();
            }
        }

        private void Start()
        {
            // Subscribe to events (only subscribe to resource events if legacy panel is enabled)
            if (enableLegacyResourcePanel)
            {
                GameEvents.OnResourceCollected += UpdateResourceDisplay;
            }
            GameEvents.OnToolEquipped += UpdateToolDisplay;
            GameEvents.OnDepthReached += UpdateDepthDisplayInt;
            GameEvents.OnTileDug += OnTileDug;

            // Max depth display defaults to 220m (final layer depth)
            // The new DiggingV2 system will update this via events

            // Initialize displays (only if legacy panel is enabled)
            if (enableLegacyResourcePanel)
            {
                InitializeResourceDisplay();
                RefreshAllResourceDisplay();
                if (debugMode) Debug.Log($"[HUDController] resourceDisplayText is {(resourceDisplayText == null ? "NULL" : "ASSIGNED")}");
            }

            HideInteractionPrompt();
            HideDigProgress();

            // Initial update
            UpdateDepthDisplayFloat(0f); // depthManager removed

            if (debugMode) Debug.Log($"[HUDController] Started. Legacy resource panel: {enableLegacyResourcePanel}");
        }

        private void EnsureUIExists()
        {
            if (debugMode) Debug.Log("[HUDController] Checking if UI needs to be created...");

            // Ensure EventSystem
            if (FindObjectOfType<EventSystem>() == null)
            {
                GameObject eventSystem = new GameObject("EventSystem");
                eventSystem.AddComponent<EventSystem>();
                eventSystem.AddComponent<StandaloneInputModule>();
                if (debugMode) Debug.Log("[HUDController] Created EventSystem");
            }

            // Find or create Canvas - AVOID LoadingOverlay canvas!
            mainCanvas = FindHUDCanvas();
            if (mainCanvas == null)
            {
                GameObject canvasObj = new GameObject("HUDCanvas");
                mainCanvas = canvasObj.AddComponent<Canvas>();
                mainCanvas.renderMode = RenderMode.ScreenSpaceOverlay;
                mainCanvas.sortingOrder = 100;

                CanvasScaler scaler = canvasObj.AddComponent<CanvasScaler>();
                scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
                scaler.referenceResolution = new Vector2(1920, 1080);
                scaler.matchWidthOrHeight = 0.5f;

                canvasObj.AddComponent<GraphicRaycaster>();
                if (debugMode) Debug.Log("[HUDController] Created Canvas");
            }
            else
            {
                if (debugMode) Debug.Log($"[HUDController] Using existing canvas: {mainCanvas.name}");
            }

            // Create depth display if needed
            if (enableDepthDisplay && depthText == null)
            {
                CreateDepthDisplay();
            }

            // Create cable length display if needed
            if (cableLengthText == null)
            {
                CreateCableLengthDisplay();
            }

            // Create resource display if needed (only if legacy panel is enabled)
            if (enableLegacyResourcePanel && resourceContainer == null)
            {
                CreateResourceDisplay();
            }

            // Create pickup notification system
            CreatePickupNotificationSystem();

            // Tool display DISABLED - not part of final HUD design
            // if (toolNameText == null)
            // {
            //     CreateToolDisplay();
            // }

            // Create crosshair if needed
            if (crosshair == null)
            {
                CreateCrosshair();
            }

            // Create interaction prompt if needed
            if (interactionPromptPanel == null)
            {
                CreateInteractionPrompt();
            }

            // Create ConsumablesHUD if it doesn't exist (bottom-right panel for drinks/lamps)
            CreateConsumablesHUD();

            // Create EstimatedValueHUD if it doesn't exist (bag value below currency)
            CreateEstimatedValueHUD();

            if (debugMode) Debug.Log("[HUDController] UI setup complete!");
        }

        private void CreateDepthDisplay()
        {
            GameObject panel = CreatePanel("DepthPanel", mainCanvas.transform,
                new Vector2(0, 0), new Vector2(0, 0), new Vector2(0, 0),
                new Vector2(20, 20), new Vector2(200, 60));

            Image bg = panel.AddComponent<Image>();
            bg.color = new Color(0, 0, 0, 0.6f);

            GameObject textObj = new GameObject("DepthText");
            textObj.transform.SetParent(panel.transform, false);
            depthText = textObj.AddComponent<TextMeshProUGUI>();
            depthText.text = "Depth: 0.0 m";
            depthText.fontSize = 24;
            depthText.color = Color.white;
            depthText.alignment = TextAlignmentOptions.Center;
            depthText.fontStyle = FontStyles.Bold;

            RectTransform textRect = textObj.GetComponent<RectTransform>();
            textRect.anchorMin = Vector2.zero;
            textRect.anchorMax = Vector2.one;
            textRect.offsetMin = new Vector2(10, 5);
            textRect.offsetMax = new Vector2(-10, -5);

            if (debugMode) Debug.Log("[HUDController] Created depth display");
        }

        private void CreateCableLengthDisplay()
        {
            // Modern styled cable panel - bottom-left, clean look
            cableLengthPanel = CreatePanel("CableLengthPanel", mainCanvas.transform,
                new Vector2(0, 0), new Vector2(0, 0), new Vector2(0, 0),
                new Vector2(20, 60), new Vector2(180, 45));

            Image bg = cableLengthPanel.AddComponent<Image>();
            bg.color = new Color(0.1f, 0.12f, 0.15f, 0.85f); // Dark subtle panel

            // Add subtle border effect
            Outline outline = cableLengthPanel.AddComponent<Outline>();
            outline.effectColor = new Color(0.3f, 0.5f, 0.6f, 0.4f); // Teal tint border
            outline.effectDistance = new Vector2(1, 1);

            // Icon/label area
            GameObject iconObj = new GameObject("CableIcon");
            iconObj.transform.SetParent(cableLengthPanel.transform, false);
            TextMeshProUGUI iconText = iconObj.AddComponent<TextMeshProUGUI>();
            iconText.text = "~"; // Cable symbol
            iconText.fontSize = 18;
            iconText.color = new Color(0.5f, 0.7f, 0.8f, 0.9f); // Teal accent
            iconText.alignment = TextAlignmentOptions.Left;
            RectTransform iconRect = iconObj.GetComponent<RectTransform>();
            iconRect.anchorMin = new Vector2(0, 0);
            iconRect.anchorMax = new Vector2(0.2f, 1);
            iconRect.offsetMin = new Vector2(10, 8);
            iconRect.offsetMax = new Vector2(0, -8);

            GameObject textObj = new GameObject("CableLengthText");
            textObj.transform.SetParent(cableLengthPanel.transform, false);
            cableLengthText = textObj.AddComponent<TextMeshProUGUI>();
            cableLengthText.text = "-- / --";
            cableLengthText.fontSize = 16;
            cableLengthText.color = new Color(0.85f, 0.9f, 0.95f, 0.95f); // Clean white
            cableLengthText.alignment = TextAlignmentOptions.Left;
            cableLengthText.fontStyle = FontStyles.Normal;

            RectTransform textRect = textObj.GetComponent<RectTransform>();
            textRect.anchorMin = new Vector2(0.2f, 0);
            textRect.anchorMax = Vector2.one;
            textRect.offsetMin = new Vector2(5, 8);
            textRect.offsetMax = new Vector2(-10, -8);

            // Start hidden
            cableLengthPanel.SetActive(false);

            if (debugMode) Debug.Log("[HUDController] Created cable length display");
        }

        private void CreateResourceDisplay()
        {
            GameObject panel = CreatePanel("ResourcePanel", mainCanvas.transform,
                new Vector2(0, 1), new Vector2(0, 1), new Vector2(0, 1),
                new Vector2(20, -20), new Vector2(220, 120));

            Image bg = panel.AddComponent<Image>();
            bg.color = new Color(0, 0, 0, 0.6f);

            // Title
            GameObject titleObj = new GameObject("Title");
            titleObj.transform.SetParent(panel.transform, false);
            TextMeshProUGUI titleText = titleObj.AddComponent<TextMeshProUGUI>();
            titleText.text = "Resources";
            titleText.fontSize = 18;
            titleText.color = new Color(1f, 0.9f, 0.5f);
            titleText.alignment = TextAlignmentOptions.Center;
            titleText.fontStyle = FontStyles.Bold;

            RectTransform titleRect = titleObj.GetComponent<RectTransform>();
            titleRect.anchorMin = new Vector2(0, 1);
            titleRect.anchorMax = new Vector2(1, 1);
            titleRect.pivot = new Vector2(0.5f, 1);
            titleRect.anchoredPosition = new Vector2(0, -5);
            titleRect.sizeDelta = new Vector2(0, 25);

            // Resource container
            GameObject containerObj = new GameObject("ResourceContainer");
            containerObj.transform.SetParent(panel.transform, false);
            resourceContainer = containerObj.transform;

            RectTransform containerRect = containerObj.AddComponent<RectTransform>();
            containerRect.anchorMin = Vector2.zero;
            containerRect.anchorMax = Vector2.one;
            containerRect.offsetMin = new Vector2(10, 10);
            containerRect.offsetMax = new Vector2(-10, -30);

            // Add simple resource text
            GameObject resourceTextObj = new GameObject("ResourceText");
            resourceTextObj.transform.SetParent(containerObj.transform, false);
            resourceDisplayText = resourceTextObj.AddComponent<TextMeshProUGUI>();
            resourceDisplayText.text = "Dirt: 0\nClay: 0\nCoal: 0\nIron: 0";
            resourceDisplayText.fontSize = 16;
            resourceDisplayText.color = Color.white;
            resourceDisplayText.alignment = TextAlignmentOptions.TopLeft;

            RectTransform resRect = resourceTextObj.GetComponent<RectTransform>();
            resRect.anchorMin = Vector2.zero;
            resRect.anchorMax = Vector2.one;
            resRect.offsetMin = Vector2.zero;
            resRect.offsetMax = Vector2.zero;

            if (debugMode) Debug.Log("[HUDController] Created resource display (stored reference for updates)");
        }

        private void CreateToolDisplay()
        {
            GameObject panel = CreatePanel("ToolPanel", mainCanvas.transform,
                new Vector2(1, 0), new Vector2(1, 0), new Vector2(1, 0),
                new Vector2(-20, 20), new Vector2(200, 50));

            Image bg = panel.AddComponent<Image>();
            bg.color = new Color(0, 0, 0, 0.6f);

            GameObject textObj = new GameObject("ToolText");
            textObj.transform.SetParent(panel.transform, false);
            toolNameText = textObj.AddComponent<TextMeshProUGUI>();
            toolNameText.text = "Tool: Wooden Shovel";
            toolNameText.fontSize = 18;
            toolNameText.color = Color.white;
            toolNameText.alignment = TextAlignmentOptions.Center;

            RectTransform textRect = textObj.GetComponent<RectTransform>();
            textRect.anchorMin = Vector2.zero;
            textRect.anchorMax = Vector2.one;
            textRect.offsetMin = new Vector2(10, 5);
            textRect.offsetMax = new Vector2(-10, -5);

            if (debugMode) Debug.Log("[HUDController] Created tool display");
        }

        private void CreateCrosshair()
        {
            crosshairContainer = new GameObject("Crosshair");
            crosshairContainer.transform.SetParent(mainCanvas.transform, false);

            RectTransform crossRect = crosshairContainer.AddComponent<RectTransform>();
            crossRect.anchorMin = new Vector2(0.5f, 0.5f);
            crossRect.anchorMax = new Vector2(0.5f, 0.5f);
            crossRect.pivot = new Vector2(0.5f, 0.5f);
            crossRect.anchoredPosition = Vector2.zero;
            crossRect.sizeDelta = new Vector2(20, 20);

            // Horizontal line
            GameObject hLine = new GameObject("HLine");
            hLine.transform.SetParent(crosshairContainer.transform, false);
            Image hImg = hLine.AddComponent<Image>();
            hImg.color = Color.white;
            RectTransform hRect = hLine.GetComponent<RectTransform>();
            hRect.anchorMin = new Vector2(0.5f, 0.5f);
            hRect.anchorMax = new Vector2(0.5f, 0.5f);
            hRect.pivot = new Vector2(0.5f, 0.5f);
            hRect.sizeDelta = new Vector2(16, 2);

            // Vertical line
            GameObject vLine = new GameObject("VLine");
            vLine.transform.SetParent(crosshairContainer.transform, false);
            Image vImg = vLine.AddComponent<Image>();
            vImg.color = Color.white;
            RectTransform vRect = vLine.GetComponent<RectTransform>();
            vRect.anchorMin = new Vector2(0.5f, 0.5f);
            vRect.anchorMax = new Vector2(0.5f, 0.5f);
            vRect.pivot = new Vector2(0.5f, 0.5f);
            vRect.sizeDelta = new Vector2(2, 16);

            // Add outline for visibility
            Outline hOutline = hLine.AddComponent<Outline>();
            hOutline.effectColor = Color.black;
            hOutline.effectDistance = new Vector2(1, 1);

            Outline vOutline = vLine.AddComponent<Outline>();
            vOutline.effectColor = Color.black;
            vOutline.effectDistance = new Vector2(1, 1);

            // Use first image as the crosshair reference for color changes
            crosshair = hImg;

            if (debugMode) Debug.Log("[HUDController] Created crosshair");
        }

        private void CreateInteractionPrompt()
        {
            // Position just below center crosshair
            interactionPromptPanel = CreatePanel("InteractionPrompt", mainCanvas.transform,
                new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f),
                new Vector2(0, -50), new Vector2(350, 50));

            Image bg = interactionPromptPanel.AddComponent<Image>();
            bg.color = new Color(0, 0, 0, 0.75f);

            GameObject textObj = new GameObject("PromptText");
            textObj.transform.SetParent(interactionPromptPanel.transform, false);
            interactionPromptText = textObj.AddComponent<TextMeshProUGUI>();
            interactionPromptText.text = "Press E to interact";
            interactionPromptText.fontSize = 22;
            interactionPromptText.color = Color.white;
            interactionPromptText.alignment = TextAlignmentOptions.Center;
            interactionPromptText.fontStyle = FontStyles.Bold;

            RectTransform textRect = textObj.GetComponent<RectTransform>();
            textRect.anchorMin = Vector2.zero;
            textRect.anchorMax = Vector2.one;
            textRect.offsetMin = new Vector2(10, 5);
            textRect.offsetMax = new Vector2(-10, -5);

            interactionPromptPanel.SetActive(false);

            if (debugMode) Debug.Log("[HUDController] Created interaction prompt");
        }

        private void CreatePickupNotificationSystem()
        {
            if (PickupNotificationSystem.Instance != null)
            {
                if (debugMode) Debug.Log("[HUDController] PickupNotificationSystem already exists");
                return;
            }

            GameObject notifObj = new GameObject("PickupNotificationSystem");
            notifObj.transform.SetParent(mainCanvas.transform, false);
            notifObj.AddComponent<PickupNotificationSystem>();

            if (debugMode) Debug.Log("[HUDController] Created PickupNotificationSystem");
        }

        private void CreateConsumablesHUD()
        {
            if (ConsumablesHUD.Instance != null)
            {
                if (debugMode) Debug.Log("[HUDController] ConsumablesHUD already exists");
                return;
            }

            GameObject hudObj = new GameObject("ConsumablesHUD");
            hudObj.transform.SetParent(mainCanvas.transform, false);
            hudObj.AddComponent<ConsumablesHUD>();

            if (debugMode) Debug.Log("[HUDController] Created ConsumablesHUD");
        }

        private void CreateEstimatedValueHUD()
        {
            if (EstimatedValueHUD.Instance != null)
            {
                if (debugMode) Debug.Log("[HUDController] EstimatedValueHUD already exists");
                return;
            }

            GameObject hudObj = new GameObject("EstimatedValueHUD");
            hudObj.transform.SetParent(mainCanvas.transform, false);
            hudObj.AddComponent<EstimatedValueHUD>();

            if (debugMode) Debug.Log("[HUDController] Created EstimatedValueHUD");
        }

        private GameObject CreatePanel(string name, Transform parent, Vector2 anchorMin, Vector2 anchorMax, Vector2 pivot, Vector2 anchoredPos, Vector2 size)
        {
            GameObject panel = new GameObject(name);
            panel.transform.SetParent(parent, false);
            RectTransform rect = panel.AddComponent<RectTransform>();
            rect.anchorMin = anchorMin;
            rect.anchorMax = anchorMax;
            rect.pivot = pivot;
            rect.anchoredPosition = anchoredPos;
            rect.sizeDelta = size;
            return panel;
        }

        private void OnDestroy()
        {
            if (enableLegacyResourcePanel)
            {
                GameEvents.OnResourceCollected -= UpdateResourceDisplay;
            }
            GameEvents.OnToolEquipped -= UpdateToolDisplay;
            GameEvents.OnDepthReached -= UpdateDepthDisplayInt;
            GameEvents.OnTileDug -= OnTileDug;
        }

        private void OnTileDug(Vector3 position, int layerIndex)
        {
            // Update layer display when a tile is dug
            UpdateLayerDisplay(layerIndex);
        }

        private void Update()
        {
            UpdateDigProgressDisplay();
            UpdateCableLengthDisplay();
        }

        private void UpdateCableLengthDisplay()
        {
            var winchAnchor = WinchAnchor.Instance;

            if (winchAnchor != null && winchAnchor.IsAttached)
            {
                // Show panel
                if (cableLengthPanel != null && !cableLengthPanel.activeSelf)
                {
                    cableLengthPanel.SetActive(true);
                }

                // Update text - clean format without "Cable:" prefix
                if (cableLengthText != null)
                {
                    float cableLength = winchAnchor.GetCurrentCableLength();
                    float maxLength = winchAnchor.MaxCableLength;
                    cableLengthText.text = $"{cableLength:F1}m / {maxLength:F0}m";
                }
            }
            else
            {
                // Hide panel when not attached
                if (cableLengthPanel != null && cableLengthPanel.activeSelf)
                {
                    cableLengthPanel.SetActive(false);
                }
            }
        }

        private void InitializeResourceDisplay()
        {
            // Create display items for each resource type
            foreach (ResourceType type in System.Enum.GetValues(typeof(ResourceType)))
            {
                if (resourceContainer != null && resourceItemPrefab != null)
                {
                    GameObject item = Instantiate(resourceItemPrefab, resourceContainer);
                    item.name = $"Resource_{type}";

                    TextMeshProUGUI text = item.GetComponentInChildren<TextMeshProUGUI>();
                    if (text != null)
                    {
                        text.text = $"{type}: 0";
                        resourceTexts[type] = text;
                    }
                }
            }
        }

        private void UpdateResourceDisplay(ResourceType type, int amount)
        {
            if (debugMode) Debug.Log($"[HUDController] UpdateResourceDisplay called: {type} +{amount}");

            // If we have individual text fields, update them
            if (resourceTexts.TryGetValue(type, out TextMeshProUGUI text))
            {
                int total = Inventory.InventorySystem.Instance?.GetResourceCount(type) ?? 0;
                text.text = $"{type}: {total}";
                if (debugMode) Debug.Log($"[HUDController] Updated {type} text to: {total}");
            }
            // Otherwise, update the combined resource display text
            else if (resourceDisplayText != null)
            {
                RefreshAllResourceDisplay();
            }
            else
            {
                Debug.LogWarning("[HUDController] No resource text field available to update!");
            }
        }

        /// <summary>
        /// Refresh the entire resource display text with current inventory values
        /// </summary>
        private void RefreshAllResourceDisplay()
        {
            if (resourceDisplayText == null) return;

            var inventory = Inventory.InventorySystem.Instance;
            if (inventory == null)
            {
                Debug.LogWarning("[HUDController] InventorySystem.Instance is null!");
                return;
            }

            int dirt = inventory.GetResourceCount(ResourceType.Dirt);
            int clay = inventory.GetResourceCount(ResourceType.Clay);
            int coal = inventory.GetResourceCount(ResourceType.Coal);
            int iron = inventory.GetResourceCount(ResourceType.IronOre);

            resourceDisplayText.text = $"Dirt: {dirt}\nClay: {clay}\nCoal: {coal}\nIron: {iron}";

            if (debugMode) Debug.Log($"[HUDController] Refreshed resource display - Dirt: {dirt}, Clay: {clay}, Coal: {coal}, Iron: {iron}");
        }

        private void UpdateToolDisplay(ToolData tool)
        {
            if (tool == null) return;

            if (toolNameText != null)
            {
                toolNameText.text = tool.toolName;
            }

            if (toolIcon != null && tool.icon != null)
            {
                toolIcon.sprite = tool.icon;
                toolIcon.gameObject.SetActive(true);
            }

            if (durabilitySlider != null)
            {
                durabilitySlider.maxValue = tool.maxDurability;
                durabilitySlider.value = tool.durability;
            }
        }

        private void UpdateDepthDisplayInt(int depth)
        {
            UpdateDepthDisplayFloat((float)depth);
        }

        private void UpdateDepthDisplayFloat(float depth)
        {
            if (depthText != null)
            {
                depthText.text = $"{depth:F1}m";
            }

            if (depthSlider != null)
            {
                depthSlider.maxValue = maxDepthDisplay;
                depthSlider.value = depth;
            }
        }

        private void UpdateLayerDisplay(int layer)
        {
            if (layerText != null)
            {
                // Layer names based on DiggingV2 depth configuration
                string[] layerNames = { "Surface", "Soft Depth", "Hard Soil", "Heat & Pressure",
                                        "Crystal Roots", "Crystal Chamber", "Final Depth" };
                string layerName = (layer >= 0 && layer < layerNames.Length)
                    ? layerNames[layer]
                    : $"Layer {layer}";
                layerText.text = layerName;
            }
        }

        private void UpdateDigProgressDisplay()
        {
            // DIGGING SYSTEM REMOVED - dig progress display disabled
            if (digProgressSlider != null && digProgressGroup != null)
            {
                // bool isDigging = Digging.DiggingSystem.Instance?.IsDigging ?? false;
                // if (isDigging)
                // {
                //     ShowDigProgress();
                //     digProgressSlider.value = Digging.DiggingSystem.Instance.DigProgress;
                // }
                // else
                // {
                //     HideDigProgress();
                // }
                HideDigProgress();
            }
        }

        public void ShowInteractionPrompt(string text)
        {
            if (interactionPromptPanel != null)
            {
                interactionPromptPanel.SetActive(true);

                if (interactionPromptText != null)
                {
                    interactionPromptText.text = text;
                }
            }

            SetCrosshairColor(interactColor);
        }

        public void HideInteractionPrompt()
        {
            if (interactionPromptPanel != null)
            {
                interactionPromptPanel.SetActive(false);
            }

            SetCrosshairColor(normalColor);
        }

        private void ShowDigProgress()
        {
            if (digProgressGroup != null)
            {
                digProgressGroup.alpha = 1f;
            }
        }

        private void HideDigProgress()
        {
            if (digProgressGroup != null)
            {
                digProgressGroup.alpha = 0f;
            }

            if (digProgressSlider != null)
            {
                digProgressSlider.value = 0;
            }
        }

        private void SetCrosshairColor(Color color)
        {
            // Update all crosshair images if using the container
            if (crosshairContainer != null)
            {
                Image[] images = crosshairContainer.GetComponentsInChildren<Image>();
                foreach (Image img in images)
                {
                    img.color = color;
                }
            }
            else if (crosshair != null)
            {
                crosshair.color = color;
            }
        }

        public void SetCrosshairVisible(bool visible)
        {
            if (crosshairContainer != null)
            {
                crosshairContainer.SetActive(visible);
            }
            else if (crosshair != null)
            {
                crosshair.gameObject.SetActive(visible);
            }
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
                    if (canvas != null)
                    {
                        return canvas;
                    }
                }
            }

            // Fall back to finding any canvas that's NOT the loading overlay
            Canvas[] allCanvases = FindObjectsOfType<Canvas>();
            foreach (var canvas in allCanvases)
            {
                // Skip loading overlay and other non-HUD canvases
                if (canvas.name == "LoadingOverlay" ||
                    canvas.name.Contains("Loading") ||
                    canvas.sortingOrder >= 9000) // Loading overlay uses sortingOrder 9999
                {
                    continue;
                }

                // Prefer screen space overlay canvases
                if (canvas.renderMode == RenderMode.ScreenSpaceOverlay)
                {
                    return canvas;
                }
            }

            // No suitable canvas found, will need to create one
            return null;
        }
    }
}
