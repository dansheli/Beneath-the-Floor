using UnityEngine;
using UnityEngine.UI;
using BeneathTheFloor.GameFlow;

namespace BeneathTheFloor.UI
{
    /// <summary>
    /// Enhanced floating diamond marker that guides players to objectives.
    /// Features: diamond with glow, light beam, ground ring, off-screen arrow.
    /// Automatically hides when the associated ReadableNote is read.
    /// </summary>
    public class ObjectiveMarker : MonoBehaviour
    {
        [Header("Diamond Settings")]
        [SerializeField] private float floatHeight = 1.5f;
        [SerializeField] private float floatAmplitude = 0.12f;
        [SerializeField] private float floatSpeed = 1.8f;
        [SerializeField] private float rotationSpeed = 45f;
        [SerializeField] private float diamondSize = 0.35f;
        [SerializeField] private float glowIntensity = 1.5f;

        [Header("Colors")]
        [SerializeField] private Color markerColor = new Color(1f, 0.85f, 0.3f, 0.9f);

        [Header("Light Beam")]
        [SerializeField] private bool enableBeam = true;
        [SerializeField] private float beamHeight = 12f;
        [SerializeField] private float beamWidth = 0.08f;
        [SerializeField] private Color beamColor = new Color(1f, 0.9f, 0.5f, 0.18f);

        [Header("Ground Ring")]
        [SerializeField] private bool enableGroundRing = true;
        [SerializeField] private float ringRadius = 1.2f;
        [SerializeField] private float ringFrameWidth = 0.08f;
        [SerializeField] private Color ringColor = new Color(1f, 0.9f, 0.4f, 0.7f);
        [SerializeField] private float ringShowDistance = 8f;

        [Header("Off-Screen Arrow")]
        [SerializeField] private bool enableOffScreenArrow = true;
        [SerializeField] private float arrowSize = 32f;
        [SerializeField] private float arrowDistanceFromCenter = 60f;
        [SerializeField] private Color arrowColor = new Color(1f, 0.85f, 0.4f, 0.75f);

        [Header("Target")]
        [SerializeField] private ReadableNote targetNote;

        // Visual components
        private GameObject diamondObject;
        private GameObject glowObject;
        private GameObject beamVisual;
        private GameObject groundRingVisual;
        private MeshRenderer meshRenderer;
        private Material diamondMaterial;
        private Material glowMaterial;
        private Material beamMaterial;
        private Material ringMaterial;

        // UI components
        private Canvas uiCanvas;
        private RectTransform arrowRect;
        private Image arrowImage;

        // State
        private float baseY;
        private float timeOffset;
        private Camera mainCamera;
        private Transform playerTransform;

        private void Start()
        {
            mainCamera = Camera.main;

            // Find player
            var player = GameObject.FindGameObjectWithTag("Player");
            if (player != null)
                playerTransform = player.transform;
            else if (mainCamera != null)
                playerTransform = mainCamera.transform;

            // Find ReadableNote if not assigned
            if (targetNote == null)
            {
                targetNote = GetComponentInParent<ReadableNote>();
                if (targetNote == null)
                {
                    targetNote = FindObjectOfType<ReadableNote>();
                }
            }

            // Subscribe to note read event
            if (targetNote != null)
            {
                targetNote.OnNoteRead += OnNoteRead;

                // Hide if already read
                if (targetNote.HasBeenRead)
                {
                    gameObject.SetActive(false);
                    return;
                }
            }

            CreateAllVisuals();
            baseY = transform.position.y + floatHeight;
            timeOffset = Random.value * Mathf.PI * 2f;

            Debug.Log("[ObjectiveMarker] Enhanced marker initialized with beam, ring, and arrow");
        }

        private void OnDestroy()
        {
            if (targetNote != null)
            {
                targetNote.OnNoteRead -= OnNoteRead;
            }

            // Clean up materials
            if (diamondMaterial != null) Destroy(diamondMaterial);
            if (glowMaterial != null) Destroy(glowMaterial);
            if (beamMaterial != null) Destroy(beamMaterial);
            if (ringMaterial != null) Destroy(ringMaterial);

            // Clean up arrow
            if (arrowImage != null) Destroy(arrowImage.gameObject);

            // Clean up unparented objects
            if (groundRingVisual != null) Destroy(groundRingVisual);
            if (beamVisual != null) Destroy(beamVisual);
        }

        private void Update()
        {
            if (diamondObject == null) return;

            UpdateDiamondAnimation();
            UpdateBeamAnimation();
            UpdateGroundRing();
            UpdateOffScreenArrow();
        }

        private void CreateAllVisuals()
        {
            CreateDiamondMarker();
            if (enableBeam) CreateLightBeam();
            if (enableGroundRing) CreateGroundRing();
            if (enableOffScreenArrow) CreateUIArrow();
        }

        private void CreateDiamondMarker()
        {
            diamondObject = new GameObject("DiamondMarker");
            diamondObject.transform.SetParent(transform);
            diamondObject.transform.localPosition = new Vector3(0, floatHeight, 0);

            // Create diamond mesh (octahedron)
            MeshFilter meshFilter = diamondObject.AddComponent<MeshFilter>();
            meshFilter.mesh = CreateDiamondMesh();

            meshRenderer = diamondObject.AddComponent<MeshRenderer>();

            // Create material for diamond
            diamondMaterial = new Material(Shader.Find("Sprites/Default"));
            diamondMaterial.color = markerColor;
            meshRenderer.material = diamondMaterial;

            // Add subtle glow sphere
            glowObject = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            glowObject.name = "DiamondGlow";
            glowObject.transform.SetParent(diamondObject.transform);
            glowObject.transform.localPosition = Vector3.zero;
            glowObject.transform.localScale = Vector3.one * diamondSize * 2f;
            Destroy(glowObject.GetComponent<Collider>());

            glowMaterial = new Material(Shader.Find("Sprites/Default"));
            Color glowCol = markerColor;
            glowCol.a = 0.15f;
            glowMaterial.color = glowCol;
            glowObject.GetComponent<MeshRenderer>().material = glowMaterial;
        }

        private void CreateLightBeam()
        {
            beamVisual = new GameObject("LightBeam");
            // Don't parent to marker - position at ground level like the ring
            Vector3 groundPos = GetGroundPosition();
            beamVisual.transform.position = groundPos + Vector3.up * (beamHeight * 0.5f);

            // Create thin cylindrical beam core
            GameObject beamMesh = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
            beamMesh.name = "BeamCore";
            beamMesh.transform.SetParent(beamVisual.transform, false);
            beamMesh.transform.localScale = new Vector3(beamWidth, beamHeight * 0.5f, beamWidth);
            beamMesh.transform.localPosition = Vector3.zero;
            Destroy(beamMesh.GetComponent<Collider>());

            beamMaterial = new Material(Shader.Find("Sprites/Default"));
            beamMaterial.color = beamColor;
            beamMesh.GetComponent<MeshRenderer>().material = beamMaterial;

            // Subtle outer glow cylinder
            GameObject beamGlow = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
            beamGlow.name = "BeamGlow";
            beamGlow.transform.SetParent(beamVisual.transform, false);
            beamGlow.transform.localScale = new Vector3(beamWidth * 3f, beamHeight * 0.5f, beamWidth * 3f);
            beamGlow.transform.localPosition = Vector3.zero;
            Destroy(beamGlow.GetComponent<Collider>());

            Material beamGlowMat = new Material(Shader.Find("Sprites/Default"));
            Color beamGlowCol = beamColor;
            beamGlowCol.a = 0.06f;
            beamGlowMat.color = beamGlowCol;
            beamGlow.GetComponent<MeshRenderer>().material = beamGlowMat;
        }

        private void CreateGroundRing()
        {
            groundRingVisual = new GameObject("GroundRing");
            // Don't parent to marker - we'll position it at ground level
            groundRingVisual.transform.position = GetGroundPosition();

            // Create flat hollow ring (annulus) - just an outline frame
            GameObject ringObj = new GameObject("HollowRing");
            ringObj.transform.SetParent(groundRingVisual.transform, false);
            ringObj.transform.localPosition = Vector3.zero;

            MeshFilter meshFilter = ringObj.AddComponent<MeshFilter>();
            meshFilter.mesh = CreateFlatRingMesh(ringRadius, ringFrameWidth, 48);

            MeshRenderer renderer = ringObj.AddComponent<MeshRenderer>();
            ringMaterial = new Material(Shader.Find("Sprites/Default"));
            ringMaterial.color = ringColor;
            renderer.material = ringMaterial;

            // Start hidden
            groundRingVisual.SetActive(false);
        }

        private Vector3 GetGroundPosition()
        {
            // Raycast down from marker to find ground
            Vector3 origin = transform.position + Vector3.up * 0.5f;
            if (Physics.Raycast(origin, Vector3.down, out RaycastHit hit, 10f))
            {
                return hit.point + Vector3.up * 0.01f; // Slightly above ground
            }
            // Fallback: assume ground is at y=0 below marker
            return new Vector3(transform.position.x, 0.01f, transform.position.z);
        }

        private Mesh CreateFlatRingMesh(float outerRadius, float frameWidth, int segments)
        {
            Mesh mesh = new Mesh();

            // Ensure inner radius is at least 70% of outer radius for a clearly hollow ring
            float innerRadius = Mathf.Max(outerRadius - frameWidth, outerRadius * 0.7f);
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

        private Mesh CreateTorusMesh(float radius, float tubeRadius, int radialSegments, int tubeSegments)
        {
            Mesh mesh = new Mesh();

            int vertCount = radialSegments * tubeSegments;
            Vector3[] vertices = new Vector3[vertCount];
            int[] triangles = new int[radialSegments * tubeSegments * 6];

            for (int i = 0; i < radialSegments; i++)
            {
                float radialAngle = (float)i / radialSegments * Mathf.PI * 2f;
                Vector3 radialDir = new Vector3(Mathf.Cos(radialAngle), 0, Mathf.Sin(radialAngle));
                Vector3 center = radialDir * radius;

                for (int j = 0; j < tubeSegments; j++)
                {
                    float tubeAngle = (float)j / tubeSegments * Mathf.PI * 2f;
                    Vector3 tubeOffset = radialDir * Mathf.Cos(tubeAngle) * tubeRadius +
                                        Vector3.up * Mathf.Sin(tubeAngle) * tubeRadius;

                    vertices[i * tubeSegments + j] = center + tubeOffset;
                }
            }

            int triIndex = 0;
            for (int i = 0; i < radialSegments; i++)
            {
                int nextI = (i + 1) % radialSegments;
                for (int j = 0; j < tubeSegments; j++)
                {
                    int nextJ = (j + 1) % tubeSegments;

                    int v0 = i * tubeSegments + j;
                    int v1 = nextI * tubeSegments + j;
                    int v2 = nextI * tubeSegments + nextJ;
                    int v3 = i * tubeSegments + nextJ;

                    triangles[triIndex++] = v0;
                    triangles[triIndex++] = v1;
                    triangles[triIndex++] = v2;

                    triangles[triIndex++] = v0;
                    triangles[triIndex++] = v2;
                    triangles[triIndex++] = v3;
                }
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

            for (int i = 0; i < pixels.Length; i++)
                pixels[i] = transparent;

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

        private Mesh CreateDiamondMesh()
        {
            Mesh mesh = new Mesh();

            float halfSize = diamondSize * 0.5f;
            float height = diamondSize;

            Vector3[] vertices = new Vector3[]
            {
                new Vector3(0, height * 0.5f, 0),    // Top
                new Vector3(halfSize, 0, 0),         // Right
                new Vector3(0, 0, halfSize),         // Front
                new Vector3(-halfSize, 0, 0),        // Left
                new Vector3(0, 0, -halfSize),        // Back
                new Vector3(0, -height * 0.5f, 0)    // Bottom
            };

            int[] triangles = new int[]
            {
                0, 1, 2, 0, 2, 3, 0, 3, 4, 0, 4, 1,  // Top pyramid
                5, 2, 1, 5, 3, 2, 5, 4, 3, 5, 1, 4   // Bottom pyramid
            };

            mesh.vertices = vertices;
            mesh.triangles = triangles;
            mesh.RecalculateNormals();

            return mesh;
        }

        private void UpdateDiamondAnimation()
        {
            // Subtle pulse (reduced intensity)
            float pulse = 1f + Mathf.Sin(Time.time * 1.5f) * 0.15f;
            float glowPulse = 0.5f + Mathf.Sin(Time.time * 1.5f) * 0.2f;

            // Gentle float up and down
            float newY = baseY + Mathf.Sin((Time.time + timeOffset) * floatSpeed) * floatAmplitude;
            diamondObject.transform.position = new Vector3(
                transform.position.x,
                newY,
                transform.position.z
            );

            // Smooth rotation
            diamondObject.transform.Rotate(Vector3.up, rotationSpeed * Time.deltaTime);

            // Subtle glow pulse
            if (glowObject != null && glowMaterial != null)
            {
                glowObject.transform.localScale = Vector3.one * diamondSize * 2f * pulse;
                Color c = markerColor;
                c.a = 0.1f + glowPulse * 0.08f;
                glowMaterial.color = c;
            }

            // Gentle diamond brightness pulse
            if (diamondMaterial != null)
            {
                Color c = markerColor * (1f + glowPulse * (glowIntensity - 1f) * 0.5f);
                c.a = markerColor.a;
                diamondMaterial.color = c;
            }
        }

        private void UpdateBeamAnimation()
        {
            if (!enableBeam || beamMaterial == null || beamVisual == null) return;

            // Keep beam at ground level, centered under marker
            Vector3 groundPos = GetGroundPosition();
            beamVisual.transform.position = new Vector3(
                transform.position.x,
                groundPos.y + beamHeight * 0.5f,
                transform.position.z
            );

            // Soft, slow pulse for the beam
            float pulse = 0.7f + Mathf.Sin(Time.time * 0.8f) * 0.3f;
            Color c = beamColor;
            c.a = beamColor.a * pulse;
            beamMaterial.color = c;
        }

        private void UpdateGroundRing()
        {
            if (!enableGroundRing || groundRingVisual == null || playerTransform == null) return;

            float distance = Vector3.Distance(playerTransform.position, transform.position);
            bool shouldShow = distance < ringShowDistance;

            if (groundRingVisual.activeSelf != shouldShow)
            {
                groundRingVisual.SetActive(shouldShow);
            }

            if (shouldShow)
            {
                // Keep ring centered under marker but at ground level
                Vector3 groundPos = GetGroundPosition();
                groundRingVisual.transform.position = new Vector3(
                    transform.position.x,
                    groundPos.y,
                    transform.position.z
                );

                // Subtle scale based on distance (closer = slightly larger)
                float scale = Mathf.Lerp(1.2f, 0.9f, distance / ringShowDistance);
                groundRingVisual.transform.localScale = Vector3.one * scale;

                // Gentle ring pulse
                if (ringMaterial != null)
                {
                    float pulse = 0.8f + Mathf.Sin(Time.time * 1.2f) * 0.2f;
                    Color c = ringColor;
                    c.a = ringColor.a * pulse;
                    ringMaterial.color = c;
                }

                // Slow rotation
                groundRingVisual.transform.Rotate(Vector3.up, 15f * Time.deltaTime);
            }
        }

        private void UpdateOffScreenArrow()
        {
            if (!enableOffScreenArrow || arrowImage == null || mainCamera == null) return;

            Vector3 targetScreenPos = mainCamera.WorldToScreenPoint(transform.position);

            bool isBehind = targetScreenPos.z < 0;
            bool isOffScreen = isBehind ||
                targetScreenPos.x < 0 || targetScreenPos.x > Screen.width ||
                targetScreenPos.y < 0 || targetScreenPos.y > Screen.height;

            arrowImage.gameObject.SetActive(isOffScreen);

            if (isOffScreen)
            {
                if (isBehind)
                {
                    targetScreenPos.x = Screen.width - targetScreenPos.x;
                    targetScreenPos.y = Screen.height - targetScreenPos.y;
                }

                Vector2 screenCenter = new Vector2(Screen.width * 0.5f, Screen.height * 0.5f);
                Vector2 direction = ((Vector2)targetScreenPos - screenCenter).normalized;

                float angle = Mathf.Atan2(direction.y, direction.x) * Mathf.Rad2Deg;
                arrowRect.rotation = Quaternion.Euler(0, 0, angle);

                // Position arrow close to center, just below crosshair
                Vector2 arrowPos = screenCenter + direction * arrowDistanceFromCenter;
                arrowRect.position = arrowPos;

                Color c = arrowColor;
                c.a = arrowColor.a * (0.7f + Mathf.Sin(Time.time * 3f) * 0.3f);
                arrowImage.color = c;
            }
        }

        private void OnNoteRead()
        {
            StartCoroutine(FadeOutAndDestroy());
        }

        private System.Collections.IEnumerator FadeOutAndDestroy()
        {
            float duration = 0.5f;
            float elapsed = 0f;

            Color startColor = markerColor;

            while (elapsed < duration)
            {
                elapsed += Time.deltaTime;
                float t = elapsed / duration;

                if (diamondMaterial != null)
                {
                    Color c = startColor;
                    c.a = 1f - t;
                    diamondMaterial.color = c;
                }

                if (diamondObject != null)
                {
                    diamondObject.transform.localScale = Vector3.one * (1f - t * 0.5f);
                }

                yield return null;
            }

            // Hide arrow
            if (arrowImage != null)
                arrowImage.gameObject.SetActive(false);

            // Clean up
            if (groundRingVisual != null) Destroy(groundRingVisual);
            if (beamVisual != null) Destroy(beamVisual);
            Destroy(diamondObject);
            Destroy(gameObject);
        }

        private void OnDisable()
        {
            if (arrowImage != null)
                arrowImage.gameObject.SetActive(false);
        }
    }
}
