using UnityEngine;
using UnityEngine.Rendering;
using System.Collections.Generic;

namespace BeneathTheFloor.Lighting
{
    /// <summary>
    /// Manages basement lighting to prevent sunlight leaks and create physically plausible interior lighting.
    ///
    /// ROOT CAUSES ADDRESSED:
    /// 1. Directional Light (sun) affects ALL layers including basement - Fixed via culling mask
    /// 2. Ambient Mode is Skybox - brings outdoor light indoors
    /// 3. Reflection Mode is Skybox - outdoor reflections in basement
    /// 4. No layer separation between outdoor and indoor areas
    /// </summary>
    public class BasementLightingManager : MonoBehaviour
    {
        [Header("Layer Configuration")]
        [Tooltip("Layer name for basement geometry (walls, ceiling, floor)")]
        [SerializeField] private string basementLayerName = "Basement";

        [Tooltip("Layer index for basement (auto-detected from name)")]
        [SerializeField] private int basementLayerIndex = 9;

        [Header("Directional Light (Sun) Settings")]
        [Tooltip("Reference to the main directional light (sun). Auto-found if null.")]
        [SerializeField] private Light directionalLight;

        [Tooltip("If true, completely exclude basement layer from directional light")]
        [SerializeField] private bool excludeBasementFromSun = true;

        [Tooltip("Shadow distance for directional light (lower = better indoor shadows)")]
        [SerializeField] private float shadowDistance = 50f;

        [Header("Ambient Lighting Override")]
        [Tooltip("Override ambient intensity for basement (0 = completely dark ambient)")]
        [SerializeField] private bool overrideAmbientForBasement = true;

        [Tooltip("Ambient intensity when in basement (0-1)")]
        [SerializeField] private float basementAmbientIntensity = 0.15f;

        [Tooltip("Ambient color for basement (dark, earthy tone)")]
        [SerializeField] private Color basementAmbientColor = new Color(0.08f, 0.06f, 0.05f);

        [Header("Basement Detection")]
        [Tooltip("Y threshold - below this Y position is considered 'basement'")]
        [SerializeField] private float basementYThreshold = 1f;

        [Tooltip("Hysteresis buffer - must go this far past threshold to trigger change (prevents jump flickering)")]
        [SerializeField] private float hysteresisBuffer = 5f;

        [Tooltip("Track player position to switch ambient")]
        [SerializeField] private Transform playerTransform;

        [Header("Auto-Setup")]
        [Tooltip("Automatically assign basement layer to underground terrain objects")]
        [SerializeField] private bool autoAssignLayersOnStart = true;

        [Tooltip("Keywords to identify basement objects (case-insensitive)")]
        [SerializeField] private string[] basementObjectKeywords = new string[]
        {
            "Basement", "Underground", "Shaft", "Terrain", "Voxel", "Chunk", "Wall_", "Segment_"
        };

        [Header("Debug")]
        [SerializeField] private bool enableDebugLogs = false;
        [SerializeField] private bool showGizmos = true;

        // Runtime state
        private float _originalAmbientIntensity;
        private Color _originalAmbientColor;
        private AmbientMode _originalAmbientMode;
        private int _originalSunCullingMask;
        private bool _isInBasement;
        private int _basementLayerMask;

        // Singleton for easy access
        public static BasementLightingManager Instance { get; private set; }

        private void Awake()
        {
            Instance = this;

            // Get basement layer - try name lookup first, then use serialized index as fallback
            int foundLayer = LayerMask.NameToLayer(basementLayerName);
            if (foundLayer >= 0)
            {
                basementLayerIndex = foundLayer;
            }
            // else: Layer name not found - use serialized index (default 9)
            _basementLayerMask = 1 << basementLayerIndex;

            // Store original settings
            _originalAmbientIntensity = RenderSettings.ambientIntensity;
            _originalAmbientColor = RenderSettings.ambientSkyColor;
            _originalAmbientMode = RenderSettings.ambientMode;
        }

        private void Start()
        {
            // Find directional light if not assigned
            if (directionalLight == null)
            {
                directionalLight = FindDirectionalLight();
            }

            // Store original culling mask
            if (directionalLight != null)
            {
                _originalSunCullingMask = directionalLight.cullingMask;
            }

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

            // Apply fixes
            ApplyDirectionalLightFix();

            if (autoAssignLayersOnStart)
            {
                AssignBasementLayersAutomatically();
            }

            // Initialize basement state based on player position
            // If player starts below ground level, assume they're in basement
            if (playerTransform != null && playerTransform.position.y < 1f)
            {
                _isInBasement = true;
                // Apply basement ambient immediately
                RenderSettings.ambientMode = AmbientMode.Flat;
                RenderSettings.ambientIntensity = basementAmbientIntensity;
                RenderSettings.ambientSkyColor = basementAmbientColor;
                RenderSettings.ambientEquatorColor = basementAmbientColor;
                RenderSettings.ambientGroundColor = basementAmbientColor * 0.5f;
            }
        }

        private void Update()
        {
            if (overrideAmbientForBasement && playerTransform != null)
            {
                UpdateAmbientBasedOnPosition();
            }
        }

        private void OnDisable()
        {
            // Restore original settings
            RestoreOriginalSettings();
        }

        /// <summary>
        /// Find the main directional light in the scene.
        /// </summary>
        private Light FindDirectionalLight()
        {
            // First try to find by RenderSettings.sun
            if (RenderSettings.sun != null)
                return RenderSettings.sun;

            // Find all directional lights
            Light[] allLights = FindObjectsOfType<Light>();
            Light brightest = null;
            float maxIntensity = 0;

            foreach (var light in allLights)
            {
                if (light.type == LightType.Directional && light.intensity > maxIntensity)
                {
                    maxIntensity = light.intensity;
                    brightest = light;
                }
            }

            if (brightest != null && enableDebugLogs)
            {
                Debug.Log($"[BasementLighting] Found directional light: {brightest.name} (intensity: {brightest.intensity})");
            }

            return brightest;
        }

        /// <summary>
        /// Apply the fix to prevent directional light from affecting basement.
        /// </summary>
        [ContextMenu("Apply Directional Light Fix")]
        public void ApplyDirectionalLightFix()
        {
            if (directionalLight == null)
            {
                return;
            }

            if (excludeBasementFromSun)
            {
                // Remove basement layer from culling mask
                // Current mask is 4294967295 (all layers)
                // We want to exclude layer 9 (Basement)
                int newMask = directionalLight.cullingMask & ~_basementLayerMask;

                // Also exclude BasementFloor layer (10)
                int basementFloorLayer = LayerMask.NameToLayer("BasementFloor");
                if (basementFloorLayer >= 0)
                {
                    newMask = newMask & ~(1 << basementFloorLayer);
                }

                directionalLight.cullingMask = newMask;

            }

            // Set shadow distance
            QualitySettings.shadowDistance = shadowDistance;
        }

        /// <summary>
        /// Update ambient lighting based on player position.
        /// Uses hysteresis to prevent flickering when jumping.
        /// </summary>
        private void UpdateAmbientBasedOnPosition()
        {
            bool wasInBasement = _isInBasement;
            float playerY = playerTransform.position.y;

            // Use hysteresis: different thresholds for entering vs leaving
            // To enter basement: must go below threshold
            // To leave basement: must go above threshold + buffer
            if (_isInBasement)
            {
                // Currently in basement - only leave if significantly above threshold
                _isInBasement = playerY < (basementYThreshold + hysteresisBuffer);
            }
            else
            {
                // Currently outside - enter basement if below threshold
                _isInBasement = playerY < basementYThreshold;
            }

            if (_isInBasement != wasInBasement)
            {
                if (_isInBasement)
                {
                    // Entering basement - darken ambient
                    RenderSettings.ambientMode = AmbientMode.Flat;
                    RenderSettings.ambientIntensity = basementAmbientIntensity;
                    RenderSettings.ambientSkyColor = basementAmbientColor;
                    RenderSettings.ambientEquatorColor = basementAmbientColor;
                    RenderSettings.ambientGroundColor = basementAmbientColor * 0.5f;

                    if (enableDebugLogs)
                        Debug.Log($"[BasementLighting] Entered basement at Y={playerY:F1}");
                }
                else
                {
                    // Leaving basement - restore ambient
                    RenderSettings.ambientMode = _originalAmbientMode;
                    RenderSettings.ambientIntensity = _originalAmbientIntensity;
                    RenderSettings.ambientSkyColor = _originalAmbientColor;

                    if (enableDebugLogs)
                        Debug.Log($"[BasementLighting] Left basement at Y={playerY:F1}");
                }
            }
        }

        /// <summary>
        /// Automatically find and assign basement layer to relevant objects.
        /// </summary>
        [ContextMenu("Assign Basement Layers Automatically")]
        public void AssignBasementLayersAutomatically()
        {
            int assignedCount = 0;

            // Find all game objects
            GameObject[] allObjects = FindObjectsOfType<GameObject>();

            foreach (var obj in allObjects)
            {
                // Check if name matches any keyword
                bool isBasementObject = false;
                string objName = obj.name.ToLower();

                foreach (var keyword in basementObjectKeywords)
                {
                    if (objName.Contains(keyword.ToLower()))
                    {
                        isBasementObject = true;
                        break;
                    }
                }

                // Also check by Y position for terrain chunks
                if (!isBasementObject && obj.transform.position.y < basementYThreshold)
                {
                    var renderer = obj.GetComponent<MeshRenderer>();
                    if (renderer != null)
                    {
                        // Likely a terrain chunk or basement object
                        isBasementObject = true;
                    }
                }

                if (isBasementObject && obj.layer != basementLayerIndex)
                {
                    obj.layer = basementLayerIndex;
                    assignedCount++;

                    // Also assign to children
                    foreach (Transform child in obj.transform)
                    {
                        SetLayerRecursive(child.gameObject, basementLayerIndex);
                    }
                }
            }

            if (enableDebugLogs)
            {
                Debug.Log($"[BasementLighting] Assigned basement layer to {assignedCount} objects");
            }
        }

        private void SetLayerRecursive(GameObject obj, int layer)
        {
            obj.layer = layer;
            foreach (Transform child in obj.transform)
            {
                SetLayerRecursive(child.gameObject, layer);
            }
        }

        /// <summary>
        /// Restore original lighting settings.
        /// </summary>
        public void RestoreOriginalSettings()
        {
            if (directionalLight != null)
            {
                directionalLight.cullingMask = _originalSunCullingMask;
            }

            RenderSettings.ambientMode = _originalAmbientMode;
            RenderSettings.ambientIntensity = _originalAmbientIntensity;
            RenderSettings.ambientSkyColor = _originalAmbientColor;

            if (enableDebugLogs)
                Debug.Log("[BasementLighting] Original settings restored");
        }

        /// <summary>
        /// Manually set an object and its children to the basement layer.
        /// </summary>
        public void SetObjectToBasementLayer(GameObject obj)
        {
            SetLayerRecursive(obj, basementLayerIndex);
        }

        /// <summary>
        /// Get the basement layer index.
        /// </summary>
        public int BasementLayerIndex => basementLayerIndex;

        /// <summary>
        /// Get the basement layer mask.
        /// </summary>
        public int BasementLayerMask => _basementLayerMask;

        private void OnDrawGizmos()
        {
            if (!showGizmos) return;

            // Draw basement threshold plane
            Gizmos.color = new Color(0.5f, 0.2f, 0.1f, 0.3f);
            Gizmos.DrawCube(new Vector3(0, basementYThreshold, 0), new Vector3(30, 0.1f, 30));

            // Label
            #if UNITY_EDITOR
            UnityEditor.Handles.Label(new Vector3(0, basementYThreshold + 1, 0),
                $"Basement Threshold (Y={basementYThreshold})");
            #endif
        }
    }
}
