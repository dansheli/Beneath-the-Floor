using System;
using System.Collections;
using UnityEngine;
using UnityEngine.UI;
using TMPro;
using BeneathTheFloor.UI;

namespace BeneathTheFloor.GameFlow
{
    /// <summary>
    /// UI controller for displaying readable notes.
    /// </summary>
    public class NoteUIController : MonoBehaviour
    {
        public static NoteUIController Instance { get; private set; }

        [Header("UI References (Auto-created if null)")]
        [SerializeField] private Canvas noteCanvas;
        [SerializeField] private CanvasGroup canvasGroup;
        [SerializeField] private Image backgroundOverlay;
        [SerializeField] private Image notePanelBackground;
        [SerializeField] private TextMeshProUGUI titleText;
        [SerializeField] private TextMeshProUGUI contentText;
        [SerializeField] private TextMeshProUGUI closeHintText;

        [Header("Styling")]
        [SerializeField] private Color overlayColor = new Color(0, 0, 0, 0.7f);
        [SerializeField] private Color panelColor = new Color(0.95f, 0.9f, 0.8f, 1f); // Aged paper color
        [SerializeField] private Color titleColor = new Color(0.2f, 0.15f, 0.1f, 1f);
        [SerializeField] private Color contentColor = new Color(0.25f, 0.2f, 0.15f, 1f);
        [SerializeField] private int titleFontSize = 32;
        [SerializeField] private int contentFontSize = 24;
        [SerializeField] private Vector2 panelSize = new Vector2(700, 500);

        [Header("Animation")]
        [SerializeField] private float fadeInDuration = 0.3f;
        [SerializeField] private float fadeOutDuration = 0.2f;

        [Header("Input")]
        [SerializeField] private KeyCode[] closeKeys = new KeyCode[]
        {
            KeyCode.E,
            KeyCode.Escape,
            KeyCode.Space,
            KeyCode.Return
        };

        [Header("Audio")]
        [Tooltip("Looping ambient sound that plays while reading the note")]
        [SerializeField] private AudioClip noteAmbientLoop;

        [Tooltip("Volume for the ambient loop")]
        [SerializeField] [Range(0f, 1f)] private float ambientVolume = 0.5f;

        [Tooltip("Fade in duration for audio (seconds)")]
        [SerializeField] private float audioFadeInDuration = 0.5f;

        [Tooltip("Fade out duration for audio (seconds)")]
        [SerializeField] private float audioFadeOutDuration = 0.3f;

        private AudioSource ambientAudioSource;

        private bool isShowing = false;
        private Action onCloseCallback;
        private MonoBehaviour playerController;
        private int openedOnFrame = -1; // Track frame when note opened to prevent same-frame close

        private void Awake()
        {
            if (Instance != null && Instance != this)
            {
                Destroy(gameObject);
                return;
            }
            Instance = this;

            CreateUI();
            CreateAudioSource();
            HideImmediate();
        }

        private void CreateAudioSource()
        {
            // Create audio source for ambient loop
            GameObject audioObj = new GameObject("NoteAmbientAudio");
            audioObj.transform.SetParent(transform);

            ambientAudioSource = audioObj.AddComponent<AudioSource>();
            ambientAudioSource.clip = noteAmbientLoop;
            ambientAudioSource.loop = true;
            ambientAudioSource.playOnAwake = false;
            ambientAudioSource.spatialBlend = 0f; // 2D sound
            ambientAudioSource.volume = 0f;
        }

        private void Update()
        {
            if (isShowing)
            {
                // Don't accept close input on the same frame the note opened
                // This prevents the same E keypress from opening and closing the note
                if (Time.frameCount == openedOnFrame)
                    return;

                foreach (var key in closeKeys)
                {
                    if (Input.GetKeyDown(key))
                    {
                        // Consume escape to prevent pause menu from opening
                        if (key == KeyCode.Escape)
                        {
                            BeneathTheFloor.UI.UIState.ConsumeEscape();
                        }
                        CloseNote();
                        return;
                    }
                }

                // Also close on mouse click
                if (Input.GetMouseButtonDown(0) || Input.GetMouseButtonDown(1))
                {
                    CloseNote();
                }
            }
        }

        private void CreateUI()
        {
            // Create Canvas if needed
            if (noteCanvas == null)
            {
                noteCanvas = GetComponent<Canvas>();
                if (noteCanvas == null)
                {
                    noteCanvas = gameObject.AddComponent<Canvas>();
                }
                noteCanvas.renderMode = RenderMode.ScreenSpaceOverlay;
                noteCanvas.sortingOrder = 200; // Above other UI
            }

            if (GetComponent<CanvasScaler>() == null)
            {
                var scaler = gameObject.AddComponent<CanvasScaler>();
                scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
                scaler.referenceResolution = new Vector2(1920, 1080);
                scaler.matchWidthOrHeight = 0.5f;
            }

            if (GetComponent<GraphicRaycaster>() == null)
            {
                gameObject.AddComponent<GraphicRaycaster>();
            }

            // Add CanvasGroup for fading
            if (canvasGroup == null)
            {
                canvasGroup = GetComponent<CanvasGroup>();
                if (canvasGroup == null)
                {
                    canvasGroup = gameObject.AddComponent<CanvasGroup>();
                }
            }

            // Create background overlay
            if (backgroundOverlay == null)
            {
                GameObject overlayObj = new GameObject("BackgroundOverlay");
                overlayObj.transform.SetParent(transform, false);

                backgroundOverlay = overlayObj.AddComponent<Image>();
                backgroundOverlay.color = overlayColor;

                RectTransform overlayRect = overlayObj.GetComponent<RectTransform>();
                overlayRect.anchorMin = Vector2.zero;
                overlayRect.anchorMax = Vector2.one;
                overlayRect.offsetMin = Vector2.zero;
                overlayRect.offsetMax = Vector2.zero;
            }

            // Create note panel
            if (notePanelBackground == null)
            {
                GameObject panelObj = new GameObject("NotePanel");
                panelObj.transform.SetParent(transform, false);

                notePanelBackground = panelObj.AddComponent<Image>();
                notePanelBackground.color = panelColor;

                RectTransform panelRect = panelObj.GetComponent<RectTransform>();
                panelRect.anchorMin = new Vector2(0.5f, 0.5f);
                panelRect.anchorMax = new Vector2(0.5f, 0.5f);
                panelRect.pivot = new Vector2(0.5f, 0.5f);
                panelRect.sizeDelta = panelSize;

                // Add subtle shadow/outline
                var outline = panelObj.AddComponent<Outline>();
                outline.effectColor = new Color(0, 0, 0, 0.3f);
                outline.effectDistance = new Vector2(3, -3);

                // Create title text
                GameObject titleObj = new GameObject("TitleText");
                titleObj.transform.SetParent(panelObj.transform, false);

                titleText = titleObj.AddComponent<TextMeshProUGUI>();
                titleText.fontSize = titleFontSize;
                titleText.color = titleColor;
                titleText.alignment = TextAlignmentOptions.Center;
                titleText.fontStyle = FontStyles.Bold;

                RectTransform titleRect = titleObj.GetComponent<RectTransform>();
                titleRect.anchorMin = new Vector2(0, 1);
                titleRect.anchorMax = new Vector2(1, 1);
                titleRect.pivot = new Vector2(0.5f, 1);
                titleRect.anchoredPosition = new Vector2(0, -20);
                titleRect.sizeDelta = new Vector2(-40, 50);

                // Create content text
                GameObject contentObj = new GameObject("ContentText");
                contentObj.transform.SetParent(panelObj.transform, false);

                contentText = contentObj.AddComponent<TextMeshProUGUI>();
                contentText.fontSize = contentFontSize;
                contentText.color = contentColor;
                contentText.alignment = TextAlignmentOptions.Center;
                contentText.enableWordWrapping = true;

                RectTransform contentRect = contentObj.GetComponent<RectTransform>();
                contentRect.anchorMin = new Vector2(0, 0);
                contentRect.anchorMax = new Vector2(1, 1);
                contentRect.offsetMin = new Vector2(40, 60);
                contentRect.offsetMax = new Vector2(-40, -80);

                // Create close hint text
                GameObject hintObj = new GameObject("CloseHintText");
                hintObj.transform.SetParent(panelObj.transform, false);

                closeHintText = hintObj.AddComponent<TextMeshProUGUI>();
                closeHintText.text = "Press E or click to close";
                closeHintText.fontSize = 16;
                closeHintText.color = new Color(0.4f, 0.35f, 0.3f, 0.8f);
                closeHintText.alignment = TextAlignmentOptions.Center;
                closeHintText.fontStyle = FontStyles.Italic;

                RectTransform hintRect = hintObj.GetComponent<RectTransform>();
                hintRect.anchorMin = new Vector2(0, 0);
                hintRect.anchorMax = new Vector2(1, 0);
                hintRect.pivot = new Vector2(0.5f, 0);
                hintRect.anchoredPosition = new Vector2(0, 15);
                hintRect.sizeDelta = new Vector2(0, 30);
            }
        }

        /// <summary>
        /// Show a note with the given title and content.
        /// </summary>
        public void ShowNote(string title, string content, Action onClose = null)
        {
            if (isShowing) return;

            onCloseCallback = onClose;

            // Set content
            if (titleText != null)
                titleText.text = title;

            if (contentText != null)
                contentText.text = content;

            // Disable player control
            DisablePlayerControl();

            // Show cursor
            Cursor.lockState = CursorLockMode.None;
            Cursor.visible = true;

            // Fade in
            StartCoroutine(FadeIn());
        }

        /// <summary>
        /// Close the note UI.
        /// </summary>
        public void CloseNote()
        {
            if (!isShowing) return;

            StartCoroutine(FadeOutAndClose());
        }

        private IEnumerator FadeIn()
        {
            isShowing = true;
            openedOnFrame = Time.frameCount; // Record frame to prevent same-frame close
            UIState.IsNoteUIOpen = true; // Block InteractionSystem

            // Enable interaction
            if (canvasGroup != null)
            {
                canvasGroup.blocksRaycasts = true;
                canvasGroup.interactable = true;
            }

            // Start ambient audio
            StartAmbientAudio();

            float elapsed = 0f;
            while (elapsed < fadeInDuration)
            {
                elapsed += Time.unscaledDeltaTime;
                float t = elapsed / fadeInDuration;
                t = t * t * (3f - 2f * t); // Smoothstep
                canvasGroup.alpha = t;
                yield return null;
            }
            canvasGroup.alpha = 1f;
        }

        private void StartAmbientAudio()
        {
            if (ambientAudioSource == null || noteAmbientLoop == null)
                return;

            // Update clip in case it was changed in Inspector
            ambientAudioSource.clip = noteAmbientLoop;
            ambientAudioSource.volume = 0f;
            ambientAudioSource.Play();

            // Start audio fade-in coroutine
            StartCoroutine(FadeAudioIn());
        }

        private IEnumerator FadeAudioIn()
        {
            if (ambientAudioSource == null)
                yield break;

            float elapsed = 0f;
            while (elapsed < audioFadeInDuration)
            {
                elapsed += Time.unscaledDeltaTime;
                float t = elapsed / audioFadeInDuration;
                ambientAudioSource.volume = Mathf.Lerp(0f, ambientVolume, t);
                yield return null;
            }
            ambientAudioSource.volume = ambientVolume;
        }

        private IEnumerator FadeOutAndClose()
        {
            // Start audio fade-out
            StartCoroutine(FadeAudioOut());

            float elapsed = 0f;
            while (elapsed < fadeOutDuration)
            {
                elapsed += Time.unscaledDeltaTime;
                float t = elapsed / fadeOutDuration;
                canvasGroup.alpha = 1f - t;
                yield return null;
            }

            HideImmediate();
            StopAmbientAudio();
            UIState.IsNoteUIOpen = false; // Allow InteractionSystem again

            // Re-enable player control
            EnablePlayerControl();

            // Hide cursor
            Cursor.lockState = CursorLockMode.Locked;
            Cursor.visible = false;

            // Invoke callback
            onCloseCallback?.Invoke();
            onCloseCallback = null;
        }

        private IEnumerator FadeAudioOut()
        {
            if (ambientAudioSource == null || !ambientAudioSource.isPlaying)
                yield break;

            float startVolume = ambientAudioSource.volume;
            float elapsed = 0f;
            while (elapsed < audioFadeOutDuration)
            {
                elapsed += Time.unscaledDeltaTime;
                float t = elapsed / audioFadeOutDuration;
                ambientAudioSource.volume = Mathf.Lerp(startVolume, 0f, t);
                yield return null;
            }
            ambientAudioSource.volume = 0f;
        }

        private void StopAmbientAudio()
        {
            if (ambientAudioSource != null && ambientAudioSource.isPlaying)
            {
                ambientAudioSource.Stop();
                ambientAudioSource.volume = 0f;
            }
        }

        private void HideImmediate()
        {
            isShowing = false;
            if (canvasGroup != null)
            {
                canvasGroup.alpha = 0f;
                canvasGroup.blocksRaycasts = false;
                canvasGroup.interactable = false;
            }
            // Don't deactivate GameObject - just hide via CanvasGroup
            // This allows coroutines to start when ShowNote is called
        }

        private void OnDisable()
        {
            // Safety: clear UIState if controller is disabled while note is open
            if (isShowing)
            {
                UIState.IsNoteUIOpen = false;
            }

            // Stop audio when disabled
            StopAmbientAudio();
        }

        /// <summary>
        /// Set the ambient loop audio clip at runtime.
        /// </summary>
        public void SetAmbientClip(AudioClip clip)
        {
            noteAmbientLoop = clip;
            if (ambientAudioSource != null)
            {
                ambientAudioSource.clip = clip;
            }
        }

        /// <summary>
        /// Set the ambient volume at runtime.
        /// </summary>
        public void SetAmbientVolume(float volume)
        {
            ambientVolume = Mathf.Clamp01(volume);
            if (ambientAudioSource != null && ambientAudioSource.isPlaying)
            {
                ambientAudioSource.volume = ambientVolume;
            }
        }

        /// <summary>
        /// Get current ambient volume setting.
        /// </summary>
        public float GetAmbientVolume()
        {
            return ambientVolume;
        }

        private void DisablePlayerControl()
        {
            // Find and disable player controller
            var player = GameObject.FindWithTag("Player");
            if (player != null)
            {
                playerController = player.GetComponent("FirstPersonController") as MonoBehaviour;
                if (playerController != null)
                {
                    playerController.enabled = false;
                }
            }

            // Hide crosshair
            if (HUDController.Instance != null)
            {
                HUDController.Instance.SetCrosshairVisible(false);
            }
        }

        private void EnablePlayerControl()
        {
            if (playerController != null)
            {
                playerController.enabled = true;
            }

            // Show crosshair
            if (HUDController.Instance != null)
            {
                HUDController.Instance.SetCrosshairVisible(true);
            }
        }

        private void OnDestroy()
        {
            // Stop audio when destroyed
            StopAmbientAudio();

            // Clear UIState when destroyed
            if (isShowing)
            {
                UIState.IsNoteUIOpen = false;
            }
            if (Instance == this)
                Instance = null;
        }
    }
}
