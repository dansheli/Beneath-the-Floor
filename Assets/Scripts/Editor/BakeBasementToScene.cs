using UnityEngine;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine.UI;
using TMPro;
using System.IO;
using System.Collections.Generic;

namespace BeneathTheFloor.Editor
{
    /// <summary>
    /// Bakes the procedural basement into static scene objects.
    /// Run via: Tools > Beneath The Floor > Bake Basement To Scene
    ///
    /// After baking:
    /// - All basement elements appear in Edit Mode
    /// - You can manually move and arrange machines
    /// - Play Mode will not regenerate/replace these objects
    /// </summary>
    public class BakeBasementToScene : EditorWindow
    {
        // Configuration
        private static readonly string BackupFolder = "Assets/_Backup_Before_Bake";

        // Basement dimensions (from BasementResizer defaults)
        private static float basementWidth = 15f;
        private static float basementLength = 15f;
        private static float basementHeight = 6f;
        private static float floorY = -3f;
        private static float wallThickness = 0.3f;

        // Machine positions (from BasementMachineSetup)
        // NOTE: Workbench removed - using UpgradeStation as single upgrade point
        private static Vector3 refineryPosition = new Vector3(3f, -2.5f, 2f);
        private static Vector3 upgradeStationPosition = new Vector3(0f, -2.5f, -3f);
        private static Vector3 energyGeneratorPosition = new Vector3(-3f, -2.5f, -2f);
        private static Vector3 tradeTerminalPosition = new Vector3(3f, -2.5f, -2f);

        private static float refineryRotation = 180f;
        private static float upgradeStationRotation = 0f;
        private static float energyGeneratorRotation = 90f;
        private static float tradeTerminalRotation = -90f;

        // Floor opening (should match terrain horizontalExtent)
        private static float holeSize = 12f;

        // Tracking
        private static List<string> bakedObjects = new List<string>();
        private static List<string> modifiedScripts = new List<string>();

        [MenuItem("Tools/Beneath The Floor/Bake Basement To Scene")]
        public static void BakeBasement()
        {
            bakedObjects.Clear();
            modifiedScripts.Clear();

            // Create backup
            CreateBackup();

            // Register undo
            Undo.SetCurrentGroupName("Bake Basement To Scene");
            int undoGroup = Undo.GetCurrentGroup();

            try
            {
                // Find or create Basement root
                GameObject basement = FindOrCreateBasementRoot();

                // Bake structure
                BakeBasementStructure(basement);

                // Bake machines
                BakeMachines(basement);

                // Bake managers
                BakeManagers();

                // Bake UI
                BakeUI();

                // Bake lighting
                BakeLighting(basement);

                // Bake floor ring (with opening for digging)
                BakeFloorRing(basement);

                // Mark scene dirty
                EditorSceneManager.MarkSceneDirty(UnityEngine.SceneManagement.SceneManager.GetActiveScene());

                Undo.CollapseUndoOperations(undoGroup);

                // Print report
                PrintReport();

                Debug.Log("[BakeBasementToScene] Bake complete! Scene saved.");
            }
            catch (System.Exception ex)
            {
                Debug.LogError($"[BakeBasementToScene] Error during bake: {ex.Message}\n{ex.StackTrace}");
                Undo.CollapseUndoOperations(undoGroup);
            }
        }

        private static void CreateBackup()
        {
            // Ensure backup folder exists
            if (!Directory.Exists(Application.dataPath.Replace("Assets", "") + BackupFolder))
            {
                Directory.CreateDirectory(Application.dataPath.Replace("Assets", "") + BackupFolder);
                AssetDatabase.Refresh();
            }

            // Copy current scene
            var scene = UnityEngine.SceneManagement.SceneManager.GetActiveScene();
            string scenePath = scene.path;
            string backupPath = $"{BackupFolder}/{scene.name}_PreBake_{System.DateTime.Now:yyyyMMdd_HHmmss}.unity";

            if (!string.IsNullOrEmpty(scenePath))
            {
                AssetDatabase.CopyAsset(scenePath, backupPath);
                Debug.Log($"[BakeBasementToScene] Created backup: {backupPath}");
            }
        }

        private static GameObject FindOrCreateBasementRoot()
        {
            GameObject basement = GameObject.Find("Basement");

            if (basement == null)
            {
                basement = new GameObject("Basement");
                basement.transform.position = Vector3.zero;
                basement.transform.rotation = Quaternion.identity;
                Undo.RegisterCreatedObjectUndo(basement, "Create Basement");
                bakedObjects.Add("Basement (root)");
                Debug.Log("[BakeBasementToScene] Created Basement root object");
            }
            else
            {
                Debug.Log("[BakeBasementToScene] Found existing Basement root object");
            }

            return basement;
        }

        #region Structure Baking

        private static void BakeBasementStructure(GameObject basement)
        {
            // Floor
            BakeFloor(basement);

            // Ceiling
            BakeCeiling(basement);

            // Walls
            BakeWalls(basement);

            // Stairs
            BakeStairs(basement);

            // Spawn Point
            BakeSpawnPoint(basement);
        }

        private static void BakeFloor(GameObject basement)
        {
            // Check if exists
            Transform existing = basement.transform.Find("BasementFloor");
            if (existing != null)
            {
                Debug.Log("[BakeBasementToScene] BasementFloor already exists, skipping");
                return;
            }

            GameObject floor = GameObject.CreatePrimitive(PrimitiveType.Cube);
            floor.name = "BasementFloor";
            floor.transform.SetParent(basement.transform);
            floor.transform.localPosition = new Vector3(0, floorY, 0);
            floor.transform.localScale = new Vector3(basementWidth, 0.2f, basementLength);

            // Set layer
            int groundLayer = LayerMask.NameToLayer("Ground");
            if (groundLayer != -1) floor.layer = groundLayer;

            // Apply material
            ApplyBasementMaterial(floor, new Color(0.4f, 0.35f, 0.3f));

            Undo.RegisterCreatedObjectUndo(floor, "Create BasementFloor");
            bakedObjects.Add("BasementFloor");
        }

        private static void BakeCeiling(GameObject basement)
        {
            Transform existing = basement.transform.Find("BasementCeiling");
            if (existing != null)
            {
                Debug.Log("[BakeBasementToScene] BasementCeiling already exists, skipping");
                return;
            }

            float ceilingY = floorY + basementHeight;

            GameObject ceiling = GameObject.CreatePrimitive(PrimitiveType.Cube);
            ceiling.name = "BasementCeiling";
            ceiling.transform.SetParent(basement.transform);
            ceiling.transform.localPosition = new Vector3(0, ceilingY, 0);
            ceiling.transform.localScale = new Vector3(basementWidth, 0.2f, basementLength);

            ApplyBasementMaterial(ceiling, new Color(0.5f, 0.5f, 0.5f));

            Undo.RegisterCreatedObjectUndo(ceiling, "Create BasementCeiling");
            bakedObjects.Add("BasementCeiling");
        }

        private static void BakeWalls(GameObject basement)
        {
            float wallHeight = basementHeight;
            float wallY = floorY + wallHeight / 2f;

            // North wall
            BakeWall(basement, "Basement_Wall_North",
                new Vector3(0, wallY, basementLength / 2f),
                new Vector3(basementWidth, wallHeight, wallThickness));

            // South wall
            BakeWall(basement, "Basement_Wall_South",
                new Vector3(0, wallY, -basementLength / 2f),
                new Vector3(basementWidth, wallHeight, wallThickness));

            // East wall
            BakeWall(basement, "Basement_Wall_East",
                new Vector3(basementWidth / 2f, wallY, 0),
                new Vector3(wallThickness, wallHeight, basementLength));

            // West wall
            BakeWall(basement, "Basement_Wall_West",
                new Vector3(-basementWidth / 2f, wallY, 0),
                new Vector3(wallThickness, wallHeight, basementLength));
        }

        private static void BakeWall(GameObject basement, string name, Vector3 position, Vector3 scale)
        {
            Transform existing = basement.transform.Find(name);
            if (existing != null)
            {
                Debug.Log($"[BakeBasementToScene] {name} already exists, skipping");
                return;
            }

            GameObject wall = GameObject.CreatePrimitive(PrimitiveType.Cube);
            wall.name = name;
            wall.transform.SetParent(basement.transform);
            wall.transform.localPosition = position;
            wall.transform.localScale = scale;

            ApplyBasementMaterial(wall, new Color(0.45f, 0.4f, 0.35f));

            Undo.RegisterCreatedObjectUndo(wall, $"Create {name}");
            bakedObjects.Add(name);
        }

        private static void BakeStairs(GameObject basement)
        {
            Transform existing = basement.transform.Find("StairsUp");
            if (existing != null)
            {
                Debug.Log("[BakeBasementToScene] StairsUp already exists, skipping");
                return;
            }

            GameObject stairs = new GameObject("StairsUp");
            stairs.transform.SetParent(basement.transform);
            stairs.transform.localPosition = new Vector3(-basementWidth / 2f + 1.5f, floorY, basementLength / 2f - 1f);
            stairs.transform.localRotation = Quaternion.Euler(0, 90, 0);

            // Create steps
            int stepCount = 10;
            float stepHeight = 0.25f;
            float stepDepth = 0.3f;
            float stepWidth = 1.2f;

            for (int i = 0; i < stepCount; i++)
            {
                GameObject step = GameObject.CreatePrimitive(PrimitiveType.Cube);
                step.name = $"Step_{i}";
                step.transform.SetParent(stairs.transform);
                step.transform.localPosition = new Vector3(0, i * stepHeight + stepHeight / 2f, -i * stepDepth);
                step.transform.localScale = new Vector3(stepWidth, stepHeight, stepDepth);

                ApplyBasementMaterial(step, new Color(0.35f, 0.3f, 0.25f));
            }

            Undo.RegisterCreatedObjectUndo(stairs, "Create StairsUp");
            bakedObjects.Add("StairsUp (with 10 steps)");
        }

        private static void BakeSpawnPoint(GameObject basement)
        {
            GameObject existing = GameObject.Find("BasementSpawnPoint");
            if (existing != null)
            {
                Debug.Log("[BakeBasementToScene] BasementSpawnPoint already exists, skipping");
                return;
            }

            GameObject spawnPoint = new GameObject("BasementSpawnPoint");
            spawnPoint.transform.SetParent(basement.transform);
            spawnPoint.transform.localPosition = new Vector3(0, floorY + 1f, 0);

            Undo.RegisterCreatedObjectUndo(spawnPoint, "Create BasementSpawnPoint");
            bakedObjects.Add("BasementSpawnPoint");
        }

        #endregion

        #region Machine Baking

        private static void BakeMachines(GameObject basement)
        {
            // Create Machines container
            GameObject machinesContainer = FindOrCreateChild(basement, "Machines");

            // Bake each machine (NOTE: Workbench removed - using UpgradeStation as single upgrade point)
            BakeRefinery(machinesContainer);
            BakeUpgradeStation(machinesContainer);
            BakeEnergyGenerator(machinesContainer);
            BakeTradeTerminal(machinesContainer);
        }

        private static GameObject FindOrCreateChild(GameObject parent, string name)
        {
            Transform existing = parent.transform.Find(name);
            if (existing != null)
                return existing.gameObject;

            GameObject child = new GameObject(name);
            child.transform.SetParent(parent.transform);
            child.transform.localPosition = Vector3.zero;
            Undo.RegisterCreatedObjectUndo(child, $"Create {name}");
            return child;
        }

        // NOTE: BakeWorkbench removed - Workbench system deprecated, using UpgradeStation instead

        private static void BakeRefinery(GameObject parent)
        {
            if (GameObject.Find("Refinery") != null)
            {
                Debug.Log("[BakeBasementToScene] Refinery already exists, skipping");
                return;
            }

            GameObject refinery = CreateMachineWithFallback("Refinery", parent.transform);
            refinery.transform.position = refineryPosition;
            refinery.transform.rotation = Quaternion.Euler(0, refineryRotation, 0);

            // Add components
            refinery.AddComponent<Machines.Refinery>();
            refinery.AddComponent<Energy.EnergyConsumer>();
            refinery.AddComponent<Machines.MachineVisualFeedback>();
            refinery.AddComponent<Audio.MachineAudio>();

            BoxCollider col = refinery.GetComponent<BoxCollider>();
            if (col == null) col = refinery.AddComponent<BoxCollider>();
            col.center = new Vector3(0, 1f, 0);
            col.size = new Vector3(1.5f, 2f, 1.5f);

            CreateFallbackRefineryVisual(refinery);

            Undo.RegisterCreatedObjectUndo(refinery, "Create Refinery");
            bakedObjects.Add("Refinery");
        }

        private static void BakeUpgradeStation(GameObject parent)
        {
            if (GameObject.Find("UpgradeStation") != null)
            {
                Debug.Log("[BakeBasementToScene] UpgradeStation already exists, skipping");
                return;
            }

            GameObject upgradeStation = CreateMachineWithFallback("UpgradeStation", parent.transform);
            upgradeStation.transform.position = upgradeStationPosition;
            upgradeStation.transform.rotation = Quaternion.Euler(0, upgradeStationRotation, 0);

            upgradeStation.AddComponent<Machines.UpgradeStation>();
            upgradeStation.AddComponent<Machines.MachineVisualFeedback>();
            upgradeStation.AddComponent<Audio.MachineAudio>();

            BoxCollider col = upgradeStation.GetComponent<BoxCollider>();
            if (col == null) col = upgradeStation.AddComponent<BoxCollider>();
            col.center = new Vector3(0, 1f, 0);
            col.size = new Vector3(1.8f, 2f, 1.2f);

            CreateFallbackUpgradeStationVisual(upgradeStation);

            Undo.RegisterCreatedObjectUndo(upgradeStation, "Create UpgradeStation");
            bakedObjects.Add("UpgradeStation");
        }

        private static void BakeEnergyGenerator(GameObject parent)
        {
            if (GameObject.Find("EnergyGenerator") != null)
            {
                Debug.Log("[BakeBasementToScene] EnergyGenerator already exists, skipping");
                return;
            }

            GameObject generator = CreateMachineWithFallback("EnergyGenerator", parent.transform);
            generator.transform.position = energyGeneratorPosition;
            generator.transform.rotation = Quaternion.Euler(0, energyGeneratorRotation, 0);

            generator.AddComponent<Machines.EnergyGenerator>();
            generator.AddComponent<Machines.MachineVisualFeedback>();
            generator.AddComponent<Audio.MachineAudio>();

            BoxCollider col = generator.GetComponent<BoxCollider>();
            if (col == null) col = generator.AddComponent<BoxCollider>();
            col.center = new Vector3(0, 0.8f, 0);
            col.size = new Vector3(1.2f, 1.6f, 1.2f);

            CreateFallbackEnergyGeneratorVisual(generator);

            Undo.RegisterCreatedObjectUndo(generator, "Create EnergyGenerator");
            bakedObjects.Add("EnergyGenerator");
        }

        private static void BakeTradeTerminal(GameObject parent)
        {
            if (GameObject.Find("TradeTerminal") != null)
            {
                Debug.Log("[BakeBasementToScene] TradeTerminal already exists, skipping");
                return;
            }

            GameObject terminal = new GameObject("TradeTerminal");
            terminal.transform.SetParent(parent.transform);
            terminal.transform.position = tradeTerminalPosition;
            terminal.transform.rotation = Quaternion.Euler(0, tradeTerminalRotation, 0);

            terminal.AddComponent<Economy.TradeTerminal>();

            BoxCollider col = terminal.AddComponent<BoxCollider>();
            col.center = new Vector3(0, 0.6f, 0);
            col.size = new Vector3(1f, 1.4f, 0.6f);

            // Visual: body
            GameObject body = GameObject.CreatePrimitive(PrimitiveType.Cube);
            body.name = "TerminalBody";
            body.transform.SetParent(terminal.transform);
            body.transform.localPosition = new Vector3(0, 0.6f, 0);
            body.transform.localScale = new Vector3(0.8f, 1.2f, 0.4f);
            Object.DestroyImmediate(body.GetComponent<Collider>());
            ApplyMaterial(body, new Color(0.2f, 0.35f, 0.25f));

            // Visual: screen
            GameObject screen = GameObject.CreatePrimitive(PrimitiveType.Quad);
            screen.name = "Screen";
            screen.transform.SetParent(terminal.transform);
            screen.transform.localPosition = new Vector3(0, 0.8f, 0.21f);
            screen.transform.localScale = new Vector3(0.5f, 0.4f, 1f);
            Object.DestroyImmediate(screen.GetComponent<Collider>());

            var screenRenderer = screen.GetComponent<Renderer>();
            if (screenRenderer != null)
            {
                Material mat = CreateMaterial(new Color(0.1f, 0.3f, 0.15f));
                mat.EnableKeyword("_EMISSION");
                mat.SetColor("_EmissionColor", new Color(0.1f, 0.4f, 0.2f));
                screenRenderer.sharedMaterial = mat;
            }

            Undo.RegisterCreatedObjectUndo(terminal, "Create TradeTerminal");
            bakedObjects.Add("TradeTerminal");
        }

        private static GameObject CreateMachineWithFallback(string name, Transform parent)
        {
            GameObject machine = new GameObject(name);
            machine.transform.SetParent(parent);
            return machine;
        }

        #endregion

        #region Fallback Visuals

        // NOTE: CreateFallbackWorkbenchVisual removed - Workbench system deprecated

        private static void CreateFallbackRefineryVisual(GameObject root)
        {
            Color color = new Color(0.35f, 0.35f, 0.4f);

            GameObject body = GameObject.CreatePrimitive(PrimitiveType.Cube);
            body.name = "Body";
            body.transform.SetParent(root.transform);
            body.transform.localPosition = new Vector3(0, 0.75f, 0);
            body.transform.localScale = new Vector3(1.2f, 1.5f, 1.2f);
            Object.DestroyImmediate(body.GetComponent<Collider>());
            ApplyMaterial(body, color);

            GameObject chimney = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
            chimney.name = "Chimney";
            chimney.transform.SetParent(root.transform);
            chimney.transform.localPosition = new Vector3(0.3f, 1.75f, 0.3f);
            chimney.transform.localScale = new Vector3(0.3f, 0.5f, 0.3f);
            Object.DestroyImmediate(chimney.GetComponent<Collider>());
            ApplyMaterial(chimney, color * 0.8f);
        }

        private static void CreateFallbackUpgradeStationVisual(GameObject root)
        {
            Color color = new Color(0.25f, 0.3f, 0.45f);

            GameObject basePlat = GameObject.CreatePrimitive(PrimitiveType.Cube);
            basePlat.name = "Base";
            basePlat.transform.SetParent(root.transform);
            basePlat.transform.localPosition = new Vector3(0, 0.1f, 0);
            basePlat.transform.localScale = new Vector3(1.5f, 0.2f, 1.5f);
            Object.DestroyImmediate(basePlat.GetComponent<Collider>());
            ApplyMaterial(basePlat, color * 0.8f);

            GameObject pedestal = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
            pedestal.name = "Pedestal";
            pedestal.transform.SetParent(root.transform);
            pedestal.transform.localPosition = new Vector3(0, 0.6f, 0);
            pedestal.transform.localScale = new Vector3(0.6f, 0.8f, 0.6f);
            Object.DestroyImmediate(pedestal.GetComponent<Collider>());
            ApplyMaterial(pedestal, color);

            GameObject console = GameObject.CreatePrimitive(PrimitiveType.Cube);
            console.name = "Console";
            console.transform.SetParent(root.transform);
            console.transform.localPosition = new Vector3(0, 0.8f, -0.5f);
            console.transform.localRotation = Quaternion.Euler(-15f, 0, 0);
            console.transform.localScale = new Vector3(0.8f, 1.2f, 0.3f);
            Object.DestroyImmediate(console.GetComponent<Collider>());
            ApplyMaterial(console, color * 1.1f);
        }

        private static void CreateFallbackEnergyGeneratorVisual(GameObject root)
        {
            Color color = new Color(0.3f, 0.35f, 0.3f);

            GameObject body = GameObject.CreatePrimitive(PrimitiveType.Cube);
            body.name = "Body";
            body.transform.SetParent(root.transform);
            body.transform.localPosition = new Vector3(0, 0.6f, 0);
            body.transform.localScale = new Vector3(1f, 1.2f, 0.8f);
            Object.DestroyImmediate(body.GetComponent<Collider>());
            ApplyMaterial(body, color);

            GameObject cylinder = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
            cylinder.name = "Generator";
            cylinder.transform.SetParent(root.transform);
            cylinder.transform.localPosition = new Vector3(0, 1.4f, 0);
            cylinder.transform.localScale = new Vector3(0.5f, 0.4f, 0.5f);
            Object.DestroyImmediate(cylinder.GetComponent<Collider>());
            ApplyMaterial(cylinder, new Color(0.5f, 0.5f, 0.2f));
        }

        #endregion

        #region Manager Baking

        private static void BakeManagers()
        {
            // Find or create GameManager
            GameObject gameManager = GameObject.Find("GameManager");
            if (gameManager == null)
            {
                gameManager = new GameObject("GameManager");
                Undo.RegisterCreatedObjectUndo(gameManager, "Create GameManager");
                bakedObjects.Add("GameManager");
            }

            // EnergyManager
            if (Object.FindObjectOfType<Energy.EnergyManager>() == null)
            {
                GameObject em = new GameObject("EnergyManager");
                em.transform.SetParent(gameManager.transform);
                em.AddComponent<Energy.EnergyManager>();
                Undo.RegisterCreatedObjectUndo(em, "Create EnergyManager");
                bakedObjects.Add("EnergyManager");
            }

            // CraftingManager
            if (Object.FindObjectOfType<Crafting.CraftingManager>() == null)
            {
                GameObject cm = new GameObject("CraftingManager");
                cm.transform.SetParent(gameManager.transform);
                cm.AddComponent<Crafting.CraftingManager>();
                Undo.RegisterCreatedObjectUndo(cm, "Create CraftingManager");
                bakedObjects.Add("CraftingManager");
            }

            // CurrencyManager
            if (Object.FindObjectOfType<Economy.CurrencyManager>() == null)
            {
                GameObject currMgr = new GameObject("CurrencyManager");
                currMgr.transform.SetParent(gameManager.transform);
                currMgr.AddComponent<Economy.CurrencyManager>();
                Undo.RegisterCreatedObjectUndo(currMgr, "Create CurrencyManager");
                bakedObjects.Add("CurrencyManager");
            }
        }

        #endregion

        #region UI Baking

        private static void BakeUI()
        {
            // Find or create MachineUICanvas
            Canvas machineCanvas = null;
            GameObject canvasObj = GameObject.Find("MachineUICanvas");

            if (canvasObj == null)
            {
                canvasObj = new GameObject("MachineUICanvas");
                machineCanvas = canvasObj.AddComponent<Canvas>();
                machineCanvas.renderMode = RenderMode.ScreenSpaceOverlay;
                machineCanvas.sortingOrder = 100;

                var scaler = canvasObj.AddComponent<CanvasScaler>();
                scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
                scaler.referenceResolution = new Vector2(1920, 1080);

                canvasObj.AddComponent<GraphicRaycaster>();

                Undo.RegisterCreatedObjectUndo(canvasObj, "Create MachineUICanvas");
                bakedObjects.Add("MachineUICanvas");
            }
            else
            {
                machineCanvas = canvasObj.GetComponent<Canvas>();
            }

            // Ensure EventSystem exists
            if (Object.FindObjectOfType<UnityEngine.EventSystems.EventSystem>() == null)
            {
                GameObject eventSystem = new GameObject("EventSystem");
                eventSystem.AddComponent<UnityEngine.EventSystems.EventSystem>();
                eventSystem.AddComponent<UnityEngine.EventSystems.StandaloneInputModule>();
                Undo.RegisterCreatedObjectUndo(eventSystem, "Create EventSystem");
                bakedObjects.Add("EventSystem");
            }

            // UI components (NOTE: WorkbenchUI removed - Workbench system deprecated)
            BakeRefineryUI(canvasObj.transform);
            BakeUpgradeStationUI(canvasObj.transform);
            BakeEnergyUI(canvasObj.transform);
            BakeTradeTerminalUI(canvasObj.transform);
            BakeCurrencyHUD();
        }

        // NOTE: BakeWorkbenchUI removed - Workbench system deprecated, using UpgradeStation instead

        private static void BakeRefineryUI(Transform parent)
        {
            if (Object.FindObjectOfType<Machines.RefineryUI>() != null) return;

            GameObject ui = new GameObject("RefineryUI");
            ui.transform.SetParent(parent, false);
            ui.AddComponent<Machines.RefineryUI>();

            CreateUIPanel("RefineryPanel", ui.transform, "Refinery", new Color(0.15f, 0.15f, 0.2f, 0.95f));

            Undo.RegisterCreatedObjectUndo(ui, "Create RefineryUI");
            bakedObjects.Add("RefineryUI");
        }

        private static void BakeUpgradeStationUI(Transform parent)
        {
            if (Object.FindObjectOfType<Machines.UpgradeStationUI>() != null) return;

            GameObject ui = new GameObject("UpgradeStationUI");
            ui.transform.SetParent(parent, false);
            ui.AddComponent<Machines.UpgradeStationUI>();

            CreateUIPanel("UpgradeStationPanel", ui.transform, "Upgrade Station", new Color(0.1f, 0.15f, 0.25f, 0.95f));

            Undo.RegisterCreatedObjectUndo(ui, "Create UpgradeStationUI");
            bakedObjects.Add("UpgradeStationUI");
        }

        private static void BakeEnergyUI(Transform parent)
        {
            if (Object.FindObjectOfType<Energy.EnergyUI>() != null) return;

            GameObject ui = new GameObject("EnergyUI");
            ui.transform.SetParent(parent, false);
            ui.AddComponent<Energy.EnergyUI>();

            Undo.RegisterCreatedObjectUndo(ui, "Create EnergyUI");
            bakedObjects.Add("EnergyUI");
        }

        private static void BakeTradeTerminalUI(Transform parent)
        {
            if (Object.FindObjectOfType<Economy.TradeTerminalUI>() != null) return;

            GameObject ui = new GameObject("TradeTerminalUI");
            ui.transform.SetParent(parent, false);
            ui.AddComponent<Economy.TradeTerminalUI>();

            Undo.RegisterCreatedObjectUndo(ui, "Create TradeTerminalUI");
            bakedObjects.Add("TradeTerminalUI");
        }

        private static void BakeCurrencyHUD()
        {
            if (Object.FindObjectOfType<Economy.CurrencyHUD>() != null) return;

            // Find HUD canvas or create under MachineUICanvas
            Canvas hudCanvas = null;
            GameObject hudCanvasObj = GameObject.Find("HUDCanvas");
            if (hudCanvasObj != null)
                hudCanvas = hudCanvasObj.GetComponent<Canvas>();

            if (hudCanvas == null)
            {
                hudCanvasObj = GameObject.Find("MachineUICanvas");
                if (hudCanvasObj != null)
                    hudCanvas = hudCanvasObj.GetComponent<Canvas>();
            }

            if (hudCanvas != null)
            {
                GameObject hud = new GameObject("CurrencyHUD");
                hud.transform.SetParent(hudCanvas.transform, false);
                hud.AddComponent<Economy.CurrencyHUD>();

                Undo.RegisterCreatedObjectUndo(hud, "Create CurrencyHUD");
                bakedObjects.Add("CurrencyHUD");
            }
        }

        private static void CreateUIPanel(string name, Transform parent, string title, Color bgColor)
        {
            GameObject panel = new GameObject(name);
            panel.transform.SetParent(parent, false);

            RectTransform rect = panel.AddComponent<RectTransform>();
            rect.anchorMin = new Vector2(0.5f, 0.5f);
            rect.anchorMax = new Vector2(0.5f, 0.5f);
            rect.pivot = new Vector2(0.5f, 0.5f);
            rect.sizeDelta = new Vector2(600, 500);

            Image bg = panel.AddComponent<Image>();
            bg.color = bgColor;

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
            closeBtn.AddComponent<Button>();

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

            // Start hidden
            panel.SetActive(false);
        }

        #endregion

        #region Lighting

        private static void BakeLighting(GameObject basement)
        {
            BakeLight(basement, "BasementLight_1", new Vector3(-3f, floorY + basementHeight - 0.5f, 0), Color.white, 15f, LightType.Point);
            BakeLight(basement, "BasementLight_2", new Vector3(3f, floorY + basementHeight - 0.5f, 0), Color.white, 15f, LightType.Point);
            BakeLight(basement, "BasementLight_Stairs", new Vector3(-basementWidth / 2f + 1.5f, floorY + 2f, basementLength / 2f - 1f), new Color(1f, 0.9f, 0.7f), 8f, LightType.Point);
        }

        private static void BakeLight(GameObject parent, string name, Vector3 position, Color color, float range, LightType type)
        {
            Transform existing = parent.transform.Find(name);
            if (existing != null)
            {
                Debug.Log($"[BakeBasementToScene] {name} already exists, skipping");
                return;
            }

            // Also check at root
            if (GameObject.Find(name) != null)
            {
                Debug.Log($"[BakeBasementToScene] {name} already exists at root, skipping");
                return;
            }

            GameObject lightObj = new GameObject(name);
            lightObj.transform.SetParent(parent.transform);
            lightObj.transform.position = position;

            Light light = lightObj.AddComponent<Light>();
            light.type = type;
            light.color = color;
            light.range = range;
            light.intensity = 1.5f;

            Undo.RegisterCreatedObjectUndo(lightObj, $"Create {name}");
            bakedObjects.Add(name);
        }

        #endregion

        #region Floor Ring

        private static void BakeFloorRing(GameObject basement)
        {
            // Check if floor ring already exists
            if (GameObject.Find("BasementFloor_Ring") != null)
            {
                Debug.Log("[BakeBasementToScene] BasementFloor_Ring already exists, skipping");
                return;
            }

            // Get floor reference
            Transform floorTransform = basement.transform.Find("BasementFloor");
            if (floorTransform == null)
            {
                Debug.LogWarning("[BakeBasementToScene] BasementFloor not found, cannot create floor ring");
                return;
            }

            GameObject floor = floorTransform.gameObject;
            Vector3 center = floor.transform.position;
            Vector3 scale = floor.transform.localScale;

            float fullSizeX = scale.x;
            float fullSizeZ = scale.z;
            float thickness = scale.y;

            // Disable original floor renderer and collider
            Renderer floorRenderer = floor.GetComponent<Renderer>();
            if (floorRenderer != null) floorRenderer.enabled = false;

            Collider floorCollider = floor.GetComponent<Collider>();
            if (floorCollider != null) floorCollider.enabled = false;

            // Create floor ring parent
            GameObject floorRing = new GameObject("BasementFloor_Ring");
            floorRing.transform.SetParent(basement.transform);
            floorRing.transform.position = center;

            int groundLayer = LayerMask.NameToLayer("Ground");
            if (groundLayer == -1) groundLayer = 0;

            float halfSize = fullSizeX / 2f;
            float holeHalf = holeSize / 2f;

            // Get material from original floor or create default
            Material floorMat = null;
            if (floorRenderer != null && floorRenderer.sharedMaterial != null)
            {
                floorMat = new Material(floorRenderer.sharedMaterial);
            }
            else
            {
                floorMat = CreateMaterial(new Color(0.4f, 0.35f, 0.3f));
            }

            // North segment
            float northDepth = halfSize - holeHalf;
            if (northDepth > 0.01f)
            {
                CreateFloorSegment(floorRing.transform, "Floor_North",
                    new Vector3(center.x, floorY, center.z + (holeHalf + halfSize) / 2f),
                    new Vector3(fullSizeX, thickness, northDepth),
                    groundLayer, floorMat);
            }

            // South segment
            float southDepth = halfSize - holeHalf;
            if (southDepth > 0.01f)
            {
                CreateFloorSegment(floorRing.transform, "Floor_South",
                    new Vector3(center.x, floorY, center.z - (holeHalf + halfSize) / 2f),
                    new Vector3(fullSizeX, thickness, southDepth),
                    groundLayer, floorMat);
            }

            // East segment
            float eastWidth = halfSize - holeHalf;
            if (eastWidth > 0.01f)
            {
                CreateFloorSegment(floorRing.transform, "Floor_East",
                    new Vector3(center.x + (holeHalf + halfSize) / 2f, floorY, center.z),
                    new Vector3(eastWidth, thickness, holeSize),
                    groundLayer, floorMat);
            }

            // West segment
            float westWidth = halfSize - holeHalf;
            if (westWidth > 0.01f)
            {
                CreateFloorSegment(floorRing.transform, "Floor_West",
                    new Vector3(center.x - (holeHalf + halfSize) / 2f, floorY, center.z),
                    new Vector3(westWidth, thickness, holeSize),
                    groundLayer, floorMat);
            }

            Undo.RegisterCreatedObjectUndo(floorRing, "Create BasementFloor_Ring");
            bakedObjects.Add("BasementFloor_Ring (Floor_North, Floor_South, Floor_East, Floor_West)");
        }

        private static void CreateFloorSegment(Transform parent, string name, Vector3 position, Vector3 scale, int layer, Material mat)
        {
            GameObject segment = GameObject.CreatePrimitive(PrimitiveType.Cube);
            segment.name = name;
            segment.transform.SetParent(parent, true);
            segment.transform.position = position;
            segment.transform.localScale = scale;
            segment.layer = layer;

            if (mat != null)
            {
                segment.GetComponent<Renderer>().sharedMaterial = mat;
            }
        }

        #endregion

        #region Utilities

        private static void ApplyBasementMaterial(GameObject obj, Color color)
        {
            ApplyMaterial(obj, color);
        }

        private static void ApplyMaterial(GameObject obj, Color color)
        {
            Renderer renderer = obj.GetComponent<Renderer>();
            if (renderer != null)
            {
                renderer.sharedMaterial = CreateMaterial(color);
            }
        }

        private static Material CreateMaterial(Color color)
        {
            Shader shader = Shader.Find("Universal Render Pipeline/Lit");
            if (shader == null) shader = Shader.Find("Standard");

            Material mat = new Material(shader);
            mat.SetColor("_BaseColor", color);
            mat.color = color;

            if (mat.HasProperty("_Smoothness"))
                mat.SetFloat("_Smoothness", 0.2f);

            return mat;
        }

        #endregion

        #region Report

        private static void PrintReport()
        {
            Debug.Log("========================================");
            Debug.Log("BAKE BASEMENT TO SCENE - REPORT");
            Debug.Log("========================================");

            Debug.Log("");
            Debug.Log("[BAKED OBJECTS]");
            foreach (var obj in bakedObjects)
            {
                Debug.Log($"  + {obj}");
            }

            Debug.Log("");
            Debug.Log("[RUNTIME SCRIPTS TO MODIFY]");
            Debug.Log("  • BasementMachineSetup.cs - Set setupOnStart = false");
            Debug.Log("  • DiggingV2RuntimeSetup.cs - Will skip machine/floor creation if baked objects exist");
            Debug.Log("  • BasementDigOpeningSetup.cs - Will skip if BasementFloor_Ring exists");

            Debug.Log("");
            Debug.Log("[NEXT STEPS]");
            Debug.Log("  1. Save the scene (Ctrl+S)");
            Debug.Log("  2. Find BasementMachineSetup in scene, set 'Setup On Start' = false");
            Debug.Log("  3. Enter Play Mode to verify machines persist");
            Debug.Log("  4. Move machines freely in Edit Mode");

            Debug.Log("");
            Debug.Log("[BACKUP LOCATION]");
            Debug.Log($"  {BackupFolder}/");

            Debug.Log("========================================");
        }

        #endregion
    }
}
