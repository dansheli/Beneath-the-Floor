using UnityEngine;
using UnityEditor;
using UnityEngine.Rendering;

namespace BeneathTheFloor.Editor
{
    public class DisableFloorLightmaps
    {
        [MenuItem("Tools/Disable Floor Lightmaps")]
        public static void DisableLightmaps()
        {
            // Find all objects with "Floor" in the name
            MeshRenderer[] allRenderers = Object.FindObjectsOfType<MeshRenderer>();
            int fixedCount = 0;

            foreach (MeshRenderer renderer in allRenderers)
            {
                string name = renderer.gameObject.name.ToLower();

                // Check if this is a floor object
                if (name.Contains("floor"))
                {
                    // Disable lightmap contribution
                    var flags = GameObjectUtility.GetStaticEditorFlags(renderer.gameObject);
                    flags &= ~StaticEditorFlags.ContributeGI; // Remove ContributeGI flag
                    GameObjectUtility.SetStaticEditorFlags(renderer.gameObject, flags);

                    // Set to use Light Probes instead of Lightmaps
                    renderer.receiveGI = ReceiveGI.LightProbes;

                    EditorUtility.SetDirty(renderer.gameObject);
                    fixedCount++;

                    Debug.Log($"[DisableFloorLightmaps] Fixed: {renderer.gameObject.name}");
                }
            }

            if (fixedCount > 0)
            {
                // Mark scene dirty
                UnityEditor.SceneManagement.EditorSceneManager.MarkSceneDirty(
                    UnityEditor.SceneManagement.EditorSceneManager.GetActiveScene());
            }

            Debug.Log($"[DisableFloorLightmaps] Done! Disabled lightmaps on {fixedCount} floor object(s).");
            Debug.Log("[DisableFloorLightmaps] Floors now use Light Probes for indirect lighting (no more seams).");
        }
    }
}
