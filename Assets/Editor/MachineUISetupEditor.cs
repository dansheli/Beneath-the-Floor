#if UNITY_EDITOR
using UnityEngine;
using UnityEngine.UI;
using UnityEditor;
using TMPro;
using BeneathTheFloor.Machines;
using BeneathTheFloor.Energy;

namespace BeneathTheFloor.Editor
{
    public class MachineUISetupEditor : EditorWindow
    {
        [MenuItem("BeneathTheFloor/Setup/Machine UI Wiring")]
        public static void WireExistingUI()
        {
            Debug.Log("[MachineUISetup] === Starting UI Wiring ===");

            // Find MachineUICanvas
            var canvas = GameObject.Find("MachineUICanvas");
            if (canvas == null)
            {
                Debug.LogError("[MachineUISetup] MachineUICanvas not found in scene!");
                return;
            }

            int wiredCount = 0;

            // NOTE: WorkbenchUI wiring removed - Workbench system deprecated, using UpgradeStation instead

            // Wire RefineryUI
            var refineryUI = Object.FindObjectOfType<RefineryUI>(true);
            if (refineryUI != null)
            {
                WireRefineryUI(refineryUI, canvas.transform);
                wiredCount++;
            }
            else
            {
                Debug.LogWarning("[MachineUISetup] RefineryUI component not found");
            }

            // Wire UpgradeStationUI
            var upgradeStationUI = Object.FindObjectOfType<UpgradeStationUI>(true);
            if (upgradeStationUI != null)
            {
                WireUpgradeStationUI(upgradeStationUI, canvas.transform);
                wiredCount++;
            }
            else
            {
                Debug.LogWarning("[MachineUISetup] UpgradeStationUI component not found");
            }

            // Wire EnergyUI
            var energyUI = Object.FindObjectOfType<EnergyUI>(true);
            if (energyUI != null)
            {
                WireEnergyUI(energyUI, canvas.transform);
                wiredCount++;
            }
            else
            {
                Debug.LogWarning("[MachineUISetup] EnergyUI component not found");
            }

            Debug.Log($"[MachineUISetup] === Wiring Complete: {wiredCount} UIs wired ===");

            // Mark scene dirty
            UnityEditor.SceneManagement.EditorSceneManager.MarkSceneDirty(
                UnityEditor.SceneManagement.EditorSceneManager.GetActiveScene());
        }

        [MenuItem("BeneathTheFloor/Setup/Create Static Machine UI")]
        public static void CreateStaticUI()
        {
            Debug.Log("[MachineUISetup] === Creating Static UI Structure ===");

            // Find or create MachineUICanvas
            var canvasObj = GameObject.Find("MachineUICanvas");
            if (canvasObj == null)
            {
                canvasObj = new GameObject("MachineUICanvas");
                var canvas = canvasObj.AddComponent<Canvas>();
                canvas.renderMode = RenderMode.ScreenSpaceOverlay;
                canvas.sortingOrder = 100;

                var scaler = canvasObj.AddComponent<CanvasScaler>();
                scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
                scaler.referenceResolution = new Vector2(1920, 1080);
                scaler.screenMatchMode = CanvasScaler.ScreenMatchMode.MatchWidthOrHeight;
                scaler.matchWidthOrHeight = 0.5f;

                canvasObj.AddComponent<GraphicRaycaster>();
                Debug.Log("[MachineUISetup] Created MachineUICanvas");
            }

            // Create panels (NOTE: WorkbenchPanel removed - Workbench system deprecated)
            CreateRefineryPanel(canvasObj.transform);
            CreateUpgradeStationPanel(canvasObj.transform);
            CreateEnergyPanel(canvasObj.transform);

            // Add UI script components if not present
            EnsureUIComponents(canvasObj);

            Debug.Log("[MachineUISetup] === Static UI Creation Complete ===");

            // Mark scene dirty
            UnityEditor.SceneManagement.EditorSceneManager.MarkSceneDirty(
                UnityEditor.SceneManagement.EditorSceneManager.GetActiveScene());
        }

        private static void EnsureUIComponents(GameObject canvas)
        {
            // NOTE: WorkbenchUI component check removed - Workbench system deprecated

            // RefineryUI
            if (Object.FindObjectOfType<RefineryUI>(true) == null)
            {
                var refineryPanel = canvas.transform.Find("RefineryPanel");
                if (refineryPanel != null)
                {
                    var comp = canvas.AddComponent<RefineryUI>();
                    Debug.Log("[MachineUISetup] Added RefineryUI component");
                }
            }

            // UpgradeStationUI
            if (Object.FindObjectOfType<UpgradeStationUI>(true) == null)
            {
                var upgradePanel = canvas.transform.Find("UpgradeStationPanel");
                if (upgradePanel != null)
                {
                    var comp = canvas.AddComponent<UpgradeStationUI>();
                    Debug.Log("[MachineUISetup] Added UpgradeStationUI component");
                }
            }

            // EnergyUI
            if (Object.FindObjectOfType<EnergyUI>(true) == null)
            {
                var energyPanel = canvas.transform.Find("EnergyPanel");
                if (energyPanel != null)
                {
                    var comp = canvas.AddComponent<EnergyUI>();
                    Debug.Log("[MachineUISetup] Added EnergyUI component");
                }
            }
        }

        // NOTE: WireWorkbenchUI method removed - Workbench system deprecated, using UpgradeStation instead

        private static void WireRefineryUI(RefineryUI ui, Transform canvas)
        {
            var so = new SerializedObject(ui);

            var panel = canvas.Find("RefineryPanel");
            if (panel != null)
            {
                so.FindProperty("refineryPanel").objectReferenceValue = panel.gameObject;

                var leftPanel = panel.Find("LeftPanel");
                if (leftPanel != null)
                {
                    var scrollView = leftPanel.Find("ScrollView");
                    if (scrollView != null)
                    {
                        var viewport = scrollView.Find("Viewport");
                        if (viewport != null)
                        {
                            var content = viewport.Find("RecipeListContent");
                            if (content != null)
                            {
                                so.FindProperty("recipeListContent").objectReferenceValue = content;
                            }
                        }
                    }
                }

                var detailsPanel = panel.Find("DetailsPanel");
                if (detailsPanel != null)
                {
                    so.FindProperty("detailsPanel").objectReferenceValue = detailsPanel.gameObject;

                    var inputText = FindChildByName<TextMeshProUGUI>(detailsPanel, "InputText");
                    if (inputText) so.FindProperty("inputText").objectReferenceValue = inputText;

                    var outputText = FindChildByName<TextMeshProUGUI>(detailsPanel, "OutputText");
                    if (outputText) so.FindProperty("outputText").objectReferenceValue = outputText;

                    var processTimeText = FindChildByName<TextMeshProUGUI>(detailsPanel, "ProcessTime");
                    if (processTimeText) so.FindProperty("processTimeText").objectReferenceValue = processTimeText;

                    var amountSlider = FindChildByName<Slider>(detailsPanel, "AmountSlider");
                    if (amountSlider) so.FindProperty("amountSlider").objectReferenceValue = amountSlider;

                    var amountText = FindChildByName<TextMeshProUGUI>(detailsPanel, "AmountText");
                    if (amountText) so.FindProperty("amountText").objectReferenceValue = amountText;

                    var processBtn = FindChildByName<Button>(detailsPanel, "ProcessButton");
                    if (processBtn) so.FindProperty("processButton").objectReferenceValue = processBtn;
                }

                var progressPanel = panel.Find("ProgressPanel");
                if (progressPanel != null)
                {
                    so.FindProperty("progressPanel").objectReferenceValue = progressPanel.gameObject;

                    var progressBar = progressPanel.GetComponentInChildren<Slider>();
                    if (progressBar) so.FindProperty("progressBar").objectReferenceValue = progressBar;

                    var progressText = FindChildByName<TextMeshProUGUI>(progressPanel, "ProgressText");
                    if (progressText) so.FindProperty("progressText").objectReferenceValue = progressText;
                }

                var header = panel.Find("Header");
                if (header != null)
                {
                    var titleText = FindChildByName<TextMeshProUGUI>(header, "TitleText");
                    if (titleText) so.FindProperty("titleText").objectReferenceValue = titleText;

                    var closeBtn = FindChildByName<Button>(header, "CloseButton");
                    if (closeBtn) so.FindProperty("closeButton").objectReferenceValue = closeBtn;
                }
            }

            so.ApplyModifiedProperties();
            Debug.Log("[MachineUISetup] Wired RefineryUI");
        }

        private static void WireUpgradeStationUI(UpgradeStationUI ui, Transform canvas)
        {
            var so = new SerializedObject(ui);

            // Wire the overlay
            var overlay = canvas.Find("UpgradeStationOverlay");
            if (overlay != null)
            {
                so.FindProperty("stationOverlay").objectReferenceValue = overlay.gameObject;
            }

            var panel = canvas.Find("UpgradeStationPanel");
            if (panel != null)
            {
                so.FindProperty("stationPanel").objectReferenceValue = panel.gameObject;

                // New structure: ContentArea/LeftTabsPanel/CategoryTabsContent
                var contentArea = panel.Find("ContentArea");
                if (contentArea != null)
                {
                    var leftTabsPanel = contentArea.Find("LeftTabsPanel");
                    if (leftTabsPanel != null)
                    {
                        var tabsContent = leftTabsPanel.Find("CategoryTabsContent");
                        if (tabsContent != null)
                        {
                            so.FindProperty("categoryTabsContent").objectReferenceValue = tabsContent;
                        }
                    }

                    // New structure: ContentArea/UpgradeListPanel/ScrollView/Viewport/UpgradeListContent
                    var upgradeListPanel = contentArea.Find("UpgradeListPanel");
                    if (upgradeListPanel != null)
                    {
                        var scrollView = upgradeListPanel.Find("ScrollView");
                        if (scrollView != null)
                        {
                            var viewport = scrollView.Find("Viewport");
                            if (viewport != null)
                            {
                                var content = viewport.Find("UpgradeListContent");
                                if (content != null)
                                {
                                    so.FindProperty("upgradeListContent").objectReferenceValue = content;
                                }
                            }
                        }
                    }

                    // New structure: ContentArea/DetailsPanel
                    var detailsPanel = contentArea.Find("DetailsPanel");
                    if (detailsPanel != null)
                    {
                        so.FindProperty("detailsPanel").objectReferenceValue = detailsPanel.gameObject;

                        var nameText = FindChildByName<TextMeshProUGUI>(detailsPanel, "UpgradeName");
                        if (nameText) so.FindProperty("upgradeNameText").objectReferenceValue = nameText;

                        var descText = FindChildByName<TextMeshProUGUI>(detailsPanel, "Description");
                        if (descText) so.FindProperty("upgradeDescriptionText").objectReferenceValue = descText;

                        var levelText = FindChildByName<TextMeshProUGUI>(detailsPanel, "CurrentLevel");
                        if (levelText) so.FindProperty("currentLevelText").objectReferenceValue = levelText;

                        var effectText = FindChildByName<TextMeshProUGUI>(detailsPanel, "EffectText");
                        if (effectText) so.FindProperty("effectText").objectReferenceValue = effectText;

                        var costText = FindChildByName<TextMeshProUGUI>(detailsPanel, "CostText");
                        if (costText) so.FindProperty("costText").objectReferenceValue = costText;

                        var progressBar = FindChildByName<Slider>(detailsPanel, "LevelProgressBar");
                        if (progressBar) so.FindProperty("levelProgressBar").objectReferenceValue = progressBar;

                        var progressText = FindChildByName<TextMeshProUGUI>(detailsPanel, "LevelProgressText");
                        if (progressText) so.FindProperty("levelProgressText").objectReferenceValue = progressText;

                        var purchaseBtn = FindChildByName<Button>(detailsPanel, "PurchaseButton");
                        if (purchaseBtn) so.FindProperty("purchaseButton").objectReferenceValue = purchaseBtn;
                    }
                }

                // New structure: TopBar contains TitleText, CloseButton, CreditsText
                var topBar = panel.Find("TopBar");
                if (topBar != null)
                {
                    var titleText = FindChildByName<TextMeshProUGUI>(topBar, "TitleText");
                    if (titleText) so.FindProperty("titleText").objectReferenceValue = titleText;

                    var closeBtn = FindChildByName<Button>(topBar, "CloseButton");
                    if (closeBtn) so.FindProperty("closeButton").objectReferenceValue = closeBtn;

                    var creditsText = FindChildByName<TextMeshProUGUI>(topBar, "CreditsText");
                    if (creditsText) so.FindProperty("creditsText").objectReferenceValue = creditsText;
                }
            }

            so.ApplyModifiedProperties();
            Debug.Log("[MachineUISetup] Wired UpgradeStationUI (modern layout)");
        }

        private static void WireEnergyUI(EnergyUI ui, Transform canvas)
        {
            var so = new SerializedObject(ui);

            var panel = canvas.Find("EnergyPanel");
            if (panel != null)
            {
                so.FindProperty("energyHUD").objectReferenceValue = panel.gameObject;

                var energyBar = panel.GetComponentInChildren<Slider>();
                if (energyBar) so.FindProperty("energyBar").objectReferenceValue = energyBar;

                var energyText = FindChildByName<TextMeshProUGUI>(panel, "EnergyText");
                if (energyText) so.FindProperty("energyText").objectReferenceValue = energyText;
            }

            so.ApplyModifiedProperties();
            Debug.Log("[MachineUISetup] Wired EnergyUI");
        }

        private static T FindChildByName<T>(Transform parent, string name) where T : Component
        {
            var found = parent.Find(name);
            if (found != null)
            {
                return found.GetComponent<T>();
            }

            // Search recursively
            foreach (Transform child in parent)
            {
                var result = FindChildByName<T>(child, name);
                if (result != null) return result;
            }

            return null;
        }

        // Panel creation methods
        // NOTE: CreateWorkbenchPanel removed - Workbench system deprecated, using UpgradeStation instead

        private static void CreateRefineryPanel(Transform parent)
        {
            // Delete existing panel if it exists (might be incomplete)
            var existing = parent.Find("RefineryPanel");
            if (existing != null)
            {
                Object.DestroyImmediate(existing.gameObject);
                Debug.Log("[MachineUISetup] Deleted existing RefineryPanel to recreate it");
            }

            var panel = CreateBasePanel(parent, "RefineryPanel", new Vector2(650, 450));

            // Header
            var header = CreateHeader(panel.transform, "Refinery (Tier 1)");

            // Left Panel with ScrollView
            var leftPanel = CreateSidePanel(panel.transform, "LeftPanel", true, 220);
            var recipeContent = CreateScrollView(leftPanel.transform, "RecipeListContent");

            // NOTE: Recipe buttons are created at RUNTIME by RefineryUI
            // Do NOT create static recipe buttons here - they will be duplicated!

            // Details Panel
            var detailsPanel = CreateSidePanel(panel.transform, "DetailsPanel", false, 390);
            CreateRefineryDetails(detailsPanel.transform);

            // Progress Panel
            CreateProgressPanel(panel.transform);

            panel.SetActive(false);
            Debug.Log("[MachineUISetup] Created RefineryPanel with 5 recipe buttons");
        }

        private static void CreateUpgradeStationPanel(Transform parent)
        {
            // Delete existing panel and overlay if they exist
            var existingOverlay = parent.Find("UpgradeStationOverlay");
            if (existingOverlay != null)
            {
                Object.DestroyImmediate(existingOverlay.gameObject);
            }
            var existing = parent.Find("UpgradeStationPanel");
            if (existing != null)
            {
                Object.DestroyImmediate(existing.gameObject);
                Debug.Log("[MachineUISetup] Deleted existing UpgradeStationPanel to recreate it");
            }

            // === BACKGROUND OVERLAY (full screen, blocks clicks) ===
            var overlay = new GameObject("UpgradeStationOverlay");
            overlay.transform.SetParent(parent, false);
            var overlayRect = overlay.AddComponent<RectTransform>();
            overlayRect.anchorMin = Vector2.zero;
            overlayRect.anchorMax = Vector2.one;
            overlayRect.sizeDelta = Vector2.zero;
            var overlayImage = overlay.AddComponent<Image>();
            overlayImage.color = new Color(0f, 0f, 0f, 0.6f);
            overlayImage.raycastTarget = true;
            overlay.SetActive(false);

            // === MAIN PANEL (centered window) ===
            var panel = new GameObject("UpgradeStationPanel");
            panel.transform.SetParent(parent, false);
            var panelRect = panel.AddComponent<RectTransform>();
            panelRect.anchorMin = new Vector2(0.5f, 0.5f);
            panelRect.anchorMax = new Vector2(0.5f, 0.5f);
            panelRect.pivot = new Vector2(0.5f, 0.5f);
            panelRect.sizeDelta = new Vector2(950, 580);
            panelRect.anchoredPosition = Vector2.zero;

            var panelImage = panel.AddComponent<Image>();
            panelImage.color = new Color(0.08f, 0.10f, 0.14f, 0.98f); // Dark blue-gray

            // === TOP BAR ===
            var topBar = new GameObject("TopBar");
            topBar.transform.SetParent(panel.transform, false);
            var topBarRect = topBar.AddComponent<RectTransform>();
            topBarRect.anchorMin = new Vector2(0, 1);
            topBarRect.anchorMax = new Vector2(1, 1);
            topBarRect.pivot = new Vector2(0.5f, 1);
            topBarRect.anchoredPosition = Vector2.zero;
            topBarRect.sizeDelta = new Vector2(0, 60);

            var topBarImage = topBar.AddComponent<Image>();
            topBarImage.color = new Color(0.12f, 0.14f, 0.20f, 1f);

            // Title text
            var titleText = CreateText(topBar.transform, "TitleText", "UPGRADE STATION", 22);
            titleText.fontStyle = TMPro.FontStyles.Bold;
            var titleRect = titleText.GetComponent<RectTransform>();
            titleRect.anchorMin = new Vector2(0, 0);
            titleRect.anchorMax = new Vector2(0.4f, 1);
            titleRect.offsetMin = new Vector2(20, 5);
            titleRect.offsetMax = new Vector2(0, -5);
            titleText.alignment = TextAlignmentOptions.Left;
            titleText.color = new Color(0.9f, 0.9f, 0.95f, 1f);

            // Subtitle text
            var subtitleText = CreateText(topBar.transform, "SubtitleText", "Beneath The Floor", 11);
            var subtitleRect = subtitleText.GetComponent<RectTransform>();
            subtitleRect.anchorMin = new Vector2(0, 0);
            subtitleRect.anchorMax = new Vector2(0.4f, 0.4f);
            subtitleRect.offsetMin = new Vector2(20, 0);
            subtitleRect.offsetMax = new Vector2(0, 0);
            subtitleText.alignment = TextAlignmentOptions.Left;
            subtitleText.color = new Color(0.5f, 0.55f, 0.65f, 1f);

            // Credits container (right side of top bar)
            var creditsContainer = new GameObject("CreditsContainer");
            creditsContainer.transform.SetParent(topBar.transform, false);
            var creditsContainerRect = creditsContainer.AddComponent<RectTransform>();
            creditsContainerRect.anchorMin = new Vector2(0.55f, 0.15f);
            creditsContainerRect.anchorMax = new Vector2(0.85f, 0.85f);
            creditsContainerRect.offsetMin = Vector2.zero;
            creditsContainerRect.offsetMax = Vector2.zero;

            var creditsContainerImage = creditsContainer.AddComponent<Image>();
            creditsContainerImage.color = new Color(0.18f, 0.22f, 0.30f, 0.9f);

            // Credits icon (placeholder circle)
            var creditsIcon = new GameObject("CreditsIcon");
            creditsIcon.transform.SetParent(creditsContainer.transform, false);
            var iconRect = creditsIcon.AddComponent<RectTransform>();
            iconRect.anchorMin = new Vector2(0, 0.2f);
            iconRect.anchorMax = new Vector2(0, 0.8f);
            iconRect.pivot = new Vector2(0, 0.5f);
            iconRect.anchoredPosition = new Vector2(12, 0);
            iconRect.sizeDelta = new Vector2(24, 0);
            var iconImage = creditsIcon.AddComponent<Image>();
            iconImage.color = new Color(1f, 0.85f, 0.2f, 1f); // Gold color

            // Credits text
            var creditsText = CreateText(creditsContainer.transform, "CreditsText", "0 Credits", 16);
            creditsText.fontStyle = TMPro.FontStyles.Bold;
            var creditsTextRect = creditsText.GetComponent<RectTransform>();
            creditsTextRect.anchorMin = new Vector2(0, 0);
            creditsTextRect.anchorMax = new Vector2(1, 1);
            creditsTextRect.offsetMin = new Vector2(45, 0);
            creditsTextRect.offsetMax = new Vector2(-10, 0);
            creditsText.alignment = TextAlignmentOptions.Left;
            creditsText.color = new Color(1f, 0.9f, 0.5f, 1f);

            // Close button
            var closeBtn = new GameObject("CloseButton");
            closeBtn.transform.SetParent(topBar.transform, false);
            var closeBtnRect = closeBtn.AddComponent<RectTransform>();
            closeBtnRect.anchorMin = new Vector2(1, 0.5f);
            closeBtnRect.anchorMax = new Vector2(1, 0.5f);
            closeBtnRect.pivot = new Vector2(1, 0.5f);
            closeBtnRect.anchoredPosition = new Vector2(-15, 0);
            closeBtnRect.sizeDelta = new Vector2(40, 40);

            var closeBtnImage = closeBtn.AddComponent<Image>();
            closeBtnImage.color = new Color(0.5f, 0.2f, 0.2f, 1f);
            var btn = closeBtn.AddComponent<Button>();
            btn.targetGraphic = closeBtnImage;

            var closeBtnText = CreateText(closeBtn.transform, "Text", "X", 20);
            closeBtnText.fontStyle = TMPro.FontStyles.Bold;
            var closeBtnTextRect = closeBtnText.GetComponent<RectTransform>();
            closeBtnTextRect.anchorMin = Vector2.zero;
            closeBtnTextRect.anchorMax = Vector2.one;
            closeBtnTextRect.sizeDelta = Vector2.zero;

            // === CONTENT AREA ===
            var contentArea = new GameObject("ContentArea");
            contentArea.transform.SetParent(panel.transform, false);
            var contentAreaRect = contentArea.AddComponent<RectTransform>();
            contentAreaRect.anchorMin = new Vector2(0, 0);
            contentAreaRect.anchorMax = new Vector2(1, 1);
            contentAreaRect.offsetMin = new Vector2(10, 10);
            contentAreaRect.offsetMax = new Vector2(-10, -70);

            // === LEFT TABS PANEL (vertical category tabs) ===
            var leftTabsPanel = new GameObject("LeftTabsPanel");
            leftTabsPanel.transform.SetParent(contentArea.transform, false);
            var leftTabsRect = leftTabsPanel.AddComponent<RectTransform>();
            leftTabsRect.anchorMin = new Vector2(0, 0);
            leftTabsRect.anchorMax = new Vector2(0, 1);
            leftTabsRect.pivot = new Vector2(0, 0.5f);
            leftTabsRect.anchoredPosition = Vector2.zero;
            leftTabsRect.sizeDelta = new Vector2(140, 0);

            var leftTabsImage = leftTabsPanel.AddComponent<Image>();
            leftTabsImage.color = new Color(0.06f, 0.07f, 0.10f, 0.95f);

            // Category tabs content (vertical layout)
            var categoryTabsContent = new GameObject("CategoryTabsContent");
            categoryTabsContent.transform.SetParent(leftTabsPanel.transform, false);
            var categoryTabsRect = categoryTabsContent.AddComponent<RectTransform>();
            categoryTabsRect.anchorMin = Vector2.zero;
            categoryTabsRect.anchorMax = Vector2.one;
            categoryTabsRect.offsetMin = new Vector2(8, 10);
            categoryTabsRect.offsetMax = new Vector2(-8, -10);

            var vlgTabs = categoryTabsContent.AddComponent<VerticalLayoutGroup>();
            vlgTabs.spacing = 6;
            vlgTabs.padding = new RectOffset(0, 0, 5, 5);
            vlgTabs.childAlignment = TextAnchor.UpperCenter;
            vlgTabs.childControlWidth = true;
            vlgTabs.childControlHeight = false;
            vlgTabs.childForceExpandWidth = true;
            vlgTabs.childForceExpandHeight = false;

            // === MIDDLE: UPGRADE LIST PANEL ===
            var upgradeListPanel = new GameObject("UpgradeListPanel");
            upgradeListPanel.transform.SetParent(contentArea.transform, false);
            var upgradeListRect = upgradeListPanel.AddComponent<RectTransform>();
            upgradeListRect.anchorMin = new Vector2(0, 0);
            upgradeListRect.anchorMax = new Vector2(0, 1);
            upgradeListRect.pivot = new Vector2(0, 0.5f);
            upgradeListRect.anchoredPosition = new Vector2(150, 0);
            upgradeListRect.sizeDelta = new Vector2(280, 0);

            var upgradeListImage = upgradeListPanel.AddComponent<Image>();
            upgradeListImage.color = new Color(0.10f, 0.11f, 0.15f, 0.9f);

            // Upgrade list scroll view
            var upgradeScrollContent = CreateScrollView(upgradeListPanel.transform, "UpgradeListContent");
            var scrollViewRect = upgradeScrollContent.parent.parent.GetComponent<RectTransform>();
            scrollViewRect.offsetMin = new Vector2(8, 8);
            scrollViewRect.offsetMax = new Vector2(-8, -8);

            // === RIGHT: DETAILS PANEL ===
            var detailsPanel = new GameObject("DetailsPanel");
            detailsPanel.transform.SetParent(contentArea.transform, false);
            var detailsRect = detailsPanel.AddComponent<RectTransform>();
            detailsRect.anchorMin = new Vector2(0, 0);
            detailsRect.anchorMax = new Vector2(1, 1);
            detailsRect.pivot = new Vector2(1, 0.5f);
            detailsRect.offsetMin = new Vector2(440, 0);
            detailsRect.offsetMax = Vector2.zero;

            var detailsImage = detailsPanel.AddComponent<Image>();
            detailsImage.color = new Color(0.10f, 0.12f, 0.16f, 0.95f);

            // Create modern upgrade details
            CreateModernUpgradeDetails(detailsPanel.transform);

            panel.SetActive(false);
            Debug.Log("[MachineUISetup] Created modern UpgradeStationPanel");
        }

        private static void CreateModernUpgradeDetails(Transform parent)
        {
            // === UPGRADE NAME (large, bold) ===
            var nameText = CreateText(parent, "UpgradeName", "Select an Upgrade", 24);
            nameText.fontStyle = TMPro.FontStyles.Bold;
            var nameRect = nameText.GetComponent<RectTransform>();
            nameRect.anchorMin = new Vector2(0, 0.85f);
            nameRect.anchorMax = new Vector2(1, 1);
            nameRect.offsetMin = new Vector2(20, 0);
            nameRect.offsetMax = new Vector2(-20, -15);
            nameText.alignment = TextAlignmentOptions.TopLeft;
            nameText.color = new Color(0.95f, 0.95f, 1f, 1f);

            // === CURRENT LEVEL ===
            var levelText = CreateText(parent, "CurrentLevel", "Level 0 / 3", 14);
            var levelRect = levelText.GetComponent<RectTransform>();
            levelRect.anchorMin = new Vector2(0, 0.78f);
            levelRect.anchorMax = new Vector2(0.5f, 0.85f);
            levelRect.offsetMin = new Vector2(20, 0);
            levelRect.offsetMax = new Vector2(-10, 0);
            levelText.alignment = TextAlignmentOptions.Left;
            levelText.color = new Color(0.4f, 0.75f, 0.9f, 1f);

            // === SEPARATOR LINE ===
            var separator = new GameObject("Separator");
            separator.transform.SetParent(parent, false);
            var sepRect = separator.AddComponent<RectTransform>();
            sepRect.anchorMin = new Vector2(0, 0.76f);
            sepRect.anchorMax = new Vector2(1, 0.76f);
            sepRect.offsetMin = new Vector2(20, 0);
            sepRect.offsetMax = new Vector2(-20, 2);
            var sepImage = separator.AddComponent<Image>();
            sepImage.color = new Color(0.3f, 0.35f, 0.45f, 0.6f);

            // === DESCRIPTION ===
            var descText = CreateText(parent, "Description", "Select an upgrade from the list to view its details.", 13);
            var descRect = descText.GetComponent<RectTransform>();
            descRect.anchorMin = new Vector2(0, 0.58f);
            descRect.anchorMax = new Vector2(1, 0.76f);
            descRect.offsetMin = new Vector2(20, 5);
            descRect.offsetMax = new Vector2(-20, -5);
            descText.alignment = TextAlignmentOptions.TopLeft;
            descText.color = new Color(0.75f, 0.78f, 0.85f, 1f);
            descText.enableWordWrapping = true;

            // === EFFECT HEADER ===
            var effectHeader = CreateText(parent, "EffectHeader", "NEXT LEVEL EFFECT", 11);
            effectHeader.fontStyle = TMPro.FontStyles.Bold;
            var effectHeaderRect = effectHeader.GetComponent<RectTransform>();
            effectHeaderRect.anchorMin = new Vector2(0, 0.50f);
            effectHeaderRect.anchorMax = new Vector2(0.5f, 0.58f);
            effectHeaderRect.offsetMin = new Vector2(20, 0);
            effectHeaderRect.offsetMax = new Vector2(-10, 0);
            effectHeader.alignment = TextAlignmentOptions.BottomLeft;
            effectHeader.color = new Color(0.5f, 0.55f, 0.65f, 1f);

            // === EFFECT TEXT ===
            var effectText = CreateText(parent, "EffectText", "+5 slots", 16);
            effectText.fontStyle = TMPro.FontStyles.Bold;
            var effectTextRect = effectText.GetComponent<RectTransform>();
            effectTextRect.anchorMin = new Vector2(0, 0.42f);
            effectTextRect.anchorMax = new Vector2(0.5f, 0.50f);
            effectTextRect.offsetMin = new Vector2(20, 0);
            effectTextRect.offsetMax = new Vector2(-10, 0);
            effectText.alignment = TextAlignmentOptions.TopLeft;
            effectText.color = new Color(0.3f, 0.9f, 0.5f, 1f);

            // === COST HEADER ===
            var costHeader = CreateText(parent, "CostHeader", "COST", 11);
            costHeader.fontStyle = TMPro.FontStyles.Bold;
            var costHeaderRect = costHeader.GetComponent<RectTransform>();
            costHeaderRect.anchorMin = new Vector2(0.5f, 0.50f);
            costHeaderRect.anchorMax = new Vector2(1, 0.58f);
            costHeaderRect.offsetMin = new Vector2(10, 0);
            costHeaderRect.offsetMax = new Vector2(-20, 0);
            costHeader.alignment = TextAlignmentOptions.BottomLeft;
            costHeader.color = new Color(0.5f, 0.55f, 0.65f, 1f);

            // === COST TEXT ===
            var costText = CreateText(parent, "CostText", "50 Credits", 16);
            costText.fontStyle = TMPro.FontStyles.Bold;
            var costTextRect = costText.GetComponent<RectTransform>();
            costTextRect.anchorMin = new Vector2(0.5f, 0.38f);
            costTextRect.anchorMax = new Vector2(1, 0.50f);
            costTextRect.offsetMin = new Vector2(10, 0);
            costTextRect.offsetMax = new Vector2(-20, 0);
            costText.alignment = TextAlignmentOptions.TopLeft;
            costText.color = new Color(1f, 0.85f, 0.3f, 1f);
            costText.richText = true;

            // === PROGRESS BAR BACKGROUND ===
            var progressBg = new GameObject("LevelProgressBar");
            progressBg.transform.SetParent(parent, false);
            var progressBgRect = progressBg.AddComponent<RectTransform>();
            progressBgRect.anchorMin = new Vector2(0, 0.28f);
            progressBgRect.anchorMax = new Vector2(1, 0.36f);
            progressBgRect.offsetMin = new Vector2(20, 0);
            progressBgRect.offsetMax = new Vector2(-20, 0);

            var slider = progressBg.AddComponent<Slider>();

            var bgObj = new GameObject("Background");
            bgObj.transform.SetParent(progressBg.transform, false);
            var bgImage = bgObj.AddComponent<Image>();
            bgImage.color = new Color(0.15f, 0.17f, 0.22f, 1f);
            var bgRect = bgObj.GetComponent<RectTransform>();
            bgRect.anchorMin = Vector2.zero;
            bgRect.anchorMax = Vector2.one;
            bgRect.sizeDelta = Vector2.zero;

            var fillArea = new GameObject("Fill Area");
            fillArea.transform.SetParent(progressBg.transform, false);
            var fillAreaRect = fillArea.AddComponent<RectTransform>();
            fillAreaRect.anchorMin = Vector2.zero;
            fillAreaRect.anchorMax = Vector2.one;
            fillAreaRect.offsetMin = new Vector2(3, 3);
            fillAreaRect.offsetMax = new Vector2(-3, -3);

            var fill = new GameObject("Fill");
            fill.transform.SetParent(fillArea.transform, false);
            var fillImage = fill.AddComponent<Image>();
            fillImage.color = new Color(0.2f, 0.6f, 0.9f, 1f);
            var fillRect = fill.GetComponent<RectTransform>();
            fillRect.anchorMin = Vector2.zero;
            fillRect.anchorMax = Vector2.one;
            fillRect.sizeDelta = Vector2.zero;

            slider.fillRect = fillRect;
            slider.interactable = false;
            slider.value = 0.33f;

            // Progress text
            var progressText = CreateText(parent, "LevelProgressText", "1 / 3", 12);
            var progressTextRect = progressText.GetComponent<RectTransform>();
            progressTextRect.anchorMin = new Vector2(0, 0.22f);
            progressTextRect.anchorMax = new Vector2(1, 0.28f);
            progressTextRect.offsetMin = new Vector2(20, 0);
            progressTextRect.offsetMax = new Vector2(-20, 0);
            progressText.alignment = TextAlignmentOptions.Center;
            progressText.color = new Color(0.6f, 0.65f, 0.75f, 1f);

            // === PURCHASE BUTTON (large, centered at bottom) ===
            var purchaseBtn = new GameObject("PurchaseButton");
            purchaseBtn.transform.SetParent(parent, false);
            var purchaseBtnRect = purchaseBtn.AddComponent<RectTransform>();
            purchaseBtnRect.anchorMin = new Vector2(0.5f, 0);
            purchaseBtnRect.anchorMax = new Vector2(0.5f, 0);
            purchaseBtnRect.pivot = new Vector2(0.5f, 0);
            purchaseBtnRect.anchoredPosition = new Vector2(0, 20);
            purchaseBtnRect.sizeDelta = new Vector2(220, 55);

            var purchaseBtnImage = purchaseBtn.AddComponent<Image>();
            purchaseBtnImage.color = new Color(0.15f, 0.45f, 0.7f, 1f);

            var purchaseBtnComponent = purchaseBtn.AddComponent<Button>();
            purchaseBtnComponent.targetGraphic = purchaseBtnImage;

            // Button text
            var purchaseBtnText = CreateText(purchaseBtn.transform, "Text", "UPGRADE", 18);
            purchaseBtnText.fontStyle = TMPro.FontStyles.Bold;
            var purchaseBtnTextRect = purchaseBtnText.GetComponent<RectTransform>();
            purchaseBtnTextRect.anchorMin = Vector2.zero;
            purchaseBtnTextRect.anchorMax = Vector2.one;
            purchaseBtnTextRect.sizeDelta = Vector2.zero;
            purchaseBtnText.color = Color.white;
        }

        private static void CreateEnergyPanel(Transform parent)
        {
            // Delete existing panel if it exists (might be incomplete)
            var existing = parent.Find("EnergyPanel");
            if (existing != null)
            {
                Object.DestroyImmediate(existing.gameObject);
                Debug.Log("[MachineUISetup] Deleted existing EnergyPanel to recreate it");
            }

            var panel = new GameObject("EnergyPanel");
            panel.transform.SetParent(parent, false);
            var panelRect = panel.AddComponent<RectTransform>();
            panelRect.anchorMin = new Vector2(1, 1);
            panelRect.anchorMax = new Vector2(1, 1);
            panelRect.pivot = new Vector2(1, 1);
            panelRect.anchoredPosition = new Vector2(-20, -20);
            panelRect.sizeDelta = new Vector2(200, 60);

            var panelImage = panel.AddComponent<Image>();
            panelImage.color = new Color(0, 0, 0, 0.6f);

            // Label
            var label = CreateText(panel.transform, "EnergyLabel", "Energy", 12);
            var labelRect = label.GetComponent<RectTransform>();
            labelRect.anchorMin = new Vector2(0, 0.7f);
            labelRect.anchorMax = new Vector2(1, 1);
            labelRect.offsetMin = Vector2.zero;
            labelRect.offsetMax = Vector2.zero;
            label.color = Color.yellow;

            // Energy bar
            var barObj = new GameObject("EnergyBar");
            barObj.transform.SetParent(panel.transform, false);
            var slider = barObj.AddComponent<Slider>();
            var barRect = barObj.GetComponent<RectTransform>();
            barRect.anchorMin = new Vector2(0.05f, 0.35f);
            barRect.anchorMax = new Vector2(0.95f, 0.65f);
            barRect.offsetMin = Vector2.zero;
            barRect.offsetMax = Vector2.zero;

            var bgObj = new GameObject("Background");
            bgObj.transform.SetParent(barObj.transform, false);
            var bgImage = bgObj.AddComponent<Image>();
            bgImage.color = new Color(0.1f, 0.1f, 0.1f, 1f);
            var bgRect = bgObj.GetComponent<RectTransform>();
            bgRect.anchorMin = Vector2.zero;
            bgRect.anchorMax = Vector2.one;
            bgRect.sizeDelta = Vector2.zero;

            var fillArea = new GameObject("Fill Area");
            fillArea.transform.SetParent(barObj.transform, false);
            var fillAreaRect = fillArea.AddComponent<RectTransform>();
            fillAreaRect.anchorMin = Vector2.zero;
            fillAreaRect.anchorMax = Vector2.one;
            fillAreaRect.offsetMin = new Vector2(2, 2);
            fillAreaRect.offsetMax = new Vector2(-2, -2);

            var fill = new GameObject("Fill");
            fill.transform.SetParent(fillArea.transform, false);
            var fillImage = fill.AddComponent<Image>();
            fillImage.color = Color.green;
            var fillRect = fill.GetComponent<RectTransform>();
            fillRect.anchorMin = Vector2.zero;
            fillRect.anchorMax = Vector2.one;
            fillRect.sizeDelta = Vector2.zero;

            slider.fillRect = fillRect;
            slider.interactable = false;
            slider.value = 1f;

            // Energy text
            var energyText = CreateText(panel.transform, "EnergyText", "100/100", 14);
            var textRect = energyText.GetComponent<RectTransform>();
            textRect.anchorMin = new Vector2(0, 0);
            textRect.anchorMax = new Vector2(1, 0.35f);
            textRect.offsetMin = Vector2.zero;
            textRect.offsetMax = Vector2.zero;

            // Energy panel should be visible by default (it's a HUD)
            panel.SetActive(true);
            Debug.Log("[MachineUISetup] Created EnergyPanel");
        }

        // Helper methods
        private static GameObject CreateBasePanel(Transform parent, string name, Vector2 size)
        {
            var panel = new GameObject(name);
            panel.transform.SetParent(parent, false);
            var rect = panel.AddComponent<RectTransform>();
            rect.anchorMin = new Vector2(0.5f, 0.5f);
            rect.anchorMax = new Vector2(0.5f, 0.5f);
            rect.pivot = new Vector2(0.5f, 0.5f);
            rect.sizeDelta = size;
            rect.anchoredPosition = Vector2.zero;

            var image = panel.AddComponent<Image>();
            image.color = new Color(0.12f, 0.1f, 0.1f, 0.95f);

            return panel;
        }

        private static GameObject CreateHeader(Transform parent, string title)
        {
            var header = new GameObject("Header");
            header.transform.SetParent(parent, false);
            var rect = header.AddComponent<RectTransform>();
            rect.anchorMin = new Vector2(0, 1);
            rect.anchorMax = new Vector2(1, 1);
            rect.pivot = new Vector2(0.5f, 1);
            rect.anchoredPosition = new Vector2(0, -10);
            rect.sizeDelta = new Vector2(-20, 50);

            var image = header.AddComponent<Image>();
            image.color = new Color(0.2f, 0.18f, 0.15f, 0.95f);

            // Title
            var titleText = CreateText(header.transform, "TitleText", title, 20);
            var titleRect = titleText.GetComponent<RectTransform>();
            titleRect.anchorMin = Vector2.zero;
            titleRect.anchorMax = Vector2.one;
            titleRect.offsetMin = new Vector2(15, 5);
            titleRect.offsetMax = new Vector2(-60, -5);
            titleText.alignment = TextAlignmentOptions.Left;

            // Close button
            var closeBtn = new GameObject("CloseButton");
            closeBtn.transform.SetParent(header.transform, false);
            var closeBtnRect = closeBtn.AddComponent<RectTransform>();
            closeBtnRect.anchorMin = new Vector2(1, 0.5f);
            closeBtnRect.anchorMax = new Vector2(1, 0.5f);
            closeBtnRect.pivot = new Vector2(1, 0.5f);
            closeBtnRect.anchoredPosition = new Vector2(-10, 0);
            closeBtnRect.sizeDelta = new Vector2(40, 40);

            var closeBtnImage = closeBtn.AddComponent<Image>();
            closeBtnImage.color = new Color(0.6f, 0.2f, 0.2f, 1f);
            var btn = closeBtn.AddComponent<Button>();
            btn.targetGraphic = closeBtnImage;

            var closeBtnText = CreateText(closeBtn.transform, "Text", "X", 18);
            var closeBtnTextRect = closeBtnText.GetComponent<RectTransform>();
            closeBtnTextRect.anchorMin = Vector2.zero;
            closeBtnTextRect.anchorMax = Vector2.one;
            closeBtnTextRect.sizeDelta = Vector2.zero;

            return header;
        }

        private static GameObject CreateSidePanel(Transform parent, string name, bool isLeft, float width)
        {
            var panel = new GameObject(name);
            panel.transform.SetParent(parent, false);
            var rect = panel.AddComponent<RectTransform>();
            rect.anchorMin = new Vector2(isLeft ? 0 : 1, 0);
            rect.anchorMax = new Vector2(isLeft ? 0 : 1, 1);
            rect.pivot = new Vector2(isLeft ? 0 : 1, 0.5f);
            rect.anchoredPosition = new Vector2(isLeft ? 10 : -10, -35);
            rect.sizeDelta = new Vector2(width, -80);

            var image = panel.AddComponent<Image>();
            image.color = new Color(0.1f, 0.08f, 0.08f, 0.9f);

            return panel;
        }

        private static Transform CreateScrollView(Transform parent, string contentName)
        {
            var scrollObj = new GameObject("ScrollView");
            scrollObj.transform.SetParent(parent, false);
            var scrollRect = scrollObj.AddComponent<RectTransform>();
            scrollRect.anchorMin = Vector2.zero;
            scrollRect.anchorMax = Vector2.one;
            scrollRect.offsetMin = new Vector2(5, 5);
            scrollRect.offsetMax = new Vector2(-5, -5);

            var scrollView = scrollObj.AddComponent<ScrollRect>();
            // ScrollView doesn't need Image or Mask - only the Viewport does

            var viewport = new GameObject("Viewport");
            viewport.transform.SetParent(scrollObj.transform, false);
            var vpRect = viewport.AddComponent<RectTransform>();
            vpRect.anchorMin = Vector2.zero;
            vpRect.anchorMax = Vector2.one;
            vpRect.sizeDelta = Vector2.zero;
            vpRect.offsetMin = Vector2.zero;
            vpRect.offsetMax = Vector2.zero;

            // Viewport needs an Image for the Mask to work (can be transparent but must exist)
            var vpImage = viewport.AddComponent<Image>();
            vpImage.color = new Color(0, 0, 0, 0.01f); // Nearly transparent but present for masking
            var vpMask = viewport.AddComponent<Mask>();
            vpMask.showMaskGraphic = false;

            var content = new GameObject(contentName);
            content.transform.SetParent(viewport.transform, false);
            var contentRect = content.AddComponent<RectTransform>();
            contentRect.anchorMin = new Vector2(0, 1);
            contentRect.anchorMax = new Vector2(1, 1);
            contentRect.pivot = new Vector2(0.5f, 1);
            contentRect.sizeDelta = new Vector2(0, 0);
            contentRect.anchoredPosition = Vector2.zero;

            var vlg = content.AddComponent<VerticalLayoutGroup>();
            vlg.spacing = 5;
            vlg.padding = new RectOffset(5, 5, 5, 5);
            vlg.childAlignment = TextAnchor.UpperCenter;
            vlg.childControlWidth = true;
            vlg.childControlHeight = false;
            vlg.childForceExpandWidth = true;
            vlg.childForceExpandHeight = false;

            var csf = content.AddComponent<ContentSizeFitter>();
            csf.verticalFit = ContentSizeFitter.FitMode.PreferredSize;

            scrollView.content = contentRect;
            scrollView.viewport = vpRect;
            scrollView.vertical = true;
            scrollView.horizontal = false;

            return content.transform;
        }

        // NOTE: CreateWorkbenchDetails removed - Workbench system deprecated, using UpgradeStation instead

        private static void CreateRefineryDetails(Transform parent)
        {
            // Input info
            var inputText = CreateText(parent, "InputText", "Input:\nSelect a recipe", 14);
            var inputRect = inputText.GetComponent<RectTransform>();
            inputRect.anchorMin = new Vector2(0, 0.7f);
            inputRect.anchorMax = new Vector2(0.5f, 0.95f);
            inputRect.offsetMin = new Vector2(10, 0);
            inputRect.offsetMax = new Vector2(-5, -10);
            inputText.alignment = TextAlignmentOptions.TopLeft;

            // Output info
            var outputText = CreateText(parent, "OutputText", "Output:", 14);
            var outputRect = outputText.GetComponent<RectTransform>();
            outputRect.anchorMin = new Vector2(0.5f, 0.7f);
            outputRect.anchorMax = new Vector2(1, 0.95f);
            outputRect.offsetMin = new Vector2(5, 0);
            outputRect.offsetMax = new Vector2(-10, -10);
            outputText.alignment = TextAlignmentOptions.TopLeft;

            // Process time
            var timeText = CreateText(parent, "ProcessTime", "Process Time: 5.0s", 12);
            var timeRect = timeText.GetComponent<RectTransform>();
            timeRect.anchorMin = new Vector2(0, 0.55f);
            timeRect.anchorMax = new Vector2(1, 0.7f);
            timeRect.offsetMin = new Vector2(10, 0);
            timeRect.offsetMax = new Vector2(-10, 0);

            // Amount slider
            var sliderObj = new GameObject("AmountSlider");
            sliderObj.transform.SetParent(parent, false);
            var slider = sliderObj.AddComponent<Slider>();
            var sliderRect = sliderObj.GetComponent<RectTransform>();
            sliderRect.anchorMin = new Vector2(0.1f, 0.4f);
            sliderRect.anchorMax = new Vector2(0.9f, 0.5f);
            sliderRect.offsetMin = Vector2.zero;
            sliderRect.offsetMax = Vector2.zero;

            var bgObj = new GameObject("Background");
            bgObj.transform.SetParent(sliderObj.transform, false);
            var bgImage = bgObj.AddComponent<Image>();
            bgImage.color = new Color(0.1f, 0.1f, 0.1f, 1f);
            var bgRect = bgObj.GetComponent<RectTransform>();
            bgRect.anchorMin = Vector2.zero;
            bgRect.anchorMax = Vector2.one;
            bgRect.sizeDelta = Vector2.zero;

            var fillArea = new GameObject("Fill Area");
            fillArea.transform.SetParent(sliderObj.transform, false);
            var fillAreaRect = fillArea.AddComponent<RectTransform>();
            fillAreaRect.anchorMin = Vector2.zero;
            fillAreaRect.anchorMax = Vector2.one;
            fillAreaRect.offsetMin = new Vector2(2, 2);
            fillAreaRect.offsetMax = new Vector2(-2, -2);

            var fill = new GameObject("Fill");
            fill.transform.SetParent(fillArea.transform, false);
            var fillImage = fill.AddComponent<Image>();
            fillImage.color = new Color(0.7f, 0.5f, 0.2f, 1f);
            var fillRect = fill.GetComponent<RectTransform>();
            fillRect.anchorMin = Vector2.zero;
            fillRect.anchorMax = Vector2.one;
            fillRect.sizeDelta = Vector2.zero;

            slider.fillRect = fillRect;
            slider.minValue = 1;
            slider.maxValue = 10;
            slider.wholeNumbers = true;

            // Amount text
            var amountText = CreateText(parent, "AmountText", "Batch: 1", 12);
            var amountRect = amountText.GetComponent<RectTransform>();
            amountRect.anchorMin = new Vector2(0, 0.25f);
            amountRect.anchorMax = new Vector2(1, 0.4f);
            amountRect.offsetMin = new Vector2(10, 0);
            amountRect.offsetMax = new Vector2(-10, 0);

            // Process button
            var processBtn = CreateButton(parent, "ProcessButton", "Start Processing", new Vector2(180, 45));
            var processRect = processBtn.GetComponent<RectTransform>();
            processRect.anchorMin = new Vector2(0.5f, 0);
            processRect.anchorMax = new Vector2(0.5f, 0);
            processRect.pivot = new Vector2(0.5f, 0);
            processRect.anchoredPosition = new Vector2(0, 15);
            processBtn.GetComponent<Image>().color = new Color(0.5f, 0.35f, 0.15f, 1f);
        }

        private static void CreateTabsPanel(Transform parent)
        {
            var tabsPanel = new GameObject("TabsPanel");
            tabsPanel.transform.SetParent(parent, false);
            var tabsRect = tabsPanel.AddComponent<RectTransform>();
            tabsRect.anchorMin = new Vector2(0, 1);
            tabsRect.anchorMax = new Vector2(1, 1);
            tabsRect.pivot = new Vector2(0.5f, 1);
            tabsRect.anchoredPosition = new Vector2(0, -65);
            tabsRect.sizeDelta = new Vector2(-20, 45);

            var tabsImage = tabsPanel.AddComponent<Image>();
            tabsImage.color = new Color(0.08f, 0.1f, 0.15f, 0.9f);

            var tabsContent = new GameObject("CategoryTabsContent");
            tabsContent.transform.SetParent(tabsPanel.transform, false);
            var tabsContentRect = tabsContent.AddComponent<RectTransform>();
            tabsContentRect.anchorMin = Vector2.zero;
            tabsContentRect.anchorMax = Vector2.one;
            tabsContentRect.offsetMin = new Vector2(5, 5);
            tabsContentRect.offsetMax = new Vector2(-5, -5);

            var hlg = tabsContent.AddComponent<HorizontalLayoutGroup>();
            hlg.spacing = 10;
            hlg.childAlignment = TextAnchor.MiddleLeft;
            hlg.childControlWidth = false;
            hlg.childControlHeight = true;
            hlg.childForceExpandWidth = false;
            hlg.childForceExpandHeight = true;
        }

        private static void CreateUpgradeDetails(Transform parent)
        {
            // Upgrade name
            var nameText = CreateText(parent, "UpgradeName", "Select an Upgrade", 18);
            var nameRect = nameText.GetComponent<RectTransform>();
            nameRect.anchorMin = new Vector2(0, 0.88f);
            nameRect.anchorMax = new Vector2(1, 1);
            nameRect.offsetMin = new Vector2(10, 0);
            nameRect.offsetMax = new Vector2(-10, -10);

            // Description
            var descText = CreateText(parent, "Description", "Upgrade description.", 12);
            var descRect = descText.GetComponent<RectTransform>();
            descRect.anchorMin = new Vector2(0, 0.7f);
            descRect.anchorMax = new Vector2(1, 0.88f);
            descRect.offsetMin = new Vector2(10, 0);
            descRect.offsetMax = new Vector2(-10, 0);
            descText.alignment = TextAlignmentOptions.TopLeft;

            // Current level
            var levelText = CreateText(parent, "CurrentLevel", "Level: 0/5", 14);
            var levelRect = levelText.GetComponent<RectTransform>();
            levelRect.anchorMin = new Vector2(0, 0.55f);
            levelRect.anchorMax = new Vector2(1, 0.7f);
            levelRect.offsetMin = new Vector2(10, 0);
            levelRect.offsetMax = new Vector2(-10, 0);

            // Effect text
            var effectText = CreateText(parent, "EffectText", "Effect: +10%", 12);
            var effectRect = effectText.GetComponent<RectTransform>();
            effectRect.anchorMin = new Vector2(0, 0.4f);
            effectRect.anchorMax = new Vector2(1, 0.55f);
            effectRect.offsetMin = new Vector2(10, 0);
            effectRect.offsetMax = new Vector2(-10, 0);

            // Cost text
            var costText = CreateText(parent, "CostText", "Cost:\n10x Dirt\n5x Stone", 11);
            var costRect = costText.GetComponent<RectTransform>();
            costRect.anchorMin = new Vector2(0, 0.15f);
            costRect.anchorMax = new Vector2(0.6f, 0.4f);
            costRect.offsetMin = new Vector2(10, 0);
            costRect.offsetMax = new Vector2(-5, 0);
            costText.alignment = TextAlignmentOptions.TopLeft;
            costText.richText = true;

            // Purchase button
            var purchaseBtn = CreateButton(parent, "PurchaseButton", "Purchase", new Vector2(150, 45));
            var purchaseRect = purchaseBtn.GetComponent<RectTransform>();
            purchaseRect.anchorMin = new Vector2(0.75f, 0);
            purchaseRect.anchorMax = new Vector2(0.75f, 0);
            purchaseRect.pivot = new Vector2(0.5f, 0);
            purchaseRect.anchoredPosition = new Vector2(0, 15);
            purchaseBtn.GetComponent<Image>().color = new Color(0.2f, 0.35f, 0.6f, 1f);
        }

        private static void CreateProgressPanel(Transform parent)
        {
            var progressPanel = new GameObject("ProgressPanel");
            progressPanel.transform.SetParent(parent, false);
            var progRect = progressPanel.AddComponent<RectTransform>();
            progRect.anchorMin = new Vector2(0.5f, 0);
            progRect.anchorMax = new Vector2(0.5f, 0);
            progRect.pivot = new Vector2(0.5f, 0);
            progRect.anchoredPosition = new Vector2(0, 70);
            progRect.sizeDelta = new Vector2(400, 50);

            var progImage = progressPanel.AddComponent<Image>();
            progImage.color = new Color(0.15f, 0.12f, 0.1f, 0.95f);

            // Progress bar
            var barObj = new GameObject("ProgressBar");
            barObj.transform.SetParent(progressPanel.transform, false);
            var slider = barObj.AddComponent<Slider>();
            var barRect = barObj.GetComponent<RectTransform>();
            barRect.anchorMin = new Vector2(0, 0.5f);
            barRect.anchorMax = new Vector2(1, 0.9f);
            barRect.offsetMin = new Vector2(10, 0);
            barRect.offsetMax = new Vector2(-10, 0);

            var bgObj = new GameObject("Background");
            bgObj.transform.SetParent(barObj.transform, false);
            var bgImage = bgObj.AddComponent<Image>();
            bgImage.color = new Color(0.1f, 0.1f, 0.1f, 1f);
            var bgRect = bgObj.GetComponent<RectTransform>();
            bgRect.anchorMin = Vector2.zero;
            bgRect.anchorMax = Vector2.one;
            bgRect.sizeDelta = Vector2.zero;

            var fillArea = new GameObject("Fill Area");
            fillArea.transform.SetParent(barObj.transform, false);
            var fillAreaRect = fillArea.AddComponent<RectTransform>();
            fillAreaRect.anchorMin = Vector2.zero;
            fillAreaRect.anchorMax = Vector2.one;
            fillAreaRect.offsetMin = new Vector2(2, 2);
            fillAreaRect.offsetMax = new Vector2(-2, -2);

            var fill = new GameObject("Fill");
            fill.transform.SetParent(fillArea.transform, false);
            var fillImage = fill.AddComponent<Image>();
            fillImage.color = new Color(0.3f, 0.6f, 0.3f, 1f);
            var fillRect = fill.GetComponent<RectTransform>();
            fillRect.anchorMin = Vector2.zero;
            fillRect.anchorMax = Vector2.one;
            fillRect.sizeDelta = Vector2.zero;

            slider.fillRect = fillRect;
            slider.interactable = false;

            // Progress text
            var progressText = CreateText(progressPanel.transform, "ProgressText", "0%", 12);
            var textRect = progressText.GetComponent<RectTransform>();
            textRect.anchorMin = new Vector2(0, 0);
            textRect.anchorMax = new Vector2(1, 0.5f);
            textRect.offsetMin = Vector2.zero;
            textRect.offsetMax = Vector2.zero;

            progressPanel.SetActive(false);
        }

        private static TextMeshProUGUI CreateText(Transform parent, string name, string text, int fontSize)
        {
            var textObj = new GameObject(name);
            textObj.transform.SetParent(parent, false);
            var rect = textObj.AddComponent<RectTransform>();
            rect.anchorMin = Vector2.zero;
            rect.anchorMax = Vector2.one;
            rect.sizeDelta = Vector2.zero;

            var tmp = textObj.AddComponent<TextMeshProUGUI>();
            tmp.text = text;
            tmp.fontSize = fontSize;
            tmp.color = Color.white;
            tmp.alignment = TextAlignmentOptions.Center;
            tmp.raycastTarget = false;

            return tmp;
        }

        private static Button CreateButton(Transform parent, string name, string text, Vector2 size)
        {
            var btnObj = new GameObject(name);
            btnObj.transform.SetParent(parent, false);
            var rect = btnObj.AddComponent<RectTransform>();
            rect.sizeDelta = size;

            var image = btnObj.AddComponent<Image>();
            image.color = new Color(0.3f, 0.3f, 0.3f, 1f);

            var btn = btnObj.AddComponent<Button>();
            btn.targetGraphic = image;

            var textObj = new GameObject("Text");
            textObj.transform.SetParent(btnObj.transform, false);
            var textRect = textObj.AddComponent<RectTransform>();
            textRect.anchorMin = Vector2.zero;
            textRect.anchorMax = Vector2.one;
            textRect.sizeDelta = Vector2.zero;

            var tmp = textObj.AddComponent<TextMeshProUGUI>();
            tmp.text = text;
            tmp.fontSize = 14;
            tmp.color = Color.white;
            tmp.alignment = TextAlignmentOptions.Center;
            tmp.raycastTarget = false;

            return btn;
        }

        private static GameObject CreateRecipeButton(Transform parent, string name, string recipeName, string requirements)
        {
            var btnObj = new GameObject(name);
            btnObj.transform.SetParent(parent, false);
            var rect = btnObj.AddComponent<RectTransform>();
            rect.sizeDelta = new Vector2(200, 50);

            var layoutElement = btnObj.AddComponent<LayoutElement>();
            layoutElement.minHeight = 50;
            layoutElement.preferredHeight = 50;
            layoutElement.flexibleWidth = 1;

            var image = btnObj.AddComponent<Image>();
            image.color = new Color(0.25f, 0.4f, 0.25f, 1f); // Green tint for craftable

            var btn = btnObj.AddComponent<Button>();
            btn.targetGraphic = image;

            // Text child
            var textObj = new GameObject("Text");
            textObj.transform.SetParent(btnObj.transform, false);
            var textRect = textObj.AddComponent<RectTransform>();
            textRect.anchorMin = Vector2.zero;
            textRect.anchorMax = Vector2.one;
            textRect.anchoredPosition = Vector2.zero;
            textRect.sizeDelta = Vector2.zero;
            textRect.offsetMin = new Vector2(5, 2);
            textRect.offsetMax = new Vector2(-5, -2);

            var tmp = textObj.AddComponent<TextMeshProUGUI>();
            tmp.text = $"{recipeName}\n<size=10>{requirements}</size>";
            tmp.fontSize = 14;
            tmp.color = Color.white;
            tmp.alignment = TextAlignmentOptions.Center;
            tmp.raycastTarget = false;
            tmp.richText = true;
            tmp.enableWordWrapping = true;
            tmp.overflowMode = TextOverflowModes.Ellipsis;

            return btnObj;
        }

        private static GameObject CreateUpgradeButton(Transform parent, string name, string upgradeName, string level)
        {
            var btnObj = new GameObject(name);
            btnObj.transform.SetParent(parent, false);
            var rect = btnObj.AddComponent<RectTransform>();
            rect.sizeDelta = new Vector2(200, 45);

            var layoutElement = btnObj.AddComponent<LayoutElement>();
            layoutElement.minHeight = 45;
            layoutElement.preferredHeight = 45;
            layoutElement.flexibleWidth = 1;

            var image = btnObj.AddComponent<Image>();
            image.color = new Color(0.2f, 0.3f, 0.5f, 1f); // Blue tint

            var btn = btnObj.AddComponent<Button>();
            btn.targetGraphic = image;

            // Text child
            var textObj = new GameObject("Text");
            textObj.transform.SetParent(btnObj.transform, false);
            var textRect = textObj.AddComponent<RectTransform>();
            textRect.anchorMin = Vector2.zero;
            textRect.anchorMax = Vector2.one;
            textRect.anchoredPosition = Vector2.zero;
            textRect.sizeDelta = Vector2.zero;
            textRect.offsetMin = new Vector2(5, 2);
            textRect.offsetMax = new Vector2(-5, -2);

            var tmp = textObj.AddComponent<TextMeshProUGUI>();
            tmp.text = $"{upgradeName}\n<size=10>{level}</size>";
            tmp.fontSize = 13;
            tmp.color = Color.white;
            tmp.alignment = TextAlignmentOptions.Center;
            tmp.raycastTarget = false;
            tmp.richText = true;
            tmp.enableWordWrapping = true;

            return btnObj;
        }
    }
}
#endif
