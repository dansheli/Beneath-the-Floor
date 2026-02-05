using UnityEngine;
using UnityEngine.UI;
using TMPro;
using System.Collections.Generic;

namespace BeneathTheFloor.UI
{
    /// <summary>
    /// DEPRECATED: This is the old tab-based inventory UI.
    /// The new Storage Tray style UI is in InventoryUIManager (BeneathTheFloor.InventoryUI namespace).
    /// This script is disabled and will destroy itself on Start.
    /// </summary>
    [System.Obsolete("Use InventoryUIManager instead")]
    public class InventoryUI : MonoBehaviour
    {
        [Header("Main Panel")]
        [SerializeField] private GameObject inventoryPanel;
        [SerializeField] private CanvasGroup canvasGroup;

        [Header("Resource Tab")]
        [SerializeField] private Transform resourceGrid;
        [SerializeField] private GameObject resourceSlotPrefab;

        [Header("Story Items Tab")]
        [SerializeField] private Transform storyItemsGrid;
        [SerializeField] private GameObject storyItemSlotPrefab;

        [Header("Tool Tab")]
        [SerializeField] private Image currentToolImage;
        [SerializeField] private TextMeshProUGUI currentToolNameText;
        [SerializeField] private TextMeshProUGUI toolStatsText;
        [SerializeField] private Slider durabilityBar;

        [Header("Tab Buttons")]
        [SerializeField] private Button resourcesTabButton;
        [SerializeField] private Button storyTabButton;
        [SerializeField] private Button toolTabButton;

        [Header("Tab Panels")]
        [SerializeField] private GameObject resourcesPanel;
        [SerializeField] private GameObject storyPanel;
        [SerializeField] private GameObject toolPanel;

        [Header("Item Details")]
        [SerializeField] private GameObject itemDetailPanel;
        [SerializeField] private TextMeshProUGUI itemNameText;
        [SerializeField] private TextMeshProUGUI itemDescriptionText;
        [SerializeField] private Image itemImage;

        private bool isOpen = false;
        private List<GameObject> resourceSlots = new List<GameObject>();
        private List<GameObject> storySlots = new List<GameObject>();

        public static InventoryUI Instance { get; private set; }

        public bool IsOpen => isOpen;

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
            // DISABLED: This old inventory UI is replaced by InventoryUIManager
            // Destroy this component and hide any associated UI
            Debug.LogWarning("[InventoryUI] DEPRECATED - This old tab-based inventory is disabled. Using InventoryUIManager instead.");

            if (inventoryPanel != null)
            {
                inventoryPanel.SetActive(false);
            }

            // Do NOT subscribe to events - let InventoryUIManager handle everything
            // Destroy this component so it doesn't interfere
            Destroy(this);
            return;

            /* OLD CODE - kept for reference
            // Subscribe to events
            GameEvents.OnInventoryToggled += ToggleInventory;
            GameEvents.OnResourceCollected += OnResourceChanged;
            GameEvents.OnStoryItemFound += OnStoryItemFound;
            GameEvents.OnToolEquipped += OnToolEquipped;

            // Setup tab buttons
            if (resourcesTabButton != null) resourcesTabButton.onClick.AddListener(() => ShowTab(0));
            if (storyTabButton != null) storyTabButton.onClick.AddListener(() => ShowTab(1));
            if (toolTabButton != null) toolTabButton.onClick.AddListener(() => ShowTab(2));

            // Initialize
            CloseInventory();
            HideItemDetails();
            */
        }

        private void OnDestroy()
        {
            GameEvents.OnInventoryToggled -= ToggleInventory;
            GameEvents.OnResourceCollected -= OnResourceChanged;
            GameEvents.OnStoryItemFound -= OnStoryItemFound;
            GameEvents.OnToolEquipped -= OnToolEquipped;
        }

        private void Update()
        {
            if (isOpen && (Input.GetKeyDown(KeyCode.Escape) || Input.GetKeyDown(KeyCode.I) || Input.GetKeyDown(KeyCode.Tab)))
            {
                if (Input.GetKeyDown(KeyCode.Escape))
                {
                    UIState.ConsumeEscape();
                }
                CloseInventory();
            }
        }

        public void ToggleInventory()
        {
            if (isOpen)
            {
                CloseInventory();
            }
            else
            {
                OpenInventory();
            }
        }

        public void OpenInventory()
        {
            isOpen = true;

            if (inventoryPanel != null)
            {
                inventoryPanel.SetActive(true);
            }

            if (canvasGroup != null)
            {
                canvasGroup.alpha = 1f;
                canvasGroup.interactable = true;
                canvasGroup.blocksRaycasts = true;
            }

            // Refresh displays
            RefreshResourceDisplay();
            RefreshStoryItemDisplay();
            RefreshToolDisplay();

            // Show resources tab by default
            ShowTab(0);

            // Set UIState so other systems know inventory is open
            UIState.IsInventoryUIOpen = true;

            // Unlock cursor
            Cursor.lockState = CursorLockMode.None;
            Cursor.visible = true;
        }

        public void CloseInventory()
        {
            isOpen = false;

            if (inventoryPanel != null)
            {
                inventoryPanel.SetActive(false);
            }

            if (canvasGroup != null)
            {
                canvasGroup.alpha = 0f;
                canvasGroup.interactable = false;
                canvasGroup.blocksRaycasts = false;
            }

            HideItemDetails();

            // Clear UIState
            UIState.IsInventoryUIOpen = false;

            // Lock cursor if no other UI is open
            if (!UIState.IsAnyUIOpen)
            {
                Cursor.lockState = CursorLockMode.Locked;
                Cursor.visible = false;
            }
        }

        private void ShowTab(int tabIndex)
        {
            if (resourcesPanel != null) resourcesPanel.SetActive(tabIndex == 0);
            if (storyPanel != null) storyPanel.SetActive(tabIndex == 1);
            if (toolPanel != null) toolPanel.SetActive(tabIndex == 2);

            HideItemDetails();
        }

        private void RefreshResourceDisplay()
        {
            // Clear existing slots
            foreach (var slot in resourceSlots)
            {
                Destroy(slot);
            }
            resourceSlots.Clear();

            if (resourceGrid == null || resourceSlotPrefab == null) return;

            var inventory = Inventory.InventorySystem.Instance;
            if (inventory == null) return;

            foreach (ResourceType type in System.Enum.GetValues(typeof(ResourceType)))
            {
                int count = inventory.GetResourceCount(type);
                if (count > 0)
                {
                    GameObject slot = Instantiate(resourceSlotPrefab, resourceGrid);
                    resourceSlots.Add(slot);

                    TextMeshProUGUI text = slot.GetComponentInChildren<TextMeshProUGUI>();
                    if (text != null)
                    {
                        text.text = $"{type}\n{count}";
                    }

                    Button btn = slot.GetComponent<Button>();
                    if (btn != null)
                    {
                        ResourceType capturedType = type;
                        btn.onClick.AddListener(() => ShowResourceDetails(capturedType));
                    }
                }
            }
        }

        private void RefreshStoryItemDisplay()
        {
            foreach (var slot in storySlots)
            {
                Destroy(slot);
            }
            storySlots.Clear();

            if (storyItemsGrid == null || storyItemSlotPrefab == null) return;

            var inventory = Inventory.InventorySystem.Instance;
            if (inventory == null) return;

            foreach (StoryItem item in inventory.StoryItems)
            {
                GameObject slot = Instantiate(storyItemSlotPrefab, storyItemsGrid);
                storySlots.Add(slot);

                TextMeshProUGUI text = slot.GetComponentInChildren<TextMeshProUGUI>();
                if (text != null)
                {
                    text.text = item.title;
                }

                Button btn = slot.GetComponent<Button>();
                if (btn != null)
                {
                    StoryItem capturedItem = item;
                    btn.onClick.AddListener(() => ShowStoryItemDetails(capturedItem));
                }
            }
        }

        private void RefreshToolDisplay()
        {
            var inventory = Inventory.InventorySystem.Instance;
            if (inventory == null) return;

            ToolData tool = inventory.CurrentTool;
            if (tool == null) return;

            if (currentToolNameText != null)
            {
                currentToolNameText.text = tool.toolName;
            }

            if (currentToolImage != null && tool.icon != null)
            {
                currentToolImage.sprite = tool.icon;
            }

            if (toolStatsText != null)
            {
                toolStatsText.text = $"Tier: {tool.tier}\n" +
                                     $"Dig Speed: {tool.digSpeed}x\n" +
                                     $"Max Depth: {tool.maxDepth}m";
            }

            if (durabilityBar != null)
            {
                durabilityBar.maxValue = tool.maxDurability;
                durabilityBar.value = tool.durability;
            }
        }

        private void ShowResourceDetails(ResourceType type)
        {
            if (itemDetailPanel != null) itemDetailPanel.SetActive(true);

            if (itemNameText != null)
            {
                itemNameText.text = type.ToString();
            }

            if (itemDescriptionText != null)
            {
                itemDescriptionText.text = GetResourceDescription(type);
            }
        }

        private void ShowStoryItemDetails(StoryItem item)
        {
            if (itemDetailPanel != null) itemDetailPanel.SetActive(true);

            if (itemNameText != null)
            {
                itemNameText.text = item.title;
            }

            if (itemDescriptionText != null)
            {
                itemDescriptionText.text = item.description;
            }

            if (itemImage != null && item.icon != null)
            {
                itemImage.sprite = item.icon;
                itemImage.gameObject.SetActive(true);
            }
        }

        private void HideItemDetails()
        {
            if (itemDetailPanel != null)
            {
                itemDetailPanel.SetActive(false);
            }
        }

        private string GetResourceDescription(ResourceType type)
        {
            return type switch
            {
                ResourceType.Dirt => "Common soil. Found near the surface.",
                ResourceType.Clay => "Moldable clay. Used for basic construction.",
                ResourceType.Coal => "Combustible mineral. Used as fuel.",
                ResourceType.IronOre => "Raw iron. Essential for tool upgrades.",
                ResourceType.Copper => "Conductive metal. Used for advanced machinery.",
                ResourceType.Silver => "Precious metal. Valuable for trading.",
                ResourceType.Gold => "Rare precious metal. Required for premium upgrades.",
                ResourceType.AncientArtifact => "A mysterious relic from ages past.",
                _ => "Unknown resource."
            };
        }

        private void OnResourceChanged(ResourceType type, int amount)
        {
            if (isOpen)
            {
                RefreshResourceDisplay();
            }
        }

        private void OnStoryItemFound(StoryItem item)
        {
            if (isOpen)
            {
                RefreshStoryItemDisplay();
            }
        }

        private void OnToolEquipped(ToolData tool)
        {
            if (isOpen)
            {
                RefreshToolDisplay();
            }
        }
    }
}
