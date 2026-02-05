using UnityEngine;
using BeneathTheFloor.Interaction;
using BeneathTheFloor.World;

namespace BeneathTheFloor.Machines
{
    /// <summary>
    /// First Room Upgrade Station - Advanced upgrade terminal with tabbed interface.
    /// Requires the engine to be activated (crystal inserted) to function.
    /// </summary>
    [RequireComponent(typeof(BoxCollider))]
    public class FirstRoomUpgradeStation : MonoBehaviour, IInteractable
    {
        [Header("Station Settings")]
        [SerializeField] private string stationName = "Upgrade Station";

        [Header("Engine Requirement")]
        [Tooltip("Reference to the Engine that must be activated for this station to work")]
        [SerializeField] private EngineActivationInteract engine;

        [Header("Visual Feedback")]
        [SerializeField] private GameObject highlightEffect;
        [SerializeField] private Light stationLight;
        [SerializeField] private Color idleColor = new Color(0.0f, 0.6f, 0.7f);
        [SerializeField] private Color activeColor = new Color(0.0f, 0.85f, 1.0f);
        [SerializeField] private Color offlineColor = new Color(0.2f, 0.2f, 0.2f);
        [SerializeField] private float lightIntensity = 1.5f;

        [Header("Audio")]
        [SerializeField] private AudioSource audioSource;
        [SerializeField] private AudioClip openSound;
        [SerializeField] private AudioClip closeSound;
        [SerializeField] private AudioClip upgradeSound;

        private bool isOpen = false;
        private bool isHovered = false;

        public static FirstRoomUpgradeStation CurrentStation { get; private set; }

        private bool IsEngineActivated
        {
            get
            {
                if (engine == null)
                {
                    engine = FindObjectOfType<EngineActivationInteract>();
                }
                return engine != null && engine.IsActivated;
            }
        }

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
                return "Upgrade Station [OFFLINE - Insert crystal into engine]";
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

            FirstRoomUpgradeStationUI ui = FirstRoomUpgradeStationUI.Instance;
            if (ui == null)
            {
                ui = EnsureUI();
            }

            if (ui == null)
            {
                Debug.LogError("[FirstRoomUpgradeStation] Failed to create UI!");
                return;
            }

            bool success = ui.ShowUI(this);
            if (!success)
            {
                Debug.LogError("[FirstRoomUpgradeStation] ShowUI failed!");
                return;
            }

            isOpen = true;
            CurrentStation = this;
            SetHighlight(true);

            if (audioSource != null && openSound != null)
            {
                audioSource.PlayOneShot(openSound);
            }
        }

        private FirstRoomUpgradeStationUI EnsureUI()
        {
            var existingUI = FindObjectOfType<FirstRoomUpgradeStationUI>();
            if (existingUI != null)
            {
                return existingUI;
            }

            Canvas canvas = null;
            GameObject existingMachineCanvas = GameObject.Find("MachineUICanvas");
            if (existingMachineCanvas != null)
            {
                canvas = existingMachineCanvas.GetComponent<Canvas>();
            }

            if (canvas == null)
            {
                GameObject canvasObj = new GameObject("MachineUICanvas");
                canvas = canvasObj.AddComponent<Canvas>();
                canvas.renderMode = RenderMode.ScreenSpaceOverlay;
                canvas.sortingOrder = 100;
                var scaler = canvasObj.AddComponent<UnityEngine.UI.CanvasScaler>();
                scaler.uiScaleMode = UnityEngine.UI.CanvasScaler.ScaleMode.ScaleWithScreenSize;
                scaler.referenceResolution = new Vector2(1920, 1080);
                canvasObj.AddComponent<UnityEngine.UI.GraphicRaycaster>();
            }

            GameObject uiObj = new GameObject("FirstRoomUpgradeStationUI");
            uiObj.transform.SetParent(canvas.transform, false);
            FirstRoomUpgradeStationUI ui = uiObj.AddComponent<FirstRoomUpgradeStationUI>();
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

            if (FirstRoomUpgradeStationUI.Instance != null)
            {
                FirstRoomUpgradeStationUI.Instance.HideUI();
            }
        }

        public void PlayUpgradeSound()
        {
            if (audioSource != null && upgradeSound != null)
            {
                audioSource.PlayOneShot(upgradeSound);
            }
        }

        public string StationName => stationName;
    }
}
