using System;
using UnityEngine;
using BeneathTheFloor.ResourceSystem;

namespace BeneathTheFloor.Tools
{
    /// <summary>
    /// Manages the radar/EMF tool held in the player's left hand.
    /// Used to detect hidden nodes and guide the player to objectives.
    /// </summary>
    public class RadarTool : MonoBehaviour
    {
        public static RadarTool Instance { get; private set; }

        /// <summary>
        /// Fired when radar has been activated for the required duration.
        /// </summary>
        public static event Action OnRadarActivated;

        [Header("Prefab")]
        [SerializeField] private string prefabPath = "Assets/lathiel/EMF/Prefabs/EMF_Modern_T2.prefab";

        [Header("Position (Left Hand)")]
        [SerializeField] private Vector3 localPosition = new Vector3(-0.35f, -0.25f, 0.4f);
        [SerializeField] private Vector3 localRotation = new Vector3(10f, 15f, -5f);
        [SerializeField] private float scale = 0.8f;

        [Header("Settings")]
        [SerializeField] private KeyCode holdKey = KeyCode.Q;
        [SerializeField] private bool startVisible = false;

        [Header("Activation Tracking")]
        [SerializeField] private float requiredActivationDuration = 1f;

        [Header("Needle Detection")]
        [Tooltip("How far the radar can detect resources")]
        [SerializeField] private float detectionRange = 25f;
        [Tooltip("Angle in degrees where needle shows maximum (5). 0 = perfect aim required, 30 = more forgiving")]
        [SerializeField] private float perfectAimAngle = 15f;
        [Tooltip("Angle in degrees where needle shows minimum (1). Beyond this = no detection")]
        [SerializeField] private float maxDetectionAngle = 90f;
        [Tooltip("How fast the needle moves to target position")]
        [SerializeField] private float needleSmoothSpeed = 8f;
        [Tooltip("Name of the needle object in the radar hierarchy")]
        [SerializeField] private string needleObjectName = "Needle";

        [Header("Needle Rotation Calibration")]
        [Tooltip("Which axis to rotate: 0=X, 1=Y, 2=Z")]
        [SerializeField] private int needleRotationAxisIndex = 2;
        [Tooltip("Needle rotation for level 1 (no detection)")]
        [SerializeField] private float needleMinRotation = 0f;
        [Tooltip("Needle rotation for level 5 (perfect aim)")]
        [SerializeField] private float needleMaxRotation = 180f;
        [Tooltip("Show debug info in console")]
        [SerializeField] private bool debugNeedle = false;

        [Header("Screen Light (Visible in Dark)")]
        [Tooltip("Add a subtle light to simulate a backlit screen - makes radar visible in dark areas")]
        [SerializeField] private bool enableScreenLight = true;
        [Tooltip("Color of the screen backlight")]
        [SerializeField] private Color screenLightColor = new Color(0.9f, 0.95f, 1f, 1f); // Slight blue-white
        [Tooltip("Intensity of the screen light")]
        [SerializeField] private float screenLightIntensity = 0.25f;
        [Tooltip("Range of the screen light (keep small so it only lights the radar)")]
        [SerializeField] private float screenLightRange = 0.8f;

        [Header("Hologram (3D Target Indicator)")]
        [Tooltip("3D prefab to show as hologram (e.g., treasure chest)")]
        [SerializeField] private GameObject hologramPrefab;
        [Tooltip("Size/scale of the hologram")]
        [SerializeField] private float hologramScale = 0.08f;
        [Tooltip("Position offset from radar (local space) - projects from edge")]
        [SerializeField] private Vector3 hologramOffset = new Vector3(0f, 0.15f, 0.05f);
        [Tooltip("Base rotation of the hologram (local euler angles)")]
        [SerializeField] private Vector3 hologramBaseRotation = new Vector3(0f, 0f, 0f);
        [Tooltip("Rotation speed in degrees per second")]
        [SerializeField] private float hologramSpinSpeed = 45f;
        [Tooltip("Vertical bob amplitude (0 to disable)")]
        [SerializeField] private float hologramBobAmplitude = 0.003f;
        [Tooltip("Vertical bob speed")]
        [SerializeField] private float hologramBobSpeed = 2f;
        [Tooltip("Hologram color/tint")]
        [SerializeField] private Color hologramColor = new Color(0.4f, 1f, 1f, 0.85f); // Bright cyan hologram
        [Tooltip("Hologram emission intensity")]
        [SerializeField] private float hologramEmission = 3f;

        [Header("Always On Top Rendering")]
        [Tooltip("Render radar and hologram on top of everything (not hidden by walls)")]
        [SerializeField] private bool renderOnTop = true;
        [Tooltip("Render queue value for on-top rendering (higher = rendered later)")]
        [SerializeField] private int onTopRenderQueue = 4000;

        [Header("References")]
        [SerializeField] private GameObject radarInstance;

        // State
        private bool isVisible = false;
        private bool isUnlocked = false;
        private Camera playerCamera;
        private float activationTimer = 0f;
        private bool hasTriggeredActivation = false;

        // Needle state
        private Transform needleTransform;
        private float currentNeedleValue = 0f; // 0-1 normalized value
        private float targetNeedleValue = 0f;
        private Vector3 needleStartEuler;

        // Screen light
        private Light screenLight;

        // Hologram
        private GameObject hologramInstance;
        private Transform hologramTransform;
        private Transform hologramModelTransform; // The actual 3D model that spins
        private float hologramSpinAngle = 0f;
        private Vector3 hologramBasePosition;

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
            playerCamera = Camera.main;

            if (playerCamera == null)
            {
                Debug.LogError("[RadarTool] No main camera found!");
                return;
            }

            // Try to find existing radar instance under camera
            if (radarInstance == null)
            {
                radarInstance = FindRadarUnderCamera();
            }

            // If still null, instantiate from prefab
            if (radarInstance == null)
            {
                InstantiateRadar();
            }

            // Always start locked - radar unlocks via mission 8 reward or RadarPickup
            isUnlocked = false;

            if (radarInstance != null)
            {
                isVisible = startVisible && isUnlocked;
                radarInstance.SetActive(isVisible);

                // Find needle if radar exists but needle not found yet
                if (needleTransform == null)
                {
                    FindNeedle();
                }

                // Ensure RadarPointer component exists (drives needle + audio detection)
                EnsureRadarPointer();

                // Setup self-illumination (for existing radar instances)
                SetupSelfIllumination();

                // Setup 3D hologram (treasure chest indicator)
                SetupHologram();

                // Apply "always on top" rendering
                if (renderOnTop)
                    ApplyAlwaysOnTopRendering();
            }
        }

        private void Update()
        {
            // Always animate hologram if it exists (even before radar is unlocked)
            if (hologramTransform != null && radarInstance != null && radarInstance.activeInHierarchy)
                UpdateHologramAnimation();

            // Only respond to input if radar is unlocked
            if (!isUnlocked) return;

            // Show radar while holding key, hide when released
            if (Input.GetKeyDown(holdKey))
            {
                ShowRadar();
            }
            else if (Input.GetKeyUp(holdKey))
            {
                HideRadar();
                activationTimer = 0f; // Reset timer when released
            }

            // Track activation duration while visible
            if (isVisible && !hasTriggeredActivation)
            {
                activationTimer += Time.deltaTime;

                if (activationTimer >= requiredActivationDuration)
                {
                    hasTriggeredActivation = true;
                    OnRadarActivated?.Invoke();
                }
            }

            // Note: Needle is now controlled by RadarPointer component with useAimAccuracyMode enabled
            // The detection logic has been moved there to keep audio and wobble features
        }

        /// <summary>
        /// Update the needle based on how well the player is aimed at resources.
        /// </summary>
        private void UpdateNeedleDetection()
        {
            if (needleTransform == null || playerCamera == null) return;

            // Get player position and look direction
            Vector3 playerPos = playerCamera.transform.position;
            Vector3 lookDirection = playerCamera.transform.forward;

            // Get nearest resource node
            HiddenNodeManager nodeManager = HiddenNodeManager.Instance;
            float angle = maxDetectionAngle; // Default to max angle (worst case)

            if (nodeManager != null)
            {
                HiddenNode nearestNode = nodeManager.GetNearestUnbrokenNode(playerPos, detectionRange);

                if (nearestNode != null)
                {
                    // Calculate direction to nearest resource
                    Vector3 directionToNode = (nearestNode.WorldPosition - playerPos).normalized;

                    // Calculate angle between look direction and direction to resource
                    angle = Vector3.Angle(lookDirection, directionToNode);
                }
            }

            // Map angle to 0-1 value (0 = bad aim/level 1, 1 = good aim/level 5)
            // perfectAimAngle or less = 1.0 (level 5)
            // maxDetectionAngle or more = 0.0 (level 1)
            if (angle <= perfectAimAngle)
            {
                targetNeedleValue = 1f; // Perfect aim
            }
            else if (angle >= maxDetectionAngle)
            {
                targetNeedleValue = 0f; // Looking away
            }
            else
            {
                // Linear interpolation between perfectAimAngle and maxDetectionAngle
                targetNeedleValue = 1f - ((angle - perfectAimAngle) / (maxDetectionAngle - perfectAimAngle));
            }

            // Smoothly move needle to target
            currentNeedleValue = Mathf.Lerp(currentNeedleValue, targetNeedleValue, Time.deltaTime * needleSmoothSpeed);

            // Apply rotation to needle
            ApplyNeedleRotation(currentNeedleValue);

            // Debug output
            if (debugNeedle && Time.frameCount % 30 == 0) // Every 30 frames
            {
                int level = Mathf.RoundToInt(1 + currentNeedleValue * 4);
                Debug.Log($"[RadarTool] Angle: {angle:F1}°, Value: {currentNeedleValue:F2}, Level: {level}");
            }
        }

        /// <summary>
        /// Apply rotation to the needle transform based on 0-1 value.
        /// </summary>
        private void ApplyNeedleRotation(float normalizedValue)
        {
            if (needleTransform == null) return;

            // Calculate the rotation for this value
            float rotation = Mathf.Lerp(needleMinRotation, needleMaxRotation, normalizedValue);

            // Apply rotation on the selected axis
            Vector3 newEuler = needleStartEuler;
            newEuler[needleRotationAxisIndex] = rotation;
            needleTransform.localEulerAngles = newEuler;
        }

        /// <summary>
        /// Get the current detection level (1-5) for UI display.
        /// </summary>
        public int GetDetectionLevel()
        {
            return Mathf.RoundToInt(1 + currentNeedleValue * 4); // 1 to 5
        }

        private void OnDestroy()
        {
            if (Instance == this)
                Instance = null;
        }

        /// <summary>
        /// Ensure the radar instance has a RadarPointer component at runtime.
        /// This is the component that actually drives needle detection and audio beeps.
        /// </summary>
        private void EnsureRadarPointer()
        {
            if (radarInstance == null) return;

            var radarPointer = radarInstance.GetComponent<RadarPointer>();
            if (radarPointer == null)
            {
                radarPointer = radarInstance.AddComponent<RadarPointer>();
            }
        }

        private GameObject FindRadarUnderCamera()
        {
            if (playerCamera == null) return null;

            foreach (Transform child in playerCamera.transform)
            {
                string nameLower = child.name.ToLower();
                if (nameLower.Contains("emf") || nameLower.Contains("radar"))
                {
                    return child.gameObject;
                }
            }
            return null;
        }

        private void InstantiateRadar()
        {
            if (playerCamera == null) return;

#if UNITY_EDITOR
            // Load prefab in editor
            GameObject prefab = UnityEditor.AssetDatabase.LoadAssetAtPath<GameObject>(prefabPath);
            if (prefab == null)
            {
                Debug.LogError($"[RadarTool] Could not load prefab at: {prefabPath}");
                return;
            }

            radarInstance = Instantiate(prefab, playerCamera.transform);
#else
            // In build, try Resources folder
            GameObject prefab = Resources.Load<GameObject>("EMF_Modern_T2");
            if (prefab == null)
            {
                Debug.LogError("[RadarTool] Could not load radar prefab from Resources!");
                return;
            }
            radarInstance = Instantiate(prefab, playerCamera.transform);
#endif

            if (radarInstance != null)
            {
                radarInstance.name = "RadarTool_EMF";

                // Position for left hand
                radarInstance.transform.localPosition = localPosition;
                radarInstance.transform.localRotation = Quaternion.Euler(localRotation);
                radarInstance.transform.localScale = Vector3.one * scale;

                // Disable shadows on all renderers
                foreach (var renderer in radarInstance.GetComponentsInChildren<Renderer>())
                {
                    renderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
                }

                // Find and setup needle
                FindNeedle();

                // Setup self-illumination (always visible in dark)
                SetupSelfIllumination();

                // Setup 3D hologram (treasure chest indicator)
                SetupHologram();

                // Apply "always on top" rendering
                if (renderOnTop)
                    ApplyAlwaysOnTopRendering();
            }
        }

        /// <summary>
        /// Setup a subtle screen light so the radar is visible in dark areas.
        /// Uses a point light positioned in front of the screen to simulate a backlit display.
        /// </summary>
        private void SetupSelfIllumination()
        {
            if (radarInstance == null) return;

            // FIRST: Reset any emission on materials (clean up from previous versions)
            // This fixes the "white/bright" appearance issue
            ResetMaterialEmission();

            // Only add screen light if enabled
            if (!enableScreenLight) return;

            // Create a subtle point light in front of the radar screen
            // This simulates a backlit LCD display
            GameObject lightObj = new GameObject("RadarScreenLight");
            lightObj.transform.SetParent(radarInstance.transform);

            // Position the light slightly in front of the screen area
            // Adjust based on radar model orientation
            lightObj.transform.localPosition = new Vector3(0f, 0.05f, 0.15f);
            lightObj.transform.localRotation = Quaternion.identity;

            screenLight = lightObj.AddComponent<Light>();
            screenLight.type = LightType.Point;
            screenLight.color = screenLightColor;
            screenLight.intensity = screenLightIntensity;
            screenLight.range = screenLightRange;
            screenLight.shadows = LightShadows.None; // No shadows for performance

        }

        /// <summary>
        /// Setup the 3D hologram that projects from the radar edge.
        /// Shows a spinning treasure chest to indicate what the radar detects.
        /// </summary>
        private void SetupHologram()
        {
            if (radarInstance == null)
            {
                Debug.LogWarning("[RadarTool] SetupHologram: radarInstance is null");
                return;
            }
            if (hologramInstance != null)
            {
                Debug.Log("[RadarTool] SetupHologram: hologramInstance already exists");
                return; // Already created
            }

            Debug.Log($"[RadarTool] SetupHologram: Creating hologram. Prefab assigned: {hologramPrefab != null}");

            // Check if hologram already exists in hierarchy (manual setup)
            Transform existingHologram = radarInstance.transform.Find("RadarHologram");
            if (existingHologram != null)
            {
                Debug.Log("[RadarTool] SetupHologram: Found existing RadarHologram in hierarchy");
                hologramInstance = existingHologram.gameObject;
                hologramTransform = existingHologram;
                hologramBasePosition = hologramTransform.localPosition;
                // Find the model child for spinning
                Transform modelChild = existingHologram.Find("HologramModel");
                if (modelChild != null)
                    hologramModelTransform = modelChild;
                else if (existingHologram.childCount > 0)
                    hologramModelTransform = existingHologram.GetChild(0); // Use first child as model
                ApplyHologramMaterial(hologramInstance);
                return;
            }

            // Create hologram container
            hologramInstance = new GameObject("RadarHologram");
            hologramInstance.transform.SetParent(radarInstance.transform);
            hologramTransform = hologramInstance.transform;

            // Position projecting from radar edge
            hologramTransform.localPosition = hologramOffset;
            hologramTransform.localRotation = Quaternion.Euler(hologramBaseRotation);
            hologramTransform.localScale = Vector3.one * hologramScale;
            hologramBasePosition = hologramOffset;

            Debug.Log($"[RadarTool] SetupHologram: Created container at localPos={hologramOffset}, scale={hologramScale}");

            // Instantiate the 3D prefab if assigned
            if (hologramPrefab != null)
            {
                GameObject model = Instantiate(hologramPrefab, hologramTransform);
                model.name = "HologramModel";
                model.transform.localPosition = Vector3.zero;
                model.transform.localRotation = Quaternion.identity;
                model.transform.localScale = Vector3.one;
                hologramModelTransform = model.transform; // Store reference to spin this

                Debug.Log($"[RadarTool] SetupHologram: Instantiated prefab '{hologramPrefab.name}'");

                // Apply hologram material effect
                ApplyHologramMaterial(model);
            }
            else
            {
                Debug.Log("[RadarTool] SetupHologram: No prefab assigned, creating fallback shape");
                // Create fallback primitive if no prefab assigned
                CreateFallbackHologramShape();
            }
        }

        /// <summary>
        /// Create a simple fallback shape when no hologram prefab is assigned.
        /// </summary>
        private void CreateFallbackHologramShape()
        {
            if (hologramTransform == null) return;

            // Create a container for the fallback shape so it can spin as one unit
            GameObject modelContainer = new GameObject("HologramModel");
            modelContainer.transform.SetParent(hologramTransform);
            modelContainer.transform.localPosition = Vector3.zero;
            modelContainer.transform.localRotation = Quaternion.identity;
            modelContainer.transform.localScale = Vector3.one;
            hologramModelTransform = modelContainer.transform;

            // Create a simple chest-like shape from primitives
            // Body (cube)
            GameObject body = GameObject.CreatePrimitive(PrimitiveType.Cube);
            body.name = "ChestBody";
            body.transform.SetParent(modelContainer.transform);
            body.transform.localPosition = Vector3.zero;
            body.transform.localScale = new Vector3(1f, 0.6f, 0.7f);
            DestroyImmediate(body.GetComponent<Collider>());

            // Lid (slightly smaller cube on top)
            GameObject lid = GameObject.CreatePrimitive(PrimitiveType.Cube);
            lid.name = "ChestLid";
            lid.transform.SetParent(modelContainer.transform);
            lid.transform.localPosition = new Vector3(0f, 0.4f, 0f);
            lid.transform.localScale = new Vector3(1.05f, 0.3f, 0.75f);
            DestroyImmediate(lid.GetComponent<Collider>());

            ApplyHologramMaterial(body);
            ApplyHologramMaterial(lid);
        }

        /// <summary>
        /// Apply hologram material effect to all renderers in the object.
        /// Creates a glowing, transparent, sci-fi hologram look.
        /// </summary>
        private void ApplyHologramMaterial(GameObject obj)
        {
            if (obj == null) return;

            foreach (var renderer in obj.GetComponentsInChildren<Renderer>())
            {
                // Find a suitable shader - try URP first, then Standard
                Shader shader = Shader.Find("Universal Render Pipeline/Lit");
                if (shader == null)
                    shader = Shader.Find("Standard");
                if (shader == null)
                {
                    Debug.LogWarning("[RadarTool] Could not find URP/Lit or Standard shader for hologram!");
                    continue;
                }

                // Create hologram material
                Material holoMat = new Material(shader);

                // Set to transparent
                holoMat.SetFloat("_Surface", 1); // Transparent
                holoMat.SetFloat("_Blend", 0); // Alpha blend
                holoMat.SetFloat("_AlphaClip", 0);
                holoMat.SetFloat("_SrcBlend", (float)UnityEngine.Rendering.BlendMode.SrcAlpha);
                holoMat.SetFloat("_DstBlend", (float)UnityEngine.Rendering.BlendMode.OneMinusSrcAlpha);
                holoMat.SetFloat("_ZWrite", 0);
                holoMat.EnableKeyword("_SURFACE_TYPE_TRANSPARENT");
                holoMat.renderQueue = 3000;

                // Base color with transparency
                holoMat.SetColor("_BaseColor", hologramColor);

                // Emission for glow effect
                holoMat.EnableKeyword("_EMISSION");
                Color emissionColor = new Color(
                    hologramColor.r * hologramEmission,
                    hologramColor.g * hologramEmission,
                    hologramColor.b * hologramEmission
                );
                holoMat.SetColor("_EmissionColor", emissionColor);

                // Apply "always on top" if enabled
                if (renderOnTop)
                {
                    holoMat.SetInt("_ZTest", (int)UnityEngine.Rendering.CompareFunction.Always);
                    holoMat.renderQueue = onTopRenderQueue;
                }

                // Disable shadows
                renderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
                renderer.receiveShadows = false;

                // Apply material to all slots
                Material[] mats = new Material[renderer.materials.Length];
                for (int i = 0; i < mats.Length; i++)
                    mats[i] = holoMat;
                renderer.materials = mats;

                Debug.Log($"[RadarTool] Applied hologram material to {renderer.gameObject.name}");
            }
        }

        /// <summary>
        /// Update hologram animation (spinning + vertical bob).
        /// Called every frame when radar is visible.
        /// </summary>
        private void UpdateHologramAnimation()
        {
            if (hologramTransform == null) return;

            // Spin
            hologramSpinAngle += hologramSpinSpeed * Time.deltaTime;
            if (hologramSpinAngle >= 360f) hologramSpinAngle -= 360f;

            // Bob (gentle vertical oscillation)
            float bobOffset = 0f;
            if (hologramBobAmplitude > 0f)
            {
                bobOffset = Mathf.Sin(Time.time * hologramBobSpeed) * hologramBobAmplitude;
            }

            // Apply position and base rotation to container
            Vector3 pos = hologramBasePosition;
            pos.y += bobOffset;
            hologramTransform.localPosition = pos;
            hologramTransform.localRotation = Quaternion.Euler(hologramBaseRotation);

            // Apply spin rotation to the model itself (Z-axis only)
            if (hologramModelTransform != null)
            {
                hologramModelTransform.localRotation = Quaternion.Euler(0f, 0f, hologramSpinAngle);
            }
            else
            {
                // Try to find model if not set
                if (hologramTransform != null && hologramTransform.childCount > 0)
                {
                    hologramModelTransform = hologramTransform.GetChild(0);
                    Debug.Log($"[RadarTool] Found hologram model: {hologramModelTransform.name}");
                }
            }
        }

        /// <summary>
        /// Apply "always on top" rendering to the radar and all its children.
        /// This ensures the radar is never hidden by walls or terrain.
        /// </summary>
        private void ApplyAlwaysOnTopRendering()
        {
            if (radarInstance == null) return;

            foreach (var renderer in radarInstance.GetComponentsInChildren<Renderer>())
            {
                foreach (var mat in renderer.materials)
                {
                    // Set ZTest to Always (renders on top of everything)
                    if (mat.HasProperty("_ZTest"))
                    {
                        mat.SetInt("_ZTest", (int)UnityEngine.Rendering.CompareFunction.Always);
                    }

                    // Increase render queue to render after other objects
                    mat.renderQueue = onTopRenderQueue;
                }
            }
        }

        /// <summary>
        /// Reset any emission on radar materials to fix white/bright appearance.
        /// </summary>
        private void ResetMaterialEmission()
        {
            if (radarInstance == null) return;

            foreach (var renderer in radarInstance.GetComponentsInChildren<Renderer>())
            {
                foreach (var mat in renderer.materials)
                {
                    // Disable emission keyword
                    mat.DisableKeyword("_EMISSION");

                    // Reset emission colors to black (no emission)
                    if (mat.HasProperty("_EmissionColor"))
                    {
                        mat.SetColor("_EmissionColor", Color.black);
                    }
                    if (mat.HasProperty("_EmissiveColor"))
                    {
                        mat.SetColor("_EmissiveColor", Color.black);
                    }

                    // Reset render queue to default
                    mat.renderQueue = -1; // -1 = use shader's default

                    // Reset ZTest if it was modified
                    if (mat.HasProperty("_ZTest"))
                    {
                        mat.SetFloat("_ZTest", 4f); // 4 = LessEqual (default)
                    }
                }
            }
        }

        /// <summary>
        /// Find the needle transform in the radar hierarchy.
        /// </summary>
        private void FindNeedle()
        {
            if (radarInstance == null) return;

            // Try to find needle by name
            needleTransform = radarInstance.transform.Find(needleObjectName);

            // If not found, search recursively
            if (needleTransform == null)
            {
                needleTransform = FindChildRecursive(radarInstance.transform, needleObjectName);
            }

            // Try common needle names if still not found
            if (needleTransform == null)
            {
                string[] commonNames = { "needle", "Needle", "pointer", "Pointer", "indicator", "Indicator", "hand", "Hand" };
                foreach (string name in commonNames)
                {
                    needleTransform = FindChildRecursive(radarInstance.transform, name);
                    if (needleTransform != null) break;
                }
            }

            if (needleTransform != null)
            {
                needleStartEuler = needleTransform.localEulerAngles;
                currentNeedleValue = 0f; // Start at minimum (level 1)
                if (debugNeedle) Debug.Log($"[RadarTool] Found needle: {needleTransform.name}, start euler: {needleStartEuler}");
            }
            else
            {
                if (debugNeedle)
                {
                    Debug.LogWarning("[RadarTool] Could not find needle transform in radar model! Listing all children:");
                    ListAllChildren(radarInstance.transform, "");
                }
            }
        }

        private void ListAllChildren(Transform parent, string indent)
        {
            foreach (Transform child in parent)
            {
                Debug.Log($"{indent}- {child.name}");
                ListAllChildren(child, indent + "  ");
            }
        }

        /// <summary>
        /// Recursively search for a child with a name containing the search string.
        /// </summary>
        private Transform FindChildRecursive(Transform parent, string nameContains)
        {
            foreach (Transform child in parent)
            {
                if (child.name.ToLower().Contains(nameContains.ToLower()))
                {
                    return child;
                }

                Transform found = FindChildRecursive(child, nameContains);
                if (found != null) return found;
            }
            return null;
        }

        /// <summary>
        /// Toggle radar visibility on/off.
        /// </summary>
        public void ToggleRadar()
        {
            if (radarInstance == null) return;

            isVisible = !isVisible;
            radarInstance.SetActive(isVisible);

        }

        /// <summary>
        /// Show the radar.
        /// </summary>
        public void ShowRadar()
        {
            if (radarInstance == null) return;

            isVisible = true;
            radarInstance.SetActive(true);
        }

        /// <summary>
        /// Hide the radar.
        /// </summary>
        public void HideRadar()
        {
            if (radarInstance == null) return;

            isVisible = false;
            radarInstance.SetActive(false);
        }

        /// <summary>
        /// Check if radar is currently visible.
        /// </summary>
        public bool IsVisible => isVisible;

        /// <summary>
        /// Check if radar is unlocked (player has acquired it).
        /// </summary>
        public bool IsUnlocked => isUnlocked;

        /// <summary>
        /// Get the radar GameObject instance.
        /// </summary>
        public GameObject RadarInstance => radarInstance;

        /// <summary>
        /// Get the hologram GameObject instance (for manual hierarchy adjustments).
        /// </summary>
        public GameObject HologramInstance => hologramInstance;

        /// <summary>
        /// Get/set the hologram prefab at runtime.
        /// </summary>
        public GameObject HologramPrefab
        {
            get => hologramPrefab;
            set
            {
                hologramPrefab = value;
                // Rebuild hologram if it already exists
                if (hologramInstance != null)
                {
                    Destroy(hologramInstance);
                    hologramInstance = null;
                    hologramTransform = null;
                    SetupHologram();
                    if (renderOnTop)
                        ApplyAlwaysOnTopRendering();
                }
            }
        }

        /// <summary>
        /// Refresh the "always on top" rendering (call after making material changes).
        /// </summary>
        public void RefreshOnTopRendering()
        {
            if (renderOnTop)
                ApplyAlwaysOnTopRendering();
        }

        /// <summary>
        /// Unlock the radar so the player can use it.
        /// Call this when the player acquires the radar.
        /// </summary>
        public void UnlockRadar()
        {
            isUnlocked = true;
            Debug.Log("[RadarTool] Radar unlocked! Press Q to use.");
        }

        /// <summary>
        /// Lock the radar so the player can't use it.
        /// </summary>
        public void LockRadar()
        {
            isUnlocked = false;
            HideRadar();
        }

        /// <summary>
        /// Reset the activation tracking (allows event to fire again).
        /// </summary>
        public void ResetActivationTracking()
        {
            hasTriggeredActivation = false;
            activationTimer = 0f;
        }

        /// <summary>
        /// Manually set position and rotation for fine-tuning.
        /// </summary>
        public void SetTransform(Vector3 position, Vector3 rotation, float newScale)
        {
            localPosition = position;
            localRotation = rotation;
            scale = newScale;

            if (radarInstance != null)
            {
                radarInstance.transform.localPosition = localPosition;
                radarInstance.transform.localRotation = Quaternion.Euler(localRotation);
                radarInstance.transform.localScale = Vector3.one * scale;
            }
        }
    }
}
