using UnityEngine;

namespace BeneathTheFloor.UI
{
    /// <summary>
    /// A subtle ring-only marker for mission objectives.
    /// No diamond, no beam - just a gentle rotating ring.
    /// </summary>
    public class SubtleRingMarker : MonoBehaviour
    {
        [Header("Ring Settings")]
        [Tooltip("Inner radius of the ring")]
        [SerializeField] private float innerRadius = 0.4f;

        [Tooltip("Outer radius of the ring")]
        [SerializeField] private float outerRadius = 0.5f;

        [Tooltip("Number of segments in the ring")]
        [SerializeField] private int segments = 32;

        [Header("Animation")]
        [Tooltip("Rotation speed (degrees per second)")]
        [SerializeField] private float rotationSpeed = 30f;

        [Tooltip("Gentle bob amplitude")]
        [SerializeField] private float bobAmplitude = 0.05f;

        [Tooltip("Bob speed")]
        [SerializeField] private float bobSpeed = 1.5f;

        [Tooltip("Pulse scale amount")]
        [SerializeField] private float pulseAmount = 0.05f;

        [Tooltip("Pulse speed")]
        [SerializeField] private float pulseSpeed = 2f;

        [Header("Appearance")]
        [Tooltip("Ring color")]
        [SerializeField] private Color ringColor = new Color(0.8f, 0.9f, 1f, 0.6f);

        [Tooltip("Height offset above target")]
        [SerializeField] private float heightOffset = 0f;

        // Components
        private MeshFilter meshFilter;
        private MeshRenderer meshRenderer;
        private Transform targetTransform;
        private Vector3 basePosition;
        private float baseScale = 1f;

        // State
        private bool isVisible = false;

        public static SubtleRingMarker Instance { get; private set; }

        private void Awake()
        {
            if (Instance == null) Instance = this;
            else { Destroy(gameObject); return; }

            CreateRingMesh();
            Hide();
        }

        private void OnDestroy()
        {
            if (Instance == this) Instance = null;
        }

        private void Update()
        {
            if (!isVisible) return;

            // Follow target if set
            if (targetTransform != null)
            {
                basePosition = targetTransform.position + Vector3.up * heightOffset;
            }

            // Rotation
            transform.Rotate(Vector3.up, rotationSpeed * Time.deltaTime);

            // Gentle bob
            float bob = Mathf.Sin(Time.time * bobSpeed) * bobAmplitude;
            transform.position = basePosition + Vector3.up * bob;

            // Subtle pulse
            float pulse = 1f + Mathf.Sin(Time.time * pulseSpeed) * pulseAmount;
            transform.localScale = Vector3.one * baseScale * pulse;
        }

        private void CreateRingMesh()
        {
            // Create mesh components
            meshFilter = gameObject.AddComponent<MeshFilter>();
            meshRenderer = gameObject.AddComponent<MeshRenderer>();

            // Create ring mesh
            Mesh mesh = new Mesh();
            mesh.name = "SubtleRingMesh";

            int vertCount = segments * 2;
            Vector3[] vertices = new Vector3[vertCount];
            int[] triangles = new int[segments * 6];
            Color[] colors = new Color[vertCount];

            for (int i = 0; i < segments; i++)
            {
                float angle = (float)i / segments * Mathf.PI * 2f;
                float cos = Mathf.Cos(angle);
                float sin = Mathf.Sin(angle);

                // Inner vertex
                vertices[i * 2] = new Vector3(cos * innerRadius, 0, sin * innerRadius);
                // Outer vertex
                vertices[i * 2 + 1] = new Vector3(cos * outerRadius, 0, sin * outerRadius);

                // Colors with slight fade at edges
                colors[i * 2] = ringColor;
                colors[i * 2 + 1] = new Color(ringColor.r, ringColor.g, ringColor.b, ringColor.a * 0.7f);

                // Triangles
                int next = (i + 1) % segments;
                int triIndex = i * 6;

                triangles[triIndex] = i * 2;
                triangles[triIndex + 1] = next * 2;
                triangles[triIndex + 2] = i * 2 + 1;

                triangles[triIndex + 3] = next * 2;
                triangles[triIndex + 4] = next * 2 + 1;
                triangles[triIndex + 5] = i * 2 + 1;
            }

            mesh.vertices = vertices;
            mesh.triangles = triangles;
            mesh.colors = colors;
            mesh.RecalculateNormals();
            mesh.RecalculateBounds();

            meshFilter.mesh = mesh;

            // Create material - try URP shader first, then fallback
            Shader shader = Shader.Find("Universal Render Pipeline/Unlit");
            if (shader == null)
            {
                shader = Shader.Find("Unlit/Color");
            }
            if (shader == null)
            {
                shader = Shader.Find("Sprites/Default");
            }

            Material mat = new Material(shader);
            mat.color = ringColor;

            // Enable transparency
            mat.SetFloat("_Surface", 1); // 0 = Opaque, 1 = Transparent (URP)
            mat.SetFloat("_Blend", 0); // Alpha blend
            mat.SetInt("_SrcBlend", (int)UnityEngine.Rendering.BlendMode.SrcAlpha);
            mat.SetInt("_DstBlend", (int)UnityEngine.Rendering.BlendMode.OneMinusSrcAlpha);
            mat.SetInt("_ZWrite", 0);
            mat.renderQueue = 3000;
            mat.EnableKeyword("_SURFACE_TYPE_TRANSPARENT");

            meshRenderer.material = mat;
            meshRenderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            meshRenderer.receiveShadows = false;

            Debug.Log($"[SubtleRingMarker] Created with shader: {shader?.name ?? "NULL"}");
        }

        /// <summary>
        /// Show the ring marker above a target.
        /// </summary>
        public void ShowAbove(Transform target, float scale = 1f)
        {
            targetTransform = target;
            baseScale = scale;

            if (target != null)
            {
                basePosition = target.position + Vector3.up * heightOffset;
                transform.position = basePosition;
                Debug.Log($"[SubtleRingMarker] ShowAbove: {target.name} at {basePosition}");
            }
            else
            {
                Debug.LogWarning("[SubtleRingMarker] ShowAbove called with null target!");
            }

            transform.localScale = Vector3.one * baseScale;
            gameObject.SetActive(true);
            isVisible = true;
        }

        /// <summary>
        /// Show the ring marker at a specific position.
        /// </summary>
        public void ShowAt(Vector3 position, float scale = 1f)
        {
            targetTransform = null;
            basePosition = position;
            baseScale = scale;

            transform.position = basePosition;
            transform.localScale = Vector3.one * baseScale;
            gameObject.SetActive(true);
            isVisible = true;
        }

        /// <summary>
        /// Hide the marker.
        /// </summary>
        public void Hide()
        {
            Debug.Log($"[SubtleRingMarker] Hide() called. Was visible: {isVisible}, GameObject: {gameObject.name}");
            isVisible = false;
            targetTransform = null;
            gameObject.SetActive(false);
        }

        /// <summary>
        /// Set the ring color.
        /// </summary>
        public void SetColor(Color color)
        {
            ringColor = color;
            if (meshRenderer != null && meshRenderer.material != null)
            {
                meshRenderer.material.color = color;
            }
        }

        /// <summary>
        /// Get or create the singleton instance.
        /// </summary>
        public static SubtleRingMarker GetOrCreate()
        {
            if (Instance != null) return Instance;

            GameObject obj = new GameObject("SubtleRingMarker");
            DontDestroyOnLoad(obj);
            return obj.AddComponent<SubtleRingMarker>();
        }
    }
}
