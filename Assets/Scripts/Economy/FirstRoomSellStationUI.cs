using UnityEngine;
using UnityEngine.UI;
using TMPro;
using System.Collections;
using System.Collections.Generic;
using BeneathTheFloor.UI;
using BeneathTheFloor.Inventory;
using BeneathTheFloor.Crafting;
using BeneathTheFloor.ResourceSystem;

namespace BeneathTheFloor.Economy
{
    /// <summary>
    /// First Room Sell Station UI - Sci-Fi styled terminal with two-column layout.
    /// Left panel: Inventory items with quantities
    /// Right panel: Prices per unit and total value
    /// </summary>
    public class FirstRoomSellStationUI : MonoBehaviour
    {
        [Header("Main Panel")]
        [SerializeField] private GameObject terminalPanel;

        // UI References
        private TextMeshProUGUI balanceText;
        private Transform inventoryContent;
        private Transform priceContent;
        private ScrollRect inventoryScrollRect;
        private ScrollRect priceScrollRect;
        private TextMeshProUGUI totalValueText;
        private Button sellAllButton;

        private bool isSellInProgress = false;
        public static FirstRoomSellStationUI Instance { get; private set; }

        private FirstRoomSellStation currentStation;
        private List<SellableItem> sellableItems = new List<SellableItem>();
        private List<GameObject> inventoryRows = new List<GameObject>();
        private List<GameObject> priceRows = new List<GameObject>();

        // Sci-Fi color scheme - Dark metallic with cyan/teal accents
        private readonly Color panelBgColor = new Color(0.08f, 0.10f, 0.12f, 0.98f);
        private readonly Color headerBgColor = new Color(0.04f, 0.06f, 0.08f, 1f);
        private readonly Color sectionBgColor = new Color(0.06f, 0.08f, 0.10f, 1f);
        private readonly Color rowBgDark = new Color(0.08f, 0.10f, 0.12f, 1f);
        private readonly Color rowBgLight = new Color(0.10f, 0.12f, 0.14f, 1f);
        private readonly Color frameBorderOuter = new Color(0.02f, 0.03f, 0.04f, 1f);
        private readonly Color frameBorderCyan = new Color(0.0f, 0.75f, 0.85f, 0.8f);
        private readonly Color frameBorderTeal = new Color(0.0f, 0.55f, 0.65f, 0.6f);
        private readonly Color textWhite = new Color(0.92f, 0.94f, 0.96f);
        private readonly Color textCyan = new Color(0.4f, 0.9f, 1.0f);
        private readonly Color textGreen = new Color(0.3f, 0.95f, 0.5f);
        private readonly Color textGray = new Color(0.45f, 0.50f, 0.55f);
        private readonly Color sectionHeaderBg = new Color(0.0f, 0.35f, 0.42f, 0.9f);
        private readonly Color buttonCyan = new Color(0.0f, 0.55f, 0.65f);
        private readonly Color buttonCyanHover = new Color(0.0f, 0.70f, 0.82f);
        private readonly Color closeRed = new Color(0.65f, 0.15f, 0.15f);

        private void Awake()
        {
            if (Instance == null) Instance = this;
            else { Destroy(gameObject); return; }
        }

        private void Start()
        {
            // Don't hide the panel if ShowUI was already called this frame
            // (This can happen when the component is created and ShowUI is called immediately)
        }

        private void OnDestroy()
        {
            if (Instance == this) Instance = null;
        }

        private void BuildUI()
        {
            Debug.Log("[FirstRoomSellStationUI] BuildUI started");

            if (terminalPanel != null)
            {
                Destroy(terminalPanel);
                terminalPanel = null;
            }

            // Prefer our dedicated MachineUICanvas for proper sorting
            Canvas canvas = null;
            GameObject machineCanvas = GameObject.Find("MachineUICanvas");
            if (machineCanvas != null)
            {
                canvas = machineCanvas.GetComponent<Canvas>();
                Debug.Log($"[FirstRoomSellStationUI] Using MachineUICanvas");
            }

            if (canvas == null)
            {
                canvas = GetComponentInParent<Canvas>();
                Debug.Log($"[FirstRoomSellStationUI] GetComponentInParent<Canvas>: {(canvas != null ? canvas.name : "null")}");
            }

            if (canvas == null)
            {
                canvas = FindObjectOfType<Canvas>();
                Debug.Log($"[FirstRoomSellStationUI] FindObjectOfType<Canvas>: {(canvas != null ? canvas.name : "null")}");
            }

            if (canvas == null)
            {
                Debug.LogError("[FirstRoomSellStationUI] NO CANVAS FOUND!");
                return;
            }

            CreateTerminalPanel(canvas);
            Debug.Log($"[FirstRoomSellStationUI] BuildUI completed on canvas: {canvas.name}, terminalPanel is {(terminalPanel != null ? "valid" : "null")}");
        }

        private void CreateTerminalPanel(Canvas canvas)
        {
            // === ROOT PANEL - Larger ===
            terminalPanel = new GameObject("FirstRoomSellStationPanel");
            terminalPanel.transform.SetParent(canvas.transform, false);

            RectTransform panelRect = terminalPanel.AddComponent<RectTransform>();
            panelRect.anchorMin = new Vector2(0.5f, 0.5f);
            panelRect.anchorMax = new Vector2(0.5f, 0.5f);
            panelRect.pivot = new Vector2(0.5f, 0.5f);
            panelRect.sizeDelta = new Vector2(750, 550); // Larger panel

            Image panelBg = terminalPanel.AddComponent<Image>();
            panelBg.color = panelBgColor;

            Outline outerFrame = terminalPanel.AddComponent<Outline>();
            outerFrame.effectColor = frameBorderCyan;
            outerFrame.effectDistance = new Vector2(2, 2);

            Shadow innerFrame = terminalPanel.AddComponent<Shadow>();
            innerFrame.effectColor = frameBorderOuter;
            innerFrame.effectDistance = new Vector2(4, -4);

            Shadow dropShadow = terminalPanel.AddComponent<Shadow>();
            dropShadow.effectColor = new Color(0, 0, 0, 0.7f);
            dropShadow.effectDistance = new Vector2(8, -8);

            VerticalLayoutGroup mainVLG = terminalPanel.AddComponent<VerticalLayoutGroup>();
            mainVLG.padding = new RectOffset(8, 8, 8, 8); // More padding
            mainVLG.spacing = 8;
            mainVLG.childControlWidth = true;
            mainVLG.childControlHeight = true;
            mainVLG.childForceExpandWidth = true;
            mainVLG.childForceExpandHeight = false;
            mainVLG.childAlignment = TextAnchor.UpperCenter;

            CreateHeaderBar(terminalPanel.transform);
            CreateTwoColumnLayout(terminalPanel.transform);
            CreateFooter(terminalPanel.transform);
        }

        private void CreateHeaderBar(Transform parent)
        {
            GameObject header = new GameObject("Header");
            header.transform.SetParent(parent, false);

            RectTransform headerRect = header.AddComponent<RectTransform>();
            headerRect.sizeDelta = new Vector2(0, 60); // Taller

            LayoutElement layout = header.AddComponent<LayoutElement>();
            layout.preferredHeight = 60;
            layout.minHeight = 60;

            Image bg = header.AddComponent<Image>();
            bg.color = headerBgColor;

            // Cyan accent line at bottom
            GameObject accentLine = new GameObject("AccentLine");
            accentLine.transform.SetParent(header.transform, false);
            RectTransform accentRect = accentLine.AddComponent<RectTransform>();
            accentRect.anchorMin = new Vector2(0, 0);
            accentRect.anchorMax = new Vector2(1, 0);
            accentRect.offsetMin = new Vector2(15, 0);
            accentRect.offsetMax = new Vector2(-15, 3);
            Image accentImg = accentLine.AddComponent<Image>();
            accentImg.color = frameBorderCyan;

            // Title - centered, larger
            GameObject titleObj = new GameObject("Title");
            titleObj.transform.SetParent(header.transform, false);
            RectTransform titleRect = titleObj.AddComponent<RectTransform>();
            titleRect.anchorMin = new Vector2(0, 0);
            titleRect.anchorMax = new Vector2(1, 1);
            titleRect.offsetMin = new Vector2(60, 0);
            titleRect.offsetMax = new Vector2(-60, 0);

            TextMeshProUGUI titleTMP = titleObj.AddComponent<TextMeshProUGUI>();
            titleTMP.text = "SELL STATION";
            titleTMP.fontSize = 32; // Larger
            titleTMP.color = textCyan;
            titleTMP.alignment = TextAlignmentOptions.Center;
            titleTMP.fontStyle = FontStyles.Bold;
            titleTMP.characterSpacing = 6f;

            // Close button (X) - larger
            GameObject closeObj = new GameObject("CloseButton");
            closeObj.transform.SetParent(header.transform, false);
            RectTransform closeRect = closeObj.AddComponent<RectTransform>();
            closeRect.anchorMin = new Vector2(1, 0.5f);
            closeRect.anchorMax = new Vector2(1, 0.5f);
            closeRect.pivot = new Vector2(1, 0.5f);
            closeRect.anchoredPosition = new Vector2(-12, 0);
            closeRect.sizeDelta = new Vector2(42, 42); // Larger

            Image closeBg = closeObj.AddComponent<Image>();
            closeBg.color = closeRed;

            Button closeBtn = closeObj.AddComponent<Button>();
            closeBtn.targetGraphic = closeBg;
            closeBtn.onClick.AddListener(OnCloseClicked);

            ColorBlock closeColors = closeBtn.colors;
            closeColors.normalColor = closeRed;
            closeColors.highlightedColor = new Color(0.85f, 0.25f, 0.25f);
            closeColors.pressedColor = new Color(0.5f, 0.1f, 0.1f);
            closeBtn.colors = closeColors;

            GameObject xText = new GameObject("X");
            xText.transform.SetParent(closeObj.transform, false);
            RectTransform xRect = xText.AddComponent<RectTransform>();
            xRect.anchorMin = Vector2.zero;
            xRect.anchorMax = Vector2.one;
            xRect.offsetMin = Vector2.zero;
            xRect.offsetMax = Vector2.zero;

            TextMeshProUGUI xTMP = xText.AddComponent<TextMeshProUGUI>();
            xTMP.text = "X";
            xTMP.fontSize = 24; // Larger
            xTMP.color = textWhite;
            xTMP.alignment = TextAlignmentOptions.Center;
            xTMP.fontStyle = FontStyles.Bold;
            xTMP.raycastTarget = false;
        }

        private void CreateTwoColumnLayout(Transform parent)
        {
            GameObject columnsContainer = new GameObject("ColumnsContainer");
            columnsContainer.transform.SetParent(parent, false);

            RectTransform containerRect = columnsContainer.AddComponent<RectTransform>();
            containerRect.sizeDelta = new Vector2(0, 350); // Taller

            LayoutElement containerLayout = columnsContainer.AddComponent<LayoutElement>();
            containerLayout.flexibleHeight = 1;
            containerLayout.minHeight = 350;
            containerLayout.preferredHeight = 350;

            HorizontalLayoutGroup hlg = columnsContainer.AddComponent<HorizontalLayoutGroup>();
            hlg.padding = new RectOffset(10, 10, 0, 0);
            hlg.spacing = 16; // More spacing between columns
            hlg.childControlWidth = true;
            hlg.childControlHeight = true;
            hlg.childForceExpandWidth = true;
            hlg.childForceExpandHeight = true;

            CreateInventoryColumn(columnsContainer.transform);
            CreatePriceColumn(columnsContainer.transform);
        }

        private void CreateInventoryColumn(Transform parent)
        {
            GameObject column = new GameObject("InventoryColumn");
            column.transform.SetParent(parent, false);

            RectTransform colRect = column.AddComponent<RectTransform>();
            LayoutElement colLayout = column.AddComponent<LayoutElement>();
            colLayout.flexibleWidth = 1;

            Image colBg = column.AddComponent<Image>();
            colBg.color = sectionBgColor;

            Outline colOutline = column.AddComponent<Outline>();
            colOutline.effectColor = frameBorderTeal;
            colOutline.effectDistance = new Vector2(1, 1);

            VerticalLayoutGroup vlg = column.AddComponent<VerticalLayoutGroup>();
            vlg.padding = new RectOffset(0, 0, 0, 0);
            vlg.spacing = 0;
            vlg.childControlWidth = true;
            vlg.childControlHeight = true;
            vlg.childForceExpandWidth = true;
            vlg.childForceExpandHeight = false;

            CreateSectionHeader(column.transform, "INVENTORY");
            CreateInventoryScrollArea(column.transform);
        }

        private void CreatePriceColumn(Transform parent)
        {
            GameObject column = new GameObject("PriceColumn");
            column.transform.SetParent(parent, false);

            RectTransform colRect = column.AddComponent<RectTransform>();
            LayoutElement colLayout = column.AddComponent<LayoutElement>();
            colLayout.flexibleWidth = 1;

            Image colBg = column.AddComponent<Image>();
            colBg.color = sectionBgColor;

            Outline colOutline = column.AddComponent<Outline>();
            colOutline.effectColor = frameBorderTeal;
            colOutline.effectDistance = new Vector2(1, 1);

            VerticalLayoutGroup vlg = column.AddComponent<VerticalLayoutGroup>();
            vlg.padding = new RectOffset(0, 0, 0, 0);
            vlg.spacing = 0;
            vlg.childControlWidth = true;
            vlg.childControlHeight = true;
            vlg.childForceExpandWidth = true;
            vlg.childForceExpandHeight = false;

            CreateSectionHeader(column.transform, "PRICE PER UNIT");
            CreatePriceScrollArea(column.transform);
        }

        private void CreateSectionHeader(Transform parent, string title)
        {
            GameObject headerObj = new GameObject($"Header_{title}");
            headerObj.transform.SetParent(parent, false);

            RectTransform rect = headerObj.AddComponent<RectTransform>();
            rect.sizeDelta = new Vector2(0, 40); // Taller

            LayoutElement layout = headerObj.AddComponent<LayoutElement>();
            layout.preferredHeight = 40;
            layout.minHeight = 40;

            Image bg = headerObj.AddComponent<Image>();
            bg.color = sectionHeaderBg;

            GameObject textObj = new GameObject("Text");
            textObj.transform.SetParent(headerObj.transform, false);
            RectTransform textRect = textObj.AddComponent<RectTransform>();
            textRect.anchorMin = Vector2.zero;
            textRect.anchorMax = Vector2.one;
            textRect.offsetMin = new Vector2(15, 0);
            textRect.offsetMax = new Vector2(-15, 0);

            TextMeshProUGUI tmp = textObj.AddComponent<TextMeshProUGUI>();
            tmp.text = title;
            tmp.fontSize = 16; // Larger
            tmp.color = textWhite;
            tmp.alignment = TextAlignmentOptions.MidlineLeft;
            tmp.fontStyle = FontStyles.Bold;
            tmp.characterSpacing = 2f;
        }

        private void CreateInventoryScrollArea(Transform parent)
        {
            GameObject scrollArea = new GameObject("ScrollArea");
            scrollArea.transform.SetParent(parent, false);

            RectTransform rect = scrollArea.AddComponent<RectTransform>();
            LayoutElement layout = scrollArea.AddComponent<LayoutElement>();
            layout.flexibleHeight = 1;
            layout.minHeight = 250;

            Image bg = scrollArea.AddComponent<Image>();
            bg.color = rowBgDark;

            inventoryScrollRect = scrollArea.AddComponent<ScrollRect>();
            inventoryScrollRect.horizontal = false;
            inventoryScrollRect.vertical = true;
            inventoryScrollRect.scrollSensitivity = 25f;

            GameObject viewport = new GameObject("Viewport");
            viewport.transform.SetParent(scrollArea.transform, false);
            RectTransform vpRect = viewport.AddComponent<RectTransform>();
            vpRect.anchorMin = Vector2.zero;
            vpRect.anchorMax = Vector2.one;
            vpRect.offsetMin = Vector2.zero;
            vpRect.offsetMax = Vector2.zero;

            Image vpMask = viewport.AddComponent<Image>();
            vpMask.color = Color.white;
            Mask mask = viewport.AddComponent<Mask>();
            mask.showMaskGraphic = false;

            GameObject content = new GameObject("Content");
            content.transform.SetParent(viewport.transform, false);
            RectTransform contentRect = content.AddComponent<RectTransform>();
            contentRect.anchorMin = new Vector2(0, 1);
            contentRect.anchorMax = new Vector2(1, 1);
            contentRect.pivot = new Vector2(0.5f, 1);
            contentRect.anchoredPosition = Vector2.zero;
            contentRect.sizeDelta = new Vector2(0, 0);

            VerticalLayoutGroup vlg = content.AddComponent<VerticalLayoutGroup>();
            vlg.padding = new RectOffset(4, 4, 4, 4);
            vlg.spacing = 2;
            vlg.childControlWidth = true;
            vlg.childControlHeight = true;
            vlg.childForceExpandWidth = true;
            vlg.childForceExpandHeight = false;

            ContentSizeFitter csf = content.AddComponent<ContentSizeFitter>();
            csf.verticalFit = ContentSizeFitter.FitMode.PreferredSize;

            inventoryScrollRect.viewport = vpRect;
            inventoryScrollRect.content = contentRect;
            inventoryContent = content.transform;
        }

        private void CreatePriceScrollArea(Transform parent)
        {
            GameObject scrollArea = new GameObject("ScrollArea");
            scrollArea.transform.SetParent(parent, false);

            RectTransform rect = scrollArea.AddComponent<RectTransform>();
            LayoutElement layout = scrollArea.AddComponent<LayoutElement>();
            layout.flexibleHeight = 1;
            layout.minHeight = 250;

            Image bg = scrollArea.AddComponent<Image>();
            bg.color = rowBgDark;

            priceScrollRect = scrollArea.AddComponent<ScrollRect>();
            priceScrollRect.horizontal = false;
            priceScrollRect.vertical = true;
            priceScrollRect.scrollSensitivity = 25f;

            GameObject viewport = new GameObject("Viewport");
            viewport.transform.SetParent(scrollArea.transform, false);
            RectTransform vpRect = viewport.AddComponent<RectTransform>();
            vpRect.anchorMin = Vector2.zero;
            vpRect.anchorMax = Vector2.one;
            vpRect.offsetMin = Vector2.zero;
            vpRect.offsetMax = Vector2.zero;

            Image vpMask = viewport.AddComponent<Image>();
            vpMask.color = Color.white;
            Mask mask = viewport.AddComponent<Mask>();
            mask.showMaskGraphic = false;

            GameObject content = new GameObject("Content");
            content.transform.SetParent(viewport.transform, false);
            RectTransform contentRect = content.AddComponent<RectTransform>();
            contentRect.anchorMin = new Vector2(0, 1);
            contentRect.anchorMax = new Vector2(1, 1);
            contentRect.pivot = new Vector2(0.5f, 1);
            contentRect.anchoredPosition = Vector2.zero;
            contentRect.sizeDelta = new Vector2(0, 0);

            VerticalLayoutGroup vlg = content.AddComponent<VerticalLayoutGroup>();
            vlg.padding = new RectOffset(4, 4, 4, 4);
            vlg.spacing = 2;
            vlg.childControlWidth = true;
            vlg.childControlHeight = true;
            vlg.childForceExpandWidth = true;
            vlg.childForceExpandHeight = false;

            ContentSizeFitter csf = content.AddComponent<ContentSizeFitter>();
            csf.verticalFit = ContentSizeFitter.FitMode.PreferredSize;

            priceScrollRect.viewport = vpRect;
            priceScrollRect.content = contentRect;
            priceContent = content.transform;
        }

        private void CreateFooter(Transform parent)
        {
            GameObject footer = new GameObject("Footer");
            footer.transform.SetParent(parent, false);

            RectTransform rect = footer.AddComponent<RectTransform>();
            rect.sizeDelta = new Vector2(0, 80); // Taller

            LayoutElement layout = footer.AddComponent<LayoutElement>();
            layout.preferredHeight = 80;
            layout.minHeight = 80;

            Image bg = footer.AddComponent<Image>();
            bg.color = headerBgColor;

            // Top accent line
            GameObject accentLine = new GameObject("AccentLine");
            accentLine.transform.SetParent(footer.transform, false);
            RectTransform accentRect = accentLine.AddComponent<RectTransform>();
            accentRect.anchorMin = new Vector2(0, 1);
            accentRect.anchorMax = new Vector2(1, 1);
            accentRect.offsetMin = new Vector2(15, -3);
            accentRect.offsetMax = new Vector2(-15, 0);
            Image accentImg = accentLine.AddComponent<Image>();
            accentImg.color = frameBorderCyan;

            // TOTAL VALUE - left side
            GameObject totalObj = new GameObject("TotalValue");
            totalObj.transform.SetParent(footer.transform, false);
            RectTransform totalRect = totalObj.AddComponent<RectTransform>();
            totalRect.anchorMin = new Vector2(0, 0.5f);
            totalRect.anchorMax = new Vector2(0.32f, 0.5f);
            totalRect.offsetMin = new Vector2(25, -30);
            totalRect.offsetMax = new Vector2(0, 30);

            VerticalLayoutGroup totalVLG = totalObj.AddComponent<VerticalLayoutGroup>();
            totalVLG.spacing = 4;
            totalVLG.childControlWidth = true;
            totalVLG.childControlHeight = true;
            totalVLG.childForceExpandWidth = true;
            totalVLG.childForceExpandHeight = true;

            GameObject totalLabelObj = new GameObject("Label");
            totalLabelObj.transform.SetParent(totalObj.transform, false);
            TextMeshProUGUI totalLabelTMP = totalLabelObj.AddComponent<TextMeshProUGUI>();
            totalLabelTMP.text = "TOTAL VALUE";
            totalLabelTMP.fontSize = 13; // Larger
            totalLabelTMP.color = textGray;
            totalLabelTMP.alignment = TextAlignmentOptions.BottomLeft;
            totalLabelTMP.characterSpacing = 2f;

            GameObject totalAmountObj = new GameObject("Amount");
            totalAmountObj.transform.SetParent(totalObj.transform, false);
            totalValueText = totalAmountObj.AddComponent<TextMeshProUGUI>();
            totalValueText.text = "$0";
            totalValueText.fontSize = 28; // Larger
            totalValueText.color = textGreen;
            totalValueText.alignment = TextAlignmentOptions.TopLeft;
            totalValueText.fontStyle = FontStyles.Bold;

            // SELL ALL button - center, larger
            GameObject btnObj = new GameObject("SellAllButton");
            btnObj.transform.SetParent(footer.transform, false);
            RectTransform btnRect = btnObj.AddComponent<RectTransform>();
            btnRect.anchorMin = new Vector2(0.5f, 0.5f);
            btnRect.anchorMax = new Vector2(0.5f, 0.5f);
            btnRect.pivot = new Vector2(0.5f, 0.5f);
            btnRect.anchoredPosition = Vector2.zero;
            btnRect.sizeDelta = new Vector2(160, 50); // Larger

            Image btnBg = btnObj.AddComponent<Image>();
            btnBg.color = buttonCyan;

            Outline btnGlow = btnObj.AddComponent<Outline>();
            btnGlow.effectColor = new Color(0.0f, 0.8f, 0.95f, 0.4f);
            btnGlow.effectDistance = new Vector2(2, 2);

            sellAllButton = btnObj.AddComponent<Button>();
            sellAllButton.targetGraphic = btnBg;
            sellAllButton.onClick.AddListener(OnSellAllClicked);

            ColorBlock btnColors = sellAllButton.colors;
            btnColors.normalColor = buttonCyan;
            btnColors.highlightedColor = buttonCyanHover;
            btnColors.pressedColor = new Color(0.0f, 0.45f, 0.55f);
            btnColors.disabledColor = new Color(0.2f, 0.25f, 0.28f, 0.6f);
            sellAllButton.colors = btnColors;

            GameObject btnTextObj = new GameObject("Text");
            btnTextObj.transform.SetParent(btnObj.transform, false);
            RectTransform btnTextRect = btnTextObj.AddComponent<RectTransform>();
            btnTextRect.anchorMin = Vector2.zero;
            btnTextRect.anchorMax = Vector2.one;
            btnTextRect.offsetMin = Vector2.zero;
            btnTextRect.offsetMax = Vector2.zero;

            TextMeshProUGUI btnTMP = btnTextObj.AddComponent<TextMeshProUGUI>();
            btnTMP.text = "SELL ALL";
            btnTMP.fontSize = 22; // Larger
            btnTMP.color = textWhite;
            btnTMP.alignment = TextAlignmentOptions.Center;
            btnTMP.fontStyle = FontStyles.Bold;
            btnTMP.characterSpacing = 2f;
            btnTMP.raycastTarget = false;

            // BALANCE - right side
            GameObject balanceObj = new GameObject("Balance");
            balanceObj.transform.SetParent(footer.transform, false);
            RectTransform balanceRect = balanceObj.AddComponent<RectTransform>();
            balanceRect.anchorMin = new Vector2(0.68f, 0.5f);
            balanceRect.anchorMax = new Vector2(1, 0.5f);
            balanceRect.offsetMin = new Vector2(0, -30);
            balanceRect.offsetMax = new Vector2(-25, 30);

            VerticalLayoutGroup balanceVLG = balanceObj.AddComponent<VerticalLayoutGroup>();
            balanceVLG.spacing = 4;
            balanceVLG.childControlWidth = true;
            balanceVLG.childControlHeight = true;
            balanceVLG.childForceExpandWidth = true;
            balanceVLG.childForceExpandHeight = true;

            GameObject balanceLabelObj = new GameObject("Label");
            balanceLabelObj.transform.SetParent(balanceObj.transform, false);
            TextMeshProUGUI balanceLabelTMP = balanceLabelObj.AddComponent<TextMeshProUGUI>();
            balanceLabelTMP.text = "CURRENT BALANCE";
            balanceLabelTMP.fontSize = 13; // Larger
            balanceLabelTMP.color = textGray;
            balanceLabelTMP.alignment = TextAlignmentOptions.BottomRight;
            balanceLabelTMP.characterSpacing = 2f;

            GameObject balanceAmountObj = new GameObject("Amount");
            balanceAmountObj.transform.SetParent(balanceObj.transform, false);
            balanceText = balanceAmountObj.AddComponent<TextMeshProUGUI>();
            balanceText.text = "$0";
            balanceText.fontSize = 28; // Larger
            balanceText.color = textCyan;
            balanceText.alignment = TextAlignmentOptions.TopRight;
            balanceText.fontStyle = FontStyles.Bold;
        }

        private void CreateInventoryRow(SellableItem sellable, int index)
        {
            if (inventoryContent == null) return;

            string itemName = sellable.isDust ? "Dust" : (sellable.item != null ? sellable.item.itemName : "Unknown");

            GameObject row = new GameObject($"InvRow_{itemName}");
            row.transform.SetParent(inventoryContent, false);

            RectTransform rect = row.AddComponent<RectTransform>();
            rect.sizeDelta = new Vector2(0, 52); // Taller rows

            LayoutElement layout = row.AddComponent<LayoutElement>();
            layout.preferredHeight = 52;
            layout.minHeight = 52;

            Image bg = row.AddComponent<Image>();
            bg.color = (index % 2 == 0) ? rowBgDark : rowBgLight;

            // Icon slot background - larger
            GameObject iconBg = new GameObject("IconBg");
            iconBg.transform.SetParent(row.transform, false);
            RectTransform iconBgRect = iconBg.AddComponent<RectTransform>();
            iconBgRect.anchorMin = new Vector2(0, 0.5f);
            iconBgRect.anchorMax = new Vector2(0, 0.5f);
            iconBgRect.pivot = new Vector2(0, 0.5f);
            iconBgRect.anchoredPosition = new Vector2(10, 0);
            iconBgRect.sizeDelta = new Vector2(40, 40); // Larger
            Image iconBgImg = iconBg.AddComponent<Image>();
            iconBgImg.color = new Color(0.03f, 0.04f, 0.05f, 0.9f);

            // Icon - larger
            GameObject iconObj = new GameObject("Icon");
            iconObj.transform.SetParent(row.transform, false);
            RectTransform iconRect = iconObj.AddComponent<RectTransform>();
            iconRect.anchorMin = new Vector2(0, 0.5f);
            iconRect.anchorMax = new Vector2(0, 0.5f);
            iconRect.pivot = new Vector2(0, 0.5f);
            iconRect.anchoredPosition = new Vector2(12, 0);
            iconRect.sizeDelta = new Vector2(36, 36); // Larger

            Image iconImg = iconObj.AddComponent<Image>();
            iconImg.preserveAspect = true;
            iconImg.raycastTarget = false;

            SetItemIcon(iconImg, sellable);

            // Item name and quantity - larger font
            GameObject textObj = new GameObject("Text");
            textObj.transform.SetParent(row.transform, false);
            RectTransform textRect = textObj.AddComponent<RectTransform>();
            textRect.anchorMin = new Vector2(0, 0);
            textRect.anchorMax = new Vector2(1, 1);
            textRect.offsetMin = new Vector2(58, 0);
            textRect.offsetMax = new Vector2(-10, 0);

            TextMeshProUGUI textTMP = textObj.AddComponent<TextMeshProUGUI>();
            textTMP.text = $"{itemName} <color=#66DDFF>x{sellable.totalAmount}</color>";
            textTMP.fontSize = 18; // Larger
            textTMP.color = textWhite;
            textTMP.alignment = TextAlignmentOptions.MidlineLeft;

            inventoryRows.Add(row);
        }

        private void CreatePriceRow(SellableItem sellable, int index)
        {
            if (priceContent == null) return;

            string itemName = sellable.isDust ? "Dust" : (sellable.item != null ? sellable.item.itemName : "Unknown");

            GameObject row = new GameObject($"PriceRow_{itemName}");
            row.transform.SetParent(priceContent, false);

            RectTransform rect = row.AddComponent<RectTransform>();
            rect.sizeDelta = new Vector2(0, 52); // Same height as inventory rows

            LayoutElement layout = row.AddComponent<LayoutElement>();
            layout.preferredHeight = 52;
            layout.minHeight = 52;

            Image bg = row.AddComponent<Image>();
            bg.color = (index % 2 == 0) ? rowBgDark : rowBgLight;

            // Use HorizontalLayoutGroup for proper text distribution
            HorizontalLayoutGroup hlg = row.AddComponent<HorizontalLayoutGroup>();
            hlg.padding = new RectOffset(15, 15, 0, 0);
            hlg.spacing = 10;
            hlg.childControlWidth = true;
            hlg.childControlHeight = true;
            hlg.childForceExpandWidth = true;
            hlg.childForceExpandHeight = true;
            hlg.childAlignment = TextAnchor.MiddleCenter;

            // Item name - left aligned
            GameObject nameObj = new GameObject("Name");
            nameObj.transform.SetParent(row.transform, false);
            LayoutElement nameLayout = nameObj.AddComponent<LayoutElement>();
            nameLayout.flexibleWidth = 1;

            TextMeshProUGUI nameTMP = nameObj.AddComponent<TextMeshProUGUI>();
            nameTMP.text = itemName;
            nameTMP.fontSize = 18; // Larger
            nameTMP.color = textWhite;
            nameTMP.alignment = TextAlignmentOptions.MidlineLeft;

            // Price - right aligned
            GameObject priceObj = new GameObject("Price");
            priceObj.transform.SetParent(row.transform, false);
            LayoutElement priceLayout = priceObj.AddComponent<LayoutElement>();
            priceLayout.flexibleWidth = 0.6f;

            TextMeshProUGUI priceTMP = priceObj.AddComponent<TextMeshProUGUI>();
            priceTMP.text = $"${sellable.sellPrice}";
            priceTMP.fontSize = 18; // Larger
            priceTMP.color = textGreen;
            priceTMP.alignment = TextAlignmentOptions.MidlineRight;
            priceTMP.fontStyle = FontStyles.Bold;

            priceRows.Add(row);
        }

        private void SetItemIcon(Image iconImg, SellableItem sellable)
        {
            // Always set a visible color first as fallback
            Color fallbackColor = GetItemFallbackColor(sellable);
            iconImg.color = fallbackColor;

            if (sellable.isDust)
            {
                // Try loading dust/dirt icon
                Sprite dustIcon = Resources.Load<Sprite>("Icons/icon_dirt");
                if (dustIcon == null) dustIcon = Resources.Load<Sprite>("Icons/icon_dust");
                if (dustIcon != null)
                {
                    iconImg.sprite = dustIcon;
                    iconImg.color = Color.white;
                }
            }
            else if (sellable.item != null)
            {
                // First try the item's own icon
                if (sellable.item.icon != null)
                {
                    iconImg.sprite = sellable.item.icon;
                    iconImg.color = Color.white;
                }
                else
                {
                    // Try loading from Resources based on item name
                    string iconName = sellable.item.itemName.Replace(" ", "_").ToLower();
                    Sprite loadedIcon = Resources.Load<Sprite>($"Icons/icon_{iconName}");

                    // Also try without "icon_" prefix
                    if (loadedIcon == null)
                        loadedIcon = Resources.Load<Sprite>($"Icons/{iconName}");

                    if (loadedIcon != null)
                    {
                        iconImg.sprite = loadedIcon;
                        iconImg.color = Color.white;
                    }
                    // Keep fallback color if no icon found
                }
            }
        }

        private Color GetItemFallbackColor(SellableItem sellable)
        {
            if (sellable.isDust)
                return new Color(0.72f, 0.58f, 0.38f); // Sandy brown

            if (sellable.item != null)
            {
                string itemName = sellable.item.itemName.ToLower();

                if (itemName.Contains("copper"))
                    return new Color(0.85f, 0.55f, 0.30f); // Copper orange
                if (itemName.Contains("iron"))
                    return new Color(0.60f, 0.55f, 0.55f); // Iron gray
                if (itemName.Contains("gold"))
                    return new Color(1.0f, 0.85f, 0.30f); // Gold
                if (itemName.Contains("silver"))
                    return new Color(0.80f, 0.80f, 0.85f); // Silver
                if (itemName.Contains("coal"))
                    return new Color(0.20f, 0.20f, 0.20f); // Coal black
                if (itemName.Contains("stone"))
                    return new Color(0.50f, 0.50f, 0.52f); // Stone gray
                if (itemName.Contains("clay"))
                    return new Color(0.75f, 0.55f, 0.40f); // Clay
                if (itemName.Contains("dirt") || itemName.Contains("dust"))
                    return new Color(0.55f, 0.40f, 0.25f); // Dirt brown
            }

            return new Color(0.5f, 0.5f, 0.55f); // Default gray
        }

        private void CreateEmptyMessage()
        {
            if (inventoryContent == null) return;

            GameObject msgObj = new GameObject("EmptyMessage");
            msgObj.transform.SetParent(inventoryContent, false);

            RectTransform rect = msgObj.AddComponent<RectTransform>();
            rect.sizeDelta = new Vector2(0, 70);

            LayoutElement layout = msgObj.AddComponent<LayoutElement>();
            layout.preferredHeight = 70;
            layout.minHeight = 70;

            GameObject textObj = new GameObject("Text");
            textObj.transform.SetParent(msgObj.transform, false);
            RectTransform textRect = textObj.AddComponent<RectTransform>();
            textRect.anchorMin = Vector2.zero;
            textRect.anchorMax = Vector2.one;
            textRect.offsetMin = Vector2.zero;
            textRect.offsetMax = Vector2.zero;

            TextMeshProUGUI tmp = textObj.AddComponent<TextMeshProUGUI>();
            tmp.text = "No items to sell";
            tmp.fontSize = 18; // Larger
            tmp.color = textGray;
            tmp.alignment = TextAlignmentOptions.Center;
            tmp.fontStyle = FontStyles.Italic;

            inventoryRows.Add(msgObj);

            // Also add empty message to price panel
            GameObject priceMsgObj = new GameObject("EmptyMessage");
            priceMsgObj.transform.SetParent(priceContent, false);

            RectTransform priceRect = priceMsgObj.AddComponent<RectTransform>();
            priceRect.sizeDelta = new Vector2(0, 70);

            LayoutElement priceLayout = priceMsgObj.AddComponent<LayoutElement>();
            priceLayout.preferredHeight = 70;
            priceLayout.minHeight = 70;

            priceRows.Add(priceMsgObj);
        }

        // ==================== PUBLIC API ====================

        /// <summary>
        /// Shows the UI. Returns true if successful, false if failed.
        /// </summary>
        public bool ShowUI(FirstRoomSellStation station)
        {
            Debug.Log("[FirstRoomSellStationUI] ShowUI called");

            currentStation = station;
            EnsureInventorySellService();

            BuildUI();

            if (terminalPanel == null)
            {
                Debug.LogError("[FirstRoomSellStationUI] terminalPanel is NULL after BuildUI!");
                return false;
            }

            terminalPanel.SetActive(true);
            Debug.Log("[FirstRoomSellStationUI] terminalPanel activated");

            Canvas.ForceUpdateCanvases();
            LayoutRebuilder.ForceRebuildLayoutImmediate(terminalPanel.GetComponent<RectTransform>());

            var canvasGroup = terminalPanel.GetComponent<CanvasGroup>();
            if (canvasGroup == null) canvasGroup = terminalPanel.AddComponent<CanvasGroup>();
            canvasGroup.interactable = true;
            canvasGroup.blocksRaycasts = true;
            canvasGroup.alpha = 1f;

            Cursor.lockState = CursorLockMode.None;
            Cursor.visible = true;

            UIState.IsMachineUIOpen = true;

            RefreshContent();
            UpdateBalanceDisplay();

            Debug.Log("[FirstRoomSellStationUI] ShowUI completed successfully");
            return true;
        }

        public void HideUI()
        {
            if (terminalPanel != null) terminalPanel.SetActive(false);
            currentStation = null;
            UIState.IsMachineUIOpen = false;
            Cursor.lockState = CursorLockMode.Locked;
            Cursor.visible = false;
            Time.timeScale = 1f;
        }

        private void RefreshContent()
        {
            foreach (var row in inventoryRows)
            {
                if (row != null) Destroy(row);
            }
            inventoryRows.Clear();

            foreach (var row in priceRows)
            {
                if (row != null) Destroy(row);
            }
            priceRows.Clear();

            sellableItems.Clear();

            // Add dust
            if (DustManager.Instance != null)
            {
                float dustAmount = DustManager.Instance.GetDust();
                int dustValue = DustManager.Instance.GetDustValue();
                if (dustAmount > 0 && dustValue > 0)
                {
                    sellableItems.Add(new SellableItem(Mathf.FloorToInt(dustAmount), dustValue));
                }
            }

            // Add inventory items
            if (InventorySystem.Instance != null)
            {
                Dictionary<ItemSO, int> itemCounts = new Dictionary<ItemSO, int>();
                int slotCount = InventorySystem.Instance.MaxSlots;

                for (int i = 0; i < slotCount; i++)
                {
                    ItemStack stack = InventorySystem.Instance.GetStackAt(i);
                    if (!stack.IsEmpty && stack.item != null && stack.item.sellPrice > 0)
                    {
                        if (stack.item.category != ItemCategory.Tool && stack.item.category != ItemCategory.Equipment)
                        {
                            if (itemCounts.ContainsKey(stack.item))
                                itemCounts[stack.item] += stack.amount;
                            else
                                itemCounts[stack.item] = stack.amount;
                        }
                    }
                }

                List<SellableItem> inventoryItems = new List<SellableItem>();
                foreach (var kvp in itemCounts)
                {
                    inventoryItems.Add(new SellableItem(kvp.Key, kvp.Value));
                }
                inventoryItems.Sort((a, b) => string.Compare(a.item.itemName, b.item.itemName));
                sellableItems.AddRange(inventoryItems);
            }

            for (int i = 0; i < sellableItems.Count; i++)
            {
                CreateInventoryRow(sellableItems[i], i);
                CreatePriceRow(sellableItems[i], i);
            }

            if (sellableItems.Count == 0)
            {
                CreateEmptyMessage();
            }

            UpdateTotalValue();
        }

        private void UpdateTotalValue()
        {
            int totalValue = 0;

            if (DustManager.Instance != null)
                totalValue += DustManager.Instance.GetDustValue();

            foreach (var item in sellableItems)
            {
                if (!item.isDust)
                    totalValue += item.totalValue;
            }

            if (totalValueText != null)
                totalValueText.text = $"${totalValue:N0}";

            if (sellAllButton != null)
                sellAllButton.interactable = totalValue > 0 && !isSellInProgress;
        }

        private void UpdateBalanceDisplay()
        {
            if (balanceText != null && CurrencyManager.Instance != null)
                balanceText.text = $"${CurrencyManager.Instance.CurrentAmount:N0}";
        }

        private void EnsureInventorySellService()
        {
            if (InventorySellService.Instance != null) return;
            GameObject serviceObj = new GameObject("InventorySellService");
            serviceObj.AddComponent<InventorySellService>();
        }

        // ==================== BUTTON HANDLERS ====================

        private void OnCloseClicked()
        {
            if (currentStation != null)
                currentStation.CloseStation();
            else
                HideUI();
        }

        private void OnSellAllClicked()
        {
            if (isSellInProgress) return;

            int expectedTotal = 0;
            if (DustManager.Instance != null)
                expectedTotal += DustManager.Instance.GetDustValue();
            foreach (var item in sellableItems)
            {
                if (!item.isDust)
                    expectedTotal += item.totalValue;
            }

            if (expectedTotal <= 0) return;

            isSellInProgress = true;
            if (sellAllButton != null) sellAllButton.interactable = false;

            int totalGained = 0;

            if (DustManager.Instance != null)
            {
                int dustGained = DustManager.Instance.SellAllDust();
                totalGained += dustGained;
            }

            if (InventorySellService.Instance != null && InventorySystem.Instance != null)
            {
                int inventoryGained = InventorySellService.Instance.SellAllResourcesOnly(InventorySystem.Instance);
                totalGained += inventoryGained;
            }

            if (totalGained > 0)
            {
                if (currentStation != null)
                    currentStation.PlaySellSound();

                ShowSellToast(totalGained);
                RefreshContent();
                UpdateBalanceDisplay();

                if (InventoryUI.InventoryUIManager.Instance != null)
                    InventoryUI.InventoryUIManager.Instance.RefreshAllSlots();
            }

            StartCoroutine(ReEnableSellButton(0.3f));
        }

        private IEnumerator ReEnableSellButton(float delay)
        {
            yield return new WaitForSeconds(delay);
            isSellInProgress = false;
            UpdateTotalValue();
        }

        private void ShowSellToast(int credits)
        {
            string message = $"+${credits:N0}";

            var objectiveUI = FindObjectOfType<GameFlow.ObjectiveUIController>();
            if (objectiveUI != null)
            {
                objectiveUI.ShowToast(message, 2f);
                return;
            }

            var notifSystem = FindObjectOfType<PickupNotificationSystem>();
            if (notifSystem != null)
                notifSystem.ShowNotification(message);
        }
    }
}
