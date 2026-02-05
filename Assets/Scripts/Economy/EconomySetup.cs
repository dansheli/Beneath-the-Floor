using UnityEngine;
using UnityEngine.UI;

namespace BeneathTheFloor.Economy
{
    /// <summary>
    /// Standalone setup script for the economy system.
    /// Add this to any GameObject in BasementScene to ensure economy components are created.
    /// </summary>
    public class EconomySetup : MonoBehaviour
    {
        [Header("Setup Options")]
        [SerializeField] private bool setupOnStart = true;
        [SerializeField] private bool createCurrencyManager = true;
        [SerializeField] private bool createCurrencyHUD = true;
        [SerializeField] private bool createBagValueHUD = true;
        [SerializeField] private bool createTradeTerminal = true;
        [SerializeField] private bool createTradeTerminalUI = true;

        [Header("Debug")]
        #pragma warning disable CS0414 // Reserved for debug logging
        [SerializeField] private bool enableDebugLogs = false;
        #pragma warning restore CS0414

        [Header("Trade Terminal Position")]
        [SerializeField] private Vector3 tradeTerminalPosition = new Vector3(3f, -2.5f, -2f);
        [SerializeField] private float tradeTerminalRotation = -90f;

        private Canvas uiCanvas;

        private void Start()
        {
            if (setupOnStart)
            {
                SetupEconomy();
            }
        }

        [ContextMenu("Setup Economy")]
        public void SetupEconomy()
        {
            // Find or create UI canvas
            FindOrCreateCanvas();

            // Create managers
            if (createCurrencyManager)
            {
                SetupCurrencyManager();
            }

            // Create Trade Terminal (the physical machine)
            if (createTradeTerminal)
            {
                SetupTradeTerminal();
            }

            // Create UI components
            if (createTradeTerminalUI)
            {
                SetupTradeTerminalUI();
            }

            if (createCurrencyHUD)
            {
                SetupCurrencyHUD();
            }

            if (createBagValueHUD)
            {
                SetupBagValueHUD();
            }
        }

        private void FindOrCreateCanvas()
        {
            // IMPORTANT: Use the same MachineUICanvas that other machine UIs use
            // This ensures consistent input handling and raycasting
            GameObject canvasObj = GameObject.Find("MachineUICanvas");
            if (canvasObj != null)
            {
                uiCanvas = canvasObj.GetComponent<Canvas>();

                // Ensure it has GraphicRaycaster
                if (uiCanvas.GetComponent<GraphicRaycaster>() == null)
                {
                    uiCanvas.gameObject.AddComponent<GraphicRaycaster>();
                }
                return;
            }

            // Fallback: Create MachineUICanvas if it doesn't exist

            canvasObj = new GameObject("MachineUICanvas");
            uiCanvas = canvasObj.AddComponent<Canvas>();
            uiCanvas.renderMode = RenderMode.ScreenSpaceOverlay;
            uiCanvas.sortingOrder = 100; // Above HUD, same as other machine UIs

            var scaler = canvasObj.AddComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920, 1080);

            canvasObj.AddComponent<GraphicRaycaster>();
        }

        private void SetupCurrencyManager()
        {
            if (CurrencyManager.Instance != null)
            {
                return;
            }

            var managerObj = new GameObject("CurrencyManager");
            managerObj.AddComponent<CurrencyManager>();
        }

        private void SetupTradeTerminal()
        {
            // Check if one already exists
            var existingTerminal = FindObjectOfType<TradeTerminal>();
            if (existingTerminal != null)
            {
                return;
            }

            // Create Trade Terminal GameObject
            var tradeTerminal = new GameObject("TradeTerminal");
            tradeTerminal.transform.position = tradeTerminalPosition;
            tradeTerminal.transform.rotation = Quaternion.Euler(0f, tradeTerminalRotation, 0f);

            // Add visual mesh (a placeholder terminal look)
            GameObject visual = GameObject.CreatePrimitive(PrimitiveType.Cube);
            visual.name = "TerminalBody";
            visual.transform.SetParent(tradeTerminal.transform);
            visual.transform.localPosition = new Vector3(0, 0.6f, 0);
            visual.transform.localScale = new Vector3(0.8f, 1.2f, 0.4f);

            // Set material color (green-ish for trade terminal)
            var renderer = visual.GetComponent<Renderer>();
            if (renderer != null)
            {
                renderer.material = new Material(Shader.Find("Standard"));
                renderer.material.color = new Color(0.2f, 0.35f, 0.25f);
            }

            // Remove collider from visual (we'll use a trigger on parent)
            var visualCollider = visual.GetComponent<Collider>();
            if (visualCollider != null) DestroyImmediate(visualCollider);

            // Add screen (a simple plane)
            GameObject screen = GameObject.CreatePrimitive(PrimitiveType.Quad);
            screen.name = "Screen";
            screen.transform.SetParent(tradeTerminal.transform);
            screen.transform.localPosition = new Vector3(0, 0.8f, 0.21f);
            screen.transform.localScale = new Vector3(0.5f, 0.4f, 1f);

            var screenRenderer = screen.GetComponent<Renderer>();
            if (screenRenderer != null)
            {
                screenRenderer.material = new Material(Shader.Find("Standard"));
                screenRenderer.material.color = new Color(0.1f, 0.3f, 0.15f);
                screenRenderer.material.SetFloat("_Metallic", 0f);
                screenRenderer.material.SetFloat("_Glossiness", 0.8f);
                // Make it emissive
                screenRenderer.material.EnableKeyword("_EMISSION");
                screenRenderer.material.SetColor("_EmissionColor", new Color(0.1f, 0.4f, 0.2f));
            }

            var screenCollider = screen.GetComponent<Collider>();
            if (screenCollider != null) DestroyImmediate(screenCollider);

            // Add collider for interaction
            BoxCollider collider = tradeTerminal.AddComponent<BoxCollider>();
            collider.center = new Vector3(0, 0.6f, 0);
            collider.size = new Vector3(1f, 1.4f, 0.6f);

            // Add TradeTerminal component
            tradeTerminal.AddComponent<TradeTerminal>();
        }

        private void SetupTradeTerminalUI()
        {
            // Check if TradeTerminalUI exists but under the WRONG canvas
            var existingUI = FindObjectOfType<TradeTerminalUI>();
            if (existingUI != null)
            {
                // Check if it's under MachineUICanvas
                Canvas parentCanvas = existingUI.GetComponentInParent<Canvas>();
                if (parentCanvas != null && parentCanvas.gameObject.name == "MachineUICanvas")
                {
                    return;
                }
                else
                {
                    // It's under the wrong canvas, destroy it and recreate
                    DestroyImmediate(existingUI.gameObject);
                }
            }

            // Create the main UI container
            var uiObj = new GameObject("TradeTerminalUI");
            uiObj.transform.SetParent(uiCanvas.transform, false);

            // Create the Trade Terminal Panel with full UI hierarchy
            GameObject terminalPanel = CreateTradeTerminalPanel(uiObj.transform);

            // Add the TradeTerminalUI component and wire up references
            var terminalUI = uiObj.AddComponent<TradeTerminalUI>();
        }

        private GameObject CreateTradeTerminalPanel(Transform parent)
        {
            // Main panel
            GameObject panel = new GameObject("TradeTerminalPanel");
            panel.transform.SetParent(parent, false);

            RectTransform panelRect = panel.AddComponent<RectTransform>();
            panelRect.anchorMin = new Vector2(0.5f, 0.5f);
            panelRect.anchorMax = new Vector2(0.5f, 0.5f);
            panelRect.pivot = new Vector2(0.5f, 0.5f);
            panelRect.anchoredPosition = Vector2.zero;
            panelRect.sizeDelta = new Vector2(700, 500);

            Image panelBg = panel.AddComponent<Image>();
            panelBg.color = new Color(0.12f, 0.14f, 0.12f, 0.95f);
            panelBg.raycastTarget = true; // Ensure panel receives clicks

            // Debug component can be added manually in inspector if needed:
            // panel.AddComponent<TradeTerminalClickDebug>();

            // Header
            CreateHeader(panel.transform);

            // Content area
            GameObject content = new GameObject("Content");
            content.transform.SetParent(panel.transform, false);

            RectTransform contentRect = content.AddComponent<RectTransform>();
            contentRect.anchorMin = new Vector2(0, 0);
            contentRect.anchorMax = new Vector2(1, 1);
            contentRect.offsetMin = new Vector2(10, 50);
            contentRect.offsetMax = new Vector2(-10, -50);

            HorizontalLayoutGroup hlg = content.AddComponent<HorizontalLayoutGroup>();
            hlg.padding = new RectOffset(5, 5, 5, 5);
            hlg.spacing = 10;
            hlg.childControlWidth = true;
            hlg.childControlHeight = true;
            hlg.childForceExpandWidth = false;
            hlg.childForceExpandHeight = true;

            // Left Panel (Item List)
            CreateItemListPanel(content.transform);

            // Right Panel (Details)
            CreateDetailsPanel(content.transform);

            // Footer
            CreateFooter(panel.transform);

            panel.SetActive(false); // Start hidden
            return panel;
        }

        private void CreateHeader(Transform parent)
        {
            GameObject header = new GameObject("Header");
            header.transform.SetParent(parent, false);

            RectTransform rect = header.AddComponent<RectTransform>();
            rect.anchorMin = new Vector2(0, 1);
            rect.anchorMax = new Vector2(1, 1);
            rect.pivot = new Vector2(0.5f, 1);
            rect.anchoredPosition = Vector2.zero;
            rect.sizeDelta = new Vector2(0, 50);

            Image bg = header.AddComponent<Image>();
            bg.color = new Color(0.15f, 0.2f, 0.15f, 1f);

            // Title
            GameObject titleObj = new GameObject("Title");
            titleObj.transform.SetParent(header.transform, false);

            var titleText = titleObj.AddComponent<TMPro.TextMeshProUGUI>();
            titleText.text = "Trade Terminal";
            titleText.fontSize = 24;
            titleText.color = new Color(0.6f, 1f, 0.6f);
            titleText.alignment = TMPro.TextAlignmentOptions.Center;
            titleText.fontStyle = TMPro.FontStyles.Bold;

            RectTransform titleRect = titleObj.GetComponent<RectTransform>();
            titleRect.anchorMin = Vector2.zero;
            titleRect.anchorMax = Vector2.one;
            titleRect.offsetMin = new Vector2(60, 5);
            titleRect.offsetMax = new Vector2(-60, -5);

            // Close Button
            GameObject closeObj = new GameObject("CloseButton");
            closeObj.transform.SetParent(header.transform, false);

            RectTransform closeRect = closeObj.AddComponent<RectTransform>();
            closeRect.anchorMin = new Vector2(1, 0.5f);
            closeRect.anchorMax = new Vector2(1, 0.5f);
            closeRect.pivot = new Vector2(1, 0.5f);
            closeRect.anchoredPosition = new Vector2(-10, 0);
            closeRect.sizeDelta = new Vector2(40, 40);

            Image closeBg = closeObj.AddComponent<Image>();
            closeBg.color = new Color(0.6f, 0.2f, 0.2f, 1f);
            closeBg.raycastTarget = true;

            Button closeBtn = closeObj.AddComponent<Button>();
            closeBtn.targetGraphic = closeBg;

            GameObject closeTextObj = new GameObject("Text");
            closeTextObj.transform.SetParent(closeObj.transform, false);
            var closeText = closeTextObj.AddComponent<TMPro.TextMeshProUGUI>();
            closeText.text = "X";
            closeText.fontSize = 24;
            closeText.color = Color.white;
            closeText.alignment = TMPro.TextAlignmentOptions.Center;
            closeText.raycastTarget = false; // Don't block button clicks

            RectTransform closeTextRect = closeTextObj.GetComponent<RectTransform>();
            closeTextRect.anchorMin = Vector2.zero;
            closeTextRect.anchorMax = Vector2.one;
            closeTextRect.offsetMin = Vector2.zero;
            closeTextRect.offsetMax = Vector2.zero;
        }

        private void CreateItemListPanel(Transform parent)
        {
            GameObject listPanel = new GameObject("LeftPanel");
            listPanel.transform.SetParent(parent, false);

            LayoutElement layout = listPanel.AddComponent<LayoutElement>();
            layout.flexibleWidth = 1;
            layout.preferredWidth = 300;

            Image bg = listPanel.AddComponent<Image>();
            bg.color = new Color(0.1f, 0.12f, 0.1f, 0.9f);

            // ScrollView
            GameObject scrollView = new GameObject("ScrollView");
            scrollView.transform.SetParent(listPanel.transform, false);

            RectTransform scrollRect = scrollView.AddComponent<RectTransform>();
            scrollRect.anchorMin = Vector2.zero;
            scrollRect.anchorMax = Vector2.one;
            scrollRect.offsetMin = new Vector2(5, 5);
            scrollRect.offsetMax = new Vector2(-5, -5);

            ScrollRect scroll = scrollView.AddComponent<ScrollRect>();
            scroll.horizontal = false;
            scroll.vertical = true;

            // Viewport
            GameObject viewport = new GameObject("Viewport");
            viewport.transform.SetParent(scrollView.transform, false);

            RectTransform viewportRect = viewport.AddComponent<RectTransform>();
            viewportRect.anchorMin = Vector2.zero;
            viewportRect.anchorMax = Vector2.one;
            viewportRect.sizeDelta = Vector2.zero;
            viewportRect.offsetMin = Vector2.zero;
            viewportRect.offsetMax = Vector2.zero;

            Image viewportMask = viewport.AddComponent<Image>();
            viewportMask.color = Color.white;
            viewportMask.raycastTarget = false; // CRITICAL: Let clicks pass through to buttons
            Mask mask = viewport.AddComponent<Mask>();
            mask.showMaskGraphic = false;

            // Content (THIS IS itemListContent)
            GameObject content = new GameObject("Content");
            content.transform.SetParent(viewport.transform, false);

            RectTransform contentRect = content.AddComponent<RectTransform>();
            contentRect.anchorMin = new Vector2(0, 1);
            contentRect.anchorMax = new Vector2(1, 1);
            contentRect.pivot = new Vector2(0.5f, 1);
            contentRect.anchoredPosition = Vector2.zero;
            contentRect.sizeDelta = new Vector2(0, 0);

            VerticalLayoutGroup vlg = content.AddComponent<VerticalLayoutGroup>();
            vlg.padding = new RectOffset(5, 5, 5, 5);
            vlg.spacing = 4;
            vlg.childControlWidth = true;
            vlg.childControlHeight = false;
            vlg.childForceExpandWidth = true;
            vlg.childForceExpandHeight = false;

            ContentSizeFitter csf = content.AddComponent<ContentSizeFitter>();
            csf.verticalFit = ContentSizeFitter.FitMode.PreferredSize;

            scroll.viewport = viewportRect;
            scroll.content = contentRect;
        }

        private void CreateDetailsPanel(Transform parent)
        {
            GameObject detailsPanel = new GameObject("RightPanel");
            detailsPanel.transform.SetParent(parent, false);

            LayoutElement layout = detailsPanel.AddComponent<LayoutElement>();
            layout.flexibleWidth = 1;
            layout.preferredWidth = 350;

            Image bg = detailsPanel.AddComponent<Image>();
            bg.color = new Color(0.1f, 0.12f, 0.1f, 0.9f);

            // Compact VerticalLayoutGroup with reduced spacing
            VerticalLayoutGroup vlg = detailsPanel.AddComponent<VerticalLayoutGroup>();
            vlg.padding = new RectOffset(10, 10, 10, 10);
            vlg.spacing = 5;
            vlg.childAlignment = TextAnchor.UpperLeft;
            vlg.childControlWidth = true;
            vlg.childControlHeight = false;
            vlg.childForceExpandWidth = false;
            vlg.childForceExpandHeight = false;

            // Item Icon - small 40x40 cube
            GameObject iconObj = new GameObject("DetailIcon");
            iconObj.transform.SetParent(detailsPanel.transform, false);
            RectTransform iconRect = iconObj.AddComponent<RectTransform>();
            iconRect.sizeDelta = new Vector2(40, 40);
            iconRect.localScale = Vector3.one;
            Image iconImg = iconObj.AddComponent<Image>();
            iconImg.color = new Color(0.4f, 0.4f, 0.4f);
            iconImg.preserveAspect = true;
            LayoutElement iconLayout = iconObj.AddComponent<LayoutElement>();
            iconLayout.preferredWidth = 40;
            iconLayout.preferredHeight = 40;
            iconLayout.flexibleWidth = 0; // Don't stretch

            // Item Name
            GameObject nameObj = new GameObject("DetailName");
            nameObj.transform.SetParent(detailsPanel.transform, false);
            var nameText = nameObj.AddComponent<TMPro.TextMeshProUGUI>();
            nameText.text = "Select an item";
            nameText.fontSize = 18;
            nameText.color = Color.white;
            nameText.alignment = TMPro.TextAlignmentOptions.Left;
            nameText.fontStyle = TMPro.FontStyles.Bold;
            nameText.overflowMode = TMPro.TextOverflowModes.Ellipsis;
            LayoutElement nameLayout = nameObj.AddComponent<LayoutElement>();
            nameLayout.preferredHeight = 24;

            // Owned Amount
            GameObject ownedObj = new GameObject("DetailOwned");
            ownedObj.transform.SetParent(detailsPanel.transform, false);
            var ownedText = ownedObj.AddComponent<TMPro.TextMeshProUGUI>();
            ownedText.text = "";
            ownedText.fontSize = 18;
            ownedText.color = new Color(0.8f, 0.8f, 0.8f);
            ownedText.alignment = TMPro.TextAlignmentOptions.Left;
            ownedText.overflowMode = TMPro.TextOverflowModes.Ellipsis;
            LayoutElement ownedLayout = ownedObj.AddComponent<LayoutElement>();
            ownedLayout.preferredHeight = 22;

            // Price Per Unit
            GameObject priceObj = new GameObject("DetailPrice");
            priceObj.transform.SetParent(detailsPanel.transform, false);
            var priceText = priceObj.AddComponent<TMPro.TextMeshProUGUI>();
            priceText.text = "";
            priceText.fontSize = 18;
            priceText.color = new Color(0.9f, 0.9f, 0.5f);
            priceText.alignment = TMPro.TextAlignmentOptions.Left;
            priceText.overflowMode = TMPro.TextOverflowModes.Ellipsis;
            LayoutElement priceLayout = priceObj.AddComponent<LayoutElement>();
            priceLayout.preferredHeight = 22;

            // Total Value Text
            GameObject totalObj = new GameObject("TotalValueText");
            totalObj.transform.SetParent(detailsPanel.transform, false);
            var totalText = totalObj.AddComponent<TMPro.TextMeshProUGUI>();
            totalText.text = "Total: 0";
            totalText.fontSize = 18;
            totalText.color = new Color(1f, 0.9f, 0.4f);
            totalText.alignment = TMPro.TextAlignmentOptions.Left;
            totalText.fontStyle = TMPro.FontStyles.Bold;
            totalText.overflowMode = TMPro.TextOverflowModes.Ellipsis;
            LayoutElement totalLayout = totalObj.AddComponent<LayoutElement>();
            totalLayout.preferredHeight = 24;

            // NOTE: Action buttons (-, +, Sell, Drop, Destroy) are created by TradeTerminalUI.CreateActionsPanel()
            // This ensures proper layout constraints and button sizing.
        }

        private void CreateFooter(Transform parent)
        {
            GameObject footer = new GameObject("Footer");
            footer.transform.SetParent(parent, false);

            RectTransform rect = footer.AddComponent<RectTransform>();
            rect.anchorMin = new Vector2(0, 0);
            rect.anchorMax = new Vector2(1, 0);
            rect.pivot = new Vector2(0.5f, 0);
            rect.anchoredPosition = Vector2.zero;
            rect.sizeDelta = new Vector2(0, 40);

            Image bg = footer.AddComponent<Image>();
            bg.color = new Color(0.1f, 0.15f, 0.1f, 1f);

            GameObject currencyObj = new GameObject("CurrencyText");
            currencyObj.transform.SetParent(footer.transform, false);

            var currencyText = currencyObj.AddComponent<TMPro.TextMeshProUGUI>();
            currencyText.text = "Balance: 0";
            currencyText.fontSize = 18;
            currencyText.color = new Color(1f, 0.9f, 0.4f);
            currencyText.alignment = TMPro.TextAlignmentOptions.Center;
            currencyText.fontStyle = TMPro.FontStyles.Bold;

            RectTransform currencyRect = currencyObj.GetComponent<RectTransform>();
            currencyRect.anchorMin = Vector2.zero;
            currencyRect.anchorMax = Vector2.one;
            currencyRect.offsetMin = new Vector2(10, 5);
            currencyRect.offsetMax = new Vector2(-10, -5);
        }

        private void SetupCurrencyHUD()
        {
            if (CurrencyHUD.Instance != null)
            {
                return;
            }

            var hudObj = new GameObject("CurrencyHUD");
            hudObj.transform.SetParent(uiCanvas.transform, false);
            hudObj.AddComponent<CurrencyHUD>();
        }

        private void SetupBagValueHUD()
        {
            if (BagValueHUD.Instance != null)
            {
                return;
            }

            var hudObj = new GameObject("BagValueHUD");
            hudObj.transform.SetParent(uiCanvas.transform, false);
            hudObj.AddComponent<BagValueHUD>();
        }
    }
}
