using UnityEngine;

namespace BeneathTheFloor.Environment
{
    /// <summary>
    /// Creates a ring-shaped floor with a square opening at the center.
    /// The opening reveals the UndergroundTerrain (DiggingV2) below.
    /// Replaces the original solid BasementFloor with 4 floor segments.
    /// </summary>
    public class BasementDigOpeningSetup : MonoBehaviour
    {
        [Header("Floor Settings")]
        [Tooltip("Y position of the floor CENTER (not top). Read from BasementFloor at runtime.")]
        public float floorY = -3.0f;

        [Tooltip("Floor thickness (Y scale).")]
        public float floorThickness = 0.2f;

        // Computed at runtime: floor TOP surface Y = floorY + floorThickness/2
        private float floorTopY;

        [Tooltip("Full floor size in X and Z (will auto-read from BasementFloor if found).")]
        public float floorSize = 15f;

        [Tooltip("Size of the square opening (holeSize x holeSize). Should match terrain horizontalExtent.")]
        public float holeSize = 12f;

        [Header("Material")]
        [Tooltip("Material for floor segments. If null, copies from original BasementFloor.")]
        public Material floorMaterial;

        [Header("Debug")]
        public bool showDebugLogs = false;

        [Header("Bake Detection")]
        [Tooltip("Skip setup if baked floor ring already exists in scene.")]
        public bool skipIfBakedFloorRingExists = true;

        // Runtime tracking
        private GameObject floorRingRoot;
        private GameObject originalFloor;
        private bool isInitialized = false;

        private void Start()
        {
            // Delay to ensure basement is fully created by other scripts
            StartCoroutine(DelayedSetup());
        }

        private System.Collections.IEnumerator DelayedSetup()
        {
            // Wait 2 frames to ensure other setup scripts (BasementResizer, etc.) complete
            yield return null;
            yield return null;

            // Try to find and setup floor, with retry if not found immediately
            int maxRetries = 5;
            for (int i = 0; i < maxRetries; i++)
            {
                if (TryFindBasementFloor())
                {
                    SetupFloorWithOpening();
                    yield break;
                }

                if (showDebugLogs)
                    Debug.Log($"[BasementDigOpeningSetup] BasementFloor not found yet, retry {i + 1}/{maxRetries}...");

                yield return null; // Wait another frame
            }

            // No BasementFloor found - check if we already have a working floor ring
            if (IsBakedFloorRingPresent())
            {
                if (showDebugLogs) Debug.Log("[BasementDigOpeningSetup] BasementFloor not found, but baked floor ring exists. Skipping setup.");
                isInitialized = true;
                yield break;
            }

            // No floor at all - just skip (terrain works independently)
        }

        /// <summary>
        /// Syncs hole size from UndergroundTerrainManager (single source of truth).
        /// </summary>
        private void SyncHoleSizeFromTerrain()
        {
            var terrainManager = Digging.UndergroundTerrainManager.Instance;
            if (terrainManager == null)
            {
                terrainManager = FindObjectOfType<Digging.UndergroundTerrainManager>();
            }

            if (terrainManager != null)
            {
                float terrainExtent = terrainManager.horizontalExtent;
                if (Mathf.Abs(holeSize - terrainExtent) > 0.01f)
                {
                    if (showDebugLogs) Debug.Log($"[BasementDigOpeningSetup] Syncing holeSize from terrain: {holeSize} -> {terrainExtent}");
                    holeSize = terrainExtent;
                }
            }
            else
            {
                Debug.LogWarning("[BasementDigOpeningSetup] UndergroundTerrainManager not found, using default holeSize");
            }
        }

        /// <summary>
        /// Attempts to find the BasementFloor GameObject.
        /// </summary>
        private bool TryFindBasementFloor()
        {
            originalFloor = GameObject.Find("BasementFloor");
            if (originalFloor == null)
            {
                // Try under Basement parent
                GameObject basement = GameObject.Find("Basement");
                if (basement != null)
                {
                    Transform floorTransform = basement.transform.Find("BasementFloor");
                    if (floorTransform != null)
                        originalFloor = floorTransform.gameObject;
                }
            }

            return originalFloor != null;
        }

        /// <summary>
        /// Checks if baked floor ring already exists in the scene.
        /// </summary>
        private bool IsBakedFloorRingPresent()
        {
            // Check for BasementFloor_Ring object
            GameObject floorRing = GameObject.Find("BasementFloor_Ring");
            if (floorRing != null)
            {
                // Verify it has children (floor segments)
                if (floorRing.transform.childCount > 0)
                {
                    if (showDebugLogs)
                        Debug.Log($"[BasementDigOpeningSetup] Found baked BasementFloor_Ring with {floorRing.transform.childCount} segments");
                    return true;
                }
            }

            // Also check for individual floor segments at expected locations
            string[] segmentNames = { "Floor_North", "Floor_South", "Floor_East", "Floor_West" };
            int foundCount = 0;

            foreach (string name in segmentNames)
            {
                if (GameObject.Find(name) != null)
                {
                    foundCount++;
                }
            }

            // If we found most segments, consider it baked
            if (foundCount >= 3)
            {
                if (showDebugLogs)
                    Debug.Log($"[BasementDigOpeningSetup] Found {foundCount} baked floor segments");
                return true;
            }

            return false;
        }

        /// <summary>
        /// Main setup method. Call this to create the floor ring.
        /// </summary>
        [ContextMenu("Setup Floor With Opening")]
        public void SetupFloorWithOpening()
        {
            if (isInitialized)
            {
                if (showDebugLogs)
                    Debug.Log("[BasementDigOpeningSetup] Already initialized, skipping.");
                return;
            }

            // Check if baked floor ring already exists
            if (skipIfBakedFloorRingExists && IsBakedFloorRingPresent())
            {
                if (showDebugLogs) Debug.Log("[BasementDigOpeningSetup] Baked floor ring detected (BasementFloor_Ring). Skipping runtime creation.");
                isInitialized = true;
                return;
            }

            // Ensure we have a reference to the floor
            if (originalFloor == null && !TryFindBasementFloor())
            {
                Debug.LogError("[BasementDigOpeningSetup] BasementFloor not found!");
                return;
            }

            // Auto-sync hole size from terrain manager (single source of truth)
            SyncHoleSizeFromTerrain();

            // Read ACTUAL floor properties (after BasementResizer has modified them)
            Vector3 center = originalFloor.transform.position;
            Vector3 scale = originalFloor.transform.localScale;

            // Use actual floor values from the scene
            float fullSizeX = scale.x;
            float fullSizeZ = scale.z;
            float thickness = scale.y;
            float actualFloorCenterY = center.y;

            // Update our fields with actual values
            floorY = actualFloorCenterY;
            floorThickness = thickness;
            floorSize = Mathf.Max(fullSizeX, fullSizeZ);

            // Calculate floor TOP surface Y (single source of truth for alignment)
            // floorTopY = floorCenterY + thickness/2
            floorTopY = floorY + (floorThickness / 2f);

            if (showDebugLogs)
            {
                Debug.Log($"[BasementDigOpeningSetup] === FLOOR READING ===");
                Debug.Log($"[BasementDigOpeningSetup] Original floor position: ({center.x:F4}, {center.y:F4}, {center.z:F4})");
                Debug.Log($"[BasementDigOpeningSetup] Original floor scale: ({scale.x:F4}, {scale.y:F4}, {scale.z:F4})");
                Debug.Log($"[BasementDigOpeningSetup] Floor CENTER Y = {floorY:F4}");
                Debug.Log($"[BasementDigOpeningSetup] Floor thickness = {floorThickness:F4}");
                Debug.Log($"[BasementDigOpeningSetup] Floor TOP Y = {floorY:F4} + {floorThickness / 2f:F4} = {floorTopY:F4}");
                Debug.Log($"[BasementDigOpeningSetup] Floor size: {fullSizeX} x {fullSizeZ}, Hole size: {holeSize}");
            }

            // Step 3: Get material from original floor if not assigned
            if (floorMaterial == null)
            {
                Renderer renderer = originalFloor.GetComponent<Renderer>();
                if (renderer != null && renderer.sharedMaterial != null)
                {
                    floorMaterial = new Material(renderer.sharedMaterial);
                }
                else
                {
                    floorMaterial = CreateDefaultFloorMaterial();
                }
            }

            // Step 4: Disable or destroy original floor's renderer and collider
            Renderer origRenderer = originalFloor.GetComponent<Renderer>();
            if (origRenderer != null) origRenderer.enabled = false;

            Collider origCollider = originalFloor.GetComponent<Collider>();
            if (origCollider != null) origCollider.enabled = false;

            // Step 5: Create the floor ring
            CreateFloorRing(center, fullSizeX, fullSizeZ, thickness, holeSize);

            // Step 6: Log UndergroundTerrain bounds
            LogUndergroundTerrainBounds();

            isInitialized = true;

            // Final summary log
            if (showDebugLogs) Debug.Log($"[DiggingV2 Setup] Old BasementFloorHoleSetup removed. New BasementDigOpeningSetup active. " +
                      $"HoleSize={holeSize}, floorSize={floorSize}.");
        }

        /// <summary>
        /// Creates 4 floor segments forming a ring with a square hole in the center.
        /// All segments are positioned so their TOP surface is at floorTopY.
        /// Properly handles rectangular floors (e.g., 30x22).
        /// </summary>
        private void CreateFloorRing(Vector3 center, float fullSizeX, float fullSizeZ, float thickness, float holeSize)
        {
            // Create parent object at origin (not at floor center, to avoid offset issues)
            floorRingRoot = new GameObject("BasementFloor_Ring");
            floorRingRoot.transform.position = Vector3.zero;
            floorRingRoot.transform.rotation = Quaternion.identity;

            // RECTANGULAR floor dimensions (30x22)
            float halfX = fullSizeX / 2f;  // 15 for X direction
            float halfZ = fullSizeZ / 2f;  // 11 for Z direction
            float holeHalf = holeSize / 2f; // 5 for 10x10 hole

            // Segment CENTER Y: position so TOP surface is at floorTopY
            // segmentCenterY = floorTopY - thickness/2
            float segmentCenterY = floorTopY - (thickness / 2f);

            // Get ground layer
            int groundLayer = LayerMask.NameToLayer("Ground");
            if (groundLayer == -1) groundLayer = 0;

            if (showDebugLogs)
            {
                Debug.Log($"[BasementDigOpeningSetup] === RING CREATION ===");
                Debug.Log($"[BasementDigOpeningSetup] Floor size: {fullSizeX} x {fullSizeZ}, Hole size: {holeSize}");
                Debug.Log($"[BasementDigOpeningSetup] Floor extends: X=[{-halfX} to {halfX}], Z=[{-halfZ} to {halfZ}]");
                Debug.Log($"[BasementDigOpeningSetup] Hole extends: X=[{-holeHalf} to {holeHalf}], Z=[{-holeHalf} to {holeHalf}]");
                Debug.Log($"[BasementDigOpeningSetup] Segment centerY={segmentCenterY:F4}, topY will be={floorTopY:F4}");
            }

            // ===== NORTH SEGMENT =====
            // Covers Z from +holeHalf to +halfZ (from hole north edge to floor north edge)
            // Full width in X (from -halfX to +halfX)
            float northDepth = halfZ - holeHalf;  // 11 - 5 = 6
            float northCenterZ = (holeHalf + halfZ) / 2f;  // (5 + 11) / 2 = 8
            if (northDepth > 0.01f)
            {
                Vector3 northScale = new Vector3(fullSizeX, thickness, northDepth);
                Vector3 northPos = new Vector3(center.x, segmentCenterY, northCenterZ);
                CreateFloorSegment("Floor_North", northPos, northScale, groundLayer);
                if (showDebugLogs) Debug.Log($"[BasementDigOpeningSetup] Floor_North: pos=({northPos.x:F2}, {northPos.y:F4}, {northPos.z:F2}), scale=({northScale.x}, {northScale.y:F2}, {northScale.z})");
            }

            // ===== SOUTH SEGMENT =====
            // Covers Z from -halfZ to -holeHalf (from floor south edge to hole south edge)
            float southDepth = halfZ - holeHalf;  // 11 - 5 = 6
            float southCenterZ = -(holeHalf + halfZ) / 2f;  // -(5 + 11) / 2 = -8
            if (southDepth > 0.01f)
            {
                Vector3 southScale = new Vector3(fullSizeX, thickness, southDepth);
                Vector3 southPos = new Vector3(center.x, segmentCenterY, southCenterZ);
                CreateFloorSegment("Floor_South", southPos, southScale, groundLayer);
                if (showDebugLogs) Debug.Log($"[BasementDigOpeningSetup] Floor_South: pos=({southPos.x:F2}, {southPos.y:F4}, {southPos.z:F2}), scale=({southScale.x}, {southScale.y:F2}, {southScale.z})");
            }

            // ===== EAST SEGMENT =====
            // Covers X from +holeHalf to +halfX (from hole east edge to floor east edge)
            // Covers Z from -holeHalf to +holeHalf (the hole's height in Z)
            float eastWidth = halfX - holeHalf;  // 15 - 5 = 10
            float eastCenterX = (holeHalf + halfX) / 2f;  // (5 + 15) / 2 = 10
            if (eastWidth > 0.01f)
            {
                Vector3 eastScale = new Vector3(eastWidth, thickness, holeSize);
                Vector3 eastPos = new Vector3(eastCenterX, segmentCenterY, center.z);
                CreateFloorSegment("Floor_East", eastPos, eastScale, groundLayer);
                if (showDebugLogs) Debug.Log($"[BasementDigOpeningSetup] Floor_East: pos=({eastPos.x:F2}, {eastPos.y:F4}, {eastPos.z:F2}), scale=({eastScale.x}, {eastScale.y:F2}, {eastScale.z})");
            }

            // ===== WEST SEGMENT =====
            // Covers X from -halfX to -holeHalf (from floor west edge to hole west edge)
            float westWidth = halfX - holeHalf;  // 15 - 5 = 10
            float westCenterX = -(holeHalf + halfX) / 2f;  // -(5 + 15) / 2 = -10
            if (westWidth > 0.01f)
            {
                Vector3 westScale = new Vector3(westWidth, thickness, holeSize);
                Vector3 westPos = new Vector3(westCenterX, segmentCenterY, center.z);
                CreateFloorSegment("Floor_West", westPos, westScale, groundLayer);
                if (showDebugLogs) Debug.Log($"[BasementDigOpeningSetup] Floor_West: pos=({westPos.x:F2}, {westPos.y:F4}, {westPos.z:F2}), scale=({westScale.x}, {westScale.y:F2}, {westScale.z})");
            }

            if (showDebugLogs) Debug.Log($"[BasementDigOpeningSetup] Created floor ring with {holeSize}x{holeSize} hole. Player can dig in the opening.");

            // Verify alignment
            VerifyRingAlignment(segmentCenterY, thickness);
        }

        /// <summary>
        /// Verifies that ring segments align with floor.
        /// </summary>
        private void VerifyRingAlignment(float segmentCenterY, float thickness)
        {
            if (!showDebugLogs) return;

            Debug.Log($"[BasementDigOpeningSetup] === RING ALIGNMENT CHECK ===");

            // Expected ring top Y based on our calculation
            float expectedRingTopY = segmentCenterY + (thickness / 2f);
            Debug.Log($"[BasementDigOpeningSetup] Expected ring top Y = {segmentCenterY:F4} + {thickness / 2f:F4} = {expectedRingTopY:F4}");
            Debug.Log($"[BasementDigOpeningSetup] Target floor top Y = {floorTopY:F4}");

            float diff = Mathf.Abs(expectedRingTopY - floorTopY);
            if (diff <= 0.001f)
            {
                Debug.Log($"[BasementDigOpeningSetup] ✓ RING ALIGNMENT OK: Ring top ({expectedRingTopY:F4}) == Floor top ({floorTopY:F4}), diff={diff:F6}");
            }
            else
            {
                Debug.LogWarning($"[BasementDigOpeningSetup] ✗ RING MISALIGNMENT! Ring top ({expectedRingTopY:F4}) != Floor top ({floorTopY:F4}), diff={diff:F4}");
            }

            // Verify actual created segments
            GameObject floorNorth = GameObject.Find("Floor_North");
            if (floorNorth != null)
            {
                float actualRingTopY = floorNorth.transform.position.y + (floorNorth.transform.localScale.y / 2f);
                float actualDiff = Mathf.Abs(actualRingTopY - floorTopY);
                Debug.Log($"[BasementDigOpeningSetup] Actual Floor_North top Y = {actualRingTopY:F4}");

                if (actualDiff > 0.001f)
                {
                    Debug.LogWarning($"[BasementDigOpeningSetup] ✗ ACTUAL RING MISALIGNMENT! Actual top ({actualRingTopY:F4}) != Floor top ({floorTopY:F4}), diff={actualDiff:F4}");
                }
            }
        }

        /// <summary>
        /// Creates a single floor segment as a cube.
        /// </summary>
        private void CreateFloorSegment(string name, Vector3 position, Vector3 scale, int layer)
        {
            GameObject segment = GameObject.CreatePrimitive(PrimitiveType.Cube);
            segment.name = name;
            segment.transform.SetParent(floorRingRoot.transform, true);
            segment.transform.position = position;
            segment.transform.localScale = scale;
            segment.layer = layer;

            // Apply material
            Renderer renderer = segment.GetComponent<Renderer>();
            if (renderer != null && floorMaterial != null)
            {
                renderer.material = floorMaterial;
            }

            // Ensure collider is solid (not trigger)
            BoxCollider col = segment.GetComponent<BoxCollider>();
            if (col != null)
            {
                col.isTrigger = false;
            }
        }

        /// <summary>
        /// Logs UndergroundTerrain bounds for verification.
        /// </summary>
        private void LogUndergroundTerrainBounds()
        {
            if (!showDebugLogs) return;

            // Find UndergroundTerrain
            GameObject terrainObj = GameObject.Find("UndergroundTerrain");
            if (terrainObj == null)
            {
                // Try to find via manager
                var manager = FindObjectOfType<Digging.UndergroundTerrainManager>();
                if (manager != null)
                    terrainObj = manager.gameObject;
            }

            if (terrainObj == null)
            {
                Debug.LogWarning("[BasementDigOpeningSetup] UndergroundTerrain not found for bounds check.");
                return;
            }

            // Find mesh collider on terrain chunk
            MeshCollider meshCollider = terrainObj.GetComponentInChildren<MeshCollider>();
            if (meshCollider != null && meshCollider.sharedMesh != null)
            {
                Bounds b = meshCollider.bounds;
                float topY = b.center.y + b.extents.y;

                Debug.Log($"[UndergroundTerrain] bounds center={b.center}, extents={b.extents}");
                Debug.Log($"[UndergroundTerrain] Top surface Y={topY:F2}, Floor opening at Y={floorY:F2}");
                Debug.Log($"[UndergroundTerrain] XZ coverage: X=[{b.min.x:F1} to {b.max.x:F1}], Z=[{b.min.z:F1} to {b.max.z:F1}]");

                // Verify alignment
                float holeHalf = holeSize / 2f;
                bool coversHole = (b.min.x <= -holeHalf && b.max.x >= holeHalf &&
                                   b.min.z <= -holeHalf && b.max.z >= holeHalf);

                if (coversHole)
                {
                    Debug.Log($"[BasementDigOpeningSetup] Terrain XZ coverage includes hole area. Good!");
                }
                else
                {
                    Debug.LogWarning($"[BasementDigOpeningSetup] Terrain may not fully cover hole area!");
                }

                // Check Y alignment
                float yDiff = Mathf.Abs(topY - floorY);
                if (yDiff < 0.5f)
                {
                    Debug.Log($"[BasementDigOpeningSetup] Terrain top Y={topY:F2} is close to floor Y={floorY:F2}. Good!");
                }
                else
                {
                    Debug.LogWarning($"[BasementDigOpeningSetup] Terrain top Y={topY:F2} differs from floor Y={floorY:F2} by {yDiff:F2}m!");
                }
            }
            else
            {
                Debug.LogWarning("[BasementDigOpeningSetup] No MeshCollider found on UndergroundTerrain for bounds check.");
            }
        }

        /// <summary>
        /// Creates a default floor material.
        /// </summary>
        private Material CreateDefaultFloorMaterial()
        {
            Shader shader = Shader.Find("Universal Render Pipeline/Lit");
            if (shader == null)
                shader = Shader.Find("Standard");

            Material mat = new Material(shader);
            mat.name = "FloorRingMaterial";
            mat.color = new Color(0.4f, 0.35f, 0.3f); // Brown-ish floor
            if (mat.HasProperty("_Smoothness"))
                mat.SetFloat("_Smoothness", 0.2f);
            return mat;
        }

        /// <summary>
        /// Resets the floor setup.
        /// </summary>
        [ContextMenu("Reset Floor Setup")]
        public void ResetFloorSetup()
        {
            if (floorRingRoot != null)
            {
                if (Application.isPlaying)
                    Destroy(floorRingRoot);
                else
                    DestroyImmediate(floorRingRoot);
                floorRingRoot = null;
            }

            if (originalFloor != null)
            {
                Renderer renderer = originalFloor.GetComponent<Renderer>();
                if (renderer != null) renderer.enabled = true;

                Collider collider = originalFloor.GetComponent<Collider>();
                if (collider != null) collider.enabled = true;
            }

            isInitialized = false;
            if (showDebugLogs) Debug.Log("[BasementDigOpeningSetup] Floor setup reset.");
        }

        private void OnDrawGizmosSelected()
        {
            // Draw the hole outline
            Gizmos.color = Color.red;
            Vector3 holeCenter = new Vector3(0, floorY + 0.05f, 0);
            Gizmos.DrawWireCube(holeCenter, new Vector3(holeSize, 0.1f, holeSize));

            // Draw floor outline
            Gizmos.color = Color.green;
            Gizmos.DrawWireCube(new Vector3(0, floorY, 0), new Vector3(floorSize, floorThickness, floorSize));

            // Draw hole center marker
            Gizmos.color = Color.yellow;
            Gizmos.DrawSphere(holeCenter, 0.2f);
        }
    }
}
