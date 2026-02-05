using UnityEngine;
using UnityEngine.UI;

namespace BeneathTheFloor.GameFlow
{
    /// <summary>
    /// Enhanced floating marker that guides players to objectives.
    /// Features: diamond marker, light beam beacon, ground ring, and off-screen arrow.
    /// </summary>
    public class FloatingObjectiveMarker : MonoBehaviour
    {
        [Header("Diamond Settings")]
        [SerializeField] private Color markerColor = new Color(1f, 0.8f, 0.2f, 1f);
        [SerializeField] private float markerSize = 0.4f;
        [SerializeField] private float glowIntensity = 2f;

        [Header("Animation")]
        [SerializeField] private float bobSpeed = 2f;
        [SerializeField] private float bobHeight = 0.12f;
        [SerializeField] private float rotationSpeed = 60f;
        [SerializeField] private float pulseSpeed = 2f;
        [SerializeField] private float pulseIntensity = 0.4f;

        [Header("Position")]
        [SerializeField] private float heightAboveTarget = 1.5f;

        [Header("Light Beam")]
        [SerializeField] private float beamHeight = 15f;
        [SerializeField] private float beamWidth = 0.3f;
        [SerializeField] private Color beamColor = new Color(1f, 0.9f, 0.4f, 0.4f);

        [Header("Ground Ring")]
        [SerializeField] private float ringRadius = 1.2f;
        [SerializeField] private float ringWidth = 0.15f;
        [SerializeField] private Color ringColor = new Color(1f, 0.85f, 0.3f, 0.6f);
        [SerializeField] private float ringShowDistance = 8f;

        [Header("Off-Screen Arrow")]
        [SerializeField] private float arrowSize = 40f;
        [SerializeField] private float arrowEdgeOffset = 60f;
        [SerializeField] private Color arrowColor = new Color(1f, 0.85f, 0.3f, 0.9f);

        // Runtime
        private Transform targetTransform;
        private Vector3 basePosition;
        private float bobOffset;

        // Visual components
        private GameObject diamondVisual;
        private GameObject glowVisual;
        private GameObject beamVisual;
        private GameObject groundRingVisual;
        private Material diamondMaterial;
        private Material glowMaterial;
        private Material beamMaterial;
        private Material ringMaterial;

        // UI components
        private Canvas uiCanvas;
        private RectTransform arrowRect;
        private Image arrowImage;

        // References
        private Camera mainCamera;
        private Transform playerTransform;

        public static FloatingObjectiveMarker Instance { get; private set; }

        private void Awake()
        {
            if (Instance != null && Instance != this)
            {
                Destroy(gameObject);
                return;
            }
            Instance = this;
        }

        private void Start()
        {
            bobOffset = Random.Range(0f, Mathf.PI * 2f);
            mainCamera = Camera.main;

            // Find player
            var player = FindObjectOfType<Player.FirstPersonController>();
            if (player != null)
                playerTransform = player.transform;
            else if (mainCamera != null)
                playerTransform = mainCamera.transform;

            // Always recreate visuals (handles code updates / hot reload)
            CleanupOldVisuals();
            CreateAllVisuals();
            CreateUIArrow();

            Debug.Log("[FloatingObjectiveMarker] Marker initialized with beam, ring, and arrow features");

            // Start hidden
            gameObject.SetActive(false);
        }

        private void CleanupOldVisuals()
        {
            // Destroy any existing child visuals
            if (diamondVisual != null) Destroy(diamondVisual);
            if (beamVisual != null) Destroy(beamVisual);
            if (groundRingVisual != null) Destroy(groundRingVisual);
            if (arrowImage != null) Destroy(arrowImage.gameObject);

            // Also destroy any unnamed children (old visuals from previous code)
            for (int i = transform.childCount - 1; i >= 0; i--)
            {
                Destroy(transform.GetChild(i).gameObject);
            }
        }

        private void CreateAllVisuals()
        {
            CreateDiamondMarker();
            CreateLightBeam();
            CreateGroundRing();
        }

        private void CreateDiamondMarker()
        {
            // Main diamond container
            diamondVisual = new GameObject("DiamondMarker");
            diamondVisual.transform.SetParent(transform, false);
            diamondVisual.transform.localPosition = Vector3.zero;

            // Create diamond shape using two pyramids (cubes rotated)
            GameObject topPyramid = GameObject.CreatePrimitive(PrimitiveType.Cube);
            topPyramid.name = "TopPyramid";
            topPyramid.transform.SetParent(diamondVisual.transform, false);
            topPyramid.transform.localScale = new Vector3(markerSize * 0.7f, markerSize * 0.5f, markerSize * 0.7f);
            topPyramid.transform.localPosition = Vector3.up * markerSize * 0.2f;
            topPyramid.transform.localRotation = Quaternion.Euler(0, 45, 0);
            Destroy(topPyramid.GetComponent<Collider>());

            GameObject bottomPyramid = GameObject.CreatePrimitive(PrimitiveType.Cube);
            bottomPyramid.name = "BottomPyramid";
            bottomPyramid.transform.SetParent(diamondVisual.transform, false);
            bottomPyramid.transform.localScale = new Vector3(markerSize * 0.7f, markerSize * 0.5f, markerSize * 0.7f);
            bottomPyramid.transform.localPosition = Vector3.down * markerSize * 0.2f;
            bottomPyramid.transform.localRotation = Quaternion.Euler(0, 45, 0);
            Destroy(bottomPyramid.GetComponent<Collider>());

            // Create emissive material
            diamondMaterial = new Material(Shader.Find("Sprites/Default"));
            diamondMaterial.color = markerColor;
            topPyramid.GetComponent<MeshRenderer>().material = diamondMaterial;
            bottomPyramid.GetComponent<MeshRenderer>().material = diamondMaterial;

            // Outer glow sphere
            glowVisual = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            glowVisual.name = "DiamondGlow";
            glowVisual.transform.SetParent(diamondVisual.transform, false);
            glowVisual.transform.localPosition = Vector3.zero;
            glowVisual.transform.localScale = Vector3.one * markerSize * 2.5f;
            Destroy(glowVisual.GetComponent<Collider>());

            glowMaterial = new Material(Shader.Find("Sprites/Default"));
            Color glowCol = markerColor;
            glowCol.a = 0.25f;
            glowMaterial.color = glowCol;
            glowVisual.GetComponent<MeshRenderer>().material = glowMaterial;
        }

        private void CreateLightBeam()
        {
            beamVisual = new GameObject("LightBeam");
            beamVisual.transform.SetParent(transform, false);
            beamVisual.transform.localPosition = Vector3.up * (beamHeight * 0.5f - heightAboveTarget);

            // Create beam using a stretched cube
            GameObject beamMesh = GameObject.CreatePrimitive(PrimitiveType.Cube);
            beamMesh.name = "BeamMesh";
            beamMesh.transform.SetParent(beamVisual.transform, false);
            beamMesh.transform.localScale = new Vector3(beamWidth, beamHeight, beamWidth);
            beamMesh.transform.localPosition = Vector3.zero;
            Destroy(beamMesh.GetComponent<Collider>());

            // Use additive-like material for the beam
            beamMaterial = new Material(Shader.Find("Sprites/Default"));
            beamMaterial.color = beamColor;
            beamMesh.GetComponent<MeshRenderer>().material = beamMaterial;

            // Add a second, wider, more transparent beam for glow effect
            GameObject beamGlow = GameObject.CreatePrimitive(PrimitiveType.Cube);
            beamGlow.name = "BeamGlow";
            beamGlow.transform.SetParent(beamVisual.transform, false);
            beamGlow.transform.localScale = new Vector3(beamWidth * 2.5f, beamHeight, beamWidth * 2.5f);
            beamGlow.transform.localPosition = Vector3.zero;
            Destroy(beamGlow.GetComponent<Collider>());

            Material beamGlowMat = new Material(Shader.Find("Sprites/Default"));
            Color beamGlowCol = beamColor;
            beamGlowCol.a = 0.15f;
            beamGlowMat.color = beamGlowCol;
            beamGlow.GetComponent<MeshRenderer>().material = beamGlowMat;
        }

        private void CreateGroundRing()
        {
            groundRingVisual = new GameObject("GroundRing");
            groundRingVisual.transform.SetParent(transform, false);

            // Create a true hollow ring mesh (flat annulus)
            GameObject ringObj = new GameObject("HollowRing");
            ringObj.transform.SetParent(groundRingVisual.transform, false);
            ringObj.transform.localPosition = Vector3.zero;

            MeshFilter meshFilter = ringObj.AddComponent<MeshFilter>();
            meshFilter.mesh = CreateFlatRingMesh(ringRadius, ringWidth, 48);

            MeshRenderer renderer = ringObj.AddComponent<MeshRenderer>();
            ringMaterial = new Material(Shader.Find("Sprites/Default"));
            ringMaterial.color = ringColor;
            renderer.material = ringMaterial;

            // Start hidden
            groundRingVisual.SetActive(false);
        }

        private Mesh CreateFlatRingMesh(float outerRadius, float frameWidth, int segments)
        {
            Mesh mesh = new Mesh();

            // Inner radius creates the hollow center
            float innerRadius = outerRadius - frameWidth;
            // Ensure minimum hollow center (at least 70% of outer radius)
            innerRadius = Mathf.Max(innerRadius, outerRadius * 0.7f);

            int vertCount = segments * 2;
            Vector3[] vertices = new Vector3[vertCount];
            int[] triangles = new int[segments * 6];

            for (int i = 0; i < segments; i++)
            {
                float angle = (float)i / segments * Mathf.PI * 2f;
                float cos = Mathf.Cos(angle);
                float sin = Mathf.Sin(angle);

                // Inner vertex
                vertices[i * 2] = new Vector3(cos * innerRadius, 0, sin * innerRadius);
                // Outer vertex
                vertices[i * 2 + 1] = new Vector3(cos * outerRadius, 0, sin * outerRadius);
            }

            int triIndex = 0;
            for (int i = 0; i < segments; i++)
            {
                int nextI = (i + 1) % segments;

                int innerCurrent = i * 2;
                int outerCurrent = i * 2 + 1;
                int innerNext = nextI * 2;
                int outerNext = nextI * 2 + 1;

                // Triangle 1
                triangles[triIndex++] = innerCurrent;
                triangles[triIndex++] = outerCurrent;
                triangles[triIndex++] = outerNext;

                // Triangle 2
                triangles[triIndex++] = innerCurrent;
                triangles[triIndex++] = outerNext;
                triangles[triIndex++] = innerNext;
            }

            mesh.vertices = vertices;
            mesh.triangles = triangles;
            mesh.RecalculateNormals();

            return mesh;
        }

        private void CreateUIArrow()
        {
            // Find or create UI canvas
            uiCanvas = FindObjectOfType<Canvas>();
            if (uiCanvas == null)
            {
                GameObject canvasObj = new GameObject("MarkerUICanvas");
                uiCanvas = canvasObj.AddComponent<Canvas>();
                uiCanvas.renderMode = RenderMode.ScreenSpaceOverlay;
                uiCanvas.sortingOrder = 200;
                canvasObj.AddComponent<CanvasScaler>();
                canvasObj.AddComponent<GraphicRaycaster>();
            }

            // Create arrow image
            GameObject arrowObj = new GameObject("OffScreenArrow");
            arrowObj.transform.SetParent(uiCanvas.transform, false);

            arrowRect = arrowObj.AddComponent<RectTransform>();
            arrowRect.sizeDelta = new Vector2(arrowSize, arrowSize);

            arrowImage = arrowObj.AddComponent<Image>();
            arrowImage.color = arrowColor;

            // Create arrow texture procedurally
            arrowImage.sprite = CreateArrowSprite();
            arrowImage.raycastTarget = false;

            arrowObj.SetActive(false);
        }

        private Sprite CreateArrowSprite()
        {
            int size = 64;
            Texture2D texture = new Texture2D(size, size, TextureFormat.RGBA32, false);
            texture.filterMode = FilterMode.Bilinear;

            Color transparent = new Color(0, 0, 0, 0);
            Color[] pixels = new Color[size * size];

            // Fill with transparent
            for (int i = 0; i < pixels.Length; i++)
                pixels[i] = transparent;

            // Draw arrow pointing right (will be rotated in code)
            int centerY = size / 2;
            int arrowTip = size - 8;
            int arrowBack = 8;

            for (int x = arrowBack; x <= arrowTip; x++)
            {
                float progress = (float)(x - arrowBack) / (arrowTip - arrowBack);
                int halfHeight = Mathf.RoundToInt((1f - progress) * (size / 3));

                for (int y = centerY - halfHeight; y <= centerY + halfHeight; y++)
                {
                    if (y >= 0 && y < size)
                    {
                        pixels[y * size + x] = Color.white;
                    }
                }
            }

            texture.SetPixels(pixels);
            texture.Apply();

            return Sprite.Create(texture, new Rect(0, 0, size, size), new Vector2(0.5f, 0.5f));
        }

        private void Update()
        {
            if (targetTransform == null) return;

            UpdatePosition();
            UpdateAnimations();
            UpdateGroundRing();
            UpdateOffScreenArrow();
        }

        private void UpdatePosition()
        {
            // Raycast to find floor level
            Vector3 floorPosition = targetTransform.position;
            RaycastHit hit;
            Vector3 rayStart = new Vector3(targetTransform.position.x, targetTransform.position.y + 5f, targetTransform.position.z);
            if (Physics.Raycast(rayStart, Vector3.down, out hit, 20f))
            {
                floorPosition = hit.point;
            }
            else
            {
                floorPosition.y = 0f;
            }

            // Position diamond above the floor
            basePosition = floorPosition + Vector3.up * (heightAboveTarget + 1f);

            // Bob animation
            float bob = Mathf.Sin((Time.time + bobOffset) * bobSpeed) * bobHeight;
            transform.position = basePosition + Vector3.up * bob;

            // Ground ring at floor level
            if (groundRingVisual != null)
            {
                groundRingVisual.transform.position = floorPosition + Vector3.up * 0.05f;
            }

            // Beam from floor up
            if (beamVisual != null)
            {
                beamVisual.transform.position = floorPosition + Vector3.up * (beamHeight * 0.5f);
            }
        }

        private void UpdateAnimations()
        {
            float pulse = 1f + Mathf.Sin(Time.time * pulseSpeed) * pulseIntensity;
            float glowPulse = 0.5f + Mathf.Sin(Time.time * pulseSpeed) * 0.3f;

            // Rotate diamond
            if (diamondVisual != null)
            {
                diamondVisual.transform.Rotate(Vector3.up, rotationSpeed * Time.deltaTime, Space.World);
            }

            // Pulse glow
            if (glowVisual != null && glowMaterial != null)
            {
                glowVisual.transform.localScale = Vector3.one * markerSize * 2.5f * pulse;
                Color c = markerColor;
                c.a = 0.2f + glowPulse * 0.15f;
                glowMaterial.color = c;
            }

            // Pulse diamond color for emissive effect
            if (diamondMaterial != null)
            {
                Color c = markerColor * (1f + glowPulse * (glowIntensity - 1f));
                c.a = 1f;
                diamondMaterial.color = c;
            }

            // Pulse beam
            if (beamMaterial != null)
            {
                Color c = beamColor;
                c.a = beamColor.a * (0.6f + Mathf.Sin(Time.time * pulseSpeed * 0.7f) * 0.4f);
                beamMaterial.color = c;
            }

            // Pulse ring
            if (ringMaterial != null && groundRingVisual.activeSelf)
            {
                Color c = ringColor;
                c.a = ringColor.a * (0.7f + Mathf.Sin(Time.time * pulseSpeed * 1.2f) * 0.3f);
                ringMaterial.color = c;

                // Slowly rotate ring
                groundRingVisual.transform.Rotate(Vector3.up, 20f * Time.deltaTime);
            }
        }

        private void UpdateGroundRing()
        {
            if (groundRingVisual == null || playerTransform == null) return;

            float distance = Vector3.Distance(playerTransform.position, targetTransform.position);
            bool shouldShow = distance < ringShowDistance;

            if (groundRingVisual.activeSelf != shouldShow)
            {
                groundRingVisual.SetActive(shouldShow);
            }

            // Scale ring based on distance (larger when closer)
            if (shouldShow)
            {
                float scale = Mathf.Lerp(1.5f, 0.8f, distance / ringShowDistance);
                groundRingVisual.transform.localScale = Vector3.one * scale;
            }
        }

        private void UpdateOffScreenArrow()
        {
            if (arrowImage == null || mainCamera == null) return;

            Vector3 targetScreenPos = mainCamera.WorldToScreenPoint(targetTransform.position);

            // Check if target is behind camera or off screen
            bool isBehind = targetScreenPos.z < 0;
            bool isOffScreen = isBehind ||
                targetScreenPos.x < 0 || targetScreenPos.x > Screen.width ||
                targetScreenPos.y < 0 || targetScreenPos.y > Screen.height;

            arrowImage.gameObject.SetActive(isOffScreen);

            if (isOffScreen)
            {
                // If behind, flip the position
                if (isBehind)
                {
                    targetScreenPos.x = Screen.width - targetScreenPos.x;
                    targetScreenPos.y = Screen.height - targetScreenPos.y;
                }

                // Calculate direction from screen center to target
                Vector2 screenCenter = new Vector2(Screen.width * 0.5f, Screen.height * 0.5f);
                Vector2 direction = ((Vector2)targetScreenPos - screenCenter).normalized;

                // Calculate angle for arrow rotation
                float angle = Mathf.Atan2(direction.y, direction.x) * Mathf.Rad2Deg;
                arrowRect.rotation = Quaternion.Euler(0, 0, angle);

                // Position arrow at screen edge
                Vector2 edgePos = screenCenter + direction * Mathf.Min(
                    (Screen.width * 0.5f - arrowEdgeOffset) / Mathf.Abs(direction.x + 0.001f),
                    (Screen.height * 0.5f - arrowEdgeOffset) / Mathf.Abs(direction.y + 0.001f)
                ) * 0.9f;

                // Clamp to screen bounds
                edgePos.x = Mathf.Clamp(edgePos.x, arrowEdgeOffset, Screen.width - arrowEdgeOffset);
                edgePos.y = Mathf.Clamp(edgePos.y, arrowEdgeOffset, Screen.height - arrowEdgeOffset);

                arrowRect.position = edgePos;

                // Pulse arrow alpha
                Color c = arrowColor;
                c.a = arrowColor.a * (0.7f + Mathf.Sin(Time.time * 3f) * 0.3f);
                arrowImage.color = c;
            }
        }

        /// <summary>
        /// Show the marker above the specified target.
        /// </summary>
        public void ShowAbove(Transform target)
        {
            if (target == null)
            {
                Debug.LogWarning("[FloatingObjectiveMarker] Cannot show - target is null");
                return;
            }

            targetTransform = target;
            basePosition = target.position + Vector3.up * heightAboveTarget;
            transform.position = basePosition;
            gameObject.SetActive(true);

            // Refresh camera reference
            if (mainCamera == null)
                mainCamera = Camera.main;
        }

        /// <summary>
        /// Hide the marker.
        /// </summary>
        public void Hide()
        {
            gameObject.SetActive(false);
            targetTransform = null;

            if (arrowImage != null)
                arrowImage.gameObject.SetActive(false);
        }

        /// <summary>
        /// Create a marker instance if one doesn't exist.
        /// </summary>
        public static FloatingObjectiveMarker GetOrCreate()
        {
            if (Instance != null)
            {
                // Ensure visuals exist (handles code updates)
                Instance.EnsureVisualsExist();
                return Instance;
            }

            GameObject markerObj = new GameObject("FloatingObjectiveMarker");
            var marker = markerObj.AddComponent<FloatingObjectiveMarker>();
            return marker;
        }

        /// <summary>
        /// Ensure all visual components exist (recreate if missing).
        /// </summary>
        private void EnsureVisualsExist()
        {
            // Check if key visuals are missing
            if (diamondVisual == null || beamVisual == null || groundRingVisual == null)
            {
                Debug.Log("[FloatingObjectiveMarker] Recreating visuals (missing components detected)");
                CleanupOldVisuals();
                CreateAllVisuals();
                CreateUIArrow();
            }
        }

        private void OnDestroy()
        {
            if (Instance == this)
                Instance = null;

            // Clean up materials
            if (diamondMaterial != null) Destroy(diamondMaterial);
            if (glowMaterial != null) Destroy(glowMaterial);
            if (beamMaterial != null) Destroy(beamMaterial);
            if (ringMaterial != null) Destroy(ringMaterial);

            // Clean up arrow
            if (arrowImage != null) Destroy(arrowImage.gameObject);
        }

        private void OnDisable()
        {
            // Hide arrow when marker is disabled
            if (arrowImage != null)
                arrowImage.gameObject.SetActive(false);
        }
    }
}
