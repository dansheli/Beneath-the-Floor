using UnityEngine;
using BeneathTheFloor.Interaction;
using BeneathTheFloor.Energy;
using BeneathTheFloor.UI;

namespace BeneathTheFloor.Machines
{
    /// <summary>
    /// Energy Generator machine that wraps EnergySource with IInteractable support.
    /// Allows the player to interact with the generator to toggle it on/off and add fuel.
    /// </summary>
    public class EnergyGenerator : MonoBehaviour, IInteractable
    {
        [Header("Generator Settings")]
        [SerializeField] private string generatorName = "Energy Generator";

        [Header("Visual Feedback")]
        [SerializeField] private GameObject highlightEffect;
        [SerializeField] private Light statusLight;
        [SerializeField] private Color activeColor = Color.green;
        [SerializeField] private Color inactiveColor = Color.red;

        [Header("Audio")]
        [SerializeField] private AudioSource audioSource;
        [SerializeField] private AudioClip toggleSound;

        // State
        private bool isOpen = false;
        private EnergySource energySource;

        public static EnergyGenerator Instance { get; private set; }
        public static EnergyGenerator CurrentGenerator { get; private set; }

        // Properties
        public bool CanInteract => true;
        public bool IsOpen => isOpen;
        public string GeneratorName => generatorName;
        public EnergySource Source => energySource;

        private void Awake()
        {
            if (Instance == null)
            {
                Instance = this;
            }

            // Get or add EnergySource component
            energySource = GetComponent<EnergySource>();
            if (energySource == null)
            {
                energySource = gameObject.AddComponent<EnergySource>();
            }

            if (audioSource == null)
            {
                audioSource = GetComponent<AudioSource>();
                if (audioSource == null)
                {
                    audioSource = gameObject.AddComponent<AudioSource>();
                }
            }
        }

        private void Start()
        {
            UpdateStatusLight();
        }

        private void Update()
        {
            if (isOpen && Input.GetKeyDown(KeyCode.Escape))
            {
                UI.UIState.ConsumeEscape();
                CloseGenerator();
            }
        }

        public string GetInteractionText()
        {
            string status = energySource != null && energySource.IsActive ? "Running" : "Stopped";
            return $"Press E to use {generatorName} [{status}]";
        }

        public void Interact(GameObject interactor)
        {
            if (isOpen)
            {
                CloseGenerator();
            }
            else
            {
                OpenGenerator();
            }
        }

        public void OnHoverEnter()
        {
            if (highlightEffect != null)
            {
                highlightEffect.SetActive(true);
            }
        }

        public void OnHoverExit()
        {
            if (highlightEffect != null)
            {
                highlightEffect.SetActive(false);
            }
        }

        public void OpenGenerator()
        {
            isOpen = true;
            CurrentGenerator = this;
            UIState.IsMachineUIOpen = true;

            // Tell interaction system that UI is open
            if (InteractionSystem.Instance != null)
            {
                InteractionSystem.Instance.SetUIOpen(true);
            }

            // Show Energy UI or a simple toggle panel
            // For now, just toggle the generator on interaction
            ToggleGenerator();

            // Disable player movement
            if (Player.FirstPersonController.Instance != null)
            {
                Player.FirstPersonController.Instance.CanMove = false;
            }

            // Unlock cursor
            Cursor.lockState = CursorLockMode.None;
            Cursor.visible = true;

            Debug.Log($"[EnergyGenerator] Opened {generatorName}");

            // Auto-close after toggle (simple interaction)
            CloseGenerator();
        }

        public void CloseGenerator()
        {
            isOpen = false;
            CurrentGenerator = null;

            // Tell interaction system that UI is closed
            if (InteractionSystem.Instance != null)
            {
                InteractionSystem.Instance.SetUIOpen(false);
            }

            // Enable player movement
            if (Player.FirstPersonController.Instance != null)
            {
                Player.FirstPersonController.Instance.CanMove = true;
            }

            // Clear UI state and restore cursor
            UI.UIState.IsMachineUIOpen = false;
            Cursor.lockState = CursorLockMode.Locked;
            Cursor.visible = false;

            // Ensure game is unpaused
            Time.timeScale = 1f;

            Debug.Log($"[EnergyGenerator] Closed {generatorName}");
        }

        public void ToggleGenerator()
        {
            if (energySource != null)
            {
                energySource.Toggle();
                UpdateStatusLight();

                if (toggleSound != null && audioSource != null)
                {
                    audioSource.PlayOneShot(toggleSound);
                }

                string status = energySource.IsActive ? "ON" : "OFF";
                Debug.Log($"[EnergyGenerator] Toggled {generatorName} - now {status}");
            }
        }

        public bool AddFuel(ResourceType fuelType, int amount)
        {
            if (energySource != null)
            {
                return energySource.AddFuel(fuelType, amount);
            }
            return false;
        }

        private void UpdateStatusLight()
        {
            if (statusLight != null && energySource != null)
            {
                statusLight.color = energySource.IsActive ? activeColor : inactiveColor;
            }
        }

        public string GetStatusText()
        {
            if (energySource != null)
            {
                return energySource.GetStatusText();
            }
            return $"{generatorName}: No energy source attached";
        }
    }
}
