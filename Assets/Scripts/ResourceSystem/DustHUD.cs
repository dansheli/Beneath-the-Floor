using UnityEngine;
using TMPro;

namespace BeneathTheFloor.ResourceSystem
{
    /// <summary>
    /// Simple HUD display for dust amount.
    /// NOTE: DISABLED - Dust display now part of EstimatedValueHUD.
    /// Shows dust count and potential credit value.
    /// </summary>
    public class DustHUD : MonoBehaviour
    {
        [Header("References")]
        [SerializeField] private TextMeshProUGUI dustText;

        [Header("Display Settings")]
        [Tooltip("DISABLED: Dust display moved to EstimatedValueHUD")]
        [SerializeField] private bool enableDisplay = false;
        [SerializeField] private string format = "Dust: {0:F0}";
        [SerializeField] private bool showValue = true;
        [SerializeField] private string valueFormat = " (${0})";

        [Header("Visual")]
        [SerializeField] private Color normalColor = new Color(0.9f, 0.8f, 0.5f); // Sandy/dusty color
        [SerializeField] private Color fullColor = new Color(1f, 0.9f, 0.3f);      // Bright when lots of dust

        [Header("Animation")]
        [SerializeField] private bool animateOnChange = true;
        [SerializeField] private float pulseScale = 1.2f;
        [SerializeField] private float pulseDuration = 0.2f;

        private float lastDustAmount = 0f;
        private Vector3 originalScale;

        private void Start()
        {
            // DISABLED: Dust display moved to EstimatedValueHUD
            if (!enableDisplay)
            {
                if (dustText != null)
                {
                    dustText.gameObject.SetActive(false);
                }
                enabled = false;
                return;
            }

            // Auto-find text component
            if (dustText == null)
            {
                dustText = GetComponent<TextMeshProUGUI>();
                if (dustText == null)
                {
                    dustText = GetComponentInChildren<TextMeshProUGUI>();
                }
            }

            if (dustText == null)
            {
                Debug.LogError("[DustHUD] No TextMeshProUGUI found!");
                enabled = false;
                return;
            }

            originalScale = transform.localScale;

            // Subscribe to dust changes
            if (DustManager.Instance != null)
            {
                DustManager.Instance.OnDustChanged += OnDustChanged;
                UpdateDisplay(DustManager.Instance.CurrentDust);
            }
            else
            {
                // Try again after a delay
                StartCoroutine(DelayedSubscribe());
            }
        }

        private System.Collections.IEnumerator DelayedSubscribe()
        {
            yield return new WaitForSeconds(0.5f);

            if (DustManager.Instance != null)
            {
                DustManager.Instance.OnDustChanged += OnDustChanged;
                UpdateDisplay(DustManager.Instance.CurrentDust);
            }
        }

        private void OnDestroy()
        {
            if (DustManager.Instance != null)
            {
                DustManager.Instance.OnDustChanged -= OnDustChanged;
            }
        }

        private void OnDustChanged(float newAmount)
        {
            UpdateDisplay(newAmount);

            // Animate if amount increased
            if (animateOnChange && newAmount > lastDustAmount)
            {
                StartCoroutine(PulseAnimation());
            }

            lastDustAmount = newAmount;
        }

        private void UpdateDisplay(float dustAmount)
        {
            if (dustText == null)
                return;

            string text = string.Format(format, dustAmount);

            if (showValue && DustManager.Instance != null)
            {
                int value = DustManager.Instance.GetDustValue();
                text += string.Format(valueFormat, value);
            }

            dustText.text = text;

            // Update color based on amount
            float t = Mathf.Clamp01(dustAmount / 1000f); // Full color at 1000 dust
            dustText.color = Color.Lerp(normalColor, fullColor, t);
        }

        private System.Collections.IEnumerator PulseAnimation()
        {
            float elapsed = 0f;
            float halfDuration = pulseDuration * 0.5f;

            // Scale up
            while (elapsed < halfDuration)
            {
                elapsed += Time.deltaTime;
                float t = elapsed / halfDuration;
                transform.localScale = Vector3.Lerp(originalScale, originalScale * pulseScale, t);
                yield return null;
            }

            // Scale down
            elapsed = 0f;
            while (elapsed < halfDuration)
            {
                elapsed += Time.deltaTime;
                float t = elapsed / halfDuration;
                transform.localScale = Vector3.Lerp(originalScale * pulseScale, originalScale, t);
                yield return null;
            }

            transform.localScale = originalScale;
        }

        /// <summary>
        /// Manually update the display (if needed).
        /// </summary>
        public void Refresh()
        {
            if (DustManager.Instance != null)
            {
                UpdateDisplay(DustManager.Instance.CurrentDust);
            }
        }
    }
}
