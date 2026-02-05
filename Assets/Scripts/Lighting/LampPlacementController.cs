using UnityEngine;
using BeneathTheFloor.Digging;

namespace BeneathTheFloor.Lighting
{
    /// <summary>
    /// Handles player input for placing lamps in the underground area.
    /// Players can purchase lamps and place them to illuminate their dig site.
    /// </summary>
    public class LampPlacementController : MonoBehaviour
    {
        [Header("Input")]
        // HARDCODED to T - do not serialize to prevent scene override
        private KeyCode placeLampKey = KeyCode.T;
        [SerializeField] private KeyCode cancelPlacementKey = KeyCode.Escape;

        [Header("Placement Settings")]
        [SerializeField] private float placementDistance = 3f;
        [SerializeField] private LayerMask placementSurfaceMask;
        [SerializeField] private float surfaceOffset = 0.1f;

        [Header("Preview")]
        [SerializeField] private GameObject lampPreviewPrefab;
        [SerializeField] private Material validPlacementMaterial;
        [SerializeField] private Material invalidPlacementMaterial;
        [SerializeField] private Color validColor = new Color(0f, 1f, 0f, 0.5f);
        [SerializeField] private Color invalidColor = new Color(1f, 0f, 0f, 0.5f);

        [Header("Inventory")]
        [SerializeField] private int lampsInInventory = 0; // Start with no lamps - must purchase from shop
        [SerializeField] private int lampCost = 50; // Cost in game currency

        // Runtime
        private bool isPlacementMode = false;
        private GameObject currentPreview;
        private Camera playerCamera;
        private bool isValidPlacement = false;
        private Vector3 placementPosition;
        private Quaternion placementRotation;
        private Vector3 placementNormal; // Normal of the surface for support detection
        private float exitPlacementTime = -1f; // Track when we exited to prevent dig on same frame

        public static LampPlacementController Instance { get; private set; }

        /// <summary>
        /// Number of lamps the player has available to place.
        /// </summary>
        public int LampsAvailable => lampsInInventory;

        /// <summary>
        /// Whether the player is currently in lamp placement mode.
        /// Returns true for a brief moment after exiting to prevent accidental digging.
        /// </summary>
        public bool IsPlacementMode => isPlacementMode || (Time.time - exitPlacementTime < 0.2f);

        /// <summary>
        /// Cost of a single lamp.
        /// </summary>
        public int LampCost => lampCost;

        private void Awake()
        {
            if (Instance == null)
            {
                Instance = this;
            }
            else if (Instance != this)
            {
                Destroy(gameObject);
                return;
            }

            // Default placement surface mask to include Diggable and Basement layers
            if (placementSurfaceMask == 0)
            {
                placementSurfaceMask = (1 << 7) | (1 << 9) | (1 << 10); // Diggable, Basement, BasementFloor
            }
        }

        private void Start()
        {
            playerCamera = Camera.main;
            CreatePreviewMaterials();
        }

        private void OnDestroy()
        {
            if (Instance == this)
            {
                Instance = null;
            }

            CleanupPreview();
        }

        private void Update()
        {
            HandleInput();

            if (isPlacementMode)
            {
                UpdatePlacementPreview();
            }
        }

        private void HandleInput()
        {
            // Toggle placement mode
            if (Input.GetKeyDown(placeLampKey))
            {
                if (!isPlacementMode)
                {
                    if (lampsInInventory > 0)
                    {
                        EnterPlacementMode();
                    }
                    else
                    {
                        // No lamps in inventory
                    }
                }
                else
                {
                    // Try to place lamp
                    TryPlaceLamp();
                }
            }

            // Cancel placement
            if (Input.GetKeyDown(cancelPlacementKey) && isPlacementMode)
            {
                ExitPlacementMode();
            }

            // Right click to also place lamp in placement mode
            if (isPlacementMode && Input.GetMouseButtonDown(0))
            {
                TryPlaceLamp();
            }

            // Right click to cancel
            if (isPlacementMode && Input.GetMouseButtonDown(1))
            {
                ExitPlacementMode();
            }
        }

        private void CreatePreviewMaterials()
        {
            if (validPlacementMaterial == null)
            {
                validPlacementMaterial = new Material(Shader.Find("Universal Render Pipeline/Lit"));
                validPlacementMaterial.SetColor("_BaseColor", validColor);
                validPlacementMaterial.SetFloat("_Surface", 1); // Transparent
                validPlacementMaterial.SetFloat("_Blend", 0); // Alpha
                validPlacementMaterial.renderQueue = 3000;
            }

            if (invalidPlacementMaterial == null)
            {
                invalidPlacementMaterial = new Material(Shader.Find("Universal Render Pipeline/Lit"));
                invalidPlacementMaterial.SetColor("_BaseColor", invalidColor);
                invalidPlacementMaterial.SetFloat("_Surface", 1);
                invalidPlacementMaterial.SetFloat("_Blend", 0);
                invalidPlacementMaterial.renderQueue = 3000;
            }
        }

        private void EnterPlacementMode()
        {
            isPlacementMode = true;
            CreatePreview();
        }

        private void ExitPlacementMode()
        {
            isPlacementMode = false;
            exitPlacementTime = Time.time; // Prevent dig trigger for brief moment
            CleanupPreview();
        }

        private void CreatePreview()
        {
            CleanupPreview();

            if (lampPreviewPrefab != null)
            {
                currentPreview = Instantiate(lampPreviewPrefab);
            }
            else
            {
                // Create default preview sphere
                currentPreview = GameObject.CreatePrimitive(PrimitiveType.Sphere);
                currentPreview.name = "LampPreview";
                currentPreview.transform.localScale = Vector3.one * 0.3f;

                // Remove collider
                var collider = currentPreview.GetComponent<Collider>();
                if (collider != null)
                {
                    Destroy(collider);
                }
            }

            // Disable any lights on preview
            foreach (var light in currentPreview.GetComponentsInChildren<Light>())
            {
                light.enabled = false;
            }

            // Disable any PlaceableLamp scripts
            foreach (var lamp in currentPreview.GetComponentsInChildren<PlaceableLamp>())
            {
                lamp.enabled = false;
            }
        }

        private void CleanupPreview()
        {
            if (currentPreview != null)
            {
                Destroy(currentPreview);
                currentPreview = null;
            }
        }

        private void UpdatePlacementPreview()
        {
            if (currentPreview == null || playerCamera == null) return;

            // Raycast from camera center
            Ray ray = playerCamera.ScreenPointToRay(new Vector3(Screen.width / 2f, Screen.height / 2f, 0f));

            // Raycast against all layers to find any surface
            if (Physics.Raycast(ray, out RaycastHit hit, placementDistance))
            {
                // Check if the surface is within the excavation area
                isValidPlacement = IsValidPlacementSurface(hit);

                // Store the surface normal for support detection
                placementNormal = hit.normal;

                // Calculate position offset from surface
                placementPosition = hit.point + hit.normal * surfaceOffset;

                // Orient lamp to face outward from surface (lamp's forward = surface normal)
                // This makes the lamp "stick out" from walls/floors properly
                Vector3 surfaceNormal = hit.normal;
                Vector3 upDirection = Vector3.up;

                // If surface is mostly horizontal (floor/ceiling), lamp faces player
                if (Mathf.Abs(Vector3.Dot(surfaceNormal, Vector3.up)) > 0.7f)
                {
                    // Floor or ceiling - lamp faces toward player camera
                    Vector3 toPlayer = (playerCamera.transform.position - placementPosition).normalized;
                    toPlayer.y = 0; // Keep horizontal
                    if (toPlayer.sqrMagnitude < 0.01f) toPlayer = Vector3.forward;
                    placementRotation = Quaternion.LookRotation(toPlayer, surfaceNormal);
                }
                else
                {
                    // Wall - lamp faces outward from wall
                    placementRotation = Quaternion.LookRotation(surfaceNormal, Vector3.up);
                }

                currentPreview.transform.position = placementPosition;
                currentPreview.transform.rotation = placementRotation;
            }
            else
            {
                // Show preview at max distance
                isValidPlacement = false;
                placementPosition = ray.origin + ray.direction * placementDistance;
                placementRotation = Quaternion.identity;

                currentPreview.transform.position = placementPosition;
                currentPreview.transform.rotation = placementRotation;
            }

            // Update preview material color
            UpdatePreviewMaterial();
        }

        private void UpdatePreviewMaterial()
        {
            if (currentPreview == null) return;

            var renderers = currentPreview.GetComponentsInChildren<MeshRenderer>();
            Material mat = isValidPlacement ? validPlacementMaterial : invalidPlacementMaterial;

            foreach (var renderer in renderers)
            {
                renderer.material = mat;
            }
        }

        /// <summary>
        /// Check if the hit surface is valid for lamp placement.
        /// Only allows placement within the excavation/dig area.
        /// </summary>
        private bool IsValidPlacementSurface(RaycastHit hit)
        {
            Vector3 hitPoint = hit.point;

            // Check if within dig bounds (the excavation area)
            if (DigBoundsProvider.Instance != null && DigBoundsProvider.Instance.HasValidBounds)
            {
                Bounds digBounds = DigBoundsProvider.Instance.DigBounds;

                // Expand bounds slightly to allow wall placement at edges
                Bounds expandedBounds = new Bounds(digBounds.center, digBounds.size + Vector3.one * 0.5f);

                if (expandedBounds.Contains(hitPoint))
                {
                    return true;
                }
            }

            // Fallback: Check if the hit object has IDiggableTerrain or is tagged appropriately
            // This allows placement on the actual terrain mesh chunks
            if (hit.collider != null)
            {
                var diggable = hit.collider.GetComponent<IDiggableTerrain>();
                if (diggable != null)
                {
                    return true;
                }

                // Also check parent (terrain chunks may have colliders on children)
                diggable = hit.collider.GetComponentInParent<IDiggableTerrain>();
                if (diggable != null)
                {
                    return true;
                }

                // Check if object name suggests it's part of the dig terrain
                string objName = hit.collider.gameObject.name.ToLower();
                if (objName.Contains("terrain") || objName.Contains("chunk") || objName.Contains("voxel"))
                {
                    // Additional bounds check for named objects
                    if (DigBoundsProvider.Instance != null && DigBoundsProvider.Instance.HasValidBounds)
                    {
                        return DigBoundsProvider.Instance.DigBounds.Contains(hitPoint);
                    }
                    return true;
                }
            }

            // Not a valid placement surface (basement, first room, etc.)
            return false;
        }

        private void TryPlaceLamp()
        {
            if (!isValidPlacement)
            {
                return;
            }

            if (lampsInInventory <= 0)
            {
                ExitPlacementMode();
                return;
            }

            // Check max lamps
            var lightingSystem = UndergroundLightingSystem.Instance;
            if (lightingSystem != null)
            {
                if (lightingSystem.PlacedLampCount >= lightingSystem.MaxLamps)
                {
                    ExitPlacementMode();
                    return;
                }

                // Place via lighting system (pass the surface normal for support detection)
                if (lightingSystem.PlaceLamp(placementPosition, placementRotation, placementNormal))
                {
                    lampsInInventory--;

                    // Continue placement mode if more lamps available
                    if (lampsInInventory <= 0)
                    {
                        ExitPlacementMode();
                    }
                }
            }
        }

        #region Public API

        /// <summary>
        /// Add lamps to the player's inventory.
        /// </summary>
        public void AddLamps(int count)
        {
            lampsInInventory += count;
        }

        /// <summary>
        /// Purchase a lamp (deducts currency and adds to inventory).
        /// Returns true if purchase was successful.
        /// </summary>
        public bool PurchaseLamp()
        {
            // TODO: Integrate with game's currency system
            // For now, just add the lamp
            AddLamps(1);
            return true;
        }

        /// <summary>
        /// Set the number of lamps in inventory.
        /// </summary>
        public void SetLampCount(int count)
        {
            lampsInInventory = Mathf.Max(0, count);
        }

        /// <summary>
        /// Start lamp placement mode if lamps are available.
        /// </summary>
        public void StartPlacement()
        {
            if (lampsInInventory > 0 && !isPlacementMode)
            {
                EnterPlacementMode();
            }
        }

        /// <summary>
        /// Cancel lamp placement mode.
        /// </summary>
        public void CancelPlacement()
        {
            if (isPlacementMode)
            {
                ExitPlacementMode();
            }
        }

        #endregion

        private void OnGUI()
        {
            if (!isPlacementMode) return;

            // Show placement instructions
            GUILayout.BeginArea(new Rect(Screen.width / 2 - 150, Screen.height - 80, 300, 70));
            GUI.Box(new Rect(0, 0, 300, 70), "");
            GUILayout.Label("LAMP PLACEMENT MODE", new GUIStyle(GUI.skin.label) { alignment = TextAnchor.MiddleCenter, fontStyle = FontStyle.Bold });
            GUILayout.Label($"Lamps: {lampsInInventory}", new GUIStyle(GUI.skin.label) { alignment = TextAnchor.MiddleCenter });
            GUILayout.Label("[LMB/T] Place  |  [RMB/Esc] Cancel", new GUIStyle(GUI.skin.label) { alignment = TextAnchor.MiddleCenter, fontSize = 10 });
            GUILayout.EndArea();
        }
    }
}
