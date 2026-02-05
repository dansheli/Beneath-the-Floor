using UnityEngine;
using UnityEditor;

namespace BeneathTheFloor.Editor
{
    /// <summary>
    /// Editor tool to clean up shaft walls that incorrectly extend into the basement.
    /// </summary>
    public static class ShaftWallCleanup
    {
        [MenuItem("Tools/Beneath The Floor/Digging/Cleanup Shaft Walls Above Floor")]
        public static void CleanupShaftWalls()
        {
            // Find the basement floor to determine the floor Y level
            float floorTopY = -3.0f; // Default basement floor Y

            GameObject basementFloor = GameObject.Find("BasementFloor");
            if (basementFloor != null)
            {
                floorTopY = basementFloor.transform.position.y + (basementFloor.transform.localScale.y / 2f);
                Debug.Log($"[ShaftWallCleanup] Found BasementFloor, floor top Y = {floorTopY:F2}");
            }
            else
            {
                Debug.LogWarning("[ShaftWallCleanup] BasementFloor not found, using default Y = -3.0");
            }

            int removedCount = 0;
            int adjustedCount = 0;

            // Find and process shaft-related objects
            string[] shaftObjectNames = new string[]
            {
                "DigShaftRoot",
                "ShaftWall",
                "SafetyWall",
                "SafetyColliders"
            };

            foreach (string name in shaftObjectNames)
            {
                GameObject[] objects = FindObjectsByPartialName(name);
                foreach (GameObject obj in objects)
                {
                    // Check if this object extends above the floor
                    Bounds? bounds = GetObjectBounds(obj);
                    if (bounds.HasValue)
                    {
                        float topY = bounds.Value.max.y;

                        if (topY > floorTopY + 0.1f) // Allow small tolerance
                        {
                            Debug.Log($"[ShaftWallCleanup] Object '{obj.name}' extends above floor (top Y = {topY:F2}, floor = {floorTopY:F2})");

                            // Option 1: Remove the object entirely
                            if (obj.name.Contains("DigShaftRoot"))
                            {
                                Undo.DestroyObjectImmediate(obj);
                                removedCount++;
                                Debug.Log($"[ShaftWallCleanup] Removed '{obj.name}'");
                                continue;
                            }

                            // Option 2: Try to adjust the object to not extend above floor
                            // This is more complex, so for now we'll just warn
                            Debug.LogWarning($"[ShaftWallCleanup] Object '{obj.name}' may need manual adjustment");
                            adjustedCount++;
                        }
                    }
                }
            }

            // Also check for any objects in the SafetyColliders container
            GameObject safetyColliders = GameObject.Find("SafetyColliders");
            if (safetyColliders != null)
            {
                foreach (Transform child in safetyColliders.transform)
                {
                    Bounds? bounds = GetObjectBounds(child.gameObject);
                    if (bounds.HasValue && bounds.Value.max.y > floorTopY + 0.1f)
                    {
                        Debug.LogWarning($"[ShaftWallCleanup] SafetyCollider '{child.name}' extends above floor!");
                    }
                }
            }

            Debug.Log($"[ShaftWallCleanup] Cleanup complete: {removedCount} objects removed, {adjustedCount} need attention");

            if (removedCount > 0)
            {
                EditorUtility.DisplayDialog(
                    "Shaft Wall Cleanup",
                    $"Removed {removedCount} shaft wall objects that extended above the basement floor.\n\n" +
                    "Enter Play Mode to regenerate correct walls.",
                    "OK"
                );
            }
            else
            {
                EditorUtility.DisplayDialog(
                    "Shaft Wall Cleanup",
                    "No shaft walls found extending above the basement floor.",
                    "OK"
                );
            }
        }

        [MenuItem("Tools/Beneath The Floor/Digging/Verify Shaft Wall Positions")]
        public static void VerifyShaftWalls()
        {
            // Find floor level
            float floorTopY = -3.0f;
            GameObject basementFloor = GameObject.Find("BasementFloor");
            if (basementFloor != null)
            {
                floorTopY = basementFloor.transform.position.y + (basementFloor.transform.localScale.y / 2f);
            }

            Debug.Log("╔══════════════════════════════════════════════════════════════╗");
            Debug.Log("║          SHAFT WALL POSITION VERIFICATION                     ║");
            Debug.Log("╠══════════════════════════════════════════════════════════════╣");
            Debug.Log($"║  Basement Floor Top Y: {floorTopY:F2}                                   ║");
            Debug.Log("╠══════════════════════════════════════════════════════════════╣");

            bool allOK = true;

            // Check SafetyColliders
            GameObject safetyColliders = GameObject.Find("SafetyColliders");
            if (safetyColliders != null)
            {
                Debug.Log("║  SafetyColliders found:                                      ║");
                foreach (Transform child in safetyColliders.transform)
                {
                    BoxCollider box = child.GetComponent<BoxCollider>();
                    if (box != null)
                    {
                        Bounds bounds = box.bounds;
                        string status = bounds.max.y <= floorTopY + 0.1f ? "✓" : "✗ ABOVE FLOOR!";
                        Debug.Log($"║    {child.name}: top Y = {bounds.max.y:F2} {status}");
                        if (bounds.max.y > floorTopY + 0.1f) allOK = false;
                    }
                }
            }
            else
            {
                Debug.Log("║  SafetyColliders not found (will be created at runtime)     ║");
            }

            // Check DigShaftRoot
            GameObject shaftRoot = GameObject.Find("DigShaftRoot");
            if (shaftRoot != null)
            {
                Debug.Log("║  DigShaftRoot found:                                         ║");
                Debug.Log($"║    Position Y: {shaftRoot.transform.position.y:F2}                                      ║");

                foreach (Transform child in shaftRoot.transform)
                {
                    if (child.name.Contains("ShaftWall"))
                    {
                        Bounds bounds = child.GetComponent<Renderer>()?.bounds ?? new Bounds();
                        string status = bounds.max.y <= floorTopY + 0.1f ? "✓" : "✗ ABOVE FLOOR!";
                        Debug.Log($"║    {child.name}: top Y = {bounds.max.y:F2} {status}");
                        if (bounds.max.y > floorTopY + 0.1f) allOK = false;
                    }
                }
            }
            else
            {
                Debug.Log("║  DigShaftRoot not found (legacy shaft not in use)           ║");
            }

            Debug.Log("╠══════════════════════════════════════════════════════════════╣");
            if (allOK)
            {
                Debug.Log("║  ✓ ALL WALLS ARE BELOW BASEMENT FLOOR - OK!                  ║");
            }
            else
            {
                Debug.LogWarning("║  ✗ SOME WALLS EXTEND ABOVE FLOOR - USE CLEANUP TOOL!        ║");
            }
            Debug.Log("╚══════════════════════════════════════════════════════════════╝");
        }

        private static GameObject[] FindObjectsByPartialName(string partialName)
        {
            var allObjects = Object.FindObjectsOfType<GameObject>();
            var matches = new System.Collections.Generic.List<GameObject>();

            foreach (var obj in allObjects)
            {
                if (obj.name.Contains(partialName))
                {
                    matches.Add(obj);
                }
            }

            return matches.ToArray();
        }

        private static Bounds? GetObjectBounds(GameObject obj)
        {
            // Try to get bounds from renderer
            Renderer renderer = obj.GetComponent<Renderer>();
            if (renderer != null)
            {
                return renderer.bounds;
            }

            // Try to get bounds from collider
            Collider collider = obj.GetComponent<Collider>();
            if (collider != null)
            {
                return collider.bounds;
            }

            // Check children
            renderer = obj.GetComponentInChildren<Renderer>();
            if (renderer != null)
            {
                return renderer.bounds;
            }

            collider = obj.GetComponentInChildren<Collider>();
            if (collider != null)
            {
                return collider.bounds;
            }

            return null;
        }
    }
}
