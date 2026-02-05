using UnityEngine;
using UnityEngine.UI;

namespace BeneathTheFloor.Robot
{
    /// <summary>
    /// Blue diamond beacon on the digger robot.
    /// On-screen: small blue diamond icon at the robot's screen position.
    /// Off-screen: blue arrow at screen edge pointing toward the robot, with distance text.
    /// Follows the same pattern as MissionMarkerVisual.
    /// </summary>
    public class DiggerRobotMarker : MonoBehaviour
    {
        [Header("General")]
        [SerializeField] private Color markerColor = new Color(0.3f, 0.55f, 1f, 1f); // blue

        [Header("On-Screen Icon")]
        [SerializeField] private float iconSize = 28f;
        [SerializeField] private float pulseScale = 1.06f;
        [SerializeField] private float pulseSeconds = 1.4f;

        [Header("Off-Screen Arrow")]
        [SerializeField] private float edgeMargin = 36f;
        [SerializeField] private float arrowSize = 32f;
        [SerializeField] private bool showDistance = true;
        [SerializeField] private int distanceFontSize = 16;

        [Header("Proximity")]
        [SerializeField] private float hideDistance = 2.5f;

        // Runtime
        private enum MarkerState { Hidden, OnScreen, OffScreen }
        private MarkerState state = MarkerState.Hidden;

        private Canvas uiCanvas;
        private GameObject onScreenRoot;
        private Image onScreenIcon;
        private GameObject offScreenRoot;
        private Image offScreenArrow;
        private Text offScreenDistText;
        private RectTransform offScreenRootRect;

        private static Texture2D diamondTex;
        private static Texture2D arrowTex;

        private DiggerRobotStateMachine stateMachine;
        private bool visualsCreated;

        private void Awake()
        {
            stateMachine = GetComponent<DiggerRobotStateMachine>();
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

        private void OnDisable() => SetState(MarkerState.Hidden);

        private void OnDestroy()
        {
            if (onScreenRoot != null) Destroy(onScreenRoot);
            if (offScreenRoot != null) Destroy(offScreenRoot);
            if (uiCanvas != null) Destroy(uiCanvas.gameObject);
        }

        private void LateUpdate()
        {
            Camera cam = Camera.main;
            if (cam == null) { SetState(MarkerState.Hidden); return; }

            // Hide when docked or carried
            if (stateMachine != null
                && (stateMachine.CurrentState == DiggerRobotStateMachine.State.Docked
                 || stateMachine.CurrentState == DiggerRobotStateMachine.State.Carried))
            {
                SetState(MarkerState.Hidden);
                return;
            }

            Vector3 targetWorld = transform.position + Vector3.up * 0.5f;
            float dist = Vector3.Distance(cam.transform.position, targetWorld);

            if (dist < hideDistance)
            {
                SetState(MarkerState.Hidden);
                return;
            }

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
        }

        // ---- State transitions ----

        private void SetState(MarkerState newState)
        {
            if (state == newState) return;
            state = newState;

            if (onScreenRoot != null) onScreenRoot.SetActive(newState == MarkerState.OnScreen);
            if (offScreenRoot != null) offScreenRoot.SetActive(newState == MarkerState.OffScreen);
        }

        // ---- On-Screen ----

        private void UpdateOnScreen(Vector3 screenPos)
        {
            if (onScreenIcon == null) return;

            RectTransform rt = onScreenIcon.rectTransform;
            rt.position = new Vector3(screenPos.x, screenPos.y, 0f);

            float t = (Mathf.Sin(Time.time * (2f * Mathf.PI / pulseSeconds)) + 1f) * 0.5f;
            float scale = Mathf.Lerp(1f, pulseScale, t);
            rt.localScale = Vector3.one * scale;

            onScreenIcon.color = markerColor;
        }

        // ---- Off-Screen ----

        private void UpdateOffScreen(Vector3 screenPos, bool behind, float distMeters)
        {
            if (offScreenArrow == null) return;

            Vector2 screenCenter = new Vector2(Screen.width * 0.5f, Screen.height * 0.5f);
            Vector2 dir2D = new Vector2(screenPos.x, screenPos.y) - screenCenter;
            if (behind) dir2D = -dir2D;
            if (dir2D.sqrMagnitude < 0.01f) dir2D = Vector2.up;

            float halfW = Screen.width * 0.5f - edgeMargin;
            float halfH = Screen.height * 0.5f - edgeMargin;

            float absX = Mathf.Abs(dir2D.x);
            float absY = Mathf.Abs(dir2D.y);
            float scaleToEdge;
            if (absX / halfW > absY / halfH)
                scaleToEdge = halfW / Mathf.Max(absX, 0.001f);
            else
                scaleToEdge = halfH / Mathf.Max(absY, 0.001f);

            Vector2 edgePos = screenCenter + dir2D * scaleToEdge;
            offScreenRootRect.position = new Vector3(edgePos.x, edgePos.y, 0f);

            float angle = Mathf.Atan2(dir2D.y, dir2D.x) * Mathf.Rad2Deg;
            offScreenArrow.rectTransform.localRotation = Quaternion.Euler(0f, 0f, angle - 90f);
            offScreenArrow.color = markerColor;

            if (offScreenDistText != null)
            {
                offScreenDistText.gameObject.SetActive(showDistance);
                if (showDistance)
                {
                    offScreenDistText.text = Mathf.RoundToInt(distMeters) + "m";
                    offScreenDistText.color = Color.white;
                }
            }
        }

        // ---- Visual creation ----

        private void CreateAllVisuals()
        {
            CreateUICanvas();
            CreateOnScreenIcon();
            CreateOffScreenArrow();
        }

        private void CreateUICanvas()
        {
            GameObject canvasObj = new GameObject("RobotMarkerCanvas");
            canvasObj.transform.SetParent(transform, false);
            uiCanvas = canvasObj.AddComponent<Canvas>();
            uiCanvas.renderMode = RenderMode.ScreenSpaceOverlay;
            uiCanvas.sortingOrder = 91;
            canvasObj.AddComponent<CanvasScaler>();
        }

        private void CreateOnScreenIcon()
        {
            onScreenRoot = new GameObject("OnScreenDiamond");
            onScreenRoot.transform.SetParent(uiCanvas.transform, false);

            onScreenIcon = onScreenRoot.AddComponent<Image>();
            onScreenIcon.sprite = Sprite.Create(diamondTex,
                new Rect(0, 0, diamondTex.width, diamondTex.height),
                new Vector2(0.5f, 0.5f), 100f);
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

            GameObject arrowObj = new GameObject("Arrow");
            arrowObj.transform.SetParent(offScreenRoot.transform, false);
            offScreenArrow = arrowObj.AddComponent<Image>();
            offScreenArrow.sprite = Sprite.Create(arrowTex,
                new Rect(0, 0, arrowTex.width, arrowTex.height),
                new Vector2(0.5f, 0.5f), 100f);
            offScreenArrow.color = markerColor;
            offScreenArrow.raycastTarget = false;

            RectTransform art = offScreenArrow.rectTransform;
            art.anchoredPosition = Vector2.zero;
            art.sizeDelta = new Vector2(arrowSize, arrowSize);

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
            trt.sizeDelta = new Vector2(80f, 22f);

            offScreenRoot.SetActive(false);
        }

        // ---- Procedural textures ----

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
            float half = size * 0.5f;
            float radius = half - 2f;

            for (int y = 0; y < size; y++)
            {
                for (int x = 0; x < size; x++)
                {
                    float dx = Mathf.Abs(x - half + 0.5f);
                    float dy = Mathf.Abs(y - half + 0.5f);
                    float d = dx + dy;
                    if (d < radius - 1f)
                        tex.SetPixel(x, y, Color.white);
                    else if (d < radius + 1f)
                        tex.SetPixel(x, y, new Color(1, 1, 1, Mathf.Clamp01(1f - (d - (radius - 1f)) / 2f)));
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

            float tipY = 0.88f, baseY = 0.25f, spread = 0.42f, thickness = 0.10f;

            for (int y = 0; y < size; y++)
            {
                for (int x = 0; x < size; x++)
                {
                    float nx = (x + 0.5f) / size;
                    float ny = (y + 0.5f) / size;

                    float t = Mathf.Clamp01((ny - baseY) / (tipY - baseY));
                    float leftX = Mathf.Lerp(0.5f - spread, 0.5f, t);
                    float rightX = Mathf.Lerp(0.5f + spread, 0.5f, t);
                    float minDist = Mathf.Min(Mathf.Abs(nx - leftX), Mathf.Abs(nx - rightX));

                    bool inY = ny >= baseY && ny <= tipY;
                    if (inY && minDist < thickness)
                    {
                        float a = 1f - Mathf.Clamp01((minDist - thickness + 0.03f) / 0.03f);
                        tex.SetPixel(x, y, new Color(1, 1, 1, a));
                    }
                    else
                        tex.SetPixel(x, y, clear);
                }
            }
            tex.Apply();
            return tex;
        }
    }
}
