using UnityEngine;
using UnityEngine.UI;
using UnityEngine.Video;

namespace BeneathTheFloor.GameFlow
{
    /// <summary>
    /// Plays a looping video in the background of the main menu.
    /// The video is rendered to a RawImage behind all other UI elements.
    /// </summary>
    public class MainMenuVideoBackground : MonoBehaviour
    {
        [Header("Video Settings")]
        [Tooltip("The video clip to play in the background")]
        [SerializeField] private VideoClip videoClip;

        [Tooltip("Whether the video should loop")]
        [SerializeField] private bool loop = true;

        [Tooltip("Playback speed (1 = normal)")]
        [SerializeField] [Range(0.1f, 2f)] private float playbackSpeed = 1f;

        [Header("Video Audio")]
        [Tooltip("Whether to play the video's built-in audio")]
        [SerializeField] private bool playVideoAudio = false;

        [Tooltip("Volume for video audio (if enabled)")]
        [SerializeField] [Range(0f, 1f)] private float videoAudioVolume = 0.5f;

        [Header("Background Music")]
        [Tooltip("Audio clip to play in a loop (separate from video audio)")]
        [SerializeField] private AudioClip backgroundMusic;

        [Tooltip("Volume for background music")]
        [SerializeField] [Range(0f, 1f)] private float musicVolume = 0.5f;

        [Tooltip("Fade in duration for music (seconds)")]
        [SerializeField] private float musicFadeInDuration = 1f;

        [Header("Visual Settings")]
        [Tooltip("Tint color applied to the video")]
        [SerializeField] private Color tintColor = Color.white;

        [Tooltip("Darken the video (0 = full brightness, 1 = black)")]
        [SerializeField] [Range(0f, 0.9f)] private float darkenAmount = 0.3f;

        [Header("References (Auto-created if null)")]
        [SerializeField] private RawImage videoDisplay;
        [SerializeField] private VideoPlayer videoPlayer;

        private RenderTexture renderTexture;
        private AudioSource musicAudioSource;

        private void Awake()
        {
            SetupVideoPlayer();
        }

        private void Start()
        {
            if (videoClip != null)
            {
                PlayVideo();
            }
            else
            {
                Debug.LogWarning("[MainMenuVideoBackground] No video clip assigned!");
            }
        }

        private void SetupVideoPlayer()
        {
            // Create VideoPlayer if not assigned
            if (videoPlayer == null)
            {
                videoPlayer = GetComponent<VideoPlayer>();
                if (videoPlayer == null)
                {
                    videoPlayer = gameObject.AddComponent<VideoPlayer>();
                }
            }

            // Configure VideoPlayer
            videoPlayer.playOnAwake = false;
            videoPlayer.isLooping = loop;
            videoPlayer.playbackSpeed = playbackSpeed;
            videoPlayer.renderMode = VideoRenderMode.RenderTexture;

            // Video audio settings
            if (playVideoAudio)
            {
                videoPlayer.audioOutputMode = VideoAudioOutputMode.Direct;
                videoPlayer.SetDirectAudioVolume(0, videoAudioVolume);
            }
            else
            {
                videoPlayer.audioOutputMode = VideoAudioOutputMode.None;
            }

            // Setup background music
            SetupBackgroundMusic();

            // Find RawImage on this object first
            if (videoDisplay == null)
            {
                videoDisplay = GetComponent<RawImage>();
            }

            // Create RenderTexture
            if (videoClip != null)
            {
                CreateRenderTexture((int)videoClip.width, (int)videoClip.height);
            }
            else
            {
                // Default size, will be resized when clip is set
                CreateRenderTexture(1920, 1080);
            }

            // Create RawImage if still null
            if (videoDisplay == null)
            {
                CreateVideoDisplay();
            }
            else
            {
                // Assign the render texture to existing RawImage
                videoDisplay.texture = renderTexture;

                // Ensure the canvas has correct sorting order
                Canvas parentCanvas = videoDisplay.GetComponentInParent<Canvas>();
                if (parentCanvas != null && parentCanvas.sortingOrder > -100)
                {
                    // Create a new background canvas and move the video there
                    CreateDedicatedBackgroundCanvas();
                }
            }

            // Apply tint and darken
            ApplyVisualSettings();
        }

        private void CreateRenderTexture(int width, int height)
        {
            // Clean up old texture
            if (renderTexture != null)
            {
                renderTexture.Release();
                Destroy(renderTexture);
            }

            renderTexture = new RenderTexture(width, height, 0);
            renderTexture.Create();
            videoPlayer.targetTexture = renderTexture;

            if (videoDisplay != null)
            {
                videoDisplay.texture = renderTexture;
            }
        }

        private void CreateVideoDisplay()
        {
            CreateDedicatedBackgroundCanvas();
        }

        private void CreateDedicatedBackgroundCanvas()
        {
            // Create a dedicated canvas for video background with lowest sorting order
            GameObject canvasObj = new GameObject("VideoBackgroundCanvas");
            Canvas canvas = canvasObj.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = -100; // Behind everything

            var scaler = canvasObj.AddComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920, 1080);

            // Create RawImage for video display
            GameObject displayObj = new GameObject("VideoDisplay");
            displayObj.transform.SetParent(canvas.transform, false);

            videoDisplay = displayObj.AddComponent<RawImage>();
            videoDisplay.texture = renderTexture;
            videoDisplay.raycastTarget = false; // Don't block input

            // Stretch to fill screen
            RectTransform rect = displayObj.GetComponent<RectTransform>();
            rect.anchorMin = Vector2.zero;
            rect.anchorMax = Vector2.one;
            rect.offsetMin = Vector2.zero;
            rect.offsetMax = Vector2.zero;
        }

        private void ApplyVisualSettings()
        {
            if (videoDisplay != null)
            {
                // Apply tint and darken
                Color finalColor = tintColor;
                finalColor.r *= (1f - darkenAmount);
                finalColor.g *= (1f - darkenAmount);
                finalColor.b *= (1f - darkenAmount);
                videoDisplay.color = finalColor;
            }
        }

        private void SetupBackgroundMusic()
        {
            if (backgroundMusic == null) return;

            // Create AudioSource for background music
            musicAudioSource = gameObject.AddComponent<AudioSource>();
            musicAudioSource.clip = backgroundMusic;
            musicAudioSource.loop = true;
            musicAudioSource.playOnAwake = false;
            musicAudioSource.volume = 0f; // Start at 0 for fade in
            musicAudioSource.spatialBlend = 0f; // 2D sound
        }

        private void PlayBackgroundMusic()
        {
            if (musicAudioSource == null || backgroundMusic == null) return;

            musicAudioSource.Play();
            StartCoroutine(FadeMusicIn());
        }

        private System.Collections.IEnumerator FadeMusicIn()
        {
            float elapsed = 0f;
            while (elapsed < musicFadeInDuration)
            {
                elapsed += Time.deltaTime;
                float t = elapsed / musicFadeInDuration;
                musicAudioSource.volume = Mathf.Lerp(0f, musicVolume, t);
                yield return null;
            }
            musicAudioSource.volume = musicVolume;
        }

        /// <summary>
        /// Stop the background music.
        /// </summary>
        public void StopBackgroundMusic()
        {
            if (musicAudioSource != null)
            {
                musicAudioSource.Stop();
            }
        }

        /// <summary>
        /// Set background music volume at runtime.
        /// </summary>
        public void SetMusicVolume(float volume)
        {
            musicVolume = Mathf.Clamp01(volume);
            if (musicAudioSource != null)
            {
                musicAudioSource.volume = musicVolume;
            }
        }

        /// <summary>
        /// Play the background video.
        /// </summary>
        public void PlayVideo()
        {
            if (videoPlayer == null || videoClip == null) return;

            videoPlayer.clip = videoClip;
            videoPlayer.isLooping = loop;

            // Resize render texture to match video
            if (renderTexture == null ||
                renderTexture.width != (int)videoClip.width ||
                renderTexture.height != (int)videoClip.height)
            {
                CreateRenderTexture((int)videoClip.width, (int)videoClip.height);
            }

            videoPlayer.Play();

            // Start background music
            PlayBackgroundMusic();
        }

        /// <summary>
        /// Stop the background video.
        /// </summary>
        public void StopVideo()
        {
            if (videoPlayer != null)
            {
                videoPlayer.Stop();
            }
        }

        /// <summary>
        /// Pause the background video.
        /// </summary>
        public void PauseVideo()
        {
            if (videoPlayer != null)
            {
                videoPlayer.Pause();
            }
        }

        /// <summary>
        /// Set a new video clip at runtime.
        /// </summary>
        public void SetVideoClip(VideoClip clip)
        {
            videoClip = clip;
            if (clip != null && videoPlayer != null)
            {
                PlayVideo();
            }
        }

        /// <summary>
        /// Set the darken amount at runtime.
        /// </summary>
        public void SetDarkenAmount(float amount)
        {
            darkenAmount = Mathf.Clamp(amount, 0f, 0.9f);
            ApplyVisualSettings();
        }

        /// <summary>
        /// Set the tint color at runtime.
        /// </summary>
        public void SetTintColor(Color color)
        {
            tintColor = color;
            ApplyVisualSettings();
        }

        private void OnDestroy()
        {
            if (renderTexture != null)
            {
                renderTexture.Release();
                Destroy(renderTexture);
            }
        }

        private void OnValidate()
        {
            // Apply changes in editor
            if (videoPlayer != null)
            {
                videoPlayer.isLooping = loop;
                videoPlayer.playbackSpeed = playbackSpeed;

                if (playVideoAudio)
                {
                    videoPlayer.audioOutputMode = VideoAudioOutputMode.Direct;
                    videoPlayer.SetDirectAudioVolume(0, videoAudioVolume);
                }
                else
                {
                    videoPlayer.audioOutputMode = VideoAudioOutputMode.None;
                }
            }

            // Update music volume if playing
            if (musicAudioSource != null && musicAudioSource.isPlaying)
            {
                musicAudioSource.volume = musicVolume;
            }

            ApplyVisualSettings();
        }
    }
}
