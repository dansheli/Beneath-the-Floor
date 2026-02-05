using UnityEngine;
using UnityEngine.UI;
using TMPro;
using System.Collections.Generic;
using BeneathTheFloor.UI;

namespace BeneathTheFloor.Machines
{
    /// <summary>
    /// RefineryUI manages the refinery processing interface.
    /// IMPORTANT: This UI should exist as a STATIC panel in the scene (under MachineUICanvas).
    /// The script only SHOWS/HIDES the existing panel and POPULATES recipe buttons - it does NOT create the UI structure.
    /// Use "BeneathTheFloor/Setup/Create Static Machine UI" in the Editor to create the panel structure.
    /// </summary>
    public class RefineryUI : MonoBehaviour
    {
        [Header("Static UI References")]
        [Tooltip("Reference to the main RefineryPanel (should exist in scene)")]
        [SerializeField] private GameObject refineryPanel;
        [Tooltip("Content transform for recipe buttons (inside ScrollView)")]
        [SerializeField] private Transform recipeListContent;
        [Tooltip("Prefab for recipe buttons - assign from Assets/Prefabs/UI/RecipeButton")]
        [SerializeField] private GameObject recipeButtonPrefab;

        [Header("Recipe Details")]
        [SerializeField] private GameObject detailsPanel;
        [SerializeField] private Image inputIcon;
        [SerializeField] private TextMeshProUGUI inputText;
        [SerializeField] private Image outputIcon;
        [SerializeField] private TextMeshProUGUI outputText;
        [SerializeField] private TextMeshProUGUI processTimeText;

        [Header("Amount Selection")]
        [SerializeField] private Slider amountSlider;
        [SerializeField] private TextMeshProUGUI amountText;
        [SerializeField] private Button processButton;
        [SerializeField] private TextMeshProUGUI processButtonText;

        [Header("Progress")]
        [SerializeField] private GameObject progressPanel;
        [SerializeField] private Slider progressBar;
        [SerializeField] private TextMeshProUGUI progressText;
        [SerializeField] private TextMeshProUGUI queueText;

        [Header("Header")]
        [SerializeField] private TextMeshProUGUI titleText;
        [SerializeField] private Button closeButton;

        [Header("Resource Icons")]
        [SerializeField] private Sprite dirtIcon;
        [SerializeField] private Sprite clayIcon;
        [SerializeField] private Sprite coalIcon;
        [SerializeField] private Sprite ironIcon;
        [SerializeField] private Sprite copperIcon;

        public static RefineryUI Instance { get; private set; }

        private Refinery currentRefinery;
        private RefiningRecipe selectedRecipe;
        private List<GameObject> recipeButtons = new List<GameObject>();
        private int selectedAmount = 1;

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
            // Validate static references
            ValidateReferences();

            // Setup button listeners
            if (closeButton != null)
            {
                closeButton.onClick.RemoveAllListeners();
                closeButton.onClick.AddListener(OnCloseClicked);
            }

            if (processButton != null)
            {
                processButton.onClick.RemoveAllListeners();
                processButton.onClick.AddListener(OnProcessClicked);
            }

            if (amountSlider != null)
            {
                amountSlider.onValueChanged.RemoveAllListeners();
                amountSlider.onValueChanged.AddListener(OnAmountChanged);
            }

            // Hide panel at start
            HideUI();
        }

        /// <summary>
        /// Validates that all required references are assigned.
        /// If references are missing, attempts to find them by name.
        /// </summary>
        private void ValidateReferences()
        {
            // Try to find refineryPanel if not assigned
            if (refineryPanel == null)
            {
                var canvas = GetComponentInParent<Canvas>();
                if (canvas != null)
                {
                    var panel = canvas.transform.Find("RefineryPanel");
                    if (panel != null)
                    {
                        refineryPanel = panel.gameObject;
                    }
                }
            }

            if (refineryPanel == null)
            {
                Debug.LogError("[RefineryUI] refineryPanel is NOT assigned! Use 'BeneathTheFloor/Setup/Create Static Machine UI' to create it.");
                return;
            }

            // Try to find recipeListContent if not assigned
            if (recipeListContent == null)
            {
                recipeListContent = FindChildRecursive(refineryPanel.transform, "RecipeListContent");
            }

            // Try to find detailsPanel if not assigned
            if (detailsPanel == null)
            {
                var details = FindChildRecursive(refineryPanel.transform, "DetailsPanel");
                if (details != null)
                {
                    detailsPanel = details.gameObject;
                }
            }

            // Try to find other references
            if (inputText == null)
                inputText = FindChildComponent<TextMeshProUGUI>(refineryPanel.transform, "InputText");

            if (outputText == null)
                outputText = FindChildComponent<TextMeshProUGUI>(refineryPanel.transform, "OutputText");

            if (processTimeText == null)
                processTimeText = FindChildComponent<TextMeshProUGUI>(refineryPanel.transform, "ProcessTime");

            if (amountSlider == null)
                amountSlider = FindChildComponent<Slider>(refineryPanel.transform, "AmountSlider");

            if (amountText == null)
                amountText = FindChildComponent<TextMeshProUGUI>(refineryPanel.transform, "AmountText");

            if (processButton == null)
                processButton = FindChildComponent<Button>(refineryPanel.transform, "ProcessButton");

            if (processButtonText == null && processButton != null)
                processButtonText = processButton.GetComponentInChildren<TextMeshProUGUI>();

            if (progressPanel == null)
            {
                var prog = FindChildRecursive(refineryPanel.transform, "ProgressPanel");
                if (prog != null) progressPanel = prog.gameObject;
            }

            if (progressBar == null)
                progressBar = FindChildComponent<Slider>(refineryPanel.transform, "ProgressBar");

            if (progressText == null)
                progressText = FindChildComponent<TextMeshProUGUI>(refineryPanel.transform, "ProgressText");

            if (titleText == null)
                titleText = FindChildComponent<TextMeshProUGUI>(refineryPanel.transform, "TitleText");

            if (closeButton == null)
                closeButton = FindChildComponent<Button>(refineryPanel.transform, "CloseButton");
        }

        private Transform FindChildRecursive(Transform parent, string name)
        {
            var found = parent.Find(name);
            if (found != null) return found;

            foreach (Transform child in parent)
            {
                var result = FindChildRecursive(child, name);
                if (result != null) return result;
            }
            return null;
        }

        private T FindChildComponent<T>(Transform parent, string name) where T : Component
        {
            var found = FindChildRecursive(parent, name);
            return found?.GetComponent<T>();
        }

        private void Update()
        {
            if (currentRefinery != null && currentRefinery.IsProcessing)
            {
                UpdateProgressUI();
            }
        }

        public void ShowUI(Refinery refinery)
        {
            currentRefinery = refinery;

            // Validate references before showing
            if (refineryPanel == null)
            {
                ValidateReferences();
            }

            if (refineryPanel == null)
            {
                Debug.LogError("[RefineryUI] Cannot show UI - refineryPanel is NULL!");
                return;
            }

            // Show the panel (it should already exist in the scene)
            refineryPanel.SetActive(true);

            // Update title
            if (titleText != null)
            {
                titleText.text = $"{refinery.RefineryName} (Tier {refinery.Tier})";
            }

            // Subscribe to events
            refinery.OnProcessingProgress += OnProcessingProgress;
            refinery.OnProcessingComplete += OnProcessingComplete;

            // Populate recipe list
            RefreshRecipeList();
            ClearDetails();

            // Set global UI state to block gameplay input
            UIState.IsMachineUIOpen = true;
        }

        public void HideUI()
        {
            if (refineryPanel != null)
            {
                refineryPanel.SetActive(false);
            }

            if (currentRefinery != null)
            {
                currentRefinery.OnProcessingProgress -= OnProcessingProgress;
                currentRefinery.OnProcessingComplete -= OnProcessingComplete;
            }

            currentRefinery = null;
            selectedRecipe = null;

            // Clear global UI state to allow gameplay input
            UIState.IsMachineUIOpen = false;

            // Restore cursor state and ensure game is unpaused
            Cursor.lockState = CursorLockMode.Locked;
            Cursor.visible = false;
            Time.timeScale = 1f;
        }

        private void RefreshRecipeList()
        {
            // Clear existing buttons
            foreach (var btn in recipeButtons)
            {
                if (btn != null) Destroy(btn);
            }
            recipeButtons.Clear();

            if (currentRefinery == null || recipeListContent == null)
            {
                return;
            }

            // Create buttons for each recipe
            foreach (var recipe in currentRefinery.Recipes)
            {
                CreateRecipeButton(recipe);
            }

            // Force layout rebuild
            var contentRect = recipeListContent.GetComponent<RectTransform>();
            if (contentRect != null)
            {
                LayoutRebuilder.ForceRebuildLayoutImmediate(contentRect);
                Canvas.ForceUpdateCanvases();
            }
        }

        private void CreateRecipeButton(RefiningRecipe recipe)
        {
            GameObject buttonObj;

            // Use prefab if available, otherwise create button from scratch
            if (recipeButtonPrefab != null)
            {
                buttonObj = Instantiate(recipeButtonPrefab, recipeListContent);
                buttonObj.name = $"Recipe_{recipe.displayName}";
            }
            else
            {
                // Fallback: create button manually
                buttonObj = new GameObject($"Recipe_{recipe.displayName}");
                buttonObj.transform.SetParent(recipeListContent, false);

                var rectTransform = buttonObj.AddComponent<RectTransform>();
                rectTransform.sizeDelta = new Vector2(200, 50);

                // Add LayoutElement so VerticalLayoutGroup respects the height
                var layoutElement = buttonObj.AddComponent<LayoutElement>();
                layoutElement.minHeight = 50;
                layoutElement.preferredHeight = 50;
                layoutElement.flexibleWidth = 1;

                var image = buttonObj.AddComponent<Image>();
                image.color = new Color(0.3f, 0.25f, 0.2f, 1f);

                var button = buttonObj.AddComponent<Button>();
                button.targetGraphic = image;

                var textObj = new GameObject("Text");
                textObj.transform.SetParent(buttonObj.transform, false);
                var text = textObj.AddComponent<TextMeshProUGUI>();

                // Ensure font is assigned
                if (text.font == null)
                {
                    text.font = TMP_Settings.defaultFontAsset;
                }

                text.fontSize = 12;
                text.alignment = TextAlignmentOptions.Center;
                text.color = Color.white;
                text.raycastTarget = false;

                var textRect = textObj.GetComponent<RectTransform>();
                textRect.anchorMin = Vector2.zero;
                textRect.anchorMax = Vector2.one;
                textRect.offsetMin = new Vector2(5, 2);
                textRect.offsetMax = new Vector2(-5, -2);
            }

            // Reset transform
            buttonObj.transform.localPosition = Vector3.zero;
            buttonObj.transform.localRotation = Quaternion.identity;
            buttonObj.transform.localScale = Vector3.one;

            // Set button text
            var buttonText = buttonObj.GetComponentInChildren<TextMeshProUGUI>();
            if (buttonText != null)
            {
                buttonText.text = $"{recipe.displayName}\n<size=10>({recipe.inputResource} -> {recipe.outputResource})</size>";
                buttonText.richText = true;
            }

            // Setup click handler
            var btn = buttonObj.GetComponent<Button>();
            if (btn != null)
            {
                RefiningRecipe capturedRecipe = recipe;
                btn.onClick.RemoveAllListeners();
                btn.onClick.AddListener(() => SelectRecipe(capturedRecipe));

                // Update button color based on processability
                bool canProcess = currentRefinery.CanProcess(recipe);
                var img = buttonObj.GetComponent<Image>();
                if (img != null)
                {
                    img.color = canProcess ?
                        new Color(0.3f, 0.35f, 0.2f, 1f) :
                        new Color(0.35f, 0.2f, 0.2f, 1f);
                }
            }

            recipeButtons.Add(buttonObj);
        }

        private void SelectRecipe(RefiningRecipe recipe)
        {
            selectedRecipe = recipe;
            ShowDetails(recipe);
        }

        private void ShowDetails(RefiningRecipe recipe)
        {
            if (detailsPanel != null)
            {
                detailsPanel.SetActive(true);
            }

            var inventory = Inventory.InventorySystem.Instance;
            int available = inventory != null ?
                inventory.GetResourceCount(recipe.inputResource) : 0;

            if (inputIcon != null)
            {
                inputIcon.sprite = GetResourceIcon(recipe.inputResource);
            }

            if (inputText != null)
            {
                inputText.text = $"<b>Input:</b>\n{recipe.inputResource}\nRequired: {recipe.inputAmount}\nAvailable: {available}";
            }

            if (outputIcon != null)
            {
                outputIcon.sprite = GetResourceIcon(recipe.outputResource);
            }

            if (outputText != null)
            {
                outputText.text = $"<b>Output:</b>\n{recipe.outputResource}\nOutput: {recipe.outputAmount}";
            }

            if (processTimeText != null)
            {
                processTimeText.text = $"Process Time: {recipe.processingTime:F1}s";
            }

            // Setup amount slider
            int maxBatches = available / recipe.inputAmount;
            maxBatches = Mathf.Max(1, maxBatches);

            if (amountSlider != null)
            {
                amountSlider.minValue = 1;
                amountSlider.maxValue = Mathf.Max(1, maxBatches);
                amountSlider.value = 1;
                selectedAmount = 1;
            }

            UpdateAmountDisplay();
            UpdateProcessButton();
        }

        private void ClearDetails()
        {
            selectedRecipe = null;

            if (detailsPanel != null)
            {
                detailsPanel.SetActive(false);
            }

            if (progressPanel != null)
            {
                progressPanel.SetActive(false);
            }
        }

        private Sprite GetResourceIcon(ResourceType type)
        {
            return type switch
            {
                ResourceType.Dirt => dirtIcon,
                ResourceType.Clay => clayIcon,
                ResourceType.Coal => coalIcon,
                ResourceType.IronOre => ironIcon,
                ResourceType.Copper => copperIcon,
                _ => null
            };
        }

        private void OnAmountChanged(float value)
        {
            selectedAmount = Mathf.RoundToInt(value);
            UpdateAmountDisplay();
            UpdateProcessButton();
        }

        private void UpdateAmountDisplay()
        {
            if (amountText != null && selectedRecipe != null)
            {
                int totalInput = selectedRecipe.inputAmount * selectedAmount;
                int totalOutput = selectedRecipe.outputAmount * selectedAmount;
                amountText.text = $"Batch: {selectedAmount}\n" +
                                 $"Input: {totalInput}x {selectedRecipe.inputResource}\n" +
                                 $"Output: {totalOutput}x {selectedRecipe.outputResource}";
            }
        }

        private void UpdateProcessButton()
        {
            if (processButton == null) return;

            bool canProcess = selectedRecipe != null &&
                             currentRefinery != null &&
                             currentRefinery.CanProcess(selectedRecipe, selectedAmount);

            processButton.interactable = canProcess;

            if (processButtonText != null)
            {
                processButtonText.text = canProcess ? "Start Processing" : "Insufficient Resources";
            }
        }

        private void OnProcessClicked()
        {
            if (selectedRecipe == null || currentRefinery == null) return;

            if (currentRefinery.StartProcessing(selectedRecipe, selectedAmount))
            {
                ShowProgressUI();
                RefreshRecipeList();
                if (selectedRecipe != null)
                {
                    ShowDetails(selectedRecipe);
                }
            }
        }

        private void ShowProgressUI()
        {
            if (progressPanel != null)
            {
                progressPanel.SetActive(true);
            }
        }

        private void UpdateProgressUI()
        {
            if (currentRefinery == null) return;

            float progress = currentRefinery.ProcessProgress;

            if (progressBar != null)
            {
                progressBar.value = progress;
            }

            if (progressText != null)
            {
                progressText.text = $"Processing: {Mathf.RoundToInt(progress * 100)}%";
            }
        }

        private void OnProcessingProgress(float progress)
        {
            UpdateProgressUI();
        }

        private void OnProcessingComplete(ResourceType outputType, int amount)
        {
            // Refresh UI
            RefreshRecipeList();

            if (selectedRecipe != null)
            {
                ShowDetails(selectedRecipe);
            }

            if (!currentRefinery.IsProcessing && progressPanel != null)
            {
                progressPanel.SetActive(false);
            }
        }

        private void OnCloseClicked()
        {
            if (currentRefinery != null)
            {
                currentRefinery.CloseRefinery();
            }
            else
            {
                HideUI();
            }
        }
    }
}
