using UnityEngine;
using System.Collections.Generic;

namespace BeneathTheFloor.Environment
{
    /// <summary>
    /// Resizes the basement room at runtime to create a MUCH larger space.
    /// Uses explicit sizes: Floor 30x22, Ceiling 30x22, DigArea 10x10.
    /// Repositions machines to room edges, leaving center free for digging.
    /// </summary>
    public class BasementResizer : MonoBehaviour
    {
        [Header("=== EXPLICIT ROOM DIMENSIONS ===")]
        [SerializeField] private float floorSizeX = 30f;
        [SerializeField] private float floorSizeZ = 22f;
        [SerializeField] private float wallHeight = 3.5f;
        [SerializeField] private float wallThickness = 0.3f;

        [Header("=== EXPLICIT DIG AREA SIZE ===")]
        [SerializeField] private float digAreaSize = 10f;
        [SerializeField] private float digVisualThickness = 0.2f;

        [Header("=== LIGHTING FOR LARGE ROOM ===")]
        [SerializeField] private float lightRange = 35f;
        [SerializeField] private float lightIntensity = 2.0f;

        [Header("=== OPTIONS ===")]
        [SerializeField] private bool runOnStart = true;
        [SerializeField] private bool showDebugLogs = false;
        [SerializeField] private bool skipIfBakedLayoutExists = true;

        [Header("=== BAKE CONTROL ===")]
        [Tooltip("When TRUE, completely disables all runtime resizing. Use this after baking the layout.")]
        public bool disableRuntimeResize = true;

        private bool _initialized = false;

        // Cached references
        private Transform basementFloor;
        private Transform basementCeiling;
        private Transform wallNorth, wallSouth, wallEast, wallWest;
        private Transform digArea;
        private Transform digSurface;
        private Transform digSurfaceVisual;
        private List<Transform> machines = new List<Transform>();

        private float floorY;           // Floor CENTER Y (position.y)
        private float floorThicknessY;   // Floor thickness (scale.y)
        private float floorTopY;         // Floor TOP surface Y = floorY + floorThicknessY/2
        private float ceilingY;

        private void Start()
        {
            // Check if runtime resize is disabled (baked layout mode)
            if (disableRuntimeResize)
            {
                Debug.Log("[BasementResizer] Runtime resize disabled - using baked layout.");
                return;
            }

            if (runOnStart)
            {
                ResizeBasement();
            }
        }

        /// <summary>
        /// Checks if a baked layout already exists (floor is already 30x22).
        /// </summary>
        private bool IsBakedLayoutPresent()
        {
            // Find BasementFloor independently (don't rely on cached reference yet)
            GameObject basement = GameObject.Find("Basement");
            Transform floor = null;

            if (basement != null)
            {
                floor = basement.transform.Find("BasementFloor");
            }
            if (floor == null)
            {
                GameObject floorObj = GameObject.Find("BasementFloor");
                if (floorObj != null)
                    floor = floorObj.transform;
            }

            if (floor != null &&
                Mathf.Approximately(floor.localScale.x, 30f) &&
                Mathf.Approximately(floor.localScale.z, 22f))
            {
                Debug.Log("[BasementResizer] Baked layout detected (30x22 floor). Skipping resize.");
                return true;
            }

            return false;
        }

        [ContextMenu("Resize Basement (30x22)")]
        public void ResizeBasement()
        {
            // Primary safeguard: check if runtime resize is disabled
            if (disableRuntimeResize)
            {
                Debug.Log("[BasementResizer] Runtime resize disabled - using baked layout. No changes made.");
                return;
            }

            if (_initialized)
            {
                Debug.Log("[BasementResizer] Already resized, skipping. Use Reset first if you want to re-run.");
                return;
            }

            // Skip if baked layout already exists
            if (skipIfBakedLayoutExists && IsBakedLayoutPresent())
            {
                Debug.Log("[BasementResizer] Baked layout detected. Skipping runtime resize.");
                _initialized = true; // Mark as initialized to prevent future attempts
                return;
            }

            Debug.Log("[BasementResizer] Runtime resize applied (no baked layout detected).");
            Debug.Log("[BasementResizer] ========== STARTING BASEMENT EXPANSION ==========");
            Debug.Log($"[BasementResizer] Target floor size: {floorSizeX} x {floorSizeZ}");
            Debug.Log($"[BasementResizer] Target dig area size: {digAreaSize} x {digAreaSize}");

            // Step 1: Find all references
            if (!FindAllReferences())
            {
                Debug.LogError("[BasementResizer] Failed to find required references!");
                return;
            }

            LogCurrentLayout();

            // Step 2: Set explicit floor size
            SetFloorSize();

            // Step 3: Reposition walls to floor edges
            RepositionWalls();

            // Step 4: Set explicit ceiling size
            SetCeilingSize();

            // Step 5: Adjust lighting for larger room
            AdjustLighting();

            // Step 6: Rearrange machines to edges
            RearrangeMachines();

            // Step 7: Set explicit dig area size and center it
            SetDigAreaSize();

            // Step 8: Setup vertical digging system (shaft + movement)
            SetupVerticalDigging();

            _initialized = true;

            // Final verification logs
            LogFinalLayout();

            Debug.Log("[BasementResizer] ========== BASEMENT EXPANSION COMPLETE ==========");
        }

        /// <summary>
        /// Sets up the vertical digging system: shaft walls and depth movement.
        /// NOTE: With DiggingV2, this is skipped - the new system uses voxel terrain instead.
        /// </summary>
        private void SetupVerticalDigging()
        {
            // With DiggingV2, we don't need the old shaft system
            // The underground terrain is now generated by UndergroundTerrainManager
            if (digArea == null || digSurfaceVisual == null)
            {
                Debug.Log("[BasementResizer] Vertical digging setup skipped – DiggingV2 uses voxel terrain instead of shaft.");
                return;
            }

            Debug.Log("[BasementResizer] Setting up vertical digging system (legacy)...");

            // Generate the dig shaft
            GenerateDigShaft();

            // Add DepthVerticalMover component
            SetupDepthVerticalMover();

            Debug.Log("[BasementResizer] Vertical digging setup complete.");
        }

        private GameObject _shaftRoot;

        private void GenerateDigShaft()
        {
            // NOTE: This legacy shaft generation is typically skipped when DiggingV2 is active.
            // The UndergroundTerrainManager now handles safety colliders.
            // If this method IS called, walls must start at floor level and go DOWN only!

            // Get center from dig area
            Vector2 shaftCenter = Vector2.zero;

            // CRITICAL FIX: Shaft walls must start at floor TOP surface and extend DOWNWARD ONLY
            // Wall top = floorTopY (no geometry above floor!)
            float shaftTopY = floorTopY;  // Top of floor surface, not below it
            float shaftRadius = digAreaSize / 2f + 0.5f; // Slightly larger than dig area
            float shaftDepth = 220f;  // Extend to full dig depth
            int wallSegments = 8;
            float wallThickness = 1f;

            if (digSurfaceVisual != null)
            {
                shaftCenter = new Vector2(digSurfaceVisual.position.x, digSurfaceVisual.position.z);
            }

            // Create shaft root - positioned at floor level
            _shaftRoot = new GameObject("DigShaftRoot");
            _shaftRoot.transform.position = new Vector3(shaftCenter.x, shaftTopY, shaftCenter.y);
            _shaftRoot.transform.SetParent(transform);

            // Create wall material
            Material wallMaterial = CreateShaftMaterial();

            // Generate wall segments in a circle
            float angleStep = 360f / wallSegments;

            for (int i = 0; i < wallSegments; i++)
            {
                float angle1 = i * angleStep * Mathf.Deg2Rad;
                float angle2 = (i + 1) * angleStep * Mathf.Deg2Rad;

                Vector3 midpointInner = new Vector3(
                    Mathf.Cos((angle1 + angle2) / 2f) * shaftRadius,
                    0,
                    Mathf.Sin((angle1 + angle2) / 2f) * shaftRadius
                );

                float segmentWidth = 2f * shaftRadius * Mathf.Sin(angleStep * Mathf.Deg2Rad / 2f);

                // Create wall segment
                GameObject wall = GameObject.CreatePrimitive(PrimitiveType.Cube);
                wall.name = $"ShaftWall_{i}";
                wall.transform.SetParent(_shaftRoot.transform);

                // CRITICAL: Wall center Y = -shaftDepth/2 means:
                //   Wall top in local space = 0 (at shaft root = floorTopY)
                //   Wall bottom = -shaftDepth
                // This ensures NO wall geometry extends above the floor!
                wall.transform.localPosition = new Vector3(
                    midpointInner.x + (midpointInner.normalized.x * wallThickness / 2f),
                    -shaftDepth / 2f,  // Center is halfway down, so top is at local Y=0 (=floorTopY)
                    midpointInner.z + (midpointInner.normalized.z * wallThickness / 2f)
                );

                // Scale: width of segment, full shaft height, wall thickness
                wall.transform.localScale = new Vector3(segmentWidth * 1.1f, shaftDepth, wallThickness);

                // Rotate to face center
                float facingAngle = Mathf.Atan2(midpointInner.z, midpointInner.x) * Mathf.Rad2Deg;
                wall.transform.localRotation = Quaternion.Euler(0, -facingAngle + 90f, 0);

                // Apply material
                var renderer = wall.GetComponent<Renderer>();
                if (renderer != null)
                {
                    renderer.material = wallMaterial;
                }
            }

            // Create shaft floor at bottom (bedrock)
            GameObject floor = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
            floor.name = "ShaftFloor";
            floor.transform.SetParent(_shaftRoot.transform);
            floor.transform.localPosition = new Vector3(0, -shaftDepth - 0.25f, 0);
            floor.transform.localScale = new Vector3(shaftRadius * 2f, 0.5f, shaftRadius * 2f);

            var floorRenderer = floor.GetComponent<Renderer>();
            if (floorRenderer != null)
            {
                floorRenderer.material = wallMaterial;
            }

            Debug.Log($"[WALLS] Created dig shaft:");
            Debug.Log($"[WALLS]   Shaft root at Y = {shaftTopY:F2} (= floorTopY, basement floor surface)");
            Debug.Log($"[WALLS]   Walls extend from Y={shaftTopY:F2} DOWN to Y={shaftTopY - shaftDepth:F2}");
            Debug.Log($"[WALLS]   NO wall geometry above basement floor!");
            Debug.Log($"[BasementResizer] Shaft center=({shaftCenter.x}, {shaftTopY}, {shaftCenter.y}), radius={shaftRadius}, depth={shaftDepth}");
        }

        private Material CreateShaftMaterial()
        {
            Shader shader = Shader.Find("Universal Render Pipeline/Lit");
            if (shader == null)
            {
                shader = Shader.Find("Standard");
            }

            Material mat = new Material(shader);
            mat.color = new Color(0.25f, 0.18f, 0.12f); // Dark brown/soil
            mat.SetFloat("_Smoothness", 0.1f);
            return mat;
        }

        private void SetupDepthVerticalMover()
        {
            // DIGGING SYSTEM REMOVED - DepthVerticalMover setup disabled
            Debug.Log("[BasementResizer] DepthVerticalMover setup skipped - digging system removed");

            // All code below was removed when digging system was disabled
            // var verticalMover = GetComponent<Digging.DepthVerticalMover>();
            // if (verticalMover == null)
            //     verticalMover = gameObject.AddComponent<Digging.DepthVerticalMover>();
            // var type = typeof(Digging.DepthVerticalMover);
            // ... reflection-based setup code removed
        }

        private bool FindAllReferences()
        {
            // Find Basement parent
            GameObject basement = GameObject.Find("Basement");

            // Find floor
            basementFloor = FindObject("BasementFloor", basement?.transform);
            if (basementFloor == null)
            {
                Debug.LogError("[BasementResizer] BasementFloor not found!");
                return false;
            }

            floorY = basementFloor.position.y;
            floorThicknessY = basementFloor.localScale.y;

            // Calculate floor TOP surface Y (single source of truth for alignment)
            floorTopY = floorY + (floorThicknessY / 2f);
            Debug.Log($"[BasementResizer] Floor Y values: CENTER={floorY:F4}, thickness={floorThicknessY:F4}, TOP={floorTopY:F4}");

            // Find walls
            wallNorth = FindObject("Basement_Wall_North", basement?.transform);
            wallSouth = FindObject("Basement_Wall_South", basement?.transform);
            wallEast = FindObject("Basement_Wall_East", basement?.transform);
            wallWest = FindObject("Basement_Wall_West", basement?.transform);

            // Find ceiling
            basementCeiling = FindObject("BasementCeiling", basement?.transform);
            if (basementCeiling != null)
            {
                ceilingY = basementCeiling.position.y;
            }

            // Find dig area
            digArea = FindObject("DigArea", basement?.transform);
            if (digArea == null) digArea = FindObject("DigArea", null);

            if (digArea != null)
            {
                digSurface = digArea.Find("DigSurface");
                // DIGGING SYSTEM REMOVED - DigSurface component check disabled
                // if (digSurface == null)
                // {
                //     var ds = digArea.GetComponent<Digging.DigSurface>();
                //     if (ds != null) digSurface = digArea;
                // }

                if (digSurface != null)
                {
                    digSurfaceVisual = digSurface.Find("DigSurfaceVisual");
                }
            }

            // Find machines
            FindMachines(basement?.transform);

            return basementFloor != null;
        }

        private Transform FindObject(string name, Transform parent)
        {
            if (parent != null)
            {
                Transform found = parent.Find(name);
                if (found != null) return found;
            }

            GameObject obj = GameObject.Find(name);
            return obj?.transform;
        }

        private void FindMachines(Transform basement)
        {
            machines.Clear();

            // NOTE: Workbench removed - using UpgradeStation as the single upgrade/crafting point
            string[] machineNames = new string[]
            {
                "Refinery",
                "UpgradeStation",
                "EnergyGenerator",
                "TradeTerminal",
                "StorageCrate",
                "FuelStation"
            };

            foreach (string name in machineNames)
            {
                Transform machine = FindObject(name, basement);
                if (machine != null)
                {
                    machines.Add(machine);
                }
            }

            // Also find by component types
            var refineries = FindObjectsOfType<Machines.Refinery>();
            foreach (var r in refineries)
            {
                if (!machines.Contains(r.transform))
                    machines.Add(r.transform);
            }

            var upgradeStations = FindObjectsOfType<Machines.UpgradeStation>();
            foreach (var us in upgradeStations)
            {
                if (!machines.Contains(us.transform))
                    machines.Add(us.transform);
            }

            var energyGenerators = FindObjectsOfType<Machines.EnergyGenerator>();
            foreach (var eg in energyGenerators)
            {
                if (!machines.Contains(eg.transform))
                    machines.Add(eg.transform);
            }

            if (showDebugLogs)
                Debug.Log($"[BasementResizer] Found {machines.Count} machines to reposition");
        }

        private void LogCurrentLayout()
        {
            if (!showDebugLogs) return;

            Debug.Log($"[BasementResizer] === BEFORE RESIZE ===");
            Debug.Log($"[BasementResizer] BasementFloor: pos={basementFloor.position}, scale={basementFloor.localScale}");

            if (basementCeiling != null)
                Debug.Log($"[BasementResizer] BasementCeiling: pos={basementCeiling.position}, scale={basementCeiling.localScale}");

            if (digSurfaceVisual != null)
                Debug.Log($"[BasementResizer] DigSurfaceVisual: pos={digSurfaceVisual.position}, scale={digSurfaceVisual.localScale}");
        }

        /// <summary>
        /// Sets the floor to EXACT size 30 x Y x 22 (keeping Y thickness).
        /// </summary>
        private void SetFloorSize()
        {
            if (basementFloor == null) return;

            // Set EXPLICIT scale - not relative!
            Vector3 newScale = new Vector3(floorSizeX, floorThicknessY, floorSizeZ);
            basementFloor.localScale = newScale;

            // Center floor at origin XZ
            Vector3 pos = basementFloor.position;
            pos.x = 0;
            pos.z = 0;
            basementFloor.position = pos;

            Debug.Log($"[BasementResizer] Floor scale set to EXPLICIT: ({floorSizeX}, {floorThicknessY}, {floorSizeZ})");
        }

        /// <summary>
        /// Repositions walls so their INNER FACE sits exactly at the floor edges.
        /// Wall thickness extends OUTWARD from the floor, not inward.
        /// </summary>
        private void RepositionWalls()
        {
            float halfX = floorSizeX / 2f;
            float halfZ = floorSizeZ / 2f;

            // Wall center Y: wall bottom touches floor top, so wallY = floorTopY + wallHeight/2
            float wallCenterY = floorTopY + (wallHeight / 2f);
            float wallBottomY = wallCenterY - (wallHeight / 2f); // Should equal floorTopY

            Debug.Log($"[BasementResizer] Wall positioning:");
            Debug.Log($"[BasementResizer]   floorTopY = {floorTopY:F4}");
            Debug.Log($"[BasementResizer]   wallCenterY = {wallCenterY:F4}");
            Debug.Log($"[BasementResizer]   wallBottomY = {wallBottomY:F4}");
            Debug.Log($"[BasementResizer]   halfX (floor edge) = {halfX:F4}");
            Debug.Log($"[BasementResizer]   halfZ (floor edge) = {halfZ:F4}");
            Debug.Log($"[BasementResizer]   wallThickness = {wallThickness:F4}");

            // NORTH wall (positive Z)
            // Inner face at Z = +halfZ, thickness extends outward (+Z direction)
            // Wall center Z = halfZ + wallThickness/2
            if (wallNorth != null)
            {
                float northCenterZ = halfZ + (wallThickness / 2f);
                wallNorth.localScale = new Vector3(floorSizeX, wallHeight, wallThickness);
                wallNorth.position = new Vector3(0f, wallCenterY, northCenterZ);
                EnsureWallCollider(wallNorth);

                float innerZ = northCenterZ - (wallThickness / 2f);
                Debug.Log($"[BasementResizer] Wall North: pos=(0, {wallCenterY:F4}, {northCenterZ:F4}), scale=({floorSizeX}, {wallHeight}, {wallThickness}), innerZ={innerZ:F4}");
            }

            // SOUTH wall (negative Z)
            // Inner face at Z = -halfZ, thickness extends outward (-Z direction)
            // Wall center Z = -halfZ - wallThickness/2
            if (wallSouth != null)
            {
                float southCenterZ = -halfZ - (wallThickness / 2f);
                wallSouth.localScale = new Vector3(floorSizeX, wallHeight, wallThickness);
                wallSouth.position = new Vector3(0f, wallCenterY, southCenterZ);
                EnsureWallCollider(wallSouth);

                float innerZ = southCenterZ + (wallThickness / 2f);
                Debug.Log($"[BasementResizer] Wall South: pos=(0, {wallCenterY:F4}, {southCenterZ:F4}), scale=({floorSizeX}, {wallHeight}, {wallThickness}), innerZ={innerZ:F4}");
            }

            // EAST wall (positive X)
            // Inner face at X = +halfX, thickness extends outward (+X direction)
            // Wall center X = halfX + wallThickness/2
            if (wallEast != null)
            {
                float eastCenterX = halfX + (wallThickness / 2f);
                wallEast.localScale = new Vector3(wallThickness, wallHeight, floorSizeZ);
                wallEast.position = new Vector3(eastCenterX, wallCenterY, 0f);
                EnsureWallCollider(wallEast);

                float innerX = eastCenterX - (wallThickness / 2f);
                Debug.Log($"[BasementResizer] Wall East: pos=({eastCenterX:F4}, {wallCenterY:F4}, 0), scale=({wallThickness}, {wallHeight}, {floorSizeZ}), innerX={innerX:F4}");
            }

            // WEST wall (negative X)
            // Inner face at X = -halfX, thickness extends outward (-X direction)
            // Wall center X = -halfX - wallThickness/2
            if (wallWest != null)
            {
                float westCenterX = -halfX - (wallThickness / 2f);
                wallWest.localScale = new Vector3(wallThickness, wallHeight, floorSizeZ);
                wallWest.position = new Vector3(westCenterX, wallCenterY, 0f);
                EnsureWallCollider(wallWest);

                float innerX = westCenterX + (wallThickness / 2f);
                Debug.Log($"[BasementResizer] Wall West: pos=({westCenterX:F4}, {wallCenterY:F4}, 0), scale=({wallThickness}, {wallHeight}, {floorSizeZ}), innerX={innerX:F4}");
            }
        }

        private void EnsureWallCollider(Transform wall)
        {
            BoxCollider collider = wall.GetComponent<BoxCollider>();
            if (collider == null)
            {
                collider = wall.gameObject.AddComponent<BoxCollider>();
            }
            collider.isTrigger = false;
        }

        /// <summary>
        /// Sets the ceiling to EXACT size 30 x Y x 22.
        /// </summary>
        private void SetCeilingSize()
        {
            if (basementCeiling == null)
            {
                Debug.LogWarning("[BasementResizer] BasementCeiling not found, skipping ceiling resize");
                return;
            }

            float ceilingThickness = basementCeiling.localScale.y;
            Vector3 newScale = new Vector3(floorSizeX, ceilingThickness, floorSizeZ);
            basementCeiling.localScale = newScale;

            // Center ceiling at origin XZ, keep Y
            Vector3 pos = basementCeiling.position;
            pos.x = 0;
            pos.z = 0;
            basementCeiling.position = pos;

            Debug.Log($"[BasementResizer] Ceiling scale set to EXPLICIT: ({floorSizeX}, {ceilingThickness}, {floorSizeZ})");
        }

        /// <summary>
        /// Adjusts lighting for the much larger room.
        /// </summary>
        private void AdjustLighting()
        {
            Light[] lights = FindObjectsOfType<Light>();
            int adjustedCount = 0;

            foreach (Light light in lights)
            {
                // Only adjust lights in the basement area (below ground level)
                if (light.transform.position.y < 0)
                {
                    light.range = lightRange;
                    light.intensity = lightIntensity;
                    adjustedCount++;
                }
            }

            // Try to find by common names
            string[] lightNames = { "BasementLight1", "BasementLight2", "BasementLight", "PointLight" };
            foreach (string name in lightNames)
            {
                GameObject lightObj = GameObject.Find(name);
                if (lightObj != null)
                {
                    Light light = lightObj.GetComponent<Light>();
                    if (light != null)
                    {
                        light.range = lightRange;
                        light.intensity = lightIntensity;
                    }
                }
            }

            Debug.Log($"[BasementResizer] Adjusted {adjustedCount} lights: range={lightRange}, intensity={lightIntensity}");
        }

        /// <summary>
        /// Rearranges machines to the room edges, leaving center free.
        /// </summary>
        private void RearrangeMachines()
        {
            if (machines.Count == 0)
            {
                Debug.Log("[BasementResizer] No machines found to rearrange");
                return;
            }

            float machineY = floorY + 0.5f; // Above floor surface
            float edgeOffset = 2.5f; // Distance from wall
            float halfX = floorSizeX / 2f - edgeOffset;
            float halfZ = floorSizeZ / 2f - edgeOffset;

            // Define positions around the perimeter - well spread out for large room
            List<Vector3> machinePositions = new List<Vector3>
            {
                // West wall positions (negative X)
                new Vector3(-halfX, machineY, halfZ - 3f),     // NW corner area
                new Vector3(-halfX, machineY, 0),              // West center
                new Vector3(-halfX, machineY, -halfZ + 3f),    // SW corner area

                // East wall positions (positive X)
                new Vector3(halfX, machineY, halfZ - 3f),      // NE corner area
                new Vector3(halfX, machineY, 0),               // East center
                new Vector3(halfX, machineY, -halfZ + 3f),     // SE corner area

                // North wall positions (positive Z)
                new Vector3(-halfX + 5f, machineY, halfZ - 1.5f),
                new Vector3(halfX - 5f, machineY, halfZ - 1.5f),

                // South wall positions (negative Z)
                new Vector3(-halfX + 5f, machineY, -halfZ + 1.5f),
                new Vector3(halfX - 5f, machineY, -halfZ + 1.5f),
            };

            for (int i = 0; i < machines.Count && i < machinePositions.Count; i++)
            {
                Transform machine = machines[i];
                Vector3 newPos = machinePositions[i];

                // Calculate rotation to face center
                Vector3 toCenter = (Vector3.zero - newPos).normalized;
                toCenter.y = 0;
                float angle = Mathf.Atan2(toCenter.x, toCenter.z) * Mathf.Rad2Deg;

                machine.position = newPos;
                machine.rotation = Quaternion.Euler(0, angle, 0);

                if (showDebugLogs)
                    Debug.Log($"[BasementResizer] Moved {machine.name} to ({newPos.x:F1}, {newPos.y:F1}, {newPos.z:F1})");
            }

            Debug.Log($"[BasementResizer] Rearranged {Mathf.Min(machines.Count, machinePositions.Count)} machines to room edges");
        }

        /// <summary>
        /// Sets the dig area to EXACT size 10x10 and centers it.
        /// NOTE: With DiggingV2, we no longer use the old DigArea object.
        /// The underground terrain is now managed by UndergroundTerrainManager.
        /// </summary>
        private void SetDigAreaSize()
        {
            if (digArea == null)
            {
                // This is expected with DiggingV2 - the old DigArea is no longer used
                Debug.Log("[BasementResizer] No legacy DigArea present – DiggingV2 handles underground terrain separately.");
                return;
            }

            // Center the dig area at floor origin
            Vector3 digPos = new Vector3(0, floorY - 0.05f, 0);
            digArea.position = digPos;

            // Center DigSurface locally
            if (digSurface != null)
            {
                digSurface.localPosition = Vector3.zero;
            }

            // Set DigSurfaceVisual to EXPLICIT 10x10 size
            if (digSurfaceVisual != null)
            {
                // DIGGING SYSTEM REMOVED - DigSurfaceDeformer check disabled
                // var deformer = digSurfaceVisual.GetComponent<Digging.DigSurfaceDeformer>();
                // if (deformer != null)
                // {
                //     float newHoleRadius = digAreaSize * 0.4f;
                //     deformer.ResizeMesh(digAreaSize, newHoleRadius);
                // }
                // else
                {
                    // Direct scale for non-deformer visual
                    Vector3 visualScale = new Vector3(digAreaSize, digVisualThickness, digAreaSize);
                    digSurfaceVisual.localScale = visualScale;
                    Debug.Log($"[BasementResizer] DigSurfaceVisual scale set to EXPLICIT: ({digAreaSize}, {digVisualThickness}, {digAreaSize})");
                }
            }

            // Update DigGridManager if it has public methods
            UpdateDigGridManager();

            Debug.Log($"[BasementResizer] Dig area centered at (0, {digPos.y:F2}, 0), target size={digAreaSize}x{digAreaSize}");
        }

        private void UpdateDigGridManager()
        {
            // DIGGING SYSTEM REMOVED - DigGridManager update disabled
            // var gridManager = Digging.DigGridManager.Instance;
            // if (gridManager == null)
            // {
            //     gridManager = FindObjectOfType<Digging.DigGridManager>();
            // }
            // if (gridManager != null)
            // {
            //     Debug.Log("[BasementResizer] DigGridManager found");
            // }
            Debug.Log("[BasementResizer] DigGridManager update skipped - digging system removed");
        }

        /// <summary>
        /// Logs the final layout after resizing for verification.
        /// </summary>
        private void LogFinalLayout()
        {
            Debug.Log($"[BasementResizer] ========== FINAL LAYOUT VERIFICATION ==========");

            if (basementFloor != null)
            {
                Debug.Log($"[BasementResizer] Final BasementFloor scale: X={basementFloor.localScale.x}, Y={basementFloor.localScale.y}, Z={basementFloor.localScale.z}");
                Debug.Log($"[BasementResizer] Final BasementFloor position: {basementFloor.position}");
            }

            if (basementCeiling != null)
            {
                Debug.Log($"[BasementResizer] Final BasementCeiling scale: X={basementCeiling.localScale.x}, Y={basementCeiling.localScale.y}, Z={basementCeiling.localScale.z}");
                Debug.Log($"[BasementResizer] Final BasementCeiling position: {basementCeiling.position}");
            }

            if (wallNorth != null)
                Debug.Log($"[BasementResizer] Final Wall North: pos={wallNorth.position}, scale={wallNorth.localScale}");
            if (wallSouth != null)
                Debug.Log($"[BasementResizer] Final Wall South: pos={wallSouth.position}, scale={wallSouth.localScale}");
            if (wallEast != null)
                Debug.Log($"[BasementResizer] Final Wall East: pos={wallEast.position}, scale={wallEast.localScale}");
            if (wallWest != null)
                Debug.Log($"[BasementResizer] Final Wall West: pos={wallWest.position}, scale={wallWest.localScale}");

            if (digSurfaceVisual != null)
            {
                Debug.Log($"[BasementResizer] Final DigSurfaceVisual scale: X={digSurfaceVisual.localScale.x}, Y={digSurfaceVisual.localScale.y}, Z={digSurfaceVisual.localScale.z}");
                Debug.Log($"[BasementResizer] Final DigSurfaceVisual position: {digSurfaceVisual.position}");
            }

            // Alignment verification
            VerifyFloorWallAlignment();

            Debug.Log($"[BasementResizer] ================================================");
        }

        /// <summary>
        /// Comprehensive diagnostic logging of all basement geometry.
        /// Verifies both vertical (Y) and horizontal (XZ) alignment.
        /// </summary>
        private void VerifyFloorWallAlignment()
        {
            Debug.Log($"[BasementResizer] ╔══════════════════════════════════════════════════════════════╗");
            Debug.Log($"[BasementResizer] ║         COMPREHENSIVE BASEMENT GEOMETRY DIAGNOSTIC           ║");
            Debug.Log($"[BasementResizer] ╚══════════════════════════════════════════════════════════════╝");

            bool allAligned = true;
            float floorEdgeX = 0f;
            float floorEdgeZ = 0f;

            // ===== FLOOR DATA =====
            Debug.Log($"[BasementResizer] ═══ FLOOR DATA ═══");
            if (basementFloor != null)
            {
                Vector3 floorPos = basementFloor.position;
                Vector3 floorScale = basementFloor.localScale;
                float actualFloorTopY = floorPos.y + (floorScale.y / 2f);
                floorEdgeX = floorScale.x / 2f;
                floorEdgeZ = floorScale.z / 2f;

                Debug.Log($"[BasementResizer] BasementFloor.position = ({floorPos.x:F4}, {floorPos.y:F4}, {floorPos.z:F4})");
                Debug.Log($"[BasementResizer] BasementFloor.localScale = ({floorScale.x:F4}, {floorScale.y:F4}, {floorScale.z:F4})");
                Debug.Log($"[BasementResizer] Floor TOP Y = {actualFloorTopY:F4}");
                Debug.Log($"[BasementResizer] Floor edges: X = ±{floorEdgeX:F4}, Z = ±{floorEdgeZ:F4}");

                // Update floorTopY with actual value
                floorTopY = actualFloorTopY;
            }
            else
            {
                Debug.LogWarning($"[BasementResizer] BasementFloor is NULL!");
            }

            // ===== RING DATA =====
            Debug.Log($"[BasementResizer] ═══ RING DATA ═══");
            string[] ringNames = { "Floor_North", "Floor_South", "Floor_East", "Floor_West" };
            float? ringTopYValue = null;

            foreach (string name in ringNames)
            {
                GameObject ringObj = GameObject.Find(name);
                if (ringObj != null)
                {
                    Vector3 pos = ringObj.transform.position;
                    Vector3 scale = ringObj.transform.localScale;
                    float topY = pos.y + (scale.y / 2f);

                    Debug.Log($"[BasementResizer] {name}: pos=({pos.x:F2}, {pos.y:F4}, {pos.z:F2}), scale=({scale.x:F2}, {scale.y:F4}, {scale.z:F2}), topY={topY:F4}");

                    if (!ringTopYValue.HasValue)
                        ringTopYValue = topY;
                }
            }

            // ===== WALL DATA WITH INNER FACE CALCULATION =====
            Debug.Log($"[BasementResizer] ═══ WALL DATA (with inner face) ═══");
            float? wallBottomYValue = null;

            // North wall
            if (wallNorth != null)
            {
                Vector3 pos = wallNorth.position;
                Vector3 scale = wallNorth.localScale;
                float bottomY = pos.y - (scale.y / 2f);
                float innerZ = pos.z - (scale.z / 2f); // Inner face toward center
                float spanX = scale.x;

                Debug.Log($"[BasementResizer] Basement_Wall_North:");
                Debug.Log($"[BasementResizer]   pos=({pos.x:F4}, {pos.y:F4}, {pos.z:F4}), scale=({scale.x:F4}, {scale.y:F4}, {scale.z:F4})");
                Debug.Log($"[BasementResizer]   bottomY={bottomY:F4}, innerZ={innerZ:F4}, spanX={spanX:F4}");

                if (!wallBottomYValue.HasValue) wallBottomYValue = bottomY;

                // Check inner Z alignment
                float zDiff = Mathf.Abs(innerZ - floorEdgeZ);
                if (zDiff > 0.001f)
                {
                    Debug.LogWarning($"[BasementResizer]   ✗ HORIZONTAL GAP! innerZ ({innerZ:F4}) != floorEdgeZ ({floorEdgeZ:F4}), diff={zDiff:F4}");
                    allAligned = false;
                }
                else
                {
                    Debug.Log($"[BasementResizer]   ✓ innerZ aligned with floor edge");
                }
            }

            // South wall
            if (wallSouth != null)
            {
                Vector3 pos = wallSouth.position;
                Vector3 scale = wallSouth.localScale;
                float bottomY = pos.y - (scale.y / 2f);
                float innerZ = pos.z + (scale.z / 2f); // Inner face toward center (positive direction)
                float spanX = scale.x;

                Debug.Log($"[BasementResizer] Basement_Wall_South:");
                Debug.Log($"[BasementResizer]   pos=({pos.x:F4}, {pos.y:F4}, {pos.z:F4}), scale=({scale.x:F4}, {scale.y:F4}, {scale.z:F4})");
                Debug.Log($"[BasementResizer]   bottomY={bottomY:F4}, innerZ={innerZ:F4}, spanX={spanX:F4}");

                // Check inner Z alignment
                float zDiff = Mathf.Abs(innerZ - (-floorEdgeZ));
                if (zDiff > 0.001f)
                {
                    Debug.LogWarning($"[BasementResizer]   ✗ HORIZONTAL GAP! innerZ ({innerZ:F4}) != -floorEdgeZ ({-floorEdgeZ:F4}), diff={zDiff:F4}");
                    allAligned = false;
                }
                else
                {
                    Debug.Log($"[BasementResizer]   ✓ innerZ aligned with floor edge");
                }
            }

            // East wall
            if (wallEast != null)
            {
                Vector3 pos = wallEast.position;
                Vector3 scale = wallEast.localScale;
                float bottomY = pos.y - (scale.y / 2f);
                float innerX = pos.x - (scale.x / 2f); // Inner face toward center
                float spanZ = scale.z;

                Debug.Log($"[BasementResizer] Basement_Wall_East:");
                Debug.Log($"[BasementResizer]   pos=({pos.x:F4}, {pos.y:F4}, {pos.z:F4}), scale=({scale.x:F4}, {scale.y:F4}, {scale.z:F4})");
                Debug.Log($"[BasementResizer]   bottomY={bottomY:F4}, innerX={innerX:F4}, spanZ={spanZ:F4}");

                // Check inner X alignment
                float xDiff = Mathf.Abs(innerX - floorEdgeX);
                if (xDiff > 0.001f)
                {
                    Debug.LogWarning($"[BasementResizer]   ✗ HORIZONTAL GAP! innerX ({innerX:F4}) != floorEdgeX ({floorEdgeX:F4}), diff={xDiff:F4}");
                    allAligned = false;
                }
                else
                {
                    Debug.Log($"[BasementResizer]   ✓ innerX aligned with floor edge");
                }
            }

            // West wall
            if (wallWest != null)
            {
                Vector3 pos = wallWest.position;
                Vector3 scale = wallWest.localScale;
                float bottomY = pos.y - (scale.y / 2f);
                float innerX = pos.x + (scale.x / 2f); // Inner face toward center (positive direction)
                float spanZ = scale.z;

                Debug.Log($"[BasementResizer] Basement_Wall_West:");
                Debug.Log($"[BasementResizer]   pos=({pos.x:F4}, {pos.y:F4}, {pos.z:F4}), scale=({scale.x:F4}, {scale.y:F4}, {scale.z:F4})");
                Debug.Log($"[BasementResizer]   bottomY={bottomY:F4}, innerX={innerX:F4}, spanZ={spanZ:F4}");

                // Check inner X alignment
                float xDiff = Mathf.Abs(innerX - (-floorEdgeX));
                if (xDiff > 0.001f)
                {
                    Debug.LogWarning($"[BasementResizer]   ✗ HORIZONTAL GAP! innerX ({innerX:F4}) != -floorEdgeX ({-floorEdgeX:F4}), diff={xDiff:F4}");
                    allAligned = false;
                }
                else
                {
                    Debug.Log($"[BasementResizer]   ✓ innerX aligned with floor edge");
                }
            }

            // ===== VERTICAL ALIGNMENT CHECK =====
            Debug.Log($"[BasementResizer] ═══ VERTICAL ALIGNMENT CHECK ═══");

            // Check wall vertical alignment
            if (wallBottomYValue.HasValue)
            {
                float wallDiff = Mathf.Abs(wallBottomYValue.Value - floorTopY);
                if (wallDiff <= 0.001f)
                {
                    Debug.Log($"[BasementResizer] ✓ WALL VERTICAL OK: Wall bottom ({wallBottomYValue.Value:F4}) == Floor top ({floorTopY:F4})");
                }
                else
                {
                    Debug.LogWarning($"[BasementResizer] ✗ WALL VERTICAL GAP! Wall bottom ({wallBottomYValue.Value:F4}) != Floor top ({floorTopY:F4}), diff={wallDiff:F4}");
                    allAligned = false;
                }
            }

            // Check ring alignment
            if (ringTopYValue.HasValue)
            {
                float ringDiff = Mathf.Abs(ringTopYValue.Value - floorTopY);
                if (ringDiff <= 0.001f)
                {
                    Debug.Log($"[BasementResizer] ✓ RING ALIGNMENT OK: Ring top ({ringTopYValue.Value:F4}) == Floor top ({floorTopY:F4})");
                }
                else
                {
                    Debug.LogWarning($"[BasementResizer] ✗ RING GAP DETECTED! Ring top ({ringTopYValue.Value:F4}) != Floor top ({floorTopY:F4}), diff={ringDiff:F4}");
                    allAligned = false;
                }
            }

            // ===== FINAL SUMMARY =====
            Debug.Log($"[BasementResizer] ═══ FINAL SUMMARY ═══");
            Debug.Log($"[BasementResizer] Floor size: {floorEdgeX * 2f} x {floorEdgeZ * 2f}");
            Debug.Log($"[BasementResizer] Floor edges: X = ±{floorEdgeX:F4}, Z = ±{floorEdgeZ:F4}");
            Debug.Log($"[BasementResizer] Floor top Y = {floorTopY:F4}");
            Debug.Log($"[BasementResizer] Wall bottom Y = {(wallBottomYValue.HasValue ? wallBottomYValue.Value.ToString("F4") : "N/A")}");
            Debug.Log($"[BasementResizer] Ring top Y = {(ringTopYValue.HasValue ? ringTopYValue.Value.ToString("F4") : "N/A")}");

            if (allAligned)
            {
                Debug.Log($"[BasementResizer] ╔════════════════════════════════════════════════════════════╗");
                Debug.Log($"[BasementResizer] ║  ✓ WALL ALIGNMENT OK – floor and walls touch with no gap  ║");
                Debug.Log($"[BasementResizer] ╚════════════════════════════════════════════════════════════╝");
            }
            else
            {
                Debug.LogWarning($"[BasementResizer] ╔════════════════════════════════════════════════════════════╗");
                Debug.LogWarning($"[BasementResizer] ║  ✗ ALIGNMENT ISSUES DETECTED - See warnings above         ║");
                Debug.LogWarning($"[BasementResizer] ╚════════════════════════════════════════════════════════════╝");
            }
        }

        [ContextMenu("Reset Basement")]
        public void ResetBasement()
        {
            _initialized = false;
            Debug.Log("[BasementResizer] Reset flag cleared - can run resize again");
        }

        private void OnDrawGizmosSelected()
        {
            float y = floorY != 0 ? floorY : -3.1f;

            // Draw new floor bounds (green)
            Gizmos.color = Color.green;
            Gizmos.DrawWireCube(new Vector3(0, y, 0), new Vector3(floorSizeX, 0.2f, floorSizeZ));

            // Draw dig area target (red)
            Gizmos.color = Color.red;
            Gizmos.DrawWireCube(new Vector3(0, y + 0.1f, 0), new Vector3(digAreaSize, 0.1f, digAreaSize));

            // Draw wall positions
            Gizmos.color = Color.blue;
            float halfX = floorSizeX / 2f;
            float halfZ = floorSizeZ / 2f;
            float wallY = y + wallHeight / 2f;

            // North/South walls
            Gizmos.DrawWireCube(new Vector3(0, wallY, halfZ), new Vector3(floorSizeX, wallHeight, wallThickness));
            Gizmos.DrawWireCube(new Vector3(0, wallY, -halfZ), new Vector3(floorSizeX, wallHeight, wallThickness));
            // East/West walls
            Gizmos.DrawWireCube(new Vector3(halfX, wallY, 0), new Vector3(wallThickness, wallHeight, floorSizeZ));
            Gizmos.DrawWireCube(new Vector3(-halfX, wallY, 0), new Vector3(wallThickness, wallHeight, floorSizeZ));
        }
    }
}
