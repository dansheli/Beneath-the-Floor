#if UNITY_EDITOR
using UnityEngine;
using UnityEditor;
using BeneathTheFloor.InventoryUI;
using BeneathTheFloor.World;

namespace BeneathTheFloor.Editor
{
    /// <summary>
    /// Editor utilities for setting up the inventory UI system.
    /// </summary>
    public class InventoryUISetupEditor
    {
        [MenuItem("BeneathTheFloor/Setup/Create Inventory UI System")]
        public static void CreateInventoryUISystem()
        {
            // Check if already exists
            if (Object.FindObjectOfType<InventoryUIManager>() != null)
            {
                Debug.Log("[InventoryUISetup] InventoryUIManager already exists in scene");
                return;
            }

            // Find the HUD Canvas
            Canvas canvas = FindHUDCanvas();
            if (canvas == null)
            {
                Debug.LogError("[InventoryUISetup] No HUD Canvas found! Create one first.");
                return;
            }

            // Create InventoryUIManager
            GameObject managerObj = new GameObject("InventoryUIManager");
            managerObj.transform.SetParent(canvas.transform, false);
            InventoryUIManager manager = managerObj.AddComponent<InventoryUIManager>();

            // Register undo
            Undo.RegisterCreatedObjectUndo(managerObj, "Create Inventory UI System");

            // Create WorldDropManager if not exists
            if (Object.FindObjectOfType<WorldDropManager>() == null)
            {
                GameObject dropManagerObj = new GameObject("WorldDropManager");
                dropManagerObj.AddComponent<WorldDropManager>();
                Undo.RegisterCreatedObjectUndo(dropManagerObj, "Create World Drop Manager");
                Debug.Log("[InventoryUISetup] Created WorldDropManager");
            }

            Debug.Log("[InventoryUISetup] Created Inventory UI System");

            // Select the created object
            Selection.activeGameObject = managerObj;
        }

        [MenuItem("BeneathTheFloor/Setup/Create World Drop Manager")]
        public static void CreateWorldDropManager()
        {
            if (Object.FindObjectOfType<WorldDropManager>() != null)
            {
                Debug.Log("[InventoryUISetup] WorldDropManager already exists in scene");
                return;
            }

            GameObject dropManagerObj = new GameObject("WorldDropManager");
            dropManagerObj.AddComponent<WorldDropManager>();
            Undo.RegisterCreatedObjectUndo(dropManagerObj, "Create World Drop Manager");

            Debug.Log("[InventoryUISetup] Created WorldDropManager");
            Selection.activeGameObject = dropManagerObj;
        }

        private static Canvas FindHUDCanvas()
        {
            // Try to find HUDCanvas
            var hudCanvas = GameObject.Find("HUDCanvas");
            if (hudCanvas != null)
            {
                return hudCanvas.GetComponent<Canvas>();
            }

            // Try any canvas with HUD in name
            foreach (var canvas in Object.FindObjectsOfType<Canvas>())
            {
                if (canvas.name.Contains("HUD") || canvas.name.Contains("UI"))
                {
                    return canvas;
                }
            }

            return Object.FindObjectOfType<Canvas>();
        }

        [MenuItem("BeneathTheFloor/Debug/Print Inventory Report")]
        public static void PrintInventoryReport()
        {
            var manager = Object.FindObjectOfType<InventoryUIManager>();
            if (manager != null)
            {
                var inventory = BeneathTheFloor.Inventory.InventorySystem.Instance;
                if (inventory != null)
                {
                    Debug.Log($"[InventoryReport] Slots: {manager.SlotCount}, MaxSlots: {inventory.MaxSlots}, IsOpen: {manager.IsOpen}");
                }
                else
                {
                    Debug.Log($"[InventoryReport] InventoryUIManager found, but no InventorySystem instance");
                }
            }
            else
            {
                Debug.LogWarning("[InventoryUISetup] No InventoryUIManager found in scene");
            }
        }

        [MenuItem("BeneathTheFloor/Debug/Add Test Items to Inventory")]
        public static void AddTestItems()
        {
            var inventory = BeneathTheFloor.Inventory.InventorySystem.Instance;
            if (inventory == null)
            {
                Debug.LogError("[InventoryUISetup] No InventorySystem instance found");
                return;
            }

            // Find some ItemSO assets to add
            string[] guids = AssetDatabase.FindAssets("t:ItemSO");
            int added = 0;

            foreach (string guid in guids)
            {
                if (added >= 5) break;

                string path = AssetDatabase.GUIDToAssetPath(guid);
                var item = AssetDatabase.LoadAssetAtPath<BeneathTheFloor.Crafting.ItemSO>(path);

                if (item != null)
                {
                    int amount = Random.Range(1, 10);
                    if (inventory.TryAddItemToGrid(item, amount))
                    {
                        Debug.Log($"[InventoryUISetup] Added {amount}x {item.itemName}");
                        added++;
                    }
                }
            }

            if (added == 0)
            {
                Debug.Log("[InventoryUISetup] No ItemSO assets found in project");
            }
            else
            {
                Debug.Log($"[InventoryUISetup] Added {added} test items to inventory");
            }
        }
    }
}
#endif
