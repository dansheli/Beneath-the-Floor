using UnityEngine;
using UnityEditor;
using System.IO;

public class FolderStructureSetup : Editor
{
    [MenuItem("Tools/Setup/Create Folder Structure")]
    public static void CreateFolderStructure()
    {
        string[] folders = new string[]
        {
            "Assets/Scenes",
            "Assets/Scripts/Player",
            "Assets/Scripts/Digging",
            "Assets/Scripts/Inventory",
            "Assets/Scripts/UI",
            "Assets/Scripts/NPC",
            "Assets/Scripts/Managers",
            "Assets/Scripts/Interaction",
            "Assets/Scripts/Story",
            "Assets/Scripts/Audio",
            "Assets/Scripts/Upgrades",
            "Assets/Prefabs/Player",
            "Assets/Prefabs/Environment",
            "Assets/Prefabs/Items",
            "Assets/Prefabs/UI",
            "Assets/Prefabs/NPC",
            "Assets/Materials",
            "Assets/Textures",
            "Assets/Models",
            "Assets/Audio/SFX",
            "Assets/Audio/Music",
            "Assets/UI/Sprites",
            "Assets/UI/Fonts",
            "Assets/Resources",
            "Assets/StreamingAssets"
        };

        foreach (string folder in folders)
        {
            string fullPath = Path.Combine(Application.dataPath, "..", folder);
            if (!Directory.Exists(fullPath))
            {
                Directory.CreateDirectory(fullPath);
                Debug.Log($"Created folder: {folder}");
            }
        }

        AssetDatabase.Refresh();
        Debug.Log("Folder structure created successfully!");
    }
}
