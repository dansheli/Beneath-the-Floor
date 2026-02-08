using UnityEngine;
using UnityEngine.UI;
using TMPro;
using System.Collections;
using System.Collections.Generic;
using BeneathTheFloor.UI;
using BeneathTheFloor.Inventory;
using BeneathTheFloor.Crafting;
using BeneathTheFloor.GameFlow;
using BeneathTheFloor.Energy;
using BeneathTheFloor.ResourceSystem;
using BeneathTheFloor.Lighting;

namespace BeneathTheFloor.Economy
{
    /// <summary>
    /// Data class for sellable items displayed in the terminal.
    /// </summary>
    public class SellableItem
    {
        public ItemSO item;
        public int totalAmount;
        public int sellPrice;
        public int totalValue => totalAmount * sellPrice;
        public bool isDust;

        public SellableItem(ItemSO item, int amount)
        {
            this.item = item;
            this.totalAmount = amount;
            this.sellPrice = item.sellPrice;
            this.isDust = false;
        }

        public SellableItem(int dustAmount, int dustValue)
        {
            this.item = null;
            this.totalAmount = dustAmount;
            this.sellPrice = dustAmount > 0 ? Mathf.CeilToInt((float)dustValue / dustAmount) : 0;
            this.isDust = true;
        }
    }

    /// <summary>
    /// Trade Terminal UI - Industrial desktop terminal style.
    /// Single root panel, no cards, no tabs.
    /// </summary>
    public class TradeTerminalUI : MonoBehaviour
    {
        [Header("Main Panel")]
        [SerializeField] private GameObject terminalPanel;

        // UI References
        private TextMeshProUGUI balanceText;
        private Transform tableContent;
        private ScrollRect tableScrollRect;
        private TextMeshProUGUI estimatedTotalText;
        private Button sellAllButton;
        private TextMeshProUGUI drinkOwnedText;
        private TextMeshProUGUI lampOwnedText;
        private Button buyDrinkButton;
        private Button buyLampButton;

        private bool isSellInProgress = false;
        public static TradeTerminalUI Instance { get; private set; }

        private TradeTerminal currentTerminal;
        private List<SellableItem> sellableItems = new List<SellableItem>();
        private List<GameObject> tableRows = new List<GameObject>();

        // Colors matching reference image - Industrial metal terminal
        private readonly Color panelBgColor = new Color(0.22f, 0.20f, 0.18f, 0.98f);      // Dark bronze/charcoal
        private readonly Color headerBgColor = new Color(0.16f, 0.14f, 0.13f, 1f);        // Darker header strip
        private readonly Color rowBgDark = new Color(0.18f, 0.16f, 0.15f, 1f);            // Row dark
        private readonly Color rowBgLight = new Color(0.21f, 0.19f, 0.17f, 1f);           // Row light
        private readonly Color frameBorderOuter = new Color(0.12f, 0.11f, 0.10f, 1f);     // Dark outer frame
        private readonly Color frameBorderInner = new Color(0.35f, 0.32f, 0.28f, 1f);     // Lighter inner edge
        private readonly Color rowSeparator = new Color(0.28f, 0.25f, 0.22f, 0.8f);       // Thin row lines
        private readonly Color textWhite = new Color(0.92f, 0.90f, 0.86f);                // Off-white text
        private readonly Color textGold = new Color(0.85f, 0.68f, 0.25f);                 // Gold/brass for values
        private readonly Color textGray = new Color(0.55f, 0.52f, 0.48f);                 // Muted metallic gray
        private readonly Color buttonTeal = new Color(0.28f, 0.52f, 0.52f);               // Industrial teal
        private readonly Color buttonTealHover = new Color(0.35f, 0.62f, 0.60f);          // Teal hover
        private readonly Color buttonTealGlow = new Color(0.4f, 0.7f, 0.68f, 0.3f);       // Button edge glow
        private readonly Color closeRed = new Color(0.55f, 0.18f, 0.18f);                 // Dark red X button

        private const int DRINK_COST = 10;
        private const int LAMP_COST = 50;
        private const int MAX_DRINKS = 5;
        private const int MAX_LAMPS = 5;

        private void Awake()
        {
            if (Instance == null) Instance = this;
            else { Destroy(gameObject); return; }
        }

        private void Start()
        {
            if (terminalPanel != null) terminalPanel.SetActive(false);
        }

        private void OnDestroy()
        {
            if (Instance == this) Instance = null;
        }

        private void BuildUI()
        {
            // FORCE destroy old panel to ensure fresh rebuild
            if (terminalPanel != null)
            {
                Destroy(terminalPanel);
                terminalPanel = null;
            }

            // Find Canvas - CRITICAL: UI must be under a Canvas to render
            Canvas canvas = GetComponentInParent<Canvas>();
            if (canvas == null)
            {
                canvas = FindObjectOfType<Canvas>();
            }

            if (canvas == null)
            {
                Debug.LogError("[TradeTerminalUI] NO CANVAS FOUND! UI cannot render without a Canvas.");
                return;
            }

            CreateTerminalPanel(canvas);
        }

        /// <summary>
        /// Creates the entire terminal panel from scratch - industrial style, single root.
        /// </summary>
        private void CreateTerminalPanel(Canvas canvas)
        {
            // === ROOT PANEL ===
            terminalPanel = new GameObject("TradeTerminalPanel");

            // CRITICAL: Parent to Canvas FIRST, then add RectTransform
            terminalPanel.transform.SetParent(canvas.transform, false);

            RectTransform panelRect = terminalPanel.AddComponent<RectTransform>();
            panelRect.anchorMin = new Vector2(0.5f, 0.5f);
            panelRect.anchorMax = new Vector2(0.5f, 0.5f);
            panelRect.pivot = new Vector2(0.5f, 0.5f);
            panelRect.sizeDelta = new Vector2(580, 560);

            Image panelBg = terminalPanel.AddComponent<Image>();
            panelBg.color = panelBgColor;

            // HEAVY METAL FRAME - Multiple borders for depth
            // Outer dark frame (thick)
            Outline outerFrame = terminalPanel.AddComponent<Outline>();
            outerFrame.effectColor = frameBorderOuter;
            outerFrame.effectDistance = new Vector2(4, 4);

            // Inner lighter edge (bevel highlight)
            Shadow innerHighlight = terminalPanel.AddComponent<Shadow>();
            innerHighlight.effectColor = frameBorderInner;
            innerHighlight.effectDistance = new Vector2(-2, 2);

            // Drop shadow for depth
            Shadow dropShadow = terminalPanel.AddComponent<Shadow>();
            dropShadow.effectColor = new Color(0, 0, 0, 0.6f);
            dropShadow.effectDistance = new Vector2(6, -6);

            // Main vertical layout - childControlHeight MUST be true for LayoutElement.preferredHeight to work
            VerticalLayoutGroup mainVLG = terminalPanel.AddComponent<VerticalLayoutGroup>();
            mainVLG.padding = new RectOffset(6, 6, 6, 6); // Inner padding for frame effect
            mainVLG.spacing = 0;
            mainVLG.childControlWidth = true;
            mainVLG.childControlHeight = true;
            mainVLG.childForceExpandWidth = true;
            mainVLG.childForceExpandHeight = false;
            mainVLG.childAlignment = TextAnchor.UpperCenter;

            // 1. HEADER BAR
            CreateHeaderBar(terminalPanel.transform);

            // 2. SELL ITEMS SECTION TITLE
            CreateSectionTitle(terminalPanel.transform, "SELL ITEMS");

            // 3. TABLE HEADER
            CreateTableHeader(terminalPanel.transform);

            // 4. SCROLLABLE TABLE CONTENT
            CreateScrollableTable(terminalPanel.transform);

            // 5. SELL FOOTER (Estimated total + SELL ALL button)
            CreateSellFooter(terminalPanel.transform);

            // 6. SUPPLIES SECTION TITLE
            CreateSectionTitle(terminalPanel.transform, "SUPPLIES");

            // 7. SUPPLIES ITEMS
            CreateSuppliesSection(terminalPanel.transform);

            // 8. BOTTOM PADDING
            CreateBottomPadding(terminalPanel.transform);
        }

        /// <summary>
        /// Header: "Trade Terminal" (left) | "Balance: XXX" (right) | X button (far right)
        /// </summary>
        private void CreateHeaderBar(Transform parent)
        {
            GameObject header = new GameObject("Header");
            header.transform.SetParent(parent, false);

            RectTransform headerRect = header.AddComponent<RectTransform>();
            headerRect.sizeDelta = new Vector2(0, 50);

            LayoutElement layout = header.AddComponent<LayoutElement>();
            layout.preferredHeight = 50;
            layout.minHeight = 50;

            Image bg = header.AddComponent<Image>();
            bg.color = headerBgColor;

            // Title - left
            GameObject titleObj = new GameObject("Title");
            titleObj.transform.SetParent(header.transform, false);
            RectTransform titleRect = titleObj.AddComponent<RectTransform>();
            titleRect.anchorMin = new Vector2(0, 0);
            titleRect.anchorMax = new Vector2(0.5f, 1);
            titleRect.offsetMin = new Vector2(20, 0);
            titleRect.offsetMax = Vector2.zero;

            TextMeshProUGUI titleTMP = titleObj.AddComponent<TextMeshProUGUI>();
            titleTMP.text = "Trade Terminal";
            titleTMP.fontSize = 22;
            titleTMP.color = textWhite;
            titleTMP.alignment = TextAlignmentOptions.MidlineLeft;
            titleTMP.fontStyle = FontStyles.Bold;

            // Balance - right
            GameObject balanceObj = new GameObject("Balance");
            balanceObj.transform.SetParent(header.transform, false);
            RectTransform balanceRect = balanceObj.AddComponent<RectTransform>();
            balanceRect.anchorMin = new Vector2(0.5f, 0);
            balanceRect.anchorMax = new Vector2(1, 1);
            balanceRect.offsetMin = Vector2.zero;
            balanceRect.offsetMax = new Vector2(-55, 0);

            balanceText = balanceObj.AddComponent<TextMeshProUGUI>();
            balanceText.text = "Balance: 0";
            balanceText.fontSize = 20;
            balanceText.color = textWhite;
            balanceText.alignment = TextAlignmentOptions.MidlineRight;
            balanceText.fontStyle = FontStyles.Bold;

            // Close button (X) - far right
            GameObject closeObj = new GameObject("CloseButton");
            closeObj.transform.SetParent(header.transform, false);
            RectTransform closeRect = closeObj.AddComponent<RectTransform>();
            closeRect.anchorMin = new Vector2(1, 0.5f);
            closeRect.anchorMax = new Vector2(1, 0.5f);
            closeRect.pivot = new Vector2(1, 0.5f);
            closeRect.anchoredPosition = new Vector2(-10, 0);
            closeRect.sizeDelta = new Vector2(36, 36);

            Image closeBg = closeObj.AddComponent<Image>();
            closeBg.color = closeRed;

            Button closeBtn = closeObj.AddComponent<Button>();
            closeBtn.targetGraphic = closeBg;
            closeBtn.onClick.AddListener(OnCloseClicked);

            ColorBlock closeColors = closeBtn.colors;
            closeColors.normalColor = closeRed;
            closeColors.highlightedColor = new Color(0.75f, 0.25f, 0.25f);
            closeColors.pressedColor = new Color(0.5f, 0.15f, 0.15f);
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
            xTMP.fontSize = 20;
            xTMP.color = textWhite;
            xTMP.alignment = TextAlignmentOptions.Center;
            xTMP.fontStyle = FontStyles.Bold;
            xTMP.raycastTarget = false;
        }

        /// <summary>
        /// Section title with divider lines: ——— SECTION NAME ———
        /// </summary>
        private void CreateSectionTitle(Transform parent, string title)
        {
            GameObject titleObj = new GameObject($"Section_{title}");
            titleObj.transform.SetParent(parent, false);

            RectTransform rect = titleObj.AddComponent<RectTransform>();
            rect.sizeDelta = new Vector2(0, 35);

            LayoutElement layout = titleObj.AddComponent<LayoutElement>();
            layout.preferredHeight = 35;
            layout.minHeight = 35;

            Image bg = titleObj.AddComponent<Image>();
            bg.color = panelBgColor;

            // Left divider line
            GameObject leftLine = new GameObject("LeftLine");
            leftLine.transform.SetParent(titleObj.transform, false);
            RectTransform leftLineRect = leftLine.AddComponent<RectTransform>();
            leftLineRect.anchorMin = new Vector2(0, 0.5f);
            leftLineRect.anchorMax = new Vector2(0.35f, 0.5f);
            leftLineRect.offsetMin = new Vector2(30, -1);
            leftLineRect.offsetMax = new Vector2(-10, 1);
            Image leftLineImg = leftLine.AddComponent<Image>();
            leftLineImg.color = rowSeparator;

            // Center text
            GameObject textObj = new GameObject("Text");
            textObj.transform.SetParent(titleObj.transform, false);
            RectTransform textRect = textObj.AddComponent<RectTransform>();
            textRect.anchorMin = new Vector2(0.35f, 0);
            textRect.anchorMax = new Vector2(0.65f, 1);
            textRect.offsetMin = Vector2.zero;
            textRect.offsetMax = Vector2.zero;

            TextMeshProUGUI tmp = textObj.AddComponent<TextMeshProUGUI>();
            tmp.text = title;
            tmp.fontSize = 14;
            tmp.color = textGray;
            tmp.alignment = TextAlignmentOptions.Center;
            tmp.fontStyle = FontStyles.Bold;
            tmp.characterSpacing = 4f; // Letter spacing for industrial look

            // Right divider line
            GameObject rightLine = new GameObject("RightLine");
            rightLine.transform.SetParent(titleObj.transform, false);
            RectTransform rightLineRect = rightLine.AddComponent<RectTransform>();
            rightLineRect.anchorMin = new Vector2(0.65f, 0.5f);
            rightLineRect.anchorMax = new Vector2(1, 0.5f);
            rightLineRect.offsetMin = new Vector2(10, -1);
            rightLineRect.offsetMax = new Vector2(-30, 1);
            Image rightLineImg = rightLine.AddComponent<Image>();
            rightLineImg.color = rowSeparator;
        }

        /// <summary>
        /// Table header row: Item | Have | Value
        /// </summary>
        private void CreateTableHeader(Transform parent)
        {
            GameObject headerRow = new GameObject("TableHeader");
            headerRow.transform.SetParent(parent, false);

            RectTransform rect = headerRow.AddComponent<RectTransform>();
            rect.sizeDelta = new Vector2(0, 30);

            LayoutElement layout = headerRow.AddComponent<LayoutElement>();
            layout.preferredHeight = 30;
            layout.minHeight = 30;

            Image bg = headerRow.AddComponent<Image>();
            bg.color = rowBgDark;

            // Item column header (left)
            GameObject itemHeader = new GameObject("ItemHeader");
            itemHeader.transform.SetParent(headerRow.transform, false);
            RectTransform itemRect = itemHeader.AddComponent<RectTransform>();
            itemRect.anchorMin = new Vector2(0, 0);
            itemRect.anchorMax = new Vector2(0.5f, 1);
            itemRect.offsetMin = new Vector2(20, 0);
            itemRect.offsetMax = Vector2.zero;

            TextMeshProUGUI itemTMP = itemHeader.AddComponent<TextMeshProUGUI>();
            itemTMP.text = "Item";
            itemTMP.fontSize = 14;
            itemTMP.color = textGray;
            itemTMP.alignment = TextAlignmentOptions.MidlineLeft;

            // Have column header (center)
            GameObject haveHeader = new GameObject("HaveHeader");
            haveHeader.transform.SetParent(headerRow.transform, false);
            RectTransform haveRect = haveHeader.AddComponent<RectTransform>();
            haveRect.anchorMin = new Vector2(0.5f, 0);
            haveRect.anchorMax = new Vector2(0.7f, 1);
            haveRect.offsetMin = Vector2.zero;
            haveRect.offsetMax = Vector2.zero;

            TextMeshProUGUI haveTMP = haveHeader.AddComponent<TextMeshProUGUI>();
            haveTMP.text = "Have";
            haveTMP.fontSize = 14;
            haveTMP.color = textGray;
            haveTMP.alignment = TextAlignmentOptions.Center;

            // Value column header (right)
            GameObject valueHeader = new GameObject("ValueHeader");
            valueHeader.transform.SetParent(headerRow.transform, false);
            RectTransform valueRect = valueHeader.AddComponent<RectTransform>();
            valueRect.anchorMin = new Vector2(0.7f, 0);
            valueRect.anchorMax = new Vector2(1, 1);
            valueRect.offsetMin = Vector2.zero;
            valueRect.offsetMax = new Vector2(-20, 0);

            TextMeshProUGUI valueTMP = valueHeader.AddComponent<TextMeshProUGUI>();
            valueTMP.text = "Value";
            valueTMP.fontSize = 14;
            valueTMP.color = textGray;
            valueTMP.alignment = TextAlignmentOptions.Center;
        }

        /// <summary>
        /// Scrollable table for sell items
        /// </summary>
        private void CreateScrollableTable(Transform parent)
        {
            GameObject scrollArea = new GameObject("ScrollArea");
            scrollArea.transform.SetParent(parent, false);

            RectTransform rect = scrollArea.AddComponent<RectTransform>();
            rect.sizeDelta = new Vector2(0, 180);

            LayoutElement layout = scrollArea.AddComponent<LayoutElement>();
            layout.flexibleHeight = 1;
            layout.minHeight = 180;
            layout.preferredHeight = 180;

            Image bg = scrollArea.AddComponent<Image>();
            bg.color = rowBgDark;

            tableScrollRect = scrollArea.AddComponent<ScrollRect>();
            tableScrollRect.horizontal = false;
            tableScrollRect.vertical = true;
            tableScrollRect.scrollSensitivity = 25f;

            // Viewport
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

            // Content
            GameObject content = new GameObject("Content");
            content.transform.SetParent(viewport.transform, false);
            RectTransform contentRect = content.AddComponent<RectTransform>();
            contentRect.anchorMin = new Vector2(0, 1);
            contentRect.anchorMax = new Vector2(1, 1);
            contentRect.pivot = new Vector2(0.5f, 1);
            contentRect.anchoredPosition = Vector2.zero;

            VerticalLayoutGroup vlg = content.AddComponent<VerticalLayoutGroup>();
            vlg.padding = new RectOffset(0, 0, 0, 0);
            vlg.spacing = 0;
            vlg.childControlWidth = true;
            vlg.childControlHeight = true;
            vlg.childForceExpandWidth = true;
            vlg.childForceExpandHeight = false;

            ContentSizeFitter csf = content.AddComponent<ContentSizeFitter>();
            csf.verticalFit = ContentSizeFitter.FitMode.PreferredSize;

            tableScrollRect.viewport = vpRect;
            tableScrollRect.content = contentRect;
            tableContent = content.transform;
        }

        /// <summary>
        /// Sell footer: "Estimated total: XXX Credits" (left) | [SELL ALL] (right)
        /// </summary>
        private void CreateSellFooter(Transform parent)
        {
            GameObject footer = new GameObject("SellFooter");
            footer.transform.SetParent(parent, false);

            RectTransform rect = footer.AddComponent<RectTransform>();
            rect.sizeDelta = new Vector2(0, 55); // Taller footer

            LayoutElement layout = footer.AddComponent<LayoutElement>();
            layout.preferredHeight = 55;
            layout.minHeight = 55;

            Image bg = footer.AddComponent<Image>();
            bg.color = headerBgColor;

            // Top separator line
            GameObject topSep = new GameObject("TopSeparator");
            topSep.transform.SetParent(footer.transform, false);
            RectTransform topSepRect = topSep.AddComponent<RectTransform>();
            topSepRect.anchorMin = new Vector2(0, 1);
            topSepRect.anchorMax = new Vector2(1, 1);
            topSepRect.offsetMin = new Vector2(0, -2);
            topSepRect.offsetMax = Vector2.zero;
            Image topSepImg = topSep.AddComponent<Image>();
            topSepImg.color = frameBorderInner;

            // Estimated total text - left side (larger, with gold number)
            GameObject totalObj = new GameObject("EstimatedTotal");
            totalObj.transform.SetParent(footer.transform, false);
            RectTransform totalRect = totalObj.AddComponent<RectTransform>();
            totalRect.anchorMin = new Vector2(0, 0);
            totalRect.anchorMax = new Vector2(0.55f, 1);
            totalRect.offsetMin = new Vector2(25, 0);
            totalRect.offsetMax = Vector2.zero;

            estimatedTotalText = totalObj.AddComponent<TextMeshProUGUI>();
            estimatedTotalText.text = "Estimated total: <b><color=#D9AD40>0</color></b> Credits";
            estimatedTotalText.fontSize = 18; // Larger
            estimatedTotalText.color = textWhite;
            estimatedTotalText.alignment = TextAlignmentOptions.MidlineLeft;

            // SELL ALL button - right side (LARGER, more prominent)
            GameObject btnObj = new GameObject("SellAllButton");
            btnObj.transform.SetParent(footer.transform, false);
            RectTransform btnRect = btnObj.AddComponent<RectTransform>();
            btnRect.anchorMin = new Vector2(1, 0.5f);
            btnRect.anchorMax = new Vector2(1, 0.5f);
            btnRect.pivot = new Vector2(1, 0.5f);
            btnRect.anchoredPosition = new Vector2(-20, 0);
            btnRect.sizeDelta = new Vector2(160, 40); // Larger button

            Image btnBg = btnObj.AddComponent<Image>();
            btnBg.color = buttonTeal;

            // Button glow/edge effect
            Outline btnGlow = btnObj.AddComponent<Outline>();
            btnGlow.effectColor = buttonTealGlow;
            btnGlow.effectDistance = new Vector2(2, 2);

            sellAllButton = btnObj.AddComponent<Button>();
            sellAllButton.targetGraphic = btnBg;
            sellAllButton.onClick.AddListener(OnSellAllClicked);

            ColorBlock btnColors = sellAllButton.colors;
            btnColors.normalColor = buttonTeal;
            btnColors.highlightedColor = buttonTealHover;
            btnColors.pressedColor = new Color(0.22f, 0.42f, 0.42f);
            btnColors.disabledColor = new Color(0.25f, 0.25f, 0.25f, 0.6f);
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
            btnTMP.fontSize = 18; // Larger text
            btnTMP.color = textWhite;
            btnTMP.alignment = TextAlignmentOptions.Center;
            btnTMP.fontStyle = FontStyles.Bold;
            btnTMP.raycastTarget = false;
        }

        /// <summary>
        /// Supplies section with Energy Drink and Lamp rows (NOT cards)
        /// </summary>
        private void CreateSuppliesSection(Transform parent)
        {
            // Energy Drink row
            CreateSupplyRow(parent, "Energy Drink", DRINK_COST, MAX_DRINKS, true);

            // Lamp row
            CreateSupplyRow(parent, "Lamp", LAMP_COST, MAX_LAMPS, false);
        }

        /// <summary>
        /// Single supply row: Icon | Name, Price, Owned | BUY button
        /// </summary>
        private void CreateSupplyRow(Transform parent, string itemName, int price, int maxOwned, bool isDrink)
        {
            GameObject row = new GameObject($"Supply_{itemName}");
            row.transform.SetParent(parent, false);

            RectTransform rect = row.AddComponent<RectTransform>();
            rect.sizeDelta = new Vector2(0, 65); // Taller rows

            LayoutElement layout = row.AddComponent<LayoutElement>();
            layout.preferredHeight = 65;
            layout.minHeight = 65;

            Image bg = row.AddComponent<Image>();
            bg.color = isDrink ? rowBgDark : rowBgLight;

            // Bottom separator line
            GameObject separator = new GameObject("Separator");
            separator.transform.SetParent(row.transform, false);
            RectTransform sepRect = separator.AddComponent<RectTransform>();
            sepRect.anchorMin = new Vector2(0, 0);
            sepRect.anchorMax = new Vector2(1, 0);
            sepRect.offsetMin = new Vector2(15, 0);
            sepRect.offsetMax = new Vector2(-15, 1);
            Image sepImg = separator.AddComponent<Image>();
            sepImg.color = rowSeparator;

            // Icon background (slot-like appearance)
            GameObject iconBg = new GameObject("IconBg");
            iconBg.transform.SetParent(row.transform, false);
            RectTransform iconBgRect = iconBg.AddComponent<RectTransform>();
            iconBgRect.anchorMin = new Vector2(0, 0.5f);
            iconBgRect.anchorMax = new Vector2(0, 0.5f);
            iconBgRect.pivot = new Vector2(0, 0.5f);
            iconBgRect.anchoredPosition = new Vector2(15, 0);
            iconBgRect.sizeDelta = new Vector2(48, 48);
            Image iconBgImg = iconBg.AddComponent<Image>();
            iconBgImg.color = new Color(0.12f, 0.11f, 0.10f, 0.8f); // Dark slot

            // Icon (left)
            GameObject iconObj = new GameObject("Icon");
            iconObj.transform.SetParent(row.transform, false);
            RectTransform iconRect = iconObj.AddComponent<RectTransform>();
            iconRect.anchorMin = new Vector2(0, 0.5f);
            iconRect.anchorMax = new Vector2(0, 0.5f);
            iconRect.pivot = new Vector2(0, 0.5f);
            iconRect.anchoredPosition = new Vector2(17, 0);
            iconRect.sizeDelta = new Vector2(44, 44);

            Image iconImg = iconObj.AddComponent<Image>();
            iconImg.preserveAspect = true;

            // Try to load icon from Resources
            if (isDrink)
            {
                Sprite drinkSprite = Resources.Load<Sprite>("Icons/icon_energy_drink");
                if (drinkSprite != null)
                {
                    iconImg.sprite = drinkSprite;
                    iconImg.color = Color.white;
                }
                else
                {
                    iconImg.color = new Color(0.35f, 0.72f, 0.45f); // Green fallback
                }
            }
            else
            {
                // Lamp icon
                Sprite lampSprite = Resources.Load<Sprite>("Icons/icon_lamp");
                if (lampSprite != null)
                {
                    iconImg.sprite = lampSprite;
                    iconImg.color = Color.white;
                }
                else
                {
                    iconImg.color = new Color(0.85f, 0.70f, 0.35f); // Warm yellow fallback
                }
            }

            // Name and info (center-left)
            GameObject infoObj = new GameObject("Info");
            infoObj.transform.SetParent(row.transform, false);
            RectTransform infoRect = infoObj.AddComponent<RectTransform>();
            infoRect.anchorMin = new Vector2(0, 0);
            infoRect.anchorMax = new Vector2(0.65f, 1);
            infoRect.offsetMin = new Vector2(75, 8);
            infoRect.offsetMax = new Vector2(0, -8);

            VerticalLayoutGroup infoVLG = infoObj.AddComponent<VerticalLayoutGroup>();
            infoVLG.spacing = 1;
            infoVLG.childControlWidth = true;
            infoVLG.childControlHeight = true;
            infoVLG.childForceExpandWidth = true;
            infoVLG.childForceExpandHeight = true;

            // Item name
            GameObject nameObj = new GameObject("Name");
            nameObj.transform.SetParent(infoObj.transform, false);
            TextMeshProUGUI nameTMP = nameObj.AddComponent<TextMeshProUGUI>();
            nameTMP.text = itemName;
            nameTMP.fontSize = 15;
            nameTMP.color = textWhite;
            nameTMP.fontStyle = FontStyles.Bold;

            // Price (gold colored)
            GameObject priceObj = new GameObject("Price");
            priceObj.transform.SetParent(infoObj.transform, false);
            TextMeshProUGUI priceTMP = priceObj.AddComponent<TextMeshProUGUI>();
            priceTMP.text = $"Price: <color=#D9AD40>{price}</color> cr";
            priceTMP.fontSize = 12;
            priceTMP.color = textGray;

            // Owned
            GameObject ownedObj = new GameObject("Owned");
            ownedObj.transform.SetParent(infoObj.transform, false);
            TextMeshProUGUI ownedTMP = ownedObj.AddComponent<TextMeshProUGUI>();
            ownedTMP.text = $"Owned: <b>0</b> / {maxOwned}";
            ownedTMP.fontSize = 12;
            ownedTMP.color = textWhite;

            if (isDrink)
                drinkOwnedText = ownedTMP;
            else
                lampOwnedText = ownedTMP;

            // BUY button (right) - slightly smaller than SELL ALL
            GameObject btnObj = new GameObject("BuyButton");
            btnObj.transform.SetParent(row.transform, false);
            RectTransform btnRect = btnObj.AddComponent<RectTransform>();
            btnRect.anchorMin = new Vector2(1, 0.5f);
            btnRect.anchorMax = new Vector2(1, 0.5f);
            btnRect.pivot = new Vector2(1, 0.5f);
            btnRect.anchoredPosition = new Vector2(-20, 0);
            btnRect.sizeDelta = new Vector2(85, 36);

            Image btnBg = btnObj.AddComponent<Image>();
            btnBg.color = buttonTeal;

            // Subtle button outline
            Outline btnOutline = btnObj.AddComponent<Outline>();
            btnOutline.effectColor = new Color(0.35f, 0.6f, 0.58f, 0.4f);
            btnOutline.effectDistance = new Vector2(1, 1);

            Button btn = btnObj.AddComponent<Button>();
            btn.targetGraphic = btnBg;

            ColorBlock btnColors = btn.colors;
            btnColors.normalColor = buttonTeal;
            btnColors.highlightedColor = buttonTealHover;
            btnColors.pressedColor = new Color(0.2f, 0.45f, 0.45f);
            btnColors.disabledColor = new Color(0.3f, 0.3f, 0.3f, 0.5f);
            btn.colors = btnColors;

            if (isDrink)
            {
                buyDrinkButton = btn;
                btn.onClick.AddListener(OnBuyDrinkClicked);
            }
            else
            {
                buyLampButton = btn;
                btn.onClick.AddListener(OnBuyLampClicked);
            }

            GameObject btnTextObj = new GameObject("Text");
            btnTextObj.transform.SetParent(btnObj.transform, false);
            RectTransform btnTextRect = btnTextObj.AddComponent<RectTransform>();
            btnTextRect.anchorMin = Vector2.zero;
            btnTextRect.anchorMax = Vector2.one;
            btnTextRect.offsetMin = Vector2.zero;
            btnTextRect.offsetMax = Vector2.zero;

            TextMeshProUGUI btnTMP = btnTextObj.AddComponent<TextMeshProUGUI>();
            btnTMP.text = "BUY";
            btnTMP.fontSize = 14;
            btnTMP.color = textWhite;
            btnTMP.alignment = TextAlignmentOptions.Center;
            btnTMP.fontStyle = FontStyles.Bold;
            btnTMP.raycastTarget = false;
        }

        /// <summary>
        /// Bottom padding for visual balance
        /// </summary>
        private void CreateBottomPadding(Transform parent)
        {
            GameObject padding = new GameObject("BottomPadding");
            padding.transform.SetParent(parent, false);

            RectTransform rect = padding.AddComponent<RectTransform>();
            rect.sizeDelta = new Vector2(0, 15);

            LayoutElement layout = padding.AddComponent<LayoutElement>();
            layout.preferredHeight = 15;
            layout.minHeight = 15;

            Image bg = padding.AddComponent<Image>();
            bg.color = panelBgColor;
        }

        /// <summary>
        /// Creates a single table row for a sellable item
        /// </summary>
        private void CreateTableRow(SellableItem sellable, int index)
        {
            if (tableContent == null) return;

            string itemName = sellable.isDust ? "Dust" : (sellable.item != null ? sellable.item.itemName : "Unknown");

            GameObject row = new GameObject($"Row_{itemName}");
            row.transform.SetParent(tableContent, false);

            RectTransform rect = row.AddComponent<RectTransform>();
            rect.sizeDelta = new Vector2(0, 50); // Slightly taller rows

            LayoutElement layout = row.AddComponent<LayoutElement>();
            layout.preferredHeight = 50;
            layout.minHeight = 50;

            // Alternating row colors
            Image bg = row.AddComponent<Image>();
            bg.color = (index % 2 == 0) ? rowBgDark : rowBgLight;

            // Bottom separator line
            GameObject separator = new GameObject("Separator");
            separator.transform.SetParent(row.transform, false);
            RectTransform sepRect = separator.AddComponent<RectTransform>();
            sepRect.anchorMin = new Vector2(0, 0);
            sepRect.anchorMax = new Vector2(1, 0);
            sepRect.offsetMin = new Vector2(15, 0);
            sepRect.offsetMax = new Vector2(-15, 1);
            Image sepImg = separator.AddComponent<Image>();
            sepImg.color = rowSeparator;

            // Icon background (slot-like appearance)
            GameObject iconBg = new GameObject("IconBg");
            iconBg.transform.SetParent(row.transform, false);
            RectTransform iconBgRect = iconBg.AddComponent<RectTransform>();
            iconBgRect.anchorMin = new Vector2(0, 0.5f);
            iconBgRect.anchorMax = new Vector2(0, 0.5f);
            iconBgRect.pivot = new Vector2(0, 0.5f);
            iconBgRect.anchoredPosition = new Vector2(60, 0);
            iconBgRect.sizeDelta = new Vector2(50, 44); // Compact background
            Image iconBgImg = iconBg.AddComponent<Image>();
            iconBgImg.color = new Color(0.12f, 0.11f, 0.10f, 0.8f); // Dark slot background

            // Icon - normal size, positioned right
            GameObject iconObj = new GameObject("Icon");
            iconObj.transform.SetParent(row.transform, false);
            RectTransform iconRect = iconObj.AddComponent<RectTransform>();
            iconRect.anchorMin = new Vector2(0, 0.5f);
            iconRect.anchorMax = new Vector2(0, 0.5f);
            iconRect.pivot = new Vector2(0, 0.5f);
            iconRect.anchoredPosition = new Vector2(63, 0);
            iconRect.sizeDelta = new Vector2(44, 40); // Normal size icon

            Image iconImg = iconObj.AddComponent<Image>();
            iconImg.type = Image.Type.Simple;
            iconImg.preserveAspect = true;
            iconImg.raycastTarget = false;

            if (sellable.isDust)
            {
                // Try to load dirt icon for dust
                Sprite dustIcon = UnityEngine.Resources.Load<Sprite>("Icons/icon_dirt");
                if (dustIcon != null)
                {
                    iconImg.sprite = dustIcon;
                    iconImg.color = Color.white;
                }
                else
                {
                    iconImg.color = new Color(0.72f, 0.58f, 0.38f); // Sandy brown fallback
                }
            }
            else if (sellable.item != null && sellable.item.icon != null)
            {
                iconImg.sprite = sellable.item.icon;
                iconImg.color = Color.white;
            }
            else
            {
                // Try to load icon from Resources if the item doesn't have one
                if (sellable.item != null && sellable.item is MaterialItemSO matItem && matItem.sourceResource.HasValue)
                {
                    string resourceName = matItem.sourceResource.Value.ToString().ToLower();
                    string iconPath = $"Icons/icon_{resourceName}";
                    Sprite loadedIcon = UnityEngine.Resources.Load<Sprite>(iconPath);
                    if (loadedIcon != null)
                    {
                        iconImg.sprite = loadedIcon;
                        iconImg.color = Color.white;
                    }
                    else
                    {
                        iconImg.color = GetResourceColor(matItem.sourceResource.Value);
                    }
                }
                else
                {
                    iconImg.color = new Color(0.5f, 0.5f, 0.5f);
                }
            }

            // Name (left of center) - After icon
            GameObject nameObj = new GameObject("Name");
            nameObj.transform.SetParent(row.transform, false);
            RectTransform nameRect = nameObj.AddComponent<RectTransform>();
            nameRect.anchorMin = new Vector2(0, 0);
            nameRect.anchorMax = new Vector2(0.5f, 1);
            nameRect.offsetMin = new Vector2(120, 0); // After icon
            nameRect.offsetMax = Vector2.zero;

            TextMeshProUGUI nameTMP = nameObj.AddComponent<TextMeshProUGUI>();
            nameTMP.text = itemName;
            nameTMP.fontSize = 16;
            nameTMP.color = textWhite;
            nameTMP.alignment = TextAlignmentOptions.MidlineLeft;

            // Have count (center)
            GameObject haveObj = new GameObject("Have");
            haveObj.transform.SetParent(row.transform, false);
            RectTransform haveRect = haveObj.AddComponent<RectTransform>();
            haveRect.anchorMin = new Vector2(0.5f, 0);
            haveRect.anchorMax = new Vector2(0.7f, 1);
            haveRect.offsetMin = Vector2.zero;
            haveRect.offsetMax = Vector2.zero;

            TextMeshProUGUI haveTMP = haveObj.AddComponent<TextMeshProUGUI>();
            haveTMP.text = sellable.totalAmount.ToString();
            haveTMP.fontSize = 16;
            haveTMP.color = textWhite;
            haveTMP.alignment = TextAlignmentOptions.Center;

            // Value (right, gold color)
            GameObject valueObj = new GameObject("Value");
            valueObj.transform.SetParent(row.transform, false);
            RectTransform valueRect = valueObj.AddComponent<RectTransform>();
            valueRect.anchorMin = new Vector2(0.7f, 0);
            valueRect.anchorMax = new Vector2(1, 1);
            valueRect.offsetMin = Vector2.zero;
            valueRect.offsetMax = new Vector2(-20, 0);

            int rowValue = sellable.isDust ? (DustManager.Instance != null ? DustManager.Instance.GetDustValue() : 0) : sellable.totalValue;

            TextMeshProUGUI valueTMP = valueObj.AddComponent<TextMeshProUGUI>();
            valueTMP.text = rowValue.ToString();
            valueTMP.fontSize = 16;
            valueTMP.color = textGold;
            valueTMP.alignment = TextAlignmentOptions.Center;
            valueTMP.fontStyle = FontStyles.Bold;

            tableRows.Add(row);
        }

        /// <summary>
        /// Empty message when nothing to sell
        /// </summary>
        private void CreateEmptyMessage()
        {
            if (tableContent == null) return;

            GameObject msgObj = new GameObject("EmptyMessage");
            msgObj.transform.SetParent(tableContent, false);

            RectTransform rect = msgObj.AddComponent<RectTransform>();
            rect.sizeDelta = new Vector2(0, 60);

            LayoutElement layout = msgObj.AddComponent<LayoutElement>();
            layout.preferredHeight = 60;
            layout.minHeight = 60;

            // Text child for proper rendering
            GameObject textObj = new GameObject("Text");
            textObj.transform.SetParent(msgObj.transform, false);
            RectTransform textRect = textObj.AddComponent<RectTransform>();
            textRect.anchorMin = Vector2.zero;
            textRect.anchorMax = Vector2.one;
            textRect.offsetMin = Vector2.zero;
            textRect.offsetMax = Vector2.zero;

            TextMeshProUGUI tmp = textObj.AddComponent<TextMeshProUGUI>();
            tmp.text = "No items to sell";
            tmp.fontSize = 16;
            tmp.color = textGray;
            tmp.alignment = TextAlignmentOptions.Center;
            tmp.fontStyle = FontStyles.Italic;

            tableRows.Add(msgObj);
        }

        // ==================== PUBLIC API ====================

        public void ShowUI(TradeTerminal terminal)
        {
            currentTerminal = terminal;
            EnsureInventorySellService();

            // Force rebuild to ensure fresh UI
            BuildUI();

            if (terminalPanel == null)
            {
                Debug.LogError("[TradeTerminalUI] terminalPanel is NULL!");
                return;
            }

            terminalPanel.SetActive(true);

            // Force layout rebuild
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

            RefreshTable();
            UpdateBalanceDisplay();
            UpdateSuppliesDisplay();
        }

        public void HideUI()
        {
            if (terminalPanel != null) terminalPanel.SetActive(false);
            currentTerminal = null;
            UIState.IsMachineUIOpen = false;
            Cursor.lockState = CursorLockMode.Locked;
            Cursor.visible = false;

            // Ensure game is unpaused when closing the terminal
            Time.timeScale = 1f;
        }

        private void RefreshTable()
        {
            foreach (var row in tableRows)
            {
                if (row != null) Destroy(row);
            }
            tableRows.Clear();
            sellableItems.Clear();

            // Add dust as a normal row
            if (DustManager.Instance != null)
            {
                float dustAmount = DustManager.Instance.GetDust();
                int dustValue = DustManager.Instance.GetDustValue();
                if (dustAmount > 0 && dustValue > 0)
                {
                    sellableItems.Add(new SellableItem(Mathf.FloorToInt(dustAmount), dustValue));
                }
            }

            // Add inventory resources
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

            // Create rows
            for (int i = 0; i < sellableItems.Count; i++)
            {
                CreateTableRow(sellableItems[i], i);
            }

            if (sellableItems.Count == 0)
            {
                CreateEmptyMessage();
            }

            UpdateEstimatedTotal();
        }

        private void UpdateEstimatedTotal()
        {
            int totalValue = 0;

            if (DustManager.Instance != null)
                totalValue += DustManager.Instance.GetDustValue();

            foreach (var item in sellableItems)
            {
                if (!item.isDust)
                    totalValue += item.totalValue;
            }

            if (estimatedTotalText != null)
                estimatedTotalText.text = $"Estimated total: <b><color=#D9AD40>{totalValue}</color></b> Credits";

            if (sellAllButton != null)
                sellAllButton.interactable = totalValue > 0 && !isSellInProgress;
        }

        private void UpdateBalanceDisplay()
        {
            if (balanceText != null && CurrencyManager.Instance != null)
                balanceText.text = $"Balance: {CurrencyManager.Instance.CurrentAmount:N0}";
        }

        private void UpdateSuppliesDisplay()
        {
            int drinkCount = EnergyManager.Instance != null ? EnergyManager.Instance.DrinkCount : 0;
            if (drinkOwnedText != null)
                drinkOwnedText.text = $"Owned: <b>{drinkCount}</b> / {MAX_DRINKS}";

            bool canBuyDrink = drinkCount < MAX_DRINKS &&
                               CurrencyManager.Instance != null &&
                               CurrencyManager.Instance.CurrentAmount >= DRINK_COST;
            if (buyDrinkButton != null)
                buyDrinkButton.interactable = canBuyDrink;

            int lampCount = LampPlacementController.Instance != null ? LampPlacementController.Instance.LampsAvailable : 0;
            if (lampOwnedText != null)
                lampOwnedText.text = $"Owned: <b>{lampCount}</b> / {MAX_LAMPS}";

            bool canBuyLamp = lampCount < MAX_LAMPS &&
                              CurrencyManager.Instance != null &&
                              CurrencyManager.Instance.CurrentAmount >= LAMP_COST;
            if (buyLampButton != null)
                buyLampButton.interactable = canBuyLamp;
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
            if (currentTerminal != null)
                currentTerminal.CloseTerminal();
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

            // Sell dust
            if (DustManager.Instance != null)
            {
                int dustGained = DustManager.Instance.SellAllDust();
                totalGained += dustGained;
            }

            // Sell inventory
            if (InventorySellService.Instance != null && InventorySystem.Instance != null)
            {
                int inventoryGained = InventorySellService.Instance.SellAllResourcesOnly(InventorySystem.Instance);
                totalGained += inventoryGained;
            }

            if (totalGained > 0)
            {
                if (currentTerminal != null)
                    currentTerminal.PlaySellSound();

                ShowSellToast(totalGained);
                RefreshTable();
                UpdateBalanceDisplay();
                UpdateSuppliesDisplay(); // Refresh buy buttons after selling

                if (InventoryUI.InventoryUIManager.Instance != null)
                    InventoryUI.InventoryUIManager.Instance.RefreshAllSlots();
            }

            StartCoroutine(ReEnableSellButton(0.3f));
        }

        private void OnBuyDrinkClicked()
        {
            if (CurrencyManager.Instance == null || EnergyManager.Instance == null) return;

            int currentDrinks = EnergyManager.Instance.DrinkCount;
            if (currentDrinks >= MAX_DRINKS) return;
            if (CurrencyManager.Instance.CurrentAmount < DRINK_COST) return;

            CurrencyManager.Instance.Spend(DRINK_COST);
            EnergyManager.Instance.AddDrinks(1);

            if (currentTerminal != null)
                currentTerminal.PlaySellSound();

            UpdateBalanceDisplay();
            UpdateSuppliesDisplay();
        }

        private void OnBuyLampClicked()
        {
            if (CurrencyManager.Instance == null || LampPlacementController.Instance == null) return;

            int currentLamps = LampPlacementController.Instance.LampsAvailable;
            if (currentLamps >= MAX_LAMPS) return;
            if (CurrencyManager.Instance.CurrentAmount < LAMP_COST) return;

            CurrencyManager.Instance.Spend(LAMP_COST);
            LampPlacementController.Instance.AddLamps(1);

            if (currentTerminal != null)
                currentTerminal.PlaySellSound();

            UpdateBalanceDisplay();
            UpdateSuppliesDisplay();
        }

        private IEnumerator ReEnableSellButton(float delay)
        {
            yield return new WaitForSeconds(delay);
            isSellInProgress = false;
            UpdateEstimatedTotal();
        }

        private void ShowSellToast(int credits)
        {
            string message = $"+{credits:N0} Credits";

            var objectiveUI = FindObjectOfType<ObjectiveUIController>();
            if (objectiveUI != null)
            {
                objectiveUI.ShowToast(message, 2f);
                return;
            }

            var notifSystem = FindObjectOfType<PickupNotificationSystem>();
            if (notifSystem != null)
                notifSystem.ShowNotification(message);
        }

        /// <summary>
        /// Get color based on resource type for items without icons.
        /// </summary>
        private Color GetResourceColor(ResourceType resourceType)
        {
            return resourceType switch
            {
                ResourceType.Dirt => new Color(0.55f, 0.40f, 0.25f),
                ResourceType.Clay => new Color(0.75f, 0.55f, 0.40f),
                ResourceType.Coal => new Color(0.15f, 0.15f, 0.15f),
                ResourceType.IronOre => new Color(0.60f, 0.45f, 0.40f),
                ResourceType.Copper => new Color(0.85f, 0.55f, 0.30f),
                ResourceType.Silver => new Color(0.80f, 0.80f, 0.85f),
                ResourceType.Gold => new Color(1.0f, 0.85f, 0.30f),
                ResourceType.IronIngot => new Color(0.55f, 0.55f, 0.60f),
                ResourceType.CopperIngot => new Color(0.90f, 0.60f, 0.35f),
                ResourceType.SilverIngot => new Color(0.90f, 0.90f, 0.95f),
                ResourceType.GoldIngot => new Color(1.0f, 0.90f, 0.45f),
                _ => new Color(0.65f, 0.55f, 0.45f)
            };
        }
    }
}
