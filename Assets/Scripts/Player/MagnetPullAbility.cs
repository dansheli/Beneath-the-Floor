using UnityEngine;
using BeneathTheFloor.Digging;
using BeneathTheFloor.Managers;

namespace BeneathTheFloor.Player
{
    /// <summary>
    /// Magnet Pull ability - allows player to pull ONE resource pickup at a time
    /// by pressing F while aiming at it. The pickup smoothly moves toward the player
    /// and is collected using the existing ResourcePickup.TryPickup() flow.
    /// </summary>
    public class MagnetPullAbility : MonoBehaviour
    {
        [Header("Input")]
        [Tooltip("Key to activate magnet pull.")]
        [SerializeField] private KeyCode pullKey = KeyCode.E;

        [Header("Raycast Settings")]
        [Tooltip("Camera to raycast from. If null, uses Camera.main.")]
        [SerializeField] private Camera playerCamera;

        [Tooltip("Maximum distance to pull pickups from.")]
        [SerializeField] private float maxPullRange = 8f;

        [Tooltip("LayerMask for raycast (should include pickup layer).")]
        [SerializeField] private LayerMask pickupLayerMask = ~0;

        [Header("Pull Behavior")]
        [Tooltip("How fast the pickup moves toward the player (units per second).")]
        [SerializeField] private float pullSpeed = 20f;

        [Tooltip("Distance from collector point at which pickup is collected.")]
        [SerializeField] private float collectDistance = 0.5f;

        [Tooltip("Offset from camera for the collector point.")]
        [SerializeField] private Vector3 collectorOffset = new Vector3(0f, -0.3f, 1.2f);

        [Header("Visual Feedback")]
        [Tooltip("Show a hint when aiming at a pullable pickup.")]
        [SerializeField] private bool showPullHint = true;

        [Header("Audio")]
        [Tooltip("AudioSource for magnet pull sounds. Auto-created if not assigned.")]
        [SerializeField] private AudioSource audioSource;
        [Tooltip("Sound to play when magnet pull starts.")]
        [SerializeField] private AudioClip magnetWhooshClip;
        [Tooltip("Volume for the whoosh sound.")]
        [SerializeField, Range(0f, 1f)] private float whooshVolume = 0.7f;
        [Tooltip("Sound to play when inventory is full and cannot pull.")]
        [SerializeField] private AudioClip inventoryFullClip;
        [Tooltip("Volume for the inventory full sound.")]
        [SerializeField, Range(0f, 1f)] private float inventoryFullVolume = 0.8f;

        [Header("Debug")]
        [SerializeField] private bool enableDebugLogs = false;

        // Runtime state
        private IPullablePickup _pullingTarget;
        private MonoBehaviour _pullingTargetMono; // Cache for transform/rigidbody access
        private bool _isPulling;
        private Rigidbody _pullingRigidbody;
        private Collider[] _pullingColliders;  // All colliders on the resource
        private bool[] _collidersWereEnabled;  // Track which were enabled
        private bool _wasKinematic;
        private bool _hadGravity;
        private RigidbodyInterpolation _wasInterpolation;

        // Cached aimed pickup (not yet pulling)
        private IPullablePickup _aimedPickup;
        private MonoBehaviour _aimedPickupMono;

        public static MagnetPullAbility Instance { get; private set; }

        /// <summary>
        /// Whether we're currently pulling a pickup.
        /// </summary>
        public bool IsPulling => _isPulling;

        /// <summary>
        /// The pickup we're currently aiming at (if any).
        /// </summary>
        public IPullablePickup AimedPickup => _aimedPickup;

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
                Debug.LogError("[MagnetPullAbility] No camera assigned and Camera.main is null!");
                enabled = false;
            }

            // Setup AudioSource if not assigned
            if (audioSource == null)
            {
                audioSource = GetComponent<AudioSource>();
                if (audioSource == null)
                {
                    audioSource = gameObject.AddComponent<AudioSource>();
                    audioSource.playOnAwake = false;
                    audioSource.spatialBlend = 0f; // 2D sound
                }
            }
        }

        private void Update()
        {
            // Update aiming (only when not pulling)
            if (!_isPulling)
            {
                UpdateAiming();
            }

            // Handle input
            HandleInput();

            // Process active pull
            if (_isPulling)
            {
                ProcessPull();
            }
        }

        private void UpdateAiming()
        {
            if (playerCamera == null)
            {
                _aimedPickup = null;
                _aimedPickupMono = null;
                return;
            }

            // Raycast from center of screen
            // Use RaycastAll to find pickups even if terrain is in the way
            // Include triggers for interaction zones
            Ray ray = playerCamera.ViewportPointToRay(new Vector3(0.5f, 0.5f, 0f));
            RaycastHit[] hits = Physics.RaycastAll(ray, maxPullRange, pickupLayerMask, QueryTriggerInteraction.Collide);

            // Find the closest IPullablePickup among all hits
            IPullablePickup closestPickup = null;
            MonoBehaviour closestMono = null;
            float closestDist = float.MaxValue;

            foreach (var hit in hits)
            {
                // Try to find IPullablePickup on hit object or parent
                var pickup = hit.collider.GetComponent<IPullablePickup>();
                if (pickup == null)
                {
                    pickup = hit.collider.GetComponentInParent<IPullablePickup>();
                }

                if (pickup != null && hit.distance < closestDist)
                {
                    closestPickup = pickup;
                    closestMono = pickup as MonoBehaviour;
                    closestDist = hit.distance;
                }
            }

            _aimedPickup = closestPickup;
            _aimedPickupMono = closestMono;
        }

        private void HandleInput()
        {
            // Only respond when pull key is pressed and we're not already pulling
            if (Input.GetKeyDown(pullKey) && !_isPulling)
            {
                // Check if we have a valid target
                if (_aimedPickup != null && _aimedPickupMono != null)
                {
                    StartPull(_aimedPickup, _aimedPickupMono);
                }
            }
        }

        private void StartPull(IPullablePickup pickup, MonoBehaviour pickupMono)
        {
            if (pickup == null || pickupMono == null)
                return;

            // CHECK INVENTORY SPACE FIRST - don't pull if inventory is full
            if (!pickup.CanPickup())
            {
                // Play inventory full sound
                if (inventoryFullClip != null && audioSource != null)
                {
                    audioSource.PlayOneShot(inventoryFullClip, inventoryFullVolume);
                }

                // Fire the inventory full event for other systems (UI feedback)
                GameEvents.OnInventoryFull?.Invoke();

                if (enableDebugLogs)
                {
                    Debug.Log($"[MagnetPull] Cannot pull {pickup.GetDisplayName()} - inventory full!");
                }

                return; // Don't start the pull
            }

            _pullingTarget = pickup;
            _pullingTargetMono = pickupMono;
            _isPulling = true;

            // Play whoosh sound
            if (magnetWhooshClip != null && audioSource != null)
            {
                audioSource.PlayOneShot(magnetWhooshClip, whooshVolume);
            }

            // Handle rigidbody - make kinematic to control movement
            _pullingRigidbody = pickupMono.GetComponent<Rigidbody>();
            if (_pullingRigidbody != null)
            {
                _wasKinematic = _pullingRigidbody.isKinematic;
                _hadGravity = _pullingRigidbody.useGravity;
                _wasInterpolation = _pullingRigidbody.interpolation;

                // Only set velocity if not already kinematic (can't set velocity on kinematic bodies)
                if (!_pullingRigidbody.isKinematic)
                {
                    _pullingRigidbody.velocity = Vector3.zero;
                    _pullingRigidbody.angularVelocity = Vector3.zero;
                }

                _pullingRigidbody.isKinematic = true;
                _pullingRigidbody.useGravity = false;
                // CRITICAL: Disable interpolation to prevent flickering when we set transform.position directly
                _pullingRigidbody.interpolation = RigidbodyInterpolation.None;
            }

            // Disable ALL colliders (including children) to prevent terrain collision during pull
            _pullingColliders = pickupMono.GetComponentsInChildren<Collider>(true);
            _collidersWereEnabled = new bool[_pullingColliders.Length];
            for (int i = 0; i < _pullingColliders.Length; i++)
            {
                _collidersWereEnabled[i] = _pullingColliders[i].enabled;
                _pullingColliders[i].enabled = false;
            }

            // Highlight the pickup being pulled
            pickup.SetHighlighted(true);

            if (enableDebugLogs)
            {
                Debug.Log($"[MagnetPull] Started pulling {pickup.GetDisplayName()} x{pickup.Amount}");
            }
        }

        private void ProcessPull()
        {
            // Safety check - target may have been destroyed
            if (_pullingTarget == null || _pullingTargetMono == null)
            {
                CancelPull();
                return;
            }

            // Calculate collector position (in front of camera)
            Vector3 collectorPos = playerCamera.transform.position +
                                   playerCamera.transform.TransformDirection(collectorOffset);

            // Move pickup toward collector
            Vector3 currentPos = _pullingTargetMono.transform.position;
            Vector3 direction = (collectorPos - currentPos).normalized;
            float distance = Vector3.Distance(currentPos, collectorPos);

            if (distance <= collectDistance)
            {
                // Close enough - collect it
                CompletePull();
            }
            else
            {
                // Move toward collector
                float moveAmount = pullSpeed * Time.deltaTime;
                moveAmount = Mathf.Min(moveAmount, distance); // Don't overshoot

                _pullingTargetMono.transform.position = currentPos + direction * moveAmount;
            }
        }

        private void CompletePull()
        {
            if (_pullingTarget == null)
            {
                CancelPull();
                return;
            }

            if (enableDebugLogs)
            {
                Debug.Log($"[MagnetPull] Collecting {_pullingTarget.GetDisplayName()} x{_pullingTarget.Amount}");
            }

            // Use the existing TryPickup method from IPullablePickup
            bool success = _pullingTarget.TryPickup();

            if (!success)
            {
                // Pickup failed (inventory full) - drop the item properly
                DropPickupSafely();
                if (enableDebugLogs)
                {
                    Debug.Log("[MagnetPull] Collection failed - inventory full, dropping item");
                }
            }

            // Clear state
            _pullingTarget = null;
            _pullingTargetMono = null;
            _pullingRigidbody = null;
            _pullingColliders = null;
            _collidersWereEnabled = null;
            _isPulling = false;
        }

        /// <summary>
        /// Drop the pickup safely when collection fails (inventory full).
        /// Moves it away from the player and lets it fall naturally.
        /// </summary>
        private void DropPickupSafely()
        {
            if (_pullingTargetMono == null)
            {
                RestorePhysics();
                return;
            }

            // Calculate a drop position - in front of and below the player
            Vector3 dropDirection = playerCamera.transform.forward;
            dropDirection.y = 0; // Keep horizontal
            if (dropDirection.sqrMagnitude < 0.01f)
            {
                dropDirection = Vector3.forward;
            }
            dropDirection.Normalize();

            // Drop the item 1.5m in front and 0.5m below camera
            Vector3 dropPosition = playerCamera.transform.position + dropDirection * 1.5f;
            dropPosition.y -= 0.5f;

            _pullingTargetMono.transform.position = dropPosition;

            // Restore physics
            RestorePhysics();

            // Give a small impulse away from player
            if (_pullingRigidbody != null && !_pullingRigidbody.isKinematic)
            {
                _pullingRigidbody.velocity = dropDirection * 2f + Vector3.down * 1f;
            }
        }

        private void CancelPull()
        {
            if (enableDebugLogs && _pullingTarget != null)
            {
                Debug.Log("[MagnetPull] Pull cancelled - target invalid");
            }

            RestorePhysics();

            _pullingTarget = null;
            _pullingTargetMono = null;
            _pullingRigidbody = null;
            _pullingColliders = null;
            _collidersWereEnabled = null;
            _isPulling = false;
        }

        private void RestorePhysics()
        {
            if (_pullingRigidbody != null)
            {
                _pullingRigidbody.isKinematic = _wasKinematic;
                _pullingRigidbody.useGravity = _hadGravity;
                _pullingRigidbody.interpolation = _wasInterpolation;
            }

            // Restore ALL colliders to their original state
            if (_pullingColliders != null && _collidersWereEnabled != null)
            {
                for (int i = 0; i < _pullingColliders.Length; i++)
                {
                    if (_pullingColliders[i] != null)
                    {
                        _pullingColliders[i].enabled = _collidersWereEnabled[i];
                    }
                }
            }
        }

        private void OnDestroy()
        {
            // Restore physics if we're destroyed while pulling
            RestorePhysics();

            if (Instance == this)
                Instance = null;
        }

        /// <summary>
        /// Check if we're currently aiming at a pickup that can be pulled.
        /// Used by UI to show hints.
        /// </summary>
        public bool CanPull()
        {
            return !_isPulling && _aimedPickup != null;
        }

        /// <summary>
        /// Get the pull hint text for UI display.
        /// Returns null if not aiming at a pullable pickup.
        /// </summary>
        public string GetPullHintText()
        {
            if (!showPullHint || !CanPull())
                return null;

            return $"[E] Pull {_aimedPickup.GetDisplayName()} x{_aimedPickup.Amount}";
        }

#if UNITY_EDITOR
        private void OnDrawGizmosSelected()
        {
            // Draw pull range
            if (playerCamera == null && Camera.main != null)
                playerCamera = Camera.main;

            if (playerCamera != null)
            {
                Gizmos.color = Color.magenta;
                Ray ray = playerCamera.ViewportPointToRay(new Vector3(0.5f, 0.5f, 0f));
                Gizmos.DrawRay(ray.origin, ray.direction * maxPullRange);

                // Draw collector point
                Gizmos.color = Color.cyan;
                Vector3 collectorPos = playerCamera.transform.position +
                                       playerCamera.transform.TransformDirection(collectorOffset);
                Gizmos.DrawWireSphere(collectorPos, collectDistance);
            }
        }
#endif
    }
}
