using UnityEngine;
using System.Collections.Generic;
using System;

namespace BeneathTheFloor.Digging
{
    /// <summary>
    /// Manages all voxel chunks in the world.
    /// Responsible for chunk creation, destruction, and coordinating dig operations.
    ///
    /// Implements IDiggableTerrain for compatibility with other systems.
    ///
    /// FUTURE OPTIMIZATION:
    /// - Chunk loading/unloading based on player position
    /// - Async chunk generation using Job System
    /// - Chunk streaming for very large worlds
    /// </summary>
    [DefaultExecutionOrder(20)] // Run AFTER DigBoundsProvider (10) but before WorldBootstrapper (50)
    public class ChunkManager : MonoBehaviour, IDiggableTerrain
    {
        [Header("Terrain Configuration")]
        [Tooltip("Size of each voxel in meters.")]
        [SerializeField] private float voxelSize = 0.35f;

        [Tooltip("Material for terrain rendering.")]
        [SerializeField] private Material terrainMaterial;

        [Header("World Bounds")]
        [Tooltip("Minimum chunk coordinate on each axis.")]
        [SerializeField] private Vector3Int minChunkCoord = new Vector3Int(-4, -20, -4);

        [Tooltip("Maximum chunk coordinate on each axis.")]
        [SerializeField] private Vector3Int maxChunkCoord = new Vector3Int(4, 0, 4);

        [Header("Generation")]
        [Tooltip("Generate surface terrain automatically. DISABLE if using WorldBootstrapper.")]
        [SerializeField] private bool autoGenerateSurface = false;

        [Tooltip("Y level of the surface in world units.")]
        [SerializeField] private float surfaceY = 0f;

        [Tooltip("Thickness of surface gradient in voxels.")]
        [SerializeField] private float surfaceGradientThickness = 4f;

        [Header("Dig Mode")]
        [Tooltip("CRITICAL: When true, dig operations can ONLY remove terrain (decrease density). Set to true for gameplay.")]
        [SerializeField] private bool digRemovalOnly = true;

        [Header("Collider Scheduling")]
        [Tooltip("Delay before updating collider after mesh change (seconds).")]
        [SerializeField] private float colliderUpdateDelay = 0.1f;

        [Tooltip("Maximum collider updates per frame.")]
        [SerializeField] private int maxColliderUpdatesPerFrame = 4;

        [Header("Protected Zones")]
        [Tooltip("Thickness of bedrock at bottom in voxels.")]
        [SerializeField] private int bedrockThickness = 2;

        [Tooltip("Thickness of boundary walls in voxels.")]
        [SerializeField] private int boundaryThickness = 2;

        [Header("Dig Bounds Constraint")]
        [Tooltip("Reference to DigBoundsProvider. If null, finds via singleton.")]
        [SerializeField] private DigBoundsProvider digBoundsProvider;

        [Tooltip("When true, strictly enforce dig bounds - reject all chunks/digs outside. DISABLE for debugging.")]
        [SerializeField] private bool enforceDigBounds = false; // DISABLED - bounds were blocking all digs

        // Chunk storage
        private Dictionary<ChunkCoord, VoxelChunk> _chunks;

        // Collider scheduler
        private ColliderUpdateScheduler _colliderScheduler;

        // Parent transform for chunk GameObjects
        private Transform _chunksParent;

        // World offset for aligning chunk grid to bounds center
        private Vector3 _worldOffset = Vector3.zero;

        // ===== QUEUED REBUILD PIPELINE =====
        // Instead of immediate per-dig rebuilds, we queue dirty chunks and process with budget
        // URGENT ZONE chunks get immediate mesh+collider updates for player safety
        private HashSet<VoxelChunk> _dirtyChunkQueue = new HashSet<VoxelChunk>();
        private const int MAX_MESH_REBUILDS_FAR = 1;        // Far chunks: conservative budget
        private const float TIME_BUDGET_MS = 3.0f;
        private const int MAX_DIRTY_QUEUE_SIZE = 32;        // PERFORMANCE: Cap dirty queue to prevent unbounded growth

        // PERFORMANCE: Pooled lists to avoid GC allocations in ProcessDirtyChunks
        private List<VoxelChunk> _pooledUrgentChunks = new List<VoxelChunk>(16);
        private List<VoxelChunk> _pooledFarChunks = new List<VoxelChunk>(16);

        // URGENT COLLIDER ZONE: Chunks within this radius of player get IMMEDIATE updates
        private const float URGENT_RADIUS_METERS = 8f;      // Radius for urgent mesh updates
        private const int MAX_URGENT_REBUILDS_PER_FRAME = 2; // Collider updates per frame (meshes unlimited)

        // Track player position for urgent zone
        private Transform _playerTransform;
        private Vector3 _lastKnownPlayerPos;

        // Track recent dig for prioritization
        private Vector3 _lastDigPosition;
        private float _lastDigTime;

        // RECENTLY MODIFIED CHUNKS: For floating island detection
        // Chunks modified by dig operations that need island cleanup check
        private HashSet<VoxelChunk> _recentlyModifiedChunks = new HashSet<VoxelChunk>();
        private const float MODIFIED_CHUNK_EXPIRY = 5f; // Clear after 5 seconds if not processed
        private const int MAX_RECENTLY_MODIFIED_SIZE = 16; // PERFORMANCE: Cap to prevent unbounded growth

        // ===== FLOATING ISLAND CLEANUP (ANCHOR FLOOD-FILL) =====
        // Single source of truth for surface threshold
        public const float ISO_LEVEL = 0.5f;
        // Max chunks to cleanup per dig operation (performance limit)
        private const int MAX_CHUNKS_TO_CLEANUP_PER_DIG = 1; // Reduced for better FPS
        // Chunks within this many chunk-widths of player get immediate collider update
        private const float URGENT_CLEANUP_CHUNK_RADIUS = 2f;
        // Debug toggle for island cleanup logging (DISABLE for better performance)
        private bool _debugIslandCleanup = false;
        // MICRO-ISLAND KILLER: Remove tiny floating remnants after anchor flood-fill
        // Components with <= this many voxels that don't touch y==0 are deleted
        private const int MICRO_ISLAND_MAX_VOXELS = 24;
        private float _lastModifiedChunkClearTime;

        // PERFORMANCE: Only run island cleanup every N digs instead of every dig
        private int _digsSinceLastIslandCleanup = 0;
        private const int DIGS_BETWEEN_ISLAND_CLEANUP = 5; // Only check for floating islands every 5 digs

        // PERFORMANCE: Enable/disable floating island cleanup entirely
        [Header("Performance - Floating Island Cleanup")]
        [Tooltip("Enable floating island detection and removal. DISABLE for better performance.")]
        [SerializeField] private bool enableFloatingIslandCleanup = false; // DISABLED by default for performance

        // PERFORMANCE: Pooled arrays for island cleanup to reduce GC allocations
        // These are reused instead of creating new arrays each call
        private static bool[,,] _pooledSolidArray;
        private static bool[,,] _pooledVisitedArray;
        private static Queue<Vector3Int> _pooledBfsQueue;
        private static readonly Vector3Int[] _bfsDirections = new Vector3Int[]
        {
            new Vector3Int(1, 0, 0),
            new Vector3Int(-1, 0, 0),
            new Vector3Int(0, 1, 0),
            new Vector3Int(0, -1, 0),
            new Vector3Int(0, 0, 1),
            new Vector3Int(0, 0, -1)
        };

        // PERFORMANCE: Chunk unloading by distance
        [Header("Performance - Chunk Unloading")]
        [Tooltip("Enable unloading chunks far from player to save memory and CPU.")]
        [SerializeField] private bool enableChunkUnloading = false;

        [Tooltip("Distance in chunks beyond which chunks get unloaded.")]
        [SerializeField] private int chunkUnloadDistance = 6; // ~33 meters at 0.35 voxel size

        [Tooltip("Seconds between chunk unload checks.")]
        [SerializeField] private float chunkUnloadInterval = 2f;

        private float _lastChunkUnloadCheck = 0f;
        private const int MAX_CHUNKS_TO_UNLOAD_PER_CHECK = 4; // Don't unload too many at once

        // Throttled logging for out-of-bounds
        private float _lastBoundsLogTime;
        private const float BOUNDS_LOG_INTERVAL = 2f;

        // Event when dig occurs (for other systems to listen)
        public event Action<DigResult> OnDigCompleted;

        // Singleton (optional - can be disabled if multiple managers are needed)
        public static ChunkManager Instance { get; private set; }

        // Public accessors
        public float VoxelSize => voxelSize;
        public int ChunkSize => ChunkMesher.CHUNK_SIZE;
        public int LoadedChunkCount => _chunks?.Count ?? 0;

        /// <summary>
        /// World offset applied to align chunk grid with dig bounds.
        /// </summary>
        public Vector3 WorldOffset => _worldOffset;

        /// <summary>
        /// Disable auto surface generation. Call this before Start() if using WorldBootstrapper.
        /// </summary>
        public void DisableAutoGenerate()
        {
            autoGenerateSurface = false;
        }

        /// <summary>
        /// When true, dig operations can ONLY remove terrain (decrease density).
        /// This prevents accidental terrain creation.
        /// </summary>
        public bool DigRemovalOnly => digRemovalOnly;

        #region Unity Lifecycle

        private void Awake()
        {
            // Singleton setup
            if (Instance != null && Instance != this)
            {
                Debug.LogWarning("[ChunkManager] Multiple instances detected. Destroying duplicate.");
                Destroy(gameObject);
                return;
            }
            Instance = this;

            // Initialize
            _chunks = new Dictionary<ChunkCoord, VoxelChunk>(256);

            // RUNTIME OVERRIDE: Force immediate collider updates for responsive digging
            // Scene-serialized delay might cause second dig to miss
            if (colliderUpdateDelay > 0.01f)
            {
                colliderUpdateDelay = 0f;
            }
            // PERFORMANCE: Limit collider updates per frame (was 8->4->2 for better FPS)
            if (maxColliderUpdatesPerFrame < 2)
            {
                maxColliderUpdatesPerFrame = 2;
            }
            else if (maxColliderUpdatesPerFrame > 2)
            {
                maxColliderUpdatesPerFrame = 2;
            }

            _colliderScheduler = new ColliderUpdateScheduler(colliderUpdateDelay, maxColliderUpdatesPerFrame);

            // Create parent for chunks
            var parentObj = new GameObject("Chunks");
            parentObj.transform.SetParent(transform, false);
            _chunksParent = parentObj.transform;
        }

        private void Start()
        {
            // Validate material - try to load triplanar material if none assigned
            if (terrainMaterial == null)
            {
                terrainMaterial = TryLoadTriplanarMaterial();
                if (terrainMaterial == null)
                {
                    terrainMaterial = CreateFallbackMaterial();
                }
            }

            // TEMPORARY: Force disable bounds checking until bounds are properly configured
            // TODO: Remove this once ModularShaftWallManager bounds work correctly
            if (enforceDigBounds)
            {
                enforceDigBounds = false;
            }

            // Find DigBoundsProvider if not assigned
            if (digBoundsProvider == null)
            {
                digBoundsProvider = DigBoundsProvider.Instance;
            }

            if (enforceDigBounds && digBoundsProvider == null)
            {
                enforceDigBounds = false;
            }
            else if (enforceDigBounds && digBoundsProvider != null && !digBoundsProvider.HasValidBounds)
            {
                enforceDigBounds = false;
            }

            // Generate initial chunks if enabled
            // NOTE: If using WorldBootstrapper, this should be FALSE (WorldBootstrapper handles generation)
            if (autoGenerateSurface)
            {
                GenerateInitialTerrain();
            }

            // Find player for urgent zone tracking
            FindPlayer();

            // Calculate and apply world offset to align chunks with dig bounds
            // This MUST happen before any chunks are created
            CalculateAndApplyWorldOffset();
        }

        /// <summary>
        /// Calculate the world offset needed to align chunk grid with dig bounds center.
        /// The chunk grid starts at (0,0,0) by default, but we need it to be centered on the actual dig area.
        /// </summary>
        private void CalculateAndApplyWorldOffset()
        {
            // Wait for DigBoundsProvider to have valid bounds
            if (digBoundsProvider == null || !digBoundsProvider.HasValidBounds)
            {
                return;
            }

            Bounds digBounds = digBoundsProvider.DigBounds;
            Vector3 boundsCenter = digBounds.center;

            // Calculate what chunk coords the bounds center maps to
            float chunkWorldSize = ChunkMesher.CHUNK_SIZE * voxelSize;

            // The chunk that contains the bounds center
            ChunkCoord centerChunkCoord = WorldToChunkCoord(boundsCenter);

            // The world position of that chunk's center (on the standard grid starting from 0,0,0)
            Vector3 chunkCenterOnGrid = new Vector3(
                (centerChunkCoord.X + 0.5f) * chunkWorldSize,
                (centerChunkCoord.Y + 0.5f) * chunkWorldSize,
                (centerChunkCoord.Z + 0.5f) * chunkWorldSize);

            // The offset needed to align the grid chunk center with the actual bounds center
            _worldOffset = boundsCenter - chunkCenterOnGrid;

            // Apply offset to chunks parent
            if (_chunksParent != null)
            {
                _chunksParent.position = _worldOffset;
            }
        }

        /// <summary>
        /// Find the player transform for urgent zone calculations.
        /// </summary>
        private void FindPlayer()
        {
            if (_playerTransform != null) return;

            var playerObj = GameObject.FindGameObjectWithTag("Player");
            if (playerObj != null)
            {
                _playerTransform = playerObj.transform;
                _lastKnownPlayerPos = _playerTransform.position;
            }
        }

        private void Update()
        {
            // Update player position for urgent zone
            if (_playerTransform == null)
            {
                FindPlayer();
            }
            if (_playerTransform != null)
            {
                _lastKnownPlayerPos = _playerTransform.position;
            }

            // Process dirty chunks with urgent zone priority
            ProcessDirtyChunks();

            // Update colliders (for non-urgent chunks)
            _colliderScheduler?.ProcessScheduledUpdates();

            // PERFORMANCE: Periodically unload far chunks
            if (enableChunkUnloading && Time.time - _lastChunkUnloadCheck >= chunkUnloadInterval)
            {
                _lastChunkUnloadCheck = Time.time;
                UnloadDistantChunks();
            }

            // PERFORMANCE: Periodically clear recently modified chunks to prevent accumulation
            if (Time.time - _lastModifiedChunkClearTime >= MODIFIED_CHUNK_EXPIRY)
            {
                _lastModifiedChunkClearTime = Time.time;
                _recentlyModifiedChunks.Clear();
            }
        }

        private void OnDestroy()
        {
            if (Instance == this)
            {
                Instance = null;
            }

            // Clean up all chunks
            if (_chunks != null)
            {
                foreach (var chunk in _chunks.Values)
                {
                    chunk.Destroy();
                }
                _chunks.Clear();
            }
        }

        #endregion

        #region Terrain Generation

        /// <summary>
        /// Generate initial terrain within configured bounds.
        /// </summary>
        public void GenerateInitialTerrain()
        {
            int chunksCreated = 0;

            for (int cx = minChunkCoord.x; cx <= maxChunkCoord.x; cx++)
            {
                for (int cy = minChunkCoord.y; cy <= maxChunkCoord.y; cy++)
                {
                    for (int cz = minChunkCoord.z; cz <= maxChunkCoord.z; cz++)
                    {
                        ChunkCoord coord = new ChunkCoord(cx, cy, cz);
                        CreateChunk(coord);
                        chunksCreated++;
                    }
                }
            }

            // Initial mesh generation for all chunks
            FlushDirtyChunks();

            // Immediate collider update for initial terrain
            _colliderScheduler.FlushAllUpdates();
        }

        /// <summary>
        /// Create a chunk at the given coordinate if it doesn't exist.
        /// Returns null if chunk is outside dig bounds (when enforceDigBounds is true).
        /// </summary>
        private VoxelChunk CreateChunk(ChunkCoord coord)
        {
            if (_chunks.ContainsKey(coord))
            {
                return _chunks[coord];
            }

            // BOUNDS CHECK: Reject chunks outside dig bounds
            if (enforceDigBounds && digBoundsProvider != null && digBoundsProvider.HasValidBounds)
            {
                Bounds chunkBounds = GetChunkBounds(coord);
                if (!digBoundsProvider.DoesChunkIntersectBounds(chunkBounds))
                {
                    // Throttled log
                    digBoundsProvider.LogChunkBlocked(coord);
                    return null;
                }
            }

            var chunk = new VoxelChunk(coord, voxelSize, this, _chunksParent, terrainMaterial);

            // Initialize with terrain generator
            chunk.InitializeWithGenerator(position => GenerateDensityAt(position, coord));

            _chunks[coord] = chunk;
            return chunk;
        }

        /// <summary>
        /// Calculate the world-space AABB of a chunk.
        /// Accounts for world offset.
        /// </summary>
        public Bounds GetChunkBounds(ChunkCoord coord)
        {
            float chunkWorldSize = ChunkMesher.CHUNK_SIZE * voxelSize;
            // Add world offset to get actual world position
            Vector3 origin = _worldOffset + new Vector3(
                coord.X * chunkWorldSize,
                coord.Y * chunkWorldSize,
                coord.Z * chunkWorldSize);
            Vector3 size = Vector3.one * chunkWorldSize;
            return new Bounds(origin + size * 0.5f, size);
        }

        /// <summary>
        /// Generate initial density at a world position.
        /// </summary>
        private float GenerateDensityAt(Vector3 worldPos, ChunkCoord chunkCoord)
        {
            // Bedrock check (bottom of world)
            float bedrockY = minChunkCoord.y * ChunkMesher.CHUNK_SIZE * voxelSize + bedrockThickness * voxelSize;
            if (worldPos.y < bedrockY)
            {
                return 1.0f; // Solid bedrock
            }

            // Surface gradient
            float distFromSurface = surfaceY - worldPos.y;
            float gradientRange = surfaceGradientThickness * voxelSize;

            if (distFromSurface <= 0)
            {
                // Above surface - air
                return 0.0f;
            }
            else if (distFromSurface < gradientRange)
            {
                // In gradient zone - smooth transition
                return distFromSurface / gradientRange;
            }
            else
            {
                // Below gradient - solid
                return 1.0f;
            }
        }

        #endregion

        #region Chunk Access

        /// <summary>
        /// Get chunk at coordinate, or null if not loaded.
        /// </summary>
        public VoxelChunk GetChunk(ChunkCoord coord)
        {
            if (_chunks == null) return null;
            _chunks.TryGetValue(coord, out VoxelChunk chunk);
            return chunk;
        }

        /// <summary>
        /// Get chunk at world position, or null if not loaded.
        /// </summary>
        public VoxelChunk GetChunkAtWorld(Vector3 worldPos)
        {
            ChunkCoord coord = WorldToChunkCoord(worldPos);
            return GetChunk(coord);
        }

        /// <summary>
        /// Ensure chunk exists at coordinate, creating if needed.
        /// Returns null if chunk is outside dig bounds (when enforceDigBounds is true).
        /// </summary>
        public VoxelChunk EnsureChunk(ChunkCoord coord)
        {
            if (_chunks == null) return null;

            if (_chunks.TryGetValue(coord, out VoxelChunk existing))
            {
                return existing;
            }
            return CreateChunk(coord); // CreateChunk handles bounds check
        }

        /// <summary>
        /// Check if a chunk exists at coordinate.
        /// </summary>
        public bool HasChunk(ChunkCoord coord)
        {
            return _chunks.ContainsKey(coord);
        }

        #endregion

        #region Coordinate Conversion

        /// <summary>
        /// Convert world position to chunk coordinate.
        /// Accounts for world offset to correctly map positions when chunks are shifted.
        /// </summary>
        public ChunkCoord WorldToChunkCoord(Vector3 worldPos)
        {
            // Subtract world offset to get position relative to chunk grid origin
            Vector3 gridPos = worldPos - _worldOffset;
            float chunkWorldSize = ChunkMesher.CHUNK_SIZE * voxelSize;
            return new ChunkCoord(
                Mathf.FloorToInt(gridPos.x / chunkWorldSize),
                Mathf.FloorToInt(gridPos.y / chunkWorldSize),
                Mathf.FloorToInt(gridPos.z / chunkWorldSize));
        }

        /// <summary>
        /// Convert chunk coordinate to world position (chunk origin).
        /// Accounts for world offset.
        /// </summary>
        public Vector3 ChunkCoordToWorld(ChunkCoord coord)
        {
            float chunkWorldSize = ChunkMesher.CHUNK_SIZE * voxelSize;
            // Add world offset to get actual world position
            return _worldOffset + new Vector3(
                coord.X * chunkWorldSize,
                coord.Y * chunkWorldSize,
                coord.Z * chunkWorldSize);
        }

        #endregion

        #region IDiggableTerrain Implementation

        /// <summary>
        /// Attempt to dig at the specified world position.
        /// </summary>
        public bool TryDig(Vector3 worldPosition, float radius, float strength)
        {
            var operation = new DigOperation(worldPosition, radius, strength);
            var result = ExecuteDig(operation);
            return result.Success;
        }

        /// <summary>
        /// Get density at a world position.
        /// </summary>
        public float GetDensityAt(Vector3 worldPosition)
        {
            VoxelChunk chunk = GetChunkAtWorld(worldPosition);
            if (chunk == null)
            {
                // Outside loaded chunks - assume solid for safety
                return 1.0f;
            }
            return chunk.GetDensityWorld(worldPosition);
        }

        /// <summary>
        /// Directly set density at a world position, bypassing dig bounds checks.
        /// Used internally for floating terrain removal.
        /// </summary>
        public bool SetDensityDirect(Vector3 worldPosition, float density)
        {
            VoxelChunk chunk = GetChunkAtWorld(worldPosition);
            if (chunk == null)
            {
                return false;
            }

            // Convert world to local chunk coordinates
            Vector3 localPos = worldPosition - chunk.WorldOrigin;
            int lx = Mathf.FloorToInt(localPos.x / voxelSize);
            int ly = Mathf.FloorToInt(localPos.y / voxelSize);
            int lz = Mathf.FloorToInt(localPos.z / voxelSize);

            // Set density directly and mark chunk dirty
            chunk.SetDensityLocal(lx, ly, lz, density);
            chunk.MarkDirty();
            return true;
        }

        /// <summary>
        /// Check if position is within terrain bounds.
        /// Uses DigBoundsProvider if available and enforceDigBounds is true.
        /// </summary>
        public bool IsWithinBounds(Vector3 worldPosition)
        {
            // Use DigBoundsProvider if enforcing dig bounds
            if (enforceDigBounds && digBoundsProvider != null && digBoundsProvider.HasValidBounds)
            {
                return digBoundsProvider.IsInsideBounds(worldPosition);
            }

            // Fallback to chunk coord bounds
            ChunkCoord coord = WorldToChunkCoord(worldPosition);
            return coord.X >= minChunkCoord.x && coord.X <= maxChunkCoord.x &&
                   coord.Y >= minChunkCoord.y && coord.Y <= maxChunkCoord.y &&
                   coord.Z >= minChunkCoord.z && coord.Z <= maxChunkCoord.z;
        }

        /// <summary>
        /// Get the world bounds of the terrain.
        /// Accounts for world offset.
        /// </summary>
        public Bounds GetWorldBounds()
        {
            float chunkWorldSize = ChunkMesher.CHUNK_SIZE * voxelSize;
            // Add world offset to get actual world positions
            Vector3 min = _worldOffset + new Vector3(
                minChunkCoord.x * chunkWorldSize,
                minChunkCoord.y * chunkWorldSize,
                minChunkCoord.z * chunkWorldSize);
            Vector3 max = _worldOffset + new Vector3(
                (maxChunkCoord.x + 1) * chunkWorldSize,
                (maxChunkCoord.y + 1) * chunkWorldSize,
                (maxChunkCoord.z + 1) * chunkWorldSize);
            return new Bounds((min + max) * 0.5f, max - min);
        }

        /// <summary>
        /// Force all dirty chunks to regenerate immediately.
        /// </summary>
        public void FlushDirtyChunks()
        {
            foreach (var chunk in _chunks.Values)
            {
                chunk.UpdateMeshIfDirty();
            }
        }

        /// <summary>
        /// Flush only the currently dirty chunks: rebuild meshes AND colliders immediately.
        /// Used after super-hit digs to prevent the player falling through terrain.
        /// </summary>
        public void FlushDirtyChunksWithColliders()
        {
            if (_dirtyChunkQueue.Count == 0) return;

            foreach (var chunk in _dirtyChunkQueue)
            {
                chunk.UpdateMeshIfDirty();
                chunk.ForceUpdateCollider();
            }
            _dirtyChunkQueue.Clear();
            _colliderScheduler?.FlushAllUpdates();
            Physics.SyncTransforms();
        }

        /// <summary>
        /// FORCE update all chunk colliders immediately.
        /// CRITICAL: Call this after bootstrap/initial terrain generation.
        /// Do NOT rely on scheduler for initial world - player must have physics immediately.
        /// </summary>
        /// <returns>Number of colliders updated.</returns>
        public int ForceAllColliderUpdates()
        {
            int updated = 0;
            int failed = 0;

            foreach (var chunk in _chunks.Values)
            {
                chunk.ForceUpdateCollider();
                updated++;

                // Safety validation
                if (!chunk.ValidateCollider())
                {
                    failed++;
                }
            }

            if (failed > 0)
            {
                Debug.LogError($"[ChunkManager] ForceAllColliderUpdates: {failed} chunks FAILED collider validation!");
            }

            // Sync physics transforms to ensure colliders are immediately active
            Physics.SyncTransforms();

            return updated;
        }

        /// <summary>
        /// Validate all chunk colliders. Logs errors for any invalid colliders.
        /// </summary>
        /// <returns>Number of chunks with valid colliders.</returns>
        public int ValidateAllColliders()
        {
            int valid = 0;
            int invalid = 0;

            foreach (var chunk in _chunks.Values)
            {
                if (chunk.ValidateCollider())
                {
                    valid++;
                }
                else
                {
                    invalid++;
                    // Try to fix it
                    chunk.ForceUpdateCollider();
                }
            }

            if (invalid > 0)
            {
                Debug.LogError($"[ChunkManager] ValidateAllColliders: {invalid} chunks had invalid colliders (attempted fix).");
            }

            return valid;
        }

        #endregion

        #region Digging Operations

        /// <summary>
        /// Execute a dig operation and return the result.
        /// </summary>
        public DigResult ExecuteDig(DigOperation operation)
        {
            // DEBUG: Verbose dig logging disabled
            // Debug.Log($"[ChunkManager] ExecuteDig at {operation.WorldPosition}");

            // DIG BOUNDS CHECK: Reject digs outside dig bounds
            if (enforceDigBounds && digBoundsProvider != null && digBoundsProvider.HasValidBounds)
            {
                if (!digBoundsProvider.IsInsideBounds(operation.WorldPosition))
                {
                    Debug.LogWarning($"[ChunkManager] Dig BLOCKED - outside bounds: {operation.WorldPosition}");
                    digBoundsProvider.LogOutOfBounds("Dig", operation.WorldPosition);
                    return DigResult.Failed(operation);
                }
            }

            // Find all affected chunks
            var affectedChunks = GetChunksInSphere(operation.WorldPosition, operation.Radius);
            // Debug.Log($"[ChunkManager] Found {affectedChunks.Count} affected chunks");

            if (affectedChunks.Count == 0)
            {
                // No chunks at dig position - silently fail (player clicked outside terrain)
                return DigResult.Failed(operation);
            }

            // Check for protected zones
            if (IsInProtectedZone(operation.WorldPosition))
            {
                Debug.LogWarning($"[ChunkManager] Dig BLOCKED - protected zone at {operation.WorldPosition}");
                return DigResult.Failed(operation);
            }

            // Apply dig to each affected chunk
            float totalVolumeRemoved = 0f;
            int chunksModified = 0;

            foreach (var chunk in affectedChunks)
            {
                float volumeRemoved = chunk.ApplyDig(
                    operation.WorldPosition,
                    operation.Radius,
                    operation.Strength);

                if (volumeRemoved > 0f)
                {
                    totalVolumeRemoved += volumeRemoved;
                    chunksModified++;

                    // Mark neighboring chunks dirty if dig is near boundary
                    MarkNeighborsDirtyIfNeeded(chunk, operation.WorldPosition, operation.Radius);
                }
            }

            // QUEUED REBUILD: Add dirty chunks to the queue for budgeted processing
            // This prevents frame spikes when multiple digs happen rapidly
            if (chunksModified > 0)
            {
                // Track dig position for priority processing
                _lastDigPosition = operation.WorldPosition;
                _lastDigTime = Time.time;

                foreach (var chunk in affectedChunks)
                {
                    if (chunk.IsDirty)
                    {
                        // PERFORMANCE: Cap queue size to prevent unbounded growth
                        if (_dirtyChunkQueue.Count >= MAX_DIRTY_QUEUE_SIZE)
                        {
                            TrimDirtyQueueToSize(MAX_DIRTY_QUEUE_SIZE - 4); // Make room for new entries
                        }
                        _dirtyChunkQueue.Add(chunk);

                        // Track for floating island detection (with cap)
                        if (_recentlyModifiedChunks.Count < MAX_RECENTLY_MODIFIED_SIZE)
                        {
                            _recentlyModifiedChunks.Add(chunk);
                        }
                    }
                }

                // Also queue any neighbors that were marked dirty
                foreach (var chunk in affectedChunks)
                {
                    QueueDirtyNeighbors(chunk.Coord);
                }

                // FLOATING ISLAND CLEANUP: Run deterministic anchor-based flood-fill
                // PERFORMANCE: Only run every N digs to reduce CPU load
                if (enableFloatingIslandCleanup)
                {
                    _digsSinceLastIslandCleanup++;
                    if (_digsSinceLastIslandCleanup >= DIGS_BETWEEN_ISLAND_CLEANUP)
                    {
                        _digsSinceLastIslandCleanup = 0;
                        CleanupFloatingIslandsInAffectedChunks(affectedChunks, operation.WorldPosition, operation.Radius);
                    }
                }
            }

            // Create result
            var result = new DigResult(
                chunksModified > 0,
                chunksModified,
                totalVolumeRemoved,
                operation);

            // Fire event
            if (result.Success)
            {
                OnDigCompleted?.Invoke(result);
            }

            return result;
        }

        /// <summary>
        /// Get all chunks that intersect with a sphere.
        /// </summary>
        private List<VoxelChunk> GetChunksInSphere(Vector3 center, float radius)
        {
            var result = new List<VoxelChunk>(8);

            // Safety check: ensure _chunks is initialized
            if (_chunks == null)
            {
                return result; // Return empty list if not ready
            }

            // Calculate chunk range that could be affected
            float chunkWorldSize = ChunkMesher.CHUNK_SIZE * voxelSize;
            Vector3 min = center - Vector3.one * radius;
            Vector3 max = center + Vector3.one * radius;

            ChunkCoord minCoord = WorldToChunkCoord(min);
            ChunkCoord maxCoord = WorldToChunkCoord(max);

            // Clamp to loaded range
            minCoord = new ChunkCoord(
                Mathf.Max(minCoord.X, minChunkCoord.x),
                Mathf.Max(minCoord.Y, minChunkCoord.y),
                Mathf.Max(minCoord.Z, minChunkCoord.z));
            maxCoord = new ChunkCoord(
                Mathf.Min(maxCoord.X, maxChunkCoord.x),
                Mathf.Min(maxCoord.Y, maxChunkCoord.y),
                Mathf.Min(maxCoord.Z, maxChunkCoord.z));

            // Collect affected chunks
            for (int cx = minCoord.X; cx <= maxCoord.X; cx++)
            {
                for (int cy = minCoord.Y; cy <= maxCoord.Y; cy++)
                {
                    for (int cz = minCoord.Z; cz <= maxCoord.Z; cz++)
                    {
                        ChunkCoord coord = new ChunkCoord(cx, cy, cz);
                        if (_chunks.TryGetValue(coord, out VoxelChunk chunk))
                        {
                            result.Add(chunk);
                        }
                    }
                }
            }

            return result;
        }

        #region Floating Island Cleanup (Anchor Flood-Fill)

        /// <summary>
        /// Clean up floating islands in affected chunks using ANCHOR flood-fill.
        /// Anchored voxels: connected to y=0 OR to solid neighbor chunk border.
        /// Any solid voxel NOT anchored = island -> hard delete.
        /// </summary>
        private void CleanupFloatingIslandsInAffectedChunks(List<VoxelChunk> affectedChunks, Vector3 digPosition, float digRadius)
        {
            if (affectedChunks == null || affectedChunks.Count == 0)
                return;

            float chunkWorldSize = ChunkMesher.CHUNK_SIZE * voxelSize;
            Vector3 playerPos = GetPlayerPosition();
            bool anyColliderUpdated = false;

            // Collect chunks to clean (affected + neighbors touching dig boundary)
            var chunksToClean = new HashSet<VoxelChunk>(affectedChunks);

            // Add neighbor chunks if dig is near chunk boundaries
            foreach (var chunk in affectedChunks)
            {
                AddNeighborChunksNearDig(chunksToClean, chunk, digPosition, digRadius);
            }

            // Sort by distance to player (prioritize nearby)
            var sortedChunks = new List<VoxelChunk>(chunksToClean);
            sortedChunks.Sort((a, b) =>
            {
                float distA = Vector3.Distance(a.WorldOrigin + Vector3.one * chunkWorldSize * 0.5f, playerPos);
                float distB = Vector3.Distance(b.WorldOrigin + Vector3.one * chunkWorldSize * 0.5f, playerPos);
                return distA.CompareTo(distB);
            });

            // Limit to max chunks
            int chunksProcessed = 0;
            int totalVoxelsRemoved = 0;

            foreach (var chunk in sortedChunks)
            {
                if (chunksProcessed >= MAX_CHUNKS_TO_CLEANUP_PER_DIG)
                    break;

                int removed = CleanupFloatingIslandsInChunk(chunk);
                totalVoxelsRemoved += removed;

                if (removed > 0)
                {
                    // PERFORMANCE: Queue island cleanup - don't do immediate collider updates
                    // The main dig processing will handle urgent chunks
                    chunk.UpdateMeshIfDirty();
                    _dirtyChunkQueue.Add(chunk);
                }

                chunksProcessed++;
            }

            // Single Physics.SyncTransforms() if any collider was updated
            if (anyColliderUpdated)
            {
                Physics.SyncTransforms();
            }

            if (_debugIslandCleanup && totalVoxelsRemoved > 0)
            {
                Debug.Log($"[IslandCleanup] Total: {totalVoxelsRemoved} voxels removed from {chunksProcessed} chunks");
            }
        }

        /// <summary>
        /// Add neighbor chunks that might be affected by dig near boundaries.
        /// </summary>
        private void AddNeighborChunksNearDig(HashSet<VoxelChunk> chunksToClean, VoxelChunk chunk, Vector3 digPos, float digRadius)
        {
            // Check if dig is near chunk boundaries
            Vector3 localDigPos = digPos - chunk.WorldOrigin;
            float chunkSize = ChunkMesher.CHUNK_SIZE * voxelSize;
            float boundaryThreshold = digRadius + voxelSize * 2; // Dig radius + buffer

            // Check each face
            int[] neighborOffsets = { -1, 1 };

            // X boundaries
            if (localDigPos.x < boundaryThreshold || localDigPos.x > chunkSize - boundaryThreshold)
            {
                foreach (int dx in neighborOffsets)
                {
                    var neighborCoord = new ChunkCoord(chunk.Coord.X + dx, chunk.Coord.Y, chunk.Coord.Z);
                    if (_chunks.TryGetValue(neighborCoord, out VoxelChunk neighbor))
                        chunksToClean.Add(neighbor);
                }
            }

            // Y boundaries
            if (localDigPos.y < boundaryThreshold || localDigPos.y > chunkSize - boundaryThreshold)
            {
                foreach (int dy in neighborOffsets)
                {
                    var neighborCoord = new ChunkCoord(chunk.Coord.X, chunk.Coord.Y + dy, chunk.Coord.Z);
                    if (_chunks.TryGetValue(neighborCoord, out VoxelChunk neighbor))
                        chunksToClean.Add(neighbor);
                }
            }

            // Z boundaries
            if (localDigPos.z < boundaryThreshold || localDigPos.z > chunkSize - boundaryThreshold)
            {
                foreach (int dz in neighborOffsets)
                {
                    var neighborCoord = new ChunkCoord(chunk.Coord.X, chunk.Coord.Y, chunk.Coord.Z + dz);
                    if (_chunks.TryGetValue(neighborCoord, out VoxelChunk neighbor))
                        chunksToClean.Add(neighbor);
                }
            }
        }

        /// <summary>
        /// Clean up floating islands in a single chunk using ANCHOR flood-fill.
        /// Returns the number of voxels removed.
        /// PERFORMANCE: Uses pooled arrays to avoid GC allocations every call.
        /// </summary>
        private int CleanupFloatingIslandsInChunk(VoxelChunk chunk)
        {
            const int SIZE = ChunkMesher.CHUNK_SIZE; // 16

            // PERFORMANCE: Use pooled arrays instead of allocating new ones
            // Initialize pools on first use
            if (_pooledSolidArray == null)
                _pooledSolidArray = new bool[SIZE, SIZE, SIZE];
            if (_pooledVisitedArray == null)
                _pooledVisitedArray = new bool[SIZE, SIZE, SIZE];
            if (_pooledBfsQueue == null)
                _pooledBfsQueue = new Queue<Vector3Int>(SIZE * SIZE * SIZE / 4);

            // Clear the pooled arrays (reuse them)
            System.Array.Clear(_pooledSolidArray, 0, _pooledSolidArray.Length);
            System.Array.Clear(_pooledVisitedArray, 0, _pooledVisitedArray.Length);
            _pooledBfsQueue.Clear();

            bool[,,] solid = _pooledSolidArray;
            bool[,,] visited = _pooledVisitedArray;
            var queue = _pooledBfsQueue;

            // 1) Build solid array
            int solidCount = 0;

            for (int x = 0; x < SIZE; x++)
            {
                for (int y = 0; y < SIZE; y++)
                {
                    for (int z = 0; z < SIZE; z++)
                    {
                        float density = chunk.GetDensityLocal(x, y, z);
                        solid[x, y, z] = density >= ISO_LEVEL;
                        if (solid[x, y, z])
                            solidCount++;
                    }
                }
            }

            // Early exit if no solid voxels
            if (solidCount == 0)
                return 0;

            // 2) Seed queue with ANCHOR voxels
            // A) All solid voxels at y==0 (bottom layer) - connected to ground
            for (int x = 0; x < SIZE; x++)
            {
                for (int z = 0; z < SIZE; z++)
                {
                    if (solid[x, 0, z])
                    {
                        queue.Enqueue(new Vector3Int(x, 0, z));
                        visited[x, 0, z] = true;
                    }
                }
            }

            // B) Border anchors via neighbor chunks
            // Check each of the 6 faces for solid voxels with solid neighbor chunk support
            AddBorderAnchors(chunk, solid, visited, queue);

            // 3) BFS flood-fill from anchors (using static directions array)
            while (queue.Count > 0)
            {
                var current = queue.Dequeue();

                foreach (var dir in _bfsDirections)
                {
                    int nx = current.x + dir.x;
                    int ny = current.y + dir.y;
                    int nz = current.z + dir.z;

                    // Skip if out of bounds
                    if (nx < 0 || nx >= SIZE || ny < 0 || ny >= SIZE || nz < 0 || nz >= SIZE)
                        continue;

                    // Skip if already visited or not solid
                    if (visited[nx, ny, nz] || !solid[nx, ny, nz])
                        continue;

                    visited[nx, ny, nz] = true;
                    queue.Enqueue(new Vector3Int(nx, ny, nz));
                }
            }

            // 4) Any solid voxel NOT visited = floating island -> hard delete
            int removedCount = 0;

            for (int x = 0; x < SIZE; x++)
            {
                for (int y = 0; y < SIZE; y++)
                {
                    for (int z = 0; z < SIZE; z++)
                    {
                        if (solid[x, y, z] && !visited[x, y, z])
                        {
                            // Floating voxel - DELETE
                            chunk.SetDensityLocal(x, y, z, 0f);
                            removedCount++;
                        }
                    }
                }
            }

            // 5) MICRO-ISLAND KILLER: DISABLED for performance
            // int microRemoved = KillMicroIslands(chunk, solid, SIZE, _bfsDirections);
            // removedCount += microRemoved;

            // 6) Mark chunk dirty if any changes
            if (removedCount > 0)
            {
                chunk.MarkDirtyForCollider();
            }

            return removedCount;
        }

        /// <summary>
        /// MICRO-ISLAND KILLER: Find and remove small floating remnants.
        /// Scans remaining solid voxels, groups into connected components,
        /// and deletes any component that is small AND doesn't touch y==0.
        /// </summary>
        private int KillMicroIslands(VoxelChunk chunk, bool[,,] solid, int SIZE, Vector3Int[] directions)
        {
            // Track which voxels we've processed in this pass
            bool[,,] processed = new bool[SIZE, SIZE, SIZE];
            int totalRemoved = 0;

            // Scan for remaining solid voxels and group into components
            for (int startX = 0; startX < SIZE; startX++)
            {
                for (int startY = 0; startY < SIZE; startY++)
                {
                    for (int startZ = 0; startZ < SIZE; startZ++)
                    {
                        // Skip if not solid or already processed
                        if (!solid[startX, startY, startZ] || processed[startX, startY, startZ])
                            continue;

                        // Flood-fill to find this connected component
                        var component = new List<Vector3Int>();
                        var queue = new Queue<Vector3Int>();
                        bool touchesBottom = false;

                        var start = new Vector3Int(startX, startY, startZ);
                        queue.Enqueue(start);
                        processed[startX, startY, startZ] = true;

                        while (queue.Count > 0)
                        {
                            var current = queue.Dequeue();
                            component.Add(current);

                            // Check if this voxel touches bottom layer
                            if (current.y == 0)
                                touchesBottom = true;

                            // Explore 6-neighbors
                            foreach (var dir in directions)
                            {
                                int nx = current.x + dir.x;
                                int ny = current.y + dir.y;
                                int nz = current.z + dir.z;

                                // Skip out of bounds
                                if (nx < 0 || nx >= SIZE || ny < 0 || ny >= SIZE || nz < 0 || nz >= SIZE)
                                    continue;

                                // Skip if not solid or already processed
                                if (!solid[nx, ny, nz] || processed[nx, ny, nz])
                                    continue;

                                processed[nx, ny, nz] = true;
                                queue.Enqueue(new Vector3Int(nx, ny, nz));
                            }
                        }

                        // Check if this is a micro-island: small AND doesn't touch bottom
                        if (component.Count <= MICRO_ISLAND_MAX_VOXELS && !touchesBottom)
                        {
                            // Kill this micro-island
                            foreach (var voxel in component)
                            {
                                chunk.SetDensityLocal(voxel.x, voxel.y, voxel.z, 0f);
                                solid[voxel.x, voxel.y, voxel.z] = false;
                                totalRemoved++;
                            }

                            if (_debugIslandCleanup && component.Count > 0)
                            {
                                Debug.Log($"[MicroIslandKiller] Chunk {chunk.Coord}: killed micro-island with {component.Count} voxels");
                            }
                        }
                    }
                }
            }

            return totalRemoved;
        }

        /// <summary>
        /// Add border voxels that are anchored to neighbor chunk solid voxels.
        /// </summary>
        private void AddBorderAnchors(VoxelChunk chunk, bool[,,] solid, bool[,,] visited, Queue<Vector3Int> queue)
        {
            const int SIZE = ChunkMesher.CHUNK_SIZE;

            // X == 0 face (check neighbor at X - 1)
            {
                var neighborCoord = new ChunkCoord(chunk.Coord.X - 1, chunk.Coord.Y, chunk.Coord.Z);
                if (_chunks.TryGetValue(neighborCoord, out VoxelChunk neighbor))
                {
                    for (int y = 0; y < SIZE; y++)
                    {
                        for (int z = 0; z < SIZE; z++)
                        {
                            if (solid[0, y, z] && !visited[0, y, z])
                            {
                                // Check if neighbor chunk's corresponding voxel (x=SIZE-1) is solid
                                if (neighbor.GetDensityLocal(SIZE - 1, y, z) >= ISO_LEVEL)
                                {
                                    visited[0, y, z] = true;
                                    queue.Enqueue(new Vector3Int(0, y, z));
                                }
                            }
                        }
                    }
                }
            }

            // X == SIZE-1 face (check neighbor at X + 1)
            {
                var neighborCoord = new ChunkCoord(chunk.Coord.X + 1, chunk.Coord.Y, chunk.Coord.Z);
                if (_chunks.TryGetValue(neighborCoord, out VoxelChunk neighbor))
                {
                    for (int y = 0; y < SIZE; y++)
                    {
                        for (int z = 0; z < SIZE; z++)
                        {
                            if (solid[SIZE - 1, y, z] && !visited[SIZE - 1, y, z])
                            {
                                // Check if neighbor chunk's corresponding voxel (x=0) is solid
                                if (neighbor.GetDensityLocal(0, y, z) >= ISO_LEVEL)
                                {
                                    visited[SIZE - 1, y, z] = true;
                                    queue.Enqueue(new Vector3Int(SIZE - 1, y, z));
                                }
                            }
                        }
                    }
                }
            }

            // Z == 0 face (check neighbor at Z - 1)
            {
                var neighborCoord = new ChunkCoord(chunk.Coord.X, chunk.Coord.Y, chunk.Coord.Z - 1);
                if (_chunks.TryGetValue(neighborCoord, out VoxelChunk neighbor))
                {
                    for (int x = 0; x < SIZE; x++)
                    {
                        for (int y = 0; y < SIZE; y++)
                        {
                            if (solid[x, y, 0] && !visited[x, y, 0])
                            {
                                if (neighbor.GetDensityLocal(x, y, SIZE - 1) >= ISO_LEVEL)
                                {
                                    visited[x, y, 0] = true;
                                    queue.Enqueue(new Vector3Int(x, y, 0));
                                }
                            }
                        }
                    }
                }
            }

            // Z == SIZE-1 face (check neighbor at Z + 1)
            {
                var neighborCoord = new ChunkCoord(chunk.Coord.X, chunk.Coord.Y, chunk.Coord.Z + 1);
                if (_chunks.TryGetValue(neighborCoord, out VoxelChunk neighbor))
                {
                    for (int x = 0; x < SIZE; x++)
                    {
                        for (int y = 0; y < SIZE; y++)
                        {
                            if (solid[x, y, SIZE - 1] && !visited[x, y, SIZE - 1])
                            {
                                if (neighbor.GetDensityLocal(x, y, 0) >= ISO_LEVEL)
                                {
                                    visited[x, y, SIZE - 1] = true;
                                    queue.Enqueue(new Vector3Int(x, y, SIZE - 1));
                                }
                            }
                        }
                    }
                }
            }

            // Y == SIZE-1 face (check neighbor at Y + 1, top of chunk)
            {
                var neighborCoord = new ChunkCoord(chunk.Coord.X, chunk.Coord.Y + 1, chunk.Coord.Z);
                if (_chunks.TryGetValue(neighborCoord, out VoxelChunk neighbor))
                {
                    for (int x = 0; x < SIZE; x++)
                    {
                        for (int z = 0; z < SIZE; z++)
                        {
                            if (solid[x, SIZE - 1, z] && !visited[x, SIZE - 1, z])
                            {
                                if (neighbor.GetDensityLocal(x, 0, z) >= ISO_LEVEL)
                                {
                                    visited[x, SIZE - 1, z] = true;
                                    queue.Enqueue(new Vector3Int(x, SIZE - 1, z));
                                }
                            }
                        }
                    }
                }
            }

            // Y == 0 is already handled by ground anchor (step A), no need to check neighbor below
            // But if we wanted to allow support from below:
            {
                var neighborCoord = new ChunkCoord(chunk.Coord.X, chunk.Coord.Y - 1, chunk.Coord.Z);
                if (_chunks.TryGetValue(neighborCoord, out VoxelChunk neighbor))
                {
                    for (int x = 0; x < SIZE; x++)
                    {
                        for (int z = 0; z < SIZE; z++)
                        {
                            // Only check if not already anchored as ground
                            if (solid[x, 0, z] && !visited[x, 0, z])
                            {
                                if (neighbor.GetDensityLocal(x, SIZE - 1, z) >= ISO_LEVEL)
                                {
                                    visited[x, 0, z] = true;
                                    queue.Enqueue(new Vector3Int(x, 0, z));
                                }
                            }
                        }
                    }
                }
            }
        }

        #endregion

        /// <summary>
        /// Check if a position is in a protected zone (bedrock, boundary).
        /// </summary>
        private bool IsInProtectedZone(Vector3 worldPos)
        {
            // Check bedrock
            float bedrockY = minChunkCoord.y * ChunkMesher.CHUNK_SIZE * voxelSize + bedrockThickness * voxelSize;
            if (worldPos.y < bedrockY)
            {
                return true;
            }

            // Check boundary walls
            float chunkWorldSize = ChunkMesher.CHUNK_SIZE * voxelSize;
            float boundarySize = boundaryThickness * voxelSize;

            float minX = minChunkCoord.x * chunkWorldSize + boundarySize;
            float maxX = (maxChunkCoord.x + 1) * chunkWorldSize - boundarySize;
            float minZ = minChunkCoord.z * chunkWorldSize + boundarySize;
            float maxZ = (maxChunkCoord.z + 1) * chunkWorldSize - boundarySize;

            if (worldPos.x < minX || worldPos.x > maxX || worldPos.z < minZ || worldPos.z > maxZ)
            {
                return true;
            }

            return false;
        }

        /// <summary>
        /// Mark neighboring chunks as dirty if the dig was near a chunk boundary.
        /// Uses border margin to account for marching cubes needing neighboring voxel data.
        /// </summary>
        private void MarkNeighborsDirtyIfNeeded(VoxelChunk chunk, Vector3 digCenter, float radius)
        {
            float chunkWorldSize = ChunkMesher.CHUNK_SIZE * voxelSize;

            // CRITICAL: Add border margin for marching cubes
            // Marching cubes needs +1 voxel of data from neighbors for proper triangulation
            // Using 1.5 voxels to be safe and account for interpolation
            float borderMargin = 1.5f * voxelSize;
            float effectiveRadius = radius + borderMargin;

            // Check each axis with the expanded radius
            bool nearXMin = digCenter.x - effectiveRadius < chunk.WorldOrigin.x;
            bool nearXMax = digCenter.x + effectiveRadius > chunk.WorldOrigin.x + chunkWorldSize;
            bool nearYMin = digCenter.y - effectiveRadius < chunk.WorldOrigin.y;
            bool nearYMax = digCenter.y + effectiveRadius > chunk.WorldOrigin.y + chunkWorldSize;
            bool nearZMin = digCenter.z - effectiveRadius < chunk.WorldOrigin.z;
            bool nearZMax = digCenter.z + effectiveRadius > chunk.WorldOrigin.z + chunkWorldSize;

            // Mark neighbors that share affected boundaries
            if (nearXMin) MarkNeighborDirty(chunk.Coord, -1, 0, 0);
            if (nearXMax) MarkNeighborDirty(chunk.Coord, 1, 0, 0);
            if (nearYMin) MarkNeighborDirty(chunk.Coord, 0, -1, 0);
            if (nearYMax) MarkNeighborDirty(chunk.Coord, 0, 1, 0);
            if (nearZMin) MarkNeighborDirty(chunk.Coord, 0, 0, -1);
            if (nearZMax) MarkNeighborDirty(chunk.Coord, 0, 0, 1);
        }

        /// <summary>
        /// Mark a neighboring chunk as dirty.
        /// </summary>
        private void MarkNeighborDirty(ChunkCoord baseCoord, int dx, int dy, int dz)
        {
            ChunkCoord neighborCoord = baseCoord.GetNeighbor(dx, dy, dz);
            if (_chunks.TryGetValue(neighborCoord, out VoxelChunk neighbor))
            {
                neighbor.MarkDirty();
                _dirtyChunkQueue.Add(neighbor);
            }
        }

        /// <summary>
        /// Add any dirty neighbors of a chunk to the rebuild queue.
        /// </summary>
        private void QueueDirtyNeighbors(ChunkCoord coord)
        {
            // Check all 6 face neighbors
            int[] offsets = { -1, 1 };
            foreach (int dx in offsets)
            {
                ChunkCoord neighborCoord = coord.GetNeighbor(dx, 0, 0);
                if (_chunks.TryGetValue(neighborCoord, out VoxelChunk neighbor) && neighbor.IsDirty)
                {
                    _dirtyChunkQueue.Add(neighbor);
                }
            }
            foreach (int dy in offsets)
            {
                ChunkCoord neighborCoord = coord.GetNeighbor(0, dy, 0);
                if (_chunks.TryGetValue(neighborCoord, out VoxelChunk neighbor) && neighbor.IsDirty)
                {
                    _dirtyChunkQueue.Add(neighbor);
                }
            }
            foreach (int dz in offsets)
            {
                ChunkCoord neighborCoord = coord.GetNeighbor(0, 0, dz);
                if (_chunks.TryGetValue(neighborCoord, out VoxelChunk neighbor) && neighbor.IsDirty)
                {
                    _dirtyChunkQueue.Add(neighbor);
                }
            }
        }

        #endregion

        #region Chunk Processing

        /// <summary>
        /// Process dirty chunks with URGENT ZONE priority.
        /// Chunks within URGENT_RADIUS_METERS of player get immediate mesh+collider updates.
        /// Far chunks use budgeted processing.
        /// </summary>
        private void ProcessDirtyChunks()
        {
            if (_dirtyChunkQueue == null || _dirtyChunkQueue.Count == 0) return;

            int urgentProcessed = 0;
            int farProcessed = 0;
            bool anyColliderUpdated = false;
            float startTime = Time.realtimeSinceStartup;
            float timeBudgetSec = TIME_BUDGET_MS / 1000f;
            float chunkWorldSize = ChunkMesher.CHUNK_SIZE * voxelSize;

            // PERFORMANCE: Use pooled lists instead of allocating new ones each frame
            _pooledUrgentChunks.Clear();
            _pooledFarChunks.Clear();
            var urgentChunks = _pooledUrgentChunks;
            var farChunks = _pooledFarChunks;

            foreach (var chunk in _dirtyChunkQueue)
            {
                if (!chunk.IsDirty) continue;

                // Calculate distance to player
                Vector3 chunkCenter = chunk.WorldOrigin + Vector3.one * (chunkWorldSize * 0.5f);
                float distToPlayer = Vector3.Distance(chunkCenter, _lastKnownPlayerPos);

                if (distToPlayer <= URGENT_RADIUS_METERS)
                    urgentChunks.Add(chunk);
                else
                    farChunks.Add(chunk);
            }

            // URGENT ZONE: Update ALL urgent chunk MESHES (no limit - meshes are cheap)
            // But limit COLLIDER updates (expensive) to MAX_URGENT_REBUILDS_PER_FRAME
            int collidersUpdated = 0;

            foreach (var chunk in urgentChunks)
            {
                if (!chunk.IsDirty)
                {
                    _dirtyChunkQueue.Remove(chunk);
                    continue;
                }

                // 1) ALWAYS update mesh (prevents visual holes at chunk boundaries)
                chunk.UpdateMeshIfDirty();
                _dirtyChunkQueue.Remove(chunk);
                urgentProcessed++;

                // 2) Limit collider updates (the expensive part)
                if (chunk.NeedsColliderUpdate)
                {
                    if (collidersUpdated < MAX_URGENT_REBUILDS_PER_FRAME)
                    {
                        chunk.ForceUpdateCollider();
                        anyColliderUpdated = true;
                        collidersUpdated++;
                    }
                    else
                    {
                        // Queue for next frame
                        _colliderScheduler.ScheduleImmediateUpdate(chunk);
                    }
                }
            }

            // FAR ZONE: Process with budget (non-urgent)
            foreach (var chunk in farChunks)
            {
                if (farProcessed >= MAX_MESH_REBUILDS_FAR)
                    break;

                float elapsed = Time.realtimeSinceStartup - startTime;
                if (elapsed >= timeBudgetSec)
                    break;

                if (!chunk.IsDirty)
                {
                    _dirtyChunkQueue.Remove(chunk);
                    continue;
                }

                chunk.UpdateMeshIfDirty();
                _dirtyChunkQueue.Remove(chunk);
                farProcessed++;

                // Schedule collider update (delayed for far chunks)
                if (chunk.NeedsColliderUpdate)
                {
                    _colliderScheduler.ScheduleUpdate(chunk);
                }
            }

            // Sync physics ONCE if any collider was updated this frame
            if (anyColliderUpdated)
            {
                Physics.SyncTransforms();
            }
        }

        /// <summary>
        /// Check if a world position is within the urgent collider zone.
        /// </summary>
        public bool IsInUrgentZone(Vector3 worldPos)
        {
            return Vector3.Distance(worldPos, _lastKnownPlayerPos) <= URGENT_RADIUS_METERS;
        }

        /// <summary>
        /// Force rebuild a specific chunk with immediate collider update.
        /// Used by FloatingTerrainRemover when modifying chunks in urgent zone.
        /// </summary>
        public void ForceRebuildChunkImmediate(VoxelChunk chunk)
        {
            if (chunk == null) return;

            chunk.MarkDirty();
            chunk.UpdateMeshIfDirty();
            chunk.ForceUpdateCollider();
            Physics.SyncTransforms();
            _dirtyChunkQueue.Remove(chunk);
        }

        /// <summary>
        /// Enqueue a chunk for rebuild. If in urgent zone, rebuilds immediately.
        /// </summary>
        public void EnqueueChunkRebuild(VoxelChunk chunk, bool forceUrgent = false)
        {
            if (chunk == null) return;

            chunk.MarkDirty();

            // Check if in urgent zone
            float chunkWorldSize = ChunkMesher.CHUNK_SIZE * voxelSize;
            Vector3 chunkCenter = chunk.WorldOrigin + Vector3.one * (chunkWorldSize * 0.5f);
            bool isUrgent = forceUrgent || Vector3.Distance(chunkCenter, _lastKnownPlayerPos) <= URGENT_RADIUS_METERS;

            if (isUrgent)
            {
                // Immediate rebuild for urgent zone
                chunk.UpdateMeshIfDirty();
                chunk.ForceUpdateCollider();
                Physics.SyncTransforms();
            }
            else
            {
                // Queue for later processing
                _dirtyChunkQueue.Add(chunk);
            }
        }

        /// <summary>
        /// Force process all dirty chunks immediately (for initial terrain, save loading).
        /// </summary>
        private void FlushDirtyChunkQueue()
        {
            if (_dirtyChunkQueue == null) return;

            foreach (var chunk in _dirtyChunkQueue)
            {
                if (chunk.IsDirty)
                {
                    chunk.UpdateMeshIfDirty();
                    if (chunk.NeedsColliderUpdate)
                    {
                        chunk.ForceUpdateCollider();
                    }
                }
            }
            _dirtyChunkQueue.Clear();
            Physics.SyncTransforms();
        }

        /// <summary>
        /// PERFORMANCE: Trim the dirty chunk queue to a target size.
        /// Removes farthest chunks from player first.
        /// </summary>
        private void TrimDirtyQueueToSize(int targetSize)
        {
            if (_dirtyChunkQueue.Count <= targetSize) return;

            float chunkWorldSize = ChunkMesher.CHUNK_SIZE * voxelSize;

            // Sort by distance to player (farthest first for removal)
            var sorted = new List<VoxelChunk>(_dirtyChunkQueue);
            sorted.Sort((a, b) =>
            {
                float distA = Vector3.Distance(a.WorldOrigin + Vector3.one * chunkWorldSize * 0.5f, _lastKnownPlayerPos);
                float distB = Vector3.Distance(b.WorldOrigin + Vector3.one * chunkWorldSize * 0.5f, _lastKnownPlayerPos);
                return distB.CompareTo(distA); // Descending - farthest first
            });

            // Remove farthest chunks until we hit target size
            int toRemove = sorted.Count - targetSize;
            for (int i = 0; i < toRemove && i < sorted.Count; i++)
            {
                _dirtyChunkQueue.Remove(sorted[i]);
            }
        }

        /// <summary>
        /// PERFORMANCE: Unload chunks that are far from the player.
        /// This saves memory and reduces processing for distant terrain.
        /// </summary>
        private void UnloadDistantChunks()
        {
            if (_chunks == null || _chunks.Count == 0) return;

            ChunkCoord playerChunkCoord = WorldToChunkCoord(_lastKnownPlayerPos);
            var chunksToUnload = new List<ChunkCoord>();

            foreach (var kvp in _chunks)
            {
                ChunkCoord coord = kvp.Key;

                // Calculate chunk distance (in chunks, not meters)
                int dx = Mathf.Abs(coord.X - playerChunkCoord.X);
                int dy = Mathf.Abs(coord.Y - playerChunkCoord.Y);
                int dz = Mathf.Abs(coord.Z - playerChunkCoord.Z);
                int maxDist = Mathf.Max(dx, Mathf.Max(dy, dz));

                if (maxDist > chunkUnloadDistance)
                {
                    chunksToUnload.Add(coord);
                    if (chunksToUnload.Count >= MAX_CHUNKS_TO_UNLOAD_PER_CHECK)
                        break;
                }
            }

            // Unload the distant chunks
            foreach (var coord in chunksToUnload)
            {
                if (_chunks.TryGetValue(coord, out VoxelChunk chunk))
                {
                    // Remove from dirty queues first
                    _dirtyChunkQueue.Remove(chunk);
                    _recentlyModifiedChunks.Remove(chunk);

                    // Destroy the chunk
                    chunk.Destroy();
                    _chunks.Remove(coord);
                }
            }
        }

        #endregion

        #region Floating Island Detection Support

        /// <summary>
        /// Get the set of recently modified chunks for floating island detection.
        /// </summary>
        public IReadOnlyCollection<VoxelChunk> GetRecentlyModifiedChunks()
        {
            return _recentlyModifiedChunks;
        }

        /// <summary>
        /// Remove a chunk from the recently modified set after processing.
        /// </summary>
        public void ClearModifiedChunk(VoxelChunk chunk)
        {
            _recentlyModifiedChunks.Remove(chunk);
        }

        /// <summary>
        /// Clear all recently modified chunks.
        /// </summary>
        public void ClearAllModifiedChunks()
        {
            _recentlyModifiedChunks.Clear();
        }

        /// <summary>
        /// Get the player position for distance checks.
        /// </summary>
        public Vector3 GetPlayerPosition()
        {
            return _lastKnownPlayerPos;
        }

        #endregion

        #region Material Loading

        /// <summary>
        /// Try to load a triplanar material from Resources or known paths.
        /// </summary>
        private Material TryLoadTriplanarMaterial()
        {
            // Try Resources folder first (runtime-safe)
            Material mat = Resources.Load<Material>("TriplanarTerrain/M_Terrain_Dirt_08_Triplanar");
            if (mat != null) return mat;

            mat = Resources.Load<Material>("M_Terrain_Dirt_08_Triplanar");
            if (mat != null) return mat;

            mat = Resources.Load<Material>("TriplanarSoil");
            if (mat != null) return mat;

            // Try to find by shader
            Shader triplanarShader = Shader.Find("BeneathTheFloor/TriplanarSoil");
            if (triplanarShader != null)
            {
                // Search for any material using this shader
                var allMaterials = Resources.FindObjectsOfTypeAll<Material>();
                foreach (var m in allMaterials)
                {
                    if (m.shader == triplanarShader)
                    {
                        return m;
                    }
                }
            }

            return null;
        }

        /// <summary>
        /// Create a fallback material when no triplanar material is available.
        /// Uses the TriplanarSoil shader if available, otherwise URP/Lit.
        /// </summary>
        private Material CreateFallbackMaterial()
        {
            Shader shader = Shader.Find("BeneathTheFloor/TriplanarSoil");
            if (shader != null)
            {
                var mat = new Material(shader);
                mat.name = "Terrain_Fallback_Triplanar";

                // Dark dirt appearance
                mat.SetColor("_DirtColor", new Color(0.3f, 0.22f, 0.15f, 1f));
                mat.SetColor("_RockColor", new Color(0.25f, 0.2f, 0.17f, 1f));
                mat.SetFloat("_Smoothness", 0.05f);
                mat.SetFloat("_TilingScale", 3f);
                mat.SetFloat("_TriplanarSharpness", 5f);
                mat.SetFloat("_NormalStrength", 1.2f);

                return mat;
            }

            // Ultimate fallback - basic URP/Lit with brown color
            var fallback = new Material(Shader.Find("Universal Render Pipeline/Lit"));
            fallback.name = "Terrain_Fallback_Basic";
            fallback.color = new Color(0.35f, 0.25f, 0.15f); // Dark brown
            return fallback;
        }

        #endregion

        #region Save/Load

        /// <summary>
        /// Capture the current terrain state into save data.
        /// Only stores chunks that have been modified to minimize file size.
        /// </summary>
        public TerrainSaveData CaptureSaveData()
        {
            var saveData = new TerrainSaveData
            {
                voxelSize = voxelSize,
                chunkSize = VoxelChunk.SIZE
            };

            int modifiedCount = 0;
            foreach (var kvp in _chunks)
            {
                var chunk = kvp.Value;
                if (chunk.HasBeenModified())
                {
                    var chunkData = new ChunkSaveData(chunk.Coord, chunk.GetDensitiesForSave());
                    saveData.modifiedChunks.Add(chunkData);
                    modifiedCount++;
                }
            }

            saveData.modifiedChunkCount = modifiedCount;
            return saveData;
        }

        /// <summary>
        /// Apply saved terrain data to the current terrain.
        /// Loads modified chunks and regenerates their meshes.
        /// </summary>
        public void ApplySaveData(TerrainSaveData saveData)
        {
            if (saveData == null || !saveData.IsValid())
            {
                Debug.LogError("[ChunkManager] Cannot apply invalid save data!");
                return;
            }

            int appliedCount = 0;
            int createdCount = 0;

            foreach (var chunkData in saveData.modifiedChunks)
            {
                ChunkCoord coord = chunkData.GetCoord();

                // Get or create the chunk
                VoxelChunk chunk;
                if (_chunks.TryGetValue(coord, out chunk))
                {
                    // Existing chunk - update its densities
                    chunk.SetDensitiesFromSave(chunkData.densities);
                    appliedCount++;
                }
                else
                {
                    // Chunk doesn't exist yet - create it and set densities
                    chunk = EnsureChunk(coord);
                    if (chunk != null)
                    {
                        chunk.SetDensitiesFromSave(chunkData.densities);
                        createdCount++;
                    }
                }
            }

            // Regenerate all dirty meshes with immediate collider updates
            // This is critical - without immediate colliders, player falls through terrain
            RegenerateAllDirtyMeshes(forceColliders: true);

        }

        /// <summary>
        /// Regenerate meshes for all dirty chunks.
        /// </summary>
        /// <param name="forceColliders">If true, immediately update colliders (REQUIRED when loading saves)</param>
        private void RegenerateAllDirtyMeshes(bool forceColliders = false)
        {
            int count = 0;
            foreach (var chunk in _chunks.Values)
            {
                if (chunk.IsDirty)
                {
                    // When loading saves, we need immediate collider updates
                    // otherwise player falls through terrain
                    chunk.RegenerateMesh(immediateCollider: forceColliders);
                    count++;
                }
            }

            // Safety: If we didn't force colliders during mesh gen, update them all now
            if (!forceColliders && count > 0)
            {
                foreach (var chunk in _chunks.Values)
                {
                    if (chunk.NeedsColliderUpdate)
                    {
                        chunk.ForceUpdateCollider();
                    }
                }
            }

            if (count > 0)
            {
            }
        }

        #endregion

        #region Debug

        /// <summary>
        /// Get debug information about the chunk manager.
        /// </summary>
        public string GetDebugInfo()
        {
            int dirtyCount = 0;
            foreach (var chunk in _chunks.Values)
            {
                if (chunk.IsDirty) dirtyCount++;
            }

            return $"ChunkManager: {_chunks.Count} chunks loaded, {dirtyCount} dirty\n" +
                   $"{_colliderScheduler.GetDebugInfo()}";
        }

        #endregion
    }
}
