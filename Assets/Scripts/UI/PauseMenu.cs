using UnityEngine;
using UnityEngine.UI;
using TMPro;

namespace BeneathTheFloor.UI
{
    public class PauseMenu : MonoBehaviour
    {
        [Header("Panels")]
        [SerializeField] private GameObject pausePanel;
        [SerializeField] private GameObject settingsPanel;
        [SerializeField] private GameObject confirmQuitPanel;

        [Header("Settings Sliders")]
        [SerializeField] private Slider musicVolumeSlider;
        [SerializeField] private Slider sfxVolumeSlider;
        [SerializeField] private Slider sensitivitySlider;

        [Header("Settings Labels")]
        [SerializeField] private TextMeshProUGUI musicVolumeLabel;
        [SerializeField] private TextMeshProUGUI sfxVolumeLabel;
        [SerializeField] private TextMeshProUGUI sensitivityLabel;

        private void Start()
        {
            // Subscribe to pause event
            GameEvents.OnPauseToggled += OnPauseToggled;

            // Initialize settings
            LoadSettings();

            // Set up slider listeners
            if (musicVolumeSlider != null)
            {
                musicVolumeSlider.onValueChanged.AddListener(OnMusicVolumeChanged);
            }
            if (sfxVolumeSlider != null)
            {
                sfxVolumeSlider.onValueChanged.AddListener(OnSFXVolumeChanged);
            }
            if (sensitivitySlider != null)
            {
                sensitivitySlider.onValueChanged.AddListener(OnSensitivityChanged);
            }

            // Hide all panels initially
            HideAllPanels();
        }

        private void OnDestroy()
        {
            GameEvents.OnPauseToggled -= OnPauseToggled;
        }

        private void OnPauseToggled()
        {
            bool isPaused = Managers.GameManager.Instance?.IsPaused ?? false;

            if (isPaused)
            {
                ShowPauseMenu();
            }
            else
            {
                HideAllPanels();
            }
        }

        private void HideAllPanels()
        {
            if (pausePanel != null) pausePanel.SetActive(false);
            if (settingsPanel != null) settingsPanel.SetActive(false);
            if (confirmQuitPanel != null) confirmQuitPanel.SetActive(false);
        }

        private void ShowPauseMenu()
        {
            HideAllPanels();
            if (pausePanel != null) pausePanel.SetActive(true);
        }

        private void LoadSettings()
        {
            float musicVolume = PlayerPrefs.GetFloat("MusicVolume", 0.5f);
            float sfxVolume = PlayerPrefs.GetFloat("SFXVolume", 1f);
            float sensitivity = PlayerPrefs.GetFloat("Sensitivity", 2f);

            if (musicVolumeSlider != null) musicVolumeSlider.value = musicVolume;
            if (sfxVolumeSlider != null) sfxVolumeSlider.value = sfxVolume;
            if (sensitivitySlider != null) sensitivitySlider.value = sensitivity;

            UpdateVolumeLabels();
        }

        private void UpdateVolumeLabels()
        {
            if (musicVolumeLabel != null && musicVolumeSlider != null)
            {
                musicVolumeLabel.text = $"Music: {Mathf.RoundToInt(musicVolumeSlider.value * 100)}%";
            }
            if (sfxVolumeLabel != null && sfxVolumeSlider != null)
            {
                sfxVolumeLabel.text = $"SFX: {Mathf.RoundToInt(sfxVolumeSlider.value * 100)}%";
            }
            if (sensitivityLabel != null && sensitivitySlider != null)
            {
                sensitivityLabel.text = $"Sensitivity: {sensitivitySlider.value:F1}";
            }
        }

        // Button Callbacks
        public void OnResumeClicked()
        {
            Managers.GameManager.Instance?.SetPause(false);
        }

        public void OnSettingsClicked()
        {
            if (pausePanel != null) pausePanel.SetActive(false);
            if (settingsPanel != null) settingsPanel.SetActive(true);
        }

        public void OnBackFromSettingsClicked()
        {
            SaveSettings();
            if (settingsPanel != null) settingsPanel.SetActive(false);
            if (pausePanel != null) pausePanel.SetActive(true);
        }

        public void OnMainMenuClicked()
        {
            if (pausePanel != null) pausePanel.SetActive(false);
            if (confirmQuitPanel != null) confirmQuitPanel.SetActive(true);
        }

        public void OnConfirmQuitClicked()
        {
            Managers.GameManager.Instance?.LoadMainMenu();
        }

        public void OnCancelQuitClicked()
        {
            if (confirmQuitPanel != null) confirmQuitPanel.SetActive(false);
            if (pausePanel != null) pausePanel.SetActive(true);
        }

        public void OnQuitGameClicked()
        {
            Managers.GameManager.Instance?.QuitGame();
        }

        // Settings Callbacks
        private void OnMusicVolumeChanged(float value)
        {
            Audio.AudioManager.Instance?.SetMusicVolume(value);
            UpdateVolumeLabels();
        }

        private void OnSFXVolumeChanged(float value)
        {
            Audio.AudioManager.Instance?.SetSFXVolume(value);
            UpdateVolumeLabels();
        }

        private void OnSensitivityChanged(float value)
        {
            PlayerPrefs.SetFloat("Sensitivity", value);
            UpdateVolumeLabels();
        }

        private void SaveSettings()
        {
            PlayerPrefs.Save();
        }
    }
}
