using UnityEngine;
using System;

namespace BeneathTheFloor.Winch
{
    /// <summary>
    /// Place this trigger at the basement floor level (pit exit).
    /// When the player crosses it, the winch cable detaches.
    /// </summary>
    [RequireComponent(typeof(Collider))]
    public class WinchExitTrigger : MonoBehaviour
    {
        public static WinchExitTrigger Instance { get; private set; }

        [Header("Settings")]
        [Tooltip("Tag to identify player.")]
        public string playerTag = "Player";

        [Tooltip("If checked, passing through this trigger will permanently lock the winch (for First Room entrance).")]
        public bool lockPermanently = false;

        // Event fired when player exits the winch area
        public static event Action OnPlayerExitedWinch;

        // Flag to permanently lock the winch (e.g., when player gets jetpack)
        public static bool IsWinchPermanentlyLocked { get; private set; } = false;

        [Header("Debug")]
        public bool showGizmos = true;
        public Color gizmoColor = new Color(0f, 1f, 0f, 0.3f);

        private void Awake()
        {
            Instance = this;
        }

        private void Start()
        {
            // Ensure collider is trigger
            Collider col = GetComponent<Collider>();
            if (col != null && !col.isTrigger)
            {
                col.isTrigger = true;
            }
        }

        private void OnDestroy()
        {
            if (Instance == this)
                Instance = null;
        }

        private void OnTriggerEnter(Collider other)
        {
            // Check if it's the player
            if (!other.CompareTag(playerTag))
            {
                // Also check for PlayerWinchAttachment component
                if (other.GetComponent<PlayerWinchAttachment>() == null &&
                    other.GetComponentInParent<PlayerWinchAttachment>() == null)
                {
                    return;
                }
            }

            // Find the player's winch attachment and detach
            PlayerWinchAttachment attachment = other.GetComponent<PlayerWinchAttachment>();
            if (attachment == null)
            {
                attachment = other.GetComponentInParent<PlayerWinchAttachment>();
            }
            if (attachment == null)
            {
                attachment = PlayerWinchAttachment.Instance;
            }

            if (lockPermanently)
            {
                // Permanently lock the winch (player got jetpack)
                LockWinchPermanently();
                OnPlayerExitedWinch?.Invoke();
            }
            else if (attachment != null && attachment.IsCableAttached)
            {
                // Just detach the cable
                attachment.DetachCable();
                OnPlayerExitedWinch?.Invoke();
            }
        }

        /// <summary>
        /// Permanently locks the winch system. Once called, the cable cannot be attached again.
        /// Use this when the player obtains the jetpack in the First Room.
        /// </summary>
        public static void LockWinchPermanently()
        {
            if (IsWinchPermanentlyLocked) return;

            IsWinchPermanentlyLocked = true;

            // Detach cable if currently attached
            PlayerWinchAttachment attachment = PlayerWinchAttachment.Instance;
            if (attachment != null && attachment.IsCableAttached)
            {
                attachment.DetachCable();
            }

            // Disable the winch anchor
            WinchAnchor anchor = WinchAnchor.Instance;
            if (anchor != null)
            {
                anchor.enabled = false;
            }

            // Disable player winch attachment
            if (attachment != null)
            {
                attachment.enabled = false;
            }

            Debug.Log("[WinchExitTrigger] Winch permanently locked - player has jetpack");
        }

        /// <summary>
        /// Reset the permanent lock flag. Call on New Game / save reset
        /// so the winch is usable again from the start.
        /// </summary>
        public static void ResetWinchLock()
        {
            IsWinchPermanentlyLocked = false;
        }

        private void OnDrawGizmos()
        {
            if (!showGizmos) return;

            Collider col = GetComponent<Collider>();
            if (col == null) return;

            Gizmos.color = gizmoColor;

            if (col is BoxCollider box)
            {
                Matrix4x4 oldMatrix = Gizmos.matrix;
                Gizmos.matrix = transform.localToWorldMatrix;
                Gizmos.DrawCube(box.center, box.size);
                Gizmos.DrawWireCube(box.center, box.size);
                Gizmos.matrix = oldMatrix;
            }
            else if (col is SphereCollider sphere)
            {
                Gizmos.DrawSphere(transform.position + sphere.center, sphere.radius);
                Gizmos.DrawWireSphere(transform.position + sphere.center, sphere.radius);
            }
        }
    }
}
