using UnityEngine;
using BeneathTheFloor.Interaction;

namespace BeneathTheFloor.Missions
{
    /// <summary>
    /// Interactable crystal that completes the current mission when interacted with.
    /// Place this on a crystal object along with a collider.
    /// </summary>
    [RequireComponent(typeof(Collider))]
    public class CrystalInteractable : MonoBehaviour, IInteractable
    {
        [Header("Interaction")]
        [Tooltip("Text shown when looking at the crystal")]
        [SerializeField] private string interactionText = "Examine Crystal";

        [Tooltip("Can only interact once")]
        [SerializeField] private bool oneTimeOnly = true;

        [Header("Audio")]
        [SerializeField] private AudioClip interactSound;
        [SerializeField] [Range(0f, 1f)] private float soundVolume = 1f;

        [Header("Visual Feedback")]
        [Tooltip("Disable the ring marker target on this object when interacted")]
        [SerializeField] private bool hideMarkerOnInteract = true;

        private bool hasInteracted = false;
        private AudioSource audioSource;
        private RingMarkerTarget markerTarget;

        public bool CanInteract => !oneTimeOnly || !hasInteracted;

        private void Awake()
        {
            // Setup audio
            audioSource = gameObject.AddComponent<AudioSource>();
            audioSource.playOnAwake = false;
            audioSource.spatialBlend = 1f;

            // Get marker target if present
            markerTarget = GetComponent<RingMarkerTarget>();
        }

        public string GetInteractionText()
        {
            return interactionText;
        }

        public void Interact(GameObject interactor)
        {
            if (oneTimeOnly && hasInteracted) return;

            hasInteracted = true;

            Debug.Log("[CrystalInteractable] Crystal interacted!");

            // Play sound
            if (interactSound != null && audioSource != null)
            {
                audioSource.PlayOneShot(interactSound, soundVolume);
            }

            // Hide marker - be aggressive about this
            if (hideMarkerOnInteract)
            {
                Debug.Log("[CrystalInteractable] Hiding markers...");

                // Hide via RingMarkerTarget if present on this object
                if (markerTarget != null)
                {
                    markerTarget.HideMarker();
                }

                // Hide via global RingMarkerTarget instance
                if (RingMarkerTarget.Instance != null)
                {
                    RingMarkerTarget.Instance.HideMarker();
                }

                // Hide the global subtle marker singleton
                if (UI.SubtleRingMarker.Instance != null)
                {
                    UI.SubtleRingMarker.Instance.Hide();
                }

                // Find and destroy any SubtleRingMarker GameObjects in scene
                var allMarkers = FindObjectsOfType<UI.SubtleRingMarker>();
                foreach (var m in allMarkers)
                {
                    Debug.Log($"[CrystalInteractable] Force hiding marker: {m.gameObject.name}");
                    m.Hide();
                }
            }

            // Complete the current mission
            if (MissionManager.Instance != null)
            {
                MissionManager.Instance.CompleteMission();
            }
        }

        public void OnHoverEnter()
        {
            // Optional: Add glow or highlight effect
        }

        public void OnHoverExit()
        {
            // Optional: Remove glow or highlight effect
        }

        private void OnDrawGizmos()
        {
            Gizmos.color = hasInteracted ? Color.gray : Color.magenta;
            Gizmos.DrawWireSphere(transform.position, 0.3f);
        }
    }
}
