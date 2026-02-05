using UnityEngine;

namespace BeneathTheFloor.WorldRooms
{
    /// <summary>
    /// Defines a single shaft wall segment type with a specific height.
    /// Each config has both a solid version and an opening version.
    /// </summary>
    [CreateAssetMenu(fileName = "ShaftSegment_", menuName = "Beneath The Floor/Shaft/Wall Segment Config")]
    public class ShaftWallSegmentConfig : ScriptableObject
    {
        [Header("Identification")]
        [Tooltip("Unique ID for this segment type, e.g. 'Segment_6m'")]
        public string segmentId = "Segment_Default";

        [Header("Dimensions")]
        [Tooltip("Height of this segment in meters")]
        [Min(1f)]
        public float height = 6f;

        [Tooltip("Width of the segment (should match shaft width = 2 * shaftHalfWidth)")]
        [Min(1f)]
        public float width = 12f;

        [Tooltip("Thickness of the wall")]
        [Min(0.1f)]
        public float thickness = 0.4f;

        [Header("Prefabs")]
        [Tooltip("Prefab for a solid wall segment (no opening)")]
        public GameObject solidPrefab;

        [Tooltip("Prefab for a wall segment with a cave opening")]
        public GameObject openingPrefab;

        [Header("Opening Settings")]
        [Tooltip("Width of the cave opening in the opening prefab (must match room entrance width)")]
        public float openingWidth = 4.5f;

        [Tooltip("Height of the cave opening in the opening prefab (must match room entrance height)")]
        public float openingHeight = 4f;

        [Tooltip("Vertical offset of opening center from segment center (0 = centered)")]
        public float openingVerticalOffset = 0f;

        /// <summary>
        /// Gets the appropriate prefab based on whether an opening is needed.
        /// </summary>
        public GameObject GetPrefab(bool withOpening)
        {
            if (withOpening && openingPrefab != null)
                return openingPrefab;
            return solidPrefab;
        }

        /// <summary>
        /// Checks if this segment config is valid for use.
        /// </summary>
        public bool IsValid()
        {
            return solidPrefab != null && height > 0f && width > 0f;
        }

#if UNITY_EDITOR
        private void OnValidate()
        {
            if (string.IsNullOrEmpty(segmentId))
            {
                segmentId = $"Segment_{height}m";
            }

            if (openingWidth > width)
            {
                Debug.LogWarning($"[ShaftWallSegmentConfig] '{name}': openingWidth ({openingWidth}) > width ({width}). Clamping.");
                openingWidth = width * 0.8f;
            }

            if (openingHeight > height)
            {
                Debug.LogWarning($"[ShaftWallSegmentConfig] '{name}': openingHeight ({openingHeight}) > height ({height}). Clamping.");
                openingHeight = height * 0.8f;
            }
        }
#endif
    }
}
