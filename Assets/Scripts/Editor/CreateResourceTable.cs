using UnityEngine;
using UnityEditor;
using BeneathTheFloor.Digging;

public static class CreateResourceTable
{
    [MenuItem("Tools/Beneath The Floor/Create Underground Resource Table")]
    public static void CreateTable()
    {
        // Create instance
        var table = ScriptableObject.CreateInstance<UndergroundResourceTable>();

        // Initialize with default layers
        table.InitializeDefaultLayers();

        // Create asset
        string path = "Assets/GameData/UndergroundResourceTable.asset";

        // Ensure directory exists
        if (!AssetDatabase.IsValidFolder("Assets/GameData"))
        {
            AssetDatabase.CreateFolder("Assets", "GameData");
        }

        AssetDatabase.CreateAsset(table, path);
        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();

        // Select the new asset
        Selection.activeObject = table;
        EditorGUIUtility.PingObject(table);

        Debug.Log($"[CreateResourceTable] Created UndergroundResourceTable at {path} with {table.layers.Count} layers");
    }
}
