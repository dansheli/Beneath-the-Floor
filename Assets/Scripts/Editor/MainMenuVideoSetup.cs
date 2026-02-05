using UnityEngine;
using UnityEngine.UI;
using UnityEditor;
using BeneathTheFloor.GameFlow;

namespace BeneathTheFloor.Editor
{
    /// <summary>
    /// Editor utility to set up the main menu video background.
    /// </summary>
    public static class MainMenuVideoSetup
    {
        [MenuItem("Beneath The Floor/UI/Setup Main Menu Video Background")]
        public static void SetupVideoBackground()
        {
            // Check if we're in the MainMenuScene
            string sceneName = UnityEditor.SceneManagement.EditorSceneManager.GetActiveScene().name;
            if (!sceneName.Contains("MainMenu"))
            {
                bool proceed = EditorUtility.DisplayDialog(
                    "Scene Check",
                    $"Current scene is '{sceneName}'.\n\n" +
                    "This setup is intended for the Main Menu scene.\n\n" +
                    "Do you want to continue anyway?",
                    "Yes, Continue",
                    "Cancel"
                );
                if (!proceed) return;
            }

            // Check if already exists
            var existing = Object.FindObjectOfType<MainMenuVideoBackground>();
            if (existing != null)
            {
                EditorUtility.DisplayDialog(
                    "Already Exists",
                    "MainMenuVideoBackground already exists in this scene.\n\n" +
                    "Select the existing object in the Hierarchy to configure it.",
                    "OK"
                );
                Selection.activeGameObject = existing.gameObject;
                return;
            }

            // Find or create a canvas for the video
            Canvas mainCanvas = null;
            var allCanvases = Object.FindObjectsOfType<Canvas>();

            foreach (var canvas in allCanvases)
            {
                if (canvas.renderMode == RenderMode.ScreenSpaceOverlay)
                {
                    mainCanvas = canvas;
                    break;
                }
            }

            if (mainCanvas == null)
            {
                // Create a new canvas
                GameObject canvasObj = new GameObject("MainMenuCanvas");
                mainCanvas = canvasObj.AddComponent<Canvas>();
                mainCanvas.renderMode = RenderMode.ScreenSpaceOverlay;
                mainCanvas.sortingOrder = 0;

                var scaler = canvasObj.AddComponent<CanvasScaler>();
                scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
                scaler.referenceResolution = new Vector2(1920, 1080);
                scaler.matchWidthOrHeight = 0.5f;

                canvasObj.AddComponent<GraphicRaycaster>();

                Undo.RegisterCreatedObjectUndo(canvasObj, "Create Main Menu Canvas");
            }

            // Create the video background object
            GameObject videoBackgroundObj = new GameObject("VideoBackground");
            videoBackgroundObj.transform.SetParent(mainCanvas.transform, false);
            videoBackgroundObj.transform.SetAsFirstSibling(); // Move to back (behind UI)

            // Add RawImage component
            RawImage rawImage = videoBackgroundObj.AddComponent<RawImage>();
            rawImage.color = new Color(0.7f, 0.7f, 0.7f, 1f); // Slightly darkened
            rawImage.raycastTarget = false;

            // Stretch to fill screen
            RectTransform rect = videoBackgroundObj.GetComponent<RectTransform>();
            rect.anchorMin = Vector2.zero;
            rect.anchorMax = Vector2.one;
            rect.offsetMin = Vector2.zero;
            rect.offsetMax = Vector2.zero;

            // Add the video background component
            MainMenuVideoBackground videoBackground = videoBackgroundObj.AddComponent<MainMenuVideoBackground>();

            // Register for undo
            Undo.RegisterCreatedObjectUndo(videoBackgroundObj, "Create Video Background");

            // Mark scene dirty
            UnityEditor.SceneManagement.EditorSceneManager.MarkSceneDirty(
                UnityEditor.SceneManagement.EditorSceneManager.GetActiveScene());

            // Select the new object
            Selection.activeGameObject = videoBackgroundObj;

            Debug.Log("[MainMenuVideoSetup] Video background created!");

            EditorUtility.DisplayDialog(
                "Video Background Created",
                "MainMenuVideoBackground has been added to the scene!\n\n" +
                "To configure:\n" +
                "1. Select 'VideoBackground' in the Hierarchy\n" +
                "2. In the Inspector, assign a Video Clip\n" +
                "3. Adjust darken amount and tint as needed\n\n" +
                "Supported formats: MP4, WebM, MOV\n\n" +
                "Place your video in Assets/Art/UI/ or similar folder.",
                "OK"
            );
        }
    }
}
