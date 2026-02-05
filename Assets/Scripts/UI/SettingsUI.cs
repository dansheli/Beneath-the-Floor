using UnityEngine;
using UnityEngine.UI;
using UnityEngine.EventSystems;
using TMPro;
using System.Collections.Generic;

namespace BeneathTheFloor.UI
{
    /// <summary>
    /// Runtime-created Settings UI panel.
    /// Creates a clean, properly styled settings menu.
    /// </summary>
    public class SettingsUI : MonoBehaviour
    {
        public static SettingsUI Instance { get; private set; }

        // Runtime created references
        private GameObject menuPanel;
        private Slider masterVolumeSlider;
        private Slider musicVolumeSlider;
        private Slider sfxVolumeSlider;
        private Toggle fullscreenToggle;
        private TMP_Dropdown resolutionDropdown;
        private TMP_Dropdown qualityDropdown;
        private TMP_Dropdown fpsDropdown;
        private Button backButton;

        // State
        private bool isOpen;
        private Resolution[] availableResolutions;

        // Colors
        private static readonly Color PANEL_BG = new Color(0.08f, 0.08f, 0.1f, 0.98f);
        private static readonly Color DARK_BG = new Color(0.02f, 0.02f, 0.05f, 0.95f);
        private static readonly Color ACCENT_COLOR = new Color(0.85f, 0.65f, 0.25f, 1f);
        private static readonly Color SLIDER_BG = new Color(0.15f, 0.15f, 0.18f, 1f);
        private static readonly Color BUTTON_COLOR = new Color(0.2f, 0.2f, 0.25f, 1f);
        private static readonly Color BUTTON_HOVER = new Color(0.3f, 0.3f, 0.35f, 1f);

        // PlayerPrefs keys
        private const string PREF_MASTER_VOLUME = "MasterVolume";
        private const string PREF_MUSIC_VOLUME = "MusicVolume";
        private const string PREF_SFX_VOLUME = "SfxVolume";
        private const string PREF_FULLSCREEN = "Fullscreen";
        private const string PREF_RESOLUTION = "Resolution";
        private const string PREF_QUALITY = "Quality";
        private const string PREF_FPS_LIMIT = "FpsLimit";

        // FPS limit options (index matches dropdown)
        private static readonly int[] FPS_OPTIONS = { -1, 144, 120, 60 }; // -1 = unlimited

        /// <summary>
        /// Event fired when menu is closed.
        /// </summary>
        public System.Action OnMenuClosed;

        private void Awake()
        {
            if (Instance == null)
            {
                Instance = this;
                // One-time cleanup of potentially bad resolution settings
                CleanupBadSettings();
                // Apply saved FPS limit on startup
                ApplySavedFpsLimit();
            }
            else if (Instance != this)
            {
                Destroy(gameObject);
                return;
            }
        }

        /// <summary>
        /// Apply the saved FPS limit setting. Can be called at game start.
        /// </summary>
        public static void ApplySavedFpsLimit()
        {
            int savedFpsIndex = PlayerPrefs.GetInt(PREF_FPS_LIMIT, 0);
            savedFpsIndex = Mathf.Clamp(savedFpsIndex, 0, FPS_OPTIONS.Length - 1);
            Application.targetFrameRate = FPS_OPTIONS[savedFpsIndex];
        }

        /// <summary>
        /// Apply FPS limit automatically when the game starts.
        /// </summary>
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
        private static void OnGameStart()
        {
            ApplySavedFpsLimit();
        }

        /// <summary>
        /// Clear bad settings that might have been saved (e.g., very low resolution).
        /// </summary>
        private void CleanupBadSettings()
        {
            // If resolution was saved and it's pointing to an index that would be a low res,
            // just delete the key so we use the current/native resolution
            if (PlayerPrefs.HasKey(PREF_RESOLUTION))
            {
                int savedIndex = PlayerPrefs.GetInt(PREF_RESOLUTION, 0);
                // If the index seems unreasonably high (old unfiltered list), reset it
                if (savedIndex > 20)
                {
                    Debug.Log("[SettingsUI] Clearing bad resolution preference");
                    PlayerPrefs.DeleteKey(PREF_RESOLUTION);
                    PlayerPrefs.Save();
                }
            }
        }

        private void Update()
        {
            if (!isOpen) return;

            // ESC to close
            if (Input.GetKeyDown(KeyCode.Escape))
            {
                Close();
            }
        }

        /// <summary>
        /// Open the settings menu.
        /// </summary>
        public void Open()
        {
            if (menuPanel == null)
            {
                CreateUI();
            }

            LoadSettings();
            isOpen = true;

            if (menuPanel != null)
            {
                menuPanel.SetActive(true);
            }
        }

        /// <summary>
        /// Close the settings menu.
        /// </summary>
        public void Close()
        {
            Debug.Log("[SettingsUI] Close() called");
            SaveSettings();
            isOpen = false;

            if (menuPanel != null)
            {
                menuPanel.SetActive(false);
            }

            OnMenuClosed?.Invoke();
        }

        /// <summary>
        /// Check if menu is currently open.
        /// </summary>
        public bool IsOpen => isOpen;

        /// <summary>
        /// Load settings from PlayerPrefs and apply to UI.
        /// </summary>
        private void LoadSettings()
        {
            // Master volume
            float masterVol = PlayerPrefs.GetFloat(PREF_MASTER_VOLUME, 1f);
            if (masterVolumeSlider != null)
            {
                masterVolumeSlider.value = masterVol;
            }
            AudioListener.volume = masterVol;

            // Music volume (placeholder - needs AudioMixer integration)
            float musicVol = PlayerPrefs.GetFloat(PREF_MUSIC_VOLUME, 1f);
            if (musicVolumeSlider != null)
            {
                musicVolumeSlider.value = musicVol;
            }

            // SFX volume (placeholder - needs AudioMixer integration)
            float sfxVol = PlayerPrefs.GetFloat(PREF_SFX_VOLUME, 1f);
            if (sfxVolumeSlider != null)
            {
                sfxVolumeSlider.value = sfxVol;
            }

            // Fullscreen
            bool fullscreen = PlayerPrefs.GetInt(PREF_FULLSCREEN, Screen.fullScreen ? 1 : 0) == 1;
            if (fullscreenToggle != null)
            {
                fullscreenToggle.isOn = fullscreen;
            }

            // Resolution - setup dropdown and select current (don't auto-apply changes)
            SetupResolutionDropdown();
            int currentResIndex = GetCurrentResolutionIndex();
            // Only use saved resolution if it was explicitly saved before
            int resIndex = PlayerPrefs.HasKey(PREF_RESOLUTION) ?
                PlayerPrefs.GetInt(PREF_RESOLUTION, currentResIndex) : currentResIndex;
            // Clamp to valid range
            if (resolutionDropdown != null && availableResolutions != null)
            {
                resIndex = Mathf.Clamp(resIndex, 0, availableResolutions.Length - 1);
                resolutionDropdown.SetValueWithoutNotify(resIndex); // Don't trigger change event on load
            }

            // Quality - show current setting (don't auto-apply changes)
            if (qualityDropdown != null)
            {
                int currentQuality = QualitySettings.GetQualityLevel();
                int savedQuality = PlayerPrefs.HasKey(PREF_QUALITY) ?
                    PlayerPrefs.GetInt(PREF_QUALITY, currentQuality) : currentQuality;
                savedQuality = Mathf.Clamp(savedQuality, 0, QualitySettings.names.Length - 1);
                qualityDropdown.SetValueWithoutNotify(savedQuality);
            }

            // FPS Limit
            int savedFpsIndex = PlayerPrefs.GetInt(PREF_FPS_LIMIT, 0); // Default to Unlimited
            savedFpsIndex = Mathf.Clamp(savedFpsIndex, 0, FPS_OPTIONS.Length - 1);
            if (fpsDropdown != null)
            {
                fpsDropdown.SetValueWithoutNotify(savedFpsIndex);
            }
            // Apply the FPS limit
            Application.targetFrameRate = FPS_OPTIONS[savedFpsIndex];
        }

        /// <summary>
        /// Save settings to PlayerPrefs.
        /// </summary>
        private void SaveSettings()
        {
            if (masterVolumeSlider != null)
                PlayerPrefs.SetFloat(PREF_MASTER_VOLUME, masterVolumeSlider.value);

            if (musicVolumeSlider != null)
                PlayerPrefs.SetFloat(PREF_MUSIC_VOLUME, musicVolumeSlider.value);

            if (sfxVolumeSlider != null)
                PlayerPrefs.SetFloat(PREF_SFX_VOLUME, sfxVolumeSlider.value);

            if (fullscreenToggle != null)
                PlayerPrefs.SetInt(PREF_FULLSCREEN, fullscreenToggle.isOn ? 1 : 0);

            if (resolutionDropdown != null)
                PlayerPrefs.SetInt(PREF_RESOLUTION, resolutionDropdown.value);

            if (qualityDropdown != null)
                PlayerPrefs.SetInt(PREF_QUALITY, qualityDropdown.value);

            if (fpsDropdown != null)
                PlayerPrefs.SetInt(PREF_FPS_LIMIT, fpsDropdown.value);

            PlayerPrefs.Save();
        }

        private int GetCurrentResolutionIndex()
        {
            if (availableResolutions == null || availableResolutions.Length == 0)
                return 0;

            // Try to find current resolution
            for (int i = 0; i < availableResolutions.Length; i++)
            {
                if (availableResolutions[i].width == Screen.currentResolution.width &&
                    availableResolutions[i].height == Screen.currentResolution.height)
                {
                    return i;
                }
            }

            // Default to highest available resolution
            return availableResolutions.Length - 1;
        }

        private void SetupResolutionDropdown()
        {
            if (resolutionDropdown == null) return;

            // Filter to reasonable resolutions (720p and up)
            List<Resolution> filtered = new List<Resolution>();
            HashSet<string> seen = new HashSet<string>();

            foreach (var res in Screen.resolutions)
            {
                // Skip resolutions smaller than 1280x720
                if (res.width < 1280 || res.height < 720) continue;

                string key = $"{res.width}x{res.height}";
                if (!seen.Contains(key))
                {
                    filtered.Add(res);
                    seen.Add(key);
                }
            }

            availableResolutions = filtered.ToArray();
            resolutionDropdown.ClearOptions();

            List<string> options = new List<string>();
            foreach (var res in availableResolutions)
            {
                options.Add($"{res.width} x {res.height}");
            }

            resolutionDropdown.AddOptions(options);
        }

        /// <summary>
        /// Create the entire UI programmatically.
        /// </summary>
        private void CreateUI()
        {
            // Ensure EventSystem exists
            EnsureEventSystemExists();

            // Main panel with Canvas
            menuPanel = new GameObject("SettingsMenuPanel");
            menuPanel.transform.SetParent(transform, false);

            Canvas canvas = menuPanel.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = 1500; // Below SaveLoadMenuUI but above game

            CanvasScaler scaler = menuPanel.AddComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920, 1080);

            menuPanel.AddComponent<GraphicRaycaster>();

            // Dark background overlay
            GameObject bgObj = new GameObject("Background");
            bgObj.transform.SetParent(menuPanel.transform, false);
            RectTransform bgRect = bgObj.AddComponent<RectTransform>();
            bgRect.anchorMin = Vector2.zero;
            bgRect.anchorMax = Vector2.one;
            bgRect.offsetMin = Vector2.zero;
            bgRect.offsetMax = Vector2.zero;
            Image bgImg = bgObj.AddComponent<Image>();
            bgImg.color = DARK_BG;

            // Content panel (centered) - taller to fit all content including FPS
            GameObject contentObj = new GameObject("Content");
            contentObj.transform.SetParent(menuPanel.transform, false);
            RectTransform contentRect = contentObj.AddComponent<RectTransform>();
            contentRect.anchorMin = new Vector2(0.5f, 0.5f);
            contentRect.anchorMax = new Vector2(0.5f, 0.5f);
            contentRect.sizeDelta = new Vector2(420, 460);
            contentRect.anchoredPosition = Vector2.zero;

            Image contentBg = contentObj.AddComponent<Image>();
            contentBg.color = PANEL_BG;

            VerticalLayoutGroup contentLayout = contentObj.AddComponent<VerticalLayoutGroup>();
            contentLayout.padding = new RectOffset(25, 25, 15, 15);
            contentLayout.spacing = 5;
            contentLayout.childControlWidth = true;
            contentLayout.childControlHeight = true; // Control height using LayoutElement
            contentLayout.childForceExpandWidth = true;
            contentLayout.childForceExpandHeight = false;

            // Title
            CreateTitle(contentObj.transform, "SETTINGS");

            // Audio Section
            CreateSectionHeader(contentObj.transform, "AUDIO");
            masterVolumeSlider = CreateSliderRow(contentObj.transform, "Master Volume", OnMasterVolumeChanged);
            musicVolumeSlider = CreateSliderRow(contentObj.transform, "Music Volume", OnMusicVolumeChanged);
            sfxVolumeSlider = CreateSliderRow(contentObj.transform, "SFX Volume", OnSfxVolumeChanged);

            // Spacer
            CreateSpacer(contentObj.transform, 8);

            // Graphics Section
            CreateSectionHeader(contentObj.transform, "GRAPHICS");
            fullscreenToggle = CreateToggleRow(contentObj.transform, "Fullscreen", OnFullscreenChanged);
            resolutionDropdown = CreateDropdownRow(contentObj.transform, "Resolution", OnResolutionChanged);
            qualityDropdown = CreateDropdownRow(contentObj.transform, "Quality", OnQualityChanged);

            // Setup quality dropdown options
            if (qualityDropdown != null)
            {
                qualityDropdown.ClearOptions();
                qualityDropdown.AddOptions(new List<string>(QualitySettings.names));
            }

            // FPS Limit dropdown
            fpsDropdown = CreateDropdownRow(contentObj.transform, "FPS Limit", OnFpsChanged);
            if (fpsDropdown != null)
            {
                fpsDropdown.ClearOptions();
                fpsDropdown.AddOptions(new List<string> { "Unlimited", "144", "120", "60" });
            }

            // Spacer before button
            CreateSpacer(contentObj.transform, 15);

            // Back button at bottom
            backButton = CreateButton(contentObj.transform, "Back", OnBackClicked);

            menuPanel.SetActive(false);
        }

        private void CreateTitle(Transform parent, string text)
        {
            GameObject titleObj = new GameObject("Title");
            titleObj.transform.SetParent(parent, false);
            TextMeshProUGUI titleText = titleObj.AddComponent<TextMeshProUGUI>();
            titleText.text = text;
            titleText.fontSize = 24;
            titleText.fontStyle = FontStyles.Bold;
            titleText.alignment = TextAlignmentOptions.Center;
            titleText.color = Color.white;
            LayoutElement titleLayout = titleObj.AddComponent<LayoutElement>();
            titleLayout.preferredHeight = 35;
            titleLayout.minHeight = 35;
        }

        private void CreateSectionHeader(Transform parent, string text)
        {
            GameObject headerObj = new GameObject($"Header_{text}");
            headerObj.transform.SetParent(parent, false);
            TextMeshProUGUI headerText = headerObj.AddComponent<TextMeshProUGUI>();
            headerText.text = text;
            headerText.fontSize = 12;
            headerText.fontStyle = FontStyles.Bold;
            headerText.alignment = TextAlignmentOptions.Left;
            headerText.color = ACCENT_COLOR;
            LayoutElement headerLayout = headerObj.AddComponent<LayoutElement>();
            headerLayout.preferredHeight = 18;
            headerLayout.minHeight = 18;
        }

        private void CreateSpacer(Transform parent, float height)
        {
            GameObject spacerObj = new GameObject("Spacer");
            spacerObj.transform.SetParent(parent, false);
            spacerObj.AddComponent<RectTransform>();
            LayoutElement spacerLayout = spacerObj.AddComponent<LayoutElement>();
            spacerLayout.preferredHeight = height;
            spacerLayout.minHeight = height;
        }

        private void CreateFlexibleSpacer(Transform parent)
        {
            GameObject spacerObj = new GameObject("FlexibleSpacer");
            spacerObj.transform.SetParent(parent, false);
            spacerObj.AddComponent<RectTransform>();
            LayoutElement spacerLayout = spacerObj.AddComponent<LayoutElement>();
            spacerLayout.flexibleHeight = 1;
        }

        private Slider CreateSliderRow(Transform parent, string label, UnityEngine.Events.UnityAction<float> onValueChanged)
        {
            GameObject rowObj = new GameObject($"Row_{label.Replace(" ", "")}");
            rowObj.transform.SetParent(parent, false);
            RectTransform rowRect = rowObj.AddComponent<RectTransform>();
            rowRect.sizeDelta = new Vector2(420, 30);

            LayoutElement rowLE = rowObj.AddComponent<LayoutElement>();
            rowLE.preferredHeight = 30;
            rowLE.minHeight = 30;

            // Label (left side)
            GameObject labelObj = new GameObject("Label");
            labelObj.transform.SetParent(rowObj.transform, false);
            RectTransform labelRect = labelObj.AddComponent<RectTransform>();
            labelRect.anchorMin = new Vector2(0, 0);
            labelRect.anchorMax = new Vector2(0, 1);
            labelRect.pivot = new Vector2(0, 0.5f);
            labelRect.anchoredPosition = new Vector2(0, 0);
            labelRect.sizeDelta = new Vector2(120, 0);
            TextMeshProUGUI labelText = labelObj.AddComponent<TextMeshProUGUI>();
            labelText.text = label;
            labelText.fontSize = 14;
            labelText.color = Color.white;
            labelText.alignment = TextAlignmentOptions.Left;

            // Slider (middle) - explicit size
            GameObject sliderObj = new GameObject("Slider");
            sliderObj.transform.SetParent(rowObj.transform, false);
            RectTransform sliderRect = sliderObj.AddComponent<RectTransform>();
            sliderRect.anchorMin = new Vector2(0, 0.5f);
            sliderRect.anchorMax = new Vector2(0, 0.5f);
            sliderRect.pivot = new Vector2(0, 0.5f);
            sliderRect.anchoredPosition = new Vector2(125, 0);
            sliderRect.sizeDelta = new Vector2(200, 12); // Thin horizontal bar

            // Background (the track)
            GameObject bgObj = new GameObject("Background");
            bgObj.transform.SetParent(sliderObj.transform, false);
            RectTransform bgRect = bgObj.AddComponent<RectTransform>();
            bgRect.anchorMin = Vector2.zero;
            bgRect.anchorMax = Vector2.one;
            bgRect.offsetMin = Vector2.zero;
            bgRect.offsetMax = Vector2.zero;
            Image bgImg = bgObj.AddComponent<Image>();
            bgImg.color = SLIDER_BG;

            // Fill Area
            GameObject fillAreaObj = new GameObject("Fill Area");
            fillAreaObj.transform.SetParent(sliderObj.transform, false);
            RectTransform fillAreaRect = fillAreaObj.AddComponent<RectTransform>();
            fillAreaRect.anchorMin = Vector2.zero;
            fillAreaRect.anchorMax = Vector2.one;
            fillAreaRect.offsetMin = Vector2.zero;
            fillAreaRect.offsetMax = Vector2.zero;

            // Fill
            GameObject fillObj = new GameObject("Fill");
            fillObj.transform.SetParent(fillAreaObj.transform, false);
            RectTransform fillRect = fillObj.AddComponent<RectTransform>();
            fillRect.anchorMin = Vector2.zero;
            fillRect.anchorMax = new Vector2(0, 1);
            fillRect.offsetMin = Vector2.zero;
            fillRect.offsetMax = Vector2.zero;
            Image fillImg = fillObj.AddComponent<Image>();
            fillImg.color = ACCENT_COLOR;

            // Handle Slide Area
            GameObject handleAreaObj = new GameObject("Handle Slide Area");
            handleAreaObj.transform.SetParent(sliderObj.transform, false);
            RectTransform handleAreaRect = handleAreaObj.AddComponent<RectTransform>();
            handleAreaRect.anchorMin = Vector2.zero;
            handleAreaRect.anchorMax = Vector2.one;
            handleAreaRect.offsetMin = new Vector2(6, 0);
            handleAreaRect.offsetMax = new Vector2(-6, 0);

            // Handle (small circle)
            GameObject handleObj = new GameObject("Handle");
            handleObj.transform.SetParent(handleAreaObj.transform, false);
            RectTransform handleRect = handleObj.AddComponent<RectTransform>();
            handleRect.anchorMin = new Vector2(0, 0.5f);
            handleRect.anchorMax = new Vector2(0, 0.5f);
            handleRect.pivot = new Vector2(0.5f, 0.5f);
            handleRect.sizeDelta = new Vector2(14, 14);
            Image handleImg = handleObj.AddComponent<Image>();
            handleImg.color = Color.white;

            // Create Slider component
            Slider slider = sliderObj.AddComponent<Slider>();
            slider.fillRect = fillRect;
            slider.handleRect = handleRect;
            slider.targetGraphic = handleImg;
            slider.direction = Slider.Direction.LeftToRight;
            slider.minValue = 0;
            slider.maxValue = 1;
            slider.value = 1;

            if (onValueChanged != null)
            {
                slider.onValueChanged.AddListener(onValueChanged);
            }

            // Value text (right side)
            GameObject valueObj = new GameObject("Value");
            valueObj.transform.SetParent(rowObj.transform, false);
            RectTransform valueRect = valueObj.AddComponent<RectTransform>();
            valueRect.anchorMin = new Vector2(1, 0);
            valueRect.anchorMax = new Vector2(1, 1);
            valueRect.pivot = new Vector2(1, 0.5f);
            valueRect.anchoredPosition = new Vector2(0, 0);
            valueRect.sizeDelta = new Vector2(50, 0);
            TextMeshProUGUI valueText = valueObj.AddComponent<TextMeshProUGUI>();
            valueText.fontSize = 12;
            valueText.color = new Color(0.7f, 0.7f, 0.7f);
            valueText.alignment = TextAlignmentOptions.Right;

            // Update value text when slider changes
            slider.onValueChanged.AddListener((val) => {
                valueText.text = Mathf.RoundToInt(val * 100) + "%";
            });
            valueText.text = "100%";

            return slider;
        }

        private Toggle CreateToggleRow(Transform parent, string label, UnityEngine.Events.UnityAction<bool> onValueChanged)
        {
            GameObject rowObj = new GameObject($"Row_{label.Replace(" ", "")}");
            rowObj.transform.SetParent(parent, false);
            RectTransform rowRect = rowObj.AddComponent<RectTransform>();
            rowRect.sizeDelta = new Vector2(420, 30);

            LayoutElement rowLE = rowObj.AddComponent<LayoutElement>();
            rowLE.preferredHeight = 30;
            rowLE.minHeight = 30;

            // Label (left side)
            GameObject labelObj = new GameObject("Label");
            labelObj.transform.SetParent(rowObj.transform, false);
            RectTransform labelRect = labelObj.AddComponent<RectTransform>();
            labelRect.anchorMin = new Vector2(0, 0);
            labelRect.anchorMax = new Vector2(0, 1);
            labelRect.pivot = new Vector2(0, 0.5f);
            labelRect.anchoredPosition = new Vector2(0, 0);
            labelRect.sizeDelta = new Vector2(120, 0);
            TextMeshProUGUI labelText = labelObj.AddComponent<TextMeshProUGUI>();
            labelText.text = label;
            labelText.fontSize = 14;
            labelText.color = Color.white;
            labelText.alignment = TextAlignmentOptions.Left;

            // Toggle (small checkbox) - explicit position and size
            GameObject toggleObj = new GameObject("Toggle");
            toggleObj.transform.SetParent(rowObj.transform, false);
            RectTransform toggleRect = toggleObj.AddComponent<RectTransform>();
            toggleRect.anchorMin = new Vector2(0, 0.5f);
            toggleRect.anchorMax = new Vector2(0, 0.5f);
            toggleRect.pivot = new Vector2(0, 0.5f);
            toggleRect.anchoredPosition = new Vector2(125, 0);
            toggleRect.sizeDelta = new Vector2(20, 20); // Small 20x20 checkbox

            // Background
            GameObject bgObj = new GameObject("Background");
            bgObj.transform.SetParent(toggleObj.transform, false);
            RectTransform bgRect = bgObj.AddComponent<RectTransform>();
            bgRect.anchorMin = Vector2.zero;
            bgRect.anchorMax = Vector2.one;
            bgRect.offsetMin = Vector2.zero;
            bgRect.offsetMax = Vector2.zero;
            Image bgImg = bgObj.AddComponent<Image>();
            bgImg.color = SLIDER_BG;

            // Checkmark (smaller inner square)
            GameObject checkObj = new GameObject("Checkmark");
            checkObj.transform.SetParent(bgObj.transform, false);
            RectTransform checkRect = checkObj.AddComponent<RectTransform>();
            checkRect.anchorMin = new Vector2(0.15f, 0.15f);
            checkRect.anchorMax = new Vector2(0.85f, 0.85f);
            checkRect.offsetMin = Vector2.zero;
            checkRect.offsetMax = Vector2.zero;
            Image checkImg = checkObj.AddComponent<Image>();
            checkImg.color = ACCENT_COLOR;

            Toggle toggle = toggleObj.AddComponent<Toggle>();
            toggle.targetGraphic = bgImg;
            toggle.graphic = checkImg;
            toggle.isOn = true;

            if (onValueChanged != null)
            {
                toggle.onValueChanged.AddListener(onValueChanged);
            }

            return toggle;
        }

        private TMP_Dropdown CreateDropdownRow(Transform parent, string label, UnityEngine.Events.UnityAction<int> onValueChanged)
        {
            GameObject rowObj = new GameObject($"Row_{label.Replace(" ", "")}");
            rowObj.transform.SetParent(parent, false);
            RectTransform rowRect = rowObj.AddComponent<RectTransform>();
            rowRect.sizeDelta = new Vector2(420, 30);

            LayoutElement rowLE = rowObj.AddComponent<LayoutElement>();
            rowLE.preferredHeight = 30;
            rowLE.minHeight = 30;

            // Label (left side)
            GameObject labelObj = new GameObject("Label");
            labelObj.transform.SetParent(rowObj.transform, false);
            RectTransform labelRect = labelObj.AddComponent<RectTransform>();
            labelRect.anchorMin = new Vector2(0, 0);
            labelRect.anchorMax = new Vector2(0, 1);
            labelRect.pivot = new Vector2(0, 0.5f);
            labelRect.anchoredPosition = new Vector2(0, 0);
            labelRect.sizeDelta = new Vector2(120, 0);
            TextMeshProUGUI labelText = labelObj.AddComponent<TextMeshProUGUI>();
            labelText.text = label;
            labelText.fontSize = 14;
            labelText.color = Color.white;
            labelText.alignment = TextAlignmentOptions.Left;

            // Dropdown - explicit position and size
            GameObject ddObj = new GameObject("Dropdown");
            ddObj.transform.SetParent(rowObj.transform, false);
            RectTransform ddRect = ddObj.AddComponent<RectTransform>();
            ddRect.anchorMin = new Vector2(0, 0.5f);
            ddRect.anchorMax = new Vector2(0, 0.5f);
            ddRect.pivot = new Vector2(0, 0.5f);
            ddRect.anchoredPosition = new Vector2(125, 0);
            ddRect.sizeDelta = new Vector2(180, 26); // Compact dropdown

            Image ddBg = ddObj.AddComponent<Image>();
            ddBg.color = BUTTON_COLOR;

            // Caption Text
            GameObject captionObj = new GameObject("Label");
            captionObj.transform.SetParent(ddObj.transform, false);
            RectTransform captionRect = captionObj.AddComponent<RectTransform>();
            captionRect.anchorMin = Vector2.zero;
            captionRect.anchorMax = Vector2.one;
            captionRect.offsetMin = new Vector2(15, 5);
            captionRect.offsetMax = new Vector2(-35, -5);
            TextMeshProUGUI captionText = captionObj.AddComponent<TextMeshProUGUI>();
            captionText.fontSize = 16;
            captionText.color = Color.white;
            captionText.alignment = TextAlignmentOptions.Left;

            // Arrow
            GameObject arrowObj = new GameObject("Arrow");
            arrowObj.transform.SetParent(ddObj.transform, false);
            RectTransform arrowRect = arrowObj.AddComponent<RectTransform>();
            arrowRect.anchorMin = new Vector2(1, 0.5f);
            arrowRect.anchorMax = new Vector2(1, 0.5f);
            arrowRect.pivot = new Vector2(1, 0.5f);
            arrowRect.anchoredPosition = new Vector2(-10, 0);
            arrowRect.sizeDelta = new Vector2(20, 20);
            TextMeshProUGUI arrowText = arrowObj.AddComponent<TextMeshProUGUI>();
            arrowText.text = "▼";
            arrowText.fontSize = 14;
            arrowText.color = Color.white;
            arrowText.alignment = TextAlignmentOptions.Center;

            // Template
            GameObject templateObj = new GameObject("Template");
            templateObj.transform.SetParent(ddObj.transform, false);
            RectTransform templateRect = templateObj.AddComponent<RectTransform>();
            templateRect.anchorMin = new Vector2(0, 0);
            templateRect.anchorMax = new Vector2(1, 0);
            templateRect.pivot = new Vector2(0.5f, 1);
            templateRect.anchoredPosition = Vector2.zero;
            templateRect.sizeDelta = new Vector2(0, 150);
            Image templateBg = templateObj.AddComponent<Image>();
            templateBg.color = PANEL_BG;

            // Viewport
            GameObject viewportObj = new GameObject("Viewport");
            viewportObj.transform.SetParent(templateObj.transform, false);
            RectTransform viewportRect = viewportObj.AddComponent<RectTransform>();
            viewportRect.anchorMin = Vector2.zero;
            viewportRect.anchorMax = Vector2.one;
            viewportRect.offsetMin = Vector2.zero;
            viewportRect.offsetMax = Vector2.zero;
            Image viewportImg = viewportObj.AddComponent<Image>();
            viewportImg.color = Color.white;
            Mask viewportMask = viewportObj.AddComponent<Mask>();
            viewportMask.showMaskGraphic = false;

            // Content
            GameObject contentObj = new GameObject("Content");
            contentObj.transform.SetParent(viewportObj.transform, false);
            RectTransform contentRect = contentObj.AddComponent<RectTransform>();
            contentRect.anchorMin = new Vector2(0, 1);
            contentRect.anchorMax = new Vector2(1, 1);
            contentRect.pivot = new Vector2(0.5f, 1);
            contentRect.sizeDelta = new Vector2(0, 30);

            // Item Template
            GameObject itemObj = new GameObject("Item");
            itemObj.transform.SetParent(contentObj.transform, false);
            RectTransform itemRect = itemObj.AddComponent<RectTransform>();
            itemRect.anchorMin = new Vector2(0, 0.5f);
            itemRect.anchorMax = new Vector2(1, 0.5f);
            itemRect.sizeDelta = new Vector2(0, 30);

            Toggle itemToggle = itemObj.AddComponent<Toggle>();

            // Item Background
            GameObject itemBgObj = new GameObject("Item Background");
            itemBgObj.transform.SetParent(itemObj.transform, false);
            RectTransform itemBgRect = itemBgObj.AddComponent<RectTransform>();
            itemBgRect.anchorMin = Vector2.zero;
            itemBgRect.anchorMax = Vector2.one;
            itemBgRect.offsetMin = Vector2.zero;
            itemBgRect.offsetMax = Vector2.zero;
            Image itemBgImg = itemBgObj.AddComponent<Image>();
            itemBgImg.color = BUTTON_HOVER;

            // Item Checkmark
            GameObject itemCheckObj = new GameObject("Item Checkmark");
            itemCheckObj.transform.SetParent(itemObj.transform, false);
            RectTransform itemCheckRect = itemCheckObj.AddComponent<RectTransform>();
            itemCheckRect.anchorMin = new Vector2(0, 0.5f);
            itemCheckRect.anchorMax = new Vector2(0, 0.5f);
            itemCheckRect.sizeDelta = new Vector2(20, 20);
            itemCheckRect.anchoredPosition = new Vector2(15, 0);
            Image itemCheckImg = itemCheckObj.AddComponent<Image>();
            itemCheckImg.color = ACCENT_COLOR;

            // Item Label
            GameObject itemLabelObj = new GameObject("Item Label");
            itemLabelObj.transform.SetParent(itemObj.transform, false);
            RectTransform itemLabelRect = itemLabelObj.AddComponent<RectTransform>();
            itemLabelRect.anchorMin = Vector2.zero;
            itemLabelRect.anchorMax = Vector2.one;
            itemLabelRect.offsetMin = new Vector2(40, 0);
            itemLabelRect.offsetMax = new Vector2(-10, 0);
            TextMeshProUGUI itemLabelText = itemLabelObj.AddComponent<TextMeshProUGUI>();
            itemLabelText.fontSize = 16;
            itemLabelText.color = Color.white;
            itemLabelText.alignment = TextAlignmentOptions.Left;

            itemToggle.targetGraphic = itemBgImg;
            itemToggle.graphic = itemCheckImg;

            // ScrollRect with faster scroll speed
            ScrollRect scrollRect = templateObj.AddComponent<ScrollRect>();
            scrollRect.content = contentRect;
            scrollRect.viewport = viewportRect;
            scrollRect.horizontal = false;
            scrollRect.vertical = true;
            scrollRect.scrollSensitivity = 30f; // Much faster scrolling (default is ~1)

            // Create Dropdown
            TMP_Dropdown dropdown = ddObj.AddComponent<TMP_Dropdown>();
            dropdown.targetGraphic = ddBg;
            dropdown.template = templateRect;
            dropdown.captionText = captionText;
            dropdown.itemText = itemLabelText;

            templateObj.SetActive(false);

            if (onValueChanged != null)
            {
                dropdown.onValueChanged.AddListener(onValueChanged);
            }

            return dropdown;
        }

        private Button CreateButton(Transform parent, string text, UnityEngine.Events.UnityAction onClick)
        {
            GameObject btnObj = new GameObject($"{text}Button");
            btnObj.transform.SetParent(parent, false);

            Image btnBg = btnObj.AddComponent<Image>();
            btnBg.color = BUTTON_COLOR;
            btnBg.raycastTarget = true;

            Button btn = btnObj.AddComponent<Button>();
            btn.targetGraphic = btnBg;

            ColorBlock colors = btn.colors;
            colors.normalColor = BUTTON_COLOR;
            colors.highlightedColor = BUTTON_HOVER;
            colors.pressedColor = new Color(0.15f, 0.15f, 0.18f, 1f);
            btn.colors = colors;

            LayoutElement btnLE = btnObj.AddComponent<LayoutElement>();
            btnLE.preferredHeight = 36;
            btnLE.minHeight = 36;

            // Text
            GameObject textObj = new GameObject("Text");
            textObj.transform.SetParent(btnObj.transform, false);
            RectTransform textRect = textObj.AddComponent<RectTransform>();
            textRect.anchorMin = Vector2.zero;
            textRect.anchorMax = Vector2.one;
            textRect.offsetMin = Vector2.zero;
            textRect.offsetMax = Vector2.zero;
            TextMeshProUGUI btnText = textObj.AddComponent<TextMeshProUGUI>();
            btnText.text = text;
            btnText.fontSize = 18;
            btnText.fontStyle = FontStyles.Bold;
            btnText.alignment = TextAlignmentOptions.Center;
            btnText.color = Color.white;

            if (onClick != null)
            {
                btn.onClick.AddListener(onClick);
            }

            return btn;
        }

        private void EnsureEventSystemExists()
        {
            if (FindObjectOfType<EventSystem>() == null)
            {
                Debug.Log("[SettingsUI] Creating EventSystem");
                GameObject esObj = new GameObject("EventSystem");
                esObj.AddComponent<EventSystem>();
                esObj.AddComponent<StandaloneInputModule>();
            }
        }

        // Settings change handlers
        private void OnMasterVolumeChanged(float value)
        {
            AudioListener.volume = value;
        }

        private void OnMusicVolumeChanged(float value)
        {
            // TODO: Implement with AudioMixer
        }

        private void OnSfxVolumeChanged(float value)
        {
            // TODO: Implement with AudioMixer
        }

        private void OnFullscreenChanged(bool isFullscreen)
        {
            Screen.fullScreen = isFullscreen;
        }

        private void OnResolutionChanged(int index)
        {
            if (availableResolutions == null || index < 0 || index >= availableResolutions.Length)
                return;

            Resolution res = availableResolutions[index];
            Debug.Log($"[SettingsUI] Resolution changed to {res.width}x{res.height}");
            Screen.SetResolution(res.width, res.height, Screen.fullScreen);

            // Save immediately
            PlayerPrefs.SetInt(PREF_RESOLUTION, index);
            PlayerPrefs.Save();
        }

        private void OnQualityChanged(int index)
        {
            if (index < 0 || index >= QualitySettings.names.Length) return;

            Debug.Log($"[SettingsUI] Quality changed to index {index}: {QualitySettings.names[index]}");
            QualitySettings.SetQualityLevel(index, true); // true = apply expensive changes

            // Save immediately
            PlayerPrefs.SetInt(PREF_QUALITY, index);
            PlayerPrefs.Save();
        }

        private void OnFpsChanged(int index)
        {
            if (index < 0 || index >= FPS_OPTIONS.Length) return;

            int targetFps = FPS_OPTIONS[index];
            Application.targetFrameRate = targetFps;

            string fpsStr = targetFps == -1 ? "Unlimited" : targetFps.ToString();
            Debug.Log($"[SettingsUI] FPS limit changed to {fpsStr}");

            // Save immediately
            PlayerPrefs.SetInt(PREF_FPS_LIMIT, index);
            PlayerPrefs.Save();
        }

        private void OnBackClicked()
        {
            Close();
        }

        /// <summary>
        /// Create a SettingsUI instance.
        /// </summary>
        public static SettingsUI Create(Transform parent = null)
        {
            GameObject obj = new GameObject("SettingsUI");
            if (parent != null)
            {
                obj.transform.SetParent(parent, false);
            }
            return obj.AddComponent<SettingsUI>();
        }
    }
}
