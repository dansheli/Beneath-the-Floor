using UnityEngine;
using UnityEditor;
using BeneathTheFloor.Player;

public class JetpackSetup
{
    [MenuItem("Tools/Beneath The Floor/Setup Jetpack System")]
    public static void SetupJetpackSystem()
    {
        // 1. Find the Player object
        GameObject player = GameObject.Find("Player");
        if (player == null)
        {
            Debug.LogError("[JetpackSetup] Could not find 'Player' in scene!");
            return;
        }

        // 2. Add JetpackController to player if not present
        JetpackController jetpackController = player.GetComponent<JetpackController>();
        if (jetpackController == null)
        {
            jetpackController = player.AddComponent<JetpackController>();
            Debug.Log("[JetpackSetup] Added JetpackController to Player");
        }
        else
        {
            Debug.Log("[JetpackSetup] JetpackController already exists on Player");
        }

        // 3. Try to find and assign the jetpack sound
        string soundPath = "Assets/Audio/SFX/Equipment/Jetpack/Jetpack_Lift_Loop.wav.wav";
        AudioClip jetpackSound = AssetDatabase.LoadAssetAtPath<AudioClip>(soundPath);
        if (jetpackSound != null)
        {
            SerializedObject so = new SerializedObject(jetpackController);
            SerializedProperty loopSoundProp = so.FindProperty("jetpackLoopSound");
            if (loopSoundProp != null)
            {
                loopSoundProp.objectReferenceValue = jetpackSound;
                so.ApplyModifiedProperties();
                Debug.Log("[JetpackSetup] Assigned jetpack loop sound");
            }
        }

        // 4. Find the Jetpack world object
        GameObject jetpackWorldObject = GameObject.Find("Jetpack");
        if (jetpackWorldObject == null)
        {
            Debug.LogWarning("[JetpackSetup] Could not find 'Jetpack' world object in scene. You'll need to add JetpackPickup component manually.");
        }
        else
        {
            // Add JetpackPickup component if not present
            JetpackPickup pickup = jetpackWorldObject.GetComponent<JetpackPickup>();
            if (pickup == null)
            {
                pickup = jetpackWorldObject.AddComponent<JetpackPickup>();
                Debug.Log("[JetpackSetup] Added JetpackPickup to Jetpack world object");
            }
            else
            {
                Debug.Log("[JetpackSetup] JetpackPickup already exists on Jetpack world object");
            }

            // Ensure it has a collider
            Collider col = jetpackWorldObject.GetComponent<Collider>();
            if (col == null)
            {
                BoxCollider box = jetpackWorldObject.AddComponent<BoxCollider>();
                box.isTrigger = false;
                box.size = new Vector3(1f, 1f, 1f);
                Debug.Log("[JetpackSetup] Added BoxCollider to Jetpack");
            }

            Selection.activeGameObject = jetpackWorldObject;
        }

        EditorUtility.SetDirty(player);
        if (jetpackWorldObject != null)
        {
            EditorUtility.SetDirty(jetpackWorldObject);
        }

        Debug.Log("[JetpackSetup] Jetpack system setup complete!");
        Debug.Log("[JetpackSetup] - Player has JetpackController");
        Debug.Log("[JetpackSetup] - Jetpack world object has JetpackPickup (if found)");
        Debug.Log("[JetpackSetup] Instructions: Player picks up jetpack, then holds SPACE to fly up (consumes energy)");
    }

    [MenuItem("Tools/Beneath The Floor/Give Player Jetpack (Debug)")]
    public static void GivePlayerJetpack()
    {
        if (!Application.isPlaying)
        {
            Debug.LogWarning("[JetpackSetup] This only works in Play Mode!");
            return;
        }

        JetpackController jetpack = Object.FindObjectOfType<JetpackController>();
        if (jetpack != null)
        {
            jetpack.GiveJetpack();
            Debug.Log("[JetpackSetup] Gave jetpack to player!");
        }
        else
        {
            Debug.LogError("[JetpackSetup] No JetpackController found in scene!");
        }
    }
}
