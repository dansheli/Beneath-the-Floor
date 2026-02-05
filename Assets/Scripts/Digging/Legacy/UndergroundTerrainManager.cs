using UnityEngine;

namespace BeneathTheFloor.Digging
{
    /// <summary>
    /// Legacy compatibility wrapper for UndergroundTerrainManager.
    /// Delegates to DepthManager and DigBoundsProvider for actual values.
    ///
    /// This exists to maintain compatibility with code that referenced the old V2 terrain manager.
    /// New code should use DepthManager and DigBoundsProvider directly.
    /// </summary>
    public class UndergroundTerrainManager : MonoBehaviour
    {
        public static UndergroundTerrainManager Instance { get; private set; }

        /// <summary>
        /// Returns true after Start() has completed and values are synced.
        /// </summary>
        public bool IsInitialized { get; private set; }

        [Header("Fallback Configuration")]
        [Tooltip("Basement floor Y position (used if DepthManager not available).")]
        public float basementFloorY = -3f;

        [Tooltip("Offset before soil/loot layers begin (used if DepthManager not available).")]
        public float soilStartOffsetMeters = 5f;

        [Tooltip("Horizontal extent of the terrain (used if DigBoundsProvider not available).")]
        public float horizontalExtent = 10f;

        [Tooltip("Maximum depth of the terrain (used if DigBoundsProvider not available).")]
        public float maxDepthMeters = 100f;

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
            SyncFromSources();
            IsInitialized = true;
        }

        private void OnDestroy()
        {
            if (Instance == this)
            {
                Instance = null;
            }
        }

        /// <summary>
        /// Sync configuration from DepthManager and DigBoundsProvider if available.
        /// </summary>
        public void SyncFromSources()
        {
            // Sync from DepthManager
            if (DepthManager.Instance != null)
            {
                basementFloorY = DepthManager.Instance.BasementFloorY;
                soilStartOffsetMeters = DepthManager.Instance.SoilStartOffset;
            }

            // Sync from DigBoundsProvider
            if (DigBoundsProvider.Instance != null && DigBoundsProvider.Instance.HasValidBounds)
            {
                Bounds bounds = DigBoundsProvider.Instance.DigBounds;
                horizontalExtent = Mathf.Max(bounds.size.x, bounds.size.z) / 2f;
                maxDepthMeters = bounds.size.y;
            }
        }

        /// <summary>
        /// Get the current terrain bounds.
        /// </summary>
        public Bounds GetTerrainBounds()
        {
            if (DigBoundsProvider.Instance != null && DigBoundsProvider.Instance.HasValidBounds)
            {
                return DigBoundsProvider.Instance.DigBounds;
            }

            // Fallback: construct from configuration
            Vector3 center = new Vector3(0, basementFloorY - maxDepthMeters / 2f, 0);
            Vector3 size = new Vector3(horizontalExtent * 2f, maxDepthMeters, horizontalExtent * 2f);
            return new Bounds(center, size);
        }
    }
}
