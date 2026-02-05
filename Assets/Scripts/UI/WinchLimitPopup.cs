using UnityEngine;
using UnityEngine.UI;
using TMPro;
using BeneathTheFloor.Winch;

namespace BeneathTheFloor.UI
{
    /// <summary>
    /// Shows a centered popup when the player reaches the winch cable limit.
    /// Light, natural style with automatic fade out.
    /// </summary>
    public class WinchLimitPopup : MonoBehaviour
    {
        private static WinchLimitPopup instance;

        [Header("Settings")]
        [SerializeField] private string message = "Cable too short - upgrade the winch";
        [SerializeField] private float displayDuration = 2.5f;
        [SerializeField] private float fadeInDuration = 0.25f;
        [SerializeField] private float fadeOutDuration = 0.5f;
        [SerializeField] private float cooldownDuration = 3f;

        // UI elements
        private GameObject popupPanel;
        private CanvasGroup canvasGroup;
        private TextMeshProUGUI messageText;

        // State
        private float displayTimer;
        private float cooldownTimer;
        private bool isShowing;
        private bool isFadingOut;

        private void Awake()
        {
            if (instance == null)
                instance = this;
            else if (instance != this)
            {
                Destroy(gameObject);
                return;
            }
        }

        private void Start()
        {
            CreateUI();

            // Subscribe to cable limit event
            WinchMotor.OnCableLimitReached += ShowPopup;
        }

        private void OnDestroy()
        {
            if (instance == this)
                instance = null;

            WinchMotor.OnCableLimitReached -= ShowPopup;
        }

        private void Update()
        {
            // Cooldown timer
            if (cooldownTimer > 0f)
                cooldownTimer -= Time.deltaTime;

            if (!isShowing) return;

            // Display timer
            if (!isFadingOut)
            {
                displayTimer -= Time.deltaTime;
                if (displayTimer <= 0f)
                {
                    isFadingOut = true;
                    displayTimer = fadeOutDuration;
                }
                else if (displayTimer > displayDuration - fadeInDuration)
                {
                    // Fade in
                    float t = 1f - (displayTimer - (displayDuration - fadeInDuration)) / fadeInDuration;
                    canvasGroup.alpha = Mathf.SmoothStep(0f, 1f, t);
                }
            }
            else
            {
                // Fade out
                displayTimer -= Time.deltaTime;
                float t = displayTimer / fadeOutDuration;
                canvasGroup.alpha = Mathf.SmoothStep(0f, 1f, t);

                if (displayTimer <= 0f)
                {
                    isShowing = false;
                    isFadingOut = false;
                    popupPanel.SetActive(false);
                }
            }
        }

        public void ShowPopup()
        {
            // Respect cooldown
            if (cooldownTimer > 0f) return;

            isShowing = true;
            isFadingOut = false;
            displayTimer = displayDuration;
            cooldownTimer = cooldownDuration;

            if (popupPanel != null)
            {
                popupPanel.SetActive(true);
                canvasGroup.alpha = 0f;
            }
        }

        private void CreateUI()
        {
            // Find or create canvas
            Canvas canvas = FindCanvas();
            if (canvas == null)
            {
                // Create our own canvas
                GameObject canvasObj = new GameObject("WinchLimitCanvas");
                canvasObj.transform.SetParent(transform);
                canvas = canvasObj.AddComponent<Canvas>();
                canvas.renderMode = RenderMode.ScreenSpaceOverlay;
                canvas.sortingOrder = 100;
                canvasObj.AddComponent<CanvasScaler>().uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
                canvasObj.GetComponent<CanvasScaler>().referenceResolution = new Vector2(1920, 1080);
            }

            // Create popup panel
            popupPanel = new GameObject("WinchLimitPopup");
            popupPanel.transform.SetParent(canvas.transform, false);

            RectTransform panelRect = popupPanel.AddComponent<RectTransform>();
            panelRect.anchorMin = new Vector2(0.5f, 0.5f);
            panelRect.anchorMax = new Vector2(0.5f, 0.5f);
            panelRect.pivot = new Vector2(0.5f, 0.5f);
            panelRect.anchoredPosition = new Vector2(0f, 50f); // Slightly above center
            panelRect.sizeDelta = new Vector2(500f, 60f);

            // Canvas group for fading
            canvasGroup = popupPanel.AddComponent<CanvasGroup>();
            canvasGroup.alpha = 0f;
            canvasGroup.blocksRaycasts = false;
            canvasGroup.interactable = false;

            // Background - subtle, semi-transparent
            Image bg = popupPanel.AddComponent<Image>();
            bg.color = new Color(0.05f, 0.05f, 0.08f, 0.75f);
            bg.raycastTarget = false;

            // Subtle rounded look via outline
            Outline outline = popupPanel.AddComponent<Outline>();
            outline.effectColor = new Color(0.9f, 0.7f, 0.3f, 0.4f); // Warm accent
            outline.effectDistance = new Vector2(1f, 1f);

            // Message text
            GameObject textObj = new GameObject("Message");
            textObj.transform.SetParent(popupPanel.transform, false);

            RectTransform textRect = textObj.AddComponent<RectTransform>();
            textRect.anchorMin = Vector2.zero;
            textRect.anchorMax = Vector2.one;
            textRect.offsetMin = new Vector2(20f, 8f);
            textRect.offsetMax = new Vector2(-20f, -8f);

            messageText = textObj.AddComponent<TextMeshProUGUI>();
            messageText.text = message;
            messageText.fontSize = 22f;
            messageText.fontStyle = FontStyles.Normal;
            messageText.color = new Color(1f, 0.95f, 0.85f); // Warm white
            messageText.alignment = TextAlignmentOptions.Center;
            messageText.raycastTarget = false;

            popupPanel.SetActive(false);
        }

        private Canvas FindCanvas()
        {
            string[] names = { "HUDCanvas", "GameCanvas", "MainCanvas", "UICanvas" };
            foreach (var n in names)
            {
                var obj = GameObject.Find(n);
                if (obj != null)
                {
                    var c = obj.GetComponent<Canvas>();
                    if (c != null) return c;
                }
            }
            return null;
        }

        /// <summary>
        /// Create and initialize the popup if it doesn't exist.
        /// Called from WinchAnchor.Start() to ensure proper initialization order.
        /// </summary>
        public static void EnsureExists()
        {
            if (instance != null) return;

            GameObject obj = new GameObject("WinchLimitPopup");
            obj.AddComponent<WinchLimitPopup>();
            DontDestroyOnLoad(obj);
            Debug.Log("[WinchLimitPopup] Created popup instance");
        }
    }
}
