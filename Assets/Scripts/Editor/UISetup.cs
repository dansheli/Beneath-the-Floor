using UnityEngine;
using UnityEngine.UI;
using UnityEditor;
using TMPro;

public class UISetup : Editor
{
    private static Color panelColor = new Color(0.1f, 0.1f, 0.1f, 0.9f);
    private static Color buttonColor = new Color(0.2f, 0.2f, 0.2f, 1f);
    private static Color accentColor = new Color(0.3f, 0.6f, 0.4f, 1f);

    [MenuItem("Tools/Beneath The Floor/Setup Complete UI")]
    public static void SetupCompleteUI()
    {
        GameObject uiRoot = GameObject.Find("UI");
        if (uiRoot == null)
        {
            Debug.LogError("UI GameObject not found!");
            return;
        }

        Canvas canvas = uiRoot.GetComponent<Canvas>();
        if (canvas == null)
        {
            Debug.LogError("Canvas component not found on UI!");
            return;
        }

        // Setup CanvasScaler for resolution scaling
        CanvasScaler scaler = uiRoot.GetComponent<CanvasScaler>();
        if (scaler != null)
        {
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920, 1080);
            scaler.screenMatchMode = CanvasScaler.ScreenMatchMode.MatchWidthOrHeight;
            scaler.matchWidthOrHeight = 0.5f;
        }

        // Setup each UI section
        SetupHUD(uiRoot);
        SetupPauseMenu(uiRoot);
        SetupInventoryUI(uiRoot);
        SetupDialoguePanel(uiRoot);
        SetupCrosshair(uiRoot);

        Debug.Log("Complete UI setup finished!");
    }

    private static void SetupHUD(GameObject uiRoot)
    {
        Transform hudTransform = uiRoot.transform.Find("HUD");
        if (hudTransform == null) return;

        GameObject hud = hudTransform.gameObject;

        // Add RectTransform if needed
        RectTransform hudRect = hud.GetComponent<RectTransform>();
        if (hudRect == null) hudRect = hud.AddComponent<RectTransform>();
        hudRect.anchorMin = Vector2.zero;
        hudRect.anchorMax = Vector2.one;
        hudRect.offsetMin = Vector2.zero;
        hudRect.offsetMax = Vector2.zero;

        // Create Resource Panel (Top Left)
        GameObject resourcePanel = CreatePanel("ResourcePanel", hud.transform);
        RectTransform rpRect = resourcePanel.GetComponent<RectTransform>();
        rpRect.anchorMin = new Vector2(0, 1);
        rpRect.anchorMax = new Vector2(0, 1);
        rpRect.pivot = new Vector2(0, 1);
        rpRect.anchoredPosition = new Vector2(20, -20);
        rpRect.sizeDelta = new Vector2(200, 280);

        // Add vertical layout
        VerticalLayoutGroup vlg = resourcePanel.AddComponent<VerticalLayoutGroup>();
        vlg.padding = new RectOffset(10, 10, 10, 10);
        vlg.spacing = 5;
        vlg.childControlWidth = true;
        vlg.childControlHeight = false;
        vlg.childForceExpandWidth = true;
        vlg.childForceExpandHeight = false;

        // Resource Title
        CreateText("ResourceTitle", resourcePanel.transform, "Resources", 18, TextAlignmentOptions.Center);

        // Create resource counters
        string[] resources = { "Dirt", "Clay", "Coal", "Iron", "Copper", "Silver", "Gold" };
        Color[] resourceColors = {
            new Color(0.6f, 0.4f, 0.2f),
            new Color(0.8f, 0.5f, 0.3f),
            new Color(0.2f, 0.2f, 0.2f),
            new Color(0.7f, 0.7f, 0.7f),
            new Color(0.8f, 0.5f, 0.2f),
            new Color(0.9f, 0.9f, 0.9f),
            new Color(1f, 0.84f, 0f)
        };

        for (int i = 0; i < resources.Length; i++)
        {
            GameObject resourceRow = new GameObject($"Resource_{resources[i]}");
            resourceRow.transform.SetParent(resourcePanel.transform, false);
            RectTransform rowRect = resourceRow.AddComponent<RectTransform>();
            rowRect.sizeDelta = new Vector2(180, 25);

            HorizontalLayoutGroup hlg = resourceRow.AddComponent<HorizontalLayoutGroup>();
            hlg.spacing = 10;
            hlg.childControlWidth = true;
            hlg.childControlHeight = true;
            hlg.childForceExpandWidth = true;
            hlg.childForceExpandHeight = true;

            // Color indicator
            GameObject colorBox = new GameObject("ColorBox");
            colorBox.transform.SetParent(resourceRow.transform, false);
            Image colorImg = colorBox.AddComponent<Image>();
            colorImg.color = resourceColors[i];
            LayoutElement colorLE = colorBox.AddComponent<LayoutElement>();
            colorLE.minWidth = 20;
            colorLE.preferredWidth = 20;

            // Resource name and count
            CreateText($"{resources[i]}Text", resourceRow.transform, $"{resources[i]}: 0", 14, TextAlignmentOptions.Left);
        }

        // Create Depth Meter (Top Center)
        GameObject depthPanel = CreatePanel("DepthPanel", hud.transform);
        RectTransform dpRect = depthPanel.GetComponent<RectTransform>();
        dpRect.anchorMin = new Vector2(0.5f, 1);
        dpRect.anchorMax = new Vector2(0.5f, 1);
        dpRect.pivot = new Vector2(0.5f, 1);
        dpRect.anchoredPosition = new Vector2(0, -20);
        dpRect.sizeDelta = new Vector2(200, 80);

        VerticalLayoutGroup depthVlg = depthPanel.AddComponent<VerticalLayoutGroup>();
        depthVlg.padding = new RectOffset(10, 10, 10, 10);
        depthVlg.spacing = 5;
        depthVlg.childControlWidth = true;
        depthVlg.childControlHeight = false;
        depthVlg.childAlignment = TextAnchor.MiddleCenter;

        CreateText("DepthTitle", depthPanel.transform, "Depth", 16, TextAlignmentOptions.Center);
        CreateText("DepthValue", depthPanel.transform, "0m", 28, TextAlignmentOptions.Center);

        // Create depth slider
        GameObject depthSlider = CreateSlider("DepthSlider", depthPanel.transform);

        // Create Tool Indicator (Top Right)
        GameObject toolPanel = CreatePanel("ToolPanel", hud.transform);
        RectTransform tpRect = toolPanel.GetComponent<RectTransform>();
        tpRect.anchorMin = new Vector2(1, 1);
        tpRect.anchorMax = new Vector2(1, 1);
        tpRect.pivot = new Vector2(1, 1);
        tpRect.anchoredPosition = new Vector2(-20, -20);
        tpRect.sizeDelta = new Vector2(200, 120);

        VerticalLayoutGroup toolVlg = toolPanel.AddComponent<VerticalLayoutGroup>();
        toolVlg.padding = new RectOffset(10, 10, 10, 10);
        toolVlg.spacing = 5;
        toolVlg.childControlWidth = true;
        toolVlg.childControlHeight = false;
        toolVlg.childAlignment = TextAnchor.MiddleCenter;

        CreateText("ToolTitle", toolPanel.transform, "Current Tool", 14, TextAlignmentOptions.Center);
        CreateText("ToolName", toolPanel.transform, "Basic Shovel", 18, TextAlignmentOptions.Center);
        CreateText("ToolStats", toolPanel.transform, "Speed: 1x | Depth: 5m", 12, TextAlignmentOptions.Center);

        // Durability bar
        GameObject durabilitySlider = CreateSlider("DurabilitySlider", toolPanel.transform);

        // Create Interaction Prompt (Bottom Center)
        GameObject interactionPanel = CreatePanel("InteractionPrompt", hud.transform);
        RectTransform ipRect = interactionPanel.GetComponent<RectTransform>();
        ipRect.anchorMin = new Vector2(0.5f, 0);
        ipRect.anchorMax = new Vector2(0.5f, 0);
        ipRect.pivot = new Vector2(0.5f, 0);
        ipRect.anchoredPosition = new Vector2(0, 100);
        ipRect.sizeDelta = new Vector2(300, 50);
        interactionPanel.SetActive(false);

        CreateText("InteractionText", interactionPanel.transform, "Press E to interact", 18, TextAlignmentOptions.Center);

        // Dig Progress Bar (Center)
        GameObject digProgress = CreatePanel("DigProgress", hud.transform);
        RectTransform digRect = digProgress.GetComponent<RectTransform>();
        digRect.anchorMin = new Vector2(0.5f, 0.5f);
        digRect.anchorMax = new Vector2(0.5f, 0.5f);
        digRect.pivot = new Vector2(0.5f, 0.5f);
        digRect.anchoredPosition = new Vector2(0, -100);
        digRect.sizeDelta = new Vector2(300, 30);

        CanvasGroup digCG = digProgress.AddComponent<CanvasGroup>();
        digCG.alpha = 0;

        CreateSlider("DigProgressSlider", digProgress.transform);

        Debug.Log("HUD setup complete");
    }

    private static void SetupPauseMenu(GameObject uiRoot)
    {
        Transform pauseTransform = uiRoot.transform.Find("PauseMenu");
        if (pauseTransform == null) return;

        GameObject pauseMenu = pauseTransform.gameObject;
        pauseMenu.SetActive(false);

        RectTransform pmRect = pauseMenu.GetComponent<RectTransform>();
        if (pmRect == null) pmRect = pauseMenu.AddComponent<RectTransform>();
        pmRect.anchorMin = Vector2.zero;
        pmRect.anchorMax = Vector2.one;
        pmRect.offsetMin = Vector2.zero;
        pmRect.offsetMax = Vector2.zero;

        // Background overlay
        Image bgImage = pauseMenu.GetComponent<Image>();
        if (bgImage == null) bgImage = pauseMenu.AddComponent<Image>();
        bgImage.color = new Color(0, 0, 0, 0.7f);

        // Main panel
        GameObject mainPanel = CreatePanel("MainPanel", pauseMenu.transform);
        RectTransform mpRect = mainPanel.GetComponent<RectTransform>();
        mpRect.anchorMin = new Vector2(0.5f, 0.5f);
        mpRect.anchorMax = new Vector2(0.5f, 0.5f);
        mpRect.pivot = new Vector2(0.5f, 0.5f);
        mpRect.anchoredPosition = Vector2.zero;
        mpRect.sizeDelta = new Vector2(400, 400);

        VerticalLayoutGroup vlg = mainPanel.AddComponent<VerticalLayoutGroup>();
        vlg.padding = new RectOffset(30, 30, 30, 30);
        vlg.spacing = 20;
        vlg.childControlWidth = true;
        vlg.childControlHeight = false;
        vlg.childForceExpandWidth = true;
        vlg.childForceExpandHeight = false;
        vlg.childAlignment = TextAnchor.MiddleCenter;

        // Title
        CreateText("PauseTitle", mainPanel.transform, "PAUSED", 36, TextAlignmentOptions.Center);

        // Buttons
        CreateButton("ResumeButton", mainPanel.transform, "Resume", 60);
        CreateButton("SettingsButton", mainPanel.transform, "Settings", 60);
        CreateButton("MainMenuButton", mainPanel.transform, "Main Menu", 60);
        CreateButton("QuitButton", mainPanel.transform, "Quit Game", 60);

        // Settings Panel (hidden by default)
        GameObject settingsPanel = CreatePanel("SettingsPanel", pauseMenu.transform);
        settingsPanel.SetActive(false);
        RectTransform spRect = settingsPanel.GetComponent<RectTransform>();
        spRect.anchorMin = new Vector2(0.5f, 0.5f);
        spRect.anchorMax = new Vector2(0.5f, 0.5f);
        spRect.pivot = new Vector2(0.5f, 0.5f);
        spRect.anchoredPosition = Vector2.zero;
        spRect.sizeDelta = new Vector2(450, 350);

        VerticalLayoutGroup sVlg = settingsPanel.AddComponent<VerticalLayoutGroup>();
        sVlg.padding = new RectOffset(30, 30, 30, 30);
        sVlg.spacing = 15;
        sVlg.childControlWidth = true;
        sVlg.childControlHeight = false;

        CreateText("SettingsTitle", settingsPanel.transform, "Settings", 28, TextAlignmentOptions.Center);
        CreateSliderWithLabel("MusicVolume", settingsPanel.transform, "Music Volume");
        CreateSliderWithLabel("SFXVolume", settingsPanel.transform, "SFX Volume");
        CreateSliderWithLabel("Sensitivity", settingsPanel.transform, "Mouse Sensitivity");
        CreateButton("BackButton", settingsPanel.transform, "Back", 50);

        Debug.Log("Pause Menu setup complete");
    }

    private static void SetupInventoryUI(GameObject uiRoot)
    {
        Transform invTransform = uiRoot.transform.Find("Inventory");
        if (invTransform == null) return;

        GameObject inventory = invTransform.gameObject;
        inventory.SetActive(false);

        RectTransform invRect = inventory.GetComponent<RectTransform>();
        if (invRect == null) invRect = inventory.AddComponent<RectTransform>();
        invRect.anchorMin = Vector2.zero;
        invRect.anchorMax = Vector2.one;
        invRect.offsetMin = Vector2.zero;
        invRect.offsetMax = Vector2.zero;

        // Background
        Image bgImage = inventory.GetComponent<Image>();
        if (bgImage == null) bgImage = inventory.AddComponent<Image>();
        bgImage.color = new Color(0, 0, 0, 0.8f);

        // Main inventory panel
        GameObject mainPanel = CreatePanel("InventoryPanel", inventory.transform);
        RectTransform mpRect = mainPanel.GetComponent<RectTransform>();
        mpRect.anchorMin = new Vector2(0.5f, 0.5f);
        mpRect.anchorMax = new Vector2(0.5f, 0.5f);
        mpRect.pivot = new Vector2(0.5f, 0.5f);
        mpRect.anchoredPosition = Vector2.zero;
        mpRect.sizeDelta = new Vector2(700, 500);

        VerticalLayoutGroup vlg = mainPanel.AddComponent<VerticalLayoutGroup>();
        vlg.padding = new RectOffset(20, 20, 20, 20);
        vlg.spacing = 10;
        vlg.childControlWidth = true;
        vlg.childControlHeight = false;

        // Title
        CreateText("InventoryTitle", mainPanel.transform, "Inventory", 28, TextAlignmentOptions.Center);

        // Tab buttons
        GameObject tabBar = new GameObject("TabBar");
        tabBar.transform.SetParent(mainPanel.transform, false);
        RectTransform tabRect = tabBar.AddComponent<RectTransform>();
        tabRect.sizeDelta = new Vector2(660, 50);

        HorizontalLayoutGroup tabHlg = tabBar.AddComponent<HorizontalLayoutGroup>();
        tabHlg.spacing = 10;
        tabHlg.childControlWidth = true;
        tabHlg.childControlHeight = true;
        tabHlg.childForceExpandWidth = true;
        tabHlg.childForceExpandHeight = true;

        CreateButton("ResourcesTab", tabBar.transform, "Resources", 50);
        CreateButton("StoryTab", tabBar.transform, "Story Items", 50);
        CreateButton("ToolsTab", tabBar.transform, "Tools", 50);

        // Content area
        GameObject contentArea = CreatePanel("ContentArea", mainPanel.transform);
        RectTransform caRect = contentArea.GetComponent<RectTransform>();
        caRect.sizeDelta = new Vector2(660, 350);

        // Resources Grid
        GameObject resourcesContent = new GameObject("ResourcesContent");
        resourcesContent.transform.SetParent(contentArea.transform, false);
        RectTransform rcRect = resourcesContent.AddComponent<RectTransform>();
        rcRect.anchorMin = Vector2.zero;
        rcRect.anchorMax = Vector2.one;
        rcRect.offsetMin = new Vector2(10, 10);
        rcRect.offsetMax = new Vector2(-10, -10);

        GridLayoutGroup resourceGrid = resourcesContent.AddComponent<GridLayoutGroup>();
        resourceGrid.cellSize = new Vector2(100, 100);
        resourceGrid.spacing = new Vector2(10, 10);
        resourceGrid.constraint = GridLayoutGroup.Constraint.FixedColumnCount;
        resourceGrid.constraintCount = 5;

        // Create placeholder slots
        for (int i = 0; i < 15; i++)
        {
            CreateInventorySlot($"ResourceSlot_{i}", resourcesContent.transform);
        }

        // Story Items Content
        GameObject storyContent = new GameObject("StoryContent");
        storyContent.transform.SetParent(contentArea.transform, false);
        storyContent.SetActive(false);
        RectTransform scRect = storyContent.AddComponent<RectTransform>();
        scRect.anchorMin = Vector2.zero;
        scRect.anchorMax = Vector2.one;
        scRect.offsetMin = new Vector2(10, 10);
        scRect.offsetMax = new Vector2(-10, -10);

        GridLayoutGroup storyGrid = storyContent.AddComponent<GridLayoutGroup>();
        storyGrid.cellSize = new Vector2(150, 150);
        storyGrid.spacing = new Vector2(10, 10);
        storyGrid.constraint = GridLayoutGroup.Constraint.FixedColumnCount;
        storyGrid.constraintCount = 4;

        for (int i = 0; i < 8; i++)
        {
            CreateInventorySlot($"StorySlot_{i}", storyContent.transform);
        }

        // Tools Content
        GameObject toolsContent = new GameObject("ToolsContent");
        toolsContent.transform.SetParent(contentArea.transform, false);
        toolsContent.SetActive(false);
        RectTransform tcRect = toolsContent.AddComponent<RectTransform>();
        tcRect.anchorMin = Vector2.zero;
        tcRect.anchorMax = Vector2.one;
        tcRect.offsetMin = new Vector2(10, 10);
        tcRect.offsetMax = new Vector2(-10, -10);

        VerticalLayoutGroup toolsVlg = toolsContent.AddComponent<VerticalLayoutGroup>();
        toolsVlg.spacing = 10;
        toolsVlg.childControlWidth = true;
        toolsVlg.childControlHeight = false;

        CreateText("CurrentToolLabel", toolsContent.transform, "Equipped Tool:", 18, TextAlignmentOptions.Left);
        CreateText("CurrentToolInfo", toolsContent.transform, "Basic Shovel - Tier 1", 24, TextAlignmentOptions.Left);
        CreateText("ToolStatsLabel", toolsContent.transform, "Stats: Speed 1x | Max Depth 5m | Durability 50/50", 16, TextAlignmentOptions.Left);

        // Item detail panel
        GameObject detailPanel = CreatePanel("ItemDetail", inventory.transform);
        detailPanel.SetActive(false);
        RectTransform dpRect = detailPanel.GetComponent<RectTransform>();
        dpRect.anchorMin = new Vector2(1, 0.5f);
        dpRect.anchorMax = new Vector2(1, 0.5f);
        dpRect.pivot = new Vector2(0, 0.5f);
        dpRect.anchoredPosition = new Vector2(20, 0);
        dpRect.sizeDelta = new Vector2(250, 300);

        VerticalLayoutGroup dpVlg = detailPanel.AddComponent<VerticalLayoutGroup>();
        dpVlg.padding = new RectOffset(15, 15, 15, 15);
        dpVlg.spacing = 10;

        CreateText("ItemName", detailPanel.transform, "Item Name", 20, TextAlignmentOptions.Center);
        CreateText("ItemDescription", detailPanel.transform, "Item description goes here.", 14, TextAlignmentOptions.Left);

        Debug.Log("Inventory UI setup complete");
    }

    private static void SetupDialoguePanel(GameObject uiRoot)
    {
        Transform dialogueTransform = uiRoot.transform.Find("DialoguePanel");
        if (dialogueTransform == null) return;

        GameObject dialoguePanel = dialogueTransform.gameObject;
        dialoguePanel.SetActive(false);

        RectTransform dpRect = dialoguePanel.GetComponent<RectTransform>();
        if (dpRect == null) dpRect = dialoguePanel.AddComponent<RectTransform>();
        dpRect.anchorMin = new Vector2(0, 0);
        dpRect.anchorMax = new Vector2(1, 0);
        dpRect.pivot = new Vector2(0.5f, 0);
        dpRect.anchoredPosition = new Vector2(0, 50);
        dpRect.sizeDelta = new Vector2(-100, 200);

        // Background
        Image bgImage = dialoguePanel.GetComponent<Image>();
        if (bgImage == null) bgImage = dialoguePanel.AddComponent<Image>();
        bgImage.color = panelColor;

        // Content layout
        HorizontalLayoutGroup hlg = dialoguePanel.AddComponent<HorizontalLayoutGroup>();
        hlg.padding = new RectOffset(20, 20, 20, 20);
        hlg.spacing = 20;
        hlg.childControlWidth = false;
        hlg.childControlHeight = true;
        hlg.childForceExpandWidth = false;
        hlg.childForceExpandHeight = true;

        // Portrait area
        GameObject portrait = new GameObject("Portrait");
        portrait.transform.SetParent(dialoguePanel.transform, false);
        Image portraitImg = portrait.AddComponent<Image>();
        portraitImg.color = new Color(0.3f, 0.3f, 0.3f, 1f);
        LayoutElement portraitLE = portrait.AddComponent<LayoutElement>();
        portraitLE.minWidth = 150;
        portraitLE.preferredWidth = 150;

        // Text area
        GameObject textArea = new GameObject("TextArea");
        textArea.transform.SetParent(dialoguePanel.transform, false);
        RectTransform taRect = textArea.AddComponent<RectTransform>();

        VerticalLayoutGroup taVlg = textArea.AddComponent<VerticalLayoutGroup>();
        taVlg.spacing = 10;
        taVlg.childControlWidth = true;
        taVlg.childControlHeight = false;
        taVlg.childForceExpandWidth = true;

        LayoutElement taLE = textArea.AddComponent<LayoutElement>();
        taLE.flexibleWidth = 1;

        // Speaker name
        CreateText("SpeakerName", textArea.transform, "Speaker Name", 20, TextAlignmentOptions.Left);

        // Dialogue text
        GameObject dialogueText = CreateText("DialogueText", textArea.transform, "Dialogue text appears here with typewriter effect...", 16, TextAlignmentOptions.Left);
        RectTransform dtRect = dialogueText.GetComponent<RectTransform>();
        dtRect.sizeDelta = new Vector2(0, 100);

        // Continue button
        GameObject continueBtn = CreateButton("ContinueButton", textArea.transform, "Continue ►", 40);
        RectTransform cbRect = continueBtn.GetComponent<RectTransform>();

        Debug.Log("Dialogue Panel setup complete");
    }

    private static void SetupCrosshair(GameObject uiRoot)
    {
        GameObject crosshair = new GameObject("Crosshair");
        crosshair.transform.SetParent(uiRoot.transform, false);

        RectTransform chRect = crosshair.AddComponent<RectTransform>();
        chRect.anchorMin = new Vector2(0.5f, 0.5f);
        chRect.anchorMax = new Vector2(0.5f, 0.5f);
        chRect.pivot = new Vector2(0.5f, 0.5f);
        chRect.anchoredPosition = Vector2.zero;
        chRect.sizeDelta = new Vector2(20, 20);

        Image chImage = crosshair.AddComponent<Image>();
        chImage.color = Color.white;

        // Create crosshair shape (simple dot for now)
        // In a real game you'd use a sprite

        Debug.Log("Crosshair setup complete");
    }

    // Helper methods
    private static GameObject CreatePanel(string name, Transform parent)
    {
        GameObject panel = new GameObject(name);
        panel.transform.SetParent(parent, false);

        RectTransform rect = panel.AddComponent<RectTransform>();
        Image image = panel.AddComponent<Image>();
        image.color = panelColor;

        return panel;
    }

    private static GameObject CreateText(string name, Transform parent, string text, int fontSize, TextAlignmentOptions alignment)
    {
        GameObject textObj = new GameObject(name);
        textObj.transform.SetParent(parent, false);

        RectTransform rect = textObj.AddComponent<RectTransform>();
        rect.sizeDelta = new Vector2(0, fontSize + 10);

        TextMeshProUGUI tmp = textObj.AddComponent<TextMeshProUGUI>();
        tmp.text = text;
        tmp.fontSize = fontSize;
        tmp.alignment = alignment;
        tmp.color = Color.white;

        LayoutElement le = textObj.AddComponent<LayoutElement>();
        le.minHeight = fontSize + 10;
        le.preferredHeight = fontSize + 10;

        return textObj;
    }

    private static GameObject CreateButton(string name, Transform parent, string text, float height)
    {
        GameObject buttonObj = new GameObject(name);
        buttonObj.transform.SetParent(parent, false);

        RectTransform rect = buttonObj.AddComponent<RectTransform>();
        rect.sizeDelta = new Vector2(0, height);

        Image image = buttonObj.AddComponent<Image>();
        image.color = buttonColor;

        Button button = buttonObj.AddComponent<Button>();
        ColorBlock colors = button.colors;
        colors.normalColor = buttonColor;
        colors.highlightedColor = accentColor;
        colors.pressedColor = new Color(0.15f, 0.15f, 0.15f, 1f);
        button.colors = colors;

        // Button text
        GameObject textObj = new GameObject("Text");
        textObj.transform.SetParent(buttonObj.transform, false);

        RectTransform textRect = textObj.AddComponent<RectTransform>();
        textRect.anchorMin = Vector2.zero;
        textRect.anchorMax = Vector2.one;
        textRect.offsetMin = Vector2.zero;
        textRect.offsetMax = Vector2.zero;

        TextMeshProUGUI tmp = textObj.AddComponent<TextMeshProUGUI>();
        tmp.text = text;
        tmp.fontSize = 18;
        tmp.alignment = TextAlignmentOptions.Center;
        tmp.color = Color.white;

        LayoutElement le = buttonObj.AddComponent<LayoutElement>();
        le.minHeight = height;
        le.preferredHeight = height;

        return buttonObj;
    }

    private static GameObject CreateSlider(string name, Transform parent)
    {
        GameObject sliderObj = new GameObject(name);
        sliderObj.transform.SetParent(parent, false);

        RectTransform rect = sliderObj.AddComponent<RectTransform>();
        rect.sizeDelta = new Vector2(0, 20);

        Slider slider = sliderObj.AddComponent<Slider>();

        // Background
        GameObject background = new GameObject("Background");
        background.transform.SetParent(sliderObj.transform, false);
        RectTransform bgRect = background.AddComponent<RectTransform>();
        bgRect.anchorMin = Vector2.zero;
        bgRect.anchorMax = Vector2.one;
        bgRect.offsetMin = Vector2.zero;
        bgRect.offsetMax = Vector2.zero;
        Image bgImage = background.AddComponent<Image>();
        bgImage.color = new Color(0.15f, 0.15f, 0.15f, 1f);

        // Fill area
        GameObject fillArea = new GameObject("Fill Area");
        fillArea.transform.SetParent(sliderObj.transform, false);
        RectTransform faRect = fillArea.AddComponent<RectTransform>();
        faRect.anchorMin = Vector2.zero;
        faRect.anchorMax = Vector2.one;
        faRect.offsetMin = new Vector2(5, 5);
        faRect.offsetMax = new Vector2(-5, -5);

        // Fill
        GameObject fill = new GameObject("Fill");
        fill.transform.SetParent(fillArea.transform, false);
        RectTransform fillRect = fill.AddComponent<RectTransform>();
        fillRect.anchorMin = Vector2.zero;
        fillRect.anchorMax = Vector2.one;
        fillRect.offsetMin = Vector2.zero;
        fillRect.offsetMax = Vector2.zero;
        Image fillImage = fill.AddComponent<Image>();
        fillImage.color = accentColor;

        slider.fillRect = fillRect;
        slider.targetGraphic = bgImage;

        LayoutElement le = sliderObj.AddComponent<LayoutElement>();
        le.minHeight = 20;
        le.preferredHeight = 20;

        return sliderObj;
    }

    private static void CreateSliderWithLabel(string name, Transform parent, string label)
    {
        GameObject container = new GameObject(name + "Container");
        container.transform.SetParent(parent, false);
        RectTransform cRect = container.AddComponent<RectTransform>();
        cRect.sizeDelta = new Vector2(0, 50);

        VerticalLayoutGroup vlg = container.AddComponent<VerticalLayoutGroup>();
        vlg.spacing = 5;
        vlg.childControlWidth = true;
        vlg.childControlHeight = false;

        LayoutElement le = container.AddComponent<LayoutElement>();
        le.minHeight = 50;
        le.preferredHeight = 50;

        CreateText(name + "Label", container.transform, label, 14, TextAlignmentOptions.Left);
        CreateSlider(name + "Slider", container.transform);
    }

    private static void CreateInventorySlot(string name, Transform parent)
    {
        GameObject slot = new GameObject(name);
        slot.transform.SetParent(parent, false);

        RectTransform rect = slot.AddComponent<RectTransform>();
        Image image = slot.AddComponent<Image>();
        image.color = new Color(0.2f, 0.2f, 0.2f, 1f);

        Button button = slot.AddComponent<Button>();

        // Icon placeholder
        GameObject icon = new GameObject("Icon");
        icon.transform.SetParent(slot.transform, false);
        RectTransform iconRect = icon.AddComponent<RectTransform>();
        iconRect.anchorMin = new Vector2(0.1f, 0.3f);
        iconRect.anchorMax = new Vector2(0.9f, 0.9f);
        iconRect.offsetMin = Vector2.zero;
        iconRect.offsetMax = Vector2.zero;
        Image iconImg = icon.AddComponent<Image>();
        iconImg.color = new Color(0.4f, 0.4f, 0.4f, 0.5f);

        // Count text
        GameObject countText = new GameObject("Count");
        countText.transform.SetParent(slot.transform, false);
        RectTransform countRect = countText.AddComponent<RectTransform>();
        countRect.anchorMin = new Vector2(0, 0);
        countRect.anchorMax = new Vector2(1, 0.3f);
        countRect.offsetMin = Vector2.zero;
        countRect.offsetMax = Vector2.zero;

        TextMeshProUGUI tmp = countText.AddComponent<TextMeshProUGUI>();
        tmp.text = "0";
        tmp.fontSize = 14;
        tmp.alignment = TextAlignmentOptions.Center;
        tmp.color = Color.white;
    }
}
