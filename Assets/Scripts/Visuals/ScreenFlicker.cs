using UnityEngine;

namespace BeneathTheFloor.Visuals
{
    /// <summary>
    /// Adds a subtle, non-periodic flicker to a screen glow material using Perlin noise.
    /// Attach to the ScreenGlow_Quad GameObject. Modulates emission intensity only.
    /// </summary>
    public class ScreenFlicker : MonoBehaviour
    {
        [Header("References")]
        [Tooltip("The Renderer component on this object. Auto-assigned if not set.")]
        [SerializeField] private Renderer targetRenderer;

        [Header("Flicker Settings")]
        [Tooltip("Base emission intensity multiplier.")]
        public float baseIntensity = 1.1f;

        [Tooltip("How much the intensity varies (subtle = 0.10-0.18).")]
        [Range(0.05f, 0.5f)]
        public float flickerAmount = 0.15f;

        [Tooltip("Speed of the flicker noise (5-7 for subtle).")]
        [Range(1f, 20f)]
        public float flickerSpeed = 6f;

        [Header("Debug")]
        [SerializeField] private bool enableDebugLogs = false;

        // Runtime state
        private Material _materialInstance;
        private Color _baseEmissionColor;
        private float _noiseOffsetX;
        private float _noiseOffsetY;
        private bool _isInitialized;

        private static readonly int EmissionColorID = Shader.PropertyToID("_EmissionColor");

        private void Reset()
        {
            // Auto-assign renderer when script is added in editor
            if (targetRenderer == null)
            {
                targetRenderer = GetComponent<Renderer>();
            }
        }

        private void Awake()
        {
            // Auto-assign renderer if not set
            if (targetRenderer == null)
            {
                targetRenderer = GetComponent<Renderer>();
            }

            // Randomize noise offset so multiple screens don't flicker in sync
            _noiseOffsetX = Random.Range(0f, 1000f);
            _noiseOffsetY = Random.Range(0f, 1000f);
        }

        private void Start()
        {
            Initialize();
        }

        private void Initialize()
        {
            if (_isInitialized) return;

            if (targetRenderer == null)
            {
                Debug.LogError($"[ScreenFlicker] No Renderer found on '{gameObject.name}'. Disabling.");
                enabled = false;
                return;
            }

            // Get material instance (this creates a unique copy, doesn't modify shared material)
            _materialInstance = targetRenderer.material;

            if (_materialInstance == null)
            {
                Debug.LogError($"[ScreenFlicker] No material on Renderer '{targetRenderer.name}'. Disabling.");
                enabled = false;
                return;
            }

            // Check if material has emission property
            if (!_materialInstance.HasProperty(EmissionColorID))
            {
                Debug.LogWarning($"[ScreenFlicker] Material '{_materialInstance.name}' doesn't have '_EmissionColor' property. " +
                                 "Make sure Emission is enabled on the material.");
            }

            // Store the base emission color (HDR color without intensity modification)
            _baseEmissionColor = _materialInstance.GetColor(EmissionColorID);

            // If base color is black/zero, use white as fallback
            if (_baseEmissionColor.maxColorComponent < 0.01f)
            {
                _baseEmissionColor = Color.white;
                if (enableDebugLogs)
                {
                    Debug.Log($"[ScreenFlicker] Base emission was near-black, using white as base.");
                }
            }

            _isInitialized = true;

            if (enableDebugLogs)
            {
                Debug.Log($"[ScreenFlicker] Initialized on '{gameObject.name}' with base color {_baseEmissionColor}");
            }
        }

        private void Update()
        {
            if (!_isInitialized || _materialInstance == null) return;

            // Use Perlin noise for smooth, non-periodic variation
            float noise = Mathf.PerlinNoise(
                Time.time * flickerSpeed + _noiseOffsetX,
                _noiseOffsetY
            );

            // Convert noise (0-1) to intensity variation centered around baseIntensity
            // noise - 0.5 gives us -0.5 to +0.5 range
            float intensity = baseIntensity + (noise - 0.5f) * flickerAmount;

            // Clamp to prevent negative intensity
            intensity = Mathf.Max(0f, intensity);

            // Apply to emission color
            Color emissionColor = _baseEmissionColor * intensity;
            _materialInstance.SetColor(EmissionColorID, emissionColor);
        }

        private void OnDestroy()
        {
            // Clean up material instance to prevent memory leaks
            if (_materialInstance != null)
            {
                Destroy(_materialInstance);
                _materialInstance = null;
            }
        }

#if UNITY_EDITOR
        private void OnValidate()
        {
            // Update in editor when values change (only if playing)
            if (Application.isPlaying && _isInitialized && _materialInstance != null)
            {
                // Force an immediate update with current values
                float noise = Mathf.PerlinNoise(Time.time * flickerSpeed + _noiseOffsetX, _noiseOffsetY);
                float intensity = baseIntensity + (noise - 0.5f) * flickerAmount;
                intensity = Mathf.Max(0f, intensity);
                _materialInstance.SetColor(EmissionColorID, _baseEmissionColor * intensity);
            }
        }
#endif
    }
}
