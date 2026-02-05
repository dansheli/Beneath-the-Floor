using UnityEngine;
using System.Collections.Generic;
using System.IO;
using BeneathTheFloor.Crafting;
using BeneathTheFloor.Digging;
using BeneathTheFloor.ResourceSystem;
using BeneathTheFloor.World;

namespace BeneathTheFloor.Save
{
    /// <summary>
    /// Manages saving and loading of world pickups (ResourcePickup, NodePickup, WorldItem).
    /// These are the loose resources dropped from digging that haven't been collected yet.
    /// </summary>
    public static class WorldPickupSaveManager
    {
        private const string SAVE_FILE_NAME = "world_pickups.json";

        private static string SavePath => Path.Combine(Application.persistentDataPath, SAVE_FILE_NAME);

        /// <summary>
        /// Capture all world pickups and save to file.
        /// </summary>
        public static void SaveWorldPickups()
        {
            var saveData = new WorldPickupSaveData();

            // Find all ResourcePickup objects
            var resourcePickups = Object.FindObjectsOfType<ResourcePickup>();
            foreach (var pickup in resourcePickups)
            {
                if (pickup == null || pickup.gameObject == null) continue;

                saveData.resourcePickups.Add(new ResourcePickupData
                {
                    resourceType = (int)pickup.resourceType,
                    amount = pickup.amount,
                    posX = pickup.transform.position.x,
                    posY = pickup.transform.position.y,
                    posZ = pickup.transform.position.z
                });
            }

            // Find all NodePickup objects
            var nodePickups = Object.FindObjectsOfType<NodePickup>();
            foreach (var pickup in nodePickups)
            {
                if (pickup == null || pickup.gameObject == null) continue;

                saveData.nodePickups.Add(new NodePickupData
                {
                    tier = pickup.Tier,
                    resourceIndex = pickup.ResourceIndex,
                    resourceId = pickup.ResourceId,
                    amount = pickup.Amount,
                    creditValue = pickup.CreditValue,
                    posX = pickup.transform.position.x,
                    posY = pickup.transform.position.y,
                    posZ = pickup.transform.position.z
                });
            }

            // Find all WorldItem objects (dropped from inventory)
            var worldItems = Object.FindObjectsOfType<WorldItem>();
            foreach (var item in worldItems)
            {
                if (item == null || item.gameObject == null) continue;
                if (item.Item == null) continue;

                saveData.worldItems.Add(new WorldItemData
                {
                    itemId = item.Item.itemId,
                    amount = item.Amount,
                    posX = item.transform.position.x,
                    posY = item.transform.position.y,
                    posZ = item.transform.position.z
                });
            }

            // Write to file
            try
            {
                string json = JsonUtility.ToJson(saveData, true);
                File.WriteAllText(SavePath, json);
            }
            catch (System.Exception e)
            {
                Debug.LogError($"[WorldPickupSaveManager] Failed to save: {e.Message}");
            }
        }

        /// <summary>
        /// Load and restore world pickups from file.
        /// </summary>
        public static void LoadWorldPickups()
        {
            if (!File.Exists(SavePath))
            {
                return;
            }

            WorldPickupSaveData saveData;
            try
            {
                string json = File.ReadAllText(SavePath);
                saveData = JsonUtility.FromJson<WorldPickupSaveData>(json);
            }
            catch (System.Exception e)
            {
                Debug.LogError($"[WorldPickupSaveManager] Failed to load: {e.Message}");
                return;
            }

            if (saveData == null)
            {
                Debug.LogWarning("[WorldPickupSaveManager] Save data is null");
                return;
            }

            // Get ResourceSystemConfig for prefabs
            var resourceConfig = Resources.Load<ResourceSystemConfig>("ResourceSystemConfig");

            int restoredCount = 0;

            // Restore ResourcePickups
            foreach (var data in saveData.resourcePickups)
            {
                Vector3 position = new Vector3(data.posX, data.posY, data.posZ);
                UndergroundResourceType type = (UndergroundResourceType)data.resourceType;

                // Try to get prefab from config
                GameObject prefab = GetPickupPrefabForType(resourceConfig, type);
                if (prefab != null)
                {
                    GameObject pickup = Object.Instantiate(prefab, position, Quaternion.identity);
                    pickup.SetActive(true);

                    // Ensure collider exists for raycast detection
                    var collider = pickup.GetComponent<Collider>();
                    if (collider == null)
                    {
                        var sphereCol = pickup.AddComponent<SphereCollider>();
                        sphereCol.radius = 0.5f;
                    }

                    var resourcePickup = pickup.GetComponent<ResourcePickup>();
                    if (resourcePickup != null)
                    {
                        resourcePickup.resourceType = type;
                        resourcePickup.amount = data.amount;
                    }
                    else
                    {
                        // Add component if missing
                        resourcePickup = pickup.AddComponent<ResourcePickup>();
                        resourcePickup.resourceType = type;
                        resourcePickup.amount = data.amount;
                    }

                    restoredCount++;
                }
                else
                {
                    Debug.LogWarning($"[WorldPickupSaveManager] No prefab found for resource type: {type}");
                }
            }

            // Restore NodePickups
            foreach (var data in saveData.nodePickups)
            {
                Vector3 position = new Vector3(data.posX, data.posY, data.posZ);

                // Get prefab from ResourceSystemConfig
                GameObject prefab = GetNodePickupPrefab(resourceConfig, data.resourceIndex, data.resourceId);
                if (prefab != null)
                {
                    GameObject pickup = Object.Instantiate(prefab, position, Quaternion.identity);
                    pickup.SetActive(true);

                    // Set layer to Interactable (same as original spawn code)
                    int interactLayer = LayerMask.NameToLayer("Interactable");
                    if (interactLayer >= 0)
                        pickup.layer = interactLayer;

                    // Ensure collider is properly configured
                    var collider = pickup.GetComponent<Collider>();
                    if (collider == null)
                    {
                        var sphereCol = pickup.AddComponent<SphereCollider>();
                        sphereCol.radius = 0.08f;
                        collider = sphereCol;
                    }
                    collider.isTrigger = false;

                    // Ensure rigidbody exists (for physics, though it's resting now)
                    var rb = pickup.GetComponent<Rigidbody>();
                    if (rb == null)
                        rb = pickup.AddComponent<Rigidbody>();
                    rb.mass = 0.5f;
                    rb.drag = 2f;
                    rb.angularDrag = 2f;
                    rb.isKinematic = false;

                    // Add NodePickup component if not present
                    var nodePickup = pickup.GetComponent<NodePickup>();
                    if (nodePickup == null)
                    {
                        nodePickup = pickup.AddComponent<NodePickup>();
                    }
                    // Initialize(tier, amount, resourceIndex, resourceId, creditValue)
                    nodePickup.Initialize(data.tier, data.amount, data.resourceIndex, data.resourceId, data.creditValue);

                    // Add larger trigger collider for easier interaction detection
                    // This is CRITICAL for raycasts to hit the pickup
                    if (pickup.transform.Find("InteractionZone") == null)
                    {
                        GameObject interactionZone = new GameObject("InteractionZone");
                        interactionZone.transform.SetParent(pickup.transform);
                        interactionZone.transform.localPosition = Vector3.zero;
                        var interactionCollider = interactionZone.AddComponent<SphereCollider>();
                        interactionCollider.radius = 0.5f;
                        interactionCollider.isTrigger = true;

                        // Add forwarder to forward interactions to parent NodePickup
                        interactionZone.AddComponent<InteractionForwarder>();
                    }

                    restoredCount++;
                }
                else
                {
                    Debug.LogWarning($"[WorldPickupSaveManager] No prefab found for node pickup: {data.resourceId}");
                }
            }

            // Restore WorldItems
            var inventory = Inventory.InventorySystem.Instance;
            foreach (var data in saveData.worldItems)
            {
                Vector3 position = new Vector3(data.posX, data.posY, data.posZ);

                // Find the ItemSO
                ItemSO item = inventory?.FindItemById(data.itemId);
                if (item != null && WorldDropManager.Instance != null)
                {
                    WorldDropManager.Instance.SpawnItemAt(item, data.amount, position);
                    restoredCount++;
                }
                else
                {
                    Debug.LogWarning($"[WorldPickupSaveManager] Could not restore WorldItem: {data.itemId}");
                }
            }

        }

        /// <summary>
        /// Delete save file and clear all existing pickups from scene.
        /// </summary>
        public static void DeleteSave()
        {
            if (File.Exists(SavePath))
            {
                try
                {
                    File.Delete(SavePath);
                }
                catch (System.Exception e)
                {
                    Debug.LogError($"[WorldPickupSaveManager] Failed to delete: {e.Message}");
                }
            }
        }

        /// <summary>
        /// Clear all existing pickups from the scene (used when loading to prevent duplicates).
        /// </summary>
        public static void ClearExistingPickups()
        {
            // Clear ResourcePickups
            var resourcePickups = Object.FindObjectsOfType<ResourcePickup>();
            foreach (var pickup in resourcePickups)
            {
                if (pickup != null && pickup.gameObject != null)
                {
                    Object.Destroy(pickup.gameObject);
                }
            }

            // Clear NodePickups
            var nodePickups = Object.FindObjectsOfType<NodePickup>();
            foreach (var pickup in nodePickups)
            {
                if (pickup != null && pickup.gameObject != null)
                {
                    Object.Destroy(pickup.gameObject);
                }
            }

            // Clear WorldItems
            var worldItems = Object.FindObjectsOfType<WorldItem>();
            foreach (var item in worldItems)
            {
                if (item != null && item.gameObject != null)
                {
                    Object.Destroy(item.gameObject);
                }
            }
        }

        private static GameObject GetPickupPrefabForType(ResourceSystemConfig config, UndergroundResourceType type)
        {
            if (config == null) return null;

            // Map UndergroundResourceType to resourceId
            string resourceId = type.ToString().ToLower();

            foreach (var resource in config.resources)
            {
                if (resource == null) continue;

                // Check if this resource matches
                bool matches = resource.resourceId.ToLower() == resourceId ||
                              resource.displayName.ToLower().Contains(resourceId);

                if (matches && resource.tiers != null && resource.tiers.Length > 0)
                {
                    // Get first tier pickup prefab
                    foreach (var tier in resource.tiers)
                    {
                        if (tier != null && tier.pickupPrefab != null)
                        {
                            return tier.pickupPrefab;
                        }
                    }
                }
            }

            return null;
        }

        private static GameObject GetNodePickupPrefab(ResourceSystemConfig config, int resourceIndex, string resourceId)
        {
            if (config == null) return null;

            // Try to find by resourceId first
            if (!string.IsNullOrEmpty(resourceId))
            {
                foreach (var resource in config.resources)
                {
                    if (resource == null) continue;

                    if (resource.resourceId.ToLower() == resourceId.ToLower())
                    {
                        if (resource.tiers != null && resource.tiers.Length > 0)
                        {
                            foreach (var tier in resource.tiers)
                            {
                                if (tier != null && tier.pickupPrefab != null)
                                {
                                    return tier.pickupPrefab;
                                }
                            }
                        }
                    }
                }
            }

            // Fallback to index
            if (resourceIndex >= 0 && resourceIndex < config.resources.Count)
            {
                var resource = config.resources[resourceIndex];
                if (resource != null && resource.tiers != null && resource.tiers.Length > 0)
                {
                    foreach (var tier in resource.tiers)
                    {
                        if (tier != null && tier.pickupPrefab != null)
                        {
                            return tier.pickupPrefab;
                        }
                    }
                }
            }

            return null;
        }
    }

    /// <summary>
    /// Save data container for all world pickups.
    /// </summary>
    [System.Serializable]
    public class WorldPickupSaveData
    {
        public List<ResourcePickupData> resourcePickups = new List<ResourcePickupData>();
        public List<NodePickupData> nodePickups = new List<NodePickupData>();
        public List<WorldItemData> worldItems = new List<WorldItemData>();
    }

    [System.Serializable]
    public class ResourcePickupData
    {
        public int resourceType;
        public int amount;
        public float posX, posY, posZ;
    }

    [System.Serializable]
    public class NodePickupData
    {
        public int tier;
        public int resourceIndex;
        public string resourceId;
        public int amount;
        public int creditValue;
        public float posX, posY, posZ;
    }

    [System.Serializable]
    public class WorldItemData
    {
        public string itemId;
        public int amount;
        public float posX, posY, posZ;
    }
}
