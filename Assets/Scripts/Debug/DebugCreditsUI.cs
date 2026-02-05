using UnityEngine;
using UnityEngine.UI;
using TMPro;
using BeneathTheFloor.Economy;

namespace BeneathTheFloor.DebugTools
{
    /// <summary>
    /// Debug tool for adding credits during testing.
    /// Press F5 to toggle the UI, enter amount, click Add to receive credits.
    /// Auto-initializes when game starts.
    /// </summary>
    public class DebugCreditsUI : MonoBehaviour
    {
        // Set to true to enable debug credits UI (F5)
        private const bool ENABLE_DEBUG = false;

        [Header("Settings")]
        [SerializeField] private KeyCode toggleKey = KeyCode.F5;
        [SerializeField] private int defaultAmount = 150;

        /// <summary>
        /// Auto-creates the debug UI when the game starts.
        /// </summary>
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void AutoInitialize()
        {
#pragma warning disable CS0162 // Unreachable code detected
            if (!ENABLE_DEBUG) return;

            // Check if already exists
            if (FindObjectOfType<DebugCreditsUI>() != null) return;

            // Create a persistent GameObject
            var obj = new GameObject("[DEBUG] Credits UI");
            obj.AddComponent<DebugCreditsUI>();
            DontDestroyOnLoad(obj);
#pragma warning restore CS0162
        }

        // Runtime UI elements
        private GameObject panel;
        private TMP_InputField inputField;
        private Button addButton;
        private TextMeshProUGUI currentCreditsText;
        private bool isOpen = false;

        private void Update()
        {
#pragma warning disable CS0162 // Unreachable code detected
            if (!ENABLE_DEBUG) return;

            if (Input.GetKeyDown(toggleKey))
            {
                TogglePanel();
            }
#pragma warning restore CS0162
        }

        private void TogglePanel()
        {
            if (panel == null)
            {
                CreateUI();
            }

            isOpen = !isOpen;
            panel.SetActive(isOpen);

            if (isOpen)
            {
                UpdateCurrentCredits();
                inputField.Select();
                inputField.ActivateInputField();
            }
        }

        private void CreateUI()
        {
            // Find or create canvas
            Canvas canvas = FindObjectOfType<Canvas>();
            if (canvas == null)
            {
                var canvasObj = new GameObject("DebugCanvas");
                canvas = canvasObj.AddComponent<Canvas>();
                canvas.renderMode = RenderMode.ScreenSpaceOverlay;
                canvas.sortingOrder = 9999; // On top of everything
                canvasObj.AddComponent<CanvasScaler>();
                canvasObj.AddComponent<GraphicRaycaster>();
            }

            // Create panel
            panel = new GameObject("DebugCreditsPanel");
            panel.transform.SetParent(canvas.transform, false);

            var panelRect = panel.AddComponent<RectTransform>();
            panelRect.anchorMin = new Vector2(0.5f, 0.5f);
            panelRect.anchorMax = new Vector2(0.5f, 0.5f);
            panelRect.pivot = new Vector2(0.5f, 0.5f);
            panelRect.sizeDelta = new Vector2(300, 180);

            var panelImage = panel.AddComponent<Image>();
            panelImage.color = new Color(0.1f, 0.1f, 0.15f, 0.95f);

            // Title
            var titleObj = new GameObject("Title");
            titleObj.transform.SetParent(panel.transform, false);
            var titleText = titleObj.AddComponent<TextMeshProUGUI>();
            titleText.text = "DEBUG: Add Credits (F5)";
            titleText.fontSize = 16;
            titleText.fontStyle = FontStyles.Bold;
            titleText.alignment = TextAlignmentOptions.Center;
            titleText.color = new Color(1f, 0.8f, 0.3f);
            if (titleText.font == null) titleText.font = TMP_Settings.defaultFontAsset;

            var titleRect = titleObj.GetComponent<RectTransform>();
            titleRect.anchorMin = new Vector2(0, 0.75f);
            titleRect.anchorMax = new Vector2(1, 1);
            titleRect.offsetMin = new Vector2(10, 0);
            titleRect.offsetMax = new Vector2(-10, -10);

            // Current credits display
            var currentObj = new GameObject("CurrentCredits");
            currentObj.transform.SetParent(panel.transform, false);
            currentCreditsText = currentObj.AddComponent<TextMeshProUGUI>();
            currentCreditsText.text = "Current: 0";
            currentCreditsText.fontSize = 14;
            currentCreditsText.alignment = TextAlignmentOptions.Center;
            currentCreditsText.color = new Color(0.7f, 0.9f, 0.7f);
            if (currentCreditsText.font == null) currentCreditsText.font = TMP_Settings.defaultFontAsset;

            var currentRect = currentObj.GetComponent<RectTransform>();
            currentRect.anchorMin = new Vector2(0, 0.55f);
            currentRect.anchorMax = new Vector2(1, 0.75f);
            currentRect.offsetMin = new Vector2(10, 0);
            currentRect.offsetMax = new Vector2(-10, 0);

            // Input field background
            var inputBg = new GameObject("InputFieldBg");
            inputBg.transform.SetParent(panel.transform, false);
            var inputBgImage = inputBg.AddComponent<Image>();
            inputBgImage.color = new Color(0.2f, 0.2f, 0.25f, 1f);

            var inputBgRect = inputBg.GetComponent<RectTransform>();
            inputBgRect.anchorMin = new Vector2(0.1f, 0.3f);
            inputBgRect.anchorMax = new Vector2(0.9f, 0.52f);
            inputBgRect.offsetMin = Vector2.zero;
            inputBgRect.offsetMax = Vector2.zero;

            // Input field
            var inputObj = new GameObject("InputField");
            inputObj.transform.SetParent(inputBg.transform, false);

            var inputRect = inputObj.AddComponent<RectTransform>();
            inputRect.anchorMin = Vector2.zero;
            inputRect.anchorMax = Vector2.one;
            inputRect.offsetMin = new Vector2(10, 5);
            inputRect.offsetMax = new Vector2(-10, -5);

            // Text area for input
            var textArea = new GameObject("Text Area");
            textArea.transform.SetParent(inputObj.transform, false);
            var textAreaRect = textArea.AddComponent<RectTransform>();
            textAreaRect.anchorMin = Vector2.zero;
            textAreaRect.anchorMax = Vector2.one;
            textAreaRect.offsetMin = Vector2.zero;
            textAreaRect.offsetMax = Vector2.zero;

            // Input text
            var inputTextObj = new GameObject("Text");
            inputTextObj.transform.SetParent(textArea.transform, false);
            var inputText = inputTextObj.AddComponent<TextMeshProUGUI>();
            inputText.fontSize = 18;
            inputText.alignment = TextAlignmentOptions.Center;
            inputText.color = Color.white;
            if (inputText.font == null) inputText.font = TMP_Settings.defaultFontAsset;

            var inputTextRect = inputTextObj.GetComponent<RectTransform>();
            inputTextRect.anchorMin = Vector2.zero;
            inputTextRect.anchorMax = Vector2.one;
            inputTextRect.offsetMin = Vector2.zero;
            inputTextRect.offsetMax = Vector2.zero;

            // Placeholder
            var placeholderObj = new GameObject("Placeholder");
            placeholderObj.transform.SetParent(textArea.transform, false);
            var placeholder = placeholderObj.AddComponent<TextMeshProUGUI>();
            placeholder.text = "Enter amount...";
            placeholder.fontSize = 18;
            placeholder.fontStyle = FontStyles.Italic;
            placeholder.alignment = TextAlignmentOptions.Center;
            placeholder.color = new Color(0.5f, 0.5f, 0.5f, 0.7f);
            if (placeholder.font == null) placeholder.font = TMP_Settings.defaultFontAsset;

            var placeholderRect = placeholderObj.GetComponent<RectTransform>();
            placeholderRect.anchorMin = Vector2.zero;
            placeholderRect.anchorMax = Vector2.one;
            placeholderRect.offsetMin = Vector2.zero;
            placeholderRect.offsetMax = Vector2.zero;

            // Add TMP_InputField component
            inputField = inputObj.AddComponent<TMP_InputField>();
            inputField.textViewport = textAreaRect;
            inputField.textComponent = inputText;
            inputField.placeholder = placeholder;
            inputField.contentType = TMP_InputField.ContentType.IntegerNumber;
            inputField.text = defaultAmount.ToString();

            // Add Button
            var buttonObj = new GameObject("AddButton");
            buttonObj.transform.SetParent(panel.transform, false);

            var buttonRect = buttonObj.AddComponent<RectTransform>();
            buttonRect.anchorMin = new Vector2(0.2f, 0.05f);
            buttonRect.anchorMax = new Vector2(0.8f, 0.25f);
            buttonRect.offsetMin = Vector2.zero;
            buttonRect.offsetMax = Vector2.zero;

            var buttonImage = buttonObj.AddComponent<Image>();
            buttonImage.color = new Color(0.2f, 0.6f, 0.3f, 1f);

            addButton = buttonObj.AddComponent<Button>();
            addButton.targetGraphic = buttonImage;
            addButton.onClick.AddListener(OnAddClicked);

            // Button text
            var buttonTextObj = new GameObject("Text");
            buttonTextObj.transform.SetParent(buttonObj.transform, false);
            var buttonText = buttonTextObj.AddComponent<TextMeshProUGUI>();
            buttonText.text = "Add Credits";
            buttonText.fontSize = 16;
            buttonText.fontStyle = FontStyles.Bold;
            buttonText.alignment = TextAlignmentOptions.Center;
            buttonText.color = Color.white;
            if (buttonText.font == null) buttonText.font = TMP_Settings.defaultFontAsset;

            var buttonTextRect = buttonTextObj.GetComponent<RectTransform>();
            buttonTextRect.anchorMin = Vector2.zero;
            buttonTextRect.anchorMax = Vector2.one;
            buttonTextRect.offsetMin = Vector2.zero;
            buttonTextRect.offsetMax = Vector2.zero;

            panel.SetActive(false);
        }

        private void OnAddClicked()
        {
            if (int.TryParse(inputField.text, out int amount) && amount > 0)
            {
                if (CurrencyManager.Instance != null)
                {
                    CurrencyManager.Instance.Add(amount);
                    UpdateCurrentCredits();

                    // Show toast notification
                    if (UI.PickupNotificationSystem.Instance != null)
                    {
                        UI.PickupNotificationSystem.Instance.ShowNotification($"+{amount} credits (DEBUG)");
                    }
                }
                else
                {
                    UnityEngine.Debug.LogWarning("[DebugCreditsUI] CurrencyManager not found!");
                }
            }
        }

        private void UpdateCurrentCredits()
        {
            if (currentCreditsText != null && CurrencyManager.Instance != null)
            {
                currentCreditsText.text = $"Current: {CurrencyManager.Instance.CurrentAmount:N0}";
            }
        }
    }
}
