using UnityEngine;
using UnityEditor;
using BeneathTheFloor.ResourceSystem;
using BeneathTheFloor.Digging;

namespace BeneathTheFloor.Editor
{
    /// <summary>
    /// Editor tool to generate fixed resource nodes in the hierarchy.
    /// Creates nodes that can be manually positioned and configured.
    /// </summary>
    public static class FixedResourceGenerator
    {
        [MenuItem("Tools/Beneath The Floor/Resource System/Generate Fixed Resources")]
        public static void GenerateFixedResources()
        {
            // Configuration
            int targetNodeCount = 60; // Reduced from 150 for better spacing
            float maxDepth = 10f;
            float minDepth = 1f; // Don't place right at surface
            float minNodeDistance = 2.5f; // Minimum distance between nodes

            // Find or create parent
            GameObject parent = GameObject.Find("FixedResources");
            if (parent != null)
            {
                if (!EditorUtility.DisplayDialog("Fixed Resources Exist",
                    "FixedResources already exists. Delete and regenerate?",
                    "Yes, Regenerate", "Cancel"))
                {
                    return;
                }
                Undo.DestroyObjectImmediate(parent);
            }

            parent = new GameObject("FixedResources");
            Undo.RegisterCreatedObjectUndo(parent, "Generate Fixed Resources");

            // Get pit bounds
            var guidedPit = Object.FindObjectOfType<GuidedPitController>();
            Bounds pitBounds;
            Vector3 pitCenter;
            float pitRadius;

            if (guidedPit != null)
            {
                pitBounds = guidedPit.GetGuidedPitBounds();
                pitCenter = new Vector3(pitBounds.center.x, 0f, pitBounds.center.z);
                pitRadius = Mathf.Min(pitBounds.size.x, pitBounds.size.z) * 0.4f;
            }
            else
            {
                // Default pit area
                pitCenter = Vector3.zero;
                pitRadius = 5f;
                pitBounds = new Bounds(pitCenter, new Vector3(10f, 20f, 10f));
            }

            // Get basement floor Y
            float basementY = -3f;
            var depthManager = Object.FindObjectOfType<DepthManager>();
            if (depthManager != null)
            {
                basementY = depthManager.BasementFloorY;
            }

            // Generate nodes with spacing enforcement
            System.Random rng = new System.Random(42); // Fixed seed for reproducibility
            var placedPositions = new System.Collections.Generic.List<Vector3>();
            int maxAttempts = targetNodeCount * 20; // Prevent infinite loop
            int attempts = 0;
            int nodesCreated = 0;

            while (nodesCreated < targetNodeCount && attempts < maxAttempts)
            {
                attempts++;

                // Random depth (0-10m below basement)
                float depth = minDepth + (float)rng.NextDouble() * (maxDepth - minDepth);
                float worldY = basementY - depth;

                // Position around pit walls (not in center where player digs)
                // Use ring distribution - more nodes at edges
                float angle = (float)rng.NextDouble() * Mathf.PI * 2f;
                float distFromCenter = pitRadius * (0.3f + (float)rng.NextDouble() * 0.7f);

                float worldX = pitCenter.x + Mathf.Cos(angle) * distFromCenter;
                float worldZ = pitCenter.z + Mathf.Sin(angle) * distFromCenter;

                Vector3 position = new Vector3(worldX, worldY, worldZ);

                // Check minimum distance from all existing nodes
                bool tooClose = false;
                foreach (var existingPos in placedPositions)
                {
                    if (Vector3.Distance(position, existingPos) < minNodeDistance)
                    {
                        tooClose = true;
                        break;
                    }
                }

                if (tooClose)
                    continue; // Try again

                // Determine tier based on depth (deeper = rarer)
                int tier = GetTierForDepth(depth, rng);

                // Random size variation
                float size = 0.6f + (float)rng.NextDouble() * 0.8f; // 0.6 to 1.4

                // Create node
                GameObject nodeObj = new GameObject($"Resource_D{depth:F1}_T{tier}_{nodesCreated}");
                nodeObj.transform.position = position;
                nodeObj.transform.SetParent(parent.transform);

                var nodeComp = nodeObj.AddComponent<FixedResourceNode>();
                nodeComp.tier = tier;
                nodeComp.size = size;
                nodeComp.revealDistance = 0.6f + size * 0.2f;

                Undo.RegisterCreatedObjectUndo(nodeObj, "Generate Fixed Resource");

                placedPositions.Add(position);
                nodesCreated++;
            }

            // Select parent
            Selection.activeGameObject = parent;

            Debug.Log($"[FixedResourceGenerator] Generated {nodesCreated} fixed resources from {minDepth}m to {maxDepth}m depth.");
            Debug.Log($"  Min distance between nodes: {minNodeDistance}m");
            Debug.Log($"  Pit center: {pitCenter}, radius: {pitRadius}");
            Debug.Log($"  Parent object: FixedResources (select in hierarchy to see all nodes)");

            // Mark scene dirty
            UnityEditor.SceneManagement.EditorSceneManager.MarkSceneDirty(
                UnityEditor.SceneManagement.EditorSceneManager.GetActiveScene());
        }

        private static int GetTierForDepth(float depth, System.Random rng)
        {
            // Tier distribution based on depth
            // 0-3m: mostly T1
            // 3-6m: T1/T2 mix
            // 6-10m: T2/T3 with some T4

            float roll = (float)rng.NextDouble();

            if (depth < 3f)
            {
                // 80% T1, 15% T2, 5% T3
                if (roll < 0.80f) return 1;
                if (roll < 0.95f) return 2;
                return 3;
            }
            else if (depth < 6f)
            {
                // 50% T1, 35% T2, 12% T3, 3% T4
                if (roll < 0.50f) return 1;
                if (roll < 0.85f) return 2;
                if (roll < 0.97f) return 3;
                return 4;
            }
            else
            {
                // 20% T1, 40% T2, 30% T3, 10% T4
                if (roll < 0.20f) return 1;
                if (roll < 0.60f) return 2;
                if (roll < 0.90f) return 3;
                return 4;
            }
        }

        [MenuItem("Tools/Beneath The Floor/Resource System/Clear Fixed Resources")]
        public static void ClearFixedResources()
        {
            GameObject parent = GameObject.Find("FixedResources");
            if (parent != null)
            {
                Undo.DestroyObjectImmediate(parent);
                Debug.Log("[FixedResourceGenerator] Cleared all fixed resources.");
            }
            else
            {
                Debug.Log("[FixedResourceGenerator] No FixedResources found.");
            }
        }

        [MenuItem("Tools/Beneath The Floor/Resource System/Randomize Fixed Resource Tiers")]
        public static void RandomizeTiers()
        {
            GameObject parent = GameObject.Find("FixedResources");
            if (parent == null)
            {
                Debug.LogWarning("[FixedResourceGenerator] No FixedResources found. Generate first.");
                return;
            }

            var depthManager = Object.FindObjectOfType<DepthManager>();
            float basementY = depthManager != null ? depthManager.BasementFloorY : -3f;

            System.Random rng = new System.Random();
            int count = 0;

            foreach (Transform child in parent.transform)
            {
                var node = child.GetComponent<FixedResourceNode>();
                if (node != null)
                {
                    float depth = basementY - child.position.y;
                    node.tier = GetTierForDepth(depth, rng);
                    EditorUtility.SetDirty(node);
                    count++;
                }
            }

            Debug.Log($"[FixedResourceGenerator] Randomized tiers for {count} nodes.");
        }

        [MenuItem("Tools/Beneath The Floor/Resource System/Randomize Fixed Resource Sizes")]
        public static void RandomizeSizes()
        {
            GameObject parent = GameObject.Find("FixedResources");
            if (parent == null)
            {
                Debug.LogWarning("[FixedResourceGenerator] No FixedResources found. Generate first.");
                return;
            }

            System.Random rng = new System.Random();
            int count = 0;

            foreach (Transform child in parent.transform)
            {
                var node = child.GetComponent<FixedResourceNode>();
                if (node != null)
                {
                    node.size = 0.6f + (float)rng.NextDouble() * 0.8f;
                    node.revealDistance = 0.6f + node.size * 0.2f;
                    EditorUtility.SetDirty(node);
                    count++;
                }
            }

            Debug.Log($"[FixedResourceGenerator] Randomized sizes for {count} nodes.");
        }
    }
}
