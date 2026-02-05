using UnityEngine;
using BeneathTheFloor.UI;

namespace BeneathTheFloor.Digging
{
    /// <summary>
    /// Bootstraps the terrain world on game start.
    /// Creates a solid underground volume within the DigBounds.
    ///
    /// KEY BEHAVIOR:
    /// - Only creates chunks INSIDE DigBounds (from DigBoundsProvider)
    /// - Air above surfaceStartWorldY (open pit)
    /// - Solid below surfaceStartWorldY (diggable terrain)
    /// - Optional rough surface for natural "started digging" look
    ///
    /// USAGE:
    /// - Requires DigBoundsProvider in scene
    /// - Attach alongside ChunkManager
    /// - Configure surfaceStartWorldY for pit depth
    /// </summary>
    [DefaultExecutionOrder(50)] // Run AFTER DigBoundsProvider (10) which runs after ModularShaftWallManager (0)
    public class WorldBootstrapper : MonoBehaviour
    {
        [Header("Bounds Source")]
        [Tooltip("DigBoundsProvider for resolving dig area. If null, finds automatically.")]
        [SerializeField] private DigBoundsProvider boundsProvider;

        [Header("Surface Configuration")]
        [Tooltip("World Y where terrain starts (solid below, air above). Set to -3 so pit is accessible.")]
        [SerializeField] private float surfaceStartWorldY = -3f; // Changed from -5 to -3 so terrain exists where player stands

        [Tooltip("Enable smooth gradient at surface transition.")]
        [SerializeField] private bool enableSurfaceGradient = true;

        [Tooltip("Thickness of surface gradient in voxels.")]
        [SerializeField] private float surfaceGradientThickness = 2f;

        [Header("Rough Surface")]
        [Tooltip("Apply roughening pass near surface for natural 'started digging' look.")]
        [SerializeField] private bool enableRoughSurface = true;

        [Tooltip("Height of rough band above/below surfaceStartWorldY (meters).")]
        [SerializeField] private float roughBandHeight = 1.0f;

        [Tooltip("Amount of roughness (0=none, 1=maximum).")]
        [Range(0f, 1f)]
        [SerializeField] private float roughnessAmount = 0.5f;

        [Tooltip("Seed for deterministic roughness (same seed = same result).")]
        [SerializeField] private int roughnessSeed = 12345;

        [Tooltip("Maximum carve depth below surfaceStartWorldY.")]
        [SerializeField] private float maxCarveDepthBelowSurface = 1.0f;

        [Header("Protected Zones")]
        [Tooltip("Thickness of bedrock layer at bottom in voxels.")]
        [SerializeField] private int bedrockThicknessVoxels = 2;

        [Header("References")]
        [Tooltip("ChunkManager to bootstrap. If null, finds via GetComponent or singleton.")]
        [SerializeField] private ChunkManager chunkManager;

        [Header("Debug")]
        [SerializeField] private bool enableDebugLogs = false;

        // Bootstrap state
        private bool _isBootstrapped = false;

        // Cached dig bounds
        private Bounds _digBounds;
        private bool _hasBounds = false;

        /// <summary>
        /// Returns true after world has been bootstrapped.
        /// </summary>
        public bool IsBootstrapped => _isBootstrapped;

        /// <summary>
        /// The surfaceStartWorldY value being used.
        /// </summary>
        public float SurfaceStartY => surfaceStartWorldY;

        private void Awake()
        {
            // FORCE disable debug logs (scene-serialized value may be true)
            enableDebugLogs = false;

            // Find ChunkManager
            if (chunkManager == null)
                chunkManager = GetComponent<ChunkManager>();
            if (chunkManager == null)
                chunkManager = ChunkManager.Instance;
            if (chunkManager == null)
                chunkManager = FindObjectOfType<ChunkManager>();

            if (chunkManager == null)
            {
                Debug.LogError("[WorldBootstrapper] No ChunkManager found! Cannot bootstrap world.");
                enabled = false;
                return;
            }

            // CRITICAL: Disable ChunkManager's auto-generation - WorldBootstrapper handles terrain creation.
            // This prevents double terrain generation with incorrect bounds.
            chunkManager.DisableAutoGenerate();

            // Find DigBoundsProvider
            if (boundsProvider == null)
                boundsProvider = GetComponent<DigBoundsProvider>();
            if (boundsProvider == null)
                boundsProvider = DigBoundsProvider.Instance;
            if (boundsProvider == null)
                boundsProvider = FindObjectOfType<DigBoundsProvider>();
        }

        private void Start()
        {
            // RUNTIME OVERRIDE: Get surfaceStartWorldY from terrain manager
            // Must match terrain surface exactly so terrain is where it was
            var terrainManager = FindObjectOfType<UndergroundTerrainManager>();
            if (terrainManager != null)
            {
                // Terrain surface = basementFloorY - soilStartOffsetMeters
                // This is where the diggable soil begins
                float terrainSurface = terrainManager.basementFloorY - terrainManager.soilStartOffsetMeters;
                surfaceStartWorldY = terrainSurface;
            }
            else
            {
                // Use a reasonable default if terrain manager not found
                if (surfaceStartWorldY > -5f)
                {
                    surfaceStartWorldY = -8f; // Default: typical terrain depth
                }
            }

            BootstrapWorld();

            // Setup resource spawner if not already present
            EnsureResourceSpawner();

            // Setup Depth HUD
            EnsureDepthHUD();
        }

        /// <summary>
        /// Ensure DepthHUD exists for debugging/display.
        /// </summary>
        private void EnsureDepthHUD()
        {
            if (DepthHUDSetup.Instance != null)
                return;

            var existing = FindObjectOfType<DepthHUDSetup>();
            if (existing != null)
                return;

            // Create DepthHUDSetup
            var hudObj = new GameObject("DepthHUDSetup");
            hudObj.AddComponent<DepthHUDSetup>();
        }

        /// <summary>
        /// Ensure ResourceSpawner exists for resource drops on digs.
        /// </summary>
        private void EnsureResourceSpawner()
        {
            if (ResourceSpawner.Instance != null)
                return;

            var existing = FindObjectOfType<ResourceSpawner>();
            if (existing != null)
                return;

            // Add to this GameObject
            var spawner = gameObject.AddComponent<ResourceSpawner>();
            if (enableDebugLogs)
                Debug.Log("[WorldBootstrapper] Added ResourceSpawner component.");
        }

        /// <summary>
        /// Bootstrap the world - generate terrain within dig bounds.
        /// </summary>
        [ContextMenu("Bootstrap World")]
        public void BootstrapWorld()
        {
            if (_isBootstrapped)
            {
                Debug.LogWarning("[WorldBootstrapper] World already bootstrapped. Ignoring.");
                return;
            }

            if (chunkManager == null)
            {
                Debug.LogError("[WorldBootstrapper] Cannot bootstrap - ChunkManager is null!");
                return;
            }

            // Resolve bounds
            if (!ResolveBounds())
            {
                Debug.LogError("[WorldBootstrapper] Cannot bootstrap - no valid dig bounds!");
                return;
            }

            float startTime = Time.realtimeSinceStartup;

            // Calculate chunk range from dig bounds
            float voxelSize = chunkManager.VoxelSize;
            int chunkSize = ChunkMesher.CHUNK_SIZE;
            float chunkWorldSize = chunkSize * voxelSize;

            // Convert bounds to chunk coordinates
            ChunkCoord minChunk = chunkManager.WorldToChunkCoord(_digBounds.min);
            ChunkCoord maxChunk = chunkManager.WorldToChunkCoord(_digBounds.max);

            // Generate chunks only inside bounds
            int chunksCreated = 0;
            int chunksSkipped = 0;

            // Find the chunk containing Y = surfaceStartWorldY
            int surfaceChunkY = Mathf.FloorToInt(surfaceStartWorldY / chunkWorldSize);

            for (int cx = minChunk.X; cx <= maxChunk.X; cx++)
            {
                for (int cy = minChunk.Y; cy <= maxChunk.Y; cy++)
                {
                    for (int cz = minChunk.Z; cz <= maxChunk.Z; cz++)
                    {
                        ChunkCoord coord = new ChunkCoord(cx, cy, cz);

                        // Calculate chunk AABB using ChunkManager (accounts for world offset)
                        Bounds chunkBounds = chunkManager.GetChunkBounds(coord);

                        // HARD CONSTRAINT: Only create chunks inside dig bounds
                        if (!_digBounds.Intersects(chunkBounds))
                        {
                            chunksSkipped++;
                            continue;
                        }

                        // SKIP chunks entirely above surface - they would be all air
                        // Only create chunks where at least some part is AT or BELOW surface
                        // Use actual chunk world position (accounts for offset)
                        float chunkBottomY = chunkBounds.min.y;
                        if (chunkBottomY > surfaceStartWorldY)
                        {
                            // This chunk is entirely above the surface - skip it
                            chunksSkipped++;
                            continue;
                        }

                        CreateChunkWithDensity(coord);
                        chunksCreated++;
                    }
                }
            }

            // Apply rough surface pass
            if (enableRoughSurface)
            {
                ApplyRoughSurfacePass();
            }

            // Force mesh generation for all chunks
            chunkManager.FlushDirtyChunks();

            // CRITICAL: Force all collider updates immediately
            // Do NOT rely on scheduler for initial terrain - player needs physics NOW
            int collidersUpdated = chunkManager.ForceAllColliderUpdates();

            // Validate all colliders are properly assigned
            int validColliders = chunkManager.ValidateAllColliders();

            // Sync physics to ensure colliders are active before player spawns
            Physics.SyncTransforms();

            float elapsed = Time.realtimeSinceStartup - startTime;
            _isBootstrapped = true;

            // DEBUG: Disabled - was spamming logs
            // LogAllChunksDebug();

            // Initialize floating terrain detector
            InitializeFloatingTerrainDetector();

        }

        /// <summary>
        /// Initialize the floating terrain detector for detecting and handling islands.
        /// </summary>
        private void InitializeFloatingTerrainDetector()
        {
            // Check if detector already exists
            var existingDetector = FindObjectOfType<FloatingTerrainDetector>();
            if (existingDetector != null)
            {
                return;
            }

            // Create detector on ChunkManager's GameObject
            if (chunkManager != null)
            {
                var detector = chunkManager.gameObject.AddComponent<FloatingTerrainDetector>();
            }
            else
            {
                // Create on our own GameObject as fallback
                var detector = gameObject.AddComponent<FloatingTerrainDetector>();
            }
        }

        /// <summary>
        /// Resolve dig bounds from DigBoundsProvider.
        /// </summary>
        private bool ResolveBounds()
        {
            if (boundsProvider != null && boundsProvider.HasValidBounds)
            {
                _digBounds = boundsProvider.DigBounds;
                _hasBounds = true;
                return true;
            }

            // Fallback: try to find provider again
            boundsProvider = FindObjectOfType<DigBoundsProvider>();
            if (boundsProvider != null && boundsProvider.HasValidBounds)
            {
                _digBounds = boundsProvider.DigBounds;
                _hasBounds = true;
                return true;
            }

            _hasBounds = false;
            return false;
        }

        /// <summary>
        /// Create a chunk with proper density (air above surface, solid below).
        /// </summary>
        private void CreateChunkWithDensity(ChunkCoord coord)
        {
            VoxelChunk chunk = chunkManager.EnsureChunk(coord);
            if (chunk == null)
                return;

            float voxelSize = chunkManager.VoxelSize;
            int chunkSize = ChunkMesher.CHUNK_SIZE;
            float chunkWorldSize = chunkSize * voxelSize;

            // Calculate bedrock Y from bounds bottom
            float bedrockY = _digBounds.min.y + bedrockThicknessVoxels * voxelSize;

            // Initialize chunk with density generator
            chunk.InitializeWithGenerator(worldPos =>
            {
                return GenerateDensity(worldPos, bedrockY);
            });
        }

        /// <summary>
        /// Generate density for a world position.
        /// Air above surfaceStartWorldY, solid below.
        /// Positions outside dig bounds are always air.
        /// </summary>
        private float GenerateDensity(Vector3 worldPos, float bedrockY)
        {
            // BOUNDS CHECK: Positions outside dig bounds are AIR
            // This ensures terrain stays strictly within ModularShaftWalls
            if (!_digBounds.Contains(worldPos))
            {
                return 0.0f; // Air outside bounds
            }

            // BEDROCK: Always solid at bottom
            if (worldPos.y < bedrockY)
            {
                return 1.0f;
            }

            // SURFACE TRANSITION
            // Above surfaceStartWorldY = AIR (open pit)
            // Below surfaceStartWorldY = SOLID (diggable terrain)
            float distBelowSurface = surfaceStartWorldY - worldPos.y;

            if (distBelowSurface <= 0)
            {
                // Above surface = AIR
                return 0.0f;
            }

            if (!enableSurfaceGradient)
            {
                // No gradient = hard cutoff to solid
                return 1.0f;
            }

            // Smooth gradient at surface
            float gradientRange = surfaceGradientThickness * chunkManager.VoxelSize;
            if (distBelowSurface < gradientRange)
            {
                return Mathf.Clamp01(distBelowSurface / gradientRange);
            }

            // Below gradient = fully solid
            return 1.0f;
        }

        /// <summary>
        /// Apply roughening pass near surface for natural "started digging" look.
        /// </summary>
        private void ApplyRoughSurfacePass()
        {
            if (roughnessAmount <= 0f || roughBandHeight <= 0f)
                return;

            // Use deterministic random based on seed
            System.Random rng = new System.Random(roughnessSeed);

            float voxelSize = chunkManager.VoxelSize;
            int chunkSize = ChunkMesher.CHUNK_SIZE;
            float chunkWorldSize = chunkSize * voxelSize;

            // Calculate Y range for roughening
            float roughMinY = surfaceStartWorldY - maxCarveDepthBelowSurface;
            float roughMaxY = surfaceStartWorldY + roughBandHeight;

            // Get chunks in the rough band
            ChunkCoord minChunk = chunkManager.WorldToChunkCoord(new Vector3(_digBounds.min.x, roughMinY, _digBounds.min.z));
            ChunkCoord maxChunk = chunkManager.WorldToChunkCoord(new Vector3(_digBounds.max.x, roughMaxY, _digBounds.max.z));

            int voxelsModified = 0;

            for (int cx = minChunk.X; cx <= maxChunk.X; cx++)
            {
                for (int cy = minChunk.Y; cy <= maxChunk.Y; cy++)
                {
                    for (int cz = minChunk.Z; cz <= maxChunk.Z; cz++)
                    {
                        ChunkCoord coord = new ChunkCoord(cx, cy, cz);
                        VoxelChunk chunk = chunkManager.GetChunk(coord);
                        if (chunk == null)
                            continue;

                        voxelsModified += ApplyRoughToChunk(chunk, rng, roughMinY, roughMaxY);
                    }
                }
            }
        }

        /// <summary>
        /// Apply roughening to a single chunk.
        /// </summary>
        private int ApplyRoughToChunk(VoxelChunk chunk, System.Random rng, float minY, float maxY)
        {
            int modified = 0;
            float voxelSize = chunkManager.VoxelSize;
            int size = ChunkMesher.CHUNK_SIZE;

            for (int x = 0; x <= size; x++)
            {
                for (int y = 0; y <= size; y++)
                {
                    for (int z = 0; z <= size; z++)
                    {
                        Vector3 worldPos = chunk.LocalToWorld(x, y, z);

                        // Skip if outside dig bounds
                        if (!_digBounds.Contains(worldPos))
                            continue;

                        // Skip if outside rough band
                        if (worldPos.y < minY || worldPos.y > maxY)
                            continue;

                        float currentDensity = chunk.GetDensityLocal(x, y, z);

                        // Only roughen near the surface (where there's some density)
                        if (currentDensity <= 0.01f || currentDensity >= 0.99f)
                        {
                            // At the edge of solid - apply carving
                            if (currentDensity >= 0.99f && worldPos.y > surfaceStartWorldY - maxCarveDepthBelowSurface)
                            {
                                // Chance to carve
                                float carveChance = roughnessAmount * 0.3f;

                                // Use position-based deterministic noise
                                int hash = HashPosition(worldPos, roughnessSeed);
                                float noise = (hash % 1000) / 1000f;

                                if (noise < carveChance)
                                {
                                    // Carve a small pocket
                                    float removalAmount = roughnessAmount * (0.3f + (hash % 500) / 1000f);
                                    chunk.RemoveDensityLocal(x, y, z, removalAmount);
                                    modified++;
                                }
                            }
                            continue;
                        }

                        // Apply noise to partial density voxels
                        int hash2 = HashPosition(worldPos, roughnessSeed + 1);
                        float noiseValue = ((hash2 % 1000) / 500f) - 1f; // -1 to +1

                        float adjustment = noiseValue * roughnessAmount * 0.5f;

                        // Only allow removal (negative adjustment decreases density)
                        if (adjustment < 0f)
                        {
                            chunk.RemoveDensityLocal(x, y, z, -adjustment);
                            modified++;
                        }
                    }
                }
            }

            if (modified > 0)
            {
                chunk.MarkDirty();
            }

            return modified;
        }

        /// <summary>
        /// Deterministic hash for position-based noise.
        /// </summary>
        private int HashPosition(Vector3 pos, int seed)
        {
            // Quantize position to voxel grid
            int px = Mathf.RoundToInt(pos.x * 100);
            int py = Mathf.RoundToInt(pos.y * 100);
            int pz = Mathf.RoundToInt(pos.z * 100);

            // Simple hash combining
            int hash = seed;
            hash = hash * 31 + px;
            hash = hash * 31 + py;
            hash = hash * 31 + pz;
            return Mathf.Abs(hash);
        }

        /// <summary>
        /// Get the current bootstrap configuration.
        /// </summary>
        public BootstrapConfig GetConfig()
        {
            return new BootstrapConfig
            {
                SurfaceStartY = surfaceStartWorldY,
                BedrockThickness = bedrockThicknessVoxels,
                HasBounds = _hasBounds,
                DigBounds = _digBounds,
                RoughSurfaceEnabled = enableRoughSurface,
                RoughnessSeed = roughnessSeed
            };
        }

        /// <summary>
        /// Configuration data for bootstrap parameters.
        /// </summary>
        public struct BootstrapConfig
        {
            public float SurfaceStartY;
            public int BedrockThickness;
            public bool HasBounds;
            public Bounds DigBounds;
            public bool RoughSurfaceEnabled;
            public int RoughnessSeed;
        }

        /// <summary>
        /// Debug method to log all chunks and their mesh state.
        /// Helps identify why "2 pieces" are being created.
        /// </summary>
        private void LogAllChunksDebug()
        {
            if (chunkManager == null)
            {
                Debug.LogError("[WorldBootstrapper] DEBUG: ChunkManager is NULL!");
                return;
            }

            Debug.Log("=== CHUNK DEBUG INFO ===");

            // Check for duplicate managers
            var allChunkManagers = FindObjectsOfType<ChunkManager>();
            var allBootstrappers = FindObjectsOfType<WorldBootstrapper>();
            Debug.Log($"[DEBUG] ChunkManager instances in scene: {allChunkManagers.Length}");
            Debug.Log($"[DEBUG] WorldBootstrapper instances in scene: {allBootstrappers.Length}");

            if (allChunkManagers.Length > 1)
            {
                Debug.LogError("[DEBUG] DUPLICATE ChunkManagers detected! This could cause 2 terrain pieces!");
                foreach (var cm in allChunkManagers)
                {
                    Debug.Log($"  - ChunkManager on '{cm.gameObject.name}'");
                }
            }

            if (allBootstrappers.Length > 1)
            {
                Debug.LogWarning("[DEBUG] Multiple WorldBootstrappers detected!");
            }

            Debug.Log($"[DEBUG] ChunkManager has {chunkManager.LoadedChunkCount} total chunks");

            // Find all chunk GameObjects in scene
            var allChunks = GameObject.FindObjectsOfType<MeshFilter>();
            int chunksWithMesh = 0;
            int chunksWithVerts = 0;

            foreach (var mf in allChunks)
            {
                // Check if this is a chunk (named "Chunk_*")
                if (mf.gameObject.name.StartsWith("Chunk_"))
                {
                    chunksWithMesh++;
                    int vertCount = mf.sharedMesh != null ? mf.sharedMesh.vertexCount : 0;

                    if (vertCount > 0)
                    {
                        chunksWithVerts++;
                        Debug.Log($"[DEBUG] VISIBLE CHUNK: '{mf.gameObject.name}' - {vertCount} vertices, pos={mf.transform.position}");
                    }
                }
            }

            Debug.Log($"[DEBUG] Total Chunk GameObjects: {chunksWithMesh}");
            Debug.Log($"[DEBUG] Chunks with vertices (visible): {chunksWithVerts}");

            // Find ALL mesh objects with significant vertex count (potential terrain)
            var allMeshFilters = GameObject.FindObjectsOfType<MeshFilter>();
            Debug.Log("[DEBUG] Searching for ALL mesh objects with vertices...");
            foreach (var mf in allMeshFilters)
            {
                if (mf.sharedMesh == null) continue;
                int verts = mf.sharedMesh.vertexCount;

                // Log any mesh with more than 100 vertices that's not a UI element
                if (verts > 100 && !mf.gameObject.name.StartsWith("Chunk_"))
                {
                    string parentName = mf.transform.parent != null ? mf.transform.parent.name : "ROOT";
                    bool isActive = mf.gameObject.activeInHierarchy;
                    Debug.Log($"[DEBUG] MESH OBJECT: '{mf.gameObject.name}' parent='{parentName}' verts={verts} active={isActive} pos={mf.transform.position}");
                }
            }

            // Check for "Chunks" parent objects (could indicate multiple ChunkManagers)
            var allTransforms = FindObjectsOfType<Transform>();
            int chunksParentCount = 0;
            foreach (var t in allTransforms)
            {
                if (t.name == "Chunks" && t.parent != null)
                {
                    chunksParentCount++;
                    Debug.Log($"[DEBUG] 'Chunks' parent found under: '{t.parent.name}'");
                }
            }
            if (chunksParentCount > 1)
            {
                Debug.LogError($"[DEBUG] MULTIPLE 'Chunks' parents found: {chunksParentCount} - this could cause duplicate terrain!");
            }

            Debug.Log("=== END CHUNK DEBUG ===");
        }

        private void OnDrawGizmosSelected()
        {
            // Draw dig bounds if available
            if (_hasBounds)
            {
                Gizmos.color = new Color(0f, 1f, 0f, 0.2f);
                Gizmos.DrawCube(_digBounds.center, _digBounds.size);
                Gizmos.color = Color.green;
                Gizmos.DrawWireCube(_digBounds.center, _digBounds.size);
            }

            // Draw surface plane
            float planeSize = _hasBounds ? Mathf.Max(_digBounds.size.x, _digBounds.size.z) : 20f;
            Vector3 planeCenter = _hasBounds ? new Vector3(_digBounds.center.x, surfaceStartWorldY, _digBounds.center.z) : new Vector3(0, surfaceStartWorldY, 0);

            Gizmos.color = Color.cyan;
            Gizmos.DrawWireCube(planeCenter, new Vector3(planeSize, 0.05f, planeSize));

            // Draw surface label
            Gizmos.color = Color.white;
            Gizmos.DrawLine(planeCenter, planeCenter + Vector3.up * 2f);

            // Draw rough band
            if (enableRoughSurface)
            {
                Gizmos.color = new Color(1f, 0.5f, 0f, 0.2f);
                Vector3 roughCenter = new Vector3(planeCenter.x, surfaceStartWorldY - maxCarveDepthBelowSurface / 2f, planeCenter.z);
                Vector3 roughSize = new Vector3(planeSize * 0.9f, roughBandHeight + maxCarveDepthBelowSurface, planeSize * 0.9f);
                Gizmos.DrawCube(roughCenter, roughSize);
            }
        }
    }
}
