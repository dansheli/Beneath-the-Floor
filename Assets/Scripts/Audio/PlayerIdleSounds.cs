using UnityEngine;

namespace BeneathTheFloor.Audio
{
    /// <summary>
    /// Plays random ambient sounds when the player is idle (not moving or performing actions).
    /// Attach this to the Player GameObject.
    /// </summary>
    public class PlayerIdleSounds : MonoBehaviour
    {
        public static PlayerIdleSounds Instance { get; private set; }

        [Header("Idle Sounds")]
        [Tooltip("Array of sounds to play randomly when idle (breathing, shuffling, sighing, etc.)")]
        [SerializeField] private AudioClip[] idleSounds;

        [Tooltip("Volume for idle sounds")]
        [SerializeField] [Range(0f, 1f)] private float idleVolume = 0.5f;

        [Header("Timing")]
        [Tooltip("How long the player must be idle before sounds start playing (seconds)")]
        [SerializeField] private float idleDelayBeforeStart = 3f;

        [Tooltip("Minimum time between idle sounds (seconds)")]
        [SerializeField] private float minTimeBetweenSounds = 5f;

        [Tooltip("Maximum time between idle sounds (seconds)")]
        [SerializeField] private float maxTimeBetweenSounds = 15f;

        [Header("Detection Settings")]
        [Tooltip("Minimum movement speed to be considered 'moving'")]
        [SerializeField] private float movementThreshold = 0.1f;

        [Tooltip("Also check if player is digging/using tools")]
        [SerializeField] private bool checkToolUsage = true;

        [Header("Audio Settings")]
        [Tooltip("Spatial blend (0 = 2D, 1 = 3D)")]
        [SerializeField] [Range(0f, 1f)] private float spatialBlend = 0f;

        [Header("Debug")]
        [SerializeField] private bool showDebugInfo = false;

        // Components
        private AudioSource audioSource;
        private CharacterController characterController;

        // State
        private float idleTimer = 0f;
        private float nextSoundTime = 0f;
        private bool isIdle = false;
        private bool hasStartedIdleSounds = false;
        private Vector3 lastPosition;

        private void Awake()
        {
            if (Instance != null && Instance != this)
            {
                Destroy(this);
                return;
            }
            Instance = this;
        }

        private void Start()
        {
            // Get or create audio source
            audioSource = GetComponent<AudioSource>();
            if (audioSource == null)
            {
                audioSource = gameObject.AddComponent<AudioSource>();
            }
            audioSource.playOnAwake = false;
            audioSource.spatialBlend = spatialBlend;
            audioSource.volume = idleVolume;

            // Get character controller
            characterController = GetComponent<CharacterController>();
            if (characterController == null)
            {
                characterController = GetComponentInParent<CharacterController>();
            }

            lastPosition = transform.position;

            // Set initial next sound time
            ResetNextSoundTime();
        }

        private void Update()
        {
            if (idleSounds == null || idleSounds.Length == 0)
                return;

            // Check if player is idle
            bool currentlyIdle = CheckIfIdle();

            if (currentlyIdle)
            {
                idleTimer += Time.deltaTime;

                // Check if we've been idle long enough to start playing sounds
                if (idleTimer >= idleDelayBeforeStart)
                {
                    if (!hasStartedIdleSounds)
                    {
                        hasStartedIdleSounds = true;
                        ResetNextSoundTime();

                        if (showDebugInfo)
                            Debug.Log("[PlayerIdleSounds] Player is now idle, will start playing sounds");
                    }

                    // Check if it's time to play a sound
                    if (Time.time >= nextSoundTime && !audioSource.isPlaying)
                    {
                        PlayRandomIdleSound();
                        ResetNextSoundTime();
                    }
                }
            }
            else
            {
                // Player is moving/active - reset idle state
                if (isIdle || hasStartedIdleSounds)
                {
                    if (showDebugInfo && hasStartedIdleSounds)
                        Debug.Log("[PlayerIdleSounds] Player is no longer idle");
                }

                idleTimer = 0f;
                hasStartedIdleSounds = false;

                // Optionally stop currently playing idle sound when player moves
                // Uncomment if you want sounds to cut off when moving:
                // if (audioSource.isPlaying) audioSource.Stop();
            }

            isIdle = currentlyIdle;
            lastPosition = transform.position;

            if (showDebugInfo && Time.frameCount % 120 == 0)
            {
                Debug.Log($"[PlayerIdleSounds] idle={isIdle}, timer={idleTimer:F1}s, hasStarted={hasStartedIdleSounds}, nextSound in {nextSoundTime - Time.time:F1}s");
            }
        }

        private bool CheckIfIdle()
        {
            // Check movement via position delta
            float movementDelta = Vector3.Distance(transform.position, lastPosition) / Time.deltaTime;
            if (movementDelta > movementThreshold)
                return false;

            // Check movement input
            float inputMagnitude = new Vector2(Input.GetAxis("Horizontal"), Input.GetAxis("Vertical")).magnitude;
            if (inputMagnitude > 0.1f)
                return false;

            // Check if jumping
            if (Input.GetButton("Jump"))
                return false;

            // Check mouse movement (looking around counts as activity)
            float mouseMovement = Mathf.Abs(Input.GetAxis("Mouse X")) + Mathf.Abs(Input.GetAxis("Mouse Y"));
            if (mouseMovement > 0.5f)
                return false;

            // Check tool usage (digging, etc.)
            if (checkToolUsage)
            {
                if (Input.GetMouseButton(0) || Input.GetMouseButton(1))
                    return false;
            }

            // Check if any UI is open (player is interacting with menus)
            if (BeneathTheFloor.UI.UIState.IsAnyUIOpen)
                return false;

            return true;
        }

        private void PlayRandomIdleSound()
        {
            if (idleSounds == null || idleSounds.Length == 0)
                return;

            // Select random sound
            AudioClip clip = idleSounds[Random.Range(0, idleSounds.Length)];
            if (clip == null)
                return;

            audioSource.clip = clip;
            audioSource.volume = idleVolume;
            audioSource.Play();

            if (showDebugInfo)
                Debug.Log($"[PlayerIdleSounds] Playing idle sound: {clip.name}");
        }

        private void ResetNextSoundTime()
        {
            float delay = Random.Range(minTimeBetweenSounds, maxTimeBetweenSounds);
            nextSoundTime = Time.time + delay;
        }

        /// <summary>
        /// Manually trigger an idle sound (for testing or external triggers).
        /// </summary>
        public void PlayIdleSoundNow()
        {
            PlayRandomIdleSound();
        }

        /// <summary>
        /// Set idle sounds at runtime.
        /// </summary>
        public void SetIdleSounds(AudioClip[] sounds)
        {
            idleSounds = sounds;
        }

        /// <summary>
        /// Set idle volume at runtime.
        /// </summary>
        public void SetVolume(float volume)
        {
            idleVolume = Mathf.Clamp01(volume);
            if (audioSource != null)
                audioSource.volume = idleVolume;
        }

        /// <summary>
        /// Enable or disable idle sounds.
        /// </summary>
        public void SetEnabled(bool enabled)
        {
            this.enabled = enabled;
            if (!enabled && audioSource != null && audioSource.isPlaying)
            {
                audioSource.Stop();
            }
        }

        private void OnDestroy()
        {
            if (Instance == this)
                Instance = null;
        }
    }
}
