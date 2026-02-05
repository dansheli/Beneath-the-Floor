using UnityEngine;

namespace BeneathTheFloor.Digging
{
    /// <summary>
    /// Legacy compatibility wrapper for GuidedPitController.
    /// Delegates to DigBoundsProvider for actual bounds.
    ///
    /// This exists to maintain compatibility with code that referenced the old V2 guided pit system.
    /// New code should use DigBoundsProvider directly.
    /// </summary>
    public class GuidedPitController : MonoBehaviour
    {
        public static GuidedPitController Instance { get; private set; }

        [Header("Fallback Configuration")]
        [Tooltip("Center position of the pit (used if DigBoundsProvider not available).")]
        [SerializeField] private Vector3 pitCenter = Vector3.zero;

        [Tooltip("Size of the pit bounds (used if DigBoundsProvider not available).")]
        [SerializeField] private Vector3 pitSize = new Vector3(20f, 50f, 20f);

        private void Awake()
        {
            if (Instance != null && Instance != this)
            {
                Destroy(gameObject);
                return;
            }
            Instance = this;
        }

        private void OnDestroy()
        {
            if (Instance == this)
            {
                Instance = null;
            }
        }

        /// <summary>
        /// Get the guided pit bounds.
        /// </summary>
        public Bounds GetGuidedPitBounds()
        {
            // Try to get from DigBoundsProvider first
            if (DigBoundsProvider.Instance != null && DigBoundsProvider.Instance.HasValidBounds)
            {
                return DigBoundsProvider.Instance.DigBounds;
            }

            // Fallback to configured values
            return new Bounds(pitCenter, pitSize);
        }

        /// <summary>
        /// Check if a position is within the guided pit bounds.
        /// </summary>
        public bool IsWithinPit(Vector3 worldPosition)
        {
            return GetGuidedPitBounds().Contains(worldPosition);
        }
    }
}
