using System.Collections;
using UnityEngine;
using UnityEngine.UI;
using TMPro;
using BeneathTheFloor.UI;

namespace BeneathTheFloor.GameFlow
{
    /// <summary>
    /// Controls the intro cinematic that plays when starting a new game.
    /// Shows atmospheric text lines with smooth fades on a black overlay.
    /// </summary>
    public class IntroCinematicController : MonoBehaviour
    {
        public static IntroCinematicController Instance { get; private set; }

        [Header("UI References")]
        [Tooltip("The black overlay CanvasGroup (covers entire screen)")]
        [SerializeField] private CanvasGroup overlayCanvasGroup;

        [Tooltip("The text element for displaying cinematic lines")]
        [SerializeField] private TextMeshProUGUI cinematicText;

        [Tooltip("CanvasGroup for the text (for fading)")]
        [SerializeField] private CanvasGroup textCanvasGroup;

        [Header("Cinematic Lines")]
        [Tooltip("The lines of text to display during the cinematic")]
        [SerializeField] [TextArea(2, 4)] private string[] cinematicLines = new string[]
        {
            "My grandfather said he could hear it.",
            "A sound... a pull... something calling from beneath the ground.",
            "He started digging. He never reached it.",
            "Now it's my turn."
        };

        [Header("Timing Settings")]
        [Tooltip("Duration to fade in the black overlay")]
        [SerializeField] private float overlayFadeInDuration = 0.6f;

        [Tooltip("Duration to fade in each text line")]
        [SerializeField] private float textFadeInDuration = 0.35f;

        [Tooltip("Duration to hold each text line fully visible")]
        [SerializeField] private float textHoldDuration = 3.5f;

        [Tooltip("Duration to fade out each text line")]
        [SerializeField] private float textFadeOutDuration = 0.35f;

        [Tooltip("Gap between text lines")]
        [SerializeField] private float gapBetweenLines = 0.25f;

        [Tooltip("Hold black after last line")]
        [SerializeField] private float holdBlackAfterLastLine = 0.35f;

        [Tooltip("Duration to fade out the black overlay")]
        [SerializeField] private float overlayFadeOutDuration = 0.8f;

        [Header("Skip Keys")]
        [Tooltip("Keys that can skip the cinematic")]
        [SerializeField] private KeyCode[] skipKeys = new KeyCode[]
        {
            KeyCode.Space,
            KeyCode.Return,
            KeyCode.Escape
        };

        [Header("Player Control")]
        [Tooltip("Reference to the player's FirstPersonController (auto-finds if null)")]
        [SerializeField] private MonoBehaviour playerController;

        [Tooltip("Name of the player GameObject to search for")]
        [SerializeField] private string playerObjectName = "Player";

        [Header("Audio - Ambient")]
        [Tooltip("Ambient sound to play during the cinematic")]
        [SerializeField] private AudioClip cinematicAmbientSound;

        [Tooltip("Volume for the ambient sound")]
        [SerializeField] [Range(0f, 1f)] private float ambientVolume = 0.7f;

        [Tooltip("Fade out the sound when cinematic ends")]
        [SerializeField] private float audioFadeOutDuration = 1.5f;

        [Header("Audio - Voice Over")]
        [Tooltip("Voice-over audio clips for each sentence (same order as Cinematic Lines)")]
        [SerializeField] private AudioClip[] voiceOverClips;

        [Tooltip("Volume for voice-over audio")]
        [SerializeField] [Range(0f, 1f)] private float voiceOverVolume = 1f;

        [Tooltip("If true, wait for voice-over to finish before fading out text (ignores textHoldDuration if voice is longer)")]
        [SerializeField] private bool waitForVoiceOver = true;

        [Header("UI Hiding")]
        [Tooltip("Canvas/GameObject containing the game HUD to hide during cinematic")]
        [SerializeField] private GameObject hudRoot;

        [Tooltip("Names of HUD GameObjects to search for if hudRoot is not assigned")]
        [SerializeField] private string[] hudSearchNames = new string[]
        {
            "HUDCanvas",
            "HUDController",
            "GameHUD",
            "PlayerHUD"
        };

        // PlayerPrefs key to detect new game
        public const string NEW_GAME_FLAG_KEY = "UTFNewGameCinematic";

        // State
        private bool isPlaying;
        private bool wasSkipped;
        private Coroutine cinematicCoroutine;
        private AudioSource ambientAudioSource;
        private AudioSource voiceOverAudioSource;

        // Events
        public event System.Action OnCinematicStarted;
        public event System.Action OnCinematicEnded;

        private void Awake()
        {
            if (Instance != null && Instance != this)
            {
                Destroy(gameObject);
                return;
            }
            Instance = this;
        }

        private void Start()
        {
            // Initialize UI to hidden state
            InitializeUI();

            // Check if we should auto-play the cinematic (new game)
            if (ShouldPlayCinematic())
            {
                // Delay start to ensure other systems (HUD, etc.) have initialized
                StartCoroutine(DelayedPlayCinematic());
            }
            else
            {
                // Not a new game, ensure player has control
                EnablePlayerControl();
            }
        }

        private IEnumerator DelayedPlayCinematic()
        {
            // Wait for other systems to initialize
            yield return null; // Wait one frame for Awake
            yield return null; // Wait another frame for Start

            PlayCinematic();
        }

        private void Update()
        {
            if (isPlaying)
            {
                CheckForSkip();

                // Keep cursor hidden during cinematic (some systems may try to show it)
                if (Cursor.visible)
                {
                    Cursor.visible = false;
                    Cursor.lockState = CursorLockMode.Locked;
                }
            }
        }

        /// <summary>
        /// Initialize UI elements to their starting state.
        /// If cinematic should play, start with black screen to prevent flash.
        /// </summary>
        private void InitializeUI()
        {
            bool willPlayCinematic = ShouldPlayCinematic();

            if (overlayCanvasGroup != null)
            {
                // KEY FIX: If cinematic will play, start with BLACK screen (alpha=1)
                // This prevents the flash of the basement before the cinematic starts
                overlayCanvasGroup.alpha = willPlayCinematic ? 1f : 0f;
                overlayCanvasGroup.blocksRaycasts = willPlayCinematic;
                overlayCanvasGroup.interactable = false;
            }

            if (textCanvasGroup != null)
            {
                textCanvasGroup.alpha = 0f;
            }

            if (cinematicText != null)
            {
                cinematicText.text = "";
            }
        }

        /// <summary>
        /// Check if the cinematic should play (new game flag is set).
        /// </summary>
        private bool ShouldPlayCinematic()
        {
            return PlayerPrefs.GetInt(NEW_GAME_FLAG_KEY, 0) == 1;
        }

        /// <summary>
        /// Clear the new game flag.
        /// </summary>
        public static void ClearNewGameFlag()
        {
            PlayerPrefs.SetInt(NEW_GAME_FLAG_KEY, 0);
            PlayerPrefs.Save();
        }

        /// <summary>
        /// Set the new game flag (call this before loading the game scene).
        /// </summary>
        public static void SetNewGameFlag()
        {
            PlayerPrefs.SetInt(NEW_GAME_FLAG_KEY, 1);
            PlayerPrefs.Save();
        }

        /// <summary>
        /// Start playing the cinematic sequence.
        /// </summary>
        public void PlayCinematic()
        {
            if (isPlaying) return;

            // Clear the flag immediately
            ClearNewGameFlag();

            isPlaying = true;
            wasSkipped = false;

            // Disable player control
            DisablePlayerControl();

            // Start ambient audio
            PlayCinematicAudio();

            OnCinematicStarted?.Invoke();

            cinematicCoroutine = StartCoroutine(CinematicSequence());
        }

        /// <summary>
        /// Check for skip input.
        /// </summary>
        private void CheckForSkip()
        {
            foreach (var key in skipKeys)
            {
                if (Input.GetKeyDown(key))
                {
                    SkipCinematic();
                    return;
                }
            }
        }

        /// <summary>
        /// Skip the cinematic immediately.
        /// </summary>
        public void SkipCinematic()
        {
            if (!isPlaying) return;

            wasSkipped = true;

            if (cinematicCoroutine != null)
            {
                StopCoroutine(cinematicCoroutine);
                cinematicCoroutine = null;
            }

            // Immediately end the cinematic
            EndCinematic();
        }

        /// <summary>
        /// The main cinematic sequence coroutine.
        /// </summary>
        private IEnumerator CinematicSequence()
        {
            // Only fade in if not already black (prevents flash fix from being undone)
            if (overlayCanvasGroup != null && overlayCanvasGroup.alpha < 1f)
            {
                yield return FadeCanvasGroup(overlayCanvasGroup, 0f, 1f, overlayFadeInDuration);
            }

            if (overlayCanvasGroup != null)
            {
                overlayCanvasGroup.blocksRaycasts = true;
            }

            // Display each line
            for (int i = 0; i < cinematicLines.Length; i++)
            {
                if (wasSkipped) yield break;

                // Set the text
                if (cinematicText != null)
                {
                    cinematicText.text = cinematicLines[i];
                }

                // Play voice-over for this sentence (if available)
                float voiceClipLength = PlayVoiceOver(i);

                // Fade in text
                yield return FadeCanvasGroup(textCanvasGroup, 0f, 1f, textFadeInDuration);

                if (wasSkipped) yield break;

                // Calculate hold duration - use longer of textHoldDuration or voice clip length
                float holdTime = textHoldDuration;
                if (waitForVoiceOver && voiceClipLength > 0f)
                {
                    // Voice clip length minus fade in time (voice started at beginning)
                    float remainingVoiceTime = voiceClipLength - textFadeInDuration;
                    holdTime = Mathf.Max(holdTime, remainingVoiceTime + 0.3f); // +0.3s buffer after voice ends
                }

                // Hold text visible
                yield return WaitForSecondsUnscaled(holdTime);

                if (wasSkipped) yield break;

                // Fade out text
                yield return FadeCanvasGroup(textCanvasGroup, 1f, 0f, textFadeOutDuration);

                if (wasSkipped) yield break;

                // Gap before next line (except after last line)
                if (i < cinematicLines.Length - 1)
                {
                    yield return WaitForSecondsUnscaled(gapBetweenLines);
                }
            }

            // Hold black after last line
            yield return WaitForSecondsUnscaled(holdBlackAfterLastLine);

            if (wasSkipped) yield break;

            // Fade out black overlay
            yield return FadeCanvasGroup(overlayCanvasGroup, 1f, 0f, overlayFadeOutDuration);

            // End cinematic
            EndCinematic();
        }

        /// <summary>
        /// End the cinematic and restore player control.
        /// </summary>
        private void EndCinematic()
        {
            isPlaying = false;
            cinematicCoroutine = null;

            // Hide overlay immediately
            if (overlayCanvasGroup != null)
            {
                overlayCanvasGroup.alpha = 0f;
                overlayCanvasGroup.blocksRaycasts = false;
                overlayCanvasGroup.interactable = false;
            }

            // Hide text
            if (textCanvasGroup != null)
            {
                textCanvasGroup.alpha = 0f;
            }

            if (cinematicText != null)
            {
                cinematicText.text = "";
            }

            // Stop voice-over
            StopVoiceOver();

            // Fade out and stop ambient audio
            StopCinematicAudio();

            // Enable player control
            EnablePlayerControl();

            OnCinematicEnded?.Invoke();
        }

        /// <summary>
        /// Play the ambient sound for the cinematic.
        /// </summary>
        private void PlayCinematicAudio()
        {
            if (cinematicAmbientSound == null) return;

            // Create AudioSource if needed
            if (ambientAudioSource == null)
            {
                ambientAudioSource = gameObject.AddComponent<AudioSource>();
                ambientAudioSource.playOnAwake = false;
                ambientAudioSource.loop = true;
            }

            ambientAudioSource.clip = cinematicAmbientSound;
            ambientAudioSource.volume = ambientVolume;
            ambientAudioSource.Play();
        }

        /// <summary>
        /// Play voice-over audio for a specific sentence.
        /// </summary>
        /// <param name="sentenceIndex">Index of the sentence (0-based)</param>
        /// <returns>Length of the audio clip in seconds, or 0 if no clip</returns>
        private float PlayVoiceOver(int sentenceIndex)
        {
            // Check if we have a voice clip for this sentence
            if (voiceOverClips == null || sentenceIndex >= voiceOverClips.Length)
                return 0f;

            AudioClip clip = voiceOverClips[sentenceIndex];
            if (clip == null)
                return 0f;

            // Create voice-over AudioSource if needed
            if (voiceOverAudioSource == null)
            {
                voiceOverAudioSource = gameObject.AddComponent<AudioSource>();
                voiceOverAudioSource.playOnAwake = false;
                voiceOverAudioSource.loop = false;
                voiceOverAudioSource.spatialBlend = 0f; // 2D sound
            }

            // Play the voice clip
            voiceOverAudioSource.clip = clip;
            voiceOverAudioSource.volume = voiceOverVolume;
            voiceOverAudioSource.Play();

            return clip.length;
        }

        /// <summary>
        /// Stop any playing voice-over audio.
        /// </summary>
        private void StopVoiceOver()
        {
            if (voiceOverAudioSource != null && voiceOverAudioSource.isPlaying)
            {
                voiceOverAudioSource.Stop();
            }
        }

        /// <summary>
        /// Stop the ambient sound with fade out.
        /// </summary>
        private void StopCinematicAudio()
        {
            if (ambientAudioSource != null && ambientAudioSource.isPlaying)
            {
                StartCoroutine(FadeOutAudio());
            }
        }

        /// <summary>
        /// Fade out the audio source.
        /// </summary>
        private IEnumerator FadeOutAudio()
        {
            if (ambientAudioSource == null) yield break;

            float startVolume = ambientAudioSource.volume;
            float elapsed = 0f;

            while (elapsed < audioFadeOutDuration)
            {
                elapsed += Time.unscaledDeltaTime;
                float t = elapsed / audioFadeOutDuration;
                ambientAudioSource.volume = Mathf.Lerp(startVolume, 0f, t);
                yield return null;
            }

            ambientAudioSource.Stop();
            ambientAudioSource.volume = ambientVolume; // Reset for next time
        }

        /// <summary>
        /// Fade a CanvasGroup using unscaled time.
        /// </summary>
        private IEnumerator FadeCanvasGroup(CanvasGroup canvasGroup, float from, float to, float duration)
        {
            if (canvasGroup == null) yield break;

            float elapsed = 0f;
            canvasGroup.alpha = from;

            while (elapsed < duration)
            {
                if (wasSkipped) yield break;

                elapsed += Time.unscaledDeltaTime;
                float t = Mathf.Clamp01(elapsed / duration);
                // Smoothstep for nicer easing
                t = t * t * (3f - 2f * t);
                canvasGroup.alpha = Mathf.Lerp(from, to, t);
                yield return null;
            }

            canvasGroup.alpha = to;
        }

        /// <summary>
        /// Wait for seconds using unscaled time.
        /// </summary>
        private IEnumerator WaitForSecondsUnscaled(float seconds)
        {
            float elapsed = 0f;
            while (elapsed < seconds)
            {
                if (wasSkipped) yield break;
                elapsed += Time.unscaledDeltaTime;
                yield return null;
            }
        }

        /// <summary>
        /// Disable player control during cinematic.
        /// </summary>
        private void DisablePlayerControl()
        {
            var controller = GetPlayerController();
            if (controller != null)
            {
                controller.enabled = false;
            }

            // Hide HUD
            HideHUD();

            // Hide rope/cable
            HideRopeObjects();

            // Hide cursor completely
            Cursor.lockState = CursorLockMode.Locked;
            Cursor.visible = false;
        }

        /// <summary>
        /// Enable player control after cinematic.
        /// </summary>
        private void EnablePlayerControl()
        {
            var controller = GetPlayerController();
            if (controller != null)
            {
                controller.enabled = true;
            }

            // Show HUD
            ShowHUD();

            // Show rope/cable
            ShowRopeObjects();

            // Lock cursor for gameplay
            Cursor.lockState = CursorLockMode.Locked;
            Cursor.visible = false;
        }

        // List of canvases we hid so we can restore them
        private System.Collections.Generic.List<Canvas> hiddenCanvases = new System.Collections.Generic.List<Canvas>();

        // Rope/cable objects to hide during cinematic
        private System.Collections.Generic.List<GameObject> hiddenRopeObjects = new System.Collections.Generic.List<GameObject>();

        /// <summary>
        /// Hide ALL game UI canvases during cinematic (except our own).
        /// </summary>
        private void HideHUD()
        {
            hiddenCanvases.Clear();

            // Find ALL canvases in the scene
            Canvas[] allCanvases = FindObjectsOfType<Canvas>(true);
            Canvas ourCanvas = overlayCanvasGroup?.GetComponentInParent<Canvas>();

            foreach (var canvas in allCanvases)
            {
                // Skip our own cinematic canvas
                if (ourCanvas != null && canvas == ourCanvas)
                    continue;

                // Skip if it's a child of our canvas
                if (ourCanvas != null && canvas.transform.IsChildOf(ourCanvas.transform))
                    continue;

                // If canvas is active, hide it and remember it
                if (canvas.gameObject.activeSelf)
                {
                    canvas.gameObject.SetActive(false);
                    hiddenCanvases.Add(canvas);
                }
            }
        }

        /// <summary>
        /// Show the game HUD after cinematic.
        /// </summary>
        private void ShowHUD()
        {
            // Restore all canvases we hid
            foreach (var canvas in hiddenCanvases)
            {
                if (canvas != null)
                {
                    canvas.gameObject.SetActive(true);
                }
            }
            hiddenCanvases.Clear();
        }

        /// <summary>
        /// Hide rope/cable objects during cinematic.
        /// </summary>
        private void HideRopeObjects()
        {
            hiddenRopeObjects.Clear();

            // Search for common rope/cable object names
            string[] ropeNames = { "Rope", "Cable", "VerletRope", "WinchCable", "WinchRope" };

            foreach (var name in ropeNames)
            {
                var found = GameObject.Find(name);
                if (found != null && found.activeSelf)
                {
                    found.SetActive(false);
                    hiddenRopeObjects.Add(found);
                }
            }

            // Also find by component type if possible (LineRenderer is commonly used for ropes)
            LineRenderer[] lineRenderers = FindObjectsOfType<LineRenderer>(true);
            foreach (var lr in lineRenderers)
            {
                // Check if it's related to winch/rope
                string objName = lr.gameObject.name.ToLower();
                if ((objName.Contains("rope") || objName.Contains("cable") || objName.Contains("winch"))
                    && lr.gameObject.activeSelf)
                {
                    lr.gameObject.SetActive(false);
                    if (!hiddenRopeObjects.Contains(lr.gameObject))
                    {
                        hiddenRopeObjects.Add(lr.gameObject);
                    }
                }
            }
        }

        /// <summary>
        /// Show rope/cable objects after cinematic.
        /// </summary>
        private void ShowRopeObjects()
        {
            foreach (var obj in hiddenRopeObjects)
            {
                if (obj != null)
                {
                    obj.SetActive(true);
                }
            }
            hiddenRopeObjects.Clear();
        }

        /// <summary>
        /// Get the player controller component.
        /// </summary>
        private MonoBehaviour GetPlayerController()
        {
            if (playerController != null)
                return playerController;

            // Try to find player by name
            var playerObj = GameObject.Find(playerObjectName);
            if (playerObj != null)
            {
                // Look for FirstPersonController component
                playerController = playerObj.GetComponent("FirstPersonController") as MonoBehaviour;
                if (playerController == null)
                {
                    // Try common controller names
                    playerController = playerObj.GetComponent("PlayerController") as MonoBehaviour;
                }
            }

            return playerController;
        }

        /// <summary>
        /// Check if the cinematic is currently playing.
        /// </summary>
        public bool IsPlaying => isPlaying;

        private void OnDestroy()
        {
            if (Instance == this)
            {
                Instance = null;
            }
        }
    }
}
