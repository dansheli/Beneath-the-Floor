using UnityEngine;

namespace BeneathTheFloor.Digging
{
    /// <summary>
    /// Bridge component that connects digging system to resource spawning.
    /// Listens to dig events and spawns resources using the existing infrastructure.
    ///
    /// NOTE: This is the OLD resource system. Set IsDisabled = true to use the new
    /// Dust + Hidden Nodes system instead.
    /// </summary>
    public class ResourceSpawner : MonoBehaviour
    {
        [Header("Resource Configuration")]
        [Tooltip("Resource table defining what spawns at each depth.")]
        [SerializeField] private UndergroundResourceTable resourceTable;

        [Header("Depth Configuration")]
        [Tooltip("Y position of the basement floor (surface level).")]
        [SerializeField] private float basementFloorY = -3.0f;

        [Tooltip("Depth offset below basement before resources can spawn.")]
        [SerializeField] private float soilStartOffset = 5f;

        [Header("Debug")]
        [SerializeField] private bool enableDebugLogs = false;

        // References
        private DiggingSystem _diggingSystem;
        private DigWorldDropSpawner _dropSpawner;

        public static ResourceSpawner Instance { get; private set; }

        /// <summary>
        /// Static flag to disable this spawner globally.
        /// Set to true when using the new Dust + Hidden Nodes system.
        /// </summary>
        public static bool IsDisabled { get; set; } = false;

        private void Awake()
        {
            // FORCE disable debug logs (scene-serialized value may be true)
            enableDebugLogs = false;

            if (Instance != null && Instance != this)
            {
                if (enableDebugLogs) Debug.LogWarning("[ResourceSpawner] Duplicate instance, destroying.");
                Destroy(gameObject);
                return;
            }
            Instance = this;
        }

        private void Start()
        {
            // Find DiggingSystem
            _diggingSystem = DiggingSystem.Instance;
            if (_diggingSystem == null)
            {
                _diggingSystem = FindObjectOfType<DiggingSystem>();
            }

            if (_diggingSystem == null)
            {
                if (enableDebugLogs) Debug.LogError("[ResourceSpawner] No DiggingSystem found! Resource spawning disabled.");
                enabled = false;
                return;
            }

            // Find DigWorldDropSpawner
            _dropSpawner = DigWorldDropSpawner.Instance;
            if (_dropSpawner == null)
            {
                _dropSpawner = FindObjectOfType<DigWorldDropSpawner>();
            }

            // Find resource table if not assigned
            if (resourceTable == null)
            {
                // Try Resources folder
                resourceTable = UnityEngine.Resources.Load<UndergroundResourceTable>("UndergroundResourceTable");
            }

            if (resourceTable == null)
            {
                if (enableDebugLogs) Debug.LogError("[ResourceSpawner] No UndergroundResourceTable found! Resource spawning disabled.");
                enabled = false;
                return;
            }

            // Try to get depth config from DepthManager if available
            var depthManager = FindObjectOfType<DepthManager>();
            if (depthManager != null)
            {
                basementFloorY = depthManager.BasementFloorY;
                soilStartOffset = depthManager.SoilStartOffset;
            }

            // Subscribe to dig events
            _diggingSystem.OnDigCompleted += HandleDigCompleted;
        }

        private void OnDestroy()
        {
            if (Instance == this)
                Instance = null;

            if (_diggingSystem != null)
            {
                _diggingSystem.OnDigCompleted -= HandleDigCompleted;
            }
        }

        /// <summary>
        /// Handle dig completion - determine resources and spawn drops.
        /// </summary>
        private void HandleDigCompleted(DigResult digResult)
        {
            // Check if disabled (new Dust + Hidden Nodes system active)
            if (IsDisabled)
                return;

            if (!digResult.Success)
                return;

            // Get dig position from the operation
            Vector3 hitPosition = digResult.Operation.WorldPosition;

            // Calculate depth
            float rawDepth = basementFloorY - hitPosition.y;
            float depthBelowSoil = Mathf.Max(0f, rawDepth - soilStartOffset);

            if (enableDebugLogs)
            {
                Debug.Log($"[ResourceSpawner] Dig at Y={hitPosition.y:F2}, rawDepth={rawDepth:F2}, depthBelowSoil={depthBelowSoil:F2}");
            }

            // Not deep enough for resources
            if (depthBelowSoil <= 0f)
            {
                if (enableDebugLogs)
                    Debug.Log("[ResourceSpawner] Not deep enough for resources yet.");
                return;
            }

            // Get resource type from table
            UndergroundResourceType resourceType = resourceTable.GetResourceForDepth(depthBelowSoil);

            if (resourceType == UndergroundResourceType.None)
            {
                if (enableDebugLogs)
                    Debug.Log($"[ResourceSpawner] No resource rolled at depth {depthBelowSoil:F1}m");
                return;
            }

            // Get layer info
            var layer = resourceTable.GetLayerForDepth(depthBelowSoil);
            int layerIndex = resourceTable.GetLayerIndex(depthBelowSoil);

            // Determine amount (from layer's resource config)
            int amount = 1;
            if (layer != null)
            {
                foreach (var rc in layer.resources)
                {
                    if (rc.resourceType == resourceType)
                    {
                        amount = rc.RollAmount();
                        break;
                    }
                }
            }

            if (enableDebugLogs)
            {
                Debug.Log($"[ResourceSpawner] Spawning {amount}x {resourceType} at depth {depthBelowSoil:F1}m (Layer {layerIndex}: {layer?.layerName ?? "?"})");
            }

            // Spawn the drop using DigWorldDropSpawner
            if (_dropSpawner != null)
            {
                _dropSpawner.SpawnDropFromDigResult(hitPosition, resourceType, amount, depthBelowSoil, layerIndex, digResult.VolumeRemoved);
            }
            else
            {
                if (enableDebugLogs) Debug.LogWarning($"[ResourceSpawner] No drop spawner - {amount}x {resourceType} would spawn at {hitPosition}");
            }
        }

        /// <summary>
        /// Calculate depth below soil from world Y position.
        /// </summary>
        public float GetDepthBelowSoil(float worldY)
        {
            float rawDepth = basementFloorY - worldY;
            return Mathf.Max(0f, rawDepth - soilStartOffset);
        }

        /// <summary>
        /// Check if a world Y position is deep enough for resources.
        /// </summary>
        public bool IsDeepEnoughForResources(float worldY)
        {
            return GetDepthBelowSoil(worldY) > 0f;
        }
    }
}
