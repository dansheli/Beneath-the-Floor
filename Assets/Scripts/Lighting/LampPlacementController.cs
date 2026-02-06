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

        [Header("Hologram Settings")]
        [SerializeField] private Color hologramColor = new Color(0.3f, 0.7f, 1.0f, 0.35f);
        [SerializeField] private float hologramEmissionIntensity = 1.5f;
        [SerializeField] private float flickerSpeed = 15f;
        [SerializeField] private float flickerAlphaMin = 0.05f;
        [SerializeField] private float flickerAlphaMax = 0.3f;
        [SerializeField] private float jitterAmount = 0.02f;

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
        private Material hologramMaterial;
        private Vector3 previewBasePosition; // For jitter offset
        private Vector3 previewOriginalScale; // Preserve prefab scale

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

            if (hologramMaterial != null)
            {
                Destroy(hologramMaterial);
            }
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
            hologramMaterial = new Material(Shader.Find("Universal Render Pipeline/Lit"));
            hologramMaterial.SetFloat("_Surface", 1); // Transparent
            hologramMaterial.SetFloat("_Blend", 0); // Alpha
            hologramMaterial.SetFloat("_AlphaClip", 0);
            hologramMaterial.SetOverrideTag("RenderType", "Transparent");
            hologramMaterial.SetInt("_SrcBlend", (int)UnityEngine.Rendering.BlendMode.SrcAlpha);
            hologramMaterial.SetInt("_DstBlend", (int)UnityEngine.Rendering.BlendMode.OneMinusSrcAlpha);
            hologramMaterial.SetInt("_ZWrite", 0);
            hologramMaterial.renderQueue = 3000;
            hologramMaterial.EnableKeyword("_SURFACE_TYPE_TRANSPARENT");
            hologramMaterial.EnableKeyword("_EMISSION");

            Color emissionColor = new Color(hologramColor.r, hologramColor.g, hologramColor.b) * hologramEmissionIntensity;
            hologramMaterial.SetColor("_BaseColor", hologramColor);
            hologramMaterial.SetColor("_EmissionColor", emissionColor);
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

            // Disable all colliders so preview doesn't block raycasts
            foreach (var col in currentPreview.GetComponentsInChildren<Collider>())
            {
                col.enabled = false;
            }

            previewOriginalScale = currentPreview.transform.localScale;
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

            // Raycast from camera center - ignore triggers
            Ray ray = playerCamera.ScreenPointToRay(new Vector3(Screen.width / 2f, Screen.height / 2f, 0f));

            if (Physics.Raycast(ray, out RaycastHit hit, placementDistance, ~0, QueryTriggerInteraction.Ignore))
            {
                // Check if the surface is within the excavation area
                isValidPlacement = IsValidPlacementSurface(hit);

                // Store the surface normal for support detection
                placementNormal = hit.normal;

                // Calculate position offset from surface
                placementPosition = hit.point + hit.normal * surfaceOffset;

                // Ground-only: lamp sits upright, facing player
                Vector3 toPlayer = (playerCamera.transform.position - placementPosition).normalized;
                toPlayer.y = 0;
                if (toPlayer.sqrMagnitude < 0.01f) toPlayer = Vector3.forward;
                placementRotation = Quaternion.LookRotation(toPlayer, Vector3.up);

                previewBasePosition = placementPosition;
                currentPreview.transform.position = placementPosition;
                currentPreview.transform.rotation = placementRotation;
            }
            else
            {
                // Show preview at max distance
                isValidPlacement = false;
                placementPosition = ray.origin + ray.direction * placementDistance;
                placementRotation = Quaternion.identity;

                previewBasePosition = placementPosition;
                currentPreview.transform.position = placementPosition;
                currentPreview.transform.rotation = placementRotation;
            }

            // Update hologram material and effects
            UpdatePreviewMaterial();

            // Apply glitch jitter when invalid
            if (!isValidPlacement && currentPreview != null)
            {
                float jx = (Mathf.PerlinNoise(Time.time * 25f, 0f) - 0.5f) * 2f * jitterAmount;
                float jy = (Mathf.PerlinNoise(0f, Time.time * 30f) - 0.5f) * 2f * jitterAmount;
                float jz = (Mathf.PerlinNoise(Time.time * 20f, Time.time * 10f) - 0.5f) * 2f * jitterAmount;
                currentPreview.transform.position = previewBasePosition + new Vector3(jx, jy, jz);

                float scaleJitter = 1f + (Mathf.PerlinNoise(Time.time * 18f, 5f) - 0.5f) * 0.06f;
                currentPreview.transform.localScale = previewOriginalScale * scaleJitter;
            }
            else if (currentPreview != null)
            {
                currentPreview.transform.localScale = previewOriginalScale;
            }
        }

        private void UpdatePreviewMaterial()
        {
            if (currentPreview == null || hologramMaterial == null) return;

            float alpha;
            if (isValidPlacement)
            {
                // Stable blue hologram
                alpha = hologramColor.a;
            }
            else
            {
                // Flickering broken hologram
                float flicker = Mathf.Sin(Time.time * flickerSpeed) * 0.5f + 0.5f;
                float noise = Mathf.PerlinNoise(Time.time * 8f, 3.7f);
                float combined = flicker * 0.6f + noise * 0.4f;
                alpha = Mathf.Lerp(flickerAlphaMin, flickerAlphaMax, combined);
            }

            Color baseColor = new Color(hologramColor.r, hologramColor.g, hologramColor.b, alpha);
            hologramMaterial.SetColor("_BaseColor", baseColor);

            var renderers = currentPreview.GetComponentsInChildren<MeshRenderer>();
            foreach (var renderer in renderers)
            {
                // Apply hologram material to all submeshes
                var mats = new Material[renderer.sharedMaterials.Length];
                for (int i = 0; i < mats.Length; i++)
                    mats[i] = hologramMaterial;
                renderer.materials = mats;
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
