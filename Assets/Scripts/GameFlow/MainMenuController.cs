using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.SceneManagement;
using UnityEngine.Audio;
using TMPro;
using BeneathTheFloor.Save;
using BeneathTheFloor.UI;

namespace BeneathTheFloor.GameFlow
{
    /// <summary>
    /// Main Menu controller handling all menu navigation, save/load operations,
    /// and settings management.
    /// </summary>
    public class MainMenuController : MonoBehaviour
    {
        [Header("Scene Settings")]
        [SerializeField] private string gameplaySceneName = "HouseBuilding";
        [SerializeField] private float menuFadeInDuration = 0.8f;

        [Header("Main Menu Panel")]
        [SerializeField] private CanvasGroup mainMenuCanvasGroup;
        [SerializeField] private Button continueButton;
        [SerializeField] private Button newGameButton;
        [SerializeField] private Button loadGameButton;
        [SerializeField] private Button settingsButton;
        [SerializeField] private Button quitButton;
        [SerializeField] private TextMeshProUGUI continueHintText;

        [Header("Load Game Panel (Legacy - now uses SaveLoadMenuUI)")]
        [SerializeField] private GameObject loadPanel;
        [SerializeField] private TextMeshProUGUI loadPanelStatusText;
        [SerializeField] private TextMeshProUGUI loadPanelSaveInfoText;
        [SerializeField] private Button loadPanelLoadButton;
        [SerializeField] private Button loadPanelDeleteButton;
        [SerializeField] private Button loadPanelBackButton;

        [Header("Save/Load Menu")]
        [SerializeField] private SaveLoadMenuUI saveLoadMenu;

        [Header("Settings Panel")]
        [SerializeField] private GameObject settingsPanel;
        [SerializeField] private Slider masterVolumeSlider;
        [SerializeField] private Slider musicVolumeSlider;
        [SerializeField] private Slider sfxVolumeSlider;
        [SerializeField] private Toggle fullscreenToggle;
        [SerializeField] private TMP_Dropdown resolutionDropdown;
        [SerializeField] private TMP_Dropdown qualityDropdown;
        [SerializeField] private Button settingsBackButton;

        [Header("Confirmation Dialog")]
        [SerializeField] private GameObject confirmDialog;
        [SerializeField] private TextMeshProUGUI confirmDialogText;
        [SerializeField] private Button confirmDialogCancelButton;
        [SerializeField] private Button confirmDialogConfirmButton;
        [SerializeField] private TextMeshProUGUI confirmDialogConfirmButtonText;

        [Header("Delete Confirmation Dialog")]
        [SerializeField] private GameObject deleteConfirmDialog;
        [SerializeField] private TextMeshProUGUI deleteConfirmText;
        [SerializeField] private Button deleteConfirmCancelButton;
        [SerializeField] private Button deleteConfirmDeleteButton;

        [Header("Transition")]
        [SerializeField] private Image fadeOverlay;
        [SerializeField] private float sceneFadeDuration = 0.5f;

        [Header("Audio (Optional)")]
        [SerializeField] private AudioMixer audioMixer;

        // PlayerPrefs keys
        private const string PREF_MASTER_VOLUME = "MasterVolume";
        private const string PREF_MUSIC_VOLUME = "MusicVolume";
        private const string PREF_SFX_VOLUME = "SfxVolume";
        private const string PREF_FULLSCREEN = "Fullscreen";
        private const string PREF_RESOLUTION = "Resolution";
        private const string PREF_QUALITY = "Quality";

        private Resolution[] availableResolutions;
        private bool isTransitioning;

        private void Start()
        {
            // Apply saved FPS limit early
            SettingsUI.ApplySavedFpsLimit();

            // Ensure optimal graphics settings on every game start
            EnsureOptimalGraphicsSettings();

            InitializeUI();
            SetupButtonListeners();
            LoadSettings();
            RefreshUI();
            StartCoroutine(FadeInMenu());
        }

        /// <summary>
        /// Ensures the game starts with optimal graphics settings:
        /// - Highest quality level
        /// - Native fullscreen resolution
        /// </summary>
        private void EnsureOptimalGraphicsSettings()
        {
            // Set to highest quality level
            int highestQuality = QualitySettings.names.Length - 1;
            QualitySettings.SetQualityLevel(highestQuality, true);

            // Set to native fullscreen resolution
            Resolution nativeRes = Screen.currentResolution;
            Screen.SetResolution(nativeRes.width, nativeRes.height, FullScreenMode.FullScreenWindow);

            // Update PlayerPrefs to reflect these optimal settings
            PlayerPrefs.SetInt(PREF_QUALITY, highestQuality);
            PlayerPrefs.SetInt(PREF_FULLSCREEN, 1);
            // Don't save resolution index since it depends on the dropdown order
            PlayerPrefs.Save();

            Debug.Log($"[MainMenu] Set optimal graphics: {QualitySettings.names[highestQuality]} @ {nativeRes.width}x{nativeRes.height} fullscreen");
        }

        /// <summary>
        /// Initialize UI state - hide panels, setup dropdowns.
        /// </summary>
        private void InitializeUI()
        {
            // Hide all panels initially
            if (loadPanel != null) loadPanel.SetActive(false);
            if (settingsPanel != null) settingsPanel.SetActive(false);
            if (confirmDialog != null) confirmDialog.SetActive(false);
            if (deleteConfirmDialog != null) deleteConfirmDialog.SetActive(false);

            // Setup fade overlay
            if (fadeOverlay != null)
            {
                SetImageAlpha(fadeOverlay, 0f);
                fadeOverlay.raycastTarget = false;
            }

            // Setup main menu alpha for fade-in
            if (mainMenuCanvasGroup != null)
            {
                mainMenuCanvasGroup.alpha = 0f;
            }

            // Setup resolution dropdown
            SetupResolutionDropdown();

            // Setup quality dropdown
            SetupQualityDropdown();
        }

        /// <summary>
        /// Wire up all button click listeners.
        /// </summary>
        private void SetupButtonListeners()
        {
            // Main menu buttons
            if (continueButton != null)
                continueButton.onClick.AddListener(OnContinueClicked);
            if (newGameButton != null)
                newGameButton.onClick.AddListener(OnNewGameClicked);
            if (loadGameButton != null)
                loadGameButton.onClick.AddListener(OnLoadGameClicked);
            if (settingsButton != null)
                settingsButton.onClick.AddListener(OnSettingsClicked);
            if (quitButton != null)
                quitButton.onClick.AddListener(OnQuitClicked);

            // Load panel buttons
            if (loadPanelLoadButton != null)
                loadPanelLoadButton.onClick.AddListener(OnLoadPanelLoadClicked);
            if (loadPanelDeleteButton != null)
                loadPanelDeleteButton.onClick.AddListener(OnLoadPanelDeleteClicked);
            if (loadPanelBackButton != null)
                loadPanelBackButton.onClick.AddListener(OnLoadPanelBackClicked);

            // Settings panel
            if (settingsBackButton != null)
                settingsBackButton.onClick.AddListener(OnSettingsBackClicked);

            // Settings sliders
            if (masterVolumeSlider != null)
                masterVolumeSlider.onValueChanged.AddListener(OnMasterVolumeChanged);
            if (musicVolumeSlider != null)
                musicVolumeSlider.onValueChanged.AddListener(OnMusicVolumeChanged);
            if (sfxVolumeSlider != null)
                sfxVolumeSlider.onValueChanged.AddListener(OnSfxVolumeChanged);

            // Settings toggles/dropdowns
            if (fullscreenToggle != null)
                fullscreenToggle.onValueChanged.AddListener(OnFullscreenChanged);
            if (resolutionDropdown != null)
                resolutionDropdown.onValueChanged.AddListener(OnResolutionChanged);
            if (qualityDropdown != null)
                qualityDropdown.onValueChanged.AddListener(OnQualityChanged);

            // Confirmation dialog
            if (confirmDialogCancelButton != null)
                confirmDialogCancelButton.onClick.AddListener(OnConfirmDialogCancel);
            if (confirmDialogConfirmButton != null)
                confirmDialogConfirmButton.onClick.AddListener(OnConfirmDialogConfirm);

            // Delete confirmation dialog
            if (deleteConfirmCancelButton != null)
                deleteConfirmCancelButton.onClick.AddListener(OnDeleteConfirmCancel);
            if (deleteConfirmDeleteButton != null)
                deleteConfirmDeleteButton.onClick.AddListener(OnDeleteConfirmConfirm);
        }

        /// <summary>
        /// Refresh UI state based on save file availability.
        /// </summary>
        public void RefreshUI()
        {
            // Use instance check first, fall back to static file check
            bool hasSave = SaveManager.Instance != null
                ? SaveManager.Instance.SavesIndex.HasAnySaves()
                : SaveManager.SaveFileExists();

            Debug.Log($"[MainMenu] RefreshUI - hasSave={hasSave}, Instance={(SaveManager.Instance != null ? "exists" : "null")}");

            // Continue button - disable if no save exists
            if (continueButton != null)
            {
                continueButton.interactable = hasSave;
            }

            // Continue hint text - show "No save found" when disabled
            if (continueHintText != null)
            {
                continueHintText.gameObject.SetActive(!hasSave);
                continueHintText.text = "No save found";
            }

            // Load button - disable if no save exists
            if (loadGameButton != null)
            {
                loadGameButton.interactable = hasSave;
            }

            // Load panel state (legacy)
            RefreshLoadPanel();
        }

        /// <summary>
        /// Refresh the load panel UI (legacy panel).
        /// </summary>
        private void RefreshLoadPanel()
        {
            bool hasSave = SaveManager.Instance != null
                ? SaveManager.Instance.SavesIndex.HasAnySaves()
                : SaveManager.SaveFileExists();

            if (loadPanelStatusText != null)
            {
                loadPanelStatusText.text = hasSave ? "Save Files Found" : "No Save Files";
            }

            if (loadPanelSaveInfoText != null)
            {
                if (hasSave)
                {
                    // Show basic info
                    int saveCount = 0;
                    if (SaveManager.Instance != null)
                    {
                        var slots = SaveManager.Instance.GetAllSlots();
                        foreach (var slot in slots)
                        {
                            if (slot.hasData) saveCount++;
                        }
                    }
                    loadPanelSaveInfoText.text = $"{saveCount} save(s) found. Click Load to select one.";
                }
                else
                {
                    loadPanelSaveInfoText.text = "Start a New Game to begin.";
                }
            }

            if (loadPanelLoadButton != null)
                loadPanelLoadButton.interactable = hasSave;

            if (loadPanelDeleteButton != null)
                loadPanelDeleteButton.interactable = hasSave;
        }

        #region Menu Fade

        private IEnumerator FadeInMenu()
        {
            if (mainMenuCanvasGroup == null) yield break;

            float elapsed = 0f;
            while (elapsed < menuFadeInDuration)
            {
                elapsed += Time.deltaTime;
                float t = Mathf.Clamp01(elapsed / menuFadeInDuration);
                t = t * t * (3f - 2f * t); // Smoothstep
                mainMenuCanvasGroup.alpha = t;
                yield return null;
            }
            mainMenuCanvasGroup.alpha = 1f;
        }

        #endregion

        #region Main Menu Button Handlers

        private void OnContinueClicked()
        {
            if (isTransitioning) return;
            if (SaveManager.Instance == null || !SaveManager.Instance.HasSaveFile) return;

            // Load the game data - SaveManager.LoadGame() will handle the scene transition
            // DO NOT call TransitionToScene() here as LoadGame() already calls SceneManager.LoadScene()
            // Calling both would load the scene TWICE, breaking the deferred load system!
            if (SaveManager.Instance.LoadGame())
            {
                isTransitioning = true;
                // Start fade but don't load scene - it's already being loaded by SaveManager
                StartCoroutine(FadeOutOnly());
            }
            else
            {
                Debug.LogWarning("[MainMenu] Failed to load save file.");
            }
        }

        /// <summary>
        /// Fade out without loading scene (scene is already being loaded by SaveManager).
        /// </summary>
        private IEnumerator FadeOutOnly()
        {
            if (fadeOverlay != null)
            {
                fadeOverlay.raycastTarget = true;

                float elapsed = 0f;
                while (elapsed < sceneFadeDuration)
                {
                    elapsed += Time.deltaTime;
                    float t = Mathf.Clamp01(elapsed / sceneFadeDuration);
                    SetImageAlpha(fadeOverlay, t);
                    yield return null;
                }

                SetImageAlpha(fadeOverlay, 1f);
            }
            // Don't call SceneManager.LoadScene() - SaveManager already did that
        }

        private void OnNewGameClicked()
        {
            if (isTransitioning) return;

            bool hasSave = SaveManager.Instance != null
                ? SaveManager.Instance.HasSaveFile
                : SaveManager.SaveFileExists();

            if (hasSave)
            {
                // Check if there's an empty slot available
                int emptySlot = SaveManager.Instance != null
                    ? SaveManager.Instance.SavesIndex.FindFirstEmptyManualSlot()
                    : -1;

                if (emptySlot >= 0)
                {
                    // Empty slot available - create new save there
                    ShowConfirmDialog(
                        "Start a new game?\nYour existing saves will be preserved.",
                        "Start New",
                        StartNewGame
                    );
                }
                else
                {
                    // No empty slots - need to overwrite
                    ShowConfirmDialog(
                        "Start a new game?\nAll save slots are full.\nThis will overwrite the oldest save.",
                        "Start New",
                        StartNewGameOverwrite
                    );
                }
            }
            else
            {
                // No save exists, start directly
                StartNewGame();
            }
        }

        private void StartNewGame()
        {
            if (isTransitioning) return;

            // Find an empty slot or use slot 1 (first manual slot)
            int targetSlot = 1; // Default to first manual slot
            if (SaveManager.Instance != null)
            {
                int emptySlot = SaveManager.Instance.SavesIndex.FindFirstEmptyManualSlot();
                if (emptySlot >= 0)
                {
                    targetSlot = emptySlot;
                }

                // Clear this slot's save file FIRST (before setting as active)
                // This order is important! DeleteSaveSlot resets active slot if we delete the active one.
                SaveManager.Instance.DeleteSaveSlot(targetSlot);

                // Now set this as the active slot (autosaves will go here)
                SaveManager.Instance.SetActiveSlot(targetSlot);
            }

            // CRITICAL: Clear all shared game state for a fresh start
            // This ensures player starts with no tools, no progress, etc.
            ClearGameStateForNewGame();

            // Set flag to trigger intro cinematic in the game scene
            IntroCinematicController.SetNewGameFlag();

            StartCoroutine(TransitionToScene(gameplaySceneName));
        }

        /// <summary>
        /// Clears all shared game state to ensure a completely fresh start.
        /// This resets PlayerPrefs, terrain, resources, etc. without deleting other save slots.
        /// </summary>
        private void ClearGameStateForNewGame()
        {
            // Clear tool-related PlayerPrefs - this is critical!
            PlayerPrefs.DeleteKey("PlayerToolTier");
            PlayerPrefs.DeleteKey("PlayerToolIndex");
            PlayerPrefs.DeleteKey("PlayerToolEquipped");
            PlayerPrefs.DeleteKey("LoadPosX");
            PlayerPrefs.DeleteKey("LoadPosY");
            PlayerPrefs.DeleteKey("LoadPosZ");
            PlayerPrefs.DeleteKey("LoadRotY");
            PlayerPrefs.DeleteKey("LoadScene");

            // Clear robot upgrade PlayerPrefs
            PlayerPrefs.DeleteKey("robot_activated");
            PlayerPrefs.DeleteKey("robot_smart_stop");
            PlayerPrefs.DeleteKey("robot_efficiency");
            PlayerPrefs.DeleteKey("logistics_robot_activated");
            PlayerPrefs.DeleteKey("logistics_autonomous");
            PlayerPrefs.DeleteKey("logistics_capacity_level");
            PlayerPrefs.DeleteKey("logistics_advanced_module");

            // Clear upgrade station progress (legacy keys)
            PlayerPrefs.DeleteKey("UpgradeStation_tool_tier");
            PlayerPrefs.DeleteKey("UpgradeStation_energy_capacity");
            PlayerPrefs.DeleteKey("UpgradeStation_headlamp");
            PlayerPrefs.DeleteKey("UpgradeStation_inventory");
            PlayerPrefs.DeleteKey("UpgradeStation_radar");
            PlayerPrefs.DeleteKey("UpgradeStation_jetpack");

            // Clear RuntimeUpgrade keys (the actual upgrade progress UpgradeStation reads)
            PlayerPrefs.DeleteKey("RuntimeUpgrade_tool_power");
            PlayerPrefs.DeleteKey("RuntimeUpgrade_tool_tier");
            PlayerPrefs.DeleteKey("RuntimeUpgrade_energy_capacity");
            PlayerPrefs.DeleteKey("RuntimeUpgrade_sonic_pulser");
            PlayerPrefs.DeleteKey("RuntimeUpgrade_inventory_size");
            PlayerPrefs.DeleteKey("RuntimeUpgrade_headlamp");
            PlayerPrefs.DeleteKey("RuntimeUpgrade_move_speed");
            PlayerPrefs.DeleteKey("RuntimeUpgrade_lamp_purchase");
            PlayerPrefs.DeleteKey("RuntimeUpgrade_winch_cable");

            PlayerPrefs.Save();

            // Clear terrain data
            BeneathTheFloor.Digging.DiggingSaveManager.DeleteSaveFile();

            // Clear resource system data
            if (BeneathTheFloor.ResourceSystem.ResourceSaveManager.Instance != null)
            {
                BeneathTheFloor.ResourceSystem.ResourceSaveManager.Instance.DeleteSave();
            }

            // Clear world pickups
            BeneathTheFloor.Save.WorldPickupSaveManager.DeleteSave();

            // Clear hidden node data (ore veins, etc.)
            if (BeneathTheFloor.ResourceSystem.HiddenNodeManager.Instance != null)
            {
                BeneathTheFloor.ResourceSystem.HiddenNodeManager.Instance.ForceClearAllSaveData();
            }

            Debug.Log("[MainMenu] Cleared all game state for new game");
        }

        /// <summary>
        /// Start a new game when all slots are full - overwrites oldest save.
        /// </summary>
        private void StartNewGameOverwrite()
        {
            if (isTransitioning) return;

            if (SaveManager.Instance != null)
            {
                // Find the oldest save slot (by savedAt date string)
                int oldestSlot = 1;
                System.DateTime oldestTime = System.DateTime.MaxValue;
                var slots = SaveManager.Instance.GetAllSlots();
                foreach (var slot in slots)
                {
                    if (slot.slotId > 0 && slot.hasData) // Skip autosave slot (0)
                    {
                        // Parse the savedAt string to DateTime
                        if (System.DateTime.TryParse(slot.savedAt, out System.DateTime slotTime))
                        {
                            if (slotTime < oldestTime)
                            {
                                oldestTime = slotTime;
                                oldestSlot = slot.slotId;
                            }
                        }
                    }
                }

                // Clear the slot FIRST, then set as active
                // This order is important! DeleteSaveSlot resets active slot if we delete the active one.
                SaveManager.Instance.DeleteSaveSlot(oldestSlot);
                SaveManager.Instance.SetActiveSlot(oldestSlot);
            }

            // CRITICAL: Clear all shared game state for a fresh start
            ClearGameStateForNewGame();

            // Set flag to trigger intro cinematic in the game scene
            IntroCinematicController.SetNewGameFlag();

            StartCoroutine(TransitionToScene(gameplaySceneName));
        }

        private void OnLoadGameClicked()
        {
            if (isTransitioning) return;

            // Create save/load menu if it doesn't exist
            EnsureSaveLoadMenuExists();

            // Open load menu
            if (saveLoadMenu != null)
            {
                saveLoadMenu.OnMenuClosed += OnSaveLoadMenuClosed;
                saveLoadMenu.OpenLoadMenu();
            }
            else
            {
                // Fallback to legacy load panel if SaveLoadMenuUI failed to create
                ShowPanel(loadPanel);
                RefreshLoadPanel();
            }
        }

        /// <summary>
        /// Called when save/load menu is closed.
        /// </summary>
        private void OnSaveLoadMenuClosed()
        {
            if (saveLoadMenu != null)
            {
                saveLoadMenu.OnMenuClosed -= OnSaveLoadMenuClosed;
            }
            RefreshUI();
        }

        /// <summary>
        /// Create the save/load menu UI if it doesn't exist.
        /// </summary>
        private void EnsureSaveLoadMenuExists()
        {
            if (saveLoadMenu == null)
            {
                saveLoadMenu = SaveLoadMenuUI.Instance;
            }

            if (saveLoadMenu == null)
            {
                // Create it programmatically
                saveLoadMenu = SaveLoadMenuUI.Create(transform.parent);
            }
        }

        private void OnSettingsClicked()
        {
            if (isTransitioning) return;

            // Use new SettingsUI instead of old settings panel
            EnsureSettingsUIExists();
            if (settingsUI != null)
            {
                settingsUI.OnMenuClosed += OnSettingsUIClosed;
                settingsUI.Open();
            }
            else
            {
                // Fallback to old panel if new UI failed
                ShowPanel(settingsPanel);
            }
        }

        private SettingsUI settingsUI;

        private void EnsureSettingsUIExists()
        {
            if (settingsUI == null)
            {
                settingsUI = SettingsUI.Instance;
            }
            if (settingsUI == null)
            {
                settingsUI = SettingsUI.Create(transform.parent);
            }
        }

        private void OnSettingsUIClosed()
        {
            if (settingsUI != null)
            {
                settingsUI.OnMenuClosed -= OnSettingsUIClosed;
            }
        }

        private void OnQuitClicked()
        {
            if (isTransitioning) return;

#if UNITY_EDITOR
            UnityEditor.EditorApplication.isPlaying = false;
#else
            Application.Quit();
#endif
        }

        #endregion

        #region Load Panel Handlers

        private void OnLoadPanelLoadClicked()
        {
            if (isTransitioning) return;
            if (SaveManager.Instance == null || !SaveManager.Instance.HasSaveFile) return;

            // Same as OnContinueClicked - LoadGame() already handles scene transition
            if (SaveManager.Instance.LoadGame())
            {
                isTransitioning = true;
                StartCoroutine(FadeOutOnly());
            }
        }

        private void OnLoadPanelDeleteClicked()
        {
            if (isTransitioning) return;
            ShowDeleteConfirmDialog();
        }

        private void OnLoadPanelBackClicked()
        {
            HidePanel(loadPanel);
        }

        #endregion

        #region Settings Panel Handlers

        private void OnSettingsBackClicked()
        {
            SaveSettings();
            HidePanel(settingsPanel);
        }

        private void OnMasterVolumeChanged(float value)
        {
            // Use AudioListener.volume as fallback if no mixer
            if (audioMixer != null)
            {
                audioMixer.SetFloat("MasterVolume", LinearToDecibel(value));
            }
            else
            {
                AudioListener.volume = value;
            }
        }

        private void OnMusicVolumeChanged(float value)
        {
            // TODO: Connect to music AudioMixerGroup if available
            if (audioMixer != null)
            {
                audioMixer.SetFloat("MusicVolume", LinearToDecibel(value));
            }
        }

        private void OnSfxVolumeChanged(float value)
        {
            // TODO: Connect to SFX AudioMixerGroup if available
            if (audioMixer != null)
            {
                audioMixer.SetFloat("SFXVolume", LinearToDecibel(value));
            }
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
            Screen.SetResolution(res.width, res.height, Screen.fullScreen);
        }

        private void OnQualityChanged(int index)
        {
            QualitySettings.SetQualityLevel(index);
        }

        private float LinearToDecibel(float linear)
        {
            // Convert 0-1 linear to decibels (-80 to 0)
            if (linear <= 0.0001f) return -80f;
            return Mathf.Log10(linear) * 20f;
        }

        #endregion

        #region Confirmation Dialog

        private System.Action pendingConfirmAction;

        private void ShowConfirmDialog(string message, string confirmText, System.Action onConfirm)
        {
            if (confirmDialog == null) return;

            pendingConfirmAction = onConfirm;

            if (confirmDialogText != null)
                confirmDialogText.text = message;

            if (confirmDialogConfirmButtonText != null)
                confirmDialogConfirmButtonText.text = confirmText;

            confirmDialog.SetActive(true);
        }

        private void OnConfirmDialogCancel()
        {
            pendingConfirmAction = null;
            if (confirmDialog != null)
                confirmDialog.SetActive(false);
        }

        private void OnConfirmDialogConfirm()
        {
            var action = pendingConfirmAction;
            pendingConfirmAction = null;

            if (confirmDialog != null)
                confirmDialog.SetActive(false);

            action?.Invoke();
        }

        #endregion

        #region Delete Confirmation Dialog

        private void ShowDeleteConfirmDialog()
        {
            if (deleteConfirmDialog == null) return;

            if (deleteConfirmText != null)
                deleteConfirmText.text = "Delete your save file?\nThis cannot be undone.";

            deleteConfirmDialog.SetActive(true);
        }

        private void OnDeleteConfirmCancel()
        {
            if (deleteConfirmDialog != null)
                deleteConfirmDialog.SetActive(false);
        }

        private void OnDeleteConfirmConfirm()
        {
            if (deleteConfirmDialog != null)
                deleteConfirmDialog.SetActive(false);

            if (SaveManager.Instance != null)
            {
                SaveManager.Instance.DeleteAllSaves();
                RefreshUI();
            }
            else
            {
                // Fallback to static deletion if no instance
                SaveManager.DeleteAllSaveFiles();
                RefreshUI();
            }
        }

        #endregion

        #region Panel Management

        private void ShowPanel(GameObject panel)
        {
            if (panel != null)
                panel.SetActive(true);
        }

        private void HidePanel(GameObject panel)
        {
            if (panel != null)
                panel.SetActive(false);
        }

        #endregion

        #region Settings Persistence

        private void SetupResolutionDropdown()
        {
            if (resolutionDropdown == null) return;

            availableResolutions = Screen.resolutions;
            resolutionDropdown.ClearOptions();

            List<string> options = new List<string>();
            int currentIndex = 0;

            for (int i = 0; i < availableResolutions.Length; i++)
            {
                Resolution res = availableResolutions[i];
                string option = $"{res.width} x {res.height}";

                // Avoid duplicate entries
                if (!options.Contains(option))
                {
                    options.Add(option);
                }

                if (res.width == Screen.currentResolution.width &&
                    res.height == Screen.currentResolution.height)
                {
                    currentIndex = options.Count - 1;
                }
            }

            resolutionDropdown.AddOptions(options);
            resolutionDropdown.value = currentIndex;
            resolutionDropdown.RefreshShownValue();
        }

        private void SetupQualityDropdown()
        {
            if (qualityDropdown == null) return;

            qualityDropdown.ClearOptions();
            qualityDropdown.AddOptions(new List<string>(QualitySettings.names));
            qualityDropdown.value = QualitySettings.GetQualityLevel();
            qualityDropdown.RefreshShownValue();
        }

        /// <summary>
        /// Find the dropdown index for the current screen resolution.
        /// </summary>
        private int FindCurrentResolutionIndex()
        {
            if (availableResolutions == null || resolutionDropdown == null)
                return -1;

            int width = Screen.width;
            int height = Screen.height;

            for (int i = 0; i < resolutionDropdown.options.Count; i++)
            {
                string option = resolutionDropdown.options[i].text;
                if (option.Contains($"{width} x {height}"))
                {
                    return i;
                }
            }

            // Fallback to highest (last) resolution
            return resolutionDropdown.options.Count - 1;
        }

        private void LoadSettings()
        {
            // Master volume
            float masterVol = PlayerPrefs.GetFloat(PREF_MASTER_VOLUME, 1f);
            if (masterVolumeSlider != null)
            {
                masterVolumeSlider.value = masterVol;
                OnMasterVolumeChanged(masterVol);
            }

            // Music volume
            float musicVol = PlayerPrefs.GetFloat(PREF_MUSIC_VOLUME, 1f);
            if (musicVolumeSlider != null)
            {
                musicVolumeSlider.value = musicVol;
                OnMusicVolumeChanged(musicVol);
            }

            // SFX volume
            float sfxVol = PlayerPrefs.GetFloat(PREF_SFX_VOLUME, 1f);
            if (sfxVolumeSlider != null)
            {
                sfxVolumeSlider.value = sfxVol;
                OnSfxVolumeChanged(sfxVol);
            }

            // Fullscreen - always use current state (set by EnsureOptimalGraphicsSettings)
            if (fullscreenToggle != null)
            {
                fullscreenToggle.isOn = Screen.fullScreen;
            }

            // Resolution - show current resolution in dropdown (don't change it)
            if (resolutionDropdown != null && availableResolutions != null)
            {
                int currentResIndex = FindCurrentResolutionIndex();
                if (currentResIndex >= 0)
                {
                    resolutionDropdown.SetValueWithoutNotify(currentResIndex);
                }
            }

            // Quality - show current quality level (already set by EnsureOptimalGraphicsSettings)
            if (qualityDropdown != null)
            {
                qualityDropdown.SetValueWithoutNotify(QualitySettings.GetQualityLevel());
            }
        }

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

            PlayerPrefs.Save();
        }

        #endregion

        #region Scene Transition

        private IEnumerator TransitionToScene(string sceneName)
        {
            isTransitioning = true;

            if (fadeOverlay != null)
            {
                fadeOverlay.raycastTarget = true;

                float elapsed = 0f;
                while (elapsed < sceneFadeDuration)
                {
                    elapsed += Time.deltaTime;
                    float t = Mathf.Clamp01(elapsed / sceneFadeDuration);
                    SetImageAlpha(fadeOverlay, t);
                    yield return null;
                }

                SetImageAlpha(fadeOverlay, 1f);
            }

            yield return new WaitForSeconds(0.1f);

            SceneManager.LoadScene(sceneName);
        }

        private void SetImageAlpha(Image image, float alpha)
        {
            if (image == null) return;
            Color c = image.color;
            c.a = alpha;
            image.color = c;
        }

        #endregion
    }
}
