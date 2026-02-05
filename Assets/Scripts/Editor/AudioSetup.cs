using UnityEngine;
using UnityEditor;
using BeneathTheFloor.Audio;

public class AudioSetup : Editor
{
    [MenuItem("Tools/Beneath The Floor/Setup Audio Manager")]
    public static void SetupAudioManager()
    {
        // Find AudioManager in scene
        GameObject audioManagerObj = GameObject.Find("AudioManager");
        if (audioManagerObj == null)
        {
            Debug.LogError("AudioManager GameObject not found in scene!");
            return;
        }

        // Add or get AudioManager component
        AudioManager audioManager = audioManagerObj.GetComponent<AudioManager>();
        if (audioManager == null)
        {
            audioManager = audioManagerObj.AddComponent<AudioManager>();
            Debug.Log("Added AudioManager component");
        }

        // Create child audio source objects
        CreateOrGetAudioSource(audioManagerObj, "MusicSource", true);
        CreateOrGetAudioSource(audioManagerObj, "AmbientSource", true);
        CreateOrGetAudioSource(audioManagerObj, "SFXSource", false);

        EditorUtility.SetDirty(audioManagerObj);
        Debug.Log("AudioManager setup complete!");
    }

    private static AudioSource CreateOrGetAudioSource(GameObject parent, string name, bool loop)
    {
        Transform existing = parent.transform.Find(name);
        if (existing != null)
        {
            AudioSource source = existing.GetComponent<AudioSource>();
            if (source != null)
            {
                Debug.Log($"AudioSource '{name}' already exists");
                return source;
            }
        }

        GameObject sourceObj = new GameObject(name);
        sourceObj.transform.parent = parent.transform;
        sourceObj.transform.localPosition = Vector3.zero;

        AudioSource audioSource = sourceObj.AddComponent<AudioSource>();
        audioSource.playOnAwake = false;
        audioSource.loop = loop;

        if (name == "MusicSource")
        {
            audioSource.volume = 0.5f;
        }
        else if (name == "AmbientSource")
        {
            audioSource.volume = 0.3f;
        }
        else
        {
            audioSource.volume = 1f;
        }

        Debug.Log($"Created AudioSource: {name}");
        return audioSource;
    }

    [MenuItem("Tools/Beneath The Floor/Create Placeholder Audio Clips")]
    public static void CreatePlaceholderAudioClips()
    {
        // Note: Unity doesn't allow creating AudioClips programmatically with audio data
        // This menu item will create the folder structure and log what clips are needed

        string audioPath = "Assets/Audio";
        string musicPath = audioPath + "/Music";
        string sfxPath = audioPath + "/SFX";
        string ambientPath = audioPath + "/Ambient";

        // Create folders if they don't exist
        if (!AssetDatabase.IsValidFolder(audioPath))
        {
            AssetDatabase.CreateFolder("Assets", "Audio");
        }
        if (!AssetDatabase.IsValidFolder(musicPath))
        {
            AssetDatabase.CreateFolder(audioPath, "Music");
        }
        if (!AssetDatabase.IsValidFolder(sfxPath))
        {
            AssetDatabase.CreateFolder(audioPath, "SFX");
        }
        if (!AssetDatabase.IsValidFolder(ambientPath))
        {
            AssetDatabase.CreateFolder(audioPath, "Ambient");
        }

        AssetDatabase.Refresh();

        Debug.Log("=== Audio Folder Structure Created ===");
        Debug.Log("Audio clips needed:");
        Debug.Log("--- Music (Assets/Audio/Music/) ---");
        Debug.Log("  - BackgroundMusic_House.wav/mp3");
        Debug.Log("  - BackgroundMusic_Basement.wav/mp3");
        Debug.Log("  - BackgroundMusic_DeepUnderground.wav/mp3");
        Debug.Log("--- Ambient (Assets/Audio/Ambient/) ---");
        Debug.Log("  - Ambient_House.wav/mp3");
        Debug.Log("  - Ambient_Basement.wav/mp3");
        Debug.Log("  - Ambient_Underground.wav/mp3");
        Debug.Log("--- SFX (Assets/Audio/SFX/) ---");
        Debug.Log("  - SFX_Dig_01.wav (+ variations)");
        Debug.Log("  - SFX_Footstep_01.wav (+ variations)");
        Debug.Log("  - SFX_UI_Click.wav");
        Debug.Log("  - SFX_Discovery.wav");
        Debug.Log("  - SFX_Upgrade.wav");
        Debug.Log("  - SFX_Interact.wav");
        Debug.Log("=======================================");
        Debug.Log("Add your audio files to these folders, then use 'Setup Audio Manager' to configure.");
    }
}
