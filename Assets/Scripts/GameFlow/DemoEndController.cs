using System.Collections;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;
using TMPro;

namespace BeneathTheFloor.GameFlow
{
    // Event fired when player clicks Continue Playing
    public static class DemoEndEvents
    {
        public static event System.Action OnContinuePlaying;
        public static void FireContinuePlaying() => OnContinuePlaying?.Invoke();
    }

    /// <summary>
    /// Controls the "End of Demo" screen behavior.
    /// Handles button interactions and scene transitions.
    /// </summary>
    public class DemoEndController : MonoBehaviour
    {
        [Header("UI References")]
        [Tooltip("The main title text")]
        [SerializeField] private TextMeshProUGUI titleText;

        [Tooltip("The teaser/description text")]
        [SerializeField] private TextMeshProUGUI teaserText;

        [Tooltip("Add to Wishlist button")]
        [SerializeField] private Button wishlistButton;

        [Tooltip("Continue Playing button")]
        [SerializeField] private Button continueButton;

        [Tooltip("Image used for fade transitions")]
        [SerializeField] private Image fadeOverlay;

        [Header("Steam Settings")]
        [Tooltip("Steam store page URL for wishlist")]
        [SerializeField] private string steamWishlistUrl = "https://store.steampowered.com/app/YOUR_APP_ID";

        [Header("Animation Settings")]
        [Tooltip("Duration for fade transitions")]
        [SerializeField] private float fadeDuration = 0.5f;

        [Tooltip("Duration for title glow pulse")]
        [SerializeField] private float glowPulseDuration = 2f;

        // Glow line references (set by setup script)
        [SerializeField] private Image leftGlowLine;
        [SerializeField] private Image rightGlowLine;

        private bool _isTransitioning;
        private Coroutine _glowPulseCoroutine;

        private void Start()
        {
            // Ensure cursor is visible
            Cursor.lockState = CursorLockMode.None;
            Cursor.visible = true;

            // Setup button listeners
            if (wishlistButton != null)
            {
                wishlistButton.onClick.AddListener(OnWishlistClicked);
            }

            if (continueButton != null)
            {
                continueButton.onClick.AddListener(OnContinueClicked);
            }

            // Initialize fade overlay
            if (fadeOverlay != null)
            {
                SetImageAlpha(fadeOverlay, 1f);
                fadeOverlay.raycastTarget = true;
            }

            // Start with fade-in from black
            StartCoroutine(FadeInCoroutine());

            // Start glow pulse animation
            if (leftGlowLine != null && rightGlowLine != null)
            {
                _glowPulseCoroutine = StartCoroutine(GlowPulseCoroutine());
            }
        }

        private void OnDestroy()
        {
            if (_glowPulseCoroutine != null)
            {
                StopCoroutine(_glowPulseCoroutine);
            }
        }

        /// <summary>
        /// Fade in from black when scene loads.
        /// </summary>
        private IEnumerator FadeInCoroutine()
        {
            if (fadeOverlay == null) yield break;

            float elapsed = 0f;
            while (elapsed < fadeDuration)
            {
                elapsed += Time.unscaledDeltaTime;
                float t = Mathf.Clamp01(elapsed / fadeDuration);
                SetImageAlpha(fadeOverlay, 1f - t);
                yield return null;
            }

            SetImageAlpha(fadeOverlay, 0f);
            fadeOverlay.raycastTarget = false;
        }

        /// <summary>
        /// Coroutine for pulsing glow lines.
        /// </summary>
        private IEnumerator GlowPulseCoroutine()
        {
            Color baseColor = new Color(0.3f, 0.85f, 1.0f, 1f);
            Color dimColor = new Color(0.15f, 0.5f, 0.6f, 0.6f);

            while (true)
            {
                // Pulse bright
                yield return StartCoroutine(LerpGlowColor(dimColor, baseColor, glowPulseDuration / 2f));
                // Pulse dim
                yield return StartCoroutine(LerpGlowColor(baseColor, dimColor, glowPulseDuration / 2f));
            }
        }

        private IEnumerator LerpGlowColor(Color from, Color to, float duration)
        {
            float elapsed = 0f;
            while (elapsed < duration)
            {
                elapsed += Time.unscaledDeltaTime;
                float t = Mathf.SmoothStep(0f, 1f, elapsed / duration);
                Color color = Color.Lerp(from, to, t);

                if (leftGlowLine != null) leftGlowLine.color = color;
                if (rightGlowLine != null) rightGlowLine.color = color;

                yield return null;
            }
        }

        /// <summary>
        /// Called when Add to Wishlist button is clicked.
        /// </summary>
        private void OnWishlistClicked()
        {
            // Try Steam overlay first, fallback to browser
            var wishlistOverlay = wishlistButton?.GetComponent<BeneathTheFloor.Steam.WishlistOverlayButton>();
            if (wishlistOverlay != null)
            {
                wishlistOverlay.OpenWishlistOverlay();
            }
            else
            {
                Application.OpenURL(steamWishlistUrl);
            }
        }

        /// <summary>
        /// Called when Continue Playing button is clicked.
        /// </summary>
        private void OnContinueClicked()
        {
            if (_isTransitioning) return;
            _isTransitioning = true;

            Debug.Log("[DemoEndController] Returning to game...");
            StartCoroutine(TransitionToGameCoroutine());
        }

        /// <summary>
        /// Fade out and return to game (unload this scene).
        /// </summary>
        private IEnumerator TransitionToGameCoroutine()
        {
            if (fadeOverlay != null)
            {
                fadeOverlay.raycastTarget = true;

                float elapsed = 0f;
                while (elapsed < fadeDuration)
                {
                    elapsed += Time.unscaledDeltaTime;
                    float t = Mathf.Clamp01(elapsed / fadeDuration);
                    SetImageAlpha(fadeOverlay, t);
                    yield return null;
                }

                SetImageAlpha(fadeOverlay, 1f);
            }

            // Small delay
            yield return new WaitForSecondsRealtime(0.1f);

            // Resume game time
            Time.timeScale = 1f;

            // Fire event so upgrade station can reopen
            DemoEndEvents.FireContinuePlaying();

            // Unload this scene (game scene stays active)
            SceneManager.UnloadSceneAsync("DemoEndScene");
        }

        /// <summary>
        /// Sets the alpha of a UI Image component.
        /// </summary>
        private void SetImageAlpha(Image image, float alpha)
        {
            if (image != null)
            {
                Color color = image.color;
                color.a = alpha;
                image.color = color;
            }
        }
    }
}
