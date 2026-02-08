using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using BeneathTheFloor.ResourceSystem;
using BeneathTheFloor.Missions;

namespace BeneathTheFloor.Tools
{
    public enum RadarMode
    {
        TreasureChests = 1,
        CoreShard = 2
        // 3, 4 reserved for future modes
    }

    /// <summary>
    /// Manages the radar/EMF tool held in the player's left hand.
    /// Used to detect hidden nodes and guide the player to objectives.
    /// Supports multiple modes (Tab to cycle) for different target types.
    /// </summary>
    public class RadarTool : MonoBehaviour
    {
        public static RadarTool Instance { get; private set; }

        /// <summary>
        /// Fired when radar has been activated for the required duration.
        /// </summary>
        public static event Action OnRadarActivated;

        /// <summary>
        /// Fired when the radar mode changes. Passes the new mode.
        /// </summary>
        public static event Action<RadarMode> OnRadarModeChanged;

        [Header("Prefab")]
        [SerializeField] private string prefabPath = "Assets/lathiel/EMF/Prefabs/EMF_Modern_T2.prefab";

        [Header("Position (Left Hand)")]
        [SerializeField] private Vector3 localPosition = new Vector3(-0.35f, -0.25f, 0.4f);
        [SerializeField] private Vector3 localRotation = new Vector3(10f, 15f, -5f);
        [SerializeField] private float scale = 0.8f;

        [Header("Settings")]
        [SerializeField] private KeyCode holdKey = KeyCode.Q;
        [SerializeField] private bool startVisible = false;

        [Header("Mode Switching")]
        [SerializeField] private KeyCode modeSwitchKey = KeyCode.Tab;

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
        [Header("Core Shard Hologram")]
        [SerializeField] private GameObject coreShardHologramPrefab;
        [Tooltip("Position offset for the Core Shard hologram (local space, relative to radar)")]
        [SerializeField] private Vector3 coreShardHologramOffset = new Vector3(0f, 0.15f, 0.05f);
        [Tooltip("Scale multiplier for the Core Shard hologram")]
        [SerializeField] private float coreShardHologramScale = 0.08f;

        [Header("Detection - Treasure Chests (Mode 1)")]
        [SerializeField] private float chestPerfectAimAngle = 10f;
        [SerializeField] private float chestMaxDetectionAngle = 90f;
        [SerializeField] private float chestMinDetectionDistance = 0.3f;
        [SerializeField] private float chestMaxDetectionDistance = 15f;

        [Header("Detection - Core Shard (Mode 2)")]
        [SerializeField] private float shardPerfectAimAngle = 30f;
        [SerializeField] private float shardMaxDetectionAngle = 120f;
        [SerializeField] private float shardMinDetectionDistance = 2f;
        [SerializeField] private float shardMaxDetectionDistance = 80f;

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

        // Mode state
        private RadarMode currentMode = RadarMode.TreasureChests;
        private List<RadarMode> unlockedModes = new List<RadarMode> { RadarMode.TreasureChests };
        private bool autoShowActive = false; // True during unlock animation auto-show

        // Needle state
        private Transform needleTransform;
        private float currentNeedleValue = 0f; // 0-1 normalized value
        private float targetNeedleValue = 0f;
        private Vector3 needleStartEuler;

        // Hologram
        private GameObject hologramInstance;
        private Transform hologramTransform;
        private Transform hologramModelTransform; // The actual 3D model that spins
        private float hologramSpinAngle = 0f;
        private Vector3 hologramBasePosition;

        // Per-mode hologram config (prefab + color for Mode 1 are the default hologramPrefab/hologramColor)
        public int CurrentMode => (int)currentMode;
        public bool HasMultipleModes => unlockedModes.Count > 1;

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

                // Put radar on HeldTool layer so it's lit by the held-tool light (not scene lights)
                int heldToolLayer = LayerMask.NameToLayer("HeldTool");
                if (heldToolLayer >= 0)
                {
                    SetLayerRecursively(radarInstance, heldToolLayer);
                }

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
            }

            // Subscribe to mission events for Mode 2 unlock
            if (MissionManager.Instance != null)
            {
                MissionManager.Instance.OnMissionStarted += OnMissionStarted;
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
                // Don't hide if auto-show animation is playing
                if (!autoShowActive)
                {
                    HideRadar();
                    activationTimer = 0f; // Reset timer when released
                }
            }

            // Mode switching with Tab (only when radar is visible and multiple modes unlocked)
            if (Input.GetKeyDown(modeSwitchKey) && isVisible && unlockedModes.Count > 1)
            {
                CycleMode();
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

        private void OnMissionStarted(MissionData mission)
        {
            if (mission.missionId == "secret_rooms") // Mission 9
            {
                UnlockMode(RadarMode.CoreShard);
                StartCoroutine(PlayModeUnlockAnimation());
            }
        }

        /// <summary>
        /// Unlock a new radar mode.
        /// </summary>
        public void UnlockMode(RadarMode mode)
        {
            if (!unlockedModes.Contains(mode))
            {
                unlockedModes.Add(mode);
                Debug.Log($"[RadarTool] Mode unlocked: {mode}");
            }
        }

        /// <summary>
        /// Cycle to the next unlocked mode.
        /// </summary>
        private void CycleMode()
        {
            if (unlockedModes.Count <= 1) return;

            int currentIndex = unlockedModes.IndexOf(currentMode);
            int nextIndex = (currentIndex + 1) % unlockedModes.Count;
            SetMode(unlockedModes[nextIndex]);
        }

        /// <summary>
        /// Switch to a specific radar mode.
        /// </summary>
        public void SetMode(RadarMode mode)
        {
            if (currentMode == mode) return;

            currentMode = mode;
            Debug.Log($"[RadarTool] Mode switched to: {mode}");

            // Swap hologram based on mode
            switch (mode)
            {
                case RadarMode.TreasureChests:
                    SwapHologram(hologramPrefab, hologramOffset, hologramScale);
                    break;
                case RadarMode.CoreShard:
                    SwapHologram(coreShardHologramPrefab, coreShardHologramOffset, coreShardHologramScale);
                    break;
            }

            // Update detection parameters on the RadarPointer
            ApplyDetectionParametersForMode(mode);

            // Refresh which target the radar points to
            RadarTarget.RefreshActiveTarget();

            // Fire event
            OnRadarModeChanged?.Invoke(mode);
        }

        private void ApplyDetectionParametersForMode(RadarMode mode)
        {
            if (radarInstance == null) return;
            var pointer = radarInstance.GetComponent<RadarPointer>();
            if (pointer == null) return;

            switch (mode)
            {
                case RadarMode.TreasureChests:
                    pointer.SetDetectionParameters(chestPerfectAimAngle, chestMaxDetectionAngle, chestMinDetectionDistance, chestMaxDetectionDistance);
                    break;
                case RadarMode.CoreShard:
                    pointer.SetDetectionParameters(shardPerfectAimAngle, shardMaxDetectionAngle, shardMinDetectionDistance, shardMaxDetectionDistance);
                    break;
            }
        }

        /// <summary>
        /// Swap the hologram model, keeping its original prefab materials.
        /// Only adjusts for on-top rendering and shadow disabling.
        /// </summary>
        private void SwapHologram(GameObject prefab, Vector3 offset, float newScale)
        {
            if (hologramTransform == null) return;

            // Update container position and scale for this mode
            hologramTransform.localPosition = offset;
            hologramTransform.localScale = Vector3.one * newScale;
            hologramBasePosition = offset;

            // Destroy existing model child
            if (hologramModelTransform != null)
            {
                Destroy(hologramModelTransform.gameObject);
                hologramModelTransform = null;
            }

            // Instantiate new model
            if (prefab != null)
            {
                GameObject model = Instantiate(prefab, hologramTransform);
                model.name = "HologramModel";
                model.transform.localPosition = Vector3.zero;
                model.transform.localRotation = Quaternion.identity;
                model.transform.localScale = Vector3.one;
                hologramModelTransform = model.transform;

                // Keep original materials - just fix shadows and on-top rendering
                PrepareHologramRenderers(model);
            }
            else
            {
                // Fallback shape (no prefab assigned)
                CreateFallbackHologramShape();
            }
        }

        /// <summary>
        /// Smooth auto-reveal animation when a new mode is unlocked.
        /// Radar appears, hologram glows up brightly, then fades to normal.
        /// Radar stays in hand after animation.
        /// </summary>
        private IEnumerator PlayModeUnlockAnimation()
        {
            autoShowActive = true;

            // Force-show radar
            ShowRadar();

            // Switch to the new mode
            SetMode(RadarMode.CoreShard);

            // Show "New Object" centered popup via MissionUI
            var missionUI = FindObjectOfType<MissionUI>();
            if (missionUI != null)
            {
                missionUI.ShowCenteredPopup("New Object", 4f);
            }

            // Smooth emission animation (0 = no glow, peak = bright flash)
            float normalEmission = 0f;
            float peakEmission = 8f;

            // Phase 1: Smooth ramp up (1s) using SmoothStep
            float rampUpDuration = 1.0f;
            float elapsed = 0f;
            while (elapsed < rampUpDuration)
            {
                elapsed += Time.deltaTime;
                float t = Mathf.SmoothStep(0f, 1f, elapsed / rampUpDuration);
                float em = Mathf.Lerp(normalEmission, peakEmission, t);
                SetHologramEmission(em);
                yield return null;
            }

            // Phase 2: Hold at bright peak (2s)
            SetHologramEmission(peakEmission);
            yield return new WaitForSeconds(2.0f);

            // Phase 3: Smooth fade back to normal (1.5s)
            float rampDownDuration = 1.5f;
            elapsed = 0f;
            while (elapsed < rampDownDuration)
            {
                elapsed += Time.deltaTime;
                float t = Mathf.SmoothStep(0f, 1f, elapsed / rampDownDuration);
                float em = Mathf.Lerp(peakEmission, normalEmission, t);
                SetHologramEmission(em);
                yield return null;
            }
            SetHologramEmission(normalEmission);

            autoShowActive = false;

            // Radar stays visible - player can press Q to hide when they want
        }

        /// <summary>
        /// Set the emission intensity on the current hologram model's existing materials.
        /// Uses white emission so the original colors are preserved but glow brighter.
        /// Intensity 0 = no extra emission, >0 = additive glow.
        /// </summary>
        private void SetHologramEmission(float intensity)
        {
            if (hologramModelTransform == null) return;

            foreach (var renderer in hologramModelTransform.GetComponentsInChildren<Renderer>())
            {
                foreach (var mat in renderer.materials)
                {
                    if (mat.HasProperty("_EmissionColor"))
                    {
                        if (intensity > 0f)
                        {
                            mat.EnableKeyword("_EMISSION");
                            // Use white emission so original colors glow evenly
                            Color emissionColor = Color.white * intensity;
                            mat.SetColor("_EmissionColor", emissionColor);
                        }
                        else
                        {
                            mat.DisableKeyword("_EMISSION");
                            mat.SetColor("_EmissionColor", Color.black);
                        }
                    }
                }
            }
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
                Debug.Log($"[RadarTool] Angle: {angle:F1}, Value: {currentNeedleValue:F2}, Level: {level}");
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

            if (MissionManager.Instance != null)
            {
                MissionManager.Instance.OnMissionStarted -= OnMissionStarted;
            }
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

                // Put radar on HeldTool layer so the headlamp doesn't illuminate it
                int heldToolLayer = LayerMask.NameToLayer("HeldTool");
                if (heldToolLayer >= 0)
                {
                    SetLayerRecursively(radarInstance, heldToolLayer);
                }

                // Find and setup needle
                FindNeedle();

                // Setup self-illumination (always visible in dark)
                SetupSelfIllumination();

                // Setup 3D hologram (treasure chest indicator)
                SetupHologram();
            }
        }

        /// <summary>
        /// Setup a subtle screen light so the radar is visible in dark areas.
        /// Uses a point light positioned in front of the screen to simulate a backlit display.
        /// </summary>
        private void SetupSelfIllumination()
        {
            if (radarInstance == null) return;

            // Destroy ALL Light components on the radar (prefab baked-in + any previously created screen lights)
            foreach (var light in radarInstance.GetComponentsInChildren<Light>(true))
            {
                // If light is on a child object (like "RadarScreenLight"), destroy the whole child
                if (light.gameObject != radarInstance)
                    Destroy(light.gameObject);
                else
                    Destroy(light); // Just remove the component if on root
            }

            // Reset any emission on materials (EMF lamp materials have white emission)
            ResetMaterialEmission();
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
                hologramInstance = existingHologram.gameObject;
                hologramTransform = existingHologram;
                hologramBasePosition = hologramTransform.localPosition;
                Transform modelChild = existingHologram.Find("HologramModel");
                if (modelChild != null)
                    hologramModelTransform = modelChild;
                else if (existingHologram.childCount > 0)
                    hologramModelTransform = existingHologram.GetChild(0);
                PrepareHologramRenderers(hologramInstance);
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

            // Instantiate the 3D prefab if assigned - keep its original materials
            if (hologramPrefab != null)
            {
                GameObject model = Instantiate(hologramPrefab, hologramTransform);
                model.name = "HologramModel";
                model.transform.localPosition = Vector3.zero;
                model.transform.localRotation = Quaternion.identity;
                model.transform.localScale = Vector3.one;
                hologramModelTransform = model.transform;

                // Keep original materials - just fix shadows and on-top rendering
                PrepareHologramRenderers(model);
            }
            else
            {
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

            ApplyFallbackHologramMaterial(body);
            ApplyFallbackHologramMaterial(lid);
        }

        /// <summary>
        /// Prepare hologram renderers: disable shadows and apply on-top rendering.
        /// Keeps the prefab's original materials intact.
        /// </summary>
        private void PrepareHologramRenderers(GameObject obj)
        {
            if (obj == null) return;

            // Destroy CrystalGlow components so they don't create lights or pulse emission
            foreach (var glow in obj.GetComponentsInChildren<Lighting.CrystalGlow>(true))
            {
                Destroy(glow);
            }

            // Destroy any Light components (e.g. CrystalLight created by the prefab)
            foreach (var light in obj.GetComponentsInChildren<Light>(true))
            {
                if (light.gameObject != obj)
                    Destroy(light.gameObject); // Destroy the whole "CrystalLight" child
                else
                    Destroy(light);
            }

            foreach (var renderer in obj.GetComponentsInChildren<Renderer>())
            {
                renderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
                renderer.receiveShadows = false;

                // .materials creates per-instance clones (original shared materials stay untouched)
                Material[] mats = renderer.materials;
                foreach (var mat in mats)
                {
                    // Kill emission so glowing prefabs (like Core Shard) don't act as flashlights on the radar
                    mat.DisableKeyword("_EMISSION");
                    if (mat.HasProperty("_EmissionColor"))
                        mat.SetColor("_EmissionColor", Color.black);
                    if (mat.HasProperty("_EmissiveColor"))
                        mat.SetColor("_EmissiveColor", Color.black);

                    if (renderOnTop)
                    {
                        if (mat.HasProperty("_ZTest"))
                            mat.SetInt("_ZTest", (int)UnityEngine.Rendering.CompareFunction.Always);
                        mat.renderQueue = onTopRenderQueue;
                    }
                }
                renderer.materials = mats;
            }
        }

        /// <summary>
        /// Apply a simple hologram tint material to fallback primitive shapes only.
        /// </summary>
        private void ApplyFallbackHologramMaterial(GameObject obj)
        {
            if (obj == null) return;

            Color color = new Color(0.4f, 1f, 1f, 0.85f); // Cyan fallback

            foreach (var renderer in obj.GetComponentsInChildren<Renderer>())
            {
                Shader shader = Shader.Find("Universal Render Pipeline/Lit");
                if (shader == null)
                    shader = Shader.Find("Standard");
                if (shader == null) continue;

                Material holoMat = new Material(shader);
                holoMat.SetFloat("_Surface", 1);
                holoMat.SetFloat("_Blend", 0);
                holoMat.SetFloat("_AlphaClip", 0);
                holoMat.SetFloat("_SrcBlend", (float)UnityEngine.Rendering.BlendMode.SrcAlpha);
                holoMat.SetFloat("_DstBlend", (float)UnityEngine.Rendering.BlendMode.OneMinusSrcAlpha);
                holoMat.SetFloat("_ZWrite", 0);
                holoMat.EnableKeyword("_SURFACE_TYPE_TRANSPARENT");
                holoMat.SetColor("_BaseColor", color);
                holoMat.renderQueue = 3000;

                if (renderOnTop)
                {
                    holoMat.SetInt("_ZTest", (int)UnityEngine.Rendering.CompareFunction.Always);
                    holoMat.renderQueue = onTopRenderQueue;
                }

                renderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
                renderer.receiveShadows = false;

                Material[] mats = new Material[renderer.materials.Length];
                for (int i = 0; i < mats.Length; i++)
                    mats[i] = holoMat;
                renderer.materials = mats;
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
        /// <summary>
        /// Reset any emission on radar materials to fix white/bright appearance.
        /// </summary>
        private void ResetMaterialEmission()
        {
            if (radarInstance == null) return;

            foreach (var renderer in radarInstance.GetComponentsInChildren<Renderer>())
            {
                Material[] mats = renderer.materials;
                foreach (var mat in mats)
                {
                    mat.DisableKeyword("_EMISSION");
                    if (mat.HasProperty("_EmissionColor"))
                        mat.SetColor("_EmissionColor", Color.black);
                    if (mat.HasProperty("_EmissiveColor"))
                        mat.SetColor("_EmissiveColor", Color.black);
                    mat.renderQueue = -1;
                    if (mat.HasProperty("_ZTest"))
                        mat.SetFloat("_ZTest", 4f);
                }
                renderer.materials = mats;
            }
        }

        private static void SetLayerRecursively(GameObject obj, int layer)
        {
            obj.layer = layer;
            foreach (Transform child in obj.transform)
                SetLayerRecursively(child.gameObject, layer);
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
                }
            }
        }

        /// <summary>
        /// Refresh the "always on top" rendering on the hologram only.
        /// </summary>
        public void RefreshOnTopRendering()
        {
            if (renderOnTop && hologramInstance != null)
            {
                foreach (var renderer in hologramInstance.GetComponentsInChildren<Renderer>())
                {
                    Material[] mats = renderer.materials;
                    foreach (var mat in mats)
                    {
                        if (mat.HasProperty("_ZTest"))
                            mat.SetInt("_ZTest", (int)UnityEngine.Rendering.CompareFunction.Always);
                        mat.renderQueue = onTopRenderQueue;
                    }
                    renderer.materials = mats;
                }
            }
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
