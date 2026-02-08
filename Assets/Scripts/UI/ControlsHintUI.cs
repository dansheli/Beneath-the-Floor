using TMPro;
using UnityEngine;
using UnityEngine.Scripting;
using UnityEngine.UI;
using BeneathTheFloor.Player;
using BeneathTheFloor.Tools;

namespace BeneathTheFloor.UI
{
    /// <summary>
    /// Displays a small controls guide on the right side of the screen.
    /// Shows key bindings for common actions.
    /// </summary>
    [Preserve]
    public class ControlsHintUI : MonoBehaviour
    {
        [Header("Display Settings")]
        [Tooltip("Font size for the hint text.")]
        [SerializeField] private int fontSize = 22;

        [Tooltip("Opacity of the hint panel (0-1).")]
        [Range(0f, 1f)]
        [SerializeField] private float opacity = 0.7f;

        [Tooltip("Offset from the right edge of the screen.")]
        [SerializeField] private float rightOffset = 20f;

        [Tooltip("Offset from the bottom of the screen.")]
        [SerializeField] private float bottomOffset = 160f;

        [Header("Colors")]
        [SerializeField] private Color keyColor = new Color(1f, 0.9f, 0.4f); // Yellow for keys
        [SerializeField] private Color actionColor = new Color(0.9f, 0.9f, 0.9f); // Light gray for actions

        [Header("Visibility")]
        [Tooltip("Key to toggle visibility of the hints.")]
        [SerializeField] private KeyCode toggleKey = KeyCode.F1;
        [SerializeField] private bool startVisible = true;

        private GameObject _panelObject;
        private TextMeshProUGUI _hintText;
        private Canvas _canvas;
        private bool _isVisible;
        private string _keyHex;
        private string _actionHex;
        private bool _hasJetpack = false;
        private bool _hasDrillPike = false;
        private bool _hasMultipleRadarModes = false;

        public static ControlsHintUI Instance { get; private set; }

        [Preserve]
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetStatics()
        {
            Instance = null;
        }

        /// <summary>
        /// Auto-create the controls hint UI when the scene loads.
        /// </summary>
        [Preserve]
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void AutoCreate()
        {
            // Don't create in menu scenes
            string sceneName = UnityEngine.SceneManagement.SceneManager.GetActiveScene().name.ToLower();

            if (sceneName.Contains("menu") || sceneName.Contains("press"))
            {
                return;
            }

            // Check if already exists
            if (Instance != null)
            {
                return;
            }

            try
            {
                // Create the controls hint UI
                GameObject hintObj = new GameObject("ControlsHintUI");
                hintObj.AddComponent<ControlsHintUI>();
            }
            catch (System.Exception e)
            {
                Debug.LogError($"[ControlsHintUI] Failed to create: {e.Message}\n{e.StackTrace}");
            }
        }

        private void Awake()
        {
            if (Instance == null)
            {
                Instance = this;
            }
            else
            {
                Destroy(gameObject);
                return;
            }
        }

        private void Start()
        {
            try
            {
                CreateUI();
                _isVisible = startVisible;
                if (_panelObject != null)
                {
                    _panelObject.SetActive(_isVisible);
                }
                else
                {
                    Debug.LogError("[ControlsHintUI] Panel object is null after CreateUI!");
                }
            }
            catch (System.Exception e)
            {
                Debug.LogError($"[ControlsHintUI] Start failed: {e.Message}\n{e.StackTrace}");
            }
        }

        private void Update()
        {
            // Toggle visibility with F1
            if (Input.GetKeyDown(toggleKey))
            {
                _isVisible = !_isVisible;
                if (_panelObject != null)
                    _panelObject.SetActive(_isVisible);
            }
        }

        private void OnEnable()
        {
            // Subscribe to pickup events
            JetpackPickup.OnJetpackPickedUp += OnJetpackPickedUp;
            DrillPikePickup.OnDrillPikePickedUp += OnDrillPikePickedUp;
            RadarTool.OnRadarModeChanged += OnRadarModeChanged;
        }

        private void OnDisable()
        {
            // Unsubscribe from pickup events
            JetpackPickup.OnJetpackPickedUp -= OnJetpackPickedUp;
            DrillPikePickup.OnDrillPikePickedUp -= OnDrillPikePickedUp;
            RadarTool.OnRadarModeChanged -= OnRadarModeChanged;
        }

        private void OnDestroy()
        {
            if (Instance == this)
                Instance = null;
        }

        private void OnJetpackPickedUp()
        {
            _hasJetpack = true;
            UpdateHintText();
        }

        private void OnDrillPikePickedUp()
        {
            _hasDrillPike = true;
            UpdateHintText();
        }

        private void OnRadarModeChanged(RadarMode mode)
        {
            if (!_hasMultipleRadarModes && RadarTool.Instance != null && RadarTool.Instance.HasMultipleModes)
            {
                _hasMultipleRadarModes = true;
                UpdateHintText();
            }
        }

        private void UpdateHintText()
        {
            if (_hintText == null) return;

            // Build hint text - "F Go Up" becomes "Hold Space Fly" when jetpack is picked up
            string flyControl = _hasJetpack
                ? $"<color=#{_keyHex}>Hold Space</color> <color=#{_actionHex}>Fly</color>"
                : $"<color=#{_keyHex}>F</color> <color=#{_actionHex}>Go Up</color>";

            // Base controls
            string hints = $"<color=#{_keyHex}>LMB</color> <color=#{_actionHex}>Dig</color>\n";

            // Add super hit if player has drill pike
            if (_hasDrillPike)
            {
                hints += $"<color=#{_keyHex}>Hold LMB</color> <color=#{_actionHex}>Super Hit</color>\n";
            }

            hints += $"<color=#{_keyHex}>E</color> <color=#{_actionHex}>Collect / Interact</color>\n" +
                    flyControl + "\n" +
                    $"<color=#{_keyHex}>Q</color> <color=#{_actionHex}>Radar</color>\n";

            if (_hasMultipleRadarModes)
            {
                hints += $"<color=#{_keyHex}>Tab</color> <color=#{_actionHex}>Radar Mode</color>\n";
            }

            hints += $"<color=#{_keyHex}>I</color> <color=#{_actionHex}>Inventory</color>";

            _hintText.text = hints;
        }

        private void CreateUI()
        {
            // Find or create canvas
            _canvas = FindHUDCanvas();
            if (_canvas == null)
            {
                _canvas = CreateCanvas();
            }

            // Create panel container
            _panelObject = new GameObject("ControlsHintPanel");
            _panelObject.transform.SetParent(_canvas.transform, false);

            // Add background image (semi-transparent)
            Image bgImage = _panelObject.AddComponent<Image>();
            bgImage.color = new Color(0f, 0f, 0f, 0.4f * opacity);

            // Setup RectTransform - anchor to bottom-right
            RectTransform panelRect = _panelObject.GetComponent<RectTransform>();
            panelRect.anchorMin = new Vector2(1, 0);
            panelRect.anchorMax = new Vector2(1, 0);
            panelRect.pivot = new Vector2(1, 0);
            panelRect.anchoredPosition = new Vector2(-rightOffset, bottomOffset);
            panelRect.sizeDelta = new Vector2(220, 160);

            // Add vertical layout
            VerticalLayoutGroup layout = _panelObject.AddComponent<VerticalLayoutGroup>();
            layout.padding = new RectOffset(12, 12, 10, 10);
            layout.spacing = 4;
            layout.childAlignment = TextAnchor.UpperLeft;
            layout.childControlWidth = true;
            layout.childControlHeight = true;
            layout.childForceExpandWidth = true;
            layout.childForceExpandHeight = false;

            // Add content size fitter
            ContentSizeFitter fitter = _panelObject.AddComponent<ContentSizeFitter>();
            fitter.horizontalFit = ContentSizeFitter.FitMode.PreferredSize;
            fitter.verticalFit = ContentSizeFitter.FitMode.PreferredSize;

            // Create hint text
            GameObject textObj = new GameObject("HintText");
            textObj.transform.SetParent(_panelObject.transform, false);

            _hintText = textObj.AddComponent<TextMeshProUGUI>();
            _hintText.fontSize = fontSize;
            _hintText.alignment = TextAlignmentOptions.TopLeft;
            _hintText.color = new Color(1f, 1f, 1f, opacity);

            // Build the hint text with color tags
            _keyHex = ColorUtility.ToHtmlStringRGB(keyColor);
            _actionHex = ColorUtility.ToHtmlStringRGB(actionColor);

            UpdateHintText();

            // Add outline for readability
            _hintText.outlineWidth = 0.15f;
            _hintText.outlineColor = new Color(0, 0, 0, 0.8f);
        }

        private Canvas FindHUDCanvas()
        {
            Canvas[] canvases = FindObjectsOfType<Canvas>();
            foreach (var c in canvases)
            {
                if (c.renderMode == RenderMode.ScreenSpaceOverlay &&
                    (c.name.Contains("HUD") || c.name.Contains("UI")))
                {
                    return c;
                }
            }

            // Return any overlay canvas
            foreach (var c in canvases)
            {
                if (c.renderMode == RenderMode.ScreenSpaceOverlay)
                {
                    return c;
                }
            }

            return null;
        }

        private Canvas CreateCanvas()
        {
            GameObject canvasObj = new GameObject("ControlsHintCanvas");
            Canvas canvas = canvasObj.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = 50;

            CanvasScaler scaler = canvasObj.AddComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920, 1080);

            canvasObj.AddComponent<GraphicRaycaster>();

            return canvas;
        }

        /// <summary>
        /// Show or hide the controls hint.
        /// </summary>
        public void SetVisible(bool visible)
        {
            _isVisible = visible;
            if (_panelObject != null)
                _panelObject.SetActive(_isVisible);
        }

        /// <summary>
        /// Toggle visibility.
        /// </summary>
        public void Toggle()
        {
            SetVisible(!_isVisible);
        }
    }
}
