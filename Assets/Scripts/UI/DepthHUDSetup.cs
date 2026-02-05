using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace BeneathTheFloor.UI
{
    /// <summary>
    /// Sets up the Depth HUD at runtime.
    /// Creates a Canvas with TextMeshProUGUI and attaches DepthHUD_YBased.
    /// Add this component to any GameObject in the scene (or it will auto-create).
    /// </summary>
    public class DepthHUDSetup : MonoBehaviour
    {
        [Header("UI Settings")]
        [Tooltip("Position of the HUD text (anchor preset).")]
        [SerializeField] private TextAnchor anchorPosition = TextAnchor.LowerLeft;

        [Tooltip("Offset from the anchor corner.")]
        [SerializeField] private Vector2 offsetFromAnchor = new Vector2(20f, 20f);

        [Tooltip("Font size for the depth text.")]
        [SerializeField] private int fontSize = 32;

        [Tooltip("Text color.")]
        [SerializeField] private Color textColor = Color.white;

        [Header("References (Auto-found if null)")]
        [Tooltip("Existing HUD Canvas to use. If null, creates a new one.")]
        [SerializeField] private Canvas targetCanvas;

        [Tooltip("Player transform to track. If null, auto-finds.")]
        [SerializeField] private Transform playerTransform;

        [Tooltip("Basement floor Y value. If 0, reads from managers.")]
        #pragma warning disable CS0414 // Reserved for manual override
        [SerializeField] private float basementFloorY = 0f;
        #pragma warning restore CS0414

        [Header("Debug")]
        [SerializeField] private bool enableDebugLogs = false;

        private GameObject _hudTextObject;
        private DepthHUD_YBased _depthHUD;

        public static DepthHUDSetup Instance { get; private set; }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetStatics()
        {
            Instance = null;
        }

        private void Awake()
        {
            if (Instance == null)
            {
                Instance = this;
            }
            else
            {
                Debug.LogWarning("[DepthHUDSetup] Multiple instances detected, destroying this one.");
                Destroy(gameObject);
                return;
            }
        }

        private void Start()
        {
            SetupDepthHUD();
        }

        private void OnDestroy()
        {
            if (Instance == this)
            {
                Instance = null;
            }
        }

        /// <summary>
        /// Set up the depth HUD UI and component.
        /// </summary>
        [ContextMenu("Setup Depth HUD")]
        public void SetupDepthHUD()
        {
            if (enableDebugLogs)
                Debug.Log("[DepthHUDSetup] Setting up Depth HUD...");

            // Step 1: Find or create canvas
            if (targetCanvas == null)
            {
                // Try to find existing HUD canvas
                Canvas[] canvases = FindObjectsOfType<Canvas>();
                foreach (var c in canvases)
                {
                    if (c.name.Contains("HUD") || c.name.Contains("UI"))
                    {
                        targetCanvas = c;
                        break;
                    }
                }

                // If still null, find any Screen Space Overlay canvas
                if (targetCanvas == null)
                {
                    foreach (var c in canvases)
                    {
                        if (c.renderMode == RenderMode.ScreenSpaceOverlay)
                        {
                            targetCanvas = c;
                            break;
                        }
                    }
                }

                // Create new canvas if none found
                if (targetCanvas == null)
                {
                    targetCanvas = CreateHUDCanvas();
                }
            }

            // Step 2: Create the depth text object
            _hudTextObject = new GameObject("DepthHUDText");
            _hudTextObject.transform.SetParent(targetCanvas.transform, false);

            // Add TextMeshProUGUI
            var tmpText = _hudTextObject.AddComponent<TextMeshProUGUI>();
            tmpText.text = "0.0m";
            tmpText.fontSize = fontSize;
            tmpText.color = textColor;
            tmpText.fontStyle = FontStyles.Bold;
            tmpText.alignment = TextAlignmentOptions.BottomLeft;

            // Add outline for visibility
            tmpText.outlineWidth = 0.2f;
            tmpText.outlineColor = Color.black;

            // Position the text (bottom-left by default)
            RectTransform rect = _hudTextObject.GetComponent<RectTransform>();
            SetAnchorPosition(rect);

            // Step 3: Add DepthHUD_YBased component
            _depthHUD = _hudTextObject.AddComponent<DepthHUD_YBased>();

            // Configure it
            // Use reflection or serialized field access if needed, but the component auto-finds most things

        }

        private Canvas CreateHUDCanvas()
        {
            GameObject canvasObj = new GameObject("DepthHUDCanvas");
            Canvas canvas = canvasObj.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = 100; // On top of most UI

            // Add required components
            var scaler = canvasObj.AddComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920, 1080);

            canvasObj.AddComponent<GraphicRaycaster>();

            if (enableDebugLogs)
                Debug.Log("[DepthHUDSetup] Created new HUD Canvas");

            return canvas;
        }

        private void SetAnchorPosition(RectTransform rect)
        {
            // Bottom-left anchor
            rect.anchorMin = new Vector2(0, 0);
            rect.anchorMax = new Vector2(0, 0);
            rect.pivot = new Vector2(0, 0);
            rect.anchoredPosition = offsetFromAnchor;
            rect.sizeDelta = new Vector2(280, 50);

            // Adjust based on anchorPosition enum if needed
            switch (anchorPosition)
            {
                case TextAnchor.LowerLeft:
                    rect.anchorMin = new Vector2(0, 0);
                    rect.anchorMax = new Vector2(0, 0);
                    rect.pivot = new Vector2(0, 0);
                    rect.anchoredPosition = offsetFromAnchor;
                    break;

                case TextAnchor.LowerRight:
                    rect.anchorMin = new Vector2(1, 0);
                    rect.anchorMax = new Vector2(1, 0);
                    rect.pivot = new Vector2(1, 0);
                    rect.anchoredPosition = new Vector2(-offsetFromAnchor.x, offsetFromAnchor.y);
                    break;

                case TextAnchor.UpperLeft:
                    rect.anchorMin = new Vector2(0, 1);
                    rect.anchorMax = new Vector2(0, 1);
                    rect.pivot = new Vector2(0, 1);
                    rect.anchoredPosition = new Vector2(offsetFromAnchor.x, -offsetFromAnchor.y);
                    break;

                case TextAnchor.UpperRight:
                    rect.anchorMin = new Vector2(1, 1);
                    rect.anchorMax = new Vector2(1, 1);
                    rect.pivot = new Vector2(1, 1);
                    rect.anchoredPosition = new Vector2(-offsetFromAnchor.x, -offsetFromAnchor.y);
                    break;
            }
        }

        /// <summary>
        /// Get the created DepthHUD_YBased component.
        /// </summary>
        public DepthHUD_YBased GetDepthHUD()
        {
            return _depthHUD;
        }
    }
}
