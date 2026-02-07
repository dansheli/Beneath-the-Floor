using UnityEngine;
using UnityEditor;
using System.IO;

public static class ClearAllSaves
{
    [MenuItem("Tools/Clear ALL Save Data")]
    public static void ClearEverything()
    {
        // 1. Nuke PlayerPrefs (RuntimeUpgrades, tool state, etc.)
        PlayerPrefs.DeleteAll();
        PlayerPrefs.Save();
        Debug.Log("[ClearAllSaves] PlayerPrefs cleared.");

        // 2. Delete all files in persistentDataPath (save JSONs, terrain, etc.)
        string path = Application.persistentDataPath;
        if (Directory.Exists(path))
        {
            int count = 0;
            foreach (string file in Directory.GetFiles(path))
            {
                File.Delete(file);
                count++;
            }
            Debug.Log($"[ClearAllSaves] Deleted {count} files from {path}");
        }

        Debug.Log("[ClearAllSaves] ALL save data wiped. Next play will be a fresh start.");
    }
}
