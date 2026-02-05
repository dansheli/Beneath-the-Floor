using UnityEngine;
using UnityEngine.Events;

namespace BeneathTheFloor.Digging
{
    /// <summary>
    /// Handles player interaction with ResourcePickup objects.
    /// Raycasts from camera to detect pickups and responds to E key input.
    /// </summary>
    public class ResourcePickupInteractor : MonoBehaviour
    {
        [Header("Raycast Settings")]
        [Tooltip("Camera to raycast from. If null, uses Camera.main.")]
        public Camera playerCamera;

        [Tooltip("Maximum distance to detect pickups.")]
        public float interactionRange = 3f;

        [Tooltip("LayerMask for raycast (use Default or a custom Pickup layer).")]
        public LayerMask pickupLayerMask = ~0; // All layers by default

        [Header("Input Settings")]
        [Tooltip("Key to press for pickup interaction.")]
        public KeyCode pickupKey = KeyCode.E;

        [Header("Events")]
        [Tooltip("Fired when player starts looking at a pickup.")]
        public UnityEvent<ResourcePickup> OnPickupTargeted;

        [Tooltip("Fired when player stops looking at a pickup.")]
        public UnityEvent OnPickupUntargeted;

        [Tooltip("Fired when a pickup is successfully collected.")]
        public UnityEvent<ResourcePickup> OnPickupCollected;

        [Tooltip("Fired when pickup fails (inventory full).")]
        public UnityEvent<ResourcePickup> OnPickupFailed;

        [Header("Debug")]
        public bool enableDebugLogs = false;
        public bool drawDebugRay = false;

        // Runtime state
        private ResourcePickup _currentTarget;
        private bool _hasTarget;

        public static ResourcePickupInteractor Instance { get; private set; }

        /// <summary>
        /// The currently targeted pickup, or null if none.
        /// </summary>
        public ResourcePickup CurrentTarget => _currentTarget;

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
            else if (Instance != this)
            {
                Debug.LogWarning("[ResourcePickupInteractor] Multiple instances detected, destroying this one.");
                Destroy(this);
                return;
            }
        }

        private void Start()
        {
            if (playerCamera == null)
            {
                playerCamera = Camera.main;
            }

            if (playerCamera == null)
            {
                Debug.LogError("[ResourcePickupInteractor] No camera assigned and Camera.main is null!");
            }
        }

        private void Update()
        {
            UpdateTargeting();
            HandleInput();
        }

        private void UpdateTargeting()
        {
            if (playerCamera == null)
                return;

            // Raycast from center of screen
            Ray ray = playerCamera.ViewportPointToRay(new Vector3(0.5f, 0.5f, 0f));
            RaycastHit hit;

#if UNITY_EDITOR
            if (drawDebugRay)
            {
                Debug.DrawRay(ray.origin, ray.direction * interactionRange, Color.yellow);
            }
#endif

            if (Physics.Raycast(ray, out hit, interactionRange, pickupLayerMask))
            {
                // Check if hit object has ResourcePickup component
                var pickup = hit.collider.GetComponent<ResourcePickup>();
                if (pickup == null)
                {
                    pickup = hit.collider.GetComponentInParent<ResourcePickup>();
                }

                if (pickup != null)
                {
                    SetTarget(pickup);
                    return;
                }
            }

            // No pickup in range
            ClearTarget();
        }

        private void SetTarget(ResourcePickup pickup)
        {
            if (_currentTarget == pickup)
                return;

            // Clear previous target highlight
            if (_currentTarget != null)
            {
                _currentTarget.SetHighlighted(false);
            }

            _currentTarget = pickup;
            _hasTarget = true;

            // Highlight new target
            _currentTarget.SetHighlighted(true);

            // Fire event
            OnPickupTargeted?.Invoke(_currentTarget);

            if (enableDebugLogs)
            {
                Debug.Log($"[ResourcePickupInteractor] Targeting: {pickup.GetDisplayName()} x{pickup.amount}");
            }
        }

        private void ClearTarget()
        {
            if (!_hasTarget)
                return;

            if (_currentTarget != null)
            {
                _currentTarget.SetHighlighted(false);
            }

            _currentTarget = null;
            _hasTarget = false;

            // Fire event
            OnPickupUntargeted?.Invoke();

            if (enableDebugLogs)
            {
                Debug.Log("[ResourcePickupInteractor] Target cleared");
            }
        }

        private void HandleInput()
        {
            if (!_hasTarget || _currentTarget == null)
                return;

            if (Input.GetKeyDown(pickupKey))
            {
                TryPickupTarget();
            }
        }

        private void TryPickupTarget()
        {
            if (_currentTarget == null)
                return;

            ResourcePickup pickup = _currentTarget;
            bool success = pickup.TryPickup();

            if (success)
            {
                OnPickupCollected?.Invoke(pickup);

                if (enableDebugLogs)
                {
                    Debug.Log($"[ResourcePickupInteractor] Collected: {pickup.GetDisplayName()} x{pickup.amount}");
                }

                // Target will be destroyed, so clear it
                _currentTarget = null;
                _hasTarget = false;
                OnPickupUntargeted?.Invoke();
            }
            else
            {
                OnPickupFailed?.Invoke(pickup);

                if (enableDebugLogs)
                {
                    Debug.Log($"[ResourcePickupInteractor] Failed to collect: {pickup.GetDisplayName()}");
                }
            }
        }

        private void OnDestroy()
        {
            if (Instance == this)
                Instance = null;
        }

        /// <summary>
        /// Force pickup the current target (for UI button use).
        /// </summary>
        public void ForcePickup()
        {
            if (_hasTarget && _currentTarget != null)
            {
                TryPickupTarget();
            }
        }

#if UNITY_EDITOR
        private void OnDrawGizmosSelected()
        {
            if (playerCamera == null && Camera.main != null)
                playerCamera = Camera.main;

            if (playerCamera != null)
            {
                Gizmos.color = Color.cyan;
                Ray ray = playerCamera.ViewportPointToRay(new Vector3(0.5f, 0.5f, 0f));
                Gizmos.DrawRay(ray.origin, ray.direction * interactionRange);
            }
        }
#endif
    }
}
