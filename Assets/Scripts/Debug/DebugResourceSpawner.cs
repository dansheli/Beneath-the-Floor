using UnityEngine;
using BeneathTheFloor.Inventory;
using BeneathTheFloor.Crafting;
using BeneathTheFloor.ResourceSystem;
using BeneathTheFloor.Digging;

namespace BeneathTheFloor.DebugTools
{
    /// <summary>
    /// Debug tool to spawn resources in front of the player for testing.
    ///
    /// Controls:
    /// - F1: Spawn Stone
    /// - F2: Spawn Iron
    /// - F3: Spawn Coal
    /// - F4: Spawn Copper
    /// - F5: Spawn 5x of selected resource directly to inventory
    /// - [ / ]: Cycle through resources
    /// - +/-: Change spawn amount
    /// </summary>
    public class DebugResourceSpawner : MonoBehaviour
    {
        // Set to true to enable debug resource spawning (F1-F6)
        private const bool ENABLE_DEBUG = false;

        [Header("Spawn Settings")]
        [SerializeField] private float spawnDistance = 2f;
        [SerializeField] private float spawnHeight = 1f;
        [SerializeField] private int spawnAmount = 1;

        [Header("Resource Config")]
        [SerializeField] private ResourceSystemConfig resourceConfig;

        private string[] resourceIds = { "stone", "iron", "Coal", "Copper" };
        private int selectedResourceIndex = 0;
        private bool showUI = false;

        private void Start()
        {
#pragma warning disable CS0162 // Unreachable code detected
            if (!ENABLE_DEBUG) return;

            // Try to load resource config
            if (resourceConfig == null)
            {
                resourceConfig = Resources.Load<ResourceSystemConfig>("ResourceSystemConfig");
            }

            UnityEngine.Debug.Log("[DebugResourceSpawner] Active! Press F1-F4 to spawn resources, F5 to add to inventory, Tab to toggle UI");
#pragma warning restore CS0162
        }

        private void Update()
        {
#pragma warning disable CS0162 // Unreachable code detected
            if (!ENABLE_DEBUG) return;

            // Toggle UI
            if (Input.GetKeyDown(KeyCode.F6))
            {
                showUI = !showUI;
            }

            // Quick spawn keys
            if (Input.GetKeyDown(KeyCode.F1)) SpawnResourceInWorld("stone");
            if (Input.GetKeyDown(KeyCode.F2)) SpawnResourceInWorld("iron");
            if (Input.GetKeyDown(KeyCode.F3)) SpawnResourceInWorld("Coal");
            if (Input.GetKeyDown(KeyCode.F4)) SpawnResourceInWorld("Copper");

            // Add directly to inventory
            if (Input.GetKeyDown(KeyCode.F5))
            {
                AddResourceToInventory(resourceIds[selectedResourceIndex], spawnAmount);
            }

            // Cycle resources
            if (Input.GetKeyDown(KeyCode.LeftBracket))
            {
                selectedResourceIndex = (selectedResourceIndex - 1 + resourceIds.Length) % resourceIds.Length;
            }
            if (Input.GetKeyDown(KeyCode.RightBracket))
            {
                selectedResourceIndex = (selectedResourceIndex + 1) % resourceIds.Length;
            }

            // Change amount
            if (Input.GetKeyDown(KeyCode.Equals) || Input.GetKeyDown(KeyCode.KeypadPlus))
            {
                spawnAmount = Mathf.Min(spawnAmount + 1, 99);
            }
            if (Input.GetKeyDown(KeyCode.Minus) || Input.GetKeyDown(KeyCode.KeypadMinus))
            {
                spawnAmount = Mathf.Max(spawnAmount - 1, 1);
            }
#pragma warning restore CS0162
        }

        private void SpawnResourceInWorld(string resourceId)
        {
            // Get player position and forward
            Transform player = Camera.main?.transform;
            if (player == null)
            {
                var playerObj = GameObject.FindGameObjectWithTag("Player");
                if (playerObj != null) player = playerObj.transform;
            }

            if (player == null)
            {
                UnityEngine.Debug.LogWarning("[DebugResourceSpawner] No player/camera found!");
                return;
            }

            Vector3 spawnPos = player.position + player.forward * spawnDistance + Vector3.up * spawnHeight;

            // Find resource config
            if (resourceConfig == null)
            {
                UnityEngine.Debug.LogWarning("[DebugResourceSpawner] No ResourceSystemConfig found!");
                // Fallback: just add to inventory
                AddResourceToInventory(resourceId, spawnAmount);
                return;
            }

            // Find the resource definition
            ResourceDefinition resourceDef = null;
            int resourceIndex = -1;
            for (int i = 0; i < resourceConfig.resources.Count; i++)
            {
                if (resourceConfig.resources[i].resourceId.Equals(resourceId, System.StringComparison.OrdinalIgnoreCase))
                {
                    resourceDef = resourceConfig.resources[i];
                    resourceIndex = i;
                    break;
                }
            }

            if (resourceDef == null || resourceDef.tiers == null || resourceDef.tiers.Length == 0)
            {
                UnityEngine.Debug.LogWarning($"[DebugResourceSpawner] Resource '{resourceId}' not found in config!");
                // Fallback: add to inventory
                AddResourceToInventory(resourceId, spawnAmount);
                return;
            }

            // Spawn pickup prefab
            var tier = resourceDef.tiers[0]; // Use tier 1
            if (tier.pickupPrefab != null)
            {
                for (int i = 0; i < spawnAmount; i++)
                {
                    Vector3 offset = new Vector3(
                        Random.Range(-0.5f, 0.5f),
                        Random.Range(0f, 0.3f),
                        Random.Range(-0.5f, 0.5f)
                    );

                    GameObject pickup = Instantiate(tier.pickupPrefab, spawnPos + offset, Quaternion.identity);
                    pickup.name = $"Debug_{resourceId}_Pickup";

                    // Try to set up the pickup component
                    var nodePickup = pickup.GetComponent<NodePickup>();
                    if (nodePickup != null)
                    {
                        int creditValue = resourceDef.creditValuePerUnit;
                        nodePickup.Initialize(1, 1, resourceIndex, resourceId, creditValue);
                    }

                    // Add collider if not present (required for physics)
                    var existingCollider = pickup.GetComponent<Collider>();
                    if (existingCollider == null)
                    {
                        var sphereCol = pickup.AddComponent<SphereCollider>();
                        sphereCol.radius = 0.3f;
                        sphereCol.isTrigger = false;
                    }
                    else if (existingCollider.isTrigger)
                    {
                        // If existing collider is trigger, add a physics collider
                        var sphereCol = pickup.AddComponent<SphereCollider>();
                        sphereCol.radius = 0.3f;
                        sphereCol.isTrigger = false;
                    }

                    // Add rigidbody if not present for physics
                    var rb = pickup.GetComponent<Rigidbody>();
                    if (rb == null)
                    {
                        rb = pickup.AddComponent<Rigidbody>();
                    }
                    rb.mass = 0.5f;
                    rb.useGravity = true;
                    rb.isKinematic = false;
                    rb.collisionDetectionMode = CollisionDetectionMode.Continuous;
                    rb.AddForce(Vector3.up * 2f + Random.insideUnitSphere, ForceMode.Impulse);

                    // Add ResourcePickup component for E key collection
                    var resourcePickup = pickup.GetComponent<ResourcePickup>();
                    if (resourcePickup == null)
                    {
                        resourcePickup = pickup.AddComponent<ResourcePickup>();
                    }
                    resourcePickup.amount = 1;
                    resourcePickup.resourceType = MapResourceIdToType(resourceId);
                    resourcePickup.destroyOnPickup = true;
                }

                UnityEngine.Debug.Log($"[DebugResourceSpawner] Spawned {spawnAmount}x {resourceId} at {spawnPos}");
            }
            else
            {
                UnityEngine.Debug.LogWarning($"[DebugResourceSpawner] No pickup prefab for {resourceId}!");
                AddResourceToInventory(resourceId, spawnAmount);
            }
        }

        private void AddResourceToInventory(string resourceId, int amount)
        {
            if (InventorySystem.Instance == null)
            {
                UnityEngine.Debug.LogWarning("[DebugResourceSpawner] No InventorySystem found!");
                return;
            }

            // Try to find existing item in inventory or create one
            ItemSO item = FindOrCreateItem(resourceId);
            if (item != null)
            {
                bool added = InventorySystem.Instance.TryAddItemToGrid(item, amount);
                if (added)
                {
                    UnityEngine.Debug.Log($"[DebugResourceSpawner] Added {amount}x {resourceId} to inventory");
                }
                else
                {
                    UnityEngine.Debug.LogWarning($"[DebugResourceSpawner] Failed to add {resourceId} - inventory full?");
                }
            }
        }

        private ItemSO FindOrCreateItem(string resourceId)
        {
            // First try to load from Resources/Items
            string[] searchPaths = {
                $"Items/{resourceId}Piece_Item",
                $"Items/{resourceId}_Item",
                $"Items/{resourceId}",
                $"GeneratedResources/{resourceId}Piece_Item",
            };

            foreach (var path in searchPaths)
            {
                var item = Resources.Load<ItemSO>(path);
                if (item != null) return item;
            }

            // Try to find in loaded assets
            var allItems = Resources.FindObjectsOfTypeAll<ItemSO>();
            foreach (var item in allItems)
            {
                if (item.itemId != null && item.itemId.ToLower().Contains(resourceId.ToLower()))
                {
                    return item;
                }
                if (item.itemName != null && item.itemName.ToLower().Contains(resourceId.ToLower()))
                {
                    return item;
                }
            }

            // Create runtime item as fallback
            var runtimeItem = ScriptableObject.CreateInstance<MaterialItemSO>();
            runtimeItem.itemId = $"debug_{resourceId.ToLower()}";
            runtimeItem.itemName = $"{resourceId} (Debug)";
            runtimeItem.description = "Debug spawned resource";
            runtimeItem.category = ItemCategory.Material;
            runtimeItem.isStackable = true;
            runtimeItem.maxStackSize = 999;
            runtimeItem.sellPrice = 5;

            // Try to get icon from resource config
            if (resourceConfig != null)
            {
                foreach (var res in resourceConfig.resources)
                {
                    if (res.resourceId.Equals(resourceId, System.StringComparison.OrdinalIgnoreCase))
                    {
                        runtimeItem.icon = res.icon;
                        break;
                    }
                }
            }

            return runtimeItem;
        }

        private void OnGUI()
        {
            if (!ENABLE_DEBUG || !showUI) return;

            GUILayout.BeginArea(new Rect(10, 10, 300, 200));
            GUILayout.BeginVertical("box");

            GUILayout.Label("<b>Debug Resource Spawner</b>");
            GUILayout.Label($"Selected: {resourceIds[selectedResourceIndex]}");
            GUILayout.Label($"Amount: {spawnAmount}");
            GUILayout.Space(10);

            GUILayout.Label("Controls:");
            GUILayout.Label("F1-F4: Spawn Stone/Iron/Coal/Copper");
            GUILayout.Label("F5: Add selected to inventory");
            GUILayout.Label("[ ]: Cycle resource");
            GUILayout.Label("+/-: Change amount");
            GUILayout.Label("F6: Toggle this UI");

            GUILayout.Space(10);

            // Quick buttons
            GUILayout.BeginHorizontal();
            if (GUILayout.Button("Stone")) SpawnResourceInWorld("stone");
            if (GUILayout.Button("Iron")) SpawnResourceInWorld("iron");
            if (GUILayout.Button("Coal")) SpawnResourceInWorld("Coal");
            if (GUILayout.Button("Copper")) SpawnResourceInWorld("Copper");
            GUILayout.EndHorizontal();

            if (GUILayout.Button($"Add {spawnAmount}x {resourceIds[selectedResourceIndex]} to Inventory"))
            {
                AddResourceToInventory(resourceIds[selectedResourceIndex], spawnAmount);
            }

            GUILayout.EndVertical();
            GUILayout.EndArea();
        }

        /// <summary>
        /// Map resource ID string to UndergroundResourceType enum.
        /// </summary>
        private UndergroundResourceType MapResourceIdToType(string resourceId)
        {
            switch (resourceId.ToLower())
            {
                case "stone":
                    return UndergroundResourceType.Stone;
                case "iron":
                    return UndergroundResourceType.IronChunk;
                case "coal":
                    return UndergroundResourceType.Coal;
                case "copper":
                    return UndergroundResourceType.CopperPiece;
                case "dirt":
                    return UndergroundResourceType.Dirt;
                case "clay":
                    return UndergroundResourceType.Clay;
                case "quartz":
                    return UndergroundResourceType.Quartz;
                case "crystal":
                    return UndergroundResourceType.CrystalShard;
                default:
                    return UndergroundResourceType.Stone;
            }
        }
    }
}
