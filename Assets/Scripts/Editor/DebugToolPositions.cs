using UnityEngine;
using UnityEditor;

namespace BeneathTheFloor.Editor
{
    public static class DebugToolPositions
    {
        [MenuItem("Beneath The Floor/Debug Tool Positions")]
        public static void LogPositions()
        {
            Debug.Log("=== FULL HIERARCHY DEBUG ===");

            // Find Player
            var player = GameObject.Find("Player");
            if (player == null)
            {
                Debug.LogError("Player not found!");
                return;
            }

            LogTransform("1. Player", player.transform);

            // Find CameraHolder under Player
            var cameraHolder = player.transform.Find("CameraHolder");
            if (cameraHolder != null)
            {
                LogTransform("2. Player/CameraHolder", cameraHolder);
            }
            else
            {
                Debug.LogWarning("CameraHolder not found under Player!");
            }

            // Find Main Camera
            Transform mainCam = null;
            if (cameraHolder != null)
            {
                mainCam = cameraHolder.Find("Main Camera");
            }
            if (mainCam == null && Camera.main != null)
            {
                mainCam = Camera.main.transform;
            }

            if (mainCam != null)
            {
                LogTransform("3. Main Camera", mainCam);
            }
            else
            {
                Debug.LogError("Main Camera not found!");
                return;
            }

            // Find ToolAnchor/ToolHolder under camera
            Transform toolAnchor = mainCam.Find("ToolAnchor");
            if (toolAnchor == null) toolAnchor = mainCam.Find("ToolHolder");

            if (toolAnchor != null)
            {
                LogTransform("4. ToolAnchor", toolAnchor);
            }
            else
            {
                Debug.LogWarning("ToolAnchor/ToolHolder not found under Main Camera!");
            }

            // Find all three tier tools
            Debug.Log("=== TOOL TIERS ===");

            string[] toolNames = {
                "Tool_Shovel_tier1",
                "Tool_Hoe_tier2",
                "Tool_Picaxe_tier3",
                "Tool_Pickaxe_tier3"  // alternate spelling
            };

            foreach (var toolName in toolNames)
            {
                var tool = GameObject.Find(toolName);
                if (tool != null)
                {
                    LogTransform($"TOOL: {toolName}", tool.transform);
                    Debug.Log($"   PARENT CHAIN: {GetParentChain(tool.transform)}");
                }
            }

            // Summary
            Debug.Log("=== SUMMARY ===");
            var t1 = GameObject.Find("Tool_Shovel_tier1");
            var t2 = GameObject.Find("Tool_Hoe_tier2");
            var t3 = GameObject.Find("Tool_Picaxe_tier3") ?? GameObject.Find("Tool_Pickaxe_tier3");

            Debug.Log($"Tier 1 (Shovel): {(t1 != null ? "FOUND" : "NOT FOUND")}");
            Debug.Log($"Tier 2 (Hoe): {(t2 != null ? "FOUND" : "NOT FOUND")}");
            Debug.Log($"Tier 3 (Pickaxe): {(t3 != null ? "FOUND" : "NOT FOUND")}");

            Debug.Log("=== END DEBUG ===");
        }

        private static void LogTransform(string label, Transform t)
        {
            Debug.Log($"{label}:");
            Debug.Log($"   WorldPos: {t.position}");
            Debug.Log($"   LocalPos: {t.localPosition}");
            Debug.Log($"   LocalRot: {t.localRotation.eulerAngles}");
            Debug.Log($"   LocalScale: {t.localScale}");
            Debug.Log($"   Parent: {(t.parent != null ? t.parent.name : "NONE (root)")}");
        }

        private static string GetParentChain(Transform t)
        {
            string chain = t.name;
            Transform parent = t.parent;
            while (parent != null)
            {
                chain = parent.name + " -> " + chain;
                parent = parent.parent;
            }
            return chain;
        }
    }
}
