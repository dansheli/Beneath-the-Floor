using UnityEngine;
using BeneathTheFloor.Machines;
using BeneathTheFloor.Energy;
using BeneathTheFloor.Crafting;

namespace BeneathTheFloor.Setup
{
    /// <summary>
    /// Automatically sets up machine components on GameObjects in the scene.
    /// Attach this to an empty GameObject and assign the machine references.
    /// </summary>
    public class MachineSetup : MonoBehaviour
    {
        [Header("Machine GameObjects")]
        // NOTE: Workbench removed - using UpgradeStation as single upgrade point
        [SerializeField] private GameObject refineryObject;
        [SerializeField] private GameObject upgradeStationObject;
        [SerializeField] private GameObject energyGeneratorObject;

        [Header("UI Canvas")]
        [SerializeField] private Canvas machineUICanvas;

        [Header("Auto Setup")]
        [SerializeField] private bool setupOnAwake = true;
        [SerializeField] private bool createUIIfMissing = true;

        private void Awake()
        {
            if (setupOnAwake)
            {
                SetupAllMachines();
            }
        }

        [ContextMenu("Setup All Machines")]
        public void SetupAllMachines()
        {
            // NOTE: SetupWorkbench removed - Workbench system deprecated
            SetupRefinery();
            SetupUpgradeStation();
            SetupEnergyGenerator();
            SetupManagers();
            SetupUI();

            Debug.Log("[MachineSetup] All machines configured!");
        }

        private void SetupRefinery()
        {
            if (refineryObject == null)
            {
                refineryObject = GameObject.Find("Refinery");
            }

            if (refineryObject != null)
            {
                if (refineryObject.GetComponent<Refinery>() == null)
                {
                    refineryObject.AddComponent<Refinery>();
                }

                if (refineryObject.GetComponent<Collider>() == null)
                {
                    var col = refineryObject.AddComponent<BoxCollider>();
                    col.size = new Vector3(1.5f, 2f, 1.5f);
                }

                // Add energy consumer
                if (refineryObject.GetComponent<EnergyConsumer>() == null)
                {
                    refineryObject.AddComponent<EnergyConsumer>();
                }

                SetInteractableLayer(refineryObject);

                Debug.Log("[MachineSetup] Refinery configured");
            }
        }

        private void SetupUpgradeStation()
        {
            if (upgradeStationObject == null)
            {
                upgradeStationObject = GameObject.Find("UpgradeStation");
            }

            if (upgradeStationObject != null)
            {
                if (upgradeStationObject.GetComponent<UpgradeStation>() == null)
                {
                    upgradeStationObject.AddComponent<UpgradeStation>();
                }

                if (upgradeStationObject.GetComponent<Collider>() == null)
                {
                    var col = upgradeStationObject.AddComponent<BoxCollider>();
                    col.size = new Vector3(1.5f, 2f, 0.5f);
                }

                SetInteractableLayer(upgradeStationObject);

                Debug.Log("[MachineSetup] Upgrade Station configured");
            }
        }

        private void SetupEnergyGenerator()
        {
            if (energyGeneratorObject == null)
            {
                energyGeneratorObject = GameObject.Find("EnergyGenerator");
            }

            if (energyGeneratorObject != null)
            {
                if (energyGeneratorObject.GetComponent<EnergySource>() == null)
                {
                    var source = energyGeneratorObject.AddComponent<EnergySource>();
                }

                if (energyGeneratorObject.GetComponent<Collider>() == null)
                {
                    var col = energyGeneratorObject.AddComponent<BoxCollider>();
                    col.size = new Vector3(1f, 1.5f, 1f);
                }

                Debug.Log("[MachineSetup] Energy Generator configured");
            }
        }

        private void SetupManagers()
        {
            // Ensure EnergyManager exists
            if (EnergyManager.Instance == null)
            {
                var energyManagerObj = new GameObject("EnergyManager");
                energyManagerObj.AddComponent<EnergyManager>();
                Debug.Log("[MachineSetup] Created EnergyManager");
            }

            // Ensure CraftingManager exists
            if (CraftingManager.Instance == null)
            {
                var craftingManagerObj = new GameObject("CraftingManager");
                craftingManagerObj.AddComponent<CraftingManager>();
                Debug.Log("[MachineSetup] Created CraftingManager");
            }
        }

        private void SetupUI()
        {
            if (!createUIIfMissing) return;

            // Find or create UI canvas
            if (machineUICanvas == null)
            {
                machineUICanvas = FindObjectOfType<Canvas>();

                if (machineUICanvas == null)
                {
                    var canvasObj = new GameObject("MachineUICanvas");
                    machineUICanvas = canvasObj.AddComponent<Canvas>();
                    machineUICanvas.renderMode = RenderMode.ScreenSpaceOverlay;
                    canvasObj.AddComponent<UnityEngine.UI.CanvasScaler>();
                    canvasObj.AddComponent<UnityEngine.UI.GraphicRaycaster>();
                }
            }

            // Add UI components (WorkbenchUI removed - system deprecated)
            if (FindObjectOfType<RefineryUI>() == null)
            {
                var refineryUI = new GameObject("RefineryUI");
                refineryUI.transform.SetParent(machineUICanvas.transform, false);
                refineryUI.AddComponent<RefineryUI>();
                Debug.Log("[MachineSetup] Created RefineryUI");
            }

            if (FindObjectOfType<UpgradeStationUI>() == null)
            {
                var upgradeUI = new GameObject("UpgradeStationUI");
                upgradeUI.transform.SetParent(machineUICanvas.transform, false);
                upgradeUI.AddComponent<UpgradeStationUI>();
                Debug.Log("[MachineSetup] Created UpgradeStationUI");
            }

            if (FindObjectOfType<EnergyUI>() == null)
            {
                var energyUI = new GameObject("EnergyUI");
                energyUI.transform.SetParent(machineUICanvas.transform, false);
                energyUI.AddComponent<EnergyUI>();
                Debug.Log("[MachineSetup] Created EnergyUI");
            }
        }

        private void SetInteractableLayer(GameObject obj)
        {
            int interactableLayer = LayerMask.NameToLayer("Interactable");
            if (interactableLayer == -1)
            {
                // Try "Default" if Interactable layer doesn't exist
                interactableLayer = 0;
            }
            obj.layer = interactableLayer;
        }

        [ContextMenu("Find Machine Objects")]
        public void FindMachineObjects()
        {
            // NOTE: Workbench removed - using UpgradeStation as single upgrade point
            refineryObject = GameObject.Find("Refinery");
            upgradeStationObject = GameObject.Find("UpgradeStation");
            energyGeneratorObject = GameObject.Find("EnergyGenerator");

            Debug.Log($"[MachineSetup] Found: " +
                     $"Refinery={refineryObject != null}, " +
                     $"UpgradeStation={upgradeStationObject != null}, " +
                     $"EnergyGenerator={energyGeneratorObject != null}");
        }
    }
}
