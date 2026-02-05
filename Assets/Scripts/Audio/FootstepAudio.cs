using UnityEngine;
using System.Collections.Generic;

namespace BeneathTheFloor.Audio
{
    public class FootstepAudio : MonoBehaviour
    {
        [Header("Audio Source")]
        [SerializeField] private AudioSource audioSource;

        [Header("Footstep Sounds")]
        [SerializeField] private AudioClip[] basementFootsteps;
        [SerializeField] private AudioClip[] dirtFootsteps;

        [Header("Settings")]
        [SerializeField] private float walkStepInterval = 0.5f;
        [SerializeField] private float sprintStepInterval = 0.35f;
        [SerializeField] private float walkVolume = 0.5f;
        [SerializeField] private float sprintVolume = 0.7f;
        [SerializeField] private float pitchVariation = 0.1f;

        [Header("Detection")]
        [Tooltip("Y position below which is considered underground (dirt). Above = basement.")]
        [SerializeField] private float undergroundThresholdY = -5f;

        [Header("References")]
        [SerializeField] private Player.FirstPersonController controller;

        public static FootstepAudio Instance { get; private set; }

        private float stepTimer;
        private SurfaceType currentSurface = SurfaceType.Basement;
        private Dictionary<SurfaceType, AudioClip[]> surfaceSounds;

        public enum SurfaceType
        {
            Basement,
            Dirt
        }

        private void Awake()
        {
            // Check if existing Instance is still valid (not destroyed)
            if (Instance != null && Instance.gameObject == null)
            {
                Instance = null;
            }

            if (Instance == null)
            {
                Instance = this;
            }
            else if (Instance != this)
            {
                Destroy(this);
                return;
            }

            if (audioSource == null)
            {
                audioSource = gameObject.AddComponent<AudioSource>();
                audioSource.playOnAwake = false;
                audioSource.spatialBlend = 0f; // 2D for player
            }

            if (controller == null)
            {
                controller = GetComponent<Player.FirstPersonController>();
            }

            InitializeSurfaceSounds();
        }

        private void InitializeSurfaceSounds()
        {
            surfaceSounds = new Dictionary<SurfaceType, AudioClip[]>
            {
                { SurfaceType.Basement, basementFootsteps },
                { SurfaceType.Dirt, dirtFootsteps }
            };
        }

        private void Update()
        {
            if (controller == null) return;
            if (!controller.IsGrounded) return;

            bool isMoving = IsPlayerMoving();
            if (!isMoving)
            {
                stepTimer = 0f;
                return;
            }

            bool isSprinting = Input.GetKey(KeyCode.LeftShift);
            float interval = isSprinting ? sprintStepInterval : walkStepInterval;

            stepTimer += Time.deltaTime;
            if (stepTimer >= interval)
            {
                stepTimer = 0f;
                DetectSurface();
                PlayFootstep(isSprinting);
            }
        }

        private bool IsPlayerMoving()
        {
            float h = Input.GetAxis("Horizontal");
            float v = Input.GetAxis("Vertical");
            return Mathf.Abs(h) > 0.1f || Mathf.Abs(v) > 0.1f;
        }

        private void DetectSurface()
        {
            // Detect surface based on player Y position
            // Above threshold = basement, below = underground (dirt)
            if (transform.position.y >= undergroundThresholdY)
            {
                currentSurface = SurfaceType.Basement;
            }
            else
            {
                currentSurface = SurfaceType.Dirt;
            }
        }

        private void PlayFootstep(bool sprinting)
        {
            AudioClip[] clips = GetSurfaceSounds(currentSurface);
            if (clips == null || clips.Length == 0) return;

            // Filter out null clips
            List<AudioClip> validClips = new List<AudioClip>();
            foreach (var clip in clips)
            {
                if (clip != null) validClips.Add(clip);
            }

            if (validClips.Count == 0) return;

            AudioClip selectedClip = validClips[Random.Range(0, validClips.Count)];
            float volume = sprinting ? sprintVolume : walkVolume;
            float pitch = 1f + Random.Range(-pitchVariation, pitchVariation);

            audioSource.pitch = pitch;
            audioSource.PlayOneShot(selectedClip, volume);
        }

        private AudioClip[] GetSurfaceSounds(SurfaceType surface)
        {
            // Ensure dictionary is initialized
            if (surfaceSounds == null)
            {
                InitializeSurfaceSounds();
            }

            if (surfaceSounds != null && surfaceSounds.TryGetValue(surface, out AudioClip[] clips))
            {
                // Return valid clips, or fallback to basement
                if (clips != null && clips.Length > 0)
                {
                    return clips;
                }
            }

            // Fallback to basement sounds
            return basementFootsteps;
        }

        public void PlayJumpSound()
        {
            AudioClip[] clips = GetSurfaceSounds(currentSurface);
            if (clips != null && clips.Length > 0)
            {
                AudioClip clip = clips[Random.Range(0, clips.Length)];
                if (clip != null)
                {
                    audioSource.pitch = 1f + Random.Range(-0.1f, 0.1f);
                    audioSource.PlayOneShot(clip, walkVolume * 0.7f);
                }
            }
        }

        public void PlayLandSound(float fallDistance)
        {
            AudioClip[] clips = GetSurfaceSounds(currentSurface);
            if (clips != null && clips.Length > 0)
            {
                AudioClip clip = clips[Random.Range(0, clips.Length)];
                if (clip != null)
                {
                    float volume = Mathf.Clamp(fallDistance / 5f, 0.3f, 1f);
                    float pitch = 0.8f + Random.Range(-0.1f, 0.1f);

                    audioSource.pitch = pitch;
                    audioSource.PlayOneShot(clip, volume);
                }
            }
        }

        public void SetWalkInterval(float interval)
        {
            walkStepInterval = Mathf.Max(0.1f, interval);
        }

        public void SetSprintInterval(float interval)
        {
            sprintStepInterval = Mathf.Max(0.1f, interval);
        }

        public void SetVolume(float walk, float sprint)
        {
            walkVolume = Mathf.Clamp01(walk);
            sprintVolume = Mathf.Clamp01(sprint);
        }
    }
}
