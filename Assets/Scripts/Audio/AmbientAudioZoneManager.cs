using UnityEngine;

namespace BeneathTheFloor.Audio
{
    /// <summary>
    /// Manages ambient audio zones based on player depth.
    /// Crossfades between basement and underground ambient sounds.
    /// </summary>
    public class AmbientAudioZoneManager : MonoBehaviour
    {
        public static AmbientAudioZoneManager Instance { get; private set; }

        [Header("Basement Audio")]
        [Tooltip("Primary ambient sound for the basement area")]
        [SerializeField] private AudioClip basementAmbient1;

        [Tooltip("Secondary ambient sound for the basement area (plays together with primary)")]
        [SerializeField] private AudioClip basementAmbient2;

        [Tooltip("Maximum volume for basement ambient 1")]
        [SerializeField] [Range(0f, 1f)] private float basementVolume1 = 0.5f;

        [Tooltip("Maximum volume for basement ambient 2")]
        [SerializeField] [Range(0f, 1f)] private float basementVolume2 = 0.4f;

        [Header("Underground Audio")]
        [Tooltip("Primary ambient sound for the underground/excavation area")]
        [SerializeField] private AudioClip undergroundAmbient1;

        [Tooltip("Secondary ambient sound for excavation (plays together with primary)")]
        [SerializeField] private AudioClip undergroundAmbient2;

        [Tooltip("Third ambient sound for excavation (plays together with others)")]
        [SerializeField] private AudioClip undergroundAmbient3;

        [Tooltip("Maximum volume for underground ambient 1")]
        [SerializeField] [Range(0f, 1f)] private float undergroundVolume1 = 0.6f;

        [Tooltip("Maximum volume for underground ambient 2")]
        [SerializeField] [Range(0f, 1f)] private float undergroundVolume2 = 0.5f;

        [Tooltip("Maximum volume for underground ambient 3")]
        [SerializeField] [Range(0f, 1f)] private float undergroundVolume3 = 0.4f;

        [Header("Zone Settings")]
        [Tooltip("Y position where underground zone begins (below this = underground)")]
        [SerializeField] private float undergroundThresholdY = -5f;

        [Tooltip("Transition range for crossfade (in units above/below threshold)")]
        [SerializeField] private float transitionRange = 3f;

        [Header("Crossfade Settings")]
        [Tooltip("How fast to crossfade between zones")]
        [SerializeField] private float crossfadeSpeed = 2f;

        [Header("Debug")]
        [SerializeField] private bool showDebugInfo = false;

        // Audio sources
        private AudioSource basementSource1;
        private AudioSource basementSource2;
        private AudioSource undergroundSource1;
        private AudioSource undergroundSource2;
        private AudioSource undergroundSource3;

        // State
        private Transform playerTransform;
        private float targetBasementVolume1;
        private float targetBasementVolume2;
        private float targetUndergroundVolume1;
        private float targetUndergroundVolume2;
        private float targetUndergroundVolume3;
        private bool isInitialized = false;

        private void Awake()
        {
            if (Instance != null && Instance != this)
            {
                Destroy(gameObject);
                return;
            }
            Instance = this;
        }

        private void Start()
        {
            Initialize();
        }

        private void Initialize()
        {
            if (isInitialized) return;

            // Create audio sources
            basementSource1 = CreateAudioSource("BasementAmbient1", basementAmbient1);
            basementSource2 = CreateAudioSource("BasementAmbient2", basementAmbient2);
            undergroundSource1 = CreateAudioSource("UndergroundAmbient1", undergroundAmbient1);
            undergroundSource2 = CreateAudioSource("UndergroundAmbient2", undergroundAmbient2);
            undergroundSource3 = CreateAudioSource("UndergroundAmbient3", undergroundAmbient3);

            // Find player
            FindPlayer();

            // Start with basement ambient (player starts in basement)
            if (basementSource1 != null && basementAmbient1 != null)
            {
                basementSource1.volume = basementVolume1;
                basementSource1.Play();
                targetBasementVolume1 = basementVolume1;
            }

            if (basementSource2 != null && basementAmbient2 != null)
            {
                basementSource2.volume = basementVolume2;
                basementSource2.Play();
                targetBasementVolume2 = basementVolume2;
            }

            // Underground sources start silent
            if (undergroundSource1 != null && undergroundAmbient1 != null)
            {
                undergroundSource1.volume = 0f;
                undergroundSource1.Play();
                targetUndergroundVolume1 = 0f;
            }

            if (undergroundSource2 != null && undergroundAmbient2 != null)
            {
                undergroundSource2.volume = 0f;
                undergroundSource2.Play();
                targetUndergroundVolume2 = 0f;
            }

            if (undergroundSource3 != null && undergroundAmbient3 != null)
            {
                undergroundSource3.volume = 0f;
                undergroundSource3.Play();
                targetUndergroundVolume3 = 0f;
            }

            isInitialized = true;
        }

        private AudioSource CreateAudioSource(string name, AudioClip clip)
        {
            if (clip == null) return null;

            GameObject audioObj = new GameObject(name);
            audioObj.transform.SetParent(transform);

            AudioSource source = audioObj.AddComponent<AudioSource>();
            source.clip = clip;
            source.loop = true;
            source.playOnAwake = false;
            source.spatialBlend = 0f; // 2D sound
            source.volume = 0f;

            return source;
        }

        private void FindPlayer()
        {
            var player = GameObject.FindGameObjectWithTag("Player");
            if (player != null)
            {
                playerTransform = player.transform;
            }
            else
            {
                player = GameObject.Find("Player");
                if (player != null)
                {
                    playerTransform = player.transform;
                }
            }
        }

        private void Update()
        {
            if (!isInitialized) return;

            // Try to find player if not found yet
            if (playerTransform == null)
            {
                FindPlayer();
                return;
            }

            // Calculate zone blend based on player Y position
            float playerY = playerTransform.position.y;
            float blend = CalculateZoneBlend(playerY);

            // Set target volumes based on blend
            // blend = 0 means fully in basement, blend = 1 means fully underground
            targetBasementVolume1 = Mathf.Lerp(basementVolume1, 0f, blend);
            targetBasementVolume2 = Mathf.Lerp(basementVolume2, 0f, blend);
            targetUndergroundVolume1 = Mathf.Lerp(0f, undergroundVolume1, blend);
            targetUndergroundVolume2 = Mathf.Lerp(0f, undergroundVolume2, blend);
            targetUndergroundVolume3 = Mathf.Lerp(0f, undergroundVolume3, blend);

            // Smoothly adjust actual volumes
            if (basementSource1 != null)
            {
                basementSource1.volume = Mathf.MoveTowards(
                    basementSource1.volume,
                    targetBasementVolume1,
                    crossfadeSpeed * Time.deltaTime
                );
            }

            if (basementSource2 != null)
            {
                basementSource2.volume = Mathf.MoveTowards(
                    basementSource2.volume,
                    targetBasementVolume2,
                    crossfadeSpeed * Time.deltaTime
                );
            }

            if (undergroundSource1 != null)
            {
                undergroundSource1.volume = Mathf.MoveTowards(
                    undergroundSource1.volume,
                    targetUndergroundVolume1,
                    crossfadeSpeed * Time.deltaTime
                );
            }

            if (undergroundSource2 != null)
            {
                undergroundSource2.volume = Mathf.MoveTowards(
                    undergroundSource2.volume,
                    targetUndergroundVolume2,
                    crossfadeSpeed * Time.deltaTime
                );
            }

            if (undergroundSource3 != null)
            {
                undergroundSource3.volume = Mathf.MoveTowards(
                    undergroundSource3.volume,
                    targetUndergroundVolume3,
                    crossfadeSpeed * Time.deltaTime
                );
            }

            if (showDebugInfo && Time.frameCount % 60 == 0)
            {
                Debug.Log($"[AmbientAudio] Y={playerY:F1}, blend={blend:F2}, " +
                          $"basement1={basementSource1?.volume:F2}, basement2={basementSource2?.volume:F2}, " +
                          $"underground1={undergroundSource1?.volume:F2}, underground2={undergroundSource2?.volume:F2}, underground3={undergroundSource3?.volume:F2}");
            }
        }

        /// <summary>
        /// Calculate the blend factor between basement and underground (0 = basement, 1 = underground).
        /// </summary>
        private float CalculateZoneBlend(float playerY)
        {
            // Above transition zone = fully basement
            if (playerY > undergroundThresholdY + transitionRange)
            {
                return 0f;
            }

            // Below transition zone = fully underground
            if (playerY < undergroundThresholdY - transitionRange)
            {
                return 1f;
            }

            // In transition zone = blend
            float transitionStart = undergroundThresholdY + transitionRange;
            float transitionEnd = undergroundThresholdY - transitionRange;
            return Mathf.InverseLerp(transitionStart, transitionEnd, playerY);
        }

        /// <summary>
        /// Set the first basement ambient clip at runtime.
        /// </summary>
        public void SetBasementAmbient1(AudioClip clip)
        {
            basementAmbient1 = clip;
            if (basementSource1 != null)
            {
                bool wasPlaying = basementSource1.isPlaying;
                basementSource1.clip = clip;
                if (wasPlaying && clip != null)
                {
                    basementSource1.Play();
                }
            }
        }

        /// <summary>
        /// Set the second basement ambient clip at runtime.
        /// </summary>
        public void SetBasementAmbient2(AudioClip clip)
        {
            basementAmbient2 = clip;
            if (basementSource2 != null)
            {
                bool wasPlaying = basementSource2.isPlaying;
                basementSource2.clip = clip;
                if (wasPlaying && clip != null)
                {
                    basementSource2.Play();
                }
            }
        }

        /// <summary>
        /// Set the first underground ambient clip at runtime.
        /// </summary>
        public void SetUndergroundAmbient1(AudioClip clip)
        {
            undergroundAmbient1 = clip;
            if (undergroundSource1 != null)
            {
                bool wasPlaying = undergroundSource1.isPlaying;
                undergroundSource1.clip = clip;
                if (wasPlaying && clip != null)
                {
                    undergroundSource1.Play();
                }
            }
        }

        /// <summary>
        /// Set the second underground ambient clip at runtime.
        /// </summary>
        public void SetUndergroundAmbient2(AudioClip clip)
        {
            undergroundAmbient2 = clip;
            if (undergroundSource2 != null)
            {
                bool wasPlaying = undergroundSource2.isPlaying;
                undergroundSource2.clip = clip;
                if (wasPlaying && clip != null)
                {
                    undergroundSource2.Play();
                }
            }
        }

        /// <summary>
        /// Set the third underground ambient clip at runtime.
        /// </summary>
        public void SetUndergroundAmbient3(AudioClip clip)
        {
            undergroundAmbient3 = clip;
            if (undergroundSource3 != null)
            {
                bool wasPlaying = undergroundSource3.isPlaying;
                undergroundSource3.clip = clip;
                if (wasPlaying && clip != null)
                {
                    undergroundSource3.Play();
                }
            }
        }

        /// <summary>
        /// Pause all ambient audio.
        /// </summary>
        public void PauseAll()
        {
            if (basementSource1 != null) basementSource1.Pause();
            if (basementSource2 != null) basementSource2.Pause();
            if (undergroundSource1 != null) undergroundSource1.Pause();
            if (undergroundSource2 != null) undergroundSource2.Pause();
            if (undergroundSource3 != null) undergroundSource3.Pause();
        }

        /// <summary>
        /// Resume all ambient audio.
        /// </summary>
        public void ResumeAll()
        {
            if (basementSource1 != null) basementSource1.UnPause();
            if (basementSource2 != null) basementSource2.UnPause();
            if (undergroundSource1 != null) undergroundSource1.UnPause();
            if (undergroundSource2 != null) undergroundSource2.UnPause();
            if (undergroundSource3 != null) undergroundSource3.UnPause();
        }

        /// <summary>
        /// Stop all ambient audio.
        /// </summary>
        public void StopAll()
        {
            if (basementSource1 != null) basementSource1.Stop();
            if (basementSource2 != null) basementSource2.Stop();
            if (undergroundSource1 != null) undergroundSource1.Stop();
            if (undergroundSource2 != null) undergroundSource2.Stop();
            if (undergroundSource3 != null) undergroundSource3.Stop();
        }

        private void OnDestroy()
        {
            if (Instance == this)
            {
                Instance = null;
            }
        }
    }
}
