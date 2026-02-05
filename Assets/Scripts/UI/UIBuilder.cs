using UnityEngine;
using UnityEngine.UI;
using TMPro;
using BeneathTheFloor.NPC;

namespace BeneathTheFloor.UI
{
    /// <summary>
    /// Runtime UI builder that creates all UI elements programmatically
    /// Attach to the Canvas or a manager object
    /// </summary>
    public class UIBuilder : MonoBehaviour
    {
        [Header("References")]
        [SerializeField] private Canvas canvas;
        [SerializeField] private HUDController hudController;
        [SerializeField] private PauseMenu pauseMenu;
#pragma warning disable CS0618 // Type or member is obsolete
        [SerializeField] private InventoryUI inventoryUI;
#pragma warning restore CS0618
        [SerializeField] private DialogueUI dialogueUI;

        [Header("Build Options")]
        [SerializeField] private bool buildOnAwake = true;
        [SerializeField] private bool destroyAfterBuild = true;

        [Header("Fonts")]
        [SerializeField] private TMP_FontAsset defaultFont;

        private void Awake()
        {
            if (buildOnAwake)
            {
                BuildAllUI();
                if (destroyAfterBuild)
                {
                    Destroy(this);
                }
            }
        }

        public void BuildAllUI()
        {
            if (canvas == null)
            {
                canvas = GetComponentInParent<Canvas>();
                if (canvas == null)
                {
                    canvas = FindObjectOfType<Canvas>();
                }
            }

            if (canvas == null)
            {
                Debug.LogError("UIBuilder: No Canvas found!");
                return;
            }

            // Find default TMP font if not assigned
            if (defaultFont == null)
            {
                defaultFont = Resources.Load<TMP_FontAsset>("Fonts & Materials/LiberationSans SDF");
            }

            BuildHUD();
            BuildPauseMenu();
            BuildInventoryUI();
            BuildDialogueUI();
            BuildCrosshair();

            Debug.Log("UIBuilder: All UI elements created successfully!");
        }

        private void BuildHUD()
        {
            Transform hud = canvas.transform.Find("HUD");
            if (hud == null)
            {
                hud = CreateUIElement("HUD", canvas.transform).transform;
            }

            // Resource Panel (top-left)
            Transform resourcePanel = hud.Find("ResourcePanel");
            if (resourcePanel == null)
            {
                resourcePanel = CreateUIElement("ResourcePanel", hud).transform;
            }
            SetupRectTransform(resourcePanel, new Vector2(0, 1), new Vector2(0, 1), new Vector2(0, 1),
                new Vector2(20, -20), new Vector2(200, 280));

            // Add background
            Image resourceBg = resourcePanel.GetComponent<Image>();
            if (resourceBg == null)
            {
                resourceBg = resourcePanel.gameObject.AddComponent<Image>();
            }
            resourceBg.color = new Color(0, 0, 0, 0.5f);

            // Add VerticalLayoutGroup
            VerticalLayoutGroup resourceLayout = resourcePanel.GetComponent<VerticalLayoutGroup>();
            if (resourceLayout == null)
            {
                resourceLayout = resourcePanel.gameObject.AddComponent<VerticalLayoutGroup>();
            }
            resourceLayout.padding = new RectOffset(10, 10, 10, 10);
            resourceLayout.spacing = 5;
            resourceLayout.childAlignment = TextAnchor.UpperLeft;
            resourceLayout.childControlHeight = false;
            resourceLayout.childControlWidth = true;
            resourceLayout.childForceExpandHeight = false;
            resourceLayout.childForceExpandWidth = true;

            // Title
            CreateTextElement("Title", resourcePanel, "Resources", 20, Color.white, TextAlignmentOptions.Center);

            // Create resource counters
            string[] resources = { "Dirt", "Clay", "Coal", "Iron", "Copper", "Silver", "Gold" };
            Color[] colors = {
                new Color(0.6f, 0.4f, 0.2f),  // Dirt - brown
                new Color(0.8f, 0.5f, 0.3f),  // Clay - orange-brown
                new Color(0.3f, 0.3f, 0.3f),  // Coal - dark gray
                new Color(0.6f, 0.6f, 0.65f), // Iron - silver-gray
                new Color(0.8f, 0.5f, 0.2f),  // Copper - copper
                new Color(0.75f, 0.75f, 0.8f),// Silver - light silver
                new Color(1f, 0.84f, 0f)      // Gold - gold
            };

            for (int i = 0; i < resources.Length; i++)
            {
                CreateTextElement($"{resources[i]}Counter", resourcePanel, $"{resources[i]}: 0", 16, colors[i], TextAlignmentOptions.Left);
            }

            // Depth Panel (top-center)
            Transform depthPanel = hud.Find("DepthPanel");
            if (depthPanel == null)
            {
                depthPanel = CreateUIElement("DepthPanel", hud).transform;
            }
            SetupRectTransform(depthPanel, new Vector2(0.5f, 1), new Vector2(0.5f, 1), new Vector2(0.5f, 1),
                new Vector2(0, -20), new Vector2(200, 80));

            Image depthBg = depthPanel.GetComponent<Image>();
            if (depthBg == null)
            {
                depthBg = depthPanel.gameObject.AddComponent<Image>();
            }
            depthBg.color = new Color(0, 0, 0, 0.5f);

            // Depth text
            TextMeshProUGUI depthText = CreateTextElement("DepthText", depthPanel, "Depth: 0m", 24, Color.white, TextAlignmentOptions.Center);
            SetupRectTransform(depthText.transform, Vector2.zero, Vector2.one, new Vector2(0.5f, 0.5f),
                Vector2.zero, Vector2.zero);

            // Tool Panel - DISABLED (bottom-right now used for ConsumablesHUD)
            // Tool display removed from HUD per design spec
            /*
            Transform toolPanel = hud.Find("ToolPanel");
            if (toolPanel == null)
            {
                toolPanel = CreateUIElement("ToolPanel", hud).transform;
            }
            SetupRectTransform(toolPanel, new Vector2(1, 0), new Vector2(1, 0), new Vector2(1, 0),
                new Vector2(-20, 20), new Vector2(180, 100));

            Image toolBg = toolPanel.GetComponent<Image>();
            if (toolBg == null)
            {
                toolBg = toolPanel.gameObject.AddComponent<Image>();
            }
            toolBg.color = new Color(0, 0, 0, 0.5f);

            // Tool icon
            GameObject toolIconObj = CreateUIElement("ToolIcon", toolPanel);
            SetupRectTransform(toolIconObj.transform, new Vector2(0, 0.5f), new Vector2(0, 0.5f), new Vector2(0, 0.5f),
                new Vector2(10, 0), new Vector2(60, 60));
            Image toolIcon = toolIconObj.AddComponent<Image>();
            toolIcon.color = Color.gray;

            // Tool name
            TextMeshProUGUI toolName = CreateTextElement("ToolName", toolPanel, "No Tool", 16, Color.white, TextAlignmentOptions.Left);
            SetupRectTransform(toolName.transform, new Vector2(0, 1), new Vector2(1, 1), new Vector2(0, 1),
                new Vector2(80, -10), new Vector2(-90, 30));

            // Durability slider
            GameObject durabilityObj = CreateUIElement("DurabilitySlider", toolPanel);
            SetupRectTransform(durabilityObj.transform, new Vector2(0, 0), new Vector2(1, 0), new Vector2(0, 0),
                new Vector2(80, 10), new Vector2(-90, 20));
            Slider durabilitySlider = CreateSlider(durabilityObj, new Color(0.2f, 0.6f, 0.2f));
            */

            // Interaction Prompt (bottom-center)
            Transform interactionPrompt = hud.Find("InteractionPrompt");
            if (interactionPrompt == null)
            {
                interactionPrompt = CreateUIElement("InteractionPrompt", hud).transform;
            }
            SetupRectTransform(interactionPrompt, new Vector2(0.5f, 0), new Vector2(0.5f, 0), new Vector2(0.5f, 0),
                new Vector2(0, 100), new Vector2(400, 50));

            Image promptBg = interactionPrompt.GetComponent<Image>();
            if (promptBg == null)
            {
                promptBg = interactionPrompt.gameObject.AddComponent<Image>();
            }
            promptBg.color = new Color(0, 0, 0, 0.7f);

            TextMeshProUGUI promptText = CreateTextElement("PromptText", interactionPrompt, "Press E to interact", 20, Color.white, TextAlignmentOptions.Center);
            SetupRectTransform(promptText.transform, Vector2.zero, Vector2.one, new Vector2(0.5f, 0.5f),
                Vector2.zero, Vector2.zero);

            interactionPrompt.gameObject.SetActive(false);

            // Dig Progress Bar (above interaction prompt)
            Transform digProgress = hud.Find("DigProgressBar");
            if (digProgress == null)
            {
                digProgress = CreateUIElement("DigProgressBar", hud).transform;
            }
            SetupRectTransform(digProgress, new Vector2(0.5f, 0), new Vector2(0.5f, 0), new Vector2(0.5f, 0),
                new Vector2(0, 160), new Vector2(300, 30));

            CanvasGroup digProgressGroup = digProgress.GetComponent<CanvasGroup>();
            if (digProgressGroup == null)
            {
                digProgressGroup = digProgress.gameObject.AddComponent<CanvasGroup>();
            }
            digProgressGroup.alpha = 0;

            Slider digSlider = CreateSlider(digProgress.gameObject, new Color(0.8f, 0.5f, 0.2f));

            // Wire up HUDController references if available
            if (hudController != null)
            {
                // These would need reflection or direct assignment in inspector
                Debug.Log("UIBuilder: HUDController found - assign references in inspector or via code");
            }
        }

        private void BuildPauseMenu()
        {
            Transform pauseMenuPanel = canvas.transform.Find("PauseMenu");
            if (pauseMenuPanel == null)
            {
                pauseMenuPanel = CreateUIElement("PauseMenu", canvas.transform).transform;
            }
            SetupRectTransform(pauseMenuPanel, Vector2.zero, Vector2.one, new Vector2(0.5f, 0.5f),
                Vector2.zero, Vector2.zero);

            Image pauseBg = pauseMenuPanel.GetComponent<Image>();
            if (pauseBg == null)
            {
                pauseBg = pauseMenuPanel.gameObject.AddComponent<Image>();
            }
            pauseBg.color = new Color(0, 0, 0, 0.8f);

            // Main Panel
            Transform mainPanel = pauseMenuPanel.Find("MainPanel");
            if (mainPanel == null)
            {
                mainPanel = CreateUIElement("MainPanel", pauseMenuPanel).transform;
            }
            SetupRectTransform(mainPanel, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f),
                Vector2.zero, new Vector2(300, 400));

            Image mainPanelBg = mainPanel.GetComponent<Image>();
            if (mainPanelBg == null)
            {
                mainPanelBg = mainPanel.gameObject.AddComponent<Image>();
            }
            mainPanelBg.color = new Color(0.2f, 0.2f, 0.2f, 0.95f);

            // Layout
            VerticalLayoutGroup layout = mainPanel.GetComponent<VerticalLayoutGroup>();
            if (layout == null)
            {
                layout = mainPanel.gameObject.AddComponent<VerticalLayoutGroup>();
            }
            layout.padding = new RectOffset(20, 20, 30, 30);
            layout.spacing = 15;
            layout.childAlignment = TextAnchor.UpperCenter;
            layout.childControlHeight = false;
            layout.childControlWidth = true;
            layout.childForceExpandHeight = false;
            layout.childForceExpandWidth = true;

            // Title
            CreateTextElement("Title", mainPanel, "PAUSED", 36, Color.white, TextAlignmentOptions.Center);

            // Buttons
            CreateButton("ResumeButton", mainPanel, "Resume", new Vector2(260, 50));
            CreateButton("SettingsButton", mainPanel, "Settings", new Vector2(260, 50));
            CreateButton("MainMenuButton", mainPanel, "Main Menu", new Vector2(260, 50));
            CreateButton("QuitButton", mainPanel, "Quit", new Vector2(260, 50));

            pauseMenuPanel.gameObject.SetActive(false);
        }

        private void BuildInventoryUI()
        {
            // DISABLED: Inventory UI is now handled by InventoryUIManager (Storage Tray style)
            // The old tab-based inventory with 6-column grid is deprecated.
            // See: Assets/Scripts/InventoryUI/InventoryUIManager.cs
            Debug.Log("[UIBuilder] BuildInventoryUI DISABLED - using InventoryUIManager instead");

            // Destroy any existing old-style "Inventory" object from the canvas
            Transform oldInventory = canvas.transform.Find("Inventory");
            if (oldInventory != null)
            {
                Debug.Log("[UIBuilder] Destroying old 'Inventory' UI object");
                Destroy(oldInventory.gameObject);
            }
        }

        private void BuildDialogueUI()
        {
            Transform dialoguePanel = canvas.transform.Find("DialoguePanel");
            if (dialoguePanel == null)
            {
                dialoguePanel = CreateUIElement("DialoguePanel", canvas.transform).transform;
            }
            SetupRectTransform(dialoguePanel, new Vector2(0.5f, 0), new Vector2(0.5f, 0), new Vector2(0.5f, 0),
                new Vector2(0, 50), new Vector2(800, 200));

            Image dialogueBg = dialoguePanel.GetComponent<Image>();
            if (dialogueBg == null)
            {
                dialogueBg = dialoguePanel.gameObject.AddComponent<Image>();
            }
            dialogueBg.color = new Color(0.1f, 0.1f, 0.1f, 0.95f);

            // Portrait frame
            Transform portrait = dialoguePanel.Find("Portrait");
            if (portrait == null)
            {
                portrait = CreateUIElement("Portrait", dialoguePanel).transform;
            }
            SetupRectTransform(portrait, new Vector2(0, 0.5f), new Vector2(0, 0.5f), new Vector2(0, 0.5f),
                new Vector2(20, 0), new Vector2(150, 150));

            Image portraitBg = portrait.GetComponent<Image>();
            if (portraitBg == null)
            {
                portraitBg = portrait.gameObject.AddComponent<Image>();
            }
            portraitBg.color = new Color(0.3f, 0.3f, 0.3f, 1f);

            // Speaker name
            TextMeshProUGUI speakerName = CreateTextElement("SpeakerName", dialoguePanel, "Speaker", 22, Color.yellow, TextAlignmentOptions.Left);
            SetupRectTransform(speakerName.transform, new Vector2(0, 1), new Vector2(1, 1), new Vector2(0, 1),
                new Vector2(190, -15), new Vector2(-220, 35));

            // Dialogue text
            TextMeshProUGUI dialogueText = CreateTextElement("DialogueText", dialoguePanel, "Dialogue text goes here...", 18, Color.white, TextAlignmentOptions.TopLeft);
            SetupRectTransform(dialogueText.transform, new Vector2(0, 0), new Vector2(1, 1), new Vector2(0.5f, 0.5f),
                new Vector2(95, 0), new Vector2(-220, -60));

            // Continue indicator
            TextMeshProUGUI continueText = CreateTextElement("ContinueIndicator", dialoguePanel, "Click to continue...", 14, new Color(0.7f, 0.7f, 0.7f), TextAlignmentOptions.Right);
            SetupRectTransform(continueText.transform, new Vector2(1, 0), new Vector2(1, 0), new Vector2(1, 0),
                new Vector2(-20, 15), new Vector2(200, 25));

            dialoguePanel.gameObject.SetActive(false);
        }

        private void BuildCrosshair()
        {
            Transform crosshair = canvas.transform.Find("Crosshair");
            if (crosshair == null)
            {
                crosshair = CreateUIElement("Crosshair", canvas.transform).transform;
            }
            SetupRectTransform(crosshair, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f),
                Vector2.zero, new Vector2(20, 20));

            Image crosshairImg = crosshair.GetComponent<Image>();
            if (crosshairImg == null)
            {
                crosshairImg = crosshair.gameObject.AddComponent<Image>();
            }
            crosshairImg.color = Color.white;

            // Create a simple crosshair using child images
            // Horizontal line
            GameObject hLine = CreateUIElement("HLine", crosshair);
            SetupRectTransform(hLine.transform, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f),
                Vector2.zero, new Vector2(16, 2));
            Image hLineImg = hLine.AddComponent<Image>();
            hLineImg.color = Color.white;

            // Vertical line
            GameObject vLine = CreateUIElement("VLine", crosshair);
            SetupRectTransform(vLine.transform, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f),
                Vector2.zero, new Vector2(2, 16));
            Image vLineImg = vLine.AddComponent<Image>();
            vLineImg.color = Color.white;

            // Disable parent image (we use children for the cross shape)
            crosshairImg.enabled = false;
        }

        #region Helper Methods

        private GameObject CreateUIElement(string name, Transform parent)
        {
            GameObject obj = new GameObject(name, typeof(RectTransform));
            obj.transform.SetParent(parent, false);
            return obj;
        }

        private void SetupRectTransform(Transform t, Vector2 anchorMin, Vector2 anchorMax, Vector2 pivot, Vector2 anchoredPos, Vector2 sizeDelta)
        {
            RectTransform rt = t as RectTransform;
            if (rt == null) rt = t.GetComponent<RectTransform>();
            if (rt == null) return;

            rt.anchorMin = anchorMin;
            rt.anchorMax = anchorMax;
            rt.pivot = pivot;
            rt.anchoredPosition = anchoredPos;
            rt.sizeDelta = sizeDelta;
        }

        private TextMeshProUGUI CreateTextElement(string name, Transform parent, string text, float fontSize, Color color, TextAlignmentOptions alignment)
        {
            GameObject obj = CreateUIElement(name, parent);
            TextMeshProUGUI tmp = obj.AddComponent<TextMeshProUGUI>();
            tmp.text = text;
            tmp.fontSize = fontSize;
            tmp.color = color;
            tmp.alignment = alignment;
            if (defaultFont != null)
            {
                tmp.font = defaultFont;
            }

            // Set layout element for vertical layouts
            LayoutElement le = obj.AddComponent<LayoutElement>();
            le.preferredHeight = fontSize + 8;

            return tmp;
        }

        private GameObject CreateButton(string name, Transform parent, string text, Vector2 size)
        {
            GameObject btnObj = CreateUIElement(name, parent);

            Image btnBg = btnObj.AddComponent<Image>();
            btnBg.color = new Color(0.3f, 0.3f, 0.35f, 1f);

            Button btn = btnObj.AddComponent<Button>();
            ColorBlock colors = btn.colors;
            colors.normalColor = new Color(0.3f, 0.3f, 0.35f);
            colors.highlightedColor = new Color(0.4f, 0.4f, 0.45f);
            colors.pressedColor = new Color(0.25f, 0.25f, 0.3f);
            colors.selectedColor = new Color(0.35f, 0.35f, 0.4f);
            btn.colors = colors;

            LayoutElement le = btnObj.AddComponent<LayoutElement>();
            le.preferredWidth = size.x;
            le.preferredHeight = size.y;

            // Text child
            TextMeshProUGUI btnText = CreateTextElement("Text", btnObj.transform, text, 18, Color.white, TextAlignmentOptions.Center);
            SetupRectTransform(btnText.transform, Vector2.zero, Vector2.one, new Vector2(0.5f, 0.5f),
                Vector2.zero, Vector2.zero);

            return btnObj;
        }

        private Slider CreateSlider(GameObject obj, Color fillColor)
        {
            Slider slider = obj.AddComponent<Slider>();
            slider.minValue = 0;
            slider.maxValue = 1;
            slider.value = 0;

            // Background
            GameObject bg = CreateUIElement("Background", obj.transform);
            SetupRectTransform(bg.transform, Vector2.zero, Vector2.one, new Vector2(0.5f, 0.5f),
                Vector2.zero, Vector2.zero);
            Image bgImg = bg.AddComponent<Image>();
            bgImg.color = new Color(0.2f, 0.2f, 0.2f, 1f);

            // Fill area
            GameObject fillArea = CreateUIElement("Fill Area", obj.transform);
            SetupRectTransform(fillArea.transform, Vector2.zero, Vector2.one, new Vector2(0.5f, 0.5f),
                Vector2.zero, new Vector2(-10, 0));

            // Fill
            GameObject fill = CreateUIElement("Fill", fillArea.transform);
            SetupRectTransform(fill.transform, Vector2.zero, new Vector2(0, 1), new Vector2(0, 0.5f),
                Vector2.zero, new Vector2(0, 0));
            Image fillImg = fill.AddComponent<Image>();
            fillImg.color = fillColor;

            slider.fillRect = fill.GetComponent<RectTransform>();
            slider.targetGraphic = bgImg;

            return slider;
        }

        #endregion
    }
}
