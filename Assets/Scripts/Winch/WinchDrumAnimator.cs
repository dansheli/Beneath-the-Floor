using UnityEngine;

namespace BeneathTheFloor.Winch
{
    /// <summary>
    /// Animates the winch drum rotation based on cable length changes.
    /// Attach to the Winch prefab root or the WinchAnchor object.
    /// </summary>
    public class WinchDrumAnimator : MonoBehaviour
    {
        [Header("Drum Reference")]
        [Tooltip("The drum mesh to rotate. If not set, will search for 'Winch.001_LOD0'.")]
        [SerializeField] private Transform drumTransform;

        [Tooltip("Name of the drum object to find if not assigned.")]
        [SerializeField] private string drumObjectName = "Winch.001_LOD0";

        [Header("Rotation Settings")]
        [Tooltip("Radius of the drum in meters (for calculating rotation amount).")]
        [SerializeField] private float drumRadius = 0.1f;

        [Tooltip("Rotation axis (local space). Default is X axis.")]
        [SerializeField] private Vector3 rotationAxis = Vector3.right;

        [Tooltip("Direction multiplier. 1 = reel in rotates positive, -1 = reel in rotates negative.")]
        [SerializeField] private float directionMultiplier = 1f;

        [Tooltip("Smooth the rotation for visual appeal.")]
        [SerializeField] private bool smoothRotation = true;

        [Tooltip("Smoothing speed (higher = faster response).")]
        [SerializeField] private float smoothSpeed = 10f;

        [Header("Debug")]
        [SerializeField] private bool enableDebugLogs = false;

        // Cached references
        private WinchAnchor winchAnchor;
        private float lastCableLength = -1f;
        private float targetRotation = 0f;
        private float currentRotation = 0f;

        private void Start()
        {
            // Find WinchAnchor
            winchAnchor = WinchAnchor.Instance;
            if (winchAnchor == null)
            {
                winchAnchor = FindObjectOfType<WinchAnchor>();
            }

            if (winchAnchor == null)
            {
                Debug.LogWarning("[WinchDrumAnimator] No WinchAnchor found. Drum animation disabled.");
                enabled = false;
                return;
            }

            // Find drum transform if not assigned
            if (drumTransform == null)
            {
                drumTransform = FindDrumTransform();
            }

            if (drumTransform == null)
            {
                Debug.LogWarning($"[WinchDrumAnimator] Drum transform '{drumObjectName}' not found. Drum animation disabled.");
                enabled = false;
                return;
            }

            if (enableDebugLogs)
                Debug.Log($"[WinchDrumAnimator] Initialized with drum: {drumTransform.name}");
        }

        private Transform FindDrumTransform()
        {
            // Search in children first
            Transform found = FindChildRecursive(transform, drumObjectName);
            if (found != null) return found;

            // Search in scene
            GameObject drumObj = GameObject.Find(drumObjectName);
            if (drumObj != null) return drumObj.transform;

            // Try partial match
            foreach (Transform child in GetComponentsInChildren<Transform>(true))
            {
                if (child.name.Contains("Winch") && child.name.Contains("LOD0"))
                {
                    return child;
                }
            }

            return null;
        }

        private Transform FindChildRecursive(Transform parent, string name)
        {
            foreach (Transform child in parent)
            {
                if (child.name == name)
                    return child;

                Transform found = FindChildRecursive(child, name);
                if (found != null)
                    return found;
            }
            return null;
        }

        private void Update()
        {
            if (winchAnchor == null || drumTransform == null) return;
            if (!winchAnchor.IsAttached) return;

            // Get current cable length
            float currentCableLength = winchAnchor.GetCurrentCableLength();

            // Initialize last length on first frame
            if (lastCableLength < 0)
            {
                lastCableLength = currentCableLength;
                return;
            }

            // Calculate length change
            float deltaLength = currentCableLength - lastCableLength;
            lastCableLength = currentCableLength;

            // Skip tiny changes to avoid jitter
            if (Mathf.Abs(deltaLength) < 0.001f) return;

            // Calculate rotation amount based on drum circumference
            // circumference = 2 * PI * radius
            // rotation = (deltaLength / circumference) * 360 degrees
            float circumference = 2f * Mathf.PI * drumRadius;
            float rotationDelta = (deltaLength / circumference) * 360f * directionMultiplier;

            // Accumulate target rotation
            targetRotation += rotationDelta;

            if (enableDebugLogs && Time.frameCount % 30 == 0)
            {
                Debug.Log($"[WinchDrumAnimator] Cable: {currentCableLength:F2}m, Delta: {deltaLength:F3}m, Rotation: {targetRotation:F1}°");
            }
        }

        private void LateUpdate()
        {
            if (drumTransform == null) return;

            // Apply rotation
            if (smoothRotation)
            {
                currentRotation = Mathf.Lerp(currentRotation, targetRotation, smoothSpeed * Time.deltaTime);
            }
            else
            {
                currentRotation = targetRotation;
            }

            // Set drum rotation around specified axis
            drumTransform.localRotation = Quaternion.AngleAxis(currentRotation, rotationAxis);
        }

        /// <summary>
        /// Reset drum rotation to zero.
        /// </summary>
        public void ResetRotation()
        {
            targetRotation = 0f;
            currentRotation = 0f;
            lastCableLength = -1f;

            if (drumTransform != null)
            {
                drumTransform.localRotation = Quaternion.identity;
            }
        }

        /// <summary>
        /// Set the drum transform manually.
        /// </summary>
        public void SetDrumTransform(Transform drum)
        {
            drumTransform = drum;
        }

        private void OnDrawGizmosSelected()
        {
            if (drumTransform == null) return;

            // Draw drum radius
            Gizmos.color = Color.yellow;
            Gizmos.DrawWireSphere(drumTransform.position, drumRadius);

            // Draw rotation axis
            Gizmos.color = Color.red;
            Vector3 axisWorld = drumTransform.TransformDirection(rotationAxis);
            Gizmos.DrawLine(drumTransform.position - axisWorld * 0.2f, drumTransform.position + axisWorld * 0.2f);
        }
    }
}
