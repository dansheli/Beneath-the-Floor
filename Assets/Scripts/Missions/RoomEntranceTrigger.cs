using UnityEngine;
using System;

namespace BeneathTheFloor.Missions
{
    /// <summary>
    /// Trigger zone that fires when player enters a room entrance.
    /// Place this on a GameObject with a trigger collider.
    /// </summary>
    [RequireComponent(typeof(Collider))]
    public class RoomEntranceTrigger : MonoBehaviour
    {
        [Header("Identification")]
        [Tooltip("Unique ID for this room entrance")]
        [SerializeField] private string entranceId;

        [Header("Settings")]
        [Tooltip("Can this trigger only fire once?")]
        [SerializeField] private bool oneTimeOnly = true;

        [Header("Audio")]
        [Tooltip("Sound to play when triggered")]
        [SerializeField] private AudioClip triggerSound;
        [SerializeField] [Range(0f, 1f)] private float soundVolume = 1f;

        [Header("Debug")]
        [SerializeField] private bool debugMode = false;

        // Events
        public static event Action<RoomEntranceTrigger> OnRoomEntranceTriggered;

        // State
        private bool hasTriggered = false;
        private AudioSource audioSource;

        public string EntranceId => entranceId;
        public bool HasTriggered => hasTriggered;

        private void Awake()
        {
            // Auto-generate ID if empty
            if (string.IsNullOrEmpty(entranceId))
            {
                entranceId = $"room_entrance_{transform.position.x:F0}_{transform.position.z:F0}";
            }

            // Ensure collider is trigger
            var collider = GetComponent<Collider>();
            if (collider != null)
            {
                collider.isTrigger = true;
            }

            // Setup audio
            audioSource = gameObject.AddComponent<AudioSource>();
            audioSource.playOnAwake = false;
            audioSource.spatialBlend = 1f;
        }

        private void OnTriggerEnter(Collider other)
        {
            // Check if it's the player
            if (!other.CompareTag("Player"))
                return;

            // Check if already triggered (one-time only)
            if (oneTimeOnly && hasTriggered)
                return;

            hasTriggered = true;

            if (debugMode)
            {
                Debug.Log($"[RoomEntranceTrigger] Player entered: {entranceId}");
            }

            // Play sound
            if (triggerSound != null && audioSource != null)
            {
                audioSource.PlayOneShot(triggerSound, soundVolume);
            }

            // Fire event
            OnRoomEntranceTriggered?.Invoke(this);
        }

        /// <summary>
        /// Reset the trigger (for save/load or testing).
        /// </summary>
        public void ResetTrigger()
        {
            hasTriggered = false;
        }

        private void OnDrawGizmos()
        {
            Gizmos.color = hasTriggered ? Color.gray : Color.cyan;
            Gizmos.DrawWireCube(transform.position, Vector3.one);
        }

        private void OnDrawGizmosSelected()
        {
            Gizmos.color = new Color(0f, 1f, 1f, 0.3f);

            var collider = GetComponent<Collider>();
            if (collider is BoxCollider box)
            {
                Gizmos.matrix = transform.localToWorldMatrix;
                Gizmos.DrawCube(box.center, box.size);
            }
        }
    }
}
