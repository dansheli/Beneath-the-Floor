using UnityEngine;
using UnityEngine.UI;
using UnityEngine.SceneManagement;
using UnityEditor;
using UnityEditor.SceneManagement;
using TMPro;
using BeneathTheFloor.Save;

namespace BeneathTheFloor.Editor
{
    /// <summary>
    /// Editor utility to create the Pause Menu prefab and add it to HouseBuilding scene.
    /// </summary>
    public static class PauseMenuSetup
    {
        private static readonly Color darkOverlay = new Color(0f, 0f, 0f, 0.85f);
        private static readonly Color panelColor = new Color(0.12f, 0.12f, 0.15f, 0.95f);
        private static readonly Color buttonNormal = new Color(0.2f, 0.2f, 0.25f, 1f);
        private static readonly Color buttonHighlight = new Color(0.3f, 0.3f, 0.35f, 1f);
        private static readonly Color accentColor = new Color(0.4f, 0.7f, 0.9f, 1f);
        private static readonly Color dangerColor = new Color(0.9f, 0.3f, 0.3f, 1f);

        [MenuItem("Tools/Beneath The Floor/Create Pause Menu in HouseBuilding")]
        public static void CreatePauseMenuInScene()
        {
            // Open HouseBuilding scene
            string scenePath = "Assets/Scenes/HouseBuilding.unity";
            Scene scene = EditorSceneManager.OpenScene(scenePath, OpenSceneMode.Single);

            if (!scene.IsValid())
            {
                Debug.LogError($"Could not open scene at {scenePath}");
                return;
            }

            // Check if PauseMenu already exists
            var existingMenu = GameObject.Find("PauseMenu");
            if (existingMenu != null)
            {
                Debug.LogWarning("PauseMenu already exists in scene. Deleting and recreating...");
                Object.DestroyImmediate(existingMenu);
            }

            // Ensure SaveManager exists in scene
            EnsureSaveManager();

            // Create the pause menu
            GameObject pauseMenu = CreatePauseMenu();

            // Save scene
            EditorSceneManager.SaveScene(scene);

            Debug.Log("Pause Menu created and added to HouseBuilding scene!");
            Selection.activeGameObject = pauseMenu;
        }

        private static void EnsureSaveManager()
        {
            // Check if SaveManager already exists
            var existingSaveManager = Object.FindObjectOfType<SaveManager>();
            if (existingSaveManager != null)
            {
                Debug.Log("SaveManager already exists in scene.");
                return;
            }

            // Create SaveManager GameObject
            GameObject saveManagerObj = new GameObject("SaveManager");
            saveManagerObj.AddComponent<SaveManager>();
            Debug.Log("SaveManager added to scene.");
        }

        [MenuItem("Tools/Beneath The Floor/Create Pause Menu Prefab Only")]
        public static void CreatePauseMenuPrefab()
        {
            GameObject pauseMenu = CreatePauseMenu();

            // Ensure prefab directory exists
            if (!AssetDatabase.IsValidFolder("Assets/Prefabs"))
                AssetDatabase.CreateFolder("Assets", "Prefabs");
            if (!AssetDatabase.IsValidFolder("Assets/Prefabs/UI"))
                AssetDatabase.CreateFolder("Assets/Prefabs", "UI");

            // Save as prefab
            string prefabPath = "Assets/Prefabs/UI/PauseMenu.prefab";
            PrefabUtility.SaveAsPrefabAsset(pauseMenu, prefabPath);

            Debug.Log($"Pause Menu prefab created at {prefabPath}");
            Selection.activeGameObject = pauseMenu;
        }

        private static GameObject CreatePauseMenu()
        {
            // Root object
            GameObject root = new GameObject("PauseMenu");
            var controller = root.AddComponent<GameFlow.PauseMenuController>();

            // Canvas
            GameObject canvasObj = new GameObject("Canvas");
            canvasObj.transform.SetParent(root.transform);
            Canvas canvas = canvasObj.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = 100; // Ensure it's on top
            canvasObj.AddComponent<CanvasScaler>().uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            canvasObj.GetComponent<CanvasScaler>().referenceResolution = new Vector2(1920, 1080);
            canvasObj.AddComponent<GraphicRaycaster>();

            // Pause Menu Root (what gets shown/hidden)
            GameObject pauseMenuRoot = new GameObject("PauseMenuRoot");
            pauseMenuRoot.transform.SetParent(canvasObj.transform, false);
            RectTransform rootRect = pauseMenuRoot.AddComponent<RectTransform>();
            rootRect.anchorMin = Vector2.zero;
            rootRect.anchorMax = Vector2.one;
            rootRect.offsetMin = Vector2.zero;
            rootRect.offsetMax = Vector2.zero;

            // Dark overlay background
            GameObject overlay = CreatePanel(pauseMenuRoot.transform, "Overlay", darkOverlay);
            RectTransform overlayRect = overlay.GetComponent<RectTransform>();
            overlayRect.anchorMin = Vector2.zero;
            overlayRect.anchorMax = Vector2.one;
            overlayRect.offsetMin = Vector2.zero;
            overlayRect.offsetMax = Vector2.zero;

            // Main Panel
            GameObject mainPanel = CreateCenterPanel(pauseMenuRoot.transform, "MainPanel");

            // Settings Panel
            GameObject settingsPanel = CreateSettingsPanel(pauseMenuRoot.transform);
            settingsPanel.SetActive(false);

            // Create main panel content
            CreateMainPanelContent(mainPanel, controller, settingsPanel);

            // Wire up controller references
            var serializedController = new SerializedObject(controller);
            serializedController.FindProperty("pauseMenuRoot").objectReferenceValue = pauseMenuRoot;
            serializedController.FindProperty("mainPanel").objectReferenceValue = mainPanel;
            serializedController.FindProperty("settingsPanel").objectReferenceValue = settingsPanel;
            serializedController.ApplyModifiedProperties();

            return root;
        }

        private static GameObject CreateCenterPanel(Transform parent, string name)
        {
            GameObject panel = CreatePanel(parent, name, panelColor);
            RectTransform rect = panel.GetComponent<RectTransform>();
            rect.anchorMin = new Vector2(0.5f, 0.5f);
            rect.anchorMax = new Vector2(0.5f, 0.5f);
            rect.sizeDelta = new Vector2(400, 500);
            rect.anchoredPosition = Vector2.zero;

            // Add vertical layout
            var layout = panel.AddComponent<VerticalLayoutGroup>();
            layout.padding = new RectOffset(30, 30, 40, 30);
            layout.spacing = 15;
            layout.childAlignment = TextAnchor.UpperCenter;
            layout.childControlWidth = true;
            layout.childControlHeight = false;
            layout.childForceExpandWidth = true;
            layout.childForceExpandHeight = false;

            return panel;
        }

        private static void CreateMainPanelContent(GameObject mainPanel, GameFlow.PauseMenuController controller, GameObject settingsPanel)
        {
            Transform parent = mainPanel.transform;
            var serializedController = new SerializedObject(controller);

            // Title
            CreateTitle(parent, "PAUSED");

            // Spacer
            CreateSpacer(parent, 20);

            // Resume Button
            Button resumeBtn = CreateButton(parent, "Resume", accentColor);
            serializedController.FindProperty("resumeButton").objectReferenceValue = resumeBtn;

            // Save Button
            Button saveBtn = CreateButton(parent, "Save Game", buttonNormal);
            serializedController.FindProperty("saveButton").objectReferenceValue = saveBtn;

            // Load Button
            Button loadBtn = CreateButton(parent, "Load Game", buttonNormal);
            serializedController.FindProperty("loadButton").objectReferenceValue = loadBtn;

            // Settings Button
            Button settingsBtn = CreateButton(parent, "Settings", buttonNormal);
            serializedController.FindProperty("settingsButton").objectReferenceValue = settingsBtn;

            // Spacer
            CreateSpacer(parent, 10);

            // Main Menu Button
            Button mainMenuBtn = CreateButton(parent, "Main Menu", buttonNormal);
            serializedController.FindProperty("mainMenuButton").objectReferenceValue = mainMenuBtn;

            // Quit Button
            Button quitBtn = CreateButton(parent, "Quit Game", dangerColor);
            serializedController.FindProperty("quitButton").objectReferenceValue = quitBtn;

            // Status Text
            GameObject statusObj = new GameObject("StatusText");
            statusObj.transform.SetParent(parent, false);
            TextMeshProUGUI statusText = statusObj.AddComponent<TextMeshProUGUI>();
            statusText.text = "";
            statusText.fontSize = 18;
            statusText.alignment = TextAlignmentOptions.Center;
            statusText.color = Color.white;
            RectTransform statusRect = statusObj.GetComponent<RectTransform>();
            statusRect.sizeDelta = new Vector2(340, 30);
            serializedController.FindProperty("statusText").objectReferenceValue = statusText;

            serializedController.ApplyModifiedProperties();
        }

        private static GameObject CreateSettingsPanel(Transform parent)
        {
            GameObject panel = CreateCenterPanel(parent, "SettingsPanel");
            RectTransform rect = panel.GetComponent<RectTransform>();
            rect.sizeDelta = new Vector2(450, 400);

            Transform panelTransform = panel.transform;

            // Title
            CreateTitle(panelTransform, "SETTINGS");

            // Spacer
            CreateSpacer(panelTransform, 15);

            // Master Volume
            CreateLabel(panelTransform, "Master Volume");
            GameObject volumeSliderObj = CreateSlider(panelTransform, "MasterVolumeSlider");

            // Spacer
            CreateSpacer(panelTransform, 10);

            // Quality
            CreateLabel(panelTransform, "Graphics Quality");
            GameObject qualityDropdownObj = CreateDropdown(panelTransform, "QualityDropdown");

            // Spacer
            CreateSpacer(panelTransform, 10);

            // Fullscreen Toggle
            GameObject fullscreenObj = CreateToggle(panelTransform, "Fullscreen", "FullscreenToggle");

            // Spacer
            CreateSpacer(panelTransform, 20);

            // Back Button
            Button backBtn = CreateButton(panelTransform, "Back", accentColor);
            backBtn.gameObject.name = "BackButton";

            // Store references - we'll need to wire these up
            var controller = parent.GetComponentInParent<GameFlow.PauseMenuController>();
            if (controller != null)
            {
                var serialized = new SerializedObject(controller);
                serialized.FindProperty("masterVolumeSlider").objectReferenceValue = volumeSliderObj.GetComponent<Slider>();
                serialized.FindProperty("qualityDropdown").objectReferenceValue = qualityDropdownObj.GetComponent<TMP_Dropdown>();
                serialized.FindProperty("fullscreenToggle").objectReferenceValue = fullscreenObj.GetComponentInChildren<Toggle>();
                serialized.FindProperty("settingsBackButton").objectReferenceValue = backBtn;
                serialized.ApplyModifiedProperties();
            }

            return panel;
        }

        private static GameObject CreatePanel(Transform parent, string name, Color color)
        {
            GameObject panel = new GameObject(name);
            panel.transform.SetParent(parent, false);
            Image image = panel.AddComponent<Image>();
            image.color = color;
            return panel;
        }

        private static void CreateTitle(Transform parent, string text)
        {
            GameObject titleObj = new GameObject("Title");
            titleObj.transform.SetParent(parent, false);
            TextMeshProUGUI title = titleObj.AddComponent<TextMeshProUGUI>();
            title.text = text;
            title.fontSize = 36;
            title.fontStyle = FontStyles.Bold;
            title.alignment = TextAlignmentOptions.Center;
            title.color = Color.white;
            RectTransform rect = titleObj.GetComponent<RectTransform>();
            rect.sizeDelta = new Vector2(340, 50);
        }

        private static void CreateLabel(Transform parent, string text)
        {
            GameObject labelObj = new GameObject("Label_" + text.Replace(" ", ""));
            labelObj.transform.SetParent(parent, false);
            TextMeshProUGUI label = labelObj.AddComponent<TextMeshProUGUI>();
            label.text = text;
            label.fontSize = 20;
            label.alignment = TextAlignmentOptions.Left;
            label.color = new Color(0.8f, 0.8f, 0.8f, 1f);
            RectTransform rect = labelObj.GetComponent<RectTransform>();
            rect.sizeDelta = new Vector2(340, 25);
        }

        private static void CreateSpacer(Transform parent, float height)
        {
            GameObject spacer = new GameObject("Spacer");
            spacer.transform.SetParent(parent, false);
            RectTransform rect = spacer.AddComponent<RectTransform>();
            rect.sizeDelta = new Vector2(0, height);
            var layout = spacer.AddComponent<LayoutElement>();
            layout.minHeight = height;
            layout.preferredHeight = height;
        }

        private static Button CreateButton(Transform parent, string text, Color color)
        {
            GameObject buttonObj = new GameObject(text.Replace(" ", "") + "Button");
            buttonObj.transform.SetParent(parent, false);

            Image buttonImage = buttonObj.AddComponent<Image>();
            buttonImage.color = color;

            Button button = buttonObj.AddComponent<Button>();
            ColorBlock colors = button.colors;
            colors.normalColor = color;
            colors.highlightedColor = new Color(color.r + 0.1f, color.g + 0.1f, color.b + 0.1f, color.a);
            colors.pressedColor = new Color(color.r - 0.1f, color.g - 0.1f, color.b - 0.1f, color.a);
            colors.disabledColor = new Color(color.r * 0.5f, color.g * 0.5f, color.b * 0.5f, color.a * 0.5f);
            button.colors = colors;

            RectTransform rect = buttonObj.GetComponent<RectTransform>();
            rect.sizeDelta = new Vector2(340, 45);

            // Button text
            GameObject textObj = new GameObject("Text");
            textObj.transform.SetParent(buttonObj.transform, false);
            TextMeshProUGUI buttonText = textObj.AddComponent<TextMeshProUGUI>();
            buttonText.text = text;
            buttonText.fontSize = 22;
            buttonText.alignment = TextAlignmentOptions.Center;
            buttonText.color = Color.white;
            RectTransform textRect = textObj.GetComponent<RectTransform>();
            textRect.anchorMin = Vector2.zero;
            textRect.anchorMax = Vector2.one;
            textRect.offsetMin = Vector2.zero;
            textRect.offsetMax = Vector2.zero;

            return button;
        }

        private static GameObject CreateSlider(Transform parent, string name)
        {
            GameObject sliderObj = new GameObject(name);
            sliderObj.transform.SetParent(parent, false);
            RectTransform sliderRect = sliderObj.AddComponent<RectTransform>();
            sliderRect.sizeDelta = new Vector2(340, 30);

            // Background
            GameObject background = new GameObject("Background");
            background.transform.SetParent(sliderObj.transform, false);
            Image bgImage = background.AddComponent<Image>();
            bgImage.color = new Color(0.1f, 0.1f, 0.1f, 1f);
            RectTransform bgRect = background.GetComponent<RectTransform>();
            bgRect.anchorMin = new Vector2(0, 0.25f);
            bgRect.anchorMax = new Vector2(1, 0.75f);
            bgRect.offsetMin = Vector2.zero;
            bgRect.offsetMax = Vector2.zero;

            // Fill Area
            GameObject fillArea = new GameObject("Fill Area");
            fillArea.transform.SetParent(sliderObj.transform, false);
            RectTransform fillAreaRect = fillArea.AddComponent<RectTransform>();
            fillAreaRect.anchorMin = new Vector2(0, 0.25f);
            fillAreaRect.anchorMax = new Vector2(1, 0.75f);
            fillAreaRect.offsetMin = new Vector2(5, 0);
            fillAreaRect.offsetMax = new Vector2(-5, 0);

            // Fill
            GameObject fill = new GameObject("Fill");
            fill.transform.SetParent(fillArea.transform, false);
            Image fillImage = fill.AddComponent<Image>();
            fillImage.color = accentColor;
            RectTransform fillRect = fill.GetComponent<RectTransform>();
            fillRect.anchorMin = Vector2.zero;
            fillRect.anchorMax = Vector2.one;
            fillRect.offsetMin = Vector2.zero;
            fillRect.offsetMax = Vector2.zero;

            // Handle Slide Area
            GameObject handleArea = new GameObject("Handle Slide Area");
            handleArea.transform.SetParent(sliderObj.transform, false);
            RectTransform handleAreaRect = handleArea.AddComponent<RectTransform>();
            handleAreaRect.anchorMin = Vector2.zero;
            handleAreaRect.anchorMax = Vector2.one;
            handleAreaRect.offsetMin = new Vector2(10, 0);
            handleAreaRect.offsetMax = new Vector2(-10, 0);

            // Handle
            GameObject handle = new GameObject("Handle");
            handle.transform.SetParent(handleArea.transform, false);
            Image handleImage = handle.AddComponent<Image>();
            handleImage.color = Color.white;
            RectTransform handleRect = handle.GetComponent<RectTransform>();
            handleRect.sizeDelta = new Vector2(20, 0);
            handleRect.anchorMin = new Vector2(0, 0);
            handleRect.anchorMax = new Vector2(0, 1);

            // Slider component
            Slider slider = sliderObj.AddComponent<Slider>();
            slider.fillRect = fillRect;
            slider.handleRect = handleRect;
            slider.minValue = 0f;
            slider.maxValue = 1f;
            slider.value = 1f;

            return sliderObj;
        }

        private static GameObject CreateDropdown(Transform parent, string name)
        {
            GameObject dropdownObj = new GameObject(name);
            dropdownObj.transform.SetParent(parent, false);
            RectTransform dropdownRect = dropdownObj.AddComponent<RectTransform>();
            dropdownRect.sizeDelta = new Vector2(340, 35);

            Image dropdownImage = dropdownObj.AddComponent<Image>();
            dropdownImage.color = buttonNormal;

            TMP_Dropdown dropdown = dropdownObj.AddComponent<TMP_Dropdown>();

            // Label
            GameObject label = new GameObject("Label");
            label.transform.SetParent(dropdownObj.transform, false);
            TextMeshProUGUI labelText = label.AddComponent<TextMeshProUGUI>();
            labelText.text = "Medium";
            labelText.fontSize = 18;
            labelText.alignment = TextAlignmentOptions.Left;
            labelText.color = Color.white;
            RectTransform labelRect = label.GetComponent<RectTransform>();
            labelRect.anchorMin = Vector2.zero;
            labelRect.anchorMax = Vector2.one;
            labelRect.offsetMin = new Vector2(10, 0);
            labelRect.offsetMax = new Vector2(-30, 0);

            // Arrow
            GameObject arrow = new GameObject("Arrow");
            arrow.transform.SetParent(dropdownObj.transform, false);
            TextMeshProUGUI arrowText = arrow.AddComponent<TextMeshProUGUI>();
            arrowText.text = "v";
            arrowText.fontSize = 18;
            arrowText.alignment = TextAlignmentOptions.Center;
            arrowText.color = Color.white;
            RectTransform arrowRect = arrow.GetComponent<RectTransform>();
            arrowRect.anchorMin = new Vector2(1, 0);
            arrowRect.anchorMax = new Vector2(1, 1);
            arrowRect.sizeDelta = new Vector2(25, 0);
            arrowRect.anchoredPosition = new Vector2(-15, 0);

            // Template
            GameObject template = new GameObject("Template");
            template.transform.SetParent(dropdownObj.transform, false);
            RectTransform templateRect = template.AddComponent<RectTransform>();
            templateRect.anchorMin = new Vector2(0, 0);
            templateRect.anchorMax = new Vector2(1, 0);
            templateRect.pivot = new Vector2(0.5f, 1);
            templateRect.sizeDelta = new Vector2(0, 150);
            Image templateImage = template.AddComponent<Image>();
            templateImage.color = panelColor;
            template.AddComponent<ScrollRect>();

            // Viewport
            GameObject viewport = new GameObject("Viewport");
            viewport.transform.SetParent(template.transform, false);
            RectTransform viewportRect = viewport.AddComponent<RectTransform>();
            viewportRect.anchorMin = Vector2.zero;
            viewportRect.anchorMax = Vector2.one;
            viewportRect.offsetMin = Vector2.zero;
            viewportRect.offsetMax = Vector2.zero;
            viewport.AddComponent<Mask>().showMaskGraphic = false;
            viewport.AddComponent<Image>();

            // Content
            GameObject content = new GameObject("Content");
            content.transform.SetParent(viewport.transform, false);
            RectTransform contentRect = content.AddComponent<RectTransform>();
            contentRect.anchorMin = new Vector2(0, 1);
            contentRect.anchorMax = new Vector2(1, 1);
            contentRect.pivot = new Vector2(0.5f, 1);
            contentRect.sizeDelta = new Vector2(0, 30);

            // Item
            GameObject item = new GameObject("Item");
            item.transform.SetParent(content.transform, false);
            RectTransform itemRect = item.AddComponent<RectTransform>();
            itemRect.anchorMin = new Vector2(0, 0.5f);
            itemRect.anchorMax = new Vector2(1, 0.5f);
            itemRect.sizeDelta = new Vector2(0, 30);
            Toggle itemToggle = item.AddComponent<Toggle>();

            // Item Background
            GameObject itemBg = new GameObject("Item Background");
            itemBg.transform.SetParent(item.transform, false);
            Image itemBgImage = itemBg.AddComponent<Image>();
            itemBgImage.color = buttonHighlight;
            RectTransform itemBgRect = itemBg.GetComponent<RectTransform>();
            itemBgRect.anchorMin = Vector2.zero;
            itemBgRect.anchorMax = Vector2.one;
            itemBgRect.offsetMin = Vector2.zero;
            itemBgRect.offsetMax = Vector2.zero;

            // Item Label
            GameObject itemLabel = new GameObject("Item Label");
            itemLabel.transform.SetParent(item.transform, false);
            TextMeshProUGUI itemLabelText = itemLabel.AddComponent<TextMeshProUGUI>();
            itemLabelText.text = "Option";
            itemLabelText.fontSize = 18;
            itemLabelText.alignment = TextAlignmentOptions.Left;
            itemLabelText.color = Color.white;
            RectTransform itemLabelRect = itemLabel.GetComponent<RectTransform>();
            itemLabelRect.anchorMin = Vector2.zero;
            itemLabelRect.anchorMax = Vector2.one;
            itemLabelRect.offsetMin = new Vector2(10, 0);
            itemLabelRect.offsetMax = new Vector2(-10, 0);

            itemToggle.targetGraphic = itemBgImage;

            // Wire up dropdown
            dropdown.captionText = labelText;
            dropdown.itemText = itemLabelText;
            dropdown.template = templateRect;

            template.GetComponent<ScrollRect>().content = contentRect;
            template.GetComponent<ScrollRect>().viewport = viewportRect;

            template.SetActive(false);

            return dropdownObj;
        }

        private static GameObject CreateToggle(Transform parent, string text, string name)
        {
            GameObject toggleObj = new GameObject(name);
            toggleObj.transform.SetParent(parent, false);
            RectTransform toggleRect = toggleObj.AddComponent<RectTransform>();
            toggleRect.sizeDelta = new Vector2(340, 35);

            HorizontalLayoutGroup layout = toggleObj.AddComponent<HorizontalLayoutGroup>();
            layout.spacing = 10;
            layout.childAlignment = TextAnchor.MiddleLeft;
            layout.childControlWidth = false;
            layout.childControlHeight = false;

            // Checkbox background
            GameObject checkboxBg = new GameObject("Background");
            checkboxBg.transform.SetParent(toggleObj.transform, false);
            Image bgImage = checkboxBg.AddComponent<Image>();
            bgImage.color = buttonNormal;
            RectTransform bgRect = checkboxBg.GetComponent<RectTransform>();
            bgRect.sizeDelta = new Vector2(30, 30);

            // Checkmark
            GameObject checkmark = new GameObject("Checkmark");
            checkmark.transform.SetParent(checkboxBg.transform, false);
            TextMeshProUGUI checkText = checkmark.AddComponent<TextMeshProUGUI>();
            checkText.text = "X";
            checkText.fontSize = 20;
            checkText.alignment = TextAlignmentOptions.Center;
            checkText.color = accentColor;
            RectTransform checkRect = checkmark.GetComponent<RectTransform>();
            checkRect.anchorMin = Vector2.zero;
            checkRect.anchorMax = Vector2.one;
            checkRect.offsetMin = Vector2.zero;
            checkRect.offsetMax = Vector2.zero;

            // Label
            GameObject label = new GameObject("Label");
            label.transform.SetParent(toggleObj.transform, false);
            TextMeshProUGUI labelText = label.AddComponent<TextMeshProUGUI>();
            labelText.text = text;
            labelText.fontSize = 20;
            labelText.alignment = TextAlignmentOptions.Left;
            labelText.color = Color.white;
            RectTransform labelRect = label.GetComponent<RectTransform>();
            labelRect.sizeDelta = new Vector2(290, 30);

            // Toggle component
            Toggle toggle = toggleObj.AddComponent<Toggle>();
            toggle.targetGraphic = bgImage;
            toggle.graphic = checkText;
            toggle.isOn = true;

            return toggleObj;
        }
    }
}
