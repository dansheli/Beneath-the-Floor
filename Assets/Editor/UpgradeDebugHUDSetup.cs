using UnityEngine;
using UnityEngine.UI;
using UnityEditor;
using TMPro;

namespace BeneathTheFloor.Editor
{
    /// <summary>
    /// Editor script to create the Upgrade Debug HUD UI panel.
    /// </summary>
    public class UpgradeDebugHUDSetup : UnityEditor.Editor
    {
        private static readonly Color panelColor = new Color(0.05f, 0.05f, 0.08f, 0.85f);
        private static readonly Color headerColor = new Color(0.2f, 0.4f, 0.6f, 1f);
        private static readonly Color textColor = new Color(0.9f, 0.95f, 1f, 1f);
        private static readonly Color valueColor = new Color(0.4f, 0.9f, 0.5f, 1f);

        [MenuItem("Tools/Beneath The Floor/Debug/Create Upgrade Debug HUD")]
        public static void CreateUpgradeDebugHUD()
        {
            // Find or create HUDCanvas
            Canvas canvas = FindOrCreateHUDCanvas();
            if (canvas == null)
            {
                Debug.LogError("[UpgradeDebugHUDSetup] Could not find or create HUDCanvas!");
                return;
            }

            // Check if already exists
            Transform existing = canvas.transform.Find("UpgradeDebugHUD");
            if (existing != null)
            {
                if (!EditorUtility.DisplayDialog("Debug HUD Exists",
                    "UpgradeDebugHUD already exists. Do you want to replace it?",
                    "Replace", "Cancel"))
                {
                    return;
                }
                Undo.DestroyObjectImmediate(existing.gameObject);
            }

            // Create the debug HUD
            GameObject debugHUD = CreateDebugHUDPanel(canvas.transform);

            // Add the PlayerUpgradeDebugHUD script - search all assemblies
            System.Type hudType = null;
            foreach (var assembly in System.AppDomain.CurrentDomain.GetAssemblies())
            {
                hudType = assembly.GetType("BeneathTheFloor.DebugTools.PlayerUpgradeDebugHUD");
                if (hudType != null) break;
            }

            if (hudType == null)
            {
                Debug.LogError("[UpgradeDebugHUDSetup] Could not find PlayerUpgradeDebugHUD type. Make sure scripts are compiled.");
                Object.DestroyImmediate(debugHUD);
                return;
            }

            Debug.Log($"[UpgradeDebugHUDSetup] Found type in assembly: {hudType.Assembly.GetName().Name}");

            var hudScript = debugHUD.AddComponent(hudType) as BeneathTheFloor.DebugTools.PlayerUpgradeDebugHUD;
            if (hudScript == null)
            {
                Debug.LogError("[UpgradeDebugHUDSetup] Failed to add PlayerUpgradeDebugHUD component.");
                Object.DestroyImmediate(debugHUD);
                return;
            }

            // Wire up references using SerializedObject
            WireReferences(hudScript, debugHUD);

            Undo.RegisterCreatedObjectUndo(debugHUD, "Create Upgrade Debug HUD");
            Selection.activeGameObject = debugHUD;

            Debug.Log("[UpgradeDebugHUDSetup] Upgrade Debug HUD created successfully! Press F4 to toggle.");
            EditorUtility.SetDirty(canvas.gameObject);
        }

        private static Canvas FindOrCreateHUDCanvas()
        {
            // Try to find HUDCanvas
            GameObject hudCanvasObj = GameObject.Find("HUDCanvas");
            if (hudCanvasObj != null)
            {
                Canvas canvas = hudCanvasObj.GetComponent<Canvas>();
                if (canvas != null)
                {
                    return canvas;
                }
            }

            // Try any canvas with "HUD" in the name
            Canvas[] allCanvases = Object.FindObjectsOfType<Canvas>();
            foreach (var c in allCanvases)
            {
                if (c.name.Contains("HUD"))
                {
                    return c;
                }
            }

            // Create new HUDCanvas
            Debug.Log("[UpgradeDebugHUDSetup] Creating new HUDCanvas...");
            GameObject newCanvas = new GameObject("HUDCanvas");
            Canvas canvas2 = newCanvas.AddComponent<Canvas>();
            canvas2.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas2.sortingOrder = 100; // High sorting order to be on top

            CanvasScaler scaler = newCanvas.AddComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920, 1080);
            scaler.screenMatchMode = CanvasScaler.ScreenMatchMode.MatchWidthOrHeight;
            scaler.matchWidthOrHeight = 0.5f;

            newCanvas.AddComponent<GraphicRaycaster>();

            return canvas2;
        }

        private static GameObject CreateDebugHUDPanel(Transform parent)
        {
            // Root panel - UpgradeDebugHUD
            GameObject rootPanel = new GameObject("UpgradeDebugHUD");
            rootPanel.transform.SetParent(parent, false);

            RectTransform rootRect = rootPanel.AddComponent<RectTransform>();
            // Anchor to top-left corner
            rootRect.anchorMin = new Vector2(0, 1);
            rootRect.anchorMax = new Vector2(0, 1);
            rootRect.pivot = new Vector2(0, 1);
            rootRect.anchoredPosition = new Vector2(10, -10);
            rootRect.sizeDelta = new Vector2(280, 320);

            // Background
            Image rootBg = rootPanel.AddComponent<Image>();
            rootBg.color = panelColor;

            // Add outline for visibility
            Outline outline = rootPanel.AddComponent<Outline>();
            outline.effectColor = new Color(0.3f, 0.5f, 0.7f, 0.5f);
            outline.effectDistance = new Vector2(1, -1);

            // Add CanvasGroup for potential fade effects
            CanvasGroup cg = rootPanel.AddComponent<CanvasGroup>();
            cg.alpha = 1f;

            // Vertical layout for stacking
            VerticalLayoutGroup vlg = rootPanel.AddComponent<VerticalLayoutGroup>();
            vlg.padding = new RectOffset(10, 10, 8, 8);
            vlg.spacing = 4;
            vlg.childControlWidth = true;
            vlg.childControlHeight = false;
            vlg.childForceExpandWidth = true;
            vlg.childForceExpandHeight = false;

            // Content Size Fitter to auto-size
            ContentSizeFitter csf = rootPanel.AddComponent<ContentSizeFitter>();
            csf.verticalFit = ContentSizeFitter.FitMode.PreferredSize;
            csf.horizontalFit = ContentSizeFitter.FitMode.Unconstrained;

            // Header
            CreateHeader(rootPanel.transform);

            // Separator
            CreateSeparator(rootPanel.transform);

            // Stats
            CreateStatText("DigSpeedText", rootPanel.transform, "Dig Speed: Lv 0 (x1.00)");
            CreateStatText("DigPowerText", rootPanel.transform, "Dig Power: 1.00");
            CreateSeparator(rootPanel.transform);
            CreateStatText("BackpackText", rootPanel.transform, "Backpack: Lv 0\n  Slots: 0/20");
            CreateSeparator(rootPanel.transform);
            CreateStatText("LightText", rootPanel.transform, "Light: Lv 0\n  Radius: 10.0m");
            CreateSeparator(rootPanel.transform);
            CreateStatText("MoveSpeedText", rootPanel.transform, "Move Speed: Lv 0");
            CreateStatText("EnergyText", rootPanel.transform, "Energy: Lv 0");
            CreateSeparator(rootPanel.transform);
            CreateStatText("CreditsText", rootPanel.transform, "Credits: 0");

            // Footer with toggle hint
            CreateFooter(rootPanel.transform);

            return rootPanel;
        }

        private static void CreateHeader(Transform parent)
        {
            GameObject header = new GameObject("Header");
            header.transform.SetParent(parent, false);

            RectTransform rect = header.AddComponent<RectTransform>();
            rect.sizeDelta = new Vector2(0, 24);

            TextMeshProUGUI tmp = header.AddComponent<TextMeshProUGUI>();
            tmp.text = "DEBUG: Upgrade Stats";
            tmp.fontSize = 16;
            tmp.fontStyle = FontStyles.Bold;
            tmp.alignment = TextAlignmentOptions.Center;
            tmp.color = headerColor;

            LayoutElement le = header.AddComponent<LayoutElement>();
            le.minHeight = 24;
            le.preferredHeight = 24;
        }

        private static void CreateSeparator(Transform parent)
        {
            GameObject sep = new GameObject("Separator");
            sep.transform.SetParent(parent, false);

            RectTransform rect = sep.AddComponent<RectTransform>();
            rect.sizeDelta = new Vector2(0, 1);

            Image img = sep.AddComponent<Image>();
            img.color = new Color(0.3f, 0.4f, 0.5f, 0.5f);

            LayoutElement le = sep.AddComponent<LayoutElement>();
            le.minHeight = 1;
            le.preferredHeight = 1;
        }

        private static GameObject CreateStatText(string name, Transform parent, string defaultText)
        {
            GameObject textObj = new GameObject(name);
            textObj.transform.SetParent(parent, false);

            RectTransform rect = textObj.AddComponent<RectTransform>();
            rect.sizeDelta = new Vector2(0, 32);

            TextMeshProUGUI tmp = textObj.AddComponent<TextMeshProUGUI>();
            tmp.text = defaultText;
            tmp.fontSize = 13;
            tmp.alignment = TextAlignmentOptions.Left;
            tmp.color = textColor;
            tmp.enableWordWrapping = true;
            tmp.overflowMode = TextOverflowModes.Overflow;

            LayoutElement le = textObj.AddComponent<LayoutElement>();
            le.minHeight = 18;
            le.preferredHeight = 32;
            le.flexibleHeight = 1;

            return textObj;
        }

        private static void CreateFooter(Transform parent)
        {
            GameObject footer = new GameObject("Footer");
            footer.transform.SetParent(parent, false);

            RectTransform rect = footer.AddComponent<RectTransform>();
            rect.sizeDelta = new Vector2(0, 16);

            TextMeshProUGUI tmp = footer.AddComponent<TextMeshProUGUI>();
            tmp.text = "[F4 to toggle]";
            tmp.fontSize = 10;
            tmp.fontStyle = FontStyles.Italic;
            tmp.alignment = TextAlignmentOptions.Center;
            tmp.color = new Color(0.5f, 0.6f, 0.7f, 0.8f);

            LayoutElement le = footer.AddComponent<LayoutElement>();
            le.minHeight = 16;
            le.preferredHeight = 16;
        }

        private static void WireReferences(BeneathTheFloor.DebugTools.PlayerUpgradeDebugHUD script, GameObject rootPanel)
        {
            SerializedObject so = new SerializedObject(script);

            // Wire rootPanel
            so.FindProperty("rootPanel").objectReferenceValue = rootPanel;

            // Wire text references
            so.FindProperty("digSpeedText").objectReferenceValue =
                rootPanel.transform.Find("DigSpeedText")?.GetComponent<TextMeshProUGUI>();
            so.FindProperty("digPowerText").objectReferenceValue =
                rootPanel.transform.Find("DigPowerText")?.GetComponent<TextMeshProUGUI>();
            so.FindProperty("backpackText").objectReferenceValue =
                rootPanel.transform.Find("BackpackText")?.GetComponent<TextMeshProUGUI>();
            so.FindProperty("lightText").objectReferenceValue =
                rootPanel.transform.Find("LightText")?.GetComponent<TextMeshProUGUI>();
            so.FindProperty("creditsText").objectReferenceValue =
                rootPanel.transform.Find("CreditsText")?.GetComponent<TextMeshProUGUI>();
            so.FindProperty("moveSpeedText").objectReferenceValue =
                rootPanel.transform.Find("MoveSpeedText")?.GetComponent<TextMeshProUGUI>();
            so.FindProperty("energyText").objectReferenceValue =
                rootPanel.transform.Find("EnergyText")?.GetComponent<TextMeshProUGUI>();

            so.ApplyModifiedPropertiesWithoutUndo();
        }

        [MenuItem("Tools/Beneath The Floor/Debug/Toggle Debug HUD (F4)")]
        public static void ToggleDebugHUD()
        {
            GameObject hudObj = GameObject.Find("UpgradeDebugHUD");
            if (hudObj != null)
            {
                hudObj.SetActive(!hudObj.activeSelf);
                Debug.Log($"[UpgradeDebugHUDSetup] Debug HUD toggled: {hudObj.activeSelf}");
            }
            else
            {
                Debug.LogWarning("[UpgradeDebugHUDSetup] UpgradeDebugHUD not found in scene. Use 'Create Upgrade Debug HUD' first.");
            }
        }

        [MenuItem("Tools/Beneath The Floor/Debug/Clean Up Missing Scripts")]
        public static void CleanUpMissingScripts()
        {
            int removedCount = 0;

            // Get all root objects from all loaded scenes
            for (int i = 0; i < UnityEngine.SceneManagement.SceneManager.sceneCount; i++)
            {
                var scene = UnityEngine.SceneManagement.SceneManager.GetSceneAt(i);
                if (!scene.isLoaded) continue;

                Debug.Log($"[CleanUp] Checking scene: {scene.name}");

                foreach (GameObject rootObj in scene.GetRootGameObjects())
                {
                    removedCount += CleanUpGameObjectRecursive(rootObj);
                }
            }

            // Also check DontDestroyOnLoad objects
            GameObject temp = new GameObject("TempDDOL");
            Object.DontDestroyOnLoad(temp);
            var ddolScene = temp.scene;
            Object.DestroyImmediate(temp);

            if (ddolScene.IsValid())
            {
                foreach (GameObject rootObj in ddolScene.GetRootGameObjects())
                {
                    if (rootObj != null)
                        removedCount += CleanUpGameObjectRecursive(rootObj);
                }
            }

            if (removedCount > 0)
            {
                Debug.Log($"[CleanUp] Removed missing scripts from {removedCount} GameObject(s). Save the scene(s) to persist changes.");
            }
            else
            {
                Debug.Log("[CleanUp] No missing scripts found in any loaded scene.");
            }
        }

        private static int CleanUpGameObjectRecursive(GameObject go)
        {
            int count = 0;

            var components = go.GetComponents<Component>();
            bool hasMissing = false;
            foreach (var comp in components)
            {
                if (comp == null)
                {
                    hasMissing = true;
                    break;
                }
            }

            if (hasMissing)
            {
                Debug.Log($"[CleanUp] Found missing script on: {GetFullPath(go)}");
                int removed = GameObjectUtility.RemoveMonoBehavioursWithMissingScript(go);
                if (removed > 0)
                {
                    count++;
                    EditorUtility.SetDirty(go);
                }
            }

            // Check children
            foreach (Transform child in go.transform)
            {
                count += CleanUpGameObjectRecursive(child.gameObject);
            }

            return count;
        }

        private static string GetFullPath(GameObject go)
        {
            string path = go.name;
            Transform parent = go.transform.parent;
            while (parent != null)
            {
                path = parent.name + "/" + path;
                parent = parent.parent;
            }
            return path;
        }
    }
}
