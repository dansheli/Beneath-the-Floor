using UnityEngine;

namespace BeneathTheFloor.Digging
{
    /// <summary>
    /// Handles visual and audio feedback for digging.
    /// </summary>
    public class DigFeedbackController : MonoBehaviour
    {
        [Header("Particle Effects")]
        [Tooltip("Particle prefab to spawn at dig location.")]
        public GameObject digParticlePrefab;

        [Tooltip("Number of particles to spawn per dig.")]
        [Range(1, 10)]
        public int particleCount = 3;

        [Header("Audio")]
        [Tooltip("Audio source for dig sounds.")]
        public AudioSource audioSource;

        [Tooltip("Dig hit sounds (plays randomly).")]
        public AudioClip[] digHitSounds;

        [Tooltip("Sound to play when dig is blocked (depth limit, etc).")]
        public AudioClip blockedSound;

        [Tooltip("Volume range for dig sounds.")]
        [Range(0f, 1f)]
        public float minVolume = 0.7f;
        [Range(0f, 1f)]
        public float maxVolume = 1f;

        [Tooltip("Pitch variation range.")]
        [Range(0f, 0.5f)]
        public float pitchVariation = 0.1f;

        [Header("Camera Shake")]
        [Tooltip("Enable camera shake on dig.")]
        public bool enableCameraShake = true;

        [Tooltip("Camera shake intensity.")]
        [Range(0f, 1f)]
        public float shakeIntensity = 0.1f;

        [Tooltip("Camera shake duration.")]
        [Range(0f, 0.5f)]
        public float shakeDuration = 0.1f;

        [Header("Debug")]
        public bool enableDebugLogs = false;

        public static DigFeedbackController Instance { get; private set; }

        /// <summary>
        /// Reset static instance when entering play mode.
        /// </summary>
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetStaticInstance()
        {
            Instance = null;
        }

        private Camera mainCamera;
        private Vector3 originalCameraPosition;
        private float shakeTimer;
        private bool isShaking;
        private static bool _hasDuplicateWarned;

        private void Awake()
        {
            // Reset warning flag on domain reload
            _hasDuplicateWarned = false;

            if (Instance == null)
            {
                Instance = this;
            }
            else if (Instance != this)
            {
                // Duplicate detected - warn once and destroy this one
                if (!_hasDuplicateWarned)
                {
                    Debug.LogWarning($"[DigFeedbackController] Duplicate detected on '{gameObject.name}'. Keeping existing instance on '{Instance.gameObject.name}'.");
                    _hasDuplicateWarned = true;
                }
                Destroy(this);
                return;
            }
        }

        /// <summary>
        /// Ensure the scene-placed instance becomes the singleton when script reloads in editor.
        /// </summary>
        private void OnEnable()
        {
            if (Instance == null)
            {
                Instance = this;
            }
        }

        private void OnDestroy()
        {
            if (Instance == this)
            {
                Instance = null;
            }
        }

        private void Start()
        {
            mainCamera = Camera.main;

            // Create audio source if not assigned
            if (audioSource == null)
            {
                audioSource = gameObject.AddComponent<AudioSource>();
                audioSource.playOnAwake = false;
                audioSource.spatialBlend = 0f; // 2D sound
            }

            // Create default particle if not assigned
            if (digParticlePrefab == null)
            {
                CreateDefaultParticle();
            }
        }

        private void Update()
        {
            // Process camera shake
            if (isShaking)
            {
                ProcessCameraShake();
            }
        }

        private void CreateDefaultParticle()
        {
            // Create a simple particle system as placeholder
            digParticlePrefab = new GameObject("DigParticlePrefab");
            digParticlePrefab.SetActive(false);

            var ps = digParticlePrefab.AddComponent<ParticleSystem>();

            // Stop before configuring to prevent "duration while playing" warning
            ps.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);

            var main = ps.main;
            main.startLifetime = 0.5f;
            main.startSpeed = 3f;
            main.startSize = 0.1f;
            main.startColor = new Color(0.5f, 0.4f, 0.3f); // Brown/dirt color
            main.simulationSpace = ParticleSystemSimulationSpace.World;
            main.maxParticles = 50;
            main.duration = 0.2f;
            main.loop = false;

            var emission = ps.emission;
            emission.rateOverTime = 0;
            emission.SetBursts(new ParticleSystem.Burst[] {
                new ParticleSystem.Burst(0f, 10)
            });

            var shape = ps.shape;
            shape.shapeType = ParticleSystemShapeType.Sphere;
            shape.radius = 0.2f;

            // Auto-destroy after playing
            var autodestroy = digParticlePrefab.AddComponent<ParticleAutoDestroy>();

            if (enableDebugLogs)
                Debug.Log("[DigFeedbackController] Created default particle prefab");
        }

        /// <summary>
        /// Play dig feedback at the given position.
        /// </summary>
        public void PlayDigFeedback(Vector3 position)
        {
            // Spawn particles
            SpawnParticles(position);

            // Play sound
            PlayDigSound();

            // Camera shake
            if (enableCameraShake)
            {
                StartCameraShake();
            }

            if (enableDebugLogs)
                Debug.Log($"[DigFeedbackController] Played feedback at {position}");
        }

        /// <summary>
        /// Play dig feedback at the given position with dig result info.
        /// </summary>
        public void PlayDigFeedback(Vector3 position, DigResult result)
        {
            PlayDigFeedback(position);
        }

        /// <summary>
        /// Play feedback when dig is blocked (depth limit, etc).
        /// No particles, just sound and subtle UI feedback.
        /// </summary>
        public void PlayBlockedFeedback(Vector3 position)
        {
            // Play blocked sound
            if (audioSource != null && blockedSound != null)
            {
                audioSource.pitch = 0.8f; // Lower pitch for "denial" feel
                audioSource.volume = maxVolume;
                audioSource.PlayOneShot(blockedSound);
            }

            if (enableDebugLogs)
                Debug.Log($"[DigFeedbackController] Played blocked feedback at {position}");
        }

        /// <summary>
        /// Play blocked feedback from a static context.
        /// </summary>
        public static void PlayBlockedFeedbackStatic(Vector3 position)
        {
            if (Instance != null)
            {
                Instance.PlayBlockedFeedback(position);
            }
        }

        private void SpawnParticles(Vector3 position)
        {
            if (digParticlePrefab == null)
                return;

            for (int i = 0; i < particleCount; i++)
            {
                // Add slight random offset
                Vector3 offset = Random.insideUnitSphere * 0.2f;
                GameObject particle = Instantiate(digParticlePrefab, position + offset, Quaternion.identity);
                particle.SetActive(true);

                // Play particle system
                var ps = particle.GetComponent<ParticleSystem>();
                if (ps != null)
                {
                    ps.Play();
                }
            }
        }

        private void PlayDigSound()
        {
            if (audioSource == null || digHitSounds == null || digHitSounds.Length == 0)
            {
                // TODO: Try AudioManager if available
                // AudioManager.Instance?.PlaySound("Dig_Hit");
                return;
            }

            // Pick random sound
            AudioClip clip = digHitSounds[Random.Range(0, digHitSounds.Length)];
            if (clip != null)
            {
                audioSource.pitch = 1f + Random.Range(-pitchVariation, pitchVariation);
                audioSource.volume = Random.Range(minVolume, maxVolume);
                audioSource.PlayOneShot(clip);
            }
        }

        private void StartCameraShake()
        {
            if (mainCamera == null)
                return;

            if (!isShaking)
            {
                originalCameraPosition = mainCamera.transform.localPosition;
            }

            shakeTimer = shakeDuration;
            isShaking = true;
        }

        private void ProcessCameraShake()
        {
            if (mainCamera == null)
            {
                isShaking = false;
                return;
            }

            shakeTimer -= Time.deltaTime;

            if (shakeTimer > 0)
            {
                // Apply shake offset
                float t = shakeTimer / shakeDuration;
                float intensity = shakeIntensity * t; // Fade out

                Vector3 offset = new Vector3(
                    Random.Range(-1f, 1f) * intensity,
                    Random.Range(-1f, 1f) * intensity,
                    0f
                );

                mainCamera.transform.localPosition = originalCameraPosition + offset;
            }
            else
            {
                // Reset to original position
                mainCamera.transform.localPosition = originalCameraPosition;
                isShaking = false;
            }
        }

        /// <summary>
        /// Play feedback from a static context.
        /// </summary>
        public static void PlayFeedbackStatic(Vector3 position)
        {
            if (Instance != null)
            {
                Instance.PlayDigFeedback(position);
            }
        }

        /// <summary>
        /// Play feedback from a static context with dig result.
        /// </summary>
        public static void PlayFeedbackStatic(Vector3 position, DigResult result)
        {
            if (Instance != null)
            {
                Instance.PlayDigFeedback(position, result);
            }
        }
    }

    /// <summary>
    /// Auto-destroys a GameObject after its particle system finishes.
    /// </summary>
    public class ParticleAutoDestroy : MonoBehaviour
    {
        private ParticleSystem ps;

        private void Start()
        {
            ps = GetComponent<ParticleSystem>();
            if (ps == null)
            {
                Destroy(gameObject);
            }
        }

        private void Update()
        {
            if (ps != null && !ps.isPlaying)
            {
                Destroy(gameObject);
            }
        }
    }
}
