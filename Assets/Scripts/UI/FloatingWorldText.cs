using UnityEngine;
using TMPro;

namespace BeneathTheFloor.UI
{
    /// <summary>
    /// A floating world-space text that appears at a position, floats up, and fades out.
    /// Used for feedback messages like "Tool too weak!" when digging fails.
    /// </summary>
    public class FloatingWorldText : MonoBehaviour
    {
        [Header("Animation")]
        [SerializeField] private float floatSpeed = 0.5f;
        [SerializeField] private float lifetime = 2f;
        [SerializeField] private float fadeStartTime = 1f;

        [Header("Appearance")]
        [SerializeField] private Color textColor = new Color(1f, 0.8f, 0.3f, 1f); // Warning yellow/orange
        [SerializeField] private float fontSize = 0.8f; // Small world-space size
        [SerializeField] private bool faceCamera = true;
        [SerializeField] private float textScale = 0.3f; // Additional scale for world space

        private TextMeshPro textMesh;
        private float spawnTime;
        private Camera mainCamera;
        private Color originalColor;

        /// <summary>
        /// Create a floating text at the specified world position.
        /// </summary>
        /// <param name="position">World position to spawn text</param>
        /// <param name="message">Text to display</param>
        /// <param name="color">Optional text color</param>
        /// <param name="duration">How long the text stays visible</param>
        /// <param name="scale">Scale of the text (default 0.3 for small readable text)</param>
        public static FloatingWorldText Create(Vector3 position, string message, Color? color = null, float duration = 2f, float scale = 0.3f)
        {
            GameObject obj = new GameObject("FloatingWorldText");
            obj.transform.position = position;

            FloatingWorldText floater = obj.AddComponent<FloatingWorldText>();
            floater.lifetime = duration;
            floater.fadeStartTime = duration * 0.5f;
            floater.textScale = scale;

            if (color.HasValue)
            {
                floater.textColor = color.Value;
            }

            floater.Initialize(message);
            return floater;
        }

        private void Initialize(string message)
        {
            // Create TextMeshPro component
            textMesh = gameObject.AddComponent<TextMeshPro>();
            textMesh.text = message;
            textMesh.fontSize = fontSize;
            textMesh.color = textColor;
            textMesh.alignment = TextAlignmentOptions.Center;
            textMesh.fontStyle = FontStyles.Bold;

            // Scale down for world space (TMP world space is huge by default)
            transform.localScale = Vector3.one * textScale;

            // Make it render on top of everything (including terrain)
            textMesh.sortingOrder = 100;

            // Use overlay rendering to not be hidden by geometry
            MeshRenderer renderer = GetComponent<MeshRenderer>();
            if (renderer != null)
            {
                // Set render queue to overlay (renders on top of most things)
                renderer.material.renderQueue = 4000;
            }

            // Set rect transform size
            RectTransform rect = GetComponent<RectTransform>();
            if (rect != null)
            {
                rect.sizeDelta = new Vector2(10f, 2f);
            }

            originalColor = textColor;
            spawnTime = Time.time;
            mainCamera = Camera.main;
        }

        private void Update()
        {
            if (textMesh == null) return;

            float elapsed = Time.time - spawnTime;

            // Float upward
            transform.position += Vector3.up * floatSpeed * Time.deltaTime;

            // Face camera
            if (faceCamera && mainCamera != null)
            {
                transform.rotation = Quaternion.LookRotation(transform.position - mainCamera.transform.position);
            }

            // Fade out
            if (elapsed > fadeStartTime)
            {
                float fadeProgress = (elapsed - fadeStartTime) / (lifetime - fadeStartTime);
                float alpha = Mathf.Lerp(1f, 0f, fadeProgress);
                textMesh.color = new Color(originalColor.r, originalColor.g, originalColor.b, alpha);
            }

            // Destroy after lifetime
            if (elapsed >= lifetime)
            {
                Destroy(gameObject);
            }
        }
    }
}
