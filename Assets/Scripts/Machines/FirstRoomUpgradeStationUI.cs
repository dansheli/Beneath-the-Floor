using UnityEngine;
using UnityEngine.UI;
using UnityEngine.SceneManagement;
using TMPro;
using System.Collections.Generic;
using BeneathTheFloor.UI;
using BeneathTheFloor.Economy;
using BeneathTheFloor.Energy;
using BeneathTheFloor.Player;
using BeneathTheFloor.GameFlow;

namespace BeneathTheFloor.Machines
{
    public class FirstRoomUpgradeStationUI : MonoBehaviour
    {
        public static FirstRoomUpgradeStationUI Instance { get; private set; }

        private GameObject mainPanel;
        private FirstRoomUpgradeStation currentStation;

        // ===== TOOL UPGRADE TRACKING =====
        // Tool Power & Radius: Level 0 = base, 1 = first upgrade, 2 = max
        public static int ToolPowerLevel { get; private set; } = 0;
        public const int TOOL_POWER_MAX_LEVEL = 2;

        // Jetpack Efficiency: Level 0 = 15/sec, 1 = 12/sec, 2 = 9/sec, 3 = 5/sec
        public static int JetpackEfficiencyLevel { get; private set; } = 0;
        public const int JETPACK_EFFICIENCY_MAX_LEVEL = 3;
        private static readonly float[] jetpackDrainPerLevel = { 15f, 12f, 9f, 5f };
        private static readonly int[] jetpackEfficiencyCosts = { 625, 1125, 1875 };

        // Power multiplier values per level
        private static readonly float[] toolPowerMultipliers = { 1.0f, 1.5f, 2.0f };

        // ===== ENERGY UPGRADE TRACKING =====
        // Energy upgrades are tracked by EnergyManager.EnergyUpgradeLevel
        // Basement station: L0→L5 (levels 0-5)
        // FirstRoom station: continues from current level, adds L6→L7 (exclusive)
        //
        // Values (from EnergyManager formula: base + level * perUpgrade):
        // L0: 100 energy, 4/s regen (base)
        // L1: 125 energy, 6/s regen
        // L2: 150 energy, 8/s regen
        // L3: 175 energy, 10/s regen
        // L4: 200 energy, 12/s regen
        // L5: 225 energy, 14/s regen (basement max)
        // L6: 250 energy, 16/s regen (FirstRoom exclusive)
        // L7: 275 energy, 18/s regen (FirstRoom exclusive)

        public const int BASEMENT_MAX_LEVEL = 5;  // Basement can upgrade to L5
        public const int TOTAL_MAX_LEVEL = 7;     // FirstRoom adds L6, L7

        // Costs for ALL energy upgrades (L0→L1 through L6→L7)
        // Basement costs (L0-L5): 15, 188, 350, 563, 875
        // FirstRoom exclusive (L6-L7): 1250, 1875
        private static readonly int[] energyUpgradeCosts = { 15, 188, 350, 563, 875, 1250, 1875 };

        // Tab system
        private enum UpgradeTab { Tools, Energy, Jetpack, Robots, Systems }
        private UpgradeTab currentTab = UpgradeTab.Tools;
        private List<Button> tabButtons = new List<Button>();
        private GameObject contentArea;

        // Sci-Fi color scheme matching FirstRoomSellStationUI
        private readonly Color panelBgColor = new Color(0.08f, 0.10f, 0.12f, 0.98f);
        private readonly Color headerBgColor = new Color(0.04f, 0.06f, 0.08f, 1f);
        private readonly Color sectionBgColor = new Color(0.06f, 0.08f, 0.10f, 1f);
        private readonly Color rowBgDark = new Color(0.08f, 0.10f, 0.12f, 1f);
        private readonly Color rowBgLight = new Color(0.10f, 0.12f, 0.14f, 1f);
        private readonly Color frameBorderCyan = new Color(0.0f, 0.75f, 0.85f, 0.8f);
        private readonly Color textWhite = new Color(0.92f, 0.94f, 0.96f);
        private readonly Color textCyan = new Color(0.4f, 0.9f, 1.0f);
        private readonly Color textGray = new Color(0.45f, 0.50f, 0.55f);
        private readonly Color textOrange = new Color(1.0f, 0.6f, 0.2f);
        private readonly Color tabActiveColor = new Color(0.0f, 0.55f, 0.65f);
        private readonly Color tabInactiveColor = new Color(0.15f, 0.17f, 0.20f);
        private readonly Color buttonCyan = new Color(0.0f, 0.55f, 0.65f);
        private readonly Color buttonGreen = new Color(0.2f, 0.6f, 0.3f);
        private readonly Color lockedColor = new Color(0.3f, 0.3f, 0.35f);

        private void Awake()
        {
            if (Instance == null) Instance = this;
            else { Destroy(gameObject); return; }
        }

        private void Start()
        {
            // Don't hide panel in Start - ShowUI handles visibility
        }

        private void OnDestroy()
        {
            if (Instance == this) Instance = null;
        }

        public bool ShowUI(FirstRoomUpgradeStation station)
        {
            currentStation = station;

            // Energy levels are read directly from EnergyManager when building the UI
            // No need to sync - we just read EnergyManager.Instance.EnergyUpgradeLevel

            BuildUI();

            if (mainPanel == null)
            {
                Debug.LogError("[FirstRoomUpgradeStationUI] mainPanel is NULL!");
                return false;
            }

            mainPanel.SetActive(true);

            Cursor.lockState = CursorLockMode.None;
            Cursor.visible = true;
            UIState.IsMachineUIOpen = true;

            SelectTab(UpgradeTab.Tools);
            return true;
        }

        /// <summary>
        /// Get the current energy upgrade level from EnergyManager.
        /// </summary>
        private int GetCurrentEnergyLevel()
        {
            if (EnergyManager.Instance == null) return 0;
            return EnergyManager.Instance.EnergyUpgradeLevel;
        }

        public void HideUI()
        {
            if (mainPanel != null) mainPanel.SetActive(false);
            currentStation = null;
            UIState.IsMachineUIOpen = false;
            Cursor.lockState = CursorLockMode.Locked;
            Cursor.visible = false;
        }

        private void BuildUI()
        {
            if (mainPanel != null)
            {
                Destroy(mainPanel);
                mainPanel = null;
            }
            tabButtons.Clear();

            Canvas canvas = null;
            GameObject machineCanvas = GameObject.Find("MachineUICanvas");
            if (machineCanvas != null)
            {
                canvas = machineCanvas.GetComponent<Canvas>();
            }
            if (canvas == null)
            {
                canvas = GetComponentInParent<Canvas>();
            }
            if (canvas == null)
            {
                canvas = FindObjectOfType<Canvas>();
            }
            if (canvas == null)
            {
                Debug.LogError("[FirstRoomUpgradeStationUI] No canvas found!");
                return;
            }

            CreateMainPanel(canvas);
        }

        private void CreateMainPanel(Canvas canvas)
        {
            // Main panel - larger to fit tabs and content
            mainPanel = new GameObject("UpgradeStationPanel");
            mainPanel.transform.SetParent(canvas.transform, false);

            RectTransform panelRect = mainPanel.AddComponent<RectTransform>();
            panelRect.anchorMin = new Vector2(0.5f, 0.5f);
            panelRect.anchorMax = new Vector2(0.5f, 0.5f);
            panelRect.pivot = new Vector2(0.5f, 0.5f);
            panelRect.sizeDelta = new Vector2(900, 600);

            Image panelBg = mainPanel.AddComponent<Image>();
            panelBg.color = panelBgColor;

            Outline frame = mainPanel.AddComponent<Outline>();
            frame.effectColor = frameBorderCyan;
            frame.effectDistance = new Vector2(2, 2);

            // Vertical layout
            VerticalLayoutGroup vlg = mainPanel.AddComponent<VerticalLayoutGroup>();
            vlg.padding = new RectOffset(0, 0, 0, 0);
            vlg.spacing = 0;
            vlg.childControlWidth = true;
            vlg.childControlHeight = false;
            vlg.childForceExpandWidth = true;
            vlg.childForceExpandHeight = false;

            // Header
            CreateHeader();

            // Tab bar
            CreateTabBar();

            // Content area
            CreateContentArea();

            // Close button
            CreateCloseButton();
        }

        private void CreateHeader()
        {
            GameObject header = new GameObject("Header");
            header.transform.SetParent(mainPanel.transform, false);

            RectTransform headerRect = header.AddComponent<RectTransform>();
            headerRect.sizeDelta = new Vector2(0, 50);

            Image headerBg = header.AddComponent<Image>();
            headerBg.color = headerBgColor;

            // Title
            GameObject titleObj = new GameObject("Title");
            titleObj.transform.SetParent(header.transform, false);

            RectTransform titleRect = titleObj.AddComponent<RectTransform>();
            titleRect.anchorMin = Vector2.zero;
            titleRect.anchorMax = Vector2.one;
            titleRect.offsetMin = Vector2.zero;
            titleRect.offsetMax = Vector2.zero;

            TextMeshProUGUI titleText = titleObj.AddComponent<TextMeshProUGUI>();
            titleText.text = "UPGRADE STATION";
            titleText.fontSize = 28;
            titleText.fontStyle = FontStyles.Bold;
            titleText.color = textWhite;
            titleText.alignment = TextAlignmentOptions.Center;

            // Cyan underline
            GameObject underline = new GameObject("Underline");
            underline.transform.SetParent(header.transform, false);

            RectTransform underlineRect = underline.AddComponent<RectTransform>();
            underlineRect.anchorMin = new Vector2(0.3f, 0);
            underlineRect.anchorMax = new Vector2(0.7f, 0);
            underlineRect.pivot = new Vector2(0.5f, 0);
            underlineRect.sizeDelta = new Vector2(0, 3);
            underlineRect.anchoredPosition = new Vector2(0, 2);

            Image underlineImg = underline.AddComponent<Image>();
            underlineImg.color = frameBorderCyan;
        }

        private void CreateTabBar()
        {
            GameObject tabBar = new GameObject("TabBar");
            tabBar.transform.SetParent(mainPanel.transform, false);

            RectTransform tabRect = tabBar.AddComponent<RectTransform>();
            tabRect.sizeDelta = new Vector2(0, 40);

            Image tabBg = tabBar.AddComponent<Image>();
            tabBg.color = sectionBgColor;

            HorizontalLayoutGroup hlg = tabBar.AddComponent<HorizontalLayoutGroup>();
            hlg.padding = new RectOffset(20, 20, 5, 5);
            hlg.spacing = 10;
            hlg.childControlWidth = true;
            hlg.childControlHeight = true;
            hlg.childForceExpandWidth = true;
            hlg.childForceExpandHeight = true;

            string[] tabNames = { "TOOLS", "ENERGY", "JETPACK", "ROBOTS", "SYSTEMS" };
            UpgradeTab[] tabValues = { UpgradeTab.Tools, UpgradeTab.Energy, UpgradeTab.Jetpack, UpgradeTab.Robots, UpgradeTab.Systems };

            for (int i = 0; i < tabNames.Length; i++)
            {
                CreateTabButton(tabBar.transform, tabNames[i], tabValues[i]);
            }
        }

        private void CreateTabButton(Transform parent, string tabName, UpgradeTab tabValue)
        {
            GameObject tabBtn = new GameObject($"Tab_{tabName}");
            tabBtn.transform.SetParent(parent, false);

            Image btnBg = tabBtn.AddComponent<Image>();
            btnBg.color = tabInactiveColor;

            Button btn = tabBtn.AddComponent<Button>();
            btn.targetGraphic = btnBg;

            ColorBlock colors = btn.colors;
            colors.normalColor = tabInactiveColor;
            colors.highlightedColor = new Color(tabInactiveColor.r + 0.1f, tabInactiveColor.g + 0.1f, tabInactiveColor.b + 0.1f);
            colors.pressedColor = tabActiveColor;
            btn.colors = colors;

            UpgradeTab capturedTab = tabValue;

            // Systems tab is still locked; Robots tab is now functional
            if (tabValue == UpgradeTab.Systems)
            {
                btn.onClick.AddListener(() => OnLockedTabClicked(capturedTab));
            }
            else
            {
                btn.onClick.AddListener(() => SelectTab(capturedTab));
            }

            // Tab text
            GameObject textObj = new GameObject("Text");
            textObj.transform.SetParent(tabBtn.transform, false);

            RectTransform textRect = textObj.AddComponent<RectTransform>();
            textRect.anchorMin = Vector2.zero;
            textRect.anchorMax = Vector2.one;
            textRect.offsetMin = Vector2.zero;
            textRect.offsetMax = Vector2.zero;

            TextMeshProUGUI text = textObj.AddComponent<TextMeshProUGUI>();
            text.text = tabName;
            text.fontSize = 16;
            text.fontStyle = FontStyles.Bold;
            text.color = textWhite;
            text.alignment = TextAlignmentOptions.Center;

            tabButtons.Add(btn);
        }

        private void CreateContentArea()
        {
            contentArea = new GameObject("ContentArea");
            contentArea.transform.SetParent(mainPanel.transform, false);

            RectTransform contentRect = contentArea.AddComponent<RectTransform>();
            contentRect.sizeDelta = new Vector2(0, 470);

            Image contentBg = contentArea.AddComponent<Image>();
            contentBg.color = new Color(0.05f, 0.07f, 0.09f, 1f);

            // Content will be populated by SelectTab
        }

        private void CreateCloseButton()
        {
            GameObject closeBtn = new GameObject("CloseButton");
            closeBtn.transform.SetParent(mainPanel.transform, false);

            RectTransform closeRect = closeBtn.AddComponent<RectTransform>();
            closeRect.anchorMin = new Vector2(1, 1);
            closeRect.anchorMax = new Vector2(1, 1);
            closeRect.pivot = new Vector2(1, 1);
            closeRect.sizeDelta = new Vector2(40, 40);
            closeRect.anchoredPosition = new Vector2(-5, -5);

            Image closeBg = closeBtn.AddComponent<Image>();
            closeBg.color = new Color(0.6f, 0.15f, 0.15f);

            Button btn = closeBtn.AddComponent<Button>();
            btn.targetGraphic = closeBg;
            btn.onClick.AddListener(() => currentStation?.CloseStation());

            GameObject closeText = new GameObject("X");
            closeText.transform.SetParent(closeBtn.transform, false);

            RectTransform xRect = closeText.AddComponent<RectTransform>();
            xRect.anchorMin = Vector2.zero;
            xRect.anchorMax = Vector2.one;
            xRect.offsetMin = Vector2.zero;
            xRect.offsetMax = Vector2.zero;

            TextMeshProUGUI xText = closeText.AddComponent<TextMeshProUGUI>();
            xText.text = "X";
            xText.fontSize = 24;
            xText.fontStyle = FontStyles.Bold;
            xText.color = textWhite;
            xText.alignment = TextAlignmentOptions.Center;
        }

        private void SelectTab(UpgradeTab tab)
        {
            currentTab = tab;

            // Update tab button colors
            string[] tabNames = { "TOOLS", "ENERGY", "JETPACK", "ROBOTS", "SYSTEMS" };
            for (int i = 0; i < tabButtons.Count; i++)
            {
                Image btnImg = tabButtons[i].GetComponent<Image>();
                if (i == (int)tab)
                {
                    btnImg.color = tabActiveColor;
                }
                else
                {
                    btnImg.color = tabInactiveColor;
                }
            }

            // Clear and rebuild content
            foreach (Transform child in contentArea.transform)
            {
                Destroy(child.gameObject);
            }

            switch (tab)
            {
                case UpgradeTab.Tools:
                    BuildToolsTab();
                    break;
                case UpgradeTab.Energy:
                    BuildEnergyTab();
                    break;
                case UpgradeTab.Jetpack:
                    BuildJetpackTab();
                    break;
                case UpgradeTab.Robots:
                    BuildRobotsTab();
                    break;
                case UpgradeTab.Systems:
                    BuildSystemsTab();
                    break;
            }
        }

        private void BuildToolsTab()
        {
            // Simple layout - sections stacked vertically, filling the space
            GameObject container = new GameObject("ToolsContainer");
            container.transform.SetParent(contentArea.transform, false);

            RectTransform containerRect = container.AddComponent<RectTransform>();
            containerRect.anchorMin = Vector2.zero;
            containerRect.anchorMax = Vector2.one;
            containerRect.offsetMin = new Vector2(30, 30);
            containerRect.offsetMax = new Vector2(-30, -30);

            // Check if Sonic Pulser is available or owned
            bool isToolMaxed = ToolPowerLevel >= TOOL_POWER_MAX_LEVEL;
            bool hasSonicPulser = UpgradeStation.Instance != null && UpgradeStation.Instance.HasSonicPulser();
            bool showSonicPulser = isToolMaxed || hasSonicPulser;

            if (showSonicPulser)
            {
                // 2-section layout: Tool Power top half, Sonic Pulser bottom half
                CreateSimpleUpgradeSection(container.transform,
                    "TOOL POWER & RADIUS",
                    "Increases dig power and digging area",
                    ToolPowerLevel,
                    TOOL_POWER_MAX_LEVEL,
                    new int[] { 500, 1000 },
                    "tool_power",
                    true); // isTopSection

                CreateSonicPulserSection(container.transform, hasSonicPulser);
            }
            else
            {
                // Single full-area section for Tool Power only
                CreateFullAreaUpgradeSection(container.transform,
                    "TOOL POWER & RADIUS",
                    "Increases dig power and digging area",
                    ToolPowerLevel,
                    TOOL_POWER_MAX_LEVEL,
                    new int[] { 500, 1000 },
                    "tool_power");
            }
        }

        /// <summary>
        /// Creates a single upgrade section that fills the entire content area.
        /// </summary>
        private void CreateFullAreaUpgradeSection(Transform parent, string title, string desc, int currentLevel, int maxLevel, int[] costs, string upgradeId)
        {
            GameObject section = new GameObject($"Section_{upgradeId}");
            section.transform.SetParent(parent, false);

            RectTransform sectionRect = section.AddComponent<RectTransform>();
            sectionRect.anchorMin = Vector2.zero;
            sectionRect.anchorMax = Vector2.one;
            sectionRect.offsetMin = Vector2.zero;
            sectionRect.offsetMax = Vector2.zero;

            Image sectionBgImg = section.AddComponent<Image>();
            sectionBgImg.color = sectionBgColor;

            // === TITLE ===
            GameObject titleObj = new GameObject("Title");
            titleObj.transform.SetParent(section.transform, false);

            RectTransform titleRect = titleObj.AddComponent<RectTransform>();
            titleRect.anchorMin = new Vector2(0, 0.7f);
            titleRect.anchorMax = new Vector2(1, 1);
            titleRect.offsetMin = new Vector2(25, 0);
            titleRect.offsetMax = new Vector2(-25, -15);

            TextMeshProUGUI titleText = titleObj.AddComponent<TextMeshProUGUI>();
            titleText.text = title;
            titleText.fontSize = 26;
            titleText.fontStyle = FontStyles.Bold;
            titleText.color = textWhite;
            titleText.alignment = TextAlignmentOptions.Left;

            // === DESCRIPTION ===
            GameObject descObj = new GameObject("Description");
            descObj.transform.SetParent(section.transform, false);

            RectTransform descRect = descObj.AddComponent<RectTransform>();
            descRect.anchorMin = new Vector2(0, 0.5f);
            descRect.anchorMax = new Vector2(1, 0.7f);
            descRect.offsetMin = new Vector2(25, 0);
            descRect.offsetMax = new Vector2(-25, 0);

            TextMeshProUGUI descText = descObj.AddComponent<TextMeshProUGUI>();
            descText.text = desc;
            descText.fontSize = 16;
            descText.color = textCyan;
            descText.alignment = TextAlignmentOptions.Left;

            // === LEVEL BOXES ROW ===
            GameObject levelRow = new GameObject("LevelRow");
            levelRow.transform.SetParent(section.transform, false);

            RectTransform levelRowRect = levelRow.AddComponent<RectTransform>();
            levelRowRect.anchorMin = new Vector2(0, 0.15f);
            levelRowRect.anchorMax = new Vector2(1, 0.45f);
            levelRowRect.offsetMin = new Vector2(25, 0);
            levelRowRect.offsetMax = new Vector2(-25, 0);

            HorizontalLayoutGroup hlg = levelRow.AddComponent<HorizontalLayoutGroup>();
            hlg.spacing = 12;
            hlg.childControlWidth = false;
            hlg.childControlHeight = true;
            hlg.childForceExpandWidth = false;
            hlg.childForceExpandHeight = true;
            hlg.childAlignment = TextAnchor.MiddleLeft;

            for (int i = 0; i <= maxLevel; i++)
            {
                bool isCompleted = (i == 0) || (i <= currentLevel);
                bool isNext = (i == currentLevel + 1);

                GameObject box = new GameObject($"Level_{i}");
                box.transform.SetParent(levelRow.transform, false);

                LayoutElement boxLe = box.AddComponent<LayoutElement>();
                boxLe.minWidth = 90;
                boxLe.preferredWidth = 90;

                Image boxBg = box.AddComponent<Image>();
                if (isCompleted)
                    boxBg.color = buttonGreen;
                else if (isNext)
                    boxBg.color = new Color(0.15f, 0.25f, 0.30f);
                else
                    boxBg.color = rowBgDark;

                if (isNext)
                {
                    Outline outline = box.AddComponent<Outline>();
                    outline.effectColor = textCyan;
                    outline.effectDistance = new Vector2(2, 2);
                }

                GameObject boxText = new GameObject("Text");
                boxText.transform.SetParent(box.transform, false);

                RectTransform boxTextRect = boxText.AddComponent<RectTransform>();
                boxTextRect.anchorMin = Vector2.zero;
                boxTextRect.anchorMax = Vector2.one;
                boxTextRect.offsetMin = Vector2.zero;
                boxTextRect.offsetMax = Vector2.zero;

                TextMeshProUGUI tmp = boxText.AddComponent<TextMeshProUGUI>();
                tmp.text = (i == 0) ? "BASE" : $"LVL {i}";
                tmp.fontSize = 17;
                tmp.fontStyle = FontStyles.Bold;
                tmp.color = isCompleted ? textWhite : (isNext ? textCyan : textGray);
                tmp.alignment = TextAlignmentOptions.Center;
            }

            // Spacer
            GameObject spacer = new GameObject("Spacer");
            spacer.transform.SetParent(levelRow.transform, false);
            LayoutElement spacerLe = spacer.AddComponent<LayoutElement>();
            spacerLe.flexibleWidth = 1;

            // Button or MAXED
            if (currentLevel >= maxLevel)
            {
                GameObject maxBox = new GameObject("Maxed");
                maxBox.transform.SetParent(levelRow.transform, false);

                LayoutElement maxLe = maxBox.AddComponent<LayoutElement>();
                maxLe.minWidth = 130;
                maxLe.preferredWidth = 130;

                Image maxBg = maxBox.AddComponent<Image>();
                maxBg.color = new Color(0.2f, 0.4f, 0.25f);

                GameObject maxText = new GameObject("Text");
                maxText.transform.SetParent(maxBox.transform, false);

                RectTransform maxTextRect = maxText.AddComponent<RectTransform>();
                maxTextRect.anchorMin = Vector2.zero;
                maxTextRect.anchorMax = Vector2.one;
                maxTextRect.offsetMin = Vector2.zero;
                maxTextRect.offsetMax = Vector2.zero;

                TextMeshProUGUI maxTmp = maxText.AddComponent<TextMeshProUGUI>();
                maxTmp.text = "MAXED";
                maxTmp.fontSize = 18;
                maxTmp.fontStyle = FontStyles.Bold;
                maxTmp.color = textWhite;
                maxTmp.alignment = TextAlignmentOptions.Center;
            }
            else
            {
                int cost = costs[currentLevel];

                GameObject btn = new GameObject("UpgradeButton");
                btn.transform.SetParent(levelRow.transform, false);

                LayoutElement btnLe = btn.AddComponent<LayoutElement>();
                btnLe.minWidth = 130;
                btnLe.preferredWidth = 130;

                Image btnBg = btn.AddComponent<Image>();
                btnBg.color = buttonCyan;

                Button button = btn.AddComponent<Button>();
                button.targetGraphic = btnBg;

                string capturedId = upgradeId;
                int capturedCost = cost;
                button.onClick.AddListener(() => TryPurchaseUpgrade(capturedId, capturedCost));

                GameObject btnText = new GameObject("Text");
                btnText.transform.SetParent(btn.transform, false);

                RectTransform btnTextRect = btnText.AddComponent<RectTransform>();
                btnTextRect.anchorMin = Vector2.zero;
                btnTextRect.anchorMax = Vector2.one;
                btnTextRect.offsetMin = Vector2.zero;
                btnTextRect.offsetMax = Vector2.zero;

                TextMeshProUGUI btnTmp = btnText.AddComponent<TextMeshProUGUI>();
                btnTmp.text = $"${cost}";
                btnTmp.fontSize = 20;
                btnTmp.fontStyle = FontStyles.Bold;
                btnTmp.color = new Color(1f, 0.9f, 0.3f);
                btnTmp.alignment = TextAlignmentOptions.Center;
                btnTmp.raycastTarget = false;
            }
        }

        /// <summary>
        /// Creates the Sonic Pulser purchase/owned section in the bottom half.
        /// </summary>
        private void CreateSonicPulserSection(Transform parent, bool isOwned)
        {
            GameObject section = new GameObject("Section_sonic_pulser");
            section.transform.SetParent(parent, false);

            RectTransform sectionRect = section.AddComponent<RectTransform>();
            sectionRect.anchorMin = new Vector2(0, 0);
            sectionRect.anchorMax = new Vector2(1, 0.48f);
            sectionRect.offsetMin = Vector2.zero;
            sectionRect.offsetMax = Vector2.zero;

            Image sectionBg = section.AddComponent<Image>();
            sectionBg.color = new Color(0.07f, 0.09f, 0.11f, 1f); // Slightly different for emphasis

            // === TITLE ===
            GameObject titleObj = new GameObject("Title");
            titleObj.transform.SetParent(section.transform, false);

            RectTransform titleRect = titleObj.AddComponent<RectTransform>();
            titleRect.anchorMin = new Vector2(0, 0.65f);
            titleRect.anchorMax = new Vector2(0.55f, 1);
            titleRect.offsetMin = new Vector2(25, 0);
            titleRect.offsetMax = new Vector2(0, -10);

            TextMeshProUGUI titleText = titleObj.AddComponent<TextMeshProUGUI>();
            titleText.text = "SONIC PULSER";
            titleText.fontSize = 22;
            titleText.fontStyle = FontStyles.Bold;
            titleText.color = new Color(1.0f, 0.8f, 0.2f); // Bright gold/yellow
            titleText.alignment = TextAlignmentOptions.Left;

            // === DESCRIPTION ===
            GameObject descObj = new GameObject("Description");
            descObj.transform.SetParent(section.transform, false);

            RectTransform descRect = descObj.AddComponent<RectTransform>();
            descRect.anchorMin = new Vector2(0, 0.35f);
            descRect.anchorMax = new Vector2(0.55f, 0.65f);
            descRect.offsetMin = new Vector2(25, 0);
            descRect.offsetMax = new Vector2(0, 0);

            TextMeshProUGUI descText = descObj.AddComponent<TextMeshProUGUI>();
            descText.text = "An advanced sonic-powered digging tool";
            descText.fontSize = 13;
            descText.color = textCyan;
            descText.alignment = TextAlignmentOptions.Left;

            if (isOwned)
            {
                // OWNED badge
                GameObject ownedBox = new GameObject("OwnedBadge");
                ownedBox.transform.SetParent(section.transform, false);

                RectTransform ownedRect = ownedBox.AddComponent<RectTransform>();
                ownedRect.anchorMin = new Vector2(0.60f, 0.25f);
                ownedRect.anchorMax = new Vector2(0.90f, 0.75f);
                ownedRect.offsetMin = Vector2.zero;
                ownedRect.offsetMax = Vector2.zero;

                Image ownedBg = ownedBox.AddComponent<Image>();
                ownedBg.color = new Color(0.2f, 0.5f, 0.25f);

                GameObject ownedText = new GameObject("Text");
                ownedText.transform.SetParent(ownedBox.transform, false);

                RectTransform ownedTextRect = ownedText.AddComponent<RectTransform>();
                ownedTextRect.anchorMin = Vector2.zero;
                ownedTextRect.anchorMax = Vector2.one;
                ownedTextRect.offsetMin = Vector2.zero;
                ownedTextRect.offsetMax = Vector2.zero;

                TextMeshProUGUI ownedTmp = ownedText.AddComponent<TextMeshProUGUI>();
                ownedTmp.text = "OWNED";
                ownedTmp.fontSize = 20;
                ownedTmp.fontStyle = FontStyles.Bold;
                ownedTmp.color = textWhite;
                ownedTmp.alignment = TextAlignmentOptions.Center;
            }
            else
            {
                // BUY button with cost
                int cost = 12500;

                GameObject btn = new GameObject("BuyButton");
                btn.transform.SetParent(section.transform, false);

                RectTransform btnRect = btn.AddComponent<RectTransform>();
                btnRect.anchorMin = new Vector2(0.60f, 0.20f);
                btnRect.anchorMax = new Vector2(0.95f, 0.80f);
                btnRect.offsetMin = Vector2.zero;
                btnRect.offsetMax = Vector2.zero;

                Image btnBg = btn.AddComponent<Image>();
                bool canAfford = CurrencyManager.Instance != null && CurrencyManager.Instance.CanAfford(cost);
                btnBg.color = canAfford ? buttonCyan : lockedColor;

                Button button = btn.AddComponent<Button>();
                button.targetGraphic = btnBg;
                button.interactable = canAfford;

                int capturedCost = cost;
                button.onClick.AddListener(() => TryPurchaseSonicPulser(capturedCost));

                GameObject btnText = new GameObject("Text");
                btnText.transform.SetParent(btn.transform, false);

                RectTransform btnTextRect = btnText.AddComponent<RectTransform>();
                btnTextRect.anchorMin = Vector2.zero;
                btnTextRect.anchorMax = Vector2.one;
                btnTextRect.offsetMin = Vector2.zero;
                btnTextRect.offsetMax = Vector2.zero;

                TextMeshProUGUI btnTmp = btnText.AddComponent<TextMeshProUGUI>();
                btnTmp.text = $"BUY  ${cost:N0}";
                btnTmp.fontSize = 18;
                btnTmp.fontStyle = FontStyles.Bold;
                btnTmp.color = canAfford ? new Color(1f, 0.9f, 0.3f) : textGray;
                btnTmp.alignment = TextAlignmentOptions.Center;
                btnTmp.raycastTarget = false;
            }
        }

        private void TryPurchaseSonicPulser(int cost)
        {
            if (CurrencyManager.Instance == null) return;

            if (!CurrencyManager.Instance.Spend(cost))
            {
                Debug.Log("[FirstRoomUpgradeStationUI] Not enough credits for Sonic Pulser");
                return;
            }

            Debug.Log($"[FirstRoomUpgradeStationUI] Purchased Sonic Pulser for ${cost}!");
            currentStation?.PlayUpgradeSound();

            // Set sonic_pulser upgrade in the main UpgradeStation system
            if (UpgradeStation.Instance != null)
            {
                var sonicUpgrade = UpgradeStation.Instance.GetRuntimeUpgradeById("sonic_pulser");
                if (sonicUpgrade != null)
                {
                    sonicUpgrade.currentLevel = 1;
                    PlayerPrefs.SetInt("RuntimeUpgrade_sonic_pulser", 1);
                }
            }
            else
            {
                // Fallback: save directly to PlayerPrefs
                PlayerPrefs.SetInt("RuntimeUpgrade_sonic_pulser", 1);
            }

            // Switch to Tool 5 in HeldToolController
            if (Tools.HeldToolController.Instance != null)
            {
                Tools.HeldToolController.Instance.SetActiveTool(4);
            }

            // Save tool index
            PlayerPrefs.SetInt("PlayerToolIndex", 4);
            PlayerPrefs.Save();

            // Show toast
            if (UI.PickupNotificationSystem.Instance != null)
            {
                UI.PickupNotificationSystem.Instance.ShowNotification("Sonic Pulser acquired!");
            }

            // Refresh tab to show OWNED state
            SelectTab(currentTab);
        }

        private void CreateSimpleUpgradeSection(Transform parent, string title, string desc, int currentLevel, int maxLevel, int[] costs, string upgradeId, bool isTop)
        {
            GameObject section = new GameObject($"Section_{upgradeId}");
            section.transform.SetParent(parent, false);

            RectTransform sectionRect = section.AddComponent<RectTransform>();
            // Top section takes top half, bottom section takes bottom half
            if (isTop)
            {
                sectionRect.anchorMin = new Vector2(0, 0.52f);
                sectionRect.anchorMax = new Vector2(1, 1);
            }
            else
            {
                sectionRect.anchorMin = new Vector2(0, 0);
                sectionRect.anchorMax = new Vector2(1, 0.48f);
            }
            sectionRect.offsetMin = Vector2.zero;
            sectionRect.offsetMax = Vector2.zero;

            Image sectionBg = section.AddComponent<Image>();
            sectionBg.color = sectionBgColor;

            // === TITLE ===
            GameObject titleObj = new GameObject("Title");
            titleObj.transform.SetParent(section.transform, false);

            RectTransform titleRect = titleObj.AddComponent<RectTransform>();
            titleRect.anchorMin = new Vector2(0, 0.7f);
            titleRect.anchorMax = new Vector2(1, 1);
            titleRect.offsetMin = new Vector2(25, 0);
            titleRect.offsetMax = new Vector2(-25, -15);

            TextMeshProUGUI titleText = titleObj.AddComponent<TextMeshProUGUI>();
            titleText.text = title;
            titleText.fontSize = 22;
            titleText.fontStyle = FontStyles.Bold;
            titleText.color = textWhite;
            titleText.alignment = TextAlignmentOptions.Left;

            // === DESCRIPTION ===
            GameObject descObj = new GameObject("Description");
            descObj.transform.SetParent(section.transform, false);

            RectTransform descRect = descObj.AddComponent<RectTransform>();
            descRect.anchorMin = new Vector2(0, 0.5f);
            descRect.anchorMax = new Vector2(1, 0.7f);
            descRect.offsetMin = new Vector2(25, 0);
            descRect.offsetMax = new Vector2(-25, 0);

            TextMeshProUGUI descText = descObj.AddComponent<TextMeshProUGUI>();
            descText.text = desc;
            descText.fontSize = 14;
            descText.color = textCyan;
            descText.alignment = TextAlignmentOptions.Left;

            // === LEVEL BOXES ROW ===
            GameObject levelRow = new GameObject("LevelRow");
            levelRow.transform.SetParent(section.transform, false);

            RectTransform levelRowRect = levelRow.AddComponent<RectTransform>();
            levelRowRect.anchorMin = new Vector2(0, 0);
            levelRowRect.anchorMax = new Vector2(1, 0.45f);
            levelRowRect.offsetMin = new Vector2(25, 15);
            levelRowRect.offsetMax = new Vector2(-25, 0);

            HorizontalLayoutGroup hlg = levelRow.AddComponent<HorizontalLayoutGroup>();
            hlg.spacing = 12;
            hlg.childControlWidth = false;
            hlg.childControlHeight = true;
            hlg.childForceExpandWidth = false;
            hlg.childForceExpandHeight = true;
            hlg.childAlignment = TextAnchor.MiddleLeft;

            // Create level boxes
            for (int i = 0; i <= maxLevel; i++)
            {
                bool isCompleted = (i == 0) || (i <= currentLevel);
                bool isNext = (i == currentLevel + 1);

                GameObject box = new GameObject($"Level_{i}");
                box.transform.SetParent(levelRow.transform, false);

                LayoutElement boxLe = box.AddComponent<LayoutElement>();
                boxLe.minWidth = 75;
                boxLe.preferredWidth = 75;

                Image boxBg = box.AddComponent<Image>();
                if (isCompleted)
                    boxBg.color = buttonGreen;
                else if (isNext)
                    boxBg.color = new Color(0.15f, 0.25f, 0.30f);
                else
                    boxBg.color = rowBgDark;

                if (isNext)
                {
                    Outline outline = box.AddComponent<Outline>();
                    outline.effectColor = textCyan;
                    outline.effectDistance = new Vector2(2, 2);
                }

                GameObject boxText = new GameObject("Text");
                boxText.transform.SetParent(box.transform, false);

                RectTransform boxTextRect = boxText.AddComponent<RectTransform>();
                boxTextRect.anchorMin = Vector2.zero;
                boxTextRect.anchorMax = Vector2.one;
                boxTextRect.offsetMin = Vector2.zero;
                boxTextRect.offsetMax = Vector2.zero;

                TextMeshProUGUI tmp = boxText.AddComponent<TextMeshProUGUI>();
                tmp.text = (i == 0) ? "BASE" : $"LVL {i}";
                tmp.fontSize = 15;
                tmp.fontStyle = FontStyles.Bold;
                tmp.color = isCompleted ? textWhite : (isNext ? textCyan : textGray);
                tmp.alignment = TextAlignmentOptions.Center;
            }

            // Spacer
            GameObject spacer = new GameObject("Spacer");
            spacer.transform.SetParent(levelRow.transform, false);
            LayoutElement spacerLe = spacer.AddComponent<LayoutElement>();
            spacerLe.flexibleWidth = 1;

            // Button or MAXED
            if (currentLevel >= maxLevel)
            {
                GameObject maxBox = new GameObject("Maxed");
                maxBox.transform.SetParent(levelRow.transform, false);

                LayoutElement maxLe = maxBox.AddComponent<LayoutElement>();
                maxLe.minWidth = 110;
                maxLe.preferredWidth = 110;

                Image maxBg = maxBox.AddComponent<Image>();
                maxBg.color = new Color(0.2f, 0.4f, 0.25f);

                GameObject maxText = new GameObject("Text");
                maxText.transform.SetParent(maxBox.transform, false);

                RectTransform maxTextRect = maxText.AddComponent<RectTransform>();
                maxTextRect.anchorMin = Vector2.zero;
                maxTextRect.anchorMax = Vector2.one;
                maxTextRect.offsetMin = Vector2.zero;
                maxTextRect.offsetMax = Vector2.zero;

                TextMeshProUGUI maxTmp = maxText.AddComponent<TextMeshProUGUI>();
                maxTmp.text = "MAXED";
                maxTmp.fontSize = 16;
                maxTmp.fontStyle = FontStyles.Bold;
                maxTmp.color = textWhite;
                maxTmp.alignment = TextAlignmentOptions.Center;
            }
            else
            {
                int cost = costs[currentLevel];

                GameObject btn = new GameObject("UpgradeButton");
                btn.transform.SetParent(levelRow.transform, false);

                LayoutElement btnLe = btn.AddComponent<LayoutElement>();
                btnLe.minWidth = 110;
                btnLe.preferredWidth = 110;

                Image btnBg = btn.AddComponent<Image>();
                btnBg.color = buttonCyan;

                Button button = btn.AddComponent<Button>();
                button.targetGraphic = btnBg;

                string capturedId = upgradeId;
                int capturedCost = cost;
                button.onClick.AddListener(() => TryPurchaseUpgrade(capturedId, capturedCost));

                GameObject btnText = new GameObject("Text");
                btnText.transform.SetParent(btn.transform, false);

                RectTransform btnTextRect = btnText.AddComponent<RectTransform>();
                btnTextRect.anchorMin = Vector2.zero;
                btnTextRect.anchorMax = Vector2.one;
                btnTextRect.offsetMin = Vector2.zero;
                btnTextRect.offsetMax = Vector2.zero;

                TextMeshProUGUI btnTmp = btnText.AddComponent<TextMeshProUGUI>();
                btnTmp.text = $"${cost}";
                btnTmp.fontSize = 18;
                btnTmp.fontStyle = FontStyles.Bold;
                btnTmp.color = new Color(1f, 0.9f, 0.3f);
                btnTmp.alignment = TextAlignmentOptions.Center;
                btnTmp.raycastTarget = false;
            }
        }


        private void BuildEnergyTab()
        {
            int currentLevel = GetCurrentEnergyLevel();

            // Container for the energy upgrade display
            GameObject container = new GameObject("EnergyContainer");
            container.transform.SetParent(contentArea.transform, false);

            RectTransform containerRect = container.AddComponent<RectTransform>();
            containerRect.anchorMin = Vector2.zero;
            containerRect.anchorMax = Vector2.one;
            containerRect.offsetMin = new Vector2(30, 20);
            containerRect.offsetMax = new Vector2(-30, -20);

            // === TITLE ===
            GameObject titleObj = new GameObject("Title");
            titleObj.transform.SetParent(container.transform, false);

            RectTransform titleRect = titleObj.AddComponent<RectTransform>();
            titleRect.anchorMin = new Vector2(0, 0.85f);
            titleRect.anchorMax = new Vector2(1, 1);
            titleRect.offsetMin = Vector2.zero;
            titleRect.offsetMax = Vector2.zero;

            TextMeshProUGUI titleText = titleObj.AddComponent<TextMeshProUGUI>();
            titleText.text = "ENERGY SYSTEM";
            titleText.fontSize = 24;
            titleText.fontStyle = FontStyles.Bold;
            titleText.color = textWhite;
            titleText.alignment = TextAlignmentOptions.Center;

            // === DESCRIPTION ===
            GameObject descObj = new GameObject("Description");
            descObj.transform.SetParent(container.transform, false);

            RectTransform descRect = descObj.AddComponent<RectTransform>();
            descRect.anchorMin = new Vector2(0, 0.75f);
            descRect.anchorMax = new Vector2(1, 0.85f);
            descRect.offsetMin = Vector2.zero;
            descRect.offsetMax = Vector2.zero;

            TextMeshProUGUI descText = descObj.AddComponent<TextMeshProUGUI>();
            descText.text = GetEnergyDescription(currentLevel);
            descText.fontSize = 14;
            descText.color = textCyan;
            descText.alignment = TextAlignmentOptions.Center;

            // === LEVEL BOXES (2 rows) ===
            // Row 1: L0-L5 (basement levels)
            CreateEnergyLevelRow(container.transform, currentLevel, 0, 5, 0.50f, 0.72f, "BASEMENT UPGRADES");

            // Row 2: L6-L7 (FirstRoom exclusive)
            CreateEnergyLevelRow(container.transform, currentLevel, 6, 7, 0.22f, 0.44f, "ADVANCED (FirstRoom Exclusive)");

            // === UPGRADE BUTTON ===
            CreateEnergyUpgradeButton(container.transform, currentLevel);
        }

        private void CreateEnergyLevelRow(Transform parent, int currentLevel, int startLevel, int endLevel, float anchorMinY, float anchorMaxY, string rowLabel)
        {
            GameObject row = new GameObject($"LevelRow_{startLevel}_{endLevel}");
            row.transform.SetParent(parent, false);

            RectTransform rowRect = row.AddComponent<RectTransform>();
            rowRect.anchorMin = new Vector2(0, anchorMinY);
            rowRect.anchorMax = new Vector2(1, anchorMaxY);
            rowRect.offsetMin = Vector2.zero;
            rowRect.offsetMax = Vector2.zero;

            // Row label
            GameObject labelObj = new GameObject("Label");
            labelObj.transform.SetParent(row.transform, false);

            RectTransform labelRect = labelObj.AddComponent<RectTransform>();
            labelRect.anchorMin = new Vector2(0, 0.7f);
            labelRect.anchorMax = new Vector2(1, 1);
            labelRect.offsetMin = Vector2.zero;
            labelRect.offsetMax = Vector2.zero;

            TextMeshProUGUI labelText = labelObj.AddComponent<TextMeshProUGUI>();
            labelText.text = rowLabel;
            labelText.fontSize = 12;
            labelText.color = textGray;
            labelText.alignment = TextAlignmentOptions.Left;

            // Level boxes container
            GameObject boxesContainer = new GameObject("Boxes");
            boxesContainer.transform.SetParent(row.transform, false);

            RectTransform boxesRect = boxesContainer.AddComponent<RectTransform>();
            boxesRect.anchorMin = new Vector2(0, 0);
            boxesRect.anchorMax = new Vector2(1, 0.7f);
            boxesRect.offsetMin = Vector2.zero;
            boxesRect.offsetMax = Vector2.zero;

            HorizontalLayoutGroup hlg = boxesContainer.AddComponent<HorizontalLayoutGroup>();
            hlg.spacing = 10;
            hlg.childControlWidth = false;
            hlg.childControlHeight = true;
            hlg.childForceExpandWidth = false;
            hlg.childForceExpandHeight = true;
            hlg.childAlignment = TextAnchor.MiddleLeft;

            // Create level boxes
            for (int i = startLevel; i <= endLevel; i++)
            {
                bool isCompleted = (i <= currentLevel);
                bool isNext = (i == currentLevel + 1);
                bool isFirstRoomExclusive = (i > BASEMENT_MAX_LEVEL);

                GameObject box = new GameObject($"Level_{i}");
                box.transform.SetParent(boxesContainer.transform, false);

                LayoutElement boxLe = box.AddComponent<LayoutElement>();
                boxLe.minWidth = 85;
                boxLe.preferredWidth = 85;

                Image boxBg = box.AddComponent<Image>();
                if (isCompleted)
                    boxBg.color = buttonGreen;
                else if (isNext)
                    boxBg.color = new Color(0.15f, 0.25f, 0.30f);
                else if (isFirstRoomExclusive)
                    boxBg.color = new Color(0.20f, 0.15f, 0.25f); // Purple tint for exclusive
                else
                    boxBg.color = rowBgDark;

                if (isNext)
                {
                    Outline outline = box.AddComponent<Outline>();
                    outline.effectColor = textCyan;
                    outline.effectDistance = new Vector2(2, 2);
                }

                // Box content
                GameObject boxText = new GameObject("Text");
                boxText.transform.SetParent(box.transform, false);

                RectTransform boxTextRect = boxText.AddComponent<RectTransform>();
                boxTextRect.anchorMin = Vector2.zero;
                boxTextRect.anchorMax = Vector2.one;
                boxTextRect.offsetMin = new Vector2(0, 8);
                boxTextRect.offsetMax = Vector2.zero;

                TextMeshProUGUI tmp = boxText.AddComponent<TextMeshProUGUI>();
                string labelStr = (i == 0) ? "BASE" : $"L{i}";
                if (isFirstRoomExclusive && !isCompleted) labelStr += "*";
                tmp.text = labelStr;
                tmp.fontSize = 14;
                tmp.fontStyle = FontStyles.Bold;
                tmp.color = isCompleted ? textWhite : (isNext ? textCyan : textGray);
                tmp.alignment = TextAlignmentOptions.Center;

                // Energy value subtitle
                GameObject valueText = new GameObject("Value");
                valueText.transform.SetParent(box.transform, false);

                RectTransform valueRect = valueText.AddComponent<RectTransform>();
                valueRect.anchorMin = Vector2.zero;
                valueRect.anchorMax = Vector2.one;
                valueRect.offsetMin = Vector2.zero;
                valueRect.offsetMax = new Vector2(0, -12);

                TextMeshProUGUI valueTmp = valueText.AddComponent<TextMeshProUGUI>();
                int energyValue = 100 + (i * 25); // Formula: base + level * 25
                valueTmp.text = $"{energyValue}";
                valueTmp.fontSize = 11;
                valueTmp.color = isCompleted ? new Color(0.7f, 0.9f, 0.7f) : textGray;
                valueTmp.alignment = TextAlignmentOptions.Center;
            }
        }

        private void CreateEnergyUpgradeButton(Transform parent, int currentLevel)
        {
            GameObject btnArea = new GameObject("UpgradeButtonArea");
            btnArea.transform.SetParent(parent, false);

            RectTransform btnAreaRect = btnArea.AddComponent<RectTransform>();
            btnAreaRect.anchorMin = new Vector2(0.3f, 0.02f);
            btnAreaRect.anchorMax = new Vector2(0.7f, 0.18f);
            btnAreaRect.offsetMin = Vector2.zero;
            btnAreaRect.offsetMax = Vector2.zero;

            bool isMaxed = currentLevel >= TOTAL_MAX_LEVEL;
            int nextLevel = currentLevel + 1;
            int cost = isMaxed ? 0 : (nextLevel <= energyUpgradeCosts.Length ? energyUpgradeCosts[nextLevel - 1] : 1500);

            Image btnBg = btnArea.AddComponent<Image>();
            btnBg.color = isMaxed ? new Color(0.2f, 0.4f, 0.25f) : buttonCyan;

            if (!isMaxed)
            {
                Button btn = btnArea.AddComponent<Button>();
                btn.targetGraphic = btnBg;
                btn.onClick.AddListener(() => TryPurchaseEnergyUpgrade(cost));
            }

            GameObject btnText = new GameObject("Text");
            btnText.transform.SetParent(btnArea.transform, false);

            RectTransform btnTextRect = btnText.AddComponent<RectTransform>();
            btnTextRect.anchorMin = Vector2.zero;
            btnTextRect.anchorMax = Vector2.one;
            btnTextRect.offsetMin = Vector2.zero;
            btnTextRect.offsetMax = Vector2.zero;

            TextMeshProUGUI btnTmp = btnText.AddComponent<TextMeshProUGUI>();
            if (isMaxed)
            {
                btnTmp.text = "FULLY UPGRADED";
                btnTmp.color = textWhite;
            }
            else
            {
                int nextEnergy = 100 + (nextLevel * 25);
                int nextRegen = 4 + (nextLevel * 2);
                btnTmp.text = $"UPGRADE TO L{nextLevel}  -  ${cost}\n({nextEnergy} energy, {nextRegen}/s regen)";
                btnTmp.color = new Color(1f, 0.9f, 0.3f);
            }
            btnTmp.fontSize = 16;
            btnTmp.fontStyle = FontStyles.Bold;
            btnTmp.alignment = TextAlignmentOptions.Center;
        }

        private string GetEnergyDescription(int currentLevel)
        {
            if (EnergyManager.Instance == null) return "Energy capacity and regeneration";

            float currentMax = EnergyManager.Instance.MaxEnergy;
            float currentRegen = EnergyManager.Instance.RegenRate;

            if (currentLevel >= TOTAL_MAX_LEVEL)
            {
                return $"Maximum: {currentMax:F0} energy, {currentRegen:F0}/s regen";
            }

            int nextEnergy = 100 + ((currentLevel + 1) * 25);
            int nextRegen = 4 + ((currentLevel + 1) * 2);
            return $"Current: {currentMax:F0} energy, {currentRegen:F0}/s  →  Next: {nextEnergy} energy, {nextRegen}/s";
        }

        private void TryPurchaseEnergyUpgrade(int cost)
        {
            if (CurrencyManager.Instance == null || EnergyManager.Instance == null)
            {
                Debug.LogWarning("[FirstRoomUpgradeStationUI] Missing CurrencyManager or EnergyManager!");
                return;
            }

            int currentLevel = GetCurrentEnergyLevel();
            if (currentLevel >= TOTAL_MAX_LEVEL)
            {
                Debug.Log("[FirstRoomUpgradeStationUI] Energy already at max level!");
                return;
            }

            if (CurrencyManager.Instance.Spend(cost))
            {
                Debug.Log($"[FirstRoomUpgradeStationUI] Purchased energy upgrade L{currentLevel}→L{currentLevel + 1} for ${cost}");
                currentStation?.PlayUpgradeSound();

                // Apply the upgrade via EnergyManager
                EnergyManager.Instance.TryUpgradeEnergy();

                // Refresh the tab to show updated state
                SelectTab(currentTab);
            }
            else
            {
                Debug.Log($"[FirstRoomUpgradeStationUI] Not enough credits for energy upgrade (need ${cost})");
            }
        }

        private void BuildJetpackTab()
        {
            GameObject container = new GameObject("JetpackContainer");
            container.transform.SetParent(contentArea.transform, false);

            RectTransform containerRect = container.AddComponent<RectTransform>();
            containerRect.anchorMin = Vector2.zero;
            containerRect.anchorMax = Vector2.one;
            containerRect.offsetMin = new Vector2(30, 30);
            containerRect.offsetMax = new Vector2(-30, -30);

            // FUEL EFFICIENCY upgrade (top half)
            string drainDesc;
            if (JetpackEfficiencyLevel >= JETPACK_EFFICIENCY_MAX_LEVEL)
                drainDesc = $"Energy drain minimized to {jetpackDrainPerLevel[JetpackEfficiencyLevel]:F0}/sec";
            else
                drainDesc = $"Reduces jetpack energy drain ({jetpackDrainPerLevel[JetpackEfficiencyLevel]:F0}/sec → {jetpackDrainPerLevel[JetpackEfficiencyLevel + 1]:F0}/sec)";

            CreateSimpleUpgradeSection(container.transform,
                "FUEL EFFICIENCY",
                drainDesc,
                JetpackEfficiencyLevel,
                JETPACK_EFFICIENCY_MAX_LEVEL,
                jetpackEfficiencyCosts,
                "jetpack_efficiency",
                true);

            // Future upgrades placeholder (bottom half) - locked
            CreateSimpleUpgradeSection(container.transform,
                "HOVER MODE (COMING SOON)",
                "Maintain altitude while flying",
                0,
                0, // maxLevel=0 means already "maxed" → shows MAXED box
                new int[] {},
                "jetpack_hover_locked",
                false);
        }

        // ===== ROBOT UPGRADE TRACKING =====
        public static bool RobotActivated => PlayerPrefs.GetInt("robot_activated", 0) == 1;
        public static bool RobotSmartStop => PlayerPrefs.GetInt("robot_smart_stop", 0) == 1;
        public static bool RobotEfficiency => PlayerPrefs.GetInt("robot_efficiency", 0) == 1;

        // ===== LOGISTICS ROBOT UPGRADE TRACKING =====
        public static bool LogisticsRobotActivated => PlayerPrefs.GetInt("logistics_robot_activated", 0) == 1;
        public static bool LogisticsAutonomous => PlayerPrefs.GetInt("logistics_autonomous", 0) == 1;
        public static int LogisticsCapacityLevel => PlayerPrefs.GetInt("logistics_capacity_level", 0);
        public static bool LogisticsAdvancedModule => PlayerPrefs.GetInt("logistics_advanced_module", 0) == 1;

        private void BuildRobotsTab()
        {
            bool activated = RobotActivated;
            bool smartStop = RobotSmartStop;

            CreateTwoColumnLayout(
                // Left column - Digger Robot
                new UpgradeItemData[] {
                    new UpgradeItemData("ACTIVATE DIGGER ROBOT",
                        "Deploy a digging robot. Shuts down when battery depletes — carry it back to dock.",
                        1500, false, !activated, "robot_activated"),
                    new UpgradeItemData("AUTONOMOUS MODE",
                        "Robot returns to dock at low battery, recharges, and resumes digging automatically.",
                        800, !activated, activated && !smartStop, "robot_smart_stop"),
                },
                // Right column - Logistics Robot
                new UpgradeItemData[] {
                    new UpgradeItemData("ACTIVATE LOGISTICS ROBOT",
                        "Deploy a resource collection robot. Shuts down when battery depletes — carry it back to dock.",
                        2000, false, !LogisticsRobotActivated, "logistics_robot_activated"),
                    new UpgradeItemData("AUTONOMOUS MODE",
                        "Robot returns to dock at low battery, recharges, and resumes collecting automatically.",
                        800, !LogisticsRobotActivated, LogisticsRobotActivated && !LogisticsAutonomous,
                        "logistics_autonomous"),
                }
            );
        }

        private void BuildSystemsTab()
        {
            CreateTwoColumnLayout(
                // Left column - Base Systems
                new UpgradeItemData[] {
                    new UpgradeItemData("HUB EXPANSION I", "Unlocks additional room", 0, true, false, ""),
                    new UpgradeItemData("HUB EXPANSION II", "Further base expansion", 0, true, false, ""),
                    new UpgradeItemData("FAST TRAVEL", "Teleport between waypoints", 0, true, false, ""),
                    new UpgradeItemData("AUTO-SORTING", "Resources sort automatically", 0, true, false, ""),
                },
                // Right column - Advanced Systems
                new UpgradeItemData[] {
                    new UpgradeItemData("DEPTH SCANNER", "Reveals resources on map", 0, true, false, ""),
                    new UpgradeItemData("DANGER DETECTOR", "Warns of hazards ahead", 0, true, false, ""),
                    new UpgradeItemData("ENVIRONMENT SHIELD", "Protection from elements", 0, true, false, ""),
                    new UpgradeItemData("QUANTUM STORAGE", "Infinite storage capacity", 0, true, false, ""),
                }
            );
        }

        private void CreateTwoColumnLayout(UpgradeItemData[] leftItems, UpgradeItemData[] rightItems)
        {
            GameObject container = new GameObject("ColumnsContainer");
            container.transform.SetParent(contentArea.transform, false);

            RectTransform containerRect = container.AddComponent<RectTransform>();
            containerRect.anchorMin = Vector2.zero;
            containerRect.anchorMax = Vector2.one;
            containerRect.offsetMin = new Vector2(15, 15);
            containerRect.offsetMax = new Vector2(-15, -15);

            HorizontalLayoutGroup hlg = container.AddComponent<HorizontalLayoutGroup>();
            hlg.spacing = 15;
            hlg.childControlWidth = true;
            hlg.childControlHeight = true;
            hlg.childForceExpandWidth = true;
            hlg.childForceExpandHeight = true;

            // Left column
            CreateUpgradeColumn(container.transform, leftItems, false);

            // Right column
            CreateUpgradeColumn(container.transform, rightItems, true);
        }

        private void CreateUpgradeColumn(Transform parent, UpgradeItemData[] items, bool isLockedColumn)
        {
            GameObject column = new GameObject(isLockedColumn ? "LockedColumn" : "AvailableColumn");
            column.transform.SetParent(parent, false);

            Image colBg = column.AddComponent<Image>();
            colBg.color = sectionBgColor;

            VerticalLayoutGroup vlg = column.AddComponent<VerticalLayoutGroup>();
            vlg.padding = new RectOffset(10, 10, 10, 10);
            vlg.spacing = 8;
            vlg.childControlWidth = true;
            vlg.childControlHeight = false;
            vlg.childForceExpandWidth = true;
            vlg.childForceExpandHeight = false;
            vlg.childAlignment = TextAnchor.UpperCenter;

            foreach (var item in items)
            {
                CreateUpgradeRow(column.transform, item);
            }
        }

        private void CreateUpgradeRow(Transform parent, UpgradeItemData item)
        {
            GameObject row = new GameObject($"Row_{item.name}");
            row.transform.SetParent(parent, false);

            LayoutElement le = row.AddComponent<LayoutElement>();
            le.minHeight = 60;
            le.preferredHeight = 60;

            Image rowBg = row.AddComponent<Image>();
            rowBg.color = item.isLocked ? lockedColor : rowBgLight;

            // Make locked items clickable to trigger demo end screen (Systems tab only)
            if (item.isLocked && currentTab == UpgradeTab.Systems)
            {
                Button rowBtn = row.AddComponent<Button>();
                rowBtn.targetGraphic = rowBg;

                ColorBlock colors = rowBtn.colors;
                colors.highlightedColor = new Color(lockedColor.r + 0.05f, lockedColor.g + 0.05f, lockedColor.b + 0.05f);
                colors.pressedColor = new Color(lockedColor.r + 0.1f, lockedColor.g + 0.1f, lockedColor.b + 0.1f);
                rowBtn.colors = colors;

                rowBtn.onClick.AddListener(() => OnLockedItemClicked());
            }

            HorizontalLayoutGroup hlg = row.AddComponent<HorizontalLayoutGroup>();
            hlg.padding = new RectOffset(10, 10, 8, 8);
            hlg.spacing = 10;
            hlg.childControlWidth = false;
            hlg.childControlHeight = true;
            hlg.childForceExpandWidth = false;
            hlg.childForceExpandHeight = true;
            hlg.childAlignment = TextAnchor.MiddleLeft;

            // Icon area
            GameObject iconArea = new GameObject("Icon");
            iconArea.transform.SetParent(row.transform, false);

            LayoutElement iconLe = iconArea.AddComponent<LayoutElement>();
            iconLe.minWidth = 44;
            iconLe.preferredWidth = 44;

            Image iconBg = iconArea.AddComponent<Image>();
            iconBg.color = item.isLocked ? new Color(0.25f, 0.25f, 0.28f) : new Color(0.15f, 0.35f, 0.4f);

            // Lock icon or upgrade icon
            GameObject iconText = new GameObject("IconText");
            iconText.transform.SetParent(iconArea.transform, false);

            RectTransform iconTextRect = iconText.AddComponent<RectTransform>();
            iconTextRect.anchorMin = Vector2.zero;
            iconTextRect.anchorMax = Vector2.one;
            iconTextRect.offsetMin = Vector2.zero;
            iconTextRect.offsetMax = Vector2.zero;

            TextMeshProUGUI iconTmp = iconText.AddComponent<TextMeshProUGUI>();
            iconTmp.text = item.isLocked ? "\u00B6" : "\u25B2"; // Lock symbol or arrow
            iconTmp.fontSize = 20;
            iconTmp.color = item.isLocked ? textGray : textCyan;
            iconTmp.alignment = TextAlignmentOptions.Center;

            // Text area
            GameObject textArea = new GameObject("TextArea");
            textArea.transform.SetParent(row.transform, false);

            LayoutElement textLe = textArea.AddComponent<LayoutElement>();
            textLe.flexibleWidth = 1;

            VerticalLayoutGroup textVlg = textArea.AddComponent<VerticalLayoutGroup>();
            textVlg.childControlWidth = true;
            textVlg.childControlHeight = true;
            textVlg.childForceExpandWidth = true;
            textVlg.childForceExpandHeight = false;

            // Name
            GameObject nameObj = new GameObject("Name");
            nameObj.transform.SetParent(textArea.transform, false);

            TextMeshProUGUI nameTmp = nameObj.AddComponent<TextMeshProUGUI>();
            nameTmp.text = item.name;
            nameTmp.fontSize = 16;
            nameTmp.fontStyle = FontStyles.Bold;
            nameTmp.color = item.isLocked ? textGray : textWhite;

            // Description
            GameObject descObj = new GameObject("Description");
            descObj.transform.SetParent(textArea.transform, false);

            TextMeshProUGUI descTmp = descObj.AddComponent<TextMeshProUGUI>();
            descTmp.text = item.description;
            descTmp.fontSize = 12;
            descTmp.color = item.isLocked ? new Color(0.5f, 0.4f, 0.3f) : textGray;

            // Button area (only for available items)
            if (!item.isLocked && item.canPurchase)
            {
                GameObject btnArea = new GameObject("ButtonArea");
                btnArea.transform.SetParent(row.transform, false);

                LayoutElement btnLe = btnArea.AddComponent<LayoutElement>();
                btnLe.minWidth = 100;
                btnLe.preferredWidth = 100;

                Image btnBg = btnArea.AddComponent<Image>();
                btnBg.color = buttonCyan;

                Button btn = btnArea.AddComponent<Button>();
                btn.targetGraphic = btnBg;

                string upgradeId = item.upgradeId;
                int cost = item.cost;
                btn.onClick.AddListener(() => TryPurchaseUpgrade(upgradeId, cost));

                GameObject btnText = new GameObject("BtnText");
                btnText.transform.SetParent(btnArea.transform, false);

                RectTransform btnTextRect = btnText.AddComponent<RectTransform>();
                btnTextRect.anchorMin = Vector2.zero;
                btnTextRect.anchorMax = Vector2.one;
                btnTextRect.offsetMin = Vector2.zero;
                btnTextRect.offsetMax = Vector2.zero;

                TextMeshProUGUI btnTmp = btnText.AddComponent<TextMeshProUGUI>();
                btnTmp.text = $"${item.cost}";
                btnTmp.fontSize = 14;
                btnTmp.fontStyle = FontStyles.Bold;
                btnTmp.color = textWhite;
                btnTmp.alignment = TextAlignmentOptions.Center;
            }
        }

        private void CreateLockedTabContent(string tabName, string statusText, string reasonText)
        {
            GameObject container = new GameObject("LockedContainer");
            container.transform.SetParent(contentArea.transform, false);

            RectTransform containerRect = container.AddComponent<RectTransform>();
            containerRect.anchorMin = Vector2.zero;
            containerRect.anchorMax = Vector2.one;
            containerRect.offsetMin = Vector2.zero;
            containerRect.offsetMax = Vector2.zero;

            VerticalLayoutGroup vlg = container.AddComponent<VerticalLayoutGroup>();
            vlg.childControlWidth = true;
            vlg.childControlHeight = false;
            vlg.childForceExpandWidth = true;
            vlg.childForceExpandHeight = false;
            vlg.childAlignment = TextAnchor.MiddleCenter;
            vlg.spacing = 20;
            vlg.padding = new RectOffset(50, 50, 150, 50);

            // Status text
            GameObject statusObj = new GameObject("Status");
            statusObj.transform.SetParent(container.transform, false);

            LayoutElement statusLe = statusObj.AddComponent<LayoutElement>();
            statusLe.preferredHeight = 40;

            TextMeshProUGUI statusTmp = statusObj.AddComponent<TextMeshProUGUI>();
            statusTmp.text = statusText;
            statusTmp.fontSize = 28;
            statusTmp.fontStyle = FontStyles.Bold;
            statusTmp.color = textOrange;
            statusTmp.alignment = TextAlignmentOptions.Center;

            // Reason text
            GameObject reasonObj = new GameObject("Reason");
            reasonObj.transform.SetParent(container.transform, false);

            LayoutElement reasonLe = reasonObj.AddComponent<LayoutElement>();
            reasonLe.preferredHeight = 30;

            TextMeshProUGUI reasonTmp = reasonObj.AddComponent<TextMeshProUGUI>();
            reasonTmp.text = reasonText;
            reasonTmp.fontSize = 18;
            reasonTmp.color = textGray;
            reasonTmp.alignment = TextAlignmentOptions.Center;
        }

        /// <summary>
        /// Called when player clicks on the Robots or Systems tab.
        /// Loads the Demo End scene additively (game stays in background).
        /// </summary>
        private void OnLockedTabClicked(UpgradeTab tab)
        {
            Debug.Log($"[FirstRoomUpgradeStationUI] Locked tab '{tab}' clicked - loading Demo End scene");

            // Hide the upgrade station UI (but keep game running in background)
            if (mainPanel != null)
            {
                mainPanel.SetActive(false);
            }

            // Pause the game
            Time.timeScale = 0f;

            // Subscribe to continue event to reopen UI when demo ends
            DemoEndEvents.OnContinuePlaying -= OnDemoEndContinue;
            DemoEndEvents.OnContinuePlaying += OnDemoEndContinue;

            // Load the Demo End scene additively (on top of current scene)
            SceneManager.LoadScene("DemoEndScene", LoadSceneMode.Additive);
        }

        /// <summary>
        /// Called when player clicks on a locked item in Robots or Systems tab.
        /// Loads the Demo End scene additively (game stays in background).
        /// </summary>
        private void OnLockedItemClicked()
        {
            Debug.Log("[FirstRoomUpgradeStationUI] Locked item clicked - loading Demo End scene");

            // Hide the upgrade station UI (but keep game running in background)
            if (mainPanel != null)
            {
                mainPanel.SetActive(false);
            }

            // Pause the game
            Time.timeScale = 0f;

            // Subscribe to continue event to reopen UI when demo ends
            DemoEndEvents.OnContinuePlaying -= OnDemoEndContinue;
            DemoEndEvents.OnContinuePlaying += OnDemoEndContinue;

            // Load the Demo End scene additively (on top of current scene)
            SceneManager.LoadScene("DemoEndScene", LoadSceneMode.Additive);
        }

        /// <summary>
        /// Called when player clicks Continue Playing on demo end screen.
        /// Reopens the upgrade station UI.
        /// </summary>
        private void OnDemoEndContinue()
        {
            Debug.Log("[FirstRoomUpgradeStationUI] Demo end continue - reopening upgrade station UI");

            // Unsubscribe
            DemoEndEvents.OnContinuePlaying -= OnDemoEndContinue;

            // Reshow the upgrade station UI
            if (mainPanel != null)
            {
                mainPanel.SetActive(true);
                Cursor.lockState = CursorLockMode.None;
                Cursor.visible = true;
                UIState.IsMachineUIOpen = true;
            }
        }

        private void TryPurchaseUpgrade(string upgradeId, int cost)
        {
            if (CurrencyManager.Instance == null)
            {
                Debug.LogWarning("[FirstRoomUpgradeStationUI] No CurrencyManager found!");
                return;
            }

            if (CurrencyManager.Instance.Spend(cost))
            {
                Debug.Log($"[FirstRoomUpgradeStationUI] Purchased upgrade: {upgradeId} for ${cost}");
                currentStation?.PlayUpgradeSound();
                ApplyUpgrade(upgradeId);
                // Refresh current tab to show updated state
                SelectTab(currentTab);
            }
            else
            {
                Debug.Log($"[FirstRoomUpgradeStationUI] Not enough credits for {upgradeId}");
            }
        }

        private void ApplyUpgrade(string upgradeId)
        {
            switch (upgradeId)
            {
                case "tool_power":
                    if (ToolPowerLevel < TOOL_POWER_MAX_LEVEL)
                    {
                        ToolPowerLevel++;
                        ApplyToolPowerUpgrade();
                        Debug.Log($"[Upgrade] Tool Power upgraded to level {ToolPowerLevel}! Multiplier: {toolPowerMultipliers[ToolPowerLevel]}x");
                    }
                    break;
                // Energy upgrades are now handled via TryPurchaseEnergyUpgrade() directly
                // No need for separate max_energy/energy_regen cases
                case "jetpack_efficiency":
                    if (JetpackEfficiencyLevel < JETPACK_EFFICIENCY_MAX_LEVEL)
                    {
                        JetpackEfficiencyLevel++;
                        var jetpack = FindObjectOfType<JetpackController>();
                        if (jetpack != null)
                        {
                            jetpack.SetEfficiencyLevel(JetpackEfficiencyLevel);
                        }
                        Debug.Log($"[Upgrade] Jetpack efficiency upgraded to level {JetpackEfficiencyLevel}! Drain: {jetpackDrainPerLevel[JetpackEfficiencyLevel]}/sec");
                    }
                    break;
                case "robot_activated":
                    PlayerPrefs.SetInt("robot_activated", 1);
                    PlayerPrefs.Save();
                    Debug.Log("[Upgrade] Digger Robot activated!");
                    break;
                case "robot_smart_stop":
                    PlayerPrefs.SetInt("robot_smart_stop", 1);
                    PlayerPrefs.Save();
                    Debug.Log("[Upgrade] Robot Smart Stop unlocked!");
                    break;
                case "robot_efficiency":
                    PlayerPrefs.SetInt("robot_efficiency", 1);
                    PlayerPrefs.Save();
                    Debug.Log("[Upgrade] Robot Energy Efficiency unlocked!");
                    break;
                case "logistics_robot_activated":
                    PlayerPrefs.SetInt("logistics_robot_activated", 1);
                    PlayerPrefs.Save();
                    Debug.Log("[Upgrade] Logistics Robot activated!");
                    break;
                case "logistics_autonomous":
                    PlayerPrefs.SetInt("logistics_autonomous", 1);
                    PlayerPrefs.Save();
                    Debug.Log("[Upgrade] Logistics Robot autonomous mode unlocked!");
                    break;
            }
        }

        /// <summary>
        /// Apply the tool power upgrade via UpgradeStation (works with both V2 and V3).
        /// </summary>
        private void ApplyToolPowerUpgrade()
        {
            float newMultiplier = toolPowerMultipliers[ToolPowerLevel];

            // Use UpgradeStation to apply the multiplier - it handles both V2 and V3
            UpgradeStation.SetFirstRoomToolPowerMultiplier(newMultiplier);

            Debug.Log($"[FirstRoomUpgradeStationUI] Applied dig power multiplier: {newMultiplier}x");
        }

        /// <summary>
        /// Get the current tool power multiplier based on upgrade level.
        /// </summary>
        public static float GetToolPowerMultiplier()
        {
            return toolPowerMultipliers[ToolPowerLevel];
        }

        private struct UpgradeItemData
        {
            public string name;
            public string description;
            public int cost;
            public bool isLocked;
            public bool canPurchase;
            public string upgradeId;

            public UpgradeItemData(string name, string description, int cost, bool isLocked, bool canPurchase, string upgradeId)
            {
                this.name = name;
                this.description = description;
                this.cost = cost;
                this.isLocked = isLocked;
                this.canPurchase = canPurchase;
                this.upgradeId = upgradeId;
            }
        }
    }
}
