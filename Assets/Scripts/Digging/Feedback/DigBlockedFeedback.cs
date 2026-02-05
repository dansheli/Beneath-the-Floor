using UnityEngine;
using BeneathTheFloor.UI;
using BeneathTheFloor.Tools;

namespace BeneathTheFloor.Digging
{
    /// <summary>
    /// Handles feedback when digging is blocked due to tool depth limit.
    /// Shows a floating message and plays a sound when the player tries to dig
    /// beyond their current tool's maximum depth.
    /// </summary>
    public class DigBlockedFeedback : MonoBehaviour
    {
        [Header("Floating Text Settings")]
        [Tooltip("Message to show when tool is too weak")]
        [SerializeField] private string blockedMessage = "Tool too weak! Upgrade to dig deeper.";

        [Tooltip("Color of the floating text")]
        [SerializeField] private Color textColor = new Color(1f, 0.6f, 0.2f, 1f); // Orange/warning color

        [Tooltip("How long the message stays visible")]
        [SerializeField] private float messageDuration = 2.5f;

        [Tooltip("Vertical offset above the dig point")]
        [SerializeField] private float verticalOffset = 1.5f;

        [Tooltip("Scale of the floating text (smaller = smaller text)")]
        [SerializeField] private float textScale = 0.25f;

        [Header("Audio")]
        [Tooltip("Sound to play when dig is blocked (broken tool sound)")]
        [SerializeField] private AudioClip blockedSound;

        [Tooltip("Volume of the blocked sound")]
        [SerializeField] [Range(0f, 1f)] private float soundVolume = 0.8f;

        [Tooltip("Start time in seconds - skip silence at the beginning of the audio clip")]
        [SerializeField] private float soundStartTime = 0f;

        [Header("Cooldown")]
        [Tooltip("Minimum time between showing messages (prevents spam)")]
        [SerializeField] private float messageCooldown = 1f;

        private float lastMessageTime;
        private AudioSource audioSource;
        private DiggingSystem diggingSystem;

        public static DigBlockedFeedback Instance { get; private set; }

        /// <summary>
        /// Ensures a DigBlockedFeedback instance exists. Creates one if needed.
        /// </summary>
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void EnsureInstanceExists()
        {
            if (Instance == null)
            {
                var existing = FindObjectOfType<DigBlockedFeedback>();
                if (existing == null)
                {
                    GameObject obj = new GameObject("DigBlockedFeedback");
                    obj.AddComponent<DigBlockedFeedback>();
                }
            }
        }

        private void Awake()
        {
            if (Instance == null)
            {
                Instance = this;
            }
            else
            {
                Destroy(gameObject);
                return;
            }

            // Create audio source for feedback sounds
            audioSource = gameObject.AddComponent<AudioSource>();
            audioSource.playOnAwake = false;
            audioSource.spatialBlend = 0f; // 2D sound
        }

        private void Start()
        {
            // Find and subscribe to DiggingSystem
            diggingSystem = DiggingSystem.Instance;
            if (diggingSystem != null)
            {
                diggingSystem.OnDigBlockedDepthLimit += OnDigBlockedByDepth;
            }
            else
            {
                Debug.LogWarning("[DigBlockedFeedback] DiggingSystem not found! Will retry...");
                // Try again after a short delay
                Invoke(nameof(TrySubscribeToDiggingSystem), 0.5f);
            }
        }

        private void TrySubscribeToDiggingSystem()
        {
            if (diggingSystem != null) return;

            diggingSystem = DiggingSystem.Instance;
            if (diggingSystem != null)
            {
                diggingSystem.OnDigBlockedDepthLimit += OnDigBlockedByDepth;
            }
            else
            {
                Debug.LogWarning("[DigBlockedFeedback] Still can't find DiggingSystem");
            }
        }

        private void OnDestroy()
        {
            if (Instance == this)
            {
                Instance = null;
            }

            // Unsubscribe from events
            if (diggingSystem != null)
            {
                diggingSystem.OnDigBlockedDepthLimit -= OnDigBlockedByDepth;
            }
        }

        /// <summary>
        /// Called when dig is blocked due to depth limit.
        /// </summary>
        private void OnDigBlockedByDepth(float currentDepth, float maxDepth)
        {
            // Check cooldown to prevent message spam
            if (Time.time - lastMessageTime < messageCooldown)
            {
                return;
            }
            lastMessageTime = Time.time;

            // Get the hit position from DiggingSystem
            Vector3 hitPosition = diggingSystem != null ? diggingSystem.LastHitPoint : Vector3.zero;

            // Show floating text
            ShowBlockedMessage(hitPosition);

            // Play sound (DiggingSystem already plays blocked sound, but we can play additional if set)
            PlayBlockedSound();

        }

        /// <summary>
        /// Show the floating blocked message at the specified position.
        /// </summary>
        private void ShowBlockedMessage(Vector3 position)
        {
            if (position == Vector3.zero)
            {
                // Fallback: show in front of camera
                Camera cam = Camera.main;
                if (cam != null)
                {
                    position = cam.transform.position + cam.transform.forward * 2f;
                }
            }

            // Offset above the dig point so it's not hidden by terrain
            Vector3 textPosition = position + Vector3.up * verticalOffset;

            // Create the floating text with custom scale
            FloatingWorldText.Create(textPosition, blockedMessage, textColor, messageDuration, textScale);
        }

        /// <summary>
        /// Play the blocked/broken tool sound.
        /// </summary>
        private void PlayBlockedSound()
        {
            if (blockedSound != null && audioSource != null)
            {
                // If there's a start time offset (to skip silence), use clip assignment + time
                if (soundStartTime > 0f)
                {
                    audioSource.clip = blockedSound;
                    audioSource.volume = soundVolume;
                    audioSource.time = Mathf.Min(soundStartTime, blockedSound.length - 0.1f);
                    audioSource.Play();
                }
                else
                {
                    audioSource.PlayOneShot(blockedSound, soundVolume);
                }
            }
        }

        /// <summary>
        /// Manually trigger the blocked feedback (for testing or other systems).
        /// </summary>
        public void TriggerBlockedFeedback(Vector3 position, float currentDepth, float maxDepth)
        {
            if (Time.time - lastMessageTime < messageCooldown)
            {
                return;
            }
            lastMessageTime = Time.time;

            ShowBlockedMessage(position);
            PlayBlockedSound();
        }
    }
}
