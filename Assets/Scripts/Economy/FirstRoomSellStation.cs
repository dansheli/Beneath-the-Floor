using UnityEngine;
using BeneathTheFloor.Interaction;
using BeneathTheFloor.Inventory;
using BeneathTheFloor.ResourceSystem;
using BeneathTheFloor.World;

namespace BeneathTheFloor.Economy
{
    /// <summary>
    /// First Room Sell Station - Similar to TradeTerminal but uses the sci-fi styled UI.
    /// Requires the engine to be activated (crystal inserted) to function.
    /// Implements IInteractable for player interaction.
    /// </summary>
    [RequireComponent(typeof(BoxCollider))]
    public class FirstRoomSellStation : MonoBehaviour, IInteractable
    {
        [Header("Station Settings")]
        [SerializeField] private string stationName = "Sell Station";

        [Header("Engine Requirement")]
        [Tooltip("Reference to the Engine that must be activated for this station to work")]
        [SerializeField] private EngineActivationInteract engine;

        [Header("Visual Feedback")]
        [SerializeField] private GameObject highlightEffect;
        [SerializeField] private Light stationLight;
        [SerializeField] private Color idleColor = new Color(0.0f, 0.6f, 0.7f); // Cyan
        [SerializeField] private Color activeColor = new Color(0.0f, 0.85f, 1.0f);
        [SerializeField] private Color offlineColor = new Color(0.2f, 0.2f, 0.2f); // Gray when offline
        [SerializeField] private float lightIntensity = 1.5f;

        [Header("Audio")]
        [SerializeField] private AudioSource audioSource;
        [SerializeField] private AudioClip openSound;
        [SerializeField] private AudioClip closeSound;
        [SerializeField] private AudioClip sellSound;

        private bool isOpen = false;
        private bool isHovered = false;

        public static FirstRoomSellStation CurrentStation { get; private set; }

        /// <summary>
        /// Returns true if the engine is activated and the station can be used.
        /// </summary>
        private bool IsEngineActivated
        {
            get
            {
                if (engine == null)
                {
                    // Try to find engine if not assigned
                    engine = FindObjectOfType<EngineActivationInteract>();
                }
                return engine != null && engine.IsActivated;
            }
        }

        // IInteractable implementation
        public bool CanInteract
        {
            get
            {
                if (isOpen) return false;
                if (!IsEngineActivated) return false;
                return true;
            }
        }

        public string GetInteractionText()
        {
            if (!IsEngineActivated)
            {
                return "Sell Station [OFFLINE - Insert crystal into engine]";
            }
            return $"Press E to use {stationName}";
        }

        public void Interact(GameObject interactor)
        {
            if (isOpen) return;
            if (!IsEngineActivated) return;
            OpenStation();
        }

        public void OnHoverEnter()
        {
            isHovered = true;
            SetHighlight(true);
        }

        public void OnHoverExit()
        {
            isHovered = false;
            if (!isOpen)
            {
                SetHighlight(false);
            }
        }

        private void Awake()
        {
            var collider = GetComponent<BoxCollider>();
            if (collider == null)
            {
                collider = gameObject.AddComponent<BoxCollider>();
            }

            if (audioSource == null)
            {
                audioSource = GetComponent<AudioSource>();
                if (audioSource == null)
                {
                    audioSource = gameObject.AddComponent<AudioSource>();
                    audioSource.playOnAwake = false;
                    audioSource.spatialBlend = 1f;
                }
            }
        }

        private void Start()
        {
            // Try to find engine if not assigned
            if (engine == null)
            {
                engine = FindObjectOfType<EngineActivationInteract>();
            }

            SetupVisuals();
            SetHighlight(false);
        }

        private void Update()
        {
            if (isOpen && Input.GetKeyDown(KeyCode.Escape))
            {
                UI.UIState.ConsumeEscape();
                CloseStation();
            }

            // Update visual state based on engine status
            UpdateOnlineStatus();
        }

        private void UpdateOnlineStatus()
        {
            if (stationLight != null && !isHovered && !isOpen)
            {
                if (IsEngineActivated)
                {
                    stationLight.color = idleColor;
                    stationLight.intensity = lightIntensity;
                }
                else
                {
                    stationLight.color = offlineColor;
                    stationLight.intensity = lightIntensity * 0.3f;
                }
            }
        }

        private void SetupVisuals()
        {
            if (stationLight == null)
            {
                GameObject lightObj = new GameObject("StationLight");
                lightObj.transform.SetParent(transform);
                lightObj.transform.localPosition = new Vector3(0, 1.5f, 0.5f);

                stationLight = lightObj.AddComponent<Light>();
                stationLight.type = LightType.Point;
                stationLight.range = 3f;
                stationLight.intensity = lightIntensity;
                stationLight.color = IsEngineActivated ? idleColor : offlineColor;
            }
        }

        private void SetHighlight(bool active)
        {
            if (highlightEffect != null)
            {
                highlightEffect.SetActive(active && IsEngineActivated);
            }

            if (stationLight != null)
            {
                if (!IsEngineActivated)
                {
                    stationLight.color = offlineColor;
                    stationLight.intensity = lightIntensity * 0.3f;
                }
                else
                {
                    stationLight.color = active ? activeColor : idleColor;
                    stationLight.intensity = active ? lightIntensity * 1.5f : lightIntensity;
                }
            }
        }

        public void OpenStation()
        {
            if (isOpen) return;
            if (!IsEngineActivated) return;

            Debug.Log("[FirstRoomSellStation] OpenStation called");

            // Get or create the UI component
            FirstRoomSellStationUI ui = FirstRoomSellStationUI.Instance;
            Debug.Log($"[FirstRoomSellStation] Instance is {(ui != null ? "valid" : "null")}");

            if (ui == null)
            {
                ui = EnsureUI();
                Debug.Log($"[FirstRoomSellStation] After EnsureUI, ui is {(ui != null ? "valid" : "null")}");
            }

            // Only proceed if we have a valid UI
            if (ui == null)
            {
                Debug.LogError("[FirstRoomSellStation] Failed to create UI!");
                return;
            }

            // Try to show the UI - only set state if successful
            bool success = ui.ShowUI(this);
            if (!success)
            {
                Debug.LogError("[FirstRoomSellStation] ShowUI failed!");
                return;
            }

            // UI successfully opened - now set state
            isOpen = true;
            CurrentStation = this;
            SetHighlight(true);
            Debug.Log("[FirstRoomSellStation] Station opened successfully");

            if (audioSource != null && openSound != null)
            {
                audioSource.PlayOneShot(openSound);
            }
        }

        private FirstRoomSellStationUI EnsureUI()
        {
            var existingUI = FindObjectOfType<FirstRoomSellStationUI>();
            if (existingUI != null)
            {
                Debug.Log($"[FirstRoomSellStation] Found existing UI on canvas: {existingUI.transform.parent?.name}");
                return existingUI;
            }

            // Always create a dedicated canvas for machine UIs to avoid conflicts
            Canvas canvas = null;
            GameObject existingMachineCanvas = GameObject.Find("MachineUICanvas");
            if (existingMachineCanvas != null)
            {
                canvas = existingMachineCanvas.GetComponent<Canvas>();
            }

            if (canvas == null)
            {
                Debug.Log("[FirstRoomSellStation] Creating dedicated MachineUICanvas");
                GameObject canvasObj = new GameObject("MachineUICanvas");
                canvas = canvasObj.AddComponent<Canvas>();
                canvas.renderMode = RenderMode.ScreenSpaceOverlay;
                canvas.sortingOrder = 100;
                var scaler = canvasObj.AddComponent<UnityEngine.UI.CanvasScaler>();
                scaler.uiScaleMode = UnityEngine.UI.CanvasScaler.ScaleMode.ScaleWithScreenSize;
                scaler.referenceResolution = new Vector2(1920, 1080);
                canvasObj.AddComponent<UnityEngine.UI.GraphicRaycaster>();
            }

            GameObject uiObj = new GameObject("FirstRoomSellStationUI");
            uiObj.transform.SetParent(canvas.transform, false);
            FirstRoomSellStationUI ui = uiObj.AddComponent<FirstRoomSellStationUI>();
            Debug.Log($"[FirstRoomSellStation] Created UI on canvas: {canvas.name}");
            return ui;
        }

        public void CloseStation()
        {
            if (!isOpen) return;

            isOpen = false;
            CurrentStation = null;

            if (!isHovered)
            {
                SetHighlight(false);
            }

            if (audioSource != null && closeSound != null)
            {
                audioSource.PlayOneShot(closeSound);
            }

            if (FirstRoomSellStationUI.Instance != null)
            {
                FirstRoomSellStationUI.Instance.HideUI();
            }
        }

        public void PlaySellSound()
        {
            if (audioSource != null && sellSound != null)
            {
                audioSource.PlayOneShot(sellSound);
            }
        }

        public int SellAllForPlayer()
        {
            if (!IsEngineActivated) return 0;

            int totalCredits = 0;

            int dustCredits = SellDust();
            totalCredits += dustCredits;

            var inventory = InventorySystem.Instance;
            if (inventory != null)
            {
                if (InventorySellService.Instance == null)
                {
                    EnsureInventorySellService();
                }

                if (InventorySellService.Instance != null)
                {
                    int itemCredits = InventorySellService.Instance.SellAllSellableItems(inventory);
                    totalCredits += itemCredits;
                }
            }

            if (totalCredits > 0)
            {
                PlaySellSound();
            }

            return totalCredits;
        }

        public int SellDust()
        {
            if (DustManager.Instance == null)
                return 0;

            return DustManager.Instance.SellAllDust();
        }

        public int GetDustValue()
        {
            if (DustManager.Instance == null)
                return 0;

            return DustManager.Instance.GetDustValue();
        }

        public float GetDustAmount()
        {
            if (DustManager.Instance == null)
                return 0f;

            return DustManager.Instance.GetDust();
        }

        private void EnsureInventorySellService()
        {
            var existing = FindObjectOfType<InventorySellService>();
            if (existing != null)
            {
                return;
            }

            GameObject serviceObj = new GameObject("InventorySellService");
            serviceObj.AddComponent<InventorySellService>();
        }

        public string StationName => stationName;
    }
}
