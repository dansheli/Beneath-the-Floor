using UnityEngine;
using UnityEngine.UI;
using TMPro;
using BeneathTheFloor.Energy;
using BeneathTheFloor.Crafting;
using BeneathTheFloor.Economy;

namespace BeneathTheFloor.Machines
{
    /// <summary>
    /// Sets up machines in the basement scene using Asset Store models.
    /// Place this on an empty GameObject in BasementScene and it will
    /// automatically create and position all machines with proper UI.
    /// </summary>
    public class BasementMachineSetup : MonoBehaviour
    {
        [Header("Machine Positions")]
        // NOTE: Workbench removed - using UpgradeStation as the single upgrade/crafting point
        [SerializeField] private Vector3 refineryPosition = new Vector3(3f, -2.5f, 2f);
        [SerializeField] private Vector3 upgradeStationPosition = new Vector3(0f, -2.5f, -3f);
        [SerializeField] private Vector3 energyGeneratorPosition = new Vector3(-3f, -2.5f, -2f);
        [SerializeField] private Vector3 tradeTerminalPosition = new Vector3(3f, -2.5f, -2f);

        [Header("Machine Rotations")]
        [SerializeField] private float refineryRotation = 180f;
        [SerializeField] private float upgradeStationRotation = 0f;
        [SerializeField] private float energyGeneratorRotation = 90f;
        [SerializeField] private float tradeTerminalRotation = -90f;

        [Header("Setup Options")]
        [SerializeField] private bool setupOnStart = true;
        [SerializeField] private bool removeOldMachines = true;
        [SerializeField] private bool createUI = true;
        [SerializeField] private bool createManagers = true;
        [SerializeField] private bool skipIfBakedMachinesExist = true;

        [Header("Asset References (Optional - will auto-load)")]
        [SerializeField] private GameObject refineryAsset;
        [SerializeField] private GameObject upgradeStationAsset;
        [SerializeField] private GameObject energyGeneratorAsset;

        // Created objects
        private GameObject refinery;
        private GameObject upgradeStation;
        private GameObject energyGenerator;
        private GameObject tradeTerminal;
        private Canvas machineUICanvas;

        private void Start()
        {
            Debug.Log($"[BasementMachineSetup] Start() called on '{gameObject.name}'. setupOnStart={setupOnStart}");

            if (setupOnStart)
            {
                SetupBasementMachines();
            }
            else
            {
                Debug.LogWarning("[BasementMachineSetup] setupOnStart is FALSE - machines will NOT be created automatically!");
            }
        }

        [ContextMenu("Setup Basement Machines")]
        public void SetupBasementMachines()
        {
            Debug.Log("[BasementMachineSetup] Starting basement machine setup...");

            // Check if baked machines already exist (from BakeBasementToScene)
            if (skipIfBakedMachinesExist && AreBakedMachinesPresent())
            {
                Debug.Log("[BasementMachineSetup] Baked machines detected in scene. Skipping runtime creation.");
                Debug.Log("[BasementMachineSetup] To force recreation, set 'Skip If Baked Machines Exist' to false.");
                return;
            }

            if (removeOldMachines)
            {
                RemoveOldMachines();
            }

            if (createManagers)
            {
                SetupManagers();
            }

            // Create Asset Machine Prefab Builder if it doesn't exist
            var builder = FindObjectOfType<AssetMachinePrefabBuilder>();
            if (builder == null)
            {
                GameObject builderObj = new GameObject("AssetMachinePrefabBuilder");
                builder = builderObj.AddComponent<AssetMachinePrefabBuilder>();
            }

            // Create machines (Workbench removed - using UpgradeStation as the single upgrade point)
            CreateRefinery(builder);
            CreateUpgradeStation(builder);
            CreateEnergyGenerator(builder);
            CreateTradeTerminal();

            if (createUI)
            {
                SetupMachineUI();
                SetupEconomyUI();
            }

            Debug.Log("[BasementMachineSetup] Basement machine setup complete!");
        }

        /// <summary>
        /// Checks if baked machines are already present in the scene.
        /// Used to skip runtime creation when machines were baked via Editor tool.
        /// </summary>
        private bool AreBakedMachinesPresent()
        {
            // Check for machines under Machines container (baked structure)
            GameObject machinesContainer = GameObject.Find("Machines");
            if (machinesContainer != null && machinesContainer.transform.childCount > 0)
            {
                // Check for at least one machine with proper component
                var refinery = machinesContainer.GetComponentInChildren<Refinery>();
                var upgradeStation = machinesContainer.GetComponentInChildren<UpgradeStation>();

                if (refinery != null || upgradeStation != null)
                {
                    Debug.Log($"[BasementMachineSetup] Found baked machines under 'Machines' container");
                    return true;
                }
            }

            // Also check for individual machines at root level with components
            string[] machineNames = { "Refinery", "UpgradeStation", "EnergyGenerator", "TradeTerminal" };
            int foundCount = 0;

            foreach (string name in machineNames)
            {
                GameObject obj = GameObject.Find(name);
                if (obj != null)
                {
                    // Verify it has the proper machine component (not just an empty placeholder)
                    if (obj.GetComponent<Refinery>() != null ||
                        obj.GetComponent<UpgradeStation>() != null ||
                        obj.GetComponent<EnergyGenerator>() != null ||
                        obj.GetComponent<Economy.TradeTerminal>() != null)
                    {
                        foundCount++;
                    }
                }
            }

            // If at least 3 machines with components exist, consider it baked
            if (foundCount >= 3)
            {
                Debug.Log($"[BasementMachineSetup] Found {foundCount} baked machines at root level");
                return true;
            }

            return false;
        }

        private void RemoveOldMachines()
        {
            // Remove any existing machine objects (Workbench excluded - no longer used)
            string[] machineNames = { "Refinery", "UpgradeStation", "EnergyGenerator", "TradeTerminal" };

            foreach (string name in machineNames)
            {
                GameObject existing = GameObject.Find(name);
                if (existing != null)
                {
                    Debug.Log($"[BasementMachineSetup] Removing old {name}");
                    DestroyImmediate(existing);
                }
            }
        }

        private void SetupManagers()
        {
            // Ensure EnergyManager exists
            if (EnergyManager.Instance == null)
            {
                var energyManagerObj = new GameObject("EnergyManager");
                energyManagerObj.AddComponent<EnergyManager>();
                Debug.Log("[BasementMachineSetup] Created EnergyManager");
            }

            // Ensure CraftingManager exists
            if (CraftingManager.Instance == null)
            {
                var craftingManagerObj = new GameObject("CraftingManager");
                craftingManagerObj.AddComponent<CraftingManager>();
                Debug.Log("[BasementMachineSetup] Created CraftingManager");
            }

            // Ensure CurrencyManager exists
            if (CurrencyManager.Instance == null)
            {
                var currencyManagerObj = new GameObject("CurrencyManager");
                currencyManagerObj.AddComponent<CurrencyManager>();
                Debug.Log("[BasementMachineSetup] Created CurrencyManager");
            }
        }

        // NOTE: CreateWorkbench removed - Workbench system is deprecated, using UpgradeStation instead

        private void CreateRefinery(AssetMachinePrefabBuilder builder)
        {
            refinery = builder.BuildRefinery();
            refinery.transform.position = refineryPosition;
            refinery.transform.rotation = Quaternion.Euler(0f, refineryRotation, 0f);

            Debug.Log($"[Machines] Placed Refinery in BasementScene at position {refineryPosition}");
        }

        private void CreateUpgradeStation(AssetMachinePrefabBuilder builder)
        {
            upgradeStation = builder.BuildUpgradeStation();
            upgradeStation.transform.position = upgradeStationPosition;
            upgradeStation.transform.rotation = Quaternion.Euler(0f, upgradeStationRotation, 0f);

            Debug.Log($"[Machines] Placed UpgradeStation in BasementScene at position {upgradeStationPosition}");
        }

        private void CreateEnergyGenerator(AssetMachinePrefabBuilder builder)
        {
            energyGenerator = builder.BuildEnergyGenerator();
            energyGenerator.transform.position = energyGeneratorPosition;
            energyGenerator.transform.rotation = Quaternion.Euler(0f, energyGeneratorRotation, 0f);

            Debug.Log($"[Machines] Placed EnergyGenerator in BasementScene at position {energyGeneratorPosition}");
        }

        private void CreateTradeTerminal()
        {
            // Create Trade Terminal GameObject
            tradeTerminal = new GameObject("TradeTerminal");
            tradeTerminal.transform.position = tradeTerminalPosition;
            tradeTerminal.transform.rotation = Quaternion.Euler(0f, tradeTerminalRotation, 0f);

            // Add visual mesh (a placeholder terminal look)
            GameObject visual = GameObject.CreatePrimitive(PrimitiveType.Cube);
            visual.name = "TerminalBody";
            visual.transform.SetParent(tradeTerminal.transform);
            visual.transform.localPosition = new Vector3(0, 0.6f, 0);
            visual.transform.localScale = new Vector3(0.8f, 1.2f, 0.4f);

            // Set material color (green-ish for trade terminal)
            var renderer = visual.GetComponent<Renderer>();
            if (renderer != null)
            {
                renderer.material = new Material(Shader.Find("Standard"));
                renderer.material.color = new Color(0.2f, 0.35f, 0.25f);
            }

            // Remove collider from visual (we'll use a trigger on parent)
            var visualCollider = visual.GetComponent<Collider>();
            if (visualCollider != null) DestroyImmediate(visualCollider);

            // Add screen (a simple plane)
            GameObject screen = GameObject.CreatePrimitive(PrimitiveType.Quad);
            screen.name = "Screen";
            screen.transform.SetParent(tradeTerminal.transform);
            screen.transform.localPosition = new Vector3(0, 0.8f, 0.21f);
            screen.transform.localScale = new Vector3(0.5f, 0.4f, 1f);

            var screenRenderer = screen.GetComponent<Renderer>();
            if (screenRenderer != null)
            {
                screenRenderer.material = new Material(Shader.Find("Standard"));
                screenRenderer.material.color = new Color(0.1f, 0.3f, 0.15f);
                screenRenderer.material.SetFloat("_Metallic", 0f);
                screenRenderer.material.SetFloat("_Glossiness", 0.8f);
                // Make it emissive
                screenRenderer.material.EnableKeyword("_EMISSION");
                screenRenderer.material.SetColor("_EmissionColor", new Color(0.1f, 0.4f, 0.2f));
            }

            var screenCollider = screen.GetComponent<Collider>();
            if (screenCollider != null) DestroyImmediate(screenCollider);

            // Add collider for interaction
            BoxCollider collider = tradeTerminal.AddComponent<BoxCollider>();
            collider.center = new Vector3(0, 0.6f, 0);
            collider.size = new Vector3(1f, 1.4f, 0.6f);

            // Add TradeTerminal component
            var terminal = tradeTerminal.AddComponent<TradeTerminal>();

            Debug.Log($"[Machines] Created TradeTerminal at position {tradeTerminalPosition}");
        }

        private void SetupEconomyUI()
        {
            Debug.Log($"[BasementMachineSetup] SetupEconomyUI called. machineUICanvas={(machineUICanvas != null ? machineUICanvas.name : "NULL")}");

            // Make sure we have a canvas
            if (machineUICanvas == null)
            {
                Debug.LogError("[BasementMachineSetup] machineUICanvas is NULL! Cannot create economy UI.");
                machineUICanvas = FindMachineUICanvas();
                if (machineUICanvas == null)
                {
                    CreateMachineUICanvas();
                }
            }

            // Create TradeTerminalUI if it doesn't exist
            if (FindObjectOfType<TradeTerminalUI>() == null)
            {
                var uiObj = new GameObject("TradeTerminalUI");
                uiObj.transform.SetParent(machineUICanvas.transform, false);
                uiObj.AddComponent<TradeTerminalUI>();
                Debug.Log("[BasementMachineSetup] Created TradeTerminalUI under " + machineUICanvas.name);
            }
            else
            {
                Debug.Log("[BasementMachineSetup] TradeTerminalUI already exists, skipping creation");
            }

            // Create CurrencyHUD if it doesn't exist
            if (FindObjectOfType<CurrencyHUD>() == null)
            {
                // Find HUD canvas (should be separate from machine UI canvas)
                Canvas hudCanvas = null;
                Canvas[] canvases = FindObjectsOfType<Canvas>();
                Debug.Log($"[BasementMachineSetup] Found {canvases.Length} canvases in scene");

                foreach (var canvas in canvases)
                {
                    Debug.Log($"[BasementMachineSetup] - Canvas: '{canvas.name}' sortingOrder={canvas.sortingOrder}");
                    if (canvas.name.Contains("HUD") || canvas.sortingOrder < 100)
                    {
                        hudCanvas = canvas;
                        break;
                    }
                }

                if (hudCanvas == null)
                {
                    hudCanvas = machineUICanvas; // Fallback
                    Debug.Log("[BasementMachineSetup] Using machineUICanvas as fallback for CurrencyHUD");
                }

                var hudObj = new GameObject("CurrencyHUD");
                hudObj.transform.SetParent(hudCanvas.transform, false);
                hudObj.AddComponent<CurrencyHUD>();
                Debug.Log($"[BasementMachineSetup] Created CurrencyHUD under {hudCanvas.name}");
            }
        }

        private void SetupMachineUI()
        {
            // Find or create UI canvas
            machineUICanvas = FindMachineUICanvas();
            if (machineUICanvas == null)
            {
                CreateMachineUICanvas();
            }

            // Ensure EventSystem exists
            if (FindObjectOfType<UnityEngine.EventSystems.EventSystem>() == null)
            {
                var eventSystemObj = new GameObject("EventSystem");
                eventSystemObj.AddComponent<UnityEngine.EventSystems.EventSystem>();
                eventSystemObj.AddComponent<UnityEngine.EventSystems.StandaloneInputModule>();
                Debug.Log("[BasementMachineSetup] Created EventSystem");
            }

            // Create UI panels for each machine (Workbench UI removed)
            CreateRefineryUI();
            CreateUpgradeStationUI();
            CreateEnergyUI();

            Debug.Log("[BasementMachineSetup] Machine UI setup complete");
        }

        private Canvas FindMachineUICanvas()
        {
            // Look for existing MachineUICanvas
            GameObject canvasObj = GameObject.Find("MachineUICanvas");
            if (canvasObj != null)
            {
                return canvasObj.GetComponent<Canvas>();
            }

            // Try to find any canvas
            Canvas[] canvases = FindObjectsOfType<Canvas>();
            foreach (var canvas in canvases)
            {
                if (canvas.renderMode == RenderMode.ScreenSpaceOverlay)
                {
                    return canvas;
                }
            }

            return null;
        }

        private void CreateMachineUICanvas()
        {
            var canvasObj = new GameObject("MachineUICanvas");
            machineUICanvas = canvasObj.AddComponent<Canvas>();
            machineUICanvas.renderMode = RenderMode.ScreenSpaceOverlay;
            machineUICanvas.sortingOrder = 100; // Above HUD

            var scaler = canvasObj.AddComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920, 1080);

            canvasObj.AddComponent<GraphicRaycaster>();

            Debug.Log("[BasementMachineSetup] Created MachineUICanvas");
        }

        // NOTE: CreateWorkbenchUI removed - Workbench system is deprecated

        private void CreateRefineryUI()
        {
            if (FindObjectOfType<RefineryUI>() != null) return;

            var uiObj = new GameObject("RefineryUI");
            uiObj.transform.SetParent(machineUICanvas.transform, false);
            var refineryUI = uiObj.AddComponent<RefineryUI>();

            // Create panel
            GameObject panel = CreateUIPanel("RefineryPanel", uiObj.transform, "Refinery", new Color(0.15f, 0.15f, 0.2f, 0.95f));
            panel.SetActive(false);

            Debug.Log("[MachineUI] Created RefineryUI");
        }

        private void CreateUpgradeStationUI()
        {
            if (FindObjectOfType<UpgradeStationUI>() != null) return;

            var uiObj = new GameObject("UpgradeStationUI");
            uiObj.transform.SetParent(machineUICanvas.transform, false);
            var upgradeStationUI = uiObj.AddComponent<UpgradeStationUI>();

            // Create panel
            GameObject panel = CreateUIPanel("UpgradeStationPanel", uiObj.transform, "Upgrade Station", new Color(0.1f, 0.15f, 0.25f, 0.95f));
            panel.SetActive(false);

            Debug.Log("[MachineUI] Created UpgradeStationUI");
        }

        private void CreateEnergyUI()
        {
            if (FindObjectOfType<EnergyUI>() != null) return;

            var uiObj = new GameObject("EnergyUI");
            uiObj.transform.SetParent(machineUICanvas.transform, false);
            uiObj.AddComponent<EnergyUI>();

            Debug.Log("[MachineUI] Created EnergyUI");
        }

        private GameObject CreateUIPanel(string name, Transform parent, string title, Color backgroundColor)
        {
            // Create panel container
            GameObject panel = new GameObject(name);
            panel.transform.SetParent(parent, false);

            RectTransform panelRect = panel.AddComponent<RectTransform>();
            panelRect.anchorMin = new Vector2(0.5f, 0.5f);
            panelRect.anchorMax = new Vector2(0.5f, 0.5f);
            panelRect.pivot = new Vector2(0.5f, 0.5f);
            panelRect.sizeDelta = new Vector2(600, 500);

            // Background
            Image bgImage = panel.AddComponent<Image>();
            bgImage.color = backgroundColor;

            // Title bar
            GameObject titleBar = new GameObject("TitleBar");
            titleBar.transform.SetParent(panel.transform, false);
            RectTransform titleRect = titleBar.AddComponent<RectTransform>();
            titleRect.anchorMin = new Vector2(0, 1);
            titleRect.anchorMax = new Vector2(1, 1);
            titleRect.pivot = new Vector2(0.5f, 1);
            titleRect.sizeDelta = new Vector2(0, 50);
            Image titleBg = titleBar.AddComponent<Image>();
            titleBg.color = new Color(0.1f, 0.1f, 0.1f, 1f);

            // Title text
            GameObject titleTextObj = new GameObject("TitleText");
            titleTextObj.transform.SetParent(titleBar.transform, false);
            RectTransform titleTextRect = titleTextObj.AddComponent<RectTransform>();
            titleTextRect.anchorMin = Vector2.zero;
            titleTextRect.anchorMax = Vector2.one;
            titleTextRect.sizeDelta = Vector2.zero;
            titleTextRect.offsetMin = new Vector2(20, 0);
            titleTextRect.offsetMax = new Vector2(-60, 0);
            TextMeshProUGUI titleText = titleTextObj.AddComponent<TextMeshProUGUI>();
            titleText.text = title;
            titleText.fontSize = 24;
            titleText.fontStyle = FontStyles.Bold;
            titleText.alignment = TextAlignmentOptions.MidlineLeft;
            titleText.color = Color.white;

            // Close button
            GameObject closeBtn = new GameObject("CloseButton");
            closeBtn.transform.SetParent(titleBar.transform, false);
            RectTransform closeBtnRect = closeBtn.AddComponent<RectTransform>();
            closeBtnRect.anchorMin = new Vector2(1, 0.5f);
            closeBtnRect.anchorMax = new Vector2(1, 0.5f);
            closeBtnRect.pivot = new Vector2(1, 0.5f);
            closeBtnRect.anchoredPosition = new Vector2(-10, 0);
            closeBtnRect.sizeDelta = new Vector2(40, 40);
            Image closeBtnImage = closeBtn.AddComponent<Image>();
            closeBtnImage.color = new Color(0.8f, 0.2f, 0.2f, 1f);
            Button closeBtnComponent = closeBtn.AddComponent<Button>();

            // Close button text
            GameObject closeBtnTextObj = new GameObject("Text");
            closeBtnTextObj.transform.SetParent(closeBtn.transform, false);
            RectTransform closeBtnTextRect = closeBtnTextObj.AddComponent<RectTransform>();
            closeBtnTextRect.anchorMin = Vector2.zero;
            closeBtnTextRect.anchorMax = Vector2.one;
            closeBtnTextRect.sizeDelta = Vector2.zero;
            TextMeshProUGUI closeBtnText = closeBtnTextObj.AddComponent<TextMeshProUGUI>();
            closeBtnText.text = "X";
            closeBtnText.fontSize = 24;
            closeBtnText.fontStyle = FontStyles.Bold;
            closeBtnText.alignment = TextAlignmentOptions.Center;
            closeBtnText.color = Color.white;

            // Content area
            GameObject content = new GameObject("Content");
            content.transform.SetParent(panel.transform, false);
            RectTransform contentRect = content.AddComponent<RectTransform>();
            contentRect.anchorMin = Vector2.zero;
            contentRect.anchorMax = Vector2.one;
            contentRect.sizeDelta = Vector2.zero;
            contentRect.offsetMin = new Vector2(20, 20);
            contentRect.offsetMax = new Vector2(-20, -60);

            // Info text in content
            GameObject infoTextObj = new GameObject("InfoText");
            infoTextObj.transform.SetParent(content.transform, false);
            RectTransform infoTextRect = infoTextObj.AddComponent<RectTransform>();
            infoTextRect.anchorMin = Vector2.zero;
            infoTextRect.anchorMax = Vector2.one;
            infoTextRect.sizeDelta = Vector2.zero;
            TextMeshProUGUI infoText = infoTextObj.AddComponent<TextMeshProUGUI>();
            infoText.text = $"<b>{title}</b>\n\nPress ESC or click X to close.\n\nUI functionality will be fully implemented\nwhen recipes and upgrades are configured.";
            infoText.fontSize = 16;
            infoText.alignment = TextAlignmentOptions.TopLeft;
            infoText.color = Color.white;

            return panel;
        }

        [ContextMenu("Reset Machine Positions")]
        public void ResetMachinePositions()
        {
            // NOTE: Workbench removed - using UpgradeStation as the single upgrade/crafting point

            if (refinery != null)
            {
                refinery.transform.position = refineryPosition;
                refinery.transform.rotation = Quaternion.Euler(0f, refineryRotation, 0f);
            }

            if (upgradeStation != null)
            {
                upgradeStation.transform.position = upgradeStationPosition;
                upgradeStation.transform.rotation = Quaternion.Euler(0f, upgradeStationRotation, 0f);
            }

            if (energyGenerator != null)
            {
                energyGenerator.transform.position = energyGeneratorPosition;
                energyGenerator.transform.rotation = Quaternion.Euler(0f, energyGeneratorRotation, 0f);
            }

            if (tradeTerminal != null)
            {
                tradeTerminal.transform.position = tradeTerminalPosition;
                tradeTerminal.transform.rotation = Quaternion.Euler(0f, tradeTerminalRotation, 0f);
            }

            Debug.Log("[BasementMachineSetup] Machine positions reset");
        }
    }
}
