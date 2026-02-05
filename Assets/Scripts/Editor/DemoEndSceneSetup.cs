using UnityEngine;
using UnityEngine.UI;
using UnityEditor;
using UnityEditor.SceneManagement;
using TMPro;

namespace BeneathTheFloor.Editor
{
    /// <summary>
    /// Editor utility to create and set up the Demo End scene.
    /// Run from menu: Tools > Beneath The Floor > Create Demo End Scene
    /// </summary>
    public static class DemoEndSceneSetup
    {
        private const string SCENE_PATH = "Assets/Scenes/DemoEndScene.unity";
        private const string CONTROLLER_SCRIPT = "BeneathTheFloor.GameFlow.DemoEndController";

        // Colors matching the reference image
        private static readonly Color darkBgColor = new Color(0.02f, 0.03f, 0.04f, 1f);
        private static readonly Color panelBgColor = new Color(0.08f, 0.10f, 0.12f, 1f);
        private static readonly Color frameBorderColor = new Color(0.20f, 0.22f, 0.25f, 1f);
        private static readonly Color innerFrameColor = new Color(0.12f, 0.14f, 0.16f, 1f);
        private static readonly Color cyanGlow = new Color(0.3f, 0.85f, 1.0f, 1f);
        private static readonly Color textWhite = new Color(0.92f, 0.94f, 0.96f);
        private static readonly Color textGray = new Color(0.55f, 0.58f, 0.62f);
        private static readonly Color buttonBgColor = new Color(0.12f, 0.14f, 0.18f, 1f);
        private static readonly Color buttonBorderColor = new Color(0.30f, 0.33f, 0.38f, 1f);
        private static readonly Color starGold = new Color(0.85f, 0.7f, 0.3f);

        [MenuItem("Tools/Beneath The Floor/Create Demo End Scene")]
        public static void CreateDemoEndScene()
        {
            // Confirm if scene already exists
            if (System.IO.File.Exists(SCENE_PATH))
            {
                if (!EditorUtility.DisplayDialog("Scene Exists",
                    "DemoEndScene already exists. Do you want to recreate it?",
                    "Recreate", "Cancel"))
                {
                    return;
                }
            }

            // Create new empty scene
            var newScene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);

            // Create Camera
            CreateCamera();

            // Create EventSystem for UI input
            CreateEventSystem();

            // Create Canvas and UI elements
            GameObject canvas = CreateCanvas();
            CreateFullScreenBackground(canvas);

            // Create the main frame panel
            GameObject mainFrame = CreateMainFrame(canvas);

            // Create UI elements inside frame
            CreateHeaderBar(mainFrame);
            GameObject titleText = CreateMainTitle(mainFrame, out GameObject leftGlow, out GameObject rightGlow);
            GameObject teaserText = CreateTeaserText(mainFrame);
            GameObject wishlistBtn = CreateWishlistButton(mainFrame);
            GameObject continueBtn = CreateContinueButton(mainFrame);
            CreateSideArrows(mainFrame);
            CreateCornerDecorations(mainFrame);

            // Create fade overlay (on top of everything)
            GameObject fadeOverlay = CreateFadeOverlay(canvas);

            // Create controller and wire references
            CreateController(canvas, titleText, teaserText, wishlistBtn, continueBtn, fadeOverlay, leftGlow, rightGlow);

            // Save scene
            EditorSceneManager.SaveScene(newScene, SCENE_PATH);

            // Add to build settings
            AddSceneToBuildSettings(SCENE_PATH);

            Debug.Log($"[DemoEndSceneSetup] Scene created and saved to {SCENE_PATH}");
            EditorUtility.DisplayDialog("Success",
                "DemoEndScene created successfully!\n\n" +
                "The scene has been added to Build Settings.\n\n" +
                "You can customize the Steam URL in the DemoEndController component.",
                "OK");
        }

        private static void CreateCamera()
        {
            GameObject cameraObj = new GameObject("Main Camera");
            Camera camera = cameraObj.AddComponent<Camera>();
            camera.clearFlags = CameraClearFlags.SolidColor;
            camera.backgroundColor = Color.black;
            camera.orthographic = true;
            cameraObj.AddComponent<AudioListener>();
            cameraObj.tag = "MainCamera";
        }

        private static void CreateEventSystem()
        {
            GameObject eventSystem = new GameObject("EventSystem");
            eventSystem.AddComponent<UnityEngine.EventSystems.EventSystem>();
            eventSystem.AddComponent<UnityEngine.EventSystems.StandaloneInputModule>();
        }

        private static GameObject CreateCanvas()
        {
            GameObject canvasObj = new GameObject("Canvas");
            Canvas canvas = canvasObj.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = 0;

            CanvasScaler scaler = canvasObj.AddComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920, 1080);
            scaler.screenMatchMode = CanvasScaler.ScreenMatchMode.MatchWidthOrHeight;
            scaler.matchWidthOrHeight = 0.5f;

            canvasObj.AddComponent<GraphicRaycaster>();

            return canvasObj;
        }

        private static void CreateFullScreenBackground(GameObject canvas)
        {
            GameObject bgObj = new GameObject("Background");
            bgObj.transform.SetParent(canvas.transform, false);

            Image bgImage = bgObj.AddComponent<Image>();
            bgImage.color = darkBgColor;
            bgImage.raycastTarget = false;

            RectTransform bgRect = bgObj.GetComponent<RectTransform>();
            bgRect.anchorMin = Vector2.zero;
            bgRect.anchorMax = Vector2.one;
            bgRect.offsetMin = Vector2.zero;
            bgRect.offsetMax = Vector2.zero;

            bgObj.transform.SetSiblingIndex(0);
        }

        private static GameObject CreateMainFrame(GameObject canvas)
        {
            // Outer frame
            GameObject outerFrame = new GameObject("MainFrame");
            outerFrame.transform.SetParent(canvas.transform, false);

            RectTransform outerRect = outerFrame.AddComponent<RectTransform>();
            outerRect.anchorMin = new Vector2(0.5f, 0.5f);
            outerRect.anchorMax = new Vector2(0.5f, 0.5f);
            outerRect.pivot = new Vector2(0.5f, 0.5f);
            outerRect.sizeDelta = new Vector2(1100, 650);

            Image outerBg = outerFrame.AddComponent<Image>();
            outerBg.color = frameBorderColor;

            // Inner frame
            GameObject innerFrame = new GameObject("InnerFrame");
            innerFrame.transform.SetParent(outerFrame.transform, false);

            RectTransform innerRect = innerFrame.AddComponent<RectTransform>();
            innerRect.anchorMin = Vector2.zero;
            innerRect.anchorMax = Vector2.one;
            innerRect.offsetMin = new Vector2(6, 6);
            innerRect.offsetMax = new Vector2(-6, -6);

            Image innerBg = innerFrame.AddComponent<Image>();
            innerBg.color = innerFrameColor;

            // Content panel
            GameObject contentPanel = new GameObject("ContentPanel");
            contentPanel.transform.SetParent(innerFrame.transform, false);

            RectTransform contentRect = contentPanel.AddComponent<RectTransform>();
            contentRect.anchorMin = Vector2.zero;
            contentRect.anchorMax = Vector2.one;
            contentRect.offsetMin = new Vector2(10, 10);
            contentRect.offsetMax = new Vector2(-10, -10);

            Image contentBg = contentPanel.AddComponent<Image>();
            contentBg.color = panelBgColor;

            return contentPanel;
        }

        private static void CreateHeaderBar(GameObject parent)
        {
            GameObject header = new GameObject("HeaderBar");
            header.transform.SetParent(parent.transform, false);

            RectTransform headerRect = header.AddComponent<RectTransform>();
            headerRect.anchorMin = new Vector2(0.3f, 0.93f);
            headerRect.anchorMax = new Vector2(0.7f, 0.99f);
            headerRect.offsetMin = Vector2.zero;
            headerRect.offsetMax = Vector2.zero;

            Image headerBg = header.AddComponent<Image>();
            headerBg.color = new Color(0.03f, 0.04f, 0.05f, 1f);

            // Header text
            GameObject headerText = new GameObject("HeaderText");
            headerText.transform.SetParent(header.transform, false);

            RectTransform textRect = headerText.AddComponent<RectTransform>();
            textRect.anchorMin = Vector2.zero;
            textRect.anchorMax = Vector2.one;
            textRect.offsetMin = Vector2.zero;
            textRect.offsetMax = Vector2.zero;

            TextMeshProUGUI tmp = headerText.AddComponent<TextMeshProUGUI>();
            tmp.text = "END OF DEMO";
            tmp.fontSize = 20;
            tmp.fontStyle = FontStyles.Bold;
            tmp.color = textGray;
            tmp.alignment = TextAlignmentOptions.Center;
        }

        private static GameObject CreateMainTitle(GameObject parent, out GameObject leftGlow, out GameObject rightGlow)
        {
            // Container for title and glow lines
            GameObject titleContainer = new GameObject("TitleContainer");
            titleContainer.transform.SetParent(parent.transform, false);

            RectTransform containerRect = titleContainer.AddComponent<RectTransform>();
            containerRect.anchorMin = new Vector2(0, 0.62f);
            containerRect.anchorMax = new Vector2(1, 0.88f);
            containerRect.offsetMin = new Vector2(40, 0);
            containerRect.offsetMax = new Vector2(-40, 0);

            // Left glow line
            GameObject leftLineObj = new GameObject("LeftGlowLine");
            leftLineObj.transform.SetParent(titleContainer.transform, false);

            RectTransform leftRect = leftLineObj.AddComponent<RectTransform>();
            leftRect.anchorMin = new Vector2(0, 0.45f);
            leftRect.anchorMax = new Vector2(0.22f, 0.55f);
            leftRect.offsetMin = Vector2.zero;
            leftRect.offsetMax = Vector2.zero;

            Image leftImg = leftLineObj.AddComponent<Image>();
            leftImg.color = cyanGlow;
            leftGlow = leftLineObj;

            // Right glow line
            GameObject rightLineObj = new GameObject("RightGlowLine");
            rightLineObj.transform.SetParent(titleContainer.transform, false);

            RectTransform rightRect = rightLineObj.AddComponent<RectTransform>();
            rightRect.anchorMin = new Vector2(0.78f, 0.45f);
            rightRect.anchorMax = new Vector2(1f, 0.55f);
            rightRect.offsetMin = Vector2.zero;
            rightRect.offsetMax = Vector2.zero;

            Image rightImg = rightLineObj.AddComponent<Image>();
            rightImg.color = cyanGlow;
            rightGlow = rightLineObj;

            // Main title text
            GameObject titleObj = new GameObject("TitleText");
            titleObj.transform.SetParent(titleContainer.transform, false);

            RectTransform titleRect = titleObj.AddComponent<RectTransform>();
            titleRect.anchorMin = new Vector2(0.12f, 0);
            titleRect.anchorMax = new Vector2(0.88f, 1);
            titleRect.offsetMin = Vector2.zero;
            titleRect.offsetMax = Vector2.zero;

            TextMeshProUGUI titleTmp = titleObj.AddComponent<TextMeshProUGUI>();
            titleTmp.text = "END OF DEMO";
            titleTmp.fontSize = 72;
            titleTmp.fontStyle = FontStyles.Bold;
            titleTmp.color = textWhite;
            titleTmp.alignment = TextAlignmentOptions.Center;
            titleTmp.enableWordWrapping = false;

            return titleObj;
        }

        private static GameObject CreateTeaserText(GameObject parent)
        {
            GameObject teaserObj = new GameObject("TeaserText");
            teaserObj.transform.SetParent(parent.transform, false);

            RectTransform teaserRect = teaserObj.AddComponent<RectTransform>();
            teaserRect.anchorMin = new Vector2(0.08f, 0.42f);
            teaserRect.anchorMax = new Vector2(0.92f, 0.60f);
            teaserRect.offsetMin = Vector2.zero;
            teaserRect.offsetMax = Vector2.zero;

            TextMeshProUGUI teaserTmp = teaserObj.AddComponent<TextMeshProUGUI>();
            teaserTmp.text = "You've only scratched the surface.\nDeeper tools, autonomous robots, rare materials and\nforgotten systems await below.";
            teaserTmp.fontSize = 24;
            teaserTmp.color = textGray;
            teaserTmp.alignment = TextAlignmentOptions.Center;
            teaserTmp.lineSpacing = 10f;

            return teaserObj;
        }

        private static GameObject CreateWishlistButton(GameObject parent)
        {
            GameObject btnObj = new GameObject("WishlistButton");
            btnObj.transform.SetParent(parent.transform, false);

            RectTransform btnRect = btnObj.AddComponent<RectTransform>();
            btnRect.anchorMin = new Vector2(0.25f, 0.24f);
            btnRect.anchorMax = new Vector2(0.75f, 0.36f);
            btnRect.offsetMin = Vector2.zero;
            btnRect.offsetMax = Vector2.zero;

            Image btnBg = btnObj.AddComponent<Image>();
            btnBg.color = buttonBgColor;

            Outline outline = btnObj.AddComponent<Outline>();
            outline.effectColor = buttonBorderColor;
            outline.effectDistance = new Vector2(2, 2);

            Button btn = btnObj.AddComponent<Button>();
            btn.targetGraphic = btnBg;

            ColorBlock colors = btn.colors;
            colors.highlightedColor = new Color(0.18f, 0.20f, 0.25f);
            colors.pressedColor = new Color(0.22f, 0.25f, 0.30f);
            btn.colors = colors;

            // Star icon
            GameObject starObj = new GameObject("StarIcon");
            starObj.transform.SetParent(btnObj.transform, false);

            RectTransform starRect = starObj.AddComponent<RectTransform>();
            starRect.anchorMin = new Vector2(0, 0);
            starRect.anchorMax = new Vector2(0.15f, 1);
            starRect.offsetMin = new Vector2(30, 0);
            starRect.offsetMax = new Vector2(0, 0);

            TextMeshProUGUI starTmp = starObj.AddComponent<TextMeshProUGUI>();
            starTmp.text = "\u2605";
            starTmp.fontSize = 36;
            starTmp.color = starGold;
            starTmp.alignment = TextAlignmentOptions.Center;

            // Button text
            GameObject textObj = new GameObject("Text");
            textObj.transform.SetParent(btnObj.transform, false);

            RectTransform textRect = textObj.AddComponent<RectTransform>();
            textRect.anchorMin = new Vector2(0.15f, 0);
            textRect.anchorMax = new Vector2(1, 1);
            textRect.offsetMin = Vector2.zero;
            textRect.offsetMax = new Vector2(-20, 0);

            TextMeshProUGUI textTmp = textObj.AddComponent<TextMeshProUGUI>();
            textTmp.text = "ADD TO WISHLIST";
            textTmp.fontSize = 26;
            textTmp.fontStyle = FontStyles.Bold;
            textTmp.color = textWhite;
            textTmp.alignment = TextAlignmentOptions.Center;

            return btnObj;
        }

        private static GameObject CreateContinueButton(GameObject parent)
        {
            GameObject btnObj = new GameObject("ContinueButton");
            btnObj.transform.SetParent(parent.transform, false);

            RectTransform btnRect = btnObj.AddComponent<RectTransform>();
            btnRect.anchorMin = new Vector2(0.25f, 0.08f);
            btnRect.anchorMax = new Vector2(0.75f, 0.20f);
            btnRect.offsetMin = Vector2.zero;
            btnRect.offsetMax = Vector2.zero;

            Image btnBg = btnObj.AddComponent<Image>();
            btnBg.color = buttonBgColor;

            Outline outline = btnObj.AddComponent<Outline>();
            outline.effectColor = buttonBorderColor;
            outline.effectDistance = new Vector2(2, 2);

            Button btn = btnObj.AddComponent<Button>();
            btn.targetGraphic = btnBg;

            ColorBlock colors = btn.colors;
            colors.highlightedColor = new Color(0.18f, 0.20f, 0.25f);
            colors.pressedColor = new Color(0.22f, 0.25f, 0.30f);
            btn.colors = colors;

            // Play icon
            GameObject playObj = new GameObject("PlayIcon");
            playObj.transform.SetParent(btnObj.transform, false);

            RectTransform playRect = playObj.AddComponent<RectTransform>();
            playRect.anchorMin = new Vector2(0, 0);
            playRect.anchorMax = new Vector2(0.15f, 1);
            playRect.offsetMin = new Vector2(30, 0);
            playRect.offsetMax = new Vector2(0, 0);

            TextMeshProUGUI playTmp = playObj.AddComponent<TextMeshProUGUI>();
            playTmp.text = "\u25B6";
            playTmp.fontSize = 30;
            playTmp.color = cyanGlow;
            playTmp.alignment = TextAlignmentOptions.Center;

            // Text container for two lines
            GameObject textContainer = new GameObject("TextContainer");
            textContainer.transform.SetParent(btnObj.transform, false);

            RectTransform containerRect = textContainer.AddComponent<RectTransform>();
            containerRect.anchorMin = new Vector2(0.15f, 0);
            containerRect.anchorMax = new Vector2(1, 1);
            containerRect.offsetMin = Vector2.zero;
            containerRect.offsetMax = new Vector2(-20, 0);

            // Main text
            GameObject mainText = new GameObject("MainText");
            mainText.transform.SetParent(textContainer.transform, false);

            RectTransform mainRect = mainText.AddComponent<RectTransform>();
            mainRect.anchorMin = new Vector2(0, 0.35f);
            mainRect.anchorMax = new Vector2(1, 1);
            mainRect.offsetMin = Vector2.zero;
            mainRect.offsetMax = Vector2.zero;

            TextMeshProUGUI mainTmp = mainText.AddComponent<TextMeshProUGUI>();
            mainTmp.text = "CONTINUE PLAYING";
            mainTmp.fontSize = 26;
            mainTmp.fontStyle = FontStyles.Bold;
            mainTmp.color = textWhite;
            mainTmp.alignment = TextAlignmentOptions.Center;

            // Sub text
            GameObject subText = new GameObject("SubText");
            subText.transform.SetParent(textContainer.transform, false);

            RectTransform subRect = subText.AddComponent<RectTransform>();
            subRect.anchorMin = new Vector2(0, 0);
            subRect.anchorMax = new Vector2(1, 0.45f);
            subRect.offsetMin = Vector2.zero;
            subRect.offsetMax = Vector2.zero;

            TextMeshProUGUI subTmp = subText.AddComponent<TextMeshProUGUI>();
            subTmp.text = "(LIMITED MODE)";
            subTmp.fontSize = 16;
            subTmp.color = textGray;
            subTmp.alignment = TextAlignmentOptions.Center;

            return btnObj;
        }

        private static void CreateSideArrows(GameObject parent)
        {
            // Left arrow
            GameObject leftArrow = new GameObject("LeftArrow");
            leftArrow.transform.SetParent(parent.transform, false);

            RectTransform leftRect = leftArrow.AddComponent<RectTransform>();
            leftRect.anchorMin = new Vector2(0, 0.5f);
            leftRect.anchorMax = new Vector2(0, 0.5f);
            leftRect.pivot = new Vector2(0, 0.5f);
            leftRect.sizeDelta = new Vector2(30, 50);
            leftRect.anchoredPosition = new Vector2(15, 0);

            TextMeshProUGUI leftTmp = leftArrow.AddComponent<TextMeshProUGUI>();
            leftTmp.text = "<";
            leftTmp.fontSize = 40;
            leftTmp.color = new Color(cyanGlow.r, cyanGlow.g, cyanGlow.b, 0.5f);
            leftTmp.alignment = TextAlignmentOptions.Center;

            // Right arrow
            GameObject rightArrow = new GameObject("RightArrow");
            rightArrow.transform.SetParent(parent.transform, false);

            RectTransform rightRect = rightArrow.AddComponent<RectTransform>();
            rightRect.anchorMin = new Vector2(1, 0.5f);
            rightRect.anchorMax = new Vector2(1, 0.5f);
            rightRect.pivot = new Vector2(1, 0.5f);
            rightRect.sizeDelta = new Vector2(30, 50);
            rightRect.anchoredPosition = new Vector2(-15, 0);

            TextMeshProUGUI rightTmp = rightArrow.AddComponent<TextMeshProUGUI>();
            rightTmp.text = ">";
            rightTmp.fontSize = 40;
            rightTmp.color = new Color(cyanGlow.r, cyanGlow.g, cyanGlow.b, 0.5f);
            rightTmp.alignment = TextAlignmentOptions.Center;
        }

        private static void CreateCornerDecorations(GameObject parent)
        {
            Vector2[] corners = {
                new Vector2(0, 1),    // Top-left
                new Vector2(1, 1),    // Top-right
                new Vector2(0, 0),    // Bottom-left
                new Vector2(1, 0)     // Bottom-right
            };

            foreach (var corner in corners)
            {
                GameObject screw = new GameObject("CornerScrew");
                screw.transform.SetParent(parent.transform, false);

                RectTransform screwRect = screw.AddComponent<RectTransform>();
                screwRect.anchorMin = corner;
                screwRect.anchorMax = corner;
                screwRect.pivot = corner;
                screwRect.sizeDelta = new Vector2(28, 28);
                screwRect.anchoredPosition = new Vector2(
                    corner.x == 0 ? 18 : -18,
                    corner.y == 0 ? 18 : -18
                );

                Image screwImg = screw.AddComponent<Image>();
                screwImg.color = new Color(0.30f, 0.32f, 0.36f);

                // Inner circle
                GameObject inner = new GameObject("Inner");
                inner.transform.SetParent(screw.transform, false);

                RectTransform innerRect = inner.AddComponent<RectTransform>();
                innerRect.anchorMin = new Vector2(0.2f, 0.2f);
                innerRect.anchorMax = new Vector2(0.8f, 0.8f);
                innerRect.offsetMin = Vector2.zero;
                innerRect.offsetMax = Vector2.zero;

                Image innerImg = inner.AddComponent<Image>();
                innerImg.color = new Color(0.18f, 0.20f, 0.22f);
            }
        }

        private static GameObject CreateFadeOverlay(GameObject canvas)
        {
            GameObject fadeObj = new GameObject("FadeOverlay");
            fadeObj.transform.SetParent(canvas.transform, false);

            Image fadeImage = fadeObj.AddComponent<Image>();
            fadeImage.color = new Color(0f, 0f, 0f, 1f);
            fadeImage.raycastTarget = true;

            RectTransform fadeRect = fadeObj.GetComponent<RectTransform>();
            fadeRect.anchorMin = Vector2.zero;
            fadeRect.anchorMax = Vector2.one;
            fadeRect.offsetMin = Vector2.zero;
            fadeRect.offsetMax = Vector2.zero;

            fadeObj.transform.SetAsLastSibling();

            return fadeObj;
        }

        private static void CreateController(GameObject canvas, GameObject titleText, GameObject teaserText,
            GameObject wishlistBtn, GameObject continueBtn, GameObject fadeOverlay,
            GameObject leftGlow, GameObject rightGlow)
        {
            System.Type controllerType = null;
            foreach (var assembly in System.AppDomain.CurrentDomain.GetAssemblies())
            {
                controllerType = assembly.GetType(CONTROLLER_SCRIPT);
                if (controllerType != null) break;
            }

            if (controllerType == null)
            {
                Debug.LogError("[DemoEndSceneSetup] Could not find DemoEndController script. Make sure it's compiled.");
                return;
            }

            Component controller = canvas.AddComponent(controllerType);

            SerializedObject serializedController = new SerializedObject(controller);

            var titleProp = serializedController.FindProperty("titleText");
            if (titleProp != null)
                titleProp.objectReferenceValue = titleText.GetComponent<TextMeshProUGUI>();

            var teaserProp = serializedController.FindProperty("teaserText");
            if (teaserProp != null)
                teaserProp.objectReferenceValue = teaserText.GetComponent<TextMeshProUGUI>();

            var wishlistProp = serializedController.FindProperty("wishlistButton");
            if (wishlistProp != null)
                wishlistProp.objectReferenceValue = wishlistBtn.GetComponent<Button>();

            var continueProp = serializedController.FindProperty("continueButton");
            if (continueProp != null)
                continueProp.objectReferenceValue = continueBtn.GetComponent<Button>();

            var fadeProp = serializedController.FindProperty("fadeOverlay");
            if (fadeProp != null)
                fadeProp.objectReferenceValue = fadeOverlay.GetComponent<Image>();

            var leftGlowProp = serializedController.FindProperty("leftGlowLine");
            if (leftGlowProp != null)
                leftGlowProp.objectReferenceValue = leftGlow.GetComponent<Image>();

            var rightGlowProp = serializedController.FindProperty("rightGlowLine");
            if (rightGlowProp != null)
                rightGlowProp.objectReferenceValue = rightGlow.GetComponent<Image>();

            serializedController.ApplyModifiedPropertiesWithoutUndo();
        }

        private static void AddSceneToBuildSettings(string scenePath)
        {
            var scenes = new System.Collections.Generic.List<EditorBuildSettingsScene>(EditorBuildSettings.scenes);

            bool exists = false;
            foreach (var scene in scenes)
            {
                if (scene.path == scenePath)
                {
                    exists = true;
                    scene.enabled = true;
                    break;
                }
            }

            if (!exists)
            {
                scenes.Add(new EditorBuildSettingsScene(scenePath, true));
            }

            EditorBuildSettings.scenes = scenes.ToArray();
            Debug.Log($"[DemoEndSceneSetup] Scene added to Build Settings");
        }
    }
}
