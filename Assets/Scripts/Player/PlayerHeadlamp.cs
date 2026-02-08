using UnityEngine;

namespace BeneathTheFloor.Player
{
    /// <summary>
    /// Manages the player's headlamp/flashlight for underground exploration.
    /// Creates and configures a Light component as a child of the camera.
    /// Can be toggled on/off with a key (default: L).
    /// </summary>
    public class PlayerHeadlamp : MonoBehaviour
    {
        [Header("Light Settings")]
        [SerializeField] private LightType lightType = LightType.Spot;
        [SerializeField] private float baseRange = 8f;
        [SerializeField] private float intensity = 2.5f;
        [SerializeField] private float spotAngle = 75f;
        [SerializeField] private float innerSpotAngle = 40f;
        [SerializeField] private Color lightColor = new Color(1f, 0.95f, 0.85f); // Warm white
        [SerializeField] private bool enableShadows = true;
        [SerializeField] private LightShadows shadowType = LightShadows.Soft;

        [Header("Toggle")]
        [SerializeField] private KeyCode toggleKey = KeyCode.L;
        [SerializeField] private bool startEnabled = true;

        [Header("Audio")]
        [SerializeField] private AudioClip toggleSound;
        private AudioSource audioSource;

        [Header("Debug")]
        [SerializeField] private bool enableDebugLogs = false;

        // Runtime
        private Light headlampLight;
        private Transform cameraTransform;
        private GameObject headlampObject;

        public static PlayerHeadlamp Instance { get; private set; }

        /// <summary>
        /// The actual Light component. Used by UpgradeStation to modify range.
        /// </summary>
        public Light HeadlampLight => headlampLight;

        /// <summary>
        /// Current range of the headlamp light.
        /// </summary>
        public float Range
        {
            get => headlampLight != null ? headlampLight.range : baseRange;
            set
            {
                if (headlampLight != null)
                {
                    headlampLight.range = value;
                }
            }
        }

        /// <summary>
        /// Whether the headlamp is currently on.
        /// </summary>
        public bool IsOn => headlampLight != null && headlampLight.enabled;

        private void Awake()
        {
            if (Instance == null)
            {
                Instance = this;
            }
            else if (Instance != this)
            {
                // Allow multiple instances on different players, but warn
                if (enableDebugLogs) Debug.LogWarning("[PlayerHeadlamp] Multiple instances detected. Using newest.");
                Instance = this;
            }
        }

        private void Start()
        {
            if (enableDebugLogs) Debug.Log("[PlayerHeadlamp] Start() called on " + gameObject.name);
            FindCamera();
            CreateHeadlamp();
            SetupAudio();

            if (headlampLight != null)
            {
                headlampLight.enabled = startEnabled;
                if (enableDebugLogs) Debug.Log($"[PlayerHeadlamp] Headlamp created successfully. Enabled: {startEnabled}, Range: {headlampLight.range}, Intensity: {headlampLight.intensity}");
            }
            else
            {
                if (enableDebugLogs) Debug.LogError("[PlayerHeadlamp] Failed to create headlamp light!");
            }
        }

        private void OnDestroy()
        {
            if (Instance == this)
            {
                Instance = null;
            }
        }

        private void FindCamera()
        {
            // Try to find camera in children (CameraHolder/Main Camera)
            var cameraHolder = transform.Find("CameraHolder");
            if (cameraHolder != null)
            {
                var mainCam = cameraHolder.Find("Main Camera");
                if (mainCam != null)
                {
                    cameraTransform = mainCam;
                    if (enableDebugLogs) Debug.Log("[PlayerHeadlamp] Found camera at CameraHolder/Main Camera");
                    return;
                }
                // Use CameraHolder itself if no Main Camera child
                cameraTransform = cameraHolder;
                if (enableDebugLogs) Debug.Log("[PlayerHeadlamp] Found CameraHolder (no Main Camera child)");
                return;
            }

            // Try Camera.main
            if (Camera.main != null)
            {
                cameraTransform = Camera.main.transform;
                if (enableDebugLogs) Debug.Log("[PlayerHeadlamp] Using Camera.main: " + cameraTransform.name);
                return;
            }

            // Last resort: use this transform
            cameraTransform = transform;
            if (enableDebugLogs) Debug.LogWarning("[PlayerHeadlamp] Could not find camera, attaching headlamp to player root.");
        }

        private void CreateHeadlamp()
        {
            if (cameraTransform == null)
            {
                if (enableDebugLogs) Debug.LogError("[PlayerHeadlamp] No camera transform found, cannot create headlamp.");
                return;
            }

            // Check if headlamp already exists
            headlampObject = cameraTransform.Find("Headlamp")?.gameObject;
            if (headlampObject != null)
            {
                headlampLight = headlampObject.GetComponent<Light>();
                if (headlampLight != null)
                {
                    return;
                }
            }

            // Create new headlamp object
            headlampObject = new GameObject("Headlamp");
            headlampObject.transform.SetParent(cameraTransform, false);
            headlampObject.transform.localPosition = Vector3.zero;
            headlampObject.transform.localRotation = Quaternion.identity;

            // Create and configure the Light component
            headlampLight = headlampObject.AddComponent<Light>();
            headlampLight.type = lightType;
            headlampLight.range = baseRange;
            headlampLight.intensity = intensity;
            headlampLight.color = lightColor;

            if (lightType == LightType.Spot)
            {
                headlampLight.spotAngle = spotAngle;
                headlampLight.innerSpotAngle = innerSpotAngle;
            }

            if (enableShadows)
            {
                headlampLight.shadows = shadowType;
                headlampLight.shadowStrength = 0.8f;
                headlampLight.shadowBias = 0.05f;
                headlampLight.shadowNormalBias = 0.4f;
            }
            else
            {
                headlampLight.shadows = LightShadows.None;
            }

            // Force pixel rendering to prevent Unity from culling the headlamp
            headlampLight.renderMode = LightRenderMode.ForcePixel;

            // Exclude HeldTool layer so the headlamp doesn't illuminate held items (radar, etc.)
            int heldToolLayer = LayerMask.NameToLayer("HeldTool");
            if (heldToolLayer >= 0)
            {
                headlampLight.cullingMask &= ~(1 << heldToolLayer);
            }
        }

        private void SetupAudio()
        {
            audioSource = GetComponent<AudioSource>();
            if (audioSource == null && toggleSound != null)
            {
                audioSource = gameObject.AddComponent<AudioSource>();
                audioSource.playOnAwake = false;
                audioSource.spatialBlend = 0f; // 2D sound
            }
        }

        private void Update()
        {
            // Retry creating headlamp if it failed during Start (timing issue in builds)
            if (headlampLight == null)
            {
                FindCamera();
                if (cameraTransform != null)
                {
                    CreateHeadlamp();
                    if (headlampLight != null)
                    {
                        headlampLight.enabled = startEnabled;
                        if (enableDebugLogs) Debug.Log("[PlayerHeadlamp] Late initialization succeeded!");
                    }
                }
            }

            // Handle toggle input
            if (Input.GetKeyDown(toggleKey))
            {
                if (enableDebugLogs) Debug.Log("[PlayerHeadlamp] Toggle key pressed (L)");
                Toggle();
            }
        }

        /// <summary>
        /// Toggle the headlamp on/off.
        /// </summary>
        public void Toggle()
        {
            if (headlampLight == null)
            {
                if (enableDebugLogs) Debug.LogError("[PlayerHeadlamp] Toggle called but headlampLight is null!");
                return;
            }

            headlampLight.enabled = !headlampLight.enabled;
            if (enableDebugLogs) Debug.Log($"[PlayerHeadlamp] Toggled headlamp. Now: {(headlampLight.enabled ? "ON" : "OFF")}");

            if (toggleSound != null && audioSource != null)
            {
                audioSource.PlayOneShot(toggleSound);
            }
        }

        /// <summary>
        /// Turn the headlamp on.
        /// </summary>
        public void TurnOn()
        {
            if (headlampLight != null)
            {
                headlampLight.enabled = true;
            }
        }

        /// <summary>
        /// Turn the headlamp off.
        /// </summary>
        public void TurnOff()
        {
            if (headlampLight != null)
            {
                headlampLight.enabled = false;
            }
        }

        /// <summary>
        /// Set the headlamp range (called by upgrade system).
        /// </summary>
        public void SetRange(float newRange)
        {
            if (headlampLight != null)
            {
                headlampLight.range = newRange;
            }
        }

        /// <summary>
        /// Set the headlamp intensity.
        /// </summary>
        public void SetIntensity(float newIntensity)
        {
            if (headlampLight != null)
            {
                headlampLight.intensity = newIntensity;
            }
        }
    }
}
