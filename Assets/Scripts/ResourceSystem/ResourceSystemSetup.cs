using UnityEngine;
using BeneathTheFloor.Digging;

namespace BeneathTheFloor.ResourceSystem
{
    /// <summary>
    /// Setup component that initializes the new resource system.
    /// Attach to a GameObject in the scene to enable the system.
    /// </summary>
    public class ResourceSystemSetup : MonoBehaviour
    {
        [Header("Configuration")]
        [Tooltip("Resource system configuration asset.")]
        [SerializeField] private ResourceSystemConfig config;

        [Header("System Control")]
        [Tooltip("Enable the new resource system (Dust + Hidden Nodes).")]
        [SerializeField] private bool enableNewSystem = true;

        [Tooltip("Disable the old ResourceSpawner random drops.")]
        [SerializeField] private bool disableOldSpawner = true;

        [Header("Debug")]
        [SerializeField] private bool enableDebugLogs = false;

        public static ResourceSystemSetup Instance { get; private set; }
        public bool IsNewSystemEnabled => enableNewSystem;

        private void Awake()
        {
            if (Instance != null && Instance != this)
            {
                Destroy(gameObject);
                return;
            }
            Instance = this;
        }

        private void Start()
        {
            InitializeSystem();
        }

        private void InitializeSystem()
        {
            // Load config from Resources if not assigned
            if (config == null)
            {
                config = Resources.Load<ResourceSystemConfig>("ResourceSystemConfig");

                if (config == null && enableDebugLogs)
                {
                    Debug.Log("[ResourceSystemSetup] No config found in Resources, creating default.");
                }
            }

            // Disable old spawner if requested
            if (disableOldSpawner)
            {
                DisableOldSpawner();
            }

            if (!enableNewSystem)
            {
                if (enableDebugLogs)
                    Debug.Log("[ResourceSystemSetup] New resource system DISABLED.");
                return;
            }

            // Create DustManager if not exists
            EnsureDustManager();

            // Create HiddenNodeManager if not exists
            EnsureHiddenNodeManager();

            // Create ResourceSaveManager if not exists
            EnsureResourceSaveManager();

            // DustHUD DISABLED - dust value now shown in EstimatedValueHUD
            // EnsureDustHUD();

            if (enableDebugLogs)
                Debug.Log("[ResourceSystemSetup] New resource system initialized successfully.");
        }

        private void DisableOldSpawner()
        {
            // Set the static flag to disable old resource drops
            // This works even if ResourceSpawner subscribes to events before/after us
            ResourceSpawner.IsDisabled = true;

            if (enableDebugLogs)
                Debug.Log("[ResourceSystemSetup] Disabled old ResourceSpawner via static flag.");
        }

        private void EnsureDustManager()
        {
            if (DustManager.Instance != null)
                return;

            var existing = FindObjectOfType<DustManager>();
            if (existing != null)
                return;

            var obj = new GameObject("DustManager");
            var dustMgr = obj.AddComponent<DustManager>();

            // Assign config via reflection or serialized field
            // For now, DustManager will load from Resources

            if (enableDebugLogs)
                Debug.Log("[ResourceSystemSetup] Created DustManager.");
        }

        private void EnsureHiddenNodeManager()
        {
            if (HiddenNodeManager.Instance != null)
                return;

            var existing = FindObjectOfType<HiddenNodeManager>();
            if (existing != null)
                return;

            var obj = new GameObject("HiddenNodeManager");
            obj.AddComponent<HiddenNodeManager>();

            if (enableDebugLogs)
                Debug.Log("[ResourceSystemSetup] Created HiddenNodeManager.");
        }

        private void EnsureResourceSaveManager()
        {
            if (ResourceSaveManager.Instance != null)
                return;

            var existing = FindObjectOfType<ResourceSaveManager>();
            if (existing != null)
                return;

            var obj = new GameObject("ResourceSaveManager");
            obj.AddComponent<ResourceSaveManager>();

            if (enableDebugLogs)
                Debug.Log("[ResourceSystemSetup] Created ResourceSaveManager.");
        }

        private void EnsureDustHUD()
        {
            // Find existing DustHUD
            var existing = FindObjectOfType<DustHUD>();
            if (existing != null)
                return;

            // Find or create canvas
            Canvas canvas = FindObjectOfType<Canvas>();
            if (canvas == null)
            {
                if (enableDebugLogs)
                    Debug.Log("[ResourceSystemSetup] No Canvas found, skipping DustHUD creation.");
                return;
            }

            // Create DustHUD
            var hudObj = new GameObject("DustHUD");
            hudObj.transform.SetParent(canvas.transform, false);

            // Add RectTransform
            var rect = hudObj.AddComponent<RectTransform>();

            // Position in bottom-left, above depth HUD
            rect.anchorMin = new Vector2(0, 0);
            rect.anchorMax = new Vector2(0, 0);
            rect.pivot = new Vector2(0, 0);
            rect.anchoredPosition = new Vector2(20, 60); // Above depth HUD
            rect.sizeDelta = new Vector2(200, 30);

            // Add TextMeshPro
            var textObj = new GameObject("DustText");
            textObj.transform.SetParent(hudObj.transform, false);

            var textRect = textObj.AddComponent<RectTransform>();
            textRect.anchorMin = Vector2.zero;
            textRect.anchorMax = Vector2.one;
            textRect.offsetMin = Vector2.zero;
            textRect.offsetMax = Vector2.zero;

            var tmpText = textObj.AddComponent<TMPro.TextMeshProUGUI>();
            tmpText.text = "Dust: 0";
            tmpText.fontSize = 18;
            tmpText.color = new Color(0.9f, 0.8f, 0.5f);
            tmpText.alignment = TMPro.TextAlignmentOptions.Left;

            // Add DustHUD component
            hudObj.AddComponent<DustHUD>();

            if (enableDebugLogs)
                Debug.Log("[ResourceSystemSetup] Created DustHUD.");
        }

        /// <summary>
        /// Get the configuration asset.
        /// </summary>
        public ResourceSystemConfig GetConfig()
        {
            return config;
        }
    }
}
