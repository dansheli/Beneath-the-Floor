using UnityEngine;
using System.Collections.Generic;

namespace BeneathTheFloor.Audio
{
    public class AudioManager : MonoBehaviour
    {
        [Header("Audio Sources")]
        [SerializeField] private AudioSource musicSource;
        [SerializeField] private AudioSource ambientSource;
        [SerializeField] private AudioSource sfxSource;

        [Header("Music Tracks")]
        [SerializeField] private AudioClip houseMusic;
        [SerializeField] private AudioClip basementMusic;
        [SerializeField] private AudioClip deepUndergroundMusic;

        [Header("Ambient Sounds")]
        [SerializeField] private AudioClip houseAmbient;
        [SerializeField] private AudioClip basementAmbient;
        [SerializeField] private AudioClip undergroundAmbient;

        [Header("Sound Effects")]
        [SerializeField] private AudioClip[] footstepSounds;
        [SerializeField] private AudioClip[] digSounds;
        [SerializeField] private AudioClip[] uiClickSounds;
        [SerializeField] private AudioClip discoverySound;
        [SerializeField] private AudioClip upgradeSound;

        [Header("Settings")]
        [SerializeField] private float musicVolume = 0.5f;
        [SerializeField] private float ambientVolume = 0.3f;
        [SerializeField] private float sfxVolume = 1f;
        [SerializeField] private float fadeTime = 2f;

        [Header("Debug")]
        #pragma warning disable CS0414 // Reserved for debug logging
        [SerializeField] private bool enableDebugLogs = false;
        #pragma warning restore CS0414

        public static AudioManager Instance { get; private set; }

        private Dictionary<string, AudioClip> sfxLibrary = new Dictionary<string, AudioClip>();

        private void Awake()
        {
            if (Instance == null)
            {
                Instance = this;

                // DontDestroyOnLoad only works on root GameObjects
                if (transform.parent != null)
                {
                    transform.SetParent(null);
                }
                DontDestroyOnLoad(gameObject);

                InitializeAudioSources();
                BuildSFXLibrary();
            }
            else
            {
                Destroy(gameObject);
            }
        }

        private void Start()
        {
            // Subscribe to scene loading events
            UnityEngine.SceneManagement.SceneManager.sceneLoaded += OnSceneLoaded;

            // Play audio for current scene
            PlayAudioForCurrentScene();
        }

        private void OnDestroy()
        {
            UnityEngine.SceneManagement.SceneManager.sceneLoaded -= OnSceneLoaded;
        }

        private void OnSceneLoaded(UnityEngine.SceneManagement.Scene scene, UnityEngine.SceneManagement.LoadSceneMode mode)
        {
            PlayAudioForScene(scene.name);
        }

        private void PlayAudioForCurrentScene()
        {
            string sceneName = UnityEngine.SceneManagement.SceneManager.GetActiveScene().name;
            PlayAudioForScene(sceneName);
        }

        private void PlayAudioForScene(string sceneName)
        {
            if (sceneName.Contains("House") || sceneName.Contains("Home"))
            {
                PlayHouseAudio();
            }
            else if (sceneName.Contains("Basement") || sceneName.Contains("Underground"))
            {
                PlayBasementAudio();
            }
        }

        public void PlayHouseAudio()
        {
            if (houseMusic != null)
            {
                PlayMusic(MusicType.House);
            }

            if (houseAmbient != null)
            {
                PlayAmbient(AmbientType.House);
            }
        }

        public void PlayBasementAudio()
        {
            if (basementMusic != null)
            {
                PlayMusic(MusicType.Basement);
            }

            if (basementAmbient != null)
            {
                PlayAmbient(AmbientType.Basement);
            }
        }

        private void InitializeAudioSources()
        {
            if (musicSource == null)
            {
                GameObject musicObj = new GameObject("MusicSource");
                musicObj.transform.parent = transform;
                musicSource = musicObj.AddComponent<AudioSource>();
                musicSource.loop = true;
                musicSource.playOnAwake = false;
            }

            if (ambientSource == null)
            {
                GameObject ambientObj = new GameObject("AmbientSource");
                ambientObj.transform.parent = transform;
                ambientSource = ambientObj.AddComponent<AudioSource>();
                ambientSource.loop = true;
                ambientSource.playOnAwake = false;
            }

            if (sfxSource == null)
            {
                GameObject sfxObj = new GameObject("SFXSource");
                sfxObj.transform.parent = transform;
                sfxSource = sfxObj.AddComponent<AudioSource>();
                sfxSource.playOnAwake = false;
            }

            ApplyVolumeSettings();
        }

        private void BuildSFXLibrary()
        {
            if (discoverySound != null) sfxLibrary["discovery"] = discoverySound;
            if (upgradeSound != null) sfxLibrary["upgrade"] = upgradeSound;
        }

        public void ApplyVolumeSettings()
        {
            if (musicSource != null) musicSource.volume = musicVolume;
            if (ambientSource != null) ambientSource.volume = ambientVolume;
            if (sfxSource != null) sfxSource.volume = sfxVolume;
        }

        public void SetMusicVolume(float volume)
        {
            musicVolume = Mathf.Clamp01(volume);
            if (musicSource != null) musicSource.volume = musicVolume;
            PlayerPrefs.SetFloat("MusicVolume", musicVolume);
        }

        public void SetAmbientVolume(float volume)
        {
            ambientVolume = Mathf.Clamp01(volume);
            if (ambientSource != null) ambientSource.volume = ambientVolume;
            PlayerPrefs.SetFloat("AmbientVolume", ambientVolume);
        }

        public void SetSFXVolume(float volume)
        {
            sfxVolume = Mathf.Clamp01(volume);
            if (sfxSource != null) sfxSource.volume = sfxVolume;
            PlayerPrefs.SetFloat("SFXVolume", sfxVolume);
        }

        public void PlayMusic(MusicType type)
        {
            AudioClip clip = type switch
            {
                MusicType.House => houseMusic,
                MusicType.Basement => basementMusic,
                MusicType.DeepUnderground => deepUndergroundMusic,
                _ => null
            };

            if (clip != null && musicSource != null)
            {
                StartCoroutine(FadeMusic(clip));
            }
        }

        public void PlayAmbient(AmbientType type)
        {
            AudioClip clip = type switch
            {
                AmbientType.House => houseAmbient,
                AmbientType.Basement => basementAmbient,
                AmbientType.Underground => undergroundAmbient,
                _ => null
            };

            if (clip != null && ambientSource != null)
            {
                StartCoroutine(FadeAmbient(clip));
            }
        }

        private System.Collections.IEnumerator FadeMusic(AudioClip newClip)
        {
            // Fade out
            float startVolume = musicSource.volume;
            for (float t = 0; t < fadeTime; t += Time.deltaTime)
            {
                musicSource.volume = Mathf.Lerp(startVolume, 0, t / fadeTime);
                yield return null;
            }

            // Switch clip
            musicSource.clip = newClip;
            musicSource.Play();

            // Fade in
            for (float t = 0; t < fadeTime; t += Time.deltaTime)
            {
                musicSource.volume = Mathf.Lerp(0, musicVolume, t / fadeTime);
                yield return null;
            }

            musicSource.volume = musicVolume;
        }

        private System.Collections.IEnumerator FadeAmbient(AudioClip newClip)
        {
            float startVolume = ambientSource.volume;
            for (float t = 0; t < fadeTime; t += Time.deltaTime)
            {
                ambientSource.volume = Mathf.Lerp(startVolume, 0, t / fadeTime);
                yield return null;
            }

            ambientSource.clip = newClip;
            ambientSource.Play();

            for (float t = 0; t < fadeTime; t += Time.deltaTime)
            {
                ambientSource.volume = Mathf.Lerp(0, ambientVolume, t / fadeTime);
                yield return null;
            }

            ambientSource.volume = ambientVolume;
        }

        public void PlaySFX(string sfxName)
        {
            if (sfxLibrary.TryGetValue(sfxName, out AudioClip clip))
            {
                PlaySFX(clip);
            }
        }

        public void PlaySFX(AudioClip clip)
        {
            if (clip != null && sfxSource != null)
            {
                sfxSource.PlayOneShot(clip, sfxVolume);
            }
        }

        public void PlayFootstep()
        {
            if (footstepSounds != null && footstepSounds.Length > 0)
            {
                AudioClip clip = footstepSounds[Random.Range(0, footstepSounds.Length)];
                PlaySFX(clip);
            }
        }

        public void PlayDigSound()
        {
            if (digSounds != null && digSounds.Length > 0)
            {
                AudioClip clip = digSounds[Random.Range(0, digSounds.Length)];
                PlaySFX(clip);
            }
        }

        public void PlayUIClick()
        {
            if (uiClickSounds != null && uiClickSounds.Length > 0)
            {
                AudioClip clip = uiClickSounds[Random.Range(0, uiClickSounds.Length)];
                PlaySFX(clip);
            }
        }

        public void PlayDiscoverySound()
        {
            PlaySFX("discovery");
        }

        public void PlayUpgradeSound()
        {
            PlaySFX("upgrade");
        }

        public void StopMusic()
        {
            if (musicSource != null)
            {
                musicSource.Stop();
            }
        }

        public void StopAmbient()
        {
            if (ambientSource != null)
            {
                ambientSource.Stop();
            }
        }

        public void StopAll()
        {
            StopMusic();
            StopAmbient();
        }
    }

    public enum MusicType
    {
        House,
        Basement,
        DeepUnderground
    }

    public enum AmbientType
    {
        House,
        Basement,
        Underground
    }
}
