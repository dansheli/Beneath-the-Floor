using UnityEngine;
using UnityEngine.UI;
using TMPro;

namespace BeneathTheFloor.UI
{
    public class UIManager : MonoBehaviour
    {
        [Header("HUD Elements")]
        [SerializeField] private GameObject hudPanel;
        [SerializeField] private TextMeshProUGUI depthText;
        [SerializeField] private Slider digProgressBar;
        [SerializeField] private TextMeshProUGUI interactionPromptText;
        [SerializeField] private GameObject interactionPrompt;

        [Header("Inventory Panel")]
        [SerializeField] private GameObject inventoryPanel;
        [SerializeField] private Transform resourceListContainer;
        [SerializeField] private GameObject resourceItemPrefab;

        [Header("Pause Menu")]
        [SerializeField] private GameObject pauseMenuPanel;

        [Header("Tool Display")]
        [SerializeField] private Image currentToolIcon;
        [SerializeField] private TextMeshProUGUI currentToolName;
        [SerializeField] private Slider toolDurabilityBar;

        [Header("Resource Display")]
        [SerializeField] private TextMeshProUGUI[] resourceCountTexts;

        public static UIManager Instance { get; private set; }

        private void Awake()
        {
            if (Instance == null)
            {
                Instance = this;
            }
            else
            {
                Destroy(gameObject);
            }
        }

        private void Start()
        {
            // Subscribe to events
            GameEvents.OnPauseToggled += OnPauseToggled;
            GameEvents.OnResourceCollected += OnResourceCollected;
            GameEvents.OnToolEquipped += OnToolEquipped;
            GameEvents.OnDepthReached += OnDepthReached;

            // Initialize UI
            HideAllPanels();
            ShowHUD();
        }

        private void OnDestroy()
        {
            // Unsubscribe from events
            GameEvents.OnPauseToggled -= OnPauseToggled;
            GameEvents.OnResourceCollected -= OnResourceCollected;
            GameEvents.OnToolEquipped -= OnToolEquipped;
            GameEvents.OnDepthReached -= OnDepthReached;
        }

        private void Update()
        {
            HandleInventoryInput();
            UpdateDigProgress();
        }

        private void HandleInventoryInput()
        {
            // DISABLED: Inventory is now handled by InventoryUIManager (Storage Tray style)
            // The InventoryUIManager handles I key directly and has its own UI
            // See: Assets/Scripts/InventoryUI/InventoryUIManager.cs
        }

        public void ToggleInventory()
        {
            // DEPRECATED: Use InventoryUIManager.Instance.ToggleInventory() instead
            // This method is kept for API compatibility but delegates to the new system
            if (BeneathTheFloor.InventoryUI.InventoryUIManager.Instance != null)
            {
                BeneathTheFloor.InventoryUI.InventoryUIManager.Instance.ToggleInventory();
            }
        }

        private void UpdateDigProgress()
        {
            // DIGGING SYSTEM REMOVED - dig progress disabled
            // if (digProgressBar != null && Digging.DiggingSystem.Instance != null)
            // {
            //     bool isDigging = Digging.DiggingSystem.Instance.IsDigging;
            //     digProgressBar.gameObject.SetActive(isDigging);
            //     if (isDigging)
            //     {
            //         digProgressBar.value = Digging.DiggingSystem.Instance.DigProgress;
            //     }
            // }
            if (digProgressBar != null)
            {
                digProgressBar.gameObject.SetActive(false);
            }
        }

        private void OnPauseToggled()
        {
            bool isPaused = Managers.GameManager.Instance?.IsPaused ?? false;

            if (pauseMenuPanel != null)
            {
                pauseMenuPanel.SetActive(isPaused);
            }

            if (hudPanel != null)
            {
                hudPanel.SetActive(!isPaused);
            }
        }

        private void OnResourceCollected(ResourceType type, int amount)
        {
            UpdateResourceDisplay();
            // Could show floating text or notification
        }

        private void OnToolEquipped(ToolData tool)
        {
            if (tool == null) return;

            if (currentToolName != null)
            {
                currentToolName.text = tool.toolName;
            }

            if (currentToolIcon != null && tool.icon != null)
            {
                currentToolIcon.sprite = tool.icon;
            }

            UpdateToolDurability(tool);
        }

        private void OnDepthReached(int depth)
        {
            if (depthText != null)
            {
                depthText.text = $"Depth: {depth}m";
            }
        }

        private void UpdateResourceDisplay()
        {
            var inventory = Inventory.InventorySystem.Instance;
            if (inventory == null) return;

            // Update resource counts in UI
            // This would iterate through resourceCountTexts and update them
        }

        private void UpdateToolDurability(ToolData tool)
        {
            if (toolDurabilityBar != null && tool != null)
            {
                toolDurabilityBar.maxValue = tool.maxDurability;
                toolDurabilityBar.value = tool.durability;
            }
        }

        public void ShowInteractionPrompt(string text)
        {
            if (interactionPrompt != null)
            {
                interactionPrompt.SetActive(true);

                if (interactionPromptText != null)
                {
                    interactionPromptText.text = text;
                }
            }
        }

        public void HideInteractionPrompt()
        {
            if (interactionPrompt != null)
            {
                interactionPrompt.SetActive(false);
            }
        }

        private void HideAllPanels()
        {
            if (inventoryPanel != null) inventoryPanel.SetActive(false);
            if (pauseMenuPanel != null) pauseMenuPanel.SetActive(false);
        }

        private void ShowHUD()
        {
            if (hudPanel != null) hudPanel.SetActive(true);
        }

        // Button callbacks
        public void OnResumeClicked()
        {
            Managers.GameManager.Instance?.SetPause(false);
        }

        public void OnMainMenuClicked()
        {
            Managers.GameManager.Instance?.LoadMainMenu();
        }

        public void OnQuitClicked()
        {
            Managers.GameManager.Instance?.QuitGame();
        }
    }
}
