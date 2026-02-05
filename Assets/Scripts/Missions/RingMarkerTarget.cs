using UnityEngine;
using BeneathTheFloor.UI;

namespace BeneathTheFloor.Missions
{
    /// <summary>
    /// Place this on any object to make it a target for the subtle ring marker.
    /// The ring will appear above this object when the associated mission is active.
    /// </summary>
    public class RingMarkerTarget : MonoBehaviour
    {
        [Header("Marker Settings")]
        [Tooltip("Scale of the ring marker")]
        [SerializeField] private float markerScale = 1f;

        [Tooltip("Custom color for the ring (leave white to use default)")]
        [SerializeField] private Color markerColor = Color.white;

        [Tooltip("Height offset above this object")]
        [SerializeField] private float heightOffset = 0.5f;

        [Header("Auto-Show")]
        [Tooltip("Automatically show marker when this object is enabled")]
        [SerializeField] private bool showOnEnable = false;

        [Header("Debug")]
        [SerializeField] private bool showGizmo = true;

        private SubtleRingMarker marker;
        private bool isShowing = false;

        public static RingMarkerTarget Instance { get; private set; }

        private void Awake()
        {
            Instance = this;
        }

        private void OnEnable()
        {
            Instance = this;
            if (showOnEnable)
            {
                ShowMarker();
            }
        }

        private void OnDisable()
        {
            HideMarker();
            if (Instance == this) Instance = null;
        }

        private void OnDestroy()
        {
            HideMarker();
            if (Instance == this) Instance = null;

            // Also complete the mission if this was a mission target
            if (MissionManager.Instance != null && MissionManager.Instance.CurrentMission != null)
            {
                var mission = MissionManager.Instance.CurrentMission;
                if (mission.useSubtleMarker && mission.completionTrigger == MissionTriggerType.Manual)
                {
                    MissionManager.Instance.CompleteMission();
                }
            }
        }

        /// <summary>
        /// Show the ring marker above this object.
        /// </summary>
        public void ShowMarker()
        {
            if (isShowing) return;

            marker = SubtleRingMarker.GetOrCreate();

            // Apply custom color if not white
            if (markerColor != Color.white)
            {
                marker.SetColor(markerColor);
            }

            // Calculate position with height offset
            Vector3 markerPos = transform.position + Vector3.up * heightOffset;
            marker.ShowAt(markerPos, markerScale);

            // Make marker follow this object
            marker.ShowAbove(transform, markerScale);

            isShowing = true;
        }

        /// <summary>
        /// Hide the ring marker.
        /// </summary>
        public void HideMarker()
        {
            // Always try to hide the marker, even if isShowing is false
            if (marker != null)
            {
                marker.Hide();
            }

            // Also hide via the singleton in case it's a different instance
            if (SubtleRingMarker.Instance != null && SubtleRingMarker.Instance != marker)
            {
                SubtleRingMarker.Instance.Hide();
            }

            isShowing = false;
        }

        /// <summary>
        /// Toggle marker visibility.
        /// </summary>
        public void ToggleMarker()
        {
            if (isShowing)
                HideMarker();
            else
                ShowMarker();
        }

        private void OnDrawGizmos()
        {
            if (!showGizmo) return;

            // Draw a ring gizmo to preview marker position
            Gizmos.color = markerColor != Color.white ? markerColor : new Color(0.8f, 0.9f, 1f, 0.6f);

            Vector3 center = transform.position + Vector3.up * heightOffset;
            float radius = 0.5f * markerScale;

            // Draw circle
            int segments = 32;
            for (int i = 0; i < segments; i++)
            {
                float angle1 = (float)i / segments * Mathf.PI * 2f;
                float angle2 = (float)(i + 1) / segments * Mathf.PI * 2f;

                Vector3 p1 = center + new Vector3(Mathf.Cos(angle1) * radius, 0, Mathf.Sin(angle1) * radius);
                Vector3 p2 = center + new Vector3(Mathf.Cos(angle2) * radius, 0, Mathf.Sin(angle2) * radius);

                Gizmos.DrawLine(p1, p2);
            }

            // Draw vertical line to object
            Gizmos.color = new Color(Gizmos.color.r, Gizmos.color.g, Gizmos.color.b, 0.3f);
            Gizmos.DrawLine(transform.position, center);
        }

        private void OnDrawGizmosSelected()
        {
            // Draw filled disc when selected
            Gizmos.color = new Color(0.8f, 0.9f, 1f, 0.2f);
            Vector3 center = transform.position + Vector3.up * heightOffset;

#if UNITY_EDITOR
            UnityEditor.Handles.color = new Color(0.8f, 0.9f, 1f, 0.2f);
            UnityEditor.Handles.DrawSolidDisc(center, Vector3.up, 0.5f * markerScale);
#endif
        }
    }
}
