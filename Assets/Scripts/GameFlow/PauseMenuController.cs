using UnityEngine;
using UnityEngine.UI;
using UnityEngine.SceneManagement;
using TMPro;
using BeneathTheFloor.Save;
using BeneathTheFloor.Player;
using BeneathTheFloor.Managers;
using BeneathTheFloor.UI;

namespace BeneathTheFloor.GameFlow
{
    /// <summary>
    /// Controls the in-game pause menu.
    /// Toggle with ESC key.
    /// </summary>
    public class PauseMenuController : MonoBehaviour
    {
        [Header("Panels")]
        [SerializeField] private GameObject pauseMenuRoot;
        [SerializeField] private GameObject mainPanel;
        [SerializeField] private GameObject settingsPanel;

        [Header("Save/Load Menu")]
        [SerializeField] private SaveLoadMenuUI saveLoadMenu;

        [Header("Buttons")]
        [SerializeField] private Button resumeButton;
        [SerializeField] private Button saveButton;
        [SerializeField] private Button loadButton;
        [SerializeField] private Button settingsButton;
        [SerializeField] private Button mainMenuButton;
        [SerializeField] private Button quitButton;
        [SerializeField] private Button settingsBackButton;

        [Header("Status")]
        [SerializeField] private TextMeshProUGUI statusText;
        [SerializeField] private float statusDisplayDuration = 2f;

        [Header("Settings Controls")]
        [SerializeField] private Slider masterVolumeSlider;
        [SerializeField] private TMP_Dropdown qualityDropdown;
        [SerializeField] private Toggle fullscreenToggle;

        [Header("Scene Names")]
        [SerializeField] private string mainMenuSceneName = "MainMenuScene";

        // Static access for other scripts to check pause state
        public static bool IsPaused { get; private set; }

        private bool wasCursorLocked;
        private bool wasCursorVisible;
        private float statusClearTime;

        // Cached player controller reference
        private FirstPersonController playerController;

        // PlayerPrefs keys
        private const string PREF_MASTER_VOLUME = "MasterVolume";
        private const string PREF_QUALITY_LEVEL = "QualityLevel";
        private const string PREF_FULLSCREEN = "Fullscreen";

        private void Awake()
        {
            if (pauseMenuRoot != null)
            {
                pauseMenuRoot.SetActive(false);
            }
        }

        private void Start()
        {
            // Find player controller
            playerController = FindObjectOfType<FirstPersonController>();

            SetupButtons();
            SetupSettings();
            LoadSettings();
        }

        private void Update()
        {
            // Toggle pause menu with ESC
            if (Input.GetKeyDown(KeyCode.Escape))
            {
                if (IsPaused)
                {
                    // If settings panel is open, go back to main panel first
                    if (settingsPanel != null && settingsPanel.activeSelf)
                    {
                        ShowMainPanel();
                    }
                    else
                    {
                        ResumeGame();
                    }
                }
                else
                {
                    // Don't open pause menu if another UI is open or just closed via ESC
                    // Those UIs handle their own ESC closing and consume the escape input
                    if (!BeneathTheFloor.UI.UIState.IsAnyUIOpen &&
                        !BeneathTheFloor.UI.UIState.WasEscapeConsumedThisFrame)
                    {
                        PauseGame();
                    }
                }
            }

            // Clear status text after duration
            if (statusClearTime > 0 && Time.unscaledTime >= statusClearTime)
            {
                ClearStatus();
                statusClearTime = 0;
            }
        }

        private void SetupButtons()
        {
            if (resumeButton != null)
                resumeButton.onClick.AddListener(ResumeGame);

            if (saveButton != null)
                saveButton.onClick.AddListener(OnSaveClicked);

            if (loadButton != null)
                loadButton.onClick.AddListener(OnLoadClicked);

            if (settingsButton != null)
                settingsButton.onClick.AddListener(ShowSettingsPanel);

            if (mainMenuButton != null)
                mainMenuButton.onClick.AddListener(OnMainMenuClicked);

            if (quitButton != null)
                quitButton.onClick.AddListener(OnQuitClicked);

            if (settingsBackButton != null)
                settingsBackButton.onClick.AddListener(ShowMainPanel);
        }

        private void SetupSettings()
        {
            if (masterVolumeSlider != null)
            {
                masterVolumeSlider.onValueChanged.AddListener(OnMasterVolumeChanged);
            }

            if (qualityDropdown != null)
            {
                qualityDropdown.ClearOptions();
                qualityDropdown.AddOptions(new System.Collections.Generic.List<string>(QualitySettings.names));
                qualityDropdown.onValueChanged.AddListener(OnQualityChanged);
            }

            if (fullscreenToggle != null)
            {
                fullscreenToggle.onValueChanged.AddListener(OnFullscreenChanged);
            }
        }

        private void LoadSettings()
        {
            // Master Volume
            float savedVolume = PlayerPrefs.GetFloat(PREF_MASTER_VOLUME, 1f);
            AudioListener.volume = savedVolume;
            if (masterVolumeSlider != null)
                masterVolumeSlider.value = savedVolume;

            // Quality
            int savedQuality = PlayerPrefs.GetInt(PREF_QUALITY_LEVEL, QualitySettings.GetQualityLevel());
            QualitySettings.SetQualityLevel(savedQuality);
            if (qualityDropdown != null)
                qualityDropdown.value = savedQuality;

            // Fullscreen
            bool savedFullscreen = PlayerPrefs.GetInt(PREF_FULLSCREEN, Screen.fullScreen ? 1 : 0) == 1;
            Screen.fullScreen = savedFullscreen;
            if (fullscreenToggle != null)
                fullscreenToggle.isOn = savedFullscreen;
        }

        public void PauseGame()
        {
            if (IsPaused) return;

            // Store cursor state before pausing
            wasCursorLocked = Cursor.lockState == CursorLockMode.Locked;
            wasCursorVisible = Cursor.visible;

            // Pause
            IsPaused = true;
            Time.timeScale = 0f;

            // Set UIState so other systems know pause menu is open
            BeneathTheFloor.UI.UIState.IsPauseMenuOpen = true;

            // Notify GameManager (so other systems like DiggingSystem know we're paused)
            if (GameManager.Instance != null)
                GameManager.Instance.SetPause(true);

            // Disable player input
            if (playerController != null)
                playerController.enabled = false;

            // Show cursor
            Cursor.visible = true;
            Cursor.lockState = CursorLockMode.None;

            // Show menu
            if (pauseMenuRoot != null)
                pauseMenuRoot.SetActive(true);

            ShowMainPanel();
            RefreshUI();
        }

        public void ResumeGame()
        {
            if (!IsPaused) return;

            IsPaused = false;
            Time.timeScale = 1f;

            // Clear UIState
            BeneathTheFloor.UI.UIState.IsPauseMenuOpen = false;

            // Notify GameManager
            if (GameManager.Instance != null)
                GameManager.Instance.SetPause(false);

            // Re-enable player input
            if (playerController != null)
                playerController.enabled = true;

            // UIState.IsPauseMenuOpen = false above triggers cursor lock via UpdateCursorState
            // Only manually restore cursor if another UI was open before pausing
            if (!BeneathTheFloor.UI.UIState.IsAnyUIOpen)
            {
                // Force lock cursor for normal gameplay
                Cursor.lockState = CursorLockMode.Locked;
                Cursor.visible = false;
            }

            // Hide menu
            if (pauseMenuRoot != null)
                pauseMenuRoot.SetActive(false);

            ClearStatus();
        }

        private void RefreshUI()
        {
            // Enable/disable Load button based on save file existence in any slot
            if (loadButton != null)
            {
                bool hasSave = SaveManager.Instance != null && SaveManager.Instance.SavesIndex.HasAnySaves();
                loadButton.interactable = hasSave;
            }
        }

        private void ShowMainPanel()
        {
            if (mainPanel != null)
                mainPanel.SetActive(true);

            if (settingsPanel != null)
                settingsPanel.SetActive(false);

            RefreshUI();
        }

        private SettingsUI settingsUI;

        private void ShowSettingsPanel()
        {
            // Use new SettingsUI instead of old settings panel
            EnsureSettingsUIExists();
            if (settingsUI != null)
            {
                if (mainPanel != null)
                    mainPanel.SetActive(false);

                settingsUI.OnMenuClosed += OnSettingsUIClosed;
                settingsUI.Open();
            }
            else
            {
                // Fallback to old panel
                if (mainPanel != null)
                    mainPanel.SetActive(false);

                if (settingsPanel != null)
                    settingsPanel.SetActive(true);
            }
        }

        private void EnsureSettingsUIExists()
        {
            if (settingsUI == null)
            {
                settingsUI = SettingsUI.Instance;
            }
            if (settingsUI == null)
            {
                settingsUI = SettingsUI.Create(pauseMenuRoot?.transform.parent);
            }
        }

        private void OnSettingsUIClosed()
        {
            if (settingsUI != null)
            {
                settingsUI.OnMenuClosed -= OnSettingsUIClosed;
            }

            // Show main pause panel again
            if (mainPanel != null)
                mainPanel.SetActive(true);
        }

        private void OnSaveClicked()
        {
            if (SaveManager.Instance == null)
            {
                ShowStatus("Save system unavailable", Color.red);
                return;
            }

            // Create save/load menu if it doesn't exist
            EnsureSaveLoadMenuExists();

            // Hide pause menu main panel
            if (mainPanel != null)
                mainPanel.SetActive(false);

            // Open save menu
            if (saveLoadMenu != null)
            {
                saveLoadMenu.OnMenuClosed += OnSaveLoadMenuClosed;
                saveLoadMenu.OpenSaveMenu();
            }
        }

        private void OnLoadClicked()
        {
            if (SaveManager.Instance == null)
            {
                ShowStatus("Save system unavailable", Color.red);
                return;
            }

            // Create save/load menu if it doesn't exist
            EnsureSaveLoadMenuExists();

            // Hide pause menu main panel
            if (mainPanel != null)
                mainPanel.SetActive(false);

            // Open load menu
            if (saveLoadMenu != null)
            {
                saveLoadMenu.OnMenuClosed += OnSaveLoadMenuClosed;
                saveLoadMenu.OpenLoadMenu();
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

            // Show pause menu main panel again
            if (mainPanel != null)
                mainPanel.SetActive(true);

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
                saveLoadMenu = SaveLoadMenuUI.Create(pauseMenuRoot?.transform.parent);
            }
        }

        private void OnMainMenuClicked()
        {
            // Reset time scale before changing scene
            Time.timeScale = 1f;

            // Save game before returning to main menu
            if (SaveManager.Instance != null)
            {
                Debug.Log("[PauseMenu] Saving before returning to main menu...");
                SaveManager.Instance.SaveGame();
            }

            // Ensure cursor is visible for main menu
            Cursor.visible = true;
            Cursor.lockState = CursorLockMode.None;

            SceneManager.LoadScene(mainMenuSceneName);
        }

        private void OnQuitClicked()
        {
            // Reset time scale
            Time.timeScale = 1f;

            // CRITICAL: Explicitly save before quitting to ensure save persists
            // OnApplicationQuit can be unreliable on Windows builds
            if (SaveManager.Instance != null)
            {
                Debug.Log("[PauseMenu] Saving before quit...");
                SaveManager.Instance.SaveGame();
                Debug.Log("[PauseMenu] Save completed, now quitting.");
            }

#if UNITY_EDITOR
            UnityEditor.EditorApplication.isPlaying = false;
#else
            Application.Quit();
#endif
        }

        private void OnMasterVolumeChanged(float value)
        {
            AudioListener.volume = value;
            PlayerPrefs.SetFloat(PREF_MASTER_VOLUME, value);
            PlayerPrefs.Save();
        }

        private void OnQualityChanged(int index)
        {
            QualitySettings.SetQualityLevel(index);
            PlayerPrefs.SetInt(PREF_QUALITY_LEVEL, index);
            PlayerPrefs.Save();
        }

        private void OnFullscreenChanged(bool isFullscreen)
        {
            Screen.fullScreen = isFullscreen;
            PlayerPrefs.SetInt(PREF_FULLSCREEN, isFullscreen ? 1 : 0);
            PlayerPrefs.Save();
        }

        private void ShowStatus(string message, Color color)
        {
            if (statusText != null)
            {
                statusText.text = message;
                statusText.color = color;
                statusClearTime = Time.unscaledTime + statusDisplayDuration;
            }
        }

        private void ClearStatus()
        {
            if (statusText != null)
            {
                statusText.text = "";
            }
        }

        private void OnDestroy()
        {
            // Ensure time scale is reset if this object is destroyed
            if (IsPaused)
            {
                Time.timeScale = 1f;
                IsPaused = false;
            }
        }
    }
}
