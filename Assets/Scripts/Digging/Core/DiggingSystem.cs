using UnityEngine;
using System;
using BeneathTheFloor.Machines;
using BeneathTheFloor.Energy;
using BeneathTheFloor.Player;
using BeneathTheFloor.Tools;
using BeneathTheFloor.UI;

namespace BeneathTheFloor.Digging
{
    /// <summary>
    /// Player-facing digging controller for the unified digging system.
    /// Handles input, raycasting, and coordinates with ChunkManager for dig operations.
    ///
    /// Integration:
    /// - Attach to the player or a dedicated DiggingSystem GameObject
    /// - Requires a ChunkManager in the scene
    /// - Subscribe to OnDigCompleted for resource spawning, feedback, etc.
    /// </summary>
    public class DiggingSystem : MonoBehaviour
    {
        [Header("Dig Parameters")]
        [Tooltip("Base radius of the dig sphere in meters.")]
        [SerializeField] private float digRadius = 0.5f;

        [Tooltip("Dig strength per operation (0-1).")]
        [SerializeField] private float digStrength = 0.8f;

        [Tooltip("Maximum digs per second.")]
        [SerializeField] private float digsPerSecond = 2f;  // ~0.5s between digs

        [Header("Raycast Settings")]
        [Tooltip("Maximum distance for dig raycast.")]
        [SerializeField] private float maxDigDistance = 4f;

        [Tooltip("Layers to hit with dig raycast.")]
        [SerializeField] private LayerMask digLayerMask = -1; // All layers

        [Header("Input")]
        [Tooltip("Input button name for digging (default: Fire1 = left mouse)")]
        [SerializeField] private string digInputName = "Fire1";

        [Tooltip("Use continuous digging while holding button.")]
        [SerializeField] private bool continuousDigging = false;  // Disabled - require individual clicks

        [Header("References")]
        [Tooltip("Camera for raycasting. If null, uses Camera.main.")]
        [SerializeField] private Camera playerCamera;

        [Tooltip("ChunkManager reference. If null, finds via singleton.")]
        [SerializeField] private ChunkManager chunkManager;

        [Header("Tool Gating")]
        [Tooltip("Require a valid dig tool to be equipped before digging. MUST be true for gameplay.")]
        [SerializeField] private bool requireDigTool = true;

        [Tooltip("Allow digging with no tool provider set (for testing only). MUST be false for gameplay.")]
        [SerializeField] private bool allowDigWithoutProvider = false;

        [Header("Tool Max Depths (by tool index, not tier)")]
        [Tooltip("Max dig depth for Tool 1 (Shovel) - all tiers")]
        [SerializeField] private float tool1MaxDepth = 15f;
        [Tooltip("Max dig depth for Tool 2 (Heavy Spade) - all tiers")]
        [SerializeField] private float tool2MaxDepth = 22f;
        [Tooltip("Max dig depth for Tool 3 (Pickaxe) - all tiers")]
        [SerializeField] private float tool3MaxDepth = 30f;
        [Tooltip("Max dig depth for Tool 4 (Drill Pike) - all tiers")]
        [SerializeField] private float tool4MaxDepth = 50f;

        [Header("Audio")]
        [Tooltip("Dig hit sounds (plays randomly on each dig).")]
        [SerializeField] private AudioClip[] digHitSounds;

        [Tooltip("Sound when dig is blocked (depth limit, no tool, etc).")]
        [SerializeField] private AudioClip blockedSound;

        [Tooltip("Volume range for dig sounds.")]
        [SerializeField] [Range(0f, 1f)] private float minVolume = 0.7f;
        [SerializeField] [Range(0f, 1f)] private float maxVolume = 1f;

        [Tooltip("Pitch variation for variety.")]
        [SerializeField] [Range(0f, 0.3f)] private float pitchVariation = 0.1f;

        [Header("Debug")]
        [SerializeField] private bool enableDebugLogs = false;

        [Tooltip("Disable energy consumption for testing (infinite energy).")]
        [SerializeField] private bool disableEnergyConsumption = false;

        [Header("TEST MODE - For Development Only")]
        [Tooltip("Enable test mode to use override values below")]
        [SerializeField] private bool enableTestMode = false;

        [Tooltip("Override dig radius (only when Test Mode enabled)")]
        [SerializeField] private float testDigRadius = 2.0f;

        [Tooltip("Override dig strength (only when Test Mode enabled)")]
        [SerializeField] private float testDigStrength = 5.0f;

        [Tooltip("Add credits when entering play mode (only when Test Mode enabled)")]
        [SerializeField] private int testCreditsToAdd = 15000;

        [Tooltip("Set to true to add credits on next frame, then auto-resets")]
        [SerializeField] private bool addTestCreditsNow = false;

        // State
        private float _lastDigTime;
        private bool _isDigging;
        private Vector3 _lastHitPoint;
        private bool _lastHitValid;
        private Collider _lastHitCollider; // For falling chunk detection

        // Drill Pike charged attack state
        private bool _isHoldingDigButton = false;
        private float _holdStartTime = 0f;
        private const float CHARGE_THRESHOLD = 0.2f; // Min hold time to trigger charged attack

        // Tool provider
        private IDigToolProvider _toolProvider;
        private bool _lastToolCheckFailed;

        // Player root transform (for raycast exclusion)
        private Transform _playerRoot;

        // Audio
        private AudioSource _audioSource;

        // Events
        /// <summary>
        /// Fired when a dig operation completes successfully.
        /// </summary>
        public event Action<DigResult> OnDigCompleted;

        /// <summary>
        /// Fired when a dig attempt fails (missed, protected zone, etc.).
        /// </summary>
        public event Action<Vector3> OnDigFailed;

        /// <summary>
        /// Fired when dig is blocked because no valid tool is equipped.
        /// </summary>
        public event Action OnDigBlockedNoTool;

        /// <summary>
        /// Fired when dig is blocked due to depth limit.
        /// </summary>
        public event Action<float, float> OnDigBlockedDepthLimit; // (currentDepth, maxDepth)

        // Singleton
        public static DiggingSystem Instance { get; private set; }

        // Public accessors
        public float DigRadius => digRadius;
        public float DigStrength => digStrength;
        public bool IsDigging => _isDigging;
        public Vector3 LastHitPoint => _lastHitPoint;
        public bool LastHitValid => _lastHitValid;

        #region Unity Lifecycle

        private void Awake()
        {
            // Singleton setup
            if (Instance != null && Instance != this)
            {
                Destroy(gameObject);
                return;
            }
            Instance = this;
        }

        private void Start()
        {
            // RUNTIME OVERRIDE: Fix scene-serialized values that might be wrong
            if (digStrength <= 0.01f) digStrength = 0.8f;
            if (digRadius <= 0.1f) digRadius = 0.5f;
            if (digsPerSecond <= 0.1f) digsPerSecond = 2.0f;
            if (continuousDigging) continuousDigging = false;
            if (digLayerMask == 0) digLayerMask = -1;

            // Get camera reference
            if (playerCamera == null)
            {
                playerCamera = Camera.main;
                if (playerCamera == null)
                {
                    enabled = false;
                    return;
                }
            }

            // Cache player root transform for raycast exclusion
            // Walk up the hierarchy from camera to find the root player object
            _playerRoot = playerCamera.transform;
            while (_playerRoot.parent != null)
            {
                _playerRoot = _playerRoot.parent;
            }

            // Get ChunkManager reference
            if (chunkManager == null)
            {
                chunkManager = ChunkManager.Instance;
                if (chunkManager == null)
                {
                    chunkManager = FindObjectOfType<ChunkManager>();
                }
            }

            if (chunkManager == null)
            {
                enabled = false;
                return;
            }

            // Subscribe to ChunkManager events
            chunkManager.OnDigCompleted += HandleChunkManagerDigComplete;

            // Setup audio source
            _audioSource = gameObject.AddComponent<AudioSource>();
            _audioSource.playOnAwake = false;
            _audioSource.spatialBlend = 0f; // 2D sound

            if (enableDebugLogs)
                Debug.Log("[DiggingSystem] Initialized successfully.");

            // Ensure demo depth limit trigger exists
            GameFlow.DemoDepthLimitTrigger.EnsureExists();

            // TEST MODE: Add credits on start
            if (enableTestMode && testCreditsToAdd > 0)
            {
                StartCoroutine(AddTestCreditsDelayed());
            }
        }

        private System.Collections.IEnumerator AddTestCreditsDelayed()
        {
            yield return new WaitForSeconds(0.5f); // Wait for CurrencyManager to initialize
            if (Economy.CurrencyManager.Instance != null)
            {
                Economy.CurrencyManager.Instance.Add(testCreditsToAdd);
            }
        }

        private void Update()
        {
            // TEST MODE: Add credits button
            if (addTestCreditsNow)
            {
                addTestCreditsNow = false;
                if (Economy.CurrencyManager.Instance != null)
                {
                    Economy.CurrencyManager.Instance.Add(testCreditsToAdd);
                }
            }

            // Update raycast hit info
            UpdateRaycastHit();

            // Handle dig input
            HandleDigInput();

            // Debug visualization completely disabled
            // Was causing visual line artifacts
        }

        private void OnDestroy()
        {
            if (Instance == this)
            {
                Instance = null;
            }

            // Unsubscribe from events
            if (chunkManager != null)
            {
                chunkManager.OnDigCompleted -= HandleChunkManagerDigComplete;
            }
        }

        #endregion

        #region Input Handling

        /// <summary>
        /// Update raycast hit information.
        /// </summary>
        private void UpdateRaycastHit()
        {
            Ray ray = new Ray(playerCamera.transform.position, playerCamera.transform.forward);

            // Use RaycastAll and filter out hits on player hierarchy
            RaycastHit[] hits = Physics.RaycastAll(ray, maxDigDistance, digLayerMask);

            // Sort by distance (closest first)
            System.Array.Sort(hits, (a, b) => a.distance.CompareTo(b.distance));

            // Find first hit that's a valid terrain chunk (NOT player, NOT other objects)
            _lastHitValid = false;
            _lastHitCollider = null;
            foreach (var hit in hits)
            {
                // Check if this hit is part of the player hierarchy
                if (IsPartOfPlayerHierarchy(hit.collider.transform))
                {
                    continue; // Skip player hits
                }

                // Check if this hit is a terrain chunk
                if (!IsTerrainChunk(hit.collider))
                {
                    continue; // Skip non-terrain hits
                }

                // Found a valid terrain hit
                _lastHitPoint = hit.point;
                _lastHitCollider = hit.collider;
                _lastHitValid = true;

                // DEBUG: Log what we hit (throttled to once per second)
                if (enableDebugLogs && Time.frameCount % 60 == 0)
                {
                    string hitObjName = hit.collider?.gameObject?.name ?? "null";
                    string parentName = hit.collider?.transform?.parent?.name ?? "no parent";
                    Debug.Log($"[DiggingSystem] Raycast hit: {hitObjName} (parent: {parentName}) at {hit.point}");
                }
                break;
            }
        }

        /// <summary>
        /// Check if a collider belongs to a terrain chunk.
        /// Terrain chunks are named "Chunk_X,Y,Z" and their parent is "Chunks".
        /// </summary>
        private bool IsTerrainChunk(Collider collider)
        {
            if (collider == null) return false;

            // Check if the object name starts with "Chunk_"
            string objName = collider.gameObject.name;
            if (objName.StartsWith("Chunk_"))
            {
                return true;
            }

            // Also check parent - ChunkManager creates "Chunks" parent
            Transform parent = collider.transform.parent;
            if (parent != null && parent.name == "Chunks")
            {
                return true;
            }

            return false;
        }

        /// <summary>
        /// Check if a transform is part of the player's hierarchy.
        /// </summary>
        private bool IsPartOfPlayerHierarchy(Transform t)
        {
            if (_playerRoot == null) return false;

            // Walk up the hierarchy to see if we find the player root
            Transform current = t;
            while (current != null)
            {
                if (current == _playerRoot)
                    return true;
                current = current.parent;
            }
            return false;
        }

        /// <summary>
        /// Handle dig input from player.
        /// </summary>
        private void HandleDigInput()
        {
            // Don't process dig input if any UI is open
            if (UI.UIState.IsAnyUIOpen)
            {
                // Cancel charging if UI opens
                if (_isHoldingDigButton)
                {
                    CancelCharging();
                }
                return;
            }

            // Don't dig while in lamp placement mode
            if (Lighting.LampPlacementController.Instance != null &&
                Lighting.LampPlacementController.Instance.IsPlacementMode)
            {
                if (_isHoldingDigButton)
                {
                    CancelCharging();
                }
                return;
            }

            bool digButtonDown = Input.GetButtonDown(digInputName);
            bool digButtonHeld = Input.GetButton(digInputName);
            bool digButtonUp = Input.GetButtonUp(digInputName);

            // Check if current tool supports charging (Drill Pike - Tool 4)
            bool supportsCharging = HeldToolController.Instance != null && HeldToolController.Instance.SupportsCharging();

            if (supportsCharging)
            {
                // DRILL PIKE: Handle charged attack input
                HandleDrillPikeInput(digButtonDown, digButtonHeld, digButtonUp);
            }
            else
            {
                // OTHER TOOLS: Single click dig
                if (digButtonDown)
                {
                    TryDig();
                }
                else if (!digButtonHeld)
                {
                    _isDigging = false;
                }
            }
        }

        /// <summary>
        /// Handle input specifically for Drill Pike's charged attack.
        /// </summary>
        private void HandleDrillPikeInput(bool digPressed, bool digHeld, bool digReleased)
        {
            // Button just pressed - start holding
            if (digPressed)
            {
                _isHoldingDigButton = true;
                _holdStartTime = Time.time;

                // Start charging animation
                if (HeldToolController.Instance != null)
                {
                    HeldToolController.Instance.StartCharging();
                }

                if (enableDebugLogs)
                    Debug.Log("[DiggingSystem] Drill Pike: Started charging");
            }

            // Button released - decide between charged attack or normal dig
            if (digReleased && _isHoldingDigButton)
            {
                float holdDuration = Time.time - _holdStartTime;
                _isHoldingDigButton = false;

                if (enableDebugLogs)
                    Debug.Log($"[DiggingSystem] Drill Pike: Button released after {holdDuration:F2}s (threshold={CHARGE_THRESHOLD}s)");

                if (holdDuration >= CHARGE_THRESHOLD)
                {
                    // Charged attack - release the super hit
                    if (HeldToolController.Instance != null)
                    {
                        HeldToolController.Instance.ReleaseChargedAttack();
                    }

                    // Execute dig with charge bonus
                    float chargePower = HeldToolController.Instance?.GetChargeProgress() ?? 0f;
                    TryChargedDig(chargePower);

                    if (enableDebugLogs)
                        Debug.Log($"[DiggingSystem] Drill Pike: Released charged attack! Power: {chargePower:F2}");
                }
                else
                {
                    // Quick tap - normal dig
                    if (HeldToolController.Instance != null)
                    {
                        HeldToolController.Instance.CancelCharging();
                    }

                    TryDig();

                    if (enableDebugLogs)
                        Debug.Log("[DiggingSystem] Drill Pike: Quick tap - normal dig");
                }
            }

            // If we're no longer holding but state says we are (edge case), cancel
            if (!digHeld && _isHoldingDigButton)
            {
                CancelCharging();
            }
        }

        /// <summary>
        /// Cancel any ongoing charging.
        /// </summary>
        private void CancelCharging()
        {
            if (_isHoldingDigButton)
            {
                _isHoldingDigButton = false;
                if (HeldToolController.Instance != null)
                {
                    HeldToolController.Instance.CancelCharging();
                }

                if (enableDebugLogs)
                    Debug.Log("[DiggingSystem] Drill Pike: Charging cancelled");
            }
        }

        /// <summary>
        /// Attempt a charged dig (for Drill Pike super hit).
        /// </summary>
        private void TryChargedDig(float chargePower)
        {
            // Validate tool
            if (!ValidateDigTool())
                return;

            // Check if we have a valid hit
            if (!_lastHitValid)
            {
                OnDigFailed?.Invoke(playerCamera.transform.position + playerCamera.transform.forward * maxDigDistance);
                return;
            }

            // Execute charged dig with bonus
            ExecuteChargedDig(_lastHitPoint, chargePower);
        }

        /// <summary>
        /// Execute a charged dig with bonus power.
        /// </summary>
        private void ExecuteChargedDig(Vector3 worldPosition, float chargePower)
        {
            if (chunkManager == null)
            {
                Debug.LogError("[DiggingSystem] Cannot dig - ChunkManager is null!");
                return;
            }

            // Check tool depth limit
            float basementFloorY = DepthManager.Instance?.BasementFloorY ?? -3f;
            float depthAtPosition = basementFloorY - worldPosition.y;

            int toolIndex = HeldToolController.Instance?.CurrentToolIndex ?? 0;
            float maxDepth = toolIndex switch
            {
                0 => tool1MaxDepth,
                1 => tool2MaxDepth,
                2 => tool3MaxDepth,
                3 => tool4MaxDepth,
                _ => tool1MaxDepth
            };

            if (maxDepth > 0f && depthAtPosition > maxDepth)
            {
                string toolName = toolIndex switch { 0 => "Shovel", 1 => "Heavy Spade", 2 => "Pickaxe", 3 => "Drill Pike", _ => "Tool" };
                if (enableDebugLogs)
                    Debug.Log($"[DiggingSystem] DEPTH BLOCKED: {toolName} cannot dig at {depthAtPosition:F1}m (max={maxDepth}m)");
                PlayBlockedSound();
                OnDigBlockedDepthLimit?.Invoke(depthAtPosition, maxDepth);
                return;
            }

            // Calculate charge bonus: 1.0x to 3.0x based on charge
            float chargeBonus = 1f + chargePower * 2f;

            // Get tool multipliers
            float radiusMultiplier = 1f;
            float strengthMultiplier = 1f;
            if (_toolProvider != null)
            {
                DigToolInfo? toolInfo = _toolProvider.GetCurrentDigTool();
                if (toolInfo.HasValue)
                {
                    radiusMultiplier = toolInfo.Value.RadiusMultiplier;
                    strengthMultiplier = toolInfo.Value.StrengthMultiplier;
                }
            }

            // Apply upgrade multipliers and charge bonus
            float tierMult = UpgradeStation.ToolTierMultiplier;
            radiusMultiplier *= UpgradeStation.ToolRadiusMultiplier * tierMult * (1f + chargePower * 0.5f); // Slight radius boost
            strengthMultiplier *= UpgradeStation.ToolPowerMultiplier * tierMult * chargeBonus; // Full charge bonus to power

            float effectiveRadius = digRadius * radiusMultiplier;
            float effectiveStrength = digStrength * strengthMultiplier;

            var operation = new DigOperation(worldPosition, effectiveRadius, effectiveStrength);

            // Execute FIRST dig
            DigResult mainResult = chunkManager.ExecuteDig(operation);

            // Force immediate collider update for super hit to prevent fall-through
            chunkManager.FlushDirtyChunksWithColliders();

            _lastDigTime = Time.time;
            _isDigging = mainResult.Success;

            if (mainResult.Success)
            {
                PlayDigSound();

                _toolProvider?.OnDigPerformed(mainResult);
                OnDigCompleted?.Invoke(mainResult);

                // Get super hit count from UpgradeStation (default 2)
                int totalHits = UpgradeStation.SuperHitCount;

                // Start coroutine for additional hits (already did 1st hit)
                if (totalHits > 1)
                {
                    StartCoroutine(MultiHitCoroutine(worldPosition, effectiveRadius, effectiveStrength, chargePower, totalHits - 1));
                }

                if (enableDebugLogs)
                    Debug.Log($"[DiggingSystem] Charged attack - hit 1/{totalHits}! Power: {chargeBonus:F2}x");
            }
            else
            {
                OnDigFailed?.Invoke(worldPosition);
            }
        }

        /// <summary>
        /// Coroutine for multi-hit super attack (Drill Pike).
        /// Executes additional hits after the first one.
        /// </summary>
        private System.Collections.IEnumerator MultiHitCoroutine(Vector3 firstHitPos, float radius, float strength, float chargePower, int remainingHits)
        {
            Vector3 forward = playerCamera != null ? playerCamera.transform.forward : Vector3.down;
            Vector3 hitPos = firstHitPos;

            for (int i = 0; i < remainingHits; i++)
            {
                // Small delay between hits
                yield return new WaitForSeconds(0.12f);

                // Move position forward for next hit (drilling deeper)
                hitPos += forward * (radius * 0.4f);

                // Execute hit
                var hitOperation = new DigOperation(hitPos, radius, strength);
                DigResult hitResult = chunkManager.ExecuteDig(hitOperation);

                // Force immediate collider update to prevent fall-through between hits
                chunkManager.FlushDirtyChunksWithColliders();

                if (hitResult.Success)
                {
                    PlayDigSound();
                    _toolProvider?.OnDigPerformed(hitResult);
                    OnDigCompleted?.Invoke(hitResult);

                    if (enableDebugLogs)
                        Debug.Log($"[DiggingSystem] Charged attack - hit {i + 2}/{remainingHits + 1}");
                }
            }

            // Trigger screen shake after final hit for impact
            TriggerScreenShake(0.15f + remainingHits * 0.03f, 0.08f + remainingHits * 0.02f);

            // Consume extra energy for charged attack (scales with hits)
            if (!disableEnergyConsumption && EnergyManager.Instance != null)
            {
                int energyCost = Mathf.CeilToInt(1f + chargePower * remainingHits);
                for (int i = 0; i < energyCost; i++)
                {
                    EnergyManager.Instance.TryConsumeDigEnergy();
                }
            }

            if (enableDebugLogs)
                Debug.Log($"[DiggingSystem] Charged attack complete - {remainingHits + 1} total hits!");
        }

        /// <summary>
        /// Trigger a small screen shake effect.
        /// </summary>
        private void TriggerScreenShake(float duration, float intensity)
        {
            if (playerCamera != null)
            {
                StartCoroutine(ScreenShakeCoroutine(duration, intensity));
            }
        }

        /// <summary>
        /// Screen shake coroutine - subtle camera shake.
        /// </summary>
        private System.Collections.IEnumerator ScreenShakeCoroutine(float duration, float intensity)
        {
            Vector3 originalLocalPos = playerCamera.transform.localPosition;
            float elapsed = 0f;

            while (elapsed < duration)
            {
                elapsed += Time.deltaTime;
                float progress = elapsed / duration;

                // Intensity decreases over time
                float currentIntensity = intensity * (1f - progress);

                // Random offset
                float offsetX = UnityEngine.Random.Range(-currentIntensity, currentIntensity);
                float offsetY = UnityEngine.Random.Range(-currentIntensity, currentIntensity);

                playerCamera.transform.localPosition = originalLocalPos + new Vector3(offsetX, offsetY, 0f);

                yield return null;
            }

            // Reset to original position
            playerCamera.transform.localPosition = originalLocalPos;
        }

        #endregion

        #region Digging

        /// <summary>
        /// Attempt to dig at current aim point.
        ///
        /// TOOL GATING: This method will block if:
        /// - requireDigTool is true AND no tool provider is set (unless allowDigWithoutProvider)
        /// - requireDigTool is true AND tool provider reports no valid tool equipped
        /// </summary>
        public void TryDig()
        {
            // DEBUG: Log dig attempt
            if (enableDebugLogs)
            {
                Debug.Log($"[DiggingSystem] TryDig called - requireDigTool={requireDigTool}, _toolProvider={(_toolProvider != null ? "SET" : "NULL")}, _lastHitValid={_lastHitValid}");
            }

            // TOOL GATING CHECK (before any other logic)
            if (!ValidateDigTool())
            {
                // Already logged in ValidateDigTool
                return;
            }

            // ENERGY CHECK - block dig if not enough energy (skip if debug toggle enabled)
            if (!disableEnergyConsumption && EnergyManager.Instance != null && !EnergyManager.Instance.HasEnergyToDig())
            {
                if (enableDebugLogs)
                    Debug.Log("[DiggingSystem] DIG BLOCKED: Not enough energy");

                // Show energy drink hint
                if (EnergyUI.Instance != null)
                {
                    EnergyUI.Instance.ShowEnergyDrinkHint();
                }
                // Also highlight the drink in consumables HUD
                if (ConsumablesHUD.Instance != null)
                {
                    ConsumablesHUD.Instance.HighlightDrink();
                }
                return;
            }

            // Check cooldown (apply speed upgrade multiplier + tier multiplier)
            float timeSinceLastDig = Time.time - _lastDigTime;
            float effectiveDigsPerSecond = digsPerSecond * UpgradeStation.ToolSpeedMultiplier * UpgradeStation.ToolTierMultiplier;
            float digCooldown = 1f / effectiveDigsPerSecond;

            if (timeSinceLastDig < digCooldown)
            {
                return;
            }

            // Check if we have a valid hit
            if (!_lastHitValid)
            {
                OnDigFailed?.Invoke(playerCamera.transform.position + playerCamera.transform.forward * maxDigDistance);
                return;
            }

            // Execute dig (energy consumed only on success inside ExecuteDig)
            ExecuteDig(_lastHitPoint);
        }

        /// <summary>
        /// Validate that the player has a valid digging tool equipped.
        /// Returns true if digging is allowed.
        /// </summary>
        private bool ValidateDigTool()
        {
            // Skip validation if tool gating is disabled
            if (!requireDigTool)
            {
                return true;
            }

            // Check if we have a tool provider
            if (_toolProvider == null)
            {
                if (allowDigWithoutProvider)
                {
                    // Testing mode - allow without provider
                    return true;
                }

                // No provider = no digging
                if (!_lastToolCheckFailed)
                {
                    _lastToolCheckFailed = true;
                    PlayBlockedSound();
                    OnDigBlockedNoTool?.Invoke();
                }
                return false;
            }

            // Ask the tool provider if we have a valid dig tool
            if (!_toolProvider.HasValidDigTool())
            {
                if (!_lastToolCheckFailed)
                {
                    _lastToolCheckFailed = true;
                    PlayBlockedSound();
                    OnDigBlockedNoTool?.Invoke();
                }
                return false;
            }

            // Tool is valid - reset the failed flag
            _lastToolCheckFailed = false;
            return true;
        }

        /// <summary>
        /// Execute a dig at the specified world position.
        /// </summary>
        public void ExecuteDig(Vector3 worldPosition)
        {
            if (chunkManager == null)
            {
                Debug.LogError("[DiggingSystem] Cannot dig - ChunkManager is null!");
                return;
            }

            // Check tool depth limit - THIS IS THE PRIMARY RESTRICTION
            // Within the tool's depth limit, it can dig ANY layer freely
            // Beyond the depth limit, digging is completely blocked
            float basementFloorY = DepthManager.Instance?.BasementFloorY ?? -3f;
            float depthAtPosition = basementFloorY - worldPosition.y;

            // Get tool's max depth based on TOOL INDEX (not tier)
            // Tool 1 (Shovel): 15m, Tool 2 (Heavy Spade): 22m, Tool 3 (Pickaxe): 30m
            int toolIndex = HeldToolController.Instance?.CurrentToolIndex ?? 0;
            float maxDepth = toolIndex switch
            {
                0 => tool1MaxDepth,  // Tool 1: Shovel - 15m
                1 => tool2MaxDepth,  // Tool 2: Heavy Spade - 22m
                2 => tool3MaxDepth,  // Tool 3: Pickaxe - 30m
                3 => tool4MaxDepth,  // Tool 4: Drill Pike - 50m
                _ => tool1MaxDepth
            };

            if (enableDebugLogs)
            {
                string toolName = toolIndex switch { 0 => "Shovel", 1 => "Heavy Spade", 2 => "Pickaxe", 3 => "Drill Pike", _ => "Tool" };
                Debug.Log($"[DiggingSystem] Tool {toolIndex + 1} ({toolName}), maxDepth: {maxDepth}m, current depth: {depthAtPosition:F1}m");
            }

            // DEPTH LIMIT CHECK - Only restriction that matters
            if (maxDepth > 0f && depthAtPosition > maxDepth)
            {
                string toolName = toolIndex switch { 0 => "Shovel", 1 => "Heavy Spade", 2 => "Pickaxe", _ => "Tool" };
                if (enableDebugLogs)
                {
                    Debug.Log($"[DiggingSystem] DEPTH BLOCKED: {toolName} cannot dig at {depthAtPosition:F1}m (max={maxDepth}m)");
                }
                PlayBlockedSound();
                OnDigBlockedDepthLimit?.Invoke(depthAtPosition, maxDepth);
                return;
            }

            // Within depth limit - tool can dig freely!
            // Layer hardness does NOT block digging within the tool's allowed depth
            // (Layer colors are still visible for visual variety)

            // Get tool multipliers if available
            float radiusMultiplier = 1f;
            float strengthMultiplier = 1f;
            if (_toolProvider != null)
            {
                DigToolInfo? toolInfo = _toolProvider.GetCurrentDigTool();
                if (toolInfo.HasValue)
                {
                    radiusMultiplier = toolInfo.Value.RadiusMultiplier;
                    strengthMultiplier = toolInfo.Value.StrengthMultiplier;
                }
            }

            // Apply upgrade station multipliers (stacks with tool multipliers)
            // Tier multiplier applies to all stats as a global bonus
            float tierMult = UpgradeStation.ToolTierMultiplier;
            radiusMultiplier *= UpgradeStation.ToolRadiusMultiplier * tierMult;
            strengthMultiplier *= UpgradeStation.ToolPowerMultiplier * tierMult;

            // Create dig operation with tool modifiers
            float effectiveRadius;
            float effectiveStrength;

            // TEST MODE: Use override values
            if (enableTestMode)
            {
                effectiveRadius = testDigRadius;
                effectiveStrength = testDigStrength;
            }
            else
            {
                effectiveRadius = digRadius * radiusMultiplier;
                effectiveStrength = digStrength * strengthMultiplier;
            }

            var operation = new DigOperation(worldPosition, effectiveRadius, effectiveStrength);

            // Check if we hit a falling terrain chunk - skip digging into falling debris
            if (_lastHitCollider != null)
            {
                var fallingChunk = _lastHitCollider.GetComponent<FallingTerrainChunk>();
                if (fallingChunk != null)
                {
                    if (enableDebugLogs)
                        Debug.Log("[DiggingSystem] Hit falling chunk - cannot dig into falling debris");
                    return;
                }
            }

            // Execute via ChunkManager (normal terrain)
            DigResult mainResult = chunkManager.ExecuteDig(operation);

            // Update state
            _lastDigTime = Time.time;
            _isDigging = mainResult.Success;

            if (mainResult.Success)
            {
                // Play dig sound
                PlayDigSound();

                // Consume energy only on successful dig (skip if debug toggle enabled)
                if (!disableEnergyConsumption && EnergyManager.Instance != null)
                {
                    EnergyManager.Instance.TryConsumeDigEnergy();
                }

                if (enableDebugLogs)
                    Debug.Log($"[DiggingSystem] Dig success: {mainResult}");

                // Notify tool provider (for durability, animations, etc.)
                _toolProvider?.OnDigPerformed(mainResult);

                // Fire local event (distinct from ChunkManager event)
                OnDigCompleted?.Invoke(mainResult);
            }
            else
            {
                if (enableDebugLogs)
                    Debug.Log($"[DiggingSystem] Dig failed at {worldPosition}");

                OnDigFailed?.Invoke(worldPosition);
            }
        }

        /// <summary>
        /// Execute a dig with custom parameters.
        /// </summary>
        public DigResult ExecuteDigCustom(Vector3 worldPosition, float radius, float strength, bool fireEvent = true)
        {
            if (chunkManager == null)
            {
                return DigResult.Failed(new DigOperation(worldPosition, radius, strength));
            }

            var operation = new DigOperation(worldPosition, radius, strength);
            DigResult result = chunkManager.ExecuteDig(operation);
            if (result.Success && fireEvent)
                OnDigCompleted?.Invoke(result);
            return result;
        }

        /// <summary>
        /// Handle dig completion from ChunkManager.
        /// </summary>
        private void HandleChunkManagerDigComplete(DigResult result)
        {
            // This is called for ALL digs (including those from other sources)
            // Add any global handling here
        }

        /// <summary>
        /// Play a random dig hit sound.
        /// </summary>
        private void PlayDigSound()
        {
            if (_audioSource == null || digHitSounds == null || digHitSounds.Length == 0)
                return;

            AudioClip clip = digHitSounds[UnityEngine.Random.Range(0, digHitSounds.Length)];
            if (clip != null)
            {
                _audioSource.pitch = 1f + UnityEngine.Random.Range(-pitchVariation, pitchVariation);
                _audioSource.volume = UnityEngine.Random.Range(minVolume, maxVolume);
                _audioSource.PlayOneShot(clip);
            }
        }

        /// <summary>
        /// Play blocked sound (depth limit, no tool, etc).
        /// </summary>
        private void PlayBlockedSound()
        {
            if (_audioSource == null || blockedSound == null)
                return;

            _audioSource.pitch = 0.8f; // Lower pitch for "denial" feel
            _audioSource.volume = maxVolume;
            _audioSource.PlayOneShot(blockedSound);
        }

        #endregion

        #region Public API

        /// <summary>
        /// Set the tool provider for dig tool validation.
        /// REQUIRED: For gameplay, you MUST set a tool provider or disable requireDigTool.
        /// </summary>
        /// <param name="provider">The IDigToolProvider implementation to use.</param>
        public void SetToolProvider(IDigToolProvider provider)
        {
            _toolProvider = provider;
            _lastToolCheckFailed = false;

            if (enableDebugLogs)
            {
                if (provider != null)
                {
                    Debug.Log($"[DiggingSystem] Tool provider set: {provider.GetType().Name}");
                }
                else
                {
                    Debug.Log("[DiggingSystem] Tool provider cleared (null)");
                }
            }
        }

        /// <summary>
        /// Get the current tool provider.
        /// </summary>
        public IDigToolProvider GetToolProvider()
        {
            return _toolProvider;
        }

        /// <summary>
        /// Check if a valid dig tool is currently equipped.
        /// </summary>
        public bool HasValidDigTool()
        {
            if (!requireDigTool)
                return true;
            if (_toolProvider == null)
                return allowDigWithoutProvider;
            return _toolProvider.HasValidDigTool();
        }

        /// <summary>
        /// Set dig parameters at runtime.
        /// </summary>
        public void SetDigParameters(float radius, float strength, float rate)
        {
            digRadius = Mathf.Max(0.1f, radius);
            digStrength = Mathf.Clamp01(strength);
            digsPerSecond = Mathf.Max(0.1f, rate);
        }

        /// <summary>
        /// Get the IDiggableTerrain interface.
        /// </summary>
        public IDiggableTerrain GetTerrain()
        {
            return chunkManager;
        }

        /// <summary>
        /// Check if a position is diggable.
        /// </summary>
        public bool CanDigAt(Vector3 worldPosition)
        {
            if (chunkManager == null)
                return false;

            // Check bounds
            if (!chunkManager.IsWithinBounds(worldPosition))
                return false;

            // Check density (can't dig air)
            float density = chunkManager.GetDensityAt(worldPosition);
            return density > 0.1f;
        }

        /// <summary>
        /// Get density at a world position.
        /// </summary>
        public float GetDensityAt(Vector3 worldPosition)
        {
            if (chunkManager == null)
                return 1.0f;

            return chunkManager.GetDensityAt(worldPosition);
        }

        /// <summary>
        /// Get the hardness rating of the currently equipped tool.
        /// </summary>
        public float GetCurrentToolHardness()
        {
            // Try to get from PlayerToolVisualController
            if (PlayerToolVisualController.Instance != null)
            {
                DigToolProfile profile = PlayerToolVisualController.Instance.GetCurrentToolProfile();
                if (profile != null)
                {
                    return profile.hardnessRating;
                }
            }

            // Default hardness for Tier 1 tool
            return 1.5f;
        }

        /// <summary>
        /// Get the ID of the currently equipped tool.
        /// </summary>
        public string GetCurrentToolId()
        {
            // Try to get from PlayerToolVisualController
            if (PlayerToolVisualController.Instance != null)
            {
                DigToolProfile profile = PlayerToolVisualController.Instance.GetCurrentToolProfile();
                if (profile != null)
                {
                    return profile.toolId;
                }
            }

            return "default_tool";
        }

        /// <summary>
        /// Reset exhaustion for the current tool (call after upgrade).
        /// </summary>
        public void ResetCurrentToolExhaustion()
        {
            string toolId = GetCurrentToolId();
            if (!string.IsNullOrEmpty(toolId) && ToolEffectivenessTracker.Instance != null)
            {
                ToolEffectivenessTracker.Instance.ResetToolExhaustion(toolId);
            }
        }

        /// <summary>
        /// Check if current tool is exhausted.
        /// </summary>
        public bool IsCurrentToolExhausted()
        {
            string toolId = GetCurrentToolId();
            if (!string.IsNullOrEmpty(toolId) && ToolEffectivenessTracker.Instance != null)
            {
                return ToolEffectivenessTracker.Instance.IsToolExhausted(toolId);
            }
            return false;
        }

        /// <summary>
        /// Get exhaustion progress for current tool (0-1).
        /// </summary>
        public float GetCurrentToolExhaustionProgress()
        {
            string toolId = GetCurrentToolId();
            if (!string.IsNullOrEmpty(toolId) && ToolEffectivenessTracker.Instance != null)
            {
                return ToolEffectivenessTracker.Instance.GetExhaustionProgress(toolId);
            }
            return 0f;
        }

        #endregion

        #region Legacy Compatibility

        // These properties and methods exist for compatibility with code that used DiggingSystemV2.
        // The unified V3 system uses different approaches (UpgradeStation multipliers, DepthManager, etc.)

        // Current tool tier (managed by UpgradeStation)
        private ToolTier _currentToolTier = ToolTier.WoodenDigger;
        private float _digSpeedMultiplier = 1f;
        private float _digPowerMultiplier = 1f;
        private int _superHitCount = 1;

        /// <summary>
        /// Current tool tier. Legacy property - reads from UpgradeStation.CurrentToolTier.
        /// </summary>
        public ToolTier CurrentToolTier => (ToolTier)Machines.UpgradeStation.CurrentToolTier;

        /// <summary>
        /// Current tool name. Legacy property - returns a generic name based on tier.
        /// </summary>
        public string CurrentToolName => $"Tool Tier {(int)CurrentToolTier + 1}";

        /// <summary>
        /// Current dig power. Legacy property - returns effective dig strength with multipliers.
        /// </summary>
        public float CurrentDigPower => digStrength * Machines.UpgradeStation.ToolPowerMultiplier * Machines.UpgradeStation.ToolTierMultiplier;

        /// <summary>
        /// Current max dig depth. Legacy property - returns max depth based on current tool.
        /// </summary>
        public float CurrentMaxDigDepth
        {
            get
            {
                int toolIndex = Tools.HeldToolController.Instance?.CurrentToolIndex ?? 0;
                return toolIndex switch
                {
                    0 => tool1MaxDepth,
                    1 => tool2MaxDepth,
                    2 => tool3MaxDepth,
                    3 => tool4MaxDepth,
                    _ => tool1MaxDepth
                };
            }
        }

        /// <summary>
        /// Current dig depth in meters. Legacy property - reads from DepthManager.
        /// </summary>
        public float CurrentDigDepthMeters => DepthManager.Instance?.DepthBelowSoil ?? 0f;

        /// <summary>
        /// Current soil hardness. Legacy property - V3 doesn't use hardness, returns 1.
        /// </summary>
        public float CurrentSoilHardness => 1f;

        /// <summary>
        /// Current calculated duration. Legacy property - returns dig cooldown time.
        /// </summary>
        public float CurrentCalculatedDuration => 1f / (digsPerSecond * Machines.UpgradeStation.ToolSpeedMultiplier * Machines.UpgradeStation.ToolTierMultiplier);

        /// <summary>
        /// Current speed factor. Legacy property - returns combined speed multiplier.
        /// </summary>
        public float CurrentSpeedFactor => 1f / (Machines.UpgradeStation.ToolSpeedMultiplier * Machines.UpgradeStation.ToolTierMultiplier);

        /// <summary>
        /// Dig speed multiplier. Legacy property - returns UpgradeStation value.
        /// </summary>
        public float DigSpeedMultiplier => Machines.UpgradeStation.ToolSpeedMultiplier;

        /// <summary>
        /// Current effective cooldown. Legacy property - returns dig interval.
        /// </summary>
        public float CurrentEffectiveCooldown => 1f / (digsPerSecond * Machines.UpgradeStation.ToolSpeedMultiplier * Machines.UpgradeStation.ToolTierMultiplier);

        /// <summary>
        /// Base dig duration. Legacy property - returns base dig interval.
        /// </summary>
        public float BaseDigDuration => 1f / digsPerSecond;

        /// <summary>
        /// Set the current tool tier. Legacy method - tool tiers are now managed by UpgradeStation.
        /// </summary>
        public void SetToolTier(ToolTier tier)
        {
            _currentToolTier = tier;
            if (enableDebugLogs)
                Debug.Log($"[DiggingSystem] SetToolTier called (legacy): {tier}. Actual multipliers from UpgradeStation.");
        }

        /// <summary>
        /// Set the active tool profile. Legacy method - V3 system reads from UpgradeStation multipliers directly.
        /// </summary>
        public void SetActiveToolProfile(DigToolProfile profile)
        {
            if (profile == null) return;
            if (enableDebugLogs)
                Debug.Log($"[DiggingSystem] SetActiveToolProfile called (legacy): {profile.displayName}. V3 reads from UpgradeStation directly.");
        }

        /// <summary>
        /// Set dig speed multiplier. Legacy method - use UpgradeStation instead.
        /// </summary>
        public void SetDigSpeedMultiplier(float multiplier)
        {
            _digSpeedMultiplier = multiplier;
            if (enableDebugLogs)
                Debug.Log($"[DiggingSystem] SetDigSpeedMultiplier called (legacy): {multiplier}. Use UpgradeStation instead.");
        }

        /// <summary>
        /// Set dig power multiplier. Legacy method - use UpgradeStation instead.
        /// </summary>
        public void SetDigPowerMultiplier(float multiplier)
        {
            _digPowerMultiplier = multiplier;
            if (enableDebugLogs)
                Debug.Log($"[DiggingSystem] SetDigPowerMultiplier called (legacy): {multiplier}. Use UpgradeStation instead.");
        }

        /// <summary>
        /// Set super hit count. Legacy method - use UpgradeStation instead.
        /// </summary>
        public void SetSuperHitCount(int count)
        {
            _superHitCount = count;
            if (enableDebugLogs)
                Debug.Log($"[DiggingSystem] SetSuperHitCount called (legacy): {count}. Use UpgradeStation instead.");
        }

        #endregion

        #region Debug

        /// <summary>
        /// Get debug info string.
        /// </summary>
        public string GetDebugInfo()
        {
            string hitInfo = _lastHitValid
                ? $"Hit: {_lastHitPoint:F2}"
                : "No hit";

            // Calculate effective values with upgrades
            float effSpeed = digsPerSecond * UpgradeStation.ToolSpeedMultiplier * UpgradeStation.ToolTierMultiplier;
            float effPower = digStrength * UpgradeStation.ToolPowerMultiplier * UpgradeStation.ToolTierMultiplier;
            float effRadius = digRadius * UpgradeStation.ToolRadiusMultiplier * UpgradeStation.ToolTierMultiplier;

            return $"DiggingSystem: ACTIVE\n" +
                   $"Digging: {_isDigging}, {hitInfo}\n" +
                   $"Base: r={digRadius:F2}, str={digStrength:F2}, rate={digsPerSecond:F1}/s\n" +
                   $"Effective: r={effRadius:F2}, str={effPower:F2}, rate={effSpeed:F1}/s\n" +
                   $"Upgrades: Spd={UpgradeStation.ToolSpeedMultiplier:F2}x Pow={UpgradeStation.ToolPowerMultiplier:F2}x Rad={UpgradeStation.ToolRadiusMultiplier:F2}x Tier={UpgradeStation.ToolTierMultiplier:F2}x";
        }

        private void OnDrawGizmosSelected()
        {
            if (_lastHitValid)
            {
                // Draw dig sphere at hit point
                Gizmos.color = new Color(1f, 0.5f, 0f, 0.5f);
                Gizmos.DrawWireSphere(_lastHitPoint, digRadius);
            }
        }

        #endregion
    }
}
