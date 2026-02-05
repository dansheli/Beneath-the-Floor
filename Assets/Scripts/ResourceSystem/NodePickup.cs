using UnityEngine;
using BeneathTheFloor.Inventory;
using BeneathTheFloor.Interaction;
using BeneathTheFloor.Crafting;
using BeneathTheFloor.Digging;
using BeneathTheFloor.Economy;

namespace BeneathTheFloor.ResourceSystem
{
    /// <summary>
    /// Pickup component for items spawned from broken hidden nodes.
    /// Integrates with existing inventory system.
    /// Implements IPullablePickup for magnet pull compatibility.
    /// </summary>
    [RequireComponent(typeof(Collider))]
    public class NodePickup : MonoBehaviour, IInteractable, IPullablePickup
    {
        [Header("Pickup Settings")]
        [SerializeField] private int tier = 1;
        [SerializeField] private int amount = 1;

        [Header("Resource Info")]
        [SerializeField] private int resourceIndex = -1;
        [SerializeField] private string resourceId;
        [SerializeField] private int creditValue = 1;

        // IPullablePickup implementation
        public int Amount => amount;

        // Resource properties
        public int Tier => tier;
        public int ResourceIndex => resourceIndex;
        public string ResourceId => resourceId;
        public int CreditValue => creditValue;

        [Header("Physics")]
        [SerializeField] private bool autoPickup = false; // Disabled - use E key / magnet only
        [SerializeField] private float autoPickupDelay = 0.5f;
        [SerializeField] private float autoPickupRadius = 1.5f;

        [Header("Fallthrough Safety Net")]
        [Tooltip("Y position below which this pickup is considered 'fallen through' and converted to money.")]
        [SerializeField] private float fallthroughYThreshold = -300f;

        [Header("Audio")]
        [Tooltip("Sound effects that play when this resource is collected (random selection).")]
        [SerializeField] private AudioClip[] collectSounds;
        [SerializeField] [Range(0f, 1f)] private float collectVolume = 0.7f;

        [Tooltip("Sound effect that plays when inventory is full and collection fails.")]
        [SerializeField] private AudioClip inventoryFullSound;
        [SerializeField] [Range(0f, 1f)] private float inventoryFullVolume = 0.6f;
        [Tooltip("Start time in seconds - skip to this point in the audio clip.")]
        [SerializeField] [Range(0f, 10f)] private float inventoryFullStartTime = 0f;

        // Static cache for sounds (shared across all pickups)
        private static AudioClip[] cachedCollectSounds;
        private static float cachedCollectVolume = 0.7f;
        private static AudioClip cachedInventoryFullSound;
        private static float cachedInventoryFullVolume = 0.6f;
        private static float cachedInventoryFullStartTime = 0f;
        private static bool soundsCached = false;

        // Reset static variables when entering play mode (Editor)
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetStatics()
        {
            cachedCollectSounds = null;
            cachedCollectVolume = 0.7f;
            cachedInventoryFullSound = null;
            cachedInventoryFullVolume = 0.6f;
            cachedInventoryFullStartTime = 0f;
            soundsCached = false;
        }

        private float spawnTime;
        private Rigidbody rb;
        private Transform playerTransform;
        private MeshRenderer _renderer;
        private Color _originalColor;
        private bool _isHighlighted;
        private bool _frozen; // physics disabled after settling

        // IInteractable
        public bool CanInteract => true;

        private void Awake()
        {
            rb = GetComponent<Rigidbody>();
            spawnTime = Time.time;

            // Cache sounds from this instance if set (prefab-based)
            if (!soundsCached && collectSounds != null && collectSounds.Length > 0)
            {
                cachedCollectSounds = collectSounds;
                cachedCollectVolume = collectVolume;
                soundsCached = true;
            }

            // Cache inventory full sound if set
            if (cachedInventoryFullSound == null && inventoryFullSound != null)
            {
                cachedInventoryFullSound = inventoryFullSound;
                cachedInventoryFullVolume = inventoryFullVolume;
                cachedInventoryFullStartTime = inventoryFullStartTime;
            }

            // Cache renderer for highlight + disable shadows for performance
            _renderer = GetComponentInChildren<MeshRenderer>();
            if (_renderer != null)
            {
                _renderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
                if (_renderer.material != null)
                    _originalColor = _renderer.material.color;
            }

            // Find player
            var player = GameObject.FindGameObjectWithTag("Player");
            if (player != null)
            {
                playerTransform = player.transform;
            }
            else
            {
                // Try to find CharacterController
                var cc = FindObjectOfType<CharacterController>();
                if (cc != null)
                    playerTransform = cc.transform;
            }
        }

        /// <summary>
        /// Set pickup sounds globally (called by HiddenNodeManager or config).
        /// </summary>
        public static void SetGlobalCollectSounds(AudioClip[] sounds, float volume = 0.7f)
        {
            cachedCollectSounds = sounds;
            cachedCollectVolume = volume;
            soundsCached = true;
        }

        /// <summary>
        /// Set inventory full sound globally (called by HiddenNodeManager or config).
        /// </summary>
        public static void SetGlobalInventoryFullSound(AudioClip sound, float volume = 0.6f, float startTime = 0f)
        {
            cachedInventoryFullSound = sound;
            cachedInventoryFullVolume = volume;
            cachedInventoryFullStartTime = startTime;
        }

        /// <summary>
        /// Play a random collect sound at the specified position.
        /// Uses reduced 3D spatialization for louder, more consistent volume.
        /// </summary>
        private void PlayCollectSound()
        {
            AudioClip[] sounds = (collectSounds != null && collectSounds.Length > 0)
                ? collectSounds
                : cachedCollectSounds;

            if (sounds == null || sounds.Length == 0)
            {
                Debug.LogWarning("[NodePickup] No collect sounds assigned! Assign sounds in HiddenNodeManager's Audio section.");
                return;
            }

            AudioClip clip = sounds[Random.Range(0, sounds.Length)];
            if (clip == null)
            {
                Debug.LogWarning("[NodePickup] Selected collect sound clip is null!");
                return;
            }

            float volume = (collectSounds != null && collectSounds.Length > 0)
                ? collectVolume
                : cachedCollectVolume;

            // Create a temporary AudioSource with boosted settings
            PlaySoundLoud(clip, transform.position, volume);
        }

        /// <summary>
        /// Play a sound with boosted volume (less 3D spatialization = louder).
        /// </summary>
        private static void PlaySoundLoud(AudioClip clip, Vector3 position, float volume)
        {
            GameObject tempGO = new GameObject("TempAudio_Collect");
            tempGO.transform.position = position;

            AudioSource audioSource = tempGO.AddComponent<AudioSource>();
            audioSource.clip = clip;
            audioSource.volume = volume;
            audioSource.spatialBlend = 0.3f; // Mostly 2D (0=2D, 1=3D) - louder and more consistent
            audioSource.rolloffMode = AudioRolloffMode.Linear;
            audioSource.minDistance = 5f;  // Sound stays loud within 5m
            audioSource.maxDistance = 20f;
            audioSource.Play();

            // Destroy after clip finishes
            Destroy(tempGO, clip.length + 0.1f);
        }

        /// <summary>
        /// Play inventory full sound when collection fails due to full inventory.
        /// </summary>
        private void PlayInventoryFullSound()
        {
            Debug.Log($"[NodePickup] PlayInventoryFullSound called. Local clip: {inventoryFullSound}, Cached clip: {cachedInventoryFullSound}");

            AudioClip clip = (inventoryFullSound != null) ? inventoryFullSound : cachedInventoryFullSound;

            if (clip == null)
            {
                Debug.LogWarning("[NodePickup] No inventory full sound assigned!");
                return;
            }

            float volume = (inventoryFullSound != null) ? inventoryFullVolume : cachedInventoryFullVolume;
            float startTime = (inventoryFullSound != null) ? inventoryFullStartTime : cachedInventoryFullStartTime;

            Debug.Log($"[NodePickup] Playing inventory full sound: {clip.name}, volume: {volume}, startTime: {startTime}");
            PlaySoundWithOffset(clip, transform.position, volume, startTime);
        }

        /// <summary>
        /// Play a sound with optional start time offset.
        /// </summary>
        private static void PlaySoundWithOffset(AudioClip clip, Vector3 position, float volume, float startTime)
        {
            // Clamp startTime to valid range
            if (startTime >= clip.length)
            {
                Debug.LogWarning($"[NodePickup] Start time ({startTime}s) is >= clip length ({clip.length}s). Playing from start.");
                startTime = 0f;
            }

            GameObject tempGO = new GameObject("TempAudio_InventoryFull");
            tempGO.transform.position = position;

            AudioSource audioSource = tempGO.AddComponent<AudioSource>();
            audioSource.clip = clip;
            audioSource.volume = volume;
            audioSource.spatialBlend = 0.3f; // Mostly 2D
            audioSource.rolloffMode = AudioRolloffMode.Linear;
            audioSource.minDistance = 5f;
            audioSource.maxDistance = 20f;

            // Set start time if specified
            if (startTime > 0f)
            {
                audioSource.time = startTime;
            }

            audioSource.Play();

            // Destroy after remaining clip finishes
            float remainingLength = Mathf.Max(clip.length - startTime, 0.1f);
            Destroy(tempGO, remainingLength + 0.1f);
        }

        /// <summary>
        /// Initialize the pickup with tier info (legacy).
        /// </summary>
        public void Initialize(int nodeTier, int pickupAmount = 1)
        {
            Initialize(nodeTier, pickupAmount, -1, null, 1);
        }

        /// <summary>
        /// Initialize the pickup with full resource info.
        /// </summary>
        public void Initialize(int nodeTier, int pickupAmount, int resIndex, string resId, int creditVal)
        {
            tier = nodeTier;
            amount = pickupAmount;
            resourceIndex = resIndex;
            resourceId = resId;
            creditValue = creditVal;

            // Update name with resource info
            string displayName = !string.IsNullOrEmpty(resourceId) ? resourceId : $"T{tier}";
            gameObject.name = $"NodePickup_{displayName}";
        }

        private void Update()
        {
            // Fallthrough safety net - if fell below threshold, convert to money
            if (transform.position.y < fallthroughYThreshold)
            {
                ConvertToMoneyAndDestroy();
                return;
            }

            // Freeze physics after settling (3s) — massive perf win for many pickups
            if (!_frozen && rb != null && Time.time - spawnTime > 3f)
            {
                if (HasGroundBelow())
                    FreezePhysics();
            }

            // Periodic ground check while frozen (every ~1s, staggered by instance)
            if (_frozen && Time.frameCount % 60 == (GetInstanceID() & 0x3F))
            {
                if (!HasGroundBelow())
                    UnfreezePhysics();
            }

            // Proximity-based auto-pickup (if enabled)
            if (autoPickup && playerTransform != null && Time.time - spawnTime > autoPickupDelay)
            {
                float distToPlayer = Vector3.Distance(transform.position, playerTransform.position);
                if (distToPlayer <= autoPickupRadius)
                {
                    TryCollect();
                }
            }
        }

        private void OnTriggerEnter(Collider other)
        {
            HandleTrigger(other);
        }

        /// <summary>
        /// Called by child PickupTriggerForwarder when player enters trigger.
        /// </summary>
        public void OnChildTriggerEnter(Collider other)
        {
            HandleTrigger(other);
        }

        private void HandleTrigger(Collider other)
        {
            if (!autoPickup)
                return;

            if (Time.time - spawnTime < autoPickupDelay)
                return;

            // Check if player
            if (other.CompareTag("Player") || other.GetComponentInParent<CharacterController>() != null)
            {
                TryCollect();
            }
        }

        /// <summary>
        /// Get interaction text for IInteractable.
        /// </summary>
        public string GetInteractionText()
        {
            return $"[E] Collect {GetDisplayName()} (x{amount})";
        }

        /// <summary>
        /// Handle interaction.
        /// </summary>
        public void Interact(GameObject interactor)
        {
            TryCollect();
        }

        public void OnHoverEnter() { }
        public void OnHoverExit() { }

        /// <summary>
        /// Try to collect this pickup.
        /// </summary>
        public bool TryCollect()
        {
            var inventory = InventorySystem.Instance;
            if (inventory == null)
            {
                Debug.LogWarning("[NodePickup] No InventorySystem found.");
                return false;
            }

            // Map resource ID to inventory resource type
            ResourceType resourceType;
            if (!string.IsNullOrEmpty(resourceId))
            {
                resourceType = resourceId.ToLower() switch
                {
                    "stone" => ResourceType.Stone,
                    "iron" => ResourceType.IronOre,
                    "copper" => ResourceType.Copper,
                    "coal" => ResourceType.Coal,
                    "silver" => ResourceType.Silver,
                    "gold" => ResourceType.Gold,
                    _ => ResourceType.Dirt
                };
            }
            else
            {
                // Legacy: Map tier to resource type
                resourceType = tier switch
                {
                    1 => ResourceType.Coal,       // T1 = common coal-like
                    2 => ResourceType.IronOre,    // T2 = iron-like
                    3 => ResourceType.Silver,     // T3 = silver-like
                    4 => ResourceType.Gold,       // T4 = gold-like
                    _ => ResourceType.Dirt
                };
            }

            // Try to add to inventory
            bool added = inventory.AddResource(resourceType, amount);

            if (added)
            {
                // Play collect sound before destroying
                PlayCollectSound();

                // Success - destroy pickup
                Destroy(gameObject);
                return true;
            }
            else
            {
                // Inventory full - play sound and flash the pickup
                PlayInventoryFullSound();
                StartCoroutine(FlashInventoryFull());
                return false;
            }
        }

        private bool HasGroundBelow()
        {
            return Physics.Raycast(transform.position + Vector3.up * 0.1f, Vector3.down, 0.5f);
        }

        private void FreezePhysics()
        {
            if (_frozen) return;
            rb.isKinematic = true;
            _frozen = true;
        }

        private void UnfreezePhysics()
        {
            if (!_frozen || rb == null) return;
            rb.isKinematic = false;
            _frozen = false;
            // Will re-freeze after landing (3s check uses spawnTime, so reset it)
            spawnTime = Time.time;
        }

        /// <summary>
        /// Safety net: Convert this pickup's value to money when it falls through terrain.
        /// </summary>
        private void ConvertToMoneyAndDestroy()
        {
            // Get sell value from ResourceSystemConfig using resourceId
            int sellValue = GetCreditValueFromConfig() * amount;

            // Fallback to creditValue if config lookup failed
            if (sellValue <= 0 && creditValue > 0)
            {
                sellValue = creditValue * amount;
            }

            // Final fallback based on tier
            if (sellValue <= 0)
            {
                sellValue = GetFallbackSellValue() * amount;
            }

            if (sellValue > 0)
            {
                var currencyManager = CurrencyManager.Instance;
                if (currencyManager != null)
                {
                    currencyManager.Add(sellValue);
                    Debug.Log($"[NodePickup SafetyNet] {resourceId ?? $"T{tier}"} x{amount} fell through terrain - auto-converted to {sellValue} credits");
                }
                else
                {
                    Debug.LogWarning($"[NodePickup SafetyNet] Resource fell through but CurrencyManager not found!");
                }
            }

            Destroy(gameObject);
        }

        /// <summary>
        /// Get credit value from ResourceSystemConfig using resourceId.
        /// </summary>
        private int GetCreditValueFromConfig()
        {
            if (string.IsNullOrEmpty(resourceId))
                return 0;

            var config = Resources.Load<ResourceSystemConfig>("ResourceSystemConfig");
            if (config == null)
                return 0;

            var resourceDef = config.GetResourceById(resourceId);
            if (resourceDef == null)
                return 0;

            return resourceDef.creditValuePerUnit;
        }

        /// <summary>
        /// Get fallback sell value based on tier when config and creditValue are not available.
        /// </summary>
        private int GetFallbackSellValue()
        {
            return tier switch
            {
                1 => 5,   // Common
                2 => 10,  // Uncommon
                3 => 25,  // Rare
                4 => 50,  // Epic
                _ => 5
            };
        }

        private System.Collections.IEnumerator FlashInventoryFull()
        {
            var renderer = GetComponent<Renderer>();
            if (renderer == null || renderer.material == null)
                yield break;

            Color originalColor = renderer.material.color;

            for (int i = 0; i < 3; i++)
            {
                renderer.material.color = Color.red;
                yield return new WaitForSeconds(0.1f);
                renderer.material.color = originalColor;
                yield return new WaitForSeconds(0.1f);
            }
        }

        // ===== IPullablePickup Implementation =====

        /// <summary>
        /// IPullablePickup: Check if this pickup CAN be collected (inventory has room).
        /// </summary>
        public bool CanPickup()
        {
            var inventory = InventorySystem.Instance;
            if (inventory == null)
                return false;

            // Check if there's at least one empty slot
            var stacks = inventory.GetAllStacks();
            foreach (var stack in stacks)
            {
                if (stack.IsEmpty)
                    return true;
            }

            return false;
        }

        /// <summary>
        /// IPullablePickup: Attempt to pick up this node fragment.
        /// </summary>
        public bool TryPickup()
        {
            return TryCollect();
        }

        /// <summary>
        /// IPullablePickup: Get display name for magnet UI.
        /// Shows actual resource name based on resource ID or tier.
        /// </summary>
        public string GetDisplayName()
        {
            // Use resource ID if available
            if (!string.IsNullOrEmpty(resourceId))
            {
                // Capitalize first letter
                return char.ToUpper(resourceId[0]) + resourceId.Substring(1);
            }

            // Legacy: use tier-based names
            string resourceName = tier switch
            {
                1 => "Coal",
                2 => "Iron Ore",
                3 => "Silver",
                4 => "Gold",
                _ => "Resource"
            };
            return resourceName;
        }

        /// <summary>
        /// IPullablePickup: Set visual highlight when aimed at.
        /// </summary>
        public void SetHighlighted(bool highlighted)
        {
            if (_renderer == null)
                return;

            if (highlighted && !_isHighlighted)
            {
                _isHighlighted = true;
                var mat = _renderer.material;
                Color newColor = _originalColor * 1.3f; // Brighten
                newColor.a = _originalColor.a;
                mat.color = newColor;

                // Enable emission for glow effect
                mat.EnableKeyword("_EMISSION");
                mat.SetColor("_EmissionColor", _originalColor * 0.5f);
            }
            else if (!highlighted && _isHighlighted)
            {
                _isHighlighted = false;
                var mat = _renderer.material;
                mat.color = _originalColor;
                mat.SetColor("_EmissionColor", Color.black);
            }
        }
    }
}
