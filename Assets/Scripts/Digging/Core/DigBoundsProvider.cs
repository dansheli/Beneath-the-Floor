using UnityEngine;
using BeneathTheFloor.WorldRooms;

namespace BeneathTheFloor.Digging
{
    /// <summary>
    /// Provides the dig bounds for the digging system.
    /// Resolves bounds from ModularShaftWallManager or explicit configuration.
    ///
    /// PRIORITY ORDER:
    /// 1. Explicit bounds collider (if boundsSource = ExplicitCollider)
    /// 2. BoxCollider on ModularShaftWallManager or children (intended as inner bounds)
    /// 3. Combined child Colliders bounds
    /// 4. Combined child Renderer bounds
    /// 5. Fallback to manual bounds configuration
    ///
    /// USAGE:
    /// - Attach alongside ChunkManager/WorldBootstrapper
    /// - Assign ModularShaftWallManager reference
    /// - Bounds are resolved on Awake()
    /// </summary>
    [DefaultExecutionOrder(10)] // Run AFTER ModularShaftWallManager (default 0) but before WorldBootstrapper (50)
    public class DigBoundsProvider : MonoBehaviour
    {
        public enum BoundsSource
        {
            /// <summary>
            /// Auto-detect from ModularShaftWallManager (BoxCollider > Colliders > Renderers).
            /// </summary>
            Auto,

            /// <summary>
            /// Use a specific collider assigned in explicitBoundsCollider.
            /// </summary>
            ExplicitCollider,

            /// <summary>
            /// Use manual bounds configured in Inspector.
            /// </summary>
            Manual
        }

        [Header("Bounds Source")]
        [Tooltip("How to resolve the dig bounds.")]
        [SerializeField] private BoundsSource boundsSource = BoundsSource.Auto;

        [Tooltip("Reference to ModularShaftWallManager for automatic bounds resolution.")]
        [SerializeField] private ModularShaftWallManager shaftWallManager;

        [Tooltip("Explicit collider to use as bounds (only if boundsSource = ExplicitCollider).")]
        [SerializeField] private Collider explicitBoundsCollider;

        [Header("Manual Bounds (if boundsSource = Manual, or as fallback)")]
        [SerializeField] private Vector3 manualBoundsCenter = new Vector3(0, -53f, 0);
        [SerializeField] private Vector3 manualBoundsSize = new Vector3(20f, 120f, 20f);

        [Tooltip("If Auto resolution fails, use manual bounds as fallback instead of failing.")]
        [SerializeField] private bool useManualAsFallback = true;

        [Header("Bounds Adjustment")]
        [Tooltip("Scale factor for horizontal bounds (1.0 = 100%, 1.1 = 110%). Applied first.")]
        [SerializeField] private float horizontalScale = 1.1f;

        [Tooltip("EXPAND bounds outward on X/Z by this amount (use positive values to make dig area larger).")]
        [SerializeField] private float horizontalExpansion = 0f;

        [Tooltip("Shrink bounds inward on X/Z by this amount (prevents terrain touching walls). Applied AFTER expansion.")]
        [SerializeField] private float horizontalInset = 0f;

        [Tooltip("Additional depth below resolved bounds (if terrain should extend deeper).")]
        [SerializeField] private float additionalDepth = 20f;

        [Header("Debug")]
        [SerializeField] private bool enableDebugLogs = false;
        [SerializeField] private bool drawGizmos = true;
        [SerializeField] private Color gizmoBoundsColor = new Color(0f, 1f, 0f, 0.3f);
        [SerializeField] private Color gizmoWireColor = Color.green;

        // Resolved bounds
        private Bounds _resolvedBounds;
        private bool _boundsValid = false;
        private string _boundsSourceDescription;

        // Singleton
        public static DigBoundsProvider Instance { get; private set; }

        /// <summary>
        /// The resolved dig bounds in world space.
        /// </summary>
        public Bounds DigBounds => _resolvedBounds;

        /// <summary>
        /// Returns true if bounds were successfully resolved.
        /// </summary>
        public bool HasValidBounds => _boundsValid;

        /// <summary>
        /// Description of where bounds were resolved from.
        /// </summary>
        public string BoundsSourceDescription => _boundsSourceDescription;

        // Throttled logging
        private float _lastOutOfBoundsLogTime;
        private const float OUT_OF_BOUNDS_LOG_INTERVAL = 2f;

        private void Awake()
        {
            // FORCE disable debug logs (scene-serialized value may be true)
            enableDebugLogs = false;

            // Singleton
            if (Instance != null && Instance != this)
            {
                if (enableDebugLogs) Debug.LogWarning("[DigBoundsProvider] Duplicate instance detected. Destroying.");
                Destroy(this);
                return;
            }
            Instance = this;
        }

        private void Start()
        {
            // Resolve bounds in Start() to ensure ModularShaftWallManager has built
            // and its TerrainBounds is available
            InitializeBounds();
        }

        /// <summary>
        /// Initialize bounds from the best available source.
        /// </summary>
        private void InitializeBounds()
        {
            // PRIORITY 1: Calculate EXACT bounds from actual wall segment positions
            // This reads the 4 walls (North, South, East, West) and calculates the precise inner space
            if (TryCalculateExactBoundsFromWalls(out Vector3 exactCenter, out Vector3 exactSize))
            {
                boundsSource = BoundsSource.Manual;
                manualBoundsCenter = exactCenter;
                manualBoundsSize = exactSize;
                horizontalScale = 1f;  // No scaling - use exact wall measurements
                horizontalExpansion = 0f;
                horizontalInset = 0f;
                additionalDepth = 0f;

                if (enableDebugLogs) Debug.Log($"[DigBoundsProvider] SUCCESS: Using EXACT wall bounds - center={exactCenter}, size={exactSize}");
                ResolveBounds();
                return;
            }

            // PRIORITY 2: Try ModularShaftWallManager's GetInnerBoundsFromWalls
            bool boundsFromWalls = TryCalculateBoundsFromWalls();
            if (boundsFromWalls)
            {
                if (enableDebugLogs) Debug.Log($"[DigBoundsProvider] Using ModularShaftWallManager bounds: center={manualBoundsCenter}, size={manualBoundsSize}");
                ResolveBounds();
                return;
            }

            // PRIORITY 3: Fallback to terrain manager parameters
            var terrainManager = FindObjectOfType<UndergroundTerrainManager>();
            Vector3 shaftCenterXZ = FindShaftCenterPosition();

            if (terrainManager != null)
            {
                float horizontalExtent = terrainManager.horizontalExtent;
                float maxDepth = terrainManager.maxDepthMeters;
                float basementFloorY = terrainManager.basementFloorY;

                float centerY = basementFloorY - (maxDepth / 2f);
                Vector3 center = new Vector3(shaftCenterXZ.x, centerY, shaftCenterXZ.z);
                Vector3 size = new Vector3(horizontalExtent, maxDepth, horizontalExtent);

                boundsSource = BoundsSource.Manual;
                manualBoundsCenter = center;
                manualBoundsSize = size;
                horizontalScale = 1f;
                horizontalExpansion = 0f;
                horizontalInset = 0f;
                additionalDepth = 0f;

                if (enableDebugLogs) Debug.Log($"[DigBoundsProvider] FALLBACK: Using terrain manager + shaft position: center={center}, size={size}");
            }
            else
            {
                if (enableDebugLogs) Debug.LogError("[DigBoundsProvider] Could not determine bounds from walls or terrain manager!");
            }

            ResolveBounds();
        }

        /// <summary>
        /// Try to calculate EXACT bounds from actual wall segment positions.
        /// Returns both center AND size calculated from the 4 walls.
        /// </summary>
        /// <returns>True if bounds were calculated, false otherwise</returns>
        private bool TryCalculateExactBoundsFromWalls(out Vector3 center, out Vector3 size)
        {
            center = Vector3.zero;
            size = Vector3.zero;

            // Find ModularShaftWalls container
            GameObject shaftWalls = GameObject.Find("ModularShaftWalls");
            if (shaftWalls == null)
            {
                var wallManager = FindObjectOfType<ModularShaftWallManager>();
                if (wallManager != null)
                {
                    Transform wallsChild = wallManager.transform.Find("ModularShaftWalls");
                    if (wallsChild != null)
                        shaftWalls = wallsChild.gameObject;
                }
            }

            if (shaftWalls == null)
            {
                if (enableDebugLogs) Debug.LogWarning("[DigBoundsProvider] ModularShaftWalls not found!");
                return false;
            }

            // Find wall containers
            Transform northWall = shaftWalls.transform.Find("Wall_North");
            Transform southWall = shaftWalls.transform.Find("Wall_South");
            Transform eastWall = shaftWalls.transform.Find("Wall_East");
            Transform westWall = shaftWalls.transform.Find("Wall_West");

            // Get positions from first segment of each wall
            Vector3? northPos = GetFirstChildPosition(northWall);
            Vector3? southPos = GetFirstChildPosition(southWall);
            Vector3? eastPos = GetFirstChildPosition(eastWall);
            Vector3? westPos = GetFirstChildPosition(westWall);

            if (!northPos.HasValue || !southPos.HasValue || !eastPos.HasValue || !westPos.HasValue)
            {
                if (enableDebugLogs) Debug.LogWarning("[DigBoundsProvider] Could not find all 4 wall segments!");
                return false;
            }

            // Wall positions (center of each wall)
            float northZ = northPos.Value.z;
            float southZ = southPos.Value.z;
            float eastX = eastPos.Value.x;
            float westX = westPos.Value.x;

            // Wall thickness (standard is 0.4m, but we'll use the wall centers)
            float wallThickness = 0.4f;
            float halfThickness = wallThickness / 2f;

            // Small overlap to close gaps at corners (terrain extends slightly into walls)
            float gapFix = 0.15f;

            // Calculate INNER bounds (inside the walls) + tiny overlap
            float innerMinX = westX + halfThickness - gapFix;
            float innerMaxX = eastX - halfThickness + gapFix;
            float innerMinZ = southZ + halfThickness - gapFix;
            float innerMaxZ = northZ - halfThickness + gapFix;

            // Calculate center
            float centerX = (innerMinX + innerMaxX) / 2f;
            float centerZ = (innerMinZ + innerMaxZ) / 2f;

            // Calculate size
            float sizeX = innerMaxX - innerMinX;
            float sizeZ = innerMaxZ - innerMinZ;

            // Get depth from terrain manager or use default
            float depth = 120f;
            float basementFloorY = -3f;
            var terrainManager = FindObjectOfType<UndergroundTerrainManager>();
            if (terrainManager != null)
            {
                depth = terrainManager.maxDepthMeters;
                basementFloorY = terrainManager.basementFloorY;
            }

            float centerY = basementFloorY - (depth / 2f);

            center = new Vector3(centerX, centerY, centerZ);
            size = new Vector3(sizeX, depth, sizeZ);

            if (enableDebugLogs)
            {
                Debug.Log($"[DigBoundsProvider] === EXACT WALL BOUNDS CALCULATION ===");
                Debug.Log($"[DigBoundsProvider] Wall positions: North Z={northZ:F2}, South Z={southZ:F2}, East X={eastX:F2}, West X={westX:F2}");
                Debug.Log($"[DigBoundsProvider] Inner bounds: X=[{innerMinX:F2} to {innerMaxX:F2}], Z=[{innerMinZ:F2} to {innerMaxZ:F2}]");
                Debug.Log($"[DigBoundsProvider] Terrain center: ({centerX:F2}, {centerY:F2}, {centerZ:F2})");
                Debug.Log($"[DigBoundsProvider] Terrain size: ({sizeX:F2}, {depth:F2}, {sizeZ:F2})");
            }

            return true;
        }

        /// <summary>
        /// Legacy method - now calls TryCalculateExactBoundsFromWalls
        /// </summary>
        private Vector3? TryGetWallSegmentsCenter()
        {
            if (TryCalculateExactBoundsFromWalls(out Vector3 center, out Vector3 size))
            {
                return new Vector3(center.x, 0f, center.z);
            }
            return null;
        }

        /// <summary>
        /// Get the world position of the first child transform.
        /// </summary>
        private Vector3? GetFirstChildPosition(Transform parent)
        {
            if (parent == null || parent.childCount == 0)
                return null;
            return parent.GetChild(0).position;
        }

        /// <summary>
        /// Find the actual shaft/basement center position in the scene.
        /// PRIORITY: ModularShaftWalls position is the source of truth for where terrain should be.
        /// </summary>
        private Vector3 FindShaftCenterPosition()
        {
            // PRIORITY 1: Find ModularShaftWalls gameobject (the actual walls container)
            // This is the most reliable source - terrain should be INSIDE these walls
            GameObject shaftWalls = GameObject.Find("ModularShaftWalls");
            if (shaftWalls != null)
            {
                Vector3 pos = shaftWalls.transform.position;
                if (enableDebugLogs) Debug.Log($"[DigBoundsProvider] Found ModularShaftWalls at position: {pos} - using this as terrain center");
                return new Vector3(pos.x, 0f, pos.z);
            }

            // Priority 2: Find ModularShaftWallManager component
            var wallManager = FindObjectOfType<ModularShaftWallManager>();
            if (wallManager != null)
            {
                // Check if it has a child called ModularShaftWalls
                Transform wallsChild = wallManager.transform.Find("ModularShaftWalls");
                if (wallsChild != null)
                {
                    Vector3 pos = wallsChild.position;
                    if (enableDebugLogs) Debug.Log($"[DigBoundsProvider] Found ModularShaftWalls child at position: {pos}");
                    return new Vector3(pos.x, 0f, pos.z);
                }

                Vector3 managerPos = wallManager.transform.position;
                if (enableDebugLogs) Debug.Log($"[DigBoundsProvider] Found ModularShaftWallManager at position: {managerPos}");
                return new Vector3(managerPos.x, 0f, managerPos.z);
            }

            // Priority 3: Find UndergroundTerrain gameobject
            GameObject undergroundTerrain = GameObject.Find("UndergroundTerrain");
            if (undergroundTerrain != null)
            {
                Vector3 pos = undergroundTerrain.transform.position;
                if (enableDebugLogs) Debug.Log($"[DigBoundsProvider] Found UndergroundTerrain at position: {pos}");
                return new Vector3(pos.x, 0f, pos.z);
            }

            // Priority 4: Find Basement gameobject
            GameObject basement = GameObject.Find("Basement");
            if (basement != null)
            {
                Vector3 pos = basement.transform.position;
                if (enableDebugLogs) Debug.Log($"[DigBoundsProvider] Found Basement at position: {pos}");
                return new Vector3(pos.x, 0f, pos.z);
            }

            // Fallback: use origin (legacy behavior)
            if (enableDebugLogs) Debug.LogWarning("[DigBoundsProvider] Could not find shaft walls position, using origin (0, 0, 0)");
            return Vector3.zero;
        }

        /// <summary>
        /// Try to calculate bounds from ModularShaftWallManager's actual wall positions.
        /// Uses GetInnerBoundsFromWalls() to get the exact space INSIDE the walls.
        /// </summary>
        private bool TryCalculateBoundsFromWalls()
        {
            // Find ModularShaftWallManager
            var shaftManager = FindObjectOfType<ModularShaftWallManager>();
            if (shaftManager == null)
                return false;

            // Check if walls are built (IsBuilt property)
            if (!shaftManager.IsBuilt)
            {
                if (enableDebugLogs) Debug.LogWarning("[DigBoundsProvider] ModularShaftWallManager found but walls not built yet");
                return false;
            }

            // Get inner bounds calculated from actual wall positions
            Bounds innerBounds = shaftManager.GetInnerBoundsFromWalls();
            if (innerBounds.size.sqrMagnitude < 1f)
            {
                if (enableDebugLogs) Debug.LogWarning("[DigBoundsProvider] GetInnerBoundsFromWalls returned invalid bounds");
                return false;
            }

            boundsSource = BoundsSource.Manual;
            manualBoundsCenter = innerBounds.center;
            manualBoundsSize = innerBounds.size;
            horizontalExpansion = 0f;  // No expansion - bounds are already precise from wall faces
            horizontalInset = 0f;
            additionalDepth = 0f;

            return true;
        }

        private void OnDestroy()
        {
            if (Instance == this)
                Instance = null;
        }

        /// <summary>
        /// Resolve bounds based on configured source.
        /// </summary>
        [ContextMenu("Resolve Bounds")]
        public void ResolveBounds()
        {
            _boundsValid = false;
            _boundsSourceDescription = "None";

            switch (boundsSource)
            {
                case BoundsSource.ExplicitCollider:
                    ResolveFromExplicitCollider();
                    break;

                case BoundsSource.Manual:
                    ResolveFromManual();
                    break;

                case BoundsSource.Auto:
                default:
                    ResolveFromShaftWallManager();
                    break;
            }

            // Apply adjustments
            if (_boundsValid)
            {
                ApplyBoundsAdjustments();
            }

            if (!_boundsValid)
            {
                if (enableDebugLogs) Debug.LogError("[DigBoundsProvider] FAILED to resolve bounds! Digging will be disabled.");
            }
        }

        /// <summary>
        /// Resolve from explicit collider.
        /// </summary>
        private void ResolveFromExplicitCollider()
        {
            if (explicitBoundsCollider == null)
            {
                if (enableDebugLogs) Debug.LogError("[DigBoundsProvider] ExplicitCollider mode but no collider assigned!");
                return;
            }

            _resolvedBounds = explicitBoundsCollider.bounds;
            _boundsValid = true;
            _boundsSourceDescription = $"ExplicitCollider: {explicitBoundsCollider.gameObject.name}";
        }

        /// <summary>
        /// Resolve from manual configuration.
        /// </summary>
        private void ResolveFromManual()
        {
            _resolvedBounds = new Bounds(manualBoundsCenter, manualBoundsSize);
            _boundsValid = true;
            _boundsSourceDescription = "Manual configuration";
        }

        /// <summary>
        /// Auto-resolve from ModularShaftWallManager.
        /// </summary>
        private void ResolveFromShaftWallManager()
        {
            // Find ModularShaftWallManager if not assigned
            if (shaftWallManager == null)
            {
                shaftWallManager = FindObjectOfType<ModularShaftWallManager>();
            }

            if (shaftWallManager == null)
            {
                if (enableDebugLogs) Debug.LogError("[DigBoundsProvider] ModularShaftWallManager not found! Assign reference or use Manual bounds.");
                return;
            }

            // Priority 1: Look for BoxCollider marked as bounds trigger
            BoxCollider boundsCollider = FindBoundsCollider(shaftWallManager.transform);
            if (boundsCollider != null)
            {
                _resolvedBounds = boundsCollider.bounds;
                _boundsValid = true;
                _boundsSourceDescription = $"BoxCollider: {boundsCollider.gameObject.name}";
                return;
            }

            // Priority 2: Combine all child Colliders
            Bounds? colliderBounds = CombineChildColliderBounds(shaftWallManager.transform);
            if (colliderBounds.HasValue)
            {
                _resolvedBounds = colliderBounds.Value;
                _boundsValid = true;
                _boundsSourceDescription = "Combined child Colliders";
                return;
            }

            // Priority 3: Combine all child Renderers
            Bounds? rendererBounds = CombineChildRendererBounds(shaftWallManager.transform);
            if (rendererBounds.HasValue)
            {
                _resolvedBounds = rendererBounds.Value;
                _boundsValid = true;
                _boundsSourceDescription = "Combined child Renderers";
                return;
            }

            // Priority 4: Use inner bounds from actual wall positions
            Bounds innerBounds = shaftWallManager.GetInnerBoundsFromWalls();
            if (innerBounds.size.sqrMagnitude > 0.1f)
            {
                _resolvedBounds = innerBounds;
                _boundsValid = true;
                _boundsSourceDescription = "ModularShaftWallManager.GetInnerBoundsFromWalls()";
                return;
            }

            // FALLBACK: Use manual bounds if configured
            if (useManualAsFallback)
            {
                if (enableDebugLogs) Debug.LogWarning("[DigBoundsProvider] Auto resolution failed - using MANUAL FALLBACK bounds.");
                ResolveFromManual();
                _boundsSourceDescription = "Manual fallback (auto failed)";
                return;
            }

            if (enableDebugLogs) Debug.LogError("[DigBoundsProvider] Could not resolve bounds from ModularShaftWallManager!");
        }

        /// <summary>
        /// Find a BoxCollider intended as inner bounds.
        /// </summary>
        private BoxCollider FindBoundsCollider(Transform root)
        {
            // Check root first
            var rootCollider = root.GetComponent<BoxCollider>();
            if (rootCollider != null && rootCollider.isTrigger)
            {
                if (enableDebugLogs)
                    Debug.Log($"[DigBoundsProvider] Found trigger BoxCollider on root: {root.name}");
                return rootCollider;
            }

            // Check children for "Bounds" or "DigArea" named objects
            string[] boundsNames = { "Bounds", "DigBounds", "DigArea", "InnerBounds", "DiggableArea" };
            foreach (string name in boundsNames)
            {
                Transform boundsObj = root.Find(name);
                if (boundsObj != null)
                {
                    var collider = boundsObj.GetComponent<BoxCollider>();
                    if (collider != null)
                    {
                        if (enableDebugLogs)
                            Debug.Log($"[DigBoundsProvider] Found BoxCollider on child: {boundsObj.name}");
                        return collider;
                    }
                }
            }

            // Check all children for trigger BoxColliders
            var allBoxColliders = root.GetComponentsInChildren<BoxCollider>(true);
            foreach (var bc in allBoxColliders)
            {
                if (bc.isTrigger && bc.gameObject != root.gameObject)
                {
                    if (enableDebugLogs)
                        Debug.Log($"[DigBoundsProvider] Found trigger BoxCollider on: {bc.gameObject.name}");
                    return bc;
                }
            }

            return null;
        }

        /// <summary>
        /// Combine bounds of all child colliders.
        /// </summary>
        private Bounds? CombineChildColliderBounds(Transform root)
        {
            var colliders = root.GetComponentsInChildren<Collider>(true);
            if (colliders.Length == 0)
                return null;

            Bounds combined = colliders[0].bounds;
            for (int i = 1; i < colliders.Length; i++)
            {
                combined.Encapsulate(colliders[i].bounds);
            }

            if (enableDebugLogs)
                Debug.Log($"[DigBoundsProvider] Combined {colliders.Length} collider bounds");

            return combined;
        }

        /// <summary>
        /// Combine bounds of all child renderers.
        /// </summary>
        private Bounds? CombineChildRendererBounds(Transform root)
        {
            var renderers = root.GetComponentsInChildren<Renderer>(true);
            if (renderers.Length == 0)
                return null;

            Bounds combined = renderers[0].bounds;
            for (int i = 1; i < renderers.Length; i++)
            {
                combined.Encapsulate(renderers[i].bounds);
            }

            if (enableDebugLogs)
                Debug.Log($"[DigBoundsProvider] Combined {renderers.Length} renderer bounds");

            return combined;
        }

        /// <summary>
        /// Apply horizontal expansion, inset, and additional depth adjustments.
        /// Also includes additional terrain areas.
        /// </summary>
        private void ApplyBoundsAdjustments()
        {
            Vector3 center = _resolvedBounds.center;
            Vector3 size = _resolvedBounds.size;

            // Apply horizontal SCALE first (1.1 = 110% = 10% larger)
            if (Mathf.Abs(horizontalScale - 1f) > 0.001f)
            {
                size.x *= horizontalScale;
                size.z *= horizontalScale;
            }

            // Apply horizontal EXPANSION (additive, after scale)
            if (horizontalExpansion > 0f)
            {
                size.x += horizontalExpansion * 2f;
                size.z += horizontalExpansion * 2f;
            }

            // Apply horizontal inset (shrink X and Z) - applied AFTER expansion
            if (horizontalInset > 0f)
            {
                size.x = Mathf.Max(1f, size.x - horizontalInset * 2f);
                size.z = Mathf.Max(1f, size.z - horizontalInset * 2f);
            }

            // Apply additional depth (extend Y downward)
            if (additionalDepth > 0f)
            {
                center.y -= additionalDepth / 2f;
                size.y += additionalDepth;
            }

            _resolvedBounds = new Bounds(center, size);
        }

        #region Public API

        /// <summary>
        /// Check if a world position is inside the dig bounds.
        /// </summary>
        public bool IsInsideBounds(Vector3 worldPosition)
        {
            if (!_boundsValid)
                return false;

            return _resolvedBounds.Contains(worldPosition);
        }

        /// <summary>
        /// Check if a chunk AABB is entirely inside the dig bounds.
        /// </summary>
        public bool IsChunkInsideBounds(Bounds chunkBounds)
        {
            if (!_boundsValid)
                return false;

            // Check if chunk is fully contained
            return _resolvedBounds.Contains(chunkBounds.min) && _resolvedBounds.Contains(chunkBounds.max);
        }

        /// <summary>
        /// Check if a chunk AABB intersects the dig bounds (partial overlap allowed).
        /// </summary>
        public bool DoesChunkIntersectBounds(Bounds chunkBounds)
        {
            if (!_boundsValid)
                return false;

            return _resolvedBounds.Intersects(chunkBounds);
        }

        /// <summary>
        /// Log throttled out-of-bounds message.
        /// </summary>
        public void LogOutOfBounds(string context, Vector3 position)
        {
            if (Time.time - _lastOutOfBoundsLogTime < OUT_OF_BOUNDS_LOG_INTERVAL)
                return;

            _lastOutOfBoundsLogTime = Time.time;
            Debug.Log($"[DigBoundsProvider] {context} blocked - position {position} outside bounds");
        }

        /// <summary>
        /// Log throttled chunk creation blocked.
        /// </summary>
        public void LogChunkBlocked(ChunkCoord coord)
        {
            if (Time.time - _lastOutOfBoundsLogTime < OUT_OF_BOUNDS_LOG_INTERVAL)
                return;

            _lastOutOfBoundsLogTime = Time.time;
            Debug.Log($"[DigBoundsProvider] Chunk {coord} creation blocked - outside dig bounds");
        }

        #endregion

        #region Gizmos

        private void OnDrawGizmos()
        {
            if (!drawGizmos)
                return;

            // Draw resolved bounds if valid
            if (_boundsValid || Application.isPlaying)
            {
                DrawBoundsGizmo(_resolvedBounds);
            }
            else
            {
                // In editor, show manual bounds preview
                if (boundsSource == BoundsSource.Manual)
                {
                    DrawBoundsGizmo(new Bounds(manualBoundsCenter, manualBoundsSize));
                }
            }
        }

        private void DrawBoundsGizmo(Bounds bounds)
        {
            // Solid fill
            Gizmos.color = gizmoBoundsColor;
            Gizmos.DrawCube(bounds.center, bounds.size);

            // Wire outline
            Gizmos.color = gizmoWireColor;
            Gizmos.DrawWireCube(bounds.center, bounds.size);

            // Draw corners
            Gizmos.color = Color.yellow;
            float cornerSize = 0.3f;
            Gizmos.DrawSphere(bounds.min, cornerSize);
            Gizmos.DrawSphere(bounds.max, cornerSize);
            Gizmos.DrawSphere(new Vector3(bounds.min.x, bounds.min.y, bounds.max.z), cornerSize);
            Gizmos.DrawSphere(new Vector3(bounds.max.x, bounds.min.y, bounds.min.z), cornerSize);
        }

        #endregion
    }
}
