using UnityEngine;
using UnityEngine.UI;
using TMPro;

namespace BeneathTheFloor.Winch
{
    /// <summary>
    /// HUD display for winch cable status.
    /// Shows cable length, max length, and a progress bar.
    /// Auto-hides when cable is not attached.
    /// </summary>
    public class WinchHUD : MonoBehaviour
    {
        [Header("References")]
        [SerializeField] private WinchAnchor winchAnchor;
        [SerializeField] private PlayerWinchAttachment winchAttachment;
        [SerializeField] private WinchMotor motor;
        [SerializeField] private WinchCable cable;

        [Header("UI Elements")]
        [Tooltip("Text showing cable length (X.Xm / MaxXm).")]
        [SerializeField] private TextMeshProUGUI lengthText;

        [Tooltip("Optional tier name text.")]
        [SerializeField] private TextMeshProUGUI tierText;

        [Tooltip("Progress bar fill image (Image with type = Filled).")]
        [SerializeField] private Image progressBarFill;

        [Tooltip("Progress bar background.")]
        [SerializeField] private Image progressBarBackground;

        [Tooltip("Optional pull indicator.")]
        [SerializeField] private GameObject pullIndicator;

        [Tooltip("Container for all HUD elements (for show/hide).")]
        [SerializeField] private GameObject hudContainer;

        [Header("Colors")]
        public Color safeColor = new Color(0.2f, 0.8f, 0.2f, 1f);
        public Color warningColor = new Color(1f, 0.8f, 0.2f, 1f);
        public Color dangerColor = new Color(1f, 0.3f, 0.2f, 1f);

        [Header("Settings")]
        [Tooltip("Update rate (times per second).")]
        public float updateRate = 10f;

        [Tooltip("Show tier name in HUD.")]
        public bool showTierName = true;

        [Tooltip("Format string for length display. {0}=current, {1}=max")]
        public string lengthFormat = "{0:F1}m / {1:F0}m";

        [Header("Debug")]
        [SerializeField] private bool enableDebugLogs = false;

        // State
        private float lastUpdateTime;
        private bool isVisible = false;

        private void Start()
        {
            // Find references
            if (winchAnchor == null)
                winchAnchor = WinchAnchor.Instance ?? FindObjectOfType<WinchAnchor>();

            if (winchAttachment == null)
                winchAttachment = PlayerWinchAttachment.Instance ?? FindObjectOfType<PlayerWinchAttachment>();

            if (motor == null && winchAttachment != null)
                motor = winchAttachment.GetComponent<WinchMotor>();

            if (cable == null)
                cable = FindObjectOfType<WinchCable>();

            // Auto-create HUD if no container assigned
            if (hudContainer == null)
            {
                CreateRuntimeHUD();
            }

            // Subscribe to events
            if (winchAnchor != null)
            {
                winchAnchor.OnPlayerAttached += OnAttached;
                winchAnchor.OnPlayerDetached += OnDetached;
                winchAnchor.OnTierChanged += OnTierChanged;
            }
            else
            {
                Debug.LogWarning("[WinchHUD] No WinchAnchor found! HUD will not work.");
            }

            // Start hidden
            SetVisible(false);
        }

        private void OnDestroy()
        {
            if (winchAnchor != null)
            {
                winchAnchor.OnPlayerAttached -= OnAttached;
                winchAnchor.OnPlayerDetached -= OnDetached;
                winchAnchor.OnTierChanged -= OnTierChanged;
            }
        }

        private void Update()
        {
            if (!isVisible) return;

            // Throttle updates
            if (Time.time - lastUpdateTime < 1f / updateRate)
                return;

            lastUpdateTime = Time.time;
            UpdateHUD();
        }

        private void OnAttached(Transform player)
        {
            SetVisible(true);
            UpdateHUD();
        }

        private void OnDetached()
        {
            SetVisible(false);
        }

        private void OnTierChanged(int newTier)
        {
            UpdateTierDisplay();
            UpdateHUD();
        }

        /// <summary>
        /// Show or hide the HUD.
        /// </summary>
        public void SetVisible(bool visible)
        {
            isVisible = visible;

            if (hudContainer != null)
            {
                hudContainer.SetActive(visible);
            }

            if (enableDebugLogs)
                Debug.Log($"[WinchHUD] Visibility: {visible}");
        }

        private void UpdateHUD()
        {
            if (winchAnchor == null) return;

            // Use cable path length (accounts for wrapping) if available, else straight-line
            float currentLength = (cable != null && cable.PathCount > 0)
                ? cable.PathLength
                : winchAnchor.GetCurrentCableLength();
            float reeledLength = winchAnchor.EffectiveCableLength; // Show reeled length, not max
            float tension = winchAnchor.GetTension();

            // Update length text (show current distance / reeled cable length)
            if (lengthText != null)
            {
                lengthText.text = string.Format(lengthFormat, currentLength, reeledLength);

                // Color based on tension
                lengthText.color = GetColorForTension(tension);
            }

            // Update progress bar (how much cable is extended)
            if (progressBarFill != null)
            {
                float fillAmount = Mathf.Clamp01(currentLength / reeledLength);
                progressBarFill.fillAmount = fillAmount;
                progressBarFill.color = GetColorForTension(tension);
            }

            // Update pull indicator
            if (pullIndicator != null && motor != null)
            {
                pullIndicator.SetActive(motor.IsReeling);
            }
        }

        private void UpdateTierDisplay()
        {
            if (tierText == null || winchAnchor == null || winchAnchor.Config == null)
                return;

            var tier = winchAnchor.Config.GetTier(winchAnchor.CurrentTierIndex);
            if (tier != null && showTierName)
            {
                tierText.text = tier.tierName;
                tierText.gameObject.SetActive(true);
            }
            else
            {
                tierText.gameObject.SetActive(false);
            }
        }

        private Color GetColorForTension(float tension)
        {
            if (tension <= 0f)
                return safeColor;
            else if (tension < 0.7f)
                return Color.Lerp(safeColor, warningColor, tension / 0.7f);
            else
                return Color.Lerp(warningColor, dangerColor, (tension - 0.7f) / 0.3f);
        }

        /// <summary>
        /// Force update the HUD immediately.
        /// </summary>
        public void ForceUpdate()
        {
            UpdateHUD();
            UpdateTierDisplay();
        }

        /// <summary>
        /// Create HUD elements at runtime if none are assigned.
        /// Call this from editor script or initialization.
        /// </summary>
        [ContextMenu("Create Runtime HUD")]
        public void CreateRuntimeHUD()
        {
            if (hudContainer == null)
            {
                // Create canvas if needed
                Canvas canvas = GetComponentInChildren<Canvas>();
                if (canvas == null)
                {
                    GameObject canvasObj = new GameObject("WinchCanvas");
                    canvasObj.transform.SetParent(transform);
                    canvas = canvasObj.AddComponent<Canvas>();
                    canvas.renderMode = RenderMode.ScreenSpaceOverlay;
                    canvas.sortingOrder = 100;
                    canvasObj.AddComponent<CanvasScaler>();
                    canvasObj.AddComponent<GraphicRaycaster>();
                }

                // Create container
                hudContainer = new GameObject("WinchHUDContainer");
                hudContainer.transform.SetParent(canvas.transform);

                RectTransform containerRect = hudContainer.AddComponent<RectTransform>();
                containerRect.anchorMin = new Vector2(0f, 1f);
                containerRect.anchorMax = new Vector2(0f, 1f);
                containerRect.pivot = new Vector2(0f, 1f);
                containerRect.anchoredPosition = new Vector2(20f, -100f);
                containerRect.sizeDelta = new Vector2(200f, 60f);

                // Create background
                GameObject bgObj = new GameObject("Background");
                bgObj.transform.SetParent(hudContainer.transform);
                Image bg = bgObj.AddComponent<Image>();
                bg.color = new Color(0f, 0f, 0f, 0.5f);
                RectTransform bgRect = bg.rectTransform;
                bgRect.anchorMin = Vector2.zero;
                bgRect.anchorMax = Vector2.one;
                bgRect.sizeDelta = Vector2.zero;
                bgRect.anchoredPosition = Vector2.zero;

                // Create length text
                GameObject textObj = new GameObject("LengthText");
                textObj.transform.SetParent(hudContainer.transform);
                lengthText = textObj.AddComponent<TextMeshProUGUI>();
                lengthText.text = "0.0m / 10m";
                lengthText.fontSize = 18;
                lengthText.alignment = TextAlignmentOptions.Center;
                RectTransform textRect = lengthText.rectTransform;
                textRect.anchorMin = new Vector2(0f, 0.5f);
                textRect.anchorMax = new Vector2(1f, 1f);
                textRect.sizeDelta = Vector2.zero;
                textRect.anchoredPosition = Vector2.zero;

                // Create progress bar background
                GameObject barBgObj = new GameObject("ProgressBarBg");
                barBgObj.transform.SetParent(hudContainer.transform);
                progressBarBackground = barBgObj.AddComponent<Image>();
                progressBarBackground.color = new Color(0.2f, 0.2f, 0.2f, 1f);
                RectTransform barBgRect = progressBarBackground.rectTransform;
                barBgRect.anchorMin = new Vector2(0.05f, 0.1f);
                barBgRect.anchorMax = new Vector2(0.95f, 0.4f);
                barBgRect.sizeDelta = Vector2.zero;
                barBgRect.anchoredPosition = Vector2.zero;

                // Create progress bar fill
                GameObject barFillObj = new GameObject("ProgressBarFill");
                barFillObj.transform.SetParent(barBgObj.transform);
                progressBarFill = barFillObj.AddComponent<Image>();
                progressBarFill.color = safeColor;
                progressBarFill.type = Image.Type.Filled;
                progressBarFill.fillMethod = Image.FillMethod.Horizontal;
                progressBarFill.fillOrigin = 0;
                progressBarFill.fillAmount = 0f;
                RectTransform barFillRect = progressBarFill.rectTransform;
                barFillRect.anchorMin = Vector2.zero;
                barFillRect.anchorMax = Vector2.one;
                barFillRect.sizeDelta = Vector2.zero;
                barFillRect.anchoredPosition = Vector2.zero;

                Debug.Log("[WinchHUD] Runtime HUD created");
            }
        }
    }
}
