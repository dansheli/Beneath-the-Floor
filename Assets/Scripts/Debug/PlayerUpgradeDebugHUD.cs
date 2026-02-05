using UnityEngine;
using UnityEngine.UI;
using TMPro;
using System.Linq;
using BeneathTheFloor.Inventory;
using BeneathTheFloor.Economy;
using BeneathTheFloor.Machines;
using BeneathTheFloor.Digging;
using BeneathTheFloor.Player;
using BeneathTheFloor.Crafting;

namespace BeneathTheFloor.DebugTools
{
    /// <summary>
    /// Self-initializing Debug HUD overlay that shows live upgrade-related stats.
    /// Creates its own UI at runtime - just add this component to any GameObject.
    /// Always visible for debugging purposes.
    /// </summary>
    public class PlayerUpgradeDebugHUD : MonoBehaviour
    {
        // Set to true to enable debug upgrade HUD (F4)
        private const bool ENABLE_DEBUG = false;

        [Header("Auto-Created UI References")]
        [SerializeField] private TextMeshProUGUI titleText;
        [SerializeField] private TextMeshProUGUI toolText;
        [SerializeField] private TextMeshProUGUI digSpeedText;
        [SerializeField] private TextMeshProUGUI backpackText;
        [SerializeField] private TextMeshProUGUI lightText;
        [SerializeField] private TextMeshProUGUI creditsText;

        [Header("Settings")]
        [SerializeField] private bool alwaysVisible = false;
        [SerializeField] private KeyCode toggleKey = KeyCode.F4;

        // Auto-created UI
        private Canvas debugCanvas;
        private GameObject panelObject;

        // Cached references
        private DiggingSystem diggingSystem;
        private InventorySystem inventorySystem;
        private UpgradeStation upgradeStation;
        private Light playerLight;
        private FirstPersonController playerController;

        public static PlayerUpgradeDebugHUD Instance { get; private set; }

        private void Awake()
        {
            if (Instance == null)
            {
                Instance = this;
                DontDestroyOnLoad(gameObject);
            }
            else
            {
                Destroy(gameObject);
                return;
            }
        }

        private void Start()
        {
#pragma warning disable CS0162 // Unreachable code detected
            if (!ENABLE_DEBUG) return;

            CreateDebugUI();
            CacheReferences();
            Debug.Log("[PlayerUpgradeDebugHUD] Debug HUD initialized and VISIBLE. Press F4 to toggle.");
#pragma warning restore CS0162
        }

        private void CreateDebugUI()
        {
            // Create dedicated Canvas for debug HUD
            GameObject canvasObj = new GameObject("DebugHUDCanvas");
            canvasObj.transform.SetParent(transform);
            debugCanvas = canvasObj.AddComponent<Canvas>();
            debugCanvas.renderMode = RenderMode.ScreenSpaceOverlay;
            debugCanvas.sortingOrder = 9999; // Always on top

            CanvasScaler scaler = canvasObj.AddComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920, 1080);
            scaler.matchWidthOrHeight = 0.5f;

            canvasObj.AddComponent<GraphicRaycaster>();

            // Create panel
            panelObject = new GameObject("DebugPanel");
            panelObject.transform.SetParent(canvasObj.transform, false);

            RectTransform panelRect = panelObject.AddComponent<RectTransform>();
            panelRect.anchorMin = new Vector2(0, 1); // Top-left
            panelRect.anchorMax = new Vector2(0, 1);
            panelRect.pivot = new Vector2(0, 1);
            panelRect.anchoredPosition = new Vector2(20, -20);
            panelRect.sizeDelta = new Vector2(320, 200);

            // Bright visible background
            Image bg = panelObject.AddComponent<Image>();
            bg.color = new Color(0.1f, 0.1f, 0.15f, 0.95f);

            // Add outline for visibility
            Outline outline = panelObject.AddComponent<Outline>();
            outline.effectColor = new Color(0f, 1f, 0.5f, 1f); // Bright green border
            outline.effectDistance = new Vector2(2, -2);

            // Vertical layout
            VerticalLayoutGroup vlg = panelObject.AddComponent<VerticalLayoutGroup>();
            vlg.padding = new RectOffset(12, 12, 10, 10);
            vlg.spacing = 6;
            vlg.childControlWidth = true;
            vlg.childControlHeight = false;
            vlg.childForceExpandWidth = true;
            vlg.childForceExpandHeight = false;

            ContentSizeFitter csf = panelObject.AddComponent<ContentSizeFitter>();
            csf.verticalFit = ContentSizeFitter.FitMode.PreferredSize;

            // Create text elements
            titleText = CreateText(panelObject.transform, "UPGRADE DEBUG HUD", 18, FontStyles.Bold, Color.green);
            CreateSeparator(panelObject.transform);
            toolText = CreateText(panelObject.transform, "Tool: ...", 14, FontStyles.Normal, new Color(1f, 0.8f, 0.5f));
            digSpeedText = CreateText(panelObject.transform, "Dig Speed: ...", 14, FontStyles.Normal, Color.white);
            backpackText = CreateText(panelObject.transform, "Backpack: ...", 14, FontStyles.Normal, Color.white);
            lightText = CreateText(panelObject.transform, "Light: ...", 14, FontStyles.Normal, Color.white);
            creditsText = CreateText(panelObject.transform, "Credits: ...", 14, FontStyles.Normal, Color.yellow);
            CreateSeparator(panelObject.transform);
            CreateText(panelObject.transform, "[F4 to toggle]", 11, FontStyles.Italic, new Color(0.6f, 0.6f, 0.6f));

            panelObject.SetActive(alwaysVisible);
        }

        private TextMeshProUGUI CreateText(Transform parent, string content, float fontSize, FontStyles style, Color color)
        {
            GameObject textObj = new GameObject("Text");
            textObj.transform.SetParent(parent, false);

            RectTransform rect = textObj.AddComponent<RectTransform>();
            rect.sizeDelta = new Vector2(0, fontSize + 8);

            TextMeshProUGUI tmp = textObj.AddComponent<TextMeshProUGUI>();
            tmp.text = content;
            tmp.fontSize = fontSize;
            tmp.fontStyle = style;
            tmp.color = color;
            tmp.alignment = TextAlignmentOptions.Left;
            tmp.enableWordWrapping = true;

            LayoutElement le = textObj.AddComponent<LayoutElement>();
            le.minHeight = fontSize + 6;
            le.preferredHeight = fontSize + 10;

            return tmp;
        }

        private void CreateSeparator(Transform parent)
        {
            GameObject sep = new GameObject("Separator");
            sep.transform.SetParent(parent, false);

            RectTransform rect = sep.AddComponent<RectTransform>();
            rect.sizeDelta = new Vector2(0, 2);

            Image img = sep.AddComponent<Image>();
            img.color = new Color(0f, 1f, 0.5f, 0.5f); // Green separator

            LayoutElement le = sep.AddComponent<LayoutElement>();
            le.minHeight = 2;
            le.preferredHeight = 2;
        }

        private void CacheReferences()
        {
            diggingSystem = DiggingSystem.Instance;
            if (diggingSystem == null)
                diggingSystem = FindObjectOfType<DiggingSystem>();

            inventorySystem = InventorySystem.Instance;
            if (inventorySystem == null)
                inventorySystem = FindObjectOfType<InventorySystem>();

            upgradeStation = UpgradeStation.Instance;
            if (upgradeStation == null)
                upgradeStation = FindObjectOfType<UpgradeStation>();

            playerController = FirstPersonController.Instance;
            if (playerController == null)
                playerController = FindObjectOfType<FirstPersonController>();

            // Try PlayerHeadlamp first (preferred), then fallback to search
            var headlamp = Player.PlayerHeadlamp.Instance;
            if (headlamp != null && headlamp.HeadlampLight != null)
            {
                playerLight = headlamp.HeadlampLight;
            }
            else if (playerController != null)
            {
                playerLight = playerController.GetComponentInChildren<Light>(includeInactive: true);
            }
        }

        private void Update()
        {
#pragma warning disable CS0162 // Unreachable code detected
            if (!ENABLE_DEBUG) return;

            // Handle toggle
            if (Input.GetKeyDown(toggleKey) && panelObject != null)
            {
                panelObject.SetActive(!panelObject.activeSelf);
                Debug.Log($"[PlayerUpgradeDebugHUD] Toggled: {panelObject.activeSelf}");
            }

            // Only update if visible
            if (panelObject == null || !panelObject.activeSelf)
                return;

            // Re-cache references if they become available
            if (diggingSystem == null || inventorySystem == null || upgradeStation == null)
            {
                CacheReferences();
            }

            UpdateAllTexts();
#pragma warning restore CS0162
        }

        private void UpdateAllTexts()
        {
            UpdateToolText();
            UpdateDigSpeedText();
            UpdateBackpackText();
            UpdateLightText();
            UpdateCreditsText();
        }

        private void UpdateToolText()
        {
            if (toolText == null) return;

            if (diggingSystem != null)
            {
                string toolName = diggingSystem.CurrentToolName;
                float digPower = diggingSystem.CurrentDigPower;
                float maxDepth = diggingSystem.CurrentMaxDigDepth;
                int toolLevel = GetUpgradeLevel(UpgradeType.ToolTier);

                // Get depth, hardness, and duration info
                float currentDepth = diggingSystem.CurrentDigDepthMeters;
                float hardness = diggingSystem.CurrentSoilHardness;
                float duration = diggingSystem.CurrentCalculatedDuration;
                float factor = diggingSystem.CurrentSpeedFactor;

                // Color code based on speed factor (lower = faster = green)
                string factorColor;
                if (factor <= 0.7f)
                    factorColor = "#80FF80"; // Green - fast
                else if (factor <= 1.2f)
                    factorColor = "#FFFF80"; // Yellow - normal
                else
                    factorColor = "#FF8080"; // Red - slow

                // Show formula: duration = base * (hardness/power) / speedMult
                toolText.text = $"Tool: {toolName} (Lv {toolLevel})\n" +
                               $"  Power: {digPower:F2} | Hardness: {hardness:F2}\n" +
                               $"  Factor: <color={factorColor}>{factor:F2}</color> | Duration: {duration:F2}s\n" +
                               $"  Depth: {currentDepth:F1}m / Max: {maxDepth:F0}m";
            }
            else
            {
                toolText.text = "Tool: [DiggingSystem not found]";
            }
        }

        private void UpdateDigSpeedText()
        {
            if (digSpeedText == null) return;

            if (diggingSystem != null)
            {
                float mult = diggingSystem.DigSpeedMultiplier;
                int level = GetUpgradeLevel(UpgradeType.DigSpeed);
                float cd = diggingSystem.CurrentEffectiveCooldown;
                float baseDur = diggingSystem.BaseDigDuration;

                // Show the formula explanation
                digSpeedText.text = $"Dig Speed: x{mult:0.00} (Lv {level})\n" +
                                   $"  Base: {baseDur:0.00}s | CD: {cd:0.00}s";
            }
            else
            {
                digSpeedText.text = "Dig Speed: [DiggingSystem not found]";
            }
        }

        private void UpdateBackpackText()
        {
            if (backpackText == null) return;

            if (inventorySystem != null)
            {
                int maxSlots = inventorySystem.MaxSlots;
                int usedSlots = CountUsedSlots();
                int level = GetUpgradeLevel(UpgradeType.InventorySize);
                backpackText.text = $"Backpack: {usedSlots}/{maxSlots} (Lv {level})";
            }
            else
            {
                backpackText.text = "Backpack: [InventorySystem not found]";
            }
        }

        private int CountUsedSlots()
        {
            if (inventorySystem == null) return 0;
            var stacks = inventorySystem.GetAllStacks();
            if (stacks == null) return 0;
            return stacks.Count(s => !s.IsEmpty);
        }

        private void UpdateLightText()
        {
            if (lightText == null) return;

            int level = GetUpgradeLevel(UpgradeType.LightRadius);

            // Re-check for light if not found
            if (playerLight == null)
            {
                var headlampCheck = Player.PlayerHeadlamp.Instance;
                if (headlampCheck != null && headlampCheck.HeadlampLight != null)
                {
                    playerLight = headlampCheck.HeadlampLight;
                }
                else if (playerController != null)
                {
                    playerLight = playerController.GetComponentInChildren<Light>(includeInactive: true);
                }
            }

            if (playerLight != null)
            {
                string status = playerLight.enabled ? "ON" : "OFF";
                string statusColor = playerLight.enabled ? "#80FF80" : "#FF8080";
                lightText.text = $"Light: {playerLight.range:F1}m (Lv {level}) <color={statusColor}>[{status}]</color>";
            }
            else
            {
                lightText.text = $"Light: Lv {level} <color=#FF8080>[not found]</color>";
            }
        }

        private void UpdateCreditsText()
        {
            if (creditsText == null) return;

            if (CurrencyManager.Instance != null)
            {
                int amount = CurrencyManager.Instance.CurrentAmount;
                creditsText.text = $"Credits: {amount:N0}";
            }
            else
            {
                creditsText.text = "Credits: [CurrencyManager not found]";
            }
        }

        private int GetUpgradeLevel(UpgradeType type)
        {
            if (upgradeStation == null) return 0;

            foreach (var upgrade in upgradeStation.RuntimeUpgrades)
            {
                if (upgrade.upgradeType == type)
                    return upgrade.currentLevel;
            }
            return 0;
        }

        public void Show()
        {
            if (panelObject != null)
                panelObject.SetActive(true);
        }

        public void Hide()
        {
            if (panelObject != null)
                panelObject.SetActive(false);
        }

        public void Toggle()
        {
            if (panelObject != null)
                panelObject.SetActive(!panelObject.activeSelf);
        }
    }
}
