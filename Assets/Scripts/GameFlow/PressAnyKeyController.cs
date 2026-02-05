using System.Collections;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;
using TMPro;

namespace BeneathTheFloor.GameFlow
{
    /// <summary>
    /// Controls the "Press Any Key" title screen behavior.
    /// Handles text pulsing animation, input detection, and scene transition.
    /// </summary>
    public class PressAnyKeyController : MonoBehaviour
    {
        [Header("UI References")]
        [Tooltip("The text displaying 'PRESS ANY KEY'")]
        [SerializeField] private TextMeshProUGUI pressAnyKeyText;

        [Tooltip("Image used for fade-to-black transition")]
        [SerializeField] private Image fadeOverlay;

        [Header("Text Fade Settings")]
        [Tooltip("Duration for text to fade from min to max alpha")]
        [SerializeField] private float textFadeInDuration = 1.5f;

        [Tooltip("Duration for text to fade from max to min alpha")]
        [SerializeField] private float textFadeOutDuration = 1.5f;

        [Tooltip("Minimum alpha value for text pulse")]
        [SerializeField] [Range(0f, 1f)] private float textMinAlpha = 0.3f;

        [Tooltip("Maximum alpha value for text pulse")]
        [SerializeField] [Range(0f, 1f)] private float textMaxAlpha = 1f;

        [Header("Screen Transition Settings")]
        [Tooltip("Duration for screen fade to black")]
        [SerializeField] private float screenFadeDuration = 1f;

        [Tooltip("Name of the scene to load after input")]
        [SerializeField] private string nextSceneName = "MainMenuScene";

        private bool _inputLocked;
        private Coroutine _textPulseCoroutine;

        private void Start()
        {
            InitializeFadeOverlay();
            StartTextPulse();
        }

        private void Update()
        {
            if (_inputLocked) return;

            if (DetectAnyInput())
            {
                HandleInput();
            }
        }

        /// <summary>
        /// Initializes the fade overlay to be fully transparent.
        /// </summary>
        private void InitializeFadeOverlay()
        {
            if (fadeOverlay != null)
            {
                SetImageAlpha(fadeOverlay, 0f);
                fadeOverlay.raycastTarget = false;
            }
        }

        /// <summary>
        /// Starts the continuous text pulse animation.
        /// </summary>
        private void StartTextPulse()
        {
            if (pressAnyKeyText != null)
            {
                _textPulseCoroutine = StartCoroutine(TextPulseCoroutine());
            }
        }

        /// <summary>
        /// Coroutine that continuously fades the text in and out.
        /// </summary>
        private IEnumerator TextPulseCoroutine()
        {
            // Start at minimum alpha
            SetTextAlpha(textMinAlpha);

            while (true)
            {
                // Fade in
                yield return StartCoroutine(FadeTextCoroutine(textMinAlpha, textMaxAlpha, textFadeInDuration));

                // Fade out
                yield return StartCoroutine(FadeTextCoroutine(textMaxAlpha, textMinAlpha, textFadeOutDuration));
            }
        }

        /// <summary>
        /// Coroutine to smoothly fade text alpha between two values.
        /// </summary>
        private IEnumerator FadeTextCoroutine(float fromAlpha, float toAlpha, float duration)
        {
            float elapsed = 0f;

            while (elapsed < duration)
            {
                elapsed += Time.deltaTime;
                float t = Mathf.Clamp01(elapsed / duration);
                // Use smooth step for nicer easing
                t = t * t * (3f - 2f * t);

                float alpha = Mathf.Lerp(fromAlpha, toAlpha, t);
                SetTextAlpha(alpha);

                yield return null;
            }

            SetTextAlpha(toAlpha);
        }

        /// <summary>
        /// Detects any input from keyboard, mouse buttons, or gamepad.
        /// </summary>
        private bool DetectAnyInput()
        {
            // Check keyboard
            if (Input.anyKeyDown)
            {
                return true;
            }

            // Check mouse movement (optional - commented out to avoid accidental triggers)
            // if (Input.GetAxis("Mouse X") != 0 || Input.GetAxis("Mouse Y") != 0)
            // {
            //     return true;
            // }

            // Check gamepad axes (triggers, sticks)
            for (int i = 1; i <= 10; i++)
            {
                string axisName = "Joystick Axis " + i;
                try
                {
                    if (Mathf.Abs(Input.GetAxisRaw(axisName)) > 0.5f)
                    {
                        return true;
                    }
                }
                catch
                {
                    // Axis doesn't exist, skip
                }
            }

            return false;
        }

        /// <summary>
        /// Handles input by locking further input and starting the transition.
        /// </summary>
        private void HandleInput()
        {
            _inputLocked = true;

            // Stop text pulse
            if (_textPulseCoroutine != null)
            {
                StopCoroutine(_textPulseCoroutine);
                _textPulseCoroutine = null;
            }

            // Start transition
            StartCoroutine(TransitionToNextSceneCoroutine());
        }

        /// <summary>
        /// Coroutine that fades the screen to black and loads the next scene.
        /// </summary>
        private IEnumerator TransitionToNextSceneCoroutine()
        {
            if (fadeOverlay != null)
            {
                fadeOverlay.raycastTarget = true;

                float elapsed = 0f;

                while (elapsed < screenFadeDuration)
                {
                    elapsed += Time.deltaTime;
                    float t = Mathf.Clamp01(elapsed / screenFadeDuration);
                    // Smooth ease-in for fade
                    t = t * t;

                    SetImageAlpha(fadeOverlay, t);
                    yield return null;
                }

                SetImageAlpha(fadeOverlay, 1f);
            }

            // Small delay for polish
            yield return new WaitForSeconds(0.1f);

            // Load next scene
            SceneManager.LoadScene(nextSceneName);
        }

        /// <summary>
        /// Sets the alpha of the TextMeshPro text component.
        /// </summary>
        private void SetTextAlpha(float alpha)
        {
            if (pressAnyKeyText != null)
            {
                Color color = pressAnyKeyText.color;
                color.a = alpha;
                pressAnyKeyText.color = color;
            }
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
