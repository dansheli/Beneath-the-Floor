using UnityEngine;
using BeneathTheFloor.Interaction;
using BeneathTheFloor.Inventory;
using BeneathTheFloor.ResourceSystem;

namespace BeneathTheFloor.Economy
{
    /// <summary>
    /// Trade Terminal machine that allows the player to sell items for currency.
    /// Implements IInteractable for player interaction.
    /// </summary>
    [RequireComponent(typeof(BoxCollider))]
    public class TradeTerminal : MonoBehaviour, IInteractable
    {
        [Header("Terminal Settings")]
        [SerializeField] private string terminalName = "Trade Terminal";

        [Header("Visual Feedback")]
        [SerializeField] private GameObject highlightEffect;
        [SerializeField] private Light terminalLight;
        [SerializeField] private Color idleColor = new Color(0.2f, 0.8f, 0.3f); // Green
        [SerializeField] private Color activeColor = new Color(0.3f, 1f, 0.4f);
        [SerializeField] private float lightIntensity = 1.5f;

        [Header("Audio")]
        [SerializeField] private AudioSource audioSource;
        [SerializeField] private AudioClip openSound;
        [SerializeField] private AudioClip closeSound;
        [SerializeField] private AudioClip sellSound;

        private bool isOpen = false;
        private bool isHovered = false;

        public static TradeTerminal CurrentTerminal { get; private set; }

        // IInteractable implementation
        public bool CanInteract => !isOpen;

        public string GetInteractionText()
        {
            return $"Press E to use {terminalName}";
        }

        public void Interact(GameObject interactor)
        {
            if (isOpen) return;
            OpenTerminal();
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
            // Ensure collider exists
            var collider = GetComponent<BoxCollider>();
            if (collider == null)
            {
                collider = gameObject.AddComponent<BoxCollider>();
            }

            // Setup audio source
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
            SetupVisuals();
            SetHighlight(false);
        }

        private void Update()
        {
            // Close on Escape when open
            if (isOpen && Input.GetKeyDown(KeyCode.Escape))
            {
                UI.UIState.ConsumeEscape();
                CloseTerminal();
            }
        }

        private void SetupVisuals()
        {
            // Create light if not assigned
            if (terminalLight == null)
            {
                GameObject lightObj = new GameObject("TerminalLight");
                lightObj.transform.SetParent(transform);
                lightObj.transform.localPosition = new Vector3(0, 1.5f, 0.5f);

                terminalLight = lightObj.AddComponent<Light>();
                terminalLight.type = LightType.Point;
                terminalLight.range = 3f;
                terminalLight.intensity = lightIntensity;
                terminalLight.color = idleColor;
            }
        }

        private void SetHighlight(bool active)
        {
            if (highlightEffect != null)
            {
                highlightEffect.SetActive(active);
            }

            if (terminalLight != null)
            {
                terminalLight.color = active ? activeColor : idleColor;
                terminalLight.intensity = active ? lightIntensity * 1.5f : lightIntensity;
            }
        }

        public void OpenTerminal()
        {
            if (isOpen) return;

            isOpen = true;
            CurrentTerminal = this;
            SetHighlight(true);

            // Play sound
            if (audioSource != null && openSound != null)
            {
                audioSource.PlayOneShot(openSound);
            }

            // Show UI - find or create if needed
            if (TradeTerminalUI.Instance == null)
            {
                EnsureTradeTerminalUI();
            }

            if (TradeTerminalUI.Instance != null)
            {
                TradeTerminalUI.Instance.ShowUI(this);
            }
        }

        /// <summary>
        /// Ensures TradeTerminalUI exists. Tries to find existing, then creates via EconomySetup.
        /// </summary>
        private void EnsureTradeTerminalUI()
        {
            // First try to find existing
            var existingUI = FindObjectOfType<TradeTerminalUI>();
            if (existingUI != null)
            {
                return;
            }

            // Try to run EconomySetup if it exists
            var economySetup = FindObjectOfType<EconomySetup>();
            if (economySetup != null)
            {
                economySetup.SetupEconomy();
                return;
            }

            // Last resort: Create minimal TradeTerminalUI

            // Find or create canvas
            Canvas canvas = FindObjectOfType<Canvas>();
            if (canvas == null)
            {
                GameObject canvasObj = new GameObject("MachineUICanvas");
                canvas = canvasObj.AddComponent<Canvas>();
                canvas.renderMode = RenderMode.ScreenSpaceOverlay;
                canvas.sortingOrder = 100;
                canvasObj.AddComponent<UnityEngine.UI.CanvasScaler>();
                canvasObj.AddComponent<UnityEngine.UI.GraphicRaycaster>();
            }

            // Create TradeTerminalUI object
            GameObject uiObj = new GameObject("TradeTerminalUI");
            uiObj.transform.SetParent(canvas.transform, false);
            uiObj.AddComponent<TradeTerminalUI>();
        }

        public void CloseTerminal()
        {
            if (!isOpen) return;

            isOpen = false;
            CurrentTerminal = null;

            if (!isHovered)
            {
                SetHighlight(false);
            }

            // Play sound
            if (audioSource != null && closeSound != null)
            {
                audioSource.PlayOneShot(closeSound);
            }

            // Hide UI
            if (TradeTerminalUI.Instance != null)
            {
                TradeTerminalUI.Instance.HideUI();
            }
        }

        /// <summary>
        /// Play the sell sound effect.
        /// </summary>
        public void PlaySellSound()
        {
            if (audioSource != null && sellSound != null)
            {
                audioSource.PlayOneShot(sellSound);
            }
        }

        /// <summary>
        /// Sell all sellable items in the player's inventory.
        /// Delegates to InventorySellService for the actual logic.
        /// Also sells any accumulated dust.
        /// </summary>
        /// <returns>Total credits gained from the sale.</returns>
        public int SellAllForPlayer()
        {
            int totalCredits = 0;

            // Sell dust first
            int dustCredits = SellDust();
            totalCredits += dustCredits;

            // Sell inventory items
            var inventory = InventorySystem.Instance;
            if (inventory != null)
            {
                // Ensure InventorySellService exists
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

        /// <summary>
        /// Sell all accumulated dust for credits.
        /// </summary>
        /// <returns>Credits gained from dust.</returns>
        public int SellDust()
        {
            if (DustManager.Instance == null)
                return 0;

            return DustManager.Instance.SellAllDust();
        }

        /// <summary>
        /// Get the value of current dust without selling.
        /// </summary>
        public int GetDustValue()
        {
            if (DustManager.Instance == null)
                return 0;

            return DustManager.Instance.GetDustValue();
        }

        /// <summary>
        /// Get current dust amount.
        /// </summary>
        public float GetDustAmount()
        {
            if (DustManager.Instance == null)
                return 0f;

            return DustManager.Instance.GetDust();
        }

        /// <summary>
        /// Ensures InventorySellService exists in the scene.
        /// </summary>
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

        /// <summary>
        /// Get the terminal name for display.
        /// </summary>
        public string TerminalName => terminalName;
    }
}
