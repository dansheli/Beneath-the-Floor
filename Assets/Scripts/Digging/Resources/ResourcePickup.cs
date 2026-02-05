using UnityEngine;
using System.Collections.Generic;

namespace BeneathTheFloor.Digging
{
    /// <summary>
    /// Component attached to world resource drops that allows the player to pick them up.
    /// When the player interacts (presses E while looking at it), the resource is added to inventory.
    /// Supports merging with nearby pickups of the same type via ResourcePickupMerger.
    ///
    /// Physics is ALWAYS enabled - resources naturally fall and rest on terrain.
    /// Distance-based culling hides renderers for performance when player is far away.
    /// </summary>
    [RequireComponent(typeof(Collider))]
    public class ResourcePickup : MonoBehaviour, IPullablePickup
    {
        // ===== STATIC REGISTRY FOR MERGING =====
        /// <summary>
        /// All active ResourcePickup instances in the scene.
        /// Used by ResourcePickupMerger to find and merge nearby pickups.
        /// </summary>
        public static readonly List<ResourcePickup> ActivePickups = new List<ResourcePickup>();

        // Cached player transform for distance checks
        private static Transform _playerTransform;
        private static float _lastPlayerSearchTime;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetStatics()
        {
            ActivePickups.Clear();
            _playerTransform = null;
            _lastPlayerSearchTime = 0f;
        }

        [Header("Resource Data")]
        [Tooltip("The type of underground resource this pickup represents.")]
        public UndergroundResourceType resourceType;

        [Tooltip("The amount of resource to award when picked up.")]
        public int amount = 1;

        /// <summary>
        /// IPullablePickup implementation - get amount.
        /// </summary>
        public int Amount => amount;

        [Header("Behavior")]
        [Tooltip("Destroy the pickup object after successful pickup.")]
        public bool destroyOnPickup = true;

        [Tooltip("Play a sound effect on pickup (if AudioSource exists).")]
        public bool playPickupSound = true;

        [Header("Visual Feedback")]
        [Tooltip("Optional highlight effect when player is looking at this pickup.")]
        public bool enableHighlight = true;

        [Tooltip("Highlight color multiplier when player is looking at this pickup.")]
        public Color highlightColor = new Color(1.2f, 1.2f, 1.2f, 1f);

        [Header("Optional References")]
        [Tooltip("Optional AudioSource for pickup sound.")]
        public AudioSource audioSource;

        [Tooltip("Optional AudioClip to play on pickup.")]
        public AudioClip pickupClip;

        [Header("Performance")]
        [Tooltip("Distance beyond which the renderer is disabled for performance.")]
        public float cullDistance = 30f;

        [Tooltip("How often to check distance for culling (seconds).")]
        public float cullCheckInterval = 0.5f;

        // Runtime state
        private MeshRenderer _renderer;
        private Color _originalColor;
        private bool _isHighlighted;
        private bool _hasBeenPickedUp;
        private Rigidbody _rigidbody;
        private float _cullCheckTimer;
        private bool _isRendererEnabled = true;

        /// <summary>
        /// Initialize this pickup with resource data.
        /// Called by DigWorldDropSpawner after spawning.
        /// </summary>
        public void Initialize(UndergroundResourceType type, int resourceAmount)
        {
            resourceType = type;
            amount = resourceAmount;
        }

        private void Awake()
        {
            _renderer = GetComponentInChildren<MeshRenderer>();
            if (_renderer != null && _renderer.material != null)
            {
                _originalColor = _renderer.material.color;
            }

            // Ensure we have a collider
            var collider = GetComponent<Collider>();
            if (collider == null)
            {
                var sphereCol = gameObject.AddComponent<SphereCollider>();
                sphereCol.radius = 0.3f;
            }

            // Set layer to "Pickup" so resources pass through each other
            int pickupLayer = LayerMask.NameToLayer("Pickup");
            if (pickupLayer >= 0)
            {
                gameObject.layer = pickupLayer;
                // Make pickups ignore collisions with each other
                Physics.IgnoreLayerCollision(pickupLayer, pickupLayer, true);
            }

            // Setup physics - ALWAYS enabled, never kinematic
            _rigidbody = GetComponent<Rigidbody>();
            if (_rigidbody == null)
            {
                _rigidbody = gameObject.AddComponent<Rigidbody>();
            }

            // Configure rigidbody - always active physics
            _rigidbody.mass = 0.5f;
            _rigidbody.drag = 2f;
            _rigidbody.angularDrag = 2f;
            _rigidbody.interpolation = RigidbodyInterpolation.None; // Better performance
            _rigidbody.collisionDetectionMode = CollisionDetectionMode.Discrete; // Better performance
            _rigidbody.isKinematic = false;
            _rigidbody.useGravity = true;

            // Randomize cull check timer to spread out checks across frames
            _cullCheckTimer = Random.Range(0f, cullCheckInterval);
        }

        private void Update()
        {
            if (_hasBeenPickedUp) return;

            // Distance-based culling for performance
            _cullCheckTimer += Time.deltaTime;
            if (_cullCheckTimer >= cullCheckInterval)
            {
                _cullCheckTimer = 0f;
                UpdateCulling();
            }
        }

        /// <summary>
        /// Enable/disable renderer based on distance to player.
        /// </summary>
        private void UpdateCulling()
        {
            if (_renderer == null) return;

            // Find player if we don't have a reference (or search periodically)
            if (_playerTransform == null || Time.time - _lastPlayerSearchTime > 5f)
            {
                var player = GameObject.FindGameObjectWithTag("Player");
                if (player != null)
                {
                    _playerTransform = player.transform;
                }
                _lastPlayerSearchTime = Time.time;
            }

            if (_playerTransform == null) return;

            float distSq = (transform.position - _playerTransform.position).sqrMagnitude;
            float cullDistSq = cullDistance * cullDistance;

            bool shouldRender = distSq <= cullDistSq;

            if (shouldRender != _isRendererEnabled)
            {
                _isRendererEnabled = shouldRender;
                _renderer.enabled = shouldRender;
            }
        }

        private void OnEnable()
        {
            // Register with static list for merging system
            if (!ActivePickups.Contains(this))
            {
                ActivePickups.Add(this);
            }
        }

        private void OnDisable()
        {
            // Unregister from static list
            ActivePickups.Remove(this);
        }

        /// <summary>
        /// Add extra amount to this pickup (used by ResourcePickupMerger when merging stacks).
        /// </summary>
        public void AddAmount(int extra)
        {
            amount += extra;
            if (amount < 0) amount = 0;
        }

        /// <summary>
        /// Check if this resource CAN be picked up (inventory has room).
        /// Does NOT actually pick up - just checks if there's space.
        /// </summary>
        public bool CanPickup()
        {
            if (_hasBeenPickedUp)
                return false;

            var bridge = DigInventoryBridge.Instance;
            if (bridge == null)
                return false;

            return bridge.CanAwardResource(resourceType, amount);
        }

        /// <summary>
        /// Attempt to pick up this resource.
        /// Returns true if pickup was successful, false if inventory was full.
        /// </summary>
        public bool TryPickup()
        {
            if (_hasBeenPickedUp)
                return false;

            var bridge = DigInventoryBridge.Instance;
            if (bridge == null)
            {
                Debug.LogWarning($"[ResourcePickup] Cannot pick up {resourceType} - DigInventoryBridge.Instance is null!");
                return false;
            }

            bool success = bridge.AwardResource(resourceType, amount);

            if (success)
            {
                _hasBeenPickedUp = true;

                // Play pickup sound using PlayClipAtPoint so it plays independently of this object
                if (playPickupSound && pickupClip != null)
                {
                    AudioSource.PlayClipAtPoint(pickupClip, transform.position);
                }

                // Visual feedback - could add particle effect here
                SpawnPickupEffect();

                if (destroyOnPickup)
                {
                    Destroy(gameObject);
                }
            }
            else
            {
                // Inventory full - show visual feedback
                StartCoroutine(FlashInventoryFull());
            }

            return success;
        }

        /// <summary>
        /// Flash the pickup red to indicate inventory is full.
        /// </summary>
        private System.Collections.IEnumerator FlashInventoryFull()
        {
            if (_renderer == null || _renderer.material == null)
                yield break;

            Color originalColor = _renderer.material.color;

            for (int i = 0; i < 3; i++)
            {
                _renderer.material.color = Color.red;
                yield return new WaitForSeconds(0.1f);
                _renderer.material.color = originalColor;
                yield return new WaitForSeconds(0.1f);
            }
        }

        /// <summary>
        /// Called by ResourcePickupInteractor when player looks at this pickup.
        /// </summary>
        public void SetHighlighted(bool highlighted)
        {
            if (!enableHighlight || _renderer == null)
                return;

            if (highlighted && !_isHighlighted)
            {
                // Apply highlight
                _isHighlighted = true;
                var mat = _renderer.material;
                Color newColor = _originalColor * highlightColor;
                newColor.a = _originalColor.a;
                mat.color = newColor;

                // Enable emission for glow effect
                mat.EnableKeyword("_EMISSION");
                mat.SetColor("_EmissionColor", _originalColor * 0.5f);
            }
            else if (!highlighted && _isHighlighted)
            {
                // Remove highlight
                _isHighlighted = false;
                var mat = _renderer.material;
                mat.color = _originalColor;
                mat.SetColor("_EmissionColor", Color.black);
            }
        }

        /// <summary>
        /// Get a display name for this resource type.
        /// </summary>
        public string GetDisplayName()
        {
            string name = resourceType.ToString();

            // Insert spaces before capitals (e.g., "IronNugget" -> "Iron Nugget")
            System.Text.StringBuilder sb = new System.Text.StringBuilder();
            for (int i = 0; i < name.Length; i++)
            {
                if (i > 0 && char.IsUpper(name[i]))
                    sb.Append(' ');
                sb.Append(name[i]);
            }

            return sb.ToString();
        }

        /// <summary>
        /// Spawn a visual effect when picked up.
        /// Override this in a subclass for custom effects.
        /// </summary>
        protected virtual void SpawnPickupEffect()
        {
            // Default: simple scale-down animation would go here
        }

        private void OnDestroy()
        {
            // Clean up highlight state
            if (_isHighlighted && _renderer != null)
            {
                try
                {
                    var mat = _renderer.material;
                    mat.color = _originalColor;
                }
                catch { }
            }
        }

#if UNITY_EDITOR
        private void OnDrawGizmosSelected()
        {
            // Draw pickup radius in editor
            Gizmos.color = Color.yellow;
            Gizmos.DrawWireSphere(transform.position, 0.5f);
        }
#endif
    }
}
