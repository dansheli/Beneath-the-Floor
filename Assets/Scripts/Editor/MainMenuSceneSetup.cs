using UnityEngine;
using UnityEngine.UI;
using UnityEditor;
using UnityEditor.SceneManagement;
using TMPro;
using System.Collections.Generic;
using BeneathTheFloor.Save;

namespace BeneathTheFloor.Editor
{
    /// <summary>
    /// Editor utility to create and set up the Main Menu scene.
    /// Run from menu: Tools > Beneath The Floor > Create Main Menu Scene
    /// </summary>
    public static class MainMenuSceneSetup
    {
        private const string SCENE_PATH = "Assets/Scenes/MainMenuScene.unity";
        private const string CONTROLLER_SCRIPT = "BeneathTheFloor.GameFlow.MainMenuController";

        // Colors
        private static readonly Color BG_COLOR = new Color(0.02f, 0.02f, 0.04f, 1f);
        private static readonly Color PANEL_BG = new Color(0.1f, 0.1f, 0.12f, 0.95f);
        private static readonly Color BUTTON_NORMAL = new Color(0.15f, 0.15f, 0.18f, 1f);
        private static readonly Color BUTTON_HIGHLIGHT = new Color(0.25f, 0.25f, 0.3f, 1f);
        private static readonly Color BUTTON_PRESSED = new Color(0.1f, 0.1f, 0.12f, 1f);
        private static readonly Color ACCENT_COLOR = new Color(0.8f, 0.6f, 0.2f, 1f);
        private static readonly Color TEXT_COLOR = new Color(0.9f, 0.9f, 0.9f, 1f);
        private static readonly Color TEXT_HINT = new Color(0.6f, 0.5f, 0.4f, 1f);

        private static TMP_FontAsset defaultFont;
        private static GameObject canvasObj;
        private static RectTransform canvasRect;

        // UI References for controller
        private static CanvasGroup mainMenuCanvasGroup;
        private static Button continueBtn, newGameBtn, loadGameBtn, settingsBtn, quitBtn;
        private static TextMeshProUGUI continueHintText;
        private static GameObject loadPanel, settingsPanel, confirmDialog, deleteConfirmDialog;
        private static Button loadPanelLoadBtn, loadPanelDeleteBtn, loadPanelBackBtn;
        private static TextMeshProUGUI loadPanelStatusText, loadPanelSaveInfoText;
        private static Slider masterSlider, musicSlider, sfxSlider;
        private static Toggle fullscreenToggle;
        private static TMP_Dropdown resDropdown, qualityDropdown;
        private static Button settingsBackBtn;
        private static TextMeshProUGUI confirmDialogText, confirmDialogConfirmBtnText;
        private static Button confirmCancelBtn, confirmConfirmBtn;
        private static TextMeshProUGUI deleteConfirmText;
        private static Button deleteCancelBtn, deleteConfirmBtn;
        private static Image fadeOverlay;

        [MenuItem("Tools/Beneath The Floor/Create Main Menu Scene")]
        public static void CreateMainMenuScene()
        {
            if (System.IO.File.Exists(SCENE_PATH))
            {
                if (!EditorUtility.DisplayDialog("Scene Exists",
                    "MainMenuScene already exists. Recreate it?", "Recreate", "Cancel"))
                    return;
            }

            // Load default font
            LoadDefaultFont();

            // Create new scene
            var newScene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);

            // Create base objects
            CreateCamera();
            CreateEventSystem();
            canvasObj = CreateCanvas();
            canvasRect = canvasObj.GetComponent<RectTransform>();

            // Create UI layers
            CreateBackgroundImage();
            CreateMainMenuPanel();
            CreateLoadPanel();
            CreateSettingsPanel();
            CreateConfirmDialog();
            CreateDeleteConfirmDialog();
            CreateFadeOverlay();

            // Ensure SaveManager exists
            EnsureSaveManager();

            // Add and wire controller
            WireController();

            // Save scene
            EditorSceneManager.SaveScene(newScene, SCENE_PATH);
            AddSceneToBuildSettings(SCENE_PATH);

            Debug.Log($"[MainMenuSceneSetup] Scene created: {SCENE_PATH}");
            EditorUtility.DisplayDialog("Success", "MainMenuScene created!", "OK");
        }

        private static void EnsureSaveManager()
        {
            var existing = Object.FindObjectOfType<SaveManager>();
            if (existing != null)
            {
                Debug.Log("[MainMenuSceneSetup] SaveManager already exists.");
                return;
            }

            GameObject saveManagerObj = new GameObject("SaveManager");
            saveManagerObj.AddComponent<SaveManager>();
            Debug.Log("[MainMenuSceneSetup] SaveManager added to scene.");
        }

        private static void LoadDefaultFont()
        {
            string[] guids = AssetDatabase.FindAssets("LiberationSans SDF t:TMP_FontAsset");
            if (guids.Length > 0)
            {
                string path = AssetDatabase.GUIDToAssetPath(guids[0]);
                defaultFont = AssetDatabase.LoadAssetAtPath<TMP_FontAsset>(path);
            }
        }

        private static void CreateCamera()
        {
            GameObject camObj = new GameObject("Main Camera");
            Camera cam = camObj.AddComponent<Camera>();
            cam.clearFlags = CameraClearFlags.SolidColor;
            cam.backgroundColor = BG_COLOR;
            cam.orthographic = true;
            camObj.AddComponent<AudioListener>();
            camObj.tag = "MainCamera";
        }

        private static void CreateEventSystem()
        {
            GameObject esObj = new GameObject("EventSystem");
            esObj.AddComponent<UnityEngine.EventSystems.EventSystem>();
            esObj.AddComponent<UnityEngine.EventSystems.StandaloneInputModule>();
        }

        private static GameObject CreateCanvas()
        {
            GameObject obj = new GameObject("Canvas");
            Canvas canvas = obj.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = 0;

            CanvasScaler scaler = obj.AddComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920, 1080);
            scaler.screenMatchMode = CanvasScaler.ScreenMatchMode.MatchWidthOrHeight;
            scaler.matchWidthOrHeight = 0.5f;

            obj.AddComponent<GraphicRaycaster>();

            return obj;
        }

        private static void CreateBackgroundImage()
        {
            GameObject bg = CreateUIObject("BackgroundImage", canvasObj);
            Image img = bg.AddComponent<Image>();
            img.color = new Color(0.03f, 0.03f, 0.05f, 1f);
            img.raycastTarget = false;
            StretchFull(bg);
        }

        private static void CreateMainMenuPanel()
        {
            // Main panel container with CanvasGroup for fade
            GameObject panel = CreateUIObject("MainMenuPanel", canvasObj);
            mainMenuCanvasGroup = panel.AddComponent<CanvasGroup>();
            StretchFull(panel);

            // Title
            GameObject titleObj = CreateUIObject("TitleText", panel);
            TextMeshProUGUI title = titleObj.AddComponent<TextMeshProUGUI>();
            title.text = "BENEATH THE FLOOR";
            title.font = defaultFont;
            title.fontSize = 72;
            title.fontStyle = FontStyles.Bold;
            title.color = TEXT_COLOR;
            title.alignment = TextAlignmentOptions.Left;
            title.raycastTarget = false;

            RectTransform titleRect = titleObj.GetComponent<RectTransform>();
            titleRect.anchorMin = new Vector2(0, 1);
            titleRect.anchorMax = new Vector2(0.5f, 1);
            titleRect.pivot = new Vector2(0, 1);
            titleRect.anchoredPosition = new Vector2(100, -80);
            titleRect.sizeDelta = new Vector2(800, 100);

            // Subtitle
            GameObject subObj = CreateUIObject("SubtitleText", panel);
            TextMeshProUGUI sub = subObj.AddComponent<TextMeshProUGUI>();
            sub.text = "A Mining Adventure";
            sub.font = defaultFont;
            sub.fontSize = 24;
            sub.color = TEXT_HINT;
            sub.alignment = TextAlignmentOptions.Left;
            sub.raycastTarget = false;

            RectTransform subRect = subObj.GetComponent<RectTransform>();
            subRect.anchorMin = new Vector2(0, 1);
            subRect.anchorMax = new Vector2(0.5f, 1);
            subRect.pivot = new Vector2(0, 1);
            subRect.anchoredPosition = new Vector2(105, -170);
            subRect.sizeDelta = new Vector2(400, 40);

            // Button container
            GameObject btnContainer = CreateUIObject("ButtonContainer", panel);
            RectTransform btnContRect = btnContainer.GetComponent<RectTransform>();
            btnContRect.anchorMin = new Vector2(0, 0.5f);
            btnContRect.anchorMax = new Vector2(0, 0.5f);
            btnContRect.pivot = new Vector2(0, 0.5f);
            btnContRect.anchoredPosition = new Vector2(100, -50);
            btnContRect.sizeDelta = new Vector2(350, 400);

            VerticalLayoutGroup vlg = btnContainer.AddComponent<VerticalLayoutGroup>();
            vlg.spacing = 15;
            vlg.childAlignment = TextAnchor.UpperLeft;
            vlg.childControlWidth = true;
            vlg.childControlHeight = false;
            vlg.childForceExpandWidth = true;
            vlg.childForceExpandHeight = false;

            // Buttons
            continueBtn = CreateMenuButton("ContinueButton", btnContainer, "CONTINUE");
            newGameBtn = CreateMenuButton("NewGameButton", btnContainer, "NEW GAME");
            loadGameBtn = CreateMenuButton("LoadGameButton", btnContainer, "LOAD GAME");
            settingsBtn = CreateMenuButton("SettingsButton", btnContainer, "SETTINGS");
            quitBtn = CreateMenuButton("QuitButton", btnContainer, "QUIT");

            // Continue hint text
            GameObject hintObj = CreateUIObject("ContinueHintText", continueBtn.gameObject);
            continueHintText = hintObj.AddComponent<TextMeshProUGUI>();
            continueHintText.text = "No save found";
            continueHintText.font = defaultFont;
            continueHintText.fontSize = 14;
            continueHintText.color = TEXT_HINT;
            continueHintText.alignment = TextAlignmentOptions.Left;
            continueHintText.raycastTarget = false;

            RectTransform hintRect = hintObj.GetComponent<RectTransform>();
            hintRect.anchorMin = new Vector2(1, 0.5f);
            hintRect.anchorMax = new Vector2(1, 0.5f);
            hintRect.pivot = new Vector2(0, 0.5f);
            hintRect.anchoredPosition = new Vector2(15, 0);
            hintRect.sizeDelta = new Vector2(150, 30);
        }

        private static Button CreateMenuButton(string name, GameObject parent, string text)
        {
            GameObject btnObj = CreateUIObject(name, parent);

            Image btnImg = btnObj.AddComponent<Image>();
            btnImg.color = BUTTON_NORMAL;

            Button btn = btnObj.AddComponent<Button>();
            ColorBlock colors = btn.colors;
            colors.normalColor = BUTTON_NORMAL;
            colors.highlightedColor = BUTTON_HIGHLIGHT;
            colors.pressedColor = BUTTON_PRESSED;
            colors.selectedColor = BUTTON_HIGHLIGHT;
            colors.disabledColor = new Color(0.1f, 0.1f, 0.1f, 0.5f);
            btn.colors = colors;

            LayoutElement le = btnObj.AddComponent<LayoutElement>();
            le.preferredHeight = 55;

            // Button text
            GameObject txtObj = CreateUIObject("Text", btnObj);
            TextMeshProUGUI tmp = txtObj.AddComponent<TextMeshProUGUI>();
            tmp.text = text;
            tmp.font = defaultFont;
            tmp.fontSize = 24;
            tmp.fontStyle = FontStyles.Bold;
            tmp.color = TEXT_COLOR;
            tmp.alignment = TextAlignmentOptions.Left;
            tmp.raycastTarget = false;

            RectTransform txtRect = txtObj.GetComponent<RectTransform>();
            txtRect.anchorMin = Vector2.zero;
            txtRect.anchorMax = Vector2.one;
            txtRect.offsetMin = new Vector2(25, 0);
            txtRect.offsetMax = new Vector2(-10, 0);

            return btn;
        }

        private static void CreateLoadPanel()
        {
            loadPanel = CreatePanelBase("LoadPanel", "LOAD GAME");

            GameObject content = loadPanel.transform.Find("Panel/Content").gameObject;

            // Status text
            GameObject statusObj = CreateUIObject("StatusText", content);
            loadPanelStatusText = statusObj.AddComponent<TextMeshProUGUI>();
            loadPanelStatusText.text = "Save File Found";
            loadPanelStatusText.font = defaultFont;
            loadPanelStatusText.fontSize = 28;
            loadPanelStatusText.fontStyle = FontStyles.Bold;
            loadPanelStatusText.color = TEXT_COLOR;
            loadPanelStatusText.alignment = TextAlignmentOptions.Center;

            LayoutElement statusLE = statusObj.AddComponent<LayoutElement>();
            statusLE.preferredHeight = 50;

            // Info text
            GameObject infoObj = CreateUIObject("InfoText", content);
            loadPanelSaveInfoText = infoObj.AddComponent<TextMeshProUGUI>();
            loadPanelSaveInfoText.text = "Click Load to continue your adventure.";
            loadPanelSaveInfoText.font = defaultFont;
            loadPanelSaveInfoText.fontSize = 18;
            loadPanelSaveInfoText.color = TEXT_HINT;
            loadPanelSaveInfoText.alignment = TextAlignmentOptions.Center;

            LayoutElement infoLE = infoObj.AddComponent<LayoutElement>();
            infoLE.preferredHeight = 40;

            // Spacer
            GameObject spacer = CreateUIObject("Spacer", content);
            LayoutElement spacerLE = spacer.AddComponent<LayoutElement>();
            spacerLE.preferredHeight = 30;

            // Buttons
            GameObject btnRow = CreateUIObject("ButtonRow", content);
            HorizontalLayoutGroup hlg = btnRow.AddComponent<HorizontalLayoutGroup>();
            hlg.spacing = 20;
            hlg.childAlignment = TextAnchor.MiddleCenter;
            hlg.childControlWidth = false;
            hlg.childControlHeight = false;

            LayoutElement rowLE = btnRow.AddComponent<LayoutElement>();
            rowLE.preferredHeight = 50;

            loadPanelLoadBtn = CreatePanelButton("LoadButton", btnRow, "Load", 150);
            loadPanelDeleteBtn = CreatePanelButton("DeleteButton", btnRow, "Delete", 150);
            loadPanelBackBtn = CreatePanelButton("BackButton", btnRow, "Back", 150);

            loadPanel.SetActive(false);
        }

        private static void CreateSettingsPanel()
        {
            settingsPanel = CreatePanelBase("SettingsPanel", "SETTINGS", 600, 550);

            GameObject content = settingsPanel.transform.Find("Panel/Content").gameObject;

            // Audio header
            CreateSectionHeader("AudioHeader", content, "AUDIO");

            masterSlider = CreateSliderRow("MasterVolume", content, "Master Volume");
            musicSlider = CreateSliderRow("MusicVolume", content, "Music Volume");
            sfxSlider = CreateSliderRow("SFXVolume", content, "SFX Volume");

            // Graphics header
            CreateSectionHeader("GraphicsHeader", content, "GRAPHICS");

            fullscreenToggle = CreateToggleRow("FullscreenToggle", content, "Fullscreen");
            resDropdown = CreateDropdownRow("ResolutionDropdown", content, "Resolution");
            qualityDropdown = CreateDropdownRow("QualityDropdown", content, "Quality");

            // Spacer
            GameObject spacer = CreateUIObject("Spacer", content);
            LayoutElement spacerLE = spacer.AddComponent<LayoutElement>();
            spacerLE.flexibleHeight = 1;

            // Back button
            GameObject btnRow = CreateUIObject("ButtonRow", content);
            HorizontalLayoutGroup hlg = btnRow.AddComponent<HorizontalLayoutGroup>();
            hlg.childAlignment = TextAnchor.MiddleCenter;
            LayoutElement rowLE = btnRow.AddComponent<LayoutElement>();
            rowLE.preferredHeight = 50;

            settingsBackBtn = CreatePanelButton("BackButton", btnRow, "Back", 200);

            settingsPanel.SetActive(false);
        }

        private static void CreateConfirmDialog()
        {
            confirmDialog = CreateDialogBase("ConfirmDialog", 450, 220);

            GameObject content = confirmDialog.transform.Find("Panel/Content").gameObject;

            // Message text
            GameObject msgObj = CreateUIObject("MessageText", content);
            confirmDialogText = msgObj.AddComponent<TextMeshProUGUI>();
            confirmDialogText.text = "Are you sure?";
            confirmDialogText.font = defaultFont;
            confirmDialogText.fontSize = 22;
            confirmDialogText.color = TEXT_COLOR;
            confirmDialogText.alignment = TextAlignmentOptions.Center;

            LayoutElement msgLE = msgObj.AddComponent<LayoutElement>();
            msgLE.preferredHeight = 80;
            msgLE.flexibleWidth = 1;

            // Buttons
            GameObject btnRow = CreateUIObject("ButtonRow", content);
            HorizontalLayoutGroup hlg = btnRow.AddComponent<HorizontalLayoutGroup>();
            hlg.spacing = 30;
            hlg.childAlignment = TextAnchor.MiddleCenter;
            hlg.childControlWidth = false;

            LayoutElement rowLE = btnRow.AddComponent<LayoutElement>();
            rowLE.preferredHeight = 50;

            confirmCancelBtn = CreatePanelButton("CancelButton", btnRow, "Cancel", 140);
            confirmConfirmBtn = CreatePanelButton("ConfirmButton", btnRow, "Confirm", 140);

            // Get confirm button text for dynamic updates
            confirmDialogConfirmBtnText = confirmConfirmBtn.GetComponentInChildren<TextMeshProUGUI>();

            confirmDialog.SetActive(false);
        }

        private static void CreateDeleteConfirmDialog()
        {
            deleteConfirmDialog = CreateDialogBase("DeleteConfirmDialog", 450, 220);

            GameObject content = deleteConfirmDialog.transform.Find("Panel/Content").gameObject;

            // Message text
            GameObject msgObj = CreateUIObject("MessageText", content);
            deleteConfirmText = msgObj.AddComponent<TextMeshProUGUI>();
            deleteConfirmText.text = "Delete your save file?\nThis cannot be undone.";
            deleteConfirmText.font = defaultFont;
            deleteConfirmText.fontSize = 22;
            deleteConfirmText.color = TEXT_COLOR;
            deleteConfirmText.alignment = TextAlignmentOptions.Center;

            LayoutElement msgLE = msgObj.AddComponent<LayoutElement>();
            msgLE.preferredHeight = 80;

            // Buttons
            GameObject btnRow = CreateUIObject("ButtonRow", content);
            HorizontalLayoutGroup hlg = btnRow.AddComponent<HorizontalLayoutGroup>();
            hlg.spacing = 30;
            hlg.childAlignment = TextAnchor.MiddleCenter;
            hlg.childControlWidth = false;

            LayoutElement rowLE = btnRow.AddComponent<LayoutElement>();
            rowLE.preferredHeight = 50;

            deleteCancelBtn = CreatePanelButton("CancelButton", btnRow, "Cancel", 140);
            deleteConfirmBtn = CreatePanelButton("DeleteButton", btnRow, "Delete", 140);

            deleteConfirmDialog.SetActive(false);
        }

        private static void CreateFadeOverlay()
        {
            GameObject fadeObj = CreateUIObject("FadeOverlay", canvasObj);
            fadeOverlay = fadeObj.AddComponent<Image>();
            fadeOverlay.color = new Color(0, 0, 0, 0);
            fadeOverlay.raycastTarget = false;
            StretchFull(fadeObj);
            fadeObj.transform.SetAsLastSibling();
        }

        #region Panel Helpers

        private static GameObject CreatePanelBase(string name, string title, float width = 500, float height = 350)
        {
            // Dimmed background
            GameObject panelRoot = CreateUIObject(name, canvasObj);
            Image dimBg = panelRoot.AddComponent<Image>();
            dimBg.color = new Color(0, 0, 0, 0.7f);
            StretchFull(panelRoot);

            // Panel
            GameObject panel = CreateUIObject("Panel", panelRoot);
            Image panelImg = panel.AddComponent<Image>();
            panelImg.color = PANEL_BG;

            RectTransform panelRect = panel.GetComponent<RectTransform>();
            panelRect.anchorMin = new Vector2(0.5f, 0.5f);
            panelRect.anchorMax = new Vector2(0.5f, 0.5f);
            panelRect.sizeDelta = new Vector2(width, height);

            // Vertical layout for panel
            VerticalLayoutGroup vlg = panel.AddComponent<VerticalLayoutGroup>();
            vlg.padding = new RectOffset(30, 30, 20, 20);
            vlg.spacing = 10;
            vlg.childAlignment = TextAnchor.UpperCenter;
            vlg.childControlWidth = true;
            vlg.childControlHeight = false;
            vlg.childForceExpandWidth = true;
            vlg.childForceExpandHeight = false;

            // Title
            GameObject titleObj = CreateUIObject("Title", panel);
            TextMeshProUGUI titleTmp = titleObj.AddComponent<TextMeshProUGUI>();
            titleTmp.text = title;
            titleTmp.font = defaultFont;
            titleTmp.fontSize = 32;
            titleTmp.fontStyle = FontStyles.Bold;
            titleTmp.color = TEXT_COLOR;
            titleTmp.alignment = TextAlignmentOptions.Center;

            LayoutElement titleLE = titleObj.AddComponent<LayoutElement>();
            titleLE.preferredHeight = 50;

            // Content container
            GameObject content = CreateUIObject("Content", panel);
            VerticalLayoutGroup contentVlg = content.AddComponent<VerticalLayoutGroup>();
            contentVlg.spacing = 8;
            contentVlg.childAlignment = TextAnchor.UpperCenter;
            contentVlg.childControlWidth = true;
            contentVlg.childControlHeight = false;
            contentVlg.childForceExpandWidth = true;

            LayoutElement contentLE = content.AddComponent<LayoutElement>();
            contentLE.flexibleHeight = 1;

            return panelRoot;
        }

        private static GameObject CreateDialogBase(string name, float width, float height)
        {
            GameObject root = CreateUIObject(name, canvasObj);
            Image dimBg = root.AddComponent<Image>();
            dimBg.color = new Color(0, 0, 0, 0.8f);
            StretchFull(root);

            GameObject panel = CreateUIObject("Panel", root);
            Image panelImg = panel.AddComponent<Image>();
            panelImg.color = PANEL_BG;

            RectTransform panelRect = panel.GetComponent<RectTransform>();
            panelRect.anchorMin = new Vector2(0.5f, 0.5f);
            panelRect.anchorMax = new Vector2(0.5f, 0.5f);
            panelRect.sizeDelta = new Vector2(width, height);

            VerticalLayoutGroup vlg = panel.AddComponent<VerticalLayoutGroup>();
            vlg.padding = new RectOffset(30, 30, 30, 30);
            vlg.spacing = 20;
            vlg.childAlignment = TextAnchor.MiddleCenter;
            vlg.childControlWidth = true;
            vlg.childControlHeight = false;

            GameObject content = CreateUIObject("Content", panel);
            VerticalLayoutGroup contentVlg = content.AddComponent<VerticalLayoutGroup>();
            contentVlg.spacing = 15;
            contentVlg.childAlignment = TextAnchor.MiddleCenter;
            contentVlg.childControlWidth = true;
            contentVlg.childControlHeight = false;

            LayoutElement le = content.AddComponent<LayoutElement>();
            le.flexibleHeight = 1;
            le.flexibleWidth = 1;

            return root;
        }

        private static Button CreatePanelButton(string name, GameObject parent, string text, float width)
        {
            GameObject btnObj = CreateUIObject(name, parent);
            Image btnImg = btnObj.AddComponent<Image>();
            btnImg.color = BUTTON_NORMAL;

            Button btn = btnObj.AddComponent<Button>();
            ColorBlock colors = btn.colors;
            colors.normalColor = BUTTON_NORMAL;
            colors.highlightedColor = BUTTON_HIGHLIGHT;
            colors.pressedColor = BUTTON_PRESSED;
            colors.disabledColor = new Color(0.1f, 0.1f, 0.1f, 0.5f);
            btn.colors = colors;

            LayoutElement le = btnObj.AddComponent<LayoutElement>();
            le.preferredWidth = width;
            le.preferredHeight = 45;

            GameObject txtObj = CreateUIObject("Text", btnObj);
            TextMeshProUGUI tmp = txtObj.AddComponent<TextMeshProUGUI>();
            tmp.text = text;
            tmp.font = defaultFont;
            tmp.fontSize = 20;
            tmp.color = TEXT_COLOR;
            tmp.alignment = TextAlignmentOptions.Center;
            tmp.raycastTarget = false;
            StretchFull(txtObj);

            return btn;
        }

        private static void CreateSectionHeader(string name, GameObject parent, string text)
        {
            GameObject obj = CreateUIObject(name, parent);
            TextMeshProUGUI tmp = obj.AddComponent<TextMeshProUGUI>();
            tmp.text = text;
            tmp.font = defaultFont;
            tmp.fontSize = 18;
            tmp.fontStyle = FontStyles.Bold;
            tmp.color = ACCENT_COLOR;
            tmp.alignment = TextAlignmentOptions.Left;

            LayoutElement le = obj.AddComponent<LayoutElement>();
            le.preferredHeight = 35;
        }

        private static Slider CreateSliderRow(string name, GameObject parent, string label)
        {
            GameObject row = CreateUIObject(name + "Row", parent);
            HorizontalLayoutGroup hlg = row.AddComponent<HorizontalLayoutGroup>();
            hlg.spacing = 15;
            hlg.childAlignment = TextAnchor.MiddleLeft;
            hlg.childControlWidth = false;
            hlg.childControlHeight = true;
            hlg.childForceExpandWidth = false;

            LayoutElement rowLE = row.AddComponent<LayoutElement>();
            rowLE.preferredHeight = 35;

            // Label
            GameObject lblObj = CreateUIObject("Label", row);
            TextMeshProUGUI lbl = lblObj.AddComponent<TextMeshProUGUI>();
            lbl.text = label;
            lbl.font = defaultFont;
            lbl.fontSize = 18;
            lbl.color = TEXT_COLOR;
            lbl.alignment = TextAlignmentOptions.Left;

            LayoutElement lblLE = lblObj.AddComponent<LayoutElement>();
            lblLE.preferredWidth = 180;

            // Slider
            GameObject sliderObj = CreateUIObject("Slider", row);
            Slider slider = sliderObj.AddComponent<Slider>();
            slider.minValue = 0;
            slider.maxValue = 1;
            slider.value = 1;

            LayoutElement sliderLE = sliderObj.AddComponent<LayoutElement>();
            sliderLE.preferredWidth = 300;
            sliderLE.preferredHeight = 20;

            // Slider background
            GameObject bgObj = CreateUIObject("Background", sliderObj);
            Image bgImg = bgObj.AddComponent<Image>();
            bgImg.color = new Color(0.2f, 0.2f, 0.2f, 1f);
            StretchFull(bgObj);

            // Fill area
            GameObject fillArea = CreateUIObject("Fill Area", sliderObj);
            RectTransform fillAreaRect = fillArea.GetComponent<RectTransform>();
            fillAreaRect.anchorMin = Vector2.zero;
            fillAreaRect.anchorMax = Vector2.one;
            fillAreaRect.offsetMin = new Vector2(5, 0);
            fillAreaRect.offsetMax = new Vector2(-5, 0);

            GameObject fill = CreateUIObject("Fill", fillArea);
            Image fillImg = fill.AddComponent<Image>();
            fillImg.color = ACCENT_COLOR;
            RectTransform fillRect = fill.GetComponent<RectTransform>();
            fillRect.anchorMin = Vector2.zero;
            fillRect.anchorMax = new Vector2(0, 1);
            fillRect.sizeDelta = Vector2.zero;

            // Handle area
            GameObject handleArea = CreateUIObject("Handle Slide Area", sliderObj);
            RectTransform handleAreaRect = handleArea.GetComponent<RectTransform>();
            handleAreaRect.anchorMin = Vector2.zero;
            handleAreaRect.anchorMax = Vector2.one;
            handleAreaRect.offsetMin = new Vector2(10, 0);
            handleAreaRect.offsetMax = new Vector2(-10, 0);

            GameObject handle = CreateUIObject("Handle", handleArea);
            Image handleImg = handle.AddComponent<Image>();
            handleImg.color = Color.white;
            RectTransform handleRect = handle.GetComponent<RectTransform>();
            handleRect.sizeDelta = new Vector2(20, 0);

            slider.fillRect = fillRect;
            slider.handleRect = handleRect;
            slider.targetGraphic = handleImg;

            return slider;
        }

        private static Toggle CreateToggleRow(string name, GameObject parent, string label)
        {
            GameObject row = CreateUIObject(name + "Row", parent);
            HorizontalLayoutGroup hlg = row.AddComponent<HorizontalLayoutGroup>();
            hlg.spacing = 15;
            hlg.childAlignment = TextAnchor.MiddleLeft;
            hlg.childControlWidth = false;

            LayoutElement rowLE = row.AddComponent<LayoutElement>();
            rowLE.preferredHeight = 35;

            // Label
            GameObject lblObj = CreateUIObject("Label", row);
            TextMeshProUGUI lbl = lblObj.AddComponent<TextMeshProUGUI>();
            lbl.text = label;
            lbl.font = defaultFont;
            lbl.fontSize = 18;
            lbl.color = TEXT_COLOR;

            LayoutElement lblLE = lblObj.AddComponent<LayoutElement>();
            lblLE.preferredWidth = 180;

            // Toggle
            GameObject toggleObj = CreateUIObject("Toggle", row);
            Toggle toggle = toggleObj.AddComponent<Toggle>();

            LayoutElement toggleLE = toggleObj.AddComponent<LayoutElement>();
            toggleLE.preferredWidth = 30;
            toggleLE.preferredHeight = 30;

            // Background
            GameObject bgObj = CreateUIObject("Background", toggleObj);
            Image bgImg = bgObj.AddComponent<Image>();
            bgImg.color = new Color(0.2f, 0.2f, 0.2f, 1f);
            StretchFull(bgObj);

            // Checkmark
            GameObject checkObj = CreateUIObject("Checkmark", bgObj);
            Image checkImg = checkObj.AddComponent<Image>();
            checkImg.color = ACCENT_COLOR;
            RectTransform checkRect = checkObj.GetComponent<RectTransform>();
            checkRect.anchorMin = new Vector2(0.15f, 0.15f);
            checkRect.anchorMax = new Vector2(0.85f, 0.85f);
            checkRect.offsetMin = Vector2.zero;
            checkRect.offsetMax = Vector2.zero;

            toggle.targetGraphic = bgImg;
            toggle.graphic = checkImg;
            toggle.isOn = true;

            return toggle;
        }

        private static TMP_Dropdown CreateDropdownRow(string name, GameObject parent, string label)
        {
            GameObject row = CreateUIObject(name + "Row", parent);
            HorizontalLayoutGroup hlg = row.AddComponent<HorizontalLayoutGroup>();
            hlg.spacing = 15;
            hlg.childAlignment = TextAnchor.MiddleLeft;
            hlg.childControlWidth = false;

            LayoutElement rowLE = row.AddComponent<LayoutElement>();
            rowLE.preferredHeight = 35;

            // Label
            GameObject lblObj = CreateUIObject("Label", row);
            TextMeshProUGUI lbl = lblObj.AddComponent<TextMeshProUGUI>();
            lbl.text = label;
            lbl.font = defaultFont;
            lbl.fontSize = 18;
            lbl.color = TEXT_COLOR;

            LayoutElement lblLE = lblObj.AddComponent<LayoutElement>();
            lblLE.preferredWidth = 180;

            // Dropdown
            GameObject ddObj = CreateUIObject("Dropdown", row);
            Image ddImg = ddObj.AddComponent<Image>();
            ddImg.color = BUTTON_NORMAL;

            TMP_Dropdown dropdown = ddObj.AddComponent<TMP_Dropdown>();

            LayoutElement ddLE = ddObj.AddComponent<LayoutElement>();
            ddLE.preferredWidth = 300;
            ddLE.preferredHeight = 35;

            // Caption text
            GameObject captionObj = CreateUIObject("Label", ddObj);
            TextMeshProUGUI captionTmp = captionObj.AddComponent<TextMeshProUGUI>();
            captionTmp.font = defaultFont;
            captionTmp.fontSize = 16;
            captionTmp.color = TEXT_COLOR;
            captionTmp.alignment = TextAlignmentOptions.Left;

            RectTransform captionRect = captionObj.GetComponent<RectTransform>();
            captionRect.anchorMin = Vector2.zero;
            captionRect.anchorMax = Vector2.one;
            captionRect.offsetMin = new Vector2(10, 0);
            captionRect.offsetMax = new Vector2(-30, 0);

            // Arrow
            GameObject arrowObj = CreateUIObject("Arrow", ddObj);
            Image arrowImg = arrowObj.AddComponent<Image>();
            arrowImg.color = TEXT_COLOR;

            RectTransform arrowRect = arrowObj.GetComponent<RectTransform>();
            arrowRect.anchorMin = new Vector2(1, 0.5f);
            arrowRect.anchorMax = new Vector2(1, 0.5f);
            arrowRect.pivot = new Vector2(1, 0.5f);
            arrowRect.anchoredPosition = new Vector2(-10, 0);
            arrowRect.sizeDelta = new Vector2(15, 15);

            // Template
            GameObject template = CreateUIObject("Template", ddObj);
            Image templateImg = template.AddComponent<Image>();
            templateImg.color = PANEL_BG;

            ScrollRect scroll = template.AddComponent<ScrollRect>();

            RectTransform templateRect = template.GetComponent<RectTransform>();
            templateRect.anchorMin = new Vector2(0, 0);
            templateRect.anchorMax = new Vector2(1, 0);
            templateRect.pivot = new Vector2(0.5f, 1);
            templateRect.anchoredPosition = Vector2.zero;
            templateRect.sizeDelta = new Vector2(0, 150);

            // Viewport
            GameObject viewport = CreateUIObject("Viewport", template);
            Image vpImg = viewport.AddComponent<Image>();
            vpImg.color = Color.white;
            Mask mask = viewport.AddComponent<Mask>();
            mask.showMaskGraphic = false;

            RectTransform vpRect = viewport.GetComponent<RectTransform>();
            vpRect.anchorMin = Vector2.zero;
            vpRect.anchorMax = Vector2.one;
            vpRect.offsetMin = Vector2.zero;
            vpRect.offsetMax = Vector2.zero;

            // Content
            GameObject contentObj = CreateUIObject("Content", viewport);
            RectTransform contentRect = contentObj.GetComponent<RectTransform>();
            contentRect.anchorMin = new Vector2(0, 1);
            contentRect.anchorMax = new Vector2(1, 1);
            contentRect.pivot = new Vector2(0.5f, 1);
            contentRect.sizeDelta = new Vector2(0, 28);

            // Item
            GameObject item = CreateUIObject("Item", contentObj);
            Toggle itemToggle = item.AddComponent<Toggle>();

            RectTransform itemRect = item.GetComponent<RectTransform>();
            itemRect.anchorMin = new Vector2(0, 0.5f);
            itemRect.anchorMax = new Vector2(1, 0.5f);
            itemRect.sizeDelta = new Vector2(0, 28);

            // Item background
            GameObject itemBg = CreateUIObject("Item Background", item);
            Image itemBgImg = itemBg.AddComponent<Image>();
            itemBgImg.color = BUTTON_HIGHLIGHT;
            StretchFull(itemBg);

            // Item checkmark
            GameObject itemCheck = CreateUIObject("Item Checkmark", item);
            Image itemCheckImg = itemCheck.AddComponent<Image>();
            itemCheckImg.color = ACCENT_COLOR;

            RectTransform itemCheckRect = itemCheck.GetComponent<RectTransform>();
            itemCheckRect.anchorMin = new Vector2(0, 0.5f);
            itemCheckRect.anchorMax = new Vector2(0, 0.5f);
            itemCheckRect.sizeDelta = new Vector2(20, 20);
            itemCheckRect.anchoredPosition = new Vector2(15, 0);

            // Item label
            GameObject itemLabel = CreateUIObject("Item Label", item);
            TextMeshProUGUI itemTmp = itemLabel.AddComponent<TextMeshProUGUI>();
            itemTmp.font = defaultFont;
            itemTmp.fontSize = 16;
            itemTmp.color = TEXT_COLOR;

            RectTransform itemLabelRect = itemLabel.GetComponent<RectTransform>();
            itemLabelRect.anchorMin = Vector2.zero;
            itemLabelRect.anchorMax = Vector2.one;
            itemLabelRect.offsetMin = new Vector2(35, 0);
            itemLabelRect.offsetMax = new Vector2(-10, 0);

            itemToggle.targetGraphic = itemBgImg;
            itemToggle.graphic = itemCheckImg;
            itemToggle.isOn = true;

            scroll.content = contentRect;
            scroll.viewport = vpRect;

            dropdown.captionText = captionTmp;
            dropdown.itemText = itemTmp;
            dropdown.template = templateRect;

            template.SetActive(false);

            return dropdown;
        }

        #endregion

        #region Utility

        private static GameObject CreateUIObject(string name, GameObject parent)
        {
            GameObject obj = new GameObject(name, typeof(RectTransform));
            obj.transform.SetParent(parent.transform, false);
            return obj;
        }

        private static void StretchFull(GameObject obj)
        {
            RectTransform rect = obj.GetComponent<RectTransform>();
            rect.anchorMin = Vector2.zero;
            rect.anchorMax = Vector2.one;
            rect.offsetMin = Vector2.zero;
            rect.offsetMax = Vector2.zero;
        }

        #endregion

        private static void WireController()
        {
            System.Type controllerType = null;
            foreach (var asm in System.AppDomain.CurrentDomain.GetAssemblies())
            {
                controllerType = asm.GetType(CONTROLLER_SCRIPT);
                if (controllerType != null) break;
            }

            if (controllerType == null)
            {
                Debug.LogError("[MainMenuSceneSetup] Controller script not found!");
                return;
            }

            Component controller = canvasObj.AddComponent(controllerType);
            SerializedObject so = new SerializedObject(controller);

            // Main menu
            SetProperty(so, "mainMenuCanvasGroup", mainMenuCanvasGroup);
            SetProperty(so, "continueButton", continueBtn);
            SetProperty(so, "newGameButton", newGameBtn);
            SetProperty(so, "loadGameButton", loadGameBtn);
            SetProperty(so, "settingsButton", settingsBtn);
            SetProperty(so, "quitButton", quitBtn);
            SetProperty(so, "continueHintText", continueHintText);

            // Load panel
            SetProperty(so, "loadPanel", loadPanel);
            SetProperty(so, "loadPanelStatusText", loadPanelStatusText);
            SetProperty(so, "loadPanelSaveInfoText", loadPanelSaveInfoText);
            SetProperty(so, "loadPanelLoadButton", loadPanelLoadBtn);
            SetProperty(so, "loadPanelDeleteButton", loadPanelDeleteBtn);
            SetProperty(so, "loadPanelBackButton", loadPanelBackBtn);

            // Settings panel
            SetProperty(so, "settingsPanel", settingsPanel);
            SetProperty(so, "masterVolumeSlider", masterSlider);
            SetProperty(so, "musicVolumeSlider", musicSlider);
            SetProperty(so, "sfxVolumeSlider", sfxSlider);
            SetProperty(so, "fullscreenToggle", fullscreenToggle);
            SetProperty(so, "resolutionDropdown", resDropdown);
            SetProperty(so, "qualityDropdown", qualityDropdown);
            SetProperty(so, "settingsBackButton", settingsBackBtn);

            // Confirm dialog
            SetProperty(so, "confirmDialog", confirmDialog);
            SetProperty(so, "confirmDialogText", confirmDialogText);
            SetProperty(so, "confirmDialogCancelButton", confirmCancelBtn);
            SetProperty(so, "confirmDialogConfirmButton", confirmConfirmBtn);
            SetProperty(so, "confirmDialogConfirmButtonText", confirmDialogConfirmBtnText);

            // Delete confirm dialog
            SetProperty(so, "deleteConfirmDialog", deleteConfirmDialog);
            SetProperty(so, "deleteConfirmText", deleteConfirmText);
            SetProperty(so, "deleteConfirmCancelButton", deleteCancelBtn);
            SetProperty(so, "deleteConfirmDeleteButton", deleteConfirmBtn);

            // Fade overlay
            SetProperty(so, "fadeOverlay", fadeOverlay);

            so.ApplyModifiedPropertiesWithoutUndo();
        }

        private static void SetProperty(SerializedObject so, string name, Object value)
        {
            var prop = so.FindProperty(name);
            if (prop != null) prop.objectReferenceValue = value;
        }

        private static void AddSceneToBuildSettings(string path)
        {
            var scenes = new List<EditorBuildSettingsScene>(EditorBuildSettings.scenes);
            bool exists = false;

            foreach (var s in scenes)
            {
                if (s.path == path) { exists = true; s.enabled = true; break; }
            }

            if (!exists)
            {
                // Add after PressAnyKeyScene (index 1)
                int insertIndex = Mathf.Min(1, scenes.Count);
                scenes.Insert(insertIndex, new EditorBuildSettingsScene(path, true));
            }

            EditorBuildSettings.scenes = scenes.ToArray();
        }
    }
}
