using UnityEngine;
using UnityEngine.UI;

namespace BeneathTheFloor.Missions
{
    /// <summary>
    /// Place this component on an empty GameObject in the scene to create a mission marker.
    /// Two states:
    ///   On-Screen  — small UI diamond icon + optional thin floor ring in world space
    ///   Off-Screen — edge-of-screen arrow pointing toward target + optional distance text
    /// Link this object to a mission in MissionManager's Mission Markers list.
    /// </summary>
    public class MissionMarkerVisual : MonoBehaviour
    {
        [Header("General")]
        [SerializeField] private Color markerColor = new Color(1f, 0.8f, 0.2f, 1f);

        [Header("On-Screen Icon")]
        [Tooltip("Size of the on-screen diamond icon in pixels.")]
        [SerializeField] private float iconSize = 42f;
        [Tooltip("Screen-space pixel offset for the diamond icon (x = right, y = up).")]
        [SerializeField] private Vector2 iconScreenOffset = Vector2.zero;
        [Tooltip("Gentle pulse: scale oscillates 1.0 → this value over pulseSeconds.")]
        [SerializeField] private float pulseScale = 1.08f;
        [SerializeField] private float pulseSeconds = 1.2f;

        [Header("Ground Ring (World-Space)")]
        [SerializeField] private bool showRing = true;
        [SerializeField] private float ringRadius = 0.8f;
        [SerializeField] private float ringWidth = 0.08f;
        [SerializeField] private Color ringColor = new Color(1f, 0.85f, 0.3f, 0.6f);

        [Header("Off-Screen Arrow")]
        [Tooltip("Pixel margin from screen edge for the arrow indicator.")]
        [SerializeField] private float edgeMargin = 40f;
        [SerializeField] private float arrowSize = 40f;
        [SerializeField] private bool showDistance = true;
        [SerializeField] private int distanceFontSize = 18;

        [Header("Proximity")]
        [Tooltip("Hide marker completely when closer than this distance (meters).")]
        [SerializeField] private float hideDistance = 3f;

        [Header("Line-of-Sight (Optional)")]
        [SerializeField] private bool enableLOSFade = false;
        [Tooltip("Layer mask for LOS raycast.")]
        [SerializeField] private LayerMask losLayers = 1;
        [Tooltip("Alpha multiplier when LOS is blocked.")]
        [SerializeField] private float occludedAlpha = 0.3f;

        // ---- Runtime ----
        private enum MarkerState { Hidden, OnScreen, OffScreen }
        private MarkerState state = MarkerState.Hidden;
        private float currentAlpha = 1f;

        // World-space ground ring
        private GameObject groundRingVisual;
        private Material ringMaterial;

        // UI overlay (created once via a shared canvas)
        private Canvas uiCanvas;
        private RectTransform canvasRect;
        private GameObject onScreenRoot;   // diamond icon
        private Image onScreenIcon;
        private GameObject offScreenRoot;  // arrow + distance
        private Image offScreenArrow;
        private Text offScreenDistText;
        private RectTransform offScreenRootRect;

        // Textures (procedural)
        private static Texture2D diamondTex;
        private static Texture2D arrowTex;

        private bool visualsCreated = false;

        // ============================================================
        //  Lifecycle
        // ============================================================

        private void Awake()
        {
            EnsureTextures();
        }

        private void OnEnable()
        {
            if (!visualsCreated)
            {
                CreateAllVisuals();
                visualsCreated = true;
            }
            SetState(MarkerState.Hidden);
        }

        private void OnDisable()
        {
            SetState(MarkerState.Hidden);
        }

        private void OnDestroy()
        {
            if (ringMaterial != null) Destroy(ringMaterial);
            if (onScreenRoot != null) Destroy(onScreenRoot);
            if (offScreenRoot != null) Destroy(offScreenRoot);
            if (uiCanvas != null) Destroy(uiCanvas.gameObject);
        }

        // ============================================================
        //  Update
        // ============================================================

        private void LateUpdate()
        {
            Camera cam = Camera.main;
            if (cam == null) { SetState(MarkerState.Hidden); return; }

            Vector3 targetWorld = transform.position;
            float dist = Vector3.Distance(cam.transform.position, targetWorld);

            // --- Proximity hide ---
            if (dist < hideDistance)
            {
                SetState(MarkerState.Hidden);
                return;
            }

            // --- LOS fade ---
            currentAlpha = 1f;
            if (enableLOSFade)
            {
                Vector3 dir = targetWorld - cam.transform.position;
                if (Physics.Raycast(cam.transform.position, dir.normalized, dir.magnitude, losLayers, QueryTriggerInteraction.Ignore))
                    currentAlpha = occludedAlpha;
            }

            // --- Screen-space detection ---
            Vector3 screenPos = cam.WorldToScreenPoint(targetWorld);
            bool behind = screenPos.z <= 0f;
            bool inScreen = !behind
                && screenPos.x >= 0 && screenPos.x <= Screen.width
                && screenPos.y >= 0 && screenPos.y <= Screen.height;

            if (inScreen)
            {
                SetState(MarkerState.OnScreen);
                UpdateOnScreen(screenPos);
            }
            else
            {
                SetState(MarkerState.OffScreen);
                UpdateOffScreen(screenPos, behind, dist);
            }

            // --- Ground ring animation ---
            UpdateGroundRing();
        }

        // ============================================================
        //  State transitions
        // ============================================================

        private void SetState(MarkerState newState)
        {
            if (state == newState) return;
            state = newState;

            bool on = newState == MarkerState.OnScreen;
            bool off = newState == MarkerState.OffScreen;

            if (onScreenRoot != null) onScreenRoot.SetActive(on);
            if (offScreenRoot != null) offScreenRoot.SetActive(off);
            if (groundRingVisual != null) groundRingVisual.SetActive(on && showRing);
        }

        // ============================================================
        //  On-Screen: diamond icon projected onto screen pos
        // ============================================================

        private void UpdateOnScreen(Vector3 screenPos)
        {
            if (onScreenIcon == null) return;

            // Position the icon at the screen-space location
            RectTransform rt = onScreenIcon.rectTransform;
            rt.position = new Vector3(screenPos.x + iconScreenOffset.x, screenPos.y + iconScreenOffset.y, 0f);

            // Gentle pulse
            float t = (Mathf.Sin(Time.time * (2f * Mathf.PI / pulseSeconds)) + 1f) * 0.5f; // 0→1
            float scale = Mathf.Lerp(1f, pulseScale, t);
            rt.localScale = Vector3.one * scale;

            // Alpha
            Color c = markerColor;
            c.a = currentAlpha;
            onScreenIcon.color = c;
        }

        // ============================================================
        //  Off-Screen: arrow at edge of screen
        // ============================================================

        private void UpdateOffScreen(Vector3 screenPos, bool behind, float distMeters)
        {
            if (offScreenArrow == null) return;

            Vector2 screenCenter = new Vector2(Screen.width * 0.5f, Screen.height * 0.5f);
            Vector2 dir2D = new Vector2(screenPos.x, screenPos.y) - screenCenter;

            if (behind) dir2D = -dir2D; // flip when behind camera

            if (dir2D.sqrMagnitude < 0.01f) dir2D = Vector2.up;

            // Clamp to screen edge with margin
            float halfW = Screen.width * 0.5f - edgeMargin;
            float halfH = Screen.height * 0.5f - edgeMargin;

            // Find where the direction ray hits the screen-edge rect
            float absX = Mathf.Abs(dir2D.x);
            float absY = Mathf.Abs(dir2D.y);
            float scaleToEdge;
            if (absX / halfW > absY / halfH)
                scaleToEdge = halfW / Mathf.Max(absX, 0.001f);
            else
                scaleToEdge = halfH / Mathf.Max(absY, 0.001f);

            Vector2 edgePos = screenCenter + dir2D * scaleToEdge;

            // Apply position
            offScreenRootRect.position = new Vector3(edgePos.x, edgePos.y, 0f);

            // Rotate arrow to point in direction
            float angle = Mathf.Atan2(dir2D.y, dir2D.x) * Mathf.Rad2Deg;
            offScreenArrow.rectTransform.localRotation = Quaternion.Euler(0f, 0f, angle - 90f); // -90 because arrow points up by default

            // Alpha
            Color c = markerColor;
            c.a = currentAlpha;
            offScreenArrow.color = c;

            // Distance text
            if (offScreenDistText != null)
            {
                offScreenDistText.gameObject.SetActive(showDistance);
                if (showDistance)
                {
                    offScreenDistText.text = Mathf.RoundToInt(distMeters) + "m";
                    Color tc = Color.white;
                    tc.a = currentAlpha;
                    offScreenDistText.color = tc;
                }
            }
        }

        // ============================================================
        //  Ground ring (world-space, thin, transparent)
        // ============================================================

        private void UpdateGroundRing()
        {
            if (groundRingVisual == null || !groundRingVisual.activeSelf) return;
            if (ringMaterial == null) return;

            // Gentle alpha pulse
            float t = (Mathf.Sin(Time.time * (2f * Mathf.PI / pulseSeconds) * 1.2f) + 1f) * 0.5f;
            Color c = ringColor;
            c.a = Mathf.Lerp(ringColor.a * 0.7f, ringColor.a, t) * currentAlpha;
            ringMaterial.color = c;

            // Slow rotation
            groundRingVisual.transform.Rotate(Vector3.up, 20f * Time.deltaTime);
        }

        // ============================================================
        //  Visual creation
        // ============================================================

        private void CreateAllVisuals()
        {
            CreateUICanvas();
            CreateOnScreenIcon();
            CreateOffScreenArrow();
            if (showRing) CreateGroundRing();
        }

        private void CreateUICanvas()
        {
            // Screen-space overlay canvas for the marker UI
            GameObject canvasObj = new GameObject("MarkerCanvas_" + gameObject.name);
            canvasObj.transform.SetParent(transform, false);
            uiCanvas = canvasObj.AddComponent<Canvas>();
            uiCanvas.renderMode = RenderMode.ScreenSpaceOverlay;
            uiCanvas.sortingOrder = 90;
            canvasObj.AddComponent<CanvasScaler>();

            canvasRect = uiCanvas.GetComponent<RectTransform>();
        }

        private void CreateOnScreenIcon()
        {
            onScreenRoot = new GameObject("OnScreenIcon");
            onScreenRoot.transform.SetParent(uiCanvas.transform, false);

            onScreenIcon = onScreenRoot.AddComponent<Image>();
            onScreenIcon.sprite = Sprite.Create(diamondTex, new Rect(0, 0, diamondTex.width, diamondTex.height), new Vector2(0.5f, 0.5f), 100f);
            onScreenIcon.color = markerColor;
            onScreenIcon.raycastTarget = false;

            RectTransform rt = onScreenIcon.rectTransform;
            rt.sizeDelta = new Vector2(iconSize, iconSize);
            rt.anchorMin = Vector2.zero;
            rt.anchorMax = Vector2.zero;
            rt.pivot = new Vector2(0.5f, 0.5f);

            onScreenRoot.SetActive(false);
        }

        private void CreateOffScreenArrow()
        {
            offScreenRoot = new GameObject("OffScreenArrow");
            offScreenRoot.transform.SetParent(uiCanvas.transform, false);

            offScreenRootRect = offScreenRoot.AddComponent<RectTransform>();
            offScreenRootRect.anchorMin = Vector2.zero;
            offScreenRootRect.anchorMax = Vector2.zero;
            offScreenRootRect.pivot = new Vector2(0.5f, 0.5f);
            offScreenRootRect.sizeDelta = new Vector2(arrowSize, arrowSize);

            // Arrow image
            GameObject arrowObj = new GameObject("Arrow");
            arrowObj.transform.SetParent(offScreenRoot.transform, false);
            offScreenArrow = arrowObj.AddComponent<Image>();
            offScreenArrow.sprite = Sprite.Create(arrowTex, new Rect(0, 0, arrowTex.width, arrowTex.height), new Vector2(0.5f, 0.5f), 100f);
            offScreenArrow.color = markerColor;
            offScreenArrow.raycastTarget = false;

            RectTransform art = offScreenArrow.rectTransform;
            art.anchoredPosition = Vector2.zero;
            art.sizeDelta = new Vector2(arrowSize, arrowSize);

            // Distance text
            GameObject textObj = new GameObject("DistText");
            textObj.transform.SetParent(offScreenRoot.transform, false);
            offScreenDistText = textObj.AddComponent<Text>();
            offScreenDistText.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            if (offScreenDistText.font == null)
                offScreenDistText.font = Resources.GetBuiltinResource<Font>("Arial.ttf");
            offScreenDistText.fontSize = distanceFontSize;
            offScreenDistText.alignment = TextAnchor.MiddleCenter;
            offScreenDistText.color = Color.white;
            offScreenDistText.raycastTarget = false;
            offScreenDistText.horizontalOverflow = HorizontalWrapMode.Overflow;

            RectTransform trt = offScreenDistText.rectTransform;
            trt.anchoredPosition = new Vector2(0f, -arrowSize * 0.7f);
            trt.sizeDelta = new Vector2(80f, 24f);

            offScreenRoot.SetActive(false);
        }

        private void CreateGroundRing()
        {
            groundRingVisual = new GameObject("GroundRing");
            groundRingVisual.transform.SetParent(transform, false);
            groundRingVisual.transform.localPosition = Vector3.up * 0.05f;

            GameObject ringObj = new GameObject("HollowRing");
            ringObj.transform.SetParent(groundRingVisual.transform, false);
            ringObj.transform.localPosition = Vector3.zero;

            MeshFilter meshFilter = ringObj.AddComponent<MeshFilter>();
            meshFilter.mesh = CreateFlatRingMesh(ringRadius, ringWidth, 48);

            MeshRenderer renderer = ringObj.AddComponent<MeshRenderer>();
            ringMaterial = new Material(Shader.Find("Sprites/Default"));
            ringMaterial.color = ringColor;
            renderer.material = ringMaterial;

            groundRingVisual.SetActive(false);
        }

        // ============================================================
        //  Procedural textures (shared across all instances)
        // ============================================================

        private static void EnsureTextures()
        {
            if (diamondTex == null) diamondTex = CreateDiamondTexture(64);
            if (arrowTex == null) arrowTex = CreateArrowTexture(64);
        }

        private static Texture2D CreateDiamondTexture(int size)
        {
            Texture2D tex = new Texture2D(size, size, TextureFormat.RGBA32, false);
            tex.filterMode = FilterMode.Bilinear;
            Color clear = new Color(1, 1, 1, 0);
            Color white = Color.white;

            float half = size * 0.5f;
            float radius = half - 2f; // slight margin for AA

            for (int y = 0; y < size; y++)
            {
                for (int x = 0; x < size; x++)
                {
                    // Diamond = rotated square: |x-center| + |y-center| <= radius
                    float dx = Mathf.Abs(x - half + 0.5f);
                    float dy = Mathf.Abs(y - half + 0.5f);
                    float d = dx + dy;
                    float edge = radius;
                    if (d < edge - 1f)
                        tex.SetPixel(x, y, white);
                    else if (d < edge + 1f)
                    {
                        float a = 1f - (d - (edge - 1f)) / 2f;
                        tex.SetPixel(x, y, new Color(1, 1, 1, Mathf.Clamp01(a)));
                    }
                    else
                        tex.SetPixel(x, y, clear);
                }
            }

            tex.Apply();
            return tex;
        }

        private static Texture2D CreateArrowTexture(int size)
        {
            Texture2D tex = new Texture2D(size, size, TextureFormat.RGBA32, false);
            tex.filterMode = FilterMode.Bilinear;
            Color clear = new Color(1, 1, 1, 0);
            Color white = Color.white;

            // Arrow pointing UP: triangle occupying the texture
            float half = size * 0.5f;
            for (int y = 0; y < size; y++)
            {
                for (int x = 0; x < size; x++)
                {
                    // Normalized coords 0→1
                    float nx = (x + 0.5f) / size;
                    float ny = (y + 0.5f) / size;

                    // Chevron shape: two lines meeting at top-center
                    // Top point at (0.5, 0.9), left base at (0.1, 0.3), right base at (0.9, 0.3)
                    float tipY = 0.88f;
                    float baseY = 0.25f;
                    float spread = 0.42f;
                    float thickness = 0.10f;

                    // Left edge line from (0.5-spread, baseY) to (0.5, tipY)
                    // Right edge line from (0.5+spread, baseY) to (0.5, tipY)
                    float tLeft = Mathf.Clamp01((ny - baseY) / (tipY - baseY));
                    float leftLineX = Mathf.Lerp(0.5f - spread, 0.5f, tLeft);
                    float distLeft = Mathf.Abs(nx - leftLineX);

                    float tRight = tLeft;
                    float rightLineX = Mathf.Lerp(0.5f + spread, 0.5f, tRight);
                    float distRight = Mathf.Abs(nx - rightLineX);

                    float minDist = Mathf.Min(distLeft, distRight);

                    bool inYRange = ny >= baseY && ny <= tipY;
                    if (inYRange && minDist < thickness)
                    {
                        float a = 1f - Mathf.Clamp01((minDist - thickness + 0.03f) / 0.03f);
                        tex.SetPixel(x, y, new Color(1, 1, 1, a));
                    }
                    else
                    {
                        tex.SetPixel(x, y, clear);
                    }
                }
            }

            tex.Apply();
            return tex;
        }

        // ============================================================
        //  Mesh helpers (unchanged)
        // ============================================================

        private Mesh CreateFlatRingMesh(float outerRadius, float frameWidth, int segments)
        {
            Mesh mesh = new Mesh();

            float innerRadius = outerRadius - frameWidth;
            innerRadius = Mathf.Max(innerRadius, outerRadius * 0.7f);

            int vertCount = segments * 2;
            Vector3[] vertices = new Vector3[vertCount];
            int[] triangles = new int[segments * 6];

            for (int i = 0; i < segments; i++)
            {
                float angle = (float)i / segments * Mathf.PI * 2f;
                float cos = Mathf.Cos(angle);
                float sin = Mathf.Sin(angle);

                vertices[i * 2] = new Vector3(cos * innerRadius, 0, sin * innerRadius);
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

                triangles[triIndex++] = innerCurrent;
                triangles[triIndex++] = outerCurrent;
                triangles[triIndex++] = outerNext;

                triangles[triIndex++] = innerCurrent;
                triangles[triIndex++] = outerNext;
                triangles[triIndex++] = innerNext;
            }

            mesh.vertices = vertices;
            mesh.triangles = triangles;
            mesh.RecalculateNormals();

            return mesh;
        }

        // ============================================================
        //  Editor gizmos (unchanged)
        // ============================================================

        private void OnDrawGizmos()
        {
            Vector3 diamondWorldPos = GetGizmoDiamondPosition();

            // Diamond icon gizmo
            Gizmos.color = new Color(1f, 0.8f, 0.2f, 0.9f);
            DrawGizmoDiamond(diamondWorldPos, 0.25f);

            // Line from ground to diamond
            Gizmos.color = new Color(1f, 0.8f, 0.2f, 0.3f);
            Gizmos.DrawLine(transform.position, diamondWorldPos);

            if (showRing)
            {
                Gizmos.color = new Color(1f, 0.85f, 0.3f, 0.4f);
                DrawGizmoRing(transform.position + Vector3.up * 0.05f, ringRadius, 32);
            }
        }

        /// <summary>
        /// Convert the screen-space icon offset into an approximate world position
        /// so the gizmo reflects where the diamond will appear at runtime.
        /// </summary>
        private Vector3 GetGizmoDiamondPosition()
        {
            Vector3 baseWorld = transform.position + Vector3.up * 0.5f;

#if UNITY_EDITOR
            Camera sceneCam = UnityEditor.SceneView.lastActiveSceneView != null
                ? UnityEditor.SceneView.lastActiveSceneView.camera
                : null;

            if (sceneCam != null && (iconScreenOffset.x != 0f || iconScreenOffset.y != 0f))
            {
                // Convert pixel offset to world offset using camera's orientation
                // Scale factor: approximate world units per pixel at the marker's distance
                float dist = Vector3.Distance(sceneCam.transform.position, baseWorld);
                float worldPerPixel = dist * 2f * Mathf.Tan(sceneCam.fieldOfView * 0.5f * Mathf.Deg2Rad) / sceneCam.pixelHeight;

                Vector3 camRight = sceneCam.transform.right;
                Vector3 camUp = sceneCam.transform.up;

                baseWorld += camRight * (iconScreenOffset.x * worldPerPixel);
                baseWorld += camUp * (iconScreenOffset.y * worldPerPixel);
            }
#endif
            return baseWorld;
        }

        private static void DrawGizmoDiamond(Vector3 center, float size)
        {
            Vector3 top = center + Vector3.up * size;
            Vector3 bottom = center - Vector3.up * size;
            Vector3 right = center + Vector3.right * size * 0.6f;
            Vector3 left = center - Vector3.right * size * 0.6f;
            Vector3 forward = center + Vector3.forward * size * 0.6f;
            Vector3 back = center - Vector3.forward * size * 0.6f;

            // Top half
            Gizmos.DrawLine(top, right);
            Gizmos.DrawLine(top, left);
            Gizmos.DrawLine(top, forward);
            Gizmos.DrawLine(top, back);
            // Bottom half
            Gizmos.DrawLine(bottom, right);
            Gizmos.DrawLine(bottom, left);
            Gizmos.DrawLine(bottom, forward);
            Gizmos.DrawLine(bottom, back);
            // Equator
            Gizmos.DrawLine(right, forward);
            Gizmos.DrawLine(forward, left);
            Gizmos.DrawLine(left, back);
            Gizmos.DrawLine(back, right);
        }

        private void DrawGizmoRing(Vector3 center, float radius, int segments)
        {
            Vector3 prevPoint = center + new Vector3(radius, 0, 0);
            for (int i = 1; i <= segments; i++)
            {
                float angle = (float)i / segments * Mathf.PI * 2f;
                Vector3 point = center + new Vector3(Mathf.Cos(angle) * radius, 0, Mathf.Sin(angle) * radius);
                Gizmos.DrawLine(prevPoint, point);
                prevPoint = point;
            }
        }

        private void OnDrawGizmosSelected()
        {
            Vector3 diamondWorldPos = GetGizmoDiamondPosition();
            Gizmos.color = Color.yellow;
            DrawGizmoDiamond(diamondWorldPos, 0.35f);
        }
    }
}
