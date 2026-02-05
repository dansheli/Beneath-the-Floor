using System.Collections;
using UnityEngine;
using UnityEngine.UI;
using TMPro;

namespace BeneathTheFloor.Missions
{
    /// <summary>
    /// Clean UI controller for displaying mission objectives.
    /// Creates UI elements dynamically if needed.
    /// </summary>
    public class MissionUI : MonoBehaviour
    {
        [Header("UI References (Auto-created if null)")]
        [SerializeField] private TextMeshProUGUI objectiveText;
        [SerializeField] private CanvasGroup objectiveGroup;

        [Header("Styling")]
        [SerializeField] private Color objectiveColor = new Color(1f, 0.95f, 0.8f, 1f);
        [SerializeField] private int fontSize = 18;
        [SerializeField] private Vector2 position = new Vector2(-70, -20); // Top-right offset
        [SerializeField] private Color bubbleColor = new Color(0.1f, 0.1f, 0.15f, 0.85f);
        [SerializeField] private Color headerColor = new Color(0.9f, 0.75f, 0.4f, 1f);
        [SerializeField] private int cornerRadius = 12;

        [Header("Completion Toast")]
        [SerializeField] private Color toastColor = new Color(0.8f, 1f, 0.8f, 1f);
        [SerializeField] private int toastFontSize = 18;
        [SerializeField] private Vector2 toastPosition = new Vector2(20, -180); // Top-left, below money display

        [Header("Help Text")]
        [SerializeField] private Color helpTextColor = new Color(0.7f, 0.7f, 0.7f, 1f);
        [SerializeField] private int helpTextFontSize = 16;

        [Header("Animation")]
        [SerializeField] private float fadeSpeed = 10f; // Fast fade

        private Canvas canvas;
        private Coroutine fadeCoroutine;
        private TextMeshProUGUI toastText;
        private CanvasGroup toastGroup;
        private Coroutine toastCoroutine;
        private TextMeshProUGUI helpText;
        private CanvasGroup helpTextGroup;
        private Sprite roundedRectSprite;

        // Centered popup
        private GameObject centeredPopupContainer;
        private TextMeshProUGUI centeredPopupText;
        private CanvasGroup centeredPopupGroup;
        private Coroutine centeredPopupCoroutine;

        // Dig counter
        private GameObject digCounterContainer;
        private TextMeshProUGUI digCounterText;
        private CanvasGroup digCounterGroup;

        // Crosshair hint (scanner prompt)
        private GameObject crosshairHintContainer;
        private TextMeshProUGUI crosshairHintText;
        private CanvasGroup crosshairHintGroup;

        private void Awake()
        {
            CreateRoundedRectSprite();
            EnsureCanvas();
            EnsureUI();
            EnsureToastUI();
            EnsureHelpTextUI();
            EnsureCenteredPopupUI();
            EnsureDigCounterUI();
            EnsureCrosshairHintUI();
        }

        /// <summary>
        /// Creates a rounded rectangle sprite procedurally for the bubble background.
        /// </summary>
        private void CreateRoundedRectSprite()
        {
            int width = 64;
            int height = 64;
            int radius = Mathf.Min(cornerRadius, Mathf.Min(width, height) / 2);

            Texture2D texture = new Texture2D(width, height, TextureFormat.RGBA32, false);
            texture.filterMode = FilterMode.Bilinear;

            Color[] pixels = new Color[width * height];

            for (int y = 0; y < height; y++)
            {
                for (int x = 0; x < width; x++)
                {
                    float alpha = 1f;

                    // Check corners
                    if (x < radius && y < radius)
                    {
                        // Bottom-left corner
                        float dist = Vector2.Distance(new Vector2(x, y), new Vector2(radius, radius));
                        alpha = Mathf.Clamp01(radius - dist + 0.5f);
                    }
                    else if (x >= width - radius && y < radius)
                    {
                        // Bottom-right corner
                        float dist = Vector2.Distance(new Vector2(x, y), new Vector2(width - radius - 1, radius));
                        alpha = Mathf.Clamp01(radius - dist + 0.5f);
                    }
                    else if (x < radius && y >= height - radius)
                    {
                        // Top-left corner
                        float dist = Vector2.Distance(new Vector2(x, y), new Vector2(radius, height - radius - 1));
                        alpha = Mathf.Clamp01(radius - dist + 0.5f);
                    }
                    else if (x >= width - radius && y >= height - radius)
                    {
                        // Top-right corner
                        float dist = Vector2.Distance(new Vector2(x, y), new Vector2(width - radius - 1, height - radius - 1));
                        alpha = Mathf.Clamp01(radius - dist + 0.5f);
                    }

                    pixels[y * width + x] = new Color(1f, 1f, 1f, alpha);
                }
            }

            texture.SetPixels(pixels);
            texture.Apply();

            // Create sprite with 9-slice borders for proper scaling
            int border = radius;
            roundedRectSprite = Sprite.Create(
                texture,
                new Rect(0, 0, width, height),
                new Vector2(0.5f, 0.5f),
                100f,
                0,
                SpriteMeshType.FullRect,
                new Vector4(border, border, border, border) // 9-slice borders
            );
        }

        private void EnsureCanvas()
        {
            canvas = GetComponent<Canvas>();
            if (canvas == null)
            {
                canvas = gameObject.AddComponent<Canvas>();
                canvas.renderMode = RenderMode.ScreenSpaceOverlay;
                canvas.sortingOrder = 100;

                var scaler = gameObject.AddComponent<CanvasScaler>();
                scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
                scaler.referenceResolution = new Vector2(1920, 1080);

                gameObject.AddComponent<GraphicRaycaster>();
            }
        }

        private void EnsureUI()
        {
            // Force position and size for existing container if it exists
            Transform existingContainer = transform.Find("ObjectiveContainer");
            if (existingContainer != null)
            {
                var existingRect = existingContainer.GetComponent<RectTransform>();
                if (existingRect != null)
                {
                    existingRect.anchorMin = new Vector2(1, 1);
                    existingRect.anchorMax = new Vector2(1, 1);
                    existingRect.pivot = new Vector2(1, 1);
                    existingRect.anchoredPosition = new Vector2(-35, -20);
                    existingRect.sizeDelta = new Vector2(320, 160);
                }
            }

            if (objectiveText == null)
            {
                // Create container with CanvasGroup for fading - TOP RIGHT position
                GameObject container = new GameObject("ObjectiveContainer");
                container.transform.SetParent(transform, false);

                var containerRect = container.AddComponent<RectTransform>();
                containerRect.anchorMin = new Vector2(1, 1); // Top-right anchor
                containerRect.anchorMax = new Vector2(1, 1);
                containerRect.pivot = new Vector2(1, 1); // Pivot at top-right
                containerRect.anchoredPosition = new Vector2(-35, -20); // Force position: 35px from right, 20px from top
                containerRect.sizeDelta = new Vector2(320, 160);

                objectiveGroup = container.AddComponent<CanvasGroup>();
                objectiveGroup.alpha = 0f;

                // Create bubble background with rounded corners
                GameObject bubbleBg = new GameObject("BubbleBackground");
                bubbleBg.transform.SetParent(container.transform, false);

                var bgRect = bubbleBg.AddComponent<RectTransform>();
                bgRect.anchorMin = Vector2.zero;
                bgRect.anchorMax = Vector2.one;
                bgRect.offsetMin = Vector2.zero;
                bgRect.offsetMax = Vector2.zero;

                var bgImage = bubbleBg.AddComponent<Image>();
                bgImage.sprite = roundedRectSprite;
                bgImage.type = Image.Type.Sliced;
                bgImage.color = bubbleColor;
                bgImage.raycastTarget = false;

                // Create header text "MISSION"
                GameObject headerObj = new GameObject("HeaderText");
                headerObj.transform.SetParent(container.transform, false);

                var headerRect = headerObj.AddComponent<RectTransform>();
                headerRect.anchorMin = new Vector2(0, 1);
                headerRect.anchorMax = new Vector2(1, 1);
                headerRect.pivot = new Vector2(0.5f, 1);
                headerRect.anchoredPosition = new Vector2(0, -8);
                headerRect.sizeDelta = new Vector2(0, 24);

                var headerText = headerObj.AddComponent<TextMeshProUGUI>();
                headerText.text = "MISSION";
                headerText.fontSize = 14;
                headerText.fontStyle = FontStyles.Bold;
                headerText.color = headerColor;
                headerText.alignment = TextAlignmentOptions.Center;
                headerText.raycastTarget = false;

                // Create divider line under header
                GameObject divider = new GameObject("Divider");
                divider.transform.SetParent(container.transform, false);

                var dividerRect = divider.AddComponent<RectTransform>();
                dividerRect.anchorMin = new Vector2(0.1f, 1);
                dividerRect.anchorMax = new Vector2(0.9f, 1);
                dividerRect.pivot = new Vector2(0.5f, 1);
                dividerRect.anchoredPosition = new Vector2(0, -32);
                dividerRect.sizeDelta = new Vector2(0, 2);

                var dividerImage = divider.AddComponent<Image>();
                dividerImage.color = new Color(headerColor.r, headerColor.g, headerColor.b, 0.5f);
                dividerImage.raycastTarget = false;

                // Create objective text
                GameObject textObj = new GameObject("ObjectiveText");
                textObj.transform.SetParent(container.transform, false);

                var textRect = textObj.AddComponent<RectTransform>();
                textRect.anchorMin = new Vector2(0, 0);
                textRect.anchorMax = new Vector2(1, 1);
                textRect.offsetMin = new Vector2(12, 10); // Padding from left and bottom
                textRect.offsetMax = new Vector2(-12, -40); // Padding from right, space for header

                objectiveText = textObj.AddComponent<TextMeshProUGUI>();
                objectiveText.fontSize = fontSize;
                objectiveText.color = objectiveColor;
                objectiveText.alignment = TextAlignmentOptions.TopLeft;
                objectiveText.enableWordWrapping = true;
                objectiveText.raycastTarget = false;
                objectiveText.lineSpacing = 8f; // Extra spacing between lines/sentences

                // Add subtle outline for better readability
                objectiveText.outlineWidth = 0.1f;
                objectiveText.outlineColor = new Color(0, 0, 0, 0.4f);
            }
        }

        private void EnsureToastUI()
        {
            if (toastText != null) return;

            // Force position below money display (override any old serialized values)
            Vector2 forcePosition = new Vector2(20, -180);

            // Create toast container (top-left, below money counter)
            GameObject container = new GameObject("ToastContainer");
            container.transform.SetParent(transform, false);

            var containerRect = container.AddComponent<RectTransform>();
            containerRect.anchorMin = new Vector2(0, 1);
            containerRect.anchorMax = new Vector2(0, 1);
            containerRect.pivot = new Vector2(0, 1);
            containerRect.anchoredPosition = forcePosition;
            containerRect.sizeDelta = new Vector2(400, 80);

            toastGroup = container.AddComponent<CanvasGroup>();
            toastGroup.alpha = 0f;

            // Create text
            GameObject textObj = new GameObject("ToastText");
            textObj.transform.SetParent(container.transform, false);

            var textRect = textObj.AddComponent<RectTransform>();
            textRect.anchorMin = Vector2.zero;
            textRect.anchorMax = Vector2.one;
            textRect.offsetMin = Vector2.zero;
            textRect.offsetMax = Vector2.zero;

            toastText = textObj.AddComponent<TextMeshProUGUI>();
            toastText.fontSize = toastFontSize;
            toastText.color = toastColor;
            toastText.alignment = TextAlignmentOptions.TopLeft;
            toastText.enableWordWrapping = true;
            toastText.raycastTarget = false;

            // Add subtle outline
            toastText.outlineWidth = 0.15f;
            toastText.outlineColor = new Color(0, 0, 0, 0.6f);
        }

        private void EnsureHelpTextUI()
        {
            if (helpText != null) return;

            // Create help text container (below mission bubble on top-right)
            GameObject container = new GameObject("HelpTextContainer");
            container.transform.SetParent(transform, false);

            var containerRect = container.AddComponent<RectTransform>();
            containerRect.anchorMin = new Vector2(1, 1); // Top-right anchor
            containerRect.anchorMax = new Vector2(1, 1);
            containerRect.pivot = new Vector2(1, 1);
            containerRect.anchoredPosition = new Vector2(-35, -185); // Below mission bubble
            containerRect.sizeDelta = new Vector2(320, 50);

            helpTextGroup = container.AddComponent<CanvasGroup>();
            helpTextGroup.alpha = 0f;

            // Create background for help text with rounded corners
            GameObject bgObj = new GameObject("HelpBackground");
            bgObj.transform.SetParent(container.transform, false);

            var bgRect = bgObj.AddComponent<RectTransform>();
            bgRect.anchorMin = Vector2.zero;
            bgRect.anchorMax = Vector2.one;
            bgRect.offsetMin = Vector2.zero;
            bgRect.offsetMax = Vector2.zero;

            var bgImage = bgObj.AddComponent<Image>();
            bgImage.sprite = roundedRectSprite;
            bgImage.type = Image.Type.Sliced;
            bgImage.color = new Color(0.1f, 0.1f, 0.15f, 0.7f);
            bgImage.raycastTarget = false;

            // Create text
            GameObject textObj = new GameObject("HelpText");
            textObj.transform.SetParent(container.transform, false);

            var textRect = textObj.AddComponent<RectTransform>();
            textRect.anchorMin = Vector2.zero;
            textRect.anchorMax = Vector2.one;
            textRect.offsetMin = new Vector2(10, 5);
            textRect.offsetMax = new Vector2(-10, -5);

            helpText = textObj.AddComponent<TextMeshProUGUI>();
            helpText.fontSize = helpTextFontSize;
            helpText.color = helpTextColor;
            helpText.alignment = TextAlignmentOptions.TopRight;
            helpText.enableWordWrapping = true;
            helpText.raycastTarget = false;

            // Add subtle outline
            helpText.outlineWidth = 0.1f;
            helpText.outlineColor = new Color(0, 0, 0, 0.5f);
        }

        private void EnsureCenteredPopupUI()
        {
            if (centeredPopupContainer != null) return;

            // Create centered popup container (above crosshair)
            centeredPopupContainer = new GameObject("CenteredPopupContainer");
            centeredPopupContainer.transform.SetParent(transform, false);

            var containerRect = centeredPopupContainer.AddComponent<RectTransform>();
            containerRect.anchorMin = new Vector2(0.5f, 0.5f); // Center anchor
            containerRect.anchorMax = new Vector2(0.5f, 0.5f);
            containerRect.pivot = new Vector2(0.5f, 0.5f);
            containerRect.anchoredPosition = new Vector2(0, -100); // Below crosshair (treasure chest popup is above)
            containerRect.sizeDelta = new Vector2(450, 70);

            centeredPopupGroup = centeredPopupContainer.AddComponent<CanvasGroup>();
            centeredPopupGroup.alpha = 0f;

            // Create background
            GameObject bgObj = new GameObject("PopupBackground");
            bgObj.transform.SetParent(centeredPopupContainer.transform, false);

            var bgRect = bgObj.AddComponent<RectTransform>();
            bgRect.anchorMin = Vector2.zero;
            bgRect.anchorMax = Vector2.one;
            bgRect.offsetMin = Vector2.zero;
            bgRect.offsetMax = Vector2.zero;

            var bgImage = bgObj.AddComponent<Image>();
            bgImage.sprite = roundedRectSprite;
            bgImage.type = Image.Type.Sliced;
            bgImage.color = new Color(0.05f, 0.08f, 0.12f, 0.92f);
            bgImage.raycastTarget = false;

            // Create text
            GameObject textObj = new GameObject("PopupText");
            textObj.transform.SetParent(centeredPopupContainer.transform, false);

            var textRect = textObj.AddComponent<RectTransform>();
            textRect.anchorMin = Vector2.zero;
            textRect.anchorMax = Vector2.one;
            textRect.offsetMin = new Vector2(15, 10);
            textRect.offsetMax = new Vector2(-15, -10);

            centeredPopupText = textObj.AddComponent<TextMeshProUGUI>();
            centeredPopupText.fontSize = 22;
            centeredPopupText.color = new Color(1f, 0.95f, 0.7f, 1f);
            centeredPopupText.alignment = TextAlignmentOptions.Center;
            centeredPopupText.enableWordWrapping = true;
            centeredPopupText.raycastTarget = false;
            centeredPopupText.fontStyle = FontStyles.Bold;

            // Add outline for readability
            centeredPopupText.outlineWidth = 0.15f;
            centeredPopupText.outlineColor = new Color(0, 0, 0, 0.6f);
        }

        private void EnsureDigCounterUI()
        {
            if (digCounterContainer != null) return;

            // Create dig counter container (below centered popup position, above crosshair)
            digCounterContainer = new GameObject("DigCounterContainer");
            digCounterContainer.transform.SetParent(transform, false);

            var containerRect = digCounterContainer.AddComponent<RectTransform>();
            containerRect.anchorMin = new Vector2(0.5f, 0.5f); // Center anchor
            containerRect.anchorMax = new Vector2(0.5f, 0.5f);
            containerRect.pivot = new Vector2(0.5f, 0.5f);
            containerRect.anchoredPosition = new Vector2(0, 170); // Above crosshair and above centered popup
            containerRect.sizeDelta = new Vector2(200, 50);

            digCounterGroup = digCounterContainer.AddComponent<CanvasGroup>();
            digCounterGroup.alpha = 0f;

            // Create background
            GameObject bgObj = new GameObject("CounterBackground");
            bgObj.transform.SetParent(digCounterContainer.transform, false);

            var bgRect = bgObj.AddComponent<RectTransform>();
            bgRect.anchorMin = Vector2.zero;
            bgRect.anchorMax = Vector2.one;
            bgRect.offsetMin = Vector2.zero;
            bgRect.offsetMax = Vector2.zero;

            var bgImage = bgObj.AddComponent<Image>();
            bgImage.sprite = roundedRectSprite;
            bgImage.type = Image.Type.Sliced;
            bgImage.color = new Color(0.05f, 0.08f, 0.12f, 0.85f);
            bgImage.raycastTarget = false;

            // Create text
            GameObject textObj = new GameObject("CounterText");
            textObj.transform.SetParent(digCounterContainer.transform, false);

            var textRect = textObj.AddComponent<RectTransform>();
            textRect.anchorMin = Vector2.zero;
            textRect.anchorMax = Vector2.one;
            textRect.offsetMin = new Vector2(10, 5);
            textRect.offsetMax = new Vector2(-10, -5);

            digCounterText = textObj.AddComponent<TextMeshProUGUI>();
            digCounterText.fontSize = 24;
            digCounterText.color = new Color(1f, 0.9f, 0.5f, 1f);
            digCounterText.alignment = TextAlignmentOptions.Center;
            digCounterText.enableWordWrapping = false;
            digCounterText.raycastTarget = false;
            digCounterText.fontStyle = FontStyles.Bold;

            // Add outline for readability
            digCounterText.outlineWidth = 0.15f;
            digCounterText.outlineColor = new Color(0, 0, 0, 0.6f);
        }

        private void EnsureCrosshairHintUI()
        {
            if (crosshairHintContainer != null) return;

            // Create crosshair hint container (below crosshair, centered)
            crosshairHintContainer = new GameObject("CrosshairHintContainer");
            crosshairHintContainer.transform.SetParent(transform, false);

            var containerRect = crosshairHintContainer.AddComponent<RectTransform>();
            containerRect.anchorMin = new Vector2(0.5f, 0.5f); // Center anchor
            containerRect.anchorMax = new Vector2(0.5f, 0.5f);
            containerRect.pivot = new Vector2(0.5f, 0.5f);
            containerRect.anchoredPosition = new Vector2(0, -60); // Below crosshair
            containerRect.sizeDelta = new Vector2(300, 45);

            crosshairHintGroup = crosshairHintContainer.AddComponent<CanvasGroup>();
            crosshairHintGroup.alpha = 0f;

            // Create background
            GameObject bgObj = new GameObject("HintBackground");
            bgObj.transform.SetParent(crosshairHintContainer.transform, false);

            var bgRect = bgObj.AddComponent<RectTransform>();
            bgRect.anchorMin = Vector2.zero;
            bgRect.anchorMax = Vector2.one;
            bgRect.offsetMin = Vector2.zero;
            bgRect.offsetMax = Vector2.zero;

            var bgImage = bgObj.AddComponent<Image>();
            bgImage.sprite = roundedRectSprite;
            bgImage.type = Image.Type.Sliced;
            bgImage.color = new Color(0.08f, 0.12f, 0.18f, 0.9f);
            bgImage.raycastTarget = false;

            // Create text with key hint styling
            GameObject textObj = new GameObject("HintText");
            textObj.transform.SetParent(crosshairHintContainer.transform, false);

            var textRect = textObj.AddComponent<RectTransform>();
            textRect.anchorMin = Vector2.zero;
            textRect.anchorMax = Vector2.one;
            textRect.offsetMin = new Vector2(10, 5);
            textRect.offsetMax = new Vector2(-10, -5);

            crosshairHintText = textObj.AddComponent<TextMeshProUGUI>();
            crosshairHintText.fontSize = 20;
            crosshairHintText.color = new Color(0.9f, 0.95f, 1f, 1f);
            crosshairHintText.alignment = TextAlignmentOptions.Center;
            crosshairHintText.enableWordWrapping = false;
            crosshairHintText.raycastTarget = false;
            crosshairHintText.fontStyle = FontStyles.Bold;

            // Add outline for readability
            crosshairHintText.outlineWidth = 0.15f;
            crosshairHintText.outlineColor = new Color(0, 0, 0, 0.7f);
        }

        /// <summary>
        /// Show a hint near the crosshair (e.g., "Press Q to activate the Scanner").
        /// </summary>
        public void ShowCrosshairHint(string text)
        {
            if (crosshairHintText == null || string.IsNullOrEmpty(text)) return;

            crosshairHintText.text = text;
            crosshairHintGroup.alpha = 1f;
        }

        /// <summary>
        /// Hide the crosshair hint.
        /// </summary>
        public void HideCrosshairHint()
        {
            if (crosshairHintGroup != null)
            {
                crosshairHintGroup.alpha = 0f;
            }
        }

        /// <summary>
        /// Show the counter with initial values.
        /// </summary>
        public void ShowCounter(int current, int required, string label = "Digs")
        {
            if (digCounterText == null) return;

            digCounterText.text = $"{label}: {current} / {required}";
            digCounterGroup.alpha = 1f;
        }

        /// <summary>
        /// Update the counter display.
        /// </summary>
        public void UpdateCounter(int current, int required, string label = "Digs")
        {
            if (digCounterText == null) return;

            digCounterText.text = $"{label}: {current} / {required}";
        }

        /// <summary>
        /// Hide the counter.
        /// </summary>
        public void HideCounter()
        {
            if (digCounterGroup != null)
            {
                digCounterGroup.alpha = 0f;
            }
        }

        // Legacy methods for backwards compatibility
        public void ShowDigCounter(int current, int required) => ShowCounter(current, required, "Digs");
        public void UpdateDigCounter(int current, int required) => UpdateCounter(current, required, "Digs");
        public void HideDigCounter() => HideCounter();

        /// <summary>
        /// Show a centered popup above the crosshair that fades out after duration.
        /// </summary>
        public void ShowCenteredPopup(string message, float duration = 4f)
        {
            if (centeredPopupText == null) return;

            centeredPopupText.text = message;

            if (centeredPopupCoroutine != null)
                StopCoroutine(centeredPopupCoroutine);

            centeredPopupCoroutine = StartCoroutine(CenteredPopupRoutine(duration));
        }

        private IEnumerator CenteredPopupRoutine(float duration)
        {
            // Fade in
            while (centeredPopupGroup.alpha < 1f)
            {
                centeredPopupGroup.alpha = Mathf.MoveTowards(centeredPopupGroup.alpha, 1f, 4f * Time.deltaTime);
                yield return null;
            }
            centeredPopupGroup.alpha = 1f;

            // Wait for duration
            yield return new WaitForSeconds(duration);

            // Fade out
            while (centeredPopupGroup.alpha > 0f)
            {
                centeredPopupGroup.alpha = Mathf.MoveTowards(centeredPopupGroup.alpha, 0f, 2f * Time.deltaTime);
                yield return null;
            }
            centeredPopupGroup.alpha = 0f;
        }

        /// <summary>
        /// Show help text below the objective.
        /// </summary>
        public void ShowHelpText(string text)
        {
            if (helpText == null || string.IsNullOrEmpty(text)) return;

            helpText.text = text;
            helpTextGroup.alpha = 1f;
        }

        /// <summary>
        /// Hide the help text.
        /// </summary>
        public void HideHelpText()
        {
            if (helpTextGroup != null)
            {
                helpTextGroup.alpha = 0f;
            }
        }

        /// <summary>
        /// Show a completion toast message that fades out after a duration.
        /// </summary>
        public void ShowCompletionToast(string message, float duration)
        {
            if (toastText == null) return;

            toastText.text = message;

            if (toastCoroutine != null)
                StopCoroutine(toastCoroutine);

            toastCoroutine = StartCoroutine(ToastRoutine(duration));
        }

        private IEnumerator ToastRoutine(float duration)
        {
            // Fade in
            while (toastGroup.alpha < 1f)
            {
                toastGroup.alpha = Mathf.MoveTowards(toastGroup.alpha, 1f, fadeSpeed * Time.deltaTime);
                yield return null;
            }
            toastGroup.alpha = 1f;

            // Wait for duration
            yield return new WaitForSeconds(duration);

            // Fade out
            while (toastGroup.alpha > 0f)
            {
                toastGroup.alpha = Mathf.MoveTowards(toastGroup.alpha, 0f, fadeSpeed * Time.deltaTime);
                yield return null;
            }
            toastGroup.alpha = 0f;
        }

        /// <summary>
        /// Show an objective with fade in.
        /// If text is empty, keeps the previous objective visible.
        /// </summary>
        public void ShowObjective(string text)
        {
            if (objectiveText == null) return;

            // If empty text, keep previous objective visible
            if (string.IsNullOrEmpty(text)) return;

            objectiveText.text = FormatObjectiveText(text);

            // Set alpha to 1 immediately for visibility
            if (objectiveGroup != null)
            {
                objectiveGroup.alpha = 1f;
            }

            if (fadeCoroutine != null)
                StopCoroutine(fadeCoroutine);

            fadeCoroutine = StartCoroutine(FadeRoutine(1f));
        }

        /// <summary>
        /// Format objective text to add spacing between sentences.
        /// </summary>
        private string FormatObjectiveText(string text)
        {
            if (string.IsNullOrEmpty(text)) return text;

            // Replace ". " with ".\n\n" to put sentences on separate lines with extra spacing
            // This adds visual spacing between multiple objectives/sentences
            text = text.Replace(". ", ".\n\n");

            return text;
        }

        /// <summary>
        /// Hide the objective with fade out.
        /// </summary>
        public void HideObjective()
        {
            if (objectiveGroup == null) return;

            if (fadeCoroutine != null)
                StopCoroutine(fadeCoroutine);

            fadeCoroutine = StartCoroutine(FadeRoutine(0f));
        }

        /// <summary>
        /// Update objective text without animation.
        /// </summary>
        public void UpdateObjective(string text)
        {
            if (objectiveText != null)
            {
                objectiveText.text = FormatObjectiveText(text);
            }
        }

        private IEnumerator FadeRoutine(float targetAlpha)
        {
            if (objectiveGroup == null) yield break;

            while (!Mathf.Approximately(objectiveGroup.alpha, targetAlpha))
            {
                objectiveGroup.alpha = Mathf.MoveTowards(objectiveGroup.alpha, targetAlpha, fadeSpeed * Time.deltaTime);
                yield return null;
            }

            objectiveGroup.alpha = targetAlpha;
        }
    }
}
