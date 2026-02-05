using UnityEngine;
using UnityEditor;
using BeneathTheFloor.Save;

namespace BeneathTheFloor.Editor
{
    public static class AddSaveManagerToScene
    {
        [MenuItem("Tools/Beneath The Floor/Add SaveManager to Current Scene")]
        public static void AddSaveManager()
        {
            var existing = Object.FindObjectOfType<SaveManager>();
            if (existing != null)
            {
                Debug.Log("[AddSaveManager] SaveManager already exists in scene.");
                EditorUtility.DisplayDialog("Info", "SaveManager already exists in this scene.", "OK");
                return;
            }

            GameObject saveManagerObj = new GameObject("SaveManager");
            saveManagerObj.AddComponent<SaveManager>();

            UnityEditor.SceneManagement.EditorSceneManager.MarkSceneDirty(
                UnityEngine.SceneManagement.SceneManager.GetActiveScene());

            Debug.Log("[AddSaveManager] SaveManager added to scene.");
            Selection.activeGameObject = saveManagerObj;
        }
    }
}
