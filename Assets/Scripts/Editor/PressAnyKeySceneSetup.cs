using UnityEngine;
using UnityEngine.UI;
using UnityEditor;
using UnityEditor.SceneManagement;
using TMPro;

namespace BeneathTheFloor.Editor
{
    /// <summary>
    /// Editor utility to create and set up the Press Any Key scene.
    /// Run from menu: Tools > Beneath The Floor > Create Press Any Key Scene
    /// </summary>
    public static class PressAnyKeySceneSetup
    {
        private const string SCENE_PATH = "Assets/Scenes/PressAnyKeyScene.unity";
        private const string CONTROLLER_SCRIPT = "BeneathTheFloor.GameFlow.PressAnyKeyController";

        [MenuItem("Tools/Beneath The Floor/Create Press Any Key Scene")]
        public static void CreatePressAnyKeyScene()
        {
            // Confirm if scene already exists
            if (System.IO.File.Exists(SCENE_PATH))
            {
                if (!EditorUtility.DisplayDialog("Scene Exists",
                    "PressAnyKeyScene already exists. Do you want to recreate it?",
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
            CreateBackgroundImage(canvas);
            GameObject pressAnyKeyText = CreatePressAnyKeyText(canvas);
            GameObject fadeOverlay = CreateFadeOverlay(canvas);

            // Create controller and wire references
            CreateController(canvas, pressAnyKeyText, fadeOverlay);

            // Save scene
            EditorSceneManager.SaveScene(newScene, SCENE_PATH);

            // Add to build settings
            AddSceneToBuildSettings(SCENE_PATH);

            Debug.Log($"[PressAnyKeySceneSetup] Scene created and saved to {SCENE_PATH}");
            EditorUtility.DisplayDialog("Success",
                "PressAnyKeyScene created successfully!\n\n" +
                "The scene has been added to Build Settings.\n\n" +
                "You can assign a background sprite to the BackgroundImage.",
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

        private static void CreateBackgroundImage(GameObject canvas)
        {
            GameObject bgObj = new GameObject("BackgroundImage");
            bgObj.transform.SetParent(canvas.transform, false);

            Image bgImage = bgObj.AddComponent<Image>();
            bgImage.color = new Color(0.05f, 0.05f, 0.08f, 1f); // Dark blue-black
            bgImage.raycastTarget = false;

            RectTransform bgRect = bgObj.GetComponent<RectTransform>();
            bgRect.anchorMin = Vector2.zero;
            bgRect.anchorMax = Vector2.one;
            bgRect.offsetMin = Vector2.zero;
            bgRect.offsetMax = Vector2.zero;

            // Set sibling index to be first (behind everything)
            bgObj.transform.SetSiblingIndex(0);
        }

        private static GameObject CreatePressAnyKeyText(GameObject canvas)
        {
            GameObject textObj = new GameObject("PressAnyKeyText");
            textObj.transform.SetParent(canvas.transform, false);

            TextMeshProUGUI tmp = textObj.AddComponent<TextMeshProUGUI>();
            tmp.text = "PRESS ANY KEY";
            tmp.fontSize = 48;
            tmp.fontStyle = FontStyles.Bold;
            tmp.color = Color.white;
            tmp.alignment = TextAlignmentOptions.Center;
            tmp.raycastTarget = false;

            // Try to assign a default TMP font
            TMP_FontAsset defaultFont = Resources.Load<TMP_FontAsset>("Fonts & Materials/LiberationSans SDF");
            if (defaultFont == null)
            {
                // Try alternate path
                string[] guids = AssetDatabase.FindAssets("LiberationSans SDF t:TMP_FontAsset");
                if (guids.Length > 0)
                {
                    string path = AssetDatabase.GUIDToAssetPath(guids[0]);
                    defaultFont = AssetDatabase.LoadAssetAtPath<TMP_FontAsset>(path);
                }
            }
            if (defaultFont != null)
            {
                tmp.font = defaultFont;
            }

            RectTransform textRect = textObj.GetComponent<RectTransform>();
            // Position at bottom center
            textRect.anchorMin = new Vector2(0.5f, 0f);
            textRect.anchorMax = new Vector2(0.5f, 0f);
            textRect.pivot = new Vector2(0.5f, 0f);
            textRect.anchoredPosition = new Vector2(0f, 100f);
            textRect.sizeDelta = new Vector2(600f, 80f);

            return textObj;
        }

        private static GameObject CreateFadeOverlay(GameObject canvas)
        {
            GameObject fadeObj = new GameObject("FadeOverlay");
            fadeObj.transform.SetParent(canvas.transform, false);

            Image fadeImage = fadeObj.AddComponent<Image>();
            fadeImage.color = new Color(0f, 0f, 0f, 0f); // Fully transparent black
            fadeImage.raycastTarget = false;

            RectTransform fadeRect = fadeObj.GetComponent<RectTransform>();
            fadeRect.anchorMin = Vector2.zero;
            fadeRect.anchorMax = Vector2.one;
            fadeRect.offsetMin = Vector2.zero;
            fadeRect.offsetMax = Vector2.zero;

            // Set sibling index to be last (in front of everything)
            fadeObj.transform.SetAsLastSibling();

            return fadeObj;
        }

        private static void CreateController(GameObject canvas, GameObject textObj, GameObject fadeObj)
        {
            // Find the PressAnyKeyController type
            System.Type controllerType = null;
            foreach (var assembly in System.AppDomain.CurrentDomain.GetAssemblies())
            {
                controllerType = assembly.GetType(CONTROLLER_SCRIPT);
                if (controllerType != null) break;
            }

            if (controllerType == null)
            {
                Debug.LogError("[PressAnyKeySceneSetup] Could not find PressAnyKeyController script. Make sure it's compiled.");
                return;
            }

            // Add controller to canvas
            Component controller = canvas.AddComponent(controllerType);

            // Use SerializedObject to set references
            SerializedObject serializedController = new SerializedObject(controller);

            SerializedProperty textProp = serializedController.FindProperty("pressAnyKeyText");
            if (textProp != null)
            {
                textProp.objectReferenceValue = textObj.GetComponent<TextMeshProUGUI>();
            }

            SerializedProperty fadeProp = serializedController.FindProperty("fadeOverlay");
            if (fadeProp != null)
            {
                fadeProp.objectReferenceValue = fadeObj.GetComponent<Image>();
            }

            serializedController.ApplyModifiedPropertiesWithoutUndo();
        }

        private static void AddSceneToBuildSettings(string scenePath)
        {
            var scenes = new System.Collections.Generic.List<EditorBuildSettingsScene>(EditorBuildSettings.scenes);

            // Check if scene already in build settings
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
                // Add at index 0 to make it the first scene (startup)
                scenes.Insert(0, new EditorBuildSettingsScene(scenePath, true));
            }

            EditorBuildSettings.scenes = scenes.ToArray();
            Debug.Log($"[PressAnyKeySceneSetup] Scene added to Build Settings at index 0");
        }
    }
}
