using System;
using System.Collections.Generic;
using UnityEngine;

namespace BeneathTheFloor.WorldRooms
{
    /// <summary>
    /// Manages the modular shaft wall system.
    /// Builds walls from segments and handles swapping solid segments for openings.
    /// </summary>
    public class ModularShaftWallManager : MonoBehaviour
    {
        [Header("Configuration")]
        [SerializeField] private ModularShaftConfig shaftConfig;

        [Header("References")]
        [Tooltip("Transform at the center of the shaft (top)")]
        [SerializeField] private Transform shaftCenter;

        [Tooltip("Y position of the shaft top (basement floor level)")]
        [SerializeField] private float shaftTopY = -3f;

        [Header("Position Offset")]
        [Tooltip("Offset to apply to all wall positions (X, Y, Z)")]
        [SerializeField] private Vector3 positionOffset = Vector3.zero;

        [Header("Build Settings")]
        [Tooltip("Build walls on Awake")]
        [SerializeField] private bool buildOnAwake = true;

        [Tooltip("Destroy existing shaft walls before building")]
        [SerializeField] private bool destroyExistingWalls = true;

        [Header("Debug")]
        [SerializeField] private bool enableDebugLogs = false;
        [SerializeField] private bool showDebugGizmos = true;
        [SerializeField] private Color solidSegmentColor = new Color(0.5f, 0.4f, 0.3f, 0.3f);
        [SerializeField] private Color openingSegmentColor = new Color(0.2f, 0.8f, 0.2f, 0.3f);

        // Tracking
        private Dictionary<ShaftSide, List<ShaftWallSegment>> _wallSegments;
        private Transform _wallsRoot;
        private bool _isBuilt = false;

        // Cached terrain bounds - walls align to these automatically
        private Bounds _terrainBounds;
        private bool _hasTerrainBounds = false;
        private Digging.UndergroundTerrainManager _terrainManager;

        // Events
        public event Action<ShaftWallSegment> OnSegmentSwapped;

        // Public access
        public bool IsBuilt => _isBuilt;
        public ModularShaftConfig Config => shaftConfig;
        public Bounds TerrainBounds => _terrainBounds;

        /// <summary>
        /// Calculate the exact inner bounds from actual wall positions.
        /// This is the space INSIDE the walls where terrain should exist.
        /// </summary>
        public Bounds GetInnerBoundsFromWalls()
        {
            if (_wallsRoot == null)
            {
                // Walls not built yet - this is expected during early initialization
                // Return terrain bounds as fallback (caller will re-query later)
                return _terrainBounds;
            }

            // Get wall thickness from config
            float wallThickness = shaftConfig?.defaultSegmentConfig?.thickness ?? 0.4f;
            float halfThickness = wallThickness / 2f;

            // Find each wall container
            Transform northWall = _wallsRoot.Find("Wall_North");
            Transform southWall = _wallsRoot.Find("Wall_South");
            Transform eastWall = _wallsRoot.Find("Wall_East");
            Transform westWall = _wallsRoot.Find("Wall_West");

            // Get actual wall positions from FIRST SEGMENT in each container (not the container itself)
            // Wall containers are at (0,0,0) but segments inside them have actual positions
            float northZ = GetFirstSegmentPosition(northWall)?.z ?? _terrainBounds.max.z;
            float southZ = GetFirstSegmentPosition(southWall)?.z ?? _terrainBounds.min.z;
            float eastX = GetFirstSegmentPosition(eastWall)?.x ?? _terrainBounds.max.x;
            float westX = GetFirstSegmentPosition(westWall)?.x ?? _terrainBounds.min.x;

            // Calculate inner face positions
            // Wall CENTER is at segment position, inner face is CENTER +/- halfThickness toward center
            float innerMinX = westX + halfThickness;   // West wall inner face (toward +X)
            float innerMaxX = eastX - halfThickness;   // East wall inner face (toward -X)
            float innerMinZ = southZ + halfThickness;  // South wall inner face (toward +Z)
            float innerMaxZ = northZ - halfThickness;  // North wall inner face (toward -Z)

            // Use terrain bounds for Y (vertical extent) - if not set, calculate from segments
            float minY, maxY;
            if (_terrainBounds.size.y > 0.1f)
            {
                minY = _terrainBounds.min.y;
                maxY = _terrainBounds.max.y;
            }
            else
            {
                // Calculate Y from wall segments
                maxY = shaftTopY;
                minY = shaftTopY - (shaftConfig?.maxDepth ?? 220f);
            }

            Vector3 min = new Vector3(innerMinX, minY, innerMinZ);
            Vector3 max = new Vector3(innerMaxX, maxY, innerMaxZ);
            Vector3 center = (min + max) * 0.5f;
            Vector3 size = max - min;

            return new Bounds(center, size);
        }

        /// <summary>
        /// Get the world position of the first segment in a wall container.
        /// </summary>
        private Vector3? GetFirstSegmentPosition(Transform wallContainer)
        {
            if (wallContainer == null || wallContainer.childCount == 0)
                return null;

            // Get first child (first segment)
            Transform firstSegment = wallContainer.GetChild(0);
            return firstSegment.position;
        }

        private void Awake()
        {
            if (enableDebugLogs)
                Debug.Log($"[ModularShaftWallManager] Awake - buildOnAwake={buildOnAwake}, shaftConfig={(shaftConfig != null ? shaftConfig.name : "NULL")}");

            // Try to find config if not assigned
            if (shaftConfig == null)
            {
                if (enableDebugLogs)
                    Debug.LogWarning("[ModularShaftWallManager] shaftConfig not assigned, attempting to find...");
                shaftConfig = Resources.Load<ModularShaftConfig>("Shaft/ModularShaftConfig");

                if (shaftConfig == null)
                {
                    // Try to find in project
                    #if UNITY_EDITOR
                    string[] guids = UnityEditor.AssetDatabase.FindAssets("ModularShaftConfig t:ModularShaftConfig");
                    if (guids.Length > 0)
                    {
                        string path = UnityEditor.AssetDatabase.GUIDToAssetPath(guids[0]);
                        shaftConfig = UnityEditor.AssetDatabase.LoadAssetAtPath<ModularShaftConfig>(path);
                        if (enableDebugLogs)
                            Debug.Log($"[ModularShaftWallManager] Found config via AssetDatabase: {path}");
                    }
                    #endif
                }
            }

            // Note: BuildShaftWalls moved to Start() to ensure UndergroundTerrainManager.Instance is available
            if (shaftConfig == null)
            {
                Debug.LogError("[ModularShaftWallManager] Cannot build walls - shaftConfig is null! Please assign ModularShaftConfig in Inspector.");
            }
        }

        private void Start()
        {
            // Build walls in Start() to ensure all other managers (especially UndergroundTerrainManager)
            // have initialized their Instance properties in Awake()

            // Auto-load wall material if not assigned
            if (shaftConfig != null && shaftConfig.wallMaterial == null)
            {
                #if UNITY_EDITOR
                // Try M_ShaftWall first (preferred), then TriplanarSoil as fallback
                shaftConfig.wallMaterial = UnityEditor.AssetDatabase.LoadAssetAtPath<Material>("Assets/Materials/M_ShaftWall.mat");
                if (shaftConfig.wallMaterial != null && enableDebugLogs)
                    Debug.Log("[ModularShaftWallManager] Auto-loaded M_ShaftWall material for walls");
                else if (shaftConfig.wallMaterial == null)
                {
                    shaftConfig.wallMaterial = UnityEditor.AssetDatabase.LoadAssetAtPath<Material>("Assets/Materials/TriplanarSoil.mat");
                    if (shaftConfig.wallMaterial != null && enableDebugLogs)
                        Debug.Log("[ModularShaftWallManager] Auto-loaded TriplanarSoil material (fallback)");
                }
                #endif
            }

            if (buildOnAwake && shaftConfig != null && !_isBuilt)
            {
                BuildShaftWalls();
            }
            else if (!buildOnAwake && !_isBuilt)
            {
                // If not building on awake, try to register existing walls
                RegisterExistingWalls();
            }
        }

        /// <summary>
        /// Registers existing wall segments without rebuilding them.
        /// Call this if walls were placed manually in Edit mode.
        /// </summary>
        [ContextMenu("Register Existing Walls")]
        public void RegisterExistingWalls()
        {
            if (_isBuilt)
            {
                if (enableDebugLogs)
                    Debug.Log("[ModularShaftWallManager] Walls already registered.");
                return;
            }

            // Initialize tracking dictionary
            _wallSegments = new Dictionary<ShaftSide, List<ShaftWallSegment>>();
            foreach (ShaftSide side in System.Enum.GetValues(typeof(ShaftSide)))
            {
                _wallSegments[side] = new List<ShaftWallSegment>();
            }

            // Find walls root - check multiple possible locations
            _wallsRoot = transform.Find("ShaftWalls") ?? transform.Find("ModularShaftWalls") ?? transform;

            // Get shaft top Y for depth calculation
            float topY = shaftTopY;
            var terrainManager = Digging.UndergroundTerrainManager.Instance;
            if (terrainManager != null)
            {
                topY = terrainManager.basementFloorY;
            }

            int registeredCount = 0;

            if (enableDebugLogs)
                Debug.Log($"[ModularShaftWallManager] Scanning for existing walls under '{_wallsRoot.name}' (shaftTopY={topY:F2})...");

            // Scan all children recursively for wall segments
            RegisterWallsRecursive(_wallsRoot, topY, ref registeredCount);

            // Sort segments by depth
            foreach (var side in _wallSegments.Keys)
            {
                _wallSegments[side].Sort((a, b) => a.depthFromTop.CompareTo(b.depthFromTop));
            }

            // Apply material to all registered walls
            if (shaftConfig != null && shaftConfig.wallMaterial != null)
            {
                ApplyMaterialToAllWalls();
            }

            if (registeredCount > 0)
            {
                _isBuilt = true;
                if (enableDebugLogs)
                {
                    Debug.Log($"[ModularShaftWallManager] Registered {registeredCount} existing wall segments:");
                    foreach (var kvp in _wallSegments)
                    {
                        if (kvp.Value.Count > 0)
                        {
                            Debug.Log($"  {kvp.Key}: {kvp.Value.Count} segments");
                            foreach (var seg in kvp.Value)
                            {
                                Debug.Log($"    - depth={seg.depthFromTop:F1}m, height={seg.height:F1}m, hasOpening={seg.hasOpening}, pos={seg.worldPosition}");
                            }
                        }
                    }
                }
            }
            else
            {
                if (enableDebugLogs)
                    Debug.LogWarning("[ModularShaftWallManager] No existing wall segments found to register. Check wall naming (must contain North/South/East/West).");
            }
        }

        private void RegisterWallsRecursive(Transform parent, float topY, ref int registeredCount)
        {
            foreach (Transform child in parent)
            {
                // Try to determine side from name
                ShaftSide? side = ParseSideFromName(child.name);

                // If this is a container (Wall_North, Wall_South, etc.), scan its children
                if (side != null && child.childCount > 0 && !child.name.Contains("Segment"))
                {
                    RegisterWallsRecursive(child, topY, ref registeredCount);
                    continue;
                }

                if (side == null) continue;

                // Get segment height from renderer bounds
                float height = 6f; // Default
                var renderer = child.GetComponentInChildren<Renderer>();
                if (renderer != null)
                {
                    height = renderer.bounds.size.y;
                }

                // Calculate depth from Y position
                // Wall center Y = shaftTopY - depthFromTop - height/2
                // So: depthFromTop = shaftTopY - wallCenterY - height/2
                float wallCenterY = child.position.y;
                float depth = topY - wallCenterY - height / 2f;

                // Clamp to valid range
                depth = Mathf.Max(0f, depth);

                // Check if it's an opening
                bool hasOpening = child.name.ToLower().Contains("opening") ||
                                  child.name.ToLower().Contains("open") ||
                                  child.name.Contains("_O_");

                // Create segment info
                var segment = new ShaftWallSegment
                {
                    side = side.Value,
                    depthFromTop = depth,
                    height = height,
                    hasOpening = hasOpening,
                    worldPosition = child.position,
                    instance = child.gameObject,
                    config = shaftConfig?.defaultSegmentConfig
                };

                _wallSegments[side.Value].Add(segment);
                registeredCount++;

                if (enableDebugLogs)
                    Debug.Log($"[ModularShaftWallManager] Registered: '{child.name}' as {side.Value} at depth {depth:F1}m (Y={wallCenterY:F2}, height={height:F1})");
            }
        }

        private ShaftSide? ParseSideFromName(string name)
        {
            string upperName = name.ToUpper();
            if (upperName.Contains("NORTH")) return ShaftSide.North;
            if (upperName.Contains("SOUTH")) return ShaftSide.South;
            if (upperName.Contains("EAST")) return ShaftSide.East;
            if (upperName.Contains("WEST")) return ShaftSide.West;
            return null;
        }

        private float ParseDepthFromName(string name)
        {
            // Try to extract depth number from name like "Segment_West_15_Solid"
            var parts = name.Split('_');
            foreach (var part in parts)
            {
                if (float.TryParse(part, out float depth))
                {
                    return depth;
                }
            }
            return 0f;
        }

        /// <summary>
        /// Builds all shaft walls from modular segments.
        /// </summary>
        [ContextMenu("Build Shaft Walls")]
        public void BuildShaftWalls()
        {
            if (shaftConfig == null)
            {
                Debug.LogError("[ModularShaftWallManager] shaftConfig is not assigned!");
                return;
            }

            if (destroyExistingWalls)
            {
                DestroyExistingWalls();
            }

            // Try to get actual dig area dimensions from terrain manager
            UpdateShaftDimensionsFromTerrain();

            // Initialize tracking
            _wallSegments = new Dictionary<ShaftSide, List<ShaftWallSegment>>();
            foreach (ShaftSide side in Enum.GetValues(typeof(ShaftSide)))
            {
                _wallSegments[side] = new List<ShaftWallSegment>();
            }

            // Create walls root
            CreateWallsRoot();

            // Get shaft center position
            Vector3 centerPos = shaftCenter != null ? shaftCenter.position : transform.position;
            centerPos.y = shaftTopY;

            // Apply position offset
            centerPos += positionOffset;

            // Build each side
            foreach (ShaftSide side in Enum.GetValues(typeof(ShaftSide)))
            {
                if (shaftConfig.IsSideEnabled(side))
                {
                    BuildSideWall(side, centerPos);
                }
            }

            _isBuilt = true;
            if (enableDebugLogs)
                Debug.Log($"[ModularShaftWallManager] Built shaft walls with {GetTotalSegmentCount()} segments");
        }

        /// <summary>
        /// Updates shaft dimensions from UndergroundTerrainManager if available.
        /// Gets the actual terrain bounds for automatic wall alignment.
        /// </summary>
        private void UpdateShaftDimensionsFromTerrain()
        {
            // Try to find UndergroundTerrainManager
            _terrainManager = Digging.UndergroundTerrainManager.Instance;
            if (_terrainManager == null)
            {
                _terrainManager = FindObjectOfType<Digging.UndergroundTerrainManager>();
            }

            // Find the actual shaft/basement center position
            Vector3 shaftCenterXZ = FindShaftCenterPosition();

            if (_terrainManager != null)
            {
                // Get the actual terrain bounds - this is the source of truth
                float halfExtent = _terrainManager.horizontalExtent / 2f;

                // Calculate terrain bounds centered at ACTUAL shaft position
                // NOT at origin - the basement may be anywhere in the scene
                Vector3 terrainCenter = new Vector3(shaftCenterXZ.x, _terrainManager.basementFloorY, shaftCenterXZ.z);
                Vector3 terrainSize = new Vector3(
                    _terrainManager.horizontalExtent,
                    _terrainManager.maxDepthMeters,
                    _terrainManager.horizontalExtent
                );
                _terrainBounds = new Bounds(terrainCenter, terrainSize);
                _hasTerrainBounds = true;

                // FORCE update config values - terrain is the source of truth
                shaftConfig.shaftHalfWidth = halfExtent;
                shaftConfig.shaftHalfLength = halfExtent;
                shaftConfig.maxDepth = _terrainManager.maxDepthMeters + 5f;

                // Also update segment configs if they exist
                if (shaftConfig.defaultSegmentConfig != null)
                {
                    shaftConfig.defaultSegmentConfig.width = _terrainManager.horizontalExtent;
                }
                foreach (var segConfig in shaftConfig.segmentConfigs)
                {
                    if (segConfig != null)
                    {
                        segConfig.width = _terrainManager.horizontalExtent;
                    }
                }

                // Sync shaftTopY with basement floor
                shaftTopY = _terrainManager.basementFloorY;

                if (enableDebugLogs)
                {
                    Debug.Log($"[ModularShaftWallManager] === TERRAIN SYNC ===");
                    Debug.Log($"  horizontalExtent={_terrainManager.horizontalExtent}, halfExtent={halfExtent}");
                    Debug.Log($"  Terrain bounds: center={_terrainBounds.center}, size={_terrainBounds.size}");
                    Debug.Log($"  Terrain edges: X=[{_terrainBounds.min.x:F2} to {_terrainBounds.max.x:F2}], Z=[{_terrainBounds.min.z:F2} to {_terrainBounds.max.z:F2}]");
                    Debug.Log($"  shaftTopY={shaftTopY}, maxDepth={shaftConfig.maxDepth}m");
                    Debug.Log($"  Wall positions will be at: North Z={_terrainBounds.max.z + 0.2f:F2}, South Z={_terrainBounds.min.z - 0.2f:F2}");
                }
            }
            else
            {
                if (enableDebugLogs)
                    Debug.LogWarning("[ModularShaftWallManager] UndergroundTerrainManager not found. Using fallback values (12x12 terrain).");
                _hasTerrainBounds = false;

                // Use hardcoded fallback for 12x12 terrain at actual shaft position
                float fallbackHalfExtent = 6f;
                shaftConfig.shaftHalfWidth = fallbackHalfExtent;
                shaftConfig.shaftHalfLength = fallbackHalfExtent;

                _terrainBounds = new Bounds(
                    new Vector3(shaftCenterXZ.x, shaftTopY, shaftCenterXZ.z),
                    new Vector3(12f, 220f, 12f)
                );
                _hasTerrainBounds = true;

                if (enableDebugLogs)
                    Debug.Log($"[ModularShaftWallManager] Using fallback 12x12 terrain bounds at ({shaftCenterXZ.x}, {shaftTopY}, {shaftCenterXZ.z})");
            }
        }

        private void CreateWallsRoot()
        {
            if (_wallsRoot != null)
            {
                DestroyImmediate(_wallsRoot.gameObject);
            }

            var rootGO = new GameObject("ModularShaftWalls");
            rootGO.transform.SetParent(transform);
            rootGO.transform.localPosition = Vector3.zero;
            _wallsRoot = rootGO.transform;
        }

        private void DestroyExistingWalls()
        {
            // Destroy our walls
            if (_wallsRoot != null)
            {
                DestroyImmediate(_wallsRoot.gameObject);
            }

            // Also try to find and disable old shaft wall generators
            var oldGenerators = FindObjectsOfType<MonoBehaviour>();
            foreach (var mb in oldGenerators)
            {
                if (mb.GetType().Name == "RectShaftGenerator" ||
                    mb.GetType().Name == "DigShaftWallGenerator")
                {
                    mb.enabled = false;
                    if (enableDebugLogs)
                        Debug.Log($"[ModularShaftWallManager] Disabled old wall generator: {mb.GetType().Name}");
                }
            }
        }

        private void BuildSideWall(ShaftSide side, Vector3 shaftCenterTop)
        {
            var sideConfig = shaftConfig.GetSideConfig(side);
            var segmentConfig = sideConfig.segmentOverride ?? shaftConfig.defaultSegmentConfig;

            if (enableDebugLogs)
                Debug.Log($"[ModularShaftWallManager] BuildSideWall {side}: segmentConfig={(segmentConfig != null ? segmentConfig.name : "NULL")}, solidPrefab={(segmentConfig?.solidPrefab != null ? segmentConfig.solidPrefab.name : "NULL")}");

            if (segmentConfig == null && enableDebugLogs)
            {
                Debug.LogWarning($"[ModularShaftWallManager] No segment config for side {side}, using primitive fallback");
            }

            // Even without prefabs, we'll create primitive segments as fallback

            // Create side container
            var sideContainer = new GameObject($"Wall_{side}");
            sideContainer.transform.SetParent(_wallsRoot);
            sideContainer.transform.localPosition = Vector3.zero;

            // Calculate wall position from terrain bounds (automatic alignment)
            // Wall INNER edge should be exactly at terrain boundary
            // Wall CENTER is at terrain boundary + half thickness (so inner edge touches boundary)
            float wallThickness = segmentConfig?.thickness ?? 0.4f;

            Vector3 wallBasePosition;
            if (_hasTerrainBounds)
            {
                // Use actual terrain bounds for precise alignment
                wallBasePosition = CalculateWallPositionFromTerrainBounds(side, wallThickness);
                wallBasePosition.y = shaftCenterTop.y;
            }
            else
            {
                // Fallback to config values
                Vector3 sideDirection = ModularShaftConfig.GetSideDirection(side);
                float terrainHalfExtent = (side == ShaftSide.North || side == ShaftSide.South)
                    ? shaftConfig.shaftHalfLength
                    : shaftConfig.shaftHalfWidth;
                float distanceFromCenter = terrainHalfExtent + (wallThickness / 2f);
                wallBasePosition = shaftCenterTop + sideDirection * distanceFromCenter;
            }

            // Build segments from top to bottom
            float currentDepth = 0f;
            int segmentIndex = 0;

            // Pre-calculate which depths need openings
            HashSet<float> openingDepths = new HashSet<float>(sideConfig.preplacedOpeningDepths);

            while (currentDepth < shaftConfig.maxDepth)
            {
                // Determine segment height (could vary based on depth or config)
                float segmentHeight = segmentConfig?.height ?? 6f; // Default 6m if no config

                // Don't exceed max depth
                if (currentDepth + segmentHeight > shaftConfig.maxDepth)
                {
                    segmentHeight = shaftConfig.maxDepth - currentDepth;
                }

                // Check if this segment should have an opening
                bool hasOpening = false;
                float segmentCenterDepth = currentDepth + segmentHeight / 2f;
                foreach (float openingDepth in openingDepths)
                {
                    if (openingDepth >= currentDepth && openingDepth < currentDepth + segmentHeight)
                    {
                        hasOpening = true;
                        break;
                    }
                }

                // Create segment
                var segment = CreateSegment(
                    side,
                    segmentConfig,
                    wallBasePosition,
                    currentDepth,
                    segmentHeight,
                    hasOpening,
                    segmentIndex,
                    sideContainer.transform
                );

                _wallSegments[side].Add(segment);

                currentDepth += segmentHeight;
                segmentIndex++;
            }

            if (enableDebugLogs)
                Debug.Log($"[ModularShaftWallManager] Built {segmentIndex} segments for {side} wall");
        }

        private ShaftWallSegment CreateSegment(
            ShaftSide side,
            ShaftWallSegmentConfig config,
            Vector3 wallBasePosition,
            float depthFromTop,
            float height,
            bool hasOpening,
            int index,
            Transform parent)
        {
            // Calculate segment center position
            float segmentCenterY = shaftTopY - depthFromTop - height / 2f;
            Vector3 segmentPosition = new Vector3(wallBasePosition.x, segmentCenterY, wallBasePosition.z);

            // Get rotation for this side
            Quaternion rotation = ModularShaftConfig.GetSideRotation(side);

            // Get target width from terrain (12m for 12x12 terrain)
            float targetWidth = (_hasTerrainBounds && _terrainManager != null)
                ? _terrainManager.horizontalExtent
                : 12f;

            // Select prefab (with null safety) - but ONLY for solid segments
            // Opening segments must be created from primitives to maintain correct opening size (3.5m)
            GameObject prefab = hasOpening ? null : config?.GetPrefab(false);

            // Instantiate
            GameObject segmentGO;
            if (prefab != null && !hasOpening)
            {
                segmentGO = Instantiate(prefab, segmentPosition, rotation, parent);

                // Scale prefab to match terrain width
                float prefabWidth = config?.width ?? 6f;
                if (Mathf.Abs(prefabWidth - targetWidth) > 0.1f)
                {
                    float scaleFactorX = targetWidth / prefabWidth;
                    Vector3 scale = segmentGO.transform.localScale;
                    scale.x *= scaleFactorX;
                    segmentGO.transform.localScale = scale;
                }
            }
            else
            {
                // Create primitive segment - this correctly handles opening size
                segmentGO = CreatePrimitiveSegment(config, hasOpening, height);
                segmentGO.transform.SetParent(parent);
                segmentGO.transform.position = segmentPosition;
                segmentGO.transform.rotation = rotation;
            }

            segmentGO.name = $"Segment_{side}_{index}_{(hasOpening ? "Open" : "Solid")}";

            // Apply material if set
            if (shaftConfig.wallMaterial != null)
            {
                var renderers = segmentGO.GetComponentsInChildren<Renderer>();
                foreach (var renderer in renderers)
                {
                    renderer.sharedMaterial = shaftConfig.wallMaterial;
                }
            }

            // Create tracking data
            var segment = new ShaftWallSegment
            {
                side = side,
                config = config,
                depthFromTop = depthFromTop,
                height = height,
                hasOpening = hasOpening,
                worldPosition = segmentPosition,
                instance = segmentGO,
                index = index
            };

            return segment;
        }

        private GameObject CreatePrimitiveSegment(ShaftWallSegmentConfig config, bool hasOpening, float overrideHeight = 0f)
        {
            // Get width from terrain if available (automatic alignment)
            float width;
            if (_hasTerrainBounds && _terrainManager != null)
            {
                width = _terrainManager.horizontalExtent; // Use actual terrain size
            }
            else
            {
                width = config?.width ?? 12f; // Fallback to config
            }

            float height = overrideHeight > 0 ? overrideHeight : (config?.height ?? 6f);
            float thickness = config?.thickness ?? 0.4f;

            if (hasOpening)
            {
                // Create a frame (4 cubes around the opening)
                var root = new GameObject("PrimitiveOpeningSegment");

                // Opening dimensions must match room entrance (RoomPrefabGenerator: 4.5m x 4m)
                float openingWidth = 4.5f;
                float openingHeight = Mathf.Min(4f, height * 0.9f);
                float sideWidth = (width - openingWidth) / 2f;
                float topBottomHeight = (height - openingHeight) / 2f;

                // Left piece
                var left = GameObject.CreatePrimitive(PrimitiveType.Cube);
                left.name = "Left";
                left.transform.SetParent(root.transform);
                left.transform.localPosition = new Vector3(-(openingWidth + sideWidth) / 2f, 0, 0);
                left.transform.localScale = new Vector3(sideWidth, height, thickness);

                // Right piece
                var right = GameObject.CreatePrimitive(PrimitiveType.Cube);
                right.name = "Right";
                right.transform.SetParent(root.transform);
                right.transform.localPosition = new Vector3((openingWidth + sideWidth) / 2f, 0, 0);
                right.transform.localScale = new Vector3(sideWidth, height, thickness);

                // Top piece
                var top = GameObject.CreatePrimitive(PrimitiveType.Cube);
                top.name = "Top";
                top.transform.SetParent(root.transform);
                top.transform.localPosition = new Vector3(0, (openingHeight + topBottomHeight) / 2f, 0);
                top.transform.localScale = new Vector3(openingWidth, topBottomHeight, thickness);

                // Bottom piece
                var bottom = GameObject.CreatePrimitive(PrimitiveType.Cube);
                bottom.name = "Bottom";
                bottom.transform.SetParent(root.transform);
                bottom.transform.localPosition = new Vector3(0, -(openingHeight + topBottomHeight) / 2f, 0);
                bottom.transform.localScale = new Vector3(openingWidth, topBottomHeight, thickness);

                return root;
            }
            else
            {
                // Create a simple solid cube
                var cube = GameObject.CreatePrimitive(PrimitiveType.Cube);
                cube.transform.localScale = new Vector3(width, height, thickness);
                return cube;
            }
        }

        /// <summary>
        /// Creates an opening in the shaft wall at the specified depth and side.
        /// Returns the segment that was modified.
        /// </summary>
        public ShaftWallSegment CreateOpeningAtDepth(float depth, ShaftSide side)
        {
            if (!_isBuilt)
            {
                Debug.LogWarning("[ModularShaftWallManager] Walls not built yet!");
                return null;
            }

            var segment = GetSegmentAtDepth(depth, side);
            if (segment == null)
            {
                if (enableDebugLogs)
                    Debug.LogWarning($"[ModularShaftWallManager] No segment found at depth {depth} on {side}");
                return null;
            }

            if (segment.hasOpening)
            {
                if (enableDebugLogs)
                    Debug.Log($"[ModularShaftWallManager] Segment at depth {depth} already has opening");
                return segment;
            }

            // Swap to opening version
            SwapSegmentToOpening(segment);
            return segment;
        }

        /// <summary>
        /// Gets the segment at a specific depth on a specific side.
        /// </summary>
        public ShaftWallSegment GetSegmentAtDepth(float depth, ShaftSide side)
        {
            if (!_wallSegments.ContainsKey(side))
                return null;

            foreach (var segment in _wallSegments[side])
            {
                if (depth >= segment.depthFromTop && depth < segment.depthFromTop + segment.height)
                {
                    return segment;
                }
            }

            return null;
        }

        /// <summary>
        /// Gets the nearest segment to a depth on any enabled side.
        /// </summary>
        public (ShaftWallSegment segment, ShaftSide side) GetNearestSegment(float depth)
        {
            ShaftWallSegment nearest = null;
            ShaftSide nearestSide = ShaftSide.North;
            float minDist = float.MaxValue;

            foreach (ShaftSide side in Enum.GetValues(typeof(ShaftSide)))
            {
                if (!shaftConfig.IsSideEnabled(side)) continue;

                var segment = GetSegmentAtDepth(depth, side);
                if (segment != null)
                {
                    float segmentCenter = segment.depthFromTop + segment.height / 2f;
                    float dist = Mathf.Abs(segmentCenter - depth);
                    if (dist < minDist)
                    {
                        minDist = dist;
                        nearest = segment;
                        nearestSide = side;
                    }
                }
            }

            return (nearest, nearestSide);
        }

        /// <summary>
        /// Swaps a solid segment for its opening version.
        /// </summary>
        public void SwapSegmentToOpening(ShaftWallSegment segment)
        {
            if (segment == null || segment.hasOpening)
                return;

            if (segment.config.openingPrefab == null)
            {
                if (enableDebugLogs)
                    Debug.LogWarning($"[ModularShaftWallManager] No opening prefab for segment config {segment.config.name}");
                return;
            }

            // Store old position and parent
            Vector3 position = segment.instance.transform.position;
            Quaternion rotation = segment.instance.transform.rotation;
            Transform parent = segment.instance.transform.parent;

            // Destroy old segment
            DestroyImmediate(segment.instance);

            // Create new segment with opening
            GameObject newSegment = Instantiate(segment.config.openingPrefab, position, rotation, parent);
            newSegment.name = $"Segment_{segment.side}_{segment.index}_Open";

            // Scale to match terrain width (same logic as CreateSegment)
            float targetWidth = (_hasTerrainBounds && _terrainManager != null)
                ? _terrainManager.horizontalExtent
                : 12f;
            float prefabWidth = segment.config?.width ?? 6f;
            if (Mathf.Abs(prefabWidth - targetWidth) > 0.1f)
            {
                float scaleFactorX = targetWidth / prefabWidth;
                Vector3 scale = newSegment.transform.localScale;
                scale.x *= scaleFactorX;
                newSegment.transform.localScale = scale;
            }

            // Apply material
            if (shaftConfig.wallMaterial != null)
            {
                var renderers = newSegment.GetComponentsInChildren<Renderer>();
                foreach (var renderer in renderers)
                {
                    renderer.sharedMaterial = shaftConfig.wallMaterial;
                }
            }

            // Update tracking
            segment.instance = newSegment;
            segment.hasOpening = true;

            if (enableDebugLogs)
                Debug.Log($"[ModularShaftWallManager] Swapped segment to opening at depth {segment.depthFromTop}m on {segment.side}");

            OnSegmentSwapped?.Invoke(segment);
        }

        /// <summary>
        /// Gets the world position for a room connection at the specified segment.
        /// </summary>
        public Vector3 GetRoomConnectionPoint(ShaftWallSegment segment)
        {
            if (segment == null)
                return Vector3.zero;

            Vector3 direction = ModularShaftConfig.GetSideDirection(segment.side);
            float offset = (segment.side == ShaftSide.North || segment.side == ShaftSide.South)
                ? shaftConfig.shaftHalfLength
                : shaftConfig.shaftHalfWidth;

            // Position just outside the wall
            return segment.worldPosition + direction * (shaftConfig.defaultSegmentConfig.thickness / 2f + 0.5f);
        }

        private int GetTotalSegmentCount()
        {
            int count = 0;
            foreach (var list in _wallSegments.Values)
            {
                count += list.Count;
            }
            return count;
        }

        /// <summary>
        /// Applies the wall material to all wall segments (existing and generated).
        /// </summary>
        private void ApplyMaterialToAllWalls()
        {
            if (shaftConfig == null || shaftConfig.wallMaterial == null)
            {
                if (enableDebugLogs)
                    Debug.LogWarning("[ModularShaftWallManager] Cannot apply material - wallMaterial is null");
                return;
            }

            if (enableDebugLogs)
                Debug.Log($"[ModularShaftWallManager] Applying material '{shaftConfig.wallMaterial.name}' to walls...");

            int appliedCount = 0;

            // Apply to tracked segments
            if (_wallSegments != null)
            {
                foreach (var kvp in _wallSegments)
                {
                    foreach (var segment in kvp.Value)
                    {
                        if (segment.instance != null)
                        {
                            var renderers = segment.instance.GetComponentsInChildren<Renderer>();
                            foreach (var renderer in renderers)
                            {
                                renderer.sharedMaterial = shaftConfig.wallMaterial;
                                appliedCount++;
                            }
                        }
                    }
                }
            }

            // Also apply to walls root directly in case walls aren't tracked yet
            if (_wallsRoot != null)
            {
                if (enableDebugLogs)
                    Debug.Log($"[ModularShaftWallManager] Found _wallsRoot: {_wallsRoot.name}");
                var allRenderers = _wallsRoot.GetComponentsInChildren<Renderer>();
                if (enableDebugLogs)
                    Debug.Log($"[ModularShaftWallManager] Found {allRenderers.Length} renderers under walls root");
                foreach (var renderer in allRenderers)
                {
                    renderer.sharedMaterial = shaftConfig.wallMaterial;
                    appliedCount++;
                }
            }
            else
            {
                if (enableDebugLogs)
                    Debug.LogWarning("[ModularShaftWallManager] _wallsRoot is null, trying to find ModularShaftWalls...");
                // Try to find walls container by name
                var wallsContainer = transform.Find("ModularShaftWalls");
                if (wallsContainer != null)
                {
                    _wallsRoot = wallsContainer;
                    var allRenderers = wallsContainer.GetComponentsInChildren<Renderer>();
                    if (enableDebugLogs)
                        Debug.Log($"[ModularShaftWallManager] Found ModularShaftWalls with {allRenderers.Length} renderers");
                    foreach (var renderer in allRenderers)
                    {
                        renderer.sharedMaterial = shaftConfig.wallMaterial;
                        appliedCount++;
                    }
                }
            }

            if (enableDebugLogs)
                Debug.Log($"[ModularShaftWallManager] Applied wall material to {appliedCount} renderers");
        }

        /// <summary>
        /// Public method to force material application (can be called from Inspector).
        /// </summary>
        [ContextMenu("Apply Wall Material")]
        public void ForceApplyWallMaterial()
        {
            // Try to load material if not set
            if (shaftConfig != null && shaftConfig.wallMaterial == null)
            {
                #if UNITY_EDITOR
                shaftConfig.wallMaterial = UnityEditor.AssetDatabase.LoadAssetAtPath<Material>("Assets/Materials/M_ShaftWall.mat");
                if (shaftConfig.wallMaterial == null)
                    shaftConfig.wallMaterial = UnityEditor.AssetDatabase.LoadAssetAtPath<Material>("Assets/Materials/TriplanarSoil.mat");
                #endif
            }

            // Find walls root if not set
            if (_wallsRoot == null)
            {
                _wallsRoot = transform.Find("ShaftWalls") ?? transform.Find("ModularShaftWalls") ?? transform;
            }

            ApplyMaterialToAllWalls();
        }

        /// <summary>
        /// Calculates wall position directly from terrain bounds.
        /// Walls are placed at the terrain boundary edges.
        /// </summary>
        private Vector3 CalculateWallPositionFromTerrainBounds(ShaftSide side, float wallThickness)
        {
            // Terrain bounds: min.x to max.x on X, min.z to max.z on Z
            // Wall CENTER is placed at terrain edge, so wall straddles the boundary

            float halfThickness = wallThickness / 2f;
            Vector3 position = Vector3.zero;

            switch (side)
            {
                case ShaftSide.North:
                    // North wall at +Z edge
                    position.x = _terrainBounds.center.x;
                    position.z = _terrainBounds.max.z + halfThickness;
                    break;

                case ShaftSide.South:
                    // South wall at -Z edge
                    position.x = _terrainBounds.center.x;
                    position.z = _terrainBounds.min.z - halfThickness;
                    break;

                case ShaftSide.East:
                    // East wall at +X edge
                    position.x = _terrainBounds.max.x + halfThickness;
                    position.z = _terrainBounds.center.z;
                    break;

                case ShaftSide.West:
                    // West wall at -X edge
                    position.x = _terrainBounds.min.x - halfThickness;
                    position.z = _terrainBounds.center.z;
                    break;
            }

            if (enableDebugLogs)
                Debug.Log($"[ModularShaftWallManager] Wall {side} position: {position} (terrain bounds: {_terrainBounds.min} to {_terrainBounds.max})");

            return position;
        }

        /// <summary>
        /// Find the actual shaft/basement center position in the scene.
        /// Calculates from actual wall segment positions if available.
        /// </summary>
        private Vector3 FindShaftCenterPosition()
        {
            // Priority 1: Use shaftCenter if assigned
            if (shaftCenter != null)
            {
                return new Vector3(shaftCenter.position.x, 0f, shaftCenter.position.z);
            }

            // Priority 2: Calculate center from existing wall segments
            if (_wallsRoot != null)
            {
                Vector3? center = CalculateCenterFromWallSegments();
                if (center.HasValue)
                {
                    if (enableDebugLogs)
                        Debug.Log($"[ModularShaftWallManager] Calculated center from wall segments: {center.Value}");
                    return center.Value;
                }
            }

            // Priority 3: Find existing ModularShaftWalls and calculate center from its children
            GameObject existingWalls = GameObject.Find("ModularShaftWalls");
            if (existingWalls != null)
            {
                var renderers = existingWalls.GetComponentsInChildren<Renderer>();
                if (renderers.Length > 0)
                {
                    Bounds combined = renderers[0].bounds;
                    for (int i = 1; i < renderers.Length; i++)
                    {
                        combined.Encapsulate(renderers[i].bounds);
                    }
                    if (enableDebugLogs)
                        Debug.Log($"[ModularShaftWallManager] Calculated center from existing walls: ({combined.center.x}, {combined.center.z})");
                    return new Vector3(combined.center.x, 0f, combined.center.z);
                }
            }

            // Priority 4: Use this object's position if it's not at origin
            if (transform.position.sqrMagnitude > 0.1f)
            {
                return new Vector3(transform.position.x, 0f, transform.position.z);
            }

            // Fallback: use origin (legacy behavior)
            if (enableDebugLogs)
                Debug.LogWarning("[ModularShaftWallManager] Could not find shaft center, using origin (0, 0, 0)");
            return Vector3.zero;
        }

        /// <summary>
        /// Calculate the center position from actual wall segment positions.
        /// </summary>
        private Vector3? CalculateCenterFromWallSegments()
        {
            if (_wallsRoot == null)
                return null;

            Transform northWall = _wallsRoot.Find("Wall_North");
            Transform southWall = _wallsRoot.Find("Wall_South");
            Transform eastWall = _wallsRoot.Find("Wall_East");
            Transform westWall = _wallsRoot.Find("Wall_West");

            float? northZ = GetFirstSegmentPosition(northWall)?.z;
            float? southZ = GetFirstSegmentPosition(southWall)?.z;
            float? eastX = GetFirstSegmentPosition(eastWall)?.x;
            float? westX = GetFirstSegmentPosition(westWall)?.x;

            if (northZ.HasValue && southZ.HasValue && eastX.HasValue && westX.HasValue)
            {
                float centerX = (eastX.Value + westX.Value) / 2f;
                float centerZ = (northZ.Value + southZ.Value) / 2f;
                return new Vector3(centerX, 0f, centerZ);
            }

            return null;
        }

#if UNITY_EDITOR
        private void OnDrawGizmos()
        {
            if (!showDebugGizmos || _wallSegments == null)
                return;

            foreach (var kvp in _wallSegments)
            {
                foreach (var segment in kvp.Value)
                {
                    Gizmos.color = segment.hasOpening ? openingSegmentColor : solidSegmentColor;
                    Vector3 size = new Vector3(
                        segment.config.width,
                        segment.height,
                        segment.config.thickness
                    );

                    // Rotate size based on side
                    if (segment.side == ShaftSide.East || segment.side == ShaftSide.West)
                    {
                        size = new Vector3(segment.config.thickness, segment.height, segment.config.width);
                    }

                    Gizmos.DrawWireCube(segment.worldPosition, size);

                    if (segment.hasOpening)
                    {
                        Gizmos.color = Color.green;
                        Gizmos.DrawWireSphere(segment.worldPosition, 0.5f);
                    }
                }
            }
        }
#endif
    }

    /// <summary>
    /// Represents a single wall segment instance.
    /// </summary>
    [Serializable]
    public class ShaftWallSegment
    {
        public ShaftSide side;
        public ShaftWallSegmentConfig config;
        public float depthFromTop;
        public float height;
        public bool hasOpening;
        public Vector3 worldPosition;
        public GameObject instance;
        public int index;

        /// <summary>
        /// Gets the depth at the center of this segment.
        /// </summary>
        public float CenterDepth => depthFromTop + height / 2f;
    }
}
