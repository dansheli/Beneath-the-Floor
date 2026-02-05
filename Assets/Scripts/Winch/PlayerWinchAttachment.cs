using UnityEngine;

namespace BeneathTheFloor.Winch
{
    /// <summary>
    /// Attach this to the Player. Handles pit entry/exit detection and cable attachment.
    /// Works with both trigger volumes and raycast fallback.
    /// </summary>
    [RequireComponent(typeof(Collider))]
    public class PlayerWinchAttachment : MonoBehaviour
    {
        public static PlayerWinchAttachment Instance { get; private set; }

        [Header("Detection Settings")]
        [Tooltip("Tag for pit trigger volumes. Player entering these triggers attaches cable.")]
        public string pitTriggerTag = "WinchPitTrigger";

        [Tooltip("How far above the anchor the player must be to auto-detach cable.")]
        public float surfaceExitHeight = 1f;

        [Tooltip("Layer for detecting dig area via raycast (fallback).")]
        public LayerMask digAreaLayer;

        [Tooltip("Use raycast fallback if no trigger found.")]
        public bool useRaycastFallback = false;

        [Header("State")]
        [SerializeField] private bool isInPit = false;
        [SerializeField] private bool cableAttached = false;

        [Header("Debug")]
        [SerializeField] private bool enableDebugLogs = false;

        // Cached references
        private WinchAnchor winchAnchor;
        private int pitTriggerCount = 0; // Track nested triggers

        // Events
        public System.Action OnEnteredPit;
        public System.Action OnExitedPit;

        // Properties
        public bool IsInPit => isInPit;
        public bool IsCableAttached => cableAttached;

        private void Awake()
        {
            // FORCE disable debug logs (scene-serialized value may be true)
            enableDebugLogs = false;

            if (Instance == null)
            {
                Instance = this;
            }
            else if (Instance != this)
            {
                Debug.LogWarning("[PlayerWinchAttachment] Multiple instances detected.");
            }
        }

        private void Start()
        {
            // Find winch anchor in scene
            winchAnchor = WinchAnchor.Instance;
            if (winchAnchor == null)
            {
                winchAnchor = FindObjectOfType<WinchAnchor>();
            }

            if (winchAnchor == null)
            {
                Debug.LogWarning("[PlayerWinchAttachment] No WinchAnchor found in scene. Winch system disabled.");
            }
        }

        private void OnDestroy()
        {
            if (Instance == this)
                Instance = null;
        }

        private void Update()
        {
            // Y-position auto-detach is DISABLED - use WinchExitTrigger instead
            // This gives more control over where exactly the cable detaches

            // Raycast fallback detection (if enabled and no trigger detected)
            if (useRaycastFallback && pitTriggerCount == 0 && winchAnchor != null && !cableAttached)
            {
                CheckRaycastDetection();
            }
        }

        private void OnTriggerEnter(Collider other)
        {
            // Check if this is a pit trigger
            if (other.gameObject.tag == pitTriggerTag || other.GetComponent<WinchPitTrigger>() != null)
            {
                pitTriggerCount++;

                if (enableDebugLogs)
                    Debug.Log($"[PlayerWinchAttachment] Entered pit trigger: {other.name} (count: {pitTriggerCount})");

                if (!isInPit)
                {
                    EnterPit();
                }
            }
        }

        private void OnTriggerExit(Collider other)
        {
            // Track trigger exit but DON'T detach - cable stays until player returns to surface
            if (other.gameObject.tag == pitTriggerTag || other.GetComponent<WinchPitTrigger>() != null)
            {
                pitTriggerCount = Mathf.Max(0, pitTriggerCount - 1);

                if (enableDebugLogs)
                    Debug.Log($"[PlayerWinchAttachment] Left pit trigger zone: {other.name} (cable stays attached until surface)");
            }
        }

        private void CheckRaycastDetection()
        {
            // Raycast down to detect dig area (only for attaching, not detaching)
            bool hitDigArea = Physics.Raycast(
                transform.position,
                Vector3.down,
                out RaycastHit hit,
                2f,
                digAreaLayer
            );

            // Only use raycast to ATTACH, not to detach
            // Detach is handled in Update() based on Y position
            if (hitDigArea && !cableAttached)
            {
                EnterPit();
            }
        }

        private void EnterPit()
        {
            if (isInPit) return;

            isInPit = true;

            if (enableDebugLogs)
                Debug.Log("[PlayerWinchAttachment] Entered pit area");

            // Auto-attach cable
            AttachCable();

            OnEnteredPit?.Invoke();
        }

        private void ExitPit()
        {
            if (!isInPit) return;

            isInPit = false;

            if (enableDebugLogs)
                Debug.Log("[PlayerWinchAttachment] Exited pit area");

            // Auto-detach cable
            DetachCable();

            OnExitedPit?.Invoke();
        }

        /// <summary>
        /// Attach the winch cable to this player.
        /// </summary>
        public void AttachCable()
        {
            if (cableAttached) return;

            // Check if winch is permanently locked (player has jetpack)
            if (WinchExitTrigger.IsWinchPermanentlyLocked) return;

            if (winchAnchor == null)
            {
                winchAnchor = WinchAnchor.Instance ?? FindObjectOfType<WinchAnchor>();
                if (winchAnchor == null) return;
            }

            // Attach to anchor first, only set flag if successful
            try
            {
                winchAnchor.AttachToPlayer(transform);
                cableAttached = true;

                // Play attach sound
                winchAnchor.PlayAttachSound();

                if (enableDebugLogs)
                    Debug.Log("[PlayerWinchAttachment] Cable attached");
            }
            catch (System.Exception e)
            {
                Debug.LogError($"[PlayerWinchAttachment] Failed to attach cable: {e.Message}");
                cableAttached = false;
            }
        }

        /// <summary>
        /// Detach the winch cable from this player.
        /// </summary>
        public void DetachCable()
        {
            if (!cableAttached) return;

            cableAttached = false;
            isInPit = false; // Reset so re-entering pit will work
            pitTriggerCount = 0; // Reset trigger count

            if (winchAnchor != null)
            {
                // Play detach sound before detaching
                winchAnchor.PlayDetachSound();
                winchAnchor.DetachFromPlayer();
            }

            if (enableDebugLogs)
                Debug.Log("[PlayerWinchAttachment] Cable detached, pit state reset");
        }

        /// <summary>
        /// Force attach/detach for external systems.
        /// </summary>
        public void SetAttached(bool attached)
        {
            if (attached)
            {
                isInPit = true;
                AttachCable();
            }
            else
            {
                DetachCable();
                isInPit = false;
            }
        }

        private void OnDrawGizmosSelected()
        {
            // Draw raycast detection range
            if (useRaycastFallback)
            {
                Gizmos.color = Color.cyan;
                Gizmos.DrawLine(transform.position, transform.position + Vector3.down * 2f);
                Gizmos.DrawWireSphere(transform.position + Vector3.down * 2f, 0.1f);
            }
        }
    }
}
