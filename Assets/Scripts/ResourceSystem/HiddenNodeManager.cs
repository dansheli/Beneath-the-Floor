using UnityEngine;
using System;
using System.Collections.Generic;
using System.Linq;
using BeneathTheFloor.Digging; // For GuidedPitController (fallback bounds), DiggingSystem (density sampling)
using BeneathTheFloor.GameFlow; // For IntroCinematicController (new game flag)
using BeneathTheFloor.Machines;
using BeneathTheFloor.WorldRooms;

namespace BeneathTheFloor.ResourceSystem
{
    /// <summary>
    /// Manages hidden resource nodes in the terrain.
    /// Uses LAZY INSTANTIATION - nodes are data only until revealed.
    /// </summary>
    public class HiddenNodeManager : MonoBehaviour
    {
        public static HiddenNodeManager Instance { get; private set; }

        [Header("Configuration")]
        [SerializeField] private ResourceSystemConfig config;

        [Header("System Toggles")]
        [Tooltip("Enable/disable the entire node spawning system")]
        [SerializeField] private bool enableNodeSystem = true;

        [Tooltip("Enable/disable regular chunk-based nodes (the main node system)")]
        [SerializeField] private bool enableRegularNodes = true;

        [Tooltip("Enable/disable bonus nodes in the top layer")]
        [SerializeField] private bool enableBonusNodes = true;

        [Header("References")]
        [SerializeField] private GuidedPitController guidedPit;
        [SerializeField] private ModularShaftWallManager shaftWallManager;

        [Header("Shaft Bounds (nodes spawn inside shaft walls)")]
        [SerializeField] private bool limitToShaftArea = true;
        private Bounds shaftBounds;

        [Header("Depth Reference")]
        [SerializeField] private float basementFloorY = -3f;

        [Tooltip("Override minimum depth for nodes. Nodes spawn below (basementFloorY - this value). Set higher to push nodes deeper.")]
        [SerializeField] private float minNodeDepthOverride = 3f;

        [Tooltip("If true, use minNodeDepthOverride instead of config value.")]
        [SerializeField] private bool useDepthOverride = true;

        [Header("Top Layer Bonus Nodes")]
        [Tooltip("Extra nodes to add specifically in the top layer of the dig area.")]
        [SerializeField] private int topLayerBonusNodes = 5;

        [Tooltip("Minimum depth for bonus nodes (below basement floor). Push deeper to avoid surface.")]
        [SerializeField] private float bonusNodeMinDepth = 5f;

        [Tooltip("Maximum depth for bonus nodes (below basement floor). Defines bottom of bonus layer.")]
        [SerializeField] private float bonusNodeMaxDepth = 12f;

        [Tooltip("Grid spacing for bonus nodes. Smaller = more nodes, more even distribution.")]
        [SerializeField] private float bonusNodeGridSpacing = 4f;

        [Tooltip("Random offset within grid cell (0-1). 0 = perfect grid, 1 = fully random within cell.")]
        [Range(0f, 1f)]
        [SerializeField] private float bonusNodeJitter = 0.7f;

        [Header("Two-Radius Detection System")]
        [Tooltip("Large radius: When dig is within this distance, node GameObject SPAWNS (but stays buried).")]
        [SerializeField] private float spawnRadius = 3.5f;

        [Tooltip("Small radius: Size of the node for exposure calculation. Should match visual prefab size.")]
        [SerializeField] private float exposureRadius = 0.5f;

        [Tooltip("Exposure threshold to break node (0-1). 0.15 = 15% of node surface is exposed to air.")]
        [SerializeField] private float breakExposureThreshold = 0.15f;

        [Tooltip("Grace period after node is revealed before it can break (seconds). Prevents instant-break for nodes at dig center.")]
        [SerializeField] private float revealGracePeriod = 2.0f; // Increased from 0.5s to 2s for more visible reveal time

        [SerializeField] private float digSearchMargin = 1.0f; // Extra margin for node search

        [Header("Audio")]
        [Tooltip("Sound effects that play when a node disintegrates (random selection).")]
        [SerializeField] private AudioClip[] disintegrationSounds;
        [SerializeField] [Range(0f, 1f)] private float disintegrationVolume = 0.8f;

        [Tooltip("Sound effects that play when stones/fragments fall from a broken node (plays after disintegration).")]
        [SerializeField] private AudioClip[] fallingStonesSounds;
        [SerializeField] [Range(0f, 1f)] private float fallingStonesVolume = 0.8f;

        [Tooltip("Sound effects that play when a resource is collected (random selection).")]
        [SerializeField] private AudioClip[] collectSounds;
        [SerializeField] [Range(0f, 1f)] private float collectVolume = 1.0f;

        [Tooltip("Sound effect that plays when collection fails because inventory is full.")]
        [SerializeField] private AudioClip inventoryFullSound;
        [SerializeField] [Range(0f, 1f)] private float inventoryFullVolume = 0.6f;
        [Tooltip("Start time in seconds - skip to this point in the audio clip.")]
        [SerializeField] [Range(0f, 10f)] private float inventoryFullStartTime = 0f;

        private AudioSource audioSource;

        [Header("Startup Settings")]
        [Tooltip("Grace period at game start to ignore initial terrain generation digs (seconds).")]
        [SerializeField] private float startupGracePeriod = 3f;

        [Header("Debug")]
        [SerializeField] private bool showNodeGizmos = true;
#pragma warning disable CS0414 // Field is assigned but never used
        [SerializeField] private bool enableDebugLogs = false;
#pragma warning restore CS0414

        // Startup grace period tracking
        private float gameStartTime;
        private bool startupGraceExpired = false;
        private bool loadedFromSave = false; // Set to true when loading save data (bypasses grace period)

        // Node DATA storage: ChunkCoord -> List of nodes (data only, no GameObjects)
        private Dictionary<Vector3Int, List<HiddenNode>> nodesByChunk = new Dictionary<Vector3Int, List<HiddenNode>>();

        // VISUAL storage: only for nodes that have been revealed (lazy instantiation)
        private Dictionary<string, GameObject> nodeVisuals = new Dictionary<string, GameObject>();

        /// <summary>
        /// Returns the active node visual GameObjects for proximity culling.
        /// </summary>
        public Dictionary<string, GameObject> GetActiveNodeVisuals() => nodeVisuals;

        // Broken nodes for save/load
        private Dictionary<Vector3Int, HashSet<int>> brokenNodeIds = new Dictionary<Vector3Int, HashSet<int>>();

        // Global bonus nodes (grid-based, not per-chunk)
        private List<HiddenNode> globalBonusNodes = new List<HiddenNode>();
        private bool bonusNodesGenerated = false;

        // World seed
        private int worldSeed;

        // Sample points for exposure calculation (cached)
        private Vector3[] samplePointsOnSphere;

        // Chunk size (V3: 16 voxels * 0.35m = 5.6m)
        private const float CHUNK_WORLD_SIZE = 5.6f;

        // Last dig info for debug
        private Vector3 lastDigPosition;
        private float lastDigRadius;

        // Events
        public event Action<HiddenNode> OnNodeRevealed;
        public event Action<HiddenNode> OnNodeBroken;
        public event Action<HiddenNode, int> OnPickupsSpawned;

        /// <summary>
        /// Check if any nodes are physically visible (have exposure > 0).
        /// Used by mission system to check if condition is already met.
        /// </summary>
        public bool HasAnyVisibleNodes()
        {
            // Check all chunks for nodes with exposure
            foreach (var kvp in nodesByChunk)
            {
                foreach (var node in kvp.Value)
                {
                    if (node.HasVisual && node.CurrentExposure > 0.1f)
                    {
                        return true;
                    }
                }
            }

            // Also check bonus nodes
            foreach (var node in globalBonusNodes)
            {
                if (node.HasVisual && node.CurrentExposure > 0.1f)
                {
                    return true;
                }
            }

            return false;
        }

        private void Awake()
        {
            if (Instance != null && Instance != this)
            {
                if (enableDebugLogs) Debug.LogWarning("[HiddenNodeManager] Duplicate instance, destroying.");
                Destroy(gameObject);
                return;
            }
            Instance = this;
        }

        private void Start()
        {
            // Check if this is a new game - if so, clear all node data to start fresh
            int newGameFlag = PlayerPrefs.GetInt(IntroCinematicController.NEW_GAME_FLAG_KEY, 0);

            if (newGameFlag == 1)
            {
                ForceClearAllSaveData();
            }

            // Force exposure threshold to match 0.2m voxel balance (override stale inspector values)
            breakExposureThreshold = 0.15f;

            // Record game start time for startup grace period
            gameStartTime = Time.time;
            startupGraceExpired = false;

            // Setup audio source for disintegration sounds
            audioSource = gameObject.AddComponent<AudioSource>();
            audioSource.playOnAwake = false;
            audioSource.spatialBlend = 0f; // 2D sound (will be overridden for 3D playback)

            // Set global collect sounds for all NodePickup instances
            if (collectSounds != null && collectSounds.Length > 0)
            {
                NodePickup.SetGlobalCollectSounds(collectSounds, collectVolume);
            }

            // Set global inventory full sound
            if (inventoryFullSound != null)
            {
                if (enableDebugLogs) Debug.Log($"[HiddenNodeManager] Setting global inventory full sound: {inventoryFullSound.name}, volume: {inventoryFullVolume}, startTime: {inventoryFullStartTime}");
                NodePickup.SetGlobalInventoryFullSound(inventoryFullSound, inventoryFullVolume, inventoryFullStartTime);
            }
            else
            {
                if (enableDebugLogs) Debug.LogWarning("[HiddenNodeManager] No inventory full sound assigned in Inspector!");
            }

            // Load config
            if (config == null)
            {
                config = Resources.Load<ResourceSystemConfig>("ResourceSystemConfig");
                if (config == null)
                {
                    config = ScriptableObject.CreateInstance<ResourceSystemConfig>();
                }
            }

            // Generate world seed
            worldSeed = config.GetEffectiveSeed();

            // Get basement floor Y
            var depthManager = FindObjectOfType<DepthManager>();
            if (depthManager != null)
            {
                basementFloorY = depthManager.BasementFloorY;
            }

            // Find ModularShaftWallManager for shaft bounds (primary)
            if (shaftWallManager == null)
            {
                shaftWallManager = FindObjectOfType<ModularShaftWallManager>();
            }
            if (shaftWallManager != null && limitToShaftArea)
            {
                shaftBounds = shaftWallManager.GetInnerBoundsFromWalls();
            }
            // Fallback to GuidedPitController
            else if (guidedPit == null)
            {
                guidedPit = FindObjectOfType<GuidedPitController>();
            }
            if (guidedPit != null && shaftWallManager == null && limitToShaftArea)
            {
                shaftBounds = guidedPit.GetGuidedPitBounds();
            }

            // Pre-generate sample points (12 matches FixedResourceNode)
            GenerateSamplePoints(12);

            // Subscribe to dig events
            SubscribeToDigEvents();

            // Ensure ProximityCuller exists for distance-based rendering optimization
            if (ProximityCuller.Instance == null)
            {
                var cullerGo = new GameObject("ProximityCuller");
                cullerGo.AddComponent<ProximityCuller>();
            }
        }

        private void OnDestroy()
        {
            if (Instance == this)
                Instance = null;
            UnsubscribeFromDigEvents();
        }

        // Periodic exposure check timer
        private float exposureCheckTimer = 0f;
        private const float EXPOSURE_CHECK_INTERVAL = 0.5f; // Check every 0.5 seconds

        private void Update()
        {
            // Periodically check all visible nodes for exposure
            // This catches nodes that become exposed when player isn't actively digging
            exposureCheckTimer += Time.deltaTime;
            if (exposureCheckTimer >= EXPOSURE_CHECK_INTERVAL)
            {
                exposureCheckTimer = 0f;
                CheckAllVisibleNodesForExposure(null);
            }
        }

        private void GenerateSamplePoints(int count)
        {
            samplePointsOnSphere = new Vector3[count];

            // Golden spiral distribution for even coverage
            float goldenRatio = (1f + Mathf.Sqrt(5f)) / 2f;
            float angleIncrement = Mathf.PI * 2f * goldenRatio;

            for (int i = 0; i < count; i++)
            {
                float t = (float)i / count;
                float inclination = Mathf.Acos(1f - 2f * t);
                float azimuth = angleIncrement * i;

                samplePointsOnSphere[i] = new Vector3(
                    Mathf.Sin(inclination) * Mathf.Cos(azimuth),
                    Mathf.Sin(inclination) * Mathf.Sin(azimuth),
                    Mathf.Cos(inclination)
                );
            }
        }

        #region Dig Event Handling

        private void SubscribeToDigEvents()
        {
            if (DiggingSystem.Instance != null)
            {
                DiggingSystem.Instance.OnDigCompleted += HandleDigCompleted;
            }
            else
            {
                StartCoroutine(DelayedSubscribe());
            }
        }

        private System.Collections.IEnumerator DelayedSubscribe()
        {
            yield return new WaitForSeconds(0.5f);
            if (DiggingSystem.Instance != null)
            {
                DiggingSystem.Instance.OnDigCompleted += HandleDigCompleted;
            }
        }

        private void UnsubscribeFromDigEvents()
        {
            if (DiggingSystem.Instance != null)
            {
                DiggingSystem.Instance.OnDigCompleted -= HandleDigCompleted;
            }
        }

        /// <summary>
        /// Handle dig completion - Two-Radius system:
        /// 1. SpawnRadius: When dig touches this, node GameObject spawns (but stays buried)
        /// 2. ExposureRadius: Uses density sampling to calculate how exposed the node is
        /// 3. When exposure >= threshold, node crumbles into pickups
        /// </summary>
        private void HandleDigCompleted(Digging.DigResult result)
        {
            if (!result.Success)
                return;

            // Skip dig events during startup grace period (initial terrain generation)
            // Exception: If loaded from save, skip the grace period (we want dig events to work immediately)
            if (!startupGraceExpired && !loadedFromSave)
            {
                float timeSinceStart = Time.time - gameStartTime;
                if (timeSinceStart < startupGracePeriod)
                {
                    // Still in grace period - ignore this dig event
                    return;
                }
                startupGraceExpired = true;
            }

            lastDigPosition = result.Operation.WorldPosition;
            lastDigRadius = result.Operation.Radius;

            // Search radius = dig radius + spawn radius + margin
            float searchRadius = lastDigRadius + spawnRadius + digSearchMargin;
            var nearbyNodes = GetNodesInRadius(lastDigPosition, searchRadius);

            foreach (var node in nearbyNodes)
            {
                if (node.IsBroken) continue;

                // Distance from dig center to node center
                float dist = Vector3.Distance(lastDigPosition, node.WorldPosition);

                // SPAWN CHECK: If dig is within spawnRadius, spawn the visual (still buried)
                if (!node.HasVisual && dist <= spawnRadius)
                {
                    CreateNodeVisual(node);
                    OnNodeRevealed?.Invoke(node);
                }
                // EXPOSURE CHECK: If visual exists, calculate exposure using density sampling
                if (node.HasVisual)
                {
                    // Get tier-specific exposure radius and threshold from ResourceTierConfig
                    float effectiveExposureRadius = exposureRadius; // Default fallback
                    float effectiveThreshold = breakExposureThreshold; // Default fallback

                    if (node.ResourceIndex >= 0 && config != null)
                    {
                        var resourceDef = config.GetResourceByIndex(node.ResourceIndex);
                        if (resourceDef != null)
                        {
                            var tierConfig = resourceDef.GetTierConfig(node.Tier);
                            if (tierConfig != null)
                            {
                                // Scale exposureRadius by sqrt of prefab scale for balanced detection
                                // Full scale (5-14x) is too large, sqrt gives reasonable 2-4x multiplier
                                float scaleFactor = Mathf.Sqrt(tierConfig.nodePrefabScale);
                                effectiveExposureRadius = tierConfig.exposureRadius * scaleFactor;
                                effectiveThreshold = tierConfig.crumbleThreshold;
                            }
                        }
                    }

                    float exposure = CalculateExposureWithRadius(node, effectiveExposureRadius);
                    node.CurrentExposure = exposure;

                    // BREAK CHECK: If exposure exceeds threshold, break the node
                    // But only if grace period has passed (prevents instant-break for nodes at dig center)
                    float timeSinceReveal = Time.time - node.RevealTime;

                    if (exposure >= effectiveThreshold && timeSinceReveal >= revealGracePeriod)
                    {
                        BreakNode(node);
                    }
                }
            }

            // Also check ALL visible nodes that might not be near this dig
            // This catches nodes that were exposed by multiple digs from different angles
            CheckAllVisibleNodesForExposure(nearbyNodes);
        }

        /// <summary>
        /// Check all visible nodes for exposure and break if threshold exceeded.
        /// Used to catch nodes exposed by multiple digs from different angles.
        /// </summary>
        /// <param name="alreadyChecked">Nodes already checked this frame (to avoid duplicates)</param>
        private void CheckAllVisibleNodesForExposure(List<HiddenNode> alreadyChecked)
        {
            // Check all visible chunk-based nodes
            foreach (var kvp in nodesByChunk)
            {
                foreach (var node in kvp.Value)
                {
                    if (node.IsBroken || !node.HasVisual)
                        continue;

                    // Skip if already checked in nearby nodes
                    if (alreadyChecked != null && alreadyChecked.Contains(node))
                        continue;

                    CheckNodeExposureAndBreak(node);
                }
            }

            // Check all visible bonus nodes
            foreach (var node in globalBonusNodes)
            {
                if (node.IsBroken || !node.HasVisual)
                    continue;

                if (alreadyChecked != null && alreadyChecked.Contains(node))
                    continue;

                CheckNodeExposureAndBreak(node);
            }
        }

        /// <summary>
        /// Check a single node's exposure and break if threshold exceeded.
        /// </summary>
        private void CheckNodeExposureAndBreak(HiddenNode node)
        {
            if (node.IsBroken || !node.HasVisual)
                return;

            // Get tier-specific exposure radius and threshold from ResourceTierConfig
            float effectiveExposureRadius = exposureRadius;
            float effectiveThreshold = breakExposureThreshold;

            if (node.ResourceIndex >= 0 && config != null)
            {
                var resourceDef = config.GetResourceByIndex(node.ResourceIndex);
                if (resourceDef != null)
                {
                    var tierConfig = resourceDef.GetTierConfig(node.Tier);
                    if (tierConfig != null)
                    {
                        // Scale exposureRadius by sqrt of prefab scale for balanced detection
                        // Full scale (5-14x) is too large, sqrt gives reasonable 2-4x multiplier
                        float scaleFactor = Mathf.Sqrt(tierConfig.nodePrefabScale);
                        effectiveExposureRadius = tierConfig.exposureRadius * scaleFactor;
                        effectiveThreshold = tierConfig.crumbleThreshold;
                    }
                }
            }

            float exposure = CalculateExposureWithRadius(node, effectiveExposureRadius);
            node.CurrentExposure = exposure;

            // Break check with grace period
            float timeSinceReveal = Time.time - node.RevealTime;
            if (exposure >= effectiveThreshold && timeSinceReveal >= revealGracePeriod)
            {
                BreakNode(node);
            }
        }

        #endregion

        #region Node Generation

        /// <summary>
        /// Get or generate nodes for a chunk (DATA only, no GameObjects).
        /// </summary>
        public List<HiddenNode> GetNodesForChunk(Vector3Int chunkCoord)
        {
            if (nodesByChunk.TryGetValue(chunkCoord, out var existingNodes))
            {
                return existingNodes;
            }

            var nodes = GenerateNodesForChunk(chunkCoord);
            nodesByChunk[chunkCoord] = nodes;

            // Apply saved broken state
            if (brokenNodeIds.TryGetValue(chunkCoord, out var brokenIds))
            {
                foreach (var node in nodes)
                {
                    if (brokenIds.Contains(node.NodeId))
                    {
                        node.IsBroken = true;
                    }
                }
            }

            // Apply saved revealed state and recreate visuals
            if (revealedNodeIds.TryGetValue(chunkCoord, out var revealedIds))
            {
                foreach (var node in nodes)
                {
                    if (revealedIds.Contains(node.NodeId) && !node.IsBroken)
                    {
                        // Recreate the visual for this revealed node
                        CreateNodeVisual(node);
                    }
                }
            }

            return nodes;
        }

        /// <summary>
        /// Generate nodes for a chunk using seeded RNG.
        /// PLACEMENT: Random distribution within ModularShaftWalls bounds.
        /// VALIDATION: Nodes must be FULLY BURIED and below minNodeDepth.
        /// </summary>
        private List<HiddenNode> GenerateNodesForChunk(Vector3Int chunkCoord)
        {
            var nodes = new List<HiddenNode>();

            // Check if node system is enabled
            if (!enableNodeSystem)
                return nodes;

            // Check if regular (chunk-based) nodes are enabled
            if (!enableRegularNodes)
                return nodes;

            if (config == null || config.nodesPerChunk <= 0)
                return nodes;

            float chunkSize = CHUNK_WORLD_SIZE;
            Vector3 chunkOrigin = new Vector3(
                chunkCoord.x * chunkSize,
                chunkCoord.y * chunkSize,
                chunkCoord.z * chunkSize
            );

            // Use config values (with optional override)
            float minDepth = useDepthOverride ? minNodeDepthOverride : config.minNodeDepth;
            int attemptsPerNode = config.nodePlacementAttempts;

            // Calculate min Y (nodes must be at or below this Y)
            // minNodeY = basement floor minus required depth
            float minNodeY = basementFloorY - minDepth;

            // Calculate bonus node layer range (controlled by inspector)
            float topLayerMinY = basementFloorY - bonusNodeMaxDepth;  // Bottom of bonus layer (deeper)
            float topLayerMaxY = basementFloorY - bonusNodeMinDepth;  // Top of bonus layer (shallower)

            // Skip chunks that are ENTIRELY above BOTH the regular node range AND the top layer
            // A chunk is valid if it overlaps with either range
            float chunkBottomY = chunkOrigin.y;
            float chunkTopY = chunkOrigin.y + chunkSize;

            bool overlapsRegularRange = chunkBottomY <= minNodeY;
            bool overlapsTopLayer = (topLayerBonusNodes > 0) && (chunkTopY >= topLayerMinY && chunkBottomY <= topLayerMaxY);

            if (!overlapsRegularRange && !overlapsTopLayer)
            {
                // Chunk doesn't overlap with any valid node range
                return nodes;
            }

            // Get FRESH shaft bounds for node placement (walls may not be built at Start time)
            Bounds nodeBounds;
            if (shaftWallManager != null)
            {
                nodeBounds = shaftWallManager.GetInnerBoundsFromWalls();
            }
            else if (guidedPit != null)
            {
                nodeBounds = guidedPit.GetGuidedPitBounds();
            }
            else
            {
                // Fallback: use chunk bounds
                nodeBounds = new Bounds(chunkOrigin + Vector3.one * chunkSize * 0.5f, Vector3.one * chunkSize);
                if (enableDebugLogs) Debug.LogWarning("[HiddenNodeManager] No shaft/pit manager found, using chunk bounds");
            }

            // Create seeded RNG
            int chunkSeed = HashChunkCoord(chunkCoord, worldSeed);
            System.Random rng = new System.Random(chunkSeed);

            int nodesCreated = 0;
            int skippedCount = 0;

            for (int i = 0; i < config.nodesPerChunk; i++)
            {
                bool placed = false;
                string skipReason = "";

                for (int attempt = 0; attempt < attemptsPerNode; attempt++)
                {
                    // RANDOM POSITION within shaft bounds (ModularShaftWalls area)
                    float worldX = nodeBounds.min.x + (float)rng.NextDouble() * nodeBounds.size.x;
                    float worldZ = nodeBounds.min.z + (float)rng.NextDouble() * nodeBounds.size.z;

                    // Random depth within chunk's Y range (but within shaft depth)
                    float worldY = chunkOrigin.y + (float)rng.NextDouble() * chunkSize;
                    // Clamp to shaft bounds Y
                    worldY = Mathf.Clamp(worldY, nodeBounds.min.y, nodeBounds.max.y);

                    Vector3 worldPos = new Vector3(worldX, worldY, worldZ);

                    // 1) DEPTH CHECK: Must be below minNodeDepth
                    float depth = basementFloorY - worldPos.y;
                    if (depth < minDepth)
                    {
                        skipReason = $"depth={depth:F1}m < min={minDepth}m";
                        continue;
                    }

                    // 2) SHAFT BOUNDS CHECK: Must be inside shaft area
                    if (limitToShaftArea && nodeBounds.size.sqrMagnitude > 0)
                    {
                        if (!nodeBounds.Contains(worldPos))
                        {
                            skipReason = "outside shaft bounds";
                            continue;
                        }
                    }

                    // 3) Random radius
                    float radius = config.minNodeRadius + (float)rng.NextDouble() * (config.maxNodeRadius - config.minNodeRadius);

                    // 4) FULLY BURIED CHECK: All sample points must be solid
                    if (!IsNodeFullyBuried(worldPos, radius))
                    {
                        skipReason = "not fully buried (air detected)";
                        continue;
                    }

                    // 5) MINIMUM DISTANCE CHECK: Must be far enough from other nodes
                    // Check against nodes in THIS chunk being generated
                    bool tooClose = false;
                    foreach (var existingNode in nodes)
                    {
                        if (Vector3.Distance(worldPos, existingNode.WorldPosition) < config.minNodeDistance)
                        {
                            tooClose = true;
                            break;
                        }
                    }

                    // Also check against nodes in NEIGHBORING chunks (cross-chunk check)
                    if (!tooClose)
                    {
                        tooClose = IsPositionTooCloseToNeighborNodes(worldPos, chunkCoord, config.minNodeDistance);
                    }

                    if (tooClose)
                    {
                        skipReason = $"too close to another node (min={config.minNodeDistance}m)";
                        continue;
                    }

                    // 6) SUCCESS: Create node DATA (no GameObject yet)
                    // Select random resource for this depth
                    var selectedResource = config.SelectRandomResourceForDepth(depth, rng);
                    int resourceIndex = -1;
                    string resourceId = null;

                    if (selectedResource != null)
                    {
                        resourceIndex = config.resources.IndexOf(selectedResource);
                        resourceId = selectedResource.resourceId;
                    }
                    else
                    {
                        // No resource configured for this depth - skip this node
                        skipReason = "no resource configured for depth";
                        continue;
                    }

                    // Get max tier allowed for this depth layer
                    var layerConfig = config.GetDepthLayerConfig(depth);
                    int maxTierAllowed = layerConfig?.maxTierAllowed ?? 4;

                    // Cap tier based on depth requirements (T2+ need deeper)
                    int maxTierForDepth = config.GetMaxTierForDepth(depth);
                    maxTierAllowed = Mathf.Min(maxTierAllowed, maxTierForDepth);

                    // Select tier based on depth
                    int tier = config.SelectRandomTier(maxTierAllowed, rng);

                    // Get resource-specific radius if available
                    if (selectedResource != null && tier >= 1 && tier <= 4)
                    {
                        var tierConfig = selectedResource.GetTierConfig(tier);
                        if (tierConfig != null)
                        {
                            radius = tierConfig.nodeRadius;
                        }
                    }

                    var node = new HiddenNode(nodesCreated, worldPos, radius, tier, chunkCoord, resourceIndex, resourceId);
                    nodes.Add(node);
                    nodesCreated++;
                    placed = true;
                    break; // Move to next node
                }

                if (!placed)
                {
                    skippedCount++;
                    // Don't log individual skips - too spammy. Summary is logged below.
                }
            }

            // Only log if we actually placed nodes (don't spam about empty chunks)
            // Global bonus nodes are generated separately (grid-based) - not per chunk
            return nodes;
        }

        /// <summary>
        /// Generate global bonus nodes using grid-based placement for even distribution.
        /// Called once, not per-chunk.
        /// </summary>
        private void GenerateGlobalBonusNodes()
        {
            try
            {
                // Check if bonus nodes are enabled
                if (!enableBonusNodes)
                    return;

                if (bonusNodesGenerated)
                    return;

                if (topLayerBonusNodes <= 0)
                    return;

                bonusNodesGenerated = true;
                globalBonusNodes.Clear();

                // Get shaft bounds
                Bounds nodeBounds;
                if (shaftWallManager != null)
                {
                    nodeBounds = shaftWallManager.GetInnerBoundsFromWalls();
                }
                else if (guidedPit != null)
                {
                    nodeBounds = guidedPit.GetGuidedPitBounds();
                }
                else
                {
                    return;
                }

                // Bonus node Y range
                float bonusMinY = basementFloorY - bonusNodeMaxDepth;  // Bottom (deeper)
                float bonusMaxY = basementFloorY - bonusNodeMinDepth;  // Top (shallower)

                // Calculate grid dimensions
                float areaWidth = nodeBounds.size.x;
                float areaDepth = nodeBounds.size.z;
                float yRange = bonusMaxY - bonusMinY;

                // Ensure yRange is positive (swap if needed)
                if (yRange < 0)
                {
                    // Swap to make it positive
                    float temp = bonusMinY;
                    bonusMinY = bonusMaxY;
                    bonusMaxY = temp;
                    yRange = -yRange;
                }

                int gridX = Mathf.Max(1, Mathf.FloorToInt(areaWidth / bonusNodeGridSpacing));
                int gridZ = Mathf.Max(1, Mathf.FloorToInt(areaDepth / bonusNodeGridSpacing));
                int gridY = Mathf.Max(1, Mathf.FloorToInt(yRange / bonusNodeGridSpacing));

                int totalCells = gridX * gridY * gridZ;

                // SAFETY: Limit grid size to prevent freezing
                if (totalCells > 10000)
                    return;

                float cellSizeX = areaWidth / gridX;
                float cellSizeZ = areaDepth / gridZ;
                float cellSizeY = yRange / gridY;

                System.Random rng = new System.Random(worldSeed + 12345); // Different seed for bonus

                int placed = 0;
                int failedBounds = 0;
                int failedDensity = 0;
                int failedTooClose = 0;

                int targetCount = topLayerBonusNodes;

                // Build list of all cell positions (shuffled for random selection)
                var cellPositions = new System.Collections.Generic.List<Vector3>();
                for (int gx = 0; gx < gridX; gx++)
                {
                    for (int gz = 0; gz < gridZ; gz++)
                    {
                        for (int gy = 0; gy < gridY; gy++)
                        {
                            float cellCenterX = nodeBounds.min.x + (gx + 0.5f) * cellSizeX;
                            float cellCenterZ = nodeBounds.min.z + (gz + 0.5f) * cellSizeZ;
                            float cellCenterY = bonusMinY + (gy + 0.5f) * cellSizeY;
                            cellPositions.Add(new Vector3(cellCenterX, cellCenterY, cellCenterZ));
                        }
                    }
                }

                // Shuffle cells for random distribution when we have more cells than target
                for (int i = cellPositions.Count - 1; i > 0; i--)
                {
                    int j = rng.Next(i + 1);
                    var temp = cellPositions[i];
                    cellPositions[i] = cellPositions[j];
                    cellPositions[j] = temp;
                }

                // Minimum distance between bonus nodes (configurable based on grid spacing)
                float minBonusDistance = Mathf.Max(1f, bonusNodeGridSpacing * 0.5f);

                // Iterate through shuffled cells until we reach target count
                foreach (var cellCenter in cellPositions)
                {
                    // Stop if we've reached target count
                    if (placed >= targetCount)
                        break;

                    // Apply jitter (random offset within cell)
                    float jitterX = (float)(rng.NextDouble() - 0.5) * cellSizeX * bonusNodeJitter;
                    float jitterZ = (float)(rng.NextDouble() - 0.5) * cellSizeZ * bonusNodeJitter;
                    float jitterY = (float)(rng.NextDouble() - 0.5) * cellSizeY * bonusNodeJitter;

                    Vector3 worldPos = new Vector3(
                        cellCenter.x + jitterX,
                        cellCenter.y + jitterY,
                        cellCenter.z + jitterZ
                    );

                    // Validate X/Z position is in shaft bounds (Y is controlled by bonus depth settings)
                    // Don't check nodeBounds.Contains() because bonus zone Y may be above shaft bounds
                    bool inXZBounds = worldPos.x >= nodeBounds.min.x && worldPos.x <= nodeBounds.max.x &&
                                      worldPos.z >= nodeBounds.min.z && worldPos.z <= nodeBounds.max.z;
                    if (!inXZBounds)
                    {
                        failedBounds++;
                        continue;
                    }

                    // Check if in solid terrain
                    float density = GetDensityAt(worldPos);
                    if (density < 0.3f)
                    {
                        failedDensity++;
                        continue;
                    }

                    // Check distance from existing bonus nodes
                    bool tooClose = false;
                    foreach (var existingNode in globalBonusNodes)
                    {
                        if (Vector3.Distance(worldPos, existingNode.WorldPosition) < minBonusDistance)
                        {
                            tooClose = true;
                            break;
                        }
                    }
                    if (tooClose)
                    {
                        failedTooClose++;
                        continue;
                    }

                    // Create bonus node WITH RESOURCE SYSTEM
                    Vector3Int chunkCoord = GetChunkCoordForPosition(worldPos);
                    int nodeId = 1000000 + placed;

                    // Calculate depth for resource selection
                    float depth = basementFloorY - worldPos.y;

                    // Select resource for this depth (using config's depth layer system)
                    var selectedResource = config.SelectRandomResourceForDepth(depth, rng);
                    int resourceIndex = -1;
                    string resourceId = null;

                    if (selectedResource != null)
                    {
                        resourceIndex = config.resources.IndexOf(selectedResource);
                        resourceId = selectedResource.resourceId;
                    }
                    else
                    {
                        // No resource configured for this depth - skip this bonus node
                        // Don't use fallback as it causes wrong resources to appear
                        continue;
                    }

                    // Get max tier allowed for this depth
                    var layerConfig = config.GetDepthLayerConfig(depth);
                    int maxTierAllowed = layerConfig?.maxTierAllowed ?? 4;
                    int maxTierForDepth = config.GetMaxTierForDepth(depth);
                    maxTierAllowed = Mathf.Min(maxTierAllowed, maxTierForDepth);

                    // Select tier based on depth
                    int tier = config.SelectRandomTier(maxTierAllowed, rng);

                    // Get radius from tier config if available
                    float radius = config.minNodeRadius + (float)rng.NextDouble() * (config.maxNodeRadius - config.minNodeRadius);
                    if (selectedResource != null && tier >= 1 && tier <= 4)
                    {
                        var tierConfig = selectedResource.GetTierConfig(tier);
                        if (tierConfig != null)
                        {
                            radius = tierConfig.nodeRadius;
                        }
                    }

                    // Create node with resource info
                    var node = new HiddenNode(nodeId, worldPos, radius, tier, chunkCoord, resourceIndex, resourceId);
                    node.IsBonusNode = true;

                    // Apply saved broken state
                    if (brokenBonusNodeIds.Contains(nodeId))
                    {
                        node.IsBroken = true;
                    }
                    // Apply saved revealed state and recreate visual
                    else if (revealedBonusNodeIds.Contains(nodeId))
                    {
                        CreateNodeVisual(node);
                    }

                    globalBonusNodes.Add(node);
                    placed++;
                }

            }
            catch (System.Exception ex)
            {
                Debug.LogError($"[HiddenNodeManager] Exception in GenerateGlobalBonusNodes: {ex.Message}");
            }
        }

        private Vector3Int GetChunkCoordForPosition(Vector3 worldPos)
        {
            float chunkSize = CHUNK_WORLD_SIZE;
            return new Vector3Int(
                Mathf.FloorToInt(worldPos.x / chunkSize),
                Mathf.FloorToInt(worldPos.y / chunkSize),
                Mathf.FloorToInt(worldPos.z / chunkSize)
            );
        }

        /// <summary>
        /// Check if a node position is FULLY BURIED (all sample points in solid terrain).
        /// CRITICAL: If density cannot be sampled, assume SOLID (1.0).
        /// </summary>
        private bool IsNodeFullyBuried(Vector3 center, float radius)
        {
            // Check center first
            float centerDensity = GetDensityAt(center);
            if (centerDensity < 0.5f)
                return false; // Center is in air

            // Check all sample points on sphere surface
            for (int i = 0; i < samplePointsOnSphere.Length; i++)
            {
                Vector3 samplePos = center + samplePointsOnSphere[i] * radius * 0.95f; // Slightly inside
                float density = GetDensityAt(samplePos);

                if (density < 0.5f)
                    return false; // This point is in air - not fully buried
            }

            return true; // All points solid
        }

        /// <summary>
        /// Get terrain density at a position using DiggingSystem.
        /// CRITICAL: Returns 1.0 (SOLID) if DiggingSystem is unavailable.
        /// </summary>
        private float GetDensityAt(Vector3 worldPos)
        {
            if (DiggingSystem.Instance == null)
                return 1f; // No digging system = assume solid

            try
            {
                float density = DiggingSystem.Instance.GetDensityAt(worldPos);

                // If GetDensityAt returns invalid/default, treat as solid
                if (float.IsNaN(density) || float.IsInfinity(density))
                    return 1f;

                return density;
            }
            catch
            {
                return 1f; // Error = assume solid
            }
        }

        private int HashChunkCoord(Vector3Int coord, int seed)
        {
            unchecked
            {
                int hash = seed;
                hash = hash * 31 + coord.x;
                hash = hash * 31 + coord.y;
                hash = hash * 31 + coord.z;
                return hash;
            }
        }

        /// <summary>
        /// Check if a position is too close to nodes in neighboring chunks.
        /// Only checks already-generated chunks to avoid infinite recursion.
        /// </summary>
        private bool IsPositionTooCloseToNeighborNodes(Vector3 worldPos, Vector3Int currentChunk, float minDistance)
        {
            // Check all 26 neighboring chunks (3x3x3 cube minus center)
            for (int dx = -1; dx <= 1; dx++)
            {
                for (int dy = -1; dy <= 1; dy++)
                {
                    for (int dz = -1; dz <= 1; dz++)
                    {
                        if (dx == 0 && dy == 0 && dz == 0)
                            continue; // Skip current chunk

                        Vector3Int neighborCoord = new Vector3Int(
                            currentChunk.x + dx,
                            currentChunk.y + dy,
                            currentChunk.z + dz
                        );

                        // Only check chunks that have ALREADY been generated
                        // This avoids infinite recursion
                        if (nodesByChunk.TryGetValue(neighborCoord, out var neighborNodes))
                        {
                            foreach (var node in neighborNodes)
                            {
                                if (Vector3.Distance(worldPos, node.WorldPosition) < minDistance)
                                {
                                    return true;
                                }
                            }
                        }
                    }
                }
            }

            return false;
        }

        #endregion

        #region Exposure Calculation

        /// <summary>
        /// Calculate exposure for a node using specified radius.
        /// Samples points on a sphere of given radius around node center.
        /// Returns 0-1 where 1 = fully exposed (all sample points in air).
        /// </summary>
        private float CalculateExposureWithRadius(HiddenNode node, float radius)
        {
            int airSamples = 0;
            int totalSamples = 0;

            // CRITICAL: Check center point first - if center is in air, node is definitely exposed
            float centerDensity = GetDensityAt(node.WorldPosition);
            totalSamples++;
            if (centerDensity < 0.5f)
            {
                airSamples++;
                // If center is in air, weight it heavily - return at least 60% exposure
                // This ensures floating nodes will break
            }

            // Sample at multiple distances to get better coverage
            float[] radiiToSample = { radius * 0.5f, radius * 0.8f, radius };

            foreach (float sampleRadius in radiiToSample)
            {
                for (int i = 0; i < samplePointsOnSphere.Length; i++)
                {
                    Vector3 samplePos = node.WorldPosition + samplePointsOnSphere[i] * sampleRadius;
                    float density = GetDensityAt(samplePos);

                    if (density < 0.5f)
                        airSamples++;

                    totalSamples++;
                }
            }

            float exposure = totalSamples > 0 ? (float)airSamples / totalSamples : 0f;

            // If center is in air, ensure at least 60% exposure (will exceed 50% threshold)
            if (centerDensity < 0.5f && exposure < 0.6f)
            {
                exposure = 0.6f;
            }

            return exposure;
        }

        /// <summary>
        /// Calculate exposure for a node using its stored radius.
        /// </summary>
        private float CalculateExposure(HiddenNode node)
        {
            return CalculateExposureWithRadius(node, node.Radius);
        }

        /// <summary>
        /// Get nodes within radius of a position (for dig event handling).
        /// Includes both chunk-based nodes AND global bonus nodes.
        /// </summary>
        private List<HiddenNode> GetNodesInRadius(Vector3 center, float radius)
        {
            var result = new List<HiddenNode>();

            // Ensure global bonus nodes are generated
            if (!bonusNodesGenerated)
            {
                GenerateGlobalBonusNodes();
            }

            float chunkSize = CHUNK_WORLD_SIZE;

            int minCX = Mathf.FloorToInt((center.x - radius) / chunkSize);
            int maxCX = Mathf.CeilToInt((center.x + radius) / chunkSize);
            int minCY = Mathf.FloorToInt((center.y - radius) / chunkSize);
            int maxCY = Mathf.CeilToInt((center.y + radius) / chunkSize);
            int minCZ = Mathf.FloorToInt((center.z - radius) / chunkSize);
            int maxCZ = Mathf.CeilToInt((center.z + radius) / chunkSize);

            // Check chunk-based nodes
            for (int cx = minCX; cx <= maxCX; cx++)
            {
                for (int cy = minCY; cy <= maxCY; cy++)
                {
                    for (int cz = minCZ; cz <= maxCZ; cz++)
                    {
                        var chunkCoord = new Vector3Int(cx, cy, cz);
                        var nodes = GetNodesForChunk(chunkCoord);

                        foreach (var node in nodes)
                        {
                            float dist = Vector3.Distance(center, node.WorldPosition);
                            if (dist <= radius + node.Radius)
                            {
                                result.Add(node);
                            }
                        }
                    }
                }
            }

            // Also check global bonus nodes
            foreach (var bonusNode in globalBonusNodes)
            {
                float dist = Vector3.Distance(center, bonusNode.WorldPosition);
                if (dist <= radius + bonusNode.Radius)
                {
                    result.Add(bonusNode);
                }
            }

            return result;
        }

        #endregion

        #region Node Visuals (Lazy Instantiation)

        /// <summary>
        /// Get node visual prefab for a specific tier.
        /// Uses the tier-specific prefab fields in config (assign in Inspector).
        /// </summary>
        private GameObject GetNodeVisualPrefabForTier(int tier)
        {
            if (config == null) return null;
            return config.GetVisualPrefabForTier(tier);
        }

        /// <summary>
        /// Get pickup prefab for a specific tier.
        /// Uses the tier-specific prefab fields in config (assign in Inspector).
        /// </summary>
        private GameObject GetPickupPrefabForTier(int tier)
        {
            if (config == null) return null;
            return config.GetPickupPrefabForTier(tier);
        }

        private string GetNodeKey(HiddenNode node)
        {
            return $"{node.ChunkCoord.x}_{node.ChunkCoord.y}_{node.ChunkCoord.z}_{node.NodeId}";
        }

        /// <summary>
        /// Create visual for a node (LAZY - only when revealed).
        /// Uses resource-specific prefabs/colors when available.
        /// </summary>
        private void CreateNodeVisual(HiddenNode node)
        {
            if (node.HasVisual || node.IsBroken) return;

            string key = GetNodeKey(node);
            if (nodeVisuals.ContainsKey(key)) return;

            GameObject visual;

            // Try to get resource-specific prefab first
            GameObject prefab = null;
            ResourceDefinition resourceDef = null;
            ResourceTierConfig tierConfig = null;
            Color resourceColor = Color.gray;
            float prefabScale = 1.0f;

            if (node.ResourceIndex >= 0 && config != null)
            {
                resourceDef = config.GetResourceByIndex(node.ResourceIndex);
                if (resourceDef != null)
                {
                    tierConfig = resourceDef.GetTierConfig(node.Tier);
                    if (tierConfig != null)
                    {
                        prefab = tierConfig.nodePrefab;
                        prefabScale = tierConfig.nodePrefabScale;
                    }
                    resourceColor = resourceDef.resourceColor;
                }
            }

            // Fall back to tier-based prefab ONLY if no resource system is configured
            if (prefab == null && node.ResourceIndex < 0)
            {
                prefab = GetNodeVisualPrefabForTier(node.Tier);
            }

            // If still no prefab and node has no resource, skip this node entirely
            if (prefab == null && resourceDef == null && config != null && config.resources != null && config.resources.Count > 0)
            {
                return;
            }

            // Get scale - use per-tier scale if available, otherwise global multiplier
            float scaleMult = prefabScale > 0 ? prefabScale : (config?.nodeVisualScaleMultiplier ?? 1.0f);

            if (prefab != null)
            {
                // Use prefab
                visual = Instantiate(prefab, node.WorldPosition, UnityEngine.Random.rotation);

                // Apply per-tier scale
                visual.transform.localScale = Vector3.one * scaleMult;
            }
            else if (resourceDef != null)
            {
                // Fallback: Create simple sphere with RESOURCE color (only for nodes with resources)
                visual = GameObject.CreatePrimitive(PrimitiveType.Sphere);
                visual.transform.position = node.WorldPosition;
                visual.transform.localScale = Vector3.one * node.Radius;

                var renderer = visual.GetComponent<Renderer>();
                if (renderer != null)
                {
                    Shader shader = Shader.Find("Universal Render Pipeline/Lit");
                    if (shader == null)
                        shader = Shader.Find("Standard");

                    Material mat = new Material(shader);
                    mat.SetColor("_BaseColor", resourceColor);
                    mat.SetFloat("_Metallic", 0.7f);
                    mat.SetFloat("_Smoothness", 0.6f);
                    renderer.material = mat;
                }
            }
            else
            {
                // No prefab, no resource - skip entirely
                return;
            }

            // Name includes resource type and tier
            string resourceName = resourceDef?.displayName ?? "Unknown";
            visual.name = $"NodeVisual_{resourceName}_T{node.Tier}_{node.NodeId}";

            // Add NodeVisualMarker (NOT NodePickup!) - this is NOT collectible
            var marker = visual.AddComponent<NodeVisualMarker>();
            marker.Initialize(node.NodeId, node.Tier, node.ChunkCoord);

            // Disable ALL colliders on the visual (not interactive)
            foreach (var col in visual.GetComponentsInChildren<Collider>())
            {
                col.enabled = false;
            }

            // Disable shadow casting on node visuals (major perf win)
            foreach (var r in visual.GetComponentsInChildren<Renderer>())
            {
                r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            }

            // Ensure NO NodePickup component exists
            var existingPickup = visual.GetComponent<NodePickup>();
            if (existingPickup != null)
                Destroy(existingPickup);

            // Register with proximity culler for distance-based rendering
            if (ProximityCuller.Instance != null)
                ProximityCuller.Instance.TrackNodeVisual(visual);

            nodeVisuals[key] = visual;
            node.VisualObject = visual;
            node.HasVisual = true;
            node.RevealTime = Time.time; // Track when node was revealed for grace period
        }

        /// <summary>
        /// Remove visual for a node.
        /// </summary>
        private void RemoveNodeVisual(HiddenNode node)
        {
            string key = GetNodeKey(node);

            if (nodeVisuals.TryGetValue(key, out var visual))
            {
                if (visual != null)
                    Destroy(visual);
                nodeVisuals.Remove(key);
            }

            if (node.VisualObject != null)
            {
                Destroy(node.VisualObject);
                node.VisualObject = null;
            }

            node.HasVisual = false;
        }

        #endregion

        #region Node Breaking

        /// <summary>
        /// Break a node - play crumble animation, then spawn pickups.
        /// Animation from FixedResourceNode: 0.6s duration, shrink, shake, flash.
        /// </summary>
        private void BreakNode(HiddenNode node)
        {
            if (node.IsBroken)
                return;

            node.IsBroken = true;

            // Track in broken nodes for save
            if (!brokenNodeIds.TryGetValue(node.ChunkCoord, out var brokenSet))
            {
                brokenSet = new HashSet<int>();
                brokenNodeIds[node.ChunkCoord] = brokenSet;
            }
            brokenSet.Add(node.NodeId);

            // Play crumble animation if visual exists, otherwise spawn immediately
            if (node.HasVisual && node.VisualObject != null)
            {
                StartCoroutine(PlayCrumbleAndSpawnPickups(node));
            }
            else
            {
                SpawnPickupsForNode(node);
            }

            OnNodeBroken?.Invoke(node);
        }

        /// <summary>
        /// Play a random disintegration sound at the specified position.
        /// </summary>
        private void PlayDisintegrationSound(Vector3 position)
        {
            if (disintegrationSounds == null || disintegrationSounds.Length == 0)
                return;

            // Select random sound from array
            AudioClip clip = disintegrationSounds[UnityEngine.Random.Range(0, disintegrationSounds.Length)];
            if (clip == null)
                return;

            // Play at node's world position
            AudioSource.PlayClipAtPoint(clip, position, disintegrationVolume);
        }

        /// <summary>
        /// Play a random falling stones sound at the specified position.
        /// Uses boosted audio settings for better audibility.
        /// </summary>
        private void PlayFallingStonesSound(Vector3 position)
        {
            if (fallingStonesSounds == null || fallingStonesSounds.Length == 0)
                return;

            // Select random sound from array
            AudioClip clip = fallingStonesSounds[UnityEngine.Random.Range(0, fallingStonesSounds.Length)];
            if (clip == null)
                return;

            // Create a temporary AudioSource with boosted settings (same as NodePickup)
            GameObject tempGO = new GameObject("TempAudio_FallingStones");
            tempGO.transform.position = position;

            AudioSource audioSrc = tempGO.AddComponent<AudioSource>();
            audioSrc.clip = clip;
            audioSrc.volume = fallingStonesVolume;
            audioSrc.spatialBlend = 0.3f; // Mostly 2D for better audibility
            audioSrc.rolloffMode = AudioRolloffMode.Linear;
            audioSrc.minDistance = 5f;
            audioSrc.maxDistance = 20f;
            audioSrc.Play();

            // Destroy after clip finishes
            Destroy(tempGO, clip.length + 0.1f);
        }

        /// <summary>
        /// Crumble animation from FixedResourceNode.
        /// Duration: 0.6s, shrink to 0.1, shake intensity 0.3, color flash.
        /// </summary>
        private System.Collections.IEnumerator PlayCrumbleAndSpawnPickups(HiddenNode node)
        {
            GameObject visual = node.VisualObject;

            if (visual == null)
            {
                SpawnPickupsForNode(node);
                yield break;
            }

            // Play disintegration sound at node position
            PlayDisintegrationSound(node.WorldPosition);

            Vector3 startScale = visual.transform.localScale;
            Vector3 startPos = visual.transform.position;
            float duration = 0.6f; // From FixedResourceNode
            float elapsed = 0f;

            // Get renderer for color flash
            var renderer = visual.GetComponentInChildren<Renderer>();
            Color originalColor = Color.white;
            if (renderer != null && renderer.material != null)
            {
                originalColor = renderer.material.color;
            }

            while (elapsed < duration)
            {
                elapsed += Time.deltaTime;
                float t = elapsed / duration;

                // Shrink with easing (accelerate shrink)
                float shrinkT = t * t;
                visual.transform.localScale = Vector3.Lerp(startScale, Vector3.one * 0.1f, shrinkT);

                // Shake that decreases over time (intensity 0.3 from FixedResourceNode)
                float shakeIntensity = 0.3f * (1f - t);
                Vector3 shake = UnityEngine.Random.insideUnitSphere * shakeIntensity;
                visual.transform.position = startPos + shake;

                // Flash white then to original
                if (renderer != null && renderer.material != null)
                {
                    float flash = Mathf.PingPong(elapsed * 10f, 1f);
                    renderer.material.color = Color.Lerp(originalColor, Color.white, flash * (1f - t));
                }

                yield return null;
            }

            // Hide visual before spawning pickups
            visual.SetActive(false);

            // Spawn pickups at original position (not shaken position)
            SpawnPickupsForNode(node, startPos);

            // Remove visual
            RemoveNodeVisual(node);
        }

        /// <summary>
        /// Spawn pickup items for a broken node.
        /// </summary>
        private void SpawnPickupsForNode(HiddenNode node)
        {
            SpawnPickupsForNode(node, node.WorldPosition);
        }

        /// <summary>
        /// Spawn pickup items for a broken node at specified position.
        /// </summary>
        private void SpawnPickupsForNode(HiddenNode node, Vector3 spawnCenter)
        {
            // Play falling stones sound when fragments spawn
            PlayFallingStonesSound(spawnCenter);

            // Get resource definition for drop count
            ResourceDefinition resourceDef = null;
            int dropCount = 3;

            if (node.ResourceIndex >= 0 && config != null)
            {
                resourceDef = config.GetResourceByIndex(node.ResourceIndex);
                if (resourceDef != null)
                {
                    var tierConfig = resourceDef.GetTierConfig(node.Tier);
                    if (tierConfig != null)
                    {
                        dropCount = tierConfig.yieldAmount;
                    }
                }
            }
            else
            {
                // Fall back to legacy tier drop counts
                dropCount = config?.GetDropCountForTier(node.Tier) ?? 3;
            }

            // Find player for direction (from FixedResourceNode)
            Vector3 playerPos = spawnCenter;
            var player = GameObject.FindGameObjectWithTag("Player");
            if (player != null)
                playerPos = player.transform.position;

            Vector3 toPlayer = (playerPos - spawnCenter).normalized;

            // Find safe exit points around the node instead of naive random offsets
            Vector3[] safePoints = FindSafeSpawnPositions(spawnCenter, dropCount);

            for (int i = 0; i < dropCount; i++)
            {
                Vector3 spawnPos = safePoints[i];
                SpawnPickupItem(spawnPos, node.Tier, node.ResourceIndex, node.ResourceId, toPlayer);
            }

            OnPickupsSpawned?.Invoke(node, dropCount);
        }

        /// <summary>
        /// Find safe spawn positions around a node center.
        /// Generates candidate points on a sphere, raycasts down to find ground,
        /// validates clearance, and returns positions slightly above the surface.
        /// Falls back to upward offset if no safe point is found.
        /// </summary>
        private Vector3[] FindSafeSpawnPositions(Vector3 center, int count)
        {
            const int candidateCount = 12;
            const float sphereRadius = 0.9f;
            const float clearanceRadius = 0.15f;
            const float aboveGround = 0.08f;
            const float rayStartOffset = 1.5f;  // Start raycast this far above candidate
            const float rayMaxDistance = 4f;      // Max downward raycast distance

            Vector3[] results = new Vector3[count];
            bool[] assigned = new bool[count];
            int assignedCount = 0;

            // Generate candidate exit points on a sphere using golden spiral
            Vector3[] candidates = new Vector3[candidateCount];
            for (int i = 0; i < candidateCount; i++)
            {
                float t = (float)i / candidateCount;
                float inclination = Mathf.Acos(1f - 2f * t);
                float azimuth = Mathf.PI * 2f * 1.618033988749f * i;

                Vector3 dir = new Vector3(
                    Mathf.Sin(inclination) * Mathf.Cos(azimuth),
                    Mathf.Sin(inclination) * Mathf.Sin(azimuth),
                    Mathf.Cos(inclination)
                );

                // Add small randomness for natural look
                dir += new Vector3(
                    UnityEngine.Random.Range(-0.15f, 0.15f),
                    UnityEngine.Random.Range(-0.1f, 0.1f),
                    UnityEngine.Random.Range(-0.15f, 0.15f)
                );
                dir.Normalize();

                candidates[i] = center + dir * sphereRadius;
            }

            // Shuffle candidates so we don't always pick the same subset
            for (int i = candidateCount - 1; i > 0; i--)
            {
                int j = UnityEngine.Random.Range(0, i + 1);
                Vector3 tmp = candidates[i];
                candidates[i] = candidates[j];
                candidates[j] = tmp;
            }

            // For each candidate: try to find a safe ground position
            for (int c = 0; c < candidateCount && assignedCount < count; c++)
            {
                Vector3 candidate = candidates[c];
                Vector3 rayStart = candidate + Vector3.up * rayStartOffset;

                // Raycast down to find ground surface
                if (Physics.Raycast(rayStart, Vector3.down, out RaycastHit hit, rayMaxDistance))
                {
                    Vector3 groundPoint = hit.point + Vector3.up * aboveGround;

                    // Check the point is clear (not inside solid geometry)
                    if (!Physics.CheckSphere(groundPoint, clearanceRadius))
                    {
                        results[assignedCount] = groundPoint;
                        assigned[assignedCount] = true;
                        assignedCount++;
                        continue;
                    }

                    // Try pushing up incrementally if inside geometry
                    for (int nudge = 1; nudge <= 4; nudge++)
                    {
                        Vector3 nudgedPoint = groundPoint + Vector3.up * (nudge * 0.15f);
                        if (!Physics.CheckSphere(nudgedPoint, clearanceRadius))
                        {
                            results[assignedCount] = nudgedPoint;
                            assigned[assignedCount] = true;
                            assignedCount++;
                            break;
                        }
                    }
                }
            }

            // Fill any remaining slots with fallback (upward offset from center)
            for (int i = 0; i < count; i++)
            {
                if (!assigned[i])
                {
                    results[i] = center + new Vector3(
                        UnityEngine.Random.Range(-0.2f, 0.2f),
                        0.5f + UnityEngine.Random.Range(0f, 0.3f),
                        UnityEngine.Random.Range(-0.2f, 0.2f)
                    );
                }
            }

            return results;
        }

        /// <summary>
        /// Spawn a single pickup item.
        /// Uses resource-specific or tier-based prefabs (SMALL, collectible).
        /// </summary>
        private void SpawnPickupItem(Vector3 position, int tier, int resourceIndex = -1, string resourceId = null, Vector3 toPlayer = default)
        {
            GameObject pickup;
            GameObject prefab = null;
            ResourceDefinition resourceDef = null;
            Color resourceColor = Color.gray;
            float pickupScale = config?.pickupPieceScaleMultiplier ?? 0.15f;

            // Try to get resource-specific prefab first
            if (resourceIndex >= 0 && config != null)
            {
                resourceDef = config.GetResourceByIndex(resourceIndex);
                if (resourceDef != null)
                {
                    var tierConfig = resourceDef.GetTierConfig(tier);
                    if (tierConfig != null)
                    {
                        prefab = tierConfig.pickupPrefab;
                        if (tierConfig.pickupPrefabScale > 0)
                        {
                            pickupScale = tierConfig.pickupPrefabScale;
                        }
                    }
                    resourceColor = resourceDef.resourceColor;
                }
            }

            // Fall back to tier-based prefab
            if (prefab == null)
            {
                prefab = GetPickupPrefabForTier(tier);
            }

            if (prefab != null)
            {
                // Use configured prefab
                pickup = Instantiate(prefab, position, UnityEngine.Random.rotation);

                // Apply fixed scale (from FixedResourceNode: 1.5x1.5x1.5)
                pickup.transform.localScale = Vector3.one * pickupScale;

                // Ensure collider exists and is solid (not trigger) for physics
                var existingCollider = pickup.GetComponent<Collider>();
                if (existingCollider == null)
                {
                    existingCollider = pickup.AddComponent<SphereCollider>();
                }
                existingCollider.isTrigger = false;
            }
            else
            {
                // Fallback: Create simple sphere
                pickup = GameObject.CreatePrimitive(PrimitiveType.Sphere);
                pickup.transform.position = position;
                pickup.transform.rotation = UnityEngine.Random.rotation;

                // Fixed scale (from FixedResourceNode: 1.5x1.5x1.5)
                pickup.transform.localScale = Vector3.one * pickupScale;

                // Set material with URP shader
                var renderer = pickup.GetComponent<Renderer>();
                if (renderer != null)
                {
                    Shader shader = Shader.Find("Universal Render Pipeline/Lit");
                    if (shader == null)
                        shader = Shader.Find("Standard");

                    Material mat = new Material(shader);

                    // Use resource color if available, otherwise tier-based
                    Color pickupColor;
                    if (resourceDef != null)
                    {
                        pickupColor = resourceColor;
                    }
                    else
                    {
                        pickupColor = tier switch
                        {
                            1 => new Color(0.72f, 0.45f, 0.20f), // Copper
                            2 => new Color(0.75f, 0.75f, 0.78f), // Silver
                            3 => new Color(1.0f, 0.84f, 0.0f),   // Gold
                            4 => new Color(0.58f, 0.44f, 0.86f), // Purple
                            _ => Color.gray
                        };
                    }
                    mat.SetColor("_BaseColor", pickupColor);
                    mat.SetFloat("_Metallic", 0.5f);
                    mat.SetFloat("_Smoothness", 0.5f);
                    renderer.material = mat;
                }

                // Solid collider for physics
                var collider = pickup.GetComponent<SphereCollider>();
                if (collider != null)
                    collider.isTrigger = false;
            }

            // Name includes resource type and tier
            string displayName = resourceDef?.displayName ?? "Unknown";
            pickup.name = $"Pickup_{displayName}_T{tier}";

            // Add Rigidbody for physics
            var rb = pickup.GetComponent<Rigidbody>();
            if (rb == null)
            {
                rb = pickup.AddComponent<Rigidbody>();
            }
            rb.mass = 0.3f;
            rb.drag = 2f;
            rb.angularDrag = 2f;
            rb.collisionDetectionMode = CollisionDetectionMode.Discrete;

            // Very gentle push towards player (from FixedResourceNode)
            Vector3 pushDir;
            if (toPlayer != default && toPlayer.sqrMagnitude > 0.01f)
            {
                pushDir = toPlayer + new Vector3(
                    UnityEngine.Random.Range(-0.2f, 0.2f),
                    0.1f, // Tiny upward
                    UnityEngine.Random.Range(-0.2f, 0.2f)
                );
                rb.velocity = pushDir.normalized * UnityEngine.Random.Range(0.5f, 1.0f); // Very gentle
            }
            else
            {
                // Fallback: random outward burst
                Vector3 randomDir = UnityEngine.Random.onUnitSphere;
                randomDir.y = Mathf.Abs(randomDir.y) * 0.5f;
                rb.velocity = randomDir * UnityEngine.Random.Range(0.5f, 1.0f);
            }

            // Add NodePickup component for collectibility - THIS IS THE KEY DIFFERENCE
            // Get credit value from resource definition
            int creditValue = 1;
            if (resourceDef != null)
            {
                creditValue = resourceDef.creditValuePerUnit;
            }

            var existingPickup = pickup.GetComponent<NodePickup>();
            if (existingPickup == null)
            {
                var pickupComp = pickup.AddComponent<NodePickup>();
                pickupComp.Initialize(tier, 1, resourceIndex, resourceId, creditValue);
            }
            else
            {
                existingPickup.Initialize(tier, 1, resourceIndex, resourceId, creditValue);
            }

            // Add larger trigger collider for easier interaction detection (from FixedResourceNode)
            // This doesn't affect physics - just makes it easier to aim at
            GameObject interactionZone = new GameObject("InteractionZone");
            interactionZone.transform.SetParent(pickup.transform);
            interactionZone.transform.localPosition = Vector3.zero;
            var interactionCollider = interactionZone.AddComponent<SphereCollider>();
            interactionCollider.radius = 0.5f; // Good balance for interaction
            interactionCollider.isTrigger = true;

            // Add InteractionForwarder if it exists
            var forwarderType = System.Type.GetType("BeneathTheFloor.ResourceSystem.InteractionForwarder");
            if (forwarderType != null)
            {
                interactionZone.AddComponent(forwarderType);
            }

            // Register with ProximityCuller for distance-based rendering
            if (ProximityCuller.Instance != null)
                ProximityCuller.Instance.TrackPickup(pickup);

        }

        #endregion

        #region Save/Load

        public List<ChunkNodesSaveData> GetSaveData()
        {
            var result = new List<ChunkNodesSaveData>();

            // Save all nodes that are either revealed OR broken from each chunk
            foreach (var kvp in nodesByChunk)
            {
                var chunkCoord = kvp.Key;
                var nodes = kvp.Value;

                var nodesToSave = new List<NodeSaveData>();
                foreach (var node in nodes)
                {
                    if (node.HasVisual || node.IsBroken)
                    {
                        nodesToSave.Add(new NodeSaveData(node));
                    }
                }

                if (nodesToSave.Count > 0)
                {
                    result.Add(new ChunkNodesSaveData(chunkCoord, nodesToSave.ToArray()));
                }
            }

            // Save bonus nodes that are revealed or broken under a special "global" chunk coordinate
            var bonusNodesToSave = globalBonusNodes
                .Where(n => n.HasVisual || n.IsBroken)
                .Select(n => new NodeSaveData(n))
                .ToArray();
            if (bonusNodesToSave.Length > 0)
            {
                result.Add(new ChunkNodesSaveData(new Vector3Int(int.MaxValue, int.MaxValue, int.MaxValue), bonusNodesToSave));
            }

            return result;
        }

        // Store broken bonus node IDs for applying after generation
        private HashSet<int> brokenBonusNodeIds = new HashSet<int>();
        // Store revealed bonus node IDs for applying after generation
        private HashSet<int> revealedBonusNodeIds = new HashSet<int>();

        // Store revealed node IDs per chunk for applying after generation
        private Dictionary<Vector3Int, HashSet<int>> revealedNodeIds = new Dictionary<Vector3Int, HashSet<int>>();

        public void LoadSaveData(List<ChunkNodesSaveData> saveData, int savedWorldSeed)
        {
            // Mark as loaded from save - bypasses startup grace period
            loadedFromSave = true;

            brokenNodeIds.Clear();
            brokenBonusNodeIds.Clear();
            revealedNodeIds.Clear();
            revealedBonusNodeIds.Clear();
            worldSeed = savedWorldSeed;

            // Destroy all existing node visuals before clearing data
            foreach (var kvp in nodeVisuals)
            {
                if (kvp.Value != null)
                    Destroy(kvp.Value);
            }
            nodeVisuals.Clear();

            if (saveData == null)
            {
                // Still need to clear and allow regeneration
                nodesByChunk.Clear();
                globalBonusNodes.Clear();
                bonusNodesGenerated = false;
                return;
            }

            // Special chunk coord for global bonus nodes
            var bonusChunkCoord = new Vector3Int(int.MaxValue, int.MaxValue, int.MaxValue);

            foreach (var chunkData in saveData)
            {
                var chunkCoord = chunkData.GetChunkCoord();

                // Check if this is bonus node save data
                if (chunkCoord == bonusChunkCoord)
                {
                    if (chunkData.BrokenNodes != null)
                    {
                        foreach (var nodeSave in chunkData.BrokenNodes)
                        {
                            if (nodeSave.IsBroken)
                                brokenBonusNodeIds.Add(nodeSave.NodeId);
                            else if (nodeSave.IsRevealed)
                                revealedBonusNodeIds.Add(nodeSave.NodeId);
                        }
                    }
                    continue;
                }

                var brokenSet = new HashSet<int>();
                var revealedSet = new HashSet<int>();

                if (chunkData.BrokenNodes != null)
                {
                    foreach (var nodeSave in chunkData.BrokenNodes)
                    {
                        if (nodeSave.IsBroken)
                            brokenSet.Add(nodeSave.NodeId);
                        else if (nodeSave.IsRevealed)
                            revealedSet.Add(nodeSave.NodeId);
                    }
                }

                if (brokenSet.Count > 0)
                    brokenNodeIds[chunkCoord] = brokenSet;
                if (revealedSet.Count > 0)
                    revealedNodeIds[chunkCoord] = revealedSet;
            }

            // Clear node data so it regenerates with the loaded seed
            nodesByChunk.Clear();
            globalBonusNodes.Clear();
            bonusNodesGenerated = false;

            // Force regenerate chunks that have saved data
            ForceRegenerateLoadedChunks();
        }

        /// <summary>
        /// Force regenerate all chunks that have saved revealed/broken node data.
        /// This ensures nodes appear immediately after loading.
        /// </summary>
        private void ForceRegenerateLoadedChunks()
        {
            // Collect all chunk coordinates that need regeneration
            var chunksToRegenerate = new HashSet<Vector3Int>();

            foreach (var chunkCoord in brokenNodeIds.Keys)
                chunksToRegenerate.Add(chunkCoord);
            foreach (var chunkCoord in revealedNodeIds.Keys)
                chunksToRegenerate.Add(chunkCoord);

            // Regenerate each chunk (this will restore revealed visuals)
            foreach (var chunkCoord in chunksToRegenerate)
            {
                GetNodesForChunk(chunkCoord); // This applies saved state and creates visuals
            }

            // Regenerate bonus nodes
            GenerateGlobalBonusNodes();
        }

        public int GetWorldSeed() => worldSeed;

        public void SetWorldSeed(int seed)
        {
            worldSeed = seed;
            nodesByChunk.Clear();
            globalBonusNodes.Clear();
            bonusNodesGenerated = false;
        }

        /// <summary>
        /// Force regenerate global bonus nodes (for editor testing).
        /// </summary>
        [ContextMenu("Regenerate Bonus Nodes")]
        public void RegenerateBonusNodes()
        {
            globalBonusNodes.Clear();
            bonusNodesGenerated = false;
            GenerateGlobalBonusNodes();
        }

        /// <summary>
        /// Clear all cached nodes and regenerate. Call this if pit position changes.
        /// </summary>
        [ContextMenu("Clear and Regenerate All Nodes")]
        public void ClearAndRegenerateNodes()
        {
            // Destroy all visuals
            foreach (var kvp in nodeVisuals)
            {
                if (kvp.Value != null)
                    Destroy(kvp.Value);
            }
            nodeVisuals.Clear();

            // Clear node data
            nodesByChunk.Clear();

            // Clear global bonus nodes so they regenerate
            globalBonusNodes.Clear();
            bonusNodesGenerated = false;
        }

        /// <summary>
        /// Force clear ALL save data and node state. Use this to start completely fresh.
        /// </summary>
        [ContextMenu("Force Clear All Save Data")]
        public void ForceClearAllSaveData()
        {
            // Clear save data collections
            brokenNodeIds.Clear();
            brokenBonusNodeIds.Clear();
            revealedNodeIds.Clear();
            revealedBonusNodeIds.Clear();

            // Destroy all visuals
            foreach (var kvp in nodeVisuals)
            {
                if (kvp.Value != null)
                    Destroy(kvp.Value);
            }
            nodeVisuals.Clear();

            // Clear node data
            nodesByChunk.Clear();
            globalBonusNodes.Clear();
            bonusNodesGenerated = false;

            // Reset save load flag so new game has startup grace period
            loadedFromSave = false;
        }

        #endregion

        #region Radar Detection

        /// <summary>
        /// Get all unbroken nodes within a certain range of a position.
        /// Used by radar tool to detect nearby resources.
        /// </summary>
        public List<HiddenNode> GetNearbyUnbrokenNodes(Vector3 position, float maxRange)
        {
            List<HiddenNode> result = new List<HiddenNode>();

            // Check chunk-based nodes
            foreach (var kvp in nodesByChunk)
            {
                foreach (var node in kvp.Value)
                {
                    if (node.IsBroken) continue;
                    if (node.ResourceIndex < 0 && !node.IsBonusNode) continue; // Skip legacy nodes

                    float distance = Vector3.Distance(position, node.WorldPosition);
                    if (distance <= maxRange)
                    {
                        result.Add(node);
                    }
                }
            }

            // Check global bonus nodes
            foreach (var node in globalBonusNodes)
            {
                if (node.IsBroken) continue;

                float distance = Vector3.Distance(position, node.WorldPosition);
                if (distance <= maxRange)
                {
                    result.Add(node);
                }
            }

            return result;
        }

        /// <summary>
        /// Get the nearest unbroken node to a position within max range.
        /// Returns null if no nodes are in range.
        /// </summary>
        public HiddenNode GetNearestUnbrokenNode(Vector3 position, float maxRange)
        {
            HiddenNode nearest = null;
            float nearestDistance = float.MaxValue;

            // Check chunk-based nodes
            foreach (var kvp in nodesByChunk)
            {
                foreach (var node in kvp.Value)
                {
                    if (node.IsBroken) continue;
                    if (node.ResourceIndex < 0 && !node.IsBonusNode) continue;

                    float distance = Vector3.Distance(position, node.WorldPosition);
                    if (distance <= maxRange && distance < nearestDistance)
                    {
                        nearest = node;
                        nearestDistance = distance;
                    }
                }
            }

            // Check global bonus nodes
            foreach (var node in globalBonusNodes)
            {
                if (node.IsBroken) continue;

                float distance = Vector3.Distance(position, node.WorldPosition);
                if (distance <= maxRange && distance < nearestDistance)
                {
                    nearest = node;
                    nearestDistance = distance;
                }
            }

            return nearest;
        }

        #endregion

        #region Debug Gizmos

        private void OnDrawGizmosSelected()
        {
            if (!showNodeGizmos || !enableNodeSystem)
                return;

            // Draw chunk-based nodes
            foreach (var kvp in nodesByChunk)
            {
                foreach (var node in kvp.Value)
                {
                    DrawNodeGizmo(node);
                }
            }

            // Draw global bonus nodes
            foreach (var bonusNode in globalBonusNodes)
            {
                DrawNodeGizmo(bonusNode);
            }
        }

        private void DrawNodeGizmo(HiddenNode node)
        {
            // Skip legacy nodes when resource system is active (but NOT bonus nodes)
            if (node.ResourceIndex < 0 && !node.IsBonusNode && config != null && config.resources != null && config.resources.Count > 0)
            {
                return; // Don't draw gizmos for legacy nodes without resources
            }

            if (node.IsBroken)
            {
                Gizmos.color = new Color(0.3f, 0.3f, 0.3f, 0.3f);
            }
            else if (node.HasVisual)
            {
                // Spawned/revealed - color by exposure level
                float exposure = node.CurrentExposure;
                Gizmos.color = Color.Lerp(Color.yellow, Color.red, exposure);
            }
            else if (node.IsBonusNode)
            {
                // BONUS NODE - Cyan color to stand out
                Gizmos.color = Color.cyan;
            }
            else
            {
                // Hidden nodes - COLOR BY RESOURCE TYPE
                Color resourceColor = node.ResourceId?.ToLower() switch
                {
                    "stone" => new Color(0.6f, 0.6f, 0.6f),     // Stone = Gray
                    "iron" => new Color(0.8f, 0.2f, 0.2f),      // Iron = Red
                    "coal" => new Color(0.2f, 0.2f, 0.2f),      // Coal = Dark Gray
                    "copper" => new Color(1f, 0.5f, 0.2f),      // Copper = Orange
                    _ => Color.white
                };

                // Brighten based on tier (T1=normal, T4=brightest)
                float tierBrightness = 0.7f + (node.Tier * 0.1f);
                Gizmos.color = resourceColor * tierBrightness;
            }

            // Draw exposure radius (small solid sphere - actual node size)
            Gizmos.DrawWireSphere(node.WorldPosition, exposureRadius);

            // BONUS NODE: Draw special marker (diamond shape using lines)
            if (node.IsBonusNode && !node.IsBroken)
            {
                Gizmos.color = Color.cyan;
                float size = 0.4f;
                Vector3 p = node.WorldPosition;

                // Draw diamond/star shape
                Gizmos.DrawLine(p + Vector3.up * size, p + Vector3.right * size);
                Gizmos.DrawLine(p + Vector3.right * size, p + Vector3.down * size);
                Gizmos.DrawLine(p + Vector3.down * size, p + Vector3.left * size);
                Gizmos.DrawLine(p + Vector3.left * size, p + Vector3.up * size);

                Gizmos.DrawLine(p + Vector3.up * size, p + Vector3.forward * size);
                Gizmos.DrawLine(p + Vector3.forward * size, p + Vector3.down * size);
                Gizmos.DrawLine(p + Vector3.down * size, p + Vector3.back * size);
                Gizmos.DrawLine(p + Vector3.back * size, p + Vector3.up * size);

                // Spawn radius for bonus nodes in cyan
                Gizmos.color = new Color(0f, 1f, 1f, 0.3f);
                Gizmos.DrawWireSphere(node.WorldPosition, spawnRadius);
            }
            else
            {
                // Regular node spawn radius - USE RESOURCE COLOR (semi-transparent)
                Color spawnColor = node.ResourceId?.ToLower() switch
                {
                    "stone" => new Color(0.6f, 0.6f, 0.6f, 0.2f),   // Stone = Gray
                    "iron" => new Color(0.8f, 0.2f, 0.2f, 0.2f),    // Iron = Red
                    "coal" => new Color(0.3f, 0.3f, 0.3f, 0.2f),    // Coal = Dark Gray
                    "copper" => new Color(1f, 0.5f, 0.2f, 0.2f),    // Copper = Orange
                    _ => new Color(1f, 1f, 1f, 0.15f)
                };
                Gizmos.color = spawnColor;
                Gizmos.DrawWireSphere(node.WorldPosition, spawnRadius);
            }

            // Draw node center point - RESOURCE COLORED with tier size
            Color centerColor = node.IsBonusNode ? Color.cyan : node.ResourceId?.ToLower() switch
            {
                "stone" => new Color(0.6f, 0.6f, 0.6f),
                "iron" => new Color(0.8f, 0.2f, 0.2f),
                "coal" => new Color(0.3f, 0.3f, 0.3f),
                "copper" => new Color(1f, 0.5f, 0.2f),
                _ => Color.white
            };
            Gizmos.color = centerColor;
            // Center dot size increases with tier
            float dotSize = 0.05f + (node.Tier * 0.02f);
            Gizmos.DrawSphere(node.WorldPosition, dotSize);
        }

        #endregion
    }
}
