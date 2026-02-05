using UnityEngine;
using UnityEngine.UI;
using TMPro;
using System.Collections.Generic;
using BeneathTheFloor.Inventory;
using BeneathTheFloor.UI;
using BeneathTheFloor.World;

namespace BeneathTheFloor.InventoryUI
{
    /// <summary>
    /// Storage Tray style Inventory UI.
    /// Minimal, physical feel - like looking at items in a tray.
    /// NO stacking, NO detail panel, NO trash area.
    /// Action strip only appears when item is selected.
    /// </summary>
    public class InventoryUIManager : MonoBehaviour
    {
        [Header("Settings")]
        [SerializeField] private KeyCode toggleKey = KeyCode.I;

        // HARDCODED: Always start with exactly 5 slots
        private const int INITIAL_SLOT_COUNT = 5;
        private const int SLOTS_PER_ROW = 5;

        // ============================================================
        // PROFESSIONAL LAYOUT CONSTANTS (MATCHING REFERENCE #2)
        // ============================================================
        // Slot dimensions
        private readonly Vector2 slotSize = new Vector2(100, 100);
        private const float slotSpacing = 20f;

        // Panel scale factor
        private const float panelScale = 1.10f;

        // Tray padding
        private const float trayPadding = 20f;

        // Action bar layout - THINNER FOOTER, CLOSER TO BOTTOM
        private const float actionBarHeight = 58f;      // Thinner footer (was 84)
        private const float actionBarBottomPad = 12f;   // Closer to bottom frame (was 18)
        private const float actionBarInnerPad = 8f;     // Tighter inner padding
        private const float slotsToBarGap = 24f;        // More gap between slots and footer

        // Button layout - BOTTOM ALIGNED IN FOOTER
        private const float buttonHeight = 38f;         // Slightly shorter buttons
        private const float buttonSpacingH = 12f;       // Spacing between buttons
        private const float buttonRightPad = 16f;       // Right padding
        private const float buttonBottomPad = 8f;       // Bottom padding within footer
        private const float dropButtonWidth = 72f;
        private const float destroyButtonWidth = 86f;
        private const float sortButtonWidth = 60f;

        // Left info area - Reserve ~50% for item info
        private const float leftInfoWidth = 200f;       // Fixed width

        [Header("Debug")]
        [SerializeField] private bool debugMode = false;

        // UI References (created at runtime)
        private GameObject inventoryPanel;
        private Transform slotContainer;
        private GameObject actionStrip;
        private GameObject actionStripItemInfo;  // Left side info group (hidden when no selection)
        private Image actionStripItemIcon;
        private TextMeshProUGUI actionStripItemName;
        private TextMeshProUGUI actionStripItemValue;
        private Button dropButton;
        private Button destroyButton;
        private Button sortButton;

        // State
        private List<InventorySlotUI> slots = new List<InventorySlotUI>();
        private int selectedSlotIndex = -1;
        private bool isInitialized = false;

        // Colors - Metal Tray Industrial Palette (matching reference)
        // Outer frame layers
        private readonly Color frameOuterDark = new Color(0.08f, 0.07f, 0.06f, 1f);      // Darkest outer edge
        private readonly Color frameMetalMid = new Color(0.20f, 0.19f, 0.17f, 1f);       // Mid metal tone
        private readonly Color frameMetalLight = new Color(0.28f, 0.26f, 0.23f, 1f);     // Lighter bevel highlight
        private readonly Color frameInnerShadow = new Color(0.05f, 0.04f, 0.03f, 1f);    // Inner shadow

        // Tray interior
        private readonly Color trayBase = new Color(0.12f, 0.11f, 0.10f, 1f);            // Dark tray floor
        private readonly Color trayWorn = new Color(0.15f, 0.14f, 0.12f, 1f);            // Worn metal texture

        // Slot compartment colors
        private readonly Color slotInsetDark = new Color(0.06f, 0.05f, 0.04f, 1f);       // Deep inset shadow
        private readonly Color slotInsetMid = new Color(0.10f, 0.09f, 0.08f, 1f);        // Slot floor
        private readonly Color slotBevelLight = new Color(0.22f, 0.20f, 0.18f, 1f);      // Top/left bevel (light)
        private readonly Color slotBevelDark = new Color(0.08f, 0.07f, 0.06f, 1f);       // Bottom/right bevel (shadow)

        // Selection highlight
        private readonly Color selectGlowOuter = new Color(0.85f, 0.65f, 0.25f, 0.6f);   // Warm gold glow
        private readonly Color selectGlowInner = new Color(0.95f, 0.75f, 0.35f, 0.8f);   // Brighter inner
        private readonly Color selectBorder = new Color(0.90f, 0.70f, 0.30f, 1f);        // Gold border

        // Title plaque
        private readonly Color plaqueMetal = new Color(0.25f, 0.23f, 0.20f, 1f);         // Metal plaque bg
        private readonly Color plaqueHighlight = new Color(0.35f, 0.32f, 0.28f, 1f);     // Top edge highlight
        private readonly Color titleGold = new Color(0.92f, 0.78f, 0.35f, 1f);           // Gold text

        // Text colors
        private readonly Color textBright = new Color(0.95f, 0.92f, 0.85f, 1f);          // Bright text
        private readonly Color textMuted = new Color(0.70f, 0.65f, 0.55f, 1f);           // Muted labels
        private readonly Color valueGold = new Color(0.85f, 0.72f, 0.30f, 1f);           // Value display

        // Action strip
        private readonly Color stripBg = new Color(0.10f, 0.09f, 0.08f, 0.95f);          // Strip background
        private readonly Color buttonNormal = new Color(0.22f, 0.20f, 0.18f, 1f);        // Normal button
        private readonly Color buttonHover = new Color(0.30f, 0.27f, 0.24f, 1f);         // Hover state
        private readonly Color buttonDestroy = new Color(0.50f, 0.20f, 0.18f, 1f);       // Destroy button (red tint)
        private readonly Color buttonDestroyHover = new Color(0.60f, 0.25f, 0.22f, 1f);  // Destroy hover

        public static InventoryUIManager Instance { get; private set; }
        public bool IsOpen => inventoryPanel != null && inventoryPanel.activeSelf;

        /// <summary>
        /// Returns the current slot count.
        /// Uses InventorySystem if available, otherwise returns INITIAL_SLOT_COUNT (5).
        /// </summary>
        public int SlotCount
        {
            get
            {
                if (InventorySystem.Instance != null)
                {
                    return InventorySystem.Instance.MaxSlots;
                }
                return INITIAL_SLOT_COUNT;
            }
        }

        // Legacy compatibility
        public InventoryDragController DragController { get; private set; }

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
            Initialize();
        }

        private void Update()
        {
            // Toggle inventory with key
            if (Input.GetKeyDown(toggleKey))
            {
                if (!UIState.IsPauseMenuOpen)
                {
                    ToggleInventory();
                }
            }
        }

        private void LateUpdate()
        {
            // Close with Escape - consume escape to prevent pause menu from opening
            if (IsOpen && Input.GetKeyDown(KeyCode.Escape))
            {
                UI.UIState.ConsumeEscape();
                CloseInventory();
            }
        }

        private void Initialize()
        {
            if (isInitialized)
            {
                return;
            }

            // Subscribe to inventory changes
            if (InventorySystem.Instance != null)
            {
                InventorySystem.Instance.OnInventoryChanged += RefreshAllSlots;
                InventorySystem.Instance.OnInventoryCapacityChanged += HandleCapacityChanged;
            }

            GameEvents.OnInventoryFull += HandleInventoryFull;

            // Create the UI
            CreateInventoryUI();

            isInitialized = true;
        }

        private void OnDestroy()
        {
            if (InventorySystem.Instance != null)
            {
                InventorySystem.Instance.OnInventoryChanged -= RefreshAllSlots;
                InventorySystem.Instance.OnInventoryCapacityChanged -= HandleCapacityChanged;
            }
            GameEvents.OnInventoryFull -= HandleInventoryFull;
        }

        #region UI Creation

        private void CreateInventoryUI()
        {
            // Clean up any existing inventory panels first
            var existingPanels = GameObject.FindObjectsOfType<RectTransform>();
            foreach (var rt in existingPanels)
            {
                if (rt.gameObject.name == "InventoryPanel")
                {
                    Destroy(rt.gameObject);
                }
            }

            Canvas canvas = FindMainCanvas();
            if (canvas == null) return;

            // Ensure Canvas has GraphicRaycaster for UI clicks to work
            if (canvas.GetComponent<UnityEngine.UI.GraphicRaycaster>() == null)
            {
                canvas.gameObject.AddComponent<UnityEngine.UI.GraphicRaycaster>();
            }

            int numSlots = SlotCount;
            int rows = Mathf.CeilToInt((float)numSlots / SLOTS_PER_ROW);

            // ============================================================
            // CALCULATE PANEL DIMENSIONS (MATCHING REFERENCE #2)
            // ============================================================
            float gridWidth = SLOTS_PER_ROW * slotSize.x + (SLOTS_PER_ROW - 1) * slotSpacing;
            float gridHeight = rows * slotSize.y + (rows - 1) * slotSpacing;

            // Header zone height (title plaque area)
            float headerZoneHeight = 50f;

            // Extra vertical space below slots (like reference - lots of empty tray space)
            float emptyTraySpace = 60f;

            // Total panel size - reference has a wide horizontal feel
            float panelWidth = gridWidth + (trayPadding * 2) + 50f;  // Extra for frame
            float panelHeight = headerZoneHeight + gridHeight + emptyTraySpace + slotsToBarGap + actionBarHeight + actionBarBottomPad + 14f;

            // Apply scale factor
            panelWidth *= panelScale;
            panelHeight *= panelScale;

            // ============================================================
            // MAIN PANEL CONTAINER
            // ============================================================
            inventoryPanel = new GameObject("InventoryPanel");
            inventoryPanel.transform.SetParent(canvas.transform, false);

            RectTransform panelRect = inventoryPanel.AddComponent<RectTransform>();
            panelRect.anchorMin = new Vector2(0.5f, 0.5f);
            panelRect.anchorMax = new Vector2(0.5f, 0.5f);
            panelRect.pivot = new Vector2(0.5f, 0.5f);
            panelRect.sizeDelta = new Vector2(panelWidth, panelHeight);

            // Create layered metal frame
            CreateMetalFrame(inventoryPanel.transform, panelWidth, panelHeight);

            // Create title plaque (Header Zone)
            CreateTitlePlaque(inventoryPanel.transform, panelWidth);

            // ============================================================
            // TRAY CONTENT - Interior container (inside frame)
            // ============================================================
            float frameThickness = 14f;
            GameObject trayContent = new GameObject("TrayContent");
            trayContent.transform.SetParent(inventoryPanel.transform, false);
            RectTransform trayContentRect = trayContent.AddComponent<RectTransform>();
            trayContentRect.anchorMin = Vector2.zero;
            trayContentRect.anchorMax = Vector2.one;
            trayContentRect.pivot = new Vector2(0.5f, 0.5f);
            trayContentRect.offsetMin = new Vector2(frameThickness, frameThickness);
            trayContentRect.offsetMax = new Vector2(-frameThickness, -frameThickness);

            // ============================================================
            // ACTION BAR AREA (C) - BOTTOM zone, FIXED height
            // Anchors: Min(0,0) Max(1,0) - bottom-stretch
            // Pivot: (0.5, 0)
            // Height: FIXED 84px
            // Left/Right: 24px, Bottom: 18px
            // ============================================================
            GameObject actionBarArea = new GameObject("ActionBarArea");
            actionBarArea.transform.SetParent(trayContent.transform, false);
            RectTransform actionBarRect = actionBarArea.AddComponent<RectTransform>();
            actionBarRect.anchorMin = new Vector2(0, 0);
            actionBarRect.anchorMax = new Vector2(1, 0);
            actionBarRect.pivot = new Vector2(0.5f, 0);
            // offsetMin.x = left padding, offsetMin.y = bottom padding
            // offsetMax.x = -right padding, offsetMax.y = bottom + height
            actionBarRect.offsetMin = new Vector2(trayPadding, actionBarBottomPad);
            actionBarRect.offsetMax = new Vector2(-trayPadding, actionBarBottomPad + actionBarHeight);

            // ============================================================
            // SLOTS AREA (B) - MIDDLE zone, stretches but STOPS above action bar
            // Anchors: Min(0,0) Max(1,1) - stretch
            // Bottom offset: actionBarHeight + gap + bottomPad = 84 + 16 + 18 = 118px
            // ============================================================
            float slotsBottomOffset = actionBarHeight + slotsToBarGap + actionBarBottomPad;
            float slotsTopOffset = headerZoneHeight;  // Space for title plaque

            GameObject slotsArea = new GameObject("SlotsArea");
            slotsArea.transform.SetParent(trayContent.transform, false);
            RectTransform slotsAreaRect = slotsArea.AddComponent<RectTransform>();
            slotsAreaRect.anchorMin = new Vector2(0, 0);
            slotsAreaRect.anchorMax = new Vector2(1, 1);
            slotsAreaRect.pivot = new Vector2(0.5f, 0.5f);
            slotsAreaRect.offsetMin = new Vector2(trayPadding, slotsBottomOffset);
            slotsAreaRect.offsetMax = new Vector2(-trayPadding, -slotsTopOffset);

            // Create slot grid inside SlotsArea (upper-LEFT for modular growth)
            CreateSlotGrid(slotsArea.transform, numSlots);

            // Create action strip inside ActionBarArea
            CreateActionStrip(actionBarArea.transform);

            // Corner bolts for industrial feel
            CreateCornerBolts(inventoryPanel.transform, panelWidth, panelHeight);

            // X Close button (top-right corner)
            CreateCloseButton(inventoryPanel.transform, panelWidth, panelHeight);

            // Hide initially
            inventoryPanel.SetActive(false);
        }

        /// <summary>
        /// Creates the layered metal frame with bevel effect
        /// </summary>
        private void CreateMetalFrame(Transform parent, float width, float height)
        {
            // Layer 1: Drop shadow (offset behind)
            GameObject shadowLayer = new GameObject("FrameShadow");
            shadowLayer.transform.SetParent(parent, false);
            RectTransform shadowRect = shadowLayer.AddComponent<RectTransform>();
            shadowRect.anchorMin = Vector2.zero;
            shadowRect.anchorMax = Vector2.one;
            shadowRect.offsetMin = new Vector2(-4, -8);
            shadowRect.offsetMax = new Vector2(4, 0);
            Image shadowImg = shadowLayer.AddComponent<Image>();
            shadowImg.color = new Color(0, 0, 0, 0.6f);
            shadowImg.raycastTarget = false;

            // Layer 2: Outer dark frame
            GameObject outerFrame = new GameObject("OuterFrame");
            outerFrame.transform.SetParent(parent, false);
            RectTransform outerRect = outerFrame.AddComponent<RectTransform>();
            outerRect.anchorMin = Vector2.zero;
            outerRect.anchorMax = Vector2.one;
            outerRect.offsetMin = Vector2.zero;
            outerRect.offsetMax = Vector2.zero;
            Image outerImg = outerFrame.AddComponent<Image>();
            outerImg.color = frameOuterDark;
            outerImg.raycastTarget = false;

            // Layer 3: Metal mid-tone (bevel base)
            GameObject midFrame = new GameObject("MidFrame");
            midFrame.transform.SetParent(parent, false);
            RectTransform midRect = midFrame.AddComponent<RectTransform>();
            midRect.anchorMin = Vector2.zero;
            midRect.anchorMax = Vector2.one;
            midRect.offsetMin = new Vector2(4, 4);
            midRect.offsetMax = new Vector2(-4, -4);
            Image midImg = midFrame.AddComponent<Image>();
            midImg.color = frameMetalMid;
            midImg.raycastTarget = false;

            // Layer 4: Light bevel highlight (top-left)
            GameObject highlightEdge = new GameObject("HighlightEdge");
            highlightEdge.transform.SetParent(parent, false);
            RectTransform hlRect = highlightEdge.AddComponent<RectTransform>();
            hlRect.anchorMin = Vector2.zero;
            hlRect.anchorMax = Vector2.one;
            hlRect.offsetMin = new Vector2(6, 6);
            hlRect.offsetMax = new Vector2(-6, -6);
            Image hlImg = highlightEdge.AddComponent<Image>();
            hlImg.color = frameMetalLight;
            hlImg.raycastTarget = false;

            // Layer 5: Inner shadow frame
            GameObject innerShadow = new GameObject("InnerShadow");
            innerShadow.transform.SetParent(parent, false);
            RectTransform isRect = innerShadow.AddComponent<RectTransform>();
            isRect.anchorMin = Vector2.zero;
            isRect.anchorMax = Vector2.one;
            isRect.offsetMin = new Vector2(10, 10);
            isRect.offsetMax = new Vector2(-10, -10);
            Image isImg = innerShadow.AddComponent<Image>();
            isImg.color = frameInnerShadow;
            isImg.raycastTarget = false;

            // Layer 6: Tray floor (worn metal)
            GameObject trayFloor = new GameObject("TrayFloor");
            trayFloor.transform.SetParent(parent, false);
            RectTransform tfRect = trayFloor.AddComponent<RectTransform>();
            tfRect.anchorMin = Vector2.zero;
            tfRect.anchorMax = Vector2.one;
            tfRect.offsetMin = new Vector2(14, 14);
            tfRect.offsetMax = new Vector2(-14, -14);
            Image tfImg = trayFloor.AddComponent<Image>();
            tfImg.color = trayBase;
            tfImg.raycastTarget = false;
        }

        /// <summary>
        /// Creates the metal title plaque with "INVENTORY" text
        /// </summary>
        private void CreateTitlePlaque(Transform parent, float panelWidth)
        {
            float plaqueWidth = 180f;
            float plaqueHeight = 36f;

            // Plaque container
            GameObject plaque = new GameObject("TitlePlaque");
            plaque.transform.SetParent(parent, false);
            RectTransform plaqueRect = plaque.AddComponent<RectTransform>();
            plaqueRect.anchorMin = new Vector2(0.5f, 1);
            plaqueRect.anchorMax = new Vector2(0.5f, 1);
            plaqueRect.pivot = new Vector2(0.5f, 1);
            plaqueRect.anchoredPosition = new Vector2(0, -18);
            plaqueRect.sizeDelta = new Vector2(plaqueWidth, plaqueHeight);

            // Plaque shadow
            GameObject plaqueShadow = new GameObject("PlaqueShadow");
            plaqueShadow.transform.SetParent(plaque.transform, false);
            RectTransform psRect = plaqueShadow.AddComponent<RectTransform>();
            psRect.anchorMin = Vector2.zero;
            psRect.anchorMax = Vector2.one;
            psRect.offsetMin = new Vector2(-2, -3);
            psRect.offsetMax = new Vector2(2, 0);
            Image psImg = plaqueShadow.AddComponent<Image>();
            psImg.color = new Color(0, 0, 0, 0.5f);
            psImg.raycastTarget = false;

            // Plaque base (dark edge)
            GameObject plaqueBase = new GameObject("PlaqueBase");
            plaqueBase.transform.SetParent(plaque.transform, false);
            RectTransform pbRect = plaqueBase.AddComponent<RectTransform>();
            pbRect.anchorMin = Vector2.zero;
            pbRect.anchorMax = Vector2.one;
            pbRect.offsetMin = Vector2.zero;
            pbRect.offsetMax = Vector2.zero;
            Image pbImg = plaqueBase.AddComponent<Image>();
            pbImg.color = frameOuterDark;
            pbImg.raycastTarget = false;

            // Plaque highlight (top edge)
            GameObject plaqueHL = new GameObject("PlaqueHighlight");
            plaqueHL.transform.SetParent(plaque.transform, false);
            RectTransform phlRect = plaqueHL.AddComponent<RectTransform>();
            phlRect.anchorMin = new Vector2(0, 0.7f);
            phlRect.anchorMax = new Vector2(1, 1);
            phlRect.offsetMin = new Vector2(2, 0);
            phlRect.offsetMax = new Vector2(-2, -2);
            Image phlImg = plaqueHL.AddComponent<Image>();
            phlImg.color = plaqueHighlight;
            phlImg.raycastTarget = false;

            // Plaque metal body
            GameObject plaqueMid = new GameObject("PlaqueMetal");
            plaqueMid.transform.SetParent(plaque.transform, false);
            RectTransform pmRect = plaqueMid.AddComponent<RectTransform>();
            pmRect.anchorMin = Vector2.zero;
            pmRect.anchorMax = Vector2.one;
            pmRect.offsetMin = new Vector2(3, 3);
            pmRect.offsetMax = new Vector2(-3, -3);
            Image pmImg = plaqueMid.AddComponent<Image>();
            pmImg.color = plaqueMetal;
            pmImg.raycastTarget = false;

            // Title text
            GameObject titleObj = new GameObject("TitleText");
            titleObj.transform.SetParent(plaque.transform, false);
            RectTransform titleRect = titleObj.AddComponent<RectTransform>();
            titleRect.anchorMin = Vector2.zero;
            titleRect.anchorMax = Vector2.one;
            titleRect.offsetMin = Vector2.zero;
            titleRect.offsetMax = Vector2.zero;

            TextMeshProUGUI titleText = titleObj.AddComponent<TextMeshProUGUI>();
            titleText.text = "INVENTORY";
            titleText.fontSize = 18;
            titleText.color = titleGold;
            titleText.alignment = TextAlignmentOptions.Center;
            titleText.fontStyle = FontStyles.Bold;
            titleText.characterSpacing = 6f;
            titleText.raycastTarget = false;

            // Add plaque bolts (small)
            CreatePlaqueBolt(plaque.transform, new Vector2(-plaqueWidth/2 + 12, 0));
            CreatePlaqueBolt(plaque.transform, new Vector2(plaqueWidth/2 - 12, 0));
        }

        private void CreatePlaqueBolt(Transform parent, Vector2 position)
        {
            GameObject bolt = new GameObject("PlaqueBolt");
            bolt.transform.SetParent(parent, false);
            RectTransform boltRect = bolt.AddComponent<RectTransform>();
            boltRect.anchorMin = new Vector2(0.5f, 0.5f);
            boltRect.anchorMax = new Vector2(0.5f, 0.5f);
            boltRect.pivot = new Vector2(0.5f, 0.5f);
            boltRect.anchoredPosition = position;
            boltRect.sizeDelta = new Vector2(8, 8);

            Image boltImg = bolt.AddComponent<Image>();
            boltImg.color = frameMetalLight;
            boltImg.raycastTarget = false;

            // Bolt shadow
            Shadow boltShadow = bolt.AddComponent<Shadow>();
            boltShadow.effectColor = new Color(0, 0, 0, 0.5f);
            boltShadow.effectDistance = new Vector2(1, -1);
        }

        private void CreateSlotGrid(Transform parent, int numSlots)
        {
            GameObject container = new GameObject("SlotContainer");
            container.transform.SetParent(parent, false);

            RectTransform rect = container.AddComponent<RectTransform>();
            // Position slot grid in UPPER-LEFT of SlotsArea (modular for upgrades/growth)
            // Anchor to top-left so new slots can be added to the right and below
            rect.anchorMin = new Vector2(0f, 1f);
            rect.anchorMax = new Vector2(0f, 1f);
            rect.pivot = new Vector2(0f, 1f);
            rect.anchoredPosition = new Vector2(8f, -8f);  // Small offset from top-left corner

            int rows = Mathf.CeilToInt((float)numSlots / SLOTS_PER_ROW);
            float gridWidth = SLOTS_PER_ROW * slotSize.x + (SLOTS_PER_ROW - 1) * slotSpacing;
            float gridHeight = rows * slotSize.y + (rows - 1) * slotSpacing;
            rect.sizeDelta = new Vector2(gridWidth, gridHeight);

            slotContainer = container.transform;

            // Create slots
            slots.Clear();
            for (int i = 0; i < numSlots; i++)
            {
                CreateInsetSlot(container.transform, i);
            }
        }

        /// <summary>
        /// Creates an inset compartment slot matching the reference metal tray look.
        /// Simplified structure: dark slot with subtle border + warm gold selection glow
        /// </summary>
        private void CreateInsetSlot(Transform parent, int index)
        {
            GameObject slotObj = new GameObject($"Slot_{index}");
            slotObj.transform.SetParent(parent, false);

            RectTransform rect = slotObj.AddComponent<RectTransform>();

            // Calculate position in grid
            int row = index / SLOTS_PER_ROW;
            int col = index % SLOTS_PER_ROW;
            float x = col * (slotSize.x + slotSpacing) - (SLOTS_PER_ROW - 1) * (slotSize.x + slotSpacing) / 2f + slotSize.x / 2f;
            float y = -row * (slotSize.y + slotSpacing) + (Mathf.CeilToInt((float)SlotCount / SLOTS_PER_ROW) - 1) * (slotSize.y + slotSpacing) / 2f - slotSize.y / 2f;

            rect.anchorMin = new Vector2(0.5f, 0.5f);
            rect.anchorMax = new Vector2(0.5f, 0.5f);
            rect.pivot = new Vector2(0.5f, 0.5f);
            rect.anchoredPosition = new Vector2(x, y);
            rect.sizeDelta = slotSize;

            // Layer 1: Outer border (thin dark edge)
            Image slotBase = slotObj.AddComponent<Image>();
            slotBase.color = new Color(0.12f, 0.11f, 0.10f, 1f);

            // Layer 2: Inner highlight edge (subtle light on bottom-right for 3D feel)
            GameObject innerHighlight = new GameObject("InnerHighlight");
            innerHighlight.transform.SetParent(slotObj.transform, false);
            RectTransform ihRect = innerHighlight.AddComponent<RectTransform>();
            ihRect.anchorMin = Vector2.zero;
            ihRect.anchorMax = Vector2.one;
            ihRect.offsetMin = new Vector2(2, 2);
            ihRect.offsetMax = new Vector2(-2, -2);
            Image ihImg = innerHighlight.AddComponent<Image>();
            ihImg.color = new Color(0.25f, 0.23f, 0.21f, 1f);
            ihImg.raycastTarget = false;

            // Layer 3: Slot interior (dark compartment floor)
            GameObject slotInterior = new GameObject("SlotInterior");
            slotInterior.transform.SetParent(slotObj.transform, false);
            RectTransform siRect = slotInterior.AddComponent<RectTransform>();
            siRect.anchorMin = Vector2.zero;
            siRect.anchorMax = Vector2.one;
            siRect.offsetMin = new Vector2(3, 3);
            siRect.offsetMax = new Vector2(-3, -3);
            Image siImg = slotInterior.AddComponent<Image>();
            siImg.color = new Color(0.08f, 0.07f, 0.06f, 1f);
            siImg.raycastTarget = false;

            // Selection glow - warm gold outer glow effect
            // This sits BEHIND the slot content but shows through as a border glow
            GameObject selectGlow = new GameObject("SelectGlow");
            selectGlow.transform.SetParent(slotObj.transform, false);
            selectGlow.transform.SetAsFirstSibling(); // Put behind other elements
            RectTransform sgRect = selectGlow.AddComponent<RectTransform>();
            sgRect.anchorMin = Vector2.zero;
            sgRect.anchorMax = Vector2.one;
            sgRect.offsetMin = new Vector2(-4, -4);
            sgRect.offsetMax = new Vector2(4, 4);
            Image sgImg = selectGlow.AddComponent<Image>();
            sgImg.color = new Color(0.85f, 0.65f, 0.25f, 0.9f);
            sgImg.raycastTarget = false;
            selectGlow.SetActive(false);

            // Selection inner border (sits on top of slot, warm gold frame)
            GameObject selectBorderObj = new GameObject("SelectBorder");
            selectBorderObj.transform.SetParent(slotObj.transform, false);
            RectTransform sbRect = selectBorderObj.AddComponent<RectTransform>();
            sbRect.anchorMin = Vector2.zero;
            sbRect.anchorMax = Vector2.one;
            sbRect.offsetMin = Vector2.zero;
            sbRect.offsetMax = Vector2.zero;
            Image sbImg = selectBorderObj.AddComponent<Image>();
            sbImg.color = new Color(0.95f, 0.75f, 0.30f, 1f);
            sbImg.raycastTarget = false;
            // Make it a border using a child mask
            GameObject selectBorderInner = new GameObject("SelectBorderInner");
            selectBorderInner.transform.SetParent(selectBorderObj.transform, false);
            RectTransform sbiRect = selectBorderInner.AddComponent<RectTransform>();
            sbiRect.anchorMin = Vector2.zero;
            sbiRect.anchorMax = Vector2.one;
            sbiRect.offsetMin = new Vector2(3, 3);
            sbiRect.offsetMax = new Vector2(-3, -3);
            Image sbiImg = selectBorderInner.AddComponent<Image>();
            sbiImg.color = new Color(0.08f, 0.07f, 0.06f, 1f); // Same as slot interior
            sbiImg.raycastTarget = false;
            selectBorderObj.SetActive(false);

            // Item icon (centered with padding - icon floats above slot floor)
            GameObject iconObj = new GameObject("ItemIcon");
            iconObj.transform.SetParent(slotObj.transform, false);
            RectTransform iconRect = iconObj.AddComponent<RectTransform>();
            iconRect.anchorMin = Vector2.zero;
            iconRect.anchorMax = Vector2.one;
            iconRect.offsetMin = new Vector2(10, 10);
            iconRect.offsetMax = new Vector2(-10, -10);

            Image icon = iconObj.AddComponent<Image>();
            icon.color = Color.white;
            icon.raycastTarget = false;
            icon.preserveAspect = true;
            icon.enabled = false;

            // Quantity label (small, bottom-right corner) - for future stacking, hidden for now
            GameObject qtyObj = new GameObject("QuantityLabel");
            qtyObj.transform.SetParent(slotObj.transform, false);
            RectTransform qtyRect = qtyObj.AddComponent<RectTransform>();
            qtyRect.anchorMin = new Vector2(1, 0);
            qtyRect.anchorMax = new Vector2(1, 0);
            qtyRect.pivot = new Vector2(1, 0);
            qtyRect.anchoredPosition = new Vector2(-6, 4);
            qtyRect.sizeDelta = new Vector2(24, 18);
            TextMeshProUGUI qtyText = qtyObj.AddComponent<TextMeshProUGUI>();
            qtyText.text = "";
            qtyText.fontSize = 14;
            qtyText.color = new Color(0.9f, 0.9f, 0.85f, 1f);
            qtyText.alignment = TextAlignmentOptions.BottomRight;
            qtyText.fontStyle = FontStyles.Bold;
            qtyText.raycastTarget = false;
            qtyText.enableWordWrapping = false;
            // Add shadow for readability
            Shadow qtyShadow = qtyObj.AddComponent<Shadow>();
            qtyShadow.effectColor = new Color(0, 0, 0, 0.8f);
            qtyShadow.effectDistance = new Vector2(1, -1);

            // Add slot component
            InventorySlotUI slotUI = slotObj.AddComponent<InventorySlotUI>();
            slotUI.Initialize(index, this);
            slots.Add(slotUI);

            // Make slot clickable
            Button btn = slotObj.AddComponent<Button>();
            btn.targetGraphic = slotBase;
            btn.transition = Selectable.Transition.None;

            int capturedIndex = index;
            btn.onClick.AddListener(() => OnSlotClicked(capturedIndex));
        }

        /// <summary>
        /// Creates the bottom action strip with proper button layout.
        /// Parent is ActionBarArea which already has correct size.
        /// </summary>
        private void CreateActionStrip(Transform parent)
        {
            // ============================================================
            // TOP SEPARATOR LINE - Visually separates footer from slot area
            // ============================================================
            GameObject separator = new GameObject("TopSeparator");
            separator.transform.SetParent(parent, false);
            RectTransform sepRect = separator.AddComponent<RectTransform>();
            sepRect.anchorMin = new Vector2(0, 1);
            sepRect.anchorMax = new Vector2(1, 1);
            sepRect.pivot = new Vector2(0.5f, 1);
            sepRect.anchoredPosition = Vector2.zero;
            sepRect.sizeDelta = new Vector2(0, 2);
            Image sepImg = separator.AddComponent<Image>();
            sepImg.color = new Color(0.18f, 0.16f, 0.14f, 1f);
            sepImg.raycastTarget = false;

            // ============================================================
            // ACTION STRIP - Fills ActionBarArea with inner padding
            // ============================================================
            actionStrip = new GameObject("ActionStrip");
            actionStrip.transform.SetParent(parent, false);

            RectTransform rect = actionStrip.AddComponent<RectTransform>();
            rect.anchorMin = Vector2.zero;
            rect.anchorMax = Vector2.one;
            rect.offsetMin = new Vector2(actionBarInnerPad, 0);  // No bottom padding (buttons handle it)
            rect.offsetMax = new Vector2(-actionBarInnerPad, -4);  // Small top offset below separator

            // Strip background with metal look
            Image stripBgImg = actionStrip.AddComponent<Image>();
            stripBgImg.color = stripBg;

            // ============================================================
            // LEFT INFO AREA - Fixed width, bottom-aligned like reference
            // ============================================================
            GameObject infoGroup = new GameObject("LeftInfoArea");
            infoGroup.transform.SetParent(actionStrip.transform, false);
            actionStripItemInfo = infoGroup;

            RectTransform infoRect = infoGroup.AddComponent<RectTransform>();
            // Anchor to bottom-left, FIXED width, stretch height
            infoRect.anchorMin = new Vector2(0, 0);
            infoRect.anchorMax = new Vector2(0, 1);
            infoRect.pivot = new Vector2(0, 0);
            infoRect.anchoredPosition = new Vector2(4, buttonBottomPad);
            infoRect.sizeDelta = new Vector2(leftInfoWidth, -buttonBottomPad * 2);

            // Item icon frame (left side of info area, vertically centered)
            float iconSize = 36f;  // Smaller to fit thinner bar
            GameObject iconFrame = new GameObject("IconFrame");
            iconFrame.transform.SetParent(infoGroup.transform, false);
            RectTransform ifRect = iconFrame.AddComponent<RectTransform>();
            ifRect.anchorMin = new Vector2(0, 0.5f);
            ifRect.anchorMax = new Vector2(0, 0.5f);
            ifRect.pivot = new Vector2(0, 0.5f);
            ifRect.anchoredPosition = new Vector2(0, 0);
            ifRect.sizeDelta = new Vector2(iconSize, iconSize);
            Image ifImg = iconFrame.AddComponent<Image>();
            ifImg.color = slotInsetDark;
            ifImg.raycastTarget = false;

            GameObject iconObj = new GameObject("ItemIcon");
            iconObj.transform.SetParent(iconFrame.transform, false);
            RectTransform iconObjRect = iconObj.AddComponent<RectTransform>();
            iconObjRect.anchorMin = Vector2.zero;
            iconObjRect.anchorMax = Vector2.one;
            iconObjRect.offsetMin = new Vector2(4, 4);
            iconObjRect.offsetMax = new Vector2(-4, -4);

            actionStripItemIcon = iconObj.AddComponent<Image>();
            actionStripItemIcon.color = Color.white;
            actionStripItemIcon.raycastTarget = false;
            actionStripItemIcon.preserveAspect = true;
            actionStripItemIcon.enabled = false;

            // Item name text (top half, right of icon)
            GameObject nameObj = new GameObject("ItemName");
            nameObj.transform.SetParent(infoGroup.transform, false);
            RectTransform nameRect = nameObj.AddComponent<RectTransform>();
            nameRect.anchorMin = new Vector2(0, 0.5f);
            nameRect.anchorMax = new Vector2(1, 1);
            nameRect.offsetMin = new Vector2(iconSize + 10, 0);
            nameRect.offsetMax = new Vector2(-4, -2);

            actionStripItemName = nameObj.AddComponent<TextMeshProUGUI>();
            actionStripItemName.text = "";
            actionStripItemName.fontSize = 14;
            actionStripItemName.color = textBright;
            actionStripItemName.alignment = TextAlignmentOptions.MidlineLeft;
            actionStripItemName.fontStyle = FontStyles.Bold;
            actionStripItemName.raycastTarget = false;

            // Item value text (bottom half, right of icon)
            GameObject valueObj = new GameObject("ItemValue");
            valueObj.transform.SetParent(infoGroup.transform, false);
            RectTransform valueRect = valueObj.AddComponent<RectTransform>();
            valueRect.anchorMin = new Vector2(0, 0);
            valueRect.anchorMax = new Vector2(1, 0.5f);
            valueRect.offsetMin = new Vector2(iconSize + 10, 2);
            valueRect.offsetMax = new Vector2(-4, 0);

            actionStripItemValue = valueObj.AddComponent<TextMeshProUGUI>();
            actionStripItemValue.text = "";
            actionStripItemValue.fontSize = 12;
            actionStripItemValue.color = valueGold;
            actionStripItemValue.alignment = TextAlignmentOptions.MidlineLeft;
            actionStripItemValue.raycastTarget = false;

            // ============================================================
            // RIGHT BUTTON GROUP - Anchored to BOTTOM-RIGHT (like reference #2)
            // Order: Drop | Destroy | Sort (right to left from edge)
            // ============================================================
            // Sort button (rightmost, smaller/less prominent)
            float sortX = -buttonRightPad;
            sortButton = CreateMetalButtonBottomAligned(actionStrip.transform, "Sort", buttonNormal, buttonHover,
                new Vector2(sortX, buttonBottomPad), new Vector2(sortButtonWidth, buttonHeight), 12);
            sortButton.onClick.AddListener(OnSortClicked);

            // Destroy button (middle, danger style)
            float destroyX = sortX - sortButtonWidth - buttonSpacingH;
            destroyButton = CreateMetalButtonBottomAligned(actionStrip.transform, "Destroy", buttonDestroy, buttonDestroyHover,
                new Vector2(destroyX, buttonBottomPad), new Vector2(destroyButtonWidth, buttonHeight), 13);
            destroyButton.onClick.AddListener(OnDestroyClicked);

            // Drop button (leftmost of group)
            float dropX = destroyX - destroyButtonWidth - buttonSpacingH;
            dropButton = CreateMetalButtonBottomAligned(actionStrip.transform, "Drop", buttonNormal, buttonHover,
                new Vector2(dropX, buttonBottomPad), new Vector2(dropButtonWidth, buttonHeight), 13);
            dropButton.onClick.AddListener(OnDropClicked);

            // Initial state: strip visible, item info hidden, Drop/Destroy disabled
            actionStripItemInfo.SetActive(false);
            dropButton.interactable = false;
            destroyButton.interactable = false;
            // Sort always enabled
        }

        /// <summary>
        /// Creates a metal-styled button with bevel effect
        /// </summary>
        private Button CreateMetalButton(Transform parent, string text, Color normalColor, Color hoverColor,
            Vector2 position, Vector2 size, int fontSize)
        {
            GameObject btnObj = new GameObject($"{text}Button");
            btnObj.transform.SetParent(parent, false);

            RectTransform rect = btnObj.AddComponent<RectTransform>();
            rect.anchorMin = new Vector2(1, 0.5f);
            rect.anchorMax = new Vector2(1, 0.5f);
            rect.pivot = new Vector2(1, 0.5f);
            rect.anchoredPosition = position;
            rect.sizeDelta = size;

            // Button base (dark edge)
            Image baseImg = btnObj.AddComponent<Image>();
            baseImg.color = new Color(normalColor.r * 0.6f, normalColor.g * 0.6f, normalColor.b * 0.6f, 1f);

            // Button face (lighter)
            GameObject face = new GameObject("Face");
            face.transform.SetParent(btnObj.transform, false);
            RectTransform faceRect = face.AddComponent<RectTransform>();
            faceRect.anchorMin = Vector2.zero;
            faceRect.anchorMax = Vector2.one;
            faceRect.offsetMin = new Vector2(2, 2);
            faceRect.offsetMax = new Vector2(-2, -2);
            Image faceImg = face.AddComponent<Image>();
            faceImg.color = normalColor;
            faceImg.raycastTarget = false;

            Button btn = btnObj.AddComponent<Button>();
            btn.targetGraphic = baseImg;

            ColorBlock colors = btn.colors;
            colors.normalColor = new Color(normalColor.r * 0.6f, normalColor.g * 0.6f, normalColor.b * 0.6f, 1f);
            colors.highlightedColor = new Color(hoverColor.r * 0.7f, hoverColor.g * 0.7f, hoverColor.b * 0.7f, 1f);
            colors.pressedColor = new Color(normalColor.r * 0.4f, normalColor.g * 0.4f, normalColor.b * 0.4f, 1f);
            colors.selectedColor = colors.normalColor;
            btn.colors = colors;

            // Text
            GameObject textObj = new GameObject("Text");
            textObj.transform.SetParent(btnObj.transform, false);

            RectTransform textRect = textObj.AddComponent<RectTransform>();
            textRect.anchorMin = Vector2.zero;
            textRect.anchorMax = Vector2.one;
            textRect.offsetMin = Vector2.zero;
            textRect.offsetMax = Vector2.zero;

            TextMeshProUGUI tmp = textObj.AddComponent<TextMeshProUGUI>();
            tmp.text = text;
            tmp.fontSize = fontSize;
            tmp.color = textBright;
            tmp.alignment = TextAlignmentOptions.Center;
            tmp.raycastTarget = false;

            return btn;
        }

        /// <summary>
        /// Creates a metal-styled button anchored to BOTTOM-RIGHT (for footer buttons)
        /// </summary>
        private Button CreateMetalButtonBottomAligned(Transform parent, string text, Color normalColor, Color hoverColor,
            Vector2 position, Vector2 size, int fontSize)
        {
            GameObject btnObj = new GameObject($"{text}Button");
            btnObj.transform.SetParent(parent, false);

            RectTransform rect = btnObj.AddComponent<RectTransform>();
            // Anchor to BOTTOM-RIGHT (not center-right)
            rect.anchorMin = new Vector2(1, 0);
            rect.anchorMax = new Vector2(1, 0);
            rect.pivot = new Vector2(1, 0);
            rect.anchoredPosition = position;
            rect.sizeDelta = size;

            // Button base (dark edge)
            Image baseImg = btnObj.AddComponent<Image>();
            baseImg.color = new Color(normalColor.r * 0.6f, normalColor.g * 0.6f, normalColor.b * 0.6f, 1f);

            // Button face (lighter)
            GameObject face = new GameObject("Face");
            face.transform.SetParent(btnObj.transform, false);
            RectTransform faceRect = face.AddComponent<RectTransform>();
            faceRect.anchorMin = Vector2.zero;
            faceRect.anchorMax = Vector2.one;
            faceRect.offsetMin = new Vector2(2, 2);
            faceRect.offsetMax = new Vector2(-2, -2);
            Image faceImg = face.AddComponent<Image>();
            faceImg.color = normalColor;
            faceImg.raycastTarget = false;

            Button btn = btnObj.AddComponent<Button>();
            btn.targetGraphic = baseImg;

            ColorBlock colors = btn.colors;
            colors.normalColor = new Color(normalColor.r * 0.6f, normalColor.g * 0.6f, normalColor.b * 0.6f, 1f);
            colors.highlightedColor = new Color(hoverColor.r * 0.7f, hoverColor.g * 0.7f, hoverColor.b * 0.7f, 1f);
            colors.pressedColor = new Color(normalColor.r * 0.4f, normalColor.g * 0.4f, normalColor.b * 0.4f, 1f);
            colors.selectedColor = colors.normalColor;
            btn.colors = colors;

            // Text
            GameObject textObj = new GameObject("Text");
            textObj.transform.SetParent(btnObj.transform, false);

            RectTransform textRect = textObj.AddComponent<RectTransform>();
            textRect.anchorMin = Vector2.zero;
            textRect.anchorMax = Vector2.one;
            textRect.offsetMin = Vector2.zero;
            textRect.offsetMax = Vector2.zero;

            TextMeshProUGUI tmp = textObj.AddComponent<TextMeshProUGUI>();
            tmp.text = text;
            tmp.fontSize = fontSize;
            tmp.color = textBright;
            tmp.alignment = TextAlignmentOptions.Center;
            tmp.raycastTarget = false;

            return btn;
        }

        /// <summary>
        /// Creates industrial corner bolts for the metal tray frame
        /// </summary>
        private void CreateCornerBolts(Transform parent, float width, float height)
        {
            float boltSize = 10f;
            float offset = 16f;

            Vector2[] positions = new Vector2[]
            {
                new Vector2(-width/2 + offset, height/2 - offset),   // Top-left
                new Vector2(width/2 - offset, height/2 - offset),    // Top-right
                new Vector2(-width/2 + offset, -height/2 + offset),  // Bottom-left
                new Vector2(width/2 - offset, -height/2 + offset)    // Bottom-right
            };

            foreach (var pos in positions)
            {
                // Bolt base (dark)
                GameObject bolt = new GameObject("Bolt");
                bolt.transform.SetParent(parent, false);

                RectTransform rect = bolt.AddComponent<RectTransform>();
                rect.anchorMin = new Vector2(0.5f, 0.5f);
                rect.anchorMax = new Vector2(0.5f, 0.5f);
                rect.pivot = new Vector2(0.5f, 0.5f);
                rect.anchoredPosition = pos;
                rect.sizeDelta = new Vector2(boltSize, boltSize);

                Image baseImg = bolt.AddComponent<Image>();
                baseImg.color = frameOuterDark;
                baseImg.raycastTarget = false;

                // Bolt highlight (lighter center)
                GameObject boltHL = new GameObject("BoltHighlight");
                boltHL.transform.SetParent(bolt.transform, false);
                RectTransform hlRect = boltHL.AddComponent<RectTransform>();
                hlRect.anchorMin = Vector2.zero;
                hlRect.anchorMax = Vector2.one;
                hlRect.offsetMin = new Vector2(2, 2);
                hlRect.offsetMax = new Vector2(-2, -2);
                Image hlImg = boltHL.AddComponent<Image>();
                hlImg.color = frameMetalLight;
                hlImg.raycastTarget = false;

                // Add shadow effect
                Shadow shadow = bolt.AddComponent<Shadow>();
                shadow.effectColor = new Color(0, 0, 0, 0.5f);
                shadow.effectDistance = new Vector2(1, -1);
            }
        }

        /// <summary>
        /// Creates the X close button in the top-right corner using same pattern as working action buttons
        /// </summary>
        private void CreateCloseButton(Transform parent, float panelWidth, float panelHeight)
        {
            float buttonSize = 32f;
            float offsetFromEdge = 22f;

            GameObject btnObj = new GameObject("CloseButton");
            btnObj.transform.SetParent(parent, false);

            RectTransform rect = btnObj.AddComponent<RectTransform>();
            rect.anchorMin = new Vector2(1, 1);
            rect.anchorMax = new Vector2(1, 1);
            rect.pivot = new Vector2(1, 1);
            rect.anchoredPosition = new Vector2(-offsetFromEdge, -offsetFromEdge);
            rect.sizeDelta = new Vector2(buttonSize, buttonSize);

            // Button base - this is what receives clicks (same as working buttons)
            Image baseImg = btnObj.AddComponent<Image>();
            baseImg.color = new Color(0.3f, 0.12f, 0.10f, 1f); // Dark red edge

            // Button face (lighter inner)
            GameObject face = new GameObject("Face");
            face.transform.SetParent(btnObj.transform, false);
            RectTransform faceRect = face.AddComponent<RectTransform>();
            faceRect.anchorMin = Vector2.zero;
            faceRect.anchorMax = Vector2.one;
            faceRect.offsetMin = new Vector2(2, 2);
            faceRect.offsetMax = new Vector2(-2, -2);
            Image faceImg = face.AddComponent<Image>();
            faceImg.color = new Color(0.5f, 0.2f, 0.18f, 1f); // Lighter red
            faceImg.raycastTarget = false;

            // Button component
            Button btn = btnObj.AddComponent<Button>();
            btn.targetGraphic = baseImg;

            ColorBlock colors = btn.colors;
            colors.normalColor = new Color(0.3f, 0.12f, 0.10f, 1f);
            colors.highlightedColor = new Color(0.5f, 0.2f, 0.18f, 1f);
            colors.pressedColor = new Color(0.2f, 0.08f, 0.06f, 1f);
            colors.selectedColor = colors.normalColor;
            btn.colors = colors;

            // X Text
            GameObject textObj = new GameObject("Text");
            textObj.transform.SetParent(btnObj.transform, false);
            RectTransform textRect = textObj.AddComponent<RectTransform>();
            textRect.anchorMin = Vector2.zero;
            textRect.anchorMax = Vector2.one;
            textRect.offsetMin = Vector2.zero;
            textRect.offsetMax = Vector2.zero;

            TextMeshProUGUI tmp = textObj.AddComponent<TextMeshProUGUI>();
            tmp.text = "X";
            tmp.fontSize = 18;
            tmp.fontStyle = FontStyles.Bold;
            tmp.color = new Color(0.95f, 0.9f, 0.85f, 1f);
            tmp.alignment = TextAlignmentOptions.Center;
            tmp.raycastTarget = false;

            // Click handler using both Button.onClick AND EventTrigger for maximum compatibility
            btn.onClick.AddListener(() => {
                CloseInventory();
            });

            // Also add EventTrigger as backup
            UnityEngine.EventSystems.EventTrigger trigger = btnObj.AddComponent<UnityEngine.EventSystems.EventTrigger>();
            var pointerClick = new UnityEngine.EventSystems.EventTrigger.Entry();
            pointerClick.eventID = UnityEngine.EventSystems.EventTriggerType.PointerClick;
            pointerClick.callback.AddListener((data) => {
                CloseInventory();
            });
            trigger.triggers.Add(pointerClick);

            // Make sure it's on top
            btnObj.transform.SetAsLastSibling();
        }

        private Canvas FindMainCanvas()
        {
            // Always use our own dedicated canvas to avoid conflicts with other UI systems
            var existingCanvas = GameObject.Find("InventoryCanvas");
            if (existingCanvas != null)
            {
                Canvas canvas = existingCanvas.GetComponent<Canvas>();
                if (canvas != null) return canvas;
            }

            // Create our own canvas
            GameObject canvasObj = new GameObject("InventoryCanvas");
            Canvas newCanvas = canvasObj.AddComponent<Canvas>();
            newCanvas.renderMode = RenderMode.ScreenSpaceOverlay;
            newCanvas.sortingOrder = 100;

            var scaler = canvasObj.AddComponent<UnityEngine.UI.CanvasScaler>();
            scaler.uiScaleMode = UnityEngine.UI.CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920, 1080);

            canvasObj.AddComponent<UnityEngine.UI.GraphicRaycaster>();
            return newCanvas;
        }

        #endregion

        #region Inventory Operations

        public void OpenInventory()
        {
            if (debugMode) LogInventoryDebugInfo();

            // Don't open while other UIs are active
            if (UIState.IsPauseMenuOpen || UIState.IsMachineUIOpen) return;

            // Recreate UI if panel was destroyed (e.g., after capacity upgrade)
            // Unity's destroyed objects return true for == null check
            if (inventoryPanel == null)
            {
                CreateInventoryUI();

                if (inventoryPanel == null) return;
            }

            EnsureInventorySubscription();

            inventoryPanel.SetActive(true);
            UIState.IsInventoryUIOpen = true;  // This automatically handles cursor + blocks FirstPersonController

            // Hide crosshair when inventory is open
            if (UI.HUDController.Instance != null)
                UI.HUDController.Instance.SetCrosshairVisible(false);

            ClearSelection();
            RefreshAllSlots();
        }

        /// <summary>
        /// Comprehensive runtime debug logging to prove inventory state.
        /// </summary>
        private void LogInventoryDebugInfo()
        {
            Debug.Log("╔════════════════════════════════════════════════════════════╗");
            Debug.Log("║          INVENTORY DEBUG - RUNTIME INVESTIGATION           ║");
            Debug.Log("╠════════════════════════════════════════════════════════════╣");

            // 1. Log this InventoryUIManager's info
            Debug.Log($"║ THIS InventoryUIManager:");
            Debug.Log($"║   - GameObject: {gameObject.name}");
            Debug.Log($"║   - Full Path: {GetFullPath(transform)}");
            Debug.Log($"║   - Instance == this: {Instance == this}");

            // 2. Find ALL inventory UI objects in scene
            Debug.Log("║");
            Debug.Log("║ ALL InventoryUIManager instances in scene:");
            var allManagers = FindObjectsOfType<InventoryUIManager>(true);
            Debug.Log($"║   Count: {allManagers.Length}");
            foreach (var mgr in allManagers)
            {
                Debug.Log($"║   - {mgr.gameObject.name} (active: {mgr.gameObject.activeInHierarchy}, enabled: {mgr.enabled})");
                Debug.Log($"║     Path: {GetFullPath(mgr.transform)}");
            }

            // 3. Find old InventoryUI instances
            Debug.Log("║");
            Debug.Log("║ OLD InventoryUI (deprecated) instances:");
#pragma warning disable CS0618 // Type or member is obsolete
            var oldUIs = FindObjectsOfType<BeneathTheFloor.UI.InventoryUI>(true);
#pragma warning restore CS0618
            Debug.Log($"║   Count: {oldUIs.Length}");
            foreach (var ui in oldUIs)
            {
                Debug.Log($"║   - {ui.gameObject.name} (active: {ui.gameObject.activeInHierarchy}, enabled: {ui.enabled})");
                Debug.Log($"║     Path: {GetFullPath(ui.transform)}");
            }

            // 4. Log capacity and slot info
            Debug.Log("║");
            Debug.Log("║ CAPACITY & SLOTS:");
            int capacity = InventorySystem.Instance != null ? InventorySystem.Instance.MaxSlots : -1;
            Debug.Log($"║   - InventorySystem.MaxSlots: {capacity}");
            Debug.Log($"║   - INITIAL_SLOT_COUNT constant: {INITIAL_SLOT_COUNT}");
            Debug.Log($"║   - SlotCount property: {SlotCount}");
            Debug.Log($"║   - slots.Count (UI elements): {slots.Count}");

            // 5. Assert expected values
            if (capacity != 5)
            {
                Debug.LogError($"║ ❌ ERROR: Inventory capacity expected 5, got {capacity}!");
            }
            if (slots.Count != 5)
            {
                Debug.LogError($"║ ❌ ERROR: Slot UI count expected 5, got {slots.Count}!");
            }

            // 6. Log slot container children
            if (slotContainer != null)
            {
                Debug.Log("║");
                Debug.Log($"║ SLOT CONTAINER: {slotContainer.name}");
                Debug.Log($"║   - Child count: {slotContainer.childCount}");
                for (int i = 0; i < slotContainer.childCount; i++)
                {
                    var child = slotContainer.GetChild(i);
                    Debug.Log($"║   [{i}] {child.name} (active: {child.gameObject.activeSelf})");
                }
            }
            else
            {
                Debug.LogWarning("║ slotContainer is NULL!");
            }

            // 7. Check for any "Inventory" named objects in scene
            Debug.Log("║");
            Debug.Log("║ OBJECTS WITH 'Inventory' IN NAME:");
            var allObjects = FindObjectsOfType<GameObject>(true);
            int invCount = 0;
            foreach (var obj in allObjects)
            {
                if (obj.name.ToLower().Contains("inventory"))
                {
                    invCount++;
                    if (invCount <= 10) // Limit output
                    {
                        Debug.Log($"║   - {obj.name} (active: {obj.activeInHierarchy})");
                        Debug.Log($"║     Path: {GetFullPath(obj.transform)}");
                    }
                }
            }
            if (invCount > 10)
            {
                Debug.Log($"║   ... and {invCount - 10} more");
            }

            // 8. Check for UIBuilder instances
            Debug.Log("║");
            Debug.Log("║ UIBuilder instances:");
            var builders = FindObjectsOfType<BeneathTheFloor.UI.UIBuilder>(true);
            Debug.Log($"║   Count: {builders.Length}");
            foreach (var b in builders)
            {
                Debug.Log($"║   - {b.gameObject.name} (active: {b.gameObject.activeInHierarchy}, enabled: {b.enabled})");
            }

            Debug.Log("╚════════════════════════════════════════════════════════════╝");
        }

        private string GetFullPath(Transform t)
        {
            string path = t.name;
            while (t.parent != null)
            {
                t = t.parent;
                path = t.name + "/" + path;
            }
            return path;
        }

        public void CloseInventory()
        {
            if (inventoryPanel == null) return;

            inventoryPanel.SetActive(false);
            UIState.IsInventoryUIOpen = false;  // This automatically handles cursor + unblocks FirstPersonController

            // Show crosshair when inventory is closed
            if (UI.HUDController.Instance != null)
                UI.HUDController.Instance.SetCrosshairVisible(true);

            ClearSelection();

            // Force cursor lock - UIState should do this but let's be explicit
            Cursor.lockState = CursorLockMode.Locked;
            Cursor.visible = false;
        }

        public void ToggleInventory()
        {
            if (IsOpen)
                CloseInventory();
            else
                OpenInventory();
        }

        public void RefreshAllSlots()
        {
            if (InventorySystem.Instance == null)
            {
                if (debugMode) Debug.LogWarning("[InventoryUIManager] RefreshAllSlots: InventorySystem.Instance is NULL!");
                return;
            }

            for (int i = 0; i < slots.Count; i++)
            {
                ItemStack stack = InventorySystem.Instance.GetStackAt(i);
                slots[i].UpdateDisplay(stack);
            }

            // Update action strip if item is selected
            if (selectedSlotIndex >= 0)
            {
                UpdateActionStrip();
            }
        }

        public void RefreshSlot(int index)
        {
            if (index < 0 || index >= slots.Count) return;
            if (InventorySystem.Instance == null) return;

            ItemStack stack = InventorySystem.Instance.GetStackAt(index);
            slots[index].UpdateDisplay(stack);
        }

        private void EnsureInventorySubscription()
        {
            if (InventorySystem.Instance != null)
            {
                InventorySystem.Instance.OnInventoryChanged -= RefreshAllSlots;
                InventorySystem.Instance.OnInventoryChanged += RefreshAllSlots;
                InventorySystem.Instance.OnInventoryCapacityChanged -= HandleCapacityChanged;
                InventorySystem.Instance.OnInventoryCapacityChanged += HandleCapacityChanged;
            }
        }

        #endregion

        #region Selection

        private void OnSlotClicked(int index)
        {
            if (InventorySystem.Instance == null) return;

            ItemStack stack = InventorySystem.Instance.GetStackAt(index);

            // If clicking empty slot, clear selection
            if (stack.IsEmpty)
            {
                ClearSelection();
                return;
            }

            // If clicking already selected slot, deselect
            if (selectedSlotIndex == index)
            {
                ClearSelection();
                return;
            }

            // Select new slot
            SelectSlot(index);
        }

        public void SelectSlot(int index)
        {
            // Deselect previous
            if (selectedSlotIndex >= 0 && selectedSlotIndex < slots.Count)
            {
                slots[selectedSlotIndex].SetSelected(false);
            }

            selectedSlotIndex = index;

            if (index >= 0 && index < slots.Count)
            {
                slots[index].SetSelected(true);
                ShowActionStrip();
                UpdateActionStrip();
            }
        }

        public void ClearSelection()
        {
            if (selectedSlotIndex >= 0 && selectedSlotIndex < slots.Count)
            {
                slots[selectedSlotIndex].SetSelected(false);
            }
            selectedSlotIndex = -1;
            HideActionStrip();
        }

        #endregion

        #region Action Strip

        private void ShowActionStrip()
        {
            // Show item info and enable buttons when item is selected
            if (actionStripItemInfo != null)
            {
                actionStripItemInfo.SetActive(true);
            }
            if (dropButton != null) dropButton.interactable = true;
            if (destroyButton != null) destroyButton.interactable = true;
        }

        private void HideActionStrip()
        {
            // Hide item info and disable buttons when no selection
            if (actionStripItemInfo != null)
            {
                actionStripItemInfo.SetActive(false);
            }
            if (dropButton != null) dropButton.interactable = false;
            if (destroyButton != null) destroyButton.interactable = false;
        }

        private void UpdateActionStrip()
        {
            if (selectedSlotIndex < 0 || InventorySystem.Instance == null)
            {
                return;
            }

            ItemStack stack = InventorySystem.Instance.GetStackAt(selectedSlotIndex);

            if (stack.IsEmpty)
            {
                HideActionStrip();
                return;
            }

            // Update icon
            if (actionStripItemIcon != null)
            {
                actionStripItemIcon.sprite = stack.item.icon;
                actionStripItemIcon.enabled = stack.item.icon != null;
                // If no icon, show a colored placeholder
                if (stack.item.icon == null)
                {
                    actionStripItemIcon.enabled = true;
                    actionStripItemIcon.sprite = null;
                    actionStripItemIcon.color = new Color(0.5f, 0.4f, 0.3f, 1f);  // Brown placeholder
                }
                else
                {
                    actionStripItemIcon.color = Color.white;
                }
            }

            // Update name
            if (actionStripItemName != null)
            {
                actionStripItemName.text = stack.item.itemName;
            }

            // Update value
            if (actionStripItemValue != null)
            {
                int sellPrice = stack.item.sellPrice;
                actionStripItemValue.text = $"Value: {sellPrice} Credits";
            }
        }

        private void OnDropClicked()
        {
            if (selectedSlotIndex < 0 || InventorySystem.Instance == null) return;

            ItemStack stack = InventorySystem.Instance.GetStackAt(selectedSlotIndex);
            if (stack.IsEmpty) return;

            // Remove from inventory (always 1 since no stacking)
            int removed = InventorySystem.Instance.RemoveFromSlot(selectedSlotIndex, 1);

            if (removed > 0 && WorldDropManager.Instance != null)
            {
                // Drop the item into the world
                WorldDropManager.Instance.DropItem(stack.item, 1);
            }

            RefreshAllSlots();

            // Check if slot is now empty
            ItemStack newStack = InventorySystem.Instance.GetStackAt(selectedSlotIndex);

            if (newStack.IsEmpty)
            {
                ClearSelection();
            }
        }

        private void OnDestroyClicked()
        {
            if (selectedSlotIndex < 0 || InventorySystem.Instance == null) return;

            ItemStack stack = InventorySystem.Instance.GetStackAt(selectedSlotIndex);
            if (stack.IsEmpty) return;

            // Remove from inventory permanently (always 1 since no stacking)
            InventorySystem.Instance.RemoveFromSlot(selectedSlotIndex, 1);

            // Item is destroyed - no world drop

            RefreshAllSlots();

            // Check if slot is now empty
            ItemStack newStack = InventorySystem.Instance.GetStackAt(selectedSlotIndex);

            if (newStack.IsEmpty)
            {
                ClearSelection();
            }
        }

        private void OnSortClicked()
        {
            if (InventorySystem.Instance != null)
            {
                InventorySystem.Instance.SortItems(InventorySortMode.ByType);
                ClearSelection();
                RefreshAllSlots();
            }
        }

        #endregion

        #region Event Handlers

        private void HandleCapacityChanged(int oldCapacity, int newCapacity)
        {
            // Clear references (CreateInventoryUI will clean up any existing panels)
            inventoryPanel = null;
            slots.Clear();
            slotContainer = null;

            // Rebuild UI immediately - CreateInventoryUI handles cleanup of old panels
            CreateInventoryUI();

            if (PickupNotificationSystem.Instance != null)
            {
                int delta = newCapacity - oldCapacity;
                if (delta > 0)
                {
                    PickupNotificationSystem.Instance.ShowNotification($"Inventory expanded! +{delta} slots");
                }
            }
        }

        private void HandleInventoryFull()
        {
            if (PickupNotificationSystem.Instance != null)
            {
                PickupNotificationSystem.Instance.ShowNotification("Inventory is full!");
            }
        }

        #endregion

        #region Public API

        public InventorySlotUI GetSlotAt(int index)
        {
            if (index < 0 || index >= slots.Count) return null;
            return slots[index];
        }

        /// <summary>
        /// Get the selected slot border color for slot UI to use.
        /// </summary>
        public Color GetSelectedBorderColor() => selectBorder;

        /// <summary>
        /// Get the normal slot border color for slot UI to use.
        /// </summary>
        public Color GetNormalBorderColor() => slotBevelDark;

        /// <summary>
        /// Get the selected slot background color.
        /// </summary>
        public Color GetSelectedBgColor() => selectGlowOuter;

        /// <summary>
        /// Get the normal slot background color.
        /// </summary>
        public Color GetNormalBgColor() => slotInsetMid;

        #endregion
    }
}
