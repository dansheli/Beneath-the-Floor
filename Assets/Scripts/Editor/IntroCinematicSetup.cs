using UnityEngine;
using UnityEngine.UI;
using UnityEditor;
using TMPro;

namespace BeneathTheFloor.GameFlow.Editor
{
    /// <summary>
    /// Editor utility to set up the Intro Cinematic UI in the current scene.
    /// </summary>
    public static class IntroCinematicSetup
    {
        [MenuItem("Beneath The Floor/Setup Intro Cinematic")]
        public static void SetupIntroCinematic()
        {
            // Check if already exists
            var existing = Object.FindObjectOfType<IntroCinematicController>();
            if (existing != null)
            {
                EditorUtility.DisplayDialog("Already Exists",
                    "IntroCinematicController already exists in the scene.", "OK");
                Selection.activeGameObject = existing.gameObject;
                return;
            }

            // Create Canvas
            GameObject canvasObj = new GameObject("IntroCinematicCanvas");
            Canvas canvas = canvasObj.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = 100;
            canvasObj.AddComponent<CanvasScaler>();
            canvasObj.AddComponent<GraphicRaycaster>();

            // Create Black Overlay
            GameObject overlayObj = new GameObject("BlackOverlay");
            overlayObj.transform.SetParent(canvasObj.transform, false);

            Image overlayImage = overlayObj.AddComponent<Image>();
            overlayImage.color = Color.black;

            RectTransform overlayRect = overlayObj.GetComponent<RectTransform>();
            overlayRect.anchorMin = Vector2.zero;
            overlayRect.anchorMax = Vector2.one;
            overlayRect.offsetMin = Vector2.zero;
            overlayRect.offsetMax = Vector2.zero;

            CanvasGroup overlayCanvasGroup = overlayObj.AddComponent<CanvasGroup>();
            overlayCanvasGroup.alpha = 0f;

            // Create Text
            GameObject textObj = new GameObject("CinematicText");
            textObj.transform.SetParent(overlayObj.transform, false);

            TextMeshProUGUI tmpText = textObj.AddComponent<TextMeshProUGUI>();
            tmpText.text = "";
            tmpText.fontSize = 42;
            tmpText.alignment = TextAlignmentOptions.Center;
            tmpText.color = Color.white;

            RectTransform textRect = textObj.GetComponent<RectTransform>();
            textRect.anchorMin = new Vector2(0.1f, 0.4f);
            textRect.anchorMax = new Vector2(0.9f, 0.6f);
            textRect.offsetMin = Vector2.zero;
            textRect.offsetMax = Vector2.zero;

            CanvasGroup textCanvasGroup = textObj.AddComponent<CanvasGroup>();
            textCanvasGroup.alpha = 0f;

            // Create Controller GameObject
            GameObject controllerObj = new GameObject("IntroCinematicController");
            IntroCinematicController controller = controllerObj.AddComponent<IntroCinematicController>();

            // Use SerializedObject to set private serialized fields
            SerializedObject so = new SerializedObject(controller);
            so.FindProperty("overlayCanvasGroup").objectReferenceValue = overlayCanvasGroup;
            so.FindProperty("cinematicText").objectReferenceValue = tmpText;
            so.FindProperty("textCanvasGroup").objectReferenceValue = textCanvasGroup;
            so.ApplyModifiedProperties();

            // Mark scene dirty
            UnityEditor.SceneManagement.EditorSceneManager.MarkSceneDirty(
                UnityEditor.SceneManagement.EditorSceneManager.GetActiveScene());

            // Select the controller
            Selection.activeGameObject = controllerObj;

            Debug.Log("[IntroCinematicSetup] Intro Cinematic UI created successfully!");
            EditorUtility.DisplayDialog("Setup Complete",
                "Intro Cinematic has been set up.\n\nSave your scene to keep the changes.", "OK");
        }
    }
}
