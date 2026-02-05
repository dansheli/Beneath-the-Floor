using UnityEngine;
using System.Collections.Generic;
using BeneathTheFloor.Digging;

namespace BeneathTheFloor.Lighting
{
    /// <summary>
    /// Comprehensive underground lighting system for the digging game.
    /// Handles:
    /// - Basement light spillover to first layer
    /// - Depth-based ambient lighting (subtle color temperature shifts)
    /// - Placeable lamp system (purchasable lights)
    /// - Glowing resources at depth
    /// </summary>
    public class UndergroundLightingSystem : MonoBehaviour
    {
        public static UndergroundLightingSystem Instance { get; private set; }

        [Header("References")]
        [SerializeField] private Transform playerTransform;
        [SerializeField] private Digging.DepthManager depthManager;

        [Header("Basement Spillover Light")]
        [Tooltip("Light that spills from basement into the first dig layer")]
        [SerializeField] private Light basementSpillLight;
        [SerializeField] private float spillLightIntensity = 1.5f;
        [SerializeField] private float spillLightRange = 8f;
        [SerializeField] private Color spillLightColor = new Color(1f, 0.95f, 0.9f); // Warm indoor light
        [SerializeField] private float spillFadeStartDepth = 0f;
        [SerializeField] private float spillFadeEndDepth = 5f; // Fades out by 5m depth

        [Header("Depth-Based Ambient")]
        [Tooltip("Enable subtle ambient color shifts based on depth")]
        [SerializeField] private bool enableDepthAmbient = true;
        [Tooltip("Physical lighting only - NO ambient light underground, only lamps illuminate")]
        [SerializeField] private bool physicalLightingOnly = true;
        [SerializeField] private Gradient depthAmbientGradient;
        [SerializeField] private AnimationCurve depthIntensityCurve = AnimationCurve.EaseInOut(0, 1, 1, 0.1f);
        [SerializeField] private float maxAmbientDepth = 30f;
        [SerializeField] private float baseAmbientIntensity = 0.3f;
        [SerializeField] private float minAmbientIntensity = 0.02f;
        [Tooltip("World Y position below which physical-only lighting applies (default 1 = ground level)")]
        [SerializeField] private float physicalLightingBelowY = 1f;

        [Header("Depth Fog")]
        [SerializeField] private bool enableDepthFog = true;
        [SerializeField] private Color shallowFogColor = new Color(0.3f, 0.25f, 0.2f);
        [SerializeField] private Color deepFogColor = new Color(0.05f, 0.03f, 0.02f);
        [SerializeField] private float baseFogDensity = 0.01f;
        [SerializeField] private float maxFogDensity = 0.08f;

        [Header("Placeable Lamps")]
        [SerializeField] private GameObject lampPrefab;
        [SerializeField] private int maxPlaceableLamps = 20;
        [Tooltip("Extra radius around dig to check for lamp support (added to dig radius).")]
        [SerializeField] private float lampSupportCheckBuffer = 1f;

        [Header("Debug")]
        [SerializeField] private bool showDebugInfo = false;

        // Runtime state
        private float _currentDepth;
        private List<PlaceableLamp> _placedLamps = new List<PlaceableLamp>();
        private Digging.ChunkManager _chunkManager;
        private float _originalAmbientIntensity;
        private Color _originalAmbientColor;
        private bool _originalFogEnabled;
        private float _originalFogDensity;
        private Color _originalFogColor;
        private float _originalReflectionIntensity;
        private UnityEngine.Rendering.AmbientMode _originalAmbientMode;

        private void Awake()
        {
            if (Instance == null)
            {
                Instance = this;
            }
            else if (Instance != this)
            {
                Destroy(gameObject);
                return;
            }

            // Store original render settings
            _originalAmbientIntensity = RenderSettings.ambientIntensity;
            _originalAmbientColor = RenderSettings.ambientSkyColor;
            _originalFogEnabled = RenderSettings.fog;
            _originalFogDensity = RenderSettings.fogDensity;
            _originalFogColor = RenderSettings.fogColor;
            _originalReflectionIntensity = RenderSettings.reflectionIntensity;
            _originalAmbientMode = RenderSettings.ambientMode;

            // Setup default gradient if not set
            if (depthAmbientGradient == null || depthAmbientGradient.colorKeys.Length == 0)
            {
                SetupDefaultDepthGradient();
            }
        }

        private void Start()
        {
            FindReferences();
            CreateBasementSpillLight();

            if (enableDepthFog)
            {
                RenderSettings.fog = true;
                RenderSettings.fogMode = FogMode.Exponential;
            }

            // Ensure directional lights don't affect underground layers
            if (physicalLightingOnly)
            {
                ExcludeDirectionalLightsFromUnderground();
            }

            // Subscribe to dig events for lamp support checking
            _chunkManager = FindObjectOfType<Digging.ChunkManager>();
            if (_chunkManager != null)
            {
                _chunkManager.OnDigCompleted += OnDigCompleted;
            }
        }

        /// <summary>
        /// Exclude all directional lights from underground layers (7=Diggable, 9=Basement, 10=BasementFloor)
        /// </summary>
        private void ExcludeDirectionalLightsFromUnderground()
        {
            Light[] allLights = FindObjectsOfType<Light>();
            int undergroundMask = (1 << 7) | (1 << 9) | (1 << 10); // Layers 7, 9, 10

            foreach (var light in allLights)
            {
                if (light.type == LightType.Directional)
                {
                    int oldMask = light.cullingMask;
                    int newMask = oldMask & ~undergroundMask; // Remove underground layers

                    if (oldMask != newMask)
                    {
                        light.cullingMask = newMask;
                    }
                }
            }
        }

        private void OnDestroy()
        {
            if (Instance == this)
            {
                Instance = null;
            }

            // Unsubscribe from dig events
            if (_chunkManager != null)
            {
                _chunkManager.OnDigCompleted -= OnDigCompleted;
            }

            // Restore original settings
            RenderSettings.ambientIntensity = _originalAmbientIntensity;
            RenderSettings.ambientSkyColor = _originalAmbientColor;
            RenderSettings.fog = _originalFogEnabled;
            RenderSettings.fogDensity = _originalFogDensity;
            RenderSettings.fogColor = _originalFogColor;
            RenderSettings.reflectionIntensity = _originalReflectionIntensity;
            RenderSettings.ambientMode = _originalAmbientMode;
        }

        /// <summary>
        /// Called when digging occurs. Checks support for lamps near the dig location.
        /// PERFORMANCE: Only checks lamps near dig, cleans up destroyed lamps.
        /// </summary>
        private void OnDigCompleted(DigResult result)
        {
            if (!result.Success || _placedLamps.Count == 0)
                return;

            Vector3 digPosition = result.Operation.WorldPosition;
            float checkRadius = result.Operation.Radius + lampSupportCheckBuffer;
            float checkRadiusSqr = checkRadius * checkRadius;

            // PERFORMANCE: Clean up destroyed lamps and check support in single pass
            for (int i = _placedLamps.Count - 1; i >= 0; i--)
            {
                var lamp = _placedLamps[i];

                // Remove destroyed/null lamps from list
                if (lamp == null)
                {
                    _placedLamps.RemoveAt(i);
                    continue;
                }

                if (lamp.IsFalling)
                    continue;

                float distSqr = (lamp.transform.position - digPosition).sqrMagnitude;
                if (distSqr <= checkRadiusSqr)
                {
                    lamp.CheckSupport();
                }
            }
        }

        private void Update()
        {
            UpdateDepth();
            UpdateBasementSpillover();
            UpdateDepthAmbient();
            UpdateDepthFog();
        }

        private void FindReferences()
        {
            if (playerTransform == null)
            {
                var player = GameObject.FindGameObjectWithTag("Player");
                if (player != null)
                    playerTransform = player.transform;
                else if (Camera.main != null)
                    playerTransform = Camera.main.transform;
            }

            if (depthManager == null)
            {
                depthManager = FindObjectOfType<Digging.DepthManager>();
            }
        }

        private void SetupDefaultDepthGradient()
        {
            // Create subtle depth gradient
            // Surface: Warm (basement light influence)
            // Mid: Neutral earth tones
            // Deep: Cool, dark tones
            depthAmbientGradient = new Gradient();

            GradientColorKey[] colorKeys = new GradientColorKey[4];
            colorKeys[0] = new GradientColorKey(new Color(0.4f, 0.35f, 0.3f), 0f);    // Surface - warm
            colorKeys[1] = new GradientColorKey(new Color(0.25f, 0.22f, 0.2f), 0.3f); // Shallow - earth
            colorKeys[2] = new GradientColorKey(new Color(0.15f, 0.13f, 0.12f), 0.6f); // Mid - cooler
            colorKeys[3] = new GradientColorKey(new Color(0.05f, 0.04f, 0.03f), 1f);   // Deep - near black

            GradientAlphaKey[] alphaKeys = new GradientAlphaKey[2];
            alphaKeys[0] = new GradientAlphaKey(1f, 0f);
            alphaKeys[1] = new GradientAlphaKey(1f, 1f);

            depthAmbientGradient.SetKeys(colorKeys, alphaKeys);
        }

        private void CreateBasementSpillLight()
        {
            if (basementSpillLight != null) return;

            // Create the spillover light
            var spillObj = new GameObject("BasementSpillLight");
            spillObj.transform.SetParent(transform);

            // Position at basement floor level, pointing down into the dig area
            var terrainManager = Digging.UndergroundTerrainManager.Instance;
            float basementY = terrainManager != null ? terrainManager.basementFloorY : -3f;
            spillObj.transform.position = new Vector3(0, basementY, 0);
            spillObj.transform.rotation = Quaternion.Euler(90, 0, 0); // Point down

            basementSpillLight = spillObj.AddComponent<Light>();
            basementSpillLight.type = LightType.Spot;
            basementSpillLight.intensity = spillLightIntensity;
            basementSpillLight.range = spillLightRange;
            basementSpillLight.spotAngle = 120f;
            basementSpillLight.innerSpotAngle = 80f;
            basementSpillLight.color = spillLightColor;
            basementSpillLight.shadows = LightShadows.Soft;
            basementSpillLight.shadowStrength = 0.5f;

            // Set to affect Basement and Diggable layers
            basementSpillLight.cullingMask = (1 << 7) | (1 << 9) | (1 << 10); // Diggable, Basement, BasementFloor
        }

        private void UpdateDepth()
        {
            if (depthManager != null)
            {
                _currentDepth = depthManager.DepthBelowSoil;
            }
            else if (playerTransform != null)
            {
                // Fallback calculation
                var terrainManager = Digging.UndergroundTerrainManager.Instance;
                float basementY = terrainManager != null ? terrainManager.basementFloorY : -3f;
                _currentDepth = Mathf.Max(0, basementY - playerTransform.position.y);
            }
        }

        private void UpdateBasementSpillover()
        {
            if (basementSpillLight == null) return;

            // Fade out spillover light as player goes deeper
            float fadeT = Mathf.InverseLerp(spillFadeStartDepth, spillFadeEndDepth, _currentDepth);
            float intensity = Mathf.Lerp(spillLightIntensity, 0f, fadeT);
            basementSpillLight.intensity = intensity;
        }

        private void UpdateDepthAmbient()
        {
            if (!enableDepthAmbient) return;

            // Use world Y position to determine if underground (not affected by jumping)
            float playerY = playerTransform != null ? playerTransform.position.y : -10f;
            bool isUnderground = playerY < physicalLightingBelowY;

            // Physical lighting only mode - zero ambient when underground
            if (physicalLightingOnly && isUnderground)
            {
                // Complete darkness - only physical lights illuminate
                RenderSettings.ambientMode = UnityEngine.Rendering.AmbientMode.Flat;
                RenderSettings.ambientLight = Color.black;
                RenderSettings.ambientSkyColor = Color.black;
                RenderSettings.ambientEquatorColor = Color.black;
                RenderSettings.ambientGroundColor = Color.black;
                RenderSettings.ambientIntensity = 0f;

                // Disable reflections underground
                RenderSettings.reflectionIntensity = 0f;
                return;
            }

            // Above ground or physical lighting disabled - use gradient-based ambient
            float depthT = Mathf.Clamp01(_currentDepth / maxAmbientDepth);

            // Get color from gradient
            Color ambientColor = depthAmbientGradient.Evaluate(depthT);
            RenderSettings.ambientSkyColor = ambientColor;
            RenderSettings.ambientEquatorColor = ambientColor * 0.8f;
            RenderSettings.ambientGroundColor = ambientColor * 0.5f;

            // Apply intensity curve
            float intensityT = depthIntensityCurve.Evaluate(depthT);
            float intensity = Mathf.Lerp(baseAmbientIntensity, minAmbientIntensity, 1f - intensityT);
            RenderSettings.ambientIntensity = intensity;

            // Restore reflections above ground
            RenderSettings.reflectionIntensity = 1f;
        }

        private void UpdateDepthFog()
        {
            if (!enableDepthFog) return;

            float depthT = Mathf.Clamp01(_currentDepth / maxAmbientDepth);

            // Interpolate fog color and density
            RenderSettings.fogColor = Color.Lerp(shallowFogColor, deepFogColor, depthT);
            RenderSettings.fogDensity = Mathf.Lerp(baseFogDensity, maxFogDensity, depthT);
        }

        #region Placeable Lamps API

        /// <summary>
        /// Attempts to place a lamp at the given position.
        /// Returns true if successful.
        /// </summary>
        public bool PlaceLamp(Vector3 position, Quaternion rotation, Vector3 surfaceNormal = default)
        {
            if (_placedLamps.Count >= maxPlaceableLamps)
            {
                return false;
            }

            // Default normal to up if not specified
            if (surfaceNormal == default)
            {
                surfaceNormal = Vector3.up;
            }

            GameObject lampObj;
            if (lampPrefab != null)
            {
                lampObj = Instantiate(lampPrefab, position, rotation);
            }
            else
            {
                // Create default lamp if no prefab assigned
                lampObj = CreateDefaultLamp(position, rotation);
            }

            var lamp = lampObj.GetComponent<PlaceableLamp>();
            if (lamp == null)
            {
                lamp = lampObj.AddComponent<PlaceableLamp>();
            }

            // Set the placement normal for support detection
            lamp.SetPlacementNormal(surfaceNormal);

            _placedLamps.Add(lamp);
            return true;
        }

        /// <summary>
        /// Creates a default lamp object when no prefab is assigned.
        /// </summary>
        private GameObject CreateDefaultLamp(Vector3 position, Quaternion rotation)
        {
            var lampObj = new GameObject("PlacedLamp");
            lampObj.transform.position = position;
            lampObj.transform.rotation = rotation;

            // Add PlaceableLamp component (it will create its own light and visual)
            lampObj.AddComponent<PlaceableLamp>();

            return lampObj;
        }

        /// <summary>
        /// Remove a placed lamp.
        /// </summary>
        public bool RemoveLamp(PlaceableLamp lamp)
        {
            if (_placedLamps.Remove(lamp))
            {
                if (lamp != null)
                {
                    Destroy(lamp.gameObject);
                }
                return true;
            }
            return false;
        }

        /// <summary>
        /// Get the number of lamps currently placed.
        /// </summary>
        public int PlacedLampCount => _placedLamps.Count;

        /// <summary>
        /// Get the maximum number of lamps allowed.
        /// </summary>
        public int MaxLamps => maxPlaceableLamps;

        /// <summary>
        /// Get all placed lamp positions and rotations for saving.
        /// </summary>
        public List<(Vector3 position, Quaternion rotation)> GetPlacedLampData()
        {
            var lampData = new List<(Vector3, Quaternion)>();
            foreach (var lamp in _placedLamps)
            {
                if (lamp != null)
                {
                    lampData.Add((lamp.transform.position, lamp.transform.rotation));
                }
            }
            return lampData;
        }

        /// <summary>
        /// Clear all placed lamps (used before loading saved lamps).
        /// </summary>
        public void ClearAllPlacedLamps()
        {
            foreach (var lamp in _placedLamps)
            {
                if (lamp != null)
                {
                    Destroy(lamp.gameObject);
                }
            }
            _placedLamps.Clear();
        }

        /// <summary>
        /// Restore placed lamps from save data.
        /// </summary>
        public void RestorePlacedLamps(List<(Vector3 position, Quaternion rotation)> lampData)
        {
            // Clear existing lamps first
            ClearAllPlacedLamps();

            // Restore each lamp
            foreach (var (position, rotation) in lampData)
            {
                PlaceLamp(position, rotation);
            }

            if (showDebugInfo)
            {
                Debug.Log($"[UndergroundLightingSystem] Restored {lampData.Count} placed lamps");
            }
        }

        #endregion

        #region Public API

        /// <summary>
        /// Get current player depth below soil surface.
        /// </summary>
        public float CurrentDepth => _currentDepth;

        /// <summary>
        /// Set the basement spillover light intensity.
        /// </summary>
        public void SetSpillLightIntensity(float intensity)
        {
            spillLightIntensity = intensity;
            if (basementSpillLight != null)
            {
                basementSpillLight.intensity = intensity;
            }
        }

        /// <summary>
        /// Set the basement spillover light range.
        /// </summary>
        public void SetSpillLightRange(float range)
        {
            spillLightRange = range;
            if (basementSpillLight != null)
            {
                basementSpillLight.range = range;
            }
        }

        #endregion

        private void OnGUI()
        {
            if (!showDebugInfo) return;

            GUILayout.BeginArea(new Rect(10, 10, 300, 200));
            GUILayout.Label($"=== Underground Lighting ===");
            GUILayout.Label($"Depth: {_currentDepth:F1}m");
            GUILayout.Label($"Spill Light: {basementSpillLight?.intensity:F2}");
            GUILayout.Label($"Ambient: {RenderSettings.ambientIntensity:F2}");
            GUILayout.Label($"Fog Density: {RenderSettings.fogDensity:F3}");
            GUILayout.Label($"Placed Lamps: {_placedLamps.Count}/{maxPlaceableLamps}");
            GUILayout.EndArea();
        }
    }
}
