using UnityEngine;
using System.Collections;
using BeneathTheFloor.Economy;
using BeneathTheFloor.ResourceSystem;

namespace BeneathTheFloor.Digging
{
    /// <summary>
    /// Spawns visual world-drop objects when digging yields resources.
    /// Called by ResourceSpawner when a dig yields loot.
    /// Creates colored spheres (or custom prefabs) at the dig location for visual feedback.
    ///
    /// Note: This is purely visual feedback. Inventory is handled separately by DigInventoryBridge.
    /// </summary>
    public class DigWorldDropSpawner : MonoBehaviour
    {
        [Header("Configuration")]
        [Tooltip("Resource drop configuration asset defining prefabs, colors, and scales.")]
        [SerializeField] private ResourceDropConfig dropConfig;

        [Header("Spawn Settings")]
        [Tooltip("Layer mask for ground raycast. Use Everything or specify terrain layers.")]
        [SerializeField] private LayerMask groundMask = ~0; // All layers by default

        [Header("Lifetime")]
        [Tooltip("How long drops persist before being destroyed (seconds). 0 = never destroy (persist until picked up).")]
        [SerializeField] private float lifetimeSeconds = 0f; // 0 = infinite, drops persist until picked up

        [Tooltip("Fade out duration before destruction. Only used if lifetimeSeconds > 0.")]
        [SerializeField] private float fadeOutDuration = 1f;

        [Header("Runtime Fallback")]
        [Tooltip("Create a default sphere prefab at runtime if no config/prefab is assigned.")]
        [SerializeField] private bool createDefaultPrefabIfMissing = true;

        [Tooltip("Size of the auto-generated sphere drop.")]
        [SerializeField] private float defaultSphereRadius = 0.12f;

        [Header("Pickup Audio")]
        [Tooltip("Default sound to play when a pickup is collected. Assigned to spawned pickups if their pickupClip is null.")]
        [SerializeField] private AudioClip defaultPickupClip;

        [Tooltip("Whether to apply the default pickup sound to spawned pickups.")]
        [SerializeField] private bool applyDefaultPickupSound = true;

        [Header("Pop Physics")]
        [Tooltip("Initial upward velocity when spawning (helps prevent falling through terrain).")]
        [SerializeField] private float popUpForce = 3f;

        [Tooltip("Random horizontal spread when popping up.")]
        [SerializeField] private float popHorizontalSpread = 1f;

        [Header("Fallthrough Safety Net")]
        [Tooltip("Y position below which resources are considered 'fallen through' and converted to money.")]
        [SerializeField] private float fallthroughYThreshold = -300f;

        [Tooltip("Enable the safety net that converts fallen resources to money.")]
        [SerializeField] private bool enableFallthroughSafetyNet = true;

        [Header("Debug")]
        [SerializeField] private bool enableDebugLogs = false;

        // Runtime state
        private GameObject _runtimeDefaultPrefab;

        public static DigWorldDropSpawner Instance { get; private set; }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetStatics()
        {
            Instance = null;
        }

        private void Awake()
        {
            if (Instance == null)
            {
                Instance = this;
            }
            else
            {
                Debug.LogWarning("[DigWorldDropSpawner] Multiple instances detected, destroying this one.");
                Destroy(gameObject);
                return;
            }
        }

        private void Start()
        {
            // Auto-load dropConfig from Resources if not assigned
            if (dropConfig == null)
            {
                dropConfig = Resources.Load<ResourceDropConfig>("ResourceDropConfig");
            }
        }

        private void OnDestroy()
        {
            if (Instance == this)
                Instance = null;

            // Clean up runtime prefab
            if (_runtimeDefaultPrefab != null)
            {
                Destroy(_runtimeDefaultPrefab);
                _runtimeDefaultPrefab = null;
            }
        }

        /// <summary>
        /// Spawn a resource drop from dig result data.
        /// Called by ResourceSpawner after determining what resource was found.
        /// </summary>
        /// <param name="hitPosition">World position where the dig hit.</param>
        /// <param name="resourceType">Type of resource to spawn.</param>
        /// <param name="amount">Amount of resource units.</param>
        /// <param name="depthBelowSoil">Depth below soil surface (for logging).</param>
        /// <param name="layerIndex">Layer index (1-6) for this depth.</param>
        /// <param name="volumeRemoved">Volume of terrain removed.</param>
        public void SpawnDropFromDigResult(Vector3 hitPosition, UndergroundResourceType resourceType, int amount, float depthBelowSoil, int layerIndex, float volumeRemoved)
        {
            if (resourceType == UndergroundResourceType.None || amount <= 0)
            {
                if (enableDebugLogs)
                    Debug.Log($"[DigWorldDropSpawner] Skipping spawn - type={resourceType}, amount={amount}");
                return;
            }

            if (enableDebugLogs)
            {
                Debug.Log($"[DigWorldDropSpawner] SpawnDropFromDigResult: {resourceType} x{amount} at {hitPosition}, depth={depthBelowSoil:F1}m, layer={layerIndex}");
            }

            // Start coroutine to spawn drop after a frame (allows terrain mesh to update)
            StartCoroutine(SpawnDropAfterFrame(hitPosition, resourceType, amount, depthBelowSoil, layerIndex, volumeRemoved));
        }

        /// <summary>
        /// Wait one frame for voxel mesh/collider to update, then spawn the drop.
        /// </summary>
        private IEnumerator SpawnDropAfterFrame(Vector3 hitPosition, UndergroundResourceType resourceType, int amount, float depthBelowSoil, int layerIndex, float volumeRemoved)
        {
            // Wait for voxel mesh + collider to fully regenerate
            yield return null;
            yield return null;
            yield return new WaitForSeconds(0.2f);

            if (enableDebugLogs)
                Debug.Log($"[DigWorldDropSpawner] Frame delay complete, spawning {resourceType} x{amount}");

            SpawnDropInternal(hitPosition, resourceType, amount, depthBelowSoil, layerIndex, volumeRemoved);
        }

        /// <summary>
        /// Internal method to spawn a visual drop.
        /// </summary>
        private void SpawnDropInternal(Vector3 hitPosition, UndergroundResourceType resourceType, int amount, float depthBelowSoil, int layerIndex, float volumeRemoved)
        {
            // Determine prefab, color, and scale
            GameObject prefab = GetPrefabForResource(resourceType);
            Color color = GetColorForResource(resourceType);
            float scale = GetScaleForResource(resourceType);

            if (prefab == null)
            {
                Debug.LogError($"[DigWorldDropSpawner] FAILED to spawn drop: No prefab available for {resourceType}! " +
                              $"dropConfig={(dropConfig != null ? dropConfig.name : "NULL")}, " +
                              $"createDefaultPrefabIfMissing={createDefaultPrefabIfMissing}");
                return;
            }

            // Find a clear spawn position
            Vector3 spawnPos = FindClearSpawnPosition(hitPosition);

            if (enableDebugLogs)
            {
                Debug.Log($"[DigWorldDropSpawner] Spawning at {spawnPos} (clear position above hit point {hitPosition})");
            }

            // Instantiate the drop
            GameObject drop = Instantiate(prefab, spawnPos, Quaternion.identity);
            drop.SetActive(true);
            drop.name = $"Drop_{resourceType}";

            // Always set scale to 1,1,1 for all resource drops
            drop.transform.localScale = Vector3.one;

            // Apply color to renderer
            ApplyColorToRenderer(drop, color);

            // Setup rigidbody and physics
            SetupPhysics(drop);

            // Add ResourcePickup component for manual pickup
            var pickup = drop.AddComponent<ResourcePickup>();
            pickup.Initialize(resourceType, amount);
            pickup.destroyOnPickup = true;
            pickup.enableHighlight = true;

            // Apply default pickup sound if configured
            if (applyDefaultPickupSound && defaultPickupClip != null && pickup.pickupClip == null)
            {
                pickup.pickupClip = defaultPickupClip;
                pickup.playPickupSound = true;

                if (pickup.audioSource == null)
                {
                    var audioSource = drop.AddComponent<AudioSource>();
                    audioSource.playOnAwake = false;
                    audioSource.spatialBlend = 1f; // 3D sound for world pickups
                    pickup.audioSource = audioSource;
                }
            }

            // Add fallthrough safety net - converts to money if falls below threshold
            if (enableFallthroughSafetyNet)
            {
                var safetyNet = drop.AddComponent<ResourceFallthroughSafetyNet>();
                safetyNet.Initialize(resourceType, amount, fallthroughYThreshold);
            }

            // Schedule destruction with optional fade
            if (lifetimeSeconds > 0f)
            {
                if (fadeOutDuration > 0f)
                {
                    var fader = drop.AddComponent<DropFadeAndDestroy>();
                    fader.Initialize(lifetimeSeconds, fadeOutDuration);
                }
                else
                {
                    Destroy(drop, lifetimeSeconds);
                }
            }

            // Disable shadow casting on pickups (perf: reduces shadow caster count)
            foreach (var r in drop.GetComponentsInChildren<Renderer>())
            {
                r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            }

            // Register with proximity culler for distance-based rendering
            if (ResourceSystem.ProximityCuller.Instance != null)
            {
                ResourceSystem.ProximityCuller.Instance.TrackPickup(drop);
            }

            if (enableDebugLogs)
            {
                Debug.Log($"[DigWorldDropSpawner] Spawned {resourceType} x{amount} at {spawnPos}, color={color}, scale={scale}");
            }
        }

        private GameObject GetPrefabForResource(UndergroundResourceType type)
        {
            // Try config first
            if (dropConfig != null)
            {
                var prefab = dropConfig.GetPrefab(type);
                if (prefab != null)
                {
                    if (enableDebugLogs)
                        Debug.Log($"[DigWorldDropSpawner] Using config prefab for {type}");
                    return prefab;
                }
                else if (enableDebugLogs)
                {
                    Debug.Log($"[DigWorldDropSpawner] No config prefab for {type}, checking fallback...");
                }
            }
            else if (enableDebugLogs)
            {
                Debug.Log($"[DigWorldDropSpawner] dropConfig is null, using runtime fallback...");
            }

            // Fallback: create runtime default if enabled
            if (createDefaultPrefabIfMissing)
            {
                var runtimePrefab = GetOrCreateRuntimePrefab();
                if (runtimePrefab != null)
                {
                    if (enableDebugLogs)
                        Debug.Log($"[DigWorldDropSpawner] Using runtime sphere prefab for {type}");
                    return runtimePrefab;
                }
                else
                {
                    Debug.LogError($"[DigWorldDropSpawner] Failed to create runtime prefab for {type}!");
                }
            }
            else
            {
                Debug.LogWarning($"[DigWorldDropSpawner] No prefab for {type} and createDefaultPrefabIfMissing=false");
            }

            return null;
        }

        private Color GetColorForResource(UndergroundResourceType type)
        {
            if (dropConfig != null)
            {
                return dropConfig.GetColor(type);
            }

            // Fallback color based on resource type name
            return GetFallbackColor(type);
        }

        private float GetScaleForResource(UndergroundResourceType type)
        {
            if (dropConfig != null)
            {
                return dropConfig.GetScale(type);
            }
            return 1f;
        }

        /// <summary>
        /// Get or create a simple sphere prefab at runtime.
        /// </summary>
        private GameObject GetOrCreateRuntimePrefab()
        {
            if (_runtimeDefaultPrefab != null)
                return _runtimeDefaultPrefab;

            // Create a simple sphere
            _runtimeDefaultPrefab = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            _runtimeDefaultPrefab.name = "RuntimeDropPrefab";
            _runtimeDefaultPrefab.SetActive(false);
            _runtimeDefaultPrefab.transform.localScale = Vector3.one * defaultSphereRadius * 2f;

            // Add Rigidbody for physics
            var rb = _runtimeDefaultPrefab.AddComponent<Rigidbody>();
            rb.mass = 0.1f;
            rb.drag = 1f;
            rb.angularDrag = 0.5f;
            rb.collisionDetectionMode = CollisionDetectionMode.Continuous;

            // Adjust collider
            var collider = _runtimeDefaultPrefab.GetComponent<SphereCollider>();
            if (collider != null)
            {
                collider.radius = 0.5f;
                var physicsMat = new PhysicMaterial("DropPhysicsMat")
                {
                    bounciness = 0.3f,
                    dynamicFriction = 0.5f,
                    staticFriction = 0.5f,
                    frictionCombine = PhysicMaterialCombine.Average,
                    bounceCombine = PhysicMaterialCombine.Average
                };
                collider.material = physicsMat;
            }

            // Create material
            var renderer = _runtimeDefaultPrefab.GetComponent<MeshRenderer>();
            if (renderer != null)
            {
                var shader = Shader.Find("Universal Render Pipeline/Lit") ?? Shader.Find("Standard");
                var mat = new Material(shader);
                mat.name = "DropMaterial";
                mat.SetFloat("_Smoothness", 0.3f);
                renderer.sharedMaterial = mat;
            }

            // Don't destroy on load so it persists
            DontDestroyOnLoad(_runtimeDefaultPrefab);

            if (enableDebugLogs)
                Debug.Log("[DigWorldDropSpawner] Created runtime default sphere prefab");

            return _runtimeDefaultPrefab;
        }

        private void ApplyColorToRenderer(GameObject drop, Color color)
        {
            var renderer = drop.GetComponentInChildren<MeshRenderer>();
            if (renderer != null)
            {
                // Create instance material to avoid modifying shared material
                var mat = renderer.material;
                mat.color = color;

                // Also set emission for slight glow on valuable items
                if (color.maxColorComponent > 0.7f)
                {
                    mat.EnableKeyword("_EMISSION");
                    mat.SetColor("_EmissionColor", color * 0.2f);
                }
            }
        }

        /// <summary>
        /// Setup physics for the resource drop.
        /// </summary>
        private void SetupPhysics(GameObject drop)
        {
            // Ensure rigidbody exists
            var rb = drop.GetComponent<Rigidbody>();
            if (rb == null)
            {
                rb = drop.AddComponent<Rigidbody>();
            }

            // Configure rigidbody
            rb.mass = 0.5f;
            rb.drag = 0.5f;
            rb.angularDrag = 0.5f;
            rb.useGravity = true;
            rb.isKinematic = false;
            rb.interpolation = RigidbodyInterpolation.Interpolate;
            rb.collisionDetectionMode = CollisionDetectionMode.Continuous;

            // Setup colliders as SOLID
            var allColliders = drop.GetComponentsInChildren<Collider>(true);
            foreach (var col in allColliders)
            {
                col.isTrigger = false;
                col.enabled = true;

                // Shrink sphere colliders to match small visual size
                if (col is SphereCollider sphereCol)
                {
                    sphereCol.radius = 0.15f;
                    sphereCol.center = Vector3.zero;
                }

                // Add physics material
                if (col.material == null)
                {
                    var physicsMat = new PhysicMaterial("DropPhysicsMat")
                    {
                        bounciness = 0.2f,
                        dynamicFriction = 0.6f,
                        staticFriction = 0.6f,
                        frictionCombine = PhysicMaterialCombine.Average,
                        bounceCombine = PhysicMaterialCombine.Average
                    };
                    col.material = physicsMat;
                }
            }

            // Apply upward "pop" force to help resource escape terrain
            if (popUpForce > 0f)
            {
                Vector3 popVelocity = Vector3.up * popUpForce;
                // Add random horizontal spread
                if (popHorizontalSpread > 0f)
                {
                    popVelocity += new Vector3(
                        Random.Range(-popHorizontalSpread, popHorizontalSpread),
                        0f,
                        Random.Range(-popHorizontalSpread, popHorizontalSpread)
                    );
                }
                rb.velocity = popVelocity;
            }

            if (enableDebugLogs)
            {
                Debug.Log($"[DigWorldDropSpawner] Physics setup complete for {drop.name}, pop velocity applied");
            }
        }

        /// <summary>
        /// Fallback color based on resource type when no config is set.
        /// </summary>
        private Color GetFallbackColor(UndergroundResourceType type)
        {
            return type switch
            {
                UndergroundResourceType.Dirt => new Color(0.55f, 0.40f, 0.25f),
                UndergroundResourceType.Clay => new Color(0.75f, 0.45f, 0.30f),
                UndergroundResourceType.SoftStone => new Color(0.65f, 0.60f, 0.55f),
                UndergroundResourceType.Stone => new Color(0.55f, 0.55f, 0.55f),
                UndergroundResourceType.Coal => new Color(0.20f, 0.20f, 0.20f),
                UndergroundResourceType.IronNugget or UndergroundResourceType.IronChunk => new Color(0.60f, 0.50f, 0.40f),
                UndergroundResourceType.CopperFragment or UndergroundResourceType.CopperPiece => new Color(0.80f, 0.50f, 0.30f),
                UndergroundResourceType.Quartz => new Color(0.95f, 0.95f, 1.0f),
                UndergroundResourceType.CrystalShard or UndergroundResourceType.CrystalDust => new Color(0.70f, 0.50f, 0.90f),
                UndergroundResourceType.AncientOre => new Color(0.50f, 0.60f, 0.45f),
                _ => new Color(0.6f, 0.5f, 0.4f) // Default brownish
            };
        }

        /// <summary>
        /// Get spawn position for resource.
        /// Spawn at the hit point so resource appears inside the dug hole.
        /// </summary>
        private Vector3 FindClearSpawnPosition(Vector3 hitPosition)
        {
            // Spawn at the hit position - resource appears directly in the hole
            return hitPosition;
        }

        /// <summary>
        /// Manually spawn a drop (for testing).
        /// </summary>
        [ContextMenu("Test Spawn Drop")]
        public void TestSpawnDrop()
        {
            SpawnDropInternal(
                transform.position + Vector3.forward * 2f,
                UndergroundResourceType.CrystalShard,
                1,
                10f,
                2,
                0.5f
            );
        }
    }

    /// <summary>
    /// Safety net component that monitors resource Y position.
    /// If resource falls below threshold (fell through terrain), converts its value to money.
    /// </summary>
    public class ResourceFallthroughSafetyNet : MonoBehaviour
    {
        private UndergroundResourceType _resourceType;
        private int _amount;
        private float _yThreshold;
        private bool _hasTriggered;

        public void Initialize(UndergroundResourceType resourceType, int amount, float yThreshold)
        {
            _resourceType = resourceType;
            _amount = amount;
            _yThreshold = yThreshold;
            _hasTriggered = false;
        }

        private void Update()
        {
            if (_hasTriggered) return;

            // Check if fallen below threshold
            if (transform.position.y < _yThreshold)
            {
                _hasTriggered = true;
                ConvertToMoneyAndDestroy();
            }
        }

        private void ConvertToMoneyAndDestroy()
        {
            // Get the sell value for this resource type
            int sellValue = GetSellValueForResource(_resourceType, _amount);

            if (sellValue > 0)
            {
                // Add money to player's currency
                var currencyManager = Economy.CurrencyManager.Instance;
                if (currencyManager != null)
                {
                    currencyManager.Add(sellValue);
                    Debug.Log($"[SafetyNet] Resource {_resourceType} x{_amount} fell through terrain - auto-converted to {sellValue} credits");
                }
                else
                {
                    Debug.LogWarning($"[SafetyNet] Resource {_resourceType} fell through but CurrencyManager not found!");
                }
            }

            // Destroy the resource object
            Destroy(gameObject);
        }

        /// <summary>
        /// Get the sell value for a resource type.
        /// Uses ResourceSystemConfig if available, otherwise DigInventoryBridge, otherwise fallback.
        /// </summary>
        private int GetSellValueForResource(UndergroundResourceType type, int amount)
        {
            // Try to get value from ResourceSystemConfig first
            var config = Resources.Load<ResourceSystemConfig>("ResourceSystemConfig");
            if (config != null)
            {
                // Map UndergroundResourceType to resource ID
                string resourceId = MapUndergroundTypeToResourceId(type);
                var resourceDef = config.GetResourceById(resourceId);
                if (resourceDef != null && resourceDef.creditValuePerUnit > 0)
                {
                    return resourceDef.creditValuePerUnit * amount;
                }
            }

            // Fallback: Try DigInventoryBridge
            var bridge = DigInventoryBridge.Instance;
            if (bridge != null && bridge.TryGetItemForResource(type, out var item, out int defaultAmountPerDig))
            {
                if (item != null)
                {
                    return item.sellPrice * amount * defaultAmountPerDig;
                }
            }

            // Final fallback: hardcoded minimum value
            return 5 * amount;
        }

        /// <summary>
        /// Map UndergroundResourceType enum to resource ID string used in ResourceSystemConfig.
        /// </summary>
        private string MapUndergroundTypeToResourceId(UndergroundResourceType type)
        {
            return type switch
            {
                UndergroundResourceType.Stone or UndergroundResourceType.SoftStone or
                UndergroundResourceType.HardStone or UndergroundResourceType.Sandstone => "stone",
                UndergroundResourceType.IronNugget or UndergroundResourceType.IronChunk => "iron",
                UndergroundResourceType.CopperFragment or UndergroundResourceType.CopperPiece => "copper",
                UndergroundResourceType.Coal => "coal",
                UndergroundResourceType.Gold => "gold",
                UndergroundResourceType.Dirt or UndergroundResourceType.HardSoil or
                UndergroundResourceType.HardSoilDeep => "dirt",
                UndergroundResourceType.Clay => "clay",
                UndergroundResourceType.Quartz or UndergroundResourceType.PurpleQuartz => "quartz",
                UndergroundResourceType.CrystalShard or UndergroundResourceType.CrystalDust or
                UndergroundResourceType.CrystalStone or UndergroundResourceType.CrystalCoreFragment or
                UndergroundResourceType.CoreCrystalChunk or UndergroundResourceType.DeepCrystalVein or
                UndergroundResourceType.LuminousDust => "crystal",
                _ => type.ToString().ToLower()
            };
        }
    }

    /// <summary>
    /// Component that fades out and destroys a drop after a delay.
    /// </summary>
    public class DropFadeAndDestroy : MonoBehaviour
    {
        private float _lifetime;
        private float _fadeDuration;
        private float _timer;
        private bool _isFading;
        private MeshRenderer _renderer;
        private Color _originalColor;

        public void Initialize(float lifetime, float fadeDuration)
        {
            _lifetime = lifetime;
            _fadeDuration = fadeDuration;
            _timer = 0f;
            _isFading = false;

            _renderer = GetComponentInChildren<MeshRenderer>();
            if (_renderer != null && _renderer.material != null)
            {
                _originalColor = _renderer.material.color;

                // Enable transparency for fade
                var mat = _renderer.material;
                mat.SetFloat("_Surface", 1); // Transparent
                mat.SetInt("_SrcBlend", (int)UnityEngine.Rendering.BlendMode.SrcAlpha);
                mat.SetInt("_DstBlend", (int)UnityEngine.Rendering.BlendMode.OneMinusSrcAlpha);
                mat.EnableKeyword("_ALPHABLEND_ON");
                mat.renderQueue = 3000;
            }
        }

        private void Update()
        {
            _timer += Time.deltaTime;

            if (!_isFading && _timer >= _lifetime - _fadeDuration)
            {
                _isFading = true;
            }

            if (_isFading && _renderer != null)
            {
                float fadeProgress = (_timer - (_lifetime - _fadeDuration)) / _fadeDuration;
                fadeProgress = Mathf.Clamp01(fadeProgress);

                Color c = _originalColor;
                c.a = 1f - fadeProgress;
                _renderer.material.color = c;
            }

            if (_timer >= _lifetime)
            {
                Destroy(gameObject);
            }
        }
    }
}
