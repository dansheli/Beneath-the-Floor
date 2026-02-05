using UnityEngine;
using UnityEditor;
using System.IO;

/// <summary>
/// Editor utility to capture Scene View screenshots for Claude to see.
/// </summary>
public class SceneViewCapture
{
        private static string capturePath = "SceneCapture.png";

        [MenuItem("Tools/Capture Scene View")]
        public static void CaptureSceneView()
        {
            SceneView sceneView = SceneView.lastActiveSceneView;
            if (sceneView == null)
            {
                Debug.LogError("[SceneCapture] No active Scene View found!");
                return;
            }

            // Get the scene view camera
            Camera cam = sceneView.camera;
            if (cam == null)
            {
                Debug.LogError("[SceneCapture] Scene View camera not found!");
                return;
            }

            // Get scene view size
            int width = (int)sceneView.position.width;
            int height = (int)sceneView.position.height;

            // Ensure minimum size
            width = Mathf.Max(width, 800);
            height = Mathf.Max(height, 600);

            // Create render texture
            RenderTexture rt = new RenderTexture(width, height, 24);
            RenderTexture prevRT = cam.targetTexture;
            RenderTexture prevActive = RenderTexture.active;

            try
            {
                cam.targetTexture = rt;
                cam.Render();

                // Read pixels
                RenderTexture.active = rt;
                Texture2D screenshot = new Texture2D(width, height, TextureFormat.RGB24, false);
                screenshot.ReadPixels(new Rect(0, 0, width, height), 0, 0);
                screenshot.Apply();

                // Save to project root folder
                string fullPath = Path.Combine(Application.dataPath, "..", capturePath);
                fullPath = Path.GetFullPath(fullPath);

                byte[] bytes = screenshot.EncodeToPNG();
                File.WriteAllBytes(fullPath, bytes);

                // Cleanup
                Object.DestroyImmediate(screenshot);

                Debug.Log($"[SceneCapture] Scene View captured to: {fullPath}");
            }
            finally
            {
                // Restore previous state
                cam.targetTexture = prevRT;
                RenderTexture.active = prevActive;
                Object.DestroyImmediate(rt);
            }
        }

        [MenuItem("Tools/Capture Game View")]
        public static void CaptureGameView()
        {
            string fullPath = Path.Combine(Application.dataPath, "..", "GameCapture.png");
            fullPath = Path.GetFullPath(fullPath);

            ScreenCapture.CaptureScreenshot(fullPath);
            Debug.Log($"[SceneCapture] Game View will be captured to: {fullPath} (on next frame)");
        }
}
