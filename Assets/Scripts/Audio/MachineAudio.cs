using UnityEngine;

namespace BeneathTheFloor.Audio
{
    public class MachineAudio : MonoBehaviour
    {
        [Header("Audio Sources")]
        [SerializeField] private AudioSource mainSource;
        [SerializeField] private AudioSource loopSource;

        [Header("Sound Clips")]
        [SerializeField] private AudioClip activateSound;
        [SerializeField] private AudioClip deactivateSound;
        [SerializeField] private AudioClip processLoop;
        [SerializeField] private AudioClip completeSound;
        [SerializeField] private AudioClip errorSound;

        [Header("Settings")]
        [SerializeField] private float mainVolume = 1f;
        [SerializeField] private float loopVolume = 0.5f;
        [SerializeField] private float pitchVariation = 0.1f;

        private void Awake()
        {
            if (mainSource == null)
            {
                mainSource = gameObject.AddComponent<AudioSource>();
                mainSource.playOnAwake = false;
            }

            if (loopSource == null)
            {
                loopSource = gameObject.AddComponent<AudioSource>();
                loopSource.playOnAwake = false;
                loopSource.loop = true;
            }

            mainSource.volume = mainVolume;
            loopSource.volume = loopVolume;
        }

        public void PlayActivate()
        {
            PlayOneShot(activateSound);
        }

        public void PlayDeactivate()
        {
            PlayOneShot(deactivateSound);
        }

        public void PlayComplete()
        {
            PlayOneShot(completeSound);
        }

        public void PlayError()
        {
            PlayOneShot(errorSound);
        }

        public void StartProcessLoop()
        {
            if (processLoop != null && loopSource != null)
            {
                loopSource.clip = processLoop;
                loopSource.Play();
            }
        }

        public void StopProcessLoop()
        {
            if (loopSource != null)
            {
                loopSource.Stop();
            }
        }

        public void SetLoopVolume(float volume)
        {
            loopVolume = Mathf.Clamp01(volume);
            if (loopSource != null)
            {
                loopSource.volume = loopVolume;
            }
        }

        private void PlayOneShot(AudioClip clip)
        {
            if (clip != null && mainSource != null)
            {
                float pitch = 1f + Random.Range(-pitchVariation, pitchVariation);
                mainSource.pitch = pitch;
                mainSource.PlayOneShot(clip);
            }
        }

        public void PlayClip(AudioClip clip, float volumeMultiplier = 1f)
        {
            if (clip != null && mainSource != null)
            {
                mainSource.PlayOneShot(clip, mainVolume * volumeMultiplier);
            }
        }
    }
}
