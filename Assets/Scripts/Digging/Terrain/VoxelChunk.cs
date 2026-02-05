using UnityEngine;
using System.Collections.Generic;

namespace BeneathTheFloor.Digging
{
    /// <summary>
    /// Represents a single 16x16x16 voxel chunk.
    /// Contains density data, generates mesh, and manages its own MeshCollider.
    ///
    /// Each chunk is responsible for:
    /// - Storing density values for its voxels
    /// - Generating mesh when dirty
    /// - Managing its MeshCollider
    /// - Providing density samples for meshing (including boundary samples from neighbors)
    ///
    /// FUTURE OPTIMIZATION: Density data can be converted to NativeArray for Jobs/Burst.
    /// </summary>
    public class VoxelChunk
    {
        // Constants
        public const int SIZE = ChunkMesher.CHUNK_SIZE; // 16
        private const int DENSITY_SIZE = SIZE + 1; // 17 (need +1 for boundary samples)

        // Identity
        public readonly ChunkCoord Coord;

        // World-space position of chunk origin (corner at local 0,0,0)
        // Note: Not readonly because we update it after parenting to account for parent offset
        public Vector3 WorldOrigin { get; private set; }

        // Configuration
        private readonly float _voxelSize;

        // Density data: [x, y, z] where each index is 0-16 inclusive
        // We store 17³ densities to include boundary samples
        private readonly float[] _densities;

        // Mesh data
        private Mesh _mesh;
        private MeshFilter _meshFilter;
        private MeshRenderer _meshRenderer;
        private MeshCollider _meshCollider;
        private GameObject _gameObject;

        // Dirty state
        public bool IsDirty { get; private set; }
        public bool NeedsColliderUpdate { get; private set; }

        // Reference to chunk manager for neighbor sampling
        private readonly ChunkManager _chunkManager;

        // Reusable lists for mesh generation (reduces GC)
        private readonly List<Vector3> _vertices;
        private readonly List<int> _triangles;
        private readonly List<Vector3> _normals;
        private readonly Dictionary<int, int> _vertexMap;

        /// <summary>
        /// Create a new voxel chunk.
        /// </summary>
        /// <param name="coord">Chunk coordinate in chunk-space.</param>
        /// <param name="voxelSize">Size of each voxel in meters.</param>
        /// <param name="chunkManager">Reference to the chunk manager for neighbor access.</param>
        /// <param name="parent">Parent transform for the chunk GameObject.</param>
        /// <param name="material">Material to use for rendering.</param>
        public VoxelChunk(
            ChunkCoord coord,
            float voxelSize,
            ChunkManager chunkManager,
            Transform parent,
            Material material)
        {
            Coord = coord;
            _voxelSize = voxelSize;
            _chunkManager = chunkManager;

            // Calculate local grid position (relative to parent)
            Vector3 gridPosition = new Vector3(
                coord.X * SIZE * voxelSize,
                coord.Y * SIZE * voxelSize,
                coord.Z * SIZE * voxelSize);

            // WorldOrigin will be set after parenting to get actual world position
            // Store grid position temporarily
            WorldOrigin = gridPosition;

            // Allocate density storage (17³ = 4913 floats)
            _densities = new float[DENSITY_SIZE * DENSITY_SIZE * DENSITY_SIZE];

            // Pre-allocate mesh lists
            int estimatedVerts = ChunkMesher.EstimateVertexCount(0.5f);
            _vertices = new List<Vector3>(estimatedVerts);
            _triangles = new List<int>(estimatedVerts * 2);
            _normals = new List<Vector3>(estimatedVerts);
            _vertexMap = new Dictionary<int, int>(estimatedVerts);

            // Create GameObject and components
            CreateGameObject(parent, material);

            // Initialize as solid (will be carved by generation or loading)
            InitializeSolid();

            IsDirty = true;
            NeedsColliderUpdate = false;
        }

        /// <summary>
        /// Create the chunk's GameObject with required components.
        /// </summary>
        private void CreateGameObject(Transform parent, Material material)
        {
            _gameObject = new GameObject($"Chunk_{Coord}");
            int terrainLayer = LayerMask.NameToLayer("Terrain");
            if (terrainLayer < 0) terrainLayer = 6; // hard-coded fallback: slot 6 in TagManager
            _gameObject.layer = terrainLayer;
            _gameObject.transform.SetParent(parent, false);
            _gameObject.transform.localPosition = WorldOrigin;

            // CRITICAL: Update WorldOrigin to actual world position after parenting
            // This accounts for any offset applied to the parent (e.g., to align with dig bounds)
            WorldOrigin = _gameObject.transform.position;

            // Add mesh components
            _meshFilter = _gameObject.AddComponent<MeshFilter>();
            _meshRenderer = _gameObject.AddComponent<MeshRenderer>();
            _meshCollider = _gameObject.AddComponent<MeshCollider>();

            // Create mesh
            _mesh = new Mesh();
            _mesh.name = $"ChunkMesh_{Coord}";
            _mesh.indexFormat = UnityEngine.Rendering.IndexFormat.UInt32; // Support >65k vertices
            _meshFilter.sharedMesh = _mesh;

            // Set material
            _meshRenderer.sharedMaterial = material;

            // Configure collider
            _meshCollider.sharedMesh = null; // Will be set after first mesh generation
            _meshCollider.cookingOptions = MeshColliderCookingOptions.EnableMeshCleaning
                                         | MeshColliderCookingOptions.WeldColocatedVertices;
        }

        /// <summary>
        /// Initialize all densities to solid (1.0).
        /// </summary>
        private void InitializeSolid()
        {
            for (int i = 0; i < _densities.Length; i++)
            {
                _densities[i] = 1.0f;
            }
        }

        /// <summary>
        /// Initialize densities with custom generator function.
        /// Generator receives world position and returns density.
        /// </summary>
        public void InitializeWithGenerator(System.Func<Vector3, float> generator)
        {
            for (int x = 0; x < DENSITY_SIZE; x++)
            {
                for (int y = 0; y < DENSITY_SIZE; y++)
                {
                    for (int z = 0; z < DENSITY_SIZE; z++)
                    {
                        Vector3 worldPos = LocalToWorld(x, y, z);
                        float density = generator(worldPos);
                        SetDensityLocal(x, y, z, density);
                    }
                }
            }
            IsDirty = true;
        }

        #region Density Access

        /// <summary>
        /// Get density at local voxel coordinates (0-16 inclusive).
        /// </summary>
        public float GetDensityLocal(int x, int y, int z)
        {
            if (x < 0 || x >= DENSITY_SIZE || y < 0 || y >= DENSITY_SIZE || z < 0 || z >= DENSITY_SIZE)
            {
                // Out of bounds - query neighbor chunk
                return GetDensityFromNeighbor(x, y, z);
            }
            return _densities[GetDensityIndex(x, y, z)];
        }

        /// <summary>
        /// Set density at local voxel coordinates (0-16 inclusive).
        /// </summary>
        public void SetDensityLocal(int x, int y, int z, float density)
        {
            if (x < 0 || x >= DENSITY_SIZE || y < 0 || y >= DENSITY_SIZE || z < 0 || z >= DENSITY_SIZE)
                return;

            int index = GetDensityIndex(x, y, z);
            _densities[index] = Mathf.Clamp01(density);
        }

        /// <summary>
        /// Modify density at local coordinates (add to existing value).
        /// </summary>
        public void ModifyDensityLocal(int x, int y, int z, float delta)
        {
            if (x < 0 || x >= DENSITY_SIZE || y < 0 || y >= DENSITY_SIZE || z < 0 || z >= DENSITY_SIZE)
                return;

            int index = GetDensityIndex(x, y, z);
            _densities[index] = Mathf.Clamp01(_densities[index] + delta);
        }

        /// <summary>
        /// Get density at world position.
        /// </summary>
        public float GetDensityWorld(Vector3 worldPos)
        {
            WorldToLocal(worldPos, out int x, out int y, out int z);
            return GetDensityLocal(x, y, z);
        }

        /// <summary>
        /// Calculate array index from local coordinates.
        /// </summary>
        private int GetDensityIndex(int x, int y, int z)
        {
            return x + y * DENSITY_SIZE + z * DENSITY_SIZE * DENSITY_SIZE;
        }

        /// <summary>
        /// Get density from neighboring chunk when local coords are out of bounds.
        /// </summary>
        private float GetDensityFromNeighbor(int x, int y, int z)
        {
            // Track if we're querying horizontal (X/Z) vs vertical (Y) neighbor
            bool horizontalQuery = false;

            // Calculate which neighbor chunk to query
            int cx = Coord.X;
            int cy = Coord.Y;
            int cz = Coord.Z;

            // Adjust chunk coord and local coord for out-of-bounds access
            if (x < 0) { cx--; x += SIZE; horizontalQuery = true; }
            else if (x >= DENSITY_SIZE) { cx++; x -= SIZE; horizontalQuery = true; }

            if (y < 0) { cy--; y += SIZE; }
            else if (y >= DENSITY_SIZE) { cy++; y -= SIZE; }

            if (z < 0) { cz--; z += SIZE; horizontalQuery = true; }
            else if (z >= DENSITY_SIZE) { cz++; z -= SIZE; horizontalQuery = true; }

            // Query neighbor
            ChunkCoord neighborCoord = new ChunkCoord(cx, cy, cz);
            VoxelChunk neighbor = _chunkManager?.GetChunk(neighborCoord);

            if (neighbor != null)
            {
                return neighbor.GetDensityLocal(x, y, z);
            }

            // No neighbor chunk exists at this boundary
            // For HORIZONTAL boundaries (X/Z - where walls are): return AIR to create surface
            // For VERTICAL boundaries (Y - top/bottom): return SOLID to prevent holes
            if (horizontalQuery)
            {
                return 0.0f; // Air - creates surface at wall boundary
            }
            return 1.0f; // Solid - prevents holes at top/bottom
        }

        #endregion

        #region Coordinate Conversion

        /// <summary>
        /// Convert local voxel coordinates to world position.
        /// </summary>
        public Vector3 LocalToWorld(int x, int y, int z)
        {
            return WorldOrigin + new Vector3(x, y, z) * _voxelSize;
        }

        /// <summary>
        /// Convert world position to local voxel coordinates.
        /// </summary>
        public void WorldToLocal(Vector3 worldPos, out int x, out int y, out int z)
        {
            Vector3 local = (worldPos - WorldOrigin) / _voxelSize;
            x = Mathf.RoundToInt(local.x);
            y = Mathf.RoundToInt(local.y);
            z = Mathf.RoundToInt(local.z);
        }

        /// <summary>
        /// Check if a world position is within this chunk's bounds.
        /// </summary>
        public bool ContainsWorldPosition(Vector3 worldPos)
        {
            Vector3 local = (worldPos - WorldOrigin) / _voxelSize;
            return local.x >= 0 && local.x < SIZE &&
                   local.y >= 0 && local.y < SIZE &&
                   local.z >= 0 && local.z < SIZE;
        }

        #endregion

        #region Mesh Generation

        /// <summary>
        /// Regenerate the chunk mesh if dirty.
        /// </summary>
        public void UpdateMeshIfDirty()
        {
            if (!IsDirty)
                return;

            RegenerateMesh();
            IsDirty = false;
            NeedsColliderUpdate = true;
        }

        /// <summary>
        /// Force mesh regeneration regardless of dirty state.
        /// </summary>
        /// <param name="immediateCollider">If true, also updates collider immediately (use for bootstrap).</param>
        public void RegenerateMesh(bool immediateCollider = false)
        {
            // Create density sampler that handles boundary cases
            ChunkMesher.DensitySampler sampler = (x, y, z) => GetDensityLocal(x, y, z);

            // Generate mesh data
            ChunkMesher.GenerateMeshNonAlloc(
                sampler,
                _voxelSize,
                _vertices,
                _triangles,
                _normals,
                _vertexMap);

            // Apply to mesh
            _mesh.Clear();

            if (_vertices.Count > 0)
            {
                _mesh.SetVertices(_vertices);
                _mesh.SetTriangles(_triangles, 0);
                _mesh.SetNormals(_normals);
                _mesh.RecalculateBounds();
            }

            IsDirty = false;

            // CRITICAL: For bootstrap/initial terrain, update collider immediately
            if (immediateCollider)
            {
                ForceUpdateCollider();
            }
            else
            {
                NeedsColliderUpdate = true;
            }
        }

        /// <summary>
        /// Update the MeshCollider with current mesh data.
        /// Call this after mesh regeneration when physics needs to be updated.
        /// </summary>
        public void UpdateCollider()
        {
            if (!NeedsColliderUpdate)
                return;

            ForceUpdateCollider();
        }

        /// <summary>
        /// FORCE update the MeshCollider immediately, regardless of NeedsColliderUpdate flag.
        /// Enforces collider correctness: disables collider if mesh is empty, enables if valid.
        /// </summary>
        public void ForceUpdateCollider()
        {
            if (_meshCollider == null)
            {
                Debug.LogError($"[VoxelChunk] {Coord}: MeshCollider component is NULL!");
                return;
            }

            // COLLIDER CORRECTNESS RULES:
            // Check if mesh is null, empty vertices, or empty triangles
            bool hasValidMesh = _mesh != null &&
                                _vertices != null && _vertices.Count > 0 &&
                                _triangles != null && _triangles.Count > 0;

            if (!hasValidMesh)
            {
                // EMPTY/INVALID MESH: Disable collider to prevent ghost collisions
                _meshCollider.sharedMesh = null;
                _meshCollider.enabled = false;
            }
            else
            {
                // VALID MESH: Enable collider and assign mesh
                // Must set to null first, then assign, to force refresh
                _meshCollider.enabled = true;
                _meshCollider.sharedMesh = null;
                _meshCollider.sharedMesh = _mesh;

                // SAFETY VALIDATION: Verify assignment succeeded
                if (_meshCollider.sharedMesh == null)
                {
                    Debug.LogError($"[VoxelChunk] {Coord}: CRITICAL - MeshCollider.sharedMesh assignment FAILED! Mesh has {_vertices.Count} verts, {_triangles.Count / 3} tris.");
                    _meshCollider.enabled = false;
                }
            }

            NeedsColliderUpdate = false;
        }

        /// <summary>
        /// Check if collider is properly assigned. Returns false if collider needs fixing.
        /// </summary>
        public bool ValidateCollider()
        {
            if (_meshCollider == null)
                return false;

            // If we have vertices but no collider mesh, something is wrong
            if (_vertices != null && _vertices.Count > 0 && _meshCollider.sharedMesh == null)
            {
                Debug.LogError($"[VoxelChunk] {Coord}: VALIDATION FAILED - Has {_vertices.Count} verts but MeshCollider.sharedMesh is NULL!");
                return false;
            }

            return true;
        }

        /// <summary>
        /// Mark the chunk as needing mesh regeneration.
        /// </summary>
        public void MarkDirty()
        {
            IsDirty = true;
        }

        /// <summary>
        /// Mark the chunk as needing both mesh regeneration AND collider update.
        /// Used after island cleanup to ensure collider is synchronized.
        /// </summary>
        public void MarkDirtyForCollider()
        {
            IsDirty = true;
            NeedsColliderUpdate = true;
        }

        #endregion

        #region Save/Load

        /// <summary>
        /// Get a copy of the density array for saving.
        /// </summary>
        public float[] GetDensitiesForSave()
        {
            float[] copy = new float[_densities.Length];
            System.Array.Copy(_densities, copy, _densities.Length);
            return copy;
        }

        /// <summary>
        /// Set all densities from a saved array.
        /// Marks the chunk as dirty for mesh regeneration.
        /// </summary>
        public void SetDensitiesFromSave(float[] savedDensities)
        {
            if (savedDensities == null || savedDensities.Length != _densities.Length)
            {
                UnityEngine.Debug.LogError($"[VoxelChunk] Invalid saved densities length: {savedDensities?.Length ?? 0}, expected: {_densities.Length}");
                return;
            }

            System.Array.Copy(savedDensities, _densities, _densities.Length);
            IsDirty = true;
            NeedsColliderUpdate = true;
        }

        /// <summary>
        /// Check if this chunk has been modified from its initial state (has any removed terrain).
        /// Used to optimize save data by only storing modified chunks.
        /// </summary>
        public bool HasBeenModified()
        {
            // A chunk is considered modified if any density value is less than 1.0
            // (assuming terrain starts fully solid at 1.0)
            for (int i = 0; i < _densities.Length; i++)
            {
                if (_densities[i] < 0.99f)
                    return true;
            }
            return false;
        }

        #endregion

        #region Digging

        /// <summary>
        /// Apply a spherical dig operation to this chunk.
        /// Returns the approximate volume of terrain removed.
        ///
        /// CRITICAL: This method ONLY REMOVES terrain (decreases density).
        /// It will NEVER increase density or create terrain.
        /// </summary>
        /// <param name="worldCenter">Center of dig sphere in world space.</param>
        /// <param name="radius">Radius of dig sphere in meters.</param>
        /// <param name="strength">Dig strength (0-1). Positive values REMOVE terrain.</param>
        /// <returns>Approximate volume removed in cubic meters.</returns>
        public float ApplyDig(Vector3 worldCenter, float radius, float strength)
        {
            // GUARD: Ensure strength is positive (removal only)
            // Negative strength would increase density = terrain creation = NOT ALLOWED
            if (strength <= 0f)
            {
                Debug.LogWarning($"[VoxelChunk] ApplyDig blocked: strength must be positive (was {strength})");
                return 0f;
            }

            float volumeRemoved = 0f;
            float radiusSq = radius * radius;
            float voxelVolume = _voxelSize * _voxelSize * _voxelSize;

            // Convert dig center to local space
            Vector3 localCenter = (worldCenter - WorldOrigin) / _voxelSize;

            // Calculate affected voxel range
            int minX = Mathf.Max(0, Mathf.FloorToInt(localCenter.x - radius / _voxelSize));
            int maxX = Mathf.Min(SIZE, Mathf.CeilToInt(localCenter.x + radius / _voxelSize));
            int minY = Mathf.Max(0, Mathf.FloorToInt(localCenter.y - radius / _voxelSize));
            int maxY = Mathf.Min(SIZE, Mathf.CeilToInt(localCenter.y + radius / _voxelSize));
            int minZ = Mathf.Max(0, Mathf.FloorToInt(localCenter.z - radius / _voxelSize));
            int maxZ = Mathf.Min(SIZE, Mathf.CeilToInt(localCenter.z + radius / _voxelSize));

            // DEBUG: Verbose logging disabled to reduce console spam
            // Debug.Log($"[VoxelChunk {Coord}] ApplyDig: worldCenter={worldCenter}");

            // Apply dig to each affected voxel
            for (int x = minX; x <= maxX; x++)
            {
                for (int y = minY; y <= maxY; y++)
                {
                    for (int z = minZ; z <= maxZ; z++)
                    {
                        Vector3 voxelWorld = LocalToWorld(x, y, z);
                        float distSq = (voxelWorld - worldCenter).sqrMagnitude;

                        if (distSq < radiusSq)
                        {
                            // Calculate falloff (quadratic, smooth at edges)
                            float t = distSq / radiusSq;
                            float falloff = 1f - t; // Linear falloff
                            falloff = falloff * falloff; // Quadratic falloff

                            // Get current density
                            float currentDensity = GetDensityLocal(x, y, z);

                            // GUARD: Skip if already air (nothing to remove)
                            if (currentDensity <= 0f)
                            {
                                continue;
                            }

                            // Calculate removal amount (ALWAYS decreases density)
                            float removal = strength * falloff;

                            // CRITICAL: New density is ALWAYS less than or equal to current
                            // Mathf.Max(0f, ...) ensures we don't go negative
                            // The subtraction ensures we only REMOVE
                            float newDensity = Mathf.Max(0f, currentDensity - removal);

                            // GUARD: Double-check we're not increasing density
                            if (newDensity > currentDensity)
                            {
                                Debug.LogError($"[VoxelChunk] CRITICAL: Density would increase! Blocking. Old={currentDensity}, New={newDensity}");
                                continue;
                            }

                            // Track volume removed
                            float densityRemoved = currentDensity - newDensity;
                            volumeRemoved += densityRemoved * voxelVolume;

                            // Apply new density (guaranteed to be <= current)
                            SetDensityLocal(x, y, z, newDensity);
                        }
                    }
                }
            }

            // DEBUG: Verbose logging disabled
            // Debug.Log($"[VoxelChunk {Coord}] ApplyDig result: volumeRemoved={volumeRemoved}");

            if (volumeRemoved > 0f)
            {
                // ARTIFACT CLEANUP PASS: Remove isolated voxels that would create floating remnants
                // Check voxels in the affected area - if they have few solid neighbors, remove them
                volumeRemoved += CleanupIsolatedVoxels(minX, maxX, minY, maxY, minZ, maxZ, voxelVolume);

                IsDirty = true;
            }

            return volumeRemoved;
        }

        /// <summary>
        /// Cleanup pass to remove only "true crumbs" - very low density voxels with no solid support.
        /// This is intentionally conservative to preserve gradient-based natural digging feel.
        /// Only removes voxels that are:
        ///   1) Very low density (< CRUMB_THRESHOLD) - barely any terrain
        ///   2) Have NO neighbors at or above isoLevel - completely isolated
        /// </summary>
        private float CleanupIsolatedVoxels(int minX, int maxX, int minY, int maxY, int minZ, int maxZ, float voxelVolume)
        {
            const float CRUMB_THRESHOLD = 0.08f;  // Only target very low density "crumbs"
            const float ISO_LEVEL = 0.5f;         // Marching cubes surface threshold
            float volumeRemoved = 0f;

            // Expand search area by 1 to catch edge artifacts
            int searchMinX = Mathf.Max(0, minX - 1);
            int searchMaxX = Mathf.Min(SIZE, maxX + 1);
            int searchMinY = Mathf.Max(0, minY - 1);
            int searchMaxY = Mathf.Min(SIZE, maxY + 1);
            int searchMinZ = Mathf.Max(0, minZ - 1);
            int searchMaxZ = Mathf.Min(SIZE, maxZ + 1);

            // Track voxels to remove (can't modify during iteration)
            var voxelsToRemove = new System.Collections.Generic.List<(int x, int y, int z, float density)>();

            for (int x = searchMinX; x <= searchMaxX; x++)
            {
                for (int y = searchMinY; y <= searchMaxY; y++)
                {
                    for (int z = searchMinZ; z <= searchMaxZ; z++)
                    {
                        float density = GetDensityLocal(x, y, z);

                        // Skip if already empty or above crumb threshold
                        // We ONLY target very low density isolated voxels
                        if (density <= 0f || density >= CRUMB_THRESHOLD)
                            continue;

                        // Check if ANY neighbor is solid (at or above isoLevel)
                        // If even one neighbor is solid, this crumb might be part of a gradient - keep it
                        bool hasSolidNeighbor =
                            GetDensityLocal(x - 1, y, z) >= ISO_LEVEL ||
                            GetDensityLocal(x + 1, y, z) >= ISO_LEVEL ||
                            GetDensityLocal(x, y - 1, z) >= ISO_LEVEL ||
                            GetDensityLocal(x, y + 1, z) >= ISO_LEVEL ||
                            GetDensityLocal(x, y, z - 1) >= ISO_LEVEL ||
                            GetDensityLocal(x, y, z + 1) >= ISO_LEVEL;

                        // Only remove if completely isolated (no solid neighbors)
                        if (!hasSolidNeighbor)
                        {
                            voxelsToRemove.Add((x, y, z, density));
                        }
                    }
                }
            }

            // Remove true crumbs
            foreach (var (x, y, z, density) in voxelsToRemove)
            {
                SetDensityLocal(x, y, z, 0f);
                volumeRemoved += density * voxelVolume;
            }

            return volumeRemoved;
        }

        /// <summary>
        /// REMOVAL ONLY: Decrease density at a specific voxel.
        /// This method ONLY removes terrain, never adds.
        /// </summary>
        /// <param name="x">Local X coordinate.</param>
        /// <param name="y">Local Y coordinate.</param>
        /// <param name="z">Local Z coordinate.</param>
        /// <param name="removalAmount">Amount to subtract from density (must be positive).</param>
        /// <returns>Actual amount removed.</returns>
        public float RemoveDensityLocal(int x, int y, int z, float removalAmount)
        {
            if (removalAmount <= 0f)
            {
                return 0f;
            }

            if (x < 0 || x >= DENSITY_SIZE || y < 0 || y >= DENSITY_SIZE || z < 0 || z >= DENSITY_SIZE)
            {
                return 0f;
            }

            int index = GetDensityIndex(x, y, z);
            float currentDensity = _densities[index];

            if (currentDensity <= 0f)
            {
                return 0f; // Already air
            }

            float newDensity = Mathf.Max(0f, currentDensity - removalAmount);
            _densities[index] = newDensity;

            return currentDensity - newDensity;
        }

        #endregion

        #region Lifecycle

        /// <summary>
        /// Enable or disable the chunk's rendering.
        /// </summary>
        public void SetVisible(bool visible)
        {
            if (_gameObject != null)
            {
                _gameObject.SetActive(visible);
            }
        }

        /// <summary>
        /// Destroy the chunk and clean up resources.
        /// </summary>
        public void Destroy()
        {
            if (_mesh != null)
            {
                Object.Destroy(_mesh);
                _mesh = null;
            }

            if (_gameObject != null)
            {
                Object.Destroy(_gameObject);
                _gameObject = null;
            }
        }

        #endregion

        #region Mesh Access

        /// <summary>
        /// Get the chunk's mesh for reading (e.g., for mesh splitting).
        /// Returns null if mesh hasn't been generated yet.
        /// </summary>
        public Mesh GetMesh()
        {
            return _mesh;
        }

        /// <summary>
        /// Get the chunk's material.
        /// </summary>
        public Material GetMaterial()
        {
            return _meshRenderer != null ? _meshRenderer.sharedMaterial : null;
        }

        #endregion

        #region Debug

        /// <summary>
        /// Get debug info about this chunk.
        /// </summary>
        public string GetDebugInfo()
        {
            int vertCount = _vertices?.Count ?? 0;
            int triCount = (_triangles?.Count ?? 0) / 3;
            return $"Chunk {Coord}: {vertCount} verts, {triCount} tris, dirty={IsDirty}";
        }

        #endregion

        #region Floating Island Detection

        /// <summary>
        /// Marching cubes iso-level. Voxels with density >= this are considered SOLID.
        /// </summary>
        public const float ISO_LEVEL = 0.5f;

        /// <summary>
        /// Detect and remove floating islands within this chunk.
        /// Uses flood-fill to find connected components.
        /// A component is floating if NOT connected to:
        ///   - Bottom of chunk (y = 0), OR
        ///   - Any neighbor chunk solid voxel
        /// Returns the number of voxels removed.
        /// </summary>
        public int DetectAndRemoveFloatingIslands()
        {
            // Build set of all solid voxel positions
            var solidVoxels = new HashSet<Vector3Int>();

            for (int x = 0; x < SIZE; x++)
            {
                for (int y = 0; y < SIZE; y++)
                {
                    for (int z = 0; z < SIZE; z++)
                    {
                        if (GetDensityLocal(x, y, z) >= ISO_LEVEL)
                        {
                            solidVoxels.Add(new Vector3Int(x, y, z));
                        }
                    }
                }
            }

            if (solidVoxels.Count == 0)
                return 0;

            // Find all connected components using flood-fill
            var visited = new HashSet<Vector3Int>();
            var floatingVoxels = new List<Vector3Int>();
            int totalRemoved = 0;

            // Direction vectors for 6-connected neighbors
            Vector3Int[] directions = new Vector3Int[]
            {
                new Vector3Int(1, 0, 0),
                new Vector3Int(-1, 0, 0),
                new Vector3Int(0, 1, 0),
                new Vector3Int(0, -1, 0),
                new Vector3Int(0, 0, 1),
                new Vector3Int(0, 0, -1)
            };

            foreach (var startVoxel in solidVoxels)
            {
                if (visited.Contains(startVoxel))
                    continue;

                // Flood-fill to find this component
                var component = new List<Vector3Int>();
                var queue = new Queue<Vector3Int>();
                bool isConnectedToGround = false;
                bool isConnectedToNeighborChunk = false;

                queue.Enqueue(startVoxel);
                visited.Add(startVoxel);

                while (queue.Count > 0)
                {
                    var current = queue.Dequeue();
                    component.Add(current);

                    // Check if connected to bottom of chunk
                    if (current.y == 0)
                    {
                        isConnectedToGround = true;
                    }

                    // Check all 6 neighbors
                    foreach (var dir in directions)
                    {
                        var neighbor = current + dir;

                        // Check if neighbor is outside chunk bounds
                        if (neighbor.x < 0 || neighbor.x >= SIZE ||
                            neighbor.y < 0 || neighbor.y >= SIZE ||
                            neighbor.z < 0 || neighbor.z >= SIZE)
                        {
                            // Check if there's solid terrain in neighbor chunk at this boundary
                            if (IsNeighborChunkSolidAt(neighbor))
                            {
                                isConnectedToNeighborChunk = true;
                            }
                            continue;
                        }

                        // Skip if already visited
                        if (visited.Contains(neighbor))
                            continue;

                        // Add to component if solid
                        if (solidVoxels.Contains(neighbor))
                        {
                            visited.Add(neighbor);
                            queue.Enqueue(neighbor);
                        }
                    }
                }

                // If this component is NOT connected to ground or neighbor chunk, it's floating
                if (!isConnectedToGround && !isConnectedToNeighborChunk)
                {
                    floatingVoxels.AddRange(component);
                }
            }

            // Hard-delete all floating voxels (set density = 0)
            foreach (var voxel in floatingVoxels)
            {
                SetDensityLocal(voxel.x, voxel.y, voxel.z, 0f);
                totalRemoved++;
            }

            if (totalRemoved > 0)
            {
                IsDirty = true;
            }

            return totalRemoved;
        }

        /// <summary>
        /// Check if there's solid terrain in a neighbor chunk at the given local position.
        /// The position is outside this chunk's bounds.
        /// </summary>
        private bool IsNeighborChunkSolidAt(Vector3Int localPos)
        {
            if (_chunkManager == null)
                return true; // Assume connected if we can't check (safe default)

            // Calculate which neighbor chunk and the local position within it
            int cx = Coord.X;
            int cy = Coord.Y;
            int cz = Coord.Z;
            int nx = localPos.x;
            int ny = localPos.y;
            int nz = localPos.z;

            // Adjust chunk coord and local coord based on which boundary we crossed
            if (nx < 0) { cx--; nx += SIZE; }
            else if (nx >= SIZE) { cx++; nx -= SIZE; }

            if (ny < 0) { cy--; ny += SIZE; }
            else if (ny >= SIZE) { cy++; ny -= SIZE; }

            if (nz < 0) { cz--; nz += SIZE; }
            else if (nz >= SIZE) { cz++; nz -= SIZE; }

            // Get the neighbor chunk
            ChunkCoord neighborCoord = new ChunkCoord(cx, cy, cz);
            VoxelChunk neighbor = _chunkManager.GetChunk(neighborCoord);

            if (neighbor == null)
            {
                // No neighbor chunk exists - treat as connected (world boundary)
                return true;
            }

            // Check density in neighbor chunk
            return neighbor.GetDensityLocal(nx, ny, nz) >= ISO_LEVEL;
        }

        #endregion
    }
}
