using UnityEngine;

namespace BeneathTheFloor.Missions
{
    /// <summary>
    /// Place this component on a marker object in the scene.
    /// The entire GameObject (with all its children/visuals) will be activated when the mission starts.
    /// </summary>
    public class MissionMarkerPoint : MonoBehaviour
    {
        [Tooltip("The mission ID this marker is for (must match MissionData.missionId)")]
        public string missionId;

        [Header("Animation (Optional)")]
        [Tooltip("Enable bobbing animation")]
        public bool enableBobbing = true;
        public float bobSpeed = 2f;
        public float bobHeight = 0.1f;

        [Tooltip("Enable rotation animation")]
        public bool enableRotation = true;
        public float rotationSpeed = 60f;

        private Vector3 startPosition;
        private float bobOffset;

        private void Awake()
        {
            // Start hidden - MissionManager will activate when needed
            gameObject.SetActive(false);
        }

        private void Start()
        {
            startPosition = transform.position;
            bobOffset = Random.Range(0f, Mathf.PI * 2f);
        }

        private void Update()
        {
            // Bobbing animation
            if (enableBobbing)
            {
                float bob = Mathf.Sin((Time.time + bobOffset) * bobSpeed) * bobHeight;
                transform.position = startPosition + Vector3.up * bob;
            }

            // Rotation animation
            if (enableRotation)
            {
                transform.Rotate(Vector3.up, rotationSpeed * Time.deltaTime, Space.World);
            }
        }

        /// <summary>
        /// Show this marker.
        /// </summary>
        public void Show()
        {
            startPosition = transform.position;
            gameObject.SetActive(true);
        }

        /// <summary>
        /// Hide this marker.
        /// </summary>
        public void Hide()
        {
            gameObject.SetActive(false);
        }

        private void OnDrawGizmos()
        {
            // Draw a visible marker in the editor (even when inactive)
            Gizmos.color = new Color(1f, 0.8f, 0.2f, 0.6f);
            Gizmos.DrawWireSphere(transform.position, 0.3f);
            Gizmos.DrawLine(transform.position, transform.position + Vector3.up * 1.5f);
        }

        private void OnDrawGizmosSelected()
        {
            // More visible when selected
            Gizmos.color = Color.yellow;
            Gizmos.DrawWireSphere(transform.position, 0.5f);
            Gizmos.DrawLine(transform.position, transform.position + Vector3.up * 2f);

            // Draw label
#if UNITY_EDITOR
            UnityEditor.Handles.Label(transform.position + Vector3.up * 0.7f,
                string.IsNullOrEmpty(missionId) ? "NO MISSION ID" : missionId);
#endif
        }
    }
}
