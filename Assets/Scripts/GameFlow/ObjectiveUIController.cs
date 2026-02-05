using System.Collections;
using UnityEngine;
using UnityEngine.UI;
using TMPro;

namespace BeneathTheFloor.GameFlow
{
    /// <summary>
    /// Controls the minimal UI for onboarding objectives and progress.
    /// Creates UI elements dynamically if not assigned.
    /// </summary>
    public class ObjectiveUIController : MonoBehaviour
    {
        [Header("UI References (Auto-created if null)")]
        [SerializeField] private TextMeshProUGUI storyText;
        [SerializeField] private TextMeshProUGUI objectiveText;
        [SerializeField] private TextMeshProUGUI progressText;
        [SerializeField] private TextMeshProUGUI toastText;
        [SerializeField] private Image fadeOverlay;

        [Header("Styling")]
        [SerializeField] private Color storyTextColor = new Color(1f, 1f, 1f, 0.95f);
        [SerializeField] private Color objectiveTextColor = new Color(1f, 0.9f, 0.6f, 1f);
        [SerializeField] private Color progressTextColor = new Color(0.8f, 0.8f, 0.8f, 1f);
        [SerializeField] private Color toastTextColor = new Color(0.6f, 1f, 0.6f, 1f);
        [SerializeField] private int storyFontSize = 28;
        [SerializeField] private int objectiveFontSize = 20;
        [SerializeField] private int progressFontSize = 18;
        [SerializeField] private int toastFontSize = 24;

        [Header("Positioning")]
        [SerializeField] private Vector2 objectivePosition = new Vector2(20, -20);
        [SerializeField] private Vector2 progressOffset = new Vector2(0, -30);

        private Canvas canvas;
        private Coroutine fadeCoroutine;
        private Coroutine toastCoroutine;

        private void Awake()
        {
            canvas = GetComponent<Canvas>();
            if (canvas == null)
            {
                canvas = gameObject.AddComponent<Canvas>();
                canvas.renderMode = RenderMode.ScreenSpaceOverlay;
                canvas.sortingOrder = 100;
            }

            EnsureUIElements();
        }

        private void EnsureUIElements()
        {
            // Ensure we have a CanvasScaler
            if (GetComponent<CanvasScaler>() == null)
            {
                var scaler = gameObject.AddComponent<CanvasScaler>();
                scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
                scaler.referenceResolution = new Vector2(1920, 1080);
            }

            if (GetComponent<GraphicRaycaster>() == null)
            {
                gameObject.AddComponent<GraphicRaycaster>();
            }

            // Create fade overlay
            if (fadeOverlay == null)
            {
                fadeOverlay = CreateFadeOverlay();
            }

            // Create story text (center screen)
            if (storyText == null)
            {
                storyText = CreateTextElement("StoryText", Vector2.zero, TextAnchor.MiddleCenter,
                    storyFontSize, storyTextColor, TextAlignmentOptions.Center);
                storyText.gameObject.SetActive(false);
            }

            // Create objective text (top-left)
            if (objectiveText == null)
            {
                objectiveText = CreateTextElement("ObjectiveText", objectivePosition, TextAnchor.UpperLeft,
                    objectiveFontSize, objectiveTextColor, TextAlignmentOptions.TopLeft);
                SetAnchorTopLeft(objectiveText.rectTransform);
                objectiveText.gameObject.SetActive(false);
            }

            // Create progress text (below objective)
            if (progressText == null)
            {
                Vector2 progressPos = objectivePosition + progressOffset;
                progressText = CreateTextElement("ProgressText", progressPos, TextAnchor.UpperLeft,
                    progressFontSize, progressTextColor, TextAlignmentOptions.TopLeft);
                SetAnchorTopLeft(progressText.rectTransform);
                progressText.gameObject.SetActive(false);
            }

            // Create toast text (center-bottom)
            if (toastText == null)
            {
                toastText = CreateTextElement("ToastText", new Vector2(0, 150), TextAnchor.LowerCenter,
                    toastFontSize, toastTextColor, TextAlignmentOptions.Center);
                SetAnchorBottomCenter(toastText.rectTransform);
                toastText.gameObject.SetActive(false);
            }
        }

        private Image CreateFadeOverlay()
        {
            GameObject overlayObj = new GameObject("FadeOverlay");
            overlayObj.transform.SetParent(transform, false);

            var rect = overlayObj.AddComponent<RectTransform>();
            rect.anchorMin = Vector2.zero;
            rect.anchorMax = Vector2.one;
            rect.offsetMin = Vector2.zero;
            rect.offsetMax = Vector2.zero;

            var image = overlayObj.AddComponent<Image>();
            image.color = new Color(0, 0, 0, 0);
            image.raycastTarget = false;

            overlayObj.SetActive(false);
            return image;
        }

        private TextMeshProUGUI CreateTextElement(string name, Vector2 position, TextAnchor alignment,
            int fontSize, Color color, TextAlignmentOptions tmpAlignment)
        {
            GameObject textObj = new GameObject(name);
            textObj.transform.SetParent(transform, false);

            var rect = textObj.AddComponent<RectTransform>();
            rect.anchoredPosition = position;
            rect.sizeDelta = new Vector2(600, 100);

            var tmp = textObj.AddComponent<TextMeshProUGUI>();
            tmp.fontSize = fontSize;
            tmp.color = color;
            tmp.alignment = tmpAlignment;
            tmp.enableWordWrapping = true;
            tmp.raycastTarget = false;

            // Add subtle shadow
            tmp.fontStyle = FontStyles.Normal;
            tmp.outlineWidth = 0.1f;
            tmp.outlineColor = new Color(0, 0, 0, 0.5f);

            return tmp;
        }

        private void SetAnchorTopLeft(RectTransform rect)
        {
            rect.anchorMin = new Vector2(0, 1);
            rect.anchorMax = new Vector2(0, 1);
            rect.pivot = new Vector2(0, 1);
        }

        private void SetAnchorBottomCenter(RectTransform rect)
        {
            rect.anchorMin = new Vector2(0.5f, 0);
            rect.anchorMax = new Vector2(0.5f, 0);
            rect.pivot = new Vector2(0.5f, 0);
        }

        #region Public API

        /// <summary>
        /// Show or hide the black fade overlay.
        /// </summary>
        public void ShowFadeOverlay(bool show, float duration = 0.5f)
        {
            if (fadeOverlay == null) return;

            fadeOverlay.gameObject.SetActive(true);

            if (fadeCoroutine != null)
                StopCoroutine(fadeCoroutine);

            fadeCoroutine = StartCoroutine(FadeOverlayRoutine(show ? 1f : 0f, duration));
        }

        /// <summary>
        /// Show story text with fade in.
        /// </summary>
        public void ShowStoryText(string text, float fadeTime = 0.5f)
        {
            if (storyText == null) return;

            storyText.text = text;
            storyText.gameObject.SetActive(true);
            StartCoroutine(FadeTextRoutine(storyText, 1f, fadeTime));
        }

        /// <summary>
        /// Hide story text with fade out.
        /// </summary>
        public void HideStoryText(float fadeTime = 0.5f)
        {
            if (storyText == null) return;

            StartCoroutine(FadeTextAndDisableRoutine(storyText, fadeTime));
        }

        /// <summary>
        /// Set the current objective text.
        /// Supports multi-line objectives with bullet points.
        /// </summary>
        public void SetObjective(string objective)
        {
            if (objectiveText == null) return;

            // Display objective text directly (no prefix)
            objectiveText.text = objective;
            objectiveText.gameObject.SetActive(true);
        }

        /// <summary>
        /// Hide the objective text.
        /// </summary>
        public void HideObjective()
        {
            if (objectiveText != null)
                objectiveText.gameObject.SetActive(false);
        }

        /// <summary>
        /// Set the progress text showing current/required with a label.
        /// </summary>
        public void SetProgress(int current, int required, string label = "Credits")
        {
            if (progressText == null) return;

            progressText.text = $"{label}: {current} / {required}";
            progressText.gameObject.SetActive(true);

            // Highlight when goal reached
            if (current >= required)
            {
                progressText.color = new Color(0.5f, 1f, 0.5f, 1f);
            }
            else
            {
                progressText.color = progressTextColor;
            }
        }

        /// <summary>
        /// Hide the progress text.
        /// </summary>
        public void HideProgress()
        {
            if (progressText != null)
                progressText.gameObject.SetActive(false);
        }

        /// <summary>
        /// Show a temporary toast message.
        /// </summary>
        public void ShowToast(string message, float duration = 2f)
        {
            if (toastText == null) return;

            if (toastCoroutine != null)
                StopCoroutine(toastCoroutine);

            toastCoroutine = StartCoroutine(ToastRoutine(message, duration));
        }

        #endregion

        #region Coroutines

        private IEnumerator FadeOverlayRoutine(float targetAlpha, float duration)
        {
            float startAlpha = fadeOverlay.color.a;
            float elapsed = 0f;

            while (elapsed < duration)
            {
                elapsed += Time.deltaTime;
                float t = elapsed / duration;
                float alpha = Mathf.Lerp(startAlpha, targetAlpha, t);
                fadeOverlay.color = new Color(0, 0, 0, alpha);
                yield return null;
            }

            fadeOverlay.color = new Color(0, 0, 0, targetAlpha);

            if (targetAlpha <= 0f)
            {
                fadeOverlay.gameObject.SetActive(false);
            }
        }

        private IEnumerator FadeTextRoutine(TextMeshProUGUI text, float targetAlpha, float duration)
        {
            Color startColor = text.color;
            startColor.a = 0f;
            text.color = startColor;

            float elapsed = 0f;
            while (elapsed < duration)
            {
                elapsed += Time.deltaTime;
                float t = elapsed / duration;
                Color c = text.color;
                c.a = Mathf.Lerp(0f, targetAlpha, t);
                text.color = c;
                yield return null;
            }

            Color finalColor = text.color;
            finalColor.a = targetAlpha;
            text.color = finalColor;
        }

        private IEnumerator FadeTextAndDisableRoutine(TextMeshProUGUI text, float duration)
        {
            float startAlpha = text.color.a;
            float elapsed = 0f;

            while (elapsed < duration)
            {
                elapsed += Time.deltaTime;
                float t = elapsed / duration;
                Color c = text.color;
                c.a = Mathf.Lerp(startAlpha, 0f, t);
                text.color = c;
                yield return null;
            }

            text.gameObject.SetActive(false);
        }

        private IEnumerator ToastRoutine(string message, float duration)
        {
            toastText.text = message;
            toastText.gameObject.SetActive(true);

            // Fade in
            yield return FadeTextRoutine(toastText, 1f, 0.3f);

            // Wait
            yield return new WaitForSeconds(duration - 0.6f);

            // Fade out
            yield return FadeTextAndDisableRoutine(toastText, 0.3f);
        }

        #endregion
    }
}
