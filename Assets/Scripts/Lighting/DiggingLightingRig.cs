using UnityEngine;

namespace BeneathTheFloor.Lighting
{
    /// <summary>
    /// Manages the lighting rig for the digging area.
    /// Creates atmospheric lighting that responds to player depth.
    /// - First 0-3m: Well-lit for clarity and WOW
    /// - Deeper: Gradually darker and more atmospheric
    /// </summary>
    public class DiggingLightingRig : MonoBehaviour
    {
        [Header("References")]
        [Tooltip("Player/camera transform for depth tracking. Auto-found if null.")]
        [SerializeField] private Transform playerTransform;

        [Tooltip("Reference to DepthManager for accurate depth values.")]
        [SerializeField] private Digging.DepthManager depthManager;

        [Header("Positioning")]
        [Tooltip("Y position of the basement floor (lights position above this).")]
        [SerializeField] private float basementFloorY = -3.0f;

        [Tooltip("Height above basement floor to place the main light.")]
        [SerializeField] private float mainLightHeightOffset = 1.5f;

        [Header("Main Work Light (Spot)")]
        [SerializeField] private Light mainSpotLight;
        [SerializeField] private float mainLightIntensity = 2.5f;
        [SerializeField] private float mainLightRange = 12f;
        [SerializeField] private float mainLightSpotAngle = 75f;
        [SerializeField] private Color mainLightColor = new Color(1f, 0.95f, 0.85f); // Warm white

        [Header("Rim Lights (Edge Highlighting)")]
        [SerializeField] private Light[] rimLights;
        [SerializeField] private float rimLightIntensity = 0.8f;
        [SerializeField] private float rimLightRange = 8f;
        [SerializeField] private float rimLightSpotAngle = 45f;
        [SerializeField] private Color rimLightColor = new Color(0.9f, 0.85f, 0.75f);

        [Header("Fill Light (Hole Interior)")]
        [SerializeField] private Light fillLight;
        [SerializeField] private float fillLightIntensity = 0.4f;
        [SerializeField] private float fillLightRange = 15f;
        [SerializeField] private Color fillLightColor = new Color(0.7f, 0.75f, 0.85f); // Cool blue tint

        [Header("Depth-Based Dimming")]
        [Tooltip("Depth (meters) where lights are at full brightness.")]
        [SerializeField] private float fullBrightnessDepth = 3f;

        [Tooltip("Depth (meters) where lights reach minimum brightness.")]
        [SerializeField] private float minBrightnessDepth = 25f;

        [Tooltip("Minimum intensity multiplier at max depth (0-1).")]
        [SerializeField] private float minIntensityMultiplier = 0.15f;

        [Tooltip("Curve for intensity falloff. Higher = faster initial dropoff.")]
        [SerializeField] private AnimationCurve depthFalloffCurve = AnimationCurve.EaseInOut(0, 1, 1, 0);

        [Header("Depth-Based Fog")]
        [SerializeField] private bool enableDepthFog = true;
        [SerializeField] private float baseFogDensity = 0.015f;
        [SerializeField] private float maxFogDensity = 0.06f;
        [SerializeField] private Color shallowFogColor = new Color(0.35f, 0.30f, 0.25f);
        [SerializeField] private Color deepFogColor = new Color(0.12f, 0.10f, 0.08f);

        [Header("Debug")]
        [SerializeField] private bool enableDebugLogs = false;
        [SerializeField] private bool showGizmos = true;

        // Runtime state
        private float _currentDepth;
        private float _currentIntensityMultiplier = 1f;
        private bool _originalFogEnabled;
        private Color _originalFogColor;
        private float _originalFogDensity;

        // Cached base values
        private float _baseMainIntensity;
        private float _baseRimIntensity;
        private float _baseFillIntensity;

        private void Awake()
        {
            // Cache base intensities
            _baseMainIntensity = mainLightIntensity;
            _baseRimIntensity = rimLightIntensity;
            _baseFillIntensity = fillLightIntensity;

            // Store original fog settings
            _originalFogEnabled = RenderSettings.fog;
            _originalFogColor = RenderSettings.fogColor;
            _originalFogDensity = RenderSettings.fogDensity;
        }

        private void Start()
        {
            // Find player if not assigned
            if (playerTransform == null)
            {
                var player = GameObject.FindGameObjectWithTag("Player");
                if (player != null)
                    playerTransform = player.transform;
                else
                {
                    var cam = Camera.main;
                    if (cam != null)
                        playerTransform = cam.transform;
                }
            }

            // Find depth manager if not assigned
            if (depthManager == null)
            {
                depthManager = FindObjectOfType<Digging.DepthManager>();
            }

            // Sync basement floor Y from terrain manager
            var terrainManager = Digging.UndergroundTerrainManager.Instance;
            if (terrainManager != null)
            {
                basementFloorY = terrainManager.basementFloorY;
                if (enableDebugLogs)
                    Debug.Log($"[DiggingLightingRig] Synced basementFloorY = {basementFloorY}");
            }

            // Create lights if not assigned
            if (mainSpotLight == null)
                CreateMainLight();

            if (rimLights == null || rimLights.Length == 0)
                CreateRimLights();

            if (fillLight == null)
                CreateFillLight();

            // Initialize fog
            if (enableDepthFog)
            {
                RenderSettings.fog = true;
                RenderSettings.fogMode = FogMode.Exponential;
            }

            if (enableDebugLogs)
                Debug.Log($"[DiggingLightingRig] Initialized at basementFloorY={basementFloorY}");
        }

        private void Update()
        {
            UpdateDepth();
            UpdateLightIntensities();
            UpdateFog();
        }

        private void OnDisable()
        {
            // Restore original fog settings
            RenderSettings.fog = _originalFogEnabled;
            RenderSettings.fogColor = _originalFogColor;
            RenderSettings.fogDensity = _originalFogDensity;
        }

        private void UpdateDepth()
        {
            if (depthManager != null)
            {
                // Use adjusted depth (relative to soil surface)
                _currentDepth = depthManager.DepthBelowSoil;
            }
            else if (playerTransform != null)
            {
                // Fallback: calculate depth from player position
                float playerY = playerTransform.position.y;
                _currentDepth = Mathf.Max(0, basementFloorY - playerY);
            }
        }

        private void UpdateLightIntensities()
        {
            // Calculate intensity multiplier based on depth
            float depthRatio = Mathf.InverseLerp(fullBrightnessDepth, minBrightnessDepth, _currentDepth);
            float curveValue = depthFalloffCurve.Evaluate(depthRatio);
            _currentIntensityMultiplier = Mathf.Lerp(1f, minIntensityMultiplier, curveValue);

            // Apply to main light
            if (mainSpotLight != null)
            {
                mainSpotLight.intensity = _baseMainIntensity * _currentIntensityMultiplier;

                // Also reduce range slightly at depth
                float rangeMultiplier = Mathf.Lerp(1f, 0.7f, depthRatio);
                mainSpotLight.range = mainLightRange * rangeMultiplier;
            }

            // Apply to rim lights (dim faster than main)
            if (rimLights != null)
            {
                float rimMultiplier = _currentIntensityMultiplier * Mathf.Lerp(1f, 0.5f, depthRatio);
                foreach (var light in rimLights)
                {
                    if (light != null)
                        light.intensity = _baseRimIntensity * rimMultiplier;
                }
            }

            // Fill light stays more consistent (ambient fill)
            if (fillLight != null)
            {
                float fillMultiplier = Mathf.Lerp(1f, 0.6f, depthRatio);
                fillLight.intensity = _baseFillIntensity * fillMultiplier;
            }
        }

        private void UpdateFog()
        {
            if (!enableDepthFog)
                return;

            float depthRatio = Mathf.InverseLerp(0, minBrightnessDepth, _currentDepth);

            // Update fog density
            float fogDensity = Mathf.Lerp(baseFogDensity, maxFogDensity, depthRatio);
            RenderSettings.fogDensity = fogDensity;

            // Update fog color
            Color fogColor = Color.Lerp(shallowFogColor, deepFogColor, depthRatio);
            RenderSettings.fogColor = fogColor;
        }

        #region Light Creation

        private void CreateMainLight()
        {
            var mainLightGO = new GameObject("MainWorkLight");
            mainLightGO.transform.SetParent(transform);

            // Position above the center of the dig area
            Vector3 lightPos = new Vector3(0, basementFloorY + mainLightHeightOffset, 0);
            mainLightGO.transform.localPosition = lightPos;
            mainLightGO.transform.rotation = Quaternion.Euler(90, 0, 0); // Point straight down

            mainSpotLight = mainLightGO.AddComponent<Light>();
            mainSpotLight.type = LightType.Spot;
            mainSpotLight.intensity = mainLightIntensity;
            mainSpotLight.range = mainLightRange;
            mainSpotLight.spotAngle = mainLightSpotAngle;
            mainSpotLight.innerSpotAngle = mainLightSpotAngle * 0.6f;
            mainSpotLight.color = mainLightColor;
            mainSpotLight.shadows = LightShadows.Soft;
            mainSpotLight.shadowStrength = 0.7f;
            mainSpotLight.shadowBias = 0.02f;
            mainSpotLight.shadowNormalBias = 0.4f;

            if (enableDebugLogs)
                Debug.Log($"[DiggingLightingRig] Created main work light at {lightPos}");
        }

        private void CreateRimLights()
        {
            rimLights = new Light[4];
            float rimOffset = 5f; // Distance from center
            float rimHeight = basementFloorY + 0.5f;
            float rimAngle = 65f; // Angle to skim the surface

            Vector3[] positions = new Vector3[]
            {
                new Vector3(rimOffset, rimHeight, 0),   // East
                new Vector3(-rimOffset, rimHeight, 0),  // West
                new Vector3(0, rimHeight, rimOffset),   // North
                new Vector3(0, rimHeight, -rimOffset),  // South
            };

            Quaternion[] rotations = new Quaternion[]
            {
                Quaternion.Euler(rimAngle, -90, 0),  // East -> points West-Down
                Quaternion.Euler(rimAngle, 90, 0),   // West -> points East-Down
                Quaternion.Euler(rimAngle, 180, 0),  // North -> points South-Down
                Quaternion.Euler(rimAngle, 0, 0),    // South -> points North-Down
            };

            string[] names = { "RimLight_East", "RimLight_West", "RimLight_North", "RimLight_South" };

            for (int i = 0; i < 4; i++)
            {
                var rimGO = new GameObject(names[i]);
                rimGO.transform.SetParent(transform);
                rimGO.transform.localPosition = positions[i];
                rimGO.transform.rotation = rotations[i];

                var light = rimGO.AddComponent<Light>();
                light.type = LightType.Spot;
                light.intensity = rimLightIntensity;
                light.range = rimLightRange;
                light.spotAngle = rimLightSpotAngle;
                light.innerSpotAngle = rimLightSpotAngle * 0.5f;
                light.color = rimLightColor;
                light.shadows = LightShadows.None; // Save performance

                rimLights[i] = light;
            }

            if (enableDebugLogs)
                Debug.Log("[DiggingLightingRig] Created 4 rim lights");
        }

        private void CreateFillLight()
        {
            var fillGO = new GameObject("FillLight");
            fillGO.transform.SetParent(transform);

            // Position inside the shaft, pointing up slightly
            Vector3 fillPos = new Vector3(0, basementFloorY - 8f, 0);
            fillGO.transform.localPosition = fillPos;

            fillLight = fillGO.AddComponent<Light>();
            fillLight.type = LightType.Point;
            fillLight.intensity = fillLightIntensity;
            fillLight.range = fillLightRange;
            fillLight.color = fillLightColor;
            fillLight.shadows = LightShadows.None; // Save performance

            if (enableDebugLogs)
                Debug.Log($"[DiggingLightingRig] Created fill light at {fillPos}");
        }

        #endregion

        #region Public API

        /// <summary>
        /// Manually set the basement floor Y position.
        /// </summary>
        public void SetBasementFloorY(float y)
        {
            basementFloorY = y;

            // Reposition lights
            if (mainSpotLight != null)
            {
                var pos = mainSpotLight.transform.localPosition;
                pos.y = basementFloorY + mainLightHeightOffset;
                mainSpotLight.transform.localPosition = pos;
            }
        }

        /// <summary>
        /// Get the current depth-based intensity multiplier (0 to 1).
        /// </summary>
        public float CurrentIntensityMultiplier => _currentIntensityMultiplier;

        /// <summary>
        /// Get the current tracked depth.
        /// </summary>
        public float CurrentDepth => _currentDepth;

        #endregion

        #region Editor Helpers

        [ContextMenu("Recreate All Lights")]
        private void RecreateAllLights()
        {
            // Destroy existing
            if (mainSpotLight != null) DestroyImmediate(mainSpotLight.gameObject);
            if (rimLights != null)
            {
                foreach (var light in rimLights)
                    if (light != null) DestroyImmediate(light.gameObject);
            }
            if (fillLight != null) DestroyImmediate(fillLight.gameObject);

            mainSpotLight = null;
            rimLights = null;
            fillLight = null;

            // Recreate
            CreateMainLight();
            CreateRimLights();
            CreateFillLight();
        }

        private void OnDrawGizmos()
        {
            if (!showGizmos) return;

            // Draw basement floor plane
            Gizmos.color = new Color(0.5f, 0.3f, 0.1f, 0.3f);
            Gizmos.DrawCube(new Vector3(transform.position.x, basementFloorY, transform.position.z),
                           new Vector3(12, 0.1f, 12));

            // Draw full brightness zone
            Gizmos.color = new Color(1f, 1f, 0f, 0.15f);
            Gizmos.DrawCube(new Vector3(transform.position.x, basementFloorY - fullBrightnessDepth/2, transform.position.z),
                           new Vector3(10, fullBrightnessDepth, 10));

            // Draw dim zone
            Gizmos.color = new Color(0.3f, 0f, 0f, 0.08f);
            Gizmos.DrawCube(new Vector3(transform.position.x, basementFloorY - minBrightnessDepth/2, transform.position.z),
                           new Vector3(8, minBrightnessDepth, 8));

            // Draw light positions
            if (mainSpotLight == null)
            {
                Gizmos.color = Color.yellow;
                Vector3 mainPos = transform.position + new Vector3(0, basementFloorY + mainLightHeightOffset, 0);
                Gizmos.DrawWireSphere(mainPos, 0.3f);
                Gizmos.DrawLine(mainPos, mainPos + Vector3.down * 3f);
            }
        }

        #endregion
    }
}
