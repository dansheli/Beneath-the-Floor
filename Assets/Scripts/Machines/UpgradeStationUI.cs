using UnityEngine;
using UnityEngine.UI;
using TMPro;
using System.Collections.Generic;
using BeneathTheFloor.Crafting;
using BeneathTheFloor.UI;
using BeneathTheFloor.Economy;

// For crosshair control

namespace BeneathTheFloor.Machines
{
    /// <summary>
    /// UpgradeStationUI - Two-column dashboard layout that fits on one screen.
    /// NO SCROLLING. Fixed height sections.
    /// LEFT: Tool Upgrades + Player Gear
    /// RIGHT: Energy System + Progression (Winch)
    /// </summary>
    public class UpgradeStationUI : MonoBehaviour
    {
        [Header("Main Panel")]
        [SerializeField] private GameObject stationPanel;

        [Header("Audio")]
        [SerializeField] private AudioClip openSound;
        [SerializeField] [Range(0f, 1f)] private float openSoundVolume = 0.7f;
        [SerializeField] private AudioClip buttonClickSound;
        [SerializeField] [Range(0f, 1f)] private float buttonClickVolume = 0.5f;
        [SerializeField] private AudioClip closeSound;
        [SerializeField] [Range(0f, 1f)] private float closeSoundVolume = 0.7f;

        [Header("Debug")]
        [SerializeField] private bool enableDebugLogs = false;

        public static UpgradeStationUI Instance { get; private set; }

        private UpgradeStation currentStation;
        private List<GameObject> upgradeRows = new List<GameObject>();
        private Dictionary<string, Button> upgradeButtons = new Dictionary<string, Button>();
        private Dictionary<string, TextMeshProUGUI> levelTexts = new Dictionary<string, TextMeshProUGUI>();
        private Dictionary<string, TextMeshProUGUI> effectTexts = new Dictionary<string, TextMeshProUGUI>();
        private Dictionary<string, TextMeshProUGUI> costTexts = new Dictionary<string, TextMeshProUGUI>();

        // UI References
        private TextMeshProUGUI creditsText;
        private Transform leftColumn;
        private Transform rightColumn;

        // Mission marker
        private GameObject winchMissionMarker;
        private bool showWinchMarker = false;

        // Upgrade locking for missions
        private static bool upgradesLockedExceptWinch = false;
        public static bool UpgradesLockedExceptWinch => upgradesLockedExceptWinch;

        // Audio
        private AudioSource audioSource;

        // Layout constants - FINAL SIZE (~25% larger than original)
        private const float PANEL_WIDTH = 1020f;      // +10% more (was 940)
        private const float PANEL_HEIGHT = 600f;      // +10% more (was 550)
        private const float HEADER_HEIGHT = 56f;      // Was 52
        private const float SECTION_HEADER_HEIGHT = 44f; // Section headers
        private const float ROW_HEIGHT = 115f;        // Large rows to fill space
        private const float TOOL_TIER_ROW_HEIGHT = 115f; // Match ROW_HEIGHT
        private const float COLUMN_SPACING = 12f;     // Was 10
        private const float CONTENT_PADDING = 14f;    // Was 12

        // Colors - Industrial terminal palette
        private readonly Color panelBgColor = new Color(0.16f, 0.14f, 0.13f, 0.98f);
        private readonly Color headerBgColor = new Color(0.12f, 0.11f, 0.10f, 1f);
        // Section headers - VERY DARK steel for maximum contrast
        private readonly Color sectionHeaderColor = new Color(0.08f, 0.07f, 0.06f, 1f);  // Nearly black steel
        private readonly Color sectionHeaderText = new Color(0.88f, 0.75f, 0.45f);       // Bright warm gold
        private readonly Color sectionAccentColor = new Color(0.65f, 0.50f, 0.25f);      // Left accent bar gold
        private readonly Color sectionDividerColor = new Color(0.45f, 0.38f, 0.25f, 1f); // Strong gold divider line
        // Rows - more muted, clearly different from headers
        private readonly Color rowBgDark = new Color(0.20f, 0.18f, 0.16f, 1f);   // Slightly lighter than before
        private readonly Color rowBgLight = new Color(0.23f, 0.21f, 0.18f, 1f);  // Slightly lighter than before
        private readonly Color frameBorder = new Color(0.28f, 0.25f, 0.22f, 1f);
        private readonly Color textWhite = new Color(0.92f, 0.90f, 0.86f);
        private readonly Color textGold = new Color(0.85f, 0.68f, 0.25f);
        private readonly Color textGray = new Color(0.55f, 0.52f, 0.48f);
        private readonly Color buttonGold = new Color(0.72f, 0.58f, 0.25f);
        private readonly Color buttonGoldHover = new Color(0.82f, 0.68f, 0.35f);
        private readonly Color buttonLocked = new Color(0.35f, 0.33f, 0.30f);
        private readonly Color closeRed = new Color(0.55f, 0.18f, 0.18f);

        private void Awake()
        {
            if (Instance == null) Instance = this;
            else { Destroy(gameObject); return; }

            // Create audio source for UI sounds
            audioSource = gameObject.AddComponent<AudioSource>();
            audioSource.playOnAwake = false;
            audioSource.spatialBlend = 0f; // 2D sound
        }

        private void Start()
        {
            if (stationPanel != null) stationPanel.SetActive(false);
        }

        private void OnDestroy()
        {
            if (Instance == this) Instance = null;
        }

        private void LogDebug(string message)
        {
            if (enableDebugLogs) Debug.Log(message);
        }

        public void ShowUI(UpgradeStation station)
        {
            currentStation = station;
            BuildUI();

            if (stationPanel == null)
            {
                Debug.LogError("[UpgradeStationUI] stationPanel is NULL after BuildUI!");
                return;
            }

            stationPanel.SetActive(true);

            // Play open sound
            if (openSound != null && audioSource != null)
            {
                audioSource.PlayOneShot(openSound, openSoundVolume);
            }

            if (CurrencyManager.Instance != null)
            {
                CurrencyManager.Instance.OnCurrencyChanged += OnCurrencyChanged;
            }

            station.OnUpgradeApplied += OnUpgradeApplied;

            UIState.IsMachineUIOpen = true;
            Cursor.lockState = CursorLockMode.None;
            Cursor.visible = true;

            // Hide crosshair when UI opens
            if (HUDController.Instance != null)
            {
                HUDController.Instance.SetCrosshairVisible(false);
            }

            RefreshAllUpgrades();
            UpdateCreditsDisplay();

            LogDebug("[UpgradeStationUI] ShowUI complete");
        }

        public void HideUI()
        {
            // Play close sound
            if (closeSound != null && audioSource != null)
            {
                audioSource.PlayOneShot(closeSound, closeSoundVolume);
            }

            if (stationPanel != null) stationPanel.SetActive(false);

            if (currentStation != null)
            {
                currentStation.OnUpgradeApplied -= OnUpgradeApplied;
            }

            if (CurrencyManager.Instance != null)
            {
                CurrencyManager.Instance.OnCurrencyChanged -= OnCurrencyChanged;
            }

            currentStation = null;
            UIState.IsMachineUIOpen = false;
            Cursor.lockState = CursorLockMode.Locked;
            Cursor.visible = false;

            // Restore crosshair when UI closes
            if (HUDController.Instance != null)
            {
                HUDController.Instance.SetCrosshairVisible(true);
            }

            LogDebug("[UpgradeStationUI] HideUI called");
        }

        /// <summary>
        /// Show mission marker on the winch cable upgrade row.
        /// Call this when a mission requires the player to upgrade the winch.
        /// </summary>
        public void ShowWinchMissionMarker()
        {
            showWinchMarker = true;
            if (winchMissionMarker != null)
            {
                winchMissionMarker.SetActive(true);
            }
            LogDebug("[UpgradeStationUI] Winch mission marker shown");
        }

        /// <summary>
        /// Hide the mission marker on the winch cable upgrade row.
        /// Call this when the mission is completed.
        /// </summary>
        public void HideWinchMissionMarker()
        {
            showWinchMarker = false;
            if (winchMissionMarker != null)
            {
                winchMissionMarker.SetActive(false);
            }
            LogDebug("[UpgradeStationUI] Winch mission marker hidden");
        }

        /// <summary>
        /// Lock all upgrades except winch during a mission.
        /// Call this when a mission requires the player to upgrade only the winch.
        /// Static method - works even if UI hasn't been opened yet.
        /// </summary>
        public static void LockUpgradesExceptWinch()
        {
            upgradesLockedExceptWinch = true;
            // Refresh UI if it's currently open
            if (Instance != null)
            {
                Instance.RefreshAllUpgrades();
                Instance.LogDebug("[UpgradeStationUI] Upgrades locked except winch");
            }
            Debug.Log("[UpgradeStationUI] Upgrades locked except winch (static)");
        }

        /// <summary>
        /// Unlock all upgrades after mission completion.
        /// Call this when the mission is completed.
        /// Static method - works even if UI hasn't been opened yet.
        /// </summary>
        public static void UnlockAllUpgrades()
        {
            upgradesLockedExceptWinch = false;
            // Refresh UI if it's currently open
            if (Instance != null)
            {
                Instance.RefreshAllUpgrades();
                Instance.LogDebug("[UpgradeStationUI] All upgrades unlocked");
            }
            Debug.Log("[UpgradeStationUI] All upgrades unlocked (static)");
        }

        private void BuildUI()
        {
            if (stationPanel != null)
            {
                Destroy(stationPanel);
                stationPanel = null;
            }

            upgradeRows.Clear();
            upgradeButtons.Clear();
            levelTexts.Clear();
            effectTexts.Clear();
            costTexts.Clear();

            Canvas canvas = GetComponentInParent<Canvas>();
            if (canvas == null) canvas = FindObjectOfType<Canvas>();
            if (canvas == null)
            {
                Debug.LogError("[UpgradeStationUI] No Canvas found!");
                return;
            }

            CreateStationPanel(canvas);
            LogDebug($"[UpgradeStationUI] UI Built - Canvas: {canvas.name}");
        }

        private void CreateStationPanel(Canvas canvas)
        {
            // === ROOT PANEL - FIXED SIZE ===
            stationPanel = new GameObject("UpgradeStationPanel");
            stationPanel.transform.SetParent(canvas.transform, false);

            RectTransform panelRect = stationPanel.AddComponent<RectTransform>();
            panelRect.anchorMin = new Vector2(0.5f, 0.5f);
            panelRect.anchorMax = new Vector2(0.5f, 0.5f);
            panelRect.pivot = new Vector2(0.5f, 0.5f);
            panelRect.sizeDelta = new Vector2(PANEL_WIDTH, PANEL_HEIGHT);

            Image panelBg = stationPanel.AddComponent<Image>();
            panelBg.color = panelBgColor;

            // Frame effects
            Outline outerFrame = stationPanel.AddComponent<Outline>();
            outerFrame.effectColor = new Color(0.10f, 0.09f, 0.08f, 1f);
            outerFrame.effectDistance = new Vector2(3, 3);

            Shadow dropShadow = stationPanel.AddComponent<Shadow>();
            dropShadow.effectColor = new Color(0, 0, 0, 0.6f);
            dropShadow.effectDistance = new Vector2(5, -5);

            // 1. HEADER BAR (fixed at top)
            CreateHeaderBar(stationPanel.transform);

            // 2. CONTENT AREA (two columns, no scroll)
            CreateContentArea(stationPanel.transform);
        }

        private void CreateHeaderBar(Transform parent)
        {
            GameObject header = new GameObject("Header");
            header.transform.SetParent(parent, false);

            RectTransform rect = header.AddComponent<RectTransform>();
            rect.anchorMin = new Vector2(0, 1);
            rect.anchorMax = new Vector2(1, 1);
            rect.pivot = new Vector2(0.5f, 1);
            rect.anchoredPosition = Vector2.zero;
            rect.sizeDelta = new Vector2(0, HEADER_HEIGHT);

            Image bg = header.AddComponent<Image>();
            bg.color = headerBgColor;

            // Title text
            GameObject titleObj = new GameObject("Title");
            titleObj.transform.SetParent(header.transform, false);
            RectTransform titleRect = titleObj.AddComponent<RectTransform>();
            titleRect.anchorMin = new Vector2(0, 0);
            titleRect.anchorMax = new Vector2(0.4f, 1);
            titleRect.offsetMin = new Vector2(20, 0);
            titleRect.offsetMax = Vector2.zero;

            TextMeshProUGUI titleText = titleObj.AddComponent<TextMeshProUGUI>();
            titleText.text = "Workbench";
            titleText.fontSize = 22;
            titleText.color = textWhite;
            titleText.alignment = TextAlignmentOptions.MidlineLeft;
            titleText.fontStyle = FontStyles.Bold;

            // Credits display
            GameObject creditsObj = new GameObject("Credits");
            creditsObj.transform.SetParent(header.transform, false);
            RectTransform creditsRect = creditsObj.AddComponent<RectTransform>();
            creditsRect.anchorMin = new Vector2(0.4f, 0);
            creditsRect.anchorMax = new Vector2(0.85f, 1);
            creditsRect.offsetMin = Vector2.zero;
            creditsRect.offsetMax = Vector2.zero;

            creditsText = creditsObj.AddComponent<TextMeshProUGUI>();
            creditsText.text = "Credits: 0";
            creditsText.fontSize = 18;
            creditsText.color = textWhite;
            creditsText.alignment = TextAlignmentOptions.MidlineRight;
            creditsText.fontStyle = FontStyles.Bold;

            // Close button
            GameObject closeObj = new GameObject("CloseButton");
            closeObj.transform.SetParent(header.transform, false);
            RectTransform closeRect = closeObj.AddComponent<RectTransform>();
            closeRect.anchorMin = new Vector2(1, 0.5f);
            closeRect.anchorMax = new Vector2(1, 0.5f);
            closeRect.pivot = new Vector2(1, 0.5f);
            closeRect.anchoredPosition = new Vector2(-10, 0);
            closeRect.sizeDelta = new Vector2(34, 34);

            Image closeBg = closeObj.AddComponent<Image>();
            closeBg.color = closeRed;

            Button closeBtn = closeObj.AddComponent<Button>();
            closeBtn.targetGraphic = closeBg;
            closeBtn.onClick.AddListener(OnCloseClicked);

            ColorBlock closeColors = closeBtn.colors;
            closeColors.normalColor = closeRed;
            closeColors.highlightedColor = new Color(0.7f, 0.25f, 0.25f);
            closeColors.pressedColor = new Color(0.45f, 0.12f, 0.12f);
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

        private void CreateContentArea(Transform parent)
        {
            // Content container (below header)
            GameObject content = new GameObject("Content");
            content.transform.SetParent(parent, false);

            RectTransform contentRect = content.AddComponent<RectTransform>();
            contentRect.anchorMin = new Vector2(0, 0);
            contentRect.anchorMax = new Vector2(1, 1);
            contentRect.offsetMin = new Vector2(CONTENT_PADDING, CONTENT_PADDING);
            contentRect.offsetMax = new Vector2(-CONTENT_PADDING, -HEADER_HEIGHT - 4);

            // HorizontalLayoutGroup for two columns
            HorizontalLayoutGroup hlg = content.AddComponent<HorizontalLayoutGroup>();
            hlg.spacing = COLUMN_SPACING;
            hlg.padding = new RectOffset(0, 0, 0, 0);
            hlg.childControlWidth = true;
            hlg.childControlHeight = true;
            hlg.childForceExpandWidth = true;
            hlg.childForceExpandHeight = true;

            // LEFT COLUMN
            GameObject leftCol = new GameObject("LeftColumn");
            leftCol.transform.SetParent(content.transform, false);

            RectTransform leftRect = leftCol.AddComponent<RectTransform>();
            LayoutElement leftLayout = leftCol.AddComponent<LayoutElement>();
            leftLayout.flexibleWidth = 1;

            Image leftBg = leftCol.AddComponent<Image>();
            leftBg.color = new Color(0, 0, 0, 0); // Transparent

            VerticalLayoutGroup leftVLG = leftCol.AddComponent<VerticalLayoutGroup>();
            leftVLG.spacing = 2;
            leftVLG.padding = new RectOffset(0, 0, 0, 0);
            leftVLG.childControlWidth = true;
            leftVLG.childControlHeight = false;
            leftVLG.childForceExpandWidth = true;
            leftVLG.childForceExpandHeight = false;

            leftColumn = leftCol.transform;

            // RIGHT COLUMN
            GameObject rightCol = new GameObject("RightColumn");
            rightCol.transform.SetParent(content.transform, false);

            RectTransform rightRect = rightCol.AddComponent<RectTransform>();
            LayoutElement rightLayout = rightCol.AddComponent<LayoutElement>();
            rightLayout.flexibleWidth = 1;

            Image rightBg = rightCol.AddComponent<Image>();
            rightBg.color = new Color(0, 0, 0, 0);

            VerticalLayoutGroup rightVLG = rightCol.AddComponent<VerticalLayoutGroup>();
            rightVLG.spacing = 2;
            rightVLG.padding = new RectOffset(0, 0, 0, 0);
            rightVLG.childControlWidth = true;
            rightVLG.childControlHeight = false;
            rightVLG.childForceExpandWidth = true;
            rightVLG.childForceExpandHeight = false;

            rightColumn = rightCol.transform;

            // Populate columns
            // LEFT: Tool Upgrades + Player Gear
            CreateToolUpgradesSection(leftColumn);
            CreatePlayerGearSection(leftColumn);

            // RIGHT: Energy System + Progression
            CreateEnergySystemSection(rightColumn);
            CreateProgressionSection(rightColumn);
        }

        private void CreateSectionHeader(Transform parent, string title)
        {
            GameObject header = new GameObject($"Section_{title}");
            header.transform.SetParent(parent, false);

            RectTransform rect = header.AddComponent<RectTransform>();
            rect.sizeDelta = new Vector2(0, SECTION_HEADER_HEIGHT);

            LayoutElement layout = header.AddComponent<LayoutElement>();
            layout.preferredHeight = SECTION_HEADER_HEIGHT;
            layout.minHeight = SECTION_HEADER_HEIGHT;

            // VERY DARK background - nearly black steel
            Image bg = header.AddComponent<Image>();
            bg.color = sectionHeaderColor;

            // Left accent bar (gold vertical stripe)
            GameObject accentBar = new GameObject("AccentBar");
            accentBar.transform.SetParent(header.transform, false);
            RectTransform accentRect = accentBar.AddComponent<RectTransform>();
            accentRect.anchorMin = new Vector2(0, 0);
            accentRect.anchorMax = new Vector2(0, 1);
            accentRect.pivot = new Vector2(0, 0.5f);
            accentRect.anchoredPosition = Vector2.zero;
            accentRect.sizeDelta = new Vector2(4, 0);  // 4px wide gold bar
            accentRect.offsetMin = new Vector2(0, 4);
            accentRect.offsetMax = new Vector2(4, -4);

            Image accentImg = accentBar.AddComponent<Image>();
            accentImg.color = sectionAccentColor;

            // Section title - BIG and BRIGHT gold
            GameObject textObj = new GameObject("Text");
            textObj.transform.SetParent(header.transform, false);
            RectTransform textRect = textObj.AddComponent<RectTransform>();
            textRect.anchorMin = Vector2.zero;
            textRect.anchorMax = Vector2.one;
            textRect.offsetMin = new Vector2(14, 0);  // Offset past accent bar
            textRect.offsetMax = new Vector2(0, -4);  // Room for divider

            TextMeshProUGUI tmp = textObj.AddComponent<TextMeshProUGUI>();
            tmp.text = title;
            tmp.fontSize = 18;  // Larger font
            tmp.color = sectionHeaderText;  // Bright warm gold
            tmp.alignment = TextAlignmentOptions.MidlineLeft;
            tmp.fontStyle = FontStyles.Bold;
            tmp.characterSpacing = 4f;  // Wide letter spacing for impact

            // Strong divider line under header
            GameObject divider = new GameObject("Divider");
            divider.transform.SetParent(header.transform, false);
            RectTransform divRect = divider.AddComponent<RectTransform>();
            divRect.anchorMin = new Vector2(0, 0);
            divRect.anchorMax = new Vector2(1, 0);
            divRect.pivot = new Vector2(0.5f, 0);
            divRect.anchoredPosition = Vector2.zero;
            divRect.sizeDelta = new Vector2(0, 2);  // 2px tall
            divRect.offsetMin = new Vector2(4, 0);  // Start at accent bar
            divRect.offsetMax = new Vector2(-4, 2);

            Image divImg = divider.AddComponent<Image>();
            divImg.color = sectionDividerColor;  // Gold divider line
        }

        private void CreateUpgradeRow(Transform parent, string upgradeId, string upgradeName, string description = null)
        {
            int rowIndex = upgradeRows.Count;
            float rowHeight = ROW_HEIGHT;

            GameObject row = new GameObject($"Row_{upgradeId}");
            row.transform.SetParent(parent, false);

            RectTransform rect = row.AddComponent<RectTransform>();
            rect.sizeDelta = new Vector2(0, rowHeight);

            LayoutElement layout = row.AddComponent<LayoutElement>();
            layout.preferredHeight = rowHeight;
            layout.minHeight = rowHeight;

            Image bg = row.AddComponent<Image>();
            bg.color = (rowIndex % 2 == 0) ? rowBgDark : rowBgLight;

            // ===== TOP ROW (55% height): Name | Level | Effect | Cost | Button =====

            // NAME (top-left)
            GameObject nameObj = new GameObject("Name");
            nameObj.transform.SetParent(row.transform, false);
            RectTransform nameRect = nameObj.AddComponent<RectTransform>();
            nameRect.anchorMin = new Vector2(0, 0.45f);
            nameRect.anchorMax = new Vector2(0.28f, 1);
            nameRect.offsetMin = new Vector2(14, 0);
            nameRect.offsetMax = new Vector2(0, -8);

            TextMeshProUGUI nameTMP = nameObj.AddComponent<TextMeshProUGUI>();
            nameTMP.text = upgradeName;
            nameTMP.fontSize = 20;
            nameTMP.color = textWhite;
            nameTMP.alignment = TextAlignmentOptions.BottomLeft;
            nameTMP.fontStyle = FontStyles.Bold;

            // LEVEL (top-center-left)
            GameObject levelObj = new GameObject("Level");
            levelObj.transform.SetParent(row.transform, false);
            RectTransform levelRect = levelObj.AddComponent<RectTransform>();
            levelRect.anchorMin = new Vector2(0.28f, 0.45f);
            levelRect.anchorMax = new Vector2(0.44f, 1);
            levelRect.offsetMin = Vector2.zero;
            levelRect.offsetMax = new Vector2(0, -8);

            TextMeshProUGUI levelTMP = levelObj.AddComponent<TextMeshProUGUI>();
            levelTMP.text = "L0 →1";
            levelTMP.fontSize = 18;
            levelTMP.color = textWhite;
            levelTMP.alignment = TextAlignmentOptions.Center;
            levelTexts[upgradeId] = levelTMP;

            // EFFECT (top-center)
            GameObject effectObj = new GameObject("Effect");
            effectObj.transform.SetParent(row.transform, false);
            RectTransform effectRect = effectObj.AddComponent<RectTransform>();
            effectRect.anchorMin = new Vector2(0.44f, 0.45f);
            effectRect.anchorMax = new Vector2(0.60f, 1);
            effectRect.offsetMin = Vector2.zero;
            effectRect.offsetMax = new Vector2(0, -8);

            TextMeshProUGUI effectTMP = effectObj.AddComponent<TextMeshProUGUI>();
            effectTMP.text = "+0%";
            effectTMP.fontSize = 18;
            effectTMP.color = textGold;
            effectTMP.alignment = TextAlignmentOptions.Center;
            effectTMP.fontStyle = FontStyles.Bold;
            effectTMP.enableWordWrapping = false;
            effectTMP.overflowMode = TextOverflowModes.Ellipsis;
            effectTexts[upgradeId] = effectTMP;

            // COST (top-center-right)
            GameObject costObj = new GameObject("Cost");
            costObj.transform.SetParent(row.transform, false);
            RectTransform costRect = costObj.AddComponent<RectTransform>();
            costRect.anchorMin = new Vector2(0.60f, 0.45f);
            costRect.anchorMax = new Vector2(0.76f, 1);
            costRect.offsetMin = Vector2.zero;
            costRect.offsetMax = new Vector2(0, -8);

            TextMeshProUGUI costTMP = costObj.AddComponent<TextMeshProUGUI>();
            costTMP.text = "+100";
            costTMP.fontSize = 17;
            costTMP.color = textGold;
            costTMP.alignment = TextAlignmentOptions.Center;
            costTexts[upgradeId] = costTMP;

            // BUTTON (top-right)
            GameObject btnObj = new GameObject("UpgradeButton");
            btnObj.transform.SetParent(row.transform, false);
            RectTransform btnRect = btnObj.AddComponent<RectTransform>();
            btnRect.anchorMin = new Vector2(0.76f, 0.48f);
            btnRect.anchorMax = new Vector2(1, 0.95f);
            btnRect.offsetMin = new Vector2(8, 4);
            btnRect.offsetMax = new Vector2(-10, -6);

            Image btnBg = btnObj.AddComponent<Image>();
            btnBg.color = buttonLocked;

            Button btn = btnObj.AddComponent<Button>();
            btn.targetGraphic = btnBg;
            btn.interactable = false;

            ColorBlock btnColors = btn.colors;
            btnColors.normalColor = buttonGold;
            btnColors.highlightedColor = buttonGoldHover;
            btnColors.pressedColor = new Color(0.6f, 0.48f, 0.18f);
            btnColors.disabledColor = buttonLocked;
            btnColors.colorMultiplier = 1f;
            btn.colors = btnColors;

            string capturedId = upgradeId;
            btn.onClick.AddListener(() => OnUpgradeClicked(capturedId));
            upgradeButtons[upgradeId] = btn;

            GameObject btnTextObj = new GameObject("Text");
            btnTextObj.transform.SetParent(btnObj.transform, false);
            RectTransform btnTextRect = btnTextObj.AddComponent<RectTransform>();
            btnTextRect.anchorMin = Vector2.zero;
            btnTextRect.anchorMax = Vector2.one;
            btnTextRect.offsetMin = Vector2.zero;
            btnTextRect.offsetMax = Vector2.zero;

            TextMeshProUGUI btnTMP = btnTextObj.AddComponent<TextMeshProUGUI>();
            btnTMP.text = "Upgrade";
            btnTMP.fontSize = 17;
            btnTMP.color = new Color(0.5f, 0.48f, 0.45f);
            btnTMP.alignment = TextAlignmentOptions.Center;
            btnTMP.fontStyle = FontStyles.Bold;
            btnTMP.raycastTarget = false;

            // ===== BOTTOM ROW (45% height): Description =====
            if (!string.IsNullOrEmpty(description))
            {
                GameObject descObj = new GameObject("Description");
                descObj.transform.SetParent(row.transform, false);
                RectTransform descRect = descObj.AddComponent<RectTransform>();
                descRect.anchorMin = new Vector2(0, 0);
                descRect.anchorMax = new Vector2(0.75f, 0.45f);
                descRect.offsetMin = new Vector2(14, 8);
                descRect.offsetMax = new Vector2(0, 0);

                TextMeshProUGUI descTMP = descObj.AddComponent<TextMeshProUGUI>();
                descTMP.text = description;
                descTMP.fontSize = 14;
                descTMP.color = new Color(0.65f, 0.62f, 0.58f);  // Lighter gray for readability
                descTMP.alignment = TextAlignmentOptions.TopLeft;
                descTMP.fontStyle = FontStyles.Italic;
            }

            upgradeRows.Add(row);
        }

        // ==================== SECTION BUILDERS ====================

        private void CreateToolUpgradesSection(Transform parent)
        {
            CreateSectionHeader(parent, "TOOL UPGRADES");
            CreateUpgradeRow(parent, "dig_power", "Dig Power",
                "Break through terrain faster and dig wider holes");
        }

        private void CreatePlayerGearSection(Transform parent)
        {
            CreateSectionHeader(parent, "PLAYER GEAR");

            CreateUpgradeRow(parent, "backpack", "Backpack",
                "Carry more items and stack them higher in your inventory");
            CreateUpgradeRow(parent, "headlamp", "Headlamp",
                "See further in the dark underground tunnels");
        }

        private void CreateEnergySystemSection(Transform parent)
        {
            CreateSectionHeader(parent, "ENERGY SYSTEM");

            CreateUpgradeRow(parent, "energy_capacity", "Energy Capacity",
                "Store more energy for longer digging sessions");
        }

        private void CreateProgressionSection(Transform parent)
        {
            CreateSectionHeader(parent, "WINCH SYSTEM");

            // Winch upgrade row (combined: length + speed + power)
            CreateWinchRow(parent);
        }

        private void CreateWinchRow(Transform parent)
        {
            string upgradeId = "winch_cable";
            int rowIndex = upgradeRows.Count;

            GameObject row = new GameObject($"Row_{upgradeId}");
            row.transform.SetParent(parent, false);

            RectTransform rect = row.AddComponent<RectTransform>();
            rect.sizeDelta = new Vector2(0, ROW_HEIGHT);

            LayoutElement layout = row.AddComponent<LayoutElement>();
            layout.preferredHeight = ROW_HEIGHT;
            layout.minHeight = ROW_HEIGHT;

            Image bg = row.AddComponent<Image>();
            bg.color = (rowIndex % 2 == 0) ? rowBgDark : rowBgLight;

            // ===== TOP ROW (55% height): Marker + Name | Depth | Cost | Button =====

            // Mission Diamond Marker (top-left corner)
            GameObject markerObj = new GameObject("MissionMarker");
            markerObj.transform.SetParent(row.transform, false);
            RectTransform markerRect = markerObj.AddComponent<RectTransform>();
            markerRect.anchorMin = new Vector2(0, 0.7f);
            markerRect.anchorMax = new Vector2(0, 0.7f);
            markerRect.pivot = new Vector2(0.5f, 0.5f);
            markerRect.anchoredPosition = new Vector2(20, 0);
            markerRect.sizeDelta = new Vector2(14, 14);

            Image markerImage = markerObj.AddComponent<Image>();
            markerImage.color = new Color(1f, 0.85f, 0.2f, 1f);
            markerObj.transform.localRotation = Quaternion.Euler(0, 0, 45);

            winchMissionMarker = markerObj;
            markerObj.SetActive(showWinchMarker);

            // NAME (top-left, offset for marker)
            GameObject nameObj = new GameObject("Name");
            nameObj.transform.SetParent(row.transform, false);
            RectTransform nameRect = nameObj.AddComponent<RectTransform>();
            nameRect.anchorMin = new Vector2(0, 0.45f);
            nameRect.anchorMax = new Vector2(0.28f, 1);
            nameRect.offsetMin = new Vector2(14, 0);
            nameRect.offsetMax = new Vector2(0, -8);

            TextMeshProUGUI nameTMP = nameObj.AddComponent<TextMeshProUGUI>();
            nameTMP.text = "Winch Cable";
            nameTMP.fontSize = 20;
            nameTMP.color = textWhite;
            nameTMP.alignment = TextAlignmentOptions.BottomLeft;
            nameTMP.fontStyle = FontStyles.Bold;

            // DEPTH (top-center-left) - replaces Level
            GameObject depthObj = new GameObject("Depth");
            depthObj.transform.SetParent(row.transform, false);
            RectTransform depthRect = depthObj.AddComponent<RectTransform>();
            depthRect.anchorMin = new Vector2(0.28f, 0.45f);
            depthRect.anchorMax = new Vector2(0.60f, 1);
            depthRect.offsetMin = Vector2.zero;
            depthRect.offsetMax = new Vector2(0, -8);

            TextMeshProUGUI depthTMP = depthObj.AddComponent<TextMeshProUGUI>();
            depthTMP.text = "<color=#D9AD40>100m</color> → <color=#D9AD40>150m</color>";
            depthTMP.fontSize = 18;
            depthTMP.color = textWhite;
            depthTMP.alignment = TextAlignmentOptions.Center;
            levelTexts[upgradeId] = depthTMP;

            // COST (top-center-right)
            GameObject costObj = new GameObject("Cost");
            costObj.transform.SetParent(row.transform, false);
            RectTransform costRect = costObj.AddComponent<RectTransform>();
            costRect.anchorMin = new Vector2(0.60f, 0.45f);
            costRect.anchorMax = new Vector2(0.76f, 1);
            costRect.offsetMin = Vector2.zero;
            costRect.offsetMax = new Vector2(0, -8);

            TextMeshProUGUI costTMP = costObj.AddComponent<TextMeshProUGUI>();
            costTMP.text = "+75";
            costTMP.fontSize = 17;
            costTMP.color = textGold;
            costTMP.alignment = TextAlignmentOptions.Center;
            costTexts[upgradeId] = costTMP;

            // BUTTON (top-right)
            GameObject btnObj = new GameObject("UpgradeButton");
            btnObj.transform.SetParent(row.transform, false);
            RectTransform btnRect = btnObj.AddComponent<RectTransform>();
            btnRect.anchorMin = new Vector2(0.76f, 0.48f);
            btnRect.anchorMax = new Vector2(1, 0.95f);
            btnRect.offsetMin = new Vector2(8, 4);
            btnRect.offsetMax = new Vector2(-10, -6);

            Image btnBg = btnObj.AddComponent<Image>();
            btnBg.color = buttonLocked;

            Button btn = btnObj.AddComponent<Button>();
            btn.targetGraphic = btnBg;
            btn.interactable = false;

            ColorBlock btnColors = btn.colors;
            btnColors.normalColor = buttonGold;
            btnColors.highlightedColor = buttonGoldHover;
            btnColors.pressedColor = new Color(0.6f, 0.48f, 0.18f);
            btnColors.disabledColor = buttonLocked;
            btnColors.colorMultiplier = 1f;
            btn.colors = btnColors;

            btn.onClick.AddListener(() => OnUpgradeClicked(upgradeId));
            upgradeButtons[upgradeId] = btn;

            GameObject btnTextObj = new GameObject("Text");
            btnTextObj.transform.SetParent(btnObj.transform, false);
            RectTransform btnTextRect = btnTextObj.AddComponent<RectTransform>();
            btnTextRect.anchorMin = Vector2.zero;
            btnTextRect.anchorMax = Vector2.one;
            btnTextRect.offsetMin = Vector2.zero;
            btnTextRect.offsetMax = Vector2.zero;

            TextMeshProUGUI btnTMP = btnTextObj.AddComponent<TextMeshProUGUI>();
            btnTMP.text = "Upgrade";
            btnTMP.fontSize = 17;
            btnTMP.color = new Color(0.5f, 0.48f, 0.45f);
            btnTMP.alignment = TextAlignmentOptions.Center;
            btnTMP.fontStyle = FontStyles.Bold;
            btnTMP.raycastTarget = false;

            // ===== BOTTOM ROW (45% height): Description =====
            GameObject descObj = new GameObject("Description");
            descObj.transform.SetParent(row.transform, false);
            RectTransform descRect = descObj.AddComponent<RectTransform>();
            descRect.anchorMin = new Vector2(0, 0);
            descRect.anchorMax = new Vector2(0.75f, 0.45f);
            descRect.offsetMin = new Vector2(14, 8);
            descRect.offsetMax = new Vector2(0, 0);

            TextMeshProUGUI descTMP = descObj.AddComponent<TextMeshProUGUI>();
            descTMP.text = "Descend deeper into the ground to find rarer resources";
            descTMP.fontSize = 14;
            descTMP.color = new Color(0.65f, 0.62f, 0.58f);
            descTMP.alignment = TextAlignmentOptions.TopLeft;
            descTMP.fontStyle = FontStyles.Italic;

            upgradeRows.Add(row);
        }

        // ==================== DATA BINDING ====================

        private void RefreshAllUpgrades()
        {
            if (currentStation == null) return;

            var runtimeUpgrades = currentStation.RuntimeUpgrades;

            foreach (var upgrade in runtimeUpgrades)
            {
                RefreshUpgradeRow(upgrade);
            }
        }

        private void RefreshUpgradeRow(RuntimeUpgrade upgrade)
        {
            string id = MapUpgradeToRowId(upgrade.upgradeId);
            if (string.IsNullOrEmpty(id))
            {
                return;
            }

            // Update level text
            if (levelTexts.TryGetValue(id, out var levelText))
            {
                if (id == "winch_cable")
                {
                    float current = upgrade.GetCurrentValue();
                    float next = upgrade.GetNextValue();
                    if (upgrade.IsMaxLevel)
                    {
                        levelText.text = $"<color=#D9AD40>{current:F0}m</color> (MAX)";
                    }
                    else
                    {
                        levelText.text = $"<color=#D9AD40>{current:F0}m</color> → <color=#D9AD40>{next:F0}m</color>";
                    }
                }
                else if (id == "dig_power")
                {
                    // Special handling for dig_power: show next tool when at max level
                    if (upgrade.IsMaxLevel)
                    {
                        if (currentStation != null && currentStation.CanUpgradeToolTier())
                        {
                            // Show next tool tier
                            string nextTool = UpgradeStation.GetNextToolTierName();
                            levelText.text = $"L{upgrade.currentLevel} →{nextTool}";
                        }
                        else
                        {
                            // Truly maxed (Tool 3 L3)
                            levelText.text = $"L{upgrade.currentLevel} MAX";
                        }
                    }
                    else
                    {
                        levelText.text = $"L{upgrade.currentLevel} →{upgrade.currentLevel + 1}";
                    }
                }
                else
                {
                    if (upgrade.IsMaxLevel)
                    {
                        levelText.text = $"L{upgrade.currentLevel} MAX";
                    }
                    else
                    {
                        levelText.text = $"L{upgrade.currentLevel} →{upgrade.currentLevel + 1}";
                    }
                }
            }

            // Update effect text
            if (effectTexts.TryGetValue(id, out var effectText))
            {
                effectText.text = GetEffectDisplayText(upgrade);
            }

            // Update cost text
            if (costTexts.TryGetValue(id, out var costText))
            {
                // Check if dig_power can transition to next tool tier
                bool canTierTransition = (id == "dig_power") && upgrade.IsMaxLevel &&
                                         currentStation != null && currentStation.CanUpgradeToolTier();

                if (upgrade.IsMaxLevel && !canTierTransition)
                {
                    costText.text = "MAX";
                    costText.color = new Color(0.5f, 0.8f, 0.5f);
                }
                else
                {
                    int cost;
                    if (canTierTransition)
                    {
                        // Get tier transition cost from tool_tier upgrade
                        var tierUpgrade = currentStation?.GetRuntimeUpgradeById("tool_tier");
                        cost = tierUpgrade?.GetNextLevelCreditCost() ?? 450;
                    }
                    else
                    {
                        cost = upgrade.GetNextLevelCreditCost();
                    }
                    costText.text = $"+{cost}";
                    costText.color = textGold;
                }
            }

            // Update button state
            if (upgradeButtons.TryGetValue(id, out var button))
            {
                // Check if dig_power can transition to next tool tier
                bool canTierTransition = (id == "dig_power") && upgrade.IsMaxLevel &&
                                         currentStation != null && currentStation.CanUpgradeToolTier();

                // Truly maxed = dig_power at L3 AND on Tool 3 (no more tiers)
                bool isTrulyMaxed = (id == "dig_power") && upgrade.IsMaxLevel && !canTierTransition;
                bool isMaxed = upgrade.IsMaxLevel && !canTierTransition;

                // Get upgrade cost (use tier cost for dig_power tier transition)
                int upgradeCost;
                if (canTierTransition)
                {
                    var tierUpgrade = currentStation?.GetRuntimeUpgradeById("tool_tier");
                    upgradeCost = tierUpgrade?.GetNextLevelCreditCost() ?? 450;
                }
                else
                {
                    upgradeCost = upgrade.GetNextLevelCreditCost();
                }

                // Check if player can AFFORD this upgrade (has enough credits)
                bool canAfford = false;
                int currentCredits = 0;
                if (CurrencyManager.Instance != null)
                {
                    currentCredits = CurrencyManager.Instance.CurrentAmount;
                    canAfford = CurrencyManager.Instance.CanAfford(upgradeCost);
                }

                // No special prerequisites needed anymore (tier transition handled automatically)
                bool toolTierPrereqsMet = true;

                // Check if this upgrade is locked by mission (only winch allowed)
                bool isLockedByMission = upgradesLockedExceptWinch && id != "winch_cable";

                // Button is ENABLED only if:
                // 1. Not maxed
                // 2. Can afford (has credits)
                // 3. Prerequisites met (for Tool Tier)
                // 4. Not locked by mission
                bool canPurchase = !isMaxed && !isTrulyMaxed && canAfford && toolTierPrereqsMet && !isLockedByMission;
                button.interactable = canPurchase;

                // DIRECTLY set button background color (ColorBlock doesn't update reliably)
                var btnImage = button.GetComponent<Image>();
                if (btnImage != null)
                {
                    if (isTrulyMaxed || isMaxed)
                    {
                        btnImage.color = new Color(0.3f, 0.5f, 0.3f);  // Green-ish for MAX
                    }
                    else if (canPurchase)
                    {
                        btnImage.color = buttonGold;  // Gold when can purchase
                    }
                    else
                    {
                        btnImage.color = buttonLocked;  // Gray when cannot purchase
                    }
                }

                var btnText = button.GetComponentInChildren<TextMeshProUGUI>();
                if (btnText != null)
                {
                    if (isTrulyMaxed || isMaxed)
                    {
                        btnText.text = "MAX";
                        btnText.color = new Color(0.85f, 0.95f, 0.85f);  // Light green on green bg
                    }
                    else if (isLockedByMission)
                    {
                        // Locked by mission - show LOCKED
                        btnText.text = "LOCKED";
                        btnText.color = new Color(0.5f, 0.48f, 0.45f);  // Light text on gray
                    }
                    else if (!toolTierPrereqsMet)
                    {
                        // ONLY Tool Tier can show LOCKED (when prerequisites not met)
                        btnText.text = "LOCKED";
                        btnText.color = new Color(0.5f, 0.48f, 0.45f);  // Light text on gray
                    }
                    else if (canPurchase)
                    {
                        btnText.text = "Upgrade";
                        btnText.color = new Color(0.12f, 0.10f, 0.08f);  // Dark text on gold
                    }
                    else
                    {
                        // Cannot afford - disabled
                        btnText.text = "Upgrade";
                        btnText.color = new Color(0.5f, 0.48f, 0.45f);  // Light text on gray
                    }
                }
            }
        }

        private string MapUpgradeToRowId(string upgradeId)
        {
            // Maps data upgrade IDs to UI row IDs
            switch (upgradeId)
            {
                // Tool upgrades (data ID -> UI row ID)
                case "tool_power": return "dig_power";      // Data: tool_power, UI: dig_power
                case "tool_tier": return "tool_upgrade";    // Data: tool_tier, UI: tool_upgrade

                // Player gear
                case "inventory_size": return "backpack";   // Data: inventory_size, UI: backpack
                case "headlamp": return "headlamp";

                // Energy system
                case "energy_capacity": return "energy_capacity";

                // Progression / Winch System
                case "winch_cable": return "winch_cable";
                // winch_motor_speed and winch_motor_power removed - now combined in winch_cable

                // Unmapped (not shown in UI)
                case "dig_speed": return null;      // Not in current UI layout
                case "move_speed": return null;     // Not in current UI layout
                case "lamp_purchase": return null;  // Not in current UI layout

                default: return null;
            }
        }

        private string GetEffectDisplayText(RuntimeUpgrade upgrade)
        {
            float current = upgrade.GetCurrentValue();
            float next = upgrade.GetNextValue();

            switch (upgrade.upgradeType)
            {
                case UpgradeType.ToolPower:
                case UpgradeType.DigPower:
                    float bonus = (next - 1f) * 100f;
                    return $"+{bonus:F0}%";

                case UpgradeType.EnergyCapacity:
                    return $"+{next:F0}";

                case UpgradeType.InventorySize:
                    // Show what the next upgrade provides
                    var inv = BeneathTheFloor.Inventory.InventorySystem.Instance;
                    if (inv != null)
                    {
                        return inv.GetNextUpgradeDescription();
                    }
                    return upgrade.currentLevel == 0 ? "More Slots" : "Stack +1";

                case UpgradeType.LightRadius:
                    return $"Wider";

                case UpgradeType.ToolLevelUp:
                case UpgradeType.ToolTier:
                    if (UpgradeStation.IsToolAtTierTransition(upgrade))
                    {
                        return "TIER UP";
                    }
                    return $"+{((next - 1f) * 100f):F0}%";

                case UpgradeType.WinchCableLength:
                    return "";

                // WinchMotorSpeed and WinchMotorPower removed - now combined in WinchCableLength

                default:
                    return $"+{next:F1}";
            }
        }

        // ==================== EVENT HANDLERS ====================

        private void OnUpgradeClicked(string rowId)
        {
            // Play button click sound
            if (buttonClickSound != null && audioSource != null)
            {
                audioSource.PlayOneShot(buttonClickSound, buttonClickVolume);
            }

            if (currentStation == null) return;

            var runtimeUpgrades = currentStation.RuntimeUpgrades;
            foreach (var upgrade in runtimeUpgrades)
            {
                if (MapUpgradeToRowId(upgrade.upgradeId) == rowId)
                {
                    if (currentStation.CanPurchaseRuntimeUpgrade(upgrade))
                    {
                        if (currentStation.PurchaseRuntimeUpgrade(upgrade))
                        {
                            LogDebug($"[UpgradeStationUI] Purchased {upgrade.upgradeName}");
                            RefreshAllUpgrades();
                            UpdateCreditsDisplay();
                        }
                    }
                    return;
                }
            }
        }

        private void OnCurrencyChanged(int newAmount)
        {
            UpdateCreditsDisplay();
            RefreshAllUpgrades();
        }

        private void OnUpgradeApplied(UpgradeNode upgrade)
        {
            RefreshAllUpgrades();
            UpdateCreditsDisplay();
        }

        private void UpdateCreditsDisplay()
        {
            if (creditsText != null && CurrencyManager.Instance != null)
            {
                creditsText.text = $"Credits: {CurrencyManager.Instance.CurrentAmount:N0}";
            }
        }

        private void OnCloseClicked()
        {
            if (currentStation != null)
            {
                currentStation.CloseStation();
            }
            else
            {
                HideUI();
            }
        }
    }
}
